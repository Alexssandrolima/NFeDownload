﻿using NFeDownload.Download;
using NFeDownload.NFe.Serialize;
using NFeDownload.NFe.Serialize.TiposBasicos;
using NFeDownload.NFe.Util;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Xml.Serialization;

namespace NFeDownload.NFe
{
    public class NFeGenerator
    {
        public NFeGenerator()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR"); ;
        }

        public void Generate(DownloadedHtmlData downloadedData, string directory)
        {
            var nota = new TNfeProc();

            UpdateDadosNfe(downloadedData.ChaveAcessso,
                            nota,
                            downloadedData.DadosNfe,
                            downloadedData.DadosEmitente,
                            downloadedData.InformacoesAdicionais);

            UpdateDadosEmitente(nota, downloadedData.DadosEmitente);
            UpdateDadosDestinatario(nota, downloadedData.DadosDestinatario);
            UpdateProdutos(nota, downloadedData.Products);
            UpdateTotais(nota, downloadedData.Totais);
            UpdateTransporte(nota, downloadedData.DadosTransporte);
            UpdateAdicionais(nota, downloadedData.InformacoesAdicionais);

            SaveXml(nota.Serialize(), directory);
        }

        private XmlDocument SaveXml(string xml, string fileName)
        {
            XmlTextWriter xtw = null;
            try
            {
                var dInfo = Directory.GetParent(fileName);
                if (!dInfo.Exists)
                {
                    dInfo.Create();
                }
                xtw = new XmlTextWriter(fileName, Encoding.UTF8);
                var xd = new XmlDocument();

                xml = xml.Replace("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"", "");
                xml = xml.Replace("xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"", "");

                xd.LoadXml(xml);
                xd.Save(xtw);
                return xd;
            }
            finally
            {
                if ((xtw != null))
                {
                    xtw.Flush();
                    xtw.Close();
                }
            }
        }

        private void UpdateDadosNfe(string chave,
                                    TNfeProc nota,
                                    IList<PostResultItem> dadosNfe,
                                    IList<PostResultItem> dadosEmitente,
                                    IList<PostResultItem> dadosAdicionais)
        {

            var chaveNfe = chave.Replace("-", string.Empty).Replace(".", string.Empty).Replace("/", string.Empty);

            var versaoNfe = "2.00";
            var cNF = chaveNfe.Substring(35, 8);
            var cDV = chave.Split(new[] { "-" }, StringSplitOptions.None)[8];
            var indPag = int.Parse(dadosNfe.Where(d => d.AttributeName == "Forma de Pagamento").FirstOrDefault().AttributeValue.Split(new[] { "-" }, StringSplitOptions.None)[0].Trim());
            var formaImprPostResult = dadosAdicionais.Where(d => d.AttributeName == "Formato de Impressão DANFE").FirstOrDefault();
            var municipioEmitente = dadosEmitente.Where(d => d.AttributeName == "Município").FirstOrDefault().AttributeValue.Split(new[] { "-" }, StringSplitOptions.None)[0].Trim();
            var tipoEmissaoPostResult = dadosNfe.Where(d => d.AttributeName == "Tipo de Emissão").FirstOrDefault();
            var finalidadePostResult = dadosNfe.Where(d => d.AttributeName == "Finalidade").FirstOrDefault();
            var procEmiPostResult = dadosNfe.Where(d => d.AttributeName == "Processo").FirstOrDefault();

            nota.NFe = new TNFe();
            nota.versao = versaoNfe;

            nota.NFe.infNFe = new TNFeInfNFe();
            nota.NFe.infNFe.versao = versaoNfe;
            nota.NFe.infNFe.Id = "NFe" + chaveNfe;

            var dataEmissaoText = dadosNfe.Where(d => d.AttributeName == "Data de Emissão").FirstOrDefault().AttributeValue;
            var dataEmissao = DateTime.Parse(dataEmissaoText);

            var dataHoraSaidaText = dadosNfe.Where(d => d.AttributeName == "Data/Hora  Saída/Entrada").FirstOrDefault();
            var dataHoraSaida = dataHoraSaidaText == null ? (DateTime?)null : DateTime.Parse(dataHoraSaidaText.AttributeValue.Replace("às", string.Empty).Replace("\r\n", string.Empty));

            nota.NFe.infNFe.ide = new TNFeInfNFeIde();
            nota.NFe.infNFe.ide.cUF = GetUF(dadosNfe.Where(a => a.AttributeName == "UF").FirstOrDefault().AttributeValue);
            nota.NFe.infNFe.ide.cNF = cNF;
            nota.NFe.infNFe.ide.natOp = dadosNfe.Where(d => d.AttributeName == "Natureza da Operação").FirstOrDefault().AttributeValue;
            nota.NFe.infNFe.ide.indPag = GetIndPag(indPag);
            nota.NFe.infNFe.ide.mod = TMod.NotaFiscalEletronica;
            nota.NFe.infNFe.ide.serie = dadosNfe.Where(d => d.AttributeName == "Série").FirstOrDefault().AttributeValue;
            nota.NFe.infNFe.ide.nNF = dadosNfe.Where(d => d.AttributeName == "Número").FirstOrDefault().AttributeValue;
            nota.NFe.infNFe.ide.dEmi = dataEmissao.ToString("yyyy-MM-dd");
            nota.NFe.infNFe.ide.dSaiEnt = dataHoraSaida == null ? string.Empty : dataHoraSaida.Value.ToString("yyyy-MM-dd");
            nota.NFe.infNFe.ide.hSaiEnt = dataHoraSaida == null ? string.Empty : dataHoraSaida.Value.ToString("hh:mm:ss");
            nota.NFe.infNFe.ide.tpNF = GetTpNF(dadosNfe.Where(d => d.AttributeName == "Tipo da Operação").FirstOrDefault().AttributeValue);
            nota.NFe.infNFe.ide.cMunFG = municipioEmitente;
            nota.NFe.infNFe.ide.tpImp = GetTpImp(formaImprPostResult.AttributeValue.Contains("retrato") ? 1 : 2);
            nota.NFe.infNFe.ide.tpEmis = GetTpEmis(int.Parse(tipoEmissaoPostResult.AttributeValue.Split(new[] { "-" }, StringSplitOptions.None)[0].Trim()));
            nota.NFe.infNFe.ide.cDV = cDV;
            nota.NFe.infNFe.ide.tpAmb = GetTpAmb(dadosNfe.Where(d => d.Legend.Contains("produção")).Any() ? 1 : 2);
            nota.NFe.infNFe.ide.finNFe = GetFinNFe(int.Parse(finalidadePostResult.AttributeValue.Split(new[] { "-" }, StringSplitOptions.None)[0].Trim()));
            nota.NFe.infNFe.ide.procEmi = GetProcEmi(int.Parse(procEmiPostResult.AttributeValue.Split(new[] { "-" }, StringSplitOptions.None)[0].Trim()));
            nota.NFe.infNFe.ide.verProc = dadosNfe.Where(d => d.AttributeName.Contains("Versão do Processo")).FirstOrDefault().AttributeValue.Trim();
        }

        private void UpdateDadosEmitente(TNfeProc nota, IList<PostResultItem> dadosEmitente)
        {
            nota.NFe.infNFe.emit = new TNFeInfNFeEmit();
            nota.NFe.infNFe.emit.ItemElementName = TipoDocumentoEmitente.CNPJ;
            nota.NFe.infNFe.emit.Item = GetValue(dadosEmitente, "CNPJ")
                .Replace(".", string.Empty)
                .Replace("/", string.Empty)
                .Replace("-", string.Empty).Trim();
            nota.NFe.infNFe.emit.xNome = GetValue(dadosEmitente, "Nome / Razão Social");
            nota.NFe.infNFe.emit.xFant = GetValue(dadosEmitente, "Nome Fantasia");
            nota.NFe.infNFe.emit.IE = GetValue(dadosEmitente, "Inscrição Estadual");
            nota.NFe.infNFe.emit.IEST = GetValue(dadosEmitente, "Inscrição Estadual do Substituto Tributário");
            nota.NFe.infNFe.emit.IM = GetValue(dadosEmitente, "Inscrição Municipal");
            nota.NFe.infNFe.emit.CNAE = GetValue(dadosEmitente, "CNAE Fiscal");
            nota.NFe.infNFe.emit.CRT = GetCrtEmit(int.Parse(GetValue(dadosEmitente, "Código de Regime Tributário").Split(new[] { "-" }, StringSplitOptions.None)[0].Trim()));

            nota.NFe.infNFe.emit.enderEmit = new TEnderEmi();
            nota.NFe.infNFe.emit.enderEmit.xLgr = GetValue(dadosEmitente, "Endereço");
            nota.NFe.infNFe.emit.enderEmit.nro = string.Empty; //Número endereço.
            nota.NFe.infNFe.emit.enderEmit.xCpl = string.Empty; //Complemento.
            nota.NFe.infNFe.emit.enderEmit.xBairro = GetValue(dadosEmitente, "Bairro / Distrito");
            nota.NFe.infNFe.emit.enderEmit.cMun = GetValue(dadosEmitente, "Município").Split(new[] { "-" }, StringSplitOptions.None)[0].Trim();
            nota.NFe.infNFe.emit.enderEmit.xMun = GetValue(dadosEmitente, "Município").Split(new[] { "-" }, StringSplitOptions.None)[1].Trim(); ;
            nota.NFe.infNFe.emit.enderEmit.UF = GetUFEmi(GetValue(dadosEmitente, "UF"));
            nota.NFe.infNFe.emit.enderEmit.CEP = GetValue(dadosEmitente, "CEP").Replace("-", string.Empty);
            nota.NFe.infNFe.emit.enderEmit.cPais = TEnderEmiCPais.Brasil;
            nota.NFe.infNFe.emit.enderEmit.xPais = TEnderEmiXPais.BRASIL;
            nota.NFe.infNFe.emit.enderEmit.fone = GetValue(dadosEmitente, "Telefone")
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("-", string.Empty);
        }

        private void UpdateDadosDestinatario(TNfeProc nota, IList<PostResultItem> dadosDestinatario)
        {
            nota.NFe.infNFe.dest = new TNFeInfNFeDest();
            nota.NFe.infNFe.dest.ItemElementName = TipoDocumentoDest.CNPJ;
            nota.NFe.infNFe.dest.Item = GetValue(dadosDestinatario, "CNPJ")
                .Replace(".", string.Empty)
                .Replace("/", string.Empty)
                .Replace("-", string.Empty).Trim();
            nota.NFe.infNFe.dest.xNome = GetValue(dadosDestinatario, "Nome / Razão Social");
            nota.NFe.infNFe.dest.IE = GetValue(dadosDestinatario, "Inscrição Estadual");

            nota.NFe.infNFe.dest.enderDest = new TEndereco();
            nota.NFe.infNFe.dest.enderDest.xLgr = GetValue(dadosDestinatario, "Endereço");
            nota.NFe.infNFe.dest.enderDest.nro = string.Empty; //Número endereço.
            nota.NFe.infNFe.dest.enderDest.xCpl = string.Empty; //Complemento.
            nota.NFe.infNFe.dest.enderDest.xBairro = GetValue(dadosDestinatario, "Bairro / Distrito");
            nota.NFe.infNFe.dest.enderDest.cMun = GetValue(dadosDestinatario, "Município").Split(new[] { "-" }, StringSplitOptions.None)[0].Trim();
            nota.NFe.infNFe.dest.enderDest.xMun = GetValue(dadosDestinatario, "Município").Split(new[] { "-" }, StringSplitOptions.None)[1].Trim(); ;
            nota.NFe.infNFe.dest.enderDest.UF = GetTUF(GetValue(dadosDestinatario, "UF"));
            nota.NFe.infNFe.dest.enderDest.CEP = GetValue(dadosDestinatario, "CEP").Replace("-", string.Empty);
            nota.NFe.infNFe.dest.enderDest.cPais = Tpais.Item1058;
            nota.NFe.infNFe.dest.enderDest.xPais = "BRASIL";
            nota.NFe.infNFe.dest.enderDest.cPaisSpecified = true;
            nota.NFe.infNFe.dest.enderDest.fone = GetValue(dadosDestinatario, "Telefone")
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("-", string.Empty);
        }

        private void UpdateProdutos(TNfeProc nota, IList<Produto> produtos)
        {
            var itensNfe = new List<TNFeInfNFeDet>();            
            foreach (var produto in produtos)
            {
                var det = new TNFeInfNFeDet();
                det.nItem = produto.Num;
                det.prod = new TNFeInfNFeDetProd();
                det.prod.cProd = produto.CodigoProduto;
                det.prod.cEAN = produto.CodigoEANComercial;
                det.prod.xProd = produto.Descricao;
                det.prod.NCM = produto.CodigoNCM;
                det.prod.CFOP = GetProdCfop(produto.CFOP);
                det.prod.uCom = produto.UnidadeComercial;
                det.prod.qCom = produto.QuantidadeComercial.Replace(",", ".");
                det.prod.vUnCom = produto.ValorUnitarioComercializacao.Replace(",",".");
                det.prod.vProd = produto.Valor.Replace(",", ".");
                det.prod.cEANTrib = produto.CodigoEANTributavel;
                det.prod.uTrib = produto.UnidadeTributavel;
                det.prod.qTrib = produto.QuantidadeTributavel.Replace(",", "."); 
                det.prod.vUnTrib = produto.ValorUnitarioTributacao.Replace(",", ".");
                det.prod.indTot = int.Parse(produto.IndicadorComposicaoValorTotalNFe.Split(new [] {"-"}, StringSplitOptions.None)[0]) == 1 ? TNFeInfNFeDetProdIndTot.CompoeValorNota : TNFeInfNFeDetProdIndTot.NaoCompoeValorNota;
                det.imposto = new TNFeInfNFeDetImposto();

                var tiposImposto = new List<object>();

                //############################ ICMS ############################
                var tributacaoICMS = produto.TributacaoICMS.Split(new [] {"-"}, StringSplitOptions.None)[0].Trim();
                var icms = new TNFeInfNFeDetImpostoICMS();                

                object detIcms;
                switch (tributacaoICMS)
                {
                    case "00":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMS00();
                        break;
                    case "10":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMS10();
                        break;
                    case "20":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMS20();
                        break;
                    case "30":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMS30();
                        break;
                    case "40":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMS40();
                        //((TNFeInfNFeDetImpostoICMSICMS40)detIcms).orig =  Torig. produto.OrigemMercadoria;
                        break;
                    case "51":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMS51();
                        break;
                    case "60":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMS60();
                        break;
                    case "70":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMS70();
                        break;
                    case "90":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMS90();
                        break;
                    case "Part":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMSPart();
                        break;
                    case "101":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMSSN101();
                        break;
                    case "102":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMSSN102();
                        break;
                    case "201":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMSSN201();
                        break;
                    case "202":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMSSN202();
                        break;
                    case "500":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMSSN500();
                        break;
                    case "900":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMSSN900();
                        break;
                    case "ST":
                        detIcms = new TNFeInfNFeDetImpostoICMSICMSST();
                        break;
                    default:
                        detIcms = new TNFeInfNFeDetImpostoICMSICMS40();
                        break;
                }
                icms.Item = detIcms;                
                tiposImposto.Add(icms);
                det.imposto.Items = tiposImposto.ToArray();        
                //############################ PIS ############################
                var pis = new TNFeInfNFeDetImpostoPIS();
                var pisCST = new TNFeInfNFeDetImpostoPISPISNT();
                var pCst = produto.PIS_CST.Split(new[] { "-" }, StringSplitOptions.None)[0].Trim();
                switch (pCst)
                {
                    case "04":
                        pisCST.CST = TNFeInfNFeDetImpostoPISPISNTCST.Item04;
                        break;
                    case "06":
                        pisCST.CST = TNFeInfNFeDetImpostoPISPISNTCST.Item06;
                        break;
                    case "07":
                        pisCST.CST = TNFeInfNFeDetImpostoPISPISNTCST.Item07;
                        break;
                    case "08":
                        pisCST.CST = TNFeInfNFeDetImpostoPISPISNTCST.Item08;
                        break;
                    case "09":
                        pisCST.CST = TNFeInfNFeDetImpostoPISPISNTCST.Item09;
                        break;
                    default:
                        break;
                }
                pis.Item = pisCST;
                det.imposto.PIS = pis;
                //############################ COFINS ############################
                det.imposto.COFINS = new TNFeInfNFeDetImpostoCOFINS();
                var confinsNT = new TNFeInfNFeDetImpostoCOFINSCOFINSNT();
                var cofinsCST = produto.COFINS_CST.Split(new[] { "-" }, StringSplitOptions.None)[0].Trim();
                switch (cofinsCST)
                {
                    case "04":
                        confinsNT.CST = TNFeInfNFeDetImpostoCOFINSCOFINSNTCST.Item04;
                        break;
                    case "06":
                        confinsNT.CST = TNFeInfNFeDetImpostoCOFINSCOFINSNTCST.Item06;
                        break;
                    case "07":
                        confinsNT.CST = TNFeInfNFeDetImpostoCOFINSCOFINSNTCST.Item07;
                        break;
                    case "08":
                        confinsNT.CST = TNFeInfNFeDetImpostoCOFINSCOFINSNTCST.Item08;
                        break;
                    case "09":
                        confinsNT.CST = TNFeInfNFeDetImpostoCOFINSCOFINSNTCST.Item09;
                        break;
                }
                det.imposto.COFINS.Item = confinsNT;              
                itensNfe.Add(det);                
            }
            nota.NFe.infNFe.det = itensNfe.ToArray();
        }

        private void UpdateTotais(TNfeProc nota, IList<PostResultItem> totais)
        {
            nota.NFe.infNFe.total = new TNFeInfNFeTotal();
            nota.NFe.infNFe.total.ICMSTot = new TNFeInfNFeTotalICMSTot();
            nota.NFe.infNFe.total.ICMSTot.vBC = GetValue(totais, "Base de Cálculo ICMS").Replace(",",".");
            nota.NFe.infNFe.total.ICMSTot.vICMS = GetValue(totais, "Valor do ICMS").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vBCST = GetValue(totais, "Base de Cálculo ICMS ST").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vST = GetValue(totais, "Valor ICMS Substituição").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vProd = GetValue(totais, "Valor Total dos Produtos").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vFrete = GetValue(totais, "Valor do Frete").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vSeg = GetValue(totais, "Valor do Seguro").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vDesc = GetValue(totais, "Valor Total dos Descontos").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vII = GetValue(totais, "Valor Total do II").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vIPI = GetValue(totais, "Valor Total do IPI").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vPIS = GetValue(totais, "Valor do PIS").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vCOFINS = GetValue(totais, "Valor da COFINS").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vOutro = GetValue(totais, "Outras Despesas Acessórias").Replace(",", ".");
            nota.NFe.infNFe.total.ICMSTot.vNF = GetValue(totais, "Valor Total da NFe").Replace(",", ".");
            nota.NFe.infNFe.total.ISSQNtot = new TNFeInfNFeTotalISSQNtot();
        }

        private void UpdateTransporte(TNfeProc nota, IList<PostResultItem> itensTransporte)
        {
            nota.NFe.infNFe.transp = new TNFeInfNFeTransp();
            var modFrete = GetValue(itensTransporte, "Modalidade do Frete").Split(new [] {"-"}, StringSplitOptions.None)[0].Trim();
            switch (modFrete)
            {
                case "0":
                    nota.NFe.infNFe.transp.modFrete = TNFeInfNFeTranspModFrete.PorContaEmitente;
                    break;
                case "1":
                    nota.NFe.infNFe.transp.modFrete = TNFeInfNFeTranspModFrete.PorContaDestinatario;
                    break;
                case "2":
                    nota.NFe.infNFe.transp.modFrete = TNFeInfNFeTranspModFrete.PorContaTerceiros;
                    break;
                case "9":
                    nota.NFe.infNFe.transp.modFrete = TNFeInfNFeTranspModFrete.SemFrete;
                    break;
            }
            nota.NFe.infNFe.transp.vol = null;
        }

        private void UpdateAdicionais(TNfeProc nota, IList<PostResultItem> adicionais)
        {
            nota.NFe.infNFe.infAdic = new TNFeInfNFeInfAdic();
            nota.NFe.infNFe.infAdic.infCpl = GetValue(adicionais, "Descrição").Trim();
        }

        private TCfop GetProdCfop(string cfop)
        {
            TCfop result = TCfop.CFOP1101;
            var enumValues = Enum.GetValues(typeof(TCfop));
            foreach (var enumVal in enumValues)
            {
                var actualEnum = (TCfop)enumVal;
                var descEnum = actualEnum.ToString2();
                if (descEnum.Equals(cfop, StringComparison.OrdinalIgnoreCase))
                    result = actualEnum;
            }
            return result;
        }

        private string GetValue(IList<PostResultItem> collection, string propertyName)
        {
            var postResult = collection.Where(d => d.AttributeName == propertyName).FirstOrDefault();
            return postResult == null ? string.Empty : postResult.AttributeValue;
        }

        private TCodUfIBGE GetUF(string uf)
        {
            var result = TCodUfIBGE.SaoPaulo;
            switch (uf.ToUpper())
            {
                case "AC":
                    result = TCodUfIBGE.Acre;
                    break;
                case "AL":
                    result = TCodUfIBGE.Alagoas;
                    break;
                case "AP":
                    result = TCodUfIBGE.Amapa;
                    break;
                case "AM":
                    result = TCodUfIBGE.Amazonas;
                    break;
                case "BA":
                    result = TCodUfIBGE.Bahia;
                    break;
                case "CE":
                    result = TCodUfIBGE.Ceara;
                    break;
                case "DF":
                    result = TCodUfIBGE.DistritoFederal;
                    break;
                case "ES":
                    result = TCodUfIBGE.EspiritoSanto;
                    break;
                case "GO":
                    result = TCodUfIBGE.Goias;
                    break;
                case "MA":
                    result = TCodUfIBGE.Maranhao;
                    break;
                case "MT":
                    result = TCodUfIBGE.MatoGrosso;
                    break;
                case "MS":
                    result = TCodUfIBGE.MatoGrossoDoSul;
                    break;
                case "MG":
                    result = TCodUfIBGE.MinasGerais;
                    break;
                case "PA":
                    result = TCodUfIBGE.Para;
                    break;
                case "PB":
                    result = TCodUfIBGE.Paraiba;
                    break;
                case "PR":
                    result = TCodUfIBGE.Parana;
                    break;
                case "PE":
                    result = TCodUfIBGE.Pernambuco;
                    break;
                case "PI":
                    result = TCodUfIBGE.Piaui;
                    break;
                case "RJ":
                    result = TCodUfIBGE.RioDeJaneiro;
                    break;
                case "RN":
                    result = TCodUfIBGE.RioGrandeDoNorte;
                    break;
                case "RS":
                    result = TCodUfIBGE.RioGrandeDoSul;
                    break;
                case "RO":
                    result = TCodUfIBGE.Rondonia;
                    break;
                case "RR":
                    result = TCodUfIBGE.Roraima;
                    break;
                case "SC":
                    result = TCodUfIBGE.SantaCatarina;
                    break;
                case "SP":
                    result = TCodUfIBGE.SaoPaulo;
                    break;
                case "SE":
                    result = TCodUfIBGE.Sergipe;
                    break;
                case "TO":
                    result = TCodUfIBGE.Tocantis;
                    break;
            }
            return result;
        }

        private TNFeInfNFeIdeIndPag GetIndPag(int indPag)
        {
            var result = TNFeInfNFeIdeIndPag.aVista;

            switch (indPag)
            {
                case 0:
                    result = TNFeInfNFeIdeIndPag.aVista;
                    break;
                case 1:
                    result = TNFeInfNFeIdeIndPag.aPrazo;
                    break;
                case 2:
                    result = TNFeInfNFeIdeIndPag.Outros;
                    break;
            }

            return result;
        }

        private TNFeInfNFeIdeTpNF GetTpNF(string tpNF)
        {
            var result = TNFeInfNFeIdeTpNF.Saida;

            switch (tpNF.Trim())
            {
                case "0 - Entrada":
                    result = TNFeInfNFeIdeTpNF.Entrada;
                    break;
                case "1 - Saída":
                    result = TNFeInfNFeIdeTpNF.Saida;
                    break;
            }

            return result;
        }

        private TNFeInfNFeIdeTpImp GetTpImp(int tpImp)
        {
            var result = TNFeInfNFeIdeTpImp.Retrato;

            switch (tpImp)
            {
                case 1:
                    result = TNFeInfNFeIdeTpImp.Retrato;
                    break;
                case 2:
                    result = TNFeInfNFeIdeTpImp.Paisagem;
                    break;
            }

            return result;
        }

        private TNFeInfNFeIdeTpEmis GetTpEmis(int tpEmis)
        {
            var result = TNFeInfNFeIdeTpEmis.ContigenciaDPEC;

            switch (tpEmis)
            {
                case 1:
                    result = TNFeInfNFeIdeTpEmis.Normal;
                    break;
                case 2:
                    result = TNFeInfNFeIdeTpEmis.ContigenciaFS;
                    break;
                case 3:
                    result = TNFeInfNFeIdeTpEmis.ContigenciaSCAN;
                    break;
                case 4:
                    result = TNFeInfNFeIdeTpEmis.ContigenciaDPEC;
                    break;
                case 5:
                    result = TNFeInfNFeIdeTpEmis.ContigenciaFSDA;
                    break;
                case 6:
                    result = TNFeInfNFeIdeTpEmis.ContigenciaSVCAN;
                    break;
                case 7:
                    result = TNFeInfNFeIdeTpEmis.ContigenciaSVCRS;
                    break;
            }

            return result;
        }

        private TAmb GetTpAmb(int tpAmb)
        {
            var result = TAmb.Producao;
            switch (tpAmb)
            {
                case 1:
                    result = TAmb.Producao;
                    break;
                case 2:
                    result = TAmb.Homologacao;
                    break;
            }
            return result;
        }

        private TFinNFe GetFinNFe(int finNfe)
        {
            var result = TFinNFe.Normal;
            switch (finNfe)
            {
                case 1:
                    result = TFinNFe.Normal;
                    break;
                case 2:
                    result = TFinNFe.Complementar;
                    break;
                case 3:
                    result = TFinNFe.Ajuste;
                    break;
            }
            return result;
        }

        private TProcEmi GetProcEmi(int procEmi)
        {
            var result = TProcEmi.Item0;

            switch (procEmi)
            {
                case 0:
                    result = TProcEmi.Item0;
                    break;
                case 1:
                    result = TProcEmi.Item1;
                    break;
                case 2:
                    result = TProcEmi.Item2;
                    break;
                case 3:
                    result = TProcEmi.Item3;
                    break;
            }

            return result;
        }

        private TUfEmi GetUFEmi(string uf)
        {
            var result = TUfEmi.SP;
            switch (uf.ToUpper())
            {
                case "AC":
                    result = TUfEmi.AC;
                    break;
                case "AL":
                    result = TUfEmi.AL;
                    break;
                case "AP":
                    result = TUfEmi.AP;
                    break;
                case "AM":
                    result = TUfEmi.AM;
                    break;
                case "BA":
                    result = TUfEmi.BA;
                    break;
                case "CE":
                    result = TUfEmi.CE;
                    break;
                case "DF":
                    result = TUfEmi.DF;
                    break;
                case "ES":
                    result = TUfEmi.ES;
                    break;
                case "GO":
                    result = TUfEmi.GO;
                    break;
                case "MA":
                    result = TUfEmi.MA;
                    break;
                case "MT":
                    result = TUfEmi.MT;
                    break;
                case "MS":
                    result = TUfEmi.MS;
                    break;
                case "MG":
                    result = TUfEmi.MG;
                    break;
                case "PA":
                    result = TUfEmi.PA;
                    break;
                case "PB":
                    result = TUfEmi.PB;
                    break;
                case "PR":
                    result = TUfEmi.PR;
                    break;
                case "PE":
                    result = TUfEmi.PE;
                    break;
                case "PI":
                    result = TUfEmi.PI;
                    break;
                case "RJ":
                    result = TUfEmi.RJ;
                    break;
                case "RN":
                    result = TUfEmi.RN;
                    break;
                case "RS":
                    result = TUfEmi.RS;
                    break;
                case "RO":
                    result = TUfEmi.RO;
                    break;
                case "RR":
                    result = TUfEmi.RR;
                    break;
                case "SC":
                    result = TUfEmi.SC;
                    break;
                case "SP":
                    result = TUfEmi.SP;
                    break;
                case "SE":
                    result = TUfEmi.SE;
                    break;
                case "TO":
                    result = TUfEmi.TO;
                    break;
            }
            return result;
        }

        private TNFeInfNFeEmitCRT GetCrtEmit(int xCRTEmit)
        {
            var result = TNFeInfNFeEmitCRT.crtRegimeNormal;
            switch (xCRTEmit)
            {
                case 1:
                    result = TNFeInfNFeEmitCRT.crtSimplesNacional;
                    break;
                case 2:
                    result = TNFeInfNFeEmitCRT.crtSimplesExcessoReceita;
                    break;
                case 3:
                    result = TNFeInfNFeEmitCRT.crtRegimeNormal;
                    break;
            }
            return result;
        }

        private TUf GetTUF(string uf)
        {
            var result = TUf.SP;
            switch (uf.ToUpper())
            {
                case "AC":
                    result = TUf.AC;
                    break;
                case "AL":
                    result = TUf.AL;
                    break;
                case "AP":
                    result = TUf.AP;
                    break;
                case "AM":
                    result = TUf.AM;
                    break;
                case "BA":
                    result = TUf.BA;
                    break;
                case "CE":
                    result = TUf.CE;
                    break;
                case "DF":
                    result = TUf.DF;
                    break;
                case "ES":
                    result = TUf.ES;
                    break;
                case "GO":
                    result = TUf.GO;
                    break;
                case "MA":
                    result = TUf.MA;
                    break;
                case "MT":
                    result = TUf.MT;
                    break;
                case "MS":
                    result = TUf.MS;
                    break;
                case "MG":
                    result = TUf.MG;
                    break;
                case "PA":
                    result = TUf.PA;
                    break;
                case "PB":
                    result = TUf.PB;
                    break;
                case "PR":
                    result = TUf.PR;
                    break;
                case "PE":
                    result = TUf.PE;
                    break;
                case "PI":
                    result = TUf.PI;
                    break;
                case "RJ":
                    result = TUf.RJ;
                    break;
                case "RN":
                    result = TUf.RN;
                    break;
                case "RS":
                    result = TUf.RS;
                    break;
                case "RO":
                    result = TUf.RO;
                    break;
                case "RR":
                    result = TUf.RR;
                    break;
                case "SC":
                    result = TUf.SC;
                    break;
                case "SP":
                    result = TUf.SP;
                    break;
                case "SE":
                    result = TUf.SE;
                    break;
                case "TO":
                    result = TUf.TO;
                    break;
            }
            return result;
        }
    }
}
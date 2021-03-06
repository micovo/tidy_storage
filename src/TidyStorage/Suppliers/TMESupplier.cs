﻿using System;
using System.Collections.Generic;

using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Security;
using System.Web;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using HtmlAgilityPack;
using TidyStorage.Suppliers.Data;

namespace TidyStorage.Suppliers
{
    class TMESupplier : Supplier
    {
        SupplierPart part;

        public override string Name { get { return "TME CZ"; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="part_number"></param>
        public TMESupplier(string part_number) : base(part_number)
        {
            //Nothing to do here
        }
        
        /// <summary>
        /// Function creates URL for the provided supplier part number
        /// </summary>
        /// <returns>Part URL</returns>
        public override string GetLink()
        {
            string fixed_part_number = part_number.Replace('/', '_');
            string url = "http://www.tme.eu/cz/details/" + HttpUtility.HtmlEncode(fixed_part_number.Trim()) + "/";
            return url;
        }

        /// <summary>
        /// Function downloads part details for the provided supplier part number
        /// </summary>
        /// <returns>Supplier Part</returns>
        public override SupplierPart DownloadPart()
        {
            HtmlDocument pricesDocument = null;
            HtmlDocument mainDocument = null;

            part = new SupplierPart();
            part.order_num = part_number;
            
            string refferer = GetLink();
            string error = "";
            string responce = "";
            string origin = "https://www.tme.eu";
            string host = "www.tme.eu";
            string url = refferer;
            CookieContainer cc = new CookieContainer();

            //Download part page and get cookies form price API
            using (HttpWebResponse resp = WebClient.Request(url, out error, cc))
            {
                if (resp != null)
                {
                    StreamReader reader = new StreamReader(resp.GetResponseStream());

                    responce = reader.ReadToEnd();
                    mainDocument = new HtmlDocument();
                    mainDocument.LoadHtml(responce);

                    refferer = resp.ResponseUri.ToString();
                }
            }


            //Download prices from the price API
            url = "https://www.tme.eu/cz/_ajax/ProductInformationPage/_getStocks.html";

            NameValueCollection nvc = new NameValueCollection();
            nvc["symbol"] = part_number;
            nvc["brutto"] = "";

            /*
            cc.Add(new Uri("http://www.tme.cz"), new Cookie("gtmCookie_3-seconds", "true"));
            cc.Add(new Uri("http://www.tme.cz"), new Cookie("gtmCookie_GAUserId", "1500533699.1512077661"));
            cc.Add(new Uri("http://www.tme.cz"), new Cookie("gtmCookie_referrer", "direct"));
            cc.Add(new Uri("http://www.tme.cz"), new Cookie("TES", "eoj2dj9lkurq9a6h2sbv3l0riv1g854i"));
            cc.Add(new Uri("http://www.tme.cz"), new Cookie("debug", "deleted"));
            cc.Add(new Uri("http://www.tme.cz"), new Cookie("lang_tme", "cz"));
            cc.Add(new Uri("http://www.tme.cz"), new Cookie("tls-checked", "1"));
            */

            using (HttpWebResponse resp = WebClient.Request(url, out error, cc, nvc, host, refferer, origin, "application/json, text/javascript, */*; q=0.01", "gzip, deflate, br"))
            {
                if (resp != null)
                {
                    StreamReader reader = new StreamReader(resp.GetResponseStream());

                    string prices_responce = reader.ReadToEnd();

                    if (prices_responce != "")
                    {
                        JToken jt = JToken.Parse(prices_responce);
                        string priceTableStr = (string)jt["Products"][0]["PriceTpl"];

                        priceTableStr = "<html><head></head><body>" + priceTableStr + "</body></html>";

                        pricesDocument = new HtmlDocument();
                        pricesDocument.LoadHtml(priceTableStr);
                    }
                }
            }


            if (pricesDocument != null)
            {
                GetPrices(pricesDocument);
            }

            if (mainDocument != null)
            {
                GetProductDescriptors(mainDocument);
            }

            return part;
        }


        /// <summary>
        /// Parser for reading product details from the prvided HTML document
        /// </summary>
        /// <param name="document">HTML document to be parsed</param>
        private void GetProductDescriptors(HtmlDocument document)
        {
            part.rows = new List<PartRow>();
            HtmlNodeCollection hnc = document.DocumentNode.SelectNodes("//div[contains(@class,'symbol-box')]");

            if (hnc != null)
            {
                var symbol_nodes = hnc.First().ChildNodes.First(x => (x.Name == "table"));

                if (symbol_nodes != null)
                {
                    HtmlNode[] rows = symbol_nodes.ChildNodes.Where(x => (x.Name == "tr")).ToArray();

                    foreach (HtmlNode tr_node in rows)
                    {
                        HtmlNode[] childs = tr_node.ChildNodes.Where(x => (x.Name == "td")).ToArray();

                        if (childs.Length == 2)
                        {
                            string name = childs[0].InnerText.Trim().Trim(':');
                            string value = childs[1].InnerText.Trim();


                            part.rows.Add(new PartRow(name, value));
                        }
                    }
                }
            }


            hnc = document.DocumentNode.SelectNodes("//a[@class='pdf']");

            if (hnc != null)
            {
                var datasheet_a_node = hnc.First();
                if (datasheet_a_node != null)
                {
                    string name = "Datasheet";
                    string value = "http://www.tme.eu" + datasheet_a_node.Attributes["href"].Value;
                    part.rows.Add(new PartRow(name, value));
                }
            }

           
            hnc = document.DocumentNode.SelectNodes("//div[@id='specification']");

            if (hnc != null)
            {
                HtmlNode hn = hnc.First().ChildNodes.First(x => x.Name == "table");

                HtmlNode[] rows = hn.ChildNodes.Where(x => (x.Name == "tr")).ToArray();

                foreach (HtmlNode tr_node in rows)
                {
                    HtmlNode[] childs = tr_node.ChildNodes.Where(x => (x.Name == "td")).ToArray();

                    if (childs.Count() == 3)
                    {
                        string value = childs[1].InnerText;
                        value = value.Replace('µ', 'u');
                        value = value.Replace('±', ' ');
                        value = value.Replace("Ω", "ohm");
                        value = value.Trim();

                        //Temperature split
                        if ((value.Contains("...") && (value.EndsWith("°C"))))
                        {
                            var split_values = value.Split(new string[] { "..." }, StringSplitOptions.None);
                            if (split_values.Count() > 1)
                            {
                                part.rows.Add(new PartRow(childs[0].InnerText.Trim() + " MIN", split_values[0] + "°C"));
                                part.rows.Add(new PartRow(childs[0].InnerText.Trim() + " MAX", split_values[1]));
                                continue;
                            }
                        }

                        part.rows.Add(new PartRow(childs[0].InnerText.Trim(), value));
                    }
                }
            }
        }

        /// <summary>
        /// Parser for reading product prices from the prvided HTML document
        /// </summary>
        /// <param name="document">HTML document to be parsed</param>
        private void GetPrices(HtmlDocument document)
        {
            try
            {
                HtmlNodeCollection hnc = document.DocumentNode.SelectNodes("//tbody[@id='prices_body']");

                if (hnc != null)
                {
                    part.prices = new List<PartPrice>();

                    HtmlNode hn = hnc.First();

                    foreach (HtmlNode xx in hn.ChildNodes)
                    {
                        if ((xx.Name == "tr") && (xx.ChildNodes.Count > 0) && (xx.ChildNodes[1].Name == "td"))
                        {
                            HtmlNode[] tds = xx.ChildNodes.Where(x => x.Name == "td").ToArray();

                            if (tds.Length == 3)
                            {
                                string tda = HttpUtility.HtmlDecode(tds[0].InnerText).Trim();
                                string tdp = HttpUtility.HtmlDecode(tds[2].InnerText).Trim();

                                tdp = tdp.Replace(",", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
                                tdp = tdp.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

                                if (tdp.Contains("Kč"))
                                {
                                    part.currency = "CZK";

                                    var aaa = tda.Split('-');
                                    var min = int.Parse(aaa[0].Trim().Trim('+'));
                                    var max = int.MaxValue;

                                    if (aaa.Length > 1)
                                    {
                                        max = int.Parse(aaa[1].Trim());
                                    }


                                    int ix = tdp.IndexOfAny(("0123456789").ToCharArray());
                                    var price = float.Parse(tdp.Substring(ix, tdp.Length - ix - 3));

                                    part.prices.Add(new PartPrice(min, max, price));
                                }
                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                throw new Exception("TME web importer failed. " + ex.Message);
            }

            //Fix prices maximal amount
            for (int i = 0; i < part.prices.Count - 1; i++)
            {
                if (part.prices[i].amount_max > part.prices[i + 1].amount_min) part.prices[i].amount_max = part.prices[i + 1].amount_min - 1;
            }
        }
    }
}

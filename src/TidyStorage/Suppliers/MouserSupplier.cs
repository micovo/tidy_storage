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
    public class MouserSupplier : Supplier
    {
        SupplierPart part;

        public MouserSupplier(string part_number) : base(part_number)
        {

        }

        public override string Name { get { return "Mouse"; } }

        public override string GetLink()
        {
            return "http://eu.mouser.com/Search/Refine.aspx?Keyword=" + part_number;
        }

        public override SupplierPart DownloadPart()
        {
            part = new SupplierPart();

            part.order_num = part_number;


            string error = "";
            string responce = "";
            string url = GetLink();

            using (HttpWebResponse resp = WebClient.Request(url, out error, null))
            {
                if (resp != null)
                {
                    StreamReader reader = new StreamReader(resp.GetResponseStream());

                    responce = reader.ReadToEnd();

                    HtmlDocument document = new HtmlDocument();
                    document.LoadHtml(responce);

                    if (document != null)
                    {
                        GetPrices(document);
                        GetProductDescriptors(document);
                    }

                }
            }

            return part;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="document"></param>
        private void GetProductDescriptors(HtmlDocument document)
        {
            part.rows = new List<PartRow>();

            HtmlNodeCollection hnc;



            hnc = document.DocumentNode.SelectNodes("//div[@id='product-desc']");

            if (hnc != null)
            {
                var header_nodes = hnc.First().ChildNodes.Where(x => (x.Attributes["class"] != null) && (x.Attributes["class"].Value == "row"));

                if (header_nodes != null)
                {
                    foreach (HtmlNode hn in header_nodes)
                    {
                        string stuff = hn.InnerText;
                        var split = stuff.Split(':');
                        if (split.Length == 2)
                        {
                            string name = split[0].Trim();
                            string value = split[1].Trim();

                            part.rows.Add(new PartRow(name, value));
                        }
                    }
                }
            }




            hnc = document.DocumentNode.SelectNodes("//a[contains(@id,'lnkCatalogDataSheet')]");

            if (hnc != null)
            {
                var datasheet_node = hnc.FirstOrDefault(x => x.InnerText == "Data Sheet");
                if (datasheet_node != null)
                {
                    part.rows.Add(new PartRow("Datasheet", datasheet_node.Attributes["href"].Value));
                }
            }




            hnc = document.DocumentNode.SelectNodes("//table[@class='specs']");

            if (hnc != null)
            {
                var specs_table = hnc.First();

                var rows = specs_table.ChildNodes.Where(x => x.Name == "tr");

                foreach (HtmlNode row in rows)
                {
                    var cells = row.ChildNodes.Where(x => x.Name == "td").ToArray();
                    string name = cells[0].InnerText.Trim().Trim(':');
                    string value = cells[1].InnerText.Trim().Replace("&nbsp;","");

                    part.rows.Add(new PartRow(name, value));
                }
            }

            
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="document"></param>
        private void GetPrices(HtmlDocument document)
        {
            var hnc = document.DocumentNode.SelectNodes("//div[contains(@class,'PriceBreaks')]");

            if (hnc != null)
            {
                var main = hnc.First();

                var quantity_nodes = main.SelectNodes("//a[contains(@id,'lnkQuantity')]");
                var price_nodes = main.SelectNodes("//span[contains(@id,'lblPrice')]");

                if ((quantity_nodes != null) && (price_nodes != null))
                {
                    if (quantity_nodes.Count == price_nodes.Count)
                    {
                        part.prices = new List<PartPrice>();

                        for (int i = 0; i < quantity_nodes.Count; i++)
                        {
                            string quantity_str = quantity_nodes[i].InnerText.Trim();
                            string price_str = price_nodes[i].InnerText.Trim();

                            int min;
                            float price;

                            if (price_str.EndsWith("€"))
                            {
                                part.currency = "EUR";

                                price_str = price_str.Trim(" €".ToCharArray());
                                quantity_str = quantity_str.Replace(".", "");

                                if (int.TryParse(quantity_str, out min) && float.TryParse(price_str, out price))
                                {
                                    part.prices.Add(new PartPrice(min, price));
                                }
                            }
                        }
                    }
                }
            }
        }



    }
}

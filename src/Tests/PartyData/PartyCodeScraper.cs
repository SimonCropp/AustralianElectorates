﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;

public static class PartyCodeScraper
{
    public static async Task Run()
    {
        var htmlPath = Path.Combine(DataLocations.TempPath, "partycodes.html");
        var url = "https://www.aec.gov.au/Electorates/party-codes.htm";
        File.Delete(htmlPath);
        try
        {
            await Downloader.DownloadFile(htmlPath, url);

            if (!File.Exists(htmlPath))
            {
                throw new Exception($"Could not download {url}");
            }

            var document = new HtmlDocument();
            document.Load(htmlPath);
            var selectSingleNode = document.DocumentNode.SelectSingleNode("//caption");
            var table = selectSingleNode.ParentNode;
            Codes = new Dictionary<string,string>();
            foreach (var node in table.SelectNodes("//tr").Skip(1))
            {
                var nodes = node.ChildNodes.Where(x=>x.NodeType != HtmlNodeType.Text).ToList();
                var abbreviation = nodes[0].InnerHtml;
                var name = nodes[1].InnerHtml.Split('(')[0].Trim();
                Codes.Add(abbreviation, name);
            }
        }
        catch (Exception exception)
        {
            throw new Exception($"Failed to parse {htmlPath} {htmlPath}", exception);
        }
    }

    public static Dictionary<string, string> Codes;
}
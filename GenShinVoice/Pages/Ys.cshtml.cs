using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;

namespace GenShinVoice.Pages
{
    public class YsModel : PageModel
    {
        private HttpClient hc;
        private readonly IMemoryCache cache;

        [BindProperty(SupportsGet = true)]
        public string Name { get; set; }
        [BindProperty(SupportsGet = true)]
        public List<VoiceData> Datas { get; set; }
        [BindProperty(SupportsGet = true)]
        public List<string> Characters { get; set; }
        [BindProperty(SupportsGet = true)]
        public int Mode { get; set; }
        public YsModel(IHttpClientFactory clientFactory, IMemoryCache cache)
        {
            hc = clientFactory.CreateClient();
            this.cache = cache;
        }
        public async Task OnGet()
        {
            var dict = await GetNamesDict();
            if (string.IsNullOrEmpty(Name))
            {
                Characters = dict.Keys.OrderBy(s => s).ToList();
                ViewData["Title"] = "角色列表";
                Mode = 0;
                return;
            }
            if (cache.TryGetValue(Name, out List<VoiceData> datas))
            {
                Datas = datas;
                ViewData["Title"] = Name;
                Mode = 1;
                return;
            }
            if (dict.TryGetValue(Name, out var key))
            {
                var isTraveler = Name == "旅行者";
                Datas = new();
                var html = await hc.GetStringAsync($"https://genshin.honeyhunterworld.com/db/char/{key}/?lang=CHS");
                var si = html.IndexOf("id=scroll_quotes");
                var ei = html.IndexOf("id=scroll_stories");
                var tsi = si;
                var tei = si;
                var doc = new HtmlDocument();
                while (tsi < ei && tsi > 0)
                {
                    tsi = html.IndexOf("<table class=item_main_table", tei);
                    if (tsi >= ei || tsi < 0)
                        break;
                    tei = html.IndexOf("/table>", tsi) + 7;
                    doc.LoadHtml(html[tsi..tei]);
                    var node = doc.DocumentNode;
                    var trs = node.Descendants("tr").ToList();
                    var offset = isTraveler && trs[1].InnerText.Contains("Unlock") ? 1 : 0;
                    var data = new VoiceData
                    {
                        Title = trs[0].InnerText,
                        Text = trs[1 + offset].InnerText
                    };
                    var tds = trs[3 + offset].Descendants("td")
                        .Where(td => td.HasChildNodes)
                        .Select(td => td.FirstChild.GetAttributeValue("data-audio", string.Empty))
                        .Where(s => !string.IsNullOrEmpty(s)).ToList();
                    if (!tds.Any())
                        continue;
                    foreach (var url in tds)
                    {
                        if (string.IsNullOrWhiteSpace(url))
                            continue;
                        var aurl = "https://genshin.honeyhunterworld.com/audio/" + url + ".ogg";
                        switch (url[^2..])
                        {
                            case "en":
                                data.En = aurl; break;
                            case "jp":
                                data.Jp = aurl; break;
                            case "cn":
                                data.Cn = aurl; break;
                            case "kr":
                                data.Kr = aurl; break;
                        }
                    }
                    Datas.Add(data);
                }
                cache.Set(Name, Datas);
                ViewData["Title"] = Name;
            }
            Mode = Datas.Any() ? 1 : 2;
        }

        private async Task<Dictionary<string, string>> GetNamesDict()
        {
            if (!cache.TryGetValue("NameDict", out Dictionary<string, string> dict))
            {
                dict = new Dictionary<string, string>();
                var html = await hc.GetStringAsync("https://genshin.honeyhunterworld.com/db/char/characters/?lang=CHS");
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                foreach (var node in doc.DocumentNode.SelectNodes("//div[@class='char_sea_cont']"))
                {
                    var span = node.SelectSingleNode(".//span[@class='sea_charname']");
                    var cn = span.InnerText;
                    if (cn.Contains("旅行者"))
                        continue;
                    var a = span.SelectSingleNode("..");
                    var url = a.GetAttributeValue("href", null);
                    var m = Regex.Match(url, "/char/(.*)/");
                    if (m.Success)
                    {
                        dict.Add(span.InnerText, m.Groups[1].Value);
                    }
                }
                dict.Add("旅行者", "traveler_girl_anemo");
                cache.Set("NameDict", dict);
            }
            return dict;
        }
    }

    public class VoiceData
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public string Cn { get; set; }
        public string Jp { get; set; }
        public string Kr { get; set; }
        public string En { get; set; }
    }
}

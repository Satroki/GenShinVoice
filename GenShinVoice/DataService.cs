using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GenShinVoice
{
    public class DataService
    {
        private readonly ILogger<DataService> logger;
        private readonly IFreeSql db;
        private readonly IMemoryCache cache;
        private readonly IHttpClientFactory clientFactory;
        public const string AudioUrlBase = "https://genshin.honeyhunterworld.com/audio/";

        public DataService(ILogger<DataService> logger, IFreeSql db, IMemoryCache cache, IHttpClientFactory clientFactory)
        {
            this.logger = logger;
            this.db = db;
            this.cache = cache;
            this.clientFactory = clientFactory;
        }

        public async Task<List<Character>> SyncCharacters()
        {
            var list = await db.Select<Character>().ToListAsync();
            var set = list.Select(c => c.NameCn).ToHashSet();
            var hc = clientFactory.CreateClient();
            var html = await hc.GetStringAsync("https://genshin.honeyhunterworld.com/db/char/characters/?lang=CHS");
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            foreach (var node in doc.DocumentNode.SelectNodes("//div[@class='char_sea_cont']"))
            {
                var span = node.SelectSingleNode(".//span[@class='sea_charname']");
                var cn = span.InnerText;
                if (set.Contains(cn) || cn.Contains("旅行者"))
                    continue;
                var a = span.SelectSingleNode("..");
                var url = a.GetAttributeValue("href", null);
                var m = Regex.Match(url, "/char/(.*)/");
                if (m.Success)
                {
                    var cc = new Character { NameCn = cn, NameEn = m.Groups[1].Value };
                    cc.Id = (int)db.Insert<Character>().AppendData(cc).ExecuteIdentity();
                    list.Add(cc);
                }
            }
            if (!set.Contains("旅行者"))
            {
                var cc = new Character { NameCn = "旅行者", NameEn = "traveler_girl_anemo" };
                cc.Id = (int)db.Insert<Character>().AppendData(cc).ExecuteIdentity();
                list.Add(cc);
            }
            logger.LogInformation($"SyncCharacters: {list.Count}");
            return list;
        }

        public async Task<List<VoiceData>> SyncVoiceData(Character c)
        {
            var isTraveler = c.NameCn == "旅行者";
            var datas = new List<VoiceData>();
            var hc = clientFactory.CreateClient();
            var html = await hc.GetStringAsync($"https://genshin.honeyhunterworld.com/db/char/{c.NameEn}/?lang=CHS");
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
                    CharacterId = c.Id,
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
                    var aurl = AudioUrlBase + url + ".ogg";
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
                datas.Add(data);
            }
            if (datas.Any())
            {
                db.Delete<VoiceData>().Where(d => d.CharacterId == c.Id).ExecuteAffrows();
                db.Insert<VoiceData>().AppendData(datas).ExecuteAffrows();
            }
            logger.LogInformation($"SyncVoiceData: {c.NameCn} {datas.Count}");
            return datas;
        }

        public async Task<List<Character>> GetCharacters()
        {
            if (!cache.TryGetValue("Characters", out List<Character> list))
            {
                list = await db.Select<Character>().ToListAsync();
                if (!list.Any())
                {
                    list = await SyncCharacters();
                }
                cache.Set("Characters", list);
            }
            return list;
        }

        public async Task<List<VoiceData>> GetVoiceDatas(Character c)
        {
            var datas = await db.Select<VoiceData>().Where(v => v.CharacterId == c.Id).ToListAsync();
            if (!datas.Any())
            {
                datas = await SyncVoiceData(c);
            }
            return datas;
        }
    }
}

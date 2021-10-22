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
        private readonly DataService ds;

        [BindProperty(SupportsGet = true)]
        public string Name { get; set; }
        [BindProperty(SupportsGet = true)]
        public List<VoiceData> Datas { get; set; }
        [BindProperty(SupportsGet = true)]
        public List<string> Characters { get; set; }
        [BindProperty(SupportsGet = true)]
        public int Mode { get; set; }
        public YsModel(IHttpClientFactory clientFactory, IMemoryCache cache, DataService ds)
        {
            hc = clientFactory.CreateClient();
            this.cache = cache;
            this.ds = ds;
        }
        public async Task OnGet()
        {
            var chars = await ds.GetCharacters();
            if (string.IsNullOrEmpty(Name))
            {
                Characters = chars.Select(c => c.NameCn).OrderBy(s => s).ToList();
                ViewData["Title"] = "╫ги╚ап╠М";
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
            var c = chars.FirstOrDefault(v => v.NameCn == Name);
            if (c == null)
            {
                Mode = 2;
            }
            else
            {
                Datas = await ds.GetVoiceDatas(c);
                cache.Set(Name, Datas);
                ViewData["Title"] = Name;
                Mode = Datas.Any() ? 1 : 2;
            }
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GenShinVoice.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class DbController : ControllerBase
    {
        private readonly ILogger<DbController> logger;
        private readonly DataService ds;
        private readonly IFreeSql db;
        private readonly IHttpClientFactory clientFactory;

        public DbController(ILogger<DbController> logger, DataService ds, IFreeSql db, IHttpClientFactory clientFactory)
        {
            this.logger = logger;
            this.ds = ds;
            this.db = db;
            this.clientFactory = clientFactory;
        }

        [HttpGet]
        public async Task Sync()
        {
            var cs = await ds.SyncCharacters();
            foreach (var c in cs)
            {
                await ds.SyncVoiceData(c);
                await Task.Delay(3000);
            }
        }

        [HttpGet]
        public async Task<List<Character>> GetAll()
        {
            var cs = await db.Select<Character>().ToListAsync();
            var vs = await db.Select<VoiceData>().ToListAsync();
            var cd = cs.ToDictionary(c => c.Id);
            foreach (var g in vs.GroupBy(v => v.CharacterId))
            {
                cd[g.Key].VoiceDatas = g.ToList();
            }
            return cs;
        }

        [HttpGet]
        public async Task Download()
        {
            var chs = await db.Select<Character>().ToListAsync();
            var cDict = chs.ToDictionary(c => c.Id);
            var vds = await db.Select<VoiceData>().ToListAsync();
            var hc = clientFactory.CreateClient();
            var len = DataService.AudioUrlBase.Length;
            var root = new DirectoryInfo("Voices");
            root.Create();
            var gs = vds.GroupBy(v => v.CharacterId).ToList();
            var pg = 0;
            foreach (var g in gs)
            {
                var gcnt = g.Count();
                var c = cDict[g.Key];
                var cdir = root.CreateSubdirectory(c.NameCn);
                var jp = cdir.CreateSubdirectory("jp");
                var pv = 0;
                pg++;
                foreach (var vd in g)
                {
                    pv++;
                    if (string.IsNullOrEmpty(vd.Jp))
                        continue;
                    var name = Path.GetFileName(vd.Jp);
                    var full = Path.Combine(jp.FullName, name);
                    if (System.IO.File.Exists(full))
                        continue;
                    using var s = await hc.GetStreamAsync(vd.Jp);
                    using var fs = System.IO.File.Create(full);
                    await s.CopyToAsync(fs);
                    logger.LogInformation($"Download {pg}/{gs.Count} - {c.NameCn} {pv}/{gcnt}");
                }
            }
        }
    }
}

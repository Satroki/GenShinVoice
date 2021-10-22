using FreeSql.DataAnnotations;
using System.Collections.Generic;

namespace GenShinVoice
{
    public class Character
    {
        [Column(IsIdentity = true, IsPrimary = true)]
        public int Id { get; set; }
        public string NameCn { get; set; }
        public string NameEn { get; set; }

        public List<VoiceData> VoiceDatas { get; set; }
    }

    public class VoiceData
    {
        [Column(IsIdentity = true, IsPrimary = true)]
        public int Id { get; set; }
        public int CharacterId { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public string Cn { get; set; }
        public string Jp { get; set; }
        public string Kr { get; set; }
        public string En { get; set; }
    }
}

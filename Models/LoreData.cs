using System.Collections.Generic;

namespace StardewAIMod.Models
{
    public class LoreData
    {
        public List<LoreTopic> Topics { get; set; } = new List<LoreTopic>();
    }

    public class LoreTopic
    {
        public List<string> Keywords { get; set; } = new List<string>();
        public string Info { get; set; } = "";
    }
}

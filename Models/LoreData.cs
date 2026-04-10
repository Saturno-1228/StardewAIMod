using System.Collections.Generic;
using System.Text.RegularExpressions;

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

        // Cache compiled regexes for faster matching
        public List<Regex> CompiledRegexes { get; set; } = new List<Regex>();
    }
}

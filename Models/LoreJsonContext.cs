using System.Text.Json.Serialization;

namespace LivingCompanionsValley.Models
{
    [JsonSerializable(typeof(NpcIdentityDto))]
    [JsonSerializable(typeof(NpcExtendedLoreDto))]
    [JsonSerializable(typeof(System.Collections.Generic.List<string>))]
    internal partial class LoreJsonContext : JsonSerializerContext
    {
    }
}

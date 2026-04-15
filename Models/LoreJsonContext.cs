using System.Text.Json.Serialization;

namespace LivingCompanionsValley.Models
{
    [JsonSerializable(typeof(NpcIdentityDto))]
    [JsonSerializable(typeof(NpcExtendedLoreDto))]
    internal partial class LoreJsonContext : JsonSerializerContext
    {
    }
}

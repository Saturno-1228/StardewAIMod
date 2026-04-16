using LiteDB;

namespace LivingCompanionsValley.Models
{
    public class LoreChunk
    {
        public ObjectId Id { get; set; }
        public string NpcName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TextContent { get; set; } = string.Empty;
    }

    public struct NpcIdentityDto
    {
        public string NpcName { get; set; }
        public string TonalStyle { get; set; }
        public string SystemPrompt { get; set; }
    }

    public struct NpcExtendedLoreDto
    {
        public string FragmentId { get; set; }
        public string Category { get; set; }
        public string Content { get; set; }
        public float[]? Vector { get; set; }
    }
}

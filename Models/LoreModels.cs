using LiteDB;

namespace LivingCompanionsValley.Models
{
    public class LoreChunk
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public string NpcName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TextContent { get; set; } = string.Empty;
    }

    public struct NpcIdentityDto
    {
        public string NpcName { get; set; } = string.Empty;
        public string TonalStyle { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
    }

    public struct NpcExtendedLoreDto
    {
        public string FragmentId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float[]? Vector { get; set; } = null;
    }
}

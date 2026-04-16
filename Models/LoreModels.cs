namespace LivingCompanionsValley.Models
{
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

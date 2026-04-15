namespace LivingCompanionsValley.Models
{
    public readonly record struct NpcIdentityDto(
        string NpcName,
        string CorePersonality,
        string RelationshipStatus,
        string TonalStyle,
        string SystemPrompt
    );

    public readonly record struct NpcExtendedLoreDto(
        string FragmentId,
        string Category,
        string Content,
        float[]? Vector
    );
}

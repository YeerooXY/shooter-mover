using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    /// <summary>
    /// Canonical weapon-definition rarity identities. Persisted legacy identities remain
    /// available for compatibility; normalized catalog content uses MythicArtifact.
    /// Equipment quality identities are separate.
    /// </summary>
    public static class StrongboxDefinitionRarityIdsV1
    {
        public static readonly StableId Common =
            StableId.Parse("rarity.common");
        public static readonly StableId Uncommon =
            StableId.Parse("rarity.uncommon");
        public static readonly StableId Rare =
            StableId.Parse("rarity.rare");
        public static readonly StableId Epic =
            StableId.Parse("rarity.epic");
        public static readonly StableId Legendary =
            StableId.Parse("rarity.legendary");
        public static readonly StableId Mythic =
            StableId.Parse("rarity.mythic");
        public static readonly StableId Artifact =
            StableId.Parse("rarity.artifact");
        public static readonly StableId MythicArtifact =
            StableId.Parse("rarity.mythic-artifact");
    }
}

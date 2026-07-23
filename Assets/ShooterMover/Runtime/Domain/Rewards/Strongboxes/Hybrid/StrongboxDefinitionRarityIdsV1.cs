using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    /// <summary>
    /// Canonical weapon-definition rarity identities. These are deliberately
    /// separate from equipment quality identities such as Common/Rare/Exceptional.
    /// </summary>
    public static class StrongboxDefinitionRarityIdsV1
    {
        public static readonly StableId Common = StableId.Parse("rarity.common");
        public static readonly StableId Uncommon = StableId.Parse("rarity.uncommon");
        public static readonly StableId Rare = StableId.Parse("rarity.rare");
        public static readonly StableId Epic = StableId.Parse("rarity.epic");
        public static readonly StableId Legendary = StableId.Parse("rarity.legendary");
        public static readonly StableId Mythic = StableId.Parse("rarity.mythic");
        public static readonly StableId Artifact = StableId.Parse("rarity.artifact");
    }
}

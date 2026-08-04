using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// Authored loot rules for every Strongbox tier.
    /// The namespace already supplies the Strongbox context, so the type stays concise.
    /// </summary>
    public static class LootTable
    {
        public static IReadOnlyList<StrongboxHybridLootPolicy> Rules
        {
            get { return StrongboxHybridLootCatalog.Policies; }
        }

        public static StrongboxHybridLootPolicy GetTier(int tier)
        {
            return StrongboxHybridLootCatalog.GetByTierNumber(tier);
        }

        public static bool TryGet(
            StableId tierId,
            out StrongboxHybridLootPolicy rules)
        {
            return StrongboxHybridLootCatalog.TryGet(tierId, out rules);
        }
    }
}

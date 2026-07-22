using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>One authored production source of truth for dropped-box tier probabilities.</summary>
    public static class ProductionStrongboxTierSelectionCatalogV1
    {
        public static readonly StableId LowSourceProfileId = StableId.Parse("strongbox-tier-profile.low-source");
        public static readonly StableId StandardSourceProfileId = StableId.Parse("strongbox-tier-profile.standard-source");
        public static readonly StableId ImprovedSourceProfileId = StableId.Parse("strongbox-tier-profile.improved-source");
        public static readonly StableId BossSourceProfileId = StableId.Parse("strongbox-tier-profile.boss-source");
        public static readonly StableId TreasureSourceProfileId = StableId.Parse("strongbox-tier-profile.treasure-source");
        public static readonly StableId CompletionMinimumProfileId = StableId.Parse("strongbox-tier-profile.run-minimum");
        private static readonly StableId TierRollPurposeId = StableId.Parse("reward-rng.strongbox-tier-v1");
        private static readonly ReadOnlyCollection<StrongboxTierSelectionProfileV1> ProfilesValue = new ReadOnlyCollection<StrongboxTierSelectionProfileV1>(new List<StrongboxTierSelectionProfileV1>
        {
            Profile(LowSourceProfileId, 700000,220000,60000,15000,3500,900,350,150,60,25,15),
            Profile(StandardSourceProfileId, 400000,300000,170000,80000,35000,10000,3500,1000,350,90,60),
            Profile(ImprovedSourceProfileId, 150000,250000,250000,180000,100000,45000,18000,6000,1800,700,300),
            Profile(BossSourceProfileId, 50000,150000,240000,230000,160000,90000,45000,22000,9000,3000,1000),
            Profile(TreasureSourceProfileId, 10000,70000,170000,230000,220000,150000,80000,40000,20000,8000,2000),
            Profile(CompletionMinimumProfileId, 350000,300000,180000,90000,45000,20000,9000,3500,1500,650,350),
        });
        public static IReadOnlyList<StrongboxTierSelectionProfileV1> Profiles { get { return ProfilesValue; } }
        public static bool TryResolve(StableId profileStableId, out StrongboxTierSelectionProfileV1 profile) { for (int index = 0; index < ProfilesValue.Count; index++) if (ProfilesValue[index].ProfileStableId == profileStableId) { profile = ProfilesValue[index]; return true; } profile = null; return false; }
        public static StrongboxTierSelectionProfileV1 Get(StableId profileStableId) { StrongboxTierSelectionProfileV1 profile; if (!TryResolve(profileStableId, out profile)) throw new KeyNotFoundException("Unknown strongbox tier-selection profile " + profileStableId + "."); return profile; }
        public static StableId SelectExactTier(StableId profileStableId, IEnumerable<StableId> activeContextIds, ulong rootSeed, int algorithmVersion, ulong ordinal)
        {
            IReadOnlyList<StrongboxTierWeightV1> weights = Get(profileStableId).Evaluate(activeContextIds); ulong total = 0UL;
            for (int index = 0; index < weights.Count; index++) total = checked(total + weights[index].Weight);
            DeterministicRandom stream = DeterministicRandom.CreateSubstream(rootSeed, algorithmVersion, TierRollPurposeId, ordinal); ulong selected; stream = stream.NextBoundedUInt64(total, out selected); ulong cumulative = 0UL;
            for (int index = 0; index < weights.Count; index++) { cumulative = checked(cumulative + weights[index].Weight); if (selected < cumulative) return weights[index].TierStableId; }
            throw new InvalidOperationException("Strongbox tier selection did not resolve an outcome.");
        }
        private static StrongboxTierSelectionProfileV1 Profile(StableId id, params ulong[] weights)
        {
            if (weights == null || weights.Length != 11) throw new ArgumentException("Production tier profiles must author all eleven canonical tiers.", nameof(weights));
            var baseWeights = new List<StrongboxTierWeightV1>(11); var modifiers = new List<StrongboxTierContextModifierV1>();
            for (int index = 0; index < weights.Length; index++)
            {
                StableId tierId = ProductionStrongboxCatalogV1.GetByNumber(index + 1).TierStableId; baseWeights.Add(new StrongboxTierWeightV1(tierId, weights[index])); int tierNumber = index + 1;
                if (tierNumber >= 5) modifiers.Add(new StrongboxTierContextModifierV1(StableId.Parse("difficulty.hard"), tierId, 1200));
                if (tierNumber >= 7) modifiers.Add(new StrongboxTierContextModifierV1(StableId.Parse("difficulty.nightmare"), tierId, 1500));
                if (tierNumber >= 8) modifiers.Add(new StrongboxTierContextModifierV1(StableId.Parse("game-mode.raid"), tierId, 1500));
                if (tierNumber >= 6) modifiers.Add(new StrongboxTierContextModifierV1(StableId.Parse("event.strongbox-tier-boost"), tierId, 2000));
            }
            return new StrongboxTierSelectionProfileV1(id, baseWeights, modifiers);
        }
    }
}

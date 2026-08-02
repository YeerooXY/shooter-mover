using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// Authored hybrid loot-selection and augment-signature balance for the eleven
    /// production strongbox tiers. Rarity values are direct first-stage selection
    /// weights; definition level affinity and base weight are applied only after a
    /// rarity with eligible candidates has been selected. Equipment quality remains
    /// a separate Common/Rare/Exceptional roll owned by BOX/GEN.
    /// </summary>
    public static class StrongboxHybridLootCatalog
    {
        private static readonly ReadOnlyCollection<StrongboxDistanceWeight>
            DefinitionBellWeightsValue =
                new ReadOnlyCollection<StrongboxDistanceWeight>(
                    new List<StrongboxDistanceWeight>
                    {
                        Distance(0, 1000000),
                        Distance(1, 945959),
                        Distance(2, 800737),
                        Distance(3, 606531),
                        Distance(4, 411112),
                        Distance(5, 249352),
                        Distance(6, 135335),
                        Distance(7, 65799),
                        Distance(8, 28566),
                        Distance(9, 11109),
                        Distance(10, 3866),
                        Distance(11, 1204),
                        Distance(12, 335),
                    });

        private static readonly ReadOnlyCollection<StrongboxWeightedIntOutcome>
            InstanceLevelOffsetsValue =
                new ReadOnlyCollection<StrongboxWeightedIntOutcome>(
                    new List<StrongboxWeightedIntOutcome>
                    {
                        Outcome(-4, 1),
                        Outcome(-3, 12),
                        Outcome(-2, 111),
                        Outcome(-1, 726),
                        Outcome(0, 1000),
                        Outcome(1, 726),
                        Outcome(2, 111),
                        Outcome(3, 12),
                        Outcome(4, 1),
                    });

        private static readonly ReadOnlyCollection<StrongboxHybridLootPolicy>
            PoliciesValue = new ReadOnlyCollection<StrongboxHybridLootPolicy>(
                new List<StrongboxHybridLootPolicy>
                {
                    Policy(1, 0, 0, 1,
                        Slots(70, 25, 4, 1),
                        Levels(new[] { 1, 2, 3, 4, 5 }, new ulong[] { 45, 30, 15, 7, 3 }),
                        Rarities(80000, 16000, 3500, 500, 0)),
                    Policy(2, 0, 1, 2,
                        Slots(52, 34, 12, 2),
                        Levels(new[] { 1, 2, 3, 4, 5, 6 }, new ulong[] { 20, 30, 25, 15, 7, 3 }),
                        Rarities(72000, 21000, 6000, 1000, 0)),
                    Policy(3, 0, 1, 3,
                        Slots(35, 38, 22, 5),
                        Levels(
                            new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                            new ulong[] { 1500, 2500, 2500, 1800, 1000, 500, 170, 25, 5 }),
                        Rarities(63000, 26000, 9000, 2000, 0)),
                    Policy(4, 0, 2, 4,
                        Slots(20, 35, 32, 13),
                        Levels(
                            new[] { 3, 4, 5, 6, 7, 8, 9, 10 },
                            new ulong[] { 120, 200, 250, 200, 130, 70, 25, 5 }),
                        Rarities(54000, 29000, 13000, 4000, 0)),
                    Policy(5, 1, 3, 5,
                        Slots(0, 35, 45, 20),
                        Levels(
                            new[] { 4, 5, 6, 7, 8, 9, 10 },
                            new ulong[] { 10, 18, 24, 20, 15, 9, 4 }),
                        Rarities(45000, 31000, 18000, 5999, 1)),
                    Policy(6, 2, 4, 6,
                        Slots(0, 15, 45, 40),
                        Levels(
                            new[] { 5, 6, 7, 8, 9, 10 },
                            new ulong[] { 8, 15, 20, 22, 23, 12 }),
                        Rarities(36000, 32000, 23000, 8995, 5)),
                    Policy(7, 3, 5, 7,
                        Slots(0, 0, 40, 60),
                        Levels(
                            new[] { 6, 7, 8, 9, 10 },
                            new ulong[] { 8, 14, 20, 30, 28 }),
                        Rarities(28000, 31000, 27000, 13980, 20)),
                    Policy(8, 4, 6, 8,
                        Slots(0, 0, 0, 100),
                        Levels(
                            new[] { 6, 7, 8, 9, 10 },
                            new ulong[] { 8, 12, 20, 25, 35 }),
                        Rarities(20000, 28000, 31000, 20950, 50)),
                    Policy(9, 5, 7, 9,
                        Slots(0, 0, 0, 100),
                        Levels(
                            new[] { 8, 9, 10 },
                            new ulong[] { 8, 27, 65 }),
                        Rarities(13000, 23000, 32000, 31750, 250)),
                    Policy(10, 6, 8, 10,
                        SlotOutcomes(
                            new[] { 3, 4 },
                            new ulong[] { 97, 3 }),
                        Levels(
                            new[] { 9, 10, 11 },
                            new ulong[] { 8, 77, 15 }),
                        Rarities(7000, 15000, 30000, 47000, 1000)),
                    Policy(11, 8, 10, 12,
                        SlotOutcomes(
                            new[] { 3, 4 },
                            new ulong[] { 70, 30 }),
                        Levels(
                            new[] { 10, 11, 12 },
                            new ulong[] { 597, 1393, 10 }),
                        Rarities(2000, 7000, 24000, 64500, 2500)),
                });

        public static IReadOnlyList<StrongboxHybridLootPolicy> Policies
        {
            get { return PoliciesValue; }
        }

        public static StrongboxHybridLootPolicy GetByTierNumber(int tierNumber)
        {
            if (tierNumber < 1 || tierNumber > PoliciesValue.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(tierNumber));
            }
            return PoliciesValue[tierNumber - 1];
        }

        public static bool TryGet(
            StableId tierStableId,
            out StrongboxHybridLootPolicy policy)
        {
            if (tierStableId != null)
            {
                for (int index = 0; index < PoliciesValue.Count; index++)
                {
                    StrongboxTier tier =
                        StrongboxCatalog.GetByNumber(index + 1);
                    if (tier.TierStableId == tierStableId)
                    {
                        policy = PoliciesValue[index];
                        return true;
                    }
                }
            }

            policy = null;
            return false;
        }

        private static StrongboxHybridLootPolicy Policy(
            int tierNumber,
            int minimumDelta,
            int modeDelta,
            int maximumDelta,
            IEnumerable<StrongboxWeightedIntOutcome> slots,
            IEnumerable<StrongboxWeightedIntOutcome> levels,
            IEnumerable<StrongboxRarityProfile> rarities)
        {
            StrongboxTier tier =
                StrongboxCatalog.GetByNumber(tierNumber);
            return StrongboxHybridLootPolicy.Create(
                StableId.Create("strongbox-hybrid-loot", tier.Slug + "-v1"),
                minimumDelta,
                modeDelta,
                maximumDelta,
                800,
                DefinitionBellWeightsValue,
                InstanceLevelOffsetsValue,
                slots,
                levels,
                rarities);
        }

        private static IEnumerable<StrongboxWeightedIntOutcome> Slots(
            ulong zero,
            ulong one,
            ulong two,
            ulong three)
        {
            var values = new List<StrongboxWeightedIntOutcome>();
            AddIfPositive(values, 0, zero);
            AddIfPositive(values, 1, one);
            AddIfPositive(values, 2, two);
            AddIfPositive(values, 3, three);
            return values;
        }

        private static IEnumerable<StrongboxWeightedIntOutcome> SlotOutcomes(
            int[] values,
            ulong[] weights)
        {
            return Outcomes(values, weights);
        }

        private static IEnumerable<StrongboxWeightedIntOutcome> Levels(
            int[] values,
            ulong[] weights)
        {
            return Outcomes(values, weights);
        }

        private static IEnumerable<StrongboxWeightedIntOutcome> Outcomes(
            int[] values,
            ulong[] weights)
        {
            if (values == null || weights == null || values.Length != weights.Length)
            {
                throw new ArgumentException(
                    "Outcome values and weights must have matching lengths.");
            }

            var output = new List<StrongboxWeightedIntOutcome>(values.Length);
            for (int index = 0; index < values.Length; index++)
            {
                output.Add(Outcome(values[index], weights[index]));
            }
            return output;
        }

        /// <summary>
        /// The five game-facing rarity anchors are authored as direct first-stage
        /// selection weights. Uncommon and Mythic retain explicit midpoint mappings
        /// so future definitions in those bands participate without a second policy.
        /// Rarities with no eligible definitions are omitted from the roll and the
        /// remaining authored weights are normalized automatically.
        /// </summary>
        private static IEnumerable<StrongboxRarityProfile> Rarities(
            int common,
            int rare,
            int epic,
            int legendary,
            int artifact)
        {
            int uncommon = Midpoint(common, rare);
            int mythic = Midpoint(legendary, artifact);
            return new[]
            {
                new StrongboxRarityProfile(
                    StrongboxDefinitionRarityIds.Common,
                    common,
                    2),
                new StrongboxRarityProfile(
                    StrongboxDefinitionRarityIds.Uncommon,
                    uncommon,
                    2),
                new StrongboxRarityProfile(
                    StrongboxDefinitionRarityIds.Rare,
                    rare,
                    1),
                new StrongboxRarityProfile(
                    StrongboxDefinitionRarityIds.Epic,
                    epic,
                    0),
                new StrongboxRarityProfile(
                    StrongboxDefinitionRarityIds.Legendary,
                    legendary,
                    -1),
                new StrongboxRarityProfile(
                    StrongboxDefinitionRarityIds.Mythic,
                    mythic,
                    -2),
                new StrongboxRarityProfile(
                    StrongboxDefinitionRarityIds.Artifact,
                    artifact,
                    -2),
            };
        }

        private static int Midpoint(int left, int right)
        {
            return checked((left + right + 1) / 2);
        }

        private static void AddIfPositive(
            ICollection<StrongboxWeightedIntOutcome> output,
            int value,
            ulong weight)
        {
            if (weight > 0UL)
            {
                output.Add(Outcome(value, weight));
            }
        }

        private static StrongboxWeightedIntOutcome Outcome(
            int value,
            ulong weight)
        {
            return new StrongboxWeightedIntOutcome(value, weight);
        }

        private static StrongboxDistanceWeight Distance(
            int distance,
            ulong weightMillionths)
        {
            return new StrongboxDistanceWeight(distance, weightMillionths);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// Authored hybrid loot-selection and augment-signature balance for the eleven
    /// production strongbox tiers. Equipment quality remains separate from the five
    /// normalized weapon-definition rarity bands.
    /// </summary>
    public static class ProductionStrongboxHybridLootCatalogV1
    {
        private static readonly ReadOnlyCollection<StrongboxWeightedIntOutcomeV1>
            InstanceLevelOffsetsValue =
                new ReadOnlyCollection<StrongboxWeightedIntOutcomeV1>(
                    new List<StrongboxWeightedIntOutcomeV1>
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

        private static readonly ReadOnlyCollection<StrongboxHybridLootPolicyV1>
            PoliciesValue = new ReadOnlyCollection<StrongboxHybridLootPolicyV1>(
                new List<StrongboxHybridLootPolicyV1>
                {
                    Policy(1, -8, -4, 0,
                        Slots(65, 28, 6, 1),
                        Levels(new[] { 1, 2, 3, 4, 5 }, new ulong[] { 45, 30, 15, 7, 3 }),
                        Rarities(1300, 500, 100, 10, 0)),
                    Policy(2, -7, -3, 1,
                        Slots(45, 35, 16, 4),
                        Levels(new[] { 1, 2, 3, 4, 5, 6 }, new ulong[] { 20, 30, 25, 15, 7, 3 }),
                        Rarities(1200, 650, 180, 20, 0)),
                    Policy(3, -6, -2, 2,
                        Slots(25, 38, 27, 10),
                        Levels(
                            new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                            new ulong[] { 1500, 2500, 2500, 1800, 1000, 500, 170, 25, 5 }),
                        Rarities(1100, 800, 300, 50, 0)),
                    Policy(4, -5, -1, 3,
                        Slots(10, 30, 38, 22),
                        Levels(
                            new[] { 3, 4, 5, 6, 7, 8, 9, 10 },
                            new ulong[] { 120, 200, 250, 200, 130, 70, 25, 5 }),
                        Rarities(1000, 1000, 500, 100, 0)),
                    Policy(5, -4, -1, 4,
                        Slots(0, 25, 45, 30),
                        Levels(
                            new[] { 4, 5, 6, 7, 8, 9, 10 },
                            new ulong[] { 10, 18, 24, 20, 15, 9, 4 }),
                        Rarities(900, 1150, 750, 200, 5)),
                    Policy(6, -3, 0, 3,
                        Slots(0, 10, 42, 48),
                        Levels(
                            new[] { 5, 6, 7, 8, 9, 10 },
                            new ulong[] { 8, 15, 20, 22, 23, 12 }),
                        Rarities(750, 1300, 1000, 400, 15)),
                    Policy(7, -2, 1, 4,
                        Slots(0, 0, 35, 65),
                        Levels(
                            new[] { 6, 7, 8, 9, 10 },
                            new ulong[] { 8, 14, 20, 30, 28 }),
                        Rarities(550, 1300, 1300, 750, 50)),
                    Policy(8, -1, 2, 5,
                        Slots(0, 0, 0, 100),
                        Levels(
                            new[] { 6, 7, 8, 9, 10 },
                            new ulong[] { 8, 12, 20, 25, 35 }),
                        Rarities(350, 1100, 1600, 1200, 150)),
                    Policy(9, 0, 3, 6,
                        Slots(0, 0, 0, 100),
                        Levels(
                            new[] { 8, 9, 10 },
                            new ulong[] { 8, 27, 65 }),
                        Rarities(200, 800, 1700, 1800, 400)),
                    Policy(10, 1, 4, 7,
                        SlotOutcomes(
                            new[] { 3, 4 },
                            new ulong[] { 97, 3 }),
                        Levels(
                            new[] { 9, 10, 11 },
                            new ulong[] { 8, 77, 15 }),
                        Rarities(100, 500, 1400, 2300, 1000)),
                    Policy(11, 3, 5, 7,
                        SlotOutcomes(
                            new[] { 3, 4 },
                            new ulong[] { 70, 30 }),
                        Levels(
                            new[] { 10, 11 },
                            new ulong[] { 30, 70 }),
                        Rarities(50, 250, 1000, 2500, 2500)),
                });

        public static IReadOnlyList<StrongboxHybridLootPolicyV1> Policies
        {
            get { return PoliciesValue; }
        }

        public static StrongboxHybridLootPolicyV1 GetByTierNumber(int tierNumber)
        {
            if (tierNumber < 1 || tierNumber > PoliciesValue.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(tierNumber));
            }
            return PoliciesValue[tierNumber - 1];
        }

        public static bool TryGet(
            StableId tierStableId,
            out StrongboxHybridLootPolicyV1 policy)
        {
            if (tierStableId != null)
            {
                for (int index = 0; index < PoliciesValue.Count; index++)
                {
                    ProductionStrongboxTierV1 tier =
                        ProductionStrongboxCatalogV1.GetByNumber(index + 1);
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

        private static StrongboxHybridLootPolicyV1 Policy(
            int tierNumber,
            int minimumDelta,
            int modeDelta,
            int maximumDelta,
            IEnumerable<StrongboxWeightedIntOutcomeV1> slots,
            IEnumerable<StrongboxWeightedIntOutcomeV1> levels,
            IEnumerable<StrongboxRarityProfileV1> rarities)
        {
            ProductionStrongboxTierV1 tier =
                ProductionStrongboxCatalogV1.GetByNumber(tierNumber);
            return StrongboxHybridLootPolicyV1.Create(
                StableId.Create("strongbox-hybrid-loot", tier.Slug + "-v1"),
                minimumDelta,
                modeDelta,
                maximumDelta,
                800,
                InstanceLevelOffsetsValue,
                slots,
                levels,
                rarities);
        }

        private static IEnumerable<StrongboxWeightedIntOutcomeV1> Slots(
            ulong zero,
            ulong one,
            ulong two,
            ulong three)
        {
            var values = new List<StrongboxWeightedIntOutcomeV1>();
            AddIfPositive(values, 0, zero);
            AddIfPositive(values, 1, one);
            AddIfPositive(values, 2, two);
            AddIfPositive(values, 3, three);
            return values;
        }

        private static IEnumerable<StrongboxWeightedIntOutcomeV1> SlotOutcomes(
            int[] values,
            ulong[] weights)
        {
            return Outcomes(values, weights);
        }

        private static IEnumerable<StrongboxWeightedIntOutcomeV1> Levels(
            int[] values,
            ulong[] weights)
        {
            return Outcomes(values, weights);
        }

        private static IEnumerable<StrongboxWeightedIntOutcomeV1> Outcomes(
            int[] values,
            ulong[] weights)
        {
            if (values == null || weights == null || values.Length != weights.Length)
            {
                throw new ArgumentException(
                    "Outcome values and weights must have matching lengths.");
            }

            var output = new List<StrongboxWeightedIntOutcomeV1>(values.Length);
            for (int index = 0; index < values.Length; index++)
            {
                output.Add(Outcome(values[index], weights[index]));
            }
            return output;
        }

        /// <summary>
        /// The original authored Common/Rare/Epic/Legendary/Artifact anchors remain
        /// unchanged. Uncommon definitions project into Common, while Mythic and
        /// Artifact share the highest MythicArtifact profile. Authored per-definition
        /// weights remain part of final selection.
        /// </summary>
        private static IEnumerable<StrongboxRarityProfileV1> Rarities(
            int common,
            int rare,
            int epic,
            int legendary,
            int mythicArtifact)
        {
            return new[]
            {
                new StrongboxRarityProfileV1(
                    StrongboxDefinitionRarityIdsV1.Common,
                    common,
                    2),
                new StrongboxRarityProfileV1(
                    StrongboxDefinitionRarityIdsV1.Rare,
                    rare,
                    1),
                new StrongboxRarityProfileV1(
                    StrongboxDefinitionRarityIdsV1.Epic,
                    epic,
                    0),
                new StrongboxRarityProfileV1(
                    StrongboxDefinitionRarityIdsV1.Legendary,
                    legendary,
                    -1),
                new StrongboxRarityProfileV1(
                    StrongboxDefinitionRarityIdsV1.MythicArtifact,
                    mythicArtifact,
                    -2),
            };
        }

        private static void AddIfPositive(
            ICollection<StrongboxWeightedIntOutcomeV1> output,
            int value,
            ulong weight)
        {
            if (weight > 0UL)
            {
                output.Add(Outcome(value, weight));
            }
        }

        private static StrongboxWeightedIntOutcomeV1 Outcome(
            int value,
            ulong weight)
        {
            return new StrongboxWeightedIntOutcomeV1(value, weight);
        }

    }
}

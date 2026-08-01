using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed partial class EnemyInstance
    {
        private bool traitsRolled;

        internal bool TraitsRolled { get { return traitsRolled; } }

        internal void MarkTraitsRolled()
        {
            traitsRolled = true;
        }
    }

    public sealed class EnemyTraitWeight
    {
        public EnemyTraitWeight(EnemyTrait trait, int weight)
        {
            if (!Enum.IsDefined(typeof(EnemyTrait), trait))
                throw new ArgumentOutOfRangeException(nameof(trait));
            if (weight < 1) throw new ArgumentOutOfRangeException(nameof(weight));

            Trait = trait;
            Weight = weight;
        }

        public EnemyTrait Trait { get; }
        public int Weight { get; }
    }

    public sealed class EnemyTraitTierCap
    {
        public EnemyTraitTierCap(int minimumTier, int maximumTraits)
        {
            if (minimumTier < 1)
                throw new ArgumentOutOfRangeException(nameof(minimumTier));
            if (maximumTraits < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumTraits));

            MinimumTier = minimumTier;
            MaximumTraits = maximumTraits;
        }

        public int MinimumTier { get; }
        public int MaximumTraits { get; }
    }

    /// <summary>
    /// Immutable balance data for deterministic enemy trait rolls.
    /// </summary>
    public sealed class EnemyTraitRollTable
    {
        private readonly ReadOnlyCollection<EnemyTraitWeight> weights;
        private readonly ReadOnlyCollection<EnemyTraitTierCap> tierCaps;

        public EnemyTraitRollTable(
            double firstTraitChance,
            double additionalTraitChance,
            IEnumerable<EnemyTraitWeight> configuredWeights,
            IEnumerable<EnemyTraitTierCap> configuredTierCaps)
        {
            RequireChance(firstTraitChance, nameof(firstTraitChance));
            RequireChance(additionalTraitChance, nameof(additionalTraitChance));

            var weightCopy = new List<EnemyTraitWeight>(
                configuredWeights
                    ?? throw new ArgumentNullException(nameof(configuredWeights)));
            if (weightCopy.Count == 0)
                throw new ArgumentException(
                    "A trait roll table requires at least one weighted trait.",
                    nameof(configuredWeights));

            var seenTraits = new HashSet<EnemyTrait>();
            for (int index = 0; index < weightCopy.Count; index++)
            {
                EnemyTraitWeight entry = weightCopy[index];
                if (entry == null)
                    throw new ArgumentException(
                        "Trait weights cannot contain null entries.",
                        nameof(configuredWeights));
                if (!seenTraits.Add(entry.Trait))
                    throw new ArgumentException(
                        "Trait weights must contain each trait at most once.",
                        nameof(configuredWeights));
            }
            weightCopy.Sort((left, right) => left.Trait.CompareTo(right.Trait));

            var capCopy = new List<EnemyTraitTierCap>(
                configuredTierCaps
                    ?? throw new ArgumentNullException(nameof(configuredTierCaps)));
            if (capCopy.Count == 0)
                throw new ArgumentException(
                    "A trait roll table requires at least one tier cap.",
                    nameof(configuredTierCaps));
            capCopy.Sort((left, right) =>
                left.MinimumTier.CompareTo(right.MinimumTier));

            int previousTier = 0;
            int previousMaximum = -1;
            for (int index = 0; index < capCopy.Count; index++)
            {
                EnemyTraitTierCap cap = capCopy[index];
                if (cap == null)
                    throw new ArgumentException(
                        "Trait tier caps cannot contain null entries.",
                        nameof(configuredTierCaps));
                if (cap.MinimumTier <= previousTier)
                    throw new ArgumentException(
                        "Trait tier caps must use unique increasing tiers.",
                        nameof(configuredTierCaps));
                if (cap.MaximumTraits < previousMaximum)
                    throw new ArgumentException(
                        "Trait tier caps cannot decrease at higher tiers.",
                        nameof(configuredTierCaps));

                previousTier = cap.MinimumTier;
                previousMaximum = cap.MaximumTraits;
            }

            FirstTraitChance = firstTraitChance;
            AdditionalTraitChance = additionalTraitChance;
            weights = new ReadOnlyCollection<EnemyTraitWeight>(weightCopy);
            tierCaps = new ReadOnlyCollection<EnemyTraitTierCap>(capCopy);
        }

        public double FirstTraitChance { get; }
        public double AdditionalTraitChance { get; }
        public IReadOnlyList<EnemyTraitWeight> Weights { get { return weights; } }
        public IReadOnlyList<EnemyTraitTierCap> TierCaps { get { return tierCaps; } }

        public int MaximumTraits(int tier)
        {
            if (tier < 1) throw new ArgumentOutOfRangeException(nameof(tier));

            int maximum = 0;
            for (int index = 0; index < tierCaps.Count; index++)
            {
                EnemyTraitTierCap cap = tierCaps[index];
                if (tier < cap.MinimumTier) break;
                maximum = cap.MaximumTraits;
            }
            return Math.Min(maximum, weights.Count);
        }

        private static void RequireChance(double value, string parameterName)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value < 0d
                || value > 1d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public static class EnemyTraitRollTables
    {
        public static EnemyTraitRollTable CreateDefault()
        {
            return new EnemyTraitRollTable(
                0.20d,
                0.35d,
                new[]
                {
                    new EnemyTraitWeight(EnemyTrait.EnergyShielded, 20),
                    new EnemyTraitWeight(EnemyTrait.Fortified, 15),
                    new EnemyTraitWeight(EnemyTrait.Golden, 10),
                    new EnemyTraitWeight(EnemyTrait.Swift, 20),
                    new EnemyTraitWeight(EnemyTrait.Overclocked, 20),
                    new EnemyTraitWeight(EnemyTrait.Volatile, 15),
                },
                new[]
                {
                    new EnemyTraitTierCap(1, 1),
                    new EnemyTraitTierCap(2, 2),
                    new EnemyTraitTierCap(3, 3),
                    new EnemyTraitTierCap(4, 4),
                });
        }
    }

    /// <summary>
    /// Assigns weighted traits before combat. The same run, room, placement and lifecycle
    /// always produce the same roll sequence.
    /// </summary>
    public static class EnemyTraitRoller
    {
        public static int Roll(
            EnemyInstance enemy,
            EnemyTraitRollTable table)
        {
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (enemy.TraitsRolled) return 0;

            int maximum = table.MaximumTraits(enemy.Tier);
            if (enemy.Traits.Count >= maximum)
            {
                enemy.MarkTraitsRolled();
                return 0;
            }

            var random = new EnemyTraitRandom(BuildSeed(enemy));
            int added = 0;
            while (enemy.Traits.Count < maximum)
            {
                double chance = enemy.Traits.Count == 0
                    ? table.FirstTraitChance
                    : table.AdditionalTraitChance;
                if (random.NextUnit() >= chance) break;

                EnemyTrait trait;
                if (!TryPick(enemy, table.Weights, random, out trait)) break;
                if (enemy.AssignTrait(trait)) added++;
            }

            enemy.MarkTraitsRolled();
            return added;
        }

        private static bool TryPick(
            EnemyInstance enemy,
            IReadOnlyList<EnemyTraitWeight> weights,
            EnemyTraitRandom random,
            out EnemyTrait trait)
        {
            trait = default(EnemyTrait);
            int total = 0;
            for (int index = 0; index < weights.Count; index++)
            {
                EnemyTraitWeight entry = weights[index];
                if (enemy.HasTrait(entry.Trait)) continue;
                total = checked(total + entry.Weight);
            }
            if (total == 0) return false;

            int roll = random.Next(total);
            for (int index = 0; index < weights.Count; index++)
            {
                EnemyTraitWeight entry = weights[index];
                if (enemy.HasTrait(entry.Trait)) continue;
                if (roll < entry.Weight)
                {
                    trait = entry.Trait;
                    return true;
                }
                roll -= entry.Weight;
            }
            throw new InvalidOperationException(
                "The weighted enemy trait roll could not resolve its selected entry.");
        }

        private static string BuildSeed(EnemyInstance enemy)
        {
            return "enemy-trait-roll-v1|"
                + enemy.Request.RunStableId + "|"
                + enemy.RoomStableId + "|"
                + enemy.PlacementStableId + "|"
                + enemy.LifecycleGeneration.ToString(CultureInfo.InvariantCulture) + "|"
                + enemy.Tier.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class EnemyTraitRandom
    {
        private ulong state;

        public EnemyTraitRandom(string seed)
        {
            if (string.IsNullOrEmpty(seed))
                throw new ArgumentException("A deterministic trait seed is required.", nameof(seed));

            state = 14695981039346656037UL;
            unchecked
            {
                for (int index = 0; index < seed.Length; index++)
                {
                    state ^= seed[index];
                    state *= 1099511628211UL;
                }
            }
            if (state == 0UL) state = 0x9E3779B97F4A7C15UL;
        }

        public int Next(int maximumExclusive)
        {
            if (maximumExclusive < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
            return (int)(NextUInt64() % (ulong)maximumExclusive);
        }

        public double NextUnit()
        {
            return (NextUInt64() >> 11)
                * (1d / 9007199254740992d);
        }

        private ulong NextUInt64()
        {
            unchecked
            {
                state ^= state >> 12;
                state ^= state << 25;
                state ^= state >> 27;
                return state * 2685821657736338717UL;
            }
        }
    }
}

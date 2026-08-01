using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed partial class EnemyInstance
    {
        private bool traitsRolled;

        internal bool TraitsRolled { get { return traitsRolled; } }

        internal void CompleteTraitRoll()
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

    /// <summary>Immutable balance data for deterministic enemy trait rolls.</summary>
    public sealed class EnemyTraitRollTable
    {
        private readonly List<EnemyTraitWeight> weights;
        private readonly List<int> maxTraitsByTier;

        public EnemyTraitRollTable(
            double firstChance,
            double extraChance,
            IEnumerable<EnemyTraitWeight> configuredWeights,
            IEnumerable<int> configuredMaxTraitsByTier)
        {
            RequireChance(firstChance, nameof(firstChance));
            RequireChance(extraChance, nameof(extraChance));

            weights = new List<EnemyTraitWeight>(
                configuredWeights
                    ?? throw new ArgumentNullException(nameof(configuredWeights)));
            if (weights.Count == 0)
                throw new ArgumentException(
                    "A trait roll table requires weighted traits.",
                    nameof(configuredWeights));

            var seen = new HashSet<EnemyTrait>();
            for (int index = 0; index < weights.Count; index++)
            {
                EnemyTraitWeight entry = weights[index];
                if (entry == null || !seen.Add(entry.Trait))
                    throw new ArgumentException(
                        "Trait weights must be non-null and unique.",
                        nameof(configuredWeights));
            }
            weights.Sort((left, right) => left.Trait.CompareTo(right.Trait));

            maxTraitsByTier = new List<int>(
                configuredMaxTraitsByTier
                    ?? throw new ArgumentNullException(
                        nameof(configuredMaxTraitsByTier)));
            if (maxTraitsByTier.Count == 0)
                throw new ArgumentException(
                    "A trait roll table requires tier caps.",
                    nameof(configuredMaxTraitsByTier));

            int previous = -1;
            for (int index = 0; index < maxTraitsByTier.Count; index++)
            {
                int maximum = maxTraitsByTier[index];
                if (maximum < 0 || maximum < previous)
                    throw new ArgumentException(
                        "Trait tier caps must be non-negative and non-decreasing.",
                        nameof(configuredMaxTraitsByTier));
                previous = maximum;
            }

            FirstChance = firstChance;
            ExtraChance = extraChance;
        }

        public double FirstChance { get; }
        public double ExtraChance { get; }
        public IReadOnlyList<EnemyTraitWeight> Weights { get { return weights; } }
        public IReadOnlyList<int> MaxTraitsByTier { get { return maxTraitsByTier; } }

        public int MaxTraits(int tier)
        {
            if (tier < 1) throw new ArgumentOutOfRangeException(nameof(tier));
            int index = Math.Min(tier, maxTraitsByTier.Count) - 1;
            return Math.Min(maxTraitsByTier[index], weights.Count);
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
                new[] { 1, 2, 3, 4 });
        }
    }

    /// <summary>
    /// The same run, room, placement and lifecycle always produce the same trait sequence.
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

            int maximum = table.MaxTraits(enemy.Tier);
            int added = 0;
            if (enemy.Traits.Count < maximum)
            {
                var random = new EnemyTraitRandom(Seed(enemy));
                while (enemy.Traits.Count < maximum)
                {
                    double chance = enemy.Traits.Count == 0
                        ? table.FirstChance
                        : table.ExtraChance;
                    if (random.NextUnit() >= chance) break;

                    EnemyTrait trait;
                    if (!TryPick(enemy, table.Weights, random, out trait)) break;
                    if (enemy.AssignTrait(trait)) added++;
                }
            }

            enemy.CompleteTraitRoll();
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
                if (!enemy.HasTrait(entry.Trait))
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
                "The weighted trait roll could not resolve its selected entry.");
        }

        private static string Seed(EnemyInstance enemy)
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
                throw new ArgumentException(
                    "A deterministic trait seed is required.",
                    nameof(seed));

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

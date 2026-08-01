using System;
using System.Collections.Generic;
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

    public sealed class TraitWeight
    {
        public TraitWeight(EnemyTrait trait, int weight)
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

    /// <summary>Trait chances, weights, and tier limits.</summary>
    public sealed class TraitRules
    {
        private readonly List<TraitWeight> weights;
        private readonly List<int> maxTraitsByTier;

        public TraitRules(
            double firstChance,
            double extraChance,
            IEnumerable<TraitWeight> traitWeights,
            IEnumerable<int> tierLimits)
        {
            RequireChance(firstChance, nameof(firstChance));
            RequireChance(extraChance, nameof(extraChance));

            weights = new List<TraitWeight>(
                traitWeights ?? throw new ArgumentNullException(nameof(traitWeights)));
            if (weights.Count == 0)
                throw new ArgumentException(
                    "Trait rules require weighted traits.",
                    nameof(traitWeights));

            var seen = new HashSet<EnemyTrait>();
            for (int index = 0; index < weights.Count; index++)
            {
                TraitWeight entry = weights[index];
                if (entry == null || !seen.Add(entry.Trait))
                    throw new ArgumentException(
                        "Trait weights must be non-null and unique.",
                        nameof(traitWeights));
            }
            weights.Sort((left, right) => left.Trait.CompareTo(right.Trait));

            maxTraitsByTier = new List<int>(
                tierLimits ?? throw new ArgumentNullException(nameof(tierLimits)));
            if (maxTraitsByTier.Count == 0)
                throw new ArgumentException(
                    "Trait rules require tier limits.",
                    nameof(tierLimits));

            int previous = -1;
            for (int index = 0; index < maxTraitsByTier.Count; index++)
            {
                int maximum = maxTraitsByTier[index];
                if (maximum < 0 || maximum < previous)
                    throw new ArgumentException(
                        "Trait limits must stay level or increase with tier.",
                        nameof(tierLimits));
                previous = maximum;
            }

            FirstChance = firstChance;
            ExtraChance = extraChance;
        }

        public static TraitRules Default { get; } = new TraitRules(
            0.20d,
            0.35d,
            new[]
            {
                new TraitWeight(EnemyTrait.EnergyShielded, 20),
                new TraitWeight(EnemyTrait.Fortified, 15),
                new TraitWeight(EnemyTrait.Golden, 10),
                new TraitWeight(EnemyTrait.Swift, 20),
                new TraitWeight(EnemyTrait.Overclocked, 20),
                new TraitWeight(EnemyTrait.Volatile, 15),
            },
            new[] { 1, 2, 3, 4 });

        public double FirstChance { get; }
        public double ExtraChance { get; }
        public IReadOnlyList<TraitWeight> Weights { get { return weights; } }
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

    /// <summary>The same enemy lifecycle always gets the same trait sequence.</summary>
    public static class TraitRoller
    {
        public static int Roll(
            EnemyInstance enemy,
            TraitRules rules)
        {
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (enemy.TraitsRolled) return 0;

            int maximum = rules.MaxTraits(enemy.Tier);
            int added = 0;
            if (enemy.Traits.Count < maximum)
            {
                var random = new TraitRandom(Seed(enemy));
                while (enemy.Traits.Count < maximum)
                {
                    double chance = enemy.Traits.Count == 0
                        ? rules.FirstChance
                        : rules.ExtraChance;
                    if (random.NextChance() >= chance) break;

                    EnemyTrait trait;
                    if (!TryPick(enemy, rules.Weights, random, out trait)) break;
                    if (enemy.AssignTrait(trait)) added++;
                }
            }

            enemy.MarkTraitsRolled();
            return added;
        }

        private static bool TryPick(
            EnemyInstance enemy,
            IReadOnlyList<TraitWeight> weights,
            TraitRandom random,
            out EnemyTrait trait)
        {
            trait = default(EnemyTrait);
            int total = 0;
            for (int index = 0; index < weights.Count; index++)
            {
                TraitWeight entry = weights[index];
                if (!enemy.HasTrait(entry.Trait))
                    total = checked(total + entry.Weight);
            }
            if (total == 0) return false;

            int roll = random.Next(total);
            for (int index = 0; index < weights.Count; index++)
            {
                TraitWeight entry = weights[index];
                if (enemy.HasTrait(entry.Trait)) continue;
                if (roll < entry.Weight)
                {
                    trait = entry.Trait;
                    return true;
                }
                roll -= entry.Weight;
            }
            throw new InvalidOperationException(
                "The weighted trait roll could not pick a trait.");
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

    internal sealed class TraitRandom
    {
        private ulong state;

        public TraitRandom(string seed)
        {
            if (string.IsNullOrEmpty(seed))
                throw new ArgumentException(
                    "A trait seed is required.",
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

        public int Next(int max)
        {
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(max));
            return (int)(NextUInt64() % (ulong)max);
        }

        public double NextChance()
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

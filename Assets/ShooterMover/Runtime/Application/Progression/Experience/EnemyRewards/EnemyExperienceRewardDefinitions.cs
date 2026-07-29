using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Progression.Experience.EnemyRewards
{
    public static class EnemyExperienceRewardIds
    {
        public const int MinimumEnemyLevel = 1;
        public const int MaximumEnemyLevel = 100;

        public static readonly StableId BlasterTurret =
            StableId.Parse("enemy.blaster-turret");
        public static readonly StableId MobileBlasterDroid =
            StableId.Parse("enemy.mobile-blaster-droid");
        public static readonly StableId PursuerDrone =
            StableId.Parse("enemy.pursuer-drone");
        public static readonly StableId RamDroid =
            StableId.Parse("enemy.ram-droid");

        private static readonly ReadOnlyCollection<StableId> KnownEnemiesValue =
            Array.AsReadOnly(
                new[]
                {
                    BlasterTurret,
                    MobileBlasterDroid,
                    PursuerDrone,
                    RamDroid,
                });

        public static IReadOnlyList<StableId> KnownEnemies
        {
            get { return KnownEnemiesValue; }
        }
    }

    /// <summary>
    /// One inclusive enemy-level interval with one authored XP amount. Definitions
    /// use contiguous bands so every supported level resolves deterministically.
    /// </summary>
    public sealed class EnemyExperienceRewardBand
    {
        public EnemyExperienceRewardBand(
            int minimumEnemyLevel,
            int maximumEnemyLevel,
            long experienceAmount)
        {
            if (minimumEnemyLevel < EnemyExperienceRewardIds.MinimumEnemyLevel
                || minimumEnemyLevel > EnemyExperienceRewardIds.MaximumEnemyLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumEnemyLevel));
            }

            if (maximumEnemyLevel < minimumEnemyLevel
                || maximumEnemyLevel > EnemyExperienceRewardIds.MaximumEnemyLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumEnemyLevel));
            }

            if (experienceAmount < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(experienceAmount),
                    experienceAmount,
                    "Enemy XP may be zero, but it cannot be negative.");
            }

            MinimumEnemyLevel = minimumEnemyLevel;
            MaximumEnemyLevel = maximumEnemyLevel;
            ExperienceAmount = experienceAmount;
        }

        public int MinimumEnemyLevel { get; }

        public int MaximumEnemyLevel { get; }

        public long ExperienceAmount { get; }

        public bool Contains(int enemyLevel)
        {
            return enemyLevel >= MinimumEnemyLevel
                && enemyLevel <= MaximumEnemyLevel;
        }
    }

    /// <summary>
    /// Reusable future-enemy contract. Implementations must return one non-negative
    /// amount for every enemy level from 1 through 100.
    /// </summary>
    public interface IEnemyExperienceRewardDefinition
    {
        StableId EnemyDefinitionStableId { get; }

        long GetExperienceAmount(int enemyLevel);
    }

    public sealed class EnemyExperienceRewardDefinition :
        IEnemyExperienceRewardDefinition
    {
        private readonly ReadOnlyCollection<EnemyExperienceRewardBand> bands;

        public EnemyExperienceRewardDefinition(
            StableId enemyDefinitionStableId,
            IEnumerable<EnemyExperienceRewardBand> rewardBands)
        {
            EnemyDefinitionStableId = enemyDefinitionStableId
                ?? throw new ArgumentNullException(nameof(enemyDefinitionStableId));
            if (rewardBands == null)
            {
                throw new ArgumentNullException(nameof(rewardBands));
            }

            var copy = new List<EnemyExperienceRewardBand>();
            foreach (EnemyExperienceRewardBand band in rewardBands)
            {
                if (band == null)
                {
                    throw new ArgumentException(
                        "Enemy XP reward bands cannot contain null.",
                        nameof(rewardBands));
                }

                copy.Add(band);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "An enemy XP definition requires at least one level band.",
                    nameof(rewardBands));
            }

            copy.Sort(CompareBands);
            int expectedMinimum = EnemyExperienceRewardIds.MinimumEnemyLevel;
            for (int index = 0; index < copy.Count; index++)
            {
                EnemyExperienceRewardBand band = copy[index];
                if (band.MinimumEnemyLevel != expectedMinimum)
                {
                    throw new ArgumentException(
                        "Enemy XP bands must be contiguous, non-overlapping, and begin at level 1.",
                        nameof(rewardBands));
                }

                expectedMinimum = band.MaximumEnemyLevel + 1;
            }

            if (expectedMinimum != EnemyExperienceRewardIds.MaximumEnemyLevel + 1)
            {
                throw new ArgumentException(
                    "Enemy XP bands must provide a value through level 100.",
                    nameof(rewardBands));
            }

            bands = new ReadOnlyCollection<EnemyExperienceRewardBand>(copy);
        }

        public StableId EnemyDefinitionStableId { get; }

        public IReadOnlyList<EnemyExperienceRewardBand> Bands
        {
            get { return bands; }
        }

        public long GetExperienceAmount(int enemyLevel)
        {
            RequireEnemyLevel(enemyLevel);
            for (int index = 0; index < bands.Count; index++)
            {
                EnemyExperienceRewardBand band = bands[index];
                if (band.Contains(enemyLevel))
                {
                    return band.ExperienceAmount;
                }
            }

            throw new InvalidOperationException(
                "A validated enemy XP definition did not resolve the requested level.");
        }

        public static void RequireEnemyLevel(int enemyLevel)
        {
            if (enemyLevel < EnemyExperienceRewardIds.MinimumEnemyLevel
                || enemyLevel > EnemyExperienceRewardIds.MaximumEnemyLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemyLevel),
                    enemyLevel,
                    "Enemy level must be between 1 and 100.");
            }
        }

        private static int CompareBands(
            EnemyExperienceRewardBand left,
            EnemyExperienceRewardBand right)
        {
            int minimumComparison = left.MinimumEnemyLevel.CompareTo(
                right.MinimumEnemyLevel);
            if (minimumComparison != 0)
            {
                return minimumComparison;
            }

            return left.MaximumEnemyLevel.CompareTo(right.MaximumEnemyLevel);
        }
    }

    public sealed class EnemyExperienceRewardCatalog
    {
        private readonly Dictionary<string, IEnemyExperienceRewardDefinition>
            definitionsById;

        public EnemyExperienceRewardCatalog(
            IEnumerable<IEnemyExperienceRewardDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            definitionsById =
                new Dictionary<string, IEnemyExperienceRewardDefinition>(
                    StringComparer.Ordinal);
            foreach (IEnemyExperienceRewardDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Enemy XP catalog definitions cannot contain null.",
                        nameof(definitions));
                }

                StableId definitionId = definition.EnemyDefinitionStableId;
                if (definitionId == null)
                {
                    throw new ArgumentException(
                        "Enemy XP definitions require a stable enemy definition ID.",
                        nameof(definitions));
                }

                for (int level = EnemyExperienceRewardIds.MinimumEnemyLevel;
                    level <= EnemyExperienceRewardIds.MaximumEnemyLevel;
                    level++)
                {
                    long amount = definition.GetExperienceAmount(level);
                    if (amount < 0L)
                    {
                        throw new ArgumentException(
                            "Enemy XP definitions cannot return negative values.",
                            nameof(definitions));
                    }
                }

                string key = definitionId.ToString();
                if (definitionsById.ContainsKey(key))
                {
                    throw new ArgumentException(
                        "Enemy XP catalog definition IDs must be unique.",
                        nameof(definitions));
                }

                definitionsById.Add(key, definition);
            }

            if (definitionsById.Count == 0)
            {
                throw new ArgumentException(
                    "Enemy XP catalog requires at least one definition.",
                    nameof(definitions));
            }
        }

        public int DefinitionCount
        {
            get { return definitionsById.Count; }
        }

        public bool TryResolve(
            StableId enemyDefinitionStableId,
            int enemyLevel,
            out long experienceAmount)
        {
            EnemyExperienceRewardDefinition.RequireEnemyLevel(enemyLevel);
            experienceAmount = 0L;
            if (enemyDefinitionStableId == null)
            {
                return false;
            }

            IEnemyExperienceRewardDefinition definition;
            if (!definitionsById.TryGetValue(
                enemyDefinitionStableId.ToString(),
                out definition))
            {
                return false;
            }

            experienceAmount = definition.GetExperienceAmount(enemyLevel);
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    public sealed class StrongboxMetricDifference
    {
        public StrongboxMetricDifference(
            string metricId,
            double primaryValue,
            double comparisonValue)
        {
            MetricId = metricId ?? string.Empty;
            PrimaryValue = primaryValue;
            ComparisonValue = comparisonValue;
            AbsoluteDifference = comparisonValue - primaryValue;
            RelativeMultiplier = primaryValue == 0d
                ? (comparisonValue == 0d ? 1d : double.PositiveInfinity)
                : comparisonValue / primaryValue;
        }

        public string MetricId { get; }
        public double PrimaryValue { get; }
        public double ComparisonValue { get; }
        public double AbsoluteDifference { get; }
        public double RelativeMultiplier { get; }
    }

    public sealed class StrongboxSimulationComparison
    {
        internal StrongboxSimulationComparison(
            StrongboxSimulationReport primary,
            StrongboxSimulationReport comparison,
            IReadOnlyList<StrongboxMetricDifference> metrics)
        {
            Primary = primary ?? throw new ArgumentNullException(nameof(primary));
            Comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        public StrongboxSimulationReport Primary { get; }
        public StrongboxSimulationReport Comparison { get; }
        public IReadOnlyList<StrongboxMetricDifference> Metrics { get; }
    }

    public sealed class StrongboxSweepEntry
    {
        internal StrongboxSweepEntry(
            int playerLevel,
            StableId strongboxTierId,
            StrongboxSimulationReport report,
            IReadOnlyList<string> warnings)
        {
            PlayerLevel = playerLevel;
            StrongboxTierId = strongboxTierId
                ?? throw new ArgumentNullException(nameof(strongboxTierId));
            Report = report ?? throw new ArgumentNullException(nameof(report));
            Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
        }

        public int PlayerLevel { get; }
        public StableId StrongboxTierId { get; }
        public StrongboxSimulationReport Report { get; }
        public IReadOnlyList<string> Warnings { get; }
    }

    public sealed class StrongboxSimulationSweep
    {
        internal StrongboxSimulationSweep(
            StrongboxSimulationMode mode,
            IReadOnlyList<StrongboxSweepEntry> entries)
        {
            Mode = mode;
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        public StrongboxSimulationMode Mode { get; }
        public IReadOnlyList<StrongboxSweepEntry> Entries { get; }
    }

    public sealed class StrongboxDiagnosticThresholds
    {
        public StrongboxDiagnosticThresholds(
            double probabilityCliffPercentagePoints = 1d,
            double relativeRegressionMultiplier = 0.8d,
            int zeroObservationRunLength = 3)
        {
            if (probabilityCliffPercentagePoints < 0d)
                throw new ArgumentOutOfRangeException(nameof(probabilityCliffPercentagePoints));
            if (relativeRegressionMultiplier < 0d)
                throw new ArgumentOutOfRangeException(nameof(relativeRegressionMultiplier));
            if (zeroObservationRunLength < 1)
                throw new ArgumentOutOfRangeException(nameof(zeroObservationRunLength));
            ProbabilityCliffPercentagePoints = probabilityCliffPercentagePoints;
            RelativeRegressionMultiplier = relativeRegressionMultiplier;
            ZeroObservationRunLength = zeroObservationRunLength;
        }

        public double ProbabilityCliffPercentagePoints { get; }
        public double RelativeRegressionMultiplier { get; }
        public int ZeroObservationRunLength { get; }
    }

    /// <summary>
    /// Multi-scenario analysis only. Every underlying sample is still produced by the
    /// supplied production gateway; this coordinator owns no loot formulas.
    /// </summary>
    public sealed class StrongboxSimulationCoordinator
    {
        private readonly StrongboxBatchSimulator simulator;

        public StrongboxSimulationCoordinator(StrongboxBatchSimulator simulator = null)
        {
            this.simulator = simulator ?? new StrongboxBatchSimulator();
        }

        public StrongboxSimulationComparison Compare(
            StrongboxSimulationScenario primary,
            StrongboxSimulationScenario comparison,
            IStrongboxSimulationProductionGateway production)
        {
            if (primary == null) throw new ArgumentNullException(nameof(primary));
            if (comparison == null) throw new ArgumentNullException(nameof(comparison));
            if (production == null) throw new ArgumentNullException(nameof(production));

            StrongboxSimulationReport left = simulator.Run(
                SingleScenarioRequest(primary),
                production);
            StrongboxSimulationReport right = simulator.Run(
                SingleScenarioRequest(comparison),
                production);

            var metrics = new List<StrongboxMetricDifference>
            {
                Difference("generation-rate-percent", GenerationRate(left), GenerationRate(right)),
                Difference("average-item-level", WeightedMean(left.ItemLevels), WeightedMean(right.ItemLevels)),
                Difference("average-augment-slots", WeightedMean(left.AugmentSlots), WeightedMean(right.AugmentSlots)),
                Difference("average-augment-level", WeightedMean(left.AugmentLevels), WeightedMean(right.AugmentLevels)),
                Difference("exceptional-slot-percent", ExceptionalSlotPercent(left), ExceptionalSlotPercent(right)),
                Difference("over-ordinary-augment-level-percent", ExceptionalLevelPercent(left), ExceptionalLevelPercent(right)),
                Difference("level-11-percent", DistributionPercent(left.AugmentLevels, "11"), DistributionPercent(right.AugmentLevels, "11")),
                Difference("level-12-percent", DistributionPercent(left.AugmentLevels, "12"), DistributionPercent(right.AugmentLevels, "12")),
            };
            metrics.Sort(delegate(StrongboxMetricDifference a, StrongboxMetricDifference b)
            {
                return string.CompareOrdinal(a.MetricId, b.MetricId);
            });
            return new StrongboxSimulationComparison(
                left,
                right,
                new ReadOnlyCollection<StrongboxMetricDifference>(metrics));
        }

        public StrongboxSimulationSweep SweepPlayerLevels(
            int minimumPlayerLevel,
            int maximumPlayerLevel,
            StableId strongboxTierId,
            int openingsPerLevel,
            ulong rootSeed,
            IStrongboxSimulationProductionGateway production,
            StableId equipmentDefinitionId = null,
            StrongboxDiagnosticThresholds thresholds = null)
        {
            if (minimumPlayerLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumPlayerLevel));
            if (maximumPlayerLevel < minimumPlayerLevel)
                throw new ArgumentOutOfRangeException(nameof(maximumPlayerLevel));
            if (strongboxTierId == null)
                throw new ArgumentNullException(nameof(strongboxTierId));
            thresholds = thresholds ?? new StrongboxDiagnosticThresholds();

            var entries = new List<StrongboxSweepEntry>();
            StrongboxSimulationReport previous = null;
            int consecutiveZero = 0;
            for (int level = minimumPlayerLevel; level <= maximumPlayerLevel; level++)
            {
                var scenario = new StrongboxSimulationScenario(
                    level,
                    strongboxTierId,
                    openingsPerLevel,
                    rootSeed,
                    equipmentDefinitionId);
                StrongboxSimulationReport report = simulator.Run(
                    SingleScenarioRequest(scenario),
                    production);
                IReadOnlyList<string> warnings = SweepWarnings(
                    previous,
                    report,
                    thresholds,
                    ref consecutiveZero);
                entries.Add(new StrongboxSweepEntry(
                    level,
                    strongboxTierId,
                    report,
                    warnings));
                previous = report;
            }
            return new StrongboxSimulationSweep(
                StrongboxSimulationMode.PlayerLevelSweep,
                new ReadOnlyCollection<StrongboxSweepEntry>(entries));
        }

        public StrongboxSimulationSweep SweepStrongboxTiers(
            int playerLevel,
            IReadOnlyList<StableId> strongboxTierIds,
            int openingsPerTier,
            ulong rootSeed,
            IStrongboxSimulationProductionGateway production,
            StableId equipmentDefinitionId = null,
            StrongboxDiagnosticThresholds thresholds = null)
        {
            if (playerLevel < 0) throw new ArgumentOutOfRangeException(nameof(playerLevel));
            if (strongboxTierIds == null) throw new ArgumentNullException(nameof(strongboxTierIds));
            if (strongboxTierIds.Count == 0)
                throw new ArgumentException("At least one tier is required.", nameof(strongboxTierIds));
            thresholds = thresholds ?? new StrongboxDiagnosticThresholds();

            var entries = new List<StrongboxSweepEntry>();
            StrongboxSimulationReport previous = null;
            int consecutiveZero = 0;
            for (int index = 0; index < strongboxTierIds.Count; index++)
            {
                StableId tierId = strongboxTierIds[index]
                    ?? throw new ArgumentException("Tier identities cannot contain null.", nameof(strongboxTierIds));
                var scenario = new StrongboxSimulationScenario(
                    playerLevel,
                    tierId,
                    openingsPerTier,
                    rootSeed,
                    equipmentDefinitionId);
                StrongboxSimulationReport report = simulator.Run(
                    SingleScenarioRequest(scenario),
                    production);
                var warnings = new List<string>(SweepWarnings(
                    previous,
                    report,
                    thresholds,
                    ref consecutiveZero));
                if (previous != null
                    && WeightedMean(report.ItemLevels) < WeightedMean(previous.ItemLevels)
                    && WeightedMean(report.AugmentSlots) < WeightedMean(previous.AugmentSlots)
                    && WeightedMean(report.AugmentLevels) < WeightedMean(previous.AugmentLevels))
                {
                    warnings.Add("suspicious-strongbox-tier-inversion");
                }
                warnings.Sort(StringComparer.Ordinal);
                entries.Add(new StrongboxSweepEntry(
                    playerLevel,
                    tierId,
                    report,
                    new ReadOnlyCollection<string>(warnings)));
                previous = report;
            }
            return new StrongboxSimulationSweep(
                StrongboxSimulationMode.StrongboxTierSweep,
                new ReadOnlyCollection<StrongboxSweepEntry>(entries));
        }

        private static StrongboxSimulationRequest SingleScenarioRequest(
            StrongboxSimulationScenario scenario)
        {
            return new StrongboxSimulationRequest(
                scenario.EquipmentDefinitionId == null
                    ? StrongboxSimulationMode.FullOpening
                    : StrongboxSimulationMode.DefinitionConditioned,
                scenario);
        }

        private static IReadOnlyList<string> SweepWarnings(
            StrongboxSimulationReport previous,
            StrongboxSimulationReport current,
            StrongboxDiagnosticThresholds thresholds,
            ref int consecutiveZero)
        {
            var warnings = new List<string>();
            if (current.GeneratedCount == 0)
            {
                consecutiveZero++;
                if (consecutiveZero >= thresholds.ZeroObservationRunLength)
                    warnings.Add("long-zero-observation-region");
            }
            else
            {
                consecutiveZero = 0;
            }

            if (previous != null)
            {
                double previousRate = GenerationRate(previous);
                double currentRate = GenerationRate(current);
                if (Math.Abs(currentRate - previousRate)
                    >= thresholds.ProbabilityCliffPercentagePoints)
                    warnings.Add("major-generation-rate-cliff");
                if (previousRate > 0d
                    && currentRate / previousRate < thresholds.RelativeRegressionMultiplier)
                    warnings.Add("unexpected-probability-regression");
                if (WeightedMean(current.AugmentSlots)
                    < WeightedMean(previous.AugmentSlots)
                    * thresholds.RelativeRegressionMultiplier)
                    warnings.Add("augment-slot-quality-regression");
                if (WeightedMean(current.AugmentLevels)
                    < WeightedMean(previous.AugmentLevels)
                    * thresholds.RelativeRegressionMultiplier)
                    warnings.Add("augment-level-quality-regression");
            }
            warnings.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(warnings);
        }

        private static StrongboxMetricDifference Difference(
            string metricId,
            double primary,
            double comparison)
        {
            return new StrongboxMetricDifference(metricId, primary, comparison);
        }

        private static double GenerationRate(StrongboxSimulationReport report)
        {
            long total = report.GeneratedCount + report.RejectedCount;
            return total == 0L ? 0d : 100d * report.GeneratedCount / total;
        }

        private static double WeightedMean(
            IReadOnlyList<StrongboxDistributionEntry> values)
        {
            long count = 0L;
            double total = 0d;
            for (int index = 0; index < values.Count; index++)
            {
                int key;
                if (!int.TryParse(
                        values[index].Key,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out key))
                    continue;
                count += values[index].Count;
                total += key * (double)values[index].Count;
            }
            return count == 0L ? 0d : total / count;
        }

        private static double ExceptionalSlotPercent(StrongboxSimulationReport report)
        {
            long count = 0L;
            for (int index = 0; index < report.Equipment.Count; index++)
                count += report.Equipment[index].ExceptionalSlots;
            return report.GeneratedCount == 0L
                ? 0d
                : 100d * count / report.GeneratedCount;
        }

        private static double ExceptionalLevelPercent(StrongboxSimulationReport report)
        {
            long count = 0L;
            for (int index = 0; index < report.Equipment.Count; index++)
                count += report.Equipment[index].ExceptionalAugmentLevels;
            return report.GeneratedCount == 0L
                ? 0d
                : 100d * count / report.GeneratedCount;
        }

        private static double DistributionPercent(
            IReadOnlyList<StrongboxDistributionEntry> values,
            string key)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index].Key, key, StringComparison.Ordinal))
                    return values[index].Percentage;
            }
            return 0d;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    public sealed class StrongboxMetricDifference
    {
        public StrongboxMetricDifference(string metricId, double primaryValue, double comparisonValue)
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
            IReadOnlyList<StrongboxMetricDifference> metrics,
            string fingerprint)
        {
            Primary = primary ?? throw new ArgumentNullException(nameof(primary));
            Comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            Fingerprint = fingerprint ?? string.Empty;
        }

        public StrongboxSimulationReport Primary { get; }
        public StrongboxSimulationReport Comparison { get; }
        public IReadOnlyList<StrongboxMetricDifference> Metrics { get; }
        public string Fingerprint { get; }
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
            StrongboxTierId = strongboxTierId ?? throw new ArgumentNullException(nameof(strongboxTierId));
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
            IReadOnlyList<StrongboxSweepEntry> entries,
            string fingerprint)
        {
            Mode = mode;
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            Fingerprint = fingerprint ?? string.Empty;
        }

        public StrongboxSimulationMode Mode { get; }
        public IReadOnlyList<StrongboxSweepEntry> Entries { get; }
        public string Fingerprint { get; }
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
            ValidateUnconditioned(primary);
            ValidateUnconditioned(comparison);
            if (production == null) throw new ArgumentNullException(nameof(production));

            StrongboxSimulationReport left = simulator.Run(FullOpeningRequest(primary), production);
            StrongboxSimulationReport right = simulator.Run(FullOpeningRequest(comparison), production);
            var metrics = new List<StrongboxMetricDifference>
            {
                Difference("average-augment-bias", StrongboxSimulationBiasMath.Average(left.AugmentBiases), StrongboxSimulationBiasMath.Average(right.AugmentBiases)),
                Difference("average-augment-level", WeightedMean(left.AugmentLevels), WeightedMean(right.AugmentLevels)),
                Difference("average-augment-slots", WeightedMean(left.AugmentSlots), WeightedMean(right.AugmentSlots)),
                Difference("average-item-level", WeightedMean(left.ItemLevels), WeightedMean(right.ItemLevels)),
                Difference("combined-exceptional-percent", left.CombinedExceptionalPercentage, right.CombinedExceptionalPercentage),
                Difference("exceptional-augment-level-percent", left.ExceptionalAugmentLevelPercentage, right.ExceptionalAugmentLevelPercentage),
                Difference("exceptional-slot-percent", left.ExceptionalSlotPercentage, right.ExceptionalSlotPercentage),
                Difference("generation-rate-percent", GenerationRate(left), GenerationRate(right)),
                Difference("rejection-rate-percent", RejectionRate(left), RejectionRate(right)),
            };
            AddDistributionMetrics(metrics, "quality", left.Qualities, right.Qualities);
            AddDiagnosticMetrics(metrics, left.Diagnostics, right.Diagnostics);
            metrics.Sort(CompareMetric);
            var readOnly = new ReadOnlyCollection<StrongboxMetricDifference>(metrics);
            return new StrongboxSimulationComparison(
                left,
                right,
                readOnly,
                BuildComparisonFingerprint(primary, comparison, left, right, readOnly));
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
            if (minimumPlayerLevel < 0) throw new ArgumentOutOfRangeException(nameof(minimumPlayerLevel));
            if (maximumPlayerLevel < minimumPlayerLevel) throw new ArgumentOutOfRangeException(nameof(maximumPlayerLevel));
            if (strongboxTierId == null) throw new ArgumentNullException(nameof(strongboxTierId));
            if (equipmentDefinitionId != null)
                throw new NotSupportedException("strongbox-simulation-definition-conditioned-unsupported");
            if (production == null) throw new ArgumentNullException(nameof(production));
            thresholds = thresholds ?? new StrongboxDiagnosticThresholds();

            var entries = new List<StrongboxSweepEntry>();
            StrongboxSimulationReport previous = null;
            int consecutiveZero = 0;
            for (int level = minimumPlayerLevel; level <= maximumPlayerLevel; level++)
            {
                var scenario = new StrongboxSimulationScenario(
                    level, strongboxTierId, openingsPerLevel, rootSeed);
                StrongboxSimulationReport report = simulator.Run(FullOpeningRequest(scenario), production);
                IReadOnlyList<string> warnings = SweepWarnings(
                    previous, report, thresholds, ref consecutiveZero);
                entries.Add(new StrongboxSweepEntry(level, strongboxTierId, report, warnings));
                previous = report;
            }
            var readOnly = new ReadOnlyCollection<StrongboxSweepEntry>(entries);
            return new StrongboxSimulationSweep(
                StrongboxSimulationMode.PlayerLevelSweep,
                readOnly,
                BuildSweepFingerprint(StrongboxSimulationMode.PlayerLevelSweep, readOnly));
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
            if (equipmentDefinitionId != null)
                throw new NotSupportedException("strongbox-simulation-definition-conditioned-unsupported");
            if (production == null) throw new ArgumentNullException(nameof(production));
            thresholds = thresholds ?? new StrongboxDiagnosticThresholds();

            var entries = new List<StrongboxSweepEntry>();
            StrongboxSimulationReport previous = null;
            int consecutiveZero = 0;
            for (int index = 0; index < strongboxTierIds.Count; index++)
            {
                StableId tierId = strongboxTierIds[index]
                    ?? throw new ArgumentException("Tier identities cannot contain null.", nameof(strongboxTierIds));
                var scenario = new StrongboxSimulationScenario(
                    playerLevel, tierId, openingsPerTier, rootSeed);
                StrongboxSimulationReport report = simulator.Run(FullOpeningRequest(scenario), production);
                var warnings = new List<string>(SweepWarnings(
                    previous, report, thresholds, ref consecutiveZero));
                if (previous != null
                    && WeightedMean(report.ItemLevels) < WeightedMean(previous.ItemLevels)
                    && WeightedMean(report.AugmentSlots) < WeightedMean(previous.AugmentSlots)
                    && WeightedMean(report.AugmentLevels) < WeightedMean(previous.AugmentLevels))
                    warnings.Add("suspicious-strongbox-tier-inversion");
                warnings.Sort(StringComparer.Ordinal);
                entries.Add(new StrongboxSweepEntry(
                    playerLevel,
                    tierId,
                    report,
                    new ReadOnlyCollection<string>(warnings)));
                previous = report;
            }
            var readOnly = new ReadOnlyCollection<StrongboxSweepEntry>(entries);
            return new StrongboxSimulationSweep(
                StrongboxSimulationMode.StrongboxTierSweep,
                readOnly,
                BuildSweepFingerprint(StrongboxSimulationMode.StrongboxTierSweep, readOnly));
        }

        private static void ValidateUnconditioned(StrongboxSimulationScenario scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (scenario.EquipmentDefinitionId != null)
                throw new NotSupportedException("strongbox-simulation-definition-conditioned-unsupported");
        }

        private static StrongboxSimulationRequest FullOpeningRequest(StrongboxSimulationScenario scenario)
        {
            return new StrongboxSimulationRequest(StrongboxSimulationMode.FullOpening, scenario);
        }

        private static IReadOnlyList<string> SweepWarnings(
            StrongboxSimulationReport previous,
            StrongboxSimulationReport current,
            StrongboxDiagnosticThresholds thresholds,
            ref int consecutiveZero)
        {
            var warnings = new List<string>();
            if (current.GeneratedCount == 0L)
            {
                consecutiveZero++;
                if (consecutiveZero >= thresholds.ZeroObservationRunLength)
                    warnings.Add("long-zero-observation-region");
            }
            else consecutiveZero = 0;

            if (current.RejectedCount > 0L) warnings.Add("simulation-rejections-present");
            if (previous != null)
            {
                double previousRate = GenerationRate(previous);
                double currentRate = GenerationRate(current);
                if (Math.Abs(currentRate - previousRate) >= thresholds.ProbabilityCliffPercentagePoints)
                    warnings.Add("major-generation-rate-cliff");
                if (previousRate > 0d
                    && currentRate / previousRate < thresholds.RelativeRegressionMultiplier)
                    warnings.Add("unexpected-probability-regression");
                if (WeightedMean(current.AugmentSlots)
                    < WeightedMean(previous.AugmentSlots) * thresholds.RelativeRegressionMultiplier)
                    warnings.Add("augment-slot-quality-regression");
                if (WeightedMean(current.AugmentLevels)
                    < WeightedMean(previous.AugmentLevels) * thresholds.RelativeRegressionMultiplier)
                    warnings.Add("augment-level-quality-regression");
                if (StrongboxSimulationBiasMath.Average(current.AugmentBiases)
                    < StrongboxSimulationBiasMath.Average(previous.AugmentBiases)
                    * thresholds.RelativeRegressionMultiplier)
                    warnings.Add("augment-bias-quality-regression");
                if (current.CombinedExceptionalPercentage
                    < previous.CombinedExceptionalPercentage * thresholds.RelativeRegressionMultiplier)
                    warnings.Add("combined-exceptional-rate-regression");
                if (RejectionRate(current) > RejectionRate(previous)
                    + thresholds.ProbabilityCliffPercentagePoints)
                    warnings.Add("rejection-rate-cliff");
            }
            warnings.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(warnings);
        }

        private static void AddDistributionMetrics(
            ICollection<StrongboxMetricDifference> metrics,
            string prefix,
            IReadOnlyList<StrongboxDistributionEntry> left,
            IReadOnlyList<StrongboxDistributionEntry> right)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < left.Count; index++) keys.Add(left[index].Key);
            for (int index = 0; index < right.Count; index++) keys.Add(right[index].Key);
            foreach (string key in keys)
                metrics.Add(Difference(
                    prefix + "-" + key + "-percent",
                    DistributionPercent(left, key),
                    DistributionPercent(right, key)));
        }

        private static void AddDiagnosticMetrics(
            ICollection<StrongboxMetricDifference> metrics,
            IReadOnlyList<StrongboxDiagnosticEntry> left,
            IReadOnlyList<StrongboxDiagnosticEntry> right)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < left.Count; index++) keys.Add(left[index].Code);
            for (int index = 0; index < right.Count; index++) keys.Add(right[index].Code);
            foreach (string key in keys)
                metrics.Add(Difference(
                    "diagnostic-" + key + "-count",
                    DiagnosticCount(left, key),
                    DiagnosticCount(right, key)));
        }

        private static double DiagnosticCount(IReadOnlyList<StrongboxDiagnosticEntry> values, string code)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(values[index].Code, code, StringComparison.Ordinal)) return values[index].Count;
            return 0d;
        }

        private static StrongboxMetricDifference Difference(string id, double left, double right)
        {
            return new StrongboxMetricDifference(id, left, right);
        }

        private static int CompareMetric(StrongboxMetricDifference left, StrongboxMetricDifference right)
        {
            return string.CompareOrdinal(left.MetricId, right.MetricId);
        }

        private static double GenerationRate(StrongboxSimulationReport report)
        {
            long total = report.GeneratedCount + report.RejectedCount;
            return total == 0L ? 0d : 100d * report.GeneratedCount / total;
        }

        private static double RejectionRate(StrongboxSimulationReport report)
        {
            long total = report.GeneratedCount + report.RejectedCount;
            return total == 0L ? 0d : 100d * report.RejectedCount / total;
        }

        private static double WeightedMean(IReadOnlyList<StrongboxDistributionEntry> values)
        {
            long count = 0L;
            double total = 0d;
            for (int index = 0; index < values.Count; index++)
            {
                int key;
                if (!int.TryParse(values[index].Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out key))
                    continue;
                count += values[index].Count;
                total += key * (double)values[index].Count;
            }
            return count == 0L ? 0d : total / count;
        }

        private static double DistributionPercent(IReadOnlyList<StrongboxDistributionEntry> values, string key)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(values[index].Key, key, StringComparison.Ordinal)) return values[index].Percentage;
            return 0d;
        }

        private static string BuildComparisonFingerprint(
            StrongboxSimulationScenario primary,
            StrongboxSimulationScenario comparison,
            StrongboxSimulationReport left,
            StrongboxSimulationReport right,
            IReadOnlyList<StrongboxMetricDifference> metrics)
        {
            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "schema", "strongbox-simulation-comparison-v1");
            StrongboxCanonicalV1.AppendToken(builder, "primary_report", left.Fingerprint);
            StrongboxCanonicalV1.AppendToken(builder, "comparison_report", right.Fingerprint);
            AppendScenario(builder, "primary", primary);
            AppendScenario(builder, "comparison", comparison);
            for (int index = 0; index < metrics.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(builder, "metric", metrics[index].MetricId);
                StrongboxCanonicalV1.AppendToken(builder, "primary_bits", Bits(metrics[index].PrimaryValue));
                StrongboxCanonicalV1.AppendToken(builder, "comparison_bits", Bits(metrics[index].ComparisonValue));
            }
            return StrongboxCanonicalV1.Fingerprint(builder.ToString());
        }

        private static string BuildSweepFingerprint(
            StrongboxSimulationMode mode,
            IReadOnlyList<StrongboxSweepEntry> entries)
        {
            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "schema", "strongbox-simulation-sweep-v1");
            StrongboxCanonicalV1.AppendToken(builder, "mode", mode.ToString());
            for (int index = 0; index < entries.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(builder, "entry_index", index.ToString(CultureInfo.InvariantCulture));
                StrongboxCanonicalV1.AppendToken(builder, "entry_level", entries[index].PlayerLevel.ToString(CultureInfo.InvariantCulture));
                StrongboxCanonicalV1.AppendToken(builder, "entry_tier", entries[index].StrongboxTierId.ToString());
                StrongboxCanonicalV1.AppendToken(builder, "entry_report", entries[index].Report.Fingerprint);
                for (int warning = 0; warning < entries[index].Warnings.Count; warning++)
                    StrongboxCanonicalV1.AppendToken(builder, "entry_warning", entries[index].Warnings[warning]);
            }
            return StrongboxCanonicalV1.Fingerprint(builder.ToString());
        }

        private static void AppendScenario(StringBuilder builder, string prefix, StrongboxSimulationScenario value)
        {
            StrongboxCanonicalV1.AppendToken(builder, prefix + "_level", value.PlayerLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, prefix + "_tier", value.StrongboxTierId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, prefix + "_samples", value.SampleCount.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, prefix + "_seed", value.RootSeed.ToString("x16", CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, prefix + "_definition", value.EquipmentDefinitionId == null ? string.Empty : value.EquipmentDefinitionId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, prefix + "_override", value.DiagnosticEligibilityOverride ? "1" : "0");
        }

        private static string Bits(double value)
        {
            return BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}

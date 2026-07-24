using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    public enum StrongboxZeroDropInterpretation
    {
        Observed = 1,
        EligibleButNotObserved = 2,
        Unavailable = 3,
        BoxPolicyExcluded = 4,
        DiagnosticOverrideRequired = 5,
        CatalogLimitation = 6,
    }

    public sealed class StrongboxCatalogCoverageEntry
    {
        public StrongboxCatalogCoverageEntry(
            StrongboxEquipmentMetadata metadata,
            long observedCount,
            StrongboxZeroDropInterpretation interpretation,
            string diagnostic)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            if (observedCount < 0L) throw new ArgumentOutOfRangeException(nameof(observedCount));
            if (!Enum.IsDefined(typeof(StrongboxZeroDropInterpretation), interpretation))
                throw new ArgumentOutOfRangeException(nameof(interpretation));
            ObservedCount = observedCount;
            Interpretation = interpretation;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public StrongboxEquipmentMetadata Metadata { get; }
        public long ObservedCount { get; }
        public StrongboxZeroDropInterpretation Interpretation { get; }
        public string Diagnostic { get; }
    }

    public sealed class StrongboxRareOutcomeQuery
    {
        public StrongboxRareOutcomeQuery(
            string queryId,
            StableId equipmentDefinitionId = null,
            int minimumSlots = 0,
            bool requireSlotsAboveOrdinaryMaximum = false,
            int minimumAugmentLevel = 0,
            bool requireAugmentLevelAboveOrdinaryMaximum = false,
            double? expectedProbability = null)
        {
            if (string.IsNullOrWhiteSpace(queryId))
                throw new ArgumentException("Query ID is required.", nameof(queryId));
            if (minimumSlots < 0) throw new ArgumentOutOfRangeException(nameof(minimumSlots));
            if (minimumAugmentLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumAugmentLevel));
            if (expectedProbability.HasValue
                && (double.IsNaN(expectedProbability.Value)
                    || double.IsInfinity(expectedProbability.Value)
                    || expectedProbability.Value <= 0d
                    || expectedProbability.Value > 1d))
                throw new ArgumentOutOfRangeException(nameof(expectedProbability));

            QueryId = queryId.Trim();
            EquipmentDefinitionId = equipmentDefinitionId;
            MinimumSlots = minimumSlots;
            RequireSlotsAboveOrdinaryMaximum = requireSlotsAboveOrdinaryMaximum;
            MinimumAugmentLevel = minimumAugmentLevel;
            RequireAugmentLevelAboveOrdinaryMaximum =
                requireAugmentLevelAboveOrdinaryMaximum;
            ExpectedProbability = expectedProbability;
        }

        public string QueryId { get; }
        public StableId EquipmentDefinitionId { get; }
        public int MinimumSlots { get; }
        public bool RequireSlotsAboveOrdinaryMaximum { get; }
        public int MinimumAugmentLevel { get; }
        public bool RequireAugmentLevelAboveOrdinaryMaximum { get; }
        public double? ExpectedProbability { get; }
    }

    public sealed class StrongboxRareOutcomeResult
    {
        public StrongboxRareOutcomeResult(
            StrongboxRareOutcomeQuery query,
            long observedCount,
            long sampleCount,
            double observedProbability,
            double? zeroObservationUpperBound,
            long? suggestedSampleCount,
            string interpretation)
        {
            Query = query ?? throw new ArgumentNullException(nameof(query));
            ObservedCount = observedCount;
            SampleCount = sampleCount;
            ObservedProbability = observedProbability;
            ZeroObservationUpperBound = zeroObservationUpperBound;
            SuggestedSampleCount = suggestedSampleCount;
            Interpretation = interpretation ?? string.Empty;
        }

        public StrongboxRareOutcomeQuery Query { get; }
        public long ObservedCount { get; }
        public long SampleCount { get; }
        public double ObservedProbability { get; }
        public double? ZeroObservationUpperBound { get; }
        public long? SuggestedSampleCount { get; }
        public string Interpretation { get; }
    }

    /// <summary>
    /// Analysis-only diagnostics. This type never evaluates selection weights and never
    /// upgrades incomplete metadata into a claim of production impossibility.
    /// </summary>
    public static class StrongboxSimulationDiagnostics
    {
        public static IReadOnlyList<StrongboxCatalogCoverageEntry> BuildCatalogCoverage(
            StrongboxSimulationReport report,
            IStrongboxSimulationProductionGateway production)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (production == null) throw new ArgumentNullException(nameof(production));

            var observed = new Dictionary<StableId, long>();
            for (int index = 0; index < report.Equipment.Count; index++)
                observed[report.Equipment[index].Metadata.DefinitionId] =
                    report.Equipment[index].Count;

            var definitions = new List<StrongboxEquipmentMetadata>(
                production.EquipmentDefinitions ?? Array.Empty<StrongboxEquipmentMetadata>());
            definitions.Sort(CompareMetadata);
            var result = new List<StrongboxCatalogCoverageEntry>(definitions.Count);

            for (int index = 0; index < definitions.Count; index++)
            {
                StrongboxEquipmentMetadata metadata = definitions[index];
                long count;
                observed.TryGetValue(metadata.DefinitionId, out count);
                StrongboxZeroDropInterpretation interpretation;
                string diagnostic;

                if (count > 0L)
                {
                    interpretation = StrongboxZeroDropInterpretation.Observed;
                    diagnostic = "observed";
                }
                else if (!metadata.Available)
                {
                    interpretation = StrongboxZeroDropInterpretation.Unavailable;
                    diagnostic = "production metadata marks the definition unavailable";
                }
                else if (metadata.TopBoxOnly)
                {
                    interpretation = StrongboxZeroDropInterpretation.CatalogLimitation;
                    diagnostic = "TopBoxOnly compatibility requires explicit production tier metadata; the simulator does not infer tier ordering from IDs";
                }
                else
                {
                    interpretation = StrongboxZeroDropInterpretation.EligibleButNotObserved;
                    diagnostic = "zero observations do not prove mathematical impossibility";
                }

                result.Add(new StrongboxCatalogCoverageEntry(
                    metadata,
                    count,
                    interpretation,
                    diagnostic));
            }
            return new ReadOnlyCollection<StrongboxCatalogCoverageEntry>(result);
        }

        public static StrongboxRareOutcomeResult EvaluateRareOutcome(
            StrongboxSimulationReport report,
            StrongboxRareOutcomeQuery query)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (query == null) throw new ArgumentNullException(nameof(query));

            long observed = 0L;
            for (int index = 0; index < report.Equipment.Count; index++)
            {
                StrongboxEquipmentStatistics equipment = report.Equipment[index];
                if (query.EquipmentDefinitionId != null
                    && equipment.Metadata.DefinitionId != query.EquipmentDefinitionId)
                    continue;
                observed += CountMatches(equipment, query);
            }

            long sampleCount = query.EquipmentDefinitionId == null
                ? report.GeneratedCount
                : CountDefinitionCopies(report, query.EquipmentDefinitionId);
            double probability = sampleCount == 0L ? 0d : (double)observed / sampleCount;
            double? upperBound = observed == 0L && sampleCount > 0L
                ? Math.Min(1d, 3d / sampleCount)
                : (double?)null;
            long? suggested = query.ExpectedProbability.HasValue
                ? checked((long)Math.Ceiling(3d / query.ExpectedProbability.Value))
                : (long?)null;
            string interpretation = observed > 0L
                ? "observed"
                : sampleCount == 0L
                    ? "not evaluated because no conditioned copies were generated"
                    : "possible-but-not-observed unless production metadata proves impossibility";

            return new StrongboxRareOutcomeResult(
                query,
                observed,
                sampleCount,
                probability,
                upperBound,
                suggested,
                interpretation);
        }

        private static long CountMatches(
            StrongboxEquipmentStatistics equipment,
            StrongboxRareOutcomeQuery query)
        {
            bool asksSlots = query.MinimumSlots > 0 || query.RequireSlotsAboveOrdinaryMaximum;
            bool asksLevels = query.MinimumAugmentLevel > 0
                || query.RequireAugmentLevelAboveOrdinaryMaximum;
            if (asksSlots && asksLevels)
                return CountCombinedMatches(equipment, query);

            IReadOnlyList<StrongboxDistributionEntry> distribution = asksLevels
                ? equipment.AugmentLevelDistribution
                : equipment.SlotDistribution;
            long count = 0L;
            for (int index = 0; index < distribution.Count; index++)
            {
                int value;
                if (!int.TryParse(distribution[index].Key, out value)) continue;
                bool matches = asksLevels
                    ? MatchesLevel(equipment, query, value)
                    : MatchesSlots(equipment, query, value);
                if (matches) count += distribution[index].Count;
            }
            return count;
        }

        private static long CountCombinedMatches(
            StrongboxEquipmentStatistics equipment,
            StrongboxRareOutcomeQuery query)
        {
            long count = 0L;
            for (int index = 0; index < equipment.AugmentSignatureDistribution.Count; index++)
            {
                StrongboxDistributionEntry entry = equipment.AugmentSignatureDistribution[index];
                int separator = entry.Key.IndexOf(':');
                if (separator <= 0 || separator == entry.Key.Length - 1) continue;
                int slots;
                int level;
                if (!int.TryParse(entry.Key.Substring(0, separator), out slots)) continue;
                if (!int.TryParse(entry.Key.Substring(separator + 1), out level)) continue;
                if (MatchesSlots(equipment, query, slots)
                    && MatchesLevel(equipment, query, level))
                    count += entry.Count;
            }
            return count;
        }

        private static bool MatchesSlots(
            StrongboxEquipmentStatistics equipment,
            StrongboxRareOutcomeQuery query,
            int value)
        {
            return value >= query.MinimumSlots
                && (!query.RequireSlotsAboveOrdinaryMaximum
                    || value > equipment.Metadata.OrdinaryMaximumSlots);
        }

        private static bool MatchesLevel(
            StrongboxEquipmentStatistics equipment,
            StrongboxRareOutcomeQuery query,
            int value)
        {
            return value >= query.MinimumAugmentLevel
                && (!query.RequireAugmentLevelAboveOrdinaryMaximum
                    || value > equipment.Metadata.OrdinaryMaximumAugmentLevel);
        }

        private static long CountDefinitionCopies(
            StrongboxSimulationReport report,
            StableId definitionId)
        {
            for (int index = 0; index < report.Equipment.Count; index++)
                if (report.Equipment[index].Metadata.DefinitionId == definitionId)
                    return report.Equipment[index].Count;
            return 0L;
        }

        private static int CompareMetadata(
            StrongboxEquipmentMetadata left,
            StrongboxEquipmentMetadata right)
        {
            int value = CompareIds(left.CategoryId, right.CategoryId);
            if (value != 0) return value;
            value = CompareIds(left.SlotId, right.SlotId);
            if (value != 0) return value;
            value = CompareIds(left.FamilyId, right.FamilyId);
            return value != 0 ? value : left.DefinitionId.CompareTo(right.DefinitionId);
        }

        private static int CompareIds(StableId left, StableId right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            return left.CompareTo(right);
        }
    }
}

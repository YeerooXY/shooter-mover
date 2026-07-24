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
            if (observedCount < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(observedCount));
            }
            if (!Enum.IsDefined(typeof(StrongboxZeroDropInterpretation), interpretation))
            {
                throw new ArgumentOutOfRangeException(nameof(interpretation));
            }

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
            {
                throw new ArgumentException("Query ID is required.", nameof(queryId));
            }
            if (minimumSlots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumSlots));
            }
            if (minimumAugmentLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAugmentLevel));
            }
            if (expectedProbability.HasValue
                && (double.IsNaN(expectedProbability.Value)
                    || double.IsInfinity(expectedProbability.Value)
                    || expectedProbability.Value <= 0d
                    || expectedProbability.Value > 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(expectedProbability));
            }

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
    /// Analysis-only diagnostics over a completed production-backed simulation report.
    /// It never evaluates loot weights and never changes generation decisions.
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
            {
                StrongboxEquipmentStatistics value = report.Equipment[index];
                observed[value.Metadata.DefinitionId] = value.Count;
            }

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
                else if (metadata.TopBoxOnly
                    && !IsTopTierScenario(report.Request.Primary.StrongboxTierId))
                {
                    interpretation = StrongboxZeroDropInterpretation.BoxPolicyExcluded;
                    diagnostic = "TopBoxOnly definition under a non-top-tier scenario";
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
            for (int equipmentIndex = 0;
                 equipmentIndex < report.Equipment.Count;
                 equipmentIndex++)
            {
                StrongboxEquipmentStatistics equipment = report.Equipment[equipmentIndex];
                if (query.EquipmentDefinitionId != null
                    && equipment.Metadata.DefinitionId != query.EquipmentDefinitionId)
                {
                    continue;
                }

                observed += CountMatchingSignatures(equipment, query);
            }

            long sampleCount = query.EquipmentDefinitionId == null
                ? report.GeneratedCount
                : CountDefinitionCopies(report, query.EquipmentDefinitionId);
            double probability = sampleCount == 0L
                ? 0d
                : (double)observed / sampleCount;

            double? upperBound = null;
            if (observed == 0L && sampleCount > 0L)
            {
                // Rule-of-three descriptive 95% upper bound. This is not a fabricated
                // production probability and is deliberately labelled as a bound.
                upperBound = Math.Min(1d, 3d / sampleCount);
            }

            long? suggested = null;
            if (query.ExpectedProbability.HasValue)
            {
                suggested = checked((long)Math.Ceiling(
                    3d / query.ExpectedProbability.Value));
            }

            string interpretation = observed > 0L
                ? "observed"
                : sampleCount == 0L
                    ? "not evaluated because the conditioned definition had no generated copies"
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

        private static long CountMatchingSignatures(
            StrongboxEquipmentStatistics equipment,
            StrongboxRareOutcomeQuery query)
        {
            long count = 0L;
            for (int slotIndex = 0;
                 slotIndex < equipment.SlotDistribution.Count;
                 slotIndex++)
            {
                StrongboxDistributionEntry slotEntry = equipment.SlotDistribution[slotIndex];
                int slots;
                if (!int.TryParse(slotEntry.Key, out slots)
                    || slots < query.MinimumSlots
                    || (query.RequireSlotsAboveOrdinaryMaximum
                        && slots <= equipment.Metadata.OrdinaryMaximumSlots))
                {
                    continue;
                }

                if (query.MinimumAugmentLevel == 0
                    && !query.RequireAugmentLevelAboveOrdinaryMaximum)
                {
                    count += slotEntry.Count;
                    continue;
                }

                // Existing per-equipment reports keep independent slot and level marginals.
                // Exact combined-query counts must come from the global signature table or a
                // future per-equipment combined-signature table, so do not multiply marginals.
                return 0L;
            }

            if (query.MinimumSlots == 0)
            {
                for (int levelIndex = 0;
                     levelIndex < equipment.AugmentLevelDistribution.Count;
                     levelIndex++)
                {
                    StrongboxDistributionEntry levelEntry =
                        equipment.AugmentLevelDistribution[levelIndex];
                    int level;
                    if (!int.TryParse(levelEntry.Key, out level)
                        || level < query.MinimumAugmentLevel
                        || (query.RequireAugmentLevelAboveOrdinaryMaximum
                            && level <= equipment.Metadata.OrdinaryMaximumAugmentLevel))
                    {
                        continue;
                    }
                    count += levelEntry.Count;
                }
            }
            return count;
        }

        private static long CountDefinitionCopies(
            StrongboxSimulationReport report,
            StableId definitionId)
        {
            for (int index = 0; index < report.Equipment.Count; index++)
            {
                StrongboxEquipmentStatistics value = report.Equipment[index];
                if (value.Metadata.DefinitionId == definitionId)
                {
                    return value.Count;
                }
            }
            return 0L;
        }

        private static int CompareMetadata(
            StrongboxEquipmentMetadata left,
            StrongboxEquipmentMetadata right)
        {
            int category = CompareIds(left.CategoryId, right.CategoryId);
            if (category != 0) return category;
            int slot = CompareIds(left.SlotId, right.SlotId);
            if (slot != 0) return slot;
            int family = CompareIds(left.FamilyId, right.FamilyId);
            if (family != 0) return family;
            return left.DefinitionId.CompareTo(right.DefinitionId);
        }

        private static int CompareIds(StableId left, StableId right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            return left.CompareTo(right);
        }

        private static bool IsTopTierScenario(StableId tierId)
        {
            // The simulator must not duplicate tier ordering. Until the production gateway
            // exposes explicit top-tier metadata, it cannot prove this gate from an ID alone.
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    public sealed class StrongboxDistributionEntry
    {
        public StrongboxDistributionEntry(string key, long count, long sampleCount)
        {
            Key = key ?? string.Empty;
            Count = count;
            Percentage = sampleCount == 0 ? 0d : (100d * count) / sampleCount;
        }
        public string Key { get; }
        public long Count { get; }
        public double Percentage { get; }
    }

    public sealed class StrongboxEquipmentStatistics
    {
        internal StrongboxEquipmentStatistics(
            StrongboxEquipmentMetadata metadata,
            long count,
            double averageItemLevel,
            double averageSlots,
            double averageAugmentLevel,
            long exceptionalSlots,
            long exceptionalAugmentLevels,
            IReadOnlyList<StrongboxDistributionEntry> slotDistribution,
            IReadOnlyList<StrongboxDistributionEntry> augmentLevelDistribution)
        {
            Metadata = metadata;
            Count = count;
            AverageItemLevel = averageItemLevel;
            AverageSlots = averageSlots;
            AverageAugmentLevel = averageAugmentLevel;
            ExceptionalSlots = exceptionalSlots;
            ExceptionalAugmentLevels = exceptionalAugmentLevels;
            SlotDistribution = slotDistribution;
            AugmentLevelDistribution = augmentLevelDistribution;
        }

        public StrongboxEquipmentMetadata Metadata { get; }
        public long Count { get; }
        public double AverageItemLevel { get; }
        public double AverageSlots { get; }
        public double AverageAugmentLevel { get; }
        public long ExceptionalSlots { get; }
        public long ExceptionalAugmentLevels { get; }
        public IReadOnlyList<StrongboxDistributionEntry> SlotDistribution { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentLevelDistribution { get; }
    }

    public sealed class StrongboxSimulationReport
    {
        internal StrongboxSimulationReport(
            StrongboxSimulationRequest request,
            StrongboxProductionFingerprints production,
            long generatedCount,
            long rejectedCount,
            IReadOnlyList<StrongboxDistributionEntry> targetLevels,
            IReadOnlyList<StrongboxDistributionEntry> itemLevels,
            IReadOnlyList<StrongboxDistributionEntry> slots,
            IReadOnlyList<StrongboxDistributionEntry> augmentLevels,
            IReadOnlyList<StrongboxDistributionEntry> signatures,
            IReadOnlyList<StrongboxEquipmentStatistics> equipment,
            IReadOnlyList<string> diagnostics,
            string fingerprint)
        {
            Request = request;
            Production = production;
            GeneratedCount = generatedCount;
            RejectedCount = rejectedCount;
            TargetLevels = targetLevels;
            ItemLevels = itemLevels;
            AugmentSlots = slots;
            AugmentLevels = augmentLevels;
            AugmentSignatures = signatures;
            Equipment = equipment;
            Diagnostics = diagnostics;
            Fingerprint = fingerprint;
        }

        public StrongboxSimulationRequest Request { get; }
        public StrongboxProductionFingerprints Production { get; }
        public long GeneratedCount { get; }
        public long RejectedCount { get; }
        public IReadOnlyList<StrongboxDistributionEntry> TargetLevels { get; }
        public IReadOnlyList<StrongboxDistributionEntry> ItemLevels { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentSlots { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentLevels { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentSignatures { get; }
        public IReadOnlyList<StrongboxEquipmentStatistics> Equipment { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public string Fingerprint { get; }
    }

    /// <summary>
    /// Streaming analysis consumer. All loot decisions are delegated to the supplied
    /// production gateway; this type owns only counters, ordering and report identity.
    /// </summary>
    public sealed class StrongboxBatchSimulator
    {
        private sealed class EquipmentAccumulator
        {
            public StrongboxEquipmentMetadata Metadata;
            public long Count;
            public long ItemLevelTotal;
            public long SlotTotal;
            public long AugmentLevelTotal;
            public long NonzeroSlotCount;
            public long ExceptionalSlots;
            public long ExceptionalLevels;
            public readonly SortedDictionary<int, long> Slots = new SortedDictionary<int, long>();
            public readonly SortedDictionary<int, long> Levels = new SortedDictionary<int, long>();
        }

        public StrongboxSimulationReport Run(
            StrongboxSimulationRequest request,
            IStrongboxSimulationProductionGateway production)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (production == null) throw new ArgumentNullException(nameof(production));
            if (request.Mode != StrongboxSimulationMode.FullOpening
                && request.Mode != StrongboxSimulationMode.DefinitionConditioned)
                throw new NotSupportedException("Use the comparison or sweep coordinator for multi-scenario modes.");

            var targetLevels = new SortedDictionary<int, long>();
            var itemLevels = new SortedDictionary<int, long>();
            var slots = new SortedDictionary<int, long>();
            var levels = new SortedDictionary<int, long>();
            var signatures = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var byDefinition = new SortedDictionary<string, EquipmentAccumulator>(StringComparer.Ordinal);
            var diagnostics = new SortedSet<string>(StringComparer.Ordinal);
            long generated = 0;
            long rejected = 0;

            for (long ordinal = 0; ordinal < request.Primary.SampleCount; ordinal++)
            {
                StrongboxGeneratedEquipmentObservation observation;
                string diagnostic;
                if (!production.TryGenerate(request.Primary, ordinal, out observation, out diagnostic)
                    || observation == null)
                {
                    rejected++;
                    if (!string.IsNullOrWhiteSpace(diagnostic)) diagnostics.Add(diagnostic);
                    continue;
                }

                ValidateProductionResult(observation);
                generated++;
                Increment(targetLevels, observation.TargetLevel);
                Increment(itemLevels, observation.ItemLevel);
                Increment(slots, observation.AugmentSlotCount);
                if (observation.AugmentSlotCount > 0) Increment(levels, observation.SharedAugmentLevel);
                string signature = observation.AugmentSlotCount.ToString(CultureInfo.InvariantCulture)
                    + ":" + (observation.AugmentSlotCount == 0
                        ? "none"
                        : observation.SharedAugmentLevel.ToString(CultureInfo.InvariantCulture));
                Increment(signatures, signature);

                string definitionKey = observation.Equipment.DefinitionId.ToString();
                EquipmentAccumulator accumulator;
                if (!byDefinition.TryGetValue(definitionKey, out accumulator))
                {
                    accumulator = new EquipmentAccumulator { Metadata = observation.Equipment };
                    byDefinition.Add(definitionKey, accumulator);
                }
                accumulator.Count++;
                accumulator.ItemLevelTotal += observation.ItemLevel;
                accumulator.SlotTotal += observation.AugmentSlotCount;
                Increment(accumulator.Slots, observation.AugmentSlotCount);
                if (observation.AugmentSlotCount > 0)
                {
                    accumulator.NonzeroSlotCount++;
                    accumulator.AugmentLevelTotal += observation.SharedAugmentLevel;
                    Increment(accumulator.Levels, observation.SharedAugmentLevel);
                }
                if (observation.ExceptionalSlotOutcome) accumulator.ExceptionalSlots++;
                if (observation.ExceptionalAugmentLevelOutcome) accumulator.ExceptionalLevels++;
            }

            var equipment = new List<StrongboxEquipmentStatistics>();
            foreach (KeyValuePair<string, EquipmentAccumulator> pair in byDefinition)
            {
                EquipmentAccumulator value = pair.Value;
                equipment.Add(new StrongboxEquipmentStatistics(
                    value.Metadata,
                    value.Count,
                    Divide(value.ItemLevelTotal, value.Count),
                    Divide(value.SlotTotal, value.Count),
                    Divide(value.AugmentLevelTotal, value.NonzeroSlotCount),
                    value.ExceptionalSlots,
                    value.ExceptionalLevels,
                    Entries(value.Slots, value.Count),
                    Entries(value.Levels, value.NonzeroSlotCount)));
            }

            string canonical = BuildCanonical(
                request, production.Fingerprints, generated, rejected,
                targetLevels, itemLevels, slots, levels, signatures, byDefinition, diagnostics);
            return new StrongboxSimulationReport(
                request,
                production.Fingerprints,
                generated,
                rejected,
                Entries(targetLevels, generated),
                Entries(itemLevels, generated),
                Entries(slots, generated),
                Entries(levels, Sum(levels)),
                Entries(signatures, generated),
                new ReadOnlyCollection<StrongboxEquipmentStatistics>(equipment),
                new ReadOnlyCollection<string>(new List<string>(diagnostics)),
                StrongboxCanonicalV1.Fingerprint(canonical));
        }

        private static void ValidateProductionResult(StrongboxGeneratedEquipmentObservation value)
        {
            if (value.AugmentSlotCount < 0
                || value.AugmentSlotCount > value.Equipment.AbsoluteMaximumSlots)
                throw new InvalidOperationException("Production returned a slot count beyond its declared absolute limit at ordinal " + value.Ordinal + ".");
            if (value.AugmentSlotCount == 0 && value.SharedAugmentLevel != 0)
                throw new InvalidOperationException("Production returned an augment level for a zero-slot item at ordinal " + value.Ordinal + ".");
            if (value.AugmentSlotCount > 0
                && (value.SharedAugmentLevel < 1
                    || value.SharedAugmentLevel > value.Equipment.AbsoluteMaximumAugmentLevel))
                throw new InvalidOperationException("Production returned an augment level beyond its declared absolute limit at ordinal " + value.Ordinal + ".");
        }

        private static string BuildCanonical(
            StrongboxSimulationRequest request,
            StrongboxProductionFingerprints production,
            long generated,
            long rejected,
            SortedDictionary<int, long> targets,
            SortedDictionary<int, long> items,
            SortedDictionary<int, long> slots,
            SortedDictionary<int, long> levels,
            SortedDictionary<string, long> signatures,
            SortedDictionary<string, EquipmentAccumulator> equipment,
            SortedSet<string> diagnostics)
        {
            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "schema", "strongbox-simulation-report-v1");
            StrongboxCanonicalV1.AppendToken(builder, "mode", request.Mode.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "player_level", request.Primary.PlayerLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "tier", request.Primary.StrongboxTierId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "sample_count", request.Primary.SampleCount.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "root_seed", request.Primary.RootSeed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "catalog", production.EquipmentCatalog);
            StrongboxCanonicalV1.AppendToken(builder, "projection", production.EquipmentProjection);
            StrongboxCanonicalV1.AppendToken(builder, "policy", production.StrongboxPolicy);
            StrongboxCanonicalV1.AppendToken(builder, "generated", generated.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "rejected", rejected.ToString(CultureInfo.InvariantCulture));
            Append(builder, "target", targets);
            Append(builder, "item", items);
            Append(builder, "slot", slots);
            Append(builder, "level", levels);
            foreach (KeyValuePair<string, long> pair in signatures)
                StrongboxCanonicalV1.AppendToken(builder, "signature", pair.Key + "=" + pair.Value.ToString(CultureInfo.InvariantCulture));
            foreach (KeyValuePair<string, EquipmentAccumulator> pair in equipment)
                StrongboxCanonicalV1.AppendToken(builder, "equipment", pair.Key + "=" + pair.Value.Count.ToString(CultureInfo.InvariantCulture));
            foreach (string diagnostic in diagnostics)
                StrongboxCanonicalV1.AppendToken(builder, "diagnostic", diagnostic);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string name, SortedDictionary<int, long> values)
        {
            foreach (KeyValuePair<int, long> pair in values)
                StrongboxCanonicalV1.AppendToken(builder, name, pair.Key.ToString(CultureInfo.InvariantCulture) + "=" + pair.Value.ToString(CultureInfo.InvariantCulture));
        }

        private static IReadOnlyList<StrongboxDistributionEntry> Entries(SortedDictionary<int, long> values, long total)
        {
            var result = new List<StrongboxDistributionEntry>();
            foreach (KeyValuePair<int, long> pair in values)
                result.Add(new StrongboxDistributionEntry(pair.Key.ToString(CultureInfo.InvariantCulture), pair.Value, total));
            return new ReadOnlyCollection<StrongboxDistributionEntry>(result);
        }

        private static IReadOnlyList<StrongboxDistributionEntry> Entries(SortedDictionary<string, long> values, long total)
        {
            var result = new List<StrongboxDistributionEntry>();
            foreach (KeyValuePair<string, long> pair in values)
                result.Add(new StrongboxDistributionEntry(pair.Key, pair.Value, total));
            return new ReadOnlyCollection<StrongboxDistributionEntry>(result);
        }

        private static void Increment(SortedDictionary<int, long> values, int key)
        {
            long count;
            values.TryGetValue(key, out count);
            values[key] = count + 1;
        }

        private static void Increment(SortedDictionary<string, long> values, string key)
        {
            long count;
            values.TryGetValue(key, out count);
            values[key] = count + 1;
        }

        private static long Sum(SortedDictionary<int, long> values)
        {
            long total = 0;
            foreach (KeyValuePair<int, long> pair in values) total += pair.Value;
            return total;
        }

        private static double Divide(long numerator, long denominator)
        {
            return denominator == 0 ? 0d : (double)numerator / denominator;
        }
    }
}

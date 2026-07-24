using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    public sealed class StrongboxSimulationIntegrityException : InvalidOperationException
    {
        public StrongboxSimulationIntegrityException(string diagnosticCode)
            : base(diagnosticCode ?? "strongbox-simulation-integrity-failed")
        {
            DiagnosticCode = diagnosticCode ?? "strongbox-simulation-integrity-failed";
        }

        public string DiagnosticCode { get; }
    }

    public sealed class StrongboxDistributionEntry
    {
        public StrongboxDistributionEntry(string key, long count, long sampleCount)
        {
            Key = key ?? string.Empty;
            Count = count;
            Percentage = sampleCount == 0L ? 0d : 100d * count / sampleCount;
        }

        public string Key { get; }
        public long Count { get; }
        public double Percentage { get; }
    }

    public sealed class StrongboxDiagnosticEntry
    {
        public StrongboxDiagnosticEntry(string code, long count)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Diagnostic code is required.", nameof(code));
            if (count < 1L) throw new ArgumentOutOfRangeException(nameof(count));
            Code = code;
            Count = count;
        }

        public string Code { get; }
        public long Count { get; }
    }

    public sealed class StrongboxEquipmentStatistics
    {
        internal StrongboxEquipmentStatistics(
            StrongboxEquipmentMetadata metadata,
            long count,
            double averageItemLevel,
            double averageSlots,
            double averageAugmentLevel,
            double averageAugmentBias,
            long exceptionalSlots,
            long exceptionalAugmentLevels,
            long combinedExceptional,
            IReadOnlyList<StrongboxDistributionEntry> itemLevelDistribution,
            IReadOnlyList<StrongboxDistributionEntry> qualityDistribution,
            IReadOnlyList<StrongboxDistributionEntry> slotDistribution,
            IReadOnlyList<StrongboxDistributionEntry> augmentLevelDistribution,
            IReadOnlyList<StrongboxDistributionEntry> augmentSignatureDistribution,
            IReadOnlyList<StrongboxDistributionEntry> augmentBiasDistribution)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Count = count;
            AverageItemLevel = averageItemLevel;
            AverageSlots = averageSlots;
            AverageAugmentLevel = averageAugmentLevel;
            AverageAugmentBias = averageAugmentBias;
            ExceptionalSlots = exceptionalSlots;
            ExceptionalAugmentLevels = exceptionalAugmentLevels;
            CombinedExceptional = combinedExceptional;
            ItemLevelDistribution = itemLevelDistribution;
            QualityDistribution = qualityDistribution;
            SlotDistribution = slotDistribution;
            AugmentLevelDistribution = augmentLevelDistribution;
            AugmentSignatureDistribution = augmentSignatureDistribution;
            AugmentBiasDistribution = augmentBiasDistribution;
        }

        public StrongboxEquipmentMetadata Metadata { get; }
        public long Count { get; }
        public double AverageItemLevel { get; }
        public double AverageSlots { get; }
        public double AverageAugmentLevel { get; }
        public double AverageAugmentBias { get; }
        public long ExceptionalSlots { get; }
        public long ExceptionalAugmentLevels { get; }
        public long CombinedExceptional { get; }
        public double ExceptionalSlotPercentage => Percentage(ExceptionalSlots, Count);
        public double ExceptionalAugmentLevelPercentage => Percentage(ExceptionalAugmentLevels, Count);
        public double CombinedExceptionalPercentage => Percentage(CombinedExceptional, Count);
        public IReadOnlyList<StrongboxDistributionEntry> ItemLevelDistribution { get; }
        public IReadOnlyList<StrongboxDistributionEntry> QualityDistribution { get; }
        public IReadOnlyList<StrongboxDistributionEntry> SlotDistribution { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentLevelDistribution { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentSignatureDistribution { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentBiasDistribution { get; }

        private static double Percentage(long count, long total)
        {
            return total == 0L ? 0d : 100d * count / total;
        }
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
            IReadOnlyList<StrongboxDistributionEntry> qualities,
            IReadOnlyList<StrongboxDistributionEntry> slots,
            IReadOnlyList<StrongboxDistributionEntry> augmentLevels,
            IReadOnlyList<StrongboxDistributionEntry> signatures,
            IReadOnlyList<StrongboxDistributionEntry> augmentBiases,
            long exceptionalSlots,
            long exceptionalAugmentLevels,
            long combinedExceptional,
            IReadOnlyList<StrongboxEquipmentStatistics> equipment,
            IReadOnlyList<StrongboxDiagnosticEntry> diagnostics,
            string fingerprint)
        {
            Request = request;
            Production = production;
            GeneratedCount = generatedCount;
            RejectedCount = rejectedCount;
            TargetLevels = targetLevels;
            ItemLevels = itemLevels;
            Qualities = qualities;
            AugmentSlots = slots;
            AugmentLevels = augmentLevels;
            AugmentSignatures = signatures;
            AugmentBiases = augmentBiases;
            ExceptionalSlots = exceptionalSlots;
            ExceptionalAugmentLevels = exceptionalAugmentLevels;
            CombinedExceptional = combinedExceptional;
            Equipment = equipment;
            Diagnostics = diagnostics;
            Fingerprint = fingerprint ?? string.Empty;
        }

        public StrongboxSimulationRequest Request { get; }
        public StrongboxProductionFingerprints Production { get; }
        public long GeneratedCount { get; }
        public long RejectedCount { get; }
        public IReadOnlyList<StrongboxDistributionEntry> TargetLevels { get; }
        public IReadOnlyList<StrongboxDistributionEntry> ItemLevels { get; }
        public IReadOnlyList<StrongboxDistributionEntry> Qualities { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentSlots { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentLevels { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentSignatures { get; }
        public IReadOnlyList<StrongboxDistributionEntry> AugmentBiases { get; }
        public long ExceptionalSlots { get; }
        public long ExceptionalAugmentLevels { get; }
        public long CombinedExceptional { get; }
        public double ExceptionalSlotPercentage => Percentage(ExceptionalSlots, GeneratedCount);
        public double ExceptionalAugmentLevelPercentage => Percentage(ExceptionalAugmentLevels, GeneratedCount);
        public double CombinedExceptionalPercentage => Percentage(CombinedExceptional, GeneratedCount);
        public IReadOnlyList<StrongboxEquipmentStatistics> Equipment { get; }
        public IReadOnlyList<StrongboxDiagnosticEntry> Diagnostics { get; }
        public string Fingerprint { get; }

        internal StrongboxSimulationReport WithFingerprint(string fingerprint)
        {
            return new StrongboxSimulationReport(
                Request, Production, GeneratedCount, RejectedCount,
                TargetLevels, ItemLevels, Qualities, AugmentSlots, AugmentLevels,
                AugmentSignatures, AugmentBiases, ExceptionalSlots,
                ExceptionalAugmentLevels, CombinedExceptional, Equipment,
                Diagnostics, fingerprint);
        }

        private static double Percentage(long count, long total)
        {
            return total == 0L ? 0d : 100d * count / total;
        }
    }

    public static class StrongboxSimulationReportFingerprint
    {
        public static string Compute(StrongboxSimulationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "schema", "strongbox-simulation-report-v3");
            AppendRequest(builder, report.Request);
            AppendProduction(builder, report.Production);
            Token(builder, "generated", report.GeneratedCount);
            Token(builder, "rejected", report.RejectedCount);
            Token(builder, "exceptional_slots", report.ExceptionalSlots);
            Token(builder, "exceptional_levels", report.ExceptionalAugmentLevels);
            Token(builder, "exceptional_combined", report.CombinedExceptional);
            Token(builder, "exceptional_slots_percent_bits", Bits(report.ExceptionalSlotPercentage));
            Token(builder, "exceptional_levels_percent_bits", Bits(report.ExceptionalAugmentLevelPercentage));
            Token(builder, "exceptional_combined_percent_bits", Bits(report.CombinedExceptionalPercentage));
            AppendDistribution(builder, "target", report.TargetLevels);
            AppendDistribution(builder, "item", report.ItemLevels);
            AppendDistribution(builder, "quality", report.Qualities);
            AppendDistribution(builder, "slot", report.AugmentSlots);
            AppendDistribution(builder, "level", report.AugmentLevels);
            AppendDistribution(builder, "signature", report.AugmentSignatures);
            AppendDistribution(builder, "bias", report.AugmentBiases);
            for (int index = 0; index < report.Equipment.Count; index++)
            {
                StrongboxEquipmentStatistics value = report.Equipment[index];
                string id = value.Metadata.DefinitionId.ToString();
                Token(builder, "equipment_id", id);
                Token(builder, "equipment_name", value.Metadata.DisplayName);
                Token(builder, "equipment_category", Id(value.Metadata.CategoryId));
                Token(builder, "equipment_family", Id(value.Metadata.FamilyId));
                Token(builder, "equipment_slot", Id(value.Metadata.SlotId));
                Token(builder, "equipment_rarity", Id(value.Metadata.RarityId));
                Token(builder, "equipment_first", value.Metadata.FirstAppearanceLevel);
                Token(builder, "equipment_anchor", value.Metadata.AnchorLevel);
                Token(builder, "equipment_weight_bits", Bits(value.Metadata.AuthoredBaseWeight));
                Token(builder, "equipment_available", value.Metadata.Available ? "1" : "0");
                Token(builder, "equipment_top_only", value.Metadata.TopBoxOnly ? "1" : "0");
                Token(builder, "equipment_ordinary_slots", value.Metadata.OrdinaryMaximumSlots);
                Token(builder, "equipment_absolute_slots", value.Metadata.AbsoluteMaximumSlots);
                Token(builder, "equipment_ordinary_level", value.Metadata.OrdinaryMaximumAugmentLevel);
                Token(builder, "equipment_absolute_level", value.Metadata.AbsoluteMaximumAugmentLevel);
                Token(builder, "equipment_count", value.Count);
                Token(builder, "equipment_avg_item_bits", Bits(value.AverageItemLevel));
                Token(builder, "equipment_avg_slot_bits", Bits(value.AverageSlots));
                Token(builder, "equipment_avg_level_bits", Bits(value.AverageAugmentLevel));
                Token(builder, "equipment_avg_bias_bits", Bits(value.AverageAugmentBias));
                Token(builder, "equipment_exceptional_slots", value.ExceptionalSlots);
                Token(builder, "equipment_exceptional_levels", value.ExceptionalAugmentLevels);
                Token(builder, "equipment_exceptional_combined", value.CombinedExceptional);
                AppendDistribution(builder, "equipment_item", value.ItemLevelDistribution);
                AppendDistribution(builder, "equipment_quality", value.QualityDistribution);
                AppendDistribution(builder, "equipment_slot_dist", value.SlotDistribution);
                AppendDistribution(builder, "equipment_level_dist", value.AugmentLevelDistribution);
                AppendDistribution(builder, "equipment_signature", value.AugmentSignatureDistribution);
                AppendDistribution(builder, "equipment_bias", value.AugmentBiasDistribution);
            }
            for (int index = 0; index < report.Diagnostics.Count; index++)
            {
                Token(builder, "diagnostic_code", report.Diagnostics[index].Code);
                Token(builder, "diagnostic_count", report.Diagnostics[index].Count);
            }
            return StrongboxCanonicalV1.Fingerprint(builder.ToString());
        }

        public static string BiasKey(double value)
        {
            return "bits:" + Bits(value);
        }

        private static void AppendRequest(StringBuilder builder, StrongboxSimulationRequest request)
        {
            Token(builder, "mode", request.Mode.ToString());
            AppendScenario(builder, "primary", request.Primary);
            if (request.Comparison == null) Token(builder, "comparison", "none");
            else AppendScenario(builder, "comparison", request.Comparison);
        }

        private static void AppendScenario(StringBuilder builder, string prefix, StrongboxSimulationScenario value)
        {
            Token(builder, prefix + "_player_level", value.PlayerLevel);
            Token(builder, prefix + "_tier", Id(value.StrongboxTierId));
            Token(builder, prefix + "_sample_count", value.SampleCount);
            Token(builder, prefix + "_root_seed", value.RootSeed.ToString("x16", CultureInfo.InvariantCulture));
            Token(builder, prefix + "_definition", Id(value.EquipmentDefinitionId));
            Token(builder, prefix + "_diagnostic_override", value.DiagnosticEligibilityOverride ? "1" : "0");
        }

        private static void AppendProduction(StringBuilder builder, StrongboxProductionFingerprints value)
        {
            Token(builder, "production_equipment_catalog", value.EquipmentCatalog);
            Token(builder, "production_equipment_projection", value.EquipmentProjection);
            Token(builder, "production_strongbox_policy", value.StrongboxPolicy);
            Token(builder, "production_rarity_policy", value.RarityPolicy);
            Token(builder, "production_item_level_policy", value.ItemLevelPolicy);
            Token(builder, "production_augment_slot_policy", value.AugmentSlotPolicy);
            Token(builder, "production_augment_level_policy", value.AugmentLevelPolicy);
        }

        private static void AppendDistribution(StringBuilder builder, string name, IReadOnlyList<StrongboxDistributionEntry> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                Token(builder, name + "_key", values[index].Key);
                Token(builder, name + "_count", values[index].Count);
                Token(builder, name + "_percent_bits", Bits(values[index].Percentage));
            }
        }

        private static string Bits(double value)
        {
            return BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture);
        }

        private static string Id(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        private static void Token(StringBuilder builder, string name, long value)
        {
            Token(builder, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Token(StringBuilder builder, string name, int value)
        {
            Token(builder, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Token(StringBuilder builder, string name, string value)
        {
            StrongboxCanonicalV1.AppendToken(builder, name, value ?? string.Empty);
        }
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
            public double BiasTotal;
            public long ExceptionalSlots;
            public long ExceptionalLevels;
            public long CombinedExceptional;
            public readonly SortedDictionary<int, long> Items = new SortedDictionary<int, long>();
            public readonly SortedDictionary<string, long> Qualities = new SortedDictionary<string, long>(StringComparer.Ordinal);
            public readonly SortedDictionary<int, long> Slots = new SortedDictionary<int, long>();
            public readonly SortedDictionary<int, long> Levels = new SortedDictionary<int, long>();
            public readonly SortedDictionary<string, long> Signatures = new SortedDictionary<string, long>(StringComparer.Ordinal);
            public readonly SortedDictionary<string, long> Biases = new SortedDictionary<string, long>(StringComparer.Ordinal);
        }

        public StrongboxSimulationReport Run(
            StrongboxSimulationRequest request,
            IStrongboxSimulationProductionGateway production)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (production == null) throw new ArgumentNullException(nameof(production));
            if (request.Mode == StrongboxSimulationMode.DefinitionConditioned)
                throw new NotSupportedException("strongbox-simulation-definition-conditioned-unsupported");
            if (request.Mode != StrongboxSimulationMode.FullOpening)
                throw new NotSupportedException("strongbox-simulation-mode-requires-coordinator");

            var targetLevels = new SortedDictionary<int, long>();
            var itemLevels = new SortedDictionary<int, long>();
            var qualities = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var slots = new SortedDictionary<int, long>();
            var levels = new SortedDictionary<int, long>();
            var signatures = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var biases = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var byDefinition = new SortedDictionary<string, EquipmentAccumulator>(StringComparer.Ordinal);
            var diagnostics = new SortedDictionary<string, long>(StringComparer.Ordinal);
            long generated = 0L;
            long rejected = 0L;
            long exceptionalSlots = 0L;
            long exceptionalLevels = 0L;
            long combinedExceptional = 0L;

            for (long ordinal = 0; ordinal < request.Primary.SampleCount; ordinal++)
            {
                StrongboxGeneratedEquipmentObservation observation;
                string diagnostic;
                if (!production.TryGenerate(request.Primary, ordinal, out observation, out diagnostic)
                    || observation == null)
                {
                    rejected++;
                    Increment(diagnostics, string.IsNullOrWhiteSpace(diagnostic)
                        ? "strongbox-simulation-rejected-unclassified"
                        : diagnostic);
                    continue;
                }

                ValidateProductionResult(observation);
                generated++;
                string quality = Id(observation.QualityId);
                string bias = StrongboxSimulationReportFingerprint.BiasKey(observation.AugmentBias);
                string signature = FormatSignature(observation.AugmentSlotCount, observation.SharedAugmentLevel);
                Increment(targetLevels, observation.TargetLevel);
                Increment(itemLevels, observation.ItemLevel);
                Increment(qualities, quality);
                Increment(slots, observation.AugmentSlotCount);
                if (observation.AugmentSlotCount > 0) Increment(levels, observation.SharedAugmentLevel);
                Increment(signatures, signature);
                Increment(biases, bias);
                if (observation.ExceptionalSlotOutcome) exceptionalSlots++;
                if (observation.ExceptionalAugmentLevelOutcome) exceptionalLevels++;
                if (observation.ExceptionalSlotOutcome && observation.ExceptionalAugmentLevelOutcome)
                    combinedExceptional++;

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
                accumulator.BiasTotal += observation.AugmentBias;
                Increment(accumulator.Items, observation.ItemLevel);
                Increment(accumulator.Qualities, quality);
                Increment(accumulator.Slots, observation.AugmentSlotCount);
                Increment(accumulator.Signatures, signature);
                Increment(accumulator.Biases, bias);
                if (observation.AugmentSlotCount > 0)
                {
                    accumulator.NonzeroSlotCount++;
                    accumulator.AugmentLevelTotal += observation.SharedAugmentLevel;
                    Increment(accumulator.Levels, observation.SharedAugmentLevel);
                }
                if (observation.ExceptionalSlotOutcome) accumulator.ExceptionalSlots++;
                if (observation.ExceptionalAugmentLevelOutcome) accumulator.ExceptionalLevels++;
                if (observation.ExceptionalSlotOutcome && observation.ExceptionalAugmentLevelOutcome)
                    accumulator.CombinedExceptional++;
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
                    value.Count == 0L ? 0d : value.BiasTotal / value.Count,
                    value.ExceptionalSlots,
                    value.ExceptionalLevels,
                    value.CombinedExceptional,
                    Entries(value.Items, value.Count),
                    Entries(value.Qualities, value.Count),
                    Entries(value.Slots, value.Count),
                    Entries(value.Levels, value.NonzeroSlotCount),
                    Entries(value.Signatures, value.Count),
                    Entries(value.Biases, value.Count)));
            }

            var diagnosticEntries = new List<StrongboxDiagnosticEntry>();
            foreach (KeyValuePair<string, long> pair in diagnostics)
                diagnosticEntries.Add(new StrongboxDiagnosticEntry(pair.Key, pair.Value));

            var report = new StrongboxSimulationReport(
                request,
                production.Fingerprints,
                generated,
                rejected,
                Entries(targetLevels, generated),
                Entries(itemLevels, generated),
                Entries(qualities, generated),
                Entries(slots, generated),
                Entries(levels, Sum(levels)),
                Entries(signatures, generated),
                Entries(biases, generated),
                exceptionalSlots,
                exceptionalLevels,
                combinedExceptional,
                new ReadOnlyCollection<StrongboxEquipmentStatistics>(equipment),
                new ReadOnlyCollection<StrongboxDiagnosticEntry>(diagnosticEntries),
                string.Empty);
            return report.WithFingerprint(StrongboxSimulationReportFingerprint.Compute(report));
        }

        private static void ValidateProductionResult(StrongboxGeneratedEquipmentObservation value)
        {
            if (value.Equipment == null)
                throw new StrongboxSimulationIntegrityException("strongbox-simulation-production-equipment-null");
            if (double.IsNaN(value.AugmentBias) || double.IsInfinity(value.AugmentBias))
                throw new StrongboxSimulationIntegrityException("strongbox-simulation-production-bias-non-finite");
            if (value.AugmentSlotCount < 0
                || value.AugmentSlotCount > value.Equipment.AbsoluteMaximumSlots)
                throw new StrongboxSimulationIntegrityException("strongbox-simulation-production-slot-limit-invalid");
            if (value.AugmentSlotCount == 0 && value.SharedAugmentLevel != 0)
                throw new StrongboxSimulationIntegrityException("strongbox-simulation-production-zero-slot-level-invalid");
            if (value.AugmentSlotCount > 0
                && (value.SharedAugmentLevel < 1
                    || value.SharedAugmentLevel > value.Equipment.AbsoluteMaximumAugmentLevel))
                throw new StrongboxSimulationIntegrityException("strongbox-simulation-production-augment-level-limit-invalid");
            bool expectedSlots = value.AugmentSlotCount > value.Equipment.OrdinaryMaximumSlots;
            bool expectedLevels = value.AugmentSlotCount > 0
                && value.SharedAugmentLevel > value.Equipment.OrdinaryMaximumAugmentLevel;
            if (value.ExceptionalSlotOutcome != expectedSlots)
                throw new StrongboxSimulationIntegrityException("strongbox-simulation-production-exceptional-slot-flag-mismatch");
            if (value.ExceptionalAugmentLevelOutcome != expectedLevels)
                throw new StrongboxSimulationIntegrityException("strongbox-simulation-production-exceptional-level-flag-mismatch");
        }

        private static string FormatSignature(int slotCount, int augmentLevel)
        {
            return slotCount.ToString(CultureInfo.InvariantCulture)
                + ":" + (slotCount == 0 ? "none" : augmentLevel.ToString(CultureInfo.InvariantCulture));
        }

        private static string Id(object value)
        {
            return value == null ? string.Empty : value.ToString();
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
            values[key] = count + 1L;
        }

        private static void Increment(SortedDictionary<string, long> values, string key)
        {
            long count;
            values.TryGetValue(key, out count);
            values[key] = count + 1L;
        }

        private static long Sum(SortedDictionary<int, long> values)
        {
            long total = 0L;
            foreach (KeyValuePair<int, long> pair in values) total += pair.Value;
            return total;
        }

        private static double Divide(long numerator, long denominator)
        {
            return denominator == 0L ? 0d : (double)numerator / denominator;
        }
    }
}

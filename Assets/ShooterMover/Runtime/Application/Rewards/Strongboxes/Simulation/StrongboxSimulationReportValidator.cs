using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    public static class StrongboxSimulationReportValidator
    {
        private const double Epsilon = 0.000000001d;

        public static IReadOnlyList<string> Validate(StrongboxSimulationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var diagnostics = new SortedSet<string>(StringComparer.Ordinal);
            if (report.Request == null || report.Production == null)
            {
                diagnostics.Add("report-required-identity-null");
                return Result(diagnostics);
            }

            long requested = report.Request.Primary.SampleCount;
            if (report.GeneratedCount < 0L || report.RejectedCount < 0L)
                diagnostics.Add("report-count-negative");
            if (report.GeneratedCount + report.RejectedCount != requested)
                diagnostics.Add("report-count-total-mismatch");

            ValidateDistribution(report.TargetLevels, report.GeneratedCount, "target-level", true, diagnostics);
            ValidateDistribution(report.ItemLevels, report.GeneratedCount, "item-level", true, diagnostics);
            ValidateDistribution(report.Qualities, report.GeneratedCount, "quality", false, diagnostics);
            ValidateDistribution(report.AugmentSlots, report.GeneratedCount, "augment-slot", true, diagnostics);
            ValidateDistribution(report.AugmentSignatures, report.GeneratedCount, "augment-signature", false, diagnostics);
            ValidateDistribution(report.AugmentBiases, report.GeneratedCount, "augment-bias", false, diagnostics);

            long nonzeroSlots = report.GeneratedCount - Count(report.AugmentSlots, "0");
            ValidateDistribution(report.AugmentLevels, nonzeroSlots, "augment-level", true, diagnostics);
            ValidateSignatureReconciliation(
                report.AugmentSignatures,
                report.AugmentSlots,
                report.AugmentLevels,
                "global",
                diagnostics);
            ValidateBiasDistribution(report.AugmentBiases, "global", diagnostics);

            long equipmentTotal = 0L;
            long exceptionalSlots = 0L;
            long exceptionalLevels = 0L;
            long combinedExceptional = 0L;
            string previousEquipmentId = null;
            for (int index = 0; index < report.Equipment.Count; index++)
            {
                StrongboxEquipmentStatistics equipment = report.Equipment[index];
                if (equipment == null || equipment.Metadata == null)
                {
                    diagnostics.Add("equipment-statistics-null");
                    continue;
                }
                string id = equipment.Metadata.DefinitionId.ToString();
                if (previousEquipmentId != null
                    && string.CompareOrdinal(previousEquipmentId, id) >= 0)
                    diagnostics.Add("equipment-ordering-or-duplicate-invalid");
                previousEquipmentId = id;
                ValidateCanonicalTags(equipment.Metadata, id, diagnostics);
                equipmentTotal += equipment.Count;
                exceptionalSlots += equipment.ExceptionalSlots;
                exceptionalLevels += equipment.ExceptionalAugmentLevels;
                combinedExceptional += equipment.CombinedExceptional;

                ValidateDistribution(equipment.ItemLevelDistribution, equipment.Count, "equipment-item-" + id, true, diagnostics);
                ValidateDistribution(equipment.QualityDistribution, equipment.Count, "equipment-quality-" + id, false, diagnostics);
                ValidateDistribution(equipment.SlotDistribution, equipment.Count, "equipment-slot-" + id, true, diagnostics);
                ValidateDistribution(equipment.AugmentSignatureDistribution, equipment.Count, "equipment-signature-" + id, false, diagnostics);
                ValidateDistribution(equipment.AugmentBiasDistribution, equipment.Count, "equipment-bias-" + id, false, diagnostics);
                long equipmentNonzero = equipment.Count - Count(equipment.SlotDistribution, "0");
                ValidateDistribution(equipment.AugmentLevelDistribution, equipmentNonzero, "equipment-level-" + id, true, diagnostics);
                ValidateSignatureReconciliation(
                    equipment.AugmentSignatureDistribution,
                    equipment.SlotDistribution,
                    equipment.AugmentLevelDistribution,
                    "equipment-" + id,
                    diagnostics);
                ValidateBiasDistribution(equipment.AugmentBiasDistribution, "equipment-" + id, diagnostics);
                ValidateExceptionalCounts(equipment, id, diagnostics);
                ValidateMean(equipment.ItemLevelDistribution, equipment.AverageItemLevel, "equipment-average-item-" + id, diagnostics);
                ValidateMean(equipment.SlotDistribution, equipment.AverageSlots, "equipment-average-slot-" + id, diagnostics);
                ValidateMean(equipment.AugmentLevelDistribution, equipment.AverageAugmentLevel, "equipment-average-level-" + id, diagnostics);
                ValidateBiasMean(equipment.AugmentBiasDistribution, equipment.AverageAugmentBias, "equipment-average-bias-" + id, diagnostics);
            }
            if (equipmentTotal != report.GeneratedCount)
                diagnostics.Add("equipment-count-total-mismatch");
            if (exceptionalSlots != report.ExceptionalSlots)
                diagnostics.Add("global-exceptional-slot-count-mismatch");
            if (exceptionalLevels != report.ExceptionalAugmentLevels)
                diagnostics.Add("global-exceptional-level-count-mismatch");
            if (combinedExceptional != report.CombinedExceptional)
                diagnostics.Add("global-exceptional-combined-count-mismatch");
            if (report.CombinedExceptional > report.ExceptionalSlots
                || report.CombinedExceptional > report.ExceptionalAugmentLevels)
                diagnostics.Add("global-exceptional-combined-count-invalid");

            long diagnosticTotal = 0L;
            string previousDiagnostic = null;
            for (int index = 0; index < report.Diagnostics.Count; index++)
            {
                StrongboxDiagnosticEntry entry = report.Diagnostics[index];
                if (entry == null)
                {
                    diagnostics.Add("diagnostic-entry-null");
                    continue;
                }
                if (entry.Count < 1L) diagnostics.Add("diagnostic-count-invalid");
                if (previousDiagnostic != null
                    && string.CompareOrdinal(previousDiagnostic, entry.Code) >= 0)
                    diagnostics.Add("diagnostic-ordering-or-duplicate-invalid");
                previousDiagnostic = entry.Code;
                diagnosticTotal += entry.Count;
            }
            if (diagnosticTotal != report.RejectedCount)
                diagnostics.Add("diagnostic-rejection-total-mismatch");

            string expectedFingerprint = StrongboxSimulationReportFingerprint.Compute(report);
            if (!string.Equals(expectedFingerprint, report.Fingerprint, StringComparison.Ordinal))
                diagnostics.Add("report-fingerprint-mismatch");

            return Result(diagnostics);
        }

        public static void ThrowIfInvalid(StrongboxSimulationReport report)
        {
            IReadOnlyList<string> diagnostics = Validate(report);
            if (diagnostics.Count == 0) return;
            throw new StrongboxSimulationIntegrityException(
                "strongbox-simulation-report-invalid-" + string.Join("_", diagnostics));
        }

        private static void ValidateDistribution(
            IReadOnlyList<StrongboxDistributionEntry> distribution,
            long expectedTotal,
            string name,
            bool numericOrdering,
            ISet<string> diagnostics)
        {
            if (distribution == null)
            {
                diagnostics.Add(name + "-distribution-null");
                return;
            }
            long total = 0L;
            string previous = null;
            int previousNumber = 0;
            bool hasPreviousNumber = false;
            for (int index = 0; index < distribution.Count; index++)
            {
                StrongboxDistributionEntry entry = distribution[index];
                if (entry == null)
                {
                    diagnostics.Add(name + "-entry-null");
                    continue;
                }
                if (entry.Count < 0L) diagnostics.Add(name + "-count-negative");
                if (numericOrdering)
                {
                    int number;
                    if (!int.TryParse(entry.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                        diagnostics.Add(name + "-numeric-key-invalid");
                    else if (hasPreviousNumber && previousNumber >= number)
                        diagnostics.Add(name + "-ordering-or-duplicate-invalid");
                    previousNumber = number;
                    hasPreviousNumber = true;
                }
                else if (previous != null && string.CompareOrdinal(previous, entry.Key) >= 0)
                {
                    diagnostics.Add(name + "-ordering-or-duplicate-invalid");
                }
                previous = entry.Key;
                total += entry.Count;
                double expectedPercentage = expectedTotal == 0L ? 0d : 100d * entry.Count / expectedTotal;
                if (Math.Abs(expectedPercentage - entry.Percentage) > Epsilon)
                    diagnostics.Add(name + "-percentage-mismatch");
            }
            if (total != expectedTotal) diagnostics.Add(name + "-count-total-mismatch");
        }

        private static void ValidateCanonicalTags(
            StrongboxEquipmentMetadata metadata,
            string id,
            ISet<string> diagnostics)
        {
            StableId previous = null;
            for (int index = 0; index < metadata.CanonicalTags.Count; index++)
            {
                StableId tag = metadata.CanonicalTags[index];
                if (tag == null)
                {
                    diagnostics.Add("equipment-canonical-tag-null-" + id);
                    continue;
                }
                if (previous != null && previous.CompareTo(tag) >= 0)
                    diagnostics.Add("equipment-canonical-tag-ordering-or-duplicate-invalid-" + id);
                previous = tag;
            }
        }

        private static void ValidateSignatureReconciliation(
            IReadOnlyList<StrongboxDistributionEntry> signatures,
            IReadOnlyList<StrongboxDistributionEntry> slots,
            IReadOnlyList<StrongboxDistributionEntry> levels,
            string name,
            ISet<string> diagnostics)
        {
            var slotCounts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var levelCounts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            for (int index = 0; index < signatures.Count; index++)
            {
                int slot;
                int level;
                if (!TryParseSignature(signatures[index].Key, out slot, out level))
                {
                    diagnostics.Add(name + "-signature-malformed");
                    continue;
                }
                Add(slotCounts, slot.ToString(CultureInfo.InvariantCulture), signatures[index].Count);
                if (slot > 0) Add(levelCounts, level.ToString(CultureInfo.InvariantCulture), signatures[index].Count);
            }
            if (!Matches(slots, slotCounts)) diagnostics.Add(name + "-signature-slot-reconciliation-mismatch");
            if (!Matches(levels, levelCounts)) diagnostics.Add(name + "-signature-level-reconciliation-mismatch");
        }

        private static void ValidateExceptionalCounts(
            StrongboxEquipmentStatistics equipment,
            string id,
            ISet<string> diagnostics)
        {
            long slots = 0L;
            long levels = 0L;
            long combined = 0L;
            for (int index = 0; index < equipment.AugmentSignatureDistribution.Count; index++)
            {
                int slot;
                int level;
                if (!TryParseSignature(equipment.AugmentSignatureDistribution[index].Key, out slot, out level))
                    continue;
                bool exceptionalSlot = slot > equipment.Metadata.OrdinaryMaximumSlots;
                bool exceptionalLevel = slot > 0 && level > equipment.Metadata.OrdinaryMaximumAugmentLevel;
                if (slot > equipment.Metadata.AbsoluteMaximumSlots
                    || (slot > 0 && level > equipment.Metadata.AbsoluteMaximumAugmentLevel))
                    diagnostics.Add("equipment-signature-absolute-limit-invalid-" + id);
                if (exceptionalSlot) slots += equipment.AugmentSignatureDistribution[index].Count;
                if (exceptionalLevel) levels += equipment.AugmentSignatureDistribution[index].Count;
                if (exceptionalSlot && exceptionalLevel) combined += equipment.AugmentSignatureDistribution[index].Count;
            }
            if (slots != equipment.ExceptionalSlots) diagnostics.Add("equipment-exceptional-slot-count-mismatch-" + id);
            if (levels != equipment.ExceptionalAugmentLevels) diagnostics.Add("equipment-exceptional-level-count-mismatch-" + id);
            if (combined != equipment.CombinedExceptional) diagnostics.Add("equipment-exceptional-combined-count-mismatch-" + id);
        }

        private static void ValidateBiasDistribution(
            IReadOnlyList<StrongboxDistributionEntry> values,
            string name,
            ISet<string> diagnostics)
        {
            for (int index = 0; index < values.Count; index++)
            {
                double ignored;
                if (!StrongboxSimulationBiasMath.TryParseKey(values[index].Key, out ignored))
                    diagnostics.Add(name + "-bias-key-invalid");
            }
        }

        private static void ValidateMean(
            IReadOnlyList<StrongboxDistributionEntry> values,
            double published,
            string name,
            ISet<string> diagnostics)
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
            double expected = count == 0L ? 0d : total / count;
            if (BitConverter.DoubleToInt64Bits(expected) != BitConverter.DoubleToInt64Bits(published))
                diagnostics.Add(name + "-mismatch");
        }

        private static void ValidateBiasMean(
            IReadOnlyList<StrongboxDistributionEntry> values,
            double published,
            string name,
            ISet<string> diagnostics)
        {
            double expected;
            try
            {
                expected = StrongboxSimulationBiasMath.Average(values);
            }
            catch (StrongboxSimulationIntegrityException)
            {
                diagnostics.Add(name + "-source-invalid");
                return;
            }
            if (BitConverter.DoubleToInt64Bits(expected) != BitConverter.DoubleToInt64Bits(published))
                diagnostics.Add(name + "-mismatch");
        }

        private static bool TryParseSignature(string signature, out int slots, out int level)
        {
            slots = 0;
            level = 0;
            int separator = signature == null ? -1 : signature.IndexOf(':');
            if (separator <= 0
                || !int.TryParse(signature.Substring(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out slots)
                || slots < 0)
                return false;
            string levelPart = signature.Substring(separator + 1);
            if (slots == 0) return string.Equals(levelPart, "none", StringComparison.Ordinal);
            return int.TryParse(levelPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out level) && level >= 1;
        }

        private static long Count(IReadOnlyList<StrongboxDistributionEntry> values, string key)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(values[index].Key, key, StringComparison.Ordinal)) return values[index].Count;
            return 0L;
        }

        private static bool Matches(
            IReadOnlyList<StrongboxDistributionEntry> published,
            SortedDictionary<string, long> expected)
        {
            if (published.Count != expected.Count) return false;
            foreach (KeyValuePair<string, long> pair in expected)
            {
                bool found = false;
                for (int index = 0; index < published.Count; index++)
                {
                    if (string.Equals(published[index].Key, pair.Key, StringComparison.Ordinal)
                        && published[index].Count == pair.Value)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        private static void Add(SortedDictionary<string, long> values, string key, long amount)
        {
            long current;
            values.TryGetValue(key, out current);
            values[key] = current + amount;
        }

        private static IReadOnlyList<string> Result(SortedSet<string> values)
        {
            return new ReadOnlyCollection<string>(new List<string>(values));
        }
    }
}

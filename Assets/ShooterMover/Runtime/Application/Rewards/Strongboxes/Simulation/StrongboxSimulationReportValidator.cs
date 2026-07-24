using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    public static class StrongboxSimulationReportValidator
    {
        public static IReadOnlyList<string> Validate(StrongboxSimulationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var diagnostics = new SortedSet<string>(StringComparer.Ordinal);

            long requested = report.Request.Primary.SampleCount;
            if (report.GeneratedCount < 0L || report.RejectedCount < 0L)
                diagnostics.Add("report-count-negative");
            if (report.GeneratedCount + report.RejectedCount != requested)
                diagnostics.Add("report-count-total-mismatch");

            ValidateDistribution(
                report.TargetLevels,
                report.GeneratedCount,
                "target-level",
                diagnostics);
            ValidateDistribution(
                report.ItemLevels,
                report.GeneratedCount,
                "item-level",
                diagnostics);
            ValidateDistribution(
                report.AugmentSlots,
                report.GeneratedCount,
                "augment-slot",
                diagnostics);
            ValidateDistribution(
                report.AugmentSignatures,
                report.GeneratedCount,
                "augment-signature",
                diagnostics);

            long equipmentTotal = 0L;
            for (int index = 0; index < report.Equipment.Count; index++)
            {
                StrongboxEquipmentStatistics equipment = report.Equipment[index];
                if (equipment == null || equipment.Metadata == null)
                {
                    diagnostics.Add("equipment-statistics-null");
                    continue;
                }
                equipmentTotal += equipment.Count;
                string id = equipment.Metadata.DefinitionId.ToString();
                ValidateDistribution(
                    equipment.SlotDistribution,
                    equipment.Count,
                    "equipment-slot-" + id,
                    diagnostics);
                ValidateDistribution(
                    equipment.AugmentSignatureDistribution,
                    equipment.Count,
                    "equipment-signature-" + id,
                    diagnostics);
                ValidateEquipmentSignatures(equipment, diagnostics);
                if (equipment.ExceptionalSlots < 0L
                    || equipment.ExceptionalSlots > equipment.Count)
                    diagnostics.Add("equipment-exceptional-slot-count-invalid-" + id);
                if (equipment.ExceptionalAugmentLevels < 0L
                    || equipment.ExceptionalAugmentLevels > equipment.Count)
                    diagnostics.Add("equipment-exceptional-level-count-invalid-" + id);
            }
            if (equipmentTotal != report.GeneratedCount)
                diagnostics.Add("equipment-count-total-mismatch");

            return new ReadOnlyCollection<string>(new List<string>(diagnostics));
        }

        public static void ThrowIfInvalid(StrongboxSimulationReport report)
        {
            IReadOnlyList<string> diagnostics = Validate(report);
            if (diagnostics.Count == 0) return;
            throw new InvalidOperationException(
                "Strongbox simulation report integrity failed: "
                + string.Join(",", diagnostics));
        }

        private static void ValidateDistribution(
            IReadOnlyList<StrongboxDistributionEntry> distribution,
            long expectedTotal,
            string name,
            ISet<string> diagnostics)
        {
            if (distribution == null)
            {
                diagnostics.Add(name + "-distribution-null");
                return;
            }
            long total = 0L;
            string previous = null;
            for (int index = 0; index < distribution.Count; index++)
            {
                StrongboxDistributionEntry entry = distribution[index];
                if (entry == null)
                {
                    diagnostics.Add(name + "-entry-null");
                    continue;
                }
                if (entry.Count < 0L)
                    diagnostics.Add(name + "-count-negative");
                if (previous != null
                    && string.CompareOrdinal(previous, entry.Key) >= 0)
                    diagnostics.Add(name + "-ordering-or-duplicate-invalid");
                previous = entry.Key;
                total += entry.Count;
                double expectedPercentage = expectedTotal == 0L
                    ? 0d
                    : 100d * entry.Count / expectedTotal;
                if (Math.Abs(expectedPercentage - entry.Percentage) > 0.000000001d)
                    diagnostics.Add(name + "-percentage-mismatch");
            }
            if (total != expectedTotal)
                diagnostics.Add(name + "-count-total-mismatch");
        }

        private static void ValidateEquipmentSignatures(
            StrongboxEquipmentStatistics equipment,
            ISet<string> diagnostics)
        {
            string id = equipment.Metadata.DefinitionId.ToString();
            for (int index = 0;
                 index < equipment.AugmentSignatureDistribution.Count;
                 index++)
            {
                string signature = equipment.AugmentSignatureDistribution[index].Key;
                int separator = signature == null ? -1 : signature.IndexOf(':');
                int slots;
                if (separator <= 0
                    || !int.TryParse(signature.Substring(0, separator), out slots)
                    || slots < 0)
                {
                    diagnostics.Add("equipment-signature-malformed-" + id);
                    continue;
                }
                string levelPart = signature.Substring(separator + 1);
                if (slots == 0)
                {
                    if (!string.Equals(levelPart, "none", StringComparison.Ordinal))
                        diagnostics.Add("equipment-zero-slot-signature-level-invalid-" + id);
                    continue;
                }
                int level;
                if (!int.TryParse(levelPart, out level) || level < 1)
                    diagnostics.Add("equipment-signature-level-invalid-" + id);
            }
        }
    }
}

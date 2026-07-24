using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    public static class StrongboxSimulationSerializers
    {
        public static string ToMarkdown(StrongboxSimulationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var builder = new StringBuilder();
            builder.AppendLine("# Strongbox simulation report");
            builder.AppendLine();
            builder.AppendLine("- Mode: `" + report.Request.Mode + "`");
            builder.AppendLine("- Player level: `" + report.Request.Primary.PlayerLevel.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Strongbox tier: `" + report.Request.Primary.StrongboxTierId + "`");
            builder.AppendLine("- Requested samples: `" + report.Request.Primary.SampleCount.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Generated: `" + report.GeneratedCount.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Rejected: `" + report.RejectedCount.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Root seed: `" + report.Request.Primary.RootSeed.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Exceptional slots: `" + CountPercent(report.ExceptionalSlots, report.ExceptionalSlotPercentage) + "`");
            builder.AppendLine("- Exceptional augment levels: `" + CountPercent(report.ExceptionalAugmentLevels, report.ExceptionalAugmentLevelPercentage) + "`");
            builder.AppendLine("- Combined exceptional slot and level: `" + CountPercent(report.CombinedExceptional, report.CombinedExceptionalPercentage) + "`");
            builder.AppendLine("- Equipment catalog fingerprint: `" + report.Production.EquipmentCatalog + "`");
            builder.AppendLine("- Equipment projection fingerprint: `" + report.Production.EquipmentProjection + "`");
            builder.AppendLine("- Strongbox policy fingerprint: `" + report.Production.StrongboxPolicy + "`");
            builder.AppendLine("- Rarity policy projection: `" + report.Production.RarityPolicy + "`");
            builder.AppendLine("- Item-level policy projection: `" + report.Production.ItemLevelPolicy + "`");
            builder.AppendLine("- Augment-slot policy projection: `" + report.Production.AugmentSlotPolicy + "`");
            builder.AppendLine("- Augment-level policy projection: `" + report.Production.AugmentLevelPolicy + "`");
            builder.AppendLine("- Report fingerprint: `" + report.Fingerprint + "`");
            AppendTable(builder, "Target levels", report.TargetLevels);
            AppendTable(builder, "Item levels", report.ItemLevels);
            AppendTable(builder, "Qualities", report.Qualities);
            AppendTable(builder, "Augment slots", report.AugmentSlots);
            AppendTable(builder, "Augment levels (conditional on slots)", report.AugmentLevels);
            AppendTable(builder, "Combined augment signatures", report.AugmentSignatures);
            AppendTable(builder, "Augment bias (exact IEEE-754 keys)", report.AugmentBiases);

            builder.AppendLine();
            builder.AppendLine("## Equipment");
            builder.AppendLine();
            builder.AppendLine("| Definition ID | Name | Count | Avg item | Avg slots | Avg level | Avg bias | Exceptional slots | Exceptional levels | Combined exceptional |");
            builder.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            for (int index = 0; index < report.Equipment.Count; index++)
            {
                StrongboxEquipmentStatistics value = report.Equipment[index];
                builder.Append('|').Append(Escape(value.Metadata.DefinitionId.ToString()))
                    .Append('|').Append(Escape(value.Metadata.DisplayName))
                    .Append('|').Append(value.Count.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(Number(value.AverageItemLevel))
                    .Append('|').Append(Number(value.AverageSlots))
                    .Append('|').Append(Number(value.AverageAugmentLevel))
                    .Append('|').Append(Number(value.AverageAugmentBias))
                    .Append('|').Append(CountPercent(value.ExceptionalSlots, value.ExceptionalSlotPercentage))
                    .Append('|').Append(CountPercent(value.ExceptionalAugmentLevels, value.ExceptionalAugmentLevelPercentage))
                    .Append('|').Append(CountPercent(value.CombinedExceptional, value.CombinedExceptionalPercentage))
                    .AppendLine("|");
            }

            if (report.Diagnostics.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Rejection diagnostics");
                builder.AppendLine();
                builder.AppendLine("| Code | Count |");
                builder.AppendLine("|---|---:|");
                for (int index = 0; index < report.Diagnostics.Count; index++)
                    builder.Append('|').Append(Escape(report.Diagnostics[index].Code))
                        .Append('|').Append(report.Diagnostics[index].Count.ToString(CultureInfo.InvariantCulture))
                        .AppendLine("|");
            }
            return builder.ToString();
        }

        public static string CatalogCoverageMarkdown(IReadOnlyList<StrongboxCatalogCoverageEntry> coverage)
        {
            if (coverage == null) throw new ArgumentNullException(nameof(coverage));
            var builder = new StringBuilder();
            builder.AppendLine("## Catalog coverage");
            builder.AppendLine();
            builder.AppendLine("| Definition ID | Name | Category | Family | Slot | Observed | Interpretation | Diagnostic |");
            builder.AppendLine("|---|---|---|---|---|---:|---|---|");
            for (int index = 0; index < coverage.Count; index++)
            {
                StrongboxCatalogCoverageEntry value = coverage[index];
                builder.Append('|').Append(Escape(value.Metadata.DefinitionId.ToString()))
                    .Append('|').Append(Escape(value.Metadata.DisplayName))
                    .Append('|').Append(Escape(value.Metadata.CategoryId.ToString()))
                    .Append('|').Append(Escape(Id(value.Metadata.FamilyId)))
                    .Append('|').Append(Escape(Id(value.Metadata.SlotId)))
                    .Append('|').Append(value.ObservedCount.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(Escape(value.Interpretation.ToString()))
                    .Append('|').Append(Escape(value.Diagnostic))
                    .AppendLine("|");
            }
            return builder.ToString();
        }

        public static string RareOutcomeMarkdown(StrongboxRareOutcomeResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var builder = new StringBuilder();
            builder.AppendLine("## Rare outcome: " + Escape(result.Query.QueryId));
            builder.AppendLine();
            builder.AppendLine("- Equipment definition: `" + Id(result.Query.EquipmentDefinitionId) + "`");
            builder.AppendLine("- Minimum slots: `" + result.Query.MinimumSlots.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Require slots above ordinary maximum: `" + result.Query.RequireSlotsAboveOrdinaryMaximum + "`");
            builder.AppendLine("- Minimum augment level: `" + result.Query.MinimumAugmentLevel.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Require augment level above ordinary maximum: `" + result.Query.RequireAugmentLevelAboveOrdinaryMaximum + "`");
            builder.AppendLine("- Observed: `" + result.ObservedCount.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Evaluated samples: `" + result.SampleCount.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Observed probability: `" + Number(result.ObservedProbability) + "`");
            builder.AppendLine("- Zero-observation upper bound: `" + NullableDouble(result.ZeroObservationUpperBound) + "`");
            builder.AppendLine("- Suggested sample count: `" + NullableLong(result.SuggestedSampleCount) + "`");
            builder.AppendLine("- Interpretation: " + Escape(result.Interpretation));
            return builder.ToString();
        }

        public static string EquipmentCsv(StrongboxSimulationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var builder = new StringBuilder();
            builder.AppendLine("definition_id,display_name,category_id,family_id,slot_id,rarity_id,count,observed_percentage,boxes_per_drop,average_item_level,average_slots,average_augment_level,average_augment_bias,exceptional_slot_count,exceptional_slot_percentage,exceptional_level_count,exceptional_level_percentage,combined_exceptional_count,combined_exceptional_percentage");
            for (int index = 0; index < report.Equipment.Count; index++)
            {
                StrongboxEquipmentStatistics value = report.Equipment[index];
                double percentage = report.GeneratedCount == 0L ? 0d : 100d * value.Count / report.GeneratedCount;
                double boxes = value.Count == 0L ? double.PositiveInfinity : (double)report.Request.Primary.SampleCount / value.Count;
                Csv(builder, value.Metadata.DefinitionId.ToString());
                Csv(builder, value.Metadata.DisplayName);
                Csv(builder, value.Metadata.CategoryId.ToString());
                Csv(builder, Id(value.Metadata.FamilyId));
                Csv(builder, Id(value.Metadata.SlotId));
                Csv(builder, Id(value.Metadata.RarityId));
                Csv(builder, value.Count.ToString(CultureInfo.InvariantCulture));
                Csv(builder, Number(percentage));
                Csv(builder, double.IsPositiveInfinity(boxes) ? string.Empty : Number(boxes));
                Csv(builder, Number(value.AverageItemLevel));
                Csv(builder, Number(value.AverageSlots));
                Csv(builder, Number(value.AverageAugmentLevel));
                Csv(builder, Number(value.AverageAugmentBias));
                Csv(builder, value.ExceptionalSlots.ToString(CultureInfo.InvariantCulture));
                Csv(builder, Number(value.ExceptionalSlotPercentage));
                Csv(builder, value.ExceptionalAugmentLevels.ToString(CultureInfo.InvariantCulture));
                Csv(builder, Number(value.ExceptionalAugmentLevelPercentage));
                Csv(builder, value.CombinedExceptional.ToString(CultureInfo.InvariantCulture));
                Csv(builder, Number(value.CombinedExceptionalPercentage), true);
            }
            return builder.ToString();
        }

        public static string DiagnosticsCsv(StrongboxSimulationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var builder = new StringBuilder();
            builder.AppendLine("diagnostic_code,count");
            for (int index = 0; index < report.Diagnostics.Count; index++)
            {
                Csv(builder, report.Diagnostics[index].Code);
                Csv(builder, report.Diagnostics[index].Count.ToString(CultureInfo.InvariantCulture), true);
            }
            return builder.ToString();
        }

        public static string CatalogCoverageCsv(IReadOnlyList<StrongboxCatalogCoverageEntry> coverage)
        {
            if (coverage == null) throw new ArgumentNullException(nameof(coverage));
            var builder = new StringBuilder();
            builder.AppendLine("definition_id,display_name,category_id,family_id,slot_id,available,top_box_only,observed_count,interpretation,diagnostic");
            for (int index = 0; index < coverage.Count; index++)
            {
                StrongboxCatalogCoverageEntry value = coverage[index];
                Csv(builder, value.Metadata.DefinitionId.ToString());
                Csv(builder, value.Metadata.DisplayName);
                Csv(builder, value.Metadata.CategoryId.ToString());
                Csv(builder, Id(value.Metadata.FamilyId));
                Csv(builder, Id(value.Metadata.SlotId));
                Csv(builder, value.Metadata.Available.ToString(CultureInfo.InvariantCulture));
                Csv(builder, value.Metadata.TopBoxOnly.ToString(CultureInfo.InvariantCulture));
                Csv(builder, value.ObservedCount.ToString(CultureInfo.InvariantCulture));
                Csv(builder, value.Interpretation.ToString());
                Csv(builder, value.Diagnostic, true);
            }
            return builder.ToString();
        }

        public static string RareOutcomeCsv(StrongboxRareOutcomeResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var builder = new StringBuilder();
            builder.AppendLine("query_id,equipment_definition_id,minimum_slots,slots_above_ordinary_maximum,minimum_augment_level,augment_level_above_ordinary_maximum,observed_count,sample_count,observed_probability,zero_observation_upper_bound,suggested_sample_count,interpretation");
            Csv(builder, result.Query.QueryId);
            Csv(builder, Id(result.Query.EquipmentDefinitionId));
            Csv(builder, result.Query.MinimumSlots.ToString(CultureInfo.InvariantCulture));
            Csv(builder, result.Query.RequireSlotsAboveOrdinaryMaximum.ToString(CultureInfo.InvariantCulture));
            Csv(builder, result.Query.MinimumAugmentLevel.ToString(CultureInfo.InvariantCulture));
            Csv(builder, result.Query.RequireAugmentLevelAboveOrdinaryMaximum.ToString(CultureInfo.InvariantCulture));
            Csv(builder, result.ObservedCount.ToString(CultureInfo.InvariantCulture));
            Csv(builder, result.SampleCount.ToString(CultureInfo.InvariantCulture));
            Csv(builder, Number(result.ObservedProbability));
            Csv(builder, NullableDouble(result.ZeroObservationUpperBound));
            Csv(builder, NullableLong(result.SuggestedSampleCount));
            Csv(builder, result.Interpretation, true);
            return builder.ToString();
        }

        public static string DistributionCsv(string dimension, IReadOnlyList<StrongboxDistributionEntry> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var builder = new StringBuilder();
            builder.AppendLine("dimension,key,count,percentage");
            for (int index = 0; index < values.Count; index++)
            {
                Csv(builder, dimension);
                Csv(builder, values[index].Key);
                Csv(builder, values[index].Count.ToString(CultureInfo.InvariantCulture));
                Csv(builder, Number(values[index].Percentage), true);
            }
            return builder.ToString();
        }

        private static void AppendTable(StringBuilder builder, string title, IReadOnlyList<StrongboxDistributionEntry> values)
        {
            builder.AppendLine();
            builder.AppendLine("## " + title);
            builder.AppendLine();
            builder.AppendLine("| Value | Count | Percentage |");
            builder.AppendLine("|---|---:|---:|");
            for (int index = 0; index < values.Count; index++)
                builder.Append('|').Append(Escape(values[index].Key))
                    .Append('|').Append(values[index].Count.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(Number(values[index].Percentage)).AppendLine("%|");
        }

        private static string CountPercent(long count, double percentage)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " (" + Number(percentage) + "%)";
        }

        private static string Number(double value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        private static string Id(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        private static string NullableDouble(double? value)
        {
            return value.HasValue ? Number(value.Value) : string.Empty;
        }

        private static string NullableLong(long? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static void Csv(StringBuilder builder, string value, bool last = false)
        {
            string safe = (value ?? string.Empty).Replace("\"", "\"\"");
            builder.Append('"').Append(safe).Append('"').Append(last ? '\n' : ',');
        }
    }
}

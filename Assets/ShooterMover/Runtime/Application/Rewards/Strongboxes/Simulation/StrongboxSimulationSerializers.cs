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
            builder.AppendLine("- Equipment catalog fingerprint: `" + report.Production.EquipmentCatalog + "`");
            builder.AppendLine("- Equipment projection fingerprint: `" + report.Production.EquipmentProjection + "`");
            builder.AppendLine("- Strongbox policy fingerprint: `" + report.Production.StrongboxPolicy + "`");
            builder.AppendLine("- Report fingerprint: `" + report.Fingerprint + "`");
            AppendTable(builder, "Target levels", report.TargetLevels);
            AppendTable(builder, "Item levels", report.ItemLevels);
            AppendTable(builder, "Augment slots", report.AugmentSlots);
            AppendTable(builder, "Augment levels (conditional on slots)", report.AugmentLevels);
            AppendTable(builder, "Combined augment signatures", report.AugmentSignatures);
            builder.AppendLine();
            builder.AppendLine("## Equipment");
            builder.AppendLine();
            builder.AppendLine("| Definition ID | Name | Category | Family | Slot | Count | Avg item level | Avg slots | Avg augment level | Exceptional slots | Exceptional levels |");
            builder.AppendLine("|---|---|---|---|---|---:|---:|---:|---:|---:|---:|");
            for (int index = 0; index < report.Equipment.Count; index++)
            {
                StrongboxEquipmentStatistics value = report.Equipment[index];
                builder.Append('|').Append(Escape(value.Metadata.DefinitionId.ToString()))
                    .Append('|').Append(Escape(value.Metadata.DisplayName))
                    .Append('|').Append(Escape(value.Metadata.CategoryId.ToString()))
                    .Append('|').Append(Escape(value.Metadata.FamilyId == null ? string.Empty : value.Metadata.FamilyId.ToString()))
                    .Append('|').Append(Escape(value.Metadata.SlotId == null ? string.Empty : value.Metadata.SlotId.ToString()))
                    .Append('|').Append(value.Count.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(value.AverageItemLevel.ToString("0.######", CultureInfo.InvariantCulture))
                    .Append('|').Append(value.AverageSlots.ToString("0.######", CultureInfo.InvariantCulture))
                    .Append('|').Append(value.AverageAugmentLevel.ToString("0.######", CultureInfo.InvariantCulture))
                    .Append('|').Append(value.ExceptionalSlots.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(value.ExceptionalAugmentLevels.ToString(CultureInfo.InvariantCulture))
                    .AppendLine("|");
            }
            if (report.Diagnostics.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Diagnostics");
                for (int index = 0; index < report.Diagnostics.Count; index++)
                    builder.AppendLine("- `" + report.Diagnostics[index] + "`");
            }
            return builder.ToString();
        }

        public static string CatalogCoverageMarkdown(
            IReadOnlyList<StrongboxCatalogCoverageEntry> coverage)
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
            builder.AppendLine("- Observed probability: `" + result.ObservedProbability.ToString("0.########", CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- Zero-observation upper bound: `" + NullableDouble(result.ZeroObservationUpperBound) + "`");
            builder.AppendLine("- Suggested sample count: `" + NullableLong(result.SuggestedSampleCount) + "`");
            builder.AppendLine("- Interpretation: " + Escape(result.Interpretation));
            return builder.ToString();
        }

        public static string EquipmentCsv(StrongboxSimulationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var builder = new StringBuilder();
            builder.AppendLine("definition_id,display_name,category_id,family_id,slot_id,rarity_id,count,observed_percentage,boxes_per_drop,average_item_level,average_slots,average_augment_level,exceptional_slot_count,exceptional_level_count");
            for (int index = 0; index < report.Equipment.Count; index++)
            {
                StrongboxEquipmentStatistics value = report.Equipment[index];
                double percentage = report.GeneratedCount == 0 ? 0d : 100d * value.Count / report.GeneratedCount;
                double boxes = value.Count == 0 ? double.PositiveInfinity : (double)report.Request.Primary.SampleCount / value.Count;
                Csv(builder, value.Metadata.DefinitionId.ToString());
                Csv(builder, value.Metadata.DisplayName);
                Csv(builder, value.Metadata.CategoryId.ToString());
                Csv(builder, Id(value.Metadata.FamilyId));
                Csv(builder, Id(value.Metadata.SlotId));
                Csv(builder, Id(value.Metadata.RarityId));
                Csv(builder, value.Count.ToString(CultureInfo.InvariantCulture));
                Csv(builder, percentage.ToString("0.########", CultureInfo.InvariantCulture));
                Csv(builder, double.IsPositiveInfinity(boxes) ? string.Empty : boxes.ToString("0.########", CultureInfo.InvariantCulture));
                Csv(builder, value.AverageItemLevel.ToString("0.########", CultureInfo.InvariantCulture));
                Csv(builder, value.AverageSlots.ToString("0.########", CultureInfo.InvariantCulture));
                Csv(builder, value.AverageAugmentLevel.ToString("0.########", CultureInfo.InvariantCulture));
                Csv(builder, value.ExceptionalSlots.ToString(CultureInfo.InvariantCulture));
                Csv(builder, value.ExceptionalAugmentLevels.ToString(CultureInfo.InvariantCulture), true);
            }
            return builder.ToString();
        }

        public static string CatalogCoverageCsv(
            IReadOnlyList<StrongboxCatalogCoverageEntry> coverage)
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
            Csv(builder, result.ObservedProbability.ToString("0.########", CultureInfo.InvariantCulture));
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
                StrongboxDistributionEntry value = values[index];
                Csv(builder, dimension);
                Csv(builder, value.Key);
                Csv(builder, value.Count.ToString(CultureInfo.InvariantCulture));
                Csv(builder, value.Percentage.ToString("0.########", CultureInfo.InvariantCulture), true);
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
            {
                StrongboxDistributionEntry value = values[index];
                builder.Append('|').Append(Escape(value.Key))
                    .Append('|').Append(value.Count.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(value.Percentage.ToString("0.######", CultureInfo.InvariantCulture))
                    .AppendLine("%|");
            }
        }

        private static string Id(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        private static string NullableDouble(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.########", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string NullableLong(long? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
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

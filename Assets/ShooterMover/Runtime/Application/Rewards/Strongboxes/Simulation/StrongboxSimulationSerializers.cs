using System;
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
                Csv(builder, value.Metadata.FamilyId == null ? string.Empty : value.Metadata.FamilyId.ToString());
                Csv(builder, value.Metadata.SlotId == null ? string.Empty : value.Metadata.SlotId.ToString());
                Csv(builder, value.Metadata.RarityId == null ? string.Empty : value.Metadata.RarityId.ToString());
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

        public static string DistributionCsv(string dimension, System.Collections.Generic.IReadOnlyList<StrongboxDistributionEntry> values)
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

        private static void AppendTable(StringBuilder builder, string title, System.Collections.Generic.IReadOnlyList<StrongboxDistributionEntry> values)
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

using System;
using System.Globalization;
using System.Text;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    public static class StrongboxSimulationSignatureSerializers
    {
        public static string ToMarkdown(StrongboxSimulationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var builder = new StringBuilder();
            builder.AppendLine("## Per-equipment augment signatures");
            builder.AppendLine();
            builder.AppendLine("| Definition ID | Name | Signature | Count | Percentage within definition |");
            builder.AppendLine("|---|---|---|---:|---:|");
            for (int equipmentIndex = 0; equipmentIndex < report.Equipment.Count; equipmentIndex++)
            {
                StrongboxEquipmentStatistics equipment = report.Equipment[equipmentIndex];
                for (int signatureIndex = 0;
                     signatureIndex < equipment.AugmentSignatureDistribution.Count;
                     signatureIndex++)
                {
                    StrongboxDistributionEntry signature =
                        equipment.AugmentSignatureDistribution[signatureIndex];
                    builder.Append('|').Append(Escape(equipment.Metadata.DefinitionId.ToString()))
                        .Append('|').Append(Escape(equipment.Metadata.DisplayName))
                        .Append('|').Append(Escape(signature.Key))
                        .Append('|').Append(signature.Count.ToString(CultureInfo.InvariantCulture))
                        .Append('|').Append(signature.Percentage.ToString("0.######", CultureInfo.InvariantCulture))
                        .AppendLine("%|");
                }
            }
            return builder.ToString();
        }

        public static string ToCsv(StrongboxSimulationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var builder = new StringBuilder();
            builder.AppendLine("definition_id,display_name,signature,slot_count,augment_level,count,percentage_within_definition");
            for (int equipmentIndex = 0; equipmentIndex < report.Equipment.Count; equipmentIndex++)
            {
                StrongboxEquipmentStatistics equipment = report.Equipment[equipmentIndex];
                for (int signatureIndex = 0;
                     signatureIndex < equipment.AugmentSignatureDistribution.Count;
                     signatureIndex++)
                {
                    StrongboxDistributionEntry signature =
                        equipment.AugmentSignatureDistribution[signatureIndex];
                    ParseSignature(signature.Key, out string slots, out string level);
                    Csv(builder, equipment.Metadata.DefinitionId.ToString());
                    Csv(builder, equipment.Metadata.DisplayName);
                    Csv(builder, signature.Key);
                    Csv(builder, slots);
                    Csv(builder, level);
                    Csv(builder, signature.Count.ToString(CultureInfo.InvariantCulture));
                    Csv(builder, signature.Percentage.ToString("0.########", CultureInfo.InvariantCulture), true);
                }
            }
            return builder.ToString();
        }

        private static void ParseSignature(
            string signature,
            out string slots,
            out string level)
        {
            string value = signature ?? string.Empty;
            int separator = value.IndexOf(':');
            if (separator < 0)
            {
                slots = string.Empty;
                level = string.Empty;
                return;
            }
            slots = value.Substring(0, separator);
            string rawLevel = value.Substring(separator + 1);
            level = string.Equals(rawLevel, "none", StringComparison.Ordinal)
                ? string.Empty
                : rawLevel;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("|", "\\|")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static void Csv(StringBuilder builder, string value, bool last = false)
        {
            string safe = (value ?? string.Empty).Replace("\"", "\"\"");
            builder.Append('"').Append(safe).Append('"').Append(last ? '\n' : ',');
        }
    }
}

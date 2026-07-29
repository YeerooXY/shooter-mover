using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// Durable character-owned state for generated augment metadata. Committed
    /// signatures belong to equipment already admitted by the holdings authority.
    /// Staged signatures are immutable opening intents retained only so an interrupted
    /// RAP claim can roll forward without rerolling.
    /// </summary>
    public sealed class GeneratedEquipmentAugmentSignatureSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<GeneratedEquipmentAugmentSignature>
            committed;
        private readonly ReadOnlyCollection<GeneratedEquipmentAugmentSignature>
            staged;
        private readonly string canonicalText;

        public GeneratedEquipmentAugmentSignatureSnapshot(
            IEnumerable<GeneratedEquipmentAugmentSignature> committed,
            IEnumerable<GeneratedEquipmentAugmentSignature> staged,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }
            SchemaVersion = schemaVersion;
            this.committed = Freeze(committed, "committed");
            this.staged = Freeze(staged, "staged");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < this.committed.Count; index++)
            {
                ids.Add(this.committed[index].EquipmentInstanceStableId.ToString());
            }
            for (int index = 0; index < this.staged.Count; index++)
            {
                if (!ids.Add(this.staged[index].EquipmentInstanceStableId.ToString()))
                {
                    throw new ArgumentException(
                        "A generated augment signature cannot be both committed and staged.",
                        nameof(staged));
                }
            }

            var builder = new StringBuilder(
                "schema=generated-equipment-augment-signature-snapshot-v1");
            builder.Append("\nschema_version=")
                .Append(SchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append("\ncommitted_count=")
                .Append(this.committed.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.committed.Count; index++)
            {
                builder.Append("\ncommitted_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("=")
                    .Append(this.committed[index].Fingerprint);
            }
            builder.Append("\nstaged_count=")
                .Append(this.staged.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.staged.Count; index++)
            {
                builder.Append("\nstaged_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("=")
                    .Append(this.staged[index].Fingerprint);
            }
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprint.Compute(canonicalText);
        }

        public int SchemaVersion { get; }

        public IReadOnlyList<GeneratedEquipmentAugmentSignature> Committed
        {
            get { return committed; }
        }

        public IReadOnlyList<GeneratedEquipmentAugmentSignature> Staged
        {
            get { return staged; }
        }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private static ReadOnlyCollection<GeneratedEquipmentAugmentSignature>
            Freeze(
                IEnumerable<GeneratedEquipmentAugmentSignature> source,
                string parameterName)
        {
            var values = new List<GeneratedEquipmentAugmentSignature>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (GeneratedEquipmentAugmentSignature value in
                source ?? Array.Empty<GeneratedEquipmentAugmentSignature>())
            {
                if (value == null
                    || !ids.Add(value.EquipmentInstanceStableId.ToString()))
                {
                    throw new ArgumentException(
                        "Generated augment signatures must be non-null and unique.",
                        parameterName);
                }
                values.Add(value);
            }
            values.Sort();
            return new ReadOnlyCollection<GeneratedEquipmentAugmentSignature>(
                values);
        }
    }
}

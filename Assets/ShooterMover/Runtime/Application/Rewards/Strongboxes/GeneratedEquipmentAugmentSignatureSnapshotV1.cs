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
    public sealed class GeneratedEquipmentAugmentSignatureSnapshotV1
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<GeneratedEquipmentAugmentSignatureV1>
            committed;
        private readonly ReadOnlyCollection<GeneratedEquipmentAugmentSignatureV1>
            staged;
        private readonly string canonicalText;

        public GeneratedEquipmentAugmentSignatureSnapshotV1(
            IEnumerable<GeneratedEquipmentAugmentSignatureV1> committed,
            IEnumerable<GeneratedEquipmentAugmentSignatureV1> staged,
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
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public int SchemaVersion { get; }

        public IReadOnlyList<GeneratedEquipmentAugmentSignatureV1> Committed
        {
            get { return committed; }
        }

        public IReadOnlyList<GeneratedEquipmentAugmentSignatureV1> Staged
        {
            get { return staged; }
        }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private static ReadOnlyCollection<GeneratedEquipmentAugmentSignatureV1>
            Freeze(
                IEnumerable<GeneratedEquipmentAugmentSignatureV1> source,
                string parameterName)
        {
            var values = new List<GeneratedEquipmentAugmentSignatureV1>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (GeneratedEquipmentAugmentSignatureV1 value in
                source ?? Array.Empty<GeneratedEquipmentAugmentSignatureV1>())
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
            return new ReadOnlyCollection<GeneratedEquipmentAugmentSignatureV1>(
                values);
        }
    }
}

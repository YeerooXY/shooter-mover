using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    public enum GeneratedEquipmentAugmentSignatureRecordStatusV1
    {
        Recorded = 1,
        ExactReplay = 2,
        ConflictingDuplicate = 3,
    }

    public sealed class GeneratedEquipmentAugmentSignatureRecordResultV1
    {
        public GeneratedEquipmentAugmentSignatureRecordResultV1(
            GeneratedEquipmentAugmentSignatureRecordStatusV1 status,
            GeneratedEquipmentAugmentSignatureV1 signature,
            string diagnostic)
        {
            if (!Enum.IsDefined(
                    typeof(GeneratedEquipmentAugmentSignatureRecordStatusV1),
                    status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Signature = signature
                ?? throw new ArgumentNullException(nameof(signature));
            Diagnostic = diagnostic ?? string.Empty;
        }

        public GeneratedEquipmentAugmentSignatureRecordStatusV1 Status { get; }
        public GeneratedEquipmentAugmentSignatureV1 Signature { get; }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status
                    != GeneratedEquipmentAugmentSignatureRecordStatusV1
                        .ConflictingDuplicate;
            }
        }
    }

    /// <summary>
    /// Exact-instance authority for generated augment capacity/shared level. It is
    /// intentionally separate from installed augment ownership and can be snapshotted
    /// and restored by the account persistence layer without mutating equipment facts.
    /// </summary>
    public sealed class GeneratedEquipmentAugmentSignatureAuthorityV1
    {
        private readonly object gate = new object();
        private readonly Dictionary<StableId, GeneratedEquipmentAugmentSignatureV1>
            byEquipmentInstance =
                new Dictionary<StableId, GeneratedEquipmentAugmentSignatureV1>();

        public int Count
        {
            get
            {
                lock (gate)
                {
                    return byEquipmentInstance.Count;
                }
            }
        }

        public GeneratedEquipmentAugmentSignatureRecordResultV1 Record(
            GeneratedEquipmentAugmentSignatureV1 signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }
            lock (gate)
            {
                GeneratedEquipmentAugmentSignatureV1 existing;
                if (byEquipmentInstance.TryGetValue(
                        signature.EquipmentInstanceStableId,
                        out existing))
                {
                    if (existing.Equals(signature))
                    {
                        return new GeneratedEquipmentAugmentSignatureRecordResultV1(
                            GeneratedEquipmentAugmentSignatureRecordStatusV1.ExactReplay,
                            existing,
                            string.Empty);
                    }
                    return new GeneratedEquipmentAugmentSignatureRecordResultV1(
                        GeneratedEquipmentAugmentSignatureRecordStatusV1
                            .ConflictingDuplicate,
                        existing,
                        "generated-equipment-augment-signature-conflict");
                }

                byEquipmentInstance.Add(
                    signature.EquipmentInstanceStableId,
                    signature);
                return new GeneratedEquipmentAugmentSignatureRecordResultV1(
                    GeneratedEquipmentAugmentSignatureRecordStatusV1.Recorded,
                    signature,
                    string.Empty);
            }
        }

        public bool TryGet(
            StableId equipmentInstanceStableId,
            out GeneratedEquipmentAugmentSignatureV1 signature)
        {
            if (equipmentInstanceStableId == null)
            {
                signature = null;
                return false;
            }
            lock (gate)
            {
                return byEquipmentInstance.TryGetValue(
                    equipmentInstanceStableId,
                    out signature);
            }
        }

        public IReadOnlyList<GeneratedEquipmentAugmentSignatureV1> ExportSnapshot()
        {
            lock (gate)
            {
                var values = new List<GeneratedEquipmentAugmentSignatureV1>(
                    byEquipmentInstance.Values);
                values.Sort();
                return new ReadOnlyCollection<GeneratedEquipmentAugmentSignatureV1>(
                    values);
            }
        }

        public void RestoreSnapshot(
            IEnumerable<GeneratedEquipmentAugmentSignatureV1> signatures)
        {
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }
            lock (gate)
            {
                var restored = new Dictionary<
                    StableId,
                    GeneratedEquipmentAugmentSignatureV1>();
                foreach (GeneratedEquipmentAugmentSignatureV1 signature in signatures)
                {
                    if (signature == null
                        || restored.ContainsKey(
                            signature.EquipmentInstanceStableId))
                    {
                        throw new ArgumentException(
                            "Restored generated augment signatures must be non-null and unique.",
                            nameof(signatures));
                    }
                    restored.Add(signature.EquipmentInstanceStableId, signature);
                }
                byEquipmentInstance.Clear();
                foreach (KeyValuePair<StableId, GeneratedEquipmentAugmentSignatureV1>
                    pair in restored)
                {
                    byEquipmentInstance.Add(pair.Key, pair.Value);
                }
            }
        }
    }
}

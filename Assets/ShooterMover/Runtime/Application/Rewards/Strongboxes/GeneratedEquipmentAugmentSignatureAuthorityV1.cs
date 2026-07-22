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
            IReadOnlyList<GeneratedEquipmentAugmentSignatureRecordResultV1>
                results;
            string diagnostic;
            if (!TryRecordBatch(
                    new[] { signature },
                    out results,
                    out diagnostic))
            {
                GeneratedEquipmentAugmentSignatureV1 existing;
                lock (gate)
                {
                    byEquipmentInstance.TryGetValue(
                        signature.EquipmentInstanceStableId,
                        out existing);
                }
                return new GeneratedEquipmentAugmentSignatureRecordResultV1(
                    GeneratedEquipmentAugmentSignatureRecordStatusV1
                        .ConflictingDuplicate,
                    existing ?? signature,
                    diagnostic);
            }
            return results[0];
        }

        /// <summary>
        /// Preflights the complete equipment batch before mutating state. A conflict in
        /// one slot therefore cannot leave earlier slot signatures partially recorded.
        /// </summary>
        public bool TryRecordBatch(
            IEnumerable<GeneratedEquipmentAugmentSignatureV1> signatures,
            out IReadOnlyList<GeneratedEquipmentAugmentSignatureRecordResultV1>
                results,
            out string diagnostic)
        {
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }
            var incoming = new List<GeneratedEquipmentAugmentSignatureV1>();
            var unique = new Dictionary<StableId, GeneratedEquipmentAugmentSignatureV1>();
            foreach (GeneratedEquipmentAugmentSignatureV1 signature in signatures)
            {
                if (signature == null)
                {
                    throw new ArgumentException(
                        "Generated augment signatures must not contain null entries.",
                        nameof(signatures));
                }
                GeneratedEquipmentAugmentSignatureV1 duplicate;
                if (unique.TryGetValue(
                        signature.EquipmentInstanceStableId,
                        out duplicate))
                {
                    if (!duplicate.Equals(signature))
                    {
                        results = Array.Empty<
                            GeneratedEquipmentAugmentSignatureRecordResultV1>();
                        diagnostic =
                            "generated-equipment-augment-signature-batch-conflict";
                        return false;
                    }
                    continue;
                }
                unique.Add(signature.EquipmentInstanceStableId, signature);
                incoming.Add(signature);
            }
            incoming.Sort();

            lock (gate)
            {
                for (int index = 0; index < incoming.Count; index++)
                {
                    GeneratedEquipmentAugmentSignatureV1 signature =
                        incoming[index];
                    GeneratedEquipmentAugmentSignatureV1 existing;
                    if (byEquipmentInstance.TryGetValue(
                            signature.EquipmentInstanceStableId,
                            out existing)
                        && !existing.Equals(signature))
                    {
                        results = Array.Empty<
                            GeneratedEquipmentAugmentSignatureRecordResultV1>();
                        diagnostic =
                            "generated-equipment-augment-signature-conflict";
                        return false;
                    }
                }

                var accepted = new List<
                    GeneratedEquipmentAugmentSignatureRecordResultV1>(
                        incoming.Count);
                for (int index = 0; index < incoming.Count; index++)
                {
                    GeneratedEquipmentAugmentSignatureV1 signature =
                        incoming[index];
                    GeneratedEquipmentAugmentSignatureV1 existing;
                    if (byEquipmentInstance.TryGetValue(
                            signature.EquipmentInstanceStableId,
                            out existing))
                    {
                        accepted.Add(
                            new GeneratedEquipmentAugmentSignatureRecordResultV1(
                                GeneratedEquipmentAugmentSignatureRecordStatusV1
                                    .ExactReplay,
                                existing,
                                string.Empty));
                    }
                    else
                    {
                        byEquipmentInstance.Add(
                            signature.EquipmentInstanceStableId,
                            signature);
                        accepted.Add(
                            new GeneratedEquipmentAugmentSignatureRecordResultV1(
                                GeneratedEquipmentAugmentSignatureRecordStatusV1
                                    .Recorded,
                                signature,
                                string.Empty));
                    }
                }
                results = new ReadOnlyCollection<
                    GeneratedEquipmentAugmentSignatureRecordResultV1>(accepted);
                diagnostic = string.Empty;
                return true;
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

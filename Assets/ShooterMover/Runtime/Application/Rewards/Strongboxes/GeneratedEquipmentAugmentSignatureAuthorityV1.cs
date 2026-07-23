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
    /// Character-owned exact-instance authority for generated augment capacity/shared
    /// level. Payload resolution stages immutable opening intent. The RAP equipment child
    /// moves that intent into committed state only after the exact equipment grant is
    /// confirmed applied. Both sets are durably snapshotted so interrupted claims roll
    /// forward without rerolling, while staged entries never masquerade as owned metadata.
    /// </summary>
    public sealed class GeneratedEquipmentAugmentSignatureAuthorityV1
    {
        private readonly object gate = new object();
        private readonly Dictionary<StableId, GeneratedEquipmentAugmentSignatureV1>
            committedByEquipmentInstance =
                new Dictionary<StableId, GeneratedEquipmentAugmentSignatureV1>();
        private readonly Dictionary<StableId, GeneratedEquipmentAugmentSignatureV1>
            stagedByEquipmentInstance =
                new Dictionary<StableId, GeneratedEquipmentAugmentSignatureV1>();

        public int Count
        {
            get
            {
                lock (gate)
                {
                    return committedByEquipmentInstance.Count;
                }
            }
        }

        public int StagedCount
        {
            get
            {
                lock (gate)
                {
                    return stagedByEquipmentInstance.Count;
                }
            }
        }

        /// <summary>
        /// Compatibility entry point for callers that already own an applied equipment
        /// fact. New BOX payload resolution must call TryStageBatch instead.
        /// </summary>
        public GeneratedEquipmentAugmentSignatureRecordResultV1 Record(
            GeneratedEquipmentAugmentSignatureV1 signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }
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
                    if (!committedByEquipmentInstance.TryGetValue(
                            signature.EquipmentInstanceStableId,
                            out existing))
                    {
                        stagedByEquipmentInstance.TryGetValue(
                            signature.EquipmentInstanceStableId,
                            out existing);
                    }
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
        /// Atomically stages a complete generated equipment batch. Staging is durable
        /// opening intent, not authoritative ownership. Exact replay is accepted; any
        /// conflicting existing committed or staged value rejects without mutation.
        /// </summary>
        public bool TryStageBatch(
            IEnumerable<GeneratedEquipmentAugmentSignatureV1> signatures,
            out string diagnostic)
        {
            List<GeneratedEquipmentAugmentSignatureV1> incoming =
                FreezeIncoming(signatures);
            lock (gate)
            {
                for (int index = 0; index < incoming.Count; index++)
                {
                    GeneratedEquipmentAugmentSignatureV1 signature =
                        incoming[index];
                    GeneratedEquipmentAugmentSignatureV1 existing;
                    if (committedByEquipmentInstance.TryGetValue(
                            signature.EquipmentInstanceStableId,
                            out existing)
                        && !existing.Equals(signature))
                    {
                        diagnostic =
                            "generated-equipment-augment-signature-committed-conflict";
                        return false;
                    }
                    if (stagedByEquipmentInstance.TryGetValue(
                            signature.EquipmentInstanceStableId,
                            out existing)
                        && !existing.Equals(signature))
                    {
                        diagnostic =
                            "generated-equipment-augment-signature-staged-conflict";
                        return false;
                    }
                }

                for (int index = 0; index < incoming.Count; index++)
                {
                    GeneratedEquipmentAugmentSignatureV1 signature =
                        incoming[index];
                    if (!committedByEquipmentInstance.ContainsKey(
                            signature.EquipmentInstanceStableId)
                        && !stagedByEquipmentInstance.ContainsKey(
                            signature.EquipmentInstanceStableId))
                    {
                        stagedByEquipmentInstance.Add(
                            signature.EquipmentInstanceStableId,
                            signature);
                    }
                }
                diagnostic = string.Empty;
                return true;
            }
        }

        /// <summary>
        /// Moves one exact staged value into committed character state. This method is
        /// called only by the RAP equipment child after holdings confirms application.
        /// </summary>
        public bool TryCommitStaged(
            StableId equipmentInstanceStableId,
            string expectedSignatureFingerprint,
            out GeneratedEquipmentAugmentSignatureV1 signature,
            out string diagnostic)
        {
            signature = null;
            if (equipmentInstanceStableId == null
                || string.IsNullOrWhiteSpace(expectedSignatureFingerprint))
            {
                diagnostic =
                    "generated-equipment-augment-signature-commit-identity-missing";
                return false;
            }

            lock (gate)
            {
                GeneratedEquipmentAugmentSignatureV1 existing;
                if (committedByEquipmentInstance.TryGetValue(
                        equipmentInstanceStableId,
                        out existing))
                {
                    if (!string.Equals(
                            existing.Fingerprint,
                            expectedSignatureFingerprint,
                            StringComparison.Ordinal))
                    {
                        diagnostic =
                            "generated-equipment-augment-signature-commit-conflict";
                        return false;
                    }
                    GeneratedEquipmentAugmentSignatureV1 staged;
                    if (stagedByEquipmentInstance.TryGetValue(
                            equipmentInstanceStableId,
                            out staged))
                    {
                        if (!staged.Equals(existing))
                        {
                            diagnostic =
                                "generated-equipment-augment-signature-stage-commit-conflict";
                            return false;
                        }
                        stagedByEquipmentInstance.Remove(
                            equipmentInstanceStableId);
                    }
                    signature = existing;
                    diagnostic = string.Empty;
                    return true;
                }

                GeneratedEquipmentAugmentSignatureV1 pending;
                if (!stagedByEquipmentInstance.TryGetValue(
                        equipmentInstanceStableId,
                        out pending))
                {
                    diagnostic =
                        "generated-equipment-augment-signature-stage-missing";
                    return false;
                }
                if (!string.Equals(
                        pending.Fingerprint,
                        expectedSignatureFingerprint,
                        StringComparison.Ordinal))
                {
                    diagnostic =
                        "generated-equipment-augment-signature-stage-fingerprint-conflict";
                    return false;
                }

                stagedByEquipmentInstance.Remove(equipmentInstanceStableId);
                committedByEquipmentInstance.Add(
                    equipmentInstanceStableId,
                    pending);
                signature = pending;
                diagnostic = string.Empty;
                return true;
            }
        }

        /// <summary>
        /// Commits an already-applied batch directly. Retained for compatibility and
        /// restore/migration boundaries; normal hybrid opening uses TryCommitStaged.
        /// </summary>
        public bool TryRecordBatch(
            IEnumerable<GeneratedEquipmentAugmentSignatureV1> signatures,
            out IReadOnlyList<GeneratedEquipmentAugmentSignatureRecordResultV1>
                results,
            out string diagnostic)
        {
            List<GeneratedEquipmentAugmentSignatureV1> incoming =
                FreezeIncoming(signatures);
            lock (gate)
            {
                for (int index = 0; index < incoming.Count; index++)
                {
                    GeneratedEquipmentAugmentSignatureV1 signature =
                        incoming[index];
                    GeneratedEquipmentAugmentSignatureV1 existing;
                    if (committedByEquipmentInstance.TryGetValue(
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
                    if (stagedByEquipmentInstance.TryGetValue(
                            signature.EquipmentInstanceStableId,
                            out existing)
                        && !existing.Equals(signature))
                    {
                        results = Array.Empty<
                            GeneratedEquipmentAugmentSignatureRecordResultV1>();
                        diagnostic =
                            "generated-equipment-augment-signature-staged-conflict";
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
                    if (committedByEquipmentInstance.TryGetValue(
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
                        stagedByEquipmentInstance.Remove(
                            signature.EquipmentInstanceStableId);
                        committedByEquipmentInstance.Add(
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
                return committedByEquipmentInstance.TryGetValue(
                    equipmentInstanceStableId,
                    out signature);
            }
        }

        public bool TryGetStagedOrCommitted(
            StableId equipmentInstanceStableId,
            out GeneratedEquipmentAugmentSignatureV1 signature,
            out bool isCommitted)
        {
            signature = null;
            isCommitted = false;
            if (equipmentInstanceStableId == null)
            {
                return false;
            }
            lock (gate)
            {
                if (committedByEquipmentInstance.TryGetValue(
                        equipmentInstanceStableId,
                        out signature))
                {
                    isCommitted = true;
                    return true;
                }
                return stagedByEquipmentInstance.TryGetValue(
                    equipmentInstanceStableId,
                    out signature);
            }
        }

        public IReadOnlyList<GeneratedEquipmentAugmentSignatureV1> ExportSnapshot()
        {
            lock (gate)
            {
                return FreezeValues(committedByEquipmentInstance.Values);
            }
        }

        public GeneratedEquipmentAugmentSignatureSnapshotV1 ExportDurableSnapshot()
        {
            lock (gate)
            {
                return new GeneratedEquipmentAugmentSignatureSnapshotV1(
                    committedByEquipmentInstance.Values,
                    stagedByEquipmentInstance.Values);
            }
        }

        public void RestoreSnapshot(
            IEnumerable<GeneratedEquipmentAugmentSignatureV1> signatures)
        {
            RestoreDurableSnapshot(
                new GeneratedEquipmentAugmentSignatureSnapshotV1(
                    signatures,
                    Array.Empty<GeneratedEquipmentAugmentSignatureV1>()));
        }

        public void RestoreDurableSnapshot(
            GeneratedEquipmentAugmentSignatureSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            lock (gate)
            {
                committedByEquipmentInstance.Clear();
                stagedByEquipmentInstance.Clear();
                for (int index = 0; index < snapshot.Committed.Count; index++)
                {
                    GeneratedEquipmentAugmentSignatureV1 signature =
                        snapshot.Committed[index];
                    committedByEquipmentInstance.Add(
                        signature.EquipmentInstanceStableId,
                        signature);
                }
                for (int index = 0; index < snapshot.Staged.Count; index++)
                {
                    GeneratedEquipmentAugmentSignatureV1 signature =
                        snapshot.Staged[index];
                    if (committedByEquipmentInstance.ContainsKey(
                            signature.EquipmentInstanceStableId))
                    {
                        throw new ArgumentException(
                            "A restored generated augment signature cannot be both staged and committed.",
                            nameof(snapshot));
                    }
                    stagedByEquipmentInstance.Add(
                        signature.EquipmentInstanceStableId,
                        signature);
                }
            }
        }

        private static List<GeneratedEquipmentAugmentSignatureV1> FreezeIncoming(
            IEnumerable<GeneratedEquipmentAugmentSignatureV1> signatures)
        {
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }
            var incoming = new List<GeneratedEquipmentAugmentSignatureV1>();
            var unique = new Dictionary<
                StableId,
                GeneratedEquipmentAugmentSignatureV1>();
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
                        throw new ArgumentException(
                            "A generated augment signature batch contains conflicting duplicate equipment identities.",
                            nameof(signatures));
                    }
                    continue;
                }
                unique.Add(signature.EquipmentInstanceStableId, signature);
                incoming.Add(signature);
            }
            incoming.Sort();
            return incoming;
        }

        private static ReadOnlyCollection<GeneratedEquipmentAugmentSignatureV1>
            FreezeValues(
                IEnumerable<GeneratedEquipmentAugmentSignatureV1> signatures)
        {
            var values = new List<GeneratedEquipmentAugmentSignatureV1>(
                signatures);
            values.Sort();
            return new ReadOnlyCollection<GeneratedEquipmentAugmentSignatureV1>(
                values);
        }
    }
}

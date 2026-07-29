using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    public enum GeneratedEquipmentAugmentSignatureRecordStatus
    {
        Recorded = 1,
        ExactReplay = 2,
        ConflictingDuplicate = 3,
    }

    public sealed class GeneratedEquipmentAugmentSignatureRecordResult
    {
        public GeneratedEquipmentAugmentSignatureRecordResult(
            GeneratedEquipmentAugmentSignatureRecordStatus status,
            GeneratedEquipmentAugmentSignature signature,
            string diagnostic)
        {
            if (!Enum.IsDefined(
                    typeof(GeneratedEquipmentAugmentSignatureRecordStatus),
                    status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Signature = signature
                ?? throw new ArgumentNullException(nameof(signature));
            Diagnostic = diagnostic ?? string.Empty;
        }

        public GeneratedEquipmentAugmentSignatureRecordStatus Status { get; }
        public GeneratedEquipmentAugmentSignature Signature { get; }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status
                    != GeneratedEquipmentAugmentSignatureRecordStatus
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
    public sealed class GeneratedEquipmentAugmentSignatureState
    {
        private readonly object gate = new object();
        private readonly Dictionary<StableId, GeneratedEquipmentAugmentSignature>
            committedByEquipmentInstance =
                new Dictionary<StableId, GeneratedEquipmentAugmentSignature>();
        private readonly Dictionary<StableId, GeneratedEquipmentAugmentSignature>
            stagedByEquipmentInstance =
                new Dictionary<StableId, GeneratedEquipmentAugmentSignature>();

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
        public GeneratedEquipmentAugmentSignatureRecordResult Record(
            GeneratedEquipmentAugmentSignature signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }
            IReadOnlyList<GeneratedEquipmentAugmentSignatureRecordResult>
                results;
            string diagnostic;
            if (!TryRecordBatch(
                    new[] { signature },
                    out results,
                    out diagnostic))
            {
                GeneratedEquipmentAugmentSignature existing;
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
                return new GeneratedEquipmentAugmentSignatureRecordResult(
                    GeneratedEquipmentAugmentSignatureRecordStatus
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
            IEnumerable<GeneratedEquipmentAugmentSignature> signatures,
            out string diagnostic)
        {
            List<GeneratedEquipmentAugmentSignature> incoming =
                FreezeIncoming(signatures);
            lock (gate)
            {
                for (int index = 0; index < incoming.Count; index++)
                {
                    GeneratedEquipmentAugmentSignature signature =
                        incoming[index];
                    GeneratedEquipmentAugmentSignature existing;
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
                    GeneratedEquipmentAugmentSignature signature =
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
            out GeneratedEquipmentAugmentSignature signature,
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
                GeneratedEquipmentAugmentSignature existing;
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
                    GeneratedEquipmentAugmentSignature staged;
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

                GeneratedEquipmentAugmentSignature pending;
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
            IEnumerable<GeneratedEquipmentAugmentSignature> signatures,
            out IReadOnlyList<GeneratedEquipmentAugmentSignatureRecordResult>
                results,
            out string diagnostic)
        {
            List<GeneratedEquipmentAugmentSignature> incoming =
                FreezeIncoming(signatures);
            lock (gate)
            {
                for (int index = 0; index < incoming.Count; index++)
                {
                    GeneratedEquipmentAugmentSignature signature =
                        incoming[index];
                    GeneratedEquipmentAugmentSignature existing;
                    if (committedByEquipmentInstance.TryGetValue(
                            signature.EquipmentInstanceStableId,
                            out existing)
                        && !existing.Equals(signature))
                    {
                        results = Array.Empty<
                            GeneratedEquipmentAugmentSignatureRecordResult>();
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
                            GeneratedEquipmentAugmentSignatureRecordResult>();
                        diagnostic =
                            "generated-equipment-augment-signature-staged-conflict";
                        return false;
                    }
                }

                var accepted = new List<
                    GeneratedEquipmentAugmentSignatureRecordResult>(
                        incoming.Count);
                for (int index = 0; index < incoming.Count; index++)
                {
                    GeneratedEquipmentAugmentSignature signature =
                        incoming[index];
                    GeneratedEquipmentAugmentSignature existing;
                    if (committedByEquipmentInstance.TryGetValue(
                            signature.EquipmentInstanceStableId,
                            out existing))
                    {
                        accepted.Add(
                            new GeneratedEquipmentAugmentSignatureRecordResult(
                                GeneratedEquipmentAugmentSignatureRecordStatus
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
                            new GeneratedEquipmentAugmentSignatureRecordResult(
                                GeneratedEquipmentAugmentSignatureRecordStatus
                                    .Recorded,
                                signature,
                                string.Empty));
                    }
                }
                results = new ReadOnlyCollection<
                    GeneratedEquipmentAugmentSignatureRecordResult>(accepted);
                diagnostic = string.Empty;
                return true;
            }
        }

        public bool TryGet(
            StableId equipmentInstanceStableId,
            out GeneratedEquipmentAugmentSignature signature)
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
            out GeneratedEquipmentAugmentSignature signature,
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

        public IReadOnlyList<GeneratedEquipmentAugmentSignature> ExportSnapshot()
        {
            lock (gate)
            {
                return FreezeValues(committedByEquipmentInstance.Values);
            }
        }

        public GeneratedEquipmentAugmentSignatureSnapshot ExportDurableSnapshot()
        {
            lock (gate)
            {
                return new GeneratedEquipmentAugmentSignatureSnapshot(
                    committedByEquipmentInstance.Values,
                    stagedByEquipmentInstance.Values);
            }
        }

        public void RestoreSnapshot(
            IEnumerable<GeneratedEquipmentAugmentSignature> signatures)
        {
            RestoreDurableSnapshot(
                new GeneratedEquipmentAugmentSignatureSnapshot(
                    signatures,
                    Array.Empty<GeneratedEquipmentAugmentSignature>()));
        }

        public void RestoreDurableSnapshot(
            GeneratedEquipmentAugmentSignatureSnapshot snapshot)
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
                    GeneratedEquipmentAugmentSignature signature =
                        snapshot.Committed[index];
                    committedByEquipmentInstance.Add(
                        signature.EquipmentInstanceStableId,
                        signature);
                }
                for (int index = 0; index < snapshot.Staged.Count; index++)
                {
                    GeneratedEquipmentAugmentSignature signature =
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

        private static List<GeneratedEquipmentAugmentSignature> FreezeIncoming(
            IEnumerable<GeneratedEquipmentAugmentSignature> signatures)
        {
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }
            var incoming = new List<GeneratedEquipmentAugmentSignature>();
            var unique = new Dictionary<
                StableId,
                GeneratedEquipmentAugmentSignature>();
            foreach (GeneratedEquipmentAugmentSignature signature in signatures)
            {
                if (signature == null)
                {
                    throw new ArgumentException(
                        "Generated augment signatures must not contain null entries.",
                        nameof(signatures));
                }
                GeneratedEquipmentAugmentSignature duplicate;
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

        private static ReadOnlyCollection<GeneratedEquipmentAugmentSignature>
            FreezeValues(
                IEnumerable<GeneratedEquipmentAugmentSignature> signatures)
        {
            var values = new List<GeneratedEquipmentAugmentSignature>(
                signatures);
            values.Sort();
            return new ReadOnlyCollection<GeneratedEquipmentAugmentSignature>(
                values);
        }
    }
}

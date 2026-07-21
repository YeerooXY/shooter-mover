using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.TerminalDropBinding
{
    public enum PendingTerminalDropAdmissionStatusV1
    {
        Accepted = 1,
        ExactReplay = 2,
        ConflictingDuplicate = 3,
        Rejected = 4,
    }

    public sealed class PendingTerminalDropAdmissionResultV1
    {
        private PendingTerminalDropAdmissionResultV1(
            PendingTerminalDropAdmissionStatusV1 status,
            StableId operationStableId,
            string batchFingerprint,
            GeneratedTerminalDropResultV1 pendingResult,
            string diagnostic)
        {
            Status = status;
            OperationStableId = operationStableId;
            BatchFingerprint = batchFingerprint ?? string.Empty;
            PendingResult = pendingResult;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PendingTerminalDropAdmissionStatusV1 Status { get; }
        public StableId OperationStableId { get; }
        public string BatchFingerprint { get; }
        public GeneratedTerminalDropResultV1 PendingResult { get; }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == PendingTerminalDropAdmissionStatusV1.Accepted
                    || Status == PendingTerminalDropAdmissionStatusV1.ExactReplay;
            }
        }

        internal static PendingTerminalDropAdmissionResultV1 Accepted(
            GeneratedTerminalDropResultV1 result)
        {
            return new PendingTerminalDropAdmissionResultV1(
                PendingTerminalDropAdmissionStatusV1.Accepted,
                result.OperationRequest.SourceOperationStableId,
                result.Fingerprint,
                result,
                string.Empty);
        }

        internal static PendingTerminalDropAdmissionResultV1 ExactReplay(
            GeneratedTerminalDropResultV1 existing)
        {
            return new PendingTerminalDropAdmissionResultV1(
                PendingTerminalDropAdmissionStatusV1.ExactReplay,
                existing.OperationRequest.SourceOperationStableId,
                existing.Fingerprint,
                existing,
                "terminal-drop-pending-exact-replay");
        }

        internal static PendingTerminalDropAdmissionResultV1 Conflict(
            StableId operationStableId,
            string incomingFingerprint,
            GeneratedTerminalDropResultV1 existing)
        {
            return new PendingTerminalDropAdmissionResultV1(
                PendingTerminalDropAdmissionStatusV1.ConflictingDuplicate,
                operationStableId,
                incomingFingerprint,
                existing,
                "terminal-drop-pending-operation-conflict");
        }

        public static PendingTerminalDropAdmissionResultV1 Rejected(
            string diagnostic)
        {
            return new PendingTerminalDropAdmissionResultV1(
                PendingTerminalDropAdmissionStatusV1.Rejected,
                null,
                string.Empty,
                null,
                diagnostic);
        }
    }

    /// <summary>
    /// Idempotent admission boundary for pending, uncollected terminal-drop batches.
    /// Implementations must key admission by canonical DROP operation identity and reject
    /// conflicting reuse without adding a second pending entry.
    /// </summary>
    public interface IGeneratedTerminalDropPendingAdmissionV1
    {
        PendingTerminalDropAdmissionResultV1 Admit(
            GeneratedTerminalDropResultV1 result);
    }

    public sealed class PendingTerminalDropAdmissionAuthorityV1 :
        IGeneratedTerminalDropPendingAdmissionV1
    {
        private sealed class PendingRecord
        {
            public PendingRecord(string fingerprint, GeneratedTerminalDropResultV1 result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public GeneratedTerminalDropResultV1 Result { get; }
        }

        private readonly object gate = new object();
        private readonly Dictionary<StableId, PendingRecord> byOperation =
            new Dictionary<StableId, PendingRecord>();

        public int PendingBatchCount
        {
            get
            {
                lock (gate)
                {
                    return byOperation.Count;
                }
            }
        }

        public PendingTerminalDropAdmissionResultV1 Admit(
            GeneratedTerminalDropResultV1 result)
        {
            if (result == null)
            {
                return PendingTerminalDropAdmissionResultV1.Rejected(
                    "terminal-drop-pending-result-null");
            }
            if (!result.IsAccepted)
            {
                return PendingTerminalDropAdmissionResultV1.Rejected(
                    "terminal-drop-pending-result-not-generated:" + result.Status);
            }
            if (result.OperationRequest == null
                || result.OperationRequest.SourceOperationStableId == null
                || string.IsNullOrWhiteSpace(result.Fingerprint))
            {
                return PendingTerminalDropAdmissionResultV1.Rejected(
                    "terminal-drop-pending-result-identity-incomplete");
            }

            StableId operationId = result.OperationRequest.SourceOperationStableId;
            lock (gate)
            {
                PendingRecord existing;
                if (byOperation.TryGetValue(operationId, out existing))
                {
                    if (string.Equals(
                        existing.Fingerprint,
                        result.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        return PendingTerminalDropAdmissionResultV1.ExactReplay(
                            existing.Result);
                    }
                    return PendingTerminalDropAdmissionResultV1.Conflict(
                        operationId,
                        result.Fingerprint,
                        existing.Result);
                }

                byOperation.Add(
                    operationId,
                    new PendingRecord(result.Fingerprint, result));
                return PendingTerminalDropAdmissionResultV1.Accepted(result);
            }
        }

        public bool TryGetPending(
            StableId operationStableId,
            out GeneratedTerminalDropResultV1 result)
        {
            result = null;
            if (operationStableId == null) return false;
            lock (gate)
            {
                PendingRecord record;
                if (!byOperation.TryGetValue(operationStableId, out record))
                    return false;
                result = record.Result;
                return result != null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.TerminalDropBinding
{
    public enum PendingTerminalDropAdmissionStatus
    {
        Accepted = 1,
        ExactReplay = 2,
        ConflictingDuplicate = 3,
        Rejected = 4,
    }

    public sealed class PendingTerminalDropAdmissionResult
    {
        private PendingTerminalDropAdmissionResult(
            PendingTerminalDropAdmissionStatus status,
            StableId operationStableId,
            string batchFingerprint,
            GeneratedTerminalDropResult pendingResult,
            string diagnostic)
        {
            Status = status;
            OperationStableId = operationStableId;
            BatchFingerprint = batchFingerprint ?? string.Empty;
            PendingResult = pendingResult;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PendingTerminalDropAdmissionStatus Status { get; }
        public StableId OperationStableId { get; }
        public string BatchFingerprint { get; }
        public GeneratedTerminalDropResult PendingResult { get; }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == PendingTerminalDropAdmissionStatus.Accepted
                    || Status == PendingTerminalDropAdmissionStatus.ExactReplay;
            }
        }

        internal static PendingTerminalDropAdmissionResult Accepted(
            GeneratedTerminalDropResult result)
        {
            return new PendingTerminalDropAdmissionResult(
                PendingTerminalDropAdmissionStatus.Accepted,
                result.OperationRequest.SourceOperationStableId,
                result.Fingerprint,
                result,
                string.Empty);
        }

        internal static PendingTerminalDropAdmissionResult ExactReplay(
            GeneratedTerminalDropResult existing)
        {
            return new PendingTerminalDropAdmissionResult(
                PendingTerminalDropAdmissionStatus.ExactReplay,
                existing.OperationRequest.SourceOperationStableId,
                existing.Fingerprint,
                existing,
                "terminal-drop-pending-exact-replay");
        }

        internal static PendingTerminalDropAdmissionResult Conflict(
            StableId operationStableId,
            string incomingFingerprint,
            GeneratedTerminalDropResult existing)
        {
            return new PendingTerminalDropAdmissionResult(
                PendingTerminalDropAdmissionStatus.ConflictingDuplicate,
                operationStableId,
                incomingFingerprint,
                existing,
                "terminal-drop-pending-operation-conflict");
        }

        public static PendingTerminalDropAdmissionResult Rejected(
            string diagnostic)
        {
            return new PendingTerminalDropAdmissionResult(
                PendingTerminalDropAdmissionStatus.Rejected,
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
    public interface IGeneratedTerminalDropPendingAdmission
    {
        PendingTerminalDropAdmissionResult Admit(
            GeneratedTerminalDropResult result);
    }

    public sealed class PendingTerminalDropAdmissionState :
        IGeneratedTerminalDropPendingAdmission
    {
        private sealed class PendingRecord
        {
            public PendingRecord(string fingerprint, GeneratedTerminalDropResult result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public GeneratedTerminalDropResult Result { get; }
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

        public PendingTerminalDropAdmissionResult Admit(
            GeneratedTerminalDropResult result)
        {
            if (result == null)
            {
                return PendingTerminalDropAdmissionResult.Rejected(
                    "terminal-drop-pending-result-null");
            }
            if (!result.IsAccepted)
            {
                return PendingTerminalDropAdmissionResult.Rejected(
                    "terminal-drop-pending-result-not-generated:" + result.Status);
            }
            if (result.OperationRequest == null
                || result.OperationRequest.SourceOperationStableId == null
                || string.IsNullOrWhiteSpace(result.Fingerprint))
            {
                return PendingTerminalDropAdmissionResult.Rejected(
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
                        return PendingTerminalDropAdmissionResult.ExactReplay(
                            existing.Result);
                    }
                    return PendingTerminalDropAdmissionResult.Conflict(
                        operationId,
                        result.Fingerprint,
                        existing.Result);
                }

                byOperation.Add(
                    operationId,
                    new PendingRecord(result.Fingerprint, result));
                return PendingTerminalDropAdmissionResult.Accepted(result);
            }
        }

        /// <summary>
        /// Compensates only a pending record created by the exact Accepted receipt supplied.
        /// ExactReplay receipts are deliberately ineligible because they refer to state owned
        /// by an earlier committed delivery. Repeating the same rollback is idempotent.
        /// </summary>
        public bool TryRollbackAccepted(
            PendingTerminalDropAdmissionResult admission,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (admission == null
                || admission.Status != PendingTerminalDropAdmissionStatus.Accepted
                || admission.OperationStableId == null
                || string.IsNullOrWhiteSpace(admission.BatchFingerprint)
                || admission.PendingResult == null)
            {
                diagnostic = "terminal-drop-pending-rollback-receipt-invalid";
                return false;
            }

            lock (gate)
            {
                PendingRecord existing;
                if (!byOperation.TryGetValue(
                        admission.OperationStableId,
                        out existing))
                {
                    diagnostic = "terminal-drop-pending-rollback-already-absent";
                    return true;
                }
                if (!string.Equals(
                        existing.Fingerprint,
                        admission.BatchFingerprint,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        existing.Result.Fingerprint,
                        admission.PendingResult.Fingerprint,
                        StringComparison.Ordinal))
                {
                    diagnostic = "terminal-drop-pending-rollback-conflict";
                    return false;
                }

                byOperation.Remove(admission.OperationStableId);
                return true;
            }
        }

        public bool TryGetPending(
            StableId operationStableId,
            out GeneratedTerminalDropResult result)
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

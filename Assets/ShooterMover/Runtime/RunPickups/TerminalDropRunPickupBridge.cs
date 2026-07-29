using System;
using System.Collections.Generic;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.RunPickups
{
    /// <summary>
    /// Copies the exact immutable pending terminal-drop batch into the pickup boundary.
    /// No profile lookup, DROP execution, GEN execution, reroll, fallback, or replacement
    /// identity is permitted here.
    /// </summary>
    public static class TerminalDropRunPickupBridge
    {
        public static bool TryCreateBatch(
            PendingTerminalDropAdmissionResult admission,
            out RunPickupGeneratedBatch batch,
            out string diagnostic)
        {
            batch = null;
            diagnostic = string.Empty;
            if (admission == null)
            {
                diagnostic = "run-pickup-pending-admission-null";
                return false;
            }
            if (!admission.IsAccepted || admission.PendingResult == null)
            {
                diagnostic = "run-pickup-pending-admission-not-accepted:" + admission.Status;
                return false;
            }

            GeneratedTerminalDropResult pending = admission.PendingResult;
            if (!pending.IsAccepted
                || pending.SourceFact == null
                || pending.OperationRequest == null
                || pending.OperationRequest.SourceOperationStableId == null
                || string.IsNullOrWhiteSpace(pending.Fingerprint))
            {
                diagnostic = "run-pickup-pending-result-context-incomplete";
                return false;
            }
            if (pending.SourceFact.AttributedParticipantStableId == null)
            {
                diagnostic = "run-pickup-pending-result-unattributed";
                return false;
            }
            if (!string.Equals(
                admission.BatchFingerprint,
                pending.Fingerprint,
                StringComparison.Ordinal))
            {
                diagnostic = "run-pickup-pending-admission-fingerprint-mismatch";
                return false;
            }

            var children = new List<RunPickupGeneratedReward>(
                pending.GeneratedRewards.Count);
            for (int index = 0; index < pending.GeneratedRewards.Count; index++)
            {
                GeneratedTerminalDropReward child = pending.GeneratedRewards[index];
                if (child == null)
                {
                    diagnostic = "run-pickup-pending-child-null";
                    return false;
                }
                children.Add(new RunPickupGeneratedReward(
                    child.RewardInstanceStableId,
                    child.Ordinal,
                    child.SourceGrantStableId,
                    child.Kind,
                    child.ContentStableId,
                    child.Quantity,
                    child.Fingerprint));
            }

            try
            {
                batch = new RunPickupGeneratedBatch(
                    pending.OperationRequest.SourceOperationStableId,
                    pending.SourceFact.TerminalEventStableId,
                    pending.SourceFact.TriggeringEventStableId,
                    pending.SourceFact.RunStableId,
                    pending.SourceFact.RunLifecycleGeneration,
                    pending.SourceFact.SourceEntityStableId,
                    pending.SourceFact.SourcePlacementStableId,
                    pending.SourceFact.SourceLifecycleGeneration,
                    pending.SourceFact.SourceDefinitionStableId,
                    pending.SourceFact.AttributedParticipantStableId,
                    pending.Fingerprint,
                    children);
                return true;
            }
            catch (Exception exception)
            {
                batch = null;
                diagnostic = "run-pickup-pending-batch-invalid:" + exception.Message;
                return false;
            }
        }
    }

    public sealed class PendingTerminalDropPickupConsumer :
        IPendingTerminalDropAdmissionConsumer
    {
        private readonly RunLocalPickupState authority;

        public PendingTerminalDropPickupConsumer(
            RunLocalPickupState authority)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
        }

        public RunPickupRealizationResult LastResult { get; private set; }

        public RunPickupRealizationResult Consume(
            PendingTerminalDropAdmissionResult admission)
        {
            RunPickupGeneratedBatch batch;
            string diagnostic;
            if (!TerminalDropRunPickupBridge.TryCreateBatch(
                admission,
                out batch,
                out diagnostic))
            {
                LastResult = new RunPickupRealizationResult(
                    RunPickupRealizationStatus.Rejected,
                    null,
                    Array.Empty<RunPickupSnapshot>(),
                    diagnostic);
                return LastResult;
            }

            LastResult = authority.Realize(batch);
            return LastResult;
        }

        void IPendingTerminalDropAdmissionConsumer.Consume(
            PendingTerminalDropAdmissionResult admission)
        {
            Consume(admission);
        }
    }
}

using System;
using System.Text;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.TerminalDropBinding;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    internal sealed class Stage1PersonalRewardBatchDeliveryResultV1
    {
        public Stage1PersonalRewardBatchDeliveryResultV1(
            bool succeeded,
            bool terminalConflict,
            PendingTerminalDropAdmissionResultV1 lastAdmission,
            string fingerprint,
            string diagnostic)
        {
            Succeeded = succeeded;
            TerminalConflict = terminalConflict;
            LastAdmission = lastAdmission;
            Fingerprint = fingerprint ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool TerminalConflict { get; }
        public PendingTerminalDropAdmissionResultV1 LastAdmission { get; }
        public string Fingerprint { get; }
        public string Diagnostic { get; }
    }

    /// <summary>
    /// Routes one generated personal batch. Results for the local participant enter the
    /// local pickup authority; results for other participants remain pending in the
    /// run-owned outbox for their participant-specific network/pickup transport.
    /// </summary>
    internal sealed class Stage1PersonalRewardBatchDeliveryV1
    {
        private readonly TerminalDropBindingCompositionV1 terminalDrops;
        private readonly PendingAdmissionPickupBridgeV1 admissionBridge;
        private readonly StableId localParticipantStableId;
        private readonly IPersonalRewardDeliveryOutboxV1 deliveryOutbox;

        public Stage1PersonalRewardBatchDeliveryV1(
            TerminalDropBindingCompositionV1 terminalDrops,
            PendingAdmissionPickupBridgeV1 admissionBridge,
            StableId localParticipantStableId,
            IPersonalRewardDeliveryOutboxV1 deliveryOutbox)
        {
            this.terminalDrops = terminalDrops
                ?? throw new ArgumentNullException(nameof(terminalDrops));
            this.admissionBridge = admissionBridge
                ?? throw new ArgumentNullException(nameof(admissionBridge));
            this.localParticipantStableId = localParticipantStableId
                ?? throw new ArgumentNullException(
                    nameof(localParticipantStableId));
            this.deliveryOutbox = deliveryOutbox
                ?? throw new ArgumentNullException(nameof(deliveryOutbox));
        }

        public Stage1PersonalRewardBatchDeliveryResultV1 Deliver(
            TerminalPersonalRewardBatchV1 batch,
            StableId runStableId,
            long lifecycleGeneration,
            StableId roomStableId,
            Vector2 position,
            string positionFingerprint)
        {
            if (batch == null || !batch.IsAccepted)
            {
                return Failure(
                    false,
                    null,
                    batch == null
                        ? "stage1-personal-reward-batch-null"
                        : batch.Diagnostic);
            }

            var fingerprint = new StringBuilder(
                "schema=stage1-personal-reward-delivery-v2");
            PendingTerminalDropAdmissionResultV1 lastAdmission = null;
            for (int index = 0; index < batch.Results.Count; index++)
            {
                GeneratedTerminalDropResultV1 generated = batch.Results[index];
                if (generated == null || !generated.IsAccepted)
                {
                    return Failure(
                        generated != null
                            && generated.Status
                                == TerminalDropBindingStatusV1
                                    .ConflictingDuplicate,
                        lastAdmission,
                        generated == null
                            ? "stage1-personal-reward-result-null"
                            : generated.Diagnostic);
                }

                StableId participantStableId =
                    generated.SourceFact.AttributedParticipantStableId;
                StableId operationStableId = generated.OperationRequest
                    .SourceOperationStableId;
                if (participantStableId == null
                    || operationStableId == null)
                {
                    return Failure(
                        false,
                        lastAdmission,
                        "stage1-personal-reward-delivery-identity-missing");
                }

                PersonalRewardDeliveryEnvelopeV1 envelope;
                if (!deliveryOutbox.TryGet(
                        operationStableId,
                        participantStableId,
                        out envelope)
                    || envelope == null)
                {
                    return Failure(
                        false,
                        lastAdmission,
                        "stage1-personal-reward-outbox-envelope-missing");
                }

                fingerprint.Append("\nparticipant_")
                    .Append(index)
                    .Append("=")
                    .Append(participantStableId)
                    .Append("|")
                    .Append(generated.Fingerprint)
                    .Append("|")
                    .Append((int)envelope.State);

                if (participantStableId != localParticipantStableId)
                {
                    continue;
                }
                if (envelope.State == PersonalRewardDeliveryStateV1.Delivered)
                {
                    continue;
                }

                PendingTerminalDropAdmissionResultV1 admission;
                try
                {
                    admission = terminalDrops.PendingAdmission.Admit(generated);
                }
                catch (Exception exception)
                {
                    return Failure(
                        false,
                        lastAdmission,
                        "stage1-personal-reward-admission-exception:"
                            + exception.GetType().Name
                            + ":"
                            + exception.Message);
                }
                lastAdmission = admission;
                if (admission == null || !admission.IsAccepted)
                {
                    bool conflict = admission != null
                        && admission.Status
                            == PendingTerminalDropAdmissionStatusV1
                                .ConflictingDuplicate;
                    return Failure(
                        conflict,
                        admission,
                        admission == null
                            ? "stage1-personal-reward-admission-null"
                            : admission.Diagnostic);
                }

                TerminalDropSourceFactV1 source =
                    admission.PendingResult.SourceFact;
                admissionBridge.RegisterFixedSource(
                    runStableId,
                    lifecycleGeneration,
                    source.SourceEntityStableId,
                    source.SourcePlacementStableId,
                    roomStableId,
                    position,
                    positionFingerprint);
                PickupDeliveryResultV1 queued =
                    admissionBridge.TryEnqueue(admission);
                if (queued == null || !queued.IsAcknowledged)
                {
                    bool conflict = queued != null
                        && queued.Disposition
                            == PickupDeliveryDispositionV1
                                .ConflictingDuplicate;
                    bool rejected = queued != null
                        && queued.Disposition
                            == PickupDeliveryDispositionV1.Rejected;
                    return Failure(
                        conflict || rejected,
                        admission,
                        queued == null
                            ? "stage1-personal-reward-queue-null"
                            : queued.Diagnostic);
                }

                string deliveryFingerprint =
                    RewardGenerationFingerprintV1.Compute(
                        generated.Fingerprint
                        + "|"
                        + positionFingerprint
                        + "|"
                        + queued.Disposition);
                PersonalRewardDeliveryEnvelopeV1 delivered;
                string deliveryDiagnostic;
                if (!deliveryOutbox.TryMarkDelivered(
                        operationStableId,
                        participantStableId,
                        envelope.Result.Fingerprint,
                        deliveryFingerprint,
                        out delivered,
                        out deliveryDiagnostic))
                {
                    return Failure(
                        true,
                        admission,
                        string.IsNullOrWhiteSpace(deliveryDiagnostic)
                            ? "stage1-personal-reward-outbox-ack-rejected"
                            : deliveryDiagnostic);
                }
            }

            admissionBridge.ProcessPending();
            return new Stage1PersonalRewardBatchDeliveryResultV1(
                true,
                false,
                lastAdmission,
                RewardGenerationFingerprintV1.Compute(
                    fingerprint.ToString()),
                admissionBridge.LastDiagnostic);
        }

        private static Stage1PersonalRewardBatchDeliveryResultV1 Failure(
            bool conflict,
            PendingTerminalDropAdmissionResultV1 admission,
            string diagnostic)
        {
            return new Stage1PersonalRewardBatchDeliveryResultV1(
                false,
                conflict,
                admission,
                string.Empty,
                diagnostic);
        }
    }
}

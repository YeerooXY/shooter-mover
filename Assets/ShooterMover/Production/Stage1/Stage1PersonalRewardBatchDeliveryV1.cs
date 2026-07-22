using System;
using System.Text;
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
    /// Admits and queues every personal result from one immutable shared source event.
    /// It contains no probability, pacing, tier or profile-selection rules.
    /// </summary>
    internal sealed class Stage1PersonalRewardBatchDeliveryV1
    {
        private readonly TerminalDropBindingCompositionV1 terminalDrops;
        private readonly PendingAdmissionPickupBridgeV1 admissionBridge;

        public Stage1PersonalRewardBatchDeliveryV1(
            TerminalDropBindingCompositionV1 terminalDrops,
            PendingAdmissionPickupBridgeV1 admissionBridge)
        {
            this.terminalDrops = terminalDrops
                ?? throw new ArgumentNullException(nameof(terminalDrops));
            this.admissionBridge = admissionBridge
                ?? throw new ArgumentNullException(nameof(admissionBridge));
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
                return new Stage1PersonalRewardBatchDeliveryResultV1(
                    false,
                    false,
                    null,
                    string.Empty,
                    batch == null
                        ? "stage1-personal-reward-batch-null"
                        : batch.Diagnostic);
            }
            var fingerprint = new StringBuilder(
                "schema=stage1-personal-reward-delivery-v1");
            PendingTerminalDropAdmissionResultV1 lastAdmission = null;
            for (int index = 0; index < batch.Results.Count; index++)
            {
                GeneratedTerminalDropResultV1 generated = batch.Results[index];
                if (generated == null || !generated.IsAccepted)
                {
                    return new Stage1PersonalRewardBatchDeliveryResultV1(
                        false,
                        generated != null
                            && generated.Status
                                == TerminalDropBindingStatusV1
                                    .ConflictingDuplicate,
                        lastAdmission,
                        string.Empty,
                        generated == null
                            ? "stage1-personal-reward-result-null"
                            : generated.Diagnostic);
                }

                PendingTerminalDropAdmissionResultV1 admission;
                try
                {
                    admission = terminalDrops.PendingAdmission.Admit(generated);
                }
                catch (Exception exception)
                {
                    return new Stage1PersonalRewardBatchDeliveryResultV1(
                        false,
                        false,
                        lastAdmission,
                        string.Empty,
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
                    return new Stage1PersonalRewardBatchDeliveryResultV1(
                        false,
                        conflict,
                        admission,
                        string.Empty,
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
                    return new Stage1PersonalRewardBatchDeliveryResultV1(
                        false,
                        conflict || rejected,
                        admission,
                        string.Empty,
                        queued == null
                            ? "stage1-personal-reward-queue-null"
                            : queued.Diagnostic);
                }
                fingerprint.Append("\nresult_")
                    .Append(index)
                    .Append("=")
                    .Append(generated.Fingerprint);
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
    }
}

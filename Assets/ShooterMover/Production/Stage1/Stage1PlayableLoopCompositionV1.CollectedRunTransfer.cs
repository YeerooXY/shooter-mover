using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.TerminalDropBinding;
using ShooterMover.UI.ProductionFlow;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private bool collectedRunTransferExitHookInstalled;
        private MissionResultPayloadV1 pendingCollectedRunMissionResult;
        private ProductionResultsSummaryV1 pendingCollectedRunSummary;

        /// <summary>
        /// Replaces only this composition's retained direct mission-result callback. Before
        /// accepted End, rejection re-arms the exit. After accepted End, this update loop
        /// retains the immutable Results handoff until the scene transition accepts it.
        /// </summary>
        private void LateUpdate()
        {
            if (pendingCollectedRunMissionResult != null)
                TryPresentPendingCollectedRunResults();

            if (!initialized
                || rooms == null
                || collectedRunTransferExitHookInstalled)
            {
                return;
            }
            rooms.FinalExitReached -= HandleFinalExitReached;
            rooms.FinalExitReached +=
                HandleFinalExitReachedWithCollectedRunTransfer;
            collectedRunTransferExitHookInstalled = true;
        }

        private void HandleFinalExitReachedWithCollectedRunTransfer()
        {
            if (ending) return;
            if (rooms != null)
            {
                rooms.FinalExitReached -=
                    HandleFinalExitReachedWithCollectedRunTransfer;
            }
            ending = true;
            effectEmitter.ClearEmittedEffects();
            CommitPendingExperienceRewards();

            RunSessionAggregateV1 sharedRun;
            if (!TryResolveSharedRunSession(out sharedRun)
                || sharedRun == null
                || sharedRun.LifecycleState
                    != RunSessionLifecycleStateV1.Active)
            {
                RejectBeforeAcceptedEnd(
                    "The shared active Run Session is unavailable at final exit.");
                return;
            }

            Stage1RunPickupBootstrap2D pickupBootstrap =
                GetComponent<Stage1RunPickupBootstrap2D>();
            TerminalDropRunGenerationContextV1 dropContext;
            TerminalDropRejectionCodeV1 dropContextRejection;
            string dropContextDiagnostic;
            if (pickupBootstrap == null
                || !pickupBootstrap.DropRunContext.TryResolve(
                    sharedRun.RunStableId,
                    sharedRun.LifecycleGeneration,
                    out dropContext,
                    out dropContextRejection,
                    out dropContextDiagnostic)
                || dropContext == null)
            {
                RejectBeforeAcceptedEnd(
                    string.IsNullOrWhiteSpace(dropContextDiagnostic)
                        ? "The frozen terminal-drop run context is unavailable: "
                            + dropContextRejection
                        : dropContextDiagnostic);
                return;
            }

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 currentProfile;
            CharacterCompositionCoordinatorV1 composition;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out currentProfile,
                    out composition)
                || graph == null
                || graph.IsDisposed
                || composition == null
                || currentProfile == null)
            {
                RejectBeforeAcceptedEnd(
                    "The selected account-backed character graph is unavailable.");
                return;
            }
            ProductionCollectedRunRewardRuntimeRegistryV2.BindRuntime(
                graph,
                composition);

            RewardApplicationServiceV1 rewardApplication;
            CollectedRunRewardPreparedTransferAuthorityV1 preparedAuthority;
            CollectedRunRewardTransferReceiptAuthorityV1 receipts;
            if (!ProductionCollectedRunRewardRuntimeRegistryV2.TryResolve(
                    graph.Character.CharacterInstanceStableId,
                    out rewardApplication,
                    out preparedAuthority,
                    out receipts))
            {
                RejectBeforeAcceptedEnd(
                    "The selected character transfer authorities are unavailable.");
                return;
            }

            IReadOnlyList<RunSessionCollectedRewardV1> collectedRewards =
                sharedRun.ExportCollectedRunRewards();
            long moneyEarned = SumCollectedReward(
                collectedRewards,
                RewardGrantKindV1.Money);
            long scrapEarned = SumCollectedReward(
                collectedRewards,
                RewardGrantKindV1.Scrap);

            var endCommand = new EndRunSessionCommandV1(
                StableId.Create(
                    "operation",
                    "drop-persist-proof-end-g"
                    + sharedRun.LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture)),
                sharedRun.RunStableId,
                sharedRun.LifecycleGeneration,
                MissionRunCompletionStateV1.Completed,
                sharedRun.AuthoritativeTick + 1L);
            var generationContext =
                new CollectedRunRewardGenerationContextV2(
                    dropContext.RootSeed,
                    dropContext.GenerationAlgorithmVersion,
                    dropContext.ProgressionContext,
                    dropContext.EventModifierContextFingerprint);

            CollectedRunRewardPreparedTransferV1 awaiting;
            string preparationDiagnostic;
            if (!CollectedRunRewardTransferPreparationFactoryV2
                .TryCreateAwaitingAcceptedEnd(
                    endCommand,
                    collectedRewards,
                    graph,
                    rewardApplication,
                    receipts,
                    preparedAuthority,
                    generationContext,
                    pickupBootstrap.EquipmentPayloadSource,
                    out awaiting,
                    out preparationDiagnostic)
                || awaiting == null)
            {
                RejectBeforeAcceptedEnd(
                    string.IsNullOrWhiteSpace(preparationDiagnostic)
                        ? "The collected-run transfer could not be frozen before End."
                        : preparationDiagnostic);
                return;
            }

            var persistence = new ProductionCollectedRunRewardPersistenceV2(
                composition,
                preparedAuthority,
                receipts,
                graph.Character.CharacterInstanceStableId);
            CollectedRunRewardTransferPersistenceResultV1 awaitingSaved =
                persistence.PersistPreparedCustody(awaiting);
            if (awaitingSaved == null || !awaitingSaved.Succeeded)
            {
                RejectBeforeAcceptedEnd(
                    awaitingSaved == null
                        ? "The pre-End transfer custody save returned no result."
                        : awaitingSaved.Diagnostic);
                return;
            }

            CollectedRunRewardPreparedTransferV1 prepared = null;
            CollectedRunRewardAtomicPlanV2 plan = null;
            RunSessionEndResultV1 acceptedEnd =
                sharedRun.EndWithDurableAcceptance(
                    endCommand,
                    candidateEnd =>
                    {
                        if (candidateEnd == null
                            || candidateEnd.Receipt == null
                            || candidateEnd.Receipt.SelectedCharacterStableId
                                != awaiting.SelectedCharacterStableId
                            || candidateEnd.Receipt.ExpectedCharacterRevision
                                != awaiting.ExpectedCharacterRevision
                            || !string.Equals(
                                candidateEnd.Receipt
                                    .ExpectedCharacterFingerprint,
                                awaiting.ExpectedCharacterFingerprint,
                                StringComparison.Ordinal))
                        {
                            return RunSessionDurableAcceptanceResultV1
                                .Rejected(
                                    "The accepted mission result does not match the exact character frozen into transfer custody.");
                        }

                        CollectedRunRewardPreparedTransferV1 candidatePrepared;
                        CollectedRunRewardAtomicPlanV2 candidatePlan;
                        string planDiagnostic;
                        if (!CollectedRunRewardTransferPreparationFactoryV2
                            .TryAcceptEndAndBuildPlan(
                                candidateEnd,
                                awaiting,
                                graph,
                                rewardApplication,
                                out candidatePrepared,
                                out candidatePlan,
                                out planDiagnostic)
                            || candidatePrepared == null
                            || candidatePlan == null)
                        {
                            return RunSessionDurableAcceptanceResultV1
                                .Rejected(
                                    string.IsNullOrWhiteSpace(planDiagnostic)
                                        ? "The immutable transfer plan could not be constructed before End acceptance."
                                        : planDiagnostic);
                        }

                        CollectedRunRewardTransferPersistenceResultV1
                            preparedSaved =
                                persistence.PersistPreparedCustody(
                                    candidatePrepared);
                        if (preparedSaved == null
                            || !preparedSaved.Succeeded)
                        {
                            return RunSessionDurableAcceptanceResultV1
                                .Rejected(
                                    preparedSaved == null
                                        ? "The Prepared transfer custody save returned no result."
                                        : preparedSaved.Diagnostic);
                        }
                        prepared = candidatePrepared;
                        plan = candidatePlan;
                        return RunSessionDurableAcceptanceResultV1
                            .Accepted();
                    });
            if (acceptedEnd == null
                || !acceptedEnd.Succeeded
                || acceptedEnd.Receipt == null
                || acceptedEnd.Receipt.MissionResult == null)
            {
                RejectBeforeAcceptedEnd(
                    acceptedEnd == null
                        ? "The Run Session durable End authority returned no result."
                        : acceptedEnd.RejectionCode);
                return;
            }

            if (prepared == null || plan == null)
            {
                ProductionCollectedRunRewardResultsBridge.Clear();
                ProductionCollectedRunRewardResultsBridge
                    .PublishPreparationFailure(
                        awaiting,
                        "Durable End succeeded without the exact Prepared transfer plan.");
                ConfigureTransferOverlay();
                QueueAcceptedResults(
                    acceptedEnd.Receipt.MissionResult,
                    BuildTransferSummary(
                        currentProfile,
                        moneyEarned,
                        scrapEarned));
                return;
            }

            var authority =
                new ProductionCollectedRunRewardAtomicAuthorityV2(
                    graph,
                    rewardApplication,
                    preparedAuthority,
                    receipts);
            var transfer =
                new ProductionCollectedRunRewardTransferServiceV2(
                    plan,
                    authority,
                    persistence);
            CollectedRunRewardTransferResultV1 transferResult =
                transfer.Apply();
            if (transferResult == null)
            {
                transferResult = new CollectedRunRewardTransferResultV1(
                    CollectedRunRewardTransferStatusV1.PreparationFailed,
                    prepared.TransferOperationStableId,
                    prepared.BatchFingerprint,
                    prepared.RunStableId,
                    prepared.SelectedCharacterStableId,
                    null,
                    null,
                    CollectedRunRewardTransferPersistenceResultV1
                        .NotAttempted(string.Empty),
                    "The collected-run transfer authority returned no result.",
                    string.Empty,
                    true);
            }

            ProductionCollectedRunRewardResultsBridge.Clear();
            ProductionCollectedRunRewardResultsBridge.Publish(
                prepared,
                transferResult);
            ConfigureTransferOverlay();
            QueueAcceptedResults(
                acceptedEnd.Receipt.MissionResult,
                BuildTransferSummary(
                    currentProfile,
                    moneyEarned,
                    scrapEarned));
        }

        private ProductionResultsSummaryV1 BuildTransferSummary(
            ProductionFlowProfileRecordV1 currentProfile,
            long moneyEarned,
            long scrapEarned)
        {
            StableId participantId = controller.PlayerRunParticipantId;
            ParticipantRunStats stats;
            if (!participantStats.TryGetValue(participantId, out stats))
                stats = new ParticipantRunStats(participantId);
            return new ProductionResultsSummaryV1(
                currentProfile.DisplayName,
                DisplayClass(
                    currentProfile.Payload.LoadoutProfileStableId),
                experience.CurrentState.Level,
                participantId,
                stats.Kills,
                stats.Experience,
                moneyEarned,
                scrapEarned);
        }

        private void ConfigureTransferOverlay()
        {
            ProductionCollectedRunRewardResultsOverlay overlay =
                flow.GetComponent<
                    ProductionCollectedRunRewardResultsOverlay>();
            if (overlay == null)
            {
                overlay = flow.gameObject.AddComponent<
                    ProductionCollectedRunRewardResultsOverlay>();
            }
            overlay.Configure();
        }

        private void QueueAcceptedResults(
            MissionResultPayloadV1 missionResult,
            ProductionResultsSummaryV1 summary)
        {
            pendingCollectedRunMissionResult = missionResult
                ?? throw new ArgumentNullException(nameof(missionResult));
            pendingCollectedRunSummary = summary
                ?? throw new ArgumentNullException(nameof(summary));
            TryPresentPendingCollectedRunResults();
        }

        private void TryPresentPendingCollectedRunResults()
        {
            if (pendingCollectedRunMissionResult == null
                || pendingCollectedRunSummary == null
                || flow == null)
            {
                return;
            }
            try
            {
                if (!ProductionReadOnlyResultsBridgeV1.Present(
                    flow,
                    pendingCollectedRunMissionResult,
                    pendingCollectedRunSummary))
                {
                    diagnostic =
                        "The accepted Results handoff is pending and will retry.";
                    return;
                }
                pendingCollectedRunMissionResult = null;
                pendingCollectedRunSummary = null;
                diagnostic = string.Empty;
            }
            catch (Exception exception)
            {
                diagnostic =
                    "The accepted Results handoff failed and remains pending: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;
            }
        }

        private void RejectBeforeAcceptedEnd(string message)
        {
            ending = false;
            diagnostic = string.IsNullOrWhiteSpace(message)
                ? "The collected-run transfer route was rejected before End."
                : message;
            if (rooms != null)
            {
                rooms.FinalExitReached -=
                    HandleFinalExitReachedWithCollectedRunTransfer;
                rooms.FinalExitReached +=
                    HandleFinalExitReachedWithCollectedRunTransfer;
            }
        }

        private static long SumCollectedReward(
            IReadOnlyList<RunSessionCollectedRewardV1> rewards,
            RewardGrantKindV1 kind)
        {
            long total = 0L;
            if (rewards == null) return total;
            checked
            {
                for (int index = 0; index < rewards.Count; index++)
                {
                    RunSessionCollectedRewardV1 reward = rewards[index];
                    if (reward != null && reward.RewardKind == kind)
                        total += reward.Quantity;
                }
            }
            return total;
        }
    }
}

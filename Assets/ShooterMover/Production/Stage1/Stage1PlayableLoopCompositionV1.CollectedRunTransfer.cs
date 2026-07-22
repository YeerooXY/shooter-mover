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
        private const float DurableEndRetryInitialDelaySeconds = 1f;
        private const float DurableEndRetryMaximumDelaySeconds = 15f;

        private bool collectedRunTransferExitHookInstalled;
        private MissionResultPayloadV1 pendingCollectedRunMissionResult;
        private ProductionResultsSummaryV1 pendingCollectedRunSummary;
        private Action pendingDurableEndRetry;
        private float pendingDurableEndRetryAt;
        private int pendingDurableEndRetryAttempt;

        /// <summary>
        /// Replaces only this composition's retained direct mission-result callback. Before
        /// the mission-result authority accepts, a safe rejection may re-arm the exit. After
        /// terminal result creation, the exact durable End candidate is frozen and retried;
        /// gameplay is never reopened around a terminal mission-result transaction.
        /// </summary>
        private void LateUpdate()
        {
            if (pendingCollectedRunMissionResult != null)
                TryPresentPendingCollectedRunResults();

            if (pendingDurableEndRetry != null
                && Time.unscaledTime >= pendingDurableEndRetryAt)
            {
                Action retry = pendingDurableEndRetry;
                pendingDurableEndRetry = null;
                retry();
            }

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
                if (awaitingSaved == null
                    || awaitingSaved.DurableStateUncertain)
                {
                    FreezeForDurableEndRecovery(
                        awaitingSaved == null
                            ? "The pre-End custody save returned no result; durable state is uncertain."
                            : awaitingSaved.Diagnostic);
                    ProductionCollectedRunRewardResultsBridge.Clear();
                    ProductionCollectedRunRewardResultsBridge
                        .PublishPreparationFailure(
                            awaiting,
                            awaitingSaved == null
                                ? "Pre-End custody durability is uncertain."
                                : awaitingSaved.Diagnostic);
                    ConfigureTransferOverlay();
                    return;
                }
                RejectBeforeAcceptedEnd(awaitingSaved.Diagnostic);
                return;
            }

            Action attemptDurableEnd = null;
            attemptDurableEnd = () =>
            {
                CollectedRunRewardPreparedTransferV1 prepared = null;
                CollectedRunRewardAtomicPlanV2 plan = null;
                CollectedRunRewardTransferPersistenceResultV1 preparedSaved =
                    null;
                RunSessionDurableAcceptanceResultV1 acceptance = null;

                RunSessionEndResultV1 acceptedEnd =
                    sharedRun.EndWithDurableAcceptance(
                        endCommand,
                        candidateEnd =>
                        {
                            if (candidateEnd == null
                                || candidateEnd.Receipt == null
                                || candidateEnd.Receipt
                                    .SelectedCharacterStableId
                                    != awaiting.SelectedCharacterStableId
                                || candidateEnd.Receipt
                                    .ExpectedCharacterRevision
                                    != awaiting.ExpectedCharacterRevision
                                || !string.Equals(
                                    candidateEnd.Receipt
                                        .ExpectedCharacterFingerprint,
                                    awaiting.ExpectedCharacterFingerprint,
                                    StringComparison.Ordinal))
                            {
                                acceptance =
                                    RunSessionDurableAcceptanceResultV1
                                        .RejectedBeforeDurability(
                                            "The terminal mission result does not match the exact character frozen into transfer custody.");
                                return acceptance;
                            }

                            string planDiagnostic;
                            if (!CollectedRunRewardTransferPreparationFactoryV2
                                .TryAcceptEndAndBuildPlan(
                                    candidateEnd,
                                    awaiting,
                                    graph,
                                    rewardApplication,
                                    out prepared,
                                    out plan,
                                    out planDiagnostic)
                                || prepared == null
                                || plan == null)
                            {
                                acceptance =
                                    RunSessionDurableAcceptanceResultV1
                                        .RejectedBeforeDurability(
                                            string.IsNullOrWhiteSpace(
                                                    planDiagnostic)
                                                ? "The immutable transfer plan could not be constructed before End acceptance."
                                                : planDiagnostic);
                                return acceptance;
                            }

                            preparedSaved =
                                persistence.PersistPreparedCustody(prepared);
                            if (preparedSaved == null)
                            {
                                acceptance =
                                    RunSessionDurableAcceptanceResultV1
                                        .Uncertain(
                                            "The Prepared custody save returned no result.");
                                return acceptance;
                            }
                            if (!preparedSaved.Succeeded)
                            {
                                acceptance = preparedSaved
                                    .DurableStateUncertain
                                    ? RunSessionDurableAcceptanceResultV1
                                        .Uncertain(preparedSaved.Diagnostic)
                                    : RunSessionDurableAcceptanceResultV1
                                        .RejectedBeforeDurability(
                                            preparedSaved.Diagnostic);
                                return acceptance;
                            }

                            acceptance =
                                RunSessionDurableAcceptanceResultV1.Accepted();
                            return acceptance;
                        });

                if (acceptedEnd != null
                    && acceptedEnd.Succeeded
                    && acceptedEnd.Receipt != null
                    && acceptedEnd.Receipt.MissionResult != null)
                {
                    pendingDurableEndRetry = null;
                    pendingDurableEndRetryAttempt = 0;
                    CompleteAcceptedCollectedRunTransfer(
                        acceptedEnd,
                        awaiting,
                        prepared,
                        plan,
                        graph,
                        rewardApplication,
                        preparedAuthority,
                        receipts,
                        persistence,
                        currentProfile,
                        moneyEarned,
                        scrapEarned);
                    return;
                }

                if (sharedRun.DurableEndState
                    == RunSessionDurableEndStateV1.PendingExactRetry)
                {
                    FreezeForDurableEndRecovery(
                        acceptedEnd == null
                            ? "The exact terminal transaction returned no result and remains pending."
                            : acceptedEnd.RejectionCode);
                    ScheduleDurableEndRetry(
                        attemptDurableEnd,
                        acceptedEnd == null
                            ? "run-end-result-null"
                            : acceptedEnd.RejectionCode);
                    return;
                }

                if (sharedRun.DurableEndState
                    == RunSessionDurableEndStateV1.DurableStateUncertain
                    || (acceptance != null
                        && acceptance.DurableStateUncertain))
                {
                    FreezeForDurableEndRecovery(
                        acceptedEnd == null
                            ? "The terminal durability result is unavailable."
                            : acceptedEnd.RejectionCode);
                    PublishDurableEndUncertainty(
                        awaiting,
                        prepared,
                        preparedSaved,
                        acceptedEnd,
                        currentProfile,
                        moneyEarned,
                        scrapEarned);
                    return;
                }

                RejectBeforeAcceptedEnd(
                    acceptedEnd == null
                        ? "The Run Session durable End authority returned no result."
                        : acceptedEnd.RejectionCode);
            };

            attemptDurableEnd();
        }

        private void CompleteAcceptedCollectedRunTransfer(
            RunSessionEndResultV1 acceptedEnd,
            CollectedRunRewardPreparedTransferV1 awaiting,
            CollectedRunRewardPreparedTransferV1 prepared,
            CollectedRunRewardAtomicPlanV2 plan,
            ProductionCharacterRuntimeGraphV1 graph,
            RewardApplicationServiceV1 rewardApplication,
            CollectedRunRewardPreparedTransferAuthorityV1 preparedAuthority,
            CollectedRunRewardTransferReceiptAuthorityV1 receipts,
            ProductionCollectedRunRewardPersistenceV2 persistence,
            ProductionFlowProfileRecordV1 currentProfile,
            long moneyEarned,
            long scrapEarned)
        {
            if (prepared == null || plan == null)
            {
                ProductionCollectedRunRewardResultsBridge.Clear();
                ProductionCollectedRunRewardResultsBridge
                    .PublishPreparationFailure(
                        awaiting,
                        "Durable End succeeded without the exact transfer plan.");
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
                    new CollectedRunRewardTransferPersistenceResultV1(
                        CollectedRunRewardTransferPersistenceStatusV1
                            .DurableStateUncertain,
                        0L,
                        string.Empty,
                        0L,
                        string.Empty,
                        "The collected-run transfer authority returned no result after accepted End."),
                    "The collected-run transfer authority returned no result after accepted End.",
                    "Durable transfer completion cannot be proven; automatic retry is disabled.",
                    false);
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

        private void PublishDurableEndUncertainty(
            CollectedRunRewardPreparedTransferV1 awaiting,
            CollectedRunRewardPreparedTransferV1 prepared,
            CollectedRunRewardTransferPersistenceResultV1 persistence,
            RunSessionEndResultV1 candidateEnd,
            ProductionFlowProfileRecordV1 currentProfile,
            long moneyEarned,
            long scrapEarned)
        {
            ProductionCollectedRunRewardResultsBridge.Clear();
            string diagnostic = candidateEnd == null
                ? "The exact terminal transaction has uncertain durable state."
                : candidateEnd.RejectionCode;

            if (prepared != null)
            {
                CollectedRunRewardTransferPersistenceResultV1 exactPersistence =
                    persistence
                    ?? new CollectedRunRewardTransferPersistenceResultV1(
                        CollectedRunRewardTransferPersistenceStatusV1
                            .DurableStateUncertain,
                        0L,
                        string.Empty,
                        0L,
                        string.Empty,
                        diagnostic);
                var fatal = new CollectedRunRewardTransferResultV1(
                    CollectedRunRewardTransferStatusV1
                        .FatalCompensationFailure,
                    prepared.TransferOperationStableId,
                    prepared.BatchFingerprint,
                    prepared.RunStableId,
                    prepared.SelectedCharacterStableId,
                    null,
                    null,
                    exactPersistence,
                    diagnostic,
                    "Durable End acceptance cannot prove whether Prepared custody replaced the active account file.",
                    false);
                ProductionCollectedRunRewardResultsBridge.Publish(
                    prepared,
                    fatal);
            }
            else
            {
                ProductionCollectedRunRewardResultsBridge
                    .PublishPreparationFailure(awaiting, diagnostic);
            }

            ConfigureTransferOverlay();
            if (candidateEnd != null
                && candidateEnd.Receipt != null
                && candidateEnd.Receipt.MissionResult != null)
            {
                QueueAcceptedResults(
                    candidateEnd.Receipt.MissionResult,
                    BuildTransferSummary(
                        currentProfile,
                        moneyEarned,
                        scrapEarned));
            }
        }

        private void ScheduleDurableEndRetry(
            Action retry,
            string retryDiagnostic)
        {
            pendingDurableEndRetry = retry
                ?? throw new ArgumentNullException(nameof(retry));
            int exponent = Math.Min(pendingDurableEndRetryAttempt, 4);
            float delay = Mathf.Min(
                DurableEndRetryMaximumDelaySeconds,
                DurableEndRetryInitialDelaySeconds * (1 << exponent));
            pendingDurableEndRetryAttempt++;
            pendingDurableEndRetryAt = Time.unscaledTime + delay;
            diagnostic =
                "The exact terminal transaction is frozen and will retry in "
                + delay.ToString("0", CultureInfo.InvariantCulture)
                + "s: "
                + retryDiagnostic;
        }

        private void FreezeForDurableEndRecovery(string message)
        {
            ending = true;
            diagnostic = string.IsNullOrWhiteSpace(message)
                ? "The terminal transaction is frozen for durable recovery."
                : message;
            DisableGameplayBehaviour(controller);
            DisableGameplayBehaviour(rooms);
            DisableGameplayBehaviour(weapons);
            DisableGameplayBehaviour(effectEmitter);
        }

        private static void DisableGameplayBehaviour(object candidate)
        {
            Behaviour behaviour = candidate as Behaviour;
            if (behaviour != null) behaviour.enabled = false;
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

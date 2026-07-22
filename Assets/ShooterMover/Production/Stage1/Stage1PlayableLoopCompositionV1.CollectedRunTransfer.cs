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

        /// <summary>
        /// Replaces only this composition's retained direct mission-result callback after
        /// the room runtime has been built. The scene controller and collision callbacks
        /// remain unaware of permanent rewards.
        /// </summary>
        private void LateUpdate()
        {
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
                RejectCollectedRunTransferExit(
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
                RejectCollectedRunTransferExit(
                    string.IsNullOrWhiteSpace(dropContextDiagnostic)
                        ? "The frozen terminal-drop run context is unavailable: "
                            + dropContextRejection
                        : dropContextDiagnostic);
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

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 currentProfile;
            CharacterCompositionCoordinatorV1 composition;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out currentProfile,
                    out composition)
                || graph == null
                || composition == null
                || currentProfile == null)
            {
                RejectCollectedRunTransferExit(
                    "The selected account-backed character graph is unavailable.");
                return;
            }

            RewardApplicationServiceV1 rewardApplication;
            CollectedRunRewardTransferReceiptAuthorityV1 receipts;
            if (!ProductionCollectedRunRewardTransferRuntimeRegistry
                .TryResolve(
                    graph.Character.CharacterInstanceStableId,
                    out rewardApplication,
                    out receipts))
            {
                RejectCollectedRunTransferExit(
                    "The selected character reward and receipt authorities are unavailable.");
                return;
            }

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
            RunSessionEndResultV1 acceptedEnd =
                sharedRun.End(endCommand);
            if (acceptedEnd == null
                || !acceptedEnd.Succeeded
                || acceptedEnd.Receipt == null)
            {
                RejectCollectedRunTransferExit(
                    acceptedEnd == null
                        ? "The Run Session end authority returned no result."
                        : acceptedEnd.RejectionCode);
                return;
            }

            var generationContext =
                new CollectedRunRewardGenerationContextV1(
                    dropContext.RootSeed,
                    dropContext.GenerationAlgorithmVersion,
                    dropContext.ProgressionContext,
                    dropContext.EventModifierContextFingerprint);
            CollectedRunRewardApplicationPlanV1 plan;
            string planDiagnostic;
            if (!RunSessionCollectedRewardTransferPlanFactory.TryCreate(
                    acceptedEnd,
                    collectedRewards,
                    graph.Character,
                    generationContext,
                    graph,
                    rewardApplication,
                    null,
                    out plan,
                    out planDiagnostic)
                || plan == null)
            {
                // The run is ended, but Results is intentionally not entered without an
                // immutable exact transfer plan. No permanent authority was mutated here.
                diagnostic = string.IsNullOrWhiteSpace(planDiagnostic)
                    ? "The collected-run transfer plan was rejected."
                    : planDiagnostic;
                return;
            }

            var authority =
                new ProductionCollectedRunRewardTransferAuthorityAdapter(
                    graph,
                    composition,
                    rewardApplication,
                    receipts,
                    plan);
            var preflightedAuthority =
                new FullyPreflightedCollectedRunRewardTransferAuthorityAdapter(
                    graph,
                    rewardApplication,
                    plan,
                    authority);
            var persistence =
                new ProductionCollectedRunRewardTransferPersistenceAdapter(
                    composition,
                    receipts,
                    graph.Character.CharacterInstanceStableId);
            var transfer =
                new FullyPreflightedCollectedRunRewardTransferService(
                    plan,
                    authority,
                    preflightedAuthority,
                    persistence);
            CollectedRunRewardTransferResultV1 transferResult =
                transfer.Apply();
            if (transferResult == null)
            {
                diagnostic =
                    "The collected-run transfer authority returned no result.";
                return;
            }

            ProductionCollectedRunRewardResultsBridge.Clear();
            ProductionCollectedRunRewardResultsBridge.Publish(
                plan.Batch,
                transferResult,
                transfer.Apply);

            StableId participantId = controller.PlayerRunParticipantId;
            ParticipantRunStats stats;
            if (!participantStats.TryGetValue(participantId, out stats))
                stats = new ParticipantRunStats(participantId);

            var summary = new ProductionResultsSummaryV1(
                currentProfile.DisplayName,
                DisplayClass(
                    currentProfile.Payload.LoadoutProfileStableId),
                experience.CurrentState.Level,
                participantId,
                stats.Kills,
                stats.Experience,
                moneyEarned,
                scrapEarned);
            if (!ProductionReadOnlyResultsBridgeV1.Present(
                flow,
                acceptedEnd.Receipt.MissionResult,
                summary))
            {
                ending = false;
                diagnostic =
                    "The canonical Results handoff rejected the mission result.";
            }
        }

        private void RejectCollectedRunTransferExit(string message)
        {
            ending = false;
            diagnostic = string.IsNullOrWhiteSpace(message)
                ? "The collected-run transfer route was rejected."
                : message;
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

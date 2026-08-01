using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Production terminal composition for the existing RunSession, RewardClaim and Results
    /// authorities. It projects only already-collected pickup journal facts and never generates
    /// another reward or opens a strongbox in the arena.
    /// </summary>
    internal sealed class RunFinish
    {
        private readonly RunLoot runtime;
        private readonly CharacterLiveGraph graph;
        private readonly CharacterSetupFlow composition;

        private RewardApplicationActions rewardApplication;
        private RewardClaimPreparedTransferStore preparedAuthority;
        private RewardClaimTransferReceiptState receipts;
        private RewardClaimPersistence persistence;
        private RewardClaimPreparedTransfer awaiting;
        private RewardClaimPreparedTransfer prepared;
        private RewardClaimAtomicPlan plan;
        private EndRunSessionCommand endCommand;
        private RunSessionEndResult acceptedEnd;
        private ResultsContext pendingResults;
        private MissionResultPayload latestResult;
        private bool strongboxesRecorded;

        public RunFinish(
            RunLoot runtime,
            CharacterLiveGraph graph,
            CharacterSetupFlow composition)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
        }

        public string LastDiagnostic { get; private set; } = string.Empty;

        public bool Complete()
        {
            try
            {
                if (pendingResults != null)
                {
                    bool presented = GameFlow.PresentResults(pendingResults);
                    LastDiagnostic = presented
                        ? string.Empty
                        : "run-results-transition-pending";
                    return presented;
                }

                if (!TryPrepareTerminalTransfer())
                {
                    return false;
                }
                if (!TryAcceptRunEnd())
                {
                    return false;
                }
                if (!TryApplyTransfer())
                {
                    return false;
                }

                pendingResults = BuildResultsContext(
                    acceptedEnd.Receipt.MissionResult);
                bool accepted = GameFlow.PresentResults(pendingResults);
                LastDiagnostic = accepted
                    ? string.Empty
                    : "run-results-transition-pending";
                return accepted;
            }
            catch (Exception exception)
            {
                LastDiagnostic = "run-reward-completion-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return false;
            }
        }

        private bool TryPrepareTerminalTransfer()
        {
            if (awaiting != null)
            {
                return true;
            }
            if (runtime.Run.LifecycleState
                != RunSessionLifecycleState.Active)
            {
                LastDiagnostic = "run-reward-completion-run-not-active";
                return false;
            }

            RewardClaimLiveRegistry.BindRuntime(graph, composition);
            if (!RewardClaimLiveRegistry.TryResolve(
                    graph.Character.CharacterInstanceStableId,
                    out rewardApplication,
                    out preparedAuthority,
                    out receipts)
                || rewardApplication == null
                || preparedAuthority == null
                || receipts == null)
            {
                LastDiagnostic =
                    "run-reward-completion-transfer-authorities-unavailable";
                return false;
            }

            if (!strongboxesRecorded && !RecordCollectedStrongboxes())
            {
                return false;
            }
            strongboxesRecorded = true;

            endCommand = new EndRunSessionCommand(
                StableId.Create(
                    "operation",
                    "playable-run-end-"
                    + RunFingerprint.Hash(
                        runtime.RunStableId
                        + "|"
                        + runtime.Run.LifecycleGeneration.ToString(
                            CultureInfo.InvariantCulture))
                        .Substring(0, 32)),
                runtime.RunStableId,
                runtime.Run.LifecycleGeneration,
                MissionRunCompletionState.Completed,
                checked(runtime.Run.AuthoritativeTick + 1L));

            var generationContext = new RewardClaimGenerationContext(
                unchecked((ulong)runtime.Run.StartCommand.DeterministicSeed),
                RunLoot.GenerationAlgorithmVersion,
                runtime.FrozenProgression,
                runtime.Run.StartCommand.EventModifierContextFingerprint);
            string diagnostic;
            if (!RewardClaimTransferPreparationFactory
                    .TryCreateAwaitingAcceptedEnd(
                        endCommand,
                        runtime.Run.ExportRewardClaims(),
                        graph,
                        rewardApplication,
                        receipts,
                        preparedAuthority,
                        generationContext,
                        new RejectingCollectedRunEquipmentPayloadSource(),
                        out awaiting,
                        out diagnostic)
                || awaiting == null)
            {
                LastDiagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "run-reward-completion-awaiting-preparation-rejected"
                    : diagnostic;
                return false;
            }

            persistence = new RewardClaimPersistence(
                composition,
                preparedAuthority,
                receipts,
                graph.Character.CharacterInstanceStableId);
            RewardClaimTransferPersistenceResult saved =
                persistence.PersistPreparedCustody(awaiting);
            if (saved == null || !saved.Succeeded)
            {
                LastDiagnostic = saved == null
                    ? "run-reward-completion-awaiting-save-null"
                    : saved.Diagnostic;
                return false;
            }
            return true;
        }

        private bool RecordCollectedStrongboxes()
        {
            IReadOnlyList<RunSessionCollectedReward> rewards =
                runtime.Run.ExportRewardClaims();
            for (int index = 0; index < rewards.Count; index++)
            {
                RunSessionCollectedReward reward = rewards[index];
                if (reward == null
                    || reward.RewardKind != RewardGrantKind.Strongbox)
                {
                    continue;
                }

                StableId operation = StableId.Create(
                    "operation",
                    "run-result-box-"
                    + RunFingerprint.Hash(
                        reward.CollectionOperationStableId
                        + "|"
                        + reward.GeneratedRewardChildStableId
                        + "|"
                        + reward.Fingerprint)
                        .Substring(0, 32));
                MissionRunStateResult recorded =
                    runtime.Run.RecordCollectedStrongbox(
                        new RunStrongboxCollectionRequest(
                            operation,
                            reward.RunStableId,
                            reward.RunLifecycleGeneration,
                            reward.ContentStableId,
                            reward.GeneratedRewardChildStableId,
                            reward.SourceGrantStableId,
                            reward.DropOperationStableId));
                if (recorded == null || !recorded.Succeeded)
                {
                    LastDiagnostic = recorded == null
                        ? "run-result-strongbox-record-null"
                        : recorded.RejectionCode;
                    return false;
                }
            }
            return true;
        }

        private bool TryAcceptRunEnd()
        {
            if (acceptedEnd != null)
            {
                return acceptedEnd.Succeeded;
            }

            RunSessionDurableAcceptanceResult acceptance = null;
            acceptedEnd = runtime.Run.EndWithDurableAcceptance(
                endCommand,
                candidate =>
                {
                    string diagnostic;
                    if (!RewardClaimTransferPreparationFactory
                            .TryAcceptEndAndBuildPlan(
                                candidate,
                                awaiting,
                                graph,
                                rewardApplication,
                                out prepared,
                                out plan,
                                out diagnostic)
                        || prepared == null
                        || plan == null)
                    {
                        acceptance = RunSessionDurableAcceptanceResult.Terminal(
                            string.IsNullOrWhiteSpace(diagnostic)
                                ? "run-reward-completion-plan-rejected"
                                : diagnostic);
                        return acceptance;
                    }

                    RewardClaimTransferPersistenceResult saved =
                        persistence.PersistPreparedCustody(prepared);
                    if (saved == null)
                    {
                        acceptance = RunSessionDurableAcceptanceResult.Uncertain(
                            "run-reward-completion-prepared-save-null");
                    }
                    else if (saved.DurableStateUncertain)
                    {
                        acceptance = RunSessionDurableAcceptanceResult.Uncertain(
                            saved.Diagnostic);
                    }
                    else if (!saved.Succeeded)
                    {
                        acceptance = RunSessionDurableAcceptanceResult.Terminal(
                            saved.Diagnostic);
                    }
                    else
                    {
                        acceptance = RunSessionDurableAcceptanceResult.Accepted();
                    }
                    return acceptance;
                });

            if (acceptedEnd == null || !acceptedEnd.Succeeded)
            {
                LastDiagnostic = acceptedEnd == null
                    ? "run-reward-completion-end-null"
                    : acceptedEnd.RejectionCode;
                return false;
            }
            if (acceptedEnd.Receipt == null
                || acceptedEnd.Receipt.MissionResult == null
                || prepared == null
                || plan == null)
            {
                LastDiagnostic =
                    "run-reward-completion-end-receipt-incomplete";
                return false;
            }
            return true;
        }

        private bool TryApplyTransfer()
        {
            var authority = new RewardClaimAtomicState(
                graph,
                rewardApplication,
                preparedAuthority,
                receipts);
            var actions = new RewardClaimTransferActions(
                plan,
                authority,
                persistence);
            RewardClaimTransferResult result = actions.Apply();
            if (result == null)
            {
                LastDiagnostic = "run-reward-completion-transfer-null";
                return false;
            }

            RewardClaimResultsBridge.Clear();
            RewardClaimResultsBridge.Publish(prepared, result);
            bool succeeded = result.Status == RewardClaimTransferStatus.Applied
                || result.Status == RewardClaimTransferStatus.ExactReplay;
            if (!succeeded)
            {
                LastDiagnostic = result.Diagnostic;
                return false;
            }
            return true;
        }

        private ResultsContext BuildResultsContext(
            MissionResultPayload result)
        {
            latestResult = result
                ?? throw new ArgumentNullException(nameof(result));
            return new ResultsContext(
                latestResult,
                graph.StrongboxAuthority,
                CreateOpeningCommand,
                graph.LoadoutRuntime.EquipmentCatalog,
                graph.LoadoutRuntime.GunCatalog,
                delegate
                {
                    latestResult = runtime.RefreshMissionResult(latestResult);
                    return latestResult;
                });
        }

        private StrongboxOpenCommand CreateOpeningCommand(
            MissionRunStrongboxResult exactStrongbox)
        {
            if (exactStrongbox == null)
                throw new ArgumentNullException(nameof(exactStrongbox));
            StableId opening = StableId.Create(
                "opening",
                "results-strongbox-"
                + RunFingerprint.Hash(
                    runtime.RunStableId
                    + "|"
                    + exactStrongbox.Fingerprint)
                    .Substring(0, 32));
            return StrongboxOpenCommand.Create(
                opening,
                runtime.RunStableId,
                exactStrongbox.InstanceStableId,
                graph.Character.CharacterInstanceStableId,
                MoneyWalletIds.AuthorityStableId,
                graph.ScrapWallet.AuthorityStableId,
                graph.LoadoutRuntime.Holdings.AuthorityStableId,
                graph.StrongboxAuthority.Sequence);
        }
    }
}

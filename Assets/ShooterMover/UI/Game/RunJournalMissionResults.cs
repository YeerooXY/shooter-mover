using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Read-only RUN-001 projection over the exact RunSession pickup-collection journal.
    /// It never generates or grants rewards. Permanent holdings are updated later by the
    /// existing RewardClaim transfer after terminal acceptance.
    /// </summary>
    internal sealed class RunJournal : IMissionRunExistingStatePort
    {
        private readonly CharacterLiveGraph graph;
        private RunSessionAggregate run;

        public RunJournal(CharacterLiveGraph graph)
        {
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        public void BindRun(RunSessionAggregate run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (this.run != null && !ReferenceEquals(this.run, run))
            {
                throw new InvalidOperationException(
                    "The mission-result journal projection is already bound to another run.");
            }
            this.run = run;
        }

        public MissionRunCollectionVerification VerifyCollectedStrongbox(
            MissionRunCollectStrongboxCommand command)
        {
            if (command == null)
            {
                return MissionRunCollectionVerification.Reject(
                    "run-journal-strongbox-command-null");
            }
            RunSessionCollectedReward exact = FindStrongbox(
                command.RunStableId,
                command.InstanceStableId);
            if (exact == null)
            {
                return MissionRunCollectionVerification.Reject(
                    "run-journal-strongbox-not-collected");
            }
            if (exact.ContentStableId != command.DefinitionStableId
                || exact.GeneratedRewardChildStableId != command.GrantStableId
                || exact.DropOperationStableId != command.SourceStableId)
            {
                return MissionRunCollectionVerification.Reject(
                    "run-journal-strongbox-provenance-mismatch");
            }

            return MissionRunCollectionVerification.Accept(
                new MissionRunStrongboxCollection(
                    command.DefinitionStableId,
                    command.InstanceStableId,
                    command.GrantStableId,
                    command.SourceStableId,
                    command.OperationStableId,
                    graph.LoadoutRuntime.Holdings.Sequence,
                    graph.LoadoutRuntime.Holdings
                        .ExportSnapshot().Fingerprint));
        }

        public MissionRunStrongboxView ProjectStrongboxStates(
            EndMissionRunCommand command,
            IReadOnlyList<MissionRunStrongboxCollection> collectedStrongboxes)
        {
            if (command == null || collectedStrongboxes == null)
            {
                return MissionRunStrongboxView.Reject(
                    "run-journal-projection-input-null");
            }

            StrongboxOpeningSnapshot openings =
                graph.StrongboxAuthority.ExportSnapshot();
            List<MissionRunStrongboxResult> results;
            string projectionRejection;
            if (!TryProject(
                    command.RunStableId,
                    collectedStrongboxes,
                    openings,
                    out results,
                    out projectionRejection))
            {
                return MissionRunStrongboxView.Reject(projectionRejection);
            }

            PlayerHoldingsSnapshot holdings =
                graph.LoadoutRuntime.Holdings.ExportSnapshot();
            return MissionRunStrongboxView.Accept(
                results,
                graph.LoadoutRuntime.Holdings.Sequence,
                holdings.Fingerprint,
                openings.Sequence,
                openings.Fingerprint);
        }

        public MissionResultPayload Refresh(MissionResultPayload prior)
        {
            if (prior == null) throw new ArgumentNullException(nameof(prior));
            if (run == null || prior.RunStableId != run.RunStableId)
            {
                throw new InvalidOperationException(
                    "The Results refresh does not belong to the bound Run Session.");
            }

            var collections = new List<MissionRunStrongboxCollection>(
                prior.Strongboxes.Count);
            for (int index = 0; index < prior.Strongboxes.Count; index++)
            {
                collections.Add(prior.Strongboxes[index].Collection);
            }

            StrongboxOpeningSnapshot openings =
                graph.StrongboxAuthority.ExportSnapshot();
            List<MissionRunStrongboxResult> results;
            string rejection;
            if (!TryProject(
                    prior.RunStableId,
                    collections,
                    openings,
                    out results,
                    out rejection))
            {
                throw new InvalidOperationException(rejection);
            }

            PlayerHoldingsSnapshot holdings =
                graph.LoadoutRuntime.Holdings.ExportSnapshot();
            return MissionResultPayload.Create(
                prior.RunStableId,
                prior.RoutePayload,
                prior.CompletionState,
                results,
                checked(prior.RunSequence + 1L),
                graph.LoadoutRuntime.Holdings.Sequence,
                holdings.Fingerprint,
                openings.Sequence,
                openings.Fingerprint);
        }

        private bool TryProject(
            StableId runStableId,
            IReadOnlyList<MissionRunStrongboxCollection> collections,
            StrongboxOpeningSnapshot openings,
            out List<MissionRunStrongboxResult> results,
            out string rejection)
        {
            results = new List<MissionRunStrongboxResult>(collections.Count);
            rejection = string.Empty;
            var seen = new HashSet<StableId>();
            for (int index = 0; index < collections.Count; index++)
            {
                MissionRunStrongboxCollection collection = collections[index];
                if (collection == null
                    || !seen.Add(collection.InstanceStableId))
                {
                    rejection = "run-journal-projection-collection-invalid";
                    return false;
                }

                RunSessionCollectedReward exact = FindStrongbox(
                    runStableId,
                    collection.InstanceStableId);
                if (exact == null
                    || exact.ContentStableId != collection.DefinitionStableId
                    || exact.GeneratedRewardChildStableId
                        != collection.GrantStableId
                    || exact.DropOperationStableId != collection.SourceStableId)
                {
                    rejection =
                        "run-journal-projection-collection-mismatch:"
                        + collection.InstanceStableId;
                    return false;
                }

                StrongboxOpeningRecordSnapshot opened = FindOpened(
                    openings,
                    runStableId,
                    collection.InstanceStableId);
                results.Add(opened == null
                    ? new MissionRunStrongboxResult(
                        collection,
                        MissionRunStrongboxState.Unopened,
                        null,
                        null)
                    : new MissionRunStrongboxResult(
                        collection,
                        MissionRunStrongboxState.Opened,
                        opened.Command.OpeningStableId,
                        opened.TerminalFact.Fingerprint));
            }
            results.Sort();
            return true;
        }

        private RunSessionCollectedReward FindStrongbox(
            StableId runStableId,
            StableId instanceStableId)
        {
            if (run == null
                || runStableId == null
                || instanceStableId == null
                || run.RunStableId != runStableId)
            {
                return null;
            }

            IReadOnlyList<RunSessionCollectedReward> rewards =
                run.ExportRewardClaims();
            RunSessionCollectedReward found = null;
            for (int index = 0; index < rewards.Count; index++)
            {
                RunSessionCollectedReward reward = rewards[index];
                if (reward == null
                    || reward.RewardKind != RewardGrantKind.Strongbox
                    || reward.GeneratedRewardChildStableId
                        != instanceStableId)
                {
                    continue;
                }
                if (found != null)
                {
                    return null;
                }
                found = reward;
            }
            return found;
        }

        private static StrongboxOpeningRecordSnapshot FindOpened(
            StrongboxOpeningSnapshot snapshot,
            StableId runStableId,
            StableId instanceStableId)
        {
            if (snapshot == null) return null;
            StrongboxOpeningRecordSnapshot match = null;
            for (int index = 0; index < snapshot.Openings.Count; index++)
            {
                StrongboxOpeningRecordSnapshot record = snapshot.Openings[index];
                if (record == null
                    || record.Command.RunStableId != runStableId
                    || record.Command.StrongboxInstanceStableId
                        != instanceStableId
                    || record.Stage != StrongboxOpeningStage.Opened
                    || record.TerminalFact == null)
                {
                    continue;
                }
                if (match != null)
                {
                    throw new InvalidOperationException(
                        "More than one opened record exists for one exact strongbox instance.");
                }
                match = record;
            }
            return match;
        }
    }
}

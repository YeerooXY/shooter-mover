using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Results
{
    /// <summary>
    /// Sole RUN-001 authority. It records verified run collection facts and freezes one
    /// immutable terminal result. It never calls reward generation or any mutation API.
    /// </summary>
    public sealed class MissionRunResultState
    {
        private sealed class RunState
        {
            public RunState(PlayerRouteProfilePayload routePayload)
            {
                RoutePayload = routePayload;
                CollectionsByInstance = new Dictionary<StableId, MissionRunStrongboxCollection>();
            }

            public PlayerRouteProfilePayload RoutePayload;
            public Dictionary<StableId, MissionRunStrongboxCollection> CollectionsByInstance;
            public MissionRunPayload LatestPayload;
            public string TerminalIntentFingerprint;
            public MissionRunStateResult TerminalResult;
        }

        private readonly IMissionRunExistingStatePort existingAuthorities;
        private readonly Dictionary<StableId, RunState> runs = new Dictionary<StableId, RunState>();
        private readonly Dictionary<StableId, string> operationFingerprints = new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, MissionRunStateResult> operationResults =
            new Dictionary<StableId, MissionRunStateResult>();
        private long sequence;

        public MissionRunResultState(IMissionRunExistingStatePort existingAuthorities)
        {
            this.existingAuthorities = existingAuthorities
                ?? throw new ArgumentNullException(nameof(existingAuthorities));
        }

        public long Sequence { get { return sequence; } }

        public MissionRunStateResult RecordCollectedStrongbox(
            MissionRunCollectStrongboxCommand command)
        {
            if (command == null)
            {
                return Reject(
                    MissionRunStateStatus.InvalidRequest,
                    null,
                    string.Empty,
                    "run-collection-command-null");
            }

            MissionRunStateResult replay = ResolveOperationReplay(
                command.OperationStableId,
                command.Fingerprint);
            if (replay != null) return replay;

            if (command.ExpectedRunSequence != sequence)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunStateStatus.StaleInput,
                    "run-sequence-stale");
            }

            RunState state;
            if (runs.TryGetValue(command.RunStableId, out state))
            {
                if (state.TerminalResult != null)
                {
                    return RejectAndRemember(
                        command.OperationStableId,
                        command.Fingerprint,
                        MissionRunStateStatus.RunAlreadyEnded,
                        "run-already-ended");
                }
                if (!string.Equals(state.RoutePayload.Fingerprint, command.RoutePayload.Fingerprint, StringComparison.Ordinal))
                {
                    return RejectAndRemember(
                        command.OperationStableId,
                        command.Fingerprint,
                        MissionRunStateStatus.RouteMismatch,
                        "run-route-mismatch");
                }

                MissionRunStrongboxCollection existing;
                if (state.CollectionsByInstance.TryGetValue(command.InstanceStableId, out existing))
                {
                    bool exact = existing.DefinitionStableId == command.DefinitionStableId
                        && existing.GrantStableId == command.GrantStableId
                        && existing.SourceStableId == command.SourceStableId;
                    MissionRunStateResult duplicate = exact
                        ? new MissionRunStateResult(
                            MissionRunStateStatus.ExactDuplicateNoChange,
                            sequence,
                            sequence,
                            command.OperationStableId,
                            command.Fingerprint,
                            state.LatestPayload,
                            existing,
                            null,
                            string.Empty)
                        : new MissionRunStateResult(
                            MissionRunStateStatus.ConflictingDuplicate,
                            sequence,
                            sequence,
                            command.OperationStableId,
                            command.Fingerprint,
                            state.LatestPayload,
                            null,
                            null,
                            "run-strongbox-instance-conflict");
                    Remember(command.OperationStableId, command.Fingerprint, duplicate);
                    return duplicate;
                }
            }

            MissionRunCollectionVerification verification =
                existingAuthorities.VerifyCollectedStrongbox(command);
            if (verification == null || !verification.Accepted || verification.Collection == null)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunStateStatus.ExternalAuthorityRejected,
                    verification == null ? "run-collection-verification-null" : verification.RejectionCode);
            }

            MissionRunStrongboxCollection collection = verification.Collection;
            if (collection.InstanceStableId != command.InstanceStableId
                || collection.DefinitionStableId != command.DefinitionStableId
                || collection.GrantStableId != command.GrantStableId
                || collection.SourceStableId != command.SourceStableId
                || collection.CollectionOperationStableId != command.OperationStableId)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunStateStatus.ExternalAuthorityRejected,
                    "run-collection-verification-mismatch");
            }

            if (state == null)
            {
                state = new RunState(command.RoutePayload);
                runs.Add(command.RunStableId, state);
            }

            long previous = sequence;
            state.CollectionsByInstance.Add(collection.InstanceStableId, collection);
            sequence++;
            state.LatestPayload = MissionRunPayload.Create(
                command.RunStableId,
                state.RoutePayload,
                CopyCollections(state.CollectionsByInstance),
                sequence);
            MissionRunStateResult result = new MissionRunStateResult(
                MissionRunStateStatus.StrongboxCollected,
                previous,
                sequence,
                command.OperationStableId,
                command.Fingerprint,
                state.LatestPayload,
                collection,
                null,
                string.Empty);
            Remember(command.OperationStableId, command.Fingerprint, result);
            return result;
        }

        public MissionRunStateResult EndRun(EndMissionRunCommand command)
        {
            if (command == null)
            {
                return Reject(
                    MissionRunStateStatus.InvalidRequest,
                    null,
                    string.Empty,
                    "run-end-command-null");
            }

            MissionRunStateResult replay = ResolveOperationReplay(
                command.OperationStableId,
                command.Fingerprint);
            if (replay != null) return replay;

            RunState state;
            if (runs.TryGetValue(command.RunStableId, out state) && state.TerminalResult != null)
            {
                if (string.Equals(
                    state.TerminalIntentFingerprint,
                    command.IntentFingerprint,
                    StringComparison.Ordinal))
                {
                    Remember(command.OperationStableId, command.Fingerprint, state.TerminalResult);
                    return state.TerminalResult;
                }

                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunStateStatus.ConflictingDuplicate,
                    "run-end-conflicting-replay");
            }

            if (command.ExpectedRunSequence != sequence)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunStateStatus.StaleInput,
                    "run-sequence-stale");
            }

            if (state != null
                && !string.Equals(state.RoutePayload.Fingerprint, command.RoutePayload.Fingerprint, StringComparison.Ordinal))
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunStateStatus.RouteMismatch,
                    "run-route-mismatch");
            }

            IReadOnlyList<MissionRunStrongboxCollection> collections =
                state == null
                    ? (IReadOnlyList<MissionRunStrongboxCollection>)Array.Empty<MissionRunStrongboxCollection>()
                    : CopyCollections(state.CollectionsByInstance);
            MissionRunStrongboxView projection =
                existingAuthorities.ProjectStrongboxStates(command, collections);
            if (projection == null || !projection.Accepted)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunStateStatus.ExternalAuthorityRejected,
                    projection == null ? "run-strongbox-projection-null" : projection.RejectionCode);
            }

            if (projection.Strongboxes.Count != collections.Count)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunStateStatus.ExternalAuthorityRejected,
                    "run-strongbox-projection-count-mismatch");
            }

            HashSet<StableId> projectedIds = new HashSet<StableId>();
            for (int index = 0; index < projection.Strongboxes.Count; index++)
            {
                MissionRunStrongboxResult projected = projection.Strongboxes[index];
                if (projected == null || !projectedIds.Add(projected.InstanceStableId))
                {
                    return RejectAndRemember(
                        command.OperationStableId,
                        command.Fingerprint,
                        MissionRunStateStatus.ExternalAuthorityRejected,
                        "run-strongbox-projection-invalid");
                }
            }
            for (int index = 0; index < collections.Count; index++)
            {
                if (!projectedIds.Contains(collections[index].InstanceStableId))
                {
                    return RejectAndRemember(
                        command.OperationStableId,
                        command.Fingerprint,
                        MissionRunStateStatus.ExternalAuthorityRejected,
                        "run-strongbox-projection-missing-instance");
                }
            }

            long previous = sequence;
            MissionRunPayload runPayload = state == null || state.LatestPayload == null
                ? MissionRunPayload.Create(
                    command.RunStableId,
                    command.RoutePayload,
                    collections,
                    previous)
                : state.LatestPayload;
            long resultSequence = checked(sequence + 1L);
            MissionResultPayload payload = MissionResultPayload.Create(
                command.RunStableId,
                command.RoutePayload,
                command.CompletionState,
                projection.Strongboxes,
                resultSequence,
                projection.HoldingsSequence,
                projection.HoldingsFingerprint,
                projection.StrongboxOpeningSequence,
                projection.StrongboxOpeningFingerprint);
            MissionRunStateResult result = new MissionRunStateResult(
                MissionRunStateStatus.RunEnded,
                previous,
                resultSequence,
                command.OperationStableId,
                command.Fingerprint,
                runPayload,
                null,
                payload,
                string.Empty);

            if (state == null)
            {
                state = new RunState(command.RoutePayload);
                runs.Add(command.RunStableId, state);
            }
            state.LatestPayload = runPayload;
            sequence = resultSequence;
            state.TerminalIntentFingerprint = command.IntentFingerprint;
            state.TerminalResult = result;
            Remember(command.OperationStableId, command.Fingerprint, result);
            return result;
        }

        public bool TryGetRun(StableId runStableId, out MissionRunPayload runPayload)
        {
            RunState state;
            if (runStableId != null
                && runs.TryGetValue(runStableId, out state)
                && state.LatestPayload != null)
            {
                runPayload = state.LatestPayload;
                return true;
            }
            runPayload = null;
            return false;
        }

        public bool TryGetResult(StableId runStableId, out MissionResultPayload resultPayload)
        {
            RunState state;
            if (runStableId != null
                && runs.TryGetValue(runStableId, out state)
                && state.TerminalResult != null)
            {
                resultPayload = state.TerminalResult.ResultPayload;
                return true;
            }
            resultPayload = null;
            return false;
        }

        private MissionRunStateResult ResolveOperationReplay(
            StableId operationStableId,
            string requestFingerprint)
        {
            string existingFingerprint;
            if (!operationFingerprints.TryGetValue(operationStableId, out existingFingerprint))
            {
                return null;
            }
            if (string.Equals(existingFingerprint, requestFingerprint, StringComparison.Ordinal))
            {
                return operationResults[operationStableId];
            }
            return Reject(
                MissionRunStateStatus.ConflictingDuplicate,
                operationStableId,
                requestFingerprint,
                "run-operation-conflicting-reuse");
        }

        private void Remember(
            StableId operationStableId,
            string requestFingerprint,
            MissionRunStateResult result)
        {
            operationFingerprints[operationStableId] = requestFingerprint;
            operationResults[operationStableId] = result;
        }

        private MissionRunStateResult RejectAndRemember(
            StableId operationStableId,
            string requestFingerprint,
            MissionRunStateStatus status,
            string rejectionCode)
        {
            MissionRunStateResult result = Reject(
                status,
                operationStableId,
                requestFingerprint,
                rejectionCode);
            Remember(operationStableId, requestFingerprint, result);
            return result;
        }

        private MissionRunStateResult Reject(
            MissionRunStateStatus status,
            StableId operationStableId,
            string requestFingerprint,
            string rejectionCode)
        {
            return new MissionRunStateResult(
                status,
                sequence,
                sequence,
                operationStableId,
                requestFingerprint,
                null,
                null,
                null,
                rejectionCode);
        }

        private static IReadOnlyList<MissionRunStrongboxCollection> CopyCollections(
            Dictionary<StableId, MissionRunStrongboxCollection> source)
        {
            List<MissionRunStrongboxCollection> values =
                new List<MissionRunStrongboxCollection>(source.Values);
            values.Sort();
            return new ReadOnlyCollection<MissionRunStrongboxCollection>(values);
        }
    }
}

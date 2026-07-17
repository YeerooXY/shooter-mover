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
    public sealed class MissionRunResultAuthorityV1
    {
        private sealed class RunState
        {
            public RunState(PlayerRouteProfilePayloadV1 routePayload)
            {
                RoutePayload = routePayload;
                CollectionsByInstance = new Dictionary<StableId, MissionRunStrongboxCollectionV1>();
            }

            public PlayerRouteProfilePayloadV1 RoutePayload;
            public Dictionary<StableId, MissionRunStrongboxCollectionV1> CollectionsByInstance;
            public MissionRunPayloadV1 LatestPayload;
            public string TerminalIntentFingerprint;
            public MissionRunAuthorityResultV1 TerminalResult;
        }

        private readonly IMissionRunExistingAuthorityPortV1 existingAuthorities;
        private readonly Dictionary<StableId, RunState> runs = new Dictionary<StableId, RunState>();
        private readonly Dictionary<StableId, string> operationFingerprints = new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, MissionRunAuthorityResultV1> operationResults =
            new Dictionary<StableId, MissionRunAuthorityResultV1>();
        private long sequence;

        public MissionRunResultAuthorityV1(IMissionRunExistingAuthorityPortV1 existingAuthorities)
        {
            this.existingAuthorities = existingAuthorities
                ?? throw new ArgumentNullException(nameof(existingAuthorities));
        }

        public long Sequence { get { return sequence; } }

        public MissionRunAuthorityResultV1 RecordCollectedStrongbox(
            MissionRunCollectStrongboxCommandV1 command)
        {
            if (command == null)
            {
                return Reject(
                    MissionRunAuthorityStatusV1.InvalidRequest,
                    null,
                    string.Empty,
                    "run-collection-command-null");
            }

            MissionRunAuthorityResultV1 replay = ResolveOperationReplay(
                command.OperationStableId,
                command.Fingerprint);
            if (replay != null) return replay;

            if (command.ExpectedRunSequence != sequence)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunAuthorityStatusV1.StaleInput,
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
                        MissionRunAuthorityStatusV1.RunAlreadyEnded,
                        "run-already-ended");
                }
                if (!string.Equals(state.RoutePayload.Fingerprint, command.RoutePayload.Fingerprint, StringComparison.Ordinal))
                {
                    return RejectAndRemember(
                        command.OperationStableId,
                        command.Fingerprint,
                        MissionRunAuthorityStatusV1.RouteMismatch,
                        "run-route-mismatch");
                }

                MissionRunStrongboxCollectionV1 existing;
                if (state.CollectionsByInstance.TryGetValue(command.InstanceStableId, out existing))
                {
                    bool exact = existing.DefinitionStableId == command.DefinitionStableId
                        && existing.GrantStableId == command.GrantStableId
                        && existing.SourceStableId == command.SourceStableId;
                    MissionRunAuthorityResultV1 duplicate = exact
                        ? new MissionRunAuthorityResultV1(
                            MissionRunAuthorityStatusV1.ExactDuplicateNoChange,
                            sequence,
                            sequence,
                            command.OperationStableId,
                            command.Fingerprint,
                            state.LatestPayload,
                            existing,
                            null,
                            string.Empty)
                        : new MissionRunAuthorityResultV1(
                            MissionRunAuthorityStatusV1.ConflictingDuplicate,
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

            MissionRunCollectionVerificationV1 verification =
                existingAuthorities.VerifyCollectedStrongbox(command);
            if (verification == null || !verification.Accepted || verification.Collection == null)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunAuthorityStatusV1.ExternalAuthorityRejected,
                    verification == null ? "run-collection-verification-null" : verification.RejectionCode);
            }

            MissionRunStrongboxCollectionV1 collection = verification.Collection;
            if (collection.InstanceStableId != command.InstanceStableId
                || collection.DefinitionStableId != command.DefinitionStableId
                || collection.GrantStableId != command.GrantStableId
                || collection.SourceStableId != command.SourceStableId
                || collection.CollectionOperationStableId != command.OperationStableId)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunAuthorityStatusV1.ExternalAuthorityRejected,
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
            state.LatestPayload = MissionRunPayloadV1.Create(
                command.RunStableId,
                state.RoutePayload,
                CopyCollections(state.CollectionsByInstance),
                sequence);
            MissionRunAuthorityResultV1 result = new MissionRunAuthorityResultV1(
                MissionRunAuthorityStatusV1.StrongboxCollected,
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

        public MissionRunAuthorityResultV1 EndRun(EndMissionRunCommandV1 command)
        {
            if (command == null)
            {
                return Reject(
                    MissionRunAuthorityStatusV1.InvalidRequest,
                    null,
                    string.Empty,
                    "run-end-command-null");
            }

            MissionRunAuthorityResultV1 replay = ResolveOperationReplay(
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
                    MissionRunAuthorityStatusV1.ConflictingDuplicate,
                    "run-end-conflicting-replay");
            }

            if (command.ExpectedRunSequence != sequence)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunAuthorityStatusV1.StaleInput,
                    "run-sequence-stale");
            }

            if (state != null
                && !string.Equals(state.RoutePayload.Fingerprint, command.RoutePayload.Fingerprint, StringComparison.Ordinal))
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunAuthorityStatusV1.RouteMismatch,
                    "run-route-mismatch");
            }

            IReadOnlyList<MissionRunStrongboxCollectionV1> collections =
                state == null
                    ? (IReadOnlyList<MissionRunStrongboxCollectionV1>)Array.Empty<MissionRunStrongboxCollectionV1>()
                    : CopyCollections(state.CollectionsByInstance);
            MissionRunStrongboxProjectionV1 projection =
                existingAuthorities.ProjectStrongboxStates(command, collections);
            if (projection == null || !projection.Accepted)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunAuthorityStatusV1.ExternalAuthorityRejected,
                    projection == null ? "run-strongbox-projection-null" : projection.RejectionCode);
            }

            if (projection.Strongboxes.Count != collections.Count)
            {
                return RejectAndRemember(
                    command.OperationStableId,
                    command.Fingerprint,
                    MissionRunAuthorityStatusV1.ExternalAuthorityRejected,
                    "run-strongbox-projection-count-mismatch");
            }

            HashSet<StableId> projectedIds = new HashSet<StableId>();
            for (int index = 0; index < projection.Strongboxes.Count; index++)
            {
                MissionRunStrongboxResultV1 projected = projection.Strongboxes[index];
                if (projected == null || !projectedIds.Add(projected.InstanceStableId))
                {
                    return RejectAndRemember(
                        command.OperationStableId,
                        command.Fingerprint,
                        MissionRunAuthorityStatusV1.ExternalAuthorityRejected,
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
                        MissionRunAuthorityStatusV1.ExternalAuthorityRejected,
                        "run-strongbox-projection-missing-instance");
                }
            }

            long previous = sequence;
            MissionRunPayloadV1 runPayload = state == null || state.LatestPayload == null
                ? MissionRunPayloadV1.Create(
                    command.RunStableId,
                    command.RoutePayload,
                    collections,
                    previous)
                : state.LatestPayload;
            long resultSequence = checked(sequence + 1L);
            MissionResultPayloadV1 payload = MissionResultPayloadV1.Create(
                command.RunStableId,
                command.RoutePayload,
                command.CompletionState,
                projection.Strongboxes,
                resultSequence,
                projection.HoldingsSequence,
                projection.HoldingsFingerprint,
                projection.StrongboxOpeningSequence,
                projection.StrongboxOpeningFingerprint);
            MissionRunAuthorityResultV1 result = new MissionRunAuthorityResultV1(
                MissionRunAuthorityStatusV1.RunEnded,
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

        public bool TryGetRun(StableId runStableId, out MissionRunPayloadV1 runPayload)
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

        public bool TryGetResult(StableId runStableId, out MissionResultPayloadV1 resultPayload)
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

        private MissionRunAuthorityResultV1 ResolveOperationReplay(
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
                MissionRunAuthorityStatusV1.ConflictingDuplicate,
                operationStableId,
                requestFingerprint,
                "run-operation-conflicting-reuse");
        }

        private void Remember(
            StableId operationStableId,
            string requestFingerprint,
            MissionRunAuthorityResultV1 result)
        {
            operationFingerprints[operationStableId] = requestFingerprint;
            operationResults[operationStableId] = result;
        }

        private MissionRunAuthorityResultV1 RejectAndRemember(
            StableId operationStableId,
            string requestFingerprint,
            MissionRunAuthorityStatusV1 status,
            string rejectionCode)
        {
            MissionRunAuthorityResultV1 result = Reject(
                status,
                operationStableId,
                requestFingerprint,
                rejectionCode);
            Remember(operationStableId, requestFingerprint, result);
            return result;
        }

        private MissionRunAuthorityResultV1 Reject(
            MissionRunAuthorityStatusV1 status,
            StableId operationStableId,
            string requestFingerprint,
            string rejectionCode)
        {
            return new MissionRunAuthorityResultV1(
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

        private static IReadOnlyList<MissionRunStrongboxCollectionV1> CopyCollections(
            Dictionary<StableId, MissionRunStrongboxCollectionV1> source)
        {
            List<MissionRunStrongboxCollectionV1> values =
                new List<MissionRunStrongboxCollectionV1>(source.Values);
            values.Sort();
            return new ReadOnlyCollection<MissionRunStrongboxCollectionV1>(values);
        }
    }
}

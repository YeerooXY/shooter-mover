using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShooterMover.Application.Modifiers;
using ShooterMover.Application.Modifiers.StatusEffects;
using ShooterMover.Domain.Modifiers.StatusEffects;

namespace ShooterMover.ConditionRuntime
{
    public sealed class ConditionLiveState
    {
        private sealed class ParticipantLive
        {
            public ParticipantLive(
                ConditionLiveParticipantDefinition definition)
            {
                Definition = definition
                    ?? throw new ArgumentNullException(nameof(definition));
                Conditions = new FactWindowConditionState(
                    definition.ParticipantId.ToString(),
                    definition.RuntimeDefinition.Conditions);
                StatusEffects = new StatusEffectState(
                    definition.ParticipantId.ToString(),
                    checked((int)definition.ActorLifecycleGeneration),
                    definition.RuntimeDefinition.StatusEffects);
                Bridge = new FactWindowStatusEffectBridge(
                    definition.RuntimeDefinition.Bindings);
            }

            public ConditionLiveParticipantDefinition Definition { get; }
            public FactWindowConditionState Conditions { get; }
            public StatusEffectState StatusEffects { get; }
            public FactWindowStatusEffectBridge Bridge { get; }
        }

        private sealed class DeliveryReplayRecord
        {
            public DeliveryReplayRecord(
                string fingerprint,
                ConditionFactIngestionResult result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public ConditionFactIngestionResult Result { get; }
        }

        private sealed class AdvanceReplayRecord
        {
            public AdvanceReplayRecord(
                string fingerprint,
                ConditionLiveSnapshot snapshot)
            {
                Fingerprint = fingerprint;
                Snapshot = snapshot;
            }

            public string Fingerprint { get; }
            public ConditionLiveSnapshot Snapshot { get; }
        }

        private sealed class ReconstructionReplayRecord
        {
            public ReconstructionReplayRecord(
                string fingerprint,
                ConditionRunReconstructionResult result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public ConditionRunReconstructionResult Result { get; }
        }

        private readonly IConditionRunClock clock;
        private readonly IConditionRunLifecycle lifecycle;
        private readonly AcceptedGameplayFactBridgeRegistry adapters;
        private readonly Dictionary<string, DeliveryReplayRecord> deliveryReplay =
            new Dictionary<string, DeliveryReplayRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConditionObservedGameplayFact>
            acceptedBySourceFact = new Dictionary<
                string,
                ConditionObservedGameplayFact>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> acceptedSourceFingerprints =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConditionFactIngestionResult>
            acceptedResultBySourceFact = new Dictionary<
                string,
                ConditionFactIngestionResult>(StringComparer.Ordinal);
        private readonly Dictionary<string, AdvanceReplayRecord> advanceReplay =
            new Dictionary<string, AdvanceReplayRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReconstructionReplayRecord>
            reconstructionReplay = new Dictionary<
                string,
                ReconstructionReplayRecord>(StringComparer.Ordinal);
        private Dictionary<string, ParticipantLive> participants;
        private ConditionRunDefinition definition;

        public ConditionLiveState(
            IConditionRunClock clock,
            IConditionRunLifecycle lifecycle,
            AcceptedGameplayFactBridgeRegistry adapters,
            ConditionRunDefinition definition)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.lifecycle = lifecycle
                ?? throw new ArgumentNullException(nameof(lifecycle));
            this.adapters = adapters
                ?? throw new ArgumentNullException(nameof(adapters));
            this.definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            if (clock.CurrentTick < 0L)
                throw new ArgumentException(
                    "The authoritative run tick cannot be negative.",
                    nameof(clock));
            if (!MatchesLifecycle(definition.Lifecycle, lifecycle.Current))
                throw new ArgumentException(
                    "The condition runtime definition must match the current run lifecycle.",
                    nameof(definition));
            participants = BuildParticipants(definition);
        }

        public ConditionLiveSnapshot Snapshot
        {
            get { return BuildSnapshot(); }
        }

        public ConditionFactIngestionResult Ingest(
            AcceptedGameplayFactDelivery delivery)
        {
            if (delivery == null)
                return Rejected(
                    "condition-fact-delivery-null",
                    null,
                    null);

            string genericSourceFingerprint =
                ConditionSourceFactFingerprint.Compute(delivery.SourceFact);
            IAcceptedGameplayFactBridge adapter;
            if (!adapters.TryResolve(delivery.SourceFact.GetType(), out adapter))
            {
                return ResolveOrStoreRejectedDelivery(
                    delivery.DeliveryOperationId,
                    DeliveryReplayFingerprint(
                        delivery,
                        genericSourceFingerprint,
                        "unsupported|"
                        + (delivery.SourceFact.GetType().FullName
                            ?? delivery.SourceFact.GetType().Name)),
                    "condition-fact-type-unsupported",
                    null);
            }

            IAcceptedGameplayFactSourceFingerprint fingerprintAdapter =
                adapter as IAcceptedGameplayFactSourceFingerprint;
            if (fingerprintAdapter == null)
            {
                return ResolveOrStoreRejectedDelivery(
                    delivery.DeliveryOperationId,
                    DeliveryReplayFingerprint(
                        delivery,
                        genericSourceFingerprint,
                        "adapter-fingerprint-missing|"
                        + adapter.SourceFactTypeId),
                    "condition-fact-adapter-source-fingerprint-missing",
                    null);
            }
            string sourceFactFingerprint =
                fingerprintAdapter.ComputeSourceFactFingerprint(
                    delivery.SourceFact);
            if (string.IsNullOrWhiteSpace(sourceFactFingerprint))
            {
                return ResolveOrStoreRejectedDelivery(
                    delivery.DeliveryOperationId,
                    DeliveryReplayFingerprint(
                        delivery,
                        genericSourceFingerprint,
                        "adapter-fingerprint-invalid|"
                        + adapter.SourceFactTypeId),
                    "condition-fact-adapter-source-fingerprint-invalid",
                    null);
            }
            sourceFactFingerprint = sourceFactFingerprint.Trim();

            ConditionObservedGameplayFact observed;
            string adapterDiagnostic;
            if (!adapter.TryAdapt(
                delivery,
                out observed,
                out adapterDiagnostic))
            {
                return ResolveOrStoreRejectedDelivery(
                    delivery.DeliveryOperationId,
                    DeliveryReplayFingerprint(
                        delivery,
                        sourceFactFingerprint,
                        "adapter-rejected|" + adapter.SourceFactTypeId + "|"
                        + (adapterDiagnostic ?? string.Empty)),
                    string.IsNullOrWhiteSpace(adapterDiagnostic)
                        ? "condition-fact-adapter-rejected"
                        : adapterDiagnostic,
                    observed);
            }

            string deliveryFingerprint = DeliveryReplayFingerprint(
                delivery,
                sourceFactFingerprint,
                "accepted|" + adapter.SourceFactTypeId + "|"
                + observed.Fingerprint);
            DeliveryReplayRecord priorDelivery;
            if (deliveryReplay.TryGetValue(
                delivery.DeliveryOperationId,
                out priorDelivery))
            {
                if (!string.Equals(
                    priorDelivery.Fingerprint,
                    deliveryFingerprint,
                    StringComparison.Ordinal))
                {
                    return Conflict(
                        "condition-delivery-operation-conflicting-duplicate",
                        observed,
                        priorDelivery.Result.Snapshot);
                }
                return Duplicate(priorDelivery.Result);
            }

            ConditionFactIngestionResult validationFailure =
                ValidateObserved(observed);
            if (validationFailure != null)
            {
                StoreDeliveryReplay(
                    delivery.DeliveryOperationId,
                    deliveryFingerprint,
                    validationFailure);
                return validationFailure;
            }

            ConditionObservedGameplayFact priorSource;
            if (acceptedBySourceFact.TryGetValue(
                observed.SourceFactId,
                out priorSource))
            {
                ConditionFactIngestionResult priorSourceResult =
                    acceptedResultBySourceFact[observed.SourceFactId];
                ConditionFactIngestionResult duplicate;
                if (!string.Equals(
                        acceptedSourceFingerprints[observed.SourceFactId],
                        sourceFactFingerprint,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        priorSource.Fingerprint,
                        observed.Fingerprint,
                        StringComparison.Ordinal))
                {
                    duplicate = Conflict(
                        "condition-source-fact-conflicting-duplicate",
                        observed,
                        priorSourceResult.Snapshot);
                }
                else
                {
                    duplicate = Duplicate(priorSourceResult);
                }
                StoreDeliveryReplay(
                    delivery.DeliveryOperationId,
                    deliveryFingerprint,
                    duplicate);
                return duplicate;
            }

            ParticipantLive participant =
                participants[observed.SubjectParticipantId.ToString()];
            LiveObservedFactResult conditionResult =
                participant.Conditions.Apply(observed.ToObservedFact());
            if (conditionResult.Status != LiveObservedFactStatus.Applied)
            {
                var rejected = new ConditionFactIngestionResult(
                    ConditionFactIngestionStatus.Rejected,
                    string.IsNullOrEmpty(conditionResult.RejectionCode)
                        ? "condition-fact-window-rejected"
                        : conditionResult.RejectionCode,
                    observed,
                    conditionResult,
                    null,
                    BuildSnapshot());
                StoreDeliveryReplay(
                    delivery.DeliveryOperationId,
                    deliveryFingerprint,
                    rejected);
                return rejected;
            }

            var effectResults = new List<StatusEffectCommandResult>();
            foreach (LiveConditionActivationFact activation in
                conditionResult.Activations)
            {
                ApplyStatusEffectCommand command;
                string effectOperationId = "condition-effect:"
                    + observed.SourceFactId + ":" + activation.ConditionId + ":"
                    + activation.Fingerprint;
                if (!participant.Bridge.TryCreateApplyCommand(
                    activation,
                    effectOperationId,
                    checked((int)participant.Definition.ActorLifecycleGeneration),
                    out command))
                {
                    var bridgeRejected = new ConditionFactIngestionResult(
                        ConditionFactIngestionStatus.Rejected,
                        "condition-effect-binding-missing",
                        observed,
                        conditionResult,
                        effectResults,
                        BuildSnapshot());
                    StoreDeliveryReplay(
                        delivery.DeliveryOperationId,
                        deliveryFingerprint,
                        bridgeRejected);
                    return bridgeRejected;
                }

                StatusEffectCommandResult effectResult =
                    participant.StatusEffects.Apply(command);
                effectResults.Add(effectResult);
                if (!effectResult.IsAccepted)
                {
                    var effectRejected = new ConditionFactIngestionResult(
                        ConditionFactIngestionStatus.Rejected,
                        string.IsNullOrEmpty(effectResult.RejectionCode)
                            ? "condition-status-effect-rejected"
                            : effectResult.RejectionCode,
                        observed,
                        conditionResult,
                        effectResults,
                        BuildSnapshot());
                    StoreDeliveryReplay(
                        delivery.DeliveryOperationId,
                        deliveryFingerprint,
                        effectRejected);
                    return effectRejected;
                }
            }

            acceptedBySourceFact.Add(observed.SourceFactId, observed);
            acceptedSourceFingerprints.Add(
                observed.SourceFactId,
                sourceFactFingerprint);
            var applied = new ConditionFactIngestionResult(
                ConditionFactIngestionStatus.Applied,
                string.Empty,
                observed,
                conditionResult,
                effectResults,
                BuildSnapshot());
            acceptedResultBySourceFact.Add(observed.SourceFactId, applied);
            StoreDeliveryReplay(
                delivery.DeliveryOperationId,
                deliveryFingerprint,
                applied);
            return applied;
        }

        public ConditionLiveSnapshot Advance(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException(
                    "A condition-runtime advance operation identity is required.",
                    nameof(operationId));
            EnsureLifecycleCurrent();
            long tick = clock.CurrentTick;
            if (tick < 0L)
                throw new InvalidOperationException(
                    "The authoritative run tick cannot be negative.");

            string normalizedOperationId = operationId.Trim();
            string fingerprint = ConditionLiveHash.Hash(
                normalizedOperationId + "|"
                + tick.ToString(CultureInfo.InvariantCulture));
            AdvanceReplayRecord prior;
            if (advanceReplay.TryGetValue(normalizedOperationId, out prior))
            {
                if (!string.Equals(
                    prior.Fingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A condition-runtime advance operation was reused with conflicting facts.");
                }
                return prior.Snapshot;
            }

            List<ParticipantLive> ordered = participants.Values
                .OrderBy(
                    item => item.Definition.ParticipantId.ToString(),
                    StringComparer.Ordinal)
                .ToList();
            PrevalidateAdvance(ordered, tick);

            foreach (ParticipantLive participant in ordered)
            {
                StatusEffectCommandResult result =
                    participant.StatusEffects.Advance(
                        new AdvanceStatusEffectTickCommand(
                            normalizedOperationId + ":"
                            + participant.Definition.ParticipantId,
                            participant.Definition.ParticipantId.ToString(),
                            checked((int)participant.Definition
                                .ActorLifecycleGeneration),
                            tick));
                if (!result.IsAccepted)
                {
                    throw new InvalidOperationException(
                        "condition-runtime-advance-downstream-rejected:"
                        + (string.IsNullOrEmpty(result.RejectionCode)
                            ? "unknown"
                            : result.RejectionCode));
                }
            }

            ConditionLiveSnapshot snapshot = BuildSnapshot();
            advanceReplay.Add(
                normalizedOperationId,
                new AdvanceReplayRecord(fingerprint, snapshot));
            return snapshot;
        }

        public ConditionRunReconstructionResult Reconstruct(
            ConditionRunReconstructionCommand command)
        {
            if (command == null)
            {
                return new ConditionRunReconstructionResult(
                    ConditionFactIngestionStatus.Rejected,
                    "condition-run-reconstruction-null",
                    BuildSnapshot());
            }

            ReconstructionReplayRecord replay;
            if (reconstructionReplay.TryGetValue(command.OperationId, out replay))
            {
                if (!string.Equals(
                    replay.Fingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return new ConditionRunReconstructionResult(
                        ConditionFactIngestionStatus.ConflictingDuplicate,
                        "condition-run-reconstruction-conflicting-duplicate",
                        replay.Result.Snapshot);
                }
                return new ConditionRunReconstructionResult(
                    ConditionFactIngestionStatus.ExactDuplicateNoChange,
                    replay.Result.DiagnosticCode,
                    replay.Result.Snapshot);
            }

            ConditionRunReconstructionResult result;
            if (!ConditionLiveHash.SameId(
                    command.ExpectedRunId,
                    definition.Lifecycle.RunId)
                || command.ExpectedRunGeneration
                    != definition.Lifecycle.Generation)
            {
                result = new ConditionRunReconstructionResult(
                    ConditionFactIngestionStatus.Rejected,
                    "condition-run-reconstruction-current-mismatch",
                    BuildSnapshot());
            }
            else if (!MatchesLifecycle(
                command.NextRun.Lifecycle,
                lifecycle.Current))
            {
                result = new ConditionRunReconstructionResult(
                    ConditionFactIngestionStatus.Rejected,
                    "condition-run-reconstruction-lifecycle-port-mismatch",
                    BuildSnapshot());
            }
            else
            {
                definition = command.NextRun;
                participants = BuildParticipants(definition);
                acceptedBySourceFact.Clear();
                acceptedSourceFingerprints.Clear();
                acceptedResultBySourceFact.Clear();
                deliveryReplay.Clear();
                advanceReplay.Clear();
                result = new ConditionRunReconstructionResult(
                    ConditionFactIngestionStatus.Applied,
                    string.Empty,
                    BuildSnapshot());
            }

            reconstructionReplay.Add(
                command.OperationId,
                new ReconstructionReplayRecord(command.Fingerprint, result));
            return result;
        }

        private ConditionFactIngestionResult ValidateObserved(
            ConditionObservedGameplayFact observed)
        {
            if (!MatchesLifecycle(definition.Lifecycle, lifecycle.Current))
                return Rejected(
                    "condition-run-lifecycle-not-reconstructed",
                    observed,
                    null);
            if (!ConditionLiveHash.SameId(
                observed.RunId,
                definition.Lifecycle.RunId))
                return Rejected(
                    "condition-fact-run-mismatch",
                    observed,
                    null);
            if (observed.RunLifecycleGeneration
                != definition.Lifecycle.Generation)
                return Rejected(
                    "condition-fact-run-lifecycle-stale",
                    observed,
                    null);
            if (observed.AuthoritativeTick > clock.CurrentTick)
                return Rejected(
                    "condition-fact-tick-future",
                    observed,
                    null);

            ParticipantLive participant;
            if (!participants.TryGetValue(
                observed.SubjectParticipantId.ToString(),
                out participant))
                return Rejected(
                    "condition-fact-participant-unknown",
                    observed,
                    null);
            if (!ConditionLiveHash.SameId(
                observed.SourceCharacterId,
                participant.Definition.CharacterId))
                return Rejected(
                    "condition-fact-source-character-mismatch",
                    observed,
                    null);
            if (!ConditionLiveHash.SameId(
                observed.SourceActorId,
                participant.Definition.ActorId))
                return Rejected(
                    "condition-fact-source-actor-mismatch",
                    observed,
                    null);
            if (observed.AuthoritativeTick
                    < participant.Conditions.LatestAcceptedTick
                || observed.AuthoritativeTick
                    < participant.StatusEffects.LatestAcceptedTick)
                return Rejected(
                    "condition-fact-tick-stale",
                    observed,
                    null);
            if (observed.SourceActorLifecycleGeneration
                != participant.Definition.ActorLifecycleGeneration)
                return Rejected(
                    "condition-fact-source-lifecycle-stale",
                    observed,
                    null);
            return null;
        }

        private static string DeliveryReplayFingerprint(
            AcceptedGameplayFactDelivery delivery,
            string sourceFactFingerprint,
            string adaptationState)
        {
            return ConditionLiveHash.Hash(
                delivery.DeliveryOperationId + "|"
                + (delivery.SourceFact.GetType().FullName
                    ?? delivery.SourceFact.GetType().Name) + "|"
                + sourceFactFingerprint + "|" + delivery.RunId + "|"
                + delivery.RunLifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture) + "|"
                + delivery.SourceActorId + "|"
                + delivery.SubjectParticipantId + "|"
                + delivery.SourceCharacterId + "|"
                + delivery.SourceActorLifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture) + "|"
                + delivery.AuthoritativeTick.ToString(
                    CultureInfo.InvariantCulture) + "|"
                + (adaptationState ?? string.Empty));
        }

        private ConditionFactIngestionResult ResolveOrStoreRejectedDelivery(
            string operationId,
            string fingerprint,
            string diagnostic,
            ConditionObservedGameplayFact observed)
        {
            DeliveryReplayRecord prior;
            if (deliveryReplay.TryGetValue(operationId, out prior))
            {
                if (!string.Equals(
                    prior.Fingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
                {
                    return Conflict(
                        "condition-delivery-operation-conflicting-duplicate",
                        observed,
                        prior.Result.Snapshot);
                }
                return Duplicate(prior.Result);
            }

            ConditionFactIngestionResult result = Rejected(
                diagnostic,
                observed,
                null);
            StoreDeliveryReplay(operationId, fingerprint, result);
            return result;
        }

        private void PrevalidateAdvance(
            IEnumerable<ParticipantLive> orderedParticipants,
            long tick)
        {
            foreach (ParticipantLive participant in orderedParticipants)
            {
                string participantId =
                    participant.Definition.ParticipantId.ToString();
                if (!string.Equals(
                    participant.StatusEffects.SubjectId,
                    participantId,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "condition-runtime-advance-subject-mismatch");
                }
                if (participant.StatusEffects.LifecycleGeneration
                    != checked((int)participant.Definition
                        .ActorLifecycleGeneration))
                {
                    throw new InvalidOperationException(
                        "condition-runtime-advance-lifecycle-mismatch");
                }
                if (tick < participant.Conditions.LatestAcceptedTick
                    || tick < participant.StatusEffects.LatestAcceptedTick)
                {
                    throw new InvalidOperationException(
                        "condition-runtime-advance-tick-stale:" + participantId);
                }
            }
        }

        private ConditionFactIngestionResult Rejected(
            string diagnostic,
            ConditionObservedGameplayFact observed,
            LiveObservedFactResult conditionResult)
        {
            return new ConditionFactIngestionResult(
                ConditionFactIngestionStatus.Rejected,
                diagnostic,
                observed,
                conditionResult,
                null,
                BuildSnapshot());
        }

        private static ConditionFactIngestionResult Conflict(
            string diagnostic,
            ConditionObservedGameplayFact observed,
            ConditionLiveSnapshot stableSnapshot)
        {
            return new ConditionFactIngestionResult(
                ConditionFactIngestionStatus.ConflictingDuplicate,
                diagnostic,
                observed,
                null,
                null,
                stableSnapshot);
        }

        private static ConditionFactIngestionResult Duplicate(
            ConditionFactIngestionResult prior)
        {
            return new ConditionFactIngestionResult(
                ConditionFactIngestionStatus.ExactDuplicateNoChange,
                prior.DiagnosticCode,
                prior.ObservedFact,
                prior.ConditionResult,
                prior.EffectResults,
                prior.Snapshot);
        }

        private void StoreDeliveryReplay(
            string operationId,
            string fingerprint,
            ConditionFactIngestionResult result)
        {
            deliveryReplay.Add(
                operationId,
                new DeliveryReplayRecord(fingerprint, result));
        }

        private ConditionLiveSnapshot BuildSnapshot()
        {
            long tick = clock.CurrentTick;
            if (tick < 0L)
                throw new InvalidOperationException(
                    "The authoritative run tick cannot be negative.");
            IEnumerable<ConditionParticipantSnapshot> snapshots =
                participants.Values.Select(
                    participant => new ConditionParticipantSnapshot(
                        participant.Definition,
                        participant.Conditions.LatestAcceptedTick,
                        participant.Conditions.ActiveConditionIdsAt(tick),
                        participant.StatusEffects.Snapshot));
            return new ConditionLiveSnapshot(
                definition,
                tick,
                snapshots,
                acceptedBySourceFact.Values);
        }

        private void EnsureLifecycleCurrent()
        {
            if (!MatchesLifecycle(definition.Lifecycle, lifecycle.Current))
                throw new InvalidOperationException(
                    "The condition runtime must be reconstructed for the current run lifecycle.");
        }

        private static Dictionary<string, ParticipantLive> BuildParticipants(
            ConditionRunDefinition runDefinition)
        {
            return runDefinition.Participants.ToDictionary(
                item => item.ParticipantId.ToString(),
                item => new ParticipantLive(item),
                StringComparer.Ordinal);
        }

        private static bool MatchesLifecycle(
            ConditionRunLifecycleSnapshot expected,
            ConditionRunLifecycleSnapshot actual)
        {
            return expected != null && actual != null
                && ConditionLiveHash.SameId(expected.RunId, actual.RunId)
                && expected.Generation == actual.Generation;
        }
    }
}

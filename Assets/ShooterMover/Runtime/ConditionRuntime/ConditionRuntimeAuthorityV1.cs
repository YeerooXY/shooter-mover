using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShooterMover.Application.Modifiers;
using ShooterMover.Application.Modifiers.StatusEffects;
using ShooterMover.Domain.Modifiers.StatusEffects;

namespace ShooterMover.ConditionRuntime
{
    public sealed class ConditionRuntimeAuthorityV1
    {
        private sealed class ParticipantRuntime
        {
            public ParticipantRuntime(
                ConditionRuntimeParticipantDefinitionV1 definition)
            {
                Definition = definition
                    ?? throw new ArgumentNullException(nameof(definition));
                Conditions = new FactWindowConditionAuthorityV1(
                    definition.ParticipantId.ToString(),
                    definition.RuntimeDefinition.Conditions);
                StatusEffects = new StatusEffectAuthorityV1(
                    definition.ParticipantId.ToString(),
                    checked((int)definition.ActorLifecycleGeneration),
                    definition.RuntimeDefinition.StatusEffects);
                Bridge = new FactWindowStatusEffectBridgeV1(
                    definition.RuntimeDefinition.Bindings);
            }

            public ConditionRuntimeParticipantDefinitionV1 Definition { get; }
            public FactWindowConditionAuthorityV1 Conditions { get; }
            public StatusEffectAuthorityV1 StatusEffects { get; }
            public FactWindowStatusEffectBridgeV1 Bridge { get; }
        }

        private sealed class DeliveryReplayRecord
        {
            public DeliveryReplayRecord(
                string fingerprint,
                ConditionFactIngestionResultV1 result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public ConditionFactIngestionResultV1 Result { get; }
        }

        private sealed class AdvanceReplayRecord
        {
            public AdvanceReplayRecord(
                string fingerprint,
                ConditionRuntimeSnapshotV1 snapshot)
            {
                Fingerprint = fingerprint;
                Snapshot = snapshot;
            }

            public string Fingerprint { get; }
            public ConditionRuntimeSnapshotV1 Snapshot { get; }
        }

        private sealed class ReconstructionReplayRecord
        {
            public ReconstructionReplayRecord(
                string fingerprint,
                ConditionRunReconstructionResultV1 result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public ConditionRunReconstructionResultV1 Result { get; }
        }

        private readonly IConditionRunClockV1 clock;
        private readonly IConditionRunLifecycleV1 lifecycle;
        private readonly AcceptedGameplayFactAdapterRegistryV1 adapters;
        private readonly Dictionary<string, DeliveryReplayRecord> deliveryReplay =
            new Dictionary<string, DeliveryReplayRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConditionObservedGameplayFactV1>
            acceptedBySourceFact = new Dictionary<
                string,
                ConditionObservedGameplayFactV1>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> acceptedSourceFingerprints =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConditionFactIngestionResultV1>
            acceptedResultBySourceFact = new Dictionary<
                string,
                ConditionFactIngestionResultV1>(StringComparer.Ordinal);
        private readonly Dictionary<string, AdvanceReplayRecord> advanceReplay =
            new Dictionary<string, AdvanceReplayRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReconstructionReplayRecord>
            reconstructionReplay = new Dictionary<
                string,
                ReconstructionReplayRecord>(StringComparer.Ordinal);
        private Dictionary<string, ParticipantRuntime> participants;
        private ConditionRunDefinitionV1 definition;

        public ConditionRuntimeAuthorityV1(
            IConditionRunClockV1 clock,
            IConditionRunLifecycleV1 lifecycle,
            AcceptedGameplayFactAdapterRegistryV1 adapters,
            ConditionRunDefinitionV1 definition)
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

        public ConditionRuntimeSnapshotV1 Snapshot
        {
            get { return BuildSnapshot(); }
        }

        public ConditionFactIngestionResultV1 Ingest(
            AcceptedGameplayFactDeliveryV1 delivery)
        {
            if (delivery == null)
                return Rejected(
                    "condition-fact-delivery-null",
                    null,
                    null);

            string genericSourceFingerprint =
                ConditionSourceFactFingerprintV1.Compute(delivery.SourceFact);
            IAcceptedGameplayFactAdapterV1 adapter;
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

            IAcceptedGameplayFactSourceFingerprintV1 fingerprintAdapter =
                adapter as IAcceptedGameplayFactSourceFingerprintV1;
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

            ConditionObservedGameplayFactV1 observed;
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

            ConditionFactIngestionResultV1 validationFailure =
                ValidateObserved(observed);
            if (validationFailure != null)
            {
                StoreDeliveryReplay(
                    delivery.DeliveryOperationId,
                    deliveryFingerprint,
                    validationFailure);
                return validationFailure;
            }

            ConditionObservedGameplayFactV1 priorSource;
            if (acceptedBySourceFact.TryGetValue(
                observed.SourceFactId,
                out priorSource))
            {
                ConditionFactIngestionResultV1 priorSourceResult =
                    acceptedResultBySourceFact[observed.SourceFactId];
                ConditionFactIngestionResultV1 duplicate;
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

            ParticipantRuntime participant =
                participants[observed.SubjectParticipantId.ToString()];
            RuntimeObservedFactResultV1 conditionResult =
                participant.Conditions.Apply(observed.ToObservedFact());
            if (conditionResult.Status != RuntimeObservedFactStatusV1.Applied)
            {
                var rejected = new ConditionFactIngestionResultV1(
                    ConditionFactIngestionStatusV1.Rejected,
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

            var effectResults = new List<StatusEffectCommandResultV1>();
            foreach (RuntimeConditionActivationFactV1 activation in
                conditionResult.Activations)
            {
                ApplyStatusEffectCommandV1 command;
                string effectOperationId = "condition-effect:"
                    + observed.SourceFactId + ":" + activation.ConditionId + ":"
                    + activation.Fingerprint;
                if (!participant.Bridge.TryCreateApplyCommand(
                    activation,
                    effectOperationId,
                    checked((int)participant.Definition.ActorLifecycleGeneration),
                    out command))
                {
                    var bridgeRejected = new ConditionFactIngestionResultV1(
                        ConditionFactIngestionStatusV1.Rejected,
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

                StatusEffectCommandResultV1 effectResult =
                    participant.StatusEffects.Apply(command);
                effectResults.Add(effectResult);
                if (!effectResult.IsAccepted)
                {
                    var effectRejected = new ConditionFactIngestionResultV1(
                        ConditionFactIngestionStatusV1.Rejected,
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
            var applied = new ConditionFactIngestionResultV1(
                ConditionFactIngestionStatusV1.Applied,
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

        public ConditionRuntimeSnapshotV1 Advance(string operationId)
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
            string fingerprint = ConditionRuntimeHashV1.Hash(
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

            List<ParticipantRuntime> ordered = participants.Values
                .OrderBy(
                    item => item.Definition.ParticipantId.ToString(),
                    StringComparer.Ordinal)
                .ToList();
            PrevalidateAdvance(ordered, tick);

            foreach (ParticipantRuntime participant in ordered)
            {
                StatusEffectCommandResultV1 result =
                    participant.StatusEffects.Advance(
                        new AdvanceStatusEffectTickCommandV1(
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

            ConditionRuntimeSnapshotV1 snapshot = BuildSnapshot();
            advanceReplay.Add(
                normalizedOperationId,
                new AdvanceReplayRecord(fingerprint, snapshot));
            return snapshot;
        }

        public ConditionRunReconstructionResultV1 Reconstruct(
            ConditionRunReconstructionCommandV1 command)
        {
            if (command == null)
            {
                return new ConditionRunReconstructionResultV1(
                    ConditionFactIngestionStatusV1.Rejected,
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
                    return new ConditionRunReconstructionResultV1(
                        ConditionFactIngestionStatusV1.ConflictingDuplicate,
                        "condition-run-reconstruction-conflicting-duplicate",
                        replay.Result.Snapshot);
                }
                return new ConditionRunReconstructionResultV1(
                    ConditionFactIngestionStatusV1.ExactDuplicateNoChange,
                    replay.Result.DiagnosticCode,
                    replay.Result.Snapshot);
            }

            ConditionRunReconstructionResultV1 result;
            if (!ConditionRuntimeHashV1.SameId(
                    command.ExpectedRunId,
                    definition.Lifecycle.RunId)
                || command.ExpectedRunGeneration
                    != definition.Lifecycle.Generation)
            {
                result = new ConditionRunReconstructionResultV1(
                    ConditionFactIngestionStatusV1.Rejected,
                    "condition-run-reconstruction-current-mismatch",
                    BuildSnapshot());
            }
            else if (!MatchesLifecycle(
                command.NextRun.Lifecycle,
                lifecycle.Current))
            {
                result = new ConditionRunReconstructionResultV1(
                    ConditionFactIngestionStatusV1.Rejected,
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
                result = new ConditionRunReconstructionResultV1(
                    ConditionFactIngestionStatusV1.Applied,
                    string.Empty,
                    BuildSnapshot());
            }

            reconstructionReplay.Add(
                command.OperationId,
                new ReconstructionReplayRecord(command.Fingerprint, result));
            return result;
        }

        private ConditionFactIngestionResultV1 ValidateObserved(
            ConditionObservedGameplayFactV1 observed)
        {
            if (!MatchesLifecycle(definition.Lifecycle, lifecycle.Current))
                return Rejected(
                    "condition-run-lifecycle-not-reconstructed",
                    observed,
                    null);
            if (!ConditionRuntimeHashV1.SameId(
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

            ParticipantRuntime participant;
            if (!participants.TryGetValue(
                observed.SubjectParticipantId.ToString(),
                out participant))
                return Rejected(
                    "condition-fact-participant-unknown",
                    observed,
                    null);
            if (!ConditionRuntimeHashV1.SameId(
                observed.SourceCharacterId,
                participant.Definition.CharacterId))
                return Rejected(
                    "condition-fact-source-character-mismatch",
                    observed,
                    null);
            if (!ConditionRuntimeHashV1.SameId(
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
            AcceptedGameplayFactDeliveryV1 delivery,
            string sourceFactFingerprint,
            string adaptationState)
        {
            return ConditionRuntimeHashV1.Hash(
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

        private ConditionFactIngestionResultV1 ResolveOrStoreRejectedDelivery(
            string operationId,
            string fingerprint,
            string diagnostic,
            ConditionObservedGameplayFactV1 observed)
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

            ConditionFactIngestionResultV1 result = Rejected(
                diagnostic,
                observed,
                null);
            StoreDeliveryReplay(operationId, fingerprint, result);
            return result;
        }

        private void PrevalidateAdvance(
            IEnumerable<ParticipantRuntime> orderedParticipants,
            long tick)
        {
            foreach (ParticipantRuntime participant in orderedParticipants)
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

        private ConditionFactIngestionResultV1 Rejected(
            string diagnostic,
            ConditionObservedGameplayFactV1 observed,
            RuntimeObservedFactResultV1 conditionResult)
        {
            return new ConditionFactIngestionResultV1(
                ConditionFactIngestionStatusV1.Rejected,
                diagnostic,
                observed,
                conditionResult,
                null,
                BuildSnapshot());
        }

        private static ConditionFactIngestionResultV1 Conflict(
            string diagnostic,
            ConditionObservedGameplayFactV1 observed,
            ConditionRuntimeSnapshotV1 stableSnapshot)
        {
            return new ConditionFactIngestionResultV1(
                ConditionFactIngestionStatusV1.ConflictingDuplicate,
                diagnostic,
                observed,
                null,
                null,
                stableSnapshot);
        }

        private static ConditionFactIngestionResultV1 Duplicate(
            ConditionFactIngestionResultV1 prior)
        {
            return new ConditionFactIngestionResultV1(
                ConditionFactIngestionStatusV1.ExactDuplicateNoChange,
                prior.DiagnosticCode,
                prior.ObservedFact,
                prior.ConditionResult,
                prior.EffectResults,
                prior.Snapshot);
        }

        private void StoreDeliveryReplay(
            string operationId,
            string fingerprint,
            ConditionFactIngestionResultV1 result)
        {
            deliveryReplay.Add(
                operationId,
                new DeliveryReplayRecord(fingerprint, result));
        }

        private ConditionRuntimeSnapshotV1 BuildSnapshot()
        {
            long tick = clock.CurrentTick;
            if (tick < 0L)
                throw new InvalidOperationException(
                    "The authoritative run tick cannot be negative.");
            IEnumerable<ConditionParticipantSnapshotV1> snapshots =
                participants.Values.Select(
                    participant => new ConditionParticipantSnapshotV1(
                        participant.Definition,
                        participant.Conditions.LatestAcceptedTick,
                        participant.Conditions.ActiveConditionIdsAt(tick),
                        participant.StatusEffects.Snapshot));
            return new ConditionRuntimeSnapshotV1(
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

        private static Dictionary<string, ParticipantRuntime> BuildParticipants(
            ConditionRunDefinitionV1 runDefinition)
        {
            return runDefinition.Participants.ToDictionary(
                item => item.ParticipantId.ToString(),
                item => new ParticipantRuntime(item),
                StringComparer.Ordinal);
        }

        private static bool MatchesLifecycle(
            ConditionRunLifecycleSnapshotV1 expected,
            ConditionRunLifecycleSnapshotV1 actual)
        {
            return expected != null && actual != null
                && ConditionRuntimeHashV1.SameId(expected.RunId, actual.RunId)
                && expected.Generation == actual.Generation;
        }
    }
}

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
            public ParticipantRuntime(ConditionRuntimeParticipantDefinitionV1 definition)
            {
                Definition = definition;
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
            public DeliveryReplayRecord(string fingerprint, ConditionFactIngestionResultV1 result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public ConditionFactIngestionResultV1 Result { get; }
        }

        private sealed class ReconstructionReplayRecord
        {
            public ReconstructionReplayRecord(string fingerprint, ConditionRunReconstructionResultV1 result)
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
        private readonly Dictionary<string, ConditionObservedGameplayFactV1> acceptedBySourceFact =
            new Dictionary<string, ConditionObservedGameplayFactV1>(StringComparer.Ordinal);
        private sealed class AdvanceReplayRecord
        {
            public AdvanceReplayRecord(string fingerprint, ConditionRuntimeSnapshotV1 snapshot)
            {
                Fingerprint = fingerprint;
                Snapshot = snapshot;
            }

            public string Fingerprint { get; }
            public ConditionRuntimeSnapshotV1 Snapshot { get; }
        }

        private readonly Dictionary<string, AdvanceReplayRecord> advanceReplay =
            new Dictionary<string, AdvanceReplayRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReconstructionReplayRecord> reconstructionReplay =
            new Dictionary<string, ReconstructionReplayRecord>(StringComparer.Ordinal);
        private Dictionary<string, ParticipantRuntime> participants;
        private ConditionRunDefinitionV1 definition;

        public ConditionRuntimeAuthorityV1(
            IConditionRunClockV1 clock,
            IConditionRunLifecycleV1 lifecycle,
            AcceptedGameplayFactAdapterRegistryV1 adapters,
            ConditionRunDefinitionV1 definition)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            this.adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (clock.CurrentTick < 0L)
                throw new ArgumentException("The authoritative run tick cannot be negative.", nameof(clock));
            if (!MatchesLifecycle(definition.Lifecycle, lifecycle.Current))
                throw new ArgumentException("The condition runtime definition must match the current run lifecycle.", nameof(definition));
            participants = BuildParticipants(definition);
        }

        public ConditionRuntimeSnapshotV1 Snapshot
        {
            get { return BuildSnapshot(); }
        }

        public ConditionFactIngestionResultV1 Ingest(AcceptedGameplayFactDeliveryV1 delivery)
        {
            if (delivery == null)
                return Rejected("condition-fact-delivery-null", null, null);

            IAcceptedGameplayFactAdapterV1 adapter;
            if (!adapters.TryResolve(delivery.SourceFact.GetType(), out adapter))
            {
                string unsupportedFingerprint = DeliveryEnvelopeFingerprint(
                    delivery,
                    "unsupported|" + delivery.SourceFact.GetType().FullName);
                return ResolveOrStoreRejectedDelivery(
                    delivery.DeliveryOperationId,
                    unsupportedFingerprint,
                    "condition-fact-type-unsupported",
                    null);
            }

            ConditionObservedGameplayFactV1 observed;
            string adapterDiagnostic;
            if (!adapter.TryAdapt(delivery, out observed, out adapterDiagnostic))
            {
                string rejectedFingerprint = DeliveryEnvelopeFingerprint(
                    delivery,
                    "adapter-rejected|" + adapter.SourceFactTypeId + "|"
                    + (adapterDiagnostic ?? string.Empty));
                return ResolveOrStoreRejectedDelivery(
                    delivery.DeliveryOperationId,
                    rejectedFingerprint,
                    string.IsNullOrWhiteSpace(adapterDiagnostic)
                        ? "condition-fact-adapter-rejected"
                        : adapterDiagnostic,
                    observed);
            }

            string deliveryFingerprint = ConditionRuntimeHashV1.Hash(
                delivery.DeliveryOperationId + "|" + observed.Fingerprint);
            DeliveryReplayRecord priorDelivery;
            if (deliveryReplay.TryGetValue(delivery.DeliveryOperationId, out priorDelivery))
            {
                if (!string.Equals(priorDelivery.Fingerprint, deliveryFingerprint, StringComparison.Ordinal))
                {
                    return new ConditionFactIngestionResultV1(
                        ConditionFactIngestionStatusV1.ConflictingDuplicate,
                        "condition-delivery-operation-conflicting-duplicate",
                        observed,
                        null,
                        null,
                        BuildSnapshot());
                }
                return Duplicate(priorDelivery.Result, observed);
            }

            ConditionFactIngestionResultV1 validationFailure = ValidateObserved(observed);
            if (validationFailure != null)
            {
                deliveryReplay.Add(
                    delivery.DeliveryOperationId,
                    new DeliveryReplayRecord(deliveryFingerprint, validationFailure));
                return validationFailure;
            }

            ConditionObservedGameplayFactV1 priorSource;
            if (acceptedBySourceFact.TryGetValue(observed.SourceFactId, out priorSource))
            {
                ConditionFactIngestionResultV1 duplicate;
                if (!string.Equals(priorSource.Fingerprint, observed.Fingerprint, StringComparison.Ordinal))
                {
                    duplicate = new ConditionFactIngestionResultV1(
                        ConditionFactIngestionStatusV1.ConflictingDuplicate,
                        "condition-source-fact-conflicting-duplicate",
                        observed,
                        null,
                        null,
                        BuildSnapshot());
                }
                else
                {
                    duplicate = new ConditionFactIngestionResultV1(
                        ConditionFactIngestionStatusV1.ExactDuplicateNoChange,
                        string.Empty,
                        priorSource,
                        null,
                        null,
                        BuildSnapshot());
                }
                deliveryReplay.Add(
                    delivery.DeliveryOperationId,
                    new DeliveryReplayRecord(deliveryFingerprint, duplicate));
                return duplicate;
            }

            ParticipantRuntime participant = participants[observed.SubjectParticipantId.ToString()];
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
                deliveryReplay.Add(
                    delivery.DeliveryOperationId,
                    new DeliveryReplayRecord(deliveryFingerprint, rejected));
                return rejected;
            }

            var effectResults = new List<StatusEffectCommandResultV1>();
            foreach (RuntimeConditionActivationFactV1 activation in conditionResult.Activations)
            {
                ApplyStatusEffectCommandV1 command;
                string effectOperationId = "condition-effect:"
                    + observed.SourceFactId + ":" + activation.ConditionId + ":" + activation.Fingerprint;
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
                    deliveryReplay.Add(
                        delivery.DeliveryOperationId,
                        new DeliveryReplayRecord(deliveryFingerprint, bridgeRejected));
                    return bridgeRejected;
                }

                StatusEffectCommandResultV1 effectResult = participant.StatusEffects.Apply(command);
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
                    deliveryReplay.Add(
                        delivery.DeliveryOperationId,
                        new DeliveryReplayRecord(deliveryFingerprint, effectRejected));
                    return effectRejected;
                }
            }

            acceptedBySourceFact.Add(observed.SourceFactId, observed);
            var applied = new ConditionFactIngestionResultV1(
                ConditionFactIngestionStatusV1.Applied,
                string.Empty,
                observed,
                conditionResult,
                effectResults,
                BuildSnapshot());
            deliveryReplay.Add(
                delivery.DeliveryOperationId,
                new DeliveryReplayRecord(deliveryFingerprint, applied));
            return applied;
        }

        public ConditionRuntimeSnapshotV1 Advance(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException("A condition-runtime advance operation identity is required.", nameof(operationId));
            EnsureLifecycleCurrent();
            long tick = clock.CurrentTick;
            if (tick < 0L) throw new InvalidOperationException("The authoritative run tick cannot be negative.");

            string fingerprint = ConditionRuntimeHashV1.Hash(operationId.Trim() + "|" + tick.ToString(CultureInfo.InvariantCulture));
            AdvanceReplayRecord prior;
            if (advanceReplay.TryGetValue(operationId.Trim(), out prior))
            {
                if (!string.Equals(prior.Fingerprint, fingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException("A condition-runtime advance operation was reused with conflicting facts.");
                return prior.Snapshot;
            }

            foreach (ParticipantRuntime participant in participants.Values
                .OrderBy(item => item.Definition.ParticipantId.ToString(), StringComparer.Ordinal))
            {
                participant.StatusEffects.Advance(
                    new AdvanceStatusEffectTickCommandV1(
                        operationId.Trim() + ":" + participant.Definition.ParticipantId,
                        participant.Definition.ParticipantId.ToString(),
                        checked((int)participant.Definition.ActorLifecycleGeneration),
                        tick));
            }
            ConditionRuntimeSnapshotV1 snapshot = BuildSnapshot();
            advanceReplay.Add(
                operationId.Trim(),
                new AdvanceReplayRecord(fingerprint, snapshot));
            return snapshot;
        }

        public ConditionRunReconstructionResultV1 Reconstruct(
            ConditionRunReconstructionCommandV1 command)
        {
            if (command == null)
                return new ConditionRunReconstructionResultV1(
                    ConditionFactIngestionStatusV1.Rejected,
                    "condition-run-reconstruction-null",
                    BuildSnapshot());

            ReconstructionReplayRecord replay;
            if (reconstructionReplay.TryGetValue(command.OperationId, out replay))
            {
                if (!string.Equals(replay.Fingerprint, command.Fingerprint, StringComparison.Ordinal))
                {
                    return new ConditionRunReconstructionResultV1(
                        ConditionFactIngestionStatusV1.ConflictingDuplicate,
                        "condition-run-reconstruction-conflicting-duplicate",
                        BuildSnapshot());
                }
                return new ConditionRunReconstructionResultV1(
                    ConditionFactIngestionStatusV1.ExactDuplicateNoChange,
                    replay.Result.DiagnosticCode,
                    replay.Result.Snapshot);
            }

            ConditionRunReconstructionResultV1 result;
            if (!ConditionRuntimeHashV1.SameId(command.ExpectedRunId, definition.Lifecycle.RunId)
                || command.ExpectedRunGeneration != definition.Lifecycle.Generation)
            {
                result = new ConditionRunReconstructionResultV1(
                    ConditionFactIngestionStatusV1.Rejected,
                    "condition-run-reconstruction-current-mismatch",
                    BuildSnapshot());
            }
            else if (!MatchesLifecycle(command.NextRun.Lifecycle, lifecycle.Current))
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
                return Rejected("condition-run-lifecycle-not-reconstructed", observed, null);
            if (!ConditionRuntimeHashV1.SameId(observed.RunId, definition.Lifecycle.RunId))
                return Rejected("condition-fact-run-mismatch", observed, null);
            if (observed.RunLifecycleGeneration != definition.Lifecycle.Generation)
                return Rejected("condition-fact-run-lifecycle-stale", observed, null);
            if (observed.AuthoritativeTick > clock.CurrentTick)
                return Rejected("condition-fact-tick-future", observed, null);

            ParticipantRuntime participant;
            if (!participants.TryGetValue(observed.SubjectParticipantId.ToString(), out participant))
                return Rejected("condition-fact-participant-unknown", observed, null);
            if (!ConditionRuntimeHashV1.SameId(observed.SourceCharacterId, participant.Definition.CharacterId))
                return Rejected("condition-fact-source-character-mismatch", observed, null);
            if (!ConditionRuntimeHashV1.SameId(observed.SourceActorId, participant.Definition.ActorId))
                return Rejected("condition-fact-source-actor-mismatch", observed, null);
            if (observed.AuthoritativeTick < participant.Conditions.LatestAcceptedTick
                || observed.AuthoritativeTick < participant.StatusEffects.LatestAcceptedTick)
                return Rejected("condition-fact-tick-stale", observed, null);
            if (observed.SourceActorLifecycleGeneration
                != participant.Definition.ActorLifecycleGeneration)
                return Rejected("condition-fact-source-lifecycle-stale", observed, null);
            return null;
        }

        private static string DeliveryEnvelopeFingerprint(
            AcceptedGameplayFactDeliveryV1 delivery,
            string adaptationState)
        {
            return ConditionRuntimeHashV1.Hash(
                delivery.DeliveryOperationId + "|"
                + delivery.SourceFact.GetType().FullName + "|"
                + delivery.RunId + "|"
                + delivery.RunLifecycleGeneration.ToString(CultureInfo.InvariantCulture) + "|"
                + delivery.SourceActorId + "|"
                + delivery.SubjectParticipantId + "|"
                + delivery.SourceCharacterId + "|"
                + delivery.SourceActorLifecycleGeneration.ToString(CultureInfo.InvariantCulture) + "|"
                + delivery.AuthoritativeTick.ToString(CultureInfo.InvariantCulture) + "|"
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
                if (!string.Equals(prior.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return new ConditionFactIngestionResultV1(
                        ConditionFactIngestionStatusV1.ConflictingDuplicate,
                        "condition-delivery-operation-conflicting-duplicate",
                        observed,
                        null,
                        null,
                        BuildSnapshot());
                }
                return Duplicate(prior.Result, observed);
            }

            ConditionFactIngestionResultV1 result = Rejected(diagnostic, observed, null);
            deliveryReplay.Add(operationId, new DeliveryReplayRecord(fingerprint, result));
            return result;
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

        private ConditionFactIngestionResultV1 Duplicate(
            ConditionFactIngestionResultV1 prior,
            ConditionObservedGameplayFactV1 observed)
        {
            return new ConditionFactIngestionResultV1(
                ConditionFactIngestionStatusV1.ExactDuplicateNoChange,
                prior.DiagnosticCode,
                observed ?? prior.ObservedFact,
                prior.ConditionResult,
                prior.EffectResults,
                BuildSnapshot());
        }

        private ConditionRuntimeSnapshotV1 BuildSnapshot()
        {
            long tick = clock.CurrentTick;
            if (tick < 0L)
                throw new InvalidOperationException("The authoritative run tick cannot be negative.");
            var snapshots = participants.Values.Select(
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
                throw new InvalidOperationException("The condition runtime must be reconstructed for the current run lifecycle.");
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

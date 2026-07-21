using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Modifiers;
using ShooterMover.Application.Modifiers.StatusEffects;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.StatusEffects;

namespace ShooterMover.ConditionRuntime
{
    public static class ConditionRuntimeFactTypeIdsV1
    {
        public const string EnemyKilled = "gameplay.enemy-killed";
    }

    public interface IConditionRunClockV1
    {
        long CurrentTick { get; }
    }

    public interface IConditionRunLifecycleV1
    {
        ConditionRunLifecycleSnapshotV1 Current { get; }
    }

    public sealed class ConditionRunLifecycleSnapshotV1
    {
        public ConditionRunLifecycleSnapshotV1(StableId runId, long generation)
        {
            RunId = runId ?? throw new ArgumentNullException(nameof(runId));
            if (generation <= 0L) throw new ArgumentOutOfRangeException(nameof(generation));
            Generation = generation;
            Fingerprint = ConditionRuntimeHashV1.Hash(
                RunId + "|" + Generation.ToString(CultureInfo.InvariantCulture));
        }

        public StableId RunId { get; }
        public long Generation { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionEffectRuntimeDefinitionV1
    {
        public ConditionEffectRuntimeDefinitionV1(
            string definitionSetId,
            string contentVersion,
            IEnumerable<FactWindowConditionDefinitionV1> conditions,
            StatusEffectCatalogV1 statusEffects,
            IEnumerable<FactWindowStatusEffectBindingV1> bindings)
        {
            if (string.IsNullOrWhiteSpace(definitionSetId))
                throw new ArgumentException("A condition runtime definition-set identity is required.", nameof(definitionSetId));
            if (string.IsNullOrWhiteSpace(contentVersion))
                throw new ArgumentException("A condition runtime content version is required.", nameof(contentVersion));

            List<FactWindowConditionDefinitionV1> conditionItems = (conditions
                ?? throw new ArgumentNullException(nameof(conditions))).ToList();
            List<FactWindowStatusEffectBindingV1> bindingItems = (bindings
                ?? throw new ArgumentNullException(nameof(bindings))).ToList();
            if (conditionItems.Count == 0 || conditionItems.Any(item => item == null))
                throw new ArgumentException("At least one non-null fact-window condition is required.", nameof(conditions));
            if (conditionItems.Select(item => item.ConditionId).Distinct(StringComparer.Ordinal).Count()
                != conditionItems.Count)
                throw new ArgumentException("Condition identities must be unique.", nameof(conditions));
            if (bindingItems.Count != conditionItems.Count || bindingItems.Any(item => item == null))
                throw new ArgumentException("Every condition must have exactly one status-effect binding.", nameof(bindings));
            if (bindingItems.Select(item => item.ConditionId).Distinct(StringComparer.Ordinal).Count()
                != bindingItems.Count)
                throw new ArgumentException("Condition bindings must be unique.", nameof(bindings));

            var conditionIds = new HashSet<string>(
                conditionItems.Select(item => item.ConditionId), StringComparer.Ordinal);
            foreach (FactWindowStatusEffectBindingV1 binding in bindingItems)
            {
                StatusEffectDefinitionV1 ignored;
                if (!conditionIds.Contains(binding.ConditionId))
                    throw new ArgumentException("A binding references an unknown condition.", nameof(bindings));
                if (!(statusEffects ?? throw new ArgumentNullException(nameof(statusEffects)))
                    .TryGetDefinition(binding.EffectId, out ignored))
                    throw new ArgumentException("A binding references an unknown status effect.", nameof(bindings));
            }

            DefinitionSetId = definitionSetId.Trim();
            ContentVersion = contentVersion.Trim();
            Conditions = new ReadOnlyCollection<FactWindowConditionDefinitionV1>(
                conditionItems.OrderBy(item => item.ConditionId, StringComparer.Ordinal).ToList());
            StatusEffects = statusEffects;
            Bindings = new ReadOnlyCollection<FactWindowStatusEffectBindingV1>(
                bindingItems.OrderBy(item => item.ConditionId, StringComparer.Ordinal).ToList());
            Fingerprint = ConditionRuntimeHashV1.Hash(
                DefinitionSetId + "|" + ContentVersion + "|"
                + string.Join(";", Conditions.Select(item => item.Fingerprint)) + "|"
                + StatusEffects.Fingerprint + "|"
                + string.Join(";", Bindings.Select(item => item.ConditionId + "|" + item.EffectId + "|" + item.SourceId)));
        }

        public string DefinitionSetId { get; }
        public string ContentVersion { get; }
        public IReadOnlyList<FactWindowConditionDefinitionV1> Conditions { get; }
        public StatusEffectCatalogV1 StatusEffects { get; }
        public IReadOnlyList<FactWindowStatusEffectBindingV1> Bindings { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionRuntimeParticipantDefinitionV1
    {
        public ConditionRuntimeParticipantDefinitionV1(
            StableId participantId,
            StableId characterId,
            StableId actorId,
            long actorLifecycleGeneration,
            string persistentSkillAllocationFingerprint,
            ConditionEffectRuntimeDefinitionV1 runtimeDefinition)
        {
            ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
            CharacterId = characterId ?? throw new ArgumentNullException(nameof(characterId));
            ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            if (actorLifecycleGeneration <= 0L
                || actorLifecycleGeneration > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(actorLifecycleGeneration));
            if (string.IsNullOrWhiteSpace(persistentSkillAllocationFingerprint))
                throw new ArgumentException("A persistent skill-allocation fingerprint is required.", nameof(persistentSkillAllocationFingerprint));
            ActorLifecycleGeneration = actorLifecycleGeneration;
            PersistentSkillAllocationFingerprint = persistentSkillAllocationFingerprint.Trim();
            RuntimeDefinition = runtimeDefinition ?? throw new ArgumentNullException(nameof(runtimeDefinition));
            Fingerprint = ConditionRuntimeHashV1.Hash(
                ParticipantId + "|" + CharacterId + "|" + ActorId + "|"
                + ActorLifecycleGeneration.ToString(CultureInfo.InvariantCulture) + "|"
                + PersistentSkillAllocationFingerprint + "|" + RuntimeDefinition.Fingerprint);
        }

        public StableId ParticipantId { get; }
        public StableId CharacterId { get; }
        public StableId ActorId { get; }
        public long ActorLifecycleGeneration { get; }
        public string PersistentSkillAllocationFingerprint { get; }
        public ConditionEffectRuntimeDefinitionV1 RuntimeDefinition { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionRunDefinitionV1
    {
        public ConditionRunDefinitionV1(
            ConditionRunLifecycleSnapshotV1 lifecycle,
            IEnumerable<ConditionRuntimeParticipantDefinitionV1> participants)
        {
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            List<ConditionRuntimeParticipantDefinitionV1> items = (participants
                ?? throw new ArgumentNullException(nameof(participants))).ToList();
            if (items.Count == 0 || items.Any(item => item == null))
                throw new ArgumentException("At least one non-null condition participant is required.", nameof(participants));
            if (items.Select(item => item.ParticipantId.ToString()).Distinct(StringComparer.Ordinal).Count()
                != items.Count)
                throw new ArgumentException("Condition participant identities must be unique.", nameof(participants));
            if (items.Select(item => item.CharacterId.ToString()).Distinct(StringComparer.Ordinal).Count()
                != items.Count)
                throw new ArgumentException("Condition character identities must be unique.", nameof(participants));
            if (items.Select(item => item.ActorId.ToString()).Distinct(StringComparer.Ordinal).Count()
                != items.Count)
                throw new ArgumentException("Condition actor identities must be unique.", nameof(participants));
            Participants = new ReadOnlyCollection<ConditionRuntimeParticipantDefinitionV1>(
                items.OrderBy(item => item.ParticipantId.ToString(), StringComparer.Ordinal).ToList());
            Fingerprint = ConditionRuntimeHashV1.Hash(
                Lifecycle.Fingerprint + "|" + string.Join(";", Participants.Select(item => item.Fingerprint)));
        }

        public ConditionRunLifecycleSnapshotV1 Lifecycle { get; }
        public IReadOnlyList<ConditionRuntimeParticipantDefinitionV1> Participants { get; }
        public string Fingerprint { get; }
    }

    public sealed class AcceptedGameplayFactDeliveryV1
    {
        public AcceptedGameplayFactDeliveryV1(
            string deliveryOperationId,
            object sourceFact,
            StableId runId,
            long runLifecycleGeneration,
            StableId sourceActorId,
            StableId subjectParticipantId,
            StableId sourceCharacterId,
            long sourceActorLifecycleGeneration,
            long authoritativeTick)
        {
            if (string.IsNullOrWhiteSpace(deliveryOperationId))
                throw new ArgumentException("A fact-delivery operation identity is required.", nameof(deliveryOperationId));
            SourceFact = sourceFact ?? throw new ArgumentNullException(nameof(sourceFact));
            RunId = runId ?? throw new ArgumentNullException(nameof(runId));
            if (runLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(runLifecycleGeneration));
            RunLifecycleGeneration = runLifecycleGeneration;
            SourceActorId = sourceActorId ?? throw new ArgumentNullException(nameof(sourceActorId));
            SubjectParticipantId = subjectParticipantId ?? throw new ArgumentNullException(nameof(subjectParticipantId));
            SourceCharacterId = sourceCharacterId ?? throw new ArgumentNullException(nameof(sourceCharacterId));
            if (sourceActorLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceActorLifecycleGeneration));
            if (authoritativeTick < 0L)
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));
            DeliveryOperationId = deliveryOperationId.Trim();
            SourceActorLifecycleGeneration = sourceActorLifecycleGeneration;
            AuthoritativeTick = authoritativeTick;
        }

        public string DeliveryOperationId { get; }
        public object SourceFact { get; }
        public StableId RunId { get; }
        public long RunLifecycleGeneration { get; }
        public StableId SourceActorId { get; }
        public StableId SubjectParticipantId { get; }
        public StableId SourceCharacterId { get; }
        public long SourceActorLifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
    }

    public sealed class ConditionObservedGameplayFactV1
    {
        public ConditionObservedGameplayFactV1(
            string sourceFactId,
            string sourceFactTypeId,
            string triggeringFactId,
            string observedFactTypeId,
            StableId runId,
            long runLifecycleGeneration,
            StableId sourceActorId,
            StableId subjectParticipantId,
            StableId sourceCharacterId,
            StableId targetActorId,
            StableId targetParticipantId,
            long sourceActorLifecycleGeneration,
            long targetActorLifecycleGeneration,
            long authoritativeTick)
        {
            SourceFactId = Require(sourceFactId, nameof(sourceFactId));
            SourceFactTypeId = Require(sourceFactTypeId, nameof(sourceFactTypeId));
            TriggeringFactId = Require(triggeringFactId, nameof(triggeringFactId));
            ObservedFactTypeId = Require(observedFactTypeId, nameof(observedFactTypeId));
            RunId = runId ?? throw new ArgumentNullException(nameof(runId));
            if (runLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(runLifecycleGeneration));
            RunLifecycleGeneration = runLifecycleGeneration;
            SourceActorId = sourceActorId ?? throw new ArgumentNullException(nameof(sourceActorId));
            SubjectParticipantId = subjectParticipantId ?? throw new ArgumentNullException(nameof(subjectParticipantId));
            SourceCharacterId = sourceCharacterId ?? throw new ArgumentNullException(nameof(sourceCharacterId));
            TargetActorId = targetActorId ?? throw new ArgumentNullException(nameof(targetActorId));
            TargetParticipantId = targetParticipantId ?? throw new ArgumentNullException(nameof(targetParticipantId));
            if (sourceActorLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceActorLifecycleGeneration));
            if (targetActorLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(targetActorLifecycleGeneration));
            if (authoritativeTick < 0L)
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));
            SourceActorLifecycleGeneration = sourceActorLifecycleGeneration;
            TargetActorLifecycleGeneration = targetActorLifecycleGeneration;
            AuthoritativeTick = authoritativeTick;
            Fingerprint = ConditionRuntimeHashV1.Hash(ToCanonicalString());
        }

        public string SourceFactId { get; }
        public string SourceFactTypeId { get; }
        public string TriggeringFactId { get; }
        public string ObservedFactTypeId { get; }
        public StableId RunId { get; }
        public long RunLifecycleGeneration { get; }
        public StableId SourceActorId { get; }
        public StableId SubjectParticipantId { get; }
        public StableId SourceCharacterId { get; }
        public StableId TargetActorId { get; }
        public StableId TargetParticipantId { get; }
        public long SourceActorLifecycleGeneration { get; }
        public long TargetActorLifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
        public string Fingerprint { get; }

        public RuntimeObservedFactV1 ToObservedFact()
        {
            return new RuntimeObservedFactV1(
                SourceFactId,
                ObservedFactTypeId,
                SubjectParticipantId.ToString(),
                AuthoritativeTick);
        }

        public string ToCanonicalString()
        {
            return SourceFactId + "|" + SourceFactTypeId + "|" + TriggeringFactId + "|"
                + ObservedFactTypeId + "|" + RunId + "|"
                + RunLifecycleGeneration.ToString(CultureInfo.InvariantCulture) + "|"
                + SourceActorId + "|"
                + SubjectParticipantId + "|" + SourceCharacterId + "|" + TargetActorId + "|"
                + TargetParticipantId + "|"
                + SourceActorLifecycleGeneration.ToString(CultureInfo.InvariantCulture) + "|"
                + TargetActorLifecycleGeneration.ToString(CultureInfo.InvariantCulture) + "|"
                + AuthoritativeTick.ToString(CultureInfo.InvariantCulture);
        }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A stable fact identity is required.", parameterName);
            return value.Trim();
        }
    }

    public interface IAcceptedGameplayFactAdapterV1
    {
        Type SourceFactRuntimeType { get; }
        string SourceFactTypeId { get; }
        bool TryAdapt(
            AcceptedGameplayFactDeliveryV1 delivery,
            out ConditionObservedGameplayFactV1 observedFact,
            out string diagnosticCode);
    }

    public sealed class AcceptedGameplayFactAdapterRegistryV1
    {
        private readonly IReadOnlyDictionary<Type, IAcceptedGameplayFactAdapterV1> adapters;

        public AcceptedGameplayFactAdapterRegistryV1(IEnumerable<IAcceptedGameplayFactAdapterV1> registrations)
        {
            List<IAcceptedGameplayFactAdapterV1> items = (registrations
                ?? throw new ArgumentNullException(nameof(registrations))).ToList();
            if (items.Count == 0 || items.Any(item => item == null))
                throw new ArgumentException("At least one non-null gameplay-fact adapter is required.", nameof(registrations));
            if (items.Select(item => item.SourceFactRuntimeType).Distinct().Count() != items.Count)
                throw new ArgumentException("Gameplay-fact runtime types must be registered once.", nameof(registrations));
            adapters = new ReadOnlyDictionary<Type, IAcceptedGameplayFactAdapterV1>(
                items.ToDictionary(item => item.SourceFactRuntimeType));
            Fingerprint = ConditionRuntimeHashV1.Hash(string.Join(";", items
                .OrderBy(item => item.SourceFactRuntimeType.FullName, StringComparer.Ordinal)
                .Select(item => item.SourceFactRuntimeType.FullName + "|" + item.SourceFactTypeId)));
        }

        public string Fingerprint { get; }

        public bool TryResolve(Type runtimeType, out IAcceptedGameplayFactAdapterV1 adapter)
        {
            if (runtimeType == null)
            {
                adapter = null;
                return false;
            }
            return adapters.TryGetValue(runtimeType, out adapter);
        }
    }

    public enum ConditionFactIngestionStatusV1
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public sealed class ConditionFactIngestionResultV1
    {
        public ConditionFactIngestionResultV1(
            ConditionFactIngestionStatusV1 status,
            string diagnosticCode,
            ConditionObservedGameplayFactV1 observedFact,
            RuntimeObservedFactResultV1 conditionResult,
            IEnumerable<StatusEffectCommandResultV1> effectResults,
            ConditionRuntimeSnapshotV1 snapshot)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            ObservedFact = observedFact;
            ConditionResult = conditionResult;
            EffectResults = new ReadOnlyCollection<StatusEffectCommandResultV1>(
                (effectResults ?? Array.Empty<StatusEffectCommandResultV1>()).ToList());
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Fingerprint = ConditionRuntimeHashV1.Hash(
                ((int)Status).ToString(CultureInfo.InvariantCulture) + "|" + DiagnosticCode + "|"
                + (ObservedFact == null ? string.Empty : ObservedFact.Fingerprint) + "|"
                + (ConditionResult == null ? string.Empty : ConditionResult.LatestAcceptedTick.ToString(CultureInfo.InvariantCulture)) + "|"
                + string.Join(";", EffectResults.Select(item => item.Fingerprint)) + "|" + Snapshot.Fingerprint);
        }

        public ConditionFactIngestionStatusV1 Status { get; }
        public string DiagnosticCode { get; }
        public ConditionObservedGameplayFactV1 ObservedFact { get; }
        public RuntimeObservedFactResultV1 ConditionResult { get; }
        public IReadOnlyList<StatusEffectCommandResultV1> EffectResults { get; }
        public ConditionRuntimeSnapshotV1 Snapshot { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionParticipantSnapshotV1
    {
        public ConditionParticipantSnapshotV1(
            ConditionRuntimeParticipantDefinitionV1 definition,
            long latestConditionTick,
            IEnumerable<string> activeConditionIds,
            StatusEffectStateSnapshotV1 statusEffects)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            LatestConditionTick = latestConditionTick;
            ActiveConditionIds = new ReadOnlyCollection<string>((activeConditionIds
                ?? Array.Empty<string>()).OrderBy(item => item, StringComparer.Ordinal).ToList());
            StatusEffects = statusEffects ?? throw new ArgumentNullException(nameof(statusEffects));
            Fingerprint = ConditionRuntimeHashV1.Hash(
                Definition.Fingerprint + "|" + LatestConditionTick.ToString(CultureInfo.InvariantCulture) + "|"
                + string.Join(";", ActiveConditionIds) + "|" + StatusEffects.Fingerprint);
        }

        public ConditionRuntimeParticipantDefinitionV1 Definition { get; }
        public long LatestConditionTick { get; }
        public IReadOnlyList<string> ActiveConditionIds { get; }
        public StatusEffectStateSnapshotV1 StatusEffects { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionRuntimeSnapshotV1
    {
        public ConditionRuntimeSnapshotV1(
            ConditionRunDefinitionV1 definition,
            long authoritativeTick,
            IEnumerable<ConditionParticipantSnapshotV1> participants,
            IEnumerable<ConditionObservedGameplayFactV1> acceptedFacts)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            AuthoritativeTick = authoritativeTick;
            Participants = new ReadOnlyCollection<ConditionParticipantSnapshotV1>((participants
                ?? Array.Empty<ConditionParticipantSnapshotV1>())
                .OrderBy(item => item.Definition.ParticipantId.ToString(), StringComparer.Ordinal).ToList());
            AcceptedFacts = new ReadOnlyCollection<ConditionObservedGameplayFactV1>((acceptedFacts
                ?? Array.Empty<ConditionObservedGameplayFactV1>())
                .OrderBy(item => item.SourceFactId, StringComparer.Ordinal).ToList());
            Fingerprint = ConditionRuntimeHashV1.Hash(
                Definition.Fingerprint + "|" + AuthoritativeTick.ToString(CultureInfo.InvariantCulture) + "|"
                + string.Join(";", Participants.Select(item => item.Fingerprint)) + "|"
                + string.Join(";", AcceptedFacts.Select(item => item.Fingerprint)));
        }

        public ConditionRunDefinitionV1 Definition { get; }
        public long AuthoritativeTick { get; }
        public IReadOnlyList<ConditionParticipantSnapshotV1> Participants { get; }
        public IReadOnlyList<ConditionObservedGameplayFactV1> AcceptedFacts { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionRunReconstructionCommandV1
    {
        public ConditionRunReconstructionCommandV1(
            string operationId,
            StableId expectedRunId,
            long expectedRunGeneration,
            ConditionRunDefinitionV1 nextRun)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException("A reconstruction operation identity is required.", nameof(operationId));
            OperationId = operationId.Trim();
            ExpectedRunId = expectedRunId ?? throw new ArgumentNullException(nameof(expectedRunId));
            if (expectedRunGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(expectedRunGeneration));
            ExpectedRunGeneration = expectedRunGeneration;
            NextRun = nextRun ?? throw new ArgumentNullException(nameof(nextRun));
            Fingerprint = ConditionRuntimeHashV1.Hash(
                OperationId + "|" + ExpectedRunId + "|"
                + ExpectedRunGeneration.ToString(CultureInfo.InvariantCulture) + "|" + NextRun.Fingerprint);
        }

        public string OperationId { get; }
        public StableId ExpectedRunId { get; }
        public long ExpectedRunGeneration { get; }
        public ConditionRunDefinitionV1 NextRun { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionRunReconstructionResultV1
    {
        public ConditionRunReconstructionResultV1(
            ConditionFactIngestionStatusV1 status,
            string diagnosticCode,
            ConditionRuntimeSnapshotV1 snapshot)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Fingerprint = ConditionRuntimeHashV1.Hash(
                ((int)Status).ToString(CultureInfo.InvariantCulture) + "|" + DiagnosticCode + "|" + Snapshot.Fingerprint);
        }

        public ConditionFactIngestionStatusV1 Status { get; }
        public string DiagnosticCode { get; }
        public ConditionRuntimeSnapshotV1 Snapshot { get; }
        public string Fingerprint { get; }
    }

    public sealed class FactWindowEffectFixtureV1
    {
        public FactWindowEffectFixtureV1(
            string conditionDefinitionId,
            string statusEffectDefinitionId,
            string observedFactTypeId,
            int requiredFactCount,
            long observationWindowTicks,
            long activeDurationTicks,
            decimal outgoingDamageMultiplier,
            StatusEffectStackingPolicyV1 stackingPolicy = StatusEffectStackingPolicyV1.Ignore,
            int maximumStacks = 1)
        {
            if (outgoingDamageMultiplier <= 0m)
                throw new ArgumentOutOfRangeException(nameof(outgoingDamageMultiplier));
            if (maximumStacks < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumStacks));
            ConditionDefinitionId = conditionDefinitionId;
            StatusEffectDefinitionId = statusEffectDefinitionId;
            ObservedFactTypeId = observedFactTypeId;
            RequiredFactCount = requiredFactCount;
            ObservationWindowTicks = observationWindowTicks;
            ActiveDurationTicks = activeDurationTicks;
            OutgoingDamageMultiplier = outgoingDamageMultiplier;
            StackingPolicy = stackingPolicy;
            MaximumStacks = maximumStacks;
        }

        public string ConditionDefinitionId { get; }
        public string StatusEffectDefinitionId { get; }
        public string ObservedFactTypeId { get; }
        public int RequiredFactCount { get; }
        public long ObservationWindowTicks { get; }
        public long ActiveDurationTicks { get; }
        public decimal OutgoingDamageMultiplier { get; }
        public StatusEffectStackingPolicyV1 StackingPolicy { get; }
        public int MaximumStacks { get; }

        public ConditionEffectRuntimeDefinitionV1 Build(
            string definitionSetId,
            string contentVersion,
            string bindingSourceId)
        {
            var condition = new FactWindowConditionDefinitionV1(
                ConditionDefinitionId,
                ObservedFactTypeId,
                RequiredFactCount,
                ObservationWindowTicks,
                ActiveDurationTicks,
                true);
            var effect = new StatusEffectDefinitionV1(
                StatusEffectDefinitionId,
                contentVersion,
                ActiveDurationTicks,
                MaximumStacks,
                StackingPolicy,
                "dispel-category.conditional",
                new[]
                {
                    new RuntimeModifierDefinitionV1(
                        StatusEffectDefinitionId + ".outgoing-damage",
                        DerivedStatTargetIdsV1.OutgoingDamageMultiplier,
                        RuntimeModifierOperationV1.Multiplicative,
                        OutgoingDamageMultiplier),
                });
            var catalog = new StatusEffectCatalogV1(
                definitionSetId + ".status-effects",
                contentVersion,
                new[] { effect });
            return new ConditionEffectRuntimeDefinitionV1(
                definitionSetId,
                contentVersion,
                new[] { condition },
                catalog,
                new[]
                {
                    new FactWindowStatusEffectBindingV1(
                        ConditionDefinitionId,
                        StatusEffectDefinitionId,
                        bindingSourceId),
                });
        }
    }

    internal static class ConditionRuntimeHashV1
    {
        internal static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        internal static bool SameId(StableId left, StableId right)
        {
            return left != null && right != null
                && string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal);
        }
    }
}

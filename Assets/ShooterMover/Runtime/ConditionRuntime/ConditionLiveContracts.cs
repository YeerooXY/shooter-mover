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
    public static class ConditionLiveFactTypeIds
    {
        public const string EnemyKilled = "gameplay.enemy-killed";
    }

    public interface IConditionRunClock
    {
        long CurrentTick { get; }
    }

    public interface IConditionRunLifecycle
    {
        ConditionRunLifecycleSnapshot Current { get; }
    }

    public sealed class ConditionRunLifecycleSnapshot
    {
        public ConditionRunLifecycleSnapshot(StableId runId, long generation)
        {
            RunId = runId ?? throw new ArgumentNullException(nameof(runId));
            if (generation <= 0L) throw new ArgumentOutOfRangeException(nameof(generation));
            Generation = generation;
            Fingerprint = ConditionLiveHash.Hash(
                RunId + "|" + Generation.ToString(CultureInfo.InvariantCulture));
        }

        public StableId RunId { get; }
        public long Generation { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionEffectLiveDefinition
    {
        public ConditionEffectLiveDefinition(
            string definitionSetId,
            string contentVersion,
            IEnumerable<FactWindowConditionDefinition> conditions,
            StatusEffectCatalog statusEffects,
            IEnumerable<FactWindowStatusEffectBinding> bindings)
        {
            if (string.IsNullOrWhiteSpace(definitionSetId))
                throw new ArgumentException("A condition runtime definition-set identity is required.", nameof(definitionSetId));
            if (string.IsNullOrWhiteSpace(contentVersion))
                throw new ArgumentException("A condition runtime content version is required.", nameof(contentVersion));

            List<FactWindowConditionDefinition> conditionItems = (conditions
                ?? throw new ArgumentNullException(nameof(conditions))).ToList();
            List<FactWindowStatusEffectBinding> bindingItems = (bindings
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
            foreach (FactWindowStatusEffectBinding binding in bindingItems)
            {
                StatusEffectDefinition ignored;
                if (!conditionIds.Contains(binding.ConditionId))
                    throw new ArgumentException("A binding references an unknown condition.", nameof(bindings));
                if (!(statusEffects ?? throw new ArgumentNullException(nameof(statusEffects)))
                    .TryGetDefinition(binding.EffectId, out ignored))
                    throw new ArgumentException("A binding references an unknown status effect.", nameof(bindings));
            }

            DefinitionSetId = definitionSetId.Trim();
            ContentVersion = contentVersion.Trim();
            Conditions = new ReadOnlyCollection<FactWindowConditionDefinition>(
                conditionItems.OrderBy(item => item.ConditionId, StringComparer.Ordinal).ToList());
            StatusEffects = statusEffects;
            Bindings = new ReadOnlyCollection<FactWindowStatusEffectBinding>(
                bindingItems.OrderBy(item => item.ConditionId, StringComparer.Ordinal).ToList());
            Fingerprint = ConditionLiveHash.Hash(
                DefinitionSetId + "|" + ContentVersion + "|"
                + string.Join(";", Conditions.Select(item => item.Fingerprint)) + "|"
                + StatusEffects.Fingerprint + "|"
                + string.Join(";", Bindings.Select(item => item.ConditionId + "|" + item.EffectId + "|" + item.SourceId)));
        }

        public string DefinitionSetId { get; }
        public string ContentVersion { get; }
        public IReadOnlyList<FactWindowConditionDefinition> Conditions { get; }
        public StatusEffectCatalog StatusEffects { get; }
        public IReadOnlyList<FactWindowStatusEffectBinding> Bindings { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionLiveParticipantDefinition
    {
        public ConditionLiveParticipantDefinition(
            StableId participantId,
            StableId characterId,
            StableId actorId,
            long actorLifecycleGeneration,
            string persistentSkillAllocationFingerprint,
            ConditionEffectLiveDefinition runtimeDefinition)
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
            Fingerprint = ConditionLiveHash.Hash(
                ParticipantId + "|" + CharacterId + "|" + ActorId + "|"
                + ActorLifecycleGeneration.ToString(CultureInfo.InvariantCulture) + "|"
                + PersistentSkillAllocationFingerprint + "|" + RuntimeDefinition.Fingerprint);
        }

        public StableId ParticipantId { get; }
        public StableId CharacterId { get; }
        public StableId ActorId { get; }
        public long ActorLifecycleGeneration { get; }
        public string PersistentSkillAllocationFingerprint { get; }
        public ConditionEffectLiveDefinition RuntimeDefinition { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionRunDefinition
    {
        public ConditionRunDefinition(
            ConditionRunLifecycleSnapshot lifecycle,
            IEnumerable<ConditionLiveParticipantDefinition> participants)
        {
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            List<ConditionLiveParticipantDefinition> items = (participants
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
            Participants = new ReadOnlyCollection<ConditionLiveParticipantDefinition>(
                items.OrderBy(item => item.ParticipantId.ToString(), StringComparer.Ordinal).ToList());
            Fingerprint = ConditionLiveHash.Hash(
                Lifecycle.Fingerprint + "|" + string.Join(";", Participants.Select(item => item.Fingerprint)));
        }

        public ConditionRunLifecycleSnapshot Lifecycle { get; }
        public IReadOnlyList<ConditionLiveParticipantDefinition> Participants { get; }
        public string Fingerprint { get; }
    }

    public sealed class AcceptedGameplayFactDelivery
    {
        public AcceptedGameplayFactDelivery(
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

    public sealed class ConditionObservedGameplayFact
    {
        public ConditionObservedGameplayFact(
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
            Fingerprint = ConditionLiveHash.Hash(ToCanonicalString());
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

        public LiveObservedFact ToObservedFact()
        {
            return new LiveObservedFact(
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

    public interface IAcceptedGameplayFactBridge
    {
        Type SourceFactRuntimeType { get; }
        string SourceFactTypeId { get; }
        bool TryAdapt(
            AcceptedGameplayFactDelivery delivery,
            out ConditionObservedGameplayFact observedFact,
            out string diagnosticCode);
    }

    public sealed class AcceptedGameplayFactBridgeRegistry
    {
        private readonly IReadOnlyDictionary<Type, IAcceptedGameplayFactBridge> adapters;

        public AcceptedGameplayFactBridgeRegistry(IEnumerable<IAcceptedGameplayFactBridge> registrations)
        {
            List<IAcceptedGameplayFactBridge> items = (registrations
                ?? throw new ArgumentNullException(nameof(registrations))).ToList();
            if (items.Count == 0 || items.Any(item => item == null))
                throw new ArgumentException("At least one non-null gameplay-fact adapter is required.", nameof(registrations));
            if (items.Select(item => item.SourceFactRuntimeType).Distinct().Count() != items.Count)
                throw new ArgumentException("Gameplay-fact runtime types must be registered once.", nameof(registrations));
            adapters = new ReadOnlyDictionary<Type, IAcceptedGameplayFactBridge>(
                items.ToDictionary(item => item.SourceFactRuntimeType));
            Fingerprint = ConditionLiveHash.Hash(string.Join(";", items
                .OrderBy(item => item.SourceFactRuntimeType.FullName, StringComparer.Ordinal)
                .Select(item => item.SourceFactRuntimeType.FullName + "|" + item.SourceFactTypeId)));
        }

        public string Fingerprint { get; }

        public bool TryResolve(Type runtimeType, out IAcceptedGameplayFactBridge adapter)
        {
            if (runtimeType == null)
            {
                adapter = null;
                return false;
            }
            return adapters.TryGetValue(runtimeType, out adapter);
        }
    }

    public enum ConditionFactIngestionStatus
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public sealed class ConditionFactIngestionResult
    {
        public ConditionFactIngestionResult(
            ConditionFactIngestionStatus status,
            string diagnosticCode,
            ConditionObservedGameplayFact observedFact,
            LiveObservedFactResult conditionResult,
            IEnumerable<StatusEffectCommandResult> effectResults,
            ConditionLiveSnapshot snapshot)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            ObservedFact = observedFact;
            ConditionResult = conditionResult;
            EffectResults = new ReadOnlyCollection<StatusEffectCommandResult>(
                (effectResults ?? Array.Empty<StatusEffectCommandResult>()).ToList());
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Fingerprint = ConditionLiveHash.Hash(
                ((int)Status).ToString(CultureInfo.InvariantCulture) + "|" + DiagnosticCode + "|"
                + (ObservedFact == null ? string.Empty : ObservedFact.Fingerprint) + "|"
                + (ConditionResult == null ? string.Empty : ConditionResult.LatestAcceptedTick.ToString(CultureInfo.InvariantCulture)) + "|"
                + string.Join(";", EffectResults.Select(item => item.Fingerprint)) + "|" + Snapshot.Fingerprint);
        }

        public ConditionFactIngestionStatus Status { get; }
        public string DiagnosticCode { get; }
        public ConditionObservedGameplayFact ObservedFact { get; }
        public LiveObservedFactResult ConditionResult { get; }
        public IReadOnlyList<StatusEffectCommandResult> EffectResults { get; }
        public ConditionLiveSnapshot Snapshot { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionParticipantSnapshot
    {
        public ConditionParticipantSnapshot(
            ConditionLiveParticipantDefinition definition,
            long latestConditionTick,
            IEnumerable<string> activeConditionIds,
            StatusEffectStateSnapshot statusEffects)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            LatestConditionTick = latestConditionTick;
            ActiveConditionIds = new ReadOnlyCollection<string>((activeConditionIds
                ?? Array.Empty<string>()).OrderBy(item => item, StringComparer.Ordinal).ToList());
            StatusEffects = statusEffects ?? throw new ArgumentNullException(nameof(statusEffects));
            Fingerprint = ConditionLiveHash.Hash(
                Definition.Fingerprint + "|" + LatestConditionTick.ToString(CultureInfo.InvariantCulture) + "|"
                + string.Join(";", ActiveConditionIds) + "|" + StatusEffects.Fingerprint);
        }

        public ConditionLiveParticipantDefinition Definition { get; }
        public long LatestConditionTick { get; }
        public IReadOnlyList<string> ActiveConditionIds { get; }
        public StatusEffectStateSnapshot StatusEffects { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionLiveSnapshot
    {
        public ConditionLiveSnapshot(
            ConditionRunDefinition definition,
            long authoritativeTick,
            IEnumerable<ConditionParticipantSnapshot> participants,
            IEnumerable<ConditionObservedGameplayFact> acceptedFacts)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            AuthoritativeTick = authoritativeTick;
            Participants = new ReadOnlyCollection<ConditionParticipantSnapshot>((participants
                ?? Array.Empty<ConditionParticipantSnapshot>())
                .OrderBy(item => item.Definition.ParticipantId.ToString(), StringComparer.Ordinal).ToList());
            AcceptedFacts = new ReadOnlyCollection<ConditionObservedGameplayFact>((acceptedFacts
                ?? Array.Empty<ConditionObservedGameplayFact>())
                .OrderBy(item => item.SourceFactId, StringComparer.Ordinal).ToList());
            Fingerprint = ConditionLiveHash.Hash(
                Definition.Fingerprint + "|" + AuthoritativeTick.ToString(CultureInfo.InvariantCulture) + "|"
                + string.Join(";", Participants.Select(item => item.Fingerprint)) + "|"
                + string.Join(";", AcceptedFacts.Select(item => item.Fingerprint)));
        }

        public ConditionRunDefinition Definition { get; }
        public long AuthoritativeTick { get; }
        public IReadOnlyList<ConditionParticipantSnapshot> Participants { get; }
        public IReadOnlyList<ConditionObservedGameplayFact> AcceptedFacts { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionRunReconstructionCommand
    {
        public ConditionRunReconstructionCommand(
            string operationId,
            StableId expectedRunId,
            long expectedRunGeneration,
            ConditionRunDefinition nextRun)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException("A reconstruction operation identity is required.", nameof(operationId));
            OperationId = operationId.Trim();
            ExpectedRunId = expectedRunId ?? throw new ArgumentNullException(nameof(expectedRunId));
            if (expectedRunGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(expectedRunGeneration));
            ExpectedRunGeneration = expectedRunGeneration;
            NextRun = nextRun ?? throw new ArgumentNullException(nameof(nextRun));
            Fingerprint = ConditionLiveHash.Hash(
                OperationId + "|" + ExpectedRunId + "|"
                + ExpectedRunGeneration.ToString(CultureInfo.InvariantCulture) + "|" + NextRun.Fingerprint);
        }

        public string OperationId { get; }
        public StableId ExpectedRunId { get; }
        public long ExpectedRunGeneration { get; }
        public ConditionRunDefinition NextRun { get; }
        public string Fingerprint { get; }
    }

    public sealed class ConditionRunReconstructionResult
    {
        public ConditionRunReconstructionResult(
            ConditionFactIngestionStatus status,
            string diagnosticCode,
            ConditionLiveSnapshot snapshot)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Fingerprint = ConditionLiveHash.Hash(
                ((int)Status).ToString(CultureInfo.InvariantCulture) + "|" + DiagnosticCode + "|" + Snapshot.Fingerprint);
        }

        public ConditionFactIngestionStatus Status { get; }
        public string DiagnosticCode { get; }
        public ConditionLiveSnapshot Snapshot { get; }
        public string Fingerprint { get; }
    }

    public sealed class FactWindowEffectFixture
    {
        public FactWindowEffectFixture(
            string conditionDefinitionId,
            string statusEffectDefinitionId,
            string observedFactTypeId,
            int requiredFactCount,
            long observationWindowTicks,
            long activeDurationTicks,
            decimal outgoingDamageMultiplier,
            StatusEffectStackingPolicy stackingPolicy = StatusEffectStackingPolicy.Ignore,
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
        public StatusEffectStackingPolicy StackingPolicy { get; }
        public int MaximumStacks { get; }

        public ConditionEffectLiveDefinition Build(
            string definitionSetId,
            string contentVersion,
            string bindingSourceId)
        {
            var condition = new FactWindowConditionDefinition(
                ConditionDefinitionId,
                ObservedFactTypeId,
                RequiredFactCount,
                ObservationWindowTicks,
                ActiveDurationTicks,
                true);
            var effect = new StatusEffectDefinition(
                StatusEffectDefinitionId,
                contentVersion,
                ActiveDurationTicks,
                MaximumStacks,
                StackingPolicy,
                "dispel-category.conditional",
                new[]
                {
                    new LiveModifierDefinition(
                        StatusEffectDefinitionId + ".outgoing-damage",
                        DerivedStatTargetIds.OutgoingDamageMultiplier,
                        LiveModifierOperation.Multiplicative,
                        OutgoingDamageMultiplier),
                });
            var catalog = new StatusEffectCatalog(
                definitionSetId + ".status-effects",
                contentVersion,
                new[] { effect });
            return new ConditionEffectLiveDefinition(
                definitionSetId,
                contentVersion,
                new[] { condition },
                catalog,
                new[]
                {
                    new FactWindowStatusEffectBinding(
                        ConditionDefinitionId,
                        StatusEffectDefinitionId,
                        bindingSourceId),
                });
        }
    }

    internal static class ConditionLiveHash
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

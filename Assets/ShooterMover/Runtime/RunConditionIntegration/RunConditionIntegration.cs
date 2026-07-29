using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Application.Runs.Session;
using ShooterMover.ConditionRuntime;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.RunConditionIntegration
{
    public sealed class RunConditionParticipantSeed
    {
        public RunConditionParticipantSeed(
            StableId participantStableId,
            StableId characterStableId,
            StableId actorStableId,
            long actorLifecycleGeneration,
            string persistentSkillAllocationFingerprint)
        {
            ParticipantStableId = participantStableId
                ?? throw new ArgumentNullException(nameof(participantStableId));
            CharacterStableId = characterStableId
                ?? throw new ArgumentNullException(nameof(characterStableId));
            ActorStableId = actorStableId
                ?? throw new ArgumentNullException(nameof(actorStableId));
            if (actorLifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actorLifecycleGeneration));
            }
            if (string.IsNullOrWhiteSpace(
                persistentSkillAllocationFingerprint))
            {
                throw new ArgumentException(
                    "A persistent skill-allocation fingerprint is required.",
                    nameof(persistentSkillAllocationFingerprint));
            }
            ActorLifecycleGeneration = actorLifecycleGeneration;
            PersistentSkillAllocationFingerprint =
                persistentSkillAllocationFingerprint.Trim();
        }

        public StableId ParticipantStableId { get; }
        public StableId CharacterStableId { get; }
        public StableId ActorStableId { get; }
        public long ActorLifecycleGeneration { get; }
        public string PersistentSkillAllocationFingerprint { get; }
    }

    public interface IRunConditionParticipantSeedProvider
    {
        IReadOnlyList<RunConditionParticipantSeed> Resolve(
            StableId runStableId,
            long lifecycleGeneration,
            FrozenCharacterRunInputs frozenInputs,
            IRunPlayerLivePort playerRuntime);
    }

    public sealed class SelectedPlayerRunConditionParticipantSeedProvider :
        IRunConditionParticipantSeedProvider
    {
        public IReadOnlyList<RunConditionParticipantSeed> Resolve(
            StableId runStableId,
            long lifecycleGeneration,
            FrozenCharacterRunInputs frozenInputs,
            IRunPlayerLivePort playerRuntime)
        {
            if (runStableId == null)
            {
                throw new ArgumentNullException(nameof(runStableId));
            }
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (frozenInputs == null)
            {
                throw new ArgumentNullException(nameof(frozenInputs));
            }
            if (playerRuntime == null)
            {
                throw new ArgumentNullException(nameof(playerRuntime));
            }
            RunPlayerLiveSnapshot player = playerRuntime.ExportSnapshot();
            if (player == null)
            {
                throw new InvalidOperationException(
                    "The player runtime did not export a run snapshot.");
            }
            if (player.LifecycleGeneration != playerRuntime.LifecycleGeneration)
            {
                throw new InvalidOperationException(
                    "The player runtime generation projection is split.");
            }
            return new ReadOnlyCollection<RunConditionParticipantSeed>(
                new List<RunConditionParticipantSeed>
                {
                    new RunConditionParticipantSeed(
                        player.ParticipantStableId,
                        frozenInputs.Character.CharacterInstanceStableId,
                        player.ActorInstanceStableId,
                        lifecycleGeneration,
                        frozenInputs.SkillSnapshot.Fingerprint),
                });
        }
    }

    public interface IRunConditionDefinitionProvider
    {
        ConditionEffectLiveDefinition Resolve(
            StableId runStableId,
            FrozenCharacterRunInputs frozenInputs,
            RunConditionParticipantSeed participant);
    }

    public sealed class RunSessionNonConditionLivePorts
    {
        public RunSessionNonConditionLivePorts(
            IRunPlayerLivePort player,
            IRunWeaponLivePort weapons,
            IRunActiveAbilityLivePort activeAbilities,
            IRunRoomLivePort rooms,
            IRunMissionResultPort missionResults)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            ActiveAbilities = activeAbilities
                ?? throw new ArgumentNullException(nameof(activeAbilities));
            Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            MissionResults = missionResults
                ?? throw new ArgumentNullException(nameof(missionResults));
            long generation = Player.LifecycleGeneration;
            if (generation <= 0L
                || Weapons.LifecycleGeneration != generation
                || ActiveAbilities.LifecycleGeneration != generation
                || Rooms.LifecycleGeneration != generation)
            {
                throw new ArgumentException(
                    "Non-condition run ports must share one positive lifecycle generation.");
            }
        }

        public IRunPlayerLivePort Player { get; }
        public IRunWeaponLivePort Weapons { get; }
        public IRunActiveAbilityLivePort ActiveAbilities { get; }
        public IRunRoomLivePort Rooms { get; }
        public IRunMissionResultPort MissionResults { get; }
    }

    public interface IRunSessionNonConditionLivePortFactory
    {
        RunSessionNonConditionLivePorts Create(
            StartRunSessionCommand command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputs frozenInputs);
    }

    public sealed class ConditionBoundRunSessionLivePortFactory :
        IRunSessionLivePortFactory
    {
        private readonly IRunSessionNonConditionLivePortFactory baseFactory;
        private readonly IRunConditionDefinitionProvider definitionProvider;
        private readonly IRunConditionParticipantSeedProvider participantProvider;
        private readonly ReadOnlyCollection<IAcceptedGameplayFactBridge>
            adapters;

        public ConditionBoundRunSessionLivePortFactory(
            IRunSessionNonConditionLivePortFactory baseFactory,
            IRunConditionDefinitionProvider definitionProvider,
            IRunConditionParticipantSeedProvider participantProvider = null,
            IEnumerable<IAcceptedGameplayFactBridge> adapters = null)
        {
            this.baseFactory = baseFactory
                ?? throw new ArgumentNullException(nameof(baseFactory));
            this.definitionProvider = definitionProvider
                ?? throw new ArgumentNullException(nameof(definitionProvider));
            this.participantProvider = participantProvider
                ?? new SelectedPlayerRunConditionParticipantSeedProvider();
            List<IAcceptedGameplayFactBridge> resolvedAdapters =
                (adapters ?? new IAcceptedGameplayFactBridge[]
                {
                    new EnemyDeathConditionFactBridge(),
                }).ToList();
            if (resolvedAdapters.Count < 1
                || resolvedAdapters.Any(item => item == null))
            {
                throw new ArgumentException(
                    "At least one gameplay-fact adapter is required.",
                    nameof(adapters));
            }
            this.adapters =
                new ReadOnlyCollection<IAcceptedGameplayFactBridge>(
                    resolvedAdapters);
        }

        public RunSessionLivePorts Create(
            StartRunSessionCommand command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputs frozenInputs)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            if (resolvedRunStableId == null)
            {
                throw new ArgumentNullException(nameof(resolvedRunStableId));
            }
            if (frozenInputs == null)
            {
                throw new ArgumentNullException(nameof(frozenInputs));
            }
            RunSessionNonConditionLivePorts basePorts =
                baseFactory.Create(command, resolvedRunStableId, frozenInputs);
            if (basePorts == null)
            {
                throw new InvalidOperationException(
                    "The non-condition runtime factory returned null.");
            }

            var conditionPort = new ExistingConditionLiveRunPort(
                resolvedRunStableId,
                command.AuthoritativeInitialTick,
                basePorts.Player.LifecycleGeneration,
                frozenInputs,
                basePorts.Player,
                definitionProvider,
                participantProvider,
                new AcceptedGameplayFactBridgeRegistry(adapters));
            var statusProjection =
                new ConditionOwnedStatusEffectRunPort(conditionPort);
            return new RunSessionLivePorts(
                basePorts.Player,
                basePorts.Weapons,
                statusProjection,
                conditionPort,
                basePorts.ActiveAbilities,
                basePorts.Rooms,
                basePorts.MissionResults);
        }
    }

    public sealed class ExistingConditionLiveRunPort :
        IRunConditionLivePort
    {
        private sealed class AdvancePresentationRecord
        {
            public AdvancePresentationRecord(
                string commandFingerprint,
                RunConditionAdvanceResult result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunConditionAdvanceResult Result { get; }
        }

        private sealed class OwningRunClock : IConditionRunClock
        {
            private readonly ExistingConditionLiveRunPort owner;

            public OwningRunClock(ExistingConditionLiveRunPort owner)
            {
                this.owner = owner;
            }

            public long CurrentTick
            {
                get { return owner.ProjectedTick; }
            }
        }

        private sealed class OwningRunLifecycle : IConditionRunLifecycle
        {
            private readonly ExistingConditionLiveRunPort owner;

            public OwningRunLifecycle(
                ExistingConditionLiveRunPort owner)
            {
                this.owner = owner;
            }

            public ConditionRunLifecycleSnapshot Current
            {
                get
                {
                    return new ConditionRunLifecycleSnapshot(
                        owner.runStableId,
                        owner.ProjectedGeneration);
                }
            }
        }

        private readonly StableId runStableId;
        private readonly FrozenCharacterRunInputs frozenInputs;
        private readonly IRunPlayerLivePort playerRuntime;
        private readonly IRunConditionDefinitionProvider definitionProvider;
        private readonly IRunConditionParticipantSeedProvider participantProvider;
        private readonly AcceptedGameplayFactBridgeRegistry adapters;
        private readonly Dictionary<string, AdvancePresentationRecord>
            advancePresentationReplay =
                new Dictionary<string, AdvancePresentationRecord>(
                    StringComparer.Ordinal);
        private readonly OwningRunClock clock;
        private readonly OwningRunLifecycle lifecycle;

        private RunSessionAggregate aggregate;
        private ConditionLiveState authority;
        private ConditionRunDefinition definition;
        private ConditionRunDefinition prevalidatedReplacement;
        private long? prevalidatedRetiringGeneration;
        private long? prevalidatedReplacementGeneration;
        private long? prevalidatedAuthoritativeTick;
        private long bootstrapTick;
        private long bootstrapGeneration;
        private long? projectedTickOverride;
        private long? projectedGenerationOverride;

        public ExistingConditionLiveRunPort(
            StableId runStableId,
            long authoritativeInitialTick,
            long lifecycleGeneration,
            FrozenCharacterRunInputs frozenInputs,
            IRunPlayerLivePort playerRuntime,
            IRunConditionDefinitionProvider definitionProvider,
            IRunConditionParticipantSeedProvider participantProvider,
            AcceptedGameplayFactBridgeRegistry adapters)
        {
            this.runStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (authoritativeInitialTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(authoritativeInitialTick));
            }
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            this.frozenInputs = frozenInputs
                ?? throw new ArgumentNullException(nameof(frozenInputs));
            this.playerRuntime = playerRuntime
                ?? throw new ArgumentNullException(nameof(playerRuntime));
            this.definitionProvider = definitionProvider
                ?? throw new ArgumentNullException(nameof(definitionProvider));
            this.participantProvider = participantProvider
                ?? throw new ArgumentNullException(nameof(participantProvider));
            this.adapters = adapters
                ?? throw new ArgumentNullException(nameof(adapters));
            bootstrapTick = authoritativeInitialTick;
            bootstrapGeneration = lifecycleGeneration;
            clock = new OwningRunClock(this);
            lifecycle = new OwningRunLifecycle(this);
            definition = BuildDefinition(lifecycleGeneration);
            authority = new ConditionLiveState(
                clock,
                lifecycle,
                adapters,
                definition);
        }

        public string PortId
        {
            get { return "condition-runtime-authority-v1"; }
        }

        public long LifecycleGeneration
        {
            get { return definition.Lifecycle.Generation; }
        }

        public string SnapshotFingerprint
        {
            get { return ExportConditionSnapshot().Fingerprint; }
        }

        public ConditionLiveState Authority
        {
            get { return authority; }
        }

        internal long ProjectedTick
        {
            get
            {
                if (projectedTickOverride.HasValue)
                {
                    return projectedTickOverride.Value;
                }
                return aggregate == null
                    ? bootstrapTick
                    : aggregate.AuthoritativeTick;
            }
        }

        internal long ProjectedGeneration
        {
            get
            {
                if (projectedGenerationOverride.HasValue)
                {
                    return projectedGenerationOverride.Value;
                }
                return aggregate == null
                    ? bootstrapGeneration
                    : aggregate.LifecycleGeneration;
            }
        }

        internal bool HasPrevalidatedRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            return prevalidatedReplacement != null
                && prevalidatedRetiringGeneration
                    == retiringLifecycleGeneration
                && prevalidatedReplacementGeneration
                    == replacementLifecycleGeneration
                && prevalidatedAuthoritativeTick == authoritativeTick;
        }

        public void Bind(RunSessionAggregate aggregate)
        {
            if (aggregate == null)
            {
                throw new ArgumentNullException(nameof(aggregate));
            }
            if (aggregate.RunStableId != runStableId)
            {
                throw new InvalidOperationException(
                    "The condition runtime cannot bind to another run.");
            }
            if (aggregate.FrozenInputs.Character.CharacterInstanceStableId
                != frozenInputs.Character.CharacterInstanceStableId)
            {
                throw new InvalidOperationException(
                    "The condition runtime cannot bind to another character.");
            }
            this.aggregate = aggregate;
            if (projectedGenerationOverride.HasValue
                && aggregate.LifecycleGeneration
                    >= projectedGenerationOverride.Value)
            {
                projectedGenerationOverride = null;
            }
            if (projectedTickOverride.HasValue
                && aggregate.AuthoritativeTick >= projectedTickOverride.Value)
            {
                projectedTickOverride = null;
            }
        }

        public RunConditionDeliveryResult Deliver(
            RunConditionGameplayFactCommand command)
        {
            if (command == null)
            {
                return DeliveryResult(
                    RunConditionDeliveryStatus.Rejected,
                    null,
                    "condition-run-port-delivery-null",
                    null);
            }
            if (command.RunStableId != runStableId)
            {
                return DeliveryResult(
                    RunConditionDeliveryStatus.WrongRun,
                    command,
                    "condition-run-port-wrong-run",
                    null);
            }
            if (command.RunLifecycleGeneration != LifecycleGeneration)
            {
                return DeliveryResult(
                    RunConditionDeliveryStatus.StaleLifecycle,
                    command,
                    command.RunLifecycleGeneration < LifecycleGeneration
                        ? "condition-run-port-stale-generation"
                        : "condition-run-port-future-generation",
                    null);
            }

            long previousTick = ProjectedTick;
            projectedTickOverride = Math.Max(
                previousTick,
                command.AuthoritativeTick);
            ConditionFactIngestionResult result = authority.Ingest(
                new AcceptedGameplayFactDelivery(
                    command.OperationStableId.ToString(),
                    command.SourceFact,
                    command.RunStableId,
                    command.RunLifecycleGeneration,
                    command.SourceActorStableId,
                    command.SubjectParticipantStableId,
                    command.SourceCharacterStableId,
                    command.SourceActorLifecycleGeneration,
                    command.AuthoritativeTick));
            RunConditionDeliveryStatus mapped = Map(result.Status);
            ConditionLiveSnapshot projectedSnapshot = result.Snapshot;
            if (mapped != RunConditionDeliveryStatus.Applied
                && mapped != RunConditionDeliveryStatus.ExactReplay)
            {
                projectedTickOverride = null;
                projectedSnapshot = authority.Snapshot;
            }
            return new RunConditionDeliveryResult(
                mapped,
                command,
                result.DiagnosticCode,
                Project(projectedSnapshot),
                result.Fingerprint);
        }

        public RunConditionAdvanceResult Advance(
            RunConditionAdvanceCommand command)
        {
            if (command == null)
            {
                return AdvanceResult(
                    RunConditionAdvanceStatus.Rejected,
                    null,
                    "condition-run-port-advance-null");
            }
            if (command.RunStableId != runStableId)
            {
                return AdvanceResult(
                    RunConditionAdvanceStatus.WrongRun,
                    command,
                    "condition-run-port-advance-wrong-run");
            }
            if (command.RunLifecycleGeneration != LifecycleGeneration)
            {
                return AdvanceResult(
                    RunConditionAdvanceStatus.StaleLifecycle,
                    command,
                    command.RunLifecycleGeneration < LifecycleGeneration
                        ? "condition-run-port-advance-stale-generation"
                        : "condition-run-port-advance-future-generation");
            }

            string operationId = command.OperationStableId.ToString();
            AdvancePresentationRecord existing;
            if (advancePresentationReplay.TryGetValue(operationId, out existing))
            {
                if (!string.Equals(
                    existing.CommandFingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return new RunConditionAdvanceResult(
                        RunConditionAdvanceStatus.ConflictingDuplicate,
                        command,
                        "condition-run-port-advance-operation-conflict",
                        existing.Result.Snapshot);
                }
                return new RunConditionAdvanceResult(
                    RunConditionAdvanceStatus.ExactReplay,
                    command,
                    existing.Result.DiagnosticCode,
                    existing.Result.Snapshot);
            }

            ConditionLiveSnapshot snapshot;
            try
            {
                snapshot = authority.Advance(operationId);
            }
            catch (InvalidOperationException exception)
            {
                return AdvanceResult(
                    exception.Message.IndexOf(
                        "reused with conflicting facts",
                        StringComparison.Ordinal) >= 0
                        ? RunConditionAdvanceStatus.ConflictingDuplicate
                        : RunConditionAdvanceStatus.Rejected,
                    command,
                    exception.Message);
            }
            var applied = new RunConditionAdvanceResult(
                RunConditionAdvanceStatus.Applied,
                command,
                string.Empty,
                Project(snapshot));
            advancePresentationReplay.Add(
                operationId,
                new AdvancePresentationRecord(command.Fingerprint, applied));
            return applied;
        }

        public RunConditionLiveSnapshot ExportConditionSnapshot()
        {
            return Project(authority.Snapshot);
        }

        public LiveModifierSnapshot ExportModifierProjection(
            StableId participantStableId)
        {
            if (participantStableId == null)
            {
                throw new ArgumentNullException(nameof(participantStableId));
            }
            ConditionParticipantSnapshot participant = authority.Snapshot
                .Participants.FirstOrDefault(item =>
                    item.Definition.ParticipantId == participantStableId);
            if (participant == null)
            {
                throw new InvalidOperationException(
                    "The condition participant is not part of this run.");
            }
            return participant.StatusEffects.ModifierProjection;
        }

        public string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            if (retiringLifecycleGeneration != LifecycleGeneration)
            {
                ClearPrevalidatedRestart();
                return retiringLifecycleGeneration < LifecycleGeneration
                    ? "condition-runtime-stale-generation"
                    : "condition-runtime-future-generation";
            }
            if (replacementLifecycleGeneration
                != retiringLifecycleGeneration + 1L)
            {
                ClearPrevalidatedRestart();
                return "condition-runtime-generation-invalid";
            }
            if (authoritativeTick < ProjectedTick)
            {
                ClearPrevalidatedRestart();
                return "condition-runtime-tick-regression";
            }
            if (HasPrevalidatedRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick))
            {
                return string.Empty;
            }
            try
            {
                prevalidatedReplacement = BuildDefinition(
                    replacementLifecycleGeneration);
                prevalidatedRetiringGeneration =
                    retiringLifecycleGeneration;
                prevalidatedReplacementGeneration =
                    replacementLifecycleGeneration;
                prevalidatedAuthoritativeTick = authoritativeTick;
            }
            catch (Exception exception)
            {
                ClearPrevalidatedRestart();
                return "condition-runtime-reconstruction-prevalidation-failed:"
                    + exception.GetType().Name;
            }
            return string.Empty;
        }

        public RunLivePortRestartResult Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = string.Empty;
            if (!HasPrevalidatedRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick))
            {
                rejection = ValidateRestart(
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration,
                    authoritativeTick);
            }
            if (!string.IsNullOrEmpty(rejection))
            {
                return new RunLivePortRestartResult(
                    false,
                    rejection,
                    LifecycleGeneration,
                    SnapshotFingerprint);
            }

            ConditionRunDefinition replacement = prevalidatedReplacement;
            projectedGenerationOverride = replacementLifecycleGeneration;
            projectedTickOverride = authoritativeTick;
            ConditionRunReconstructionResult result = authority.Reconstruct(
                new ConditionRunReconstructionCommand(
                    operationStableId + ":condition-reconstruct",
                    runStableId,
                    retiringLifecycleGeneration,
                    replacement));
            bool succeeded = result.Status
                    == ConditionFactIngestionStatus.Applied
                || result.Status
                    == ConditionFactIngestionStatus.ExactDuplicateNoChange;
            if (!succeeded)
            {
                projectedGenerationOverride = null;
                projectedTickOverride = null;
                ClearPrevalidatedRestart();
                return new RunLivePortRestartResult(
                    false,
                    result.DiagnosticCode,
                    LifecycleGeneration,
                    SnapshotFingerprint);
            }

            definition = replacement;
            bootstrapGeneration = replacementLifecycleGeneration;
            bootstrapTick = authoritativeTick;
            advancePresentationReplay.Clear();
            ClearPrevalidatedRestart();
            return new RunLivePortRestartResult(
                true,
                string.Empty,
                replacementLifecycleGeneration,
                Project(result.Snapshot).Fingerprint);
        }

        private ConditionRunDefinition BuildDefinition(long generation)
        {
            IReadOnlyList<RunConditionParticipantSeed> seeds =
                participantProvider.Resolve(
                    runStableId,
                    generation,
                    frozenInputs,
                    playerRuntime);
            if (seeds == null || seeds.Count < 1
                || seeds.Any(item => item == null))
            {
                throw new InvalidOperationException(
                    "Condition participants were not resolved.");
            }
            var participants =
                new List<ConditionLiveParticipantDefinition>();
            foreach (RunConditionParticipantSeed seed in seeds)
            {
                ConditionEffectLiveDefinition runtimeDefinition =
                    definitionProvider.Resolve(
                        runStableId,
                        frozenInputs,
                        seed);
                if (runtimeDefinition == null)
                {
                    throw new InvalidOperationException(
                        "A condition definition provider returned null.");
                }
                participants.Add(new ConditionLiveParticipantDefinition(
                    seed.ParticipantStableId,
                    seed.CharacterStableId,
                    seed.ActorStableId,
                    seed.ActorLifecycleGeneration,
                    seed.PersistentSkillAllocationFingerprint,
                    runtimeDefinition));
            }
            return new ConditionRunDefinition(
                new ConditionRunLifecycleSnapshot(runStableId, generation),
                participants);
        }

        private RunConditionLiveSnapshot Project(
            ConditionLiveSnapshot source)
        {
            if (source == null)
            {
                return null;
            }
            return new RunConditionLiveSnapshot(
                source.Definition.Lifecycle.RunId,
                source.Definition.Lifecycle.Generation,
                source.AuthoritativeTick,
                source.Definition.Fingerprint,
                source.Participants.Select(item =>
                    new RunConditionParticipantSnapshot(
                        item.Definition.ParticipantId,
                        item.Definition.CharacterId,
                        item.Definition.ActorId,
                        item.Definition.ActorLifecycleGeneration,
                        item.LatestConditionTick,
                        item.ActiveConditionIds,
                        item.StatusEffects.ActiveEffects.Count,
                        item.StatusEffects.Fingerprint,
                        item.StatusEffects.ModifierProjection)),
                source.AcceptedFacts.Count);
        }

        private RunConditionDeliveryResult DeliveryResult(
            RunConditionDeliveryStatus status,
            RunConditionGameplayFactCommand command,
            string diagnostic,
            string downstreamFingerprint)
        {
            return new RunConditionDeliveryResult(
                status,
                command,
                diagnostic,
                ExportConditionSnapshot(),
                downstreamFingerprint ?? string.Empty);
        }

        private RunConditionAdvanceResult AdvanceResult(
            RunConditionAdvanceStatus status,
            RunConditionAdvanceCommand command,
            string diagnostic)
        {
            return new RunConditionAdvanceResult(
                status,
                command,
                diagnostic,
                ExportConditionSnapshot());
        }

        private void ClearPrevalidatedRestart()
        {
            prevalidatedReplacement = null;
            prevalidatedRetiringGeneration = null;
            prevalidatedReplacementGeneration = null;
            prevalidatedAuthoritativeTick = null;
        }

        private static RunConditionDeliveryStatus Map(
            ConditionFactIngestionStatus status)
        {
            switch (status)
            {
                case ConditionFactIngestionStatus.Applied:
                    return RunConditionDeliveryStatus.Applied;
                case ConditionFactIngestionStatus.ExactDuplicateNoChange:
                    return RunConditionDeliveryStatus.ExactReplay;
                case ConditionFactIngestionStatus.ConflictingDuplicate:
                    return RunConditionDeliveryStatus.ConflictingDuplicate;
                default:
                    return RunConditionDeliveryStatus.Rejected;
            }
        }
    }

    public sealed class ConditionOwnedStatusEffectRunPort :
        IRunStatusEffectLivePort
    {
        private readonly ExistingConditionLiveRunPort conditionRuntime;

        public ExistingConditionLiveRunPort ConditionRuntime
        {
            get { return conditionRuntime; }
        }

        public ConditionOwnedStatusEffectRunPort(
            ExistingConditionLiveRunPort conditionRuntime)
        {
            this.conditionRuntime = conditionRuntime
                ?? throw new ArgumentNullException(nameof(conditionRuntime));
        }

        public string PortId
        {
            get { return "condition-owned-status-effect-runtime-v1"; }
        }

        public long LifecycleGeneration
        {
            get { return conditionRuntime.LifecycleGeneration; }
        }

        public string SnapshotFingerprint
        {
            get
            {
                RunConditionLiveSnapshot snapshot =
                    conditionRuntime.ExportConditionSnapshot();
                return PortId + "|" + snapshot.Fingerprint;
            }
        }

        public int ActiveEffectCount
        {
            get
            {
                return conditionRuntime.ExportConditionSnapshot()
                    .Participants.Sum(item => item.ActiveEffectCount);
            }
        }

        public string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            return conditionRuntime.ValidateRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
        }

        public RunLivePortRestartResult Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = string.Empty;
            if (!conditionRuntime.HasPrevalidatedRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick))
            {
                rejection = conditionRuntime.ValidateRestart(
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration,
                    authoritativeTick);
            }
            return new RunLivePortRestartResult(
                string.IsNullOrEmpty(rejection),
                rejection,
                string.IsNullOrEmpty(rejection)
                    ? replacementLifecycleGeneration
                    : LifecycleGeneration,
                SnapshotFingerprint);
        }
    }
}

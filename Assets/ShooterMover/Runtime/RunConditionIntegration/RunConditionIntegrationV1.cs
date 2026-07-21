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
    public sealed class RunConditionParticipantSeedV1
    {
        public RunConditionParticipantSeedV1(
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

    public interface IRunConditionParticipantSeedProviderV1
    {
        IReadOnlyList<RunConditionParticipantSeedV1> Resolve(
            StableId runStableId,
            long lifecycleGeneration,
            FrozenCharacterRunInputsV1 frozenInputs,
            IRunPlayerRuntimePortV1 playerRuntime);
    }

    public sealed class SelectedPlayerRunConditionParticipantSeedProviderV1 :
        IRunConditionParticipantSeedProviderV1
    {
        public IReadOnlyList<RunConditionParticipantSeedV1> Resolve(
            StableId runStableId,
            long lifecycleGeneration,
            FrozenCharacterRunInputsV1 frozenInputs,
            IRunPlayerRuntimePortV1 playerRuntime)
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
            RunPlayerRuntimeSnapshotV1 player = playerRuntime.ExportSnapshot();
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
            return new ReadOnlyCollection<RunConditionParticipantSeedV1>(
                new List<RunConditionParticipantSeedV1>
                {
                    new RunConditionParticipantSeedV1(
                        player.ParticipantStableId,
                        frozenInputs.Character.CharacterInstanceStableId,
                        player.ActorInstanceStableId,
                        lifecycleGeneration,
                        frozenInputs.SkillSnapshot.Fingerprint),
                });
        }
    }

    public interface IRunConditionDefinitionProviderV1
    {
        ConditionEffectRuntimeDefinitionV1 Resolve(
            StableId runStableId,
            FrozenCharacterRunInputsV1 frozenInputs,
            RunConditionParticipantSeedV1 participant);
    }

    public sealed class RunSessionNonConditionRuntimePortsV1
    {
        public RunSessionNonConditionRuntimePortsV1(
            IRunPlayerRuntimePortV1 player,
            IRunWeaponRuntimePortV1 weapons,
            IRunActiveAbilityRuntimePortV1 activeAbilities,
            IRunRoomRuntimePortV1 rooms,
            IRunMissionResultPortV1 missionResults)
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

        public IRunPlayerRuntimePortV1 Player { get; }
        public IRunWeaponRuntimePortV1 Weapons { get; }
        public IRunActiveAbilityRuntimePortV1 ActiveAbilities { get; }
        public IRunRoomRuntimePortV1 Rooms { get; }
        public IRunMissionResultPortV1 MissionResults { get; }
    }

    public interface IRunSessionNonConditionRuntimePortFactoryV1
    {
        RunSessionNonConditionRuntimePortsV1 Create(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputsV1 frozenInputs);
    }

    public sealed class ProductionConditionBoundRunSessionRuntimePortFactoryV1 :
        IRunSessionRuntimePortFactoryV1
    {
        private readonly IRunSessionNonConditionRuntimePortFactoryV1 baseFactory;
        private readonly IRunConditionDefinitionProviderV1 definitionProvider;
        private readonly IRunConditionParticipantSeedProviderV1 participantProvider;
        private readonly ReadOnlyCollection<IAcceptedGameplayFactAdapterV1>
            adapters;

        public ProductionConditionBoundRunSessionRuntimePortFactoryV1(
            IRunSessionNonConditionRuntimePortFactoryV1 baseFactory,
            IRunConditionDefinitionProviderV1 definitionProvider,
            IRunConditionParticipantSeedProviderV1 participantProvider = null,
            IEnumerable<IAcceptedGameplayFactAdapterV1> adapters = null)
        {
            this.baseFactory = baseFactory
                ?? throw new ArgumentNullException(nameof(baseFactory));
            this.definitionProvider = definitionProvider
                ?? throw new ArgumentNullException(nameof(definitionProvider));
            this.participantProvider = participantProvider
                ?? new SelectedPlayerRunConditionParticipantSeedProviderV1();
            List<IAcceptedGameplayFactAdapterV1> resolvedAdapters =
                (adapters ?? new IAcceptedGameplayFactAdapterV1[]
                {
                    new EnemyDeathConditionFactAdapterV1(),
                }).ToList();
            if (resolvedAdapters.Count < 1
                || resolvedAdapters.Any(item => item == null))
            {
                throw new ArgumentException(
                    "At least one gameplay-fact adapter is required.",
                    nameof(adapters));
            }
            this.adapters =
                new ReadOnlyCollection<IAcceptedGameplayFactAdapterV1>(
                    resolvedAdapters);
        }

        public RunSessionRuntimePortsV1 Create(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputsV1 frozenInputs)
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
            RunSessionNonConditionRuntimePortsV1 basePorts =
                baseFactory.Create(command, resolvedRunStableId, frozenInputs);
            if (basePorts == null)
            {
                throw new InvalidOperationException(
                    "The non-condition runtime factory returned null.");
            }

            var conditionPort = new ExistingConditionRuntimeRunPortV1(
                resolvedRunStableId,
                command.AuthoritativeInitialTick,
                basePorts.Player.LifecycleGeneration,
                frozenInputs,
                basePorts.Player,
                definitionProvider,
                participantProvider,
                new AcceptedGameplayFactAdapterRegistryV1(adapters));
            var statusProjection =
                new ConditionOwnedStatusEffectRunPortV1(conditionPort);
            return new RunSessionRuntimePortsV1(
                basePorts.Player,
                basePorts.Weapons,
                statusProjection,
                conditionPort,
                basePorts.ActiveAbilities,
                basePorts.Rooms,
                basePorts.MissionResults);
        }
    }

    public sealed class ExistingConditionRuntimeRunPortV1 :
        IRunConditionRuntimePortV1
    {
        private sealed class AdvancePresentationRecord
        {
            public AdvancePresentationRecord(
                string commandFingerprint,
                RunConditionAdvanceResultV1 result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunConditionAdvanceResultV1 Result { get; }
        }

        private sealed class OwningRunClockV1 : IConditionRunClockV1
        {
            private readonly ExistingConditionRuntimeRunPortV1 owner;

            public OwningRunClockV1(ExistingConditionRuntimeRunPortV1 owner)
            {
                this.owner = owner;
            }

            public long CurrentTick
            {
                get { return owner.ProjectedTick; }
            }
        }

        private sealed class OwningRunLifecycleV1 : IConditionRunLifecycleV1
        {
            private readonly ExistingConditionRuntimeRunPortV1 owner;

            public OwningRunLifecycleV1(
                ExistingConditionRuntimeRunPortV1 owner)
            {
                this.owner = owner;
            }

            public ConditionRunLifecycleSnapshotV1 Current
            {
                get
                {
                    return new ConditionRunLifecycleSnapshotV1(
                        owner.runStableId,
                        owner.ProjectedGeneration);
                }
            }
        }

        private readonly StableId runStableId;
        private readonly FrozenCharacterRunInputsV1 frozenInputs;
        private readonly IRunPlayerRuntimePortV1 playerRuntime;
        private readonly IRunConditionDefinitionProviderV1 definitionProvider;
        private readonly IRunConditionParticipantSeedProviderV1 participantProvider;
        private readonly AcceptedGameplayFactAdapterRegistryV1 adapters;
        private readonly Dictionary<string, AdvancePresentationRecord>
            advancePresentationReplay =
                new Dictionary<string, AdvancePresentationRecord>(
                    StringComparer.Ordinal);
        private readonly OwningRunClockV1 clock;
        private readonly OwningRunLifecycleV1 lifecycle;

        private RunSessionAggregateV1 aggregate;
        private ConditionRuntimeAuthorityV1 authority;
        private ConditionRunDefinitionV1 definition;
        private ConditionRunDefinitionV1 prevalidatedReplacement;
        private long? prevalidatedRetiringGeneration;
        private long? prevalidatedReplacementGeneration;
        private long? prevalidatedAuthoritativeTick;
        private long bootstrapTick;
        private long bootstrapGeneration;
        private long? projectedTickOverride;
        private long? projectedGenerationOverride;

        public ExistingConditionRuntimeRunPortV1(
            StableId runStableId,
            long authoritativeInitialTick,
            long lifecycleGeneration,
            FrozenCharacterRunInputsV1 frozenInputs,
            IRunPlayerRuntimePortV1 playerRuntime,
            IRunConditionDefinitionProviderV1 definitionProvider,
            IRunConditionParticipantSeedProviderV1 participantProvider,
            AcceptedGameplayFactAdapterRegistryV1 adapters)
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
            clock = new OwningRunClockV1(this);
            lifecycle = new OwningRunLifecycleV1(this);
            definition = BuildDefinition(lifecycleGeneration);
            authority = new ConditionRuntimeAuthorityV1(
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

        public ConditionRuntimeAuthorityV1 Authority
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

        public void Bind(RunSessionAggregateV1 aggregate)
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

        public RunConditionDeliveryResultV1 Deliver(
            RunConditionGameplayFactCommandV1 command)
        {
            if (command == null)
            {
                return DeliveryResult(
                    RunConditionDeliveryStatusV1.Rejected,
                    null,
                    "condition-run-port-delivery-null",
                    null);
            }
            if (command.RunStableId != runStableId)
            {
                return DeliveryResult(
                    RunConditionDeliveryStatusV1.WrongRun,
                    command,
                    "condition-run-port-wrong-run",
                    null);
            }
            if (command.RunLifecycleGeneration != LifecycleGeneration)
            {
                return DeliveryResult(
                    RunConditionDeliveryStatusV1.StaleLifecycle,
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
            ConditionFactIngestionResultV1 result = authority.Ingest(
                new AcceptedGameplayFactDeliveryV1(
                    command.OperationStableId.ToString(),
                    command.SourceFact,
                    command.RunStableId,
                    command.RunLifecycleGeneration,
                    command.SourceActorStableId,
                    command.SubjectParticipantStableId,
                    command.SourceCharacterStableId,
                    command.SourceActorLifecycleGeneration,
                    command.AuthoritativeTick));
            RunConditionDeliveryStatusV1 mapped = Map(result.Status);
            ConditionRuntimeSnapshotV1 projectedSnapshot = result.Snapshot;
            if (mapped != RunConditionDeliveryStatusV1.Applied
                && mapped != RunConditionDeliveryStatusV1.ExactReplay)
            {
                projectedTickOverride = null;
                projectedSnapshot = authority.Snapshot;
            }
            return new RunConditionDeliveryResultV1(
                mapped,
                command,
                result.DiagnosticCode,
                Project(projectedSnapshot),
                result.Fingerprint);
        }

        public RunConditionAdvanceResultV1 Advance(
            RunConditionAdvanceCommandV1 command)
        {
            if (command == null)
            {
                return AdvanceResult(
                    RunConditionAdvanceStatusV1.Rejected,
                    null,
                    "condition-run-port-advance-null");
            }
            if (command.RunStableId != runStableId)
            {
                return AdvanceResult(
                    RunConditionAdvanceStatusV1.WrongRun,
                    command,
                    "condition-run-port-advance-wrong-run");
            }
            if (command.RunLifecycleGeneration != LifecycleGeneration)
            {
                return AdvanceResult(
                    RunConditionAdvanceStatusV1.StaleLifecycle,
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
                    return new RunConditionAdvanceResultV1(
                        RunConditionAdvanceStatusV1.ConflictingDuplicate,
                        command,
                        "condition-run-port-advance-operation-conflict",
                        existing.Result.Snapshot);
                }
                return new RunConditionAdvanceResultV1(
                    RunConditionAdvanceStatusV1.ExactReplay,
                    command,
                    existing.Result.DiagnosticCode,
                    existing.Result.Snapshot);
            }

            ConditionRuntimeSnapshotV1 snapshot;
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
                        ? RunConditionAdvanceStatusV1.ConflictingDuplicate
                        : RunConditionAdvanceStatusV1.Rejected,
                    command,
                    exception.Message);
            }
            var applied = new RunConditionAdvanceResultV1(
                RunConditionAdvanceStatusV1.Applied,
                command,
                string.Empty,
                Project(snapshot));
            advancePresentationReplay.Add(
                operationId,
                new AdvancePresentationRecord(command.Fingerprint, applied));
            return applied;
        }

        public RunConditionRuntimeSnapshotV1 ExportConditionSnapshot()
        {
            return Project(authority.Snapshot);
        }

        public RuntimeModifierSnapshotV1 ExportModifierProjection(
            StableId participantStableId)
        {
            if (participantStableId == null)
            {
                throw new ArgumentNullException(nameof(participantStableId));
            }
            ConditionParticipantSnapshotV1 participant = authority.Snapshot
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

        public RunRuntimePortRestartResultV1 Restart(
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
                return new RunRuntimePortRestartResultV1(
                    false,
                    rejection,
                    LifecycleGeneration,
                    SnapshotFingerprint);
            }

            ConditionRunDefinitionV1 replacement = prevalidatedReplacement;
            projectedGenerationOverride = replacementLifecycleGeneration;
            projectedTickOverride = authoritativeTick;
            ConditionRunReconstructionResultV1 result = authority.Reconstruct(
                new ConditionRunReconstructionCommandV1(
                    operationStableId + ":condition-reconstruct",
                    runStableId,
                    retiringLifecycleGeneration,
                    replacement));
            bool succeeded = result.Status
                    == ConditionFactIngestionStatusV1.Applied
                || result.Status
                    == ConditionFactIngestionStatusV1.ExactDuplicateNoChange;
            if (!succeeded)
            {
                projectedGenerationOverride = null;
                projectedTickOverride = null;
                ClearPrevalidatedRestart();
                return new RunRuntimePortRestartResultV1(
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
            return new RunRuntimePortRestartResultV1(
                true,
                string.Empty,
                replacementLifecycleGeneration,
                Project(result.Snapshot).Fingerprint);
        }

        private ConditionRunDefinitionV1 BuildDefinition(long generation)
        {
            IReadOnlyList<RunConditionParticipantSeedV1> seeds =
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
                new List<ConditionRuntimeParticipantDefinitionV1>();
            foreach (RunConditionParticipantSeedV1 seed in seeds)
            {
                ConditionEffectRuntimeDefinitionV1 runtimeDefinition =
                    definitionProvider.Resolve(
                        runStableId,
                        frozenInputs,
                        seed);
                if (runtimeDefinition == null)
                {
                    throw new InvalidOperationException(
                        "A condition definition provider returned null.");
                }
                participants.Add(new ConditionRuntimeParticipantDefinitionV1(
                    seed.ParticipantStableId,
                    seed.CharacterStableId,
                    seed.ActorStableId,
                    seed.ActorLifecycleGeneration,
                    seed.PersistentSkillAllocationFingerprint,
                    runtimeDefinition));
            }
            return new ConditionRunDefinitionV1(
                new ConditionRunLifecycleSnapshotV1(runStableId, generation),
                participants);
        }

        private RunConditionRuntimeSnapshotV1 Project(
            ConditionRuntimeSnapshotV1 source)
        {
            if (source == null)
            {
                return null;
            }
            return new RunConditionRuntimeSnapshotV1(
                source.Definition.Lifecycle.RunId,
                source.Definition.Lifecycle.Generation,
                source.AuthoritativeTick,
                source.Definition.Fingerprint,
                source.Participants.Select(item =>
                    new RunConditionParticipantSnapshotV1(
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

        private RunConditionDeliveryResultV1 DeliveryResult(
            RunConditionDeliveryStatusV1 status,
            RunConditionGameplayFactCommandV1 command,
            string diagnostic,
            string downstreamFingerprint)
        {
            return new RunConditionDeliveryResultV1(
                status,
                command,
                diagnostic,
                ExportConditionSnapshot(),
                downstreamFingerprint ?? string.Empty);
        }

        private RunConditionAdvanceResultV1 AdvanceResult(
            RunConditionAdvanceStatusV1 status,
            RunConditionAdvanceCommandV1 command,
            string diagnostic)
        {
            return new RunConditionAdvanceResultV1(
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

        private static RunConditionDeliveryStatusV1 Map(
            ConditionFactIngestionStatusV1 status)
        {
            switch (status)
            {
                case ConditionFactIngestionStatusV1.Applied:
                    return RunConditionDeliveryStatusV1.Applied;
                case ConditionFactIngestionStatusV1.ExactDuplicateNoChange:
                    return RunConditionDeliveryStatusV1.ExactReplay;
                case ConditionFactIngestionStatusV1.ConflictingDuplicate:
                    return RunConditionDeliveryStatusV1.ConflictingDuplicate;
                default:
                    return RunConditionDeliveryStatusV1.Rejected;
            }
        }
    }

    public sealed class ConditionOwnedStatusEffectRunPortV1 :
        IRunStatusEffectRuntimePortV1
    {
        private readonly ExistingConditionRuntimeRunPortV1 conditionRuntime;

        public ExistingConditionRuntimeRunPortV1 ConditionRuntime
        {
            get { return conditionRuntime; }
        }

        public ConditionOwnedStatusEffectRunPortV1(
            ExistingConditionRuntimeRunPortV1 conditionRuntime)
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
                RunConditionRuntimeSnapshotV1 snapshot =
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

        public RunRuntimePortRestartResultV1 Restart(
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
            return new RunRuntimePortRestartResultV1(
                string.IsNullOrEmpty(rejection),
                rejection,
                string.IsNullOrEmpty(rejection)
                    ? replacementLifecycleGeneration
                    : LifecycleGeneration,
                SnapshotFingerprint);
        }
    }
}

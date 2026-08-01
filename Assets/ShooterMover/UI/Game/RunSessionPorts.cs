using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.UnityAdapters.Missions.Rooms;

namespace ShooterMover.UI.Game
{
    internal sealed class LevelStats : IRunStatInputResolver
    {
        private readonly PlayableLevelDefinition level;
        private readonly ProgressionContext frozenProgression;

        public LevelStats(
            PlayableLevelDefinition level,
            ProgressionContext frozenProgression)
        {
            this.level = level ?? throw new ArgumentNullException(nameof(level));
            this.frozenProgression = frozenProgression
                ?? throw new ArgumentNullException(nameof(frozenProgression));
        }

        public RunStatInputResolution Resolve(
            StartRunSessionCommand command,
            StableId resolvedRunStableId,
            CharacterLiveGraph characterGraph,
            ShooterMover.Domain.Persistence.Accounts.CharacterInstanceSnapshot character,
            PlayerRouteProfilePayload currentRoutePayload,
            RankedSkillAllocationSnapshot skillSnapshot,
            IReadOnlyList<FrozenRunEquipment> frozenEquipment)
        {
            if (command == null
                || resolvedRunStableId == null
                || characterGraph == null
                || character == null
                || frozenProgression.CharacterLevel < 1
                || frozenProgression.DifficultyId != command.DifficultyStableId)
            {
                throw new InvalidOperationException(
                    "The frozen selected-character run context is unavailable or mismatched.");
            }
            var values = new Dictionary<string, decimal>
            {
                { DerivedStatTargetIds.MaximumHealth, 100m },
                { DerivedStatTargetIds.MovementSpeed, 6m },
            };
            return new RunStatInputResolution(
                new DerivedCharacterStatInput(
                    character.CharacterInstanceStableId.ToString(),
                    new CharacterBaseStatProfile(
                        "base-profile.production-playable-level",
                        character.ClassDefinitionStableId.ToString(),
                        frozenProgression.CharacterLevel,
                        RunFingerprint.Hash(
                            "production-playable-level-base-v1|"
                            + level.LevelStableId + "|"
                            + character.ClassDefinitionStableId + "|"
                            + frozenProgression.Fingerprint),
                        values),
                    Array.Empty<DerivedStatModifierSource>(),
                    DerivedStatPolicy.CreateDefault()),
                Array.Empty<DerivedStatModifierSource>(),
                Array.Empty<string>());
        }
    }

    internal sealed class LevelPorts : IRunSessionLivePortFactory
    {
        private readonly LevelRooms rooms;
        private readonly RunJournal missionProjection;
        private readonly ExistingMissionResultRunPort missionResults;

        public LevelPorts(
            LevelRooms rooms,
            CharacterLiveGraph graph)
        {
            this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            missionProjection = new RunJournal(graph);
            missionResults = new ExistingMissionResultRunPort(
                new MissionRunResultState(missionProjection));
        }

        public void BindRun(RunSessionAggregate run)
        {
            missionProjection.BindRun(run);
        }

        public ShooterMover.Contracts.Missions.Results.MissionResultPayload
            RefreshMissionResult(
                ShooterMover.Contracts.Missions.Results.MissionResultPayload prior)
        {
            return missionProjection.Refresh(prior);
        }

        public RunSessionLivePorts Create(
            StartRunSessionCommand command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputs frozenInputs)
        {
            const long generation = 1L;
            return new RunSessionLivePorts(
                new PlayerPort(
                    generation,
                    resolvedRunStableId,
                    (double)frozenInputs.CombatProfile.MaximumHealth),
                new GunPort(
                    generation,
                    frozenInputs.Equipment
                        .Where(item => item.EquipmentDefinition.CategoryId
                            == EquipmentCategoryIds.Gun)
                        .Select(item => item.EquipmentInstanceStableId)),
                new StatusPort(generation),
                new CondPort(generation),
                new AbilityPort(generation),
                new RoomPort(generation, rooms),
                missionResults);
        }
    }

    internal abstract class RunPort : IRunLifecycleLivePort
    {
        protected RunPort(string portId, long generation)
        {
            PortId = portId;
            LifecycleGeneration = generation;
        }
        public string PortId { get; }
        public long LifecycleGeneration { get; }
        public virtual string SnapshotFingerprint
        {
            get { return RunFingerprint.Hash(PortId + "|" + LifecycleGeneration); }
        }
        public string ValidateRestart(long retiring, long replacement, long tick)
        {
            return "playable-run-restart-not-composed";
        }
        public RunLivePortRestartResult Restart(
            StableId operation, long retiring, long replacement, long tick)
        {
            return new RunLivePortRestartResult(
                false,
                ValidateRestart(retiring, replacement, tick),
                LifecycleGeneration,
                SnapshotFingerprint);
        }
    }

    internal sealed class PlayerPort : RunPort, IRunPlayerLivePort
    {
        private readonly StableId actorId;
        private readonly StableId participantId;
        private readonly double health;

        public PlayerPort(long generation, StableId runId, double health)
            : base("production-playable-player-projection", generation)
        {
            actorId = StableId.Create("run-actor", runId.Value);
            participantId = StableId.Create("run-participant", runId.Value);
            this.health = health;
        }

        public RunPlayerSnapshot ExportSnapshot()
        {
            return new RunPlayerSnapshot(
                actorId, participantId, LifecycleGeneration,
                health, health, 0d, 0d, 0L);
        }

        public override string SnapshotFingerprint
        {
            get { return ExportSnapshot().Fingerprint; }
        }
    }

    internal sealed class GunPort : RunPort, IRunGunLivePort
    {
        private readonly IReadOnlyList<StableId> ids;

        public GunPort(long generation, IEnumerable<StableId> ids)
            : base("production-playable-gun-projection", generation)
        {
            this.ids = ids.OrderBy(value => value).ToList().AsReadOnly();
        }

        public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
        {
            get { return ids; }
        }
    }

    internal sealed class StatusPort : RunPort, IRunStatusEffectLivePort
    {
        public StatusPort(long generation)
            : base("production-playable-status-projection", generation) { }

        public int ActiveEffectCount { get { return 0; } }
    }

    internal sealed class CondPort : RunPort, IRunConditionalFactLivePort
    {
        public CondPort(long generation)
            : base("production-playable-condition-projection", generation) { }
    }

    internal sealed class AbilityPort : RunPort, IRunActiveAbilityLivePort
    {
        public AbilityPort(long generation)
            : base("production-playable-ability-projection", generation) { }
    }

    internal sealed class RoomPort : RunPort, IRunRoomLivePort
    {
        private readonly LevelRooms rooms;

        public RoomPort(long generation, LevelRooms rooms)
            : base("production-playable-room-projection", generation)
        {
            this.rooms = rooms;
        }

        public StableId CurrentRoomStableId
        {
            get { return rooms.CurrentRoomStableId; }
        }

        public override string SnapshotFingerprint
        {
            get
            {
                return RunFingerprint.Hash(
                    PortId + "|" + LifecycleGeneration + "|" + CurrentRoomStableId);
            }
        }
    }
}

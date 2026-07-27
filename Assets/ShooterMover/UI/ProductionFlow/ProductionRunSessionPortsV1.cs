using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.UnityAdapters.Missions.Rooms;

namespace ShooterMover.UI.ProductionFlow
{
    internal sealed class ProductionPlayableLevelStatInputResolverV1 :
        IProductionRunStatInputResolverV1
    {
        private readonly ProductionPlayableLevelDefinitionV1 level;
        public ProductionPlayableLevelStatInputResolverV1(
            ProductionPlayableLevelDefinitionV1 level)
        {
            this.level = level ?? throw new ArgumentNullException(nameof(level));
        }

        public ProductionRunStatInputResolutionV1 Resolve(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            ProductionCharacterRuntimeGraphV1 characterGraph,
            ShooterMover.Domain.Persistence.Accounts.CharacterInstanceSnapshotV1 character,
            PlayerRouteProfilePayloadV1 currentRoutePayload,
            RankedSkillAllocationSnapshotV2 skillSnapshot,
            IReadOnlyList<FrozenRunEquipmentV1> frozenEquipment)
        {
            if (level.LevelStableId != ProductionPlayableLevelCatalogV1.FirstLevelStableId)
            {
                throw new InvalidOperationException(
                    "No authored run-stat baseline exists for level " + level.LevelStableId);
            }
            ProgressionContext progression = characterGraph.ExperienceAuthority.CurrentContext;
            if (progression == null || progression.PlayerLevel < 1)
            {
                throw new InvalidOperationException(
                    "The selected character progression context is unavailable.");
            }
            var values = new Dictionary<string, decimal>
            {
                { DerivedStatTargetIdsV1.MaximumHealth, 100m },
                { DerivedStatTargetIdsV1.MovementSpeed, 6m },
            };
            return new ProductionRunStatInputResolutionV1(
                new DerivedCharacterStatInputV1(
                    character.CharacterInstanceStableId.ToString(),
                    new CharacterBaseStatProfileV1(
                        "base-profile.production-playable-level-1",
                        character.ClassDefinitionStableId.ToString(),
                        progression.PlayerLevel,
                        RunSessionFingerprintV1.Hash(
                            "production-playable-level-1-base-v1|"
                            + character.ClassDefinitionStableId),
                        values),
                    Array.Empty<DerivedStatModifierSourceV1>(),
                    DerivedStatPolicyV1.CreateDefault()),
                Array.Empty<DerivedStatModifierSourceV1>(),
                Array.Empty<string>());
        }
    }

    internal sealed class ProductionPlayableLevelRuntimePortFactoryV1 :
        IRunSessionRuntimePortFactoryV1
    {
        private readonly RoomRuntimeComposition2D rooms;
        public ProductionPlayableLevelRuntimePortFactoryV1(RoomRuntimeComposition2D rooms)
        {
            this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        }

        public RunSessionRuntimePortsV1 Create(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputsV1 frozenInputs)
        {
            const long generation = 1L;
            return new RunSessionRuntimePortsV1(
                new SnapshotPlayerRunPortV1(
                    generation,
                    resolvedRunStableId,
                    (double)frozenInputs.CombatProfile.MaximumHealth),
                new SnapshotWeaponRunPortV1(
                    generation,
                    frozenInputs.Equipment
                        .Where(item => item.EquipmentDefinition.CategoryId
                            == EquipmentCategoryIds.Weapon)
                        .Select(item => item.EquipmentInstanceStableId)),
                new SnapshotStatusRunPortV1(generation),
                new SnapshotConditionalRunPortV1(generation),
                new SnapshotAbilityRunPortV1(generation),
                new SnapshotRoomRunPortV1(generation, rooms),
                new UnsupportedMissionResultRunPortV1());
        }
    }

    internal abstract class ImmutableRunLifecyclePortV1 : IRunLifecycleRuntimePortV1
    {
        protected ImmutableRunLifecyclePortV1(string portId, long generation)
        {
            PortId = portId;
            LifecycleGeneration = generation;
        }
        public string PortId { get; }
        public long LifecycleGeneration { get; }
        public virtual string SnapshotFingerprint
        {
            get { return RunSessionFingerprintV1.Hash(PortId + "|" + LifecycleGeneration); }
        }
        public string ValidateRestart(long retiring, long replacement, long tick)
        {
            return "playable-run-restart-not-composed";
        }
        public RunRuntimePortRestartResultV1 Restart(
            StableId operation, long retiring, long replacement, long tick)
        {
            return new RunRuntimePortRestartResultV1(
                false,
                ValidateRestart(retiring, replacement, tick),
                LifecycleGeneration,
                SnapshotFingerprint);
        }
    }

    internal sealed class SnapshotPlayerRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunPlayerRuntimePortV1
    {
        private readonly StableId actorId;
        private readonly StableId participantId;
        private readonly double health;
        public SnapshotPlayerRunPortV1(long generation, StableId runId, double health)
            : base("production-playable-player-projection", generation)
        {
            actorId = StableId.Create("run-actor", runId.Value);
            participantId = StableId.Create("run-participant", runId.Value);
            this.health = health;
        }
        public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
        {
            return new RunPlayerRuntimeSnapshotV1(
                actorId, participantId, LifecycleGeneration,
                health, health, 0d, 0d, 0L);
        }
        public override string SnapshotFingerprint { get { return ExportSnapshot().Fingerprint; } }
    }

    internal sealed class SnapshotWeaponRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunWeaponRuntimePortV1
    {
        private readonly IReadOnlyList<StableId> ids;
        public SnapshotWeaponRunPortV1(long generation, IEnumerable<StableId> ids)
            : base("production-playable-weapon-projection", generation)
        {
            this.ids = ids.OrderBy(value => value).ToList().AsReadOnly();
        }
        public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds { get { return ids; } }
    }

    internal sealed class SnapshotStatusRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunStatusEffectRuntimePortV1
    {
        public SnapshotStatusRunPortV1(long generation)
            : base("production-playable-status-projection", generation) { }
        public int ActiveEffectCount { get { return 0; } }
    }

    internal sealed class SnapshotConditionalRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunConditionalFactRuntimePortV1
    {
        public SnapshotConditionalRunPortV1(long generation)
            : base("production-playable-condition-projection", generation) { }
    }

    internal sealed class SnapshotAbilityRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunActiveAbilityRuntimePortV1
    {
        public SnapshotAbilityRunPortV1(long generation)
            : base("production-playable-ability-projection", generation) { }
    }

    internal sealed class SnapshotRoomRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunRoomRuntimePortV1
    {
        private readonly RoomRuntimeComposition2D rooms;
        public SnapshotRoomRunPortV1(long generation, RoomRuntimeComposition2D rooms)
            : base("production-playable-room-projection", generation)
        {
            this.rooms = rooms;
        }
        public StableId CurrentRoomStableId { get { return rooms.CurrentRoomStableId; } }
        public override string SnapshotFingerprint
        {
            get
            {
                return RunSessionFingerprintV1.Hash(
                    PortId + "|" + LifecycleGeneration + "|" + CurrentRoomStableId);
            }
        }
    }

    internal sealed class UnsupportedMissionResultRunPortV1 : IRunMissionResultPortV1
    {
        public long Sequence { get { return 0L; } }
        public bool TryGetRun(StableId runStableId, out MissionRunPayloadV1 runPayload)
        {
            runPayload = null;
            return false;
        }
        public MissionRunAuthorityResultV1 RecordCollectedStrongbox(
            RunStrongboxCollectionRequestV1 request,
            PlayerRouteProfilePayloadV1 routePayload)
        {
            return Invalid(
                request == null ? null : request.OperationStableId,
                request == null ? string.Empty : request.Fingerprint);
        }
        public MissionRunAuthorityResultV1 EndRun(
            EndRunSessionCommandV1 command,
            PlayerRouteProfilePayloadV1 routePayload)
        {
            return Invalid(
                command == null ? null : command.OperationStableId,
                command == null ? string.Empty : command.Fingerprint);
        }
        private static MissionRunAuthorityResultV1 Invalid(
            StableId operation,
            string fingerprint)
        {
            return new MissionRunAuthorityResultV1(
                MissionRunAuthorityStatusV1.InvalidRequest,
                0L,
                0L,
                operation,
                fingerprint,
                null,
                null,
                null,
                "run-results-not-composed");
        }
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Players;
using ShooterMover.UnityAdapters.Production.Level1;
using ShooterMover.UnityAdapters.Weapons.Live;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Resolves the selected character's Run Session stat input while preserving the live player
    /// authority's already composed maximum health. All other modifiers remain owned by the
    /// existing derived-stat composer and selected-character graph.
    /// </summary>
    internal sealed class Stage1ProductionRunStatInputResolverV1 :
        IProductionRunStatInputResolverV1
    {
        private readonly Level1PlayerRuntimeSceneAdapterV1 player;

        public Stage1ProductionRunStatInputResolverV1(
            Level1PlayerRuntimeSceneAdapterV1 player)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public ProductionRunStatInputResolutionV1 Resolve(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            ProductionCharacterRuntimeGraphV1 characterGraph,
            ShooterMover.Domain.Persistence.Accounts.CharacterInstanceSnapshotV1 character,
            ShooterMover.Contracts.Flow.Session.PlayerRouteProfilePayloadV1 currentRoutePayload,
            RankedSkillAllocationSnapshotV2 skillSnapshot,
            IReadOnlyList<FrozenRunEquipmentV1> frozenEquipment)
        {
            PlayerRuntimeSnapshot runtime = player.ExportSnapshot();
            if (runtime == null || runtime.Player == null)
            {
                return ProductionRunStatInputResolutionV1.Reject(
                    "stage1-run-player-snapshot-unavailable");
            }
            var baseValues = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                {
                    DerivedStatTargetIdsV1.MaximumHealth,
                    Convert.ToDecimal(runtime.Player.MaximumHealth,
                        CultureInfo.InvariantCulture)
                },
                {
                    DerivedStatTargetIdsV1.MovementSpeed,
                    6m
                },
            };
            return new ProductionRunStatInputResolutionV1(
                new DerivedCharacterStatInputV1(
                    character.CharacterInstanceStableId.ToString(),
                    new CharacterBaseStatProfileV1(
                        "base-profile.stage1-production",
                        character.ClassDefinitionStableId.ToString(),
                        1,
                        character.Fingerprint,
                        baseValues),
                    null,
                    DerivedStatPolicyV1.CreateDefault()),
                null,
                null);
        }
    }

    internal abstract class Stage1RunLifecycleProjectionV1 :
        IRunLifecycleRuntimePortV1
    {
        private long lifecycleGeneration;

        protected Stage1RunLifecycleProjectionV1(
            string portId,
            long lifecycleGeneration)
        {
            if (string.IsNullOrWhiteSpace(portId))
            {
                throw new ArgumentException(
                    "A Stage 1 run-port identity is required.",
                    nameof(portId));
            }
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            PortId = portId.Trim();
            this.lifecycleGeneration = lifecycleGeneration;
        }

        public string PortId { get; }

        public virtual long LifecycleGeneration
        {
            get { return lifecycleGeneration; }
        }

        public virtual string SnapshotFingerprint
        {
            get
            {
                return RunSessionFingerprintV1.Hash(
                    PortId
                    + "|"
                    + LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture));
            }
        }

        public virtual string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            if (retiringLifecycleGeneration != LifecycleGeneration)
            {
                return retiringLifecycleGeneration < LifecycleGeneration
                    ? PortId + "-stale-generation"
                    : PortId + "-future-generation";
            }
            return replacementLifecycleGeneration
                    == retiringLifecycleGeneration + 1L
                ? string.Empty
                : PortId + "-replacement-generation-invalid";
        }

        public virtual RunRuntimePortRestartResultV1 Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = ValidateRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
            if (!string.IsNullOrEmpty(rejection))
            {
                return new RunRuntimePortRestartResultV1(
                    false,
                    rejection,
                    LifecycleGeneration,
                    SnapshotFingerprint);
            }
            lifecycleGeneration = replacementLifecycleGeneration;
            return new RunRuntimePortRestartResultV1(
                true,
                string.Empty,
                LifecycleGeneration,
                SnapshotFingerprint);
        }

        protected void CommitGeneration(long replacementLifecycleGeneration)
        {
            lifecycleGeneration = replacementLifecycleGeneration;
        }
    }

    internal sealed class Stage1PlayerRunPortV1 :
        Stage1RunLifecycleProjectionV1,
        IRunPlayerRuntimePortV1
    {
        private readonly Level1PlayerRuntimeSceneAdapterV1 player;

        public Stage1PlayerRunPortV1(
            Level1PlayerRuntimeSceneAdapterV1 player)
            : base(
                "stage1-player-runtime",
                ReadGeneration(player))
        {
            this.player = player;
        }

        public override long LifecycleGeneration
        {
            get { return ReadGeneration(player); }
        }

        public override string SnapshotFingerprint
        {
            get { return ExportSnapshot().Fingerprint; }
        }

        public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
        {
            PlayerRuntimeSnapshot snapshot = player.ExportSnapshot();
            return new RunPlayerRuntimeSnapshotV1(
                snapshot.Player.ActorInstanceId,
                snapshot.Player.RunParticipantId,
                snapshot.Player.LifecycleGeneration,
                snapshot.Player.CurrentHealth,
                snapshot.Player.MaximumHealth,
                snapshot.Movement.PositionX,
                snapshot.Movement.PositionY,
                snapshot.Player.AcceptedSequence);
        }

        public override RunRuntimePortRestartResultV1 Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = ValidateRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
            if (!string.IsNullOrEmpty(rejection))
            {
                return new RunRuntimePortRestartResultV1(
                    false,
                    rejection,
                    LifecycleGeneration,
                    SnapshotFingerprint);
            }
            PlayerRuntimeSnapshot before = player.ExportSnapshot();
            PlayerRuntimeRestartResult result = player.ApplyRestartCommand(
                new PlayerRuntimeRestartCommand(
                    operationStableId,
                    before.Player.ActorInstanceId,
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration));
            bool accepted = result != null
                && (result.Status == PlayerRuntimeRestartStatus.Applied
                    || result.Status == PlayerRuntimeRestartStatus.Duplicate);
            return new RunRuntimePortRestartResultV1(
                accepted,
                accepted
                    ? string.Empty
                    : (result == null
                        ? "stage1-player-restart-null"
                        : "stage1-player-restart-" + result.RejectionCode),
                LifecycleGeneration,
                SnapshotFingerprint);
        }

        private static long ReadGeneration(
            Level1PlayerRuntimeSceneAdapterV1 player)
        {
            if (player == null || !player.IsInitialized)
            {
                throw new InvalidOperationException(
                    "The Stage 1 player runtime is unavailable.");
            }
            return player.ExportSnapshot().Player.LifecycleGeneration;
        }
    }

    internal sealed class Stage1WeaponRunPortV1 :
        Stage1RunLifecycleProjectionV1,
        IRunWeaponRuntimePortV1
    {
        private readonly IReadOnlyList<StableId> equipmentIds;
        private readonly Action clearTransientEffects;

        public Stage1WeaponRunPortV1(
            long lifecycleGeneration,
            IEnumerable<StableId> frozenEquipmentInstanceStableIds,
            Action clearTransientEffects)
            : base("stage1-inventory-weapon-runtime", lifecycleGeneration)
        {
            this.clearTransientEffects = clearTransientEffects
                ?? throw new ArgumentNullException(nameof(clearTransientEffects));
            List<StableId> ids = (frozenEquipmentInstanceStableIds
                    ?? throw new ArgumentNullException(
                        nameof(frozenEquipmentInstanceStableIds)))
                .Where(item => item != null)
                .Distinct()
                .OrderBy(item => item)
                .ToList();
            if (ids.Count == 0)
            {
                throw new ArgumentException(
                    "At least one frozen weapon identity is required.",
                    nameof(frozenEquipmentInstanceStableIds));
            }
            equipmentIds = ids.AsReadOnly();
        }

        public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
        {
            get { return equipmentIds; }
        }

        public override string SnapshotFingerprint
        {
            get
            {
                var builder = new StringBuilder(base.SnapshotFingerprint);
                for (int index = 0; index < equipmentIds.Count; index++)
                {
                    builder.Append('|').Append(equipmentIds[index]);
                }
                return RunSessionFingerprintV1.Hash(builder.ToString());
            }
        }

        public override RunRuntimePortRestartResultV1 Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = ValidateRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
            if (!string.IsNullOrEmpty(rejection))
            {
                return new RunRuntimePortRestartResultV1(
                    false,
                    rejection,
                    LifecycleGeneration,
                    SnapshotFingerprint);
            }
            clearTransientEffects();
            CommitGeneration(replacementLifecycleGeneration);
            return new RunRuntimePortRestartResultV1(
                true,
                string.Empty,
                LifecycleGeneration,
                SnapshotFingerprint);
        }
    }

    internal sealed class Stage1StatusRunProjectionV1 :
        Stage1RunLifecycleProjectionV1,
        IRunStatusEffectRuntimePortV1
    {
        public Stage1StatusRunProjectionV1(long lifecycleGeneration)
            : base("stage1-status-runtime-projection", lifecycleGeneration)
        {
        }

        public int ActiveEffectCount
        {
            get { return 0; }
        }
    }

    internal sealed class Stage1ConditionRunProjectionV1 :
        Stage1RunLifecycleProjectionV1,
        IRunConditionalFactRuntimePortV1
    {
        public Stage1ConditionRunProjectionV1(long lifecycleGeneration)
            : base("stage1-condition-runtime-projection", lifecycleGeneration)
        {
        }
    }

    internal sealed class Stage1AbilityRunProjectionV1 :
        Stage1RunLifecycleProjectionV1,
        IRunActiveAbilityRuntimePortV1
    {
        public Stage1AbilityRunProjectionV1(long lifecycleGeneration)
            : base("stage1-ability-runtime-projection", lifecycleGeneration)
        {
        }
    }

    internal sealed class Stage1RoomRunPortV1 :
        Stage1RunLifecycleProjectionV1,
        IRunRoomRuntimePortV1
    {
        private readonly RoomRuntimeComposition2D rooms;

        public Stage1RoomRunPortV1(
            long lifecycleGeneration,
            RoomRuntimeComposition2D rooms)
            : base("stage1-room-runtime", lifecycleGeneration)
        {
            this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        }

        public StableId CurrentRoomStableId
        {
            get { return rooms.CurrentRoomStableId; }
        }

        public override string SnapshotFingerprint
        {
            get
            {
                return RunSessionFingerprintV1.Hash(
                    base.SnapshotFingerprint
                    + "|"
                    + (CurrentRoomStableId == null
                        ? "-"
                        : CurrentRoomStableId.ToString()));
            }
        }

        public override RunRuntimePortRestartResultV1 Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = ValidateRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
            if (!string.IsNullOrEmpty(rejection))
            {
                return new RunRuntimePortRestartResultV1(
                    false,
                    rejection,
                    LifecycleGeneration,
                    SnapshotFingerprint);
            }
            rooms.Restart(StableId.Create(
                "operation",
                "stage1-room-run-restart-g"
                    + replacementLifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture)));
            CommitGeneration(replacementLifecycleGeneration);
            return new RunRuntimePortRestartResultV1(
                true,
                string.Empty,
                LifecycleGeneration,
                SnapshotFingerprint);
        }
    }

    internal sealed class Stage1RunSessionRuntimePortFactoryV1 :
        IRunSessionRuntimePortFactoryV1
    {
        private readonly Level1PlayerRuntimeSceneAdapterV1 player;
        private readonly RoomRuntimeComposition2D rooms;
        private readonly IRunMissionResultPortV1 missionResults;
        private readonly Action clearTransientWeaponEffects;

        public Stage1RunSessionRuntimePortFactoryV1(
            Level1PlayerRuntimeSceneAdapterV1 player,
            RoomRuntimeComposition2D rooms,
            IRunMissionResultPortV1 missionResults,
            Action clearTransientWeaponEffects)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            this.missionResults = missionResults
                ?? throw new ArgumentNullException(nameof(missionResults));
            this.clearTransientWeaponEffects = clearTransientWeaponEffects
                ?? throw new ArgumentNullException(
                    nameof(clearTransientWeaponEffects));
        }

        public RunSessionRuntimePortsV1 Create(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputsV1 frozenInputs)
        {
            var playerPort = new Stage1PlayerRunPortV1(player);
            long generation = playerPort.LifecycleGeneration;
            IEnumerable<StableId> weaponIds = frozenInputs.Equipment
                .Where(item => item.EquipmentDefinition.CategoryId
                    == EquipmentCategoryIds.Weapon)
                .Select(item => item.EquipmentInstanceStableId);
            return new RunSessionRuntimePortsV1(
                playerPort,
                new Stage1WeaponRunPortV1(
                    generation,
                    weaponIds,
                    clearTransientWeaponEffects),
                new Stage1StatusRunProjectionV1(generation),
                new Stage1ConditionRunProjectionV1(generation),
                new Stage1AbilityRunProjectionV1(generation),
                new Stage1RoomRunPortV1(generation, rooms),
                missionResults);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Players;
using ShooterMover.UnityAdapters.Production.Level1;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    internal sealed class Stage1PickupRunStatInputResolverV1 :
        IProductionRunStatInputResolverV1
    {
        private readonly Level1PlayerRuntimeSceneAdapterV1 player;

        public Stage1PickupRunStatInputResolverV1(
            Level1PlayerRuntimeSceneAdapterV1 player)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public ProductionRunStatInputResolutionV1 Resolve(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            ProductionCharacterRuntimeGraphV1 characterGraph,
            CharacterInstanceSnapshotV1 character,
            PlayerRouteProfilePayloadV1 currentRoutePayload,
            RankedSkillAllocationSnapshotV2 skillSnapshot,
            IReadOnlyList<FrozenRunEquipmentV1> frozenEquipment)
        {
            PlayerRuntimeSnapshot runtime = player.ExportSnapshot();
            if (runtime == null || runtime.Player == null)
                throw new InvalidOperationException(
                    "stage1-pickup-run-player-snapshot-unavailable");

            var baseValues = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                {
                    DerivedStatTargetIdsV1.MaximumHealth,
                    checked((decimal)runtime.Player.MaximumHealth)
                },
                { DerivedStatTargetIdsV1.MovementSpeed, 6m },
            };
            return new ProductionRunStatInputResolutionV1(
                new DerivedCharacterStatInputV1(
                    character.CharacterInstanceStableId.ToString(),
                    new CharacterBaseStatProfileV1(
                        "base-profile.stage1-pickup-production",
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

    internal abstract class Stage1PickupRunLifecycleProjectionV1 :
        IRunLifecycleRuntimePortV1
    {
        private long lifecycleGeneration;

        protected Stage1PickupRunLifecycleProjectionV1(
            string portId,
            long lifecycleGeneration)
        {
            if (string.IsNullOrWhiteSpace(portId))
                throw new ArgumentException("A pickup run-port identity is required.", nameof(portId));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            PortId = portId.Trim();
            this.lifecycleGeneration = lifecycleGeneration;
        }

        public string PortId { get; }
        public virtual long LifecycleGeneration { get { return lifecycleGeneration; } }
        public virtual string SnapshotFingerprint
        {
            get
            {
                return RunSessionFingerprintV1.Hash(
                    PortId + "|" + LifecycleGeneration.ToString(CultureInfo.InvariantCulture));
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
            return replacementLifecycleGeneration == retiringLifecycleGeneration + 1L
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
            if (!string.IsNullOrEmpty(rejection)) return Rejected(rejection);
            lifecycleGeneration = replacementLifecycleGeneration;
            return Accepted();
        }

        protected void CommitGeneration(long generation)
        {
            lifecycleGeneration = generation;
        }

        protected RunRuntimePortRestartResultV1 Accepted()
        {
            return new RunRuntimePortRestartResultV1(
                true,
                string.Empty,
                LifecycleGeneration,
                SnapshotFingerprint);
        }

        protected RunRuntimePortRestartResultV1 Rejected(string code)
        {
            return new RunRuntimePortRestartResultV1(
                false,
                code,
                LifecycleGeneration,
                SnapshotFingerprint);
        }
    }

    internal sealed class Stage1PickupPlayerRunPortV1 :
        Stage1PickupRunLifecycleProjectionV1,
        IRunPlayerRuntimePortV1
    {
        private readonly Level1PlayerRuntimeSceneAdapterV1 player;

        public Stage1PickupPlayerRunPortV1(
            Level1PlayerRuntimeSceneAdapterV1 player)
            : base("stage1-pickup-player-runtime", ReadGeneration(player))
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
            if (snapshot == null || snapshot.Player == null)
                throw new InvalidOperationException(
                    "stage1-pickup-player-snapshot-unavailable");
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
            if (!string.IsNullOrEmpty(rejection)) return Rejected(rejection);

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
            return accepted
                ? Accepted()
                : Rejected(result == null
                    ? "stage1-pickup-player-restart-null"
                    : "stage1-pickup-player-restart-" + result.RejectionCode);
        }

        private static long ReadGeneration(Level1PlayerRuntimeSceneAdapterV1 player)
        {
            if (player == null || !player.IsInitialized)
                throw new InvalidOperationException(
                    "The Stage 1 pickup player runtime is unavailable.");
            return player.ExportSnapshot().Player.LifecycleGeneration;
        }
    }

    internal sealed class Stage1PickupWeaponRunPortV1 :
        Stage1PickupRunLifecycleProjectionV1,
        IRunWeaponRuntimePortV1
    {
        private readonly IReadOnlyList<StableId> equipmentIds;
        private readonly Action clearTransientEffects;

        public Stage1PickupWeaponRunPortV1(
            long lifecycleGeneration,
            IEnumerable<StableId> frozenEquipmentInstanceStableIds,
            Action clearTransientEffects)
            : base("stage1-pickup-weapon-runtime", lifecycleGeneration)
        {
            this.clearTransientEffects = clearTransientEffects
                ?? throw new ArgumentNullException(nameof(clearTransientEffects));
            List<StableId> ids = (frozenEquipmentInstanceStableIds
                    ?? throw new ArgumentNullException(nameof(frozenEquipmentInstanceStableIds)))
                .Where(item => item != null)
                .Distinct()
                .OrderBy(item => item)
                .ToList();
            if (ids.Count == 0)
                throw new ArgumentException(
                    "At least one frozen weapon identity is required.",
                    nameof(frozenEquipmentInstanceStableIds));
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
                    builder.Append('|').Append(equipmentIds[index]);
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
            if (!string.IsNullOrEmpty(rejection)) return Rejected(rejection);
            clearTransientEffects();
            CommitGeneration(replacementLifecycleGeneration);
            return Accepted();
        }
    }

    internal sealed class Stage1PickupStatusRunProjectionV1 :
        Stage1PickupRunLifecycleProjectionV1,
        IRunStatusEffectRuntimePortV1
    {
        public Stage1PickupStatusRunProjectionV1(long generation)
            : base("stage1-pickup-status-runtime", generation) { }
        public int ActiveEffectCount { get { return 0; } }
    }

    internal sealed class Stage1PickupConditionRunProjectionV1 :
        Stage1PickupRunLifecycleProjectionV1,
        IRunConditionalFactRuntimePortV1
    {
        public Stage1PickupConditionRunProjectionV1(long generation)
            : base("stage1-pickup-condition-runtime", generation) { }
    }

    internal sealed class Stage1PickupAbilityRunProjectionV1 :
        Stage1PickupRunLifecycleProjectionV1,
        IRunActiveAbilityRuntimePortV1
    {
        public Stage1PickupAbilityRunProjectionV1(long generation)
            : base("stage1-pickup-ability-runtime", generation) { }
    }

    internal sealed class Stage1PickupRoomRunPortV1 :
        Stage1PickupRunLifecycleProjectionV1,
        IRunRoomRuntimePortV1
    {
        private readonly RoomRuntimeComposition2D rooms;

        public Stage1PickupRoomRunPortV1(
            long generation,
            RoomRuntimeComposition2D rooms)
            : base("stage1-pickup-room-runtime", generation)
        {
            this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        }

        public StableId CurrentRoomStableId { get { return rooms.CurrentRoomStableId; } }

        public override string SnapshotFingerprint
        {
            get
            {
                return RunSessionFingerprintV1.Hash(
                    base.SnapshotFingerprint + "|"
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
            if (!string.IsNullOrEmpty(rejection)) return Rejected(rejection);
            rooms.Restart(StableId.Create(
                "operation",
                "stage1-pickup-room-restart-g"
                    + replacementLifecycleGeneration.ToString(CultureInfo.InvariantCulture)));
            CommitGeneration(replacementLifecycleGeneration);
            return Accepted();
        }
    }

    internal sealed class Stage1PickupRunSessionRuntimePortFactoryV1 :
        IRunSessionRuntimePortFactoryV1
    {
        private readonly Level1PlayerRuntimeSceneAdapterV1 player;
        private readonly RoomRuntimeComposition2D rooms;
        private readonly IRunMissionResultPortV1 missionResults;
        private readonly Action clearTransientWeaponEffects;

        public Stage1PickupRunSessionRuntimePortFactoryV1(
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
                ?? throw new ArgumentNullException(nameof(clearTransientWeaponEffects));
        }

        public RunSessionRuntimePortsV1 Create(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputsV1 frozenInputs)
        {
            var playerPort = new Stage1PickupPlayerRunPortV1(player);
            long generation = playerPort.LifecycleGeneration;
            IEnumerable<StableId> weaponIds = frozenInputs.Equipment
                .Where(item => item.EquipmentDefinition.CategoryId
                    == EquipmentCategoryIds.Weapon)
                .Select(item => item.EquipmentInstanceStableId);
            return new RunSessionRuntimePortsV1(
                playerPort,
                new Stage1PickupWeaponRunPortV1(
                    generation,
                    weaponIds,
                    clearTransientWeaponEffects),
                new Stage1PickupStatusRunProjectionV1(generation),
                new Stage1PickupConditionRunProjectionV1(generation),
                new Stage1PickupAbilityRunProjectionV1(generation),
                new Stage1PickupRoomRunPortV1(generation, rooms),
                missionResults);
        }
    }
}

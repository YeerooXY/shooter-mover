using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShooterMover.Application.Modifiers.StatusEffects;
using ShooterMover.Application.Runs.Session;
using ShooterMover.ConditionRuntime;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.StatusEffects;
using ShooterMover.RunConditionIntegration;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Production.Level1;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Current Stage 1 condition content is neutral, while lifecycle, replay, fact windows and
    /// status effects are owned by the merged production condition runtime.
    /// </summary>
    internal sealed class Stage1ProductionConditionDefinitionProviderV1 :
        IRunConditionDefinitionProviderV1
    {
        private const string ConditionId =
            "condition.stage1-baseline-enemy-observation";
        private const string EffectId =
            "status-effect.stage1-baseline-enemy-observation";
        private const string ContentVersion = "1.0.0";

        private static readonly ConditionEffectRuntimeDefinitionV1 Definition =
            BuildDefinition();

        public ConditionEffectRuntimeDefinitionV1 Resolve(
            StableId runStableId,
            FrozenCharacterRunInputsV1 frozenInputs,
            RunConditionParticipantSeedV1 participant)
        {
            if (runStableId == null)
            {
                throw new ArgumentNullException(nameof(runStableId));
            }
            if (frozenInputs == null)
            {
                throw new ArgumentNullException(nameof(frozenInputs));
            }
            if (participant == null)
            {
                throw new ArgumentNullException(nameof(participant));
            }
            return Definition;
        }

        private static ConditionEffectRuntimeDefinitionV1 BuildDefinition()
        {
            var condition = new FactWindowConditionDefinitionV1(
                ConditionId,
                ConditionRuntimeFactTypeIdsV1.EnemyKilled,
                int.MaxValue,
                3600L,
                1L,
                true);
            var effect = new StatusEffectDefinitionV1(
                EffectId,
                ContentVersion,
                1L,
                1,
                StatusEffectStackingPolicyV1.Ignore,
                "dispel-category.conditional",
                Array.Empty<RuntimeModifierDefinitionV1>());
            var catalog = new StatusEffectCatalogV1(
                "status-effect-catalog.stage1-production",
                ContentVersion,
                new[] { effect });
            return new ConditionEffectRuntimeDefinitionV1(
                "condition-runtime.stage1-production",
                ContentVersion,
                new[] { condition },
                catalog,
                new[]
                {
                    new FactWindowStatusEffectBindingV1(
                        ConditionId,
                        EffectId,
                        "conditional-source.stage1-production"),
                });
        }
    }

    internal sealed class Stage1RoomRunPortV1 : IRunRoomRuntimePortV1
    {
        private readonly RoomRuntimeComposition2D rooms;
        private long lifecycleGeneration;

        public Stage1RoomRunPortV1(
            long lifecycleGeneration,
            RoomRuntimeComposition2D rooms)
        {
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            this.lifecycleGeneration = lifecycleGeneration;
        }

        public string PortId { get { return "stage1-room-runtime"; } }
        public long LifecycleGeneration { get { return lifecycleGeneration; } }
        public StableId CurrentRoomStableId { get { return rooms.CurrentRoomStableId; } }
        public string SnapshotFingerprint
        {
            get
            {
                return RunSessionFingerprintV1.Hash(
                    PortId
                    + "|"
                    + lifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + (CurrentRoomStableId == null
                        ? "-"
                        : CurrentRoomStableId.ToString()));
            }
        }

        public string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            if (retiringLifecycleGeneration != lifecycleGeneration)
            {
                return retiringLifecycleGeneration < lifecycleGeneration
                    ? "stage1-room-runtime-stale-generation"
                    : "stage1-room-runtime-future-generation";
            }
            return replacementLifecycleGeneration == lifecycleGeneration + 1L
                ? string.Empty
                : "stage1-room-runtime-replacement-generation-invalid";
        }

        public RunRuntimePortRestartResultV1 Restart(
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
                return Result(false, rejection);
            }
            rooms.Restart(StableId.Create(
                "operation",
                "stage1-room-run-restart-g"
                    + replacementLifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture)));
            lifecycleGeneration = replacementLifecycleGeneration;
            return Result(true, string.Empty);
        }

        private RunRuntimePortRestartResultV1 Result(bool accepted, string rejection)
        {
            return new RunRuntimePortRestartResultV1(
                accepted,
                rejection,
                lifecycleGeneration,
                SnapshotFingerprint);
        }
    }

    /// <summary>
    /// Builds only the non-condition half of the one shared Stage 1 Run Session graph. The merged
    /// condition-bound factory adds the canonical condition and status-effect owners.
    /// </summary>
    internal sealed class Stage1SharedRunSessionNonConditionRuntimePortFactoryV1 :
        IRunSessionNonConditionRuntimePortFactoryV1
    {
        private readonly Level1PlayerRuntimeSceneAdapterV1 player;
        private readonly RoomRuntimeComposition2D rooms;
        private readonly IRunMissionResultPortV1 missionResults;
        private readonly Action clearTransientWeaponEffects;

        public Stage1SharedRunSessionNonConditionRuntimePortFactoryV1(
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

        public RunSessionNonConditionRuntimePortsV1 Create(
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

            var playerPort = new Stage1PlayerRunPortV1(player);
            long generation = playerPort.LifecycleGeneration;
            IEnumerable<StableId> weaponIds = frozenInputs.Equipment
                .Where(item => item.EquipmentDefinition.CategoryId
                    == EquipmentCategoryIds.Weapon)
                .Select(item => item.EquipmentInstanceStableId);
            return new RunSessionNonConditionRuntimePortsV1(
                playerPort,
                new Stage1WeaponRunPortV1(
                    generation,
                    weaponIds,
                    clearTransientWeaponEffects),
                new EmptyActiveAbilityRunPortV1(
                    generation,
                    frozenInputs.Character.Fingerprint),
                new Stage1RoomRunPortV1(generation, rooms),
                missionResults);
        }
    }
}

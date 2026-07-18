using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.Application.Missions.Run
{
    public interface ILevelRunStableIdFactoryV1
    {
        StableId CreateRunStableId();
    }

    public sealed class UniqueLevelRunStableIdFactoryV1 :
        ILevelRunStableIdFactoryV1
    {
        public StableId CreateRunStableId()
        {
            return StableId.Create(
                "run",
                "level-run-" + Guid.NewGuid().ToString("N"));
        }
    }

    public sealed class MissionRunAuthorityCheckpointV1
    {
        public MissionRunAuthorityCheckpointV1(
            long holdingsSequence,
            string holdingsFingerprint,
            long strongboxOpeningSequence,
            string strongboxOpeningFingerprint)
        {
            if (holdingsSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(holdingsSequence));
            }

            if (strongboxOpeningSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(strongboxOpeningSequence));
            }

            HoldingsSequence = holdingsSequence;
            HoldingsFingerprint = holdingsFingerprint
                ?? throw new ArgumentNullException(nameof(holdingsFingerprint));
            StrongboxOpeningSequence = strongboxOpeningSequence;
            StrongboxOpeningFingerprint = strongboxOpeningFingerprint
                ?? throw new ArgumentNullException(
                    nameof(strongboxOpeningFingerprint));
        }

        public long HoldingsSequence { get; }
        public string HoldingsFingerprint { get; }
        public long StrongboxOpeningSequence { get; }
        public string StrongboxOpeningFingerprint { get; }
    }

    /// <summary>
    /// Engine-independent production run coordinator. It owns only run-scoped
    /// registration, room completion, active-slot selection, and per-player
    /// contribution state. XP, holdings, strongboxes, and terminal mission results
    /// remain owned by their existing authorities.
    /// </summary>
    public sealed partial class LevelRunCoordinatorV1
    {
        private sealed class MutableContribution
        {
            public MutableContribution(StableId playerStableId)
            {
                PlayerStableId = playerStableId;
            }

            public StableId PlayerStableId;
            public int KillCount;
            public long ExperienceEarned;
        }

        public static readonly StableId Level1StableId =
            StableId.Parse("level.stage-1");

        private readonly PlayerRouteProfilePayloadV1 routePayload;
        private readonly StableId selectedModeStableId;
        private readonly StableId selectedLevelStableId;
        private readonly StableId runStableId;
        private readonly StableId extractionRoomStableId;
        private readonly RoomMissionLayoutV1 roomLayout;
        private readonly LevelRunLoadoutResolutionV1 loadout;
        private readonly EnemyExperienceRewardServiceV1 experienceRewards;
        private readonly MissionRunResultAuthorityV1 missionResults;
        private readonly MissionRunAuthorityCheckpointV1 checkpoint;
        private readonly Dictionary<StableId, HashSet<StableId>> enemiesByRoom =
            new Dictionary<StableId, HashSet<StableId>>();
        private readonly Dictionary<StableId, StableId> roomByEnemy =
            new Dictionary<StableId, StableId>();
        private readonly Dictionary<StableId, StableId> playerBySource =
            new Dictionary<StableId, StableId>();
        private readonly Dictionary<StableId, MutableContribution> contributions =
            new Dictionary<StableId, MutableContribution>();
        private readonly HashSet<StableId> processedDestructionEvents =
            new HashSet<StableId>();
        private readonly HashSet<StableId> destroyedEnemies =
            new HashSet<StableId>();

        private int activeSlotIndex;
        private bool completionRequested;
        private LevelRunExtractionResultV1 terminalExtraction;

        private LevelRunCoordinatorV1(
            PlayerRouteProfilePayloadV1 routePayload,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            StableId runStableId,
            StableId extractionRoomStableId,
            RoomMissionLayoutV1 roomLayout,
            LevelRunLoadoutResolutionV1 loadout,
            EnemyExperienceRewardServiceV1 experienceRewards,
            MissionRunResultAuthorityV1 missionResults,
            MissionRunAuthorityCheckpointV1 checkpoint)
        {
            this.routePayload = routePayload;
            this.selectedModeStableId = selectedModeStableId;
            this.selectedLevelStableId = selectedLevelStableId;
            this.runStableId = runStableId;
            this.extractionRoomStableId = extractionRoomStableId;
            this.roomLayout = roomLayout;
            this.loadout = loadout;
            this.experienceRewards = experienceRewards;
            this.missionResults = missionResults;
            this.checkpoint = checkpoint;
            activeSlotIndex = loadout.ActiveSlotIndex;
        }

        public PlayerRouteProfilePayloadV1 RoutePayload { get { return routePayload; } }
        public StableId SelectedModeStableId { get { return selectedModeStableId; } }
        public StableId SelectedLevelStableId { get { return selectedLevelStableId; } }
        public StableId RunStableId { get { return runStableId; } }
        public RoomMissionLayoutV1 RoomLayout { get { return roomLayout; } }
        public LevelRunLoadoutResolutionV1 Loadout { get { return loadout; } }
        public int ActiveSlotIndex { get { return activeSlotIndex; } }
        public ResolvedLevelRunWeaponSlotV1 ActiveWeapon
        {
            get { return loadout.Slots[activeSlotIndex]; }
        }
        public bool HasTerminalResult { get { return terminalExtraction != null; } }

        public static LevelRunStartStatusV1 TryCreateNewRun(
            PlayerRouteProfilePayloadV1 routePayload,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            ILevelRunStableIdFactoryV1 runStableIdFactory,
            StableId extractionRoomStableId,
            RoomMissionLayoutV1 roomLayout,
            ILevelRunLoadoutResolverV1 loadoutResolver,
            EnemyExperienceRewardServiceV1 experienceRewards,
            MissionRunResultAuthorityV1 missionResults,
            MissionRunAuthorityCheckpointV1 checkpoint,
            out LevelRunCoordinatorV1 coordinator,
            out string rejectionCode)
        {
            if (runStableIdFactory == null)
            {
                coordinator = null;
                rejectionCode = "level-run-id-factory-missing";
                return LevelRunStartStatusV1.InvalidRoutePayload;
            }

            StableId generated = runStableIdFactory.CreateRunStableId();
            if (generated == null)
            {
                coordinator = null;
                rejectionCode = "level-run-id-generation-failed";
                return LevelRunStartStatusV1.InvalidRoutePayload;
            }

            return TryCreate(
                routePayload,
                selectedModeStableId,
                selectedLevelStableId,
                generated,
                extractionRoomStableId,
                roomLayout,
                loadoutResolver,
                experienceRewards,
                missionResults,
                checkpoint,
                out coordinator,
                out rejectionCode);
        }

        public static LevelRunStartStatusV1 TryCreate(
            PlayerRouteProfilePayloadV1 routePayload,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            StableId runStableId,
            StableId extractionRoomStableId,
            RoomMissionLayoutV1 roomLayout,
            ILevelRunLoadoutResolverV1 loadoutResolver,
            EnemyExperienceRewardServiceV1 experienceRewards,
            MissionRunResultAuthorityV1 missionResults,
            MissionRunAuthorityCheckpointV1 checkpoint,
            out LevelRunCoordinatorV1 coordinator,
            out string rejectionCode)
        {
            coordinator = null;
            rejectionCode = string.Empty;
            if (routePayload == null || !routePayload.HasValidFingerprint())
            {
                rejectionCode = "level-run-route-payload-invalid";
                return LevelRunStartStatusV1.InvalidRoutePayload;
            }

            if (selectedModeStableId == null)
            {
                rejectionCode = "level-run-selected-mode-missing";
                return LevelRunStartStatusV1.MissingMode;
            }

            if (selectedLevelStableId == null)
            {
                rejectionCode = "level-run-selected-level-missing";
                return LevelRunStartStatusV1.MissingLevel;
            }

            if (selectedLevelStableId != Level1StableId)
            {
                rejectionCode = "level-run-selected-level-unsupported";
                return LevelRunStartStatusV1.WrongLevel;
            }

            if (routePayload.SelectedCharacterStableId == null)
            {
                rejectionCode = "level-run-character-missing";
                return LevelRunStartStatusV1.MissingCharacter;
            }

            if (routePayload.LoadoutProfileStableId == null)
            {
                rejectionCode = "level-run-loadout-profile-missing";
                return LevelRunStartStatusV1.MissingLoadoutProfile;
            }

            if (runStableId == null
                || extractionRoomStableId == null
                || roomLayout == null
                || loadoutResolver == null
                || experienceRewards == null
                || missionResults == null
                || checkpoint == null)
            {
                rejectionCode = "level-run-composition-invalid";
                return LevelRunStartStatusV1.InvalidRoutePayload;
            }

            LevelRunLoadoutResolutionV1 resolved =
                loadoutResolver.Resolve(routePayload);
            if (resolved == null || !resolved.Accepted)
            {
                rejectionCode = resolved == null
                    ? "level-run-loadout-resolution-null"
                    : resolved.RejectionCode;
                return LevelRunStartStatusV1.InvalidRoutePayload;
            }

            coordinator = new LevelRunCoordinatorV1(
                routePayload,
                selectedModeStableId,
                selectedLevelStableId,
                runStableId,
                extractionRoomStableId,
                roomLayout,
                resolved,
                experienceRewards,
                missionResults,
                checkpoint);
            return LevelRunStartStatusV1.Started;
        }

        public LevelRunStartStatusV1 TryRestart(
            ILevelRunStableIdFactoryV1 runStableIdFactory,
            MissionRunAuthorityCheckpointV1 nextCheckpoint,
            out LevelRunCoordinatorV1 restarted,
            out string rejectionCode)
        {
            restarted = null;
            rejectionCode = string.Empty;
            if (runStableIdFactory == null || nextCheckpoint == null)
            {
                rejectionCode = "level-run-restart-composition-invalid";
                return LevelRunStartStatusV1.InvalidRoutePayload;
            }

            StableId nextRunStableId = runStableIdFactory.CreateRunStableId();
            if (nextRunStableId == null || nextRunStableId == runStableId)
            {
                rejectionCode = "level-run-restart-identity-invalid";
                return LevelRunStartStatusV1.InvalidRoutePayload;
            }

            restarted = new LevelRunCoordinatorV1(
                routePayload,
                selectedModeStableId,
                selectedLevelStableId,
                nextRunStableId,
                extractionRoomStableId,
                new RoomMissionLayoutV1(roomLayout.Definition),
                loadout,
                experienceRewards,
                missionResults,
                nextCheckpoint);
            return LevelRunStartStatusV1.Started;
        }

        public bool RegisterPlayerSource(
            StableId sourceActorStableId,
            StableId playerStableId)
        {
            if (sourceActorStableId == null || playerStableId == null)
            {
                return false;
            }

            StableId existing;
            if (playerBySource.TryGetValue(sourceActorStableId, out existing))
            {
                return existing == playerStableId;
            }

            playerBySource.Add(sourceActorStableId, playerStableId);
            if (!contributions.ContainsKey(playerStableId))
            {
                contributions.Add(
                    playerStableId,
                    new MutableContribution(playerStableId));
            }

            return true;
        }

        public bool RegisterRoomEnemies(
            StableId roomStableId,
            IEnumerable<StableId> enemyActorStableIds)
        {
            if (roomStableId == null || enemyActorStableIds == null)
            {
                return false;
            }

            try
            {
                roomLayout.GetRoomState(roomStableId);
            }
            catch (KeyNotFoundException)
            {
                return false;
            }

            var registered = new HashSet<StableId>();
            foreach (StableId enemyId in enemyActorStableIds)
            {
                if (enemyId == null || !registered.Add(enemyId))
                {
                    return false;
                }

                StableId existingRoom;
                if (roomByEnemy.TryGetValue(enemyId, out existingRoom)
                    && existingRoom != roomStableId)
                {
                    return false;
                }
            }

            HashSet<StableId> existingRegistration;
            if (enemiesByRoom.TryGetValue(roomStableId, out existingRegistration))
            {
                if (!existingRegistration.SetEquals(registered))
                {
                    return false;
                }

                if (registered.Count == 0
                    && roomLayout.CurrentRoomState.RoomStableId == roomStableId)
                {
                    roomLayout.CompleteCurrentRoom();
                }

                return true;
            }

            enemiesByRoom.Add(roomStableId, registered);
            foreach (StableId enemyId in registered)
            {
                roomByEnemy[enemyId] = roomStableId;
            }

            if (registered.Count == 0
                && roomLayout.CurrentRoomState.RoomStableId == roomStableId)
            {
                roomLayout.CompleteCurrentRoom();
            }

            return true;
        }

        public bool TrySelectActiveSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= loadout.Slots.Count)
            {
                return false;
            }

            if (slotIndex == activeSlotIndex)
            {
                return false;
            }

            activeSlotIndex = slotIndex;
            return true;
        }

        public ShooterMover.Contracts.Missions.Rooms.RoomGraphOperationResultV1
            Traverse(StableId exitStableId)
        {
            ShooterMover.Contracts.Missions.Rooms.RoomGraphOperationResultV1 result =
                roomLayout.Traverse(exitStableId);
            HashSet<StableId> registered;
            StableId currentRoomStableId =
                roomLayout.CurrentRoomState.RoomStableId;
            if (enemiesByRoom.TryGetValue(
                    currentRoomStableId,
                    out registered))
            {
                TryCompleteRoomIfClear(currentRoomStableId);
            }

            return result;
        }

    }
}

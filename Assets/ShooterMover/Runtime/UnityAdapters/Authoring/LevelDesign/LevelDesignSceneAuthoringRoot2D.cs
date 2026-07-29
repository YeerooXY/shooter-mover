using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [DisallowMultipleComponent]
    public sealed class LevelDesignSceneAuthoringRoot2D : MonoBehaviour
    {
        [Header("Stable identity")]
        [SerializeField] private string levelId = "level.unassigned";

        [Header("Validation scope")]
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private bool validateOnEnable;

        private LevelDesignValidationResult lastValidation =
            LevelDesignValidationResult.Empty();
        private LevelGridValidationResult lastGridValidation =
            LevelGridValidationResult.Empty();

        public string LevelIdText
        {
            get { return levelId; }
        }

        public LevelDesignValidationResult LastValidation
        {
            get { return lastValidation; }
        }

        public LevelGridValidationResult LastGridValidation
        {
            get { return lastGridValidation; }
        }

        private void OnEnable()
        {
            if (validateOnEnable)
            {
                ValidateHierarchy();
                ValidateGridAuthoring(LevelGridValidationPurpose.Draft);
            }
        }

        public LevelDesignValidationResult ValidateHierarchy()
        {
            LevelRoomAuthoring2D[] roomComponents =
                GetComponentsInChildren<LevelRoomAuthoring2D>(includeInactive);
            LevelPlacementAuthoring2D[] placementComponents =
                GetComponentsInChildren<LevelPlacementAuthoring2D>(includeInactive);
            LevelDoorConnectionAuthoring2D[] doorComponents =
                GetComponentsInChildren<LevelDoorConnectionAuthoring2D>(
                    includeInactive);
            LevelVoidRegionAuthoring2D[] voidComponents =
                GetComponentsInChildren<LevelVoidRegionAuthoring2D>(includeInactive);

            List<LevelRoomRecord> rooms =
                new List<LevelRoomRecord>(roomComponents.Length);
            for (int index = 0; index < roomComponents.Length; index++)
            {
                rooms.Add(roomComponents[index].BuildRecord());
            }

            List<LevelPlacementRecord> placements =
                new List<LevelPlacementRecord>(placementComponents.Length);
            for (int index = 0; index < placementComponents.Length; index++)
            {
                placements.Add(placementComponents[index].BuildRecord());
            }

            List<LevelDoorRecord> doors =
                new List<LevelDoorRecord>(doorComponents.Length);
            for (int index = 0; index < doorComponents.Length; index++)
            {
                doors.Add(doorComponents[index].BuildRecord());
            }

            List<LevelVoidRecord> voids =
                new List<LevelVoidRecord>(voidComponents.Length);
            for (int index = 0; index < voidComponents.Length; index++)
            {
                voids.Add(voidComponents[index].BuildRecord());
            }

            lastValidation = LevelDesignFoundationValidator.Validate(
                levelId,
                rooms,
                placements,
                doors,
                voids);
            return lastValidation;
        }

        public LevelGridValidationResult ValidateGridAuthoring(
            LevelGridValidationPurpose purpose)
        {
            LevelRoomAuthoring2D[] roomComponents =
                GetComponentsInChildren<LevelRoomAuthoring2D>(includeInactive);
            LevelDoorEndpointAuthoring2D[] doorComponents =
                GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(includeInactive);
            LevelDoorLinkAuthoring2D[] connectionComponents =
                GetComponentsInChildren<LevelDoorLinkAuthoring2D>(includeInactive);

            List<LevelRoomRecord> rooms =
                new List<LevelRoomRecord>(roomComponents.Length);
            List<LevelGridRoomRecord> gridRooms =
                new List<LevelGridRoomRecord>(roomComponents.Length);
            for (int index = 0; index < roomComponents.Length; index++)
            {
                rooms.Add(roomComponents[index].BuildRecord());
                gridRooms.Add(roomComponents[index].BuildGridRecord());
            }

            List<LevelGridDoorRecord> doors =
                new List<LevelGridDoorRecord>(doorComponents.Length);
            for (int index = 0; index < doorComponents.Length; index++)
            {
                doors.Add(doorComponents[index].BuildRecord());
            }

            List<LevelGridConnectionRecord> connections =
                new List<LevelGridConnectionRecord>(connectionComponents.Length);
            for (int index = 0; index < connectionComponents.Length; index++)
            {
                connections.Add(connectionComponents[index].BuildRecord());
            }

            string finalExitRoomId = string.Empty;
            string finalExitDoorId = string.Empty;
            LevelGridPlayableMetadata metadata =
                GetComponent<LevelGridPlayableMetadata>();
            if (metadata != null
                && metadata.FinalExitRoom != null
                && metadata.FinalExitDoor != null
                && metadata.FinalExitRoom.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>()
                    == this
                && metadata.FinalExitDoor.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>()
                    == this
                && metadata.FinalExitDoor.OwningRoom == metadata.FinalExitRoom)
            {
                finalExitRoomId = metadata.FinalExitRoom.RoomIdText;
                finalExitDoorId = metadata.FinalExitDoor.DoorIdText;
            }

            lastGridValidation = LevelGridPlayableValidation.Validate(
                rooms,
                gridRooms,
                doors,
                connections,
                purpose,
                finalExitRoomId,
                finalExitDoorId);
            return lastGridValidation;
        }

        public void AssignNewStableId()
        {
            levelId = LevelDesignAuthoringId.New("level");
        }

        public void ConfigureForTests(string configuredLevelId)
        {
            levelId = configuredLevelId;
            includeInactive = true;
            validateOnEnable = false;
        }
    }
}

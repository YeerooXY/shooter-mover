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
        private LevelGridValidationResultV2 lastGridValidation =
            LevelGridValidationResultV2.Empty();

        public string LevelIdText
        {
            get { return levelId; }
        }

        public LevelDesignValidationResult LastValidation
        {
            get { return lastValidation; }
        }

        public LevelGridValidationResultV2 LastGridValidation
        {
            get { return lastGridValidation; }
        }

        private void OnEnable()
        {
            if (validateOnEnable)
            {
                ValidateHierarchy();
                ValidateGridAuthoring(LevelGridValidationPurposeV2.Draft);
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

        public LevelGridValidationResultV2 ValidateGridAuthoring(
            LevelGridValidationPurposeV2 purpose)
        {
            LevelRoomAuthoring2D[] roomComponents =
                GetComponentsInChildren<LevelRoomAuthoring2D>(includeInactive);
            LevelDoorEndpointAuthoring2D[] doorComponents =
                GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(includeInactive);
            LevelDoorLinkAuthoring2D[] connectionComponents =
                GetComponentsInChildren<LevelDoorLinkAuthoring2D>(includeInactive);

            List<LevelRoomRecord> rooms =
                new List<LevelRoomRecord>(roomComponents.Length);
            List<LevelGridRoomRecordV2> gridRooms =
                new List<LevelGridRoomRecordV2>(roomComponents.Length);
            for (int index = 0; index < roomComponents.Length; index++)
            {
                rooms.Add(roomComponents[index].BuildRecord());
                gridRooms.Add(roomComponents[index].BuildGridRecord());
            }

            List<LevelGridDoorRecordV2> doors =
                new List<LevelGridDoorRecordV2>(doorComponents.Length);
            for (int index = 0; index < doorComponents.Length; index++)
            {
                doors.Add(doorComponents[index].BuildRecord());
            }

            List<LevelGridConnectionRecordV2> connections =
                new List<LevelGridConnectionRecordV2>(connectionComponents.Length);
            for (int index = 0; index < connectionComponents.Length; index++)
            {
                connections.Add(connectionComponents[index].BuildRecord());
            }

            string finalExitRoomId = string.Empty;
            string finalExitDoorId = string.Empty;
            LevelGridPlayableMetadataV2 metadata =
                GetComponent<LevelGridPlayableMetadataV2>();
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

            lastGridValidation = LevelGridPlayableValidationV2.Validate(
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

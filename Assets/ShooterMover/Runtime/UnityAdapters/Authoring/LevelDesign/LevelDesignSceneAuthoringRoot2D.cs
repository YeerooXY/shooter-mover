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
            for (int index = 0; index < roomComponents.Length; index++)
            {
                rooms.Add(roomComponents[index].BuildRecord());
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

            lastGridValidation = LevelGridAuthoringV2Validator.Validate(
                rooms,
                doors,
                connections,
                purpose);
            return lastGridValidation;
        }

        [ContextMenu("Assign New Stable ID")]
        public void AssignNewStableId()
        {
            levelId = LevelDesignAuthoringId.New("level");
        }

        [ContextMenu("Validate Level Design Foundation")]
        private void ValidateFromContextMenu()
        {
            LevelDesignValidationResult result = ValidateHierarchy();
            if (result.IsValid)
            {
                Debug.Log(
                    "Level design foundation validation passed with "
                    + result.WarningCount + " warning(s).",
                    this);
                return;
            }

            for (int index = 0; index < result.Issues.Count; index++)
            {
                LevelDesignValidationIssue issue = result.Issues[index];
                if (issue.Severity == LevelDesignValidationSeverity.Error)
                {
                    Debug.LogError(issue.ToString(), this);
                }
                else
                {
                    Debug.LogWarning(issue.ToString(), this);
                }
            }
        }

        [ContextMenu("Validate Grid Draft")]
        private void ValidateGridDraftFromContextMenu()
        {
            LogGridResult(ValidateGridAuthoring(LevelGridValidationPurposeV2.Draft));
        }

        [ContextMenu("Validate Grid Production Publish")]
        private void ValidateGridProductionFromContextMenu()
        {
            LogGridResult(
                ValidateGridAuthoring(
                    LevelGridValidationPurposeV2.ProductionPublish));
        }

        public void ConfigureForTests(string configuredLevelId)
        {
            levelId = configuredLevelId;
            includeInactive = true;
            validateOnEnable = false;
        }

        private void LogGridResult(LevelGridValidationResultV2 result)
        {
            if (result.CanPublish)
            {
                Debug.Log(
                    "Level grid " + result.Purpose + " validation passed with "
                    + result.WarningCount + " warning(s).",
                    this);
                return;
            }

            for (int index = 0; index < result.Problems.Count; index++)
            {
                LevelGridProblemV2 problem = result.Problems[index];
                if (problem.Severity == LevelDesignValidationSeverity.Error)
                {
                    Debug.LogError(problem.ToString(), this);
                }
                else
                {
                    Debug.LogWarning(problem.ToString(), this);
                }
            }
        }
    }
}

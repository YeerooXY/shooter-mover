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

        public string LevelIdText
        {
            get { return levelId; }
        }

        public LevelDesignValidationResult LastValidation
        {
            get { return lastValidation; }
        }

        private void OnEnable()
        {
            if (validateOnEnable)
            {
                ValidateHierarchy();
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

        public void ConfigureForTests(string configuredLevelId)
        {
            levelId = configuredLevelId;
            includeInactive = true;
            validateOnEnable = false;
        }
    }
}

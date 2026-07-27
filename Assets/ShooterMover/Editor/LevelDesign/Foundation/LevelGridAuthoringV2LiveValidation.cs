#if UNITY_EDITOR
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    [InitializeOnLoad]
    public static class LevelGridAuthoringV2LiveValidation
    {
        private static readonly HashSet<LevelDesignSceneAuthoringRoot2D> PendingRoots =
            new HashSet<LevelDesignSceneAuthoringRoot2D>();
        private static readonly HashSet<LevelDoorEndpointAuthoring2D> MovedFixedDoors =
            new HashSet<LevelDoorEndpointAuthoring2D>();
        private static bool refreshScheduled;

        static LevelGridAuthoringV2LiveValidation()
        {
            Undo.postprocessModifications += OnPostprocessModifications;
        }

        internal static void MarkSynchronouslyValidated(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (root != null)
            {
                PendingRoots.Remove(root);
            }
        }

        private static UndoPropertyModification[] OnPostprocessModifications(
            UndoPropertyModification[] modifications)
        {
            for (int index = 0; index < modifications.Length; index++)
            {
                UndoPropertyModification modification = modifications[index];
                Object target = modification.currentValue.target;
                GameObject gameObject = target as GameObject;
                Component component = target as Component;
                if (gameObject == null && component != null)
                {
                    gameObject = component.gameObject;
                }

                Transform movedTransform = target as Transform;
                if (movedTransform != null
                    && IsPositionModification(modification.currentValue.propertyPath))
                {
                    LevelDoorEndpointAuthoring2D fixedDoor =
                        movedTransform.GetComponent<LevelDoorEndpointAuthoring2D>();
                    if (fixedDoor != null
                        && fixedDoor.PlacementMode == LevelDoorPlacementModeV2.Fixed)
                    {
                        MovedFixedDoors.Add(fixedDoor);
                    }
                }

                LevelDesignSceneAuthoringRoot2D root = gameObject == null
                    ? null
                    : gameObject.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
                if (root != null)
                {
                    PendingRoots.Add(root);
                }
            }

            if ((PendingRoots.Count > 0 || MovedFixedDoors.Count > 0)
                && !refreshScheduled)
            {
                refreshScheduled = true;
                EditorApplication.delayCall += RefreshPendingRoots;
            }

            return modifications;
        }

        private static void RefreshPendingRoots()
        {
            refreshScheduled = false;

            foreach (LevelDoorEndpointAuthoring2D door in MovedFixedDoors)
            {
                if (door == null
                    || door.PlacementMode != LevelDoorPlacementModeV2.Fixed)
                {
                    continue;
                }

                door.CaptureCurrentFixedPosition();
                EditorUtility.SetDirty(door);
            }
            MovedFixedDoors.Clear();

            foreach (LevelDesignSceneAuthoringRoot2D root in PendingRoots)
            {
                if (root == null)
                {
                    continue;
                }

                LevelGridDoorOperationsV2.ReflowAll(root);
                root.ValidateHierarchy();
                root.ValidateGridAuthoring(root.LastGridValidation.Purpose);
            }
            PendingRoots.Clear();

            LevelGridProblemsWindowV2[] windows =
                Resources.FindObjectsOfTypeAll<LevelGridProblemsWindowV2>();
            for (int index = 0; index < windows.Length; index++)
            {
                windows[index].Repaint();
            }

            LevelGridEditorWindowV2[] gridEditors =
                Resources.FindObjectsOfTypeAll<LevelGridEditorWindowV2>();
            for (int index = 0; index < gridEditors.Length; index++)
            {
                gridEditors[index].RefreshAfterExternalValidation();
            }
            SceneView.RepaintAll();
        }

        private static bool IsPositionModification(string propertyPath)
        {
            return !string.IsNullOrEmpty(propertyPath)
                && propertyPath.StartsWith("m_LocalPosition");
        }
    }
}
#endif

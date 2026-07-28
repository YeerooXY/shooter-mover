#if UNITY_EDITOR
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Internal mechanics used by LevelGridEditorOperationsV2. This type owns no user-visible
    /// commands. Bulk inspection is deliberately read-only; an exact door moves only when an
    /// explicit canonical editor operation calls ReflowDoor.
    /// </summary>
    internal static class LevelGridDoorOperationsV2
    {
        /// <summary>
        /// Compatibility entry retained for existing callers. It no longer mutates authoring state;
        /// validation and Build must report required reflow rather than silently editing the scene.
        /// </summary>
        internal static int ReflowAll(LevelDesignSceneAuthoringRoot2D root)
        {
            return CountDoorsNeedingReflow(root);
        }

        internal static int CountDoorsNeedingReflow(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (root == null)
            {
                return 0;
            }

            LevelDoorEndpointAuthoring2D[] doors =
                root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            int count = 0;
            for (int index = 0; index < doors.Length; index++)
            {
                LevelDoorEndpointAuthoring2D door = doors[index];
                LevelDoorSideV2 expected;
                if (!TryResolveExpectedSide(root, door, out expected))
                {
                    continue;
                }

                bool sideMismatch = door.Side != expected;
                bool positionMismatch = !sideMismatch
                    && door.transform.localPosition != door.ResolveTargetLocalPosition();
                if (sideMismatch || positionMismatch)
                {
                    count++;
                }
            }
            return count;
        }

        internal static bool ReflowDoor(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDoorEndpointAuthoring2D door)
        {
            LevelDoorSideV2 expected;
            if (!TryResolveExpectedSide(root, door, out expected))
            {
                return false;
            }

            bool changed = door.Side != expected
                || door.transform.localPosition != ResolveTargetPosition(door, expected);
            if (!changed)
            {
                return false;
            }

            door.SetEdgeSideForAuthoring(expected);
            return true;
        }

        /// <summary>
        /// Physical deletion helper for the canonical LevelGridEditorOperationsV2 façade. No menu,
        /// inspector, context action or live-validation callback calls this method directly.
        /// </summary>
        internal static int DeleteDoorUndoable(
            LevelDoorEndpointAuthoring2D door,
            bool openProblemsWindow = true)
        {
            if (door == null)
            {
                return 0;
            }

            LevelDesignSceneAuthoringRoot2D root =
                door.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            if (root == null)
            {
                return 0;
            }

            List<LevelDoorLinkAuthoring2D> attached =
                FindAttachedConnections(root, door);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Level Door");
            for (int index = 0; index < attached.Count; index++)
            {
                DestroyConnectionWithUndo(attached[index]);
            }
            Undo.DestroyObjectImmediate(door.gameObject);
            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeObject = root;
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            LevelGridAuthoringV2LiveValidation.ValidateNow(
                root,
                LevelGridValidationPurposeV2.Draft,
                false);
            if (openProblemsWindow)
            {
                LevelGridProblemsWindowV2.Open(root);
            }
            SceneView.RepaintAll();

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.ShowNotification(new GUIContent(
                    "Door deleted; " + attached.Count
                    + " connection(s) removed. Ctrl+Z to undo."));
            }
            return attached.Count;
        }

        internal static List<LevelDoorLinkAuthoring2D> FindAttachedConnections(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDoorEndpointAuthoring2D door)
        {
            var attached = new List<LevelDoorLinkAuthoring2D>();
            if (root == null || door == null)
            {
                return attached;
            }

            LevelDoorLinkAuthoring2D[] links =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            for (int index = 0; index < links.Length; index++)
            {
                if (links[index].SourceDoor == door
                    || links[index].DestinationDoor == door)
                {
                    attached.Add(links[index]);
                }
            }
            return attached;
        }

        private static bool TryResolveExpectedSide(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDoorEndpointAuthoring2D door,
            out LevelDoorSideV2 expected)
        {
            expected = LevelDoorSideV2.North;
            if (root == null
                || door == null
                || door.PlacementMode != LevelDoorPlacementModeV2.EdgeManaged
                || !door.AutoFaceConnection
                || door.OwningRoom == null)
            {
                return false;
            }

            LevelDoorLinkAuthoring2D[] links =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            for (int index = 0; index < links.Length; index++)
            {
                LevelDoorLinkAuthoring2D link = links[index];
                LevelRoomAuthoring2D otherRoom = null;
                if (link.SourceDoor == door)
                {
                    otherRoom = link.DestinationRoom;
                }
                else if (link.DestinationDoor == door)
                {
                    otherRoom = link.SourceRoom;
                }

                if (otherRoom == null)
                {
                    continue;
                }

                return LevelGridAuthoringV2CompositeValidator.TryResolveFacingSide(
                    door.OwningRoom.GridCoordinate,
                    otherRoom.GridCoordinate,
                    out expected);
            }

            return false;
        }

        private static Vector3 ResolveTargetPosition(
            LevelDoorEndpointAuthoring2D door,
            LevelDoorSideV2 expected)
        {
            if (door.Side == expected)
            {
                return door.ResolveTargetLocalPosition();
            }

            // A side change necessarily requires an explicit canonical reflow. The exact resulting
            // position is calculated by SetEdgeSideForAuthoring after Undo has been recorded.
            return door.transform.localPosition;
        }

        private static void DestroyConnectionWithUndo(LevelDoorLinkAuthoring2D link)
        {
            if (link == null)
            {
                return;
            }

            Component[] components = link.GetComponents<Component>();
            if (components.Length <= 2)
            {
                Undo.DestroyObjectImmediate(link.gameObject);
            }
            else
            {
                Undo.DestroyObjectImmediate(link);
            }
        }
    }
}
#endif

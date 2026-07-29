#if UNITY_EDITOR
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Internal mechanics used by LevelGridEditorOperations. This type owns no user-visible
    /// commands. Bulk inspection is deliberately read-only; an exact door moves only when an
    /// explicit canonical editor operation calls ReflowDoor.
    /// </summary>
    internal static class LevelGridDoorOperations
    {
        /// <summary>
        /// Compatibility entry retained for existing callers. It no longer mutates authoring state;
        /// validation and Build must report required reflow rather than silently editing the scene.
        /// </summary>
        internal static int ReflowAll(LevelDraft root)
        {
            return CountDoorsNeedingReflow(root);
        }

        internal static int CountDoorsNeedingReflow(
            LevelDraft root)
        {
            if (root == null)
            {
                return 0;
            }

            DoorEndpoint[] doors =
                root.GetComponentsInChildren<DoorEndpoint>(true);
            int count = 0;
            for (int index = 0; index < doors.Length; index++)
            {
                DoorEndpoint door = doors[index];
                LevelDoorSide expected;
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
            LevelDraft root,
            DoorEndpoint door)
        {
            LevelDoorSide expected;
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
        /// Physical deletion helper for the canonical LevelGridEditorOperations façade. No menu,
        /// inspector, context action or live-validation callback calls this method directly.
        /// </summary>
        internal static int DeleteDoorUndoable(
            DoorEndpoint door,
            bool openProblemsWindow = true)
        {
            if (door == null)
            {
                return 0;
            }

            LevelDraft root =
                door.GetComponentInParent<LevelDraft>();
            if (root == null)
            {
                return 0;
            }

            List<DoorLink> attached =
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
            LevelGridAuthoringLiveValidation.ValidateNow(
                root,
                LevelGridValidationPurpose.Draft,
                false);
            if (openProblemsWindow)
            {
                LevelGridProblemsWindow.Open(root);
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

        internal static List<DoorLink> FindAttachedConnections(
            LevelDraft root,
            DoorEndpoint door)
        {
            var attached = new List<DoorLink>();
            if (root == null || door == null)
            {
                return attached;
            }

            DoorLink[] links =
                root.GetComponentsInChildren<DoorLink>(true);
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
            LevelDraft root,
            DoorEndpoint door,
            out LevelDoorSide expected)
        {
            expected = LevelDoorSide.North;
            if (root == null
                || door == null
                || door.PlacementMode != LevelDoorPlacementMode.EdgeManaged
                || !door.AutoFaceConnection
                || door.OwningRoom == null)
            {
                return false;
            }

            DoorLink[] links =
                root.GetComponentsInChildren<DoorLink>(true);
            for (int index = 0; index < links.Length; index++)
            {
                DoorLink link = links[index];
                LevelRoom otherRoom = null;
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

                return LevelGridAuthoringCompositeValidator.TryResolveFacingSide(
                    door.OwningRoom.GridCoordinate,
                    otherRoom.GridCoordinate,
                    out expected);
            }

            return false;
        }

        private static Vector3 ResolveTargetPosition(
            DoorEndpoint door,
            LevelDoorSide expected)
        {
            if (door.Side == expected)
            {
                return door.ResolveTargetLocalPosition();
            }

            // A side change necessarily requires an explicit canonical reflow. The exact resulting
            // position is calculated by SetEdgeSideForAuthoring after Undo has been recorded.
            return door.transform.localPosition;
        }

        private static void DestroyConnectionWithUndo(DoorLink link)
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

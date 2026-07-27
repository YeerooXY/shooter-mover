#if UNITY_EDITOR
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public static class LevelGridDoorOperationsV2
    {
        [MenuItem(
            "Tools/Shooter Mover/Level Design/Delete Selected Door (Undoable)",
            priority = 241)]
        private static void DeleteSelectedDoor()
        {
            LevelDoorEndpointAuthoring2D door = ResolveSelectedDoor();
            if (door == null)
            {
                EditorUtility.DisplayDialog(
                    "Delete Door",
                    "Select a Level Grid V2 door endpoint.",
                    "OK");
                return;
            }

            DeleteDoorUndoable(door);
        }

        [MenuItem(
            "CONTEXT/LevelDoorEndpointAuthoring2D/Delete Door (Undoable)")]
        private static void DeleteDoorFromContext(MenuCommand command)
        {
            LevelDoorEndpointAuthoring2D door =
                command.context as LevelDoorEndpointAuthoring2D;
            if (door != null)
            {
                DeleteDoorUndoable(door);
            }
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Reflow Selected Edge Door",
            priority = 242)]
        private static void ReflowSelectedDoor()
        {
            LevelDoorEndpointAuthoring2D door = ResolveSelectedDoor();
            if (door == null)
            {
                EditorUtility.DisplayDialog(
                    "Reflow Door",
                    "Select a Level Grid V2 door endpoint.",
                    "OK");
                return;
            }

            LevelDesignSceneAuthoringRoot2D root =
                door.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            if (root == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Reflow Edge-Managed Door");
            Undo.RecordObject(door, "Reflow Edge-Managed Door");
            Undo.RecordObject(door.transform, "Reflow Edge-Managed Door");
            door.SetAutoFaceConnectionForAuthoring(true);
            ReflowDoor(root, door);
            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.SetDirty(door);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            LevelGridAuthoringV2LiveValidation.ValidateNow(
                root,
                LevelGridValidationPurposeV2.Draft,
                false);
            SceneView.RepaintAll();
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Keep Selected Door Placement",
            priority = 243)]
        private static void KeepSelectedDoorPlacement()
        {
            LevelDoorEndpointAuthoring2D door = ResolveSelectedDoor();
            if (door == null)
            {
                return;
            }

            Undo.RecordObject(door, "Keep Door Placement");
            door.SetAutoFaceConnectionForAuthoring(false);
            EditorUtility.SetDirty(door);
            LevelDesignSceneAuthoringRoot2D root =
                door.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            if (root != null)
            {
                LevelGridAuthoringV2LiveValidation.ValidateNow(
                    root,
                    LevelGridValidationPurposeV2.Draft,
                    false);
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            }
            SceneView.RepaintAll();
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Capture Selected Door As Fixed",
            priority = 244)]
        private static void CaptureSelectedDoorAsFixed()
        {
            LevelDoorEndpointAuthoring2D door = ResolveSelectedDoor();
            if (door == null)
            {
                return;
            }

            Undo.RecordObject(door, "Capture Fixed Door Position");
            door.CaptureCurrentPositionAsFixedPlacement();
            EditorUtility.SetDirty(door);
            LevelDesignSceneAuthoringRoot2D root =
                door.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            if (root != null)
            {
                LevelGridAuthoringV2LiveValidation.ValidateNow(
                    root,
                    LevelGridValidationPurposeV2.Draft,
                    false);
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            }
            SceneView.RepaintAll();
        }

        public static int ReflowAll(LevelDesignSceneAuthoringRoot2D root)
        {
            if (root == null)
            {
                return 0;
            }

            LevelDoorEndpointAuthoring2D[] doors =
                root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            int changed = 0;
            for (int index = 0; index < doors.Length; index++)
            {
                LevelDoorEndpointAuthoring2D door = doors[index];
                if (door == null
                    || door.PlacementMode != LevelDoorPlacementModeV2.EdgeManaged
                    || !door.AutoFaceConnection)
                {
                    continue;
                }

                LevelDoorSideV2 expected;
                if (!TryResolveExpectedSide(root, door, out expected))
                {
                    continue;
                }

                Vector3 expectedPosition = ResolvePositionForSide(door, expected);
                bool sideChanged = door.Side != expected;
                bool positionChanged = door.transform.localPosition != expectedPosition;
                if (!sideChanged && !positionChanged)
                {
                    continue;
                }

                Undo.RecordObject(door, "Auto-Reflow Edge Door");
                Undo.RecordObject(door.transform, "Auto-Reflow Edge Door");
                door.SetEdgeSideForAuthoring(expected);
                EditorUtility.SetDirty(door);
                changed++;
            }

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            }
            return changed;
        }

        public static bool ReflowDoor(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDoorEndpointAuthoring2D door)
        {
            LevelDoorSideV2 expected;
            if (!TryResolveExpectedSide(root, door, out expected))
            {
                return false;
            }

            door.SetEdgeSideForAuthoring(expected);
            return true;
        }

        public static int DeleteDoorUndoable(
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

        public static List<LevelDoorLinkAuthoring2D> FindAttachedConnections(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDoorEndpointAuthoring2D door)
        {
            List<LevelDoorLinkAuthoring2D> attached =
                new List<LevelDoorLinkAuthoring2D>();
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

        private static Vector3 ResolvePositionForSide(
            LevelDoorEndpointAuthoring2D door,
            LevelDoorSideV2 expected)
        {
            LevelDoorSideV2 original = door.Side;
            if (original == expected)
            {
                return door.ResolveTargetLocalPosition();
            }

            // The public authoring setter also snaps. Use current position as a conservative
            // comparison and let the recorded setter perform the authoritative reflow.
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

        private static LevelDoorEndpointAuthoring2D ResolveSelectedDoor()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDoorEndpointAuthoring2D>();
        }
    }
}
#endif

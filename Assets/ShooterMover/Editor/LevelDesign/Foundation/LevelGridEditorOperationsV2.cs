#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public static class LevelGridEditorOperationsV2
    {
        private const int DestructiveLinkThreshold = 8;
        private const int DestructiveObjectThreshold = 100;
        private static readonly Vector2 DefaultCellSize = new Vector2(20f, 14f);

        public static int FindNextFreeFolderSlot(
            LevelDesignSceneAuthoringRoot2D root,
            Vector2Int coordinate)
        {
            if (root == null)
            {
                return 1;
            }

            HashSet<int> occupied = new HashSet<int>();
            LevelRoomAuthoring2D[] rooms =
                root.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
            for (int index = 0; index < rooms.Length; index++)
            {
                if (rooms[index].GridCoordinate == coordinate)
                {
                    occupied.Add(Mathf.Max(1, rooms[index].FolderSlot));
                }
            }

            int slot = 1;
            while (occupied.Contains(slot))
            {
                slot++;
            }
            return slot;
        }

        public static LevelRoomAuthoring2D CreateRoom(
            LevelDesignSceneAuthoringRoot2D root,
            Vector2Int coordinate)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            int nextFolderSlot = FindNextFreeFolderSlot(root, coordinate);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Level Grid Room");

            GameObject roomObject = new GameObject("Room " + coordinate.x + "," + coordinate.y);
            roomObject.transform.SetParent(root.transform, false);
            Undo.RegisterCreatedObjectUndo(roomObject, "Create Level Grid Room");

            Vector2 cellSize = ResolveCellSize(root);
            BoxCollider2D bounds = Undo.AddComponent<BoxCollider2D>(roomObject);
            bounds.size = cellSize;

            LevelRoomAuthoring2D room =
                Undo.AddComponent<LevelRoomAuthoring2D>(roomObject);
            room.AssignNewStableId();
            SerializedObject serialized = new SerializedObject(room);
            serialized.FindProperty("displayName").stringValue = string.Empty;
            serialized.FindProperty("gridCoordinate").vector2IntValue = coordinate;
            serialized.FindProperty("folderSlot").intValue = nextFolderSlot;
            serialized.FindProperty("cellSize").vector2Value = cellSize;
            serialized.FindProperty("footprintCells").vector2IntValue = Vector2Int.one;
            serialized.FindProperty("roomBounds").objectReferenceValue = bounds;
            serialized.FindProperty("mapCoordinate").vector2IntValue = coordinate;
            serialized.FindProperty("visibleOnMap").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            room.SnapToAuthoredGrid();

            Undo.CollapseUndoOperations(group);
            MarkChanged(root, room);
            Selection.activeObject = room;
            return room;
        }

        public static void MoveRoom(
            LevelRoomAuthoring2D room,
            Vector2Int coordinate)
        {
            if (room == null || room.GridCoordinate == coordinate)
            {
                return;
            }

            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(room);
            if (root == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Move Level Grid Room");
            Undo.RecordObject(room, "Move Level Grid Room");
            Undo.RecordObject(room.transform, "Move Level Grid Room");

            SerializedObject serialized = new SerializedObject(room);
            serialized.FindProperty("gridCoordinate").vector2IntValue = coordinate;
            serialized.ApplyModifiedProperties();
            room.SnapToAuthoredGrid();
            LevelGridDoorOperationsV2.ReflowAll(root);

            Undo.CollapseUndoOperations(group);
            MarkChanged(root, room);
        }

        public static void SetRoomDisplayName(
            LevelRoomAuthoring2D room,
            string displayName)
        {
            if (room == null)
            {
                return;
            }

            string normalized = string.IsNullOrWhiteSpace(displayName)
                ? string.Empty
                : displayName.Trim();
            if (string.Equals(room.DisplayName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(room);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rename Level Grid Room");
            Undo.RecordObject(room, "Rename Level Grid Room");
            Undo.RecordObject(room.gameObject, "Rename Level Grid Room");
            SerializedObject serialized = new SerializedObject(room);
            serialized.FindProperty("displayName").stringValue = normalized;
            serialized.ApplyModifiedProperties();
            room.gameObject.name = string.IsNullOrEmpty(normalized)
                ? "Room " + room.GridCoordinate.x + "," + room.GridCoordinate.y
                : normalized;
            Undo.CollapseUndoOperations(group);
            MarkChanged(root, room);
        }

        public static void SetConnectionTravelPolicy(
            LevelDoorLinkAuthoring2D connection,
            LevelDoorTravelPolicy travelPolicy)
        {
            if (connection == null || connection.TravelPolicy == travelPolicy)
            {
                return;
            }

            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(connection);
            Undo.RecordObject(connection, "Change Door Travel Policy");
            connection.ConfigureConnection(
                connection.ConnectionIdText,
                connection.SourceRoom,
                connection.SourceDoor,
                connection.DestinationRoom,
                connection.DestinationDoor,
                travelPolicy);
            MarkChanged(root, connection);
        }

        public static void SetFolderSlot(LevelRoomAuthoring2D room, int folderSlot)
        {
            if (room == null)
            {
                return;
            }

            int normalized = Mathf.Max(1, folderSlot);
            if (room.FolderSlot == normalized)
            {
                return;
            }

            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(room);
            Undo.RecordObject(room, "Change Room Folder Slot");
            SerializedObject serialized = new SerializedObject(room);
            serialized.FindProperty("folderSlot").intValue = normalized;
            serialized.ApplyModifiedProperties();
            MarkChanged(root, room);
        }

        public static void ResizeRoom(
            LevelRoomAuthoring2D room,
            Vector2Int footprint)
        {
            if (room == null)
            {
                return;
            }

            footprint.x = Mathf.Max(1, footprint.x);
            footprint.y = Mathf.Max(1, footprint.y);
            if (room.FootprintCells == footprint)
            {
                return;
            }

            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(room);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Resize Level Grid Room");
            Undo.RecordObject(room, "Resize Level Grid Room");
            Undo.RecordObject(room.transform, "Resize Level Grid Room");
            if (room.RoomBounds != null)
            {
                Undo.RecordObject(room.RoomBounds, "Resize Level Grid Room");
            }

            SerializedObject serialized = new SerializedObject(room);
            serialized.FindProperty("footprintCells").vector2IntValue = footprint;
            serialized.ApplyModifiedProperties();
            BoxCollider2D box = room.RoomBounds as BoxCollider2D;
            if (box != null)
            {
                box.size = new Vector2(
                    room.CellSize.x * footprint.x,
                    room.CellSize.y * footprint.y);
            }
            room.SnapToAuthoredGrid();
            LevelGridDoorOperationsV2.ReflowAll(root);

            Undo.CollapseUndoOperations(group);
            MarkChanged(root, room);
        }

        public static LevelDoorEndpointAuthoring2D CreateDoor(
            LevelRoomAuthoring2D room,
            LevelDoorSideV2 side,
            float edgeOffset)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(room);
            if (root == null)
            {
                throw new InvalidOperationException(
                    "The room must belong to a LevelDesignSceneAuthoringRoot2D.");
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Level Grid Door");
            GameObject doorObject = new GameObject(side + " Door");
            doorObject.transform.SetParent(room.transform, false);
            Undo.RegisterCreatedObjectUndo(doorObject, "Add Level Grid Door");
            LevelDoorEndpointAuthoring2D door =
                Undo.AddComponent<LevelDoorEndpointAuthoring2D>(doorObject);
            door.AssignNewStableId();
            door.ConfigureAuthoring(
                door.DoorIdText,
                room,
                side,
                LevelDoorPlacementModeV2.EdgeManaged,
                Mathf.Clamp01(edgeOffset),
                Vector2.zero,
                true,
                true);
            door.SnapToPlacement();
            Undo.CollapseUndoOperations(group);

            MarkChanged(root, door);
            Selection.activeObject = door;
            return door;
        }

        public static void UpdateDoor(
            LevelDoorEndpointAuthoring2D door,
            LevelDoorSideV2 side,
            LevelDoorPlacementModeV2 placementMode,
            float edgeOffset,
            Vector2 fixedRoomRelativePosition,
            bool traversable,
            bool visibleOnMap,
            bool automaticFacing)
        {
            if (door == null)
            {
                return;
            }

            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(door);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Edit Level Grid Door");
            Undo.RecordObject(door, "Edit Level Grid Door");
            Undo.RecordObject(door.transform, "Edit Level Grid Door");

            SerializedObject serialized = new SerializedObject(door);
            serialized.FindProperty("side").enumValueIndex = (int)side - 1;
            serialized.FindProperty("placementMode").enumValueIndex =
                (int)placementMode - 1;
            serialized.FindProperty("edgeOffset").floatValue = Mathf.Clamp01(edgeOffset);
            serialized.FindProperty("fixedLocalPosition").vector2Value =
                fixedRoomRelativePosition;
            SerializedProperty version =
                serialized.FindProperty("fixedPositionSpaceVersion");
            if (version != null)
            {
                version.intValue = 1;
            }
            serialized.FindProperty("traversable").boolValue = traversable;
            serialized.FindProperty("visibleOnMap").boolValue = visibleOnMap;
            serialized.FindProperty("autoFaceConnection").boolValue = automaticFacing;
            serialized.ApplyModifiedProperties();
            door.SnapToPlacement();
            if (root != null)
            {
                LevelGridDoorOperationsV2.ReflowDoor(root, door);
            }

            Undo.CollapseUndoOperations(group);
            MarkChanged(root, door);
        }

        public static bool TryCreateConnection(
            LevelDesignSceneAuthoringRoot2D selectedRoot,
            LevelDoorEndpointAuthoring2D source,
            LevelDoorEndpointAuthoring2D destination,
            out LevelDoorLinkAuthoring2D created,
            out string rejection)
        {
            created = null;
            rejection = ValidateConnectionAttempt(selectedRoot, source, destination);
            if (!string.IsNullOrEmpty(rejection))
            {
                return false;
            }

            LevelRoomAuthoring2D sourceRoom = source.OwningRoom;
            LevelRoomAuthoring2D destinationRoom = destination.OwningRoom;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect Level Grid Doors");

            GameObject connectionObject = new GameObject("Door Connection");
            connectionObject.transform.SetParent(selectedRoot.transform, false);
            Undo.RegisterCreatedObjectUndo(
                connectionObject,
                "Connect Level Grid Doors");
            created = Undo.AddComponent<LevelDoorLinkAuthoring2D>(connectionObject);
            created.AssignNewStableId();
            created.ConfigureConnection(
                created.ConnectionIdText,
                sourceRoom,
                source,
                destinationRoom,
                destination,
                LevelDoorTravelPolicy.Bidirectional);
            Undo.RecordObject(source, "Connect Level Grid Doors");
            Undo.RecordObject(source.transform, "Connect Level Grid Doors");
            Undo.RecordObject(destination, "Connect Level Grid Doors");
            Undo.RecordObject(destination.transform, "Connect Level Grid Doors");
            LevelGridDoorOperationsV2.ReflowDoor(selectedRoot, source);
            LevelGridDoorOperationsV2.ReflowDoor(selectedRoot, destination);
            Undo.CollapseUndoOperations(group);

            MarkChanged(selectedRoot, created);
            Selection.activeObject = created;
            return true;
        }

        public static string ValidateConnectionAttempt(
            LevelDesignSceneAuthoringRoot2D selectedRoot,
            LevelDoorEndpointAuthoring2D source,
            LevelDoorEndpointAuthoring2D destination)
        {
            if (selectedRoot == null)
            {
                return "Select a level root before connecting doors.";
            }
            if (source == null || destination == null)
            {
                return "Drag from one door endpoint onto another endpoint.";
            }
            if (source == destination)
            {
                return "A door endpoint cannot connect to itself.";
            }
            if (source.OwningRoom == null || destination.OwningRoom == null)
            {
                return "Both endpoints must have an owning room.";
            }
            if (source.OwningRoom == destination.OwningRoom)
            {
                return "Endpoints must belong to different rooms.";
            }
            if (ResolveRoot(source) != selectedRoot || ResolveRoot(destination) != selectedRoot)
            {
                return "Both endpoints must belong to the selected level root.";
            }
            if (IsConnected(selectedRoot, source))
            {
                return "The source endpoint is already connected.";
            }
            if (IsConnected(selectedRoot, destination))
            {
                return "The destination endpoint is already connected.";
            }
            if (HasDuplicateLink(selectedRoot, source, destination))
            {
                return "That endpoint-to-endpoint link already exists.";
            }
            return string.Empty;
        }

        public static bool IsConnected(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDoorEndpointAuthoring2D door)
        {
            return FindAttachedConnection(root, door) != null;
        }

        public static LevelDoorLinkAuthoring2D FindAttachedConnection(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDoorEndpointAuthoring2D door)
        {
            if (root == null || door == null)
            {
                return null;
            }

            List<LevelDoorLinkAuthoring2D> links =
                LevelGridDoorOperationsV2.FindAttachedConnections(root, door);
            return links.Count == 0 ? null : links[0];
        }

        public static void DeleteConnection(LevelDoorLinkAuthoring2D connection)
        {
            if (connection == null)
            {
                return;
            }

            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(connection);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Level Grid Connection");
            DestroyComponentOrObjectWithUndo(connection);
            Undo.CollapseUndoOperations(group);
            Selection.activeObject = root;
            MarkChanged(root, root);
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.ShowNotification(new GUIContent(
                    "Connection deleted; both endpoints remain. Ctrl+Z to undo."));
            }
        }

        public static void DeleteDoor(LevelDoorEndpointAuthoring2D door)
        {
            LevelGridDoorOperationsV2.DeleteDoorUndoable(door, false);
        }

        public static bool DeleteRoom(LevelRoomAuthoring2D room, bool allowModalWarning)
        {
            if (room == null)
            {
                return false;
            }

            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(room);
            if (root == null)
            {
                return false;
            }

            List<LevelDoorLinkAuthoring2D> attached =
                new List<LevelDoorLinkAuthoring2D>();
            LevelDoorLinkAuthoring2D[] links =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            for (int index = 0; index < links.Length; index++)
            {
                if (links[index].SourceRoom == room
                    || links[index].DestinationRoom == room)
                {
                    attached.Add(links[index]);
                }
            }

            int objectCount = room.GetComponentsInChildren<Transform>(true).Length;
            bool unusuallyDestructive = attached.Count > DestructiveLinkThreshold
                || objectCount > DestructiveObjectThreshold;
            if (allowModalWarning
                && unusuallyDestructive
                && !EditorUtility.DisplayDialog(
                    "Delete unusually large room?",
                    "This undoable deletion removes " + objectCount
                        + " room objects and " + attached.Count
                        + " connections. Continue?",
                    "Delete",
                    "Cancel"))
            {
                return false;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Level Grid Room");
            for (int index = 0; index < attached.Count; index++)
            {
                DestroyComponentOrObjectWithUndo(attached[index]);
            }
            Undo.DestroyObjectImmediate(room.gameObject);
            Undo.CollapseUndoOperations(group);
            Selection.activeObject = root;
            MarkChanged(root, root);
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.ShowNotification(new GUIContent(
                    "Room deleted; neighbouring endpoints remain unresolved. Ctrl+Z to undo."));
            }
            return true;
        }

        public static void ReflowDoor(LevelDoorEndpointAuthoring2D door)
        {
            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(door);
            if (root == null || door == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Reflow Level Grid Door");
            Undo.RecordObject(door, "Reflow Level Grid Door");
            Undo.RecordObject(door.transform, "Reflow Level Grid Door");
            door.SetAutoFaceConnectionForAuthoring(true);
            LevelGridDoorOperationsV2.ReflowDoor(root, door);
            Undo.CollapseUndoOperations(group);
            MarkChanged(root, door);
        }

        public static void KeepDoorPlacement(LevelDoorEndpointAuthoring2D door)
        {
            LevelDesignSceneAuthoringRoot2D root = ResolveRoot(door);
            if (root == null || door == null)
            {
                return;
            }

            Undo.RecordObject(door, "Keep Level Grid Door Placement");
            door.SetAutoFaceConnectionForAuthoring(false);
            MarkChanged(root, door);
        }

        public static void Validate(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridValidationPurposeV2 purpose)
        {
            if (root == null)
            {
                return;
            }

            LevelGridDoorOperationsV2.ReflowAll(root);
            root.ValidateHierarchy();
            root.ValidateGridAuthoring(purpose);
            LevelGridAuthoringV2LiveValidation.MarkSynchronouslyValidated(root);
            EditorUtility.SetDirty(root);
            SceneView.RepaintAll();
        }

        public static LevelDesignSceneAuthoringRoot2D ResolveRoot(Component component)
        {
            return component == null
                ? null
                : component.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
        }

        private static bool HasDuplicateLink(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDoorEndpointAuthoring2D source,
            LevelDoorEndpointAuthoring2D destination)
        {
            LevelDoorLinkAuthoring2D[] links =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            for (int index = 0; index < links.Length; index++)
            {
                bool sameDirection = links[index].SourceDoor == source
                    && links[index].DestinationDoor == destination;
                bool reverseDirection = links[index].SourceDoor == destination
                    && links[index].DestinationDoor == source;
                if (sameDirection || reverseDirection)
                {
                    return true;
                }
            }
            return false;
        }

        private static Vector2 ResolveCellSize(LevelDesignSceneAuthoringRoot2D root)
        {
            LevelRoomAuthoring2D[] rooms =
                root.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
            for (int index = 0; index < rooms.Length; index++)
            {
                if (rooms[index].CellSize.x > 0f && rooms[index].CellSize.y > 0f)
                {
                    return rooms[index].CellSize;
                }
            }
            return DefaultCellSize;
        }

        private static void DestroyComponentOrObjectWithUndo(Component component)
        {
            if (component == null)
            {
                return;
            }

            Component[] components = component.GetComponents<Component>();
            if (components.Length <= 2)
            {
                Undo.DestroyObjectImmediate(component.gameObject);
            }
            else
            {
                Undo.DestroyObjectImmediate(component);
            }
        }

        private static void MarkChanged(
            LevelDesignSceneAuthoringRoot2D root,
            UnityEngine.Object changed)
        {
            if (changed != null)
            {
                EditorUtility.SetDirty(changed);
            }
            if (root == null)
            {
                return;
            }

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            root.ValidateHierarchy();
            root.ValidateGridAuthoring(LevelGridValidationPurposeV2.Draft);
            LevelGridAuthoringV2LiveValidation.MarkSynchronouslyValidated(root);
            SceneView.RepaintAll();
        }
    }
}
#endif

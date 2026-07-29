#if UNITY_EDITOR
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public static class LevelGridAuthoringThreeRoomExampleMenu
    {
        private static readonly Vector2 RoomCellSize = new Vector2(20f, 14f);
        private static readonly Vector2Int RoomFootprint = Vector2Int.one;

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Create Three-Room Starter Example",
            priority = 245)]
        private static void CreateThreeRoomStarterExample()
        {
            LevelDraft root = ResolveSelectedRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Create Three-Room Example",
                    "Select an object below a LevelDraft.",
                    "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Three-Room Starter Example");

            GameObject exampleRoot = CreateObject(
                "Three-Room Starter Example",
                root.transform);

            LevelRoom starter = CreateRoom(
                exampleRoot.transform,
                "Starter Room",
                "Starter Room",
                new Vector2Int(0, 0),
                1);
            LevelRoom rightOne = CreateRoom(
                exampleRoot.transform,
                "Room 1,0",
                string.Empty,
                new Vector2Int(1, 0),
                1);
            LevelRoom rightTwo = CreateRoom(
                exampleRoot.transform,
                "Room 2,0",
                string.Empty,
                new Vector2Int(2, 0),
                1);

            DoorEndpoint starterEast = CreateDoor(
                starter,
                "East Door",
                LevelDoorSide.East);
            DoorEndpoint rightOneWest = CreateDoor(
                rightOne,
                "West Door",
                LevelDoorSide.West);
            DoorEndpoint rightOneEast = CreateDoor(
                rightOne,
                "East Door",
                LevelDoorSide.East);
            DoorEndpoint rightTwoWest = CreateDoor(
                rightTwo,
                "West Door",
                LevelDoorSide.West);

            CreateConnection(
                exampleRoot.transform,
                "Starter to Room 1,0",
                starter,
                starterEast,
                rightOne,
                rightOneWest);
            CreateConnection(
                exampleRoot.transform,
                "Room 1,0 to Room 2,0",
                rightOne,
                rightOneEast,
                rightTwo,
                rightTwoWest);

            LevelGridDoorOperations.ReflowAll(root);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

            LevelDesignValidationResult foundation = root.ValidateHierarchy();
            LevelGridValidationResult graph = root.ValidateGridAuthoring(
                LevelGridValidationPurpose.ProductionPublish);
            Selection.activeGameObject = exampleRoot;
            SceneView.RepaintAll();

            if (!foundation.IsValid || !graph.CanPublish)
            {
                Debug.LogWarning(
                    "The generated three-room Phase 1 example exists, but the current "
                        + "level root does not pass the combined validated-authoring gate. "
                        + "Inspect Foundation validation and the Level Problems panel.",
                    root);
                LevelGridProblemsWindow.Open(root);
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.FrameSelected();
                sceneView.ShowNotification(new GUIContent(
                    "Created 3 connected rooms. Ctrl+Z to undo."));
            }
        }

        private static LevelRoom CreateRoom(
            Transform parent,
            string hierarchyName,
            string optionalDisplayName,
            Vector2Int gridCoordinate,
            int folderSlot)
        {
            GameObject roomObject = CreateObject(hierarchyName, parent);
            BoxCollider2D bounds = Undo.AddComponent<BoxCollider2D>(roomObject);
            bounds.size = new Vector2(
                RoomCellSize.x * RoomFootprint.x,
                RoomCellSize.y * RoomFootprint.y);

            LevelRoom room =
                Undo.AddComponent<LevelRoom>(roomObject);
            room.AssignNewStableId();

            SerializedObject serialized = new SerializedObject(room);
            serialized.FindProperty("displayName").stringValue = optionalDisplayName;
            serialized.FindProperty("gridCoordinate").vector2IntValue = gridCoordinate;
            serialized.FindProperty("folderSlot").intValue = folderSlot;
            serialized.FindProperty("cellSize").vector2Value = RoomCellSize;
            serialized.FindProperty("footprintCells").vector2IntValue = RoomFootprint;
            serialized.FindProperty("roomBounds").objectReferenceValue = bounds;
            serialized.FindProperty("mapCoordinate").vector2IntValue = gridCoordinate;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            room.SnapToAuthoredGrid();
            return room;
        }

        private static DoorEndpoint CreateDoor(
            LevelRoom room,
            string hierarchyName,
            LevelDoorSide side)
        {
            GameObject doorObject = CreateObject(hierarchyName, room.transform);
            DoorEndpoint door =
                Undo.AddComponent<DoorEndpoint>(doorObject);
            door.AssignNewStableId();
            door.ConfigureAuthoring(
                door.DoorIdText,
                room,
                side,
                LevelDoorPlacementMode.EdgeManaged,
                0.5f,
                Vector2.zero,
                true,
                true);
            door.SnapToPlacement();
            return door;
        }

        private static DoorLink CreateConnection(
            Transform parent,
            string hierarchyName,
            LevelRoom sourceRoom,
            DoorEndpoint sourceDoor,
            LevelRoom destinationRoom,
            DoorEndpoint destinationDoor)
        {
            GameObject connectionObject = CreateObject(hierarchyName, parent);
            DoorLink connection =
                Undo.AddComponent<DoorLink>(connectionObject);
            connection.AssignNewStableId();
            connection.ConfigureConnection(
                connection.ConnectionIdText,
                sourceRoom,
                sourceDoor,
                destinationRoom,
                destinationDoor,
                LevelDoorTravelPolicy.Bidirectional);
            return connection;
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name);
            created.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            return created;
        }

        private static LevelDraft ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDraft>();
        }
    }
}
#endif

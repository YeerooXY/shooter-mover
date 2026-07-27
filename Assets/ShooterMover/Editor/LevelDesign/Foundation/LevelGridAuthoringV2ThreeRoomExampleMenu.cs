#if UNITY_EDITOR
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public static class LevelGridAuthoringV2ThreeRoomExampleMenu
    {
        private static readonly Vector2 RoomCellSize = new Vector2(20f, 14f);
        private static readonly Vector2Int RoomFootprint = Vector2Int.one;

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Create Three-Room Starter Example",
            priority = 245)]
        private static void CreateThreeRoomStarterExample()
        {
            LevelDesignSceneAuthoringRoot2D root = ResolveSelectedRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Create Three-Room Example",
                    "Select an object below a LevelDesignSceneAuthoringRoot2D.",
                    "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Three-Room Starter Example");

            GameObject exampleRoot = CreateObject(
                "Three-Room Starter Example",
                root.transform);

            LevelRoomAuthoring2D starter = CreateRoom(
                exampleRoot.transform,
                "Starter Room",
                "Starter Room",
                new Vector2Int(0, 0));
            LevelRoomAuthoring2D rightOne = CreateRoom(
                exampleRoot.transform,
                "Room 1,0",
                string.Empty,
                new Vector2Int(1, 0));
            LevelRoomAuthoring2D rightTwo = CreateRoom(
                exampleRoot.transform,
                "Room 2,0",
                string.Empty,
                new Vector2Int(2, 0));

            LevelDoorEndpointAuthoring2D starterEast = CreateDoor(
                starter,
                "East Door",
                LevelDoorSideV2.East);
            LevelDoorEndpointAuthoring2D rightOneWest = CreateDoor(
                rightOne,
                "West Door",
                LevelDoorSideV2.West);
            LevelDoorEndpointAuthoring2D rightOneEast = CreateDoor(
                rightOne,
                "East Door",
                LevelDoorSideV2.East);
            LevelDoorEndpointAuthoring2D rightTwoWest = CreateDoor(
                rightTwo,
                "West Door",
                LevelDoorSideV2.West);

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

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

            LevelGridValidationResultV2 validation = root.ValidateGridAuthoring(
                LevelGridValidationPurposeV2.ProductionPublish);
            Selection.activeGameObject = exampleRoot;
            SceneView.RepaintAll();

            if (!validation.CanPublish)
            {
                Debug.LogError(
                    "The generated three-room example did not pass production "
                        + "validation. Inspect the Level Problems panel.",
                    root);
                LevelGridProblemsWindowV2.Open(root);
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

        private static LevelRoomAuthoring2D CreateRoom(
            Transform parent,
            string hierarchyName,
            string optionalDisplayName,
            Vector2Int gridCoordinate)
        {
            GameObject roomObject = CreateObject(hierarchyName, parent);
            BoxCollider2D bounds = Undo.AddComponent<BoxCollider2D>(roomObject);
            bounds.size = new Vector2(
                RoomCellSize.x * RoomFootprint.x,
                RoomCellSize.y * RoomFootprint.y);

            LevelRoomAuthoring2D room =
                Undo.AddComponent<LevelRoomAuthoring2D>(roomObject);
            room.AssignNewStableId();

            SerializedObject serialized = new SerializedObject(room);
            serialized.FindProperty("displayName").stringValue = optionalDisplayName;
            serialized.FindProperty("gridCoordinate").vector2IntValue = gridCoordinate;
            serialized.FindProperty("cellSize").vector2Value = RoomCellSize;
            serialized.FindProperty("footprintCells").vector2IntValue = RoomFootprint;
            serialized.FindProperty("roomBounds").objectReferenceValue = bounds;
            serialized.FindProperty("mapCoordinate").vector2IntValue = gridCoordinate;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            room.SnapToAuthoredGrid();
            return room;
        }

        private static LevelDoorEndpointAuthoring2D CreateDoor(
            LevelRoomAuthoring2D room,
            string hierarchyName,
            LevelDoorSideV2 side)
        {
            GameObject doorObject = CreateObject(hierarchyName, room.transform);
            LevelDoorEndpointAuthoring2D door =
                Undo.AddComponent<LevelDoorEndpointAuthoring2D>(doorObject);
            door.AssignNewStableId();

            SerializedObject serialized = new SerializedObject(door);
            serialized.FindProperty("owningRoom").objectReferenceValue = room;
            serialized.FindProperty("side").enumValueIndex = (int)side - 1;
            serialized.FindProperty("placementMode").enumValueIndex =
                (int)LevelDoorPlacementModeV2.EdgeManaged - 1;
            serialized.FindProperty("edgeOffset").floatValue = 0.5f;
            serialized.FindProperty("traversable").boolValue = true;
            serialized.FindProperty("visibleOnMap").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            door.SnapToPlacement();
            return door;
        }

        private static LevelDoorLinkAuthoring2D CreateConnection(
            Transform parent,
            string hierarchyName,
            LevelRoomAuthoring2D sourceRoom,
            LevelDoorEndpointAuthoring2D sourceDoor,
            LevelRoomAuthoring2D destinationRoom,
            LevelDoorEndpointAuthoring2D destinationDoor)
        {
            GameObject connectionObject = CreateObject(hierarchyName, parent);
            LevelDoorLinkAuthoring2D connection =
                Undo.AddComponent<LevelDoorLinkAuthoring2D>(connectionObject);
            connection.AssignNewStableId();

            SerializedObject serialized = new SerializedObject(connection);
            serialized.FindProperty("sourceRoom").objectReferenceValue = sourceRoom;
            serialized.FindProperty("sourceDoor").objectReferenceValue = sourceDoor;
            serialized.FindProperty("destinationRoom").objectReferenceValue =
                destinationRoom;
            serialized.FindProperty("destinationDoor").objectReferenceValue =
                destinationDoor;
            serialized.FindProperty("travelPolicy").enumValueIndex =
                (int)LevelDoorTravelPolicy.Bidirectional - 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return connection;
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name);
            created.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            return created;
        }

        private static LevelDesignSceneAuthoringRoot2D ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
        }
    }
}
#endif

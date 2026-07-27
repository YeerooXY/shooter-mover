#if UNITY_EDITOR
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public static class LevelGridAuthoringV2CreationMenu
    {
        [MenuItem(
            "GameObject/Shooter Mover/Level Grid V2/Door Endpoint",
            false,
            20)]
        private static void CreateDoorEndpoint(MenuCommand command)
        {
            GameObject context = command.context as GameObject;
            if (context == null)
            {
                context = Selection.activeGameObject;
            }

            LevelRoomAuthoring2D room = context == null
                ? null
                : context.GetComponentInParent<LevelRoomAuthoring2D>();
            if (room == null)
            {
                EditorUtility.DisplayDialog(
                    "Create Door Endpoint",
                    "Select a room or one of its children first.",
                    "OK");
                return;
            }

            GameObject endpointObject = new GameObject("Door Endpoint");
            Undo.RegisterCreatedObjectUndo(endpointObject, "Create Door Endpoint");
            endpointObject.transform.SetParent(room.transform, false);
            LevelDoorEndpointAuthoring2D endpoint =
                Undo.AddComponent<LevelDoorEndpointAuthoring2D>(endpointObject);
            endpoint.AssignNewStableId();
            endpoint.SnapToPlacement();

            Selection.activeGameObject = endpointObject;
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            SceneView.RepaintAll();
        }

        [MenuItem(
            "GameObject/Shooter Mover/Level Grid V2/Connect Selected Door Endpoints",
            false,
            21)]
        private static void ConnectSelectedDoorEndpoints(MenuCommand command)
        {
            LevelDoorEndpointAuthoring2D[] endpoints = GetSelectedEndpoints();
            if (endpoints.Length != 2)
            {
                EditorUtility.DisplayDialog(
                    "Connect Door Endpoints",
                    "Select exactly two door endpoint objects.",
                    "OK");
                return;
            }

            LevelRoomAuthoring2D sourceRoom = endpoints[0].OwningRoom;
            LevelRoomAuthoring2D destinationRoom = endpoints[1].OwningRoom;
            LevelDesignSceneAuthoringRoot2D sourceRoot = sourceRoom == null
                ? null
                : sourceRoom.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            LevelDesignSceneAuthoringRoot2D destinationRoot = destinationRoom == null
                ? null
                : destinationRoom.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            if (sourceRoom == null
                || destinationRoom == null
                || sourceRoom == destinationRoom
                || sourceRoot == null
                || sourceRoot != destinationRoot)
            {
                EditorUtility.DisplayDialog(
                    "Connect Door Endpoints",
                    "The two endpoints must belong to different rooms under the same level root.",
                    "OK");
                return;
            }

            GameObject connectionObject = new GameObject("Door Connection");
            Undo.RegisterCreatedObjectUndo(
                connectionObject,
                "Connect Door Endpoints");
            connectionObject.transform.SetParent(sourceRoot.transform, false);
            LevelDoorLinkAuthoring2D link =
                Undo.AddComponent<LevelDoorLinkAuthoring2D>(connectionObject);
            link.AssignNewStableId();
            link.ConfigureForTests(
                link.ConnectionIdText,
                sourceRoom,
                endpoints[0],
                destinationRoom,
                endpoints[1]);

            sourceRoot.ValidateGridAuthoring(LevelGridValidationPurposeV2.Draft);
            Selection.activeGameObject = connectionObject;
            EditorSceneManager.MarkSceneDirty(sourceRoot.gameObject.scene);
            SceneView.RepaintAll();
        }

        [MenuItem(
            "GameObject/Shooter Mover/Level Grid V2/Connect Selected Door Endpoints",
            true)]
        private static bool ValidateConnectSelectedDoorEndpoints()
        {
            return GetSelectedEndpoints().Length == 2;
        }

        private static LevelDoorEndpointAuthoring2D[] GetSelectedEndpoints()
        {
            GameObject[] selected = Selection.gameObjects;
            List<LevelDoorEndpointAuthoring2D> endpoints =
                new List<LevelDoorEndpointAuthoring2D>();
            for (int index = 0; index < selected.Length; index++)
            {
                LevelDoorEndpointAuthoring2D endpoint =
                    selected[index].GetComponent<LevelDoorEndpointAuthoring2D>();
                if (endpoint != null && !endpoints.Contains(endpoint))
                {
                    endpoints.Add(endpoint);
                }
            }

            return endpoints.ToArray();
        }
    }
}
#endif

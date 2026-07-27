#if UNITY_EDITOR
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Compatibility GameObject menus. These callbacks select context only; all topology mutation is
    /// delegated to LevelGridEditorOperationsV2.
    /// </summary>
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

            try
            {
                LevelGridEditorOperationsV2.CreateDoor(
                    room,
                    LevelDoorSideV2.North,
                    0.5f);
            }
            catch (System.Exception exception)
            {
                if (exception is System.OutOfMemoryException
                    || exception is System.StackOverflowException
                    || exception is System.AccessViolationException)
                {
                    throw;
                }

                EditorUtility.DisplayDialog(
                    "Create Door Endpoint Failed",
                    exception.Message,
                    "OK");
            }
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

            LevelDesignSceneAuthoringRoot2D root =
                LevelGridEditorOperationsV2.ResolveRoot(endpoints[0]);
            LevelDoorLinkAuthoring2D created;
            string rejection;
            if (!LevelGridEditorOperationsV2.TryCreateConnection(
                    root,
                    endpoints[0],
                    endpoints[1],
                    out created,
                    out rejection))
            {
                EditorUtility.DisplayDialog(
                    "Connect Door Endpoints",
                    rejection,
                    "OK");
            }
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
            var endpoints = new List<LevelDoorEndpointAuthoring2D>();
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

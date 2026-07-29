#if UNITY_EDITOR
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Compatibility GameObject menus. These callbacks select context only; all topology mutation is
    /// delegated to LevelGridEditorOperations.
    /// </summary>
    public static class LevelGridAuthoringCreationMenu
    {
        [MenuItem(
            "GameObject/Shooter Mover/Level Level/Door Endpoint",
            false,
            20)]
        private static void CreateDoorEndpoint(MenuCommand command)
        {
            GameObject context = command.context as GameObject;
            if (context == null)
            {
                context = Selection.activeGameObject;
            }

            LevelRoom room = context == null
                ? null
                : context.GetComponentInParent<LevelRoom>();
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
                LevelGridEditorOperations.CreateDoor(
                    room,
                    LevelDoorSide.North,
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
            "GameObject/Shooter Mover/Level Level/Connect Selected Door Endpoints",
            false,
            21)]
        private static void ConnectSelectedDoorEndpoints(MenuCommand command)
        {
            DoorEndpoint[] endpoints = GetSelectedEndpoints();
            if (endpoints.Length != 2)
            {
                EditorUtility.DisplayDialog(
                    "Connect Door Endpoints",
                    "Select exactly two door endpoint objects.",
                    "OK");
                return;
            }

            LevelDraft root =
                LevelGridEditorOperations.ResolveRoot(endpoints[0]);
            DoorLink created;
            string rejection;
            if (!LevelGridEditorOperations.TryCreateConnection(
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
            "GameObject/Shooter Mover/Level Level/Connect Selected Door Endpoints",
            true)]
        private static bool ValidateConnectSelectedDoorEndpoints()
        {
            return GetSelectedEndpoints().Length == 2;
        }

        private static DoorEndpoint[] GetSelectedEndpoints()
        {
            GameObject[] selected = Selection.gameObjects;
            var endpoints = new List<DoorEndpoint>();
            for (int index = 0; index < selected.Length; index++)
            {
                DoorEndpoint endpoint =
                    selected[index].GetComponent<DoorEndpoint>();
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

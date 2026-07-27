#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public sealed class LevelGridProblemsWindowV2 : EditorWindow
    {
        private static LevelDesignSceneAuthoringRoot2D currentRoot;
        private Vector2 scrollPosition;

        public static void Open(LevelDesignSceneAuthoringRoot2D root)
        {
            currentRoot = root;
            LevelGridProblemsWindowV2 window = GetWindow<LevelGridProblemsWindowV2>();
            window.titleContent = new GUIContent("Level Problems");
            window.minSize = new Vector2(420f, 240f);
            window.Show();
            window.Repaint();
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Open Grid Problems",
            priority = 230)]
        private static void OpenFromSelection()
        {
            LevelDesignSceneAuthoringRoot2D root = ResolveSelectedRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Grid Problems",
                    "Select an object below a LevelDesignSceneAuthoringRoot2D.",
                    "OK");
                return;
            }

            root.ValidateHierarchy();
            root.ValidateGridAuthoring(LevelGridValidationPurposeV2.Draft);
            Open(root);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += RefreshAfterAuthoringChange;
            EditorApplication.hierarchyChanged += RefreshAfterAuthoringChange;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RefreshAfterAuthoringChange;
            EditorApplication.hierarchyChanged -= RefreshAfterAuthoringChange;
        }

        private void OnGUI()
        {
            if (currentRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a level authoring root and open this panel again.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.ObjectField(
                "Level root",
                currentRoot,
                typeof(LevelDesignSceneAuthoringRoot2D),
                true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Draft"))
            {
                currentRoot.ValidateHierarchy();
                currentRoot.ValidateGridAuthoring(
                    LevelGridValidationPurposeV2.Draft);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Validate Production"))
            {
                currentRoot.ValidateHierarchy();
                currentRoot.ValidateGridAuthoring(
                    LevelGridValidationPurposeV2.ProductionPublish);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            LevelGridValidationResultV2 result = currentRoot.LastGridValidation;
            LevelDesignValidationResult foundation = currentRoot.LastValidation;
            bool combinedPublishAllowed = foundation.IsValid && result.CanPublish;
            MessageType summaryType = !foundation.IsValid || result.ErrorCount > 0
                ? MessageType.Error
                : result.WarningCount > 0
                    ? MessageType.Warning
                    : MessageType.Info;
            EditorGUILayout.HelpBox(
                "Mode: " + result.Purpose
                + "\nFoundation errors: " + foundation.ErrorCount
                + " | V2 errors: " + result.ErrorCount
                + " | V2 warnings: " + result.WarningCount
                + " | Unconnected traversable doors: "
                + result.UnconnectedTraversableDoorCount
                + "\nDraft saving remains allowed. Validated authoring publish is "
                + (combinedPublishAllowed ? "allowed." : "blocked."),
                summaryType);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (result.Problems.Count == 0)
            {
                EditorGUILayout.LabelField("No V2 grid problems.");
            }
            else
            {
                for (int index = 0; index < result.Problems.Count; index++)
                {
                    DrawProblem(result.Problems[index]);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawProblem(LevelGridProblemV2 problem)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                problem.Code.ToString(),
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (problem.Code == LevelGridProblemCodeV2.EdgeManagedDoorFacingMismatch)
            {
                if (GUILayout.Button("Reflow", GUILayout.Width(64f)))
                {
                    ReflowProblemDoor(problem);
                }
                if (GUILayout.Button("Keep", GUILayout.Width(52f)))
                {
                    KeepProblemDoor(problem);
                }
            }
            if (GUILayout.Button("Select", GUILayout.Width(64f)))
            {
                SelectProblemObject(problem);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                problem.AuthoredId,
                EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(problem.DiagnosticLocation))
            {
                EditorGUILayout.LabelField(
                    problem.DiagnosticLocation,
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.LabelField(
                problem.Message,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }

        private static void ReflowProblemDoor(LevelGridProblemV2 problem)
        {
            LevelDoorEndpointAuthoring2D door = FindProblemDoor(problem);
            if (door == null || currentRoot == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Reflow Edge-Managed Door");
            Undo.RecordObject(door, "Reflow Edge-Managed Door");
            Undo.RecordObject(door.transform, "Reflow Edge-Managed Door");
            door.SetAutoFaceConnectionForAuthoring(true);
            LevelGridDoorOperationsV2.ReflowDoor(currentRoot, door);
            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(door);
            EditorSceneManager.MarkSceneDirty(currentRoot.gameObject.scene);
            currentRoot.ValidateGridAuthoring(currentRoot.LastGridValidation.Purpose);
            SceneView.RepaintAll();
        }

        private static void KeepProblemDoor(LevelGridProblemV2 problem)
        {
            LevelDoorEndpointAuthoring2D door = FindProblemDoor(problem);
            if (door == null || currentRoot == null)
            {
                return;
            }

            Undo.RecordObject(door, "Keep Edge Door Placement");
            door.SetAutoFaceConnectionForAuthoring(false);
            EditorUtility.SetDirty(door);
            EditorSceneManager.MarkSceneDirty(currentRoot.gameObject.scene);
            currentRoot.ValidateGridAuthoring(currentRoot.LastGridValidation.Purpose);
            SceneView.RepaintAll();
        }

        private static void SelectProblemObject(LevelGridProblemV2 problem)
        {
            Component exact = FindExactProblemObject(problem);
            if (exact != null)
            {
                Selection.activeGameObject = exact.gameObject;
                EditorGUIUtility.PingObject(exact);
                return;
            }

            Component fallback = FindFirstProblemObjectById(problem);
            if (fallback != null)
            {
                Selection.activeGameObject = fallback.gameObject;
                EditorGUIUtility.PingObject(fallback);
                return;
            }

            Selection.activeObject = currentRoot;
            EditorGUIUtility.PingObject(currentRoot);
        }

        private static LevelDoorEndpointAuthoring2D FindProblemDoor(
            LevelGridProblemV2 problem)
        {
            Component exact = FindExactProblemObject(problem);
            LevelDoorEndpointAuthoring2D exactDoor =
                exact as LevelDoorEndpointAuthoring2D;
            if (exactDoor != null)
            {
                return exactDoor;
            }

            Component fallback = FindFirstProblemObjectById(problem);
            return fallback as LevelDoorEndpointAuthoring2D;
        }

        private static Component FindExactProblemObject(LevelGridProblemV2 problem)
        {
            if (currentRoot == null || string.IsNullOrEmpty(problem.DiagnosticLocation))
            {
                return null;
            }

            LevelRoomAuthoring2D[] rooms =
                currentRoot.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
            for (int index = 0; index < rooms.Length; index++)
            {
                if (string.Equals(rooms[index].RoomIdText, problem.AuthoredId, StringComparison.Ordinal)
                    && string.Equals(
                        BuildDiagnosticLocation(rooms[index].transform),
                        problem.DiagnosticLocation,
                        StringComparison.Ordinal))
                {
                    return rooms[index];
                }
            }

            LevelDoorEndpointAuthoring2D[] doors =
                currentRoot.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            for (int index = 0; index < doors.Length; index++)
            {
                if (string.Equals(doors[index].DoorIdText, problem.AuthoredId, StringComparison.Ordinal)
                    && string.Equals(
                        BuildDiagnosticLocation(doors[index].transform),
                        problem.DiagnosticLocation,
                        StringComparison.Ordinal))
                {
                    return doors[index];
                }
            }

            LevelDoorLinkAuthoring2D[] links =
                currentRoot.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            for (int index = 0; index < links.Length; index++)
            {
                if (string.Equals(links[index].ConnectionIdText, problem.AuthoredId, StringComparison.Ordinal)
                    && string.Equals(
                        BuildDiagnosticLocation(links[index].transform),
                        problem.DiagnosticLocation,
                        StringComparison.Ordinal))
                {
                    return links[index];
                }
            }

            return null;
        }

        private static Component FindFirstProblemObjectById(LevelGridProblemV2 problem)
        {
            if (currentRoot == null)
            {
                return null;
            }

            LevelRoomAuthoring2D[] rooms =
                currentRoot.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
            for (int index = 0; index < rooms.Length; index++)
            {
                if (string.Equals(
                    rooms[index].RoomIdText,
                    problem.AuthoredId,
                    StringComparison.Ordinal))
                {
                    return rooms[index];
                }
            }

            LevelDoorEndpointAuthoring2D[] doors =
                currentRoot.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            for (int index = 0; index < doors.Length; index++)
            {
                if (string.Equals(
                    doors[index].DoorIdText,
                    problem.AuthoredId,
                    StringComparison.Ordinal))
                {
                    return doors[index];
                }
            }

            LevelDoorLinkAuthoring2D[] links =
                currentRoot.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            for (int index = 0; index < links.Length; index++)
            {
                if (string.Equals(
                    links[index].ConnectionIdText,
                    problem.AuthoredId,
                    StringComparison.Ordinal))
                {
                    return links[index];
                }
            }

            return null;
        }

        private void RefreshAfterAuthoringChange()
        {
            if (currentRoot == null)
            {
                return;
            }

            LevelGridDoorOperationsV2.ReflowAll(currentRoot);
            currentRoot.ValidateHierarchy();
            currentRoot.ValidateGridAuthoring(LevelGridValidationPurposeV2.Draft);
            Repaint();
            SceneView.RepaintAll();
        }

        private static string BuildDiagnosticLocation(Transform transform)
        {
            return transform.gameObject.scene.name + ":" + GetHierarchyPath(transform);
        }

        private static string GetHierarchyPath(Transform current)
        {
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }

        private static LevelDesignSceneAuthoringRoot2D ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
        }
    }

    public static class LevelGridAuthoringV2Menu
    {
        private const int DestructiveLinkThreshold = 8;
        private const int DestructiveObjectThreshold = 100;

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Validate Grid Draft",
            priority = 231)]
        private static void ValidateDraft()
        {
            ValidateSelected(LevelGridValidationPurposeV2.Draft);
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Validate Grid Production Publish",
            priority = 232)]
        private static void ValidateProduction()
        {
            ValidateSelected(LevelGridValidationPurposeV2.ProductionPublish);
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Delete Selected Room (Undoable)",
            priority = 240)]
        private static void DeleteSelectedRoom()
        {
            GameObject selected = Selection.activeGameObject;
            LevelRoomAuthoring2D room = selected == null
                ? null
                : selected.GetComponentInParent<LevelRoomAuthoring2D>();
            if (room == null)
            {
                EditorUtility.DisplayDialog(
                    "Delete Room",
                    "Select a room or one of its children.",
                    "OK");
                return;
            }

            LevelDesignSceneAuthoringRoot2D root =
                room.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            if (root == null)
            {
                return;
            }

            LevelDoorLinkAuthoring2D[] allLinks =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            List<LevelDoorLinkAuthoring2D> attachedLinks =
                new List<LevelDoorLinkAuthoring2D>();
            for (int index = 0; index < allLinks.Length; index++)
            {
                LevelDoorLinkAuthoring2D link = allLinks[index];
                if (link.SourceRoom == room || link.DestinationRoom == room)
                {
                    attachedLinks.Add(link);
                }
            }

            int objectCount = room.GetComponentsInChildren<Transform>(true).Length;
            bool unusuallyDestructive =
                attachedLinks.Count > DestructiveLinkThreshold
                || objectCount > DestructiveObjectThreshold;
            if (unusuallyDestructive
                && !EditorUtility.DisplayDialog(
                    "Delete unusually large room?",
                    "This undoable deletion removes " + objectCount
                        + " room objects and " + attachedLinks.Count
                        + " connections. Continue?",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Level Room");
            for (int index = 0; index < attachedLinks.Count; index++)
            {
                DestroyConnectionWithUndo(attachedLinks[index]);
            }
            Undo.DestroyObjectImmediate(room.gameObject);
            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeObject = root;
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            root.ValidateGridAuthoring(LevelGridValidationPurposeV2.Draft);
            LevelGridProblemsWindowV2.Open(root);
            SceneView.RepaintAll();

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.ShowNotification(new GUIContent(
                    "Room deleted; " + attachedLinks.Count
                    + " connection(s) removed. Ctrl+Z to undo."));
            }
        }

        private static void ValidateSelected(LevelGridValidationPurposeV2 purpose)
        {
            GameObject selected = Selection.activeGameObject;
            LevelDesignSceneAuthoringRoot2D root = selected == null
                ? null
                : selected.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Grid Validation",
                    "Select an object below a LevelDesignSceneAuthoringRoot2D.",
                    "OK");
                return;
            }

            LevelGridDoorOperationsV2.ReflowAll(root);
            LevelDesignValidationResult foundation = root.ValidateHierarchy();
            LevelGridValidationResultV2 result = root.ValidateGridAuthoring(purpose);
            LevelDesignSceneAuthoringRoot2DEditor.LogResult(root, foundation);
            LevelDesignSceneAuthoringRoot2DEditor.LogGridResult(root, result);
            LevelGridProblemsWindowV2.Open(root);
            SceneView.RepaintAll();
        }

        private static void DestroyConnectionWithUndo(LevelDoorLinkAuthoring2D link)
        {
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

    public static class LevelGridAuthoringV2Gizmos
    {
        [DrawGizmo(
            GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawDoorEndpoint(
            LevelDoorEndpointAuthoring2D door,
            GizmoType gizmoType)
        {
            LevelDesignSceneAuthoringRoot2D root =
                door.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            bool connected = IsConnected(root, door);
            Color previous = Handles.color;
            if (door.Traversable && !connected)
            {
                bool productionError = HasProductionProblem(root, door.DoorIdText);
                Handles.color = productionError
                    ? Color.red
                    : new Color(1f, 0.45f, 0f, 1f);
            }

            float radius = HandleUtility.GetHandleSize(door.transform.position) * 0.09f;
            Handles.DrawWireDisc(
                door.transform.position,
                Vector3.forward,
                radius);
            Handles.Label(
                door.transform.position + Vector3.up * radius,
                door.DoorIdText);
            Handles.color = previous;
        }

        [DrawGizmo(
            GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawConnection(
            LevelDoorLinkAuthoring2D connection,
            GizmoType gizmoType)
        {
            if (connection.SourceDoor == null || connection.DestinationDoor == null)
            {
                return;
            }

            Handles.DrawLine(
                connection.SourceDoor.transform.position,
                connection.DestinationDoor.transform.position);
            Handles.Label(
                Vector3.Lerp(
                    connection.SourceDoor.transform.position,
                    connection.DestinationDoor.transform.position,
                    0.5f),
                connection.ConnectionIdText);
        }

        private static bool IsConnected(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDoorEndpointAuthoring2D door)
        {
            if (root == null)
            {
                return false;
            }

            LevelDoorLinkAuthoring2D[] links =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            for (int index = 0; index < links.Length; index++)
            {
                if (links[index].SourceDoor == door
                    || links[index].DestinationDoor == door)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasProductionProblem(
            LevelDesignSceneAuthoringRoot2D root,
            string doorId)
        {
            if (root == null
                || root.LastGridValidation.Purpose
                    != LevelGridValidationPurposeV2.ProductionPublish)
            {
                return false;
            }

            IReadOnlyList<LevelGridProblemV2> problems =
                root.LastGridValidation.Problems;
            for (int index = 0; index < problems.Count; index++)
            {
                if (problems[index].Code
                        == LevelGridProblemCodeV2.UnconnectedTraversableDoor
                    && string.Equals(
                        problems[index].AuthoredId,
                        doorId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif

#if UNITY_EDITOR
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    [CustomEditor(typeof(LevelDesignSceneAuthoringRoot2D))]
    public sealed class LevelDesignSceneAuthoringRoot2DEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LevelDesignSceneAuthoringRoot2D root =
                (LevelDesignSceneAuthoringRoot2D)target;
            EditorGUILayout.Space();
            if (GUILayout.Button("Validate Level Design Foundation"))
            {
                LevelDesignValidationResult result = root.ValidateHierarchy();
                EditorUtility.SetDirty(root);
                SceneView.RepaintAll();
                LogResult(root, result);
            }

            LevelDesignValidationResult last = root.LastValidation;
            MessageType type = last.IsValid
                ? last.WarningCount == 0
                    ? MessageType.Info
                    : MessageType.Warning
                : MessageType.Error;
            EditorGUILayout.HelpBox(
                "Errors: " + last.ErrorCount
                + " | Warnings: " + last.WarningCount,
                type);

            int maxIssues = Mathf.Min(12, last.Issues.Count);
            for (int index = 0; index < maxIssues; index++)
            {
                LevelDesignValidationIssue issue = last.Issues[index];
                EditorGUILayout.HelpBox(
                    issue.ToString(),
                    issue.Severity == LevelDesignValidationSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }

            if (last.Issues.Count > maxIssues)
            {
                EditorGUILayout.LabelField(
                    "... and " + (last.Issues.Count - maxIssues)
                    + " more issue(s).");
            }
        }

        internal static void LogResult(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDesignValidationResult result)
        {
            if (result.IsValid)
            {
                Debug.Log(
                    "LEVELDES-001 validation passed with "
                    + result.WarningCount + " warning(s).",
                    root);
                return;
            }

            for (int index = 0; index < result.Issues.Count; index++)
            {
                LevelDesignValidationIssue issue = result.Issues[index];
                if (issue.Severity == LevelDesignValidationSeverity.Error)
                {
                    Debug.LogError(issue.ToString(), root);
                }
                else
                {
                    Debug.LogWarning(issue.ToString(), root);
                }
            }
        }
    }

    public static class LevelDesignFoundationMenu
    {
        [MenuItem(
            "Tools/Shooter Mover/Level Design/Validate Selected Foundation",
            priority = 200)]
        private static void ValidateSelected()
        {
            GameObject selected = Selection.activeGameObject;
            LevelDesignSceneAuthoringRoot2D root = selected == null
                ? null
                : selected.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Design Validation",
                    "Select an object below a LevelDesignSceneAuthoringRoot2D.",
                    "OK");
                return;
            }

            LevelDesignValidationResult result = root.ValidateHierarchy();
            LevelDesignSceneAuthoringRoot2DEditor.LogResult(root, result);
            Selection.activeObject = root;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Validate Open Foundations",
            priority = 201)]
        private static void ValidateOpenFoundations()
        {
            LevelDesignSceneAuthoringRoot2D[] roots =
                UnityEngine.Object.FindObjectsByType<LevelDesignSceneAuthoringRoot2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (roots.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Level Design Validation",
                    "No LevelDesignSceneAuthoringRoot2D exists in the open scenes.",
                    "OK");
                return;
            }

            int errors = 0;
            int warnings = 0;
            for (int index = 0; index < roots.Length; index++)
            {
                LevelDesignValidationResult result = roots[index].ValidateHierarchy();
                errors += result.ErrorCount;
                warnings += result.WarningCount;
                LevelDesignSceneAuthoringRoot2DEditor.LogResult(
                    roots[index],
                    result);
            }

            EditorUtility.DisplayDialog(
                "Level Design Validation",
                "Foundations: " + roots.Length
                + "\nErrors: " + errors
                + "\nWarnings: " + warnings,
                "OK");
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Snap Selected To Authored Grid",
            priority = 220)]
        private static void SnapSelected()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            LevelRoomAuthoring2D room =
                selected.GetComponent<LevelRoomAuthoring2D>();
            if (room != null)
            {
                Undo.RecordObject(room.transform, "Snap Room To Authored Grid");
                room.SnapToAuthoredGrid();
                EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            }

            LevelPlacementAuthoring2D placement =
                selected.GetComponent<LevelPlacementAuthoring2D>();
            if (placement != null)
            {
                Undo.RecordObject(
                    placement.transform,
                    "Snap Placement To Room Grid");
                placement.SnapToGrid();
                EditorSceneManager.MarkSceneDirty(placement.gameObject.scene);
            }
        }
    }

    public static class LevelDesignFoundationGizmos
    {
        [DrawGizmo(
            GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawRoom(
            LevelRoomAuthoring2D room,
            GizmoType gizmoType)
        {
            Collider2D bounds = room.RoomBounds;
            if (bounds == null)
            {
                return;
            }

            Gizmos.DrawWireCube(bounds.bounds.center, bounds.bounds.size);
            Handles.Label(
                bounds.bounds.center,
                room.RoomIdText + "  grid " + room.GridCoordinate);
        }

        [DrawGizmo(
            GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawPlacement(
            LevelPlacementAuthoring2D placement,
            GizmoType gizmoType)
        {
            float radius = HandleUtility.GetHandleSize(
                placement.transform.position) * 0.08f;
            Gizmos.DrawWireSphere(placement.transform.position, radius);
            Handles.Label(
                placement.transform.position,
                placement.PlacementKind + "\n" + placement.AuthoredIdText);
        }

        [DrawGizmo(
            GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawDoor(
            LevelDoorConnectionAuthoring2D door,
            GizmoType gizmoType)
        {
            LevelRoomAuthoring2D source = door.SourceRoom;
            LevelRoomAuthoring2D destination = door.DestinationRoom;
            if (source != null && destination != null)
            {
                Gizmos.DrawLine(
                    source.transform.position,
                    destination.transform.position);
            }

            Handles.Label(door.transform.position, door.DoorIdText);
        }

        [DrawGizmo(
            GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawVoid(
            LevelVoidRegionAuthoring2D region,
            GizmoType gizmoType)
        {
            if (region.RegionCollider == null)
            {
                return;
            }

            Bounds bounds = region.RegionCollider.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Handles.Label(bounds.center, "VOID\n" + region.VoidRegionIdText);
        }
    }

}
#endif

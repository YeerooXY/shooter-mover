#if UNITY_EDITOR
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    [CustomEditor(typeof(LevelDraft))]
    public sealed class LevelDraftEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LevelDraft root =
                (LevelDraft)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Canonical Level Grid workflow",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use the exact-root Level Grid editor for topology mutation, validation, playable "
                    + "export, transactional build status and production Level Selection.",
                MessageType.Info);

            if (GUILayout.Button("Open Level Grid Editor"))
            {
                LevelGridEditorWindow.OpenForRoot(root);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Draft"))
            {
                Validate(root, LevelGridValidationPurpose.Draft);
            }
            if (GUILayout.Button("Validate Production"))
            {
                Validate(root, LevelGridValidationPurpose.ProductionPublish);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Open Problems In Level Grid Editor"))
            {
                LevelGridProblemsWindow.Open(root);
            }

            LevelDesignValidationResult foundation = root.LastValidation;
            LevelGridValidationResult grid = root.LastGridValidation;
            bool productionValidationRun = grid.Purpose
                == LevelGridValidationPurpose.ProductionPublish;
            bool combinedPublishAllowed = productionValidationRun
                && foundation.IsValid
                && grid.CanPublish;
            MessageType type = !foundation.IsValid || grid.ErrorCount > 0
                ? MessageType.Error
                : foundation.WarningCount > 0 || grid.WarningCount > 0
                    ? MessageType.Warning
                    : MessageType.Info;
            string productionStatus = !productionValidationRun
                ? "not run"
                : combinedPublishAllowed ? "allowed" : "blocked";
            EditorGUILayout.HelpBox(
                "Mode: " + grid.Purpose
                    + " | Foundation errors: " + foundation.ErrorCount
                    + " | Foundation warnings: " + foundation.WarningCount
                    + " | V2 errors: " + grid.ErrorCount
                    + " | V2 warnings: " + grid.WarningCount
                    + "\nProduction validation: "
                    + productionStatus
                    + "\nRuntime pipeline: compiler-ready source → transactional publication "
                    + "→ exact catalogue registration → production Level Selection.",
                type);
        }

        internal static void LogResult(
            LevelDraft root,
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

        internal static void LogGridResult(
            LevelDraft root,
            LevelGridValidationResult result)
        {
            if (result.Problems.Count == 0)
            {
                Debug.Log(
                    "Level Grid Authoring V2 " + result.Purpose
                    + " validation passed.",
                    root);
                return;
            }

            for (int index = 0; index < result.Problems.Count; index++)
            {
                LevelGridProblem problem = result.Problems[index];
                if (problem.Severity == LevelDesignValidationSeverity.Error)
                {
                    Debug.LogError(problem.ToString(), root);
                }
                else
                {
                    Debug.LogWarning(problem.ToString(), root);
                }
            }
        }

        private static void Validate(
            LevelDraft root,
            LevelGridValidationPurpose purpose)
        {
            LevelGridEditorOperations.Validate(root, purpose);
            LogResult(root, root.LastValidation);
            LogGridResult(root, root.LastGridValidation);
            LevelGridEditorWindow.OpenForRoot(root);
        }
    }

    public static class LevelDesignFoundationMenu
    {
        [MenuItem(
            "Tools/Shooter Mover/Level Design/Validate Selected Foundation",
            priority = 200)]
        private static void ValidateSelected()
        {
            LevelDraft root = ResolveSelectedRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Design Validation",
                    "Select an object below a LevelDraft.",
                    "OK");
                return;
            }

            LevelGridEditorOperations.Validate(
                root,
                LevelGridValidationPurpose.Draft);
            LevelDraftEditor.LogResult(root, root.LastValidation);
            LevelDraftEditor.LogGridResult(
                root,
                root.LastGridValidation);
            LevelGridEditorWindow.OpenForRoot(root);
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Validate Open Foundations",
            priority = 201)]
        private static void ValidateOpenFoundations()
        {
            LevelDraft[] roots =
                UnityEngine.Object.FindObjectsByType<LevelDraft>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (roots.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Level Design Validation",
                    "No LevelDraft exists in the open scenes.",
                    "OK");
                return;
            }

            int errors = 0;
            int warnings = 0;
            for (int index = 0; index < roots.Length; index++)
            {
                LevelGridEditorOperations.Validate(
                    roots[index],
                    LevelGridValidationPurpose.Draft);
                errors += roots[index].LastValidation.ErrorCount;
                errors += roots[index].LastGridValidation.ErrorCount;
                warnings += roots[index].LastValidation.WarningCount;
                warnings += roots[index].LastGridValidation.WarningCount;
                LevelDraftEditor.LogResult(
                    roots[index],
                    roots[index].LastValidation);
                LevelDraftEditor.LogGridResult(
                    roots[index],
                    roots[index].LastGridValidation);
            }

            EditorUtility.DisplayDialog(
                "Level Design Validation",
                "Foundations: " + roots.Length
                    + "\nCombined errors: " + errors
                    + "\nCombined warnings: " + warnings,
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

            if (TrySnapSelectedPlacement(selected))
            {
                return;
            }

            DoorEndpoint door =
                selected.GetComponent<DoorEndpoint>();
            LevelRoom room =
                selected.GetComponentInParent<LevelRoom>();
            Component gridObject = door != null ? (Component)door : room;
            if (gridObject != null)
            {
                LevelDraft root =
                    LevelGridEditorOperations.ResolveRoot(gridObject);
                if (root != null)
                {
                    LevelGridEditorWindow.OpenForRoot(root);
                    EditorUtility.DisplayDialog(
                        "Use Canonical Level Grid Commands",
                        "Room and Level door placement is owned by the Level Grid editor. "
                            + "Use Move, Reflow or Keep there so validation, Undo and freshness "
                            + "remain consistent.",
                        "OK");
                }
                return;
            }
        }

        internal static bool TrySnapSelectedPlacement(GameObject selected)
        {
            if (selected == null)
            {
                return false;
            }

            LevelObject placement =
                selected.GetComponent<LevelObject>();
            if (placement == null)
            {
                return false;
            }

            Undo.RecordObject(
                placement.transform,
                "Snap Placement To Room Grid");
            placement.SnapToGrid();
            EditorSceneManager.MarkSceneDirty(placement.gameObject.scene);
            return true;
        }

        private static LevelDraft ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDraft>();
        }
    }

    public static class LevelDesignFoundationGizmos
    {
        [DrawGizmo(
            GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawRoom(
            LevelRoom room,
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
                room.EditorLabel + "\n" + room.RoomIdText
                    + "  grid " + room.GridCoordinate
                    + " slot " + room.FolderSlot.ToString("00"));
        }

        [DrawGizmo(
            GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawPlacement(
            LevelObject placement,
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
            DoorConnection door,
            GizmoType gizmoType)
        {
            LevelRoom source = door.SourceRoom;
            LevelRoom destination = door.DestinationRoom;
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
            VoidArea region,
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

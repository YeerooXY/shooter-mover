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
            EditorGUILayout.LabelField(
                "Canonical Level Grid workflow",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use the exact-root Level Grid editor for topology mutation, validation, playable "
                    + "export, transactional build status and production Level Selection.",
                MessageType.Info);

            if (GUILayout.Button("Open Level Grid Editor"))
            {
                LevelGridEditorWindowV2.OpenForRoot(root);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Draft"))
            {
                Validate(root, LevelGridValidationPurposeV2.Draft);
            }
            if (GUILayout.Button("Validate Production"))
            {
                Validate(root, LevelGridValidationPurposeV2.ProductionPublish);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Open Problems In Level Grid Editor"))
            {
                LevelGridProblemsWindowV2.Open(root);
            }

            LevelDesignValidationResult foundation = root.LastValidation;
            LevelGridValidationResultV2 grid = root.LastGridValidation;
            bool productionValidationRun = grid.Purpose
                == LevelGridValidationPurposeV2.ProductionPublish;
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

        internal static void LogGridResult(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridValidationResultV2 result)
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
                LevelGridProblemV2 problem = result.Problems[index];
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
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridValidationPurposeV2 purpose)
        {
            LevelGridEditorOperationsV2.Validate(root, purpose);
            LogResult(root, root.LastValidation);
            LogGridResult(root, root.LastGridValidation);
            LevelGridEditorWindowV2.OpenForRoot(root);
        }
    }

    public static class LevelDesignFoundationMenu
    {
        [MenuItem(
            "Tools/Shooter Mover/Level Design/Validate Selected Foundation",
            priority = 200)]
        private static void ValidateSelected()
        {
            LevelDesignSceneAuthoringRoot2D root = ResolveSelectedRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Design Validation",
                    "Select an object below a LevelDesignSceneAuthoringRoot2D.",
                    "OK");
                return;
            }

            LevelGridEditorOperationsV2.Validate(
                root,
                LevelGridValidationPurposeV2.Draft);
            LevelDesignSceneAuthoringRoot2DEditor.LogResult(root, root.LastValidation);
            LevelDesignSceneAuthoringRoot2DEditor.LogGridResult(
                root,
                root.LastGridValidation);
            LevelGridEditorWindowV2.OpenForRoot(root);
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
                LevelGridEditorOperationsV2.Validate(
                    roots[index],
                    LevelGridValidationPurposeV2.Draft);
                errors += roots[index].LastValidation.ErrorCount;
                errors += roots[index].LastGridValidation.ErrorCount;
                warnings += roots[index].LastValidation.WarningCount;
                warnings += roots[index].LastGridValidation.WarningCount;
                LevelDesignSceneAuthoringRoot2DEditor.LogResult(
                    roots[index],
                    roots[index].LastValidation);
                LevelDesignSceneAuthoringRoot2DEditor.LogGridResult(
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

            LevelDoorEndpointAuthoring2D door =
                selected.GetComponent<LevelDoorEndpointAuthoring2D>();
            LevelRoomAuthoring2D room =
                selected.GetComponentInParent<LevelRoomAuthoring2D>();
            Component gridObject = door != null ? (Component)door : room;
            if (gridObject != null)
            {
                LevelDesignSceneAuthoringRoot2D root =
                    LevelGridEditorOperationsV2.ResolveRoot(gridObject);
                if (root != null)
                {
                    LevelGridEditorWindowV2.OpenForRoot(root);
                    EditorUtility.DisplayDialog(
                        "Use Canonical Level Grid Commands",
                        "Room and Grid V2 door placement is owned by the Level Grid editor. "
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

            LevelPlacementAuthoring2D placement =
                selected.GetComponent<LevelPlacementAuthoring2D>();
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

        private static LevelDesignSceneAuthoringRoot2D ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
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
                room.EditorLabel + "\n" + room.RoomIdText
                    + "  grid " + room.GridCoordinate
                    + " slot " + room.FolderSlot.ToString("00"));
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

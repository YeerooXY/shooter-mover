#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Retained compatibility type for callers that previously opened a separate Problems window.
    /// Problems are now presented by the canonical Level Grid editor for the exact selected root.
    /// </summary>
    public sealed class LevelGridProblemsWindowV2 : EditorWindow
    {
        private static LevelDesignSceneAuthoringRoot2D currentRoot;

        public static void Open(LevelDesignSceneAuthoringRoot2D root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            currentRoot = root;
            LevelGridEditorWindowV2.OpenForRoot(root);
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

            Open(root);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "The separate Problems window is retired. Validation, object-specific problems, "
                    + "playable status and Build now live in the canonical Level Grid editor.",
                MessageType.Info);
            EditorGUI.BeginDisabledGroup(currentRoot == null);
            if (GUILayout.Button("Open Canonical Level Grid Editor"))
            {
                LevelGridEditorWindowV2.OpenForRoot(currentRoot);
                Close();
            }
            EditorGUI.EndDisabledGroup();
        }

        private static LevelDesignSceneAuthoringRoot2D ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
        }
    }

    /// <summary>
    /// Compatibility menu callbacks. They resolve context and delegate every Grid V2 operation to
    /// LevelGridEditorOperationsV2; they do not own topology mutation or validation.
    /// </summary>
    public static class LevelGridAuthoringV2Menu
    {
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
                LevelGridEditorOperationsV2.ResolveRoot(room);
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Delete Room",
                    "The selected room does not belong to a level authoring root.",
                    "OK");
                return;
            }

            if (LevelGridEditorOperationsV2.DeleteRoom(room, true))
            {
                LevelGridEditorWindowV2.OpenForRoot(root);
            }
        }

        private static void ValidateSelected(LevelGridValidationPurposeV2 purpose)
        {
            LevelDesignSceneAuthoringRoot2D root = ResolveSelectedRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Grid Validation",
                    "Select an object below a LevelDesignSceneAuthoringRoot2D.",
                    "OK");
                return;
            }

            LevelGridEditorOperationsV2.Validate(root, purpose);
            LevelDesignSceneAuthoringRoot2DEditor.LogResult(root, root.LastValidation);
            LevelDesignSceneAuthoringRoot2DEditor.LogGridResult(
                root,
                root.LastGridValidation);
            LevelGridEditorWindowV2.OpenForRoot(root);
        }

        private static LevelDesignSceneAuthoringRoot2D ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
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
                LevelGridEditorOperationsV2.ResolveRoot(door);
            bool connected = LevelGridEditorOperationsV2.IsConnected(root, door);
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

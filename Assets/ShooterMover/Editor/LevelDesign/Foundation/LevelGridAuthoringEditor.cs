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
    public sealed class LevelGridProblemsWindow : EditorWindow
    {
        private static LevelDraft currentRoot;

        public static void Open(LevelDraft root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            currentRoot = root;
            LevelGridEditorWindow.OpenForRoot(root);
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Open Grid Problems",
            priority = 230)]
        private static void OpenFromSelection()
        {
            LevelDraft root = ResolveSelectedRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Grid Problems",
                    "Select an object below a LevelDraft.",
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
                LevelGridEditorWindow.OpenForRoot(currentRoot);
                Close();
            }
            EditorGUI.EndDisabledGroup();
        }

        private static LevelDraft ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDraft>();
        }
    }

    /// <summary>
    /// Compatibility menu callbacks. They resolve context and delegate every Level operation to
    /// LevelGridEditorOperations; they do not own topology mutation or validation.
    /// </summary>
    public static class LevelGridAuthoringMenu
    {
        [MenuItem(
            "Tools/Shooter Mover/Level Design/Validate Grid Draft",
            priority = 231)]
        private static void ValidateDraft()
        {
            ValidateSelected(LevelGridValidationPurpose.Draft);
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Validate Grid Production Publish",
            priority = 232)]
        private static void ValidateProduction()
        {
            ValidateSelected(LevelGridValidationPurpose.ProductionPublish);
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Delete Selected Room (Undoable)",
            priority = 240)]
        private static void DeleteSelectedRoom()
        {
            GameObject selected = Selection.activeGameObject;
            LevelRoom room = selected == null
                ? null
                : selected.GetComponentInParent<LevelRoom>();
            if (room == null)
            {
                EditorUtility.DisplayDialog(
                    "Delete Room",
                    "Select a room or one of its children.",
                    "OK");
                return;
            }

            LevelDraft root =
                LevelGridEditorOperations.ResolveRoot(room);
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Delete Room",
                    "The selected room does not belong to a level authoring root.",
                    "OK");
                return;
            }

            if (LevelGridEditorOperations.DeleteRoom(room, true))
            {
                LevelGridEditorWindow.OpenForRoot(root);
            }
        }

        private static void ValidateSelected(LevelGridValidationPurpose purpose)
        {
            LevelDraft root = ResolveSelectedRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Grid Validation",
                    "Select an object below a LevelDraft.",
                    "OK");
                return;
            }

            LevelGridEditorOperations.Validate(root, purpose);
            LevelDraftEditor.LogResult(root, root.LastValidation);
            LevelDraftEditor.LogGridResult(
                root,
                root.LastGridValidation);
            LevelGridEditorWindow.OpenForRoot(root);
        }

        private static LevelDraft ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDraft>();
        }
    }

    public static class LevelGridAuthoringGizmos
    {
        [DrawGizmo(
            GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawDoorEndpoint(
            DoorEndpoint door,
            GizmoType gizmoType)
        {
            LevelDraft root =
                LevelGridEditorOperations.ResolveRoot(door);
            bool connected = LevelGridEditorOperations.IsConnected(root, door);
            bool exactFinalExit = IsExactFinalExit(root, door);
            Color previous = Handles.color;
            if (door.Traversable && !connected && !exactFinalExit)
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
            DoorLink connection,
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

        private static bool IsExactFinalExit(
            LevelDraft root,
            DoorEndpoint door)
        {
            if (root == null || door == null)
            {
                return false;
            }

            LevelGridPlayableMetadata metadata =
                root.GetComponent<LevelGridPlayableMetadata>();
            return metadata != null
                && metadata.FinalExitRoom == door.OwningRoom
                && metadata.FinalExitDoor == door;
        }

        private static bool HasProductionProblem(
            LevelDraft root,
            string doorId)
        {
            if (root == null
                || root.LastGridValidation.Purpose
                    != LevelGridValidationPurpose.ProductionPublish)
            {
                return false;
            }

            IReadOnlyList<LevelGridProblem> problems =
                root.LastGridValidation.Problems;
            for (int index = 0; index < problems.Count; index++)
            {
                if (problems[index].Code
                        == LevelGridProblemCode.UnconnectedTraversableDoor
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

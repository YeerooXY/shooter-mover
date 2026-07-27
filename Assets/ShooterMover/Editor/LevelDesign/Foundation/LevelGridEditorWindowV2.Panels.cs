#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public sealed partial class LevelGridEditorWindowV2 : EditorWindow
    {

        private void DrawProblemsPanel(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            EditorGUILayout.LabelField("Problems", EditorStyles.boldLabel);
            if (activeRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a level root to show validation status.",
                    MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            LevelDesignValidationResult foundation = activeRoot.LastValidation;
            LevelGridValidationResultV2 grid = activeRoot.LastGridValidation;
            bool publishAllowed = foundation.IsValid && grid.CanPublish;
            MessageType summaryType = !foundation.IsValid || grid.ErrorCount > 0
                ? MessageType.Error
                : grid.WarningCount > 0
                    ? MessageType.Warning
                    : MessageType.Info;
            EditorGUILayout.HelpBox(
                "Foundation errors: " + foundation.ErrorCount
                    + " | V2 errors: " + grid.ErrorCount
                    + " | V2 warnings: " + grid.WarningCount
                    + " | Unconnected traversable: "
                    + grid.UnconnectedTraversableDoorCount
                    + "\nDraft status: save allowed"
                    + " | Validated-authoring publish: "
                    + (publishAllowed ? "allowed" : "blocked")
                    + " | Runtime import: not connected",
                summaryType);

            problemsScroll = EditorGUILayout.BeginScrollView(problemsScroll);
            for (int index = 0; index < foundation.Issues.Count; index++)
            {
                LevelDesignValidationIssue issue = foundation.Issues[index];
                EditorGUILayout.HelpBox(
                    issue.ToString(),
                    issue.Severity == LevelDesignValidationSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }

            for (int index = 0; index < grid.Problems.Count; index++)
            {
                DrawGridProblem(grid.Problems[index]);
            }

            if (foundation.Issues.Count == 0 && grid.Problems.Count == 0)
            {
                EditorGUILayout.LabelField("No validation problems.");
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawGridProblem(LevelGridProblemV2 problem)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                problem.Code.ToString(),
                EditorStyles.boldLabel,
                GUILayout.MinWidth(130f));
            GUILayout.FlexibleSpace();
            if (problem.Code == LevelGridProblemCodeV2.EdgeManagedDoorFacingMismatch)
            {
                if (GUILayout.Button("Reflow", GUILayout.Width(58f)))
                {
                    LevelDoorEndpointAuthoring2D door =
                        LevelGridEditorProblemLocatorV2.FindExact(activeRoot, problem)
                            as LevelDoorEndpointAuthoring2D;
                    LevelGridEditorOperationsV2.ReflowDoor(door);
                    RequestRefresh(true);
                }
                if (GUILayout.Button("Keep Placement", GUILayout.Width(96f)))
                {
                    LevelDoorEndpointAuthoring2D door =
                        LevelGridEditorProblemLocatorV2.FindExact(activeRoot, problem)
                            as LevelDoorEndpointAuthoring2D;
                    LevelGridEditorOperationsV2.KeepDoorPlacement(door);
                    RequestRefresh(true);
                }
            }
            if (GUILayout.Button("Select", GUILayout.Width(54f)))
            {
                SelectProblem(problem, false);
            }
            if (GUILayout.Button("Frame", GUILayout.Width(52f)))
            {
                SelectProblem(problem, true);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(problem.AuthoredId, EditorStyles.miniLabel);
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

        private void DrawSelectionInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            EditorGUILayout.LabelField("Selection Inspector", EditorStyles.boldLabel);
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

            if (selectedProblem != null)
            {
                DrawProblemInspector(selectedProblem);
            }
            else if (selectedAuthoringObject is LevelDoorEndpointAuthoring2D)
            {
                DrawDoorInspector((LevelDoorEndpointAuthoring2D)selectedAuthoringObject);
            }
            else if (selectedAuthoringObject is LevelDoorLinkAuthoring2D)
            {
                DrawConnectionInspector((LevelDoorLinkAuthoring2D)selectedAuthoringObject);
            }
            else if (selectedAuthoringObject is LevelRoomAuthoring2D)
            {
                DrawRoomInspector((LevelRoomAuthoring2D)selectedAuthoringObject);
            }
            else if (selectedAuthoringObject is LevelDesignSceneAuthoringRoot2D)
            {
                DrawRootInspector((LevelDesignSceneAuthoringRoot2D)selectedAuthoringObject);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Select a room, door, connection, root, or validation problem.",
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRootInspector(LevelDesignSceneAuthoringRoot2D root)
        {
            EditorGUILayout.LabelField("Level root", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Stable level ID", root.LevelIdText);
            EditorGUILayout.ObjectField(
                "Scene object",
                root,
                typeof(LevelDesignSceneAuthoringRoot2D),
                true);
            DrawRevealButtons(root);
        }

        private void DrawRoomInspector(LevelRoomAuthoring2D room)
        {
            EditorGUILayout.LabelField("Room", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Stable room ID", room.RoomIdText);

            string displayName = EditorGUILayout.TextField("Display label", room.DisplayName);
            if (!string.Equals(displayName, room.DisplayName, StringComparison.Ordinal))
            {
                LevelGridEditorOperationsV2.SetRoomDisplayName(room, displayName);
                RequestRefresh(true);
            }

            Vector2Int coordinate =
                EditorGUILayout.Vector2IntField("Grid coordinate", room.GridCoordinate);
            if (coordinate != room.GridCoordinate)
            {
                LevelGridEditorOperationsV2.MoveRoom(room, coordinate);
                RequestRefresh(true);
            }

            int folderSlot = EditorGUILayout.IntField("Folder slot", room.FolderSlot);
            if (folderSlot != room.FolderSlot)
            {
                LevelGridEditorOperationsV2.SetFolderSlot(room, folderSlot);
                RequestRefresh(true);
            }

            Vector2Int footprint =
                EditorGUILayout.Vector2IntField("Footprint", room.FootprintCells);
            if (footprint != room.FootprintCells)
            {
                LevelGridEditorOperationsV2.ResizeRoom(room, footprint);
                RequestRefresh(true);
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Vector2Field("Cell size", room.CellSize);
            EditorGUILayout.ObjectField("Room bounds", room.RoomBounds, typeof(Collider2D), true);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.LabelField(
                "Door count",
                room.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true)
                    .Length.ToString());
            DrawRevealButtons(room);
            if (GUILayout.Button("Delete Room"))
            {
                LevelGridEditorOperationsV2.DeleteRoom(room, true);
                RequestRefresh(true);
            }
        }

        private void DrawDoorInspector(LevelDoorEndpointAuthoring2D door)
        {
            EditorGUILayout.LabelField("Door endpoint", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Stable door ID", door.DoorIdText);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(
                "Owning room",
                door.OwningRoom,
                typeof(LevelRoomAuthoring2D),
                true);
            EditorGUI.EndDisabledGroup();

            LevelDoorSideV2 side = (LevelDoorSideV2)EditorGUILayout.EnumPopup(
                "Side",
                door.Side);
            LevelDoorPlacementModeV2 placement =
                (LevelDoorPlacementModeV2)EditorGUILayout.EnumPopup(
                    "Placement mode",
                    door.PlacementMode);
            float edgeOffset = EditorGUILayout.Slider(
                "Edge offset",
                door.EdgeOffset,
                0f,
                1f);
            Vector2 fixedPosition = EditorGUILayout.Vector2Field(
                "Fixed room-relative position",
                door.FixedLocalPosition);
            bool traversable = EditorGUILayout.Toggle("Traversable", door.Traversable);
            bool mapVisible = EditorGUILayout.Toggle("Map-visible", door.VisibleOnMap);
            bool autoFacing = EditorGUILayout.Toggle(
                "Automatic facing",
                door.AutoFaceConnection);

            if (side != door.Side
                || placement != door.PlacementMode
                || !Mathf.Approximately(edgeOffset, door.EdgeOffset)
                || fixedPosition != door.FixedLocalPosition
                || traversable != door.Traversable
                || mapVisible != door.VisibleOnMap
                || autoFacing != door.AutoFaceConnection)
            {
                LevelGridEditorOperationsV2.UpdateDoor(
                    door,
                    side,
                    placement,
                    edgeOffset,
                    fixedPosition,
                    traversable,
                    mapVisible,
                    autoFacing);
                RequestRefresh(true);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reflow"))
            {
                LevelGridEditorOperationsV2.ReflowDoor(door);
                RequestRefresh(true);
            }
            if (GUILayout.Button("Keep Placement"))
            {
                LevelGridEditorOperationsV2.KeepDoorPlacement(door);
                RequestRefresh(true);
            }
            EditorGUILayout.EndHorizontal();
            DrawRevealButtons(door);
            if (GUILayout.Button("Delete Door"))
            {
                LevelGridEditorOperationsV2.DeleteDoor(door);
                RequestRefresh(true);
            }
        }

        private void DrawConnectionInspector(LevelDoorLinkAuthoring2D connection)
        {
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Connection ID", connection.ConnectionIdText);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(
                "Source room",
                connection.SourceRoom,
                typeof(LevelRoomAuthoring2D),
                true);
            EditorGUILayout.ObjectField(
                "Source door",
                connection.SourceDoor,
                typeof(LevelDoorEndpointAuthoring2D),
                true);
            EditorGUILayout.ObjectField(
                "Destination room",
                connection.DestinationRoom,
                typeof(LevelRoomAuthoring2D),
                true);
            EditorGUILayout.ObjectField(
                "Destination door",
                connection.DestinationDoor,
                typeof(LevelDoorEndpointAuthoring2D),
                true);
            EditorGUI.EndDisabledGroup();

            LevelDoorTravelPolicy travelPolicy =
                (LevelDoorTravelPolicy)EditorGUILayout.EnumPopup(
                    "Travel policy",
                    connection.TravelPolicy);
            if (travelPolicy != connection.TravelPolicy)
            {
                LevelGridEditorOperationsV2.SetConnectionTravelPolicy(
                    connection,
                    travelPolicy);
                RequestRefresh(true);
            }

            DrawRevealButtons(connection);
            if (GUILayout.Button("Delete Connection"))
            {
                LevelGridEditorOperationsV2.DeleteConnection(connection);
                RequestRefresh(true);
            }
        }

        private void DrawProblemInspector(LevelGridProblemV2 problem)
        {
            EditorGUILayout.LabelField("Validation problem", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Code", problem.Code.ToString());
            EditorGUILayout.LabelField("Severity", problem.Severity.ToString());
            EditorGUILayout.LabelField("Stable ID", problem.AuthoredId);
            EditorGUILayout.LabelField(
                "Hierarchy path",
                problem.DiagnosticLocation,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                "Message",
                problem.Message,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select"))
            {
                SelectProblem(problem, false);
            }
            if (GUILayout.Button("Frame"))
            {
                SelectProblem(problem, true);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRevealButtons(Component component)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Hierarchy"))
            {
                suppressSelectionSync = true;
                Selection.activeGameObject = component.gameObject;
                suppressSelectionSync = false;
                EditorGUIUtility.PingObject(component.gameObject);
            }
            if (GUILayout.Button("Unity Inspector"))
            {
                suppressSelectionSync = true;
                Selection.activeObject = component;
                suppressSelectionSync = false;
                EditorGUIUtility.PingObject(component);
            }
            if (GUILayout.Button("Scene View"))
            {
                suppressSelectionSync = true;
                Selection.activeGameObject = component.gameObject;
                suppressSelectionSync = false;
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    sceneView.FrameSelected();
                    sceneView.Focus();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SelectProblem(LevelGridProblemV2 problem, bool frame)
        {
            selectedProblem = problem;
            Component selected = LevelGridEditorProblemLocatorV2.SelectExact(
                activeRoot,
                problem);
            if (selected != null)
            {
                selectedAuthoringObject = selected;
                if (frame)
                {
                    FrameSelection();
                }
            }
            Repaint();
        }

        private void DeleteSelection()
        {
            if (selectedAuthoringObject is LevelDoorEndpointAuthoring2D)
            {
                LevelGridEditorOperationsV2.DeleteDoor(
                    (LevelDoorEndpointAuthoring2D)selectedAuthoringObject);
            }
            else if (selectedAuthoringObject is LevelDoorLinkAuthoring2D)
            {
                LevelGridEditorOperationsV2.DeleteConnection(
                    (LevelDoorLinkAuthoring2D)selectedAuthoringObject);
            }
            else if (selectedAuthoringObject is LevelRoomAuthoring2D)
            {
                LevelGridEditorOperationsV2.DeleteRoom(
                    (LevelRoomAuthoring2D)selectedAuthoringObject,
                    true);
            }
            else
            {
                return;
            }
            selectedAuthoringObject = activeRoot;
            selectedProblem = null;
            RequestRefresh(true);
        }

        private void Validate(LevelGridValidationPurposeV2 purpose)
        {
            LevelGridEditorOperationsV2.Validate(activeRoot, purpose);
            projectionDirty = true;
            EnsureProjection();
            Repaint();
        }

        private void ExecuteExistingCommand(string menuPath)
        {
            if (activeRoot == null)
            {
                return;
            }

            suppressSelectionSync = true;
            Selection.activeObject = activeRoot;
            suppressSelectionSync = false;
            if (!EditorApplication.ExecuteMenuItem(menuPath))
            {
                ShowNotification(
                    "Unity could not execute the existing command: " + menuPath,
                    lastCanvasMouse);
            }
            RequestRefresh(true);
        }
    }
}
#endif

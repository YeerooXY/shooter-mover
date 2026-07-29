#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public sealed partial class LevelGridEditorWindow : EditorWindow
    {
        private Vector2 playableScroll;
        private LevelGridPlayableStatus playableStatus;
        private string playableSceneFingerprint = string.Empty;
        private string playableSourceSnapshot = string.Empty;
        private string playableOperationMessage = string.Empty;
        private MessageType playableOperationMessageType = MessageType.Info;

        private void DrawPlayablePanel(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            EditorGUILayout.LabelField("Playable Level", EditorStyles.boldLabel);
            playableScroll = EditorGUILayout.BeginScrollView(playableScroll);
            if (activeRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a level root to configure playable metadata and build output.",
                    MessageType.Info);
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            EnsurePlayableStatus();
            DrawPlayableMetadataSection();
            EditorGUILayout.Space();
            DrawPlayableDestinations();
            EditorGUILayout.Space();
            DrawPlayableStatus();
            EditorGUILayout.Space();
            DrawPlayableActions();
            if (!string.IsNullOrEmpty(playableOperationMessage))
            {
                EditorGUILayout.HelpBox(
                    playableOperationMessage,
                    playableOperationMessageType);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawIntegratedProblemsPanel(Rect rect)
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

            EnsurePlayableStatus();
            LevelDesignValidationResult foundation = activeRoot.LastValidation;
            LevelGridValidationResult grid = activeRoot.LastGridValidation;
            bool productionValidationRun = grid.Purpose
                == LevelGridValidationPurpose.ProductionPublish;
            bool publishAllowed = productionValidationRun
                && foundation.IsValid
                && grid.CanPublish;
            MessageType summaryType = !foundation.IsValid || grid.ErrorCount > 0
                ? MessageType.Error
                : foundation.WarningCount > 0 || grid.WarningCount > 0
                    ? MessageType.Warning
                    : MessageType.Info;
            string productionStatus = !productionValidationRun
                ? "not run"
                : publishAllowed ? "allowed" : "blocked";
            EditorGUILayout.HelpBox(
                "Foundation errors: " + foundation.ErrorCount
                    + " | Foundation warnings: " + foundation.WarningCount
                    + " | V2 errors: " + grid.ErrorCount
                    + " | V2 warnings: " + grid.WarningCount
                    + " | Unconnected traversable: "
                    + grid.UnconnectedTraversableDoorCount
                    + "\nDraft status: save allowed"
                    + " | Production publish: "
                    + productionStatus
                    + " | Playable pipeline: "
                    + GetPlayableProblemsSummary(),
                summaryType);

            problemsScroll = EditorGUILayout.BeginScrollView(problemsScroll);
            for (int index = 0; index < foundation.Issues.Count; index++)
            {
                DrawFoundationIssue(foundation.Issues[index]);
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

        private void DrawPlayableMetadataSection()
        {
            LevelGridPlayableMetadata metadata =
                activeRoot.GetComponent<LevelGridPlayableMetadata>();
            if (metadata == null)
            {
                EditorGUILayout.HelpBox(
                    "Playable metadata is not configured.",
                    MessageType.Warning);
                if (GUILayout.Button("Add Playable Metadata"))
                {
                    LevelGridPlayableMetadataOperations.Add(activeRoot);
                    InvalidatePlayableStatus();
                }
                return;
            }

            EditorGUILayout.LabelField("Playable metadata", EditorStyles.boldLabel);
            LevelRoom startRoom = DrawRoomPopup(
                "Start room",
                metadata.StartRoom);
            if (startRoom != metadata.StartRoom)
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperations.SetStartRoom(
                        activeRoot,
                        metadata,
                        startRoom),
                    "Start room updated.");
            }

            Vector2 playerPosition = DelayedVector2Field(
                "Player start local position",
                metadata.PlayerStartLocalPosition);
            float playerRotation = EditorGUILayout.DelayedFloatField(
                "Player start rotation",
                metadata.PlayerStartRotation);
            if (playerPosition != metadata.PlayerStartLocalPosition
                || !Mathf.Approximately(playerRotation, metadata.PlayerStartRotation))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperations.SetPlayerStart(
                        activeRoot,
                        metadata,
                        playerPosition,
                        playerRotation),
                    "Player start updated.");
            }

            LevelRoom finalRoom = DrawRoomPopup(
                "Final-exit room",
                metadata.FinalExitRoom);
            if (finalRoom != metadata.FinalExitRoom)
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperations.SetFinalRoom(
                        activeRoot,
                        metadata,
                        finalRoom),
                    "Final room updated; incompatible final-door references were cleared.");
            }

            DoorEndpoint finalDoor = DrawFinalDoorPopup(
                metadata.FinalExitRoom,
                metadata.FinalExitDoor);
            if (finalDoor != metadata.FinalExitDoor)
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperations.SetFinalDoor(
                        activeRoot,
                        metadata,
                        finalDoor),
                    "Final-exit door updated.");
            }

            string runtimeDoorObjectId = EditorGUILayout.DelayedTextField(
                "Runtime door object ID",
                metadata.RuntimeDoorObjectId);
            if (!string.Equals(
                runtimeDoorObjectId,
                metadata.RuntimeDoorObjectId,
                StringComparison.Ordinal))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperations.SetRuntimeDoorObjectId(
                        activeRoot,
                        metadata,
                        runtimeDoorObjectId),
                    "Runtime door object ID updated.");
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(
                "Exact start reference",
                metadata.StartRoom,
                typeof(LevelRoom),
                true);
            EditorGUILayout.ObjectField(
                "Exact final room reference",
                metadata.FinalExitRoom,
                typeof(LevelRoom),
                true);
            EditorGUILayout.ObjectField(
                "Exact final door reference",
                metadata.FinalExitDoor,
                typeof(DoorEndpoint),
                true);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.BeginHorizontal();
            LevelRoom selectedRoom = selectedAuthoringObject
                as LevelRoom;
            EditorGUI.BeginDisabledGroup(selectedRoom == null);
            if (GUILayout.Button("Use selected room as start"))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperations.SetStartRoom(
                        activeRoot,
                        metadata,
                        selectedRoom),
                    "Selected room assigned as start.");
            }
            if (GUILayout.Button("Use selected room as final room"))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperations.SetFinalRoom(
                        activeRoot,
                        metadata,
                        selectedRoom),
                    "Selected room assigned as final room.");
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            DoorEndpoint selectedDoor = selectedAuthoringObject
                as DoorEndpoint;
            EditorGUI.BeginDisabledGroup(selectedDoor == null);
            if (GUILayout.Button("Use selected door as final exit"))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperations.UseDoorAsFinalExit(
                        activeRoot,
                        metadata,
                        selectedDoor),
                    "Selected exact room-plus-door assigned as final exit.");
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawPlayableDestinations()
        {
            EditorGUILayout.LabelField("Build output", EditorStyles.boldLabel);
            if (playableStatus == null || playableStatus.Paths == null)
            {
                EditorGUILayout.HelpBox(
                    "Build destinations cannot be resolved.",
                    MessageType.Error);
                return;
            }
            DrawSelectablePath("Source package path", playableStatus.Paths.SourcePackagePath);
            DrawSelectablePath(
                "Generated asset folder",
                playableStatus.Paths.GeneratedAssetFolder);
            DrawSelectablePath(
                "Compiled room-content asset path",
                playableStatus.Paths.CompiledAssetPath);
            DrawSelectablePath(
                "Production Resource path",
                playableStatus.Paths.ResourcePath);
            EditorGUILayout.LabelField(
                "Production catalogue registration",
                playableStatus.Registered ? "Registered" : "Not registered");
            if (!playableStatus.Paths.IsLevel1)
            {
                EditorGUILayout.HelpBox(
                    "This generic level uses stable-ID-derived destinations and cannot reuse the "
                    + "tracked Combat Loop paths.",
                    MessageType.Info);
            }
        }

        private void DrawPlayableStatus()
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            if (playableStatus == null)
            {
                EditorGUILayout.HelpBox("Status is unavailable.", MessageType.Error);
                return;
            }
            DrawStatusLine(
                "Authoring validation",
                playableStatus.AuthoringStatus,
                playableStatus.AuthoringDetail);
            DrawStatusLine(
                "Playable metadata validation",
                playableStatus.MetadataStatus,
                playableStatus.MetadataDetail);
            DrawStatusLine(
                "Export package status",
                playableStatus.ExportStatus,
                playableStatus.ExportDetail);
            DrawStatusLine(
                "Compiled asset status",
                playableStatus.CompiledStatus,
                playableStatus.CompiledDetail);
            DrawStatusLine(
                "Production catalogue status",
                playableStatus.CatalogueStatus,
                playableStatus.CatalogueDetail);
            DrawStatusLine(
                "Play readiness",
                playableStatus.PlayStatus,
                playableStatus.PlayDetail);
        }

        private void DrawPlayableActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Playable"))
            {
                ApplyBuildResult(
                    LevelGridPlayableBuildFacade.ValidatePlayable(activeRoot));
            }
            if (GUILayout.Button("Build"))
            {
                ApplyBuildResult(
                    LevelGridPlayableBuildFacade.ExportAndCompile(activeRoot));
            }
            EditorGUI.BeginDisabledGroup(playableStatus == null || !playableStatus.PlayReady);
            if (GUILayout.Button("Open production level-selection scene"))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableBuildFacade
                        .OpenLevelMenu(activeRoot),
                    "Opened production Level Selection. Choose the exact registered level.");
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayableToolbarControls()
        {
            if (ToolbarButton("Validate", "Run every production build gate without writing files."))
            {
                ApplyBuildResult(
                    LevelGridPlayableBuildFacade.ValidatePlayable(activeRoot));
            }
            if (ToolbarButton("Build", "Validate, export playable source, then compile atomically."))
            {
                ApplyBuildResult(
                    LevelGridPlayableBuildFacade.ExportAndCompile(activeRoot));
            }
            if (ToolbarButton("Play", "Open the real production Level Selection route."))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableBuildFacade
                        .OpenLevelMenu(activeRoot),
                    "Opened production Level Selection.");
            }
            if (ToolbarButton("More", "Less frequent topology and playable actions."))
            {
                ShowPlayableMoreMenu();
            }
        }

        private void ShowPlayableMoreMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("Validation/Validate Draft"),
                false,
                () => Validate(LevelGridValidationPurpose.Draft));
            menu.AddItem(
                new GUIContent("Validation/Validate Production"),
                false,
                () => Validate(LevelGridValidationPurpose.ProductionPublish));
            menu.AddItem(
                new GUIContent("Validation/Open Problems"),
                false,
                () => LevelGridProblemsWindow.Open(activeRoot));
            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Playable/Select compiled asset"),
                false,
                () => RunPlayableOperation(
                    () => LevelGridPlayableBuildFacade.SelectCompiledAsset(activeRoot),
                    "Selected compiled asset."));
            menu.AddItem(
                new GUIContent("Playable/Reveal source folder"),
                false,
                () => RunPlayableOperation(
                    () => LevelGridPlayableBuildFacade.RevealSourceFolder(activeRoot),
                    "Revealed source folder."));
            menu.AddItem(
                new GUIContent("Playable/Reveal generated folder"),
                false,
                () => RunPlayableOperation(
                    () => LevelGridPlayableBuildFacade.RevealGeneratedFolder(activeRoot),
                    "Revealed generated folder."));
            menu.AddItem(
                new GUIContent("Playable/Open catalogue source"),
                false,
                () => RunPlayableOperation(
                    LevelGridPlayableBuildFacade.OpenCatalogueSource,
                    "Opened production catalogue source."));
            menu.AddItem(
                new GUIContent("Playable/Copy registration values"),
                false,
                () => RunPlayableOperation(
                    () => LevelGridPlayableBuildFacade.CopyRegistrationValues(activeRoot),
                    "Copied exact registration values."));
            menu.ShowAsContext();
        }

        private LevelRoom DrawRoomPopup(
            string label,
            LevelRoom current)
        {
            LevelRoom[] rooms =
                activeRoot.GetComponentsInChildren<LevelRoom>(true);
            Array.Sort(
                rooms,
                (left, right) => string.CompareOrdinal(
                    left == null ? string.Empty : left.RoomIdText,
                    right == null ? string.Empty : right.RoomIdText));
            string[] labels = new string[rooms.Length + 1];
            labels[0] = "<None>";
            int selected = 0;
            for (int index = 0; index < rooms.Length; index++)
            {
                LevelRoom room = rooms[index];
                labels[index + 1] = room.EditorLabel + " — " + room.RoomIdText;
                if (room == current) selected = index + 1;
            }
            int chosen = EditorGUILayout.Popup(label, selected, labels);
            return chosen <= 0 ? null : rooms[chosen - 1];
        }

        private DoorEndpoint DrawFinalDoorPopup(
            LevelRoom finalRoom,
            DoorEndpoint current)
        {
            var doors = new List<DoorEndpoint>();
            if (finalRoom != null)
            {
                DoorEndpoint[] owned =
                    finalRoom.GetComponentsInChildren<DoorEndpoint>(true);
                for (int index = 0; index < owned.Length; index++)
                {
                    if (owned[index] != null
                        && owned[index].OwningRoom == finalRoom
                        && owned[index].Traversable)
                    {
                        doors.Add(owned[index]);
                    }
                }
            }
            doors.Sort((left, right) => string.CompareOrdinal(
                left.DoorIdText,
                right.DoorIdText));
            string[] labels = new string[doors.Count + 1];
            labels[0] = "<None>";
            int selected = 0;
            for (int index = 0; index < doors.Count; index++)
            {
                labels[index + 1] = doors[index].DoorIdText;
                if (doors[index] == current) selected = index + 1;
            }
            int chosen = EditorGUILayout.Popup("Final-exit door", selected, labels);
            return chosen <= 0 ? null : doors[chosen - 1];
        }

        private static void DrawSelectablePath(string label, string value)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(
                value ?? string.Empty,
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private static void DrawStatusLine(
            string label,
            LevelGridPlayableStatusKind status,
            string detail)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label + ": " + StatusLabel(status),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(detail ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private static string StatusLabel(LevelGridPlayableStatusKind status)
        {
            switch (status)
            {
                case LevelGridPlayableStatusKind.NotConfigured:
                    return "Not configured";
                case LevelGridPlayableStatusKind.Valid:
                    return "Valid";
                case LevelGridPlayableStatusKind.ValidButNotExported:
                    return "Valid but not exported";
                case LevelGridPlayableStatusKind.ExportedButStale:
                    return "Exported but stale";
                case LevelGridPlayableStatusKind.ExportCurrent:
                    return "Export current";
                case LevelGridPlayableStatusKind.CompiledButStale:
                    return "Compiled but stale";
                case LevelGridPlayableStatusKind.CompiledCurrent:
                    return "Compiled current";
                case LevelGridPlayableStatusKind.NotRegistered:
                    return "Not registered";
                case LevelGridPlayableStatusKind.Registered:
                    return "Registered";
                case LevelGridPlayableStatusKind.ReadyToPlay:
                    return "Ready to play";
                default:
                    return "Invalid";
            }
        }

        private void EnsurePlayableStatus()
        {
            if (activeRoot == null)
            {
                playableStatus = null;
                return;
            }
            string sceneFingerprint =
                LevelGridPlayableProvenance.ComputeSceneFingerprint(activeRoot);
            string sourceSnapshot = "unresolved";
            try
            {
                LevelGridPlayableBuildPaths paths =
                    LevelGridPlayableBuildPaths.Resolve(activeRoot);
                sourceSnapshot = LevelGridPlayableProvenance.ComputeSourceSnapshot(
                    paths.SourcePackageAbsolutePath);
            }
            catch (Exception)
            {
                sourceSnapshot = "invalid";
            }
            if (playableStatus == null
                || !string.Equals(
                    sceneFingerprint,
                    playableSceneFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    sourceSnapshot,
                    playableSourceSnapshot,
                    StringComparison.Ordinal))
            {
                playableSceneFingerprint = sceneFingerprint;
                playableSourceSnapshot = sourceSnapshot;
                playableStatus = LevelGridPlayableStatusEvaluator.Evaluate(activeRoot);
            }
        }

        private void InvalidatePlayableStatus()
        {
            playableStatus = null;
            playableSceneFingerprint = string.Empty;
            playableSourceSnapshot = string.Empty;
            Repaint();
        }

        private string GetPlayableProblemsSummary()
        {
            if (playableStatus == null) return "status unavailable";
            if (playableStatus.PlayReady) return "ready to play";
            if (playableStatus.CompiledCurrent) return "compiled current; "
                + playableStatus.CatalogueDetail;
            if (playableStatus.ExportCurrent) return "export current; "
                + playableStatus.CompiledDetail;
            return playableStatus.PlayDetail;
        }

        private void ApplyBuildResult(LevelGridPlayableBuildResult result)
        {
            playableOperationMessage = result == null
                ? "The playable operation returned no result."
                : result.Message;
            playableOperationMessageType = result != null && result.Failure == null
                ? MessageType.Info
                : MessageType.Error;
            if (result != null && result.CompiledAsset != null)
            {
                Selection.activeObject = result.CompiledAsset;
                EditorGUIUtility.PingObject(result.CompiledAsset);
            }
            InvalidatePlayableStatus();
            ShowNotification(playableOperationMessage, lastCanvasMouse);
        }

        private void RunPlayableOperation(Action action, string successMessage)
        {
            try
            {
                action();
                playableOperationMessage = successMessage;
                playableOperationMessageType = MessageType.Info;
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException
                    || exception is StackOverflowException
                    || exception is AccessViolationException)
                {
                    throw;
                }
                playableOperationMessage = exception.Message;
                playableOperationMessageType = MessageType.Error;
            }
            InvalidatePlayableStatus();
            ShowNotification(playableOperationMessage, lastCanvasMouse);
        }
    }
}
#endif

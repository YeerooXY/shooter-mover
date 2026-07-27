#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public sealed partial class LevelGridEditorWindowV2 : EditorWindow
    {
        private Vector2 playableScroll;
        private LevelGridPlayableStatusV2 playableStatus;
        private string playableSceneFingerprint = string.Empty;
        private string playableSourceSnapshot = string.Empty;
        private double nextPlayableStatusCheck;
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
            LevelGridValidationResultV2 grid = activeRoot.LastGridValidation;
            bool publishAllowed = foundation.IsValid && grid.CanPublish;
            MessageType summaryType = !foundation.IsValid || grid.ErrorCount > 0
                ? MessageType.Error
                : foundation.WarningCount > 0 || grid.WarningCount > 0
                    ? MessageType.Warning
                    : MessageType.Info;
            EditorGUILayout.HelpBox(
                "Foundation errors: " + foundation.ErrorCount
                    + " | Foundation warnings: " + foundation.WarningCount
                    + " | V2 errors: " + grid.ErrorCount
                    + " | V2 warnings: " + grid.WarningCount
                    + " | Unconnected traversable: "
                    + grid.UnconnectedTraversableDoorCount
                    + "\nDraft status: save allowed"
                    + " | Production publish: "
                    + (publishAllowed ? "allowed" : "blocked")
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
            LevelGridPlayableMetadataV2 metadata =
                activeRoot.GetComponent<LevelGridPlayableMetadataV2>();
            if (metadata == null)
            {
                EditorGUILayout.HelpBox(
                    "Playable metadata is not configured.",
                    MessageType.Warning);
                if (GUILayout.Button("Add Playable Metadata"))
                {
                    LevelGridPlayableMetadataOperationsV2.Add(activeRoot);
                    InvalidatePlayableStatus();
                }
                return;
            }

            EditorGUILayout.LabelField("Playable metadata", EditorStyles.boldLabel);
            LevelRoomAuthoring2D startRoom = DrawRoomPopup(
                "Start room",
                metadata.StartRoom);
            if (startRoom != metadata.StartRoom)
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperationsV2.SetStartRoom(
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
                    () => LevelGridPlayableMetadataOperationsV2.SetPlayerStart(
                        activeRoot,
                        metadata,
                        playerPosition,
                        playerRotation),
                    "Player start updated.");
            }

            LevelRoomAuthoring2D finalRoom = DrawRoomPopup(
                "Final-exit room",
                metadata.FinalExitRoom);
            if (finalRoom != metadata.FinalExitRoom)
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperationsV2.SetFinalRoom(
                        activeRoot,
                        metadata,
                        finalRoom),
                    "Final room updated; incompatible final-door references were cleared.");
            }

            LevelDoorEndpointAuthoring2D finalDoor = DrawFinalDoorPopup(
                metadata.FinalExitRoom,
                metadata.FinalExitDoor);
            if (finalDoor != metadata.FinalExitDoor)
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperationsV2.SetFinalDoor(
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
                    () => LevelGridPlayableMetadataOperationsV2.SetRuntimeDoorObjectId(
                        activeRoot,
                        metadata,
                        runtimeDoorObjectId),
                    "Runtime door object ID updated.");
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(
                "Exact start reference",
                metadata.StartRoom,
                typeof(LevelRoomAuthoring2D),
                true);
            EditorGUILayout.ObjectField(
                "Exact final room reference",
                metadata.FinalExitRoom,
                typeof(LevelRoomAuthoring2D),
                true);
            EditorGUILayout.ObjectField(
                "Exact final door reference",
                metadata.FinalExitDoor,
                typeof(LevelDoorEndpointAuthoring2D),
                true);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.BeginHorizontal();
            LevelRoomAuthoring2D selectedRoom = selectedAuthoringObject
                as LevelRoomAuthoring2D;
            EditorGUI.BeginDisabledGroup(selectedRoom == null);
            if (GUILayout.Button("Use selected room as start"))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperationsV2.SetStartRoom(
                        activeRoot,
                        metadata,
                        selectedRoom),
                    "Selected room assigned as start.");
            }
            if (GUILayout.Button("Use selected room as final room"))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperationsV2.SetFinalRoom(
                        activeRoot,
                        metadata,
                        selectedRoom),
                    "Selected room assigned as final room.");
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            LevelDoorEndpointAuthoring2D selectedDoor = selectedAuthoringObject
                as LevelDoorEndpointAuthoring2D;
            EditorGUI.BeginDisabledGroup(selectedDoor == null);
            if (GUILayout.Button("Use selected door as final exit"))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableMetadataOperationsV2.UseDoorAsFinalExit(
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
            if (!playableStatus.Paths.IsTrackedCombatLoop)
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
                    LevelGridPlayableBuildFacadeV2.ValidatePlayable(activeRoot));
            }
            if (GUILayout.Button("Build"))
            {
                ApplyBuildResult(
                    LevelGridPlayableBuildFacadeV2.ExportAndCompile(activeRoot));
            }
            EditorGUI.BeginDisabledGroup(playableStatus == null || !playableStatus.PlayReady);
            if (GUILayout.Button("Open production level-selection scene"))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableBuildFacadeV2
                        .OpenProductionLevelSelectionScene(activeRoot),
                    "Opened production Level Selection. Choose the exact registered level.");
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export only"))
            {
                ApplyBuildResult(
                    LevelGridPlayableBuildFacadeV2.ExportPlayable(activeRoot));
            }
            if (GUILayout.Button("Compile only"))
            {
                ApplyBuildResult(
                    LevelGridPlayableBuildFacadeV2.CompileAsset(activeRoot));
            }
            if (GUILayout.Button("Select compiled asset"))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableBuildFacadeV2.SelectCompiledAsset(activeRoot),
                    "Selected the exact compiled runtime asset.");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayableToolbarControls()
        {
            if (ToolbarButton("Validate", "Run every production build gate without writing files."))
            {
                ApplyBuildResult(
                    LevelGridPlayableBuildFacadeV2.ValidatePlayable(activeRoot));
            }
            if (ToolbarButton("Build", "Validate, export playable source, then compile atomically."))
            {
                ApplyBuildResult(
                    LevelGridPlayableBuildFacadeV2.ExportAndCompile(activeRoot));
            }
            if (ToolbarButton("Play", "Open the real production Level Selection route."))
            {
                RunPlayableOperation(
                    () => LevelGridPlayableBuildFacadeV2
                        .OpenProductionLevelSelectionScene(activeRoot),
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
                () => Validate(LevelGridValidationPurposeV2.Draft));
            menu.AddItem(
                new GUIContent("Validation/Validate Production"),
                false,
                () => Validate(LevelGridValidationPurposeV2.ProductionPublish));
            menu.AddItem(
                new GUIContent("Validation/Open Problems"),
                false,
                () => LevelGridProblemsWindowV2.Open(activeRoot));
            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Playable/Export only"),
                false,
                () => ApplyBuildResult(
                    LevelGridPlayableBuildFacadeV2.ExportPlayable(activeRoot)));
            menu.AddItem(
                new GUIContent("Playable/Compile only"),
                false,
                () => ApplyBuildResult(
                    LevelGridPlayableBuildFacadeV2.CompileAsset(activeRoot)));
            menu.AddItem(
                new GUIContent("Playable/Select compiled asset"),
                false,
                () => RunPlayableOperation(
                    () => LevelGridPlayableBuildFacadeV2.SelectCompiledAsset(activeRoot),
                    "Selected compiled asset."));
            menu.AddItem(
                new GUIContent("Playable/Reveal source folder"),
                false,
                () => RunPlayableOperation(
                    () => LevelGridPlayableBuildFacadeV2.RevealSourceFolder(activeRoot),
                    "Revealed source folder."));
            menu.AddItem(
                new GUIContent("Playable/Reveal generated folder"),
                false,
                () => RunPlayableOperation(
                    () => LevelGridPlayableBuildFacadeV2.RevealGeneratedFolder(activeRoot),
                    "Revealed generated folder."));
            menu.AddItem(
                new GUIContent("Playable/Open catalogue source"),
                false,
                () => RunPlayableOperation(
                    LevelGridPlayableBuildFacadeV2.OpenCatalogueSource,
                    "Opened production catalogue source."));
            menu.AddItem(
                new GUIContent("Playable/Copy registration values"),
                false,
                () => RunPlayableOperation(
                    () => LevelGridPlayableBuildFacadeV2.CopyRegistrationValues(activeRoot),
                    "Copied exact registration values."));
            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Legacy/Create Three-Room Starter Example"),
                false,
                () => ExecuteExistingCommand(
                    MenuPrefix + "Create Three-Room Starter Example"));
            menu.AddItem(
                new GUIContent("Legacy/Export Grid V2 Draft Folder"),
                false,
                () => ExecuteExistingCommand(
                    MenuPrefix + "Export Grid V2 Draft Folder..."));
            menu.AddItem(
                new GUIContent("Legacy/Publish Validated Authoring Folder"),
                false,
                () => ExecuteExistingCommand(
                    MenuPrefix + "Publish Grid V2 Validated Authoring Folder..."));
            menu.ShowAsContext();
        }

        private LevelRoomAuthoring2D DrawRoomPopup(
            string label,
            LevelRoomAuthoring2D current)
        {
            LevelRoomAuthoring2D[] rooms =
                activeRoot.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
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
                LevelRoomAuthoring2D room = rooms[index];
                labels[index + 1] = room.EditorLabel + " — " + room.RoomIdText;
                if (room == current) selected = index + 1;
            }
            int chosen = EditorGUILayout.Popup(label, selected, labels);
            return chosen <= 0 ? null : rooms[chosen - 1];
        }

        private LevelDoorEndpointAuthoring2D DrawFinalDoorPopup(
            LevelRoomAuthoring2D finalRoom,
            LevelDoorEndpointAuthoring2D current)
        {
            var doors = new List<LevelDoorEndpointAuthoring2D>();
            if (finalRoom != null)
            {
                LevelDoorEndpointAuthoring2D[] owned =
                    finalRoom.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
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
            LevelGridPlayableStatusKindV2 status,
            string detail)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label + ": " + StatusLabel(status),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(detail ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private static string StatusLabel(LevelGridPlayableStatusKindV2 status)
        {
            switch (status)
            {
                case LevelGridPlayableStatusKindV2.NotConfigured:
                    return "Not configured";
                case LevelGridPlayableStatusKindV2.ValidButNotExported:
                    return "Valid but not exported";
                case LevelGridPlayableStatusKindV2.ExportedButStale:
                    return "Exported but stale";
                case LevelGridPlayableStatusKindV2.ExportCurrent:
                    return "Export current";
                case LevelGridPlayableStatusKindV2.CompiledButStale:
                    return "Compiled but stale";
                case LevelGridPlayableStatusKindV2.CompiledCurrent:
                    return "Compiled current";
                case LevelGridPlayableStatusKindV2.NotRegistered:
                    return "Not registered";
                case LevelGridPlayableStatusKindV2.Registered:
                    return "Registered";
                case LevelGridPlayableStatusKindV2.ReadyToPlay:
                    return "Ready to play";
                default:
                    return "Invalid";
            }
        }

        private void EnsurePlayableStatus()
        {
            double now = EditorApplication.timeSinceStartup;
            if (activeRoot == null)
            {
                playableStatus = null;
                return;
            }
            string sceneFingerprint =
                LevelGridPlayableProvenanceV2.ComputeSceneFingerprint(activeRoot);
            string sourceSnapshot = "unresolved";
            try
            {
                LevelGridPlayableBuildPathsV2 paths =
                    LevelGridPlayableBuildPathsV2.Resolve(activeRoot);
                sourceSnapshot = LevelGridPlayableProvenanceV2.ComputeSourceSnapshot(
                    paths.SourcePackageAbsolutePath);
            }
            catch (Exception)
            {
                sourceSnapshot = "invalid";
            }
            if (playableStatus == null
                || now >= nextPlayableStatusCheck
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
                playableStatus = LevelGridPlayableStatusEvaluatorV2.Evaluate(activeRoot);
                nextPlayableStatusCheck = now + 1d;
            }
        }

        private void InvalidatePlayableStatus()
        {
            playableStatus = null;
            playableSceneFingerprint = string.Empty;
            playableSourceSnapshot = string.Empty;
            nextPlayableStatusCheck = 0d;
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

        private void ApplyBuildResult(LevelGridPlayableBuildResultV2 result)
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
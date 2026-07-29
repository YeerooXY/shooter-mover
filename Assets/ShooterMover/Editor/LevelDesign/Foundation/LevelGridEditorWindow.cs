#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public sealed partial class LevelGridEditorWindow : EditorWindow
    {
        private const float ToolbarHeight = 44f;
        private const float BottomPanelHeight = 310f;
        private const float PanelGap = 3f;
        private const float RoomCellWidth = 190f;
        private const float RoomCellHeight = 128f;
        private const float RoomMargin = 10f;
        private const float MinZoom = 0.25f;
        private const float MaxZoom = 2.25f;
        private const float DoorRadius = 7f;
        private const string RootPreferenceKey =
            "ShooterMover.LevelGridEditor.ActiveRoot";
        private const string MenuPrefix = "Tools/Shooter Mover/Level Design/";

        private readonly Dictionary<LevelRoom, Rect> roomRects =
            new Dictionary<LevelRoom, Rect>();
        private readonly Dictionary<DoorEndpoint, Rect> doorRects =
            new Dictionary<DoorEndpoint, Rect>();
        private readonly Dictionary<DoorLink, LineVisual> linkLines =
            new Dictionary<DoorLink, LineVisual>();

        [SerializeField] private LevelDraft activeRoot;
        [SerializeField] private Vector2 pan = Vector2.zero;
        [SerializeField] private float zoom = 1f;
        [SerializeField] private UnityEngine.Object selectedAuthoringObject;

        private LevelGridEditorView projection =
            LevelGridEditorView.Empty;
        private bool projectionDirty = true;
        private bool suppressSelectionSync;
        private bool panning;
        private Vector2 panStartMouse;
        private Vector2 panStartValue;
        private LevelRoom draggedRoom;
        private Vector2 dragGridOffset;
        private Vector2Int dragPreviewCoordinate;
        private DoorEndpoint connectionDragSource;
        private Vector2 connectionDragMouse;
        private LevelGridProblem selectedProblem;
        private Vector2 problemsScroll;
        private Vector2 inspectorScroll;
        private Rect canvasRect;
        private Vector2 lastCanvasMouse;
        private string notificationMessage = string.Empty;
        private Vector2 notificationPosition;
        private double notificationUntil;
        // Retained for the legacy no-op queued-refresh callback in the state partial.
        private bool validationQueued;

        public LevelDraft ActiveRoot
        {
            get { return activeRoot; }
        }

        public LevelGridEditorView Projection
        {
            get
            {
                EnsureProjection();
                return projection;
            }
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Open Level Grid Editor",
            priority = 220)]
        public static void OpenWindow()
        {
            LevelGridEditorWindow window =
                GetWindow<LevelGridEditorWindow>();
            window.titleContent = new GUIContent("Level Grid Editor");
            window.minSize = new Vector2(780f, 520f);
            window.Show();
            window.TryAdoptSelectionRoot();
            window.Repaint();
        }

        public void SetActiveRootForTests(LevelDraft root)
        {
            SetActiveRoot(root);
        }

        public void RebuildProjectionForTests()
        {
            projectionDirty = true;
            EnsureProjection();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Level Grid Editor");
            minSize = new Vector2(780f, 520f);
            Selection.selectionChanged += OnUnitySelectionChanged;
            EditorApplication.update += OnEditorUpdate;
            RestorePersistedRoot();
            if (activeRoot == null)
            {
                TryAdoptSelectionRoot();
            }
            LoadViewState();
            projectionDirty = true;
        }

        private void OnDisable()
        {
            SaveViewState();
            Selection.selectionChanged -= OnUnitySelectionChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnGUI()
        {
            float bottomY = Mathf.Max(
                ToolbarHeight + 120f,
                position.height - BottomPanelHeight);
            canvasRect = new Rect(
                0f,
                ToolbarHeight,
                position.width,
                bottomY - ToolbarHeight - PanelGap);
            float problemsWidth = position.width * 0.36f;
            float inspectorWidth = position.width * 0.29f;
            Rect problemsRect = new Rect(
                0f,
                bottomY,
                problemsWidth - PanelGap,
                position.height - bottomY);
            Rect inspectorRect = new Rect(
                problemsRect.xMax + PanelGap,
                bottomY,
                inspectorWidth - PanelGap,
                position.height - bottomY);
            Rect playableRect = new Rect(
                inspectorRect.xMax + PanelGap,
                bottomY,
                position.width - inspectorRect.xMax - PanelGap,
                position.height - bottomY);

            EnsureProjection();
            DrawCanvas(canvasRect);
            DrawIntegratedProblemsPanel(problemsRect);
            DrawSelectionInspector(inspectorRect);
            DrawPlayablePanel(playableRect);
            // Draw the toolbar last so panned canvas visuals cannot paint over it.
            DrawToolbar();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, ToolbarHeight));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(
                new GUIContent("Select Level Root", "Choose among scene authoring roots."),
                EditorStyles.toolbarButton,
                GUILayout.Width(112f)))
            {
                ShowRootMenu();
            }

            LevelDraft chosen =
                (LevelDraft)EditorGUILayout.ObjectField(
                    activeRoot,
                    typeof(LevelDraft),
                    true,
                    GUILayout.MinWidth(130f),
                    GUILayout.MaxWidth(360f));
            if (chosen != activeRoot)
            {
                SetActiveRoot(chosen);
            }
            GUILayout.FlexibleSpace();
            DrawToolbarStatus();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.enabled = activeRoot != null;
            if (ToolbarButton("+ Room", "Create a room at the last canvas position."))
            {
                Vector2 createPosition = canvasRect.Contains(lastCanvasMouse)
                    ? lastCanvasMouse
                    : canvasRect.center;
                LevelRoom created = LevelGridEditorOperations.CreateRoom(
                    activeRoot,
                    ScreenToNearestGrid(createPosition));
                SetSelectedAuthoringObject(created);
                RequestRefresh(true);
            }
            if (ToolbarButton("Frame All", "Frame every room."))
            {
                FrameAll();
            }
            if (ToolbarButton("Frame Selection", "Frame the selected room or endpoint."))
            {
                FrameSelection();
            }
            DrawPlayableToolbarControls();
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static bool ToolbarButton(string label, string tooltip)
        {
            return GUILayout.Button(
                new GUIContent(label, tooltip),
                EditorStyles.toolbarButton);
        }

        internal void ReconcileSelectedDiagnosticsAfterValidation()
        {
            if (activeRoot == null)
            {
                selectedProblem = null;
                selectedFoundationIssue = null;
                return;
            }

            selectedProblem = FindCurrentGridProblem(selectedProblem);
            selectedFoundationIssue =
                FindCurrentFoundationIssue(selectedFoundationIssue);
        }

        private LevelGridProblem FindCurrentGridProblem(
            LevelGridProblem selected)
        {
            if (selected == null)
            {
                return null;
            }

            IReadOnlyList<LevelGridProblem> problems =
                activeRoot.LastGridValidation.Problems;
            for (int index = 0; index < problems.Count; index++)
            {
                LevelGridProblem candidate = problems[index];
                if (candidate.Code == selected.Code
                    && string.Equals(
                        candidate.AuthoredId,
                        selected.AuthoredId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.DiagnosticLocation,
                        selected.DiagnosticLocation,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
            return null;
        }

        private LevelDesignValidationIssue FindCurrentFoundationIssue(
            LevelDesignValidationIssue selected)
        {
            if (selected == null)
            {
                return null;
            }

            IReadOnlyList<LevelDesignValidationIssue> issues =
                activeRoot.LastValidation.Issues;
            for (int index = 0; index < issues.Count; index++)
            {
                LevelDesignValidationIssue candidate = issues[index];
                if (candidate.Code == selected.Code
                    && string.Equals(
                        candidate.AuthoredId,
                        selected.AuthoredId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.DiagnosticLocation,
                        selected.DiagnosticLocation,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
            return null;
        }

        private void DrawToolbarStatus()
        {
            if (activeRoot == null)
            {
                GUILayout.Label("No level root", EditorStyles.miniLabel);
                return;
            }

            LevelGridValidationResult grid = activeRoot.LastGridValidation;
            int totalWarnings = grid.WarningCount;
            GUILayout.Label(
                activeRoot.LevelIdText + "  |  " + grid.Purpose + ": "
                    + totalWarnings
                    + (totalWarnings == 1 ? " warning" : " warnings"),
                EditorStyles.miniLabel);
        }
    }
}
#endif
using System;
using ShooterMover.Application.Flow.PlaySelection;
using ShooterMover.Content.Definitions.Flow.PlayModes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.PlaySelection
{
    public sealed class RecordingPlaySelectionRouteBridge :
        IPlaySelectionRouteBridge
    {
        public PlaySelectionRoute LastRoute { get; private set; }

        public PlayerRouteProfilePayload LastPayload { get; private set; }

        public int PresentCount { get; private set; }

        public void Present(
            PlaySelectionRoute route,
            PlayerRouteProfilePayload payload)
        {
            if (route != PlaySelectionRoute.Hub
                && route != PlaySelectionRoute.LevelSelection)
            {
                throw new ArgumentOutOfRangeException(nameof(route));
            }

            LastRoute = route;
            LastPayload = payload
                ?? throw new ArgumentNullException(nameof(payload));
            PresentCount++;
        }
    }

    /// <summary>
    /// Responsive Play screen projection. It submits pure decisions to the application
    /// service and emits at most one route through the injected adapter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaySelectionController : MonoBehaviour
    {
        private const float MaximumPanelWidth = 900f;
        private const float MaximumPanelHeight = 680f;

        [SerializeField]
        private PlayModeCatalogDefinition playModeCatalog;

        private PlaySelectionActions service;
        private IPlaySelectionRouteBridge routeAdapter;
        private PlaySelectionResult lastResult;
        private bool explicitlyConfigured;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle detailStyle;
        private GUIStyle feedbackStyle;
        private Vector2 scrollPosition;

        public PlaySelectionResult LastResult
        {
            get { return lastResult; }
        }

        public PlayerRouteProfilePayload Payload
        {
            get
            {
                EnsureInitialized();
                return service.Payload;
            }
        }

        public PlayModeCatalog Catalog
        {
            get
            {
                EnsureInitialized();
                return service.Catalog;
            }
        }

        public bool IsInputLocked
        {
            get
            {
                EnsureInitialized();
                return service.IsInputLocked;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            bool keyboardBack = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            bool gamepadBack = Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (keyboardBack || gamepadBack)
            {
                NavigateBack();
            }
        }

        private void OnGUI()
        {
            EnsureInitialized();
            EnsureStyles();

            int priorDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);

            float width = Mathf.Min(
                MaximumPanelWidth,
                Mathf.Max(360f, Screen.width - 24f));
            float height = Mathf.Min(
                MaximumPanelHeight,
                Mathf.Max(320f, Screen.height - 24f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(panel, GUI.skin.window);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("SELECT PLAY MODE", titleStyle);
            GUILayout.Label(
                "Choose Solo to continue. Multiplayer is a visible placeholder only.",
                bodyStyle);
            GUILayout.Space(18f);

            if (service.Payload == null || !service.Payload.HasValidFingerprint())
            {
                GUILayout.Label(
                    "A valid Hub route payload is required before play can continue.",
                    feedbackStyle);
            }

            for (int index = 0; index < service.Catalog.Modes.Count; index++)
            {
                DrawMode(service.Catalog.Modes[index]);
                GUILayout.Space(12f);
            }

            DrawFeedback();
            GUILayout.Space(18f);
            DrawActionButton("BACK TO HUB", NavigateBack);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.depth = priorDepth;
        }

        public void Configure(
            PlayerRouteProfilePayload payload,
            PlayModeCatalog catalog,
            IPlaySelectionRouteBridge adapter)
        {
            explicitlyConfigured = true;
            service = new PlaySelectionActions(
                payload,
                catalog ?? throw new ArgumentNullException(nameof(catalog)));
            routeAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            lastResult = null;
        }

        public PlaySelectionResult SelectSolo()
        {
            return SelectMode(
                StableId.Parse(PlaySelectionActions.SoloModeStableIdText));
        }

        public PlaySelectionResult SelectMultiplayer()
        {
            return SelectMode(
                StableId.Parse(
                    PlaySelectionActions.MultiplayerModeStableIdText));
        }

        public PlaySelectionResult SelectMode(StableId modeStableId)
        {
            EnsureInitialized();
            lastResult = service.SelectMode(modeStableId);
            EmitRouteWhenAccepted(lastResult);
            return lastResult;
        }

        public PlaySelectionResult NavigateBack()
        {
            EnsureInitialized();
            lastResult = service.NavigateBack();
            EmitRouteWhenAccepted(lastResult);
            return lastResult;
        }

        private void EnsureInitialized()
        {
            if (service != null || explicitlyConfigured)
            {
                return;
            }

            PlayModeCatalog catalog = playModeCatalog == null
                ? PlayModeCatalogDefinition.CreateDefaultCatalog()
                : playModeCatalog.BuildCatalog();
            service = new PlaySelectionActions(null, catalog);
            routeAdapter = new RecordingPlaySelectionRouteBridge();
        }

        private void EmitRouteWhenAccepted(PlaySelectionResult result)
        {
            if (result == null || !result.RouteEmitted)
            {
                return;
            }

            routeAdapter.Present(result.Route, result.Payload);
        }

        private void DrawMode(PlayModeDefinition mode)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(mode.DisplayName, headingStyle);
            GUILayout.Label(mode.Description, bodyStyle);
            GUILayout.Label(
                "Mode identity: " + mode.ModeStableId,
                detailStyle);

            string actionLabel = mode.Availability
                == PlayModeAvailability.Available
                ? "SELECT"
                : "VIEW PLACEHOLDER";
            DrawActionButton(
                actionLabel,
                delegate { return SelectMode(mode.ModeStableId); });
            GUILayout.EndVertical();
        }

        private void DrawFeedback()
        {
            if (lastResult == null)
            {
                return;
            }

            switch (lastResult.Status)
            {
                case PlaySelectionStatus.ModeUnavailable:
                    GUILayout.Label(
                        "MULTIPLAYER / CO-OP IS NOT AVAILABLE YET. "
                        + "No network or gameplay session was started.",
                        feedbackStyle);
                    break;
                case PlaySelectionStatus.InvalidPayload:
                    GUILayout.Label(
                        "Cannot continue: the incoming Hub route payload is missing "
                        + "or invalid.",
                        feedbackStyle);
                    break;
                case PlaySelectionStatus.UnknownMode:
                    GUILayout.Label(
                        "Cannot continue: the selected mode identity is unknown.",
                        feedbackStyle);
                    break;
                case PlaySelectionStatus.InputLocked:
                    GUILayout.Label(
                        "A route has already been emitted. Repeated input was ignored.",
                        feedbackStyle);
                    break;
                case PlaySelectionStatus.RouteEmitted:
                    GUILayout.Label(
                        lastResult.Route == PlaySelectionRoute.LevelSelection
                            ? "Continuing to Level Selection."
                            : "Returning to the Hub.",
                        feedbackStyle);
                    break;
            }
        }

        private static void DrawActionButton(
            string label,
            Func<PlaySelectionResult> action)
        {
            if (GUILayout.Button(label, GUILayout.MinHeight(44f)))
            {
                action();
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                wordWrap = true,
            };
            detailStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                wordWrap = true,
            };
            feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
        }
    }
}

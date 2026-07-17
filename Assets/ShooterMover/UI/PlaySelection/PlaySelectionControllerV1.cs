using System;
using ShooterMover.Application.Flow.PlaySelection;
using ShooterMover.Content.Definitions.Flow.PlayModes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.PlaySelection
{
    public sealed class RecordingPlaySelectionRouteAdapterV1 :
        IPlaySelectionRouteAdapterV1
    {
        public PlaySelectionRouteV1 LastRoute { get; private set; }

        public PlayerRouteProfilePayloadV1 LastPayload { get; private set; }

        public int PresentCount { get; private set; }

        public void Present(
            PlaySelectionRouteV1 route,
            PlayerRouteProfilePayloadV1 payload)
        {
            if (route != PlaySelectionRouteV1.Hub
                && route != PlaySelectionRouteV1.LevelSelection)
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
    public sealed class PlaySelectionControllerV1 : MonoBehaviour
    {
        private const float MaximumPanelWidth = 900f;
        private const float MaximumPanelHeight = 680f;

        [SerializeField]
        private PlayModeCatalogDefinitionV1 playModeCatalog;

        private PlaySelectionServiceV1 service;
        private IPlaySelectionRouteAdapterV1 routeAdapter;
        private PlaySelectionResultV1 lastResult;
        private bool explicitlyConfigured;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle detailStyle;
        private GUIStyle feedbackStyle;
        private Vector2 scrollPosition;

        public PlaySelectionResultV1 LastResult
        {
            get { return lastResult; }
        }

        public PlayerRouteProfilePayloadV1 Payload
        {
            get
            {
                EnsureInitialized();
                return service.Payload;
            }
        }

        public PlayModeCatalogV1 Catalog
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
            PlayerRouteProfilePayloadV1 payload,
            PlayModeCatalogV1 catalog,
            IPlaySelectionRouteAdapterV1 adapter)
        {
            explicitlyConfigured = true;
            service = new PlaySelectionServiceV1(
                payload,
                catalog ?? throw new ArgumentNullException(nameof(catalog)));
            routeAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            lastResult = null;
        }

        public PlaySelectionResultV1 SelectSolo()
        {
            return SelectMode(
                StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText));
        }

        public PlaySelectionResultV1 SelectMultiplayer()
        {
            return SelectMode(
                StableId.Parse(
                    PlaySelectionServiceV1.MultiplayerModeStableIdText));
        }

        public PlaySelectionResultV1 SelectMode(StableId modeStableId)
        {
            EnsureInitialized();
            lastResult = service.SelectMode(modeStableId);
            EmitRouteWhenAccepted(lastResult);
            return lastResult;
        }

        public PlaySelectionResultV1 NavigateBack()
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

            PlayModeCatalogV1 catalog = playModeCatalog == null
                ? PlayModeCatalogDefinitionV1.CreateDefaultCatalog()
                : playModeCatalog.BuildCatalog();
            service = new PlaySelectionServiceV1(null, catalog);
            routeAdapter = new RecordingPlaySelectionRouteAdapterV1();
        }

        private void EmitRouteWhenAccepted(PlaySelectionResultV1 result)
        {
            if (result == null || !result.RouteEmitted)
            {
                return;
            }

            routeAdapter.Present(result.Route, result.Payload);
        }

        private void DrawMode(PlayModeDefinitionV1 mode)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(mode.DisplayName, headingStyle);
            GUILayout.Label(mode.Description, bodyStyle);
            GUILayout.Label(
                "Mode identity: " + mode.ModeStableId,
                detailStyle);

            string actionLabel = mode.Availability
                == PlayModeAvailabilityV1.Available
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
                case PlaySelectionStatusV1.ModeUnavailable:
                    GUILayout.Label(
                        "MULTIPLAYER / CO-OP IS NOT AVAILABLE YET. "
                        + "No network or gameplay session was started.",
                        feedbackStyle);
                    break;
                case PlaySelectionStatusV1.InvalidPayload:
                    GUILayout.Label(
                        "Cannot continue: the incoming Hub route payload is missing "
                        + "or invalid.",
                        feedbackStyle);
                    break;
                case PlaySelectionStatusV1.UnknownMode:
                    GUILayout.Label(
                        "Cannot continue: the selected mode identity is unknown.",
                        feedbackStyle);
                    break;
                case PlaySelectionStatusV1.InputLocked:
                    GUILayout.Label(
                        "A route has already been emitted. Repeated input was ignored.",
                        feedbackStyle);
                    break;
                case PlaySelectionStatusV1.RouteEmitted:
                    GUILayout.Label(
                        lastResult.Route == PlaySelectionRouteV1.LevelSelection
                            ? "Continuing to Level Selection."
                            : "Returning to the Hub.",
                        feedbackStyle);
                    break;
            }
        }

        private static void DrawActionButton(
            string label,
            Func<PlaySelectionResultV1> action)
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

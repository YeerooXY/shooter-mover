using System;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.LevelSelection
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectionControllerV1 : MonoBehaviour
    {
        private const float MaximumPanelWidth = 980f;
        private const float MaximumPanelHeight = 720f;

        [SerializeField]
        private LevelSelectionCatalogDefinitionV1 levelCatalog;

        [SerializeField]
        private Texture2D backplate;

        private LevelSelectionServiceV1 service;
        private ILevelSelectionRouteAdapterV1 routeAdapter;
        private LevelSelectionResultV1 lastResult;
        private bool explicitlyConfigured;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle detailStyle;
        private GUIStyle feedbackStyle;
        private GUIStyle prototypeStyle;
        private Vector2 scrollPosition;

        public LevelSelectionResultV1 LastResult
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

        public StableId SelectedModeStableId
        {
            get
            {
                EnsureInitialized();
                return service.SelectedModeStableId;
            }
        }

        public LevelSelectionCatalogV1 Catalog
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
            DrawBackplate();

            float width = Mathf.Min(
                MaximumPanelWidth,
                Mathf.Max(380f, Screen.width - 30f));
            float height = Mathf.Min(
                MaximumPanelHeight,
                Mathf.Max(360f, Screen.height - 30f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(panel, GUI.skin.window);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("LEVEL SELECTION", titleStyle);
            GUILayout.Label(
                "Choose a route. Level metadata owns identities, availability, "
                + "recommendations, and exact scene paths.",
                bodyStyle);
            GUILayout.Label(
                "Catalog: " + ShortFingerprint(service.Catalog.Fingerprint),
                detailStyle);

            if (service.Payload == null
                || !service.Payload.HasValidFingerprint()
                || service.SelectedModeStableId == null)
            {
                GUILayout.Space(10f);
                GUILayout.Label(
                    "A valid Hub profile/loadout payload and selected play mode "
                    + "are required before a level can launch.",
                    feedbackStyle);
            }
            else
            {
                GUILayout.Label(
                    "Mode: " + service.SelectedModeStableId
                    + "  /  Payload: "
                    + ShortFingerprint(service.Payload.Fingerprint),
                    detailStyle);
            }

            GUILayout.Space(16f);
            for (int index = 0; index < service.Catalog.Levels.Count; index++)
            {
                DrawLevel(service.Catalog.Levels[index]);
                GUILayout.Space(12f);
            }

            DrawFeedback();
            GUILayout.Space(14f);
            DrawActionButton("BACK TO PLAY SELECTION", NavigateBack, true);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.depth = priorDepth;
        }

        public void Configure(
            PlayerRouteProfilePayloadV1 payload,
            StableId selectedModeStableId,
            LevelSelectionCatalogV1 catalog,
            ILevelSelectionRouteAdapterV1 adapter)
        {
            explicitlyConfigured = true;
            service = new LevelSelectionServiceV1(
                payload,
                selectedModeStableId,
                catalog ?? throw new ArgumentNullException(nameof(catalog)));
            routeAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            lastResult = null;
        }

        public LevelSelectionResultV1 SelectLevel1()
        {
            return SelectLevel(
                StableId.Parse(
                    LevelSelectionCatalogDefinitionV1.Level1StableIdText));
        }

        public LevelSelectionResultV1 SelectLevel2()
        {
            return SelectLevel(
                StableId.Parse(
                    LevelSelectionCatalogDefinitionV1.Level2StableIdText));
        }

        public LevelSelectionResultV1 SelectLevel(StableId levelStableId)
        {
            EnsureInitialized();
            lastResult = service.SelectLevel(levelStableId);
            EmitRouteWhenAccepted(lastResult);
            return lastResult;
        }

        public LevelSelectionResultV1 NavigateBack()
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

            LevelSelectionCatalogV1 catalog = levelCatalog == null
                ? LevelSelectionCatalogDefinitionV1.CreateDefaultCatalog()
                : levelCatalog.BuildCatalog();

            PlayerRouteProfilePayloadV1 payload;
            StableId selectedModeStableId;
            StableId ignoredLevelStableId;
            LevelSelectionRouteContextV1.TryRead(
                out payload,
                out selectedModeStableId,
                out ignoredLevelStableId);

            service = new LevelSelectionServiceV1(
                payload,
                selectedModeStableId,
                catalog);
            routeAdapter = new UnityLevelSelectionRouteAdapterV1();
        }

        private void EmitRouteWhenAccepted(LevelSelectionResultV1 result)
        {
            if (result == null || !result.RouteEmitted)
            {
                return;
            }

            routeAdapter.Present(result);
        }

        private void DrawLevel(LevelSelectionDefinitionV1 definition)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(definition.DisplayName, headingStyle);

            if (definition.ReleaseState == LevelReleaseStateV1.Prototype)
            {
                GUILayout.Label("PROTOTYPE", prototypeStyle);
            }

            GUILayout.Label(definition.Description, bodyStyle);
            GUILayout.Label(
                "Recommended: player "
                + definition.Recommendation.RecommendedPlayerLevel
                + " / equipment "
                + definition.Recommendation.RecommendedEquipmentLevel
                + " / party "
                + definition.Recommendation.RecommendedPartySize
                + " / "
                + definition.Recommendation.DifficultyLabel,
                detailStyle);
            GUILayout.Label(
                "Identity: " + definition.LevelStableId,
                detailStyle);
            GUILayout.Label(
                string.IsNullOrEmpty(definition.ScenePath)
                    ? "Route: unavailable during runtime rebuild"
                    : "Route: " + definition.ScenePath,
                detailStyle);

            bool unlocked =
                definition.Availability == LevelAvailabilityV1.Unlocked;
            DrawActionButton(
                unlocked ? "SELECT" : "UNAVAILABLE",
                delegate { return SelectLevel(definition.LevelStableId); },
                unlocked);
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
                case LevelSelectionStatusV1.LevelLocked:
                    GUILayout.Label(
                        "That level is locked. No scene route was emitted.",
                        feedbackStyle);
                    break;
                case LevelSelectionStatusV1.UnknownLevel:
                    GUILayout.Label(
                        "The selected level identity is not in this catalog.",
                        feedbackStyle);
                    break;
                case LevelSelectionStatusV1.InvalidContext:
                    GUILayout.Label(
                        "The route context is missing or invalid.",
                        feedbackStyle);
                    break;
                case LevelSelectionStatusV1.InputLocked:
                    GUILayout.Label(
                        "A route was already emitted. Repeated input was ignored.",
                        feedbackStyle);
                    break;
                case LevelSelectionStatusV1.RouteEmitted:
                    GUILayout.Label(
                        lastResult.Route == LevelSelectionRouteV1.PlaySelection
                            ? "Returning to Play Selection."
                            : "Loading " + lastResult.DestinationScenePath,
                        feedbackStyle);
                    break;
            }
        }

        private static void DrawActionButton(
            string label,
            Func<LevelSelectionResultV1> action,
            bool enabled)
        {
            bool priorEnabled = GUI.enabled;
            GUI.enabled = enabled;
            if (GUILayout.Button(label, GUILayout.MinHeight(46f)))
            {
                action();
            }
            GUI.enabled = priorEnabled;
        }

        private void DrawBackplate()
        {
            GUI.Box(
                new Rect(0f, 0f, Screen.width, Screen.height),
                GUIContent.none);

            if (backplate == null)
            {
                return;
            }

            float sourceAspect =
                (float)backplate.width / Mathf.Max(1f, backplate.height);
            float screenAspect =
                (float)Screen.width / Mathf.Max(1f, Screen.height);
            Rect destination;

            if (screenAspect > sourceAspect)
            {
                float height = Screen.width / sourceAspect;
                destination = new Rect(
                    0f,
                    (Screen.height - height) * 0.5f,
                    Screen.width,
                    height);
            }
            else
            {
                float width = Screen.height * sourceAspect;
                destination = new Rect(
                    (Screen.width - width) * 0.5f,
                    0f,
                    width,
                    Screen.height);
            }

            GUI.DrawTexture(
                destination,
                backplate,
                ScaleMode.ScaleAndCrop,
                true);
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
                fontSize = 21,
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
                fontSize = 11,
                wordWrap = true,
            };
            feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            prototypeStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
        }

        private static string ShortFingerprint(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint) || fingerprint.Length <= 20)
            {
                return fingerprint ?? string.Empty;
            }

            return fingerprint.Substring(0, 20);
        }
    }
}

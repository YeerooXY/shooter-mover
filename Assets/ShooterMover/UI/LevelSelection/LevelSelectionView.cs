using System;
using ShooterMover.Application.Flow.LevelSelection;
using UnityEngine;

namespace ShooterMover.UI.LevelSelection
{
    internal sealed class LevelSelectionView
    {
        private const float MaximumPanelWidth = 980f;
        private const float MaximumPanelHeight = 720f;

        private readonly Texture2D backplate;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle detailStyle;
        private GUIStyle feedbackStyle;
        private GUIStyle prototypeStyle;
        private Vector2 scrollPosition;

        public LevelSelectionView(Texture2D backplate)
        {
            this.backplate = backplate;
        }

        public void Draw(
            LevelSelectionActions service,
            LevelSelectionResult lastResult,
            Func<LevelSelectionDefinition, LevelSelectionResult> selectLevel,
            Func<LevelSelectionResult> navigateBack)
        {
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

            DrawContext(service);
            GUILayout.Space(16f);

            if (service.Catalog.Levels.Count == 0)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("NO PLAYABLE LEVELS", headingStyle);
                GUILayout.Label(
                    "No level packages are currently registered. Create and publish "
                    + "a new level before starting a run.",
                    feedbackStyle);
                GUILayout.EndVertical();
            }
            else
            {
                for (int index = 0; index < service.Catalog.Levels.Count; index++)
                {
                    LevelSelectionDefinition definition =
                        service.Catalog.Levels[index];
                    DrawLevel(definition, selectLevel);
                    GUILayout.Space(12f);
                }
            }

            DrawFeedback(lastResult);
            GUILayout.Space(14f);
            DrawActionButton("BACK TO PLAY SELECTION", navigateBack, true);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.depth = priorDepth;
        }

        private void DrawContext(LevelSelectionActions service)
        {
            if (service.Payload == null
                || !service.Payload.HasValidFingerprint()
                || service.SelectedModeStableId == null)
            {
                GUILayout.Space(10f);
                GUILayout.Label(
                    "A valid Hub profile/loadout payload and selected play mode "
                    + "are required before a level can launch.",
                    feedbackStyle);
                return;
            }

            GUILayout.Label(
                "Mode: " + service.SelectedModeStableId
                + "  /  Payload: "
                + ShortFingerprint(service.Payload.Fingerprint),
                detailStyle);
        }

        private void DrawLevel(
            LevelSelectionDefinition definition,
            Func<LevelSelectionDefinition, LevelSelectionResult> selectLevel)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(definition.DisplayName, headingStyle);

            if (definition.ReleaseState == LevelReleaseState.Prototype)
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
                definition.Availability == LevelAvailability.Unlocked;
            DrawActionButton(
                unlocked ? "SELECT" : "UNAVAILABLE",
                delegate { return selectLevel(definition); },
                unlocked);
            GUILayout.EndVertical();
        }

        private void DrawFeedback(LevelSelectionResult lastResult)
        {
            if (lastResult == null)
            {
                return;
            }

            switch (lastResult.Status)
            {
                case LevelSelectionStatus.LevelLocked:
                    GUILayout.Label(
                        "That level is locked. No scene route was emitted.",
                        feedbackStyle);
                    break;
                case LevelSelectionStatus.UnknownLevel:
                    GUILayout.Label(
                        "The selected level identity is not in this catalog.",
                        feedbackStyle);
                    break;
                case LevelSelectionStatus.InvalidContext:
                    GUILayout.Label(
                        "The route context is missing or invalid.",
                        feedbackStyle);
                    break;
                case LevelSelectionStatus.InputLocked:
                    GUILayout.Label(
                        "A route was already emitted. Repeated input was ignored.",
                        feedbackStyle);
                    break;
                case LevelSelectionStatus.RouteEmitted:
                    GUILayout.Label(
                        lastResult.Route == LevelSelectionRoute.PlaySelection
                            ? "Returning to Play Selection."
                            : "Loading " + lastResult.DestinationScenePath,
                        feedbackStyle);
                    break;
            }
        }

        private static void DrawActionButton(
            string label,
            Func<LevelSelectionResult> action,
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

            titleStyle = CreateStyle(30, FontStyle.Bold);
            headingStyle = CreateStyle(21, FontStyle.Bold);
            bodyStyle = CreateStyle(15, FontStyle.Normal);
            detailStyle = CreateStyle(11, FontStyle.Normal);
            feedbackStyle = CreateStyle(15, FontStyle.Bold);
            prototypeStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
        }

        private static GUIStyle CreateStyle(
            int fontSize,
            FontStyle fontStyle)
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = fontStyle,
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

using System;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Results
{
    [DisallowMultipleComponent]
    public sealed class ResultsControllerV1 : MonoBehaviour
    {
        public const string ScenePath =
            "Assets/ShooterMover/Scenes/Flow/Results/Results.unity";
        public const string HubScenePath =
            "Assets/ShooterMover/Scenes/Flow/Hub/HubFlow.unity";

        [SerializeField]
        private TextAsset backgroundImageBytes;

        private MissionResultsRoutePayloadV1 route;
        private Texture2D background;
        private string diagnostic;
        private string selectedStrongboxMessage;
        private bool returnRequested;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle detailStyle;
        private Vector2 scroll;

        public bool HasValidRoute { get { return route != null; } }
        public MissionResultsRoutePayloadV1 Route { get { return route; } }
        public string Diagnostic { get { return diagnostic ?? string.Empty; } }

        private void Awake()
        {
            MissionResultsRoutePayloadV1 incoming;
            if (!MissionResultsRouteContextV1.TryRead(out incoming))
            {
                diagnostic = "No completed mission result was supplied.";
                return;
            }

            if (incoming.RoutePayload == null
                || !incoming.RoutePayload.HasValidFingerprint()
                || incoming.Summary == null
                || !incoming.Summary.HasValidFingerprint()
                || incoming.Session == null
                || incoming.Session.Snapshot == null
                || incoming.Session.Snapshot.RunStableId != incoming.Summary.RunStableId
                || incoming.Session.Snapshot.RoutePayload.Fingerprint
                    != incoming.RoutePayload.Fingerprint)
            {
                diagnostic = "The mission result handoff is invalid or inconsistent.";
                return;
            }

            route = incoming;
            background = DecodeBackground(backgroundImageBytes);
        }

        private void OnDestroy()
        {
            if (background != null)
            {
                Destroy(background);
                background = null;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawBackground();

            float width = Mathf.Min(960f, Mathf.Max(380f, Screen.width - 32f));
            float height = Mathf.Min(760f, Mathf.Max(360f, Screen.height - 32f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(panel, GUI.skin.window);
            scroll = GUILayout.BeginScrollView(scroll);

            if (route == null)
            {
                GUILayout.Label("RESULTS UNAVAILABLE", titleStyle);
                GUILayout.Space(18f);
                GUILayout.Label(Diagnostic, bodyStyle);
                GUILayout.Space(20f);
                if (GUILayout.Button("BACK TO LEVEL SELECTION", GUILayout.MinHeight(48f)))
                {
                    SceneManager.LoadScene(
                        "Assets/ShooterMover/Scenes/Flow/LevelSelection/LevelSelection.unity",
                        LoadSceneMode.Single);
                }
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            LevelRunPlayerContributionV1 local =
                route.Summary.FindContribution(
                    route.RoutePayload.SelectedCharacterStableId);
            int kills = local == null ? 0 : local.KillCount;
            long experience = local == null ? 0L : local.ExperienceEarned;

            GUILayout.Label("LEVEL COMPLETE", titleStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                "Character: " + route.RoutePayload.SelectedCharacterStableId,
                headingStyle);
            GUILayout.Label(
                "Loadout: " + route.RoutePayload.LoadoutProfileStableId,
                detailStyle);
            GUILayout.Label(
                "Level: " + route.SelectedLevelStableId,
                detailStyle);
            GUILayout.Label(
                "Completion: " + route.Summary.CompletionState,
                detailStyle);
            GUILayout.Label(
                "Run: " + route.Summary.RunStableId,
                detailStyle);

            GUILayout.Space(18f);
            GUILayout.BeginHorizontal();
            DrawMetric("KILLS", kills.ToString());
            DrawMetric("XP EARNED", experience.ToString());
            DrawMetric(
                "UNOPENED STRONGBOXES",
                route.Session.UnopenedStrongboxCount.ToString());
            GUILayout.EndHorizontal();

            GUILayout.Space(20f);
            GUILayout.Label("UNOPENED STRONGBOXES", headingStyle);
            if (route.Session.Snapshot.UnopenedStrongboxes.Count == 0)
            {
                GUILayout.Label("No strongboxes collected during this run.", bodyStyle);
            }
            else
            {
                for (int index = 0;
                    index < route.Session.Snapshot.UnopenedStrongboxes.Count;
                    index++)
                {
                    MissionRunStrongboxResultV1 strongbox =
                        route.Session.Snapshot.UnopenedStrongboxes[index];
                    string label = strongbox.DefinitionStableId
                        + "\nInstance: "
                        + strongbox.InstanceStableId;
                    if (GUILayout.Button(label, GUILayout.MinHeight(58f)))
                    {
                        selectedStrongboxMessage =
                            "Opening flow not connected here yet. The box remains unopened.";
                    }
                }
            }

            if (!string.IsNullOrEmpty(selectedStrongboxMessage))
            {
                GUILayout.Space(8f);
                GUILayout.Label(selectedStrongboxMessage, bodyStyle);
            }

            GUILayout.Space(24f);
            GUI.enabled = !returnRequested;
            if (GUILayout.Button("RETURN TO HUB", GUILayout.MinHeight(54f)))
            {
                ReturnToHub();
            }
            GUI.enabled = true;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public bool ReturnToHub()
        {
            if (returnRequested || route == null)
            {
                return false;
            }

            returnRequested = true;
            HubReturnRouteContextV1.Capture(route.RoutePayload);
            MissionResultsRouteContextV1.Clear();
            SceneManager.LoadScene(HubScenePath, LoadSceneMode.Single);
            return true;
        }

        private void DrawMetric(string label, string value)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.MinHeight(90f));
            GUILayout.Label(label, detailStyle);
            GUILayout.Label(value, headingStyle);
            GUILayout.EndVertical();
        }

        private void DrawBackground()
        {
            GUI.Box(
                new Rect(0f, 0f, Screen.width, Screen.height),
                GUIContent.none);
            if (background == null)
            {
                return;
            }

            float sourceAspect =
                (float)background.width / Mathf.Max(1f, background.height);
            float screenAspect =
                (float)Screen.width / Mathf.Max(1f, Screen.height);
            Rect target;
            if (screenAspect > sourceAspect)
            {
                float height = Screen.width / sourceAspect;
                target = new Rect(
                    0f,
                    (Screen.height - height) * 0.5f,
                    Screen.width,
                    height);
            }
            else
            {
                float width = Screen.height * sourceAspect;
                target = new Rect(
                    (Screen.width - width) * 0.5f,
                    0f,
                    width,
                    Screen.height);
            }

            GUI.DrawTexture(target, background, ScaleMode.ScaleAndCrop, false);
        }

        private static Texture2D DecodeBackground(TextAsset source)
        {
            if (source == null || source.bytes == null || source.bytes.Length == 0)
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = "ResultsBackground";
            if (ImageConversion.LoadImage(texture, source.bytes, false))
            {
                return texture;
            }

            Destroy(texture);
            return null;
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
                fontSize = 34,
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
                fontSize = 12,
                wordWrap = true,
            };
        }
    }
}

using System;
using System.Globalization;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Optional immutable presentation facts that supplement, but never replace, the exact
    /// RUN-001 mission-result payload.
    /// </summary>
    public sealed class ProductionResultsSummaryV1
    {
        public ProductionResultsSummaryV1(
            string playerName,
            string className,
            int level,
            StableId participantStableId,
            int kills,
            long experience,
            long money,
            long scrap)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                throw new ArgumentException(
                    "A Results player name is required.",
                    nameof(playerName));
            }
            if (string.IsNullOrWhiteSpace(className))
            {
                throw new ArgumentException(
                    "A Results class name is required.",
                    nameof(className));
            }
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
            if (participantStableId == null)
            {
                throw new ArgumentNullException(nameof(participantStableId));
            }
            if (kills < 0
                || experience < 0L
                || money < 0L
                || scrap < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kills),
                    "Results metrics cannot be negative.");
            }

            PlayerName = playerName.Trim();
            ClassName = className.Trim();
            Level = level;
            ParticipantStableId = participantStableId;
            Kills = kills;
            Experience = experience;
            Money = money;
            Scrap = scrap;
        }

        public string PlayerName { get; }
        public string ClassName { get; }
        public int Level { get; }
        public StableId ParticipantStableId { get; }
        public int Kills { get; }
        public long Experience { get; }
        public long Money { get; }
        public long Scrap { get; }
    }

    /// <summary>
    /// One-shot handoff for completed runs that contain no unopened strongboxes and
    /// therefore require no BOX command binding. The canonical Results controller remains
    /// the sole screen owner.
    /// </summary>
    public static class ProductionReadOnlyResultsBridgeV1
    {
        private static ReadOnlyContext pending;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            pending = null;
        }

        public static bool Present(
            ProductionFlowCoordinatorV1 flow,
            MissionResultPayloadV1 result,
            ProductionResultsSummaryV1 summary)
        {
            if (flow == null)
            {
                throw new ArgumentNullException(nameof(flow));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }
            if (result.UnopenedStrongboxes.Count != 0)
            {
                throw new InvalidOperationException(
                    "Read-only Results cannot accept unopened strongboxes without a BOX binding.");
            }
            if (pending != null || flow.Transitions.IsTransitionPending)
            {
                return false;
            }

            pending = new ReadOnlyContext(result, summary);
            if (flow.Transitions.TryLoadSubflow(
                ProductionFlowScenePathsV1.Results))
            {
                return true;
            }

            pending = null;
            return false;
        }

        internal static bool TryConfigure(
            ProductionResultsControllerV1 controller)
        {
            if (controller == null || pending == null)
            {
                return false;
            }

            ProductionFlowCoordinatorV1 flow =
                UnityEngine.Object.FindFirstObjectByType<
                    ProductionFlowCoordinatorV1>(
                    FindObjectsInactive.Include);
            if (flow == null || flow.Transitions == null)
            {
                return false;
            }

            ReadOnlyContext context = pending;
            pending = null;
            controller.Configure(
                context.Result,
                context.Summary,
                delegate { return false; },
                delegate
                {
                    return flow.Transitions.TryReturnToHub(
                        context.Result.RoutePayload);
                });
            return true;
        }

        private sealed class ReadOnlyContext
        {
            public ReadOnlyContext(
                MissionResultPayloadV1 result,
                ProductionResultsSummaryV1 summary)
            {
                Result = result;
                Summary = summary;
            }

            public MissionResultPayloadV1 Result { get; }
            public ProductionResultsSummaryV1 Summary { get; }
        }
    }

    /// <summary>
    /// Read-only Results projection over the exact immutable RUN-001 payload. Opening
    /// requests pass the exact MissionRunStrongboxResultV1 object to the composition root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionResultsControllerV1 : MonoBehaviour
    {
        [SerializeField] private TextAsset resultsBackgroundAsset;

        private MissionResultPayloadV1 result;
        private ProductionResultsSummaryV1 summary;
        private Func<MissionRunStrongboxResultV1, bool> openStrongbox;
        private Func<bool> returnToHub;
        private Texture2D background;
        private Vector2 scroll;
        private bool inputLocked;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;

        public MissionResultPayloadV1 Result { get { return result; } }
        public ProductionResultsSummaryV1 Summary { get { return summary; } }

        public MissionRunStrongboxResultV1 LastSelectedStrongbox
        {
            get;
            private set;
        }

        public int OpenRequestCount { get; private set; }

        public bool HasBackgroundAsset
        {
            get { return resultsBackgroundAsset != null; }
        }

        private void Awake()
        {
            ProductionReadOnlyResultsBridgeV1.TryConfigure(this);
        }

        public void Configure(
            MissionResultPayloadV1 result,
            Func<MissionRunStrongboxResultV1, bool> openStrongbox,
            Func<bool> returnToHub)
        {
            Configure(result, null, openStrongbox, returnToHub);
        }

        public void Configure(
            MissionResultPayloadV1 result,
            ProductionResultsSummaryV1 summary,
            Func<MissionRunStrongboxResultV1, bool> openStrongbox,
            Func<bool> returnToHub)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.summary = summary;
            this.openStrongbox = openStrongbox
                ?? throw new ArgumentNullException(nameof(openStrongbox));
            this.returnToHub = returnToHub
                ?? throw new ArgumentNullException(nameof(returnToHub));
            LastSelectedStrongbox = null;
            OpenRequestCount = 0;
            inputLocked = false;
        }

        private void Update()
        {
            bool back = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            back |= Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (back) Back();
        }

        private void OnGUI()
        {
            EnsureTexture();
            EnsureStyles();
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            if (background != null)
            {
                GUI.DrawTexture(
                    screen,
                    background,
                    ScaleMode.ScaleAndCrop,
                    false);
            }
            else
            {
                GUI.Box(screen, GUIContent.none);
            }

            float width = Mathf.Min(1120f, Mathf.Max(480f, Screen.width - 32f));
            float height = Mathf.Min(760f, Mathf.Max(360f, Screen.height - 32f));
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height),
                GUI.skin.window);
            GUILayout.Label("RESULTS", titleStyle);

            if (result == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    "Awaiting an immutable RUN-001 mission result.",
                    bodyStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndArea();
                return;
            }

            DrawSummary();
            GUILayout.Label(
                "Run " + result.RunStableId
                + "  •  "
                + result.CompletionState,
                bodyStyle);
            GUILayout.Label(
                "Result " + result.Fingerprint,
                smallStyle);
            GUILayout.Space(12f);

            scroll = GUILayout.BeginScrollView(scroll);
            if (result.Strongboxes.Count == 0)
            {
                GUILayout.Label(
                    "Collected strongboxes: none (authoritative empty result).",
                    bodyStyle);
            }
            for (int index = 0; index < result.Strongboxes.Count; index++)
            {
                DrawStrongbox(result.Strongboxes[index]);
            }
            GUILayout.EndScrollView();

            GUI.enabled = !inputLocked;
            if (GUILayout.Button("RETURN TO HUB", GUILayout.Height(46f)))
            {
                Back();
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        public bool OpenExact(MissionRunStrongboxResultV1 strongbox)
        {
            if (inputLocked
                || result == null
                || strongbox == null
                || !strongbox.IsUnopened)
            {
                return false;
            }

            bool exactReference = false;
            for (int index = 0; index < result.UnopenedStrongboxes.Count; index++)
            {
                if (ReferenceEquals(
                    result.UnopenedStrongboxes[index],
                    strongbox))
                {
                    exactReference = true;
                    break;
                }
            }

            if (!exactReference) return false;

            OpenRequestCount++;
            if (!openStrongbox(strongbox)) return false;
            LastSelectedStrongbox = strongbox;
            inputLocked = true;
            return true;
        }

        public bool Back()
        {
            if (inputLocked || returnToHub == null) return false;
            if (!returnToHub()) return false;
            inputLocked = true;
            return true;
        }

        private void DrawSummary()
        {
            if (summary == null)
            {
                return;
            }

            GUILayout.Label(
                summary.PlayerName
                + "  •  "
                + summary.ClassName
                + "  •  Level "
                + summary.Level.ToString(CultureInfo.InvariantCulture),
                bodyStyle);
            GUILayout.Label(
                "Participant: " + summary.ParticipantStableId,
                smallStyle);
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            DrawMetric(
                "KILLS",
                summary.Kills.ToString(CultureInfo.InvariantCulture));
            DrawMetric(
                "XP EARNED",
                summary.Experience.ToString(CultureInfo.InvariantCulture));
            DrawMetric(
                "MONEY",
                summary.Money.ToString(CultureInfo.InvariantCulture));
            DrawMetric(
                "SCRAP",
                summary.Scrap.ToString(CultureInfo.InvariantCulture));
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);
        }

        private void DrawMetric(string label, string value)
        {
            GUILayout.BeginVertical(
                GUI.skin.box,
                GUILayout.MinWidth(180f));
            GUILayout.Label(label, smallStyle);
            GUILayout.Label(value, titleStyle);
            GUILayout.EndVertical();
        }

        private void DrawStrongbox(MissionRunStrongboxResultV1 strongbox)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(
                strongbox.IsUnopened ? "UNOPENED STRONGBOX" : "OPENED STRONGBOX",
                bodyStyle);
            GUILayout.Label(
                "Definition: " + strongbox.DefinitionStableId
                + "\nInstance: " + strongbox.InstanceStableId
                + "\nFact: " + strongbox.Fingerprint,
                smallStyle);
            GUI.enabled = !inputLocked && strongbox.IsUnopened;
            if (GUILayout.Button(
                strongbox.IsUnopened ? "OPEN THIS EXACT INSTANCE" : "ALREADY OPENED",
                GUILayout.Height(40f)))
            {
                OpenExact(strongbox);
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        private void EnsureTexture()
        {
            if (background != null
                || resultsBackgroundAsset == null
                || resultsBackgroundAsset.bytes.Length == 0)
            {
                return;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(
                    resultsBackgroundAsset.text.Trim());
            }
            catch (FormatException)
            {
                return;
            }

            Texture2D loaded = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false);
            if (ImageConversion.LoadImage(loaded, bytes, false))
            {
                background = loaded;
            }
            else
            {
                Destroy(loaded);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                wordWrap = true,
            };
        }

        private void OnDestroy()
        {
            if (background != null)
            {
                Destroy(background);
                background = null;
            }
        }
    }
}

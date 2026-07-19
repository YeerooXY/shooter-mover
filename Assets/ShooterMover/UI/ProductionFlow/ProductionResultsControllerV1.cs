using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Contracts.Missions.Results;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Read-only Results projection over the exact immutable RUN-001 payload. Opening
    /// requests pass the exact MissionRunStrongboxResultV1 object to the composition root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionResultsControllerV1 : MonoBehaviour
    {
        [SerializeField] private TextAsset resultsBackgroundAsset;

        private MissionResultPayloadV1 result;
        private Func<MissionRunStrongboxResultV1, bool> openStrongbox;
        private Func<bool> returnToHub;
        private Texture2D background;
        private Vector2 scroll;
        private bool inputLocked;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;

        public MissionResultPayloadV1 Result { get { return result; } }

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

        public void Configure(
            MissionResultPayloadV1 result,
            Func<MissionRunStrongboxResultV1, bool> openStrongbox,
            Func<bool> returnToHub)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
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
            if (inputLocked || strongbox == null || !strongbox.IsUnopened)
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

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Canonical Main Menu projection. It reuses the supplied Main Menu art and exposes
    /// only Play; route state remains outside this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionMainMenuControllerV1 : MonoBehaviour
    {
        [SerializeField] private TextAsset mainMenuBackgroundAsset;

        private Texture2D background;
        private Func<bool> requestPlay;
        private bool requestAccepted;
        private GUIStyle titleStyle;
        private GUIStyle buttonStyle;

        public int PlayRequestCount { get; private set; }

        public bool IsConfigured { get { return requestPlay != null; } }

        public bool HasBackgroundAsset
        {
            get { return mainMenuBackgroundAsset != null; }
        }

        public void Configure(Func<bool> play)
        {
            requestPlay = play ?? throw new ArgumentNullException(nameof(play));
            requestAccepted = false;
            PlayRequestCount = 0;
        }

        private void Update()
        {
            bool confirm = Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.spaceKey.wasPressedThisFrame);
            confirm |= Gamepad.current != null
                && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (confirm) RequestPlay();
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

            GUI.Label(
                new Rect(0f, Screen.height * 0.16f, Screen.width, 58f),
                "SHOOTER MOVER",
                titleStyle);
            GUI.enabled = !requestAccepted;
            Rect playRect = new Rect(
                Screen.width * 0.35f,
                Screen.height * 0.58f,
                Screen.width * 0.30f,
                62f);
            if (GUI.Button(playRect, "PLAY", buttonStyle))
            {
                RequestPlay();
            }
            GUI.enabled = true;
        }

        public bool RequestPlay()
        {
            if (requestAccepted || requestPlay == null)
            {
                return false;
            }

            PlayRequestCount++;
            if (!requestPlay())
            {
                return false;
            }

            requestAccepted = true;
            return true;
        }

        private void EnsureTexture()
        {
            if (background != null
                || mainMenuBackgroundAsset == null
                || mainMenuBackgroundAsset.bytes.Length == 0)
            {
                return;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(
                    mainMenuBackgroundAsset.text.Trim());
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
                fontSize = 38,
                fontStyle = FontStyle.Bold,
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
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

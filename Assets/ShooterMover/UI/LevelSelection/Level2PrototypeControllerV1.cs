using System;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.LevelSelection
{
    [DisallowMultipleComponent]
    public sealed class Level2PrototypeControllerV1 : MonoBehaviour
    {
        public const string LevelSelectionScenePath =
            "Assets/ShooterMover/Scenes/Flow/LevelSelection/LevelSelection.unity";

        private ILevelSelectionSceneLoaderV1 sceneLoader;
        private bool inputLocked;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle detailStyle;

        public bool IsInputLocked
        {
            get { return inputLocked; }
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
                BackToLevelSelection();
            }
        }

        private void OnGUI()
        {
            EnsureInitialized();
            EnsureStyles();

            GUI.Box(
                new Rect(0f, 0f, Screen.width, Screen.height),
                GUIContent.none);

            float width = Mathf.Min(760f, Mathf.Max(340f, Screen.width - 30f));
            float height = Mathf.Min(520f, Mathf.Max(300f, Screen.height - 30f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(panel, GUI.skin.window);
            GUILayout.Label("LEVEL 2", titleStyle);
            GUILayout.Label("PROTOTYPE", headingStyle);
            GUILayout.Space(14f);
            GUILayout.Label(
                "This is a bounded placeholder scene. It starts no combat, "
                + "rewards, XP grants, inventory changes, or run authority.",
                bodyStyle);
            GUILayout.Space(14f);
            DrawRouteContext();
            GUILayout.FlexibleSpace();

            bool priorEnabled = GUI.enabled;
            GUI.enabled = !inputLocked;
            if (GUILayout.Button(
                "BACK TO LEVEL SELECTION",
                GUILayout.MinHeight(48f)))
            {
                BackToLevelSelection();
            }
            GUI.enabled = priorEnabled;
            GUILayout.EndArea();
        }

        public void ConfigureForTests(ILevelSelectionSceneLoaderV1 loader)
        {
            sceneLoader = loader
                ?? throw new ArgumentNullException(nameof(loader));
            inputLocked = false;
        }

        public bool BackToLevelSelection()
        {
            EnsureInitialized();
            if (inputLocked)
            {
                return false;
            }

            inputLocked = true;
            sceneLoader.Load(LevelSelectionScenePath);
            return true;
        }

        private void DrawRouteContext()
        {
            PlayerRouteProfilePayloadV1 payload;
            StableId modeStableId;
            StableId levelStableId;
            if (!LevelSelectionRouteContextV1.TryRead(
                out payload,
                out modeStableId,
                out levelStableId))
            {
                GUILayout.Label(
                    "No valid route context is available.",
                    detailStyle);
                return;
            }

            GUILayout.Label("Mode: " + modeStableId, detailStyle);
            GUILayout.Label(
                "Level identity: "
                + (levelStableId == null
                    ? string.Empty
                    : levelStableId.ToString()),
                detailStyle);
            GUILayout.Label(
                "Character: " + payload.SelectedCharacterStableId,
                detailStyle);
            GUILayout.Label(
                "Loadout: " + payload.LoadoutProfileStableId,
                detailStyle);
            GUILayout.Label(
                "Payload: " + payload.Fingerprint,
                detailStyle);
        }

        private void EnsureInitialized()
        {
            if (sceneLoader == null)
            {
                sceneLoader = new UnityLevelSelectionSceneLoaderV1();
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
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            headingStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
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

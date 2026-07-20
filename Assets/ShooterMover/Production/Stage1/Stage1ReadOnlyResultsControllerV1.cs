using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.ContentPackages.Enemies.BlasterTurret;
using ShooterMover.ContentPackages.Enemies.MobileBlasterDroid;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    internal sealed class Stage1ReadOnlyResultsProjectionV1
    {
        public Stage1ReadOnlyResultsProjectionV1(
            MissionResultPayloadV1 result,
            string playerName,
            string className,
            int level,
            StableId participantStableId,
            int kills,
            long experience,
            long money,
            long scrap)
        {
            Result = result;
            PlayerName = playerName;
            ClassName = className;
            Level = level;
            ParticipantStableId = participantStableId;
            Kills = kills;
            Experience = experience;
            Money = money;
            Scrap = scrap;
        }

        public MissionResultPayloadV1 Result { get; }
        public string PlayerName { get; }
        public string ClassName { get; }
        public int Level { get; }
        public StableId ParticipantStableId { get; }
        public int Kills { get; }
        public long Experience { get; }
        public long Money { get; }
        public long Scrap { get; }
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1ReadOnlyResultsControllerV1 : MonoBehaviour
    {
        private Stage1ReadOnlyResultsProjectionV1 projection;
        private ProductionFlowCoordinatorV1 flow;
        private TextAsset backgroundAsset;
        private Texture2D background;
        private Vector2 scroll;
        private bool locked;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;

        public void Configure(
            Stage1ReadOnlyResultsProjectionV1 configuredProjection,
            TextAsset suppliedBackground)
        {
            projection = configuredProjection;
            backgroundAsset = suppliedBackground;
            flow = FindFirstObjectByType<ProductionFlowCoordinatorV1>(
                FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (locked) return;
            bool back = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            back |= Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (back) ReturnToHub();
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
            float width = Mathf.Min(980f, Mathf.Max(520f, Screen.width - 40f));
            float height = Mathf.Min(760f, Mathf.Max(420f, Screen.height - 40f));
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height),
                GUI.skin.window);
            GUILayout.Label("MISSION RESULTS", titleStyle);
            if (projection == null || projection.Result == null)
            {
                GUILayout.Label("No authoritative result is available.", bodyStyle);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label(
                projection.PlayerName + "  •  " + projection.ClassName
                + "  •  Level " + projection.Level,
                bodyStyle);
            GUILayout.Label(
                "Participant: " + projection.ParticipantStableId,
                smallStyle);
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            DrawMetric("KILLS", projection.Kills.ToString(CultureInfo.InvariantCulture));
            DrawMetric("XP EARNED", projection.Experience.ToString(CultureInfo.InvariantCulture));
            DrawMetric("MONEY", projection.Money.ToString(CultureInfo.InvariantCulture));
            DrawMetric("SCRAP", projection.Scrap.ToString(CultureInfo.InvariantCulture));
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);
            GUILayout.Label(
                "Run " + projection.Result.RunStableId
                + "  •  " + projection.Result.CompletionState,
                bodyStyle);
            GUILayout.Label(
                "Result " + projection.Result.Fingerprint,
                smallStyle);

            scroll = GUILayout.BeginScrollView(scroll);
            if (projection.Result.Strongboxes.Count == 0)
            {
                GUILayout.Label(
                    "Collected strongboxes: none (authoritative empty result).",
                    bodyStyle);
            }
            for (int index = 0; index < projection.Result.Strongboxes.Count; index++)
            {
                MissionRunStrongboxResultV1 box =
                    projection.Result.Strongboxes[index];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(
                    (box.IsUnopened ? "UNOPENED" : "OPENED")
                    + "  " + box.DefinitionStableId,
                    bodyStyle);
                GUILayout.Label(
                    "Exact instance: " + box.InstanceStableId
                    + "\nFact: " + box.Fingerprint,
                    smallStyle);
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();

            GUI.enabled = !locked;
            if (GUILayout.Button("RETURN TO HUB", GUILayout.Height(48f)))
            {
                ReturnToHub();
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawMetric(string label, string value)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.MinWidth(180f));
            GUILayout.Label(label, smallStyle);
            GUILayout.Label(value, titleStyle);
            GUILayout.EndVertical();
        }

        private void ReturnToHub()
        {
            if (locked
                || projection == null
                || projection.Result == null
                || flow == null)
            {
                return;
            }
            if (flow.Transitions.TryReturnToHub(projection.Result.RoutePayload))
            {
                Stage1PlayableLoopCompositionV1.ClearPendingResults();
                locked = true;
            }
        }

        private void EnsureTexture()
        {
            if (background != null
                || backgroundAsset == null
                || backgroundAsset.bytes.Length == 0)
            {
                return;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(backgroundAsset.text.Trim());
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

        private void OnDestroy()
        {
            if (background != null)
            {
                Destroy(background);
                background = null;
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
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
    }
}

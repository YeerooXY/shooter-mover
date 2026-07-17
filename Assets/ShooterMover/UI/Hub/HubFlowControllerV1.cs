using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Hub
{
    /// <summary>
    /// Test/destination shell used until the separately owned destination scenes land.
    /// It records projection only and never mutates the route payload or any authority.
    /// </summary>
    public sealed class HubRoutePlaceholderAdapterV1 : IHubRouteDestinationAdapterV1
    {
        public HubRouteV1 LastRoute { get; private set; }

        public PlayerRouteProfilePayloadV1 LastPayload { get; private set; }

        public int PresentCount { get; private set; }

        public void Present(HubRouteV1 route, PlayerRouteProfilePayloadV1 payload)
        {
            LastRoute = route;
            LastPayload = payload ?? throw new ArgumentNullException(nameof(payload));
            PresentCount++;
        }
    }

    /// <summary>
    /// Responsive IMGUI projection for Main Menu -> Character Select -> Hub and
    /// placeholder destinations. The controller owns route presentation only.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class HubFlowControllerV1 : MonoBehaviour
    {
        private const float MaximumPanelWidth = 1040f;
        private const float MaximumPanelHeight = 760f;

        private HubNavigationServiceV1 navigation;
        private IHubRouteDestinationAdapterV1 destinationAdapter;
        private HubNavigationResultV1 lastNavigationResult;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle fingerprintStyle;
        private Vector2 scrollPosition;

        public HubRouteV1 CurrentRoute
        {
            get
            {
                EnsureInitialized();
                return navigation.CurrentRoute;
            }
        }

        public PlayerRouteProfilePayloadV1 Payload
        {
            get
            {
                EnsureInitialized();
                return navigation.Payload;
            }
        }

        public HubNavigationResultV1 LastNavigationResult
        {
            get { return lastNavigationResult; }
        }

        public HubNavigationSnapshotV1 ExportSnapshot()
        {
            EnsureInitialized();
            return navigation.ExportSnapshot();
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
                Mathf.Max(300f, Screen.height - 24f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(panel, GUI.skin.window);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            DrawHeader();
            GUILayout.Space(14f);

            switch (navigation.CurrentRoute)
            {
                case HubRouteV1.MainMenu:
                    DrawMainMenu();
                    break;
                case HubRouteV1.CharacterSelect:
                    DrawCharacterSelect();
                    break;
                case HubRouteV1.InventoryLoadoutHub:
                    DrawHub();
                    break;
                case HubRouteV1.Inventory:
                case HubRouteV1.Skills:
                case HubRouteV1.Shop:
                case HubRouteV1.Crafting:
                case HubRouteV1.Play:
                    DrawDestinationShell(navigation.CurrentRoute);
                    break;
                default:
                    GUILayout.Label("Unsupported route.", bodyStyle);
                    break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.depth = priorDepth;
        }

        public void ConfigureForTests(
            PlayerRouteProfilePayloadV1 payload,
            IHubRouteDestinationAdapterV1 adapter)
        {
            navigation = new HubNavigationServiceV1(
                payload ?? throw new ArgumentNullException(nameof(payload)));
            destinationAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            lastNavigationResult = null;
            destinationAdapter.Present(navigation.CurrentRoute, navigation.Payload);
        }

        public bool OpenCharacterSelect()
        {
            return NavigateTo(HubRouteV1.CharacterSelect);
        }

        public bool ContinueToHub()
        {
            return NavigateTo(HubRouteV1.InventoryLoadoutHub);
        }

        public bool OpenDestination(HubRouteV1 route)
        {
            if (!HubNavigationServiceV1.IsHubDestination(route))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(route),
                    "Only Inventory, Skills, Shop, Crafting, or Play are Hub destinations.");
            }

            return NavigateTo(route);
        }

        public bool ReturnToHub()
        {
            EnsureInitialized();
            if (!HubNavigationServiceV1.IsHubDestination(navigation.CurrentRoute))
            {
                return false;
            }

            return NavigateBack();
        }

        public bool GoToMainMenu()
        {
            return NavigateTo(HubRouteV1.MainMenu);
        }

        public bool NavigateBack()
        {
            EnsureInitialized();
            lastNavigationResult = navigation.NavigateBack();
            if (lastNavigationResult.Changed)
            {
                destinationAdapter.Present(
                    navigation.CurrentRoute,
                    navigation.Payload);
            }

            return lastNavigationResult.Changed;
        }

        private bool NavigateTo(HubRouteV1 route)
        {
            EnsureInitialized();
            lastNavigationResult = navigation.TryNavigateTo(route);
            if (lastNavigationResult.Changed)
            {
                destinationAdapter.Present(
                    navigation.CurrentRoute,
                    navigation.Payload);
            }

            return lastNavigationResult.Changed;
        }

        private void EnsureInitialized()
        {
            if (navigation != null)
            {
                return;
            }

            PlayerRouteProfilePayloadV1 payload =
                PlayerRouteProfilePayloadV1.Create(
                    StableId.Parse("character.default-pilot"),
                    StableId.Parse("loadout-profile.hub-session-default"),
                    new List<StableId>
                    {
                        StableId.Parse("equipment-instance.hub-slot-1"),
                        StableId.Parse("equipment-instance.hub-slot-2"),
                        StableId.Parse("equipment-instance.hub-slot-3"),
                        StableId.Parse("equipment-instance.hub-slot-4"),
                    });
            destinationAdapter = new HubRoutePlaceholderAdapterV1();
            navigation = new HubNavigationServiceV1(payload);
            destinationAdapter.Present(navigation.CurrentRoute, navigation.Payload);
        }

        private void DrawHeader()
        {
            GUILayout.Label("SHOOTER MOVER", titleStyle);
            GUILayout.Label(RouteLabel(navigation.CurrentRoute), headingStyle);
            GUILayout.Label(
                "Route payload: " + ShortFingerprint(navigation.Payload.Fingerprint),
                fingerprintStyle);
        }

        private void DrawMainMenu()
        {
            GUILayout.Label(
                "Canonical session entry. Continue to the character-selection shell; "
                + "the immutable profile/loadout payload is retained for the full route.",
                bodyStyle);
            GUILayout.Space(24f);
            DrawButton("CHARACTER SELECT", OpenCharacterSelect);
        }

        private void DrawCharacterSelect()
        {
            GUILayout.Label("SELECTED CHARACTER", headingStyle);
            GUILayout.Label(
                navigation.Payload.SelectedCharacterStableId.ToString(),
                bodyStyle);
            GUILayout.Space(10f);
            GUILayout.Label(
                "This shell confirms the selected identity. CHAR-001 may later project "
                + "its real selection through the same immutable V1 payload contract.",
                bodyStyle);
            GUILayout.Space(24f);
            DrawButton("CONTINUE TO INVENTORY / LOADOUT HUB", ContinueToHub);
            DrawButton("BACK", NavigateBack);
        }

        private void DrawHub()
        {
            GUILayout.Label("PROFILE", headingStyle);
            GUILayout.Label(
                navigation.Payload.SelectedCharacterStableId
                + "  /  "
                + navigation.Payload.LoadoutProfileStableId,
                bodyStyle);
            GUILayout.Space(12f);

            GUILayout.Label("WEAPON LOADOUT — CONCRETE INSTANCE IDENTITIES", headingStyle);
            for (int index = 0; index < navigation.Payload.WeaponSlots.Count; index++)
            {
                PlayerRouteWeaponSlotV1 slot = navigation.Payload.WeaponSlots[index];
                GUILayout.Label(
                    slot.WeaponSlotStableId
                    + "  →  "
                    + slot.EquipmentInstanceStableId,
                    bodyStyle);
            }

            GUILayout.Space(20f);
            GUILayout.BeginHorizontal();
            DrawButton("INVENTORY", delegate { return OpenDestination(HubRouteV1.Inventory); });
            DrawButton("SKILLS", delegate { return OpenDestination(HubRouteV1.Skills); });
            DrawButton("SHOP", delegate { return OpenDestination(HubRouteV1.Shop); });
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawButton("CRAFTING", delegate { return OpenDestination(HubRouteV1.Crafting); });
            DrawButton("PLAY", delegate { return OpenDestination(HubRouteV1.Play); });
            GUILayout.EndHorizontal();

            GUILayout.Space(18f);
            DrawButton("BACK", NavigateBack);
            DrawButton("MAIN MENU", GoToMainMenu);
        }

        private void DrawDestinationShell(HubRouteV1 route)
        {
            GUILayout.Label(RouteLabel(route), titleStyle);
            GUILayout.Label(
                "Placeholder destination adapter. The separately owned screen may "
                + "replace this projection without changing the route payload.",
                bodyStyle);
            GUILayout.Space(12f);
            GUILayout.Label(
                "Character: " + navigation.Payload.SelectedCharacterStableId,
                bodyStyle);
            GUILayout.Label(
                "Loadout profile: " + navigation.Payload.LoadoutProfileStableId,
                bodyStyle);
            GUILayout.Label(
                "Payload fingerprint: " + navigation.Payload.Fingerprint,
                fingerprintStyle);
            GUILayout.Space(20f);
            DrawButton("RETURN TO HUB", ReturnToHub);
            DrawButton("MAIN MENU", GoToMainMenu);
        }

        private static void DrawButton(string label, Func<bool> action)
        {
            if (GUILayout.Button(label, GUILayout.MinHeight(46f)))
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
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                wordWrap = true,
            };
            fingerprintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                wordWrap = true,
            };
        }

        private static string RouteLabel(HubRouteV1 route)
        {
            switch (route)
            {
                case HubRouteV1.MainMenu:
                    return "MAIN MENU";
                case HubRouteV1.CharacterSelect:
                    return "CHARACTER SELECT";
                case HubRouteV1.InventoryLoadoutHub:
                    return "INVENTORY / LOADOUT HUB";
                case HubRouteV1.Inventory:
                    return "INVENTORY";
                case HubRouteV1.Skills:
                    return "SKILLS";
                case HubRouteV1.Shop:
                    return "SHOP";
                case HubRouteV1.Crafting:
                    return "CRAFTING";
                case HubRouteV1.Play:
                    return "PLAY";
                default:
                    return route.ToString().ToUpperInvariant();
            }
        }

        private static string ShortFingerprint(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint) || fingerprint.Length <= 16)
            {
                return fingerprint ?? string.Empty;
            }

            return fingerprint.Substring(0, 16);
        }
    }

    /// <summary>
    /// Installs the HUB-owned route projection when the accepted Main Menu scene is
    /// loaded. No MENU-owned serialized file or runtime authority is modified.
    /// </summary>
    internal static class HubFlowRuntimeBootstrapV1
    {
        private const string MainMenuScenePath =
            "Assets/ShooterMover/Scenes/Menu/MainMenu.unity";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForActiveScene()
        {
            Install(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (!scene.IsValid()
                || !string.Equals(scene.path, MainMenuScenePath, StringComparison.Ordinal))
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (roots[rootIndex].GetComponentInChildren<HubFlowControllerV1>(true) != null)
                {
                    return;
                }
            }

            var host = new GameObject("HUB-001 Route Host");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<HubFlowControllerV1>();
        }
    }
}

using System;
using System.Collections.Generic;
using ShooterMover.Application.Crafting;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Menu;
using ShooterMover.Application.Shops;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.MainMenu
{
    /// <summary>
    /// Standalone IMGUI shell for MENU-001. All durable economy, holdings, shop,
    /// crafting, and gameplay mutations remain delegated to their existing services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        private const float PanelWidth = 820f;
        private const float PanelHeight = 680f;

        private MainMenuFlowState state;
        private IMainMenuPlatformActions platformActions;
        private PlayerHoldingsService holdingsService;
        private EquipmentCatalog equipmentCatalog;
        private ShopRuntimeServiceV1 shopService;
        private CraftingServiceV1 craftingService;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle selectedSlotStyle;

        public MainMenuFlowState State
        {
            get
            {
                EnsureInitialized();
                return state;
            }
        }

        public PlayerHoldingsService BoundHoldingsService
        {
            get { return holdingsService; }
        }

        public ShopRuntimeServiceV1 BoundShopService
        {
            get { return shopService; }
        }

        public CraftingServiceV1 BoundCraftingService
        {
            get { return craftingService; }
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

            float width = Mathf.Min(PanelWidth, Mathf.Max(420f, Screen.width - 40f));
            float height = Mathf.Min(PanelHeight, Mathf.Max(360f, Screen.height - 40f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(panel, GUI.skin.window);
            DrawHeader();
            GUILayout.Space(12f);

            switch (state.CurrentScreen)
            {
                case MainMenuScreen.Title:
                    DrawTitleScreen();
                    break;
                case MainMenuScreen.Armory:
                    DrawArmory();
                    break;
                case MainMenuScreen.Shop:
                    DrawServiceShell(
                        "SHOP",
                        state.ShopConnected,
                        "The procedural shop runtime is connected. "
                        + "Purchases remain owned by ShopRuntimeServiceV1.");
                    break;
                case MainMenuScreen.Crafting:
                    DrawServiceShell(
                        "CRAFTING",
                        state.CraftingConnected,
                        "The crafting runtime is connected. "
                        + "Crafting remains owned by CraftingServiceV1.");
                    break;
                case MainMenuScreen.Settings:
                    DrawSettings();
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unsupported main-menu screen " + state.CurrentScreen + ".");
            }

            GUILayout.EndArea();
        }

        public void ConfigureForTests(
            IMainMenuPlatformActions actions,
            IEnumerable<MenuWeaponOption> weaponOptions)
        {
            platformActions = actions
                ?? throw new ArgumentNullException(nameof(actions));
            state = new MainMenuFlowState(
                weaponOptions ?? throw new ArgumentNullException(nameof(weaponOptions)));
        }

        /// <summary>
        /// Connects existing runtime authorities without manufacturing transactions,
        /// inventory, shop stock, purchases, recipes, crafting results, or rewards.
        /// </summary>
        public void BindRuntimeServices(
            PlayerHoldingsService holdings,
            EquipmentCatalog catalog,
            ShopRuntimeServiceV1 shop,
            CraftingServiceV1 crafting)
        {
            EnsureInitialized();
            holdingsService = holdings;
            equipmentCatalog = catalog;
            shopService = shop;
            craftingService = crafting;

            state.SetRuntimeConnections(
                holdingsService != null && equipmentCatalog != null,
                shopService != null,
                craftingService != null);

            RefreshArmoryFromHoldings();
        }

        public bool RefreshArmoryFromHoldings()
        {
            EnsureInitialized();
            if (holdingsService == null || equipmentCatalog == null)
            {
                return false;
            }

            PlayerHoldingsSnapshotV1 snapshot = holdingsService.ExportSnapshot();
            List<MenuWeaponOption> ownedWeapons = new List<MenuWeaponOption>();
            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshotV1 holding = snapshot.UniqueHoldings[index];
                if (holding == null
                    || holding.RewardKind != RewardGrantKindV1.EquipmentReference
                    || holding.EquipmentInstance == null)
                {
                    continue;
                }

                EquipmentDefinition definition =
                    equipmentCatalog.FindEquipmentDefinition(holding.DefinitionStableId);
                if (definition == null
                    || definition.CategoryId != EquipmentCategoryIds.Weapon)
                {
                    continue;
                }

                ownedWeapons.Add(new MenuWeaponOption(
                    holding.InstanceStableId,
                    holding.DefinitionStableId,
                    definition.DisplayName,
                    holding.EquipmentInstance.ItemLevel));
            }

            if (ownedWeapons.Count == 0)
            {
                return false;
            }

            state.Armory.ReplaceOptions(ownedWeapons);
            return true;
        }

        public bool OpenScreen(MainMenuScreen screen)
        {
            EnsureInitialized();
            return state.OpenScreen(screen);
        }

        public bool NavigateBack()
        {
            EnsureInitialized();
            bool navigated = state.NavigateBack();
            if (state.QuitRequested)
            {
                platformActions.Quit();
            }

            return navigated;
        }

        public void RequestPlay()
        {
            EnsureInitialized();
            if (state.PlayRequested)
            {
                return;
            }

            state.RequestPlay();
            platformActions.LoadPlayScene(
                MainMenuFlowState.PlayScenePath,
                state.Settings.ReducedEffects,
                state.Settings.Grayscale);
        }

        public void RequestQuit()
        {
            EnsureInitialized();
            if (state.QuitRequested)
            {
                return;
            }

            state.RequestQuit();
            platformActions.Quit();
        }

        public bool SelectArmorySlot(int slotIndex)
        {
            EnsureInitialized();
            return state.Armory.SelectSlot(slotIndex);
        }

        public bool CycleArmoryWeapon(int delta)
        {
            EnsureInitialized();
            return state.Armory.CycleActiveSelection(delta);
        }

        public bool SelectArmoryWeapon(StableId instanceStableId)
        {
            EnsureInitialized();
            return state.Armory.TrySelectInstance(
                state.Armory.ActiveSlotIndex,
                instanceStableId);
        }

        public bool SetReducedEffects(bool value)
        {
            EnsureInitialized();
            return state.Settings.SetReducedEffects(value);
        }

        public bool SetGrayscale(bool value)
        {
            EnsureInitialized();
            return state.Settings.SetGrayscale(value);
        }

        private void DrawHeader()
        {
            GUILayout.Label("SHOOTER MOVER", titleStyle);
            GUILayout.Label("MAIN MENU", headingStyle);
        }

        private void DrawTitleScreen()
        {
            GUILayout.FlexibleSpace();
            DrawMenuButton("PLAY", RequestPlay);
            DrawMenuButton(
                "ARMORY / LOADOUT",
                delegate { OpenScreen(MainMenuScreen.Armory); });
            DrawMenuButton("SHOP", delegate { OpenScreen(MainMenuScreen.Shop); });
            DrawMenuButton(
                "CRAFTING",
                delegate { OpenScreen(MainMenuScreen.Crafting); });
            DrawMenuButton(
                "SETTINGS",
                delegate { OpenScreen(MainMenuScreen.Settings); });
            DrawMenuButton("QUIT", RequestQuit);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "Escape / Backspace / controller East: back or quit",
                bodyStyle);
        }

        private void DrawArmory()
        {
            GUILayout.Label("ARMORY / FOUR-SLOT LOADOUT", headingStyle);
            GUILayout.Label(
                state.HoldingsConnected
                    ? "Bound to the existing holdings authority. "
                        + "Each owned equipment instance remains selectable."
                    : "Session reference roster. A holdings bootstrap may replace "
                        + "this list without changing menu selection rules.",
                bodyStyle);
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            for (int slot = 0; slot < ArmoryLoadoutState.SlotCount; slot++)
            {
                MenuWeaponOption selected = state.Armory.GetSelectedWeapon(slot);
                string label = "SLOT " + (slot + 1) + "\n" + selected.DisplayName;
                GUIStyle style = state.Armory.ActiveSlotIndex == slot
                    ? selectedSlotStyle
                    : GUI.skin.button;
                if (GUILayout.Button(label, style, GUILayout.Height(70f)))
                {
                    SelectArmorySlot(slot);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("PREVIOUS", GUILayout.Height(38f)))
            {
                CycleArmoryWeapon(-1);
            }

            GUILayout.Label(
                "Editing slot " + (state.Armory.ActiveSlotIndex + 1),
                headingStyle,
                GUILayout.ExpandWidth(true));
            if (GUILayout.Button("NEXT", GUILayout.Height(38f)))
            {
                CycleArmoryWeapon(1);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label(
                "Weapon instances (duplicate definitions are intentionally retained):",
                bodyStyle);
            for (int index = 0; index < state.Armory.Options.Count; index++)
            {
                MenuWeaponOption option = state.Armory.Options[index];
                bool selected =
                    state.Armory.GetSelectedWeapon(state.Armory.ActiveSlotIndex)
                        .InstanceStableId == option.InstanceStableId;
                string prefix = selected ? "●  " : "○  ";
                if (GUILayout.Button(
                    prefix + option.ToDisplayString(),
                    GUILayout.Height(34f)))
                {
                    SelectArmoryWeapon(option.InstanceStableId);
                }
            }

            GUILayout.FlexibleSpace();
            DrawBackButton();
        }

        private void DrawSettings()
        {
            GUILayout.Label("SETTINGS", headingStyle);
            GUILayout.Label(
                "These values are passed to the current Stage 1 presentation "
                + "setters when Play loads. No replacement gameplay authority is created.",
                bodyStyle);
            GUILayout.Space(18f);

            bool reduced = GUILayout.Toggle(
                state.Settings.ReducedEffects,
                "Reduced effects",
                GUILayout.Height(42f));
            SetReducedEffects(reduced);

            bool grayscale = GUILayout.Toggle(
                state.Settings.Grayscale,
                "Grayscale",
                GUILayout.Height(42f));
            SetGrayscale(grayscale);

            GUILayout.Space(12f);
            GUILayout.Label(
                "Current launch options: reduced effects "
                + (state.Settings.ReducedEffects ? "ON" : "OFF")
                + " / grayscale "
                + (state.Settings.Grayscale ? "ON" : "OFF"),
                bodyStyle);

            GUILayout.FlexibleSpace();
            DrawBackButton();
        }

        private void DrawServiceShell(
            string title,
            bool connected,
            string connectedMessage)
        {
            GUILayout.Label(title, headingStyle);
            GUILayout.Space(14f);
            GUILayout.Label(
                connected
                    ? "RUNTIME SERVICE CONNECTED"
                    : "RUNTIME SERVICE NOT YET BOUND",
                headingStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                connected
                    ? connectedMessage
                    : "This screen is navigation-only until the composition root "
                        + "supplies the existing runtime service. No placeholder "
                        + "purchase, currency, item, recipe, or reward is generated.",
                bodyStyle);
            GUILayout.FlexibleSpace();
            DrawBackButton();
        }

        private void DrawBackButton()
        {
            if (GUILayout.Button("BACK", GUILayout.Height(42f)))
            {
                NavigateBack();
            }
        }

        private static void DrawMenuButton(string label, Action action)
        {
            if (GUILayout.Button(label, GUILayout.Height(48f)))
            {
                action();
            }

            GUILayout.Space(6f);
        }

        private void EnsureInitialized()
        {
            if (state == null)
            {
                state = new MainMenuFlowState(CreateReferenceWeaponOptions());
            }

            if (platformActions == null)
            {
                platformActions = new UnityMainMenuPlatformActions();
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
            selectedSlotStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
            };
        }

        private static IEnumerable<MenuWeaponOption> CreateReferenceWeaponOptions()
        {
            StableId blasterDefinition = StableId.Parse("menuref.blaster");
            return new[]
            {
                new MenuWeaponOption(
                    StableId.Parse("menuitem.blaster-a"),
                    blasterDefinition,
                    "Blaster A"),
                new MenuWeaponOption(
                    StableId.Parse("menuitem.blaster-b"),
                    blasterDefinition,
                    "Blaster B"),
                new MenuWeaponOption(
                    StableId.Parse("menuitem.shotgun"),
                    StableId.Parse("menuref.shotgun"),
                    "Shotgun"),
                new MenuWeaponOption(
                    StableId.Parse("menuitem.rocket"),
                    StableId.Parse("menuref.rocket"),
                    "Rocket Launcher"),
                new MenuWeaponOption(
                    StableId.Parse("menuitem.arc"),
                    StableId.Parse("menuref.arc"),
                    "Arc Gun"),
            };
        }
    }
}

using System;
using ShooterMover.Application.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.MainMenu
{
    public enum MenuArtworkScreen
    {
        Title = 1,
        LevelSelection = 2,
        Skills = 3,
        Inventory = 4,
        Shop = 5,
        Crafting = 6,
        Settings = 7,
        Results = 8,
    }

    /// <summary>
    /// Presentation-only 16:9 artwork shell. The supplied images are passive
    /// backplates; all actions use code-owned overlay hit regions. This component
    /// projects the existing MENU-001 state and routes scenes without owning wallet,
    /// inventory, XP, skill, or reward truth.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MainMenuController))]
    public sealed class MainMenuArtworkController : MonoBehaviour
    {
        private const float DesignWidth = 640f;
        private const float DesignHeight = 360f;
        private const float PanelWidth = 820f;
        private const float PanelHeight = 680f;
        private const int SkillNodeCount = 20;

        [SerializeField] private MainMenuController menuBackend;
        [Header("16:9 presentation backplates (base64 JPEG TextAssets)")]
        [SerializeField] private TextAsset mainMenuBackgroundAsset;
        [SerializeField] private TextAsset levelSelectBackgroundAsset;
        [SerializeField] private TextAsset skillsBackgroundAsset;
        [SerializeField] private TextAsset resultsBackgroundAsset;

        private readonly bool[] highlightedSkillNodes = new bool[SkillNodeCount];
        private Texture2D mainMenuBackground;
        private Texture2D levelSelectBackground;
        private Texture2D skillsBackground;
        private Texture2D resultsBackground;
        private GUIStyle hitRegionStyle;
        private GUIStyle overlayButtonStyle;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle selectedSlotStyle;
        private GUIStyle unavailableStyle;
        private int selectedLevelCardIndex = -1;

        public MenuArtworkScreen CurrentScreen { get; private set; } = MenuArtworkScreen.Title;
        public int SelectedLevelCardIndex { get { return selectedLevelCardIndex; } }

        public int BoundBackgroundCount
        {
            get
            {
                int count = 0;
                if (mainMenuBackgroundAsset != null) { count++; }
                if (levelSelectBackgroundAsset != null) { count++; }
                if (skillsBackgroundAsset != null) { count++; }
                if (resultsBackgroundAsset != null) { count++; }
                return count;
            }
        }

        private void Awake()
        {
            EnsureBackend();
            menuBackend.enabled = false;
            EnsureBackgroundTextures();
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
            EnsureBackend();
            EnsureBackgroundTextures();
            EnsureStyles();

            switch (CurrentScreen)
            {
                case MenuArtworkScreen.Title:
                    DrawTitleScreen();
                    break;
                case MenuArtworkScreen.LevelSelection:
                    DrawLevelSelectionScreen();
                    break;
                case MenuArtworkScreen.Skills:
                    DrawSkillsScreen();
                    break;
                case MenuArtworkScreen.Results:
                    DrawResultsScreen();
                    break;
                case MenuArtworkScreen.Inventory:
                    DrawPanel(DrawInventoryScreen);
                    break;
                case MenuArtworkScreen.Shop:
                    DrawPanel(delegate
                    {
                        DrawServiceShell(
                            "SHOP",
                            menuBackend.State.ShopConnected,
                            "The existing ShopRuntimeServiceV1 is connected. "
                            + "No purchase action is exposed by this shell.");
                    });
                    break;
                case MenuArtworkScreen.Crafting:
                    DrawPanel(delegate
                    {
                        DrawServiceShell(
                            "CRAFTING",
                            menuBackend.State.CraftingConnected,
                            "The existing CraftingServiceV1 is connected. "
                            + "No crafting action is exposed by this shell.");
                    });
                    break;
                case MenuArtworkScreen.Settings:
                    DrawPanel(DrawSettingsScreen);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unsupported artwork screen " + CurrentScreen + ".");
            }
        }

        private void OnDestroy()
        {
            DestroyTexture(mainMenuBackground);
            DestroyTexture(levelSelectBackground);
            DestroyTexture(skillsBackground);
            DestroyTexture(resultsBackground);
        }

        public void ConfigureForTests(MainMenuController backend)
        {
            menuBackend = backend ?? throw new ArgumentNullException(nameof(backend));
            menuBackend.enabled = false;
        }

        public void ConfigureBackgroundsForTests(
            TextAsset main,
            TextAsset levels,
            TextAsset skills,
            TextAsset results)
        {
            mainMenuBackgroundAsset = main;
            levelSelectBackgroundAsset = levels;
            skillsBackgroundAsset = skills;
            resultsBackgroundAsset = results;
            DestroyTexture(mainMenuBackground);
            DestroyTexture(levelSelectBackground);
            DestroyTexture(skillsBackground);
            DestroyTexture(resultsBackground);
            mainMenuBackground = null;
            levelSelectBackground = null;
            skillsBackground = null;
            resultsBackground = null;
        }

        public bool OpenScreen(MenuArtworkScreen screen)
        {
            if (!Enum.IsDefined(typeof(MenuArtworkScreen), screen))
            {
                throw new ArgumentOutOfRangeException(nameof(screen));
            }

            if (CurrentScreen == screen)
            {
                return false;
            }

            CurrentScreen = screen;
            return true;
        }

        public bool NavigateBack()
        {
            EnsureBackend();
            if (CurrentScreen == MenuArtworkScreen.Title)
            {
                menuBackend.RequestQuit();
                return false;
            }

            CurrentScreen = MenuArtworkScreen.Title;
            return true;
        }

        public bool ActivateLevelCard(int levelCardIndex)
        {
            if (levelCardIndex < 0 || levelCardIndex > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(levelCardIndex));
            }

            bool changed = selectedLevelCardIndex != levelCardIndex;
            selectedLevelCardIndex = levelCardIndex;
            if (levelCardIndex == 0)
            {
                EnsureBackend();
                menuBackend.RequestPlay();
            }

            return changed;
        }

        public bool ActivateSkillNode(int nodeIndex)
        {
            ValidateSkillNode(nodeIndex);
            highlightedSkillNodes[nodeIndex] = !highlightedSkillNodes[nodeIndex];
            return highlightedSkillNodes[nodeIndex];
        }

        public bool IsSkillNodeHighlighted(int nodeIndex)
        {
            ValidateSkillNode(nodeIndex);
            return highlightedSkillNodes[nodeIndex];
        }

        public bool ResetSkillPreview()
        {
            bool changed = false;
            for (int index = 0; index < highlightedSkillNodes.Length; index++)
            {
                changed |= highlightedSkillNodes[index];
                highlightedSkillNodes[index] = false;
            }

            return changed;
        }

        private void DrawTitleScreen()
        {
            Rect canvas = DrawBackplate(mainMenuBackground, "MAIN MENU");
            DrawHitRegion(canvas, new Rect(190f, 95f, 120f, 70f), delegate
            {
                OpenScreen(MenuArtworkScreen.LevelSelection);
            });
            DrawHitRegion(canvas, new Rect(330f, 95f, 120f, 70f), delegate
            {
                OpenScreen(MenuArtworkScreen.Skills);
            });
            DrawHitRegion(canvas, new Rect(190f, 180f, 120f, 70f), delegate
            {
                OpenScreen(MenuArtworkScreen.Inventory);
            });
            DrawHitRegion(canvas, new Rect(330f, 180f, 120f, 70f), delegate
            {
                OpenScreen(MenuArtworkScreen.Shop);
            });
            DrawHitRegion(canvas, new Rect(584f, 314f, 42f, 38f), delegate
            {
                OpenScreen(MenuArtworkScreen.Settings);
            });
            DrawOverlayButton(
                canvas,
                new Rect(475f, 314f, 95f, 38f),
                "CRAFTING",
                delegate { OpenScreen(MenuArtworkScreen.Crafting); });
            DrawOverlayButton(
                canvas,
                new Rect(10f, 314f, 70f, 38f),
                "QUIT",
                delegate { menuBackend.RequestQuit(); });
        }

        private void DrawLevelSelectionScreen()
        {
            Rect canvas = DrawBackplate(levelSelectBackground, "LEVEL SELECTION");
            DrawHitRegion(canvas, new Rect(12f, 15f, 66f, 33f), delegate
            {
                NavigateBack();
            });
            DrawHitRegion(canvas, new Rect(70f, 100f, 225f, 145f), delegate
            {
                ActivateLevelCard(0);
            });
            DrawHitRegion(canvas, new Rect(345f, 100f, 225f, 145f), delegate
            {
                ActivateLevelCard(1);
            });

            if (selectedLevelCardIndex == 1)
            {
                GUI.Label(
                    ToScreenRect(canvas, new Rect(210f, 276f, 220f, 38f)),
                    "LEVEL 2 ROUTE SHELL — NO SCENE BOUND",
                    unavailableStyle);
            }
        }

        private void DrawSkillsScreen()
        {
            Rect canvas = DrawBackplate(skillsBackground, "SKILLS");
            DrawHitRegion(canvas, new Rect(10f, 10f, 45f, 35f), delegate
            {
                NavigateBack();
            });

            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    int nodeIndex = row * 5 + column;
                    Rect designRect = new Rect(
                        175f + column * 84f,
                        62f + row * 58f,
                        70f,
                        46f);
                    DrawHitRegion(canvas, designRect, delegate
                    {
                        ActivateSkillNode(nodeIndex);
                    });
                    if (highlightedSkillNodes[nodeIndex])
                    {
                        DrawSelectionOutline(ToScreenRect(canvas, designRect));
                    }
                }
            }

            DrawHitRegion(canvas, new Rect(20f, 315f, 105f, 32f), delegate
            {
                ResetSkillPreview();
            });
            DrawHitRegion(canvas, new Rect(430f, 315f, 195f, 32f), delegate
            {
                OpenScreen(MenuArtworkScreen.Title);
            });
        }

        private void DrawResultsScreen()
        {
            Rect canvas = DrawBackplate(resultsBackground, "RESULTS");
            DrawHitRegion(canvas, new Rect(15f, 318f, 160f, 32f), delegate
            {
                OpenScreen(MenuArtworkScreen.Title);
            });
        }

        private void DrawInventoryScreen()
        {
            ArmoryLoadoutState armory = menuBackend.State.Armory;
            GUILayout.Label("INVENTORY / FOUR-SLOT LOADOUT", headingStyle);
            GUILayout.Label(
                menuBackend.State.HoldingsConnected
                    ? "Projected from the existing holdings authority. "
                        + "Concrete equipment instances remain independently selectable."
                    : "Session reference roster. No inventory mutation is performed.",
                bodyStyle);
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            for (int slot = 0; slot < ArmoryLoadoutState.SlotCount; slot++)
            {
                MenuWeaponOption selected = armory.GetSelectedWeapon(slot);
                GUIStyle style = armory.ActiveSlotIndex == slot
                    ? selectedSlotStyle
                    : GUI.skin.button;
                if (GUILayout.Button(
                    "SLOT " + (slot + 1) + "\n" + selected.DisplayName,
                    style,
                    GUILayout.Height(70f)))
                {
                    menuBackend.SelectArmorySlot(slot);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("PREVIOUS", GUILayout.Height(38f)))
            {
                menuBackend.CycleArmoryWeapon(-1);
            }
            GUILayout.Label(
                "Editing slot " + (armory.ActiveSlotIndex + 1),
                headingStyle,
                GUILayout.ExpandWidth(true));
            if (GUILayout.Button("NEXT", GUILayout.Height(38f)))
            {
                menuBackend.CycleArmoryWeapon(1);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label(
                "Weapon instances — duplicate definitions are retained:",
                bodyStyle);
            for (int index = 0; index < armory.Options.Count; index++)
            {
                MenuWeaponOption option = armory.Options[index];
                bool selected = armory.GetSelectedWeapon(armory.ActiveSlotIndex)
                    .InstanceStableId == option.InstanceStableId;
                if (GUILayout.Button(
                    (selected ? "●  " : "○  ") + option.ToDisplayString(),
                    GUILayout.Height(34f)))
                {
                    menuBackend.SelectArmoryWeapon(option.InstanceStableId);
                }
            }

            GUILayout.FlexibleSpace();
            DrawPanelBackButton();
        }

        private void DrawSettingsScreen()
        {
            GUILayout.Label("SETTINGS", headingStyle);
            GUILayout.Label(
                "These values are projected into the current Stage 1 presentation "
                + "setters when Level 1 loads.",
                bodyStyle);
            GUILayout.Space(18f);

            bool reduced = GUILayout.Toggle(
                menuBackend.State.Settings.ReducedEffects,
                "Reduced effects",
                GUILayout.Height(42f));
            menuBackend.SetReducedEffects(reduced);

            bool grayscale = GUILayout.Toggle(
                menuBackend.State.Settings.Grayscale,
                "Grayscale",
                GUILayout.Height(42f));
            menuBackend.SetGrayscale(grayscale);

            GUILayout.FlexibleSpace();
            DrawPanelBackButton();
        }

        private void DrawServiceShell(
            string title,
            bool connected,
            string connectedMessage)
        {
            GUILayout.Label(title, headingStyle);
            GUILayout.Space(14f);
            GUILayout.Label(
                connected ? "RUNTIME SERVICE CONNECTED" : "RUNTIME SERVICE NOT BOUND",
                headingStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                connected
                    ? connectedMessage
                    : "Navigation shell only. No wallet, inventory, XP, skill, "
                        + "purchase, craft, or reward mutation is generated.",
                bodyStyle);
            GUILayout.FlexibleSpace();
            DrawPanelBackButton();
        }

        private void DrawPanel(Action drawContent)
        {
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.blackTexture,
                ScaleMode.StretchToFill);
            float width = Mathf.Min(PanelWidth, Mathf.Max(420f, Screen.width - 40f));
            float height = Mathf.Min(PanelHeight, Mathf.Max(360f, Screen.height - 40f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUILayout.BeginArea(panel, GUI.skin.window);
            GUILayout.Label("SHOOTER MOVER", titleStyle);
            GUILayout.Space(12f);
            drawContent();
            GUILayout.EndArea();
        }

        private void DrawPanelBackButton()
        {
            if (GUILayout.Button("BACK", GUILayout.Height(42f)))
            {
                NavigateBack();
            }
        }

        private Rect DrawBackplate(Texture2D texture, string fallbackLabel)
        {
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.blackTexture,
                ScaleMode.StretchToFill);
            Rect canvas = CalculateCanvasRect();
            if (texture != null)
            {
                GUI.DrawTexture(canvas, texture, ScaleMode.StretchToFill, false);
            }
            else
            {
                GUI.Box(canvas, GUIContent.none);
                GUI.Label(canvas, fallbackLabel, titleStyle);
            }

            return canvas;
        }

        private void DrawHitRegion(Rect canvas, Rect designRect, Action action)
        {
            if (GUI.Button(ToScreenRect(canvas, designRect), GUIContent.none, hitRegionStyle))
            {
                action();
            }
        }

        private void DrawOverlayButton(
            Rect canvas,
            Rect designRect,
            string label,
            Action action)
        {
            if (GUI.Button(ToScreenRect(canvas, designRect), label, overlayButtonStyle))
            {
                action();
            }
        }

        private static Rect CalculateCanvasRect()
        {
            float scale = Mathf.Min(
                Screen.width / DesignWidth,
                Screen.height / DesignHeight);
            float width = DesignWidth * scale;
            float height = DesignHeight * scale;
            return new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
        }

        private static Rect ToScreenRect(Rect canvas, Rect designRect)
        {
            float scaleX = canvas.width / DesignWidth;
            float scaleY = canvas.height / DesignHeight;
            return new Rect(
                canvas.x + designRect.x * scaleX,
                canvas.y + designRect.y * scaleY,
                designRect.width * scaleX,
                designRect.height * scaleY);
        }

        private static void DrawSelectionOutline(Rect rect)
        {
            Color prior = GUI.color;
            GUI.color = new Color(1f, 0.35f, 0.2f, 0.95f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = prior;
        }

        private void EnsureBackend()
        {
            if (menuBackend == null)
            {
                menuBackend = GetComponent<MainMenuController>();
            }

            if (menuBackend == null)
            {
                throw new InvalidOperationException(
                    "MainMenuArtworkController requires a MainMenuController backend.");
            }
        }

        private void EnsureBackgroundTextures()
        {
            if (mainMenuBackground == null)
            {
                mainMenuBackground = DecodeBackground(
                    mainMenuBackgroundAsset,
                    "MENU-001 Main Menu Backplate");
            }
            if (levelSelectBackground == null)
            {
                levelSelectBackground = DecodeBackground(
                    levelSelectBackgroundAsset,
                    "MENU-001 Level Selection Backplate");
            }
            if (skillsBackground == null)
            {
                skillsBackground = DecodeBackground(
                    skillsBackgroundAsset,
                    "MENU-001 Skills Backplate");
            }
            if (resultsBackground == null)
            {
                resultsBackground = DecodeBackground(
                    resultsBackgroundAsset,
                    "MENU-001 Results Backplate");
            }
        }

        private static Texture2D DecodeBackground(TextAsset asset, string textureName)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return null;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(asset.text.Trim());
            }
            catch (FormatException)
            {
                Debug.LogError(textureName + " is not valid base64 image data.");
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = textureName,
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            if (!UnityEngine.ImageConversion.LoadImage(texture, bytes, true))
            {
                DestroyTexture(texture);
                Debug.LogError(textureName + " could not be decoded as an image.");
                return null;
            }

            return texture;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            hitRegionStyle = new GUIStyle();
            overlayButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
            };
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
            unavailableStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
        }

        private static void ValidateSkillNode(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= SkillNodeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeIndex));
            }
        }
    }
}

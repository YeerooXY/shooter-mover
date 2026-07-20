using System;
using System.Collections.Generic;
using ShooterMover.Application.Characters.Selection;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Content.Definitions.Characters.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Characters.Selection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.ProductionFlow
{
    public enum ProductionCharacterSelectionStageV1
    {
        CharacterSlots = 1,
        CharacterCreation = 2,
    }

    /// <summary>
    /// Canonical character slot/creation presentation. Character and class identities
    /// are selected exclusively through CharacterSelectionServiceV1 and its existing
    /// catalog. The only additional persisted field is the user-facing display name.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionCharacterSelectionControllerV1 :
        MonoBehaviour
    {
        private const int ProfileSlotCount = 6;
        private readonly Dictionary<string, Texture2D> textures =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        private CharacterSelectionServiceV1 selection;
        private IReadOnlyList<ProductionFlowProfileRecordV1> profiles;
        private Func<int, PlayerRouteProfilePayloadV1, bool> selectExisting;
        private Func<int, string, CharacterSelectionRouteResultV1, bool>
            createCharacter;
        private Func<bool> navigateBack;
        private bool classExplicitlySelected;
        private bool terminal;
        private string characterName = string.Empty;
        private string validationMessage = string.Empty;
        private int selectedSlotIndex;
        private GUIStyle titleStyle;
        private GUIStyle cardStyle;
        private GUIStyle selectedStyle;
        private GUIStyle bodyStyle;
        private GUIStyle actionStyle;

        public ProductionCharacterSelectionStageV1 Stage { get; private set; }

        public string CharacterName { get { return characterName; } }

        public bool ClassExplicitlySelected
        {
            get { return classExplicitlySelected; }
        }

        public bool IsTerminal { get { return terminal; } }

        public int SelectedSlotIndex { get { return selectedSlotIndex; } }

        public CharacterSelectionServiceV1 Selection
        {
            get { return selection; }
        }

        public void Configure(
            PlayerRouteProfilePayloadV1 incomingPayload,
            ProductionFlowProfileRecordV1 existingProfile,
            Func<PlayerRouteProfilePayloadV1, bool> selectExisting,
            Func<string, CharacterSelectionRouteResultV1, bool>
                createCharacter,
            Func<bool> navigateBack)
        {
            Configure(
                incomingPayload,
                new ProductionFlowProfileRecordV1[] { existingProfile },
                (slot, payload) => slot == 0 && selectExisting(payload),
                (slot, name, result) =>
                    slot == 0 && createCharacter(name, result),
                navigateBack);
        }

        public void Configure(
            PlayerRouteProfilePayloadV1 incomingPayload,
            IReadOnlyList<ProductionFlowProfileRecordV1> profiles,
            Func<int, PlayerRouteProfilePayloadV1, bool> selectExisting,
            Func<int, string, CharacterSelectionRouteResultV1, bool>
                createCharacter,
            Func<bool> navigateBack)
        {
            CharacterSelectionCatalogV1 catalog =
                BuiltInCharacterSelectionCatalogV1.Create();
            selection = new CharacterSelectionServiceV1(
                catalog,
                incomingPayload
                    ?? throw new ArgumentNullException(nameof(incomingPayload)));
            this.profiles = profiles
                ?? throw new ArgumentNullException(nameof(profiles));
            this.selectExisting = selectExisting
                ?? throw new ArgumentNullException(nameof(selectExisting));
            this.createCharacter = createCharacter
                ?? throw new ArgumentNullException(nameof(createCharacter));
            this.navigateBack = navigateBack
                ?? throw new ArgumentNullException(nameof(navigateBack));
            selectedSlotIndex = 0;
            Stage = HasAnyProfile()
                ? ProductionCharacterSelectionStageV1.CharacterSlots
                : ProductionCharacterSelectionStageV1.CharacterCreation;
            classExplicitlySelected = false;
            terminal = false;
            characterName = string.Empty;
            validationMessage = string.Empty;
        }

        private void Update()
        {
            if (terminal) return;
            bool back = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            back |= Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (back) Back();
        }

        private void OnGUI()
        {
            if (selection == null) return;
            EnsureStyles();
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawBackdrop(screen);

            float width = Mathf.Min(1280f, Mathf.Max(420f, Screen.width - 32f));
            float height = Mathf.Min(760f, Mathf.Max(360f, Screen.height - 32f));
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(panel);
            if (Stage == ProductionCharacterSelectionStageV1.CharacterSlots)
            {
                DrawSlots();
            }
            else
            {
                DrawCreation();
            }
            GUILayout.EndArea();
        }

        public bool ChooseExisting()
        {
            ProductionFlowProfileRecordV1 selected = SelectedProfile;
            if (terminal || selected == null) return false;
            if (!selectExisting(selectedSlotIndex, selected.Payload)) return false;
            terminal = true;
            return true;
        }

        public bool ChooseEmptySlot()
        {
            if (terminal) return false;
            if (SelectedProfile != null) return false;
            Stage = ProductionCharacterSelectionStageV1.CharacterCreation;
            characterName = string.Empty;
            classExplicitlySelected = false;
            validationMessage = string.Empty;
            return true;
        }

        public void SetCharacterName(string value)
        {
            characterName = value ?? string.Empty;
            validationMessage = string.Empty;
        }

        public bool SelectClassByIndex(int index)
        {
            if (terminal || selection == null) return false;
            IReadOnlyList<CharacterClassProfileDefinitionV1> profiles =
                selection.Catalog.GetProfiles(
                    selection.HighlightedCharacterStableId);
            if (index < 0 || index >= profiles.Count) return false;
            CharacterSelectionOperationResultV1 result =
                selection.TryHighlightProfile(
                    profiles[index].LoadoutProfileStableId);
            if (result.Status == CharacterSelectionOperationStatusV1.Rejected)
            {
                validationMessage = result.RejectionCode;
                return false;
            }

            classExplicitlySelected = true;
            validationMessage = string.Empty;
            return true;
        }

        public bool ConfirmCreation()
        {
            if (terminal) return false;
            if (string.IsNullOrWhiteSpace(characterName))
            {
                validationMessage = "A character name is required.";
                return false;
            }

            if (!classExplicitlySelected)
            {
                validationMessage = "Choose one class before confirming.";
                return false;
            }

            CharacterSelectionRouteResultV1 result = selection.Confirm();
            if (!createCharacter(
                selectedSlotIndex,
                characterName.Trim(),
                result))
            {
                validationMessage = "The route transition is busy.";
                return false;
            }

            terminal = true;
            return true;
        }

        public bool Back()
        {
            if (terminal) return false;
            if (Stage == ProductionCharacterSelectionStageV1.CharacterCreation
                && profiles != null)
            {
                Stage = ProductionCharacterSelectionStageV1.CharacterSlots;
                characterName = string.Empty;
                classExplicitlySelected = false;
                validationMessage = string.Empty;
                return true;
            }

            if (!navigateBack()) return false;
            terminal = true;
            return true;
        }

        private void DrawSlots()
        {
            GUILayout.Label("CHARACTER SELECTION", titleStyle);
            GUILayout.Space(18f);
            for (int index = 0; index < ProfileSlotCount; index++)
            {
                if (index % 3 == 0) GUILayout.BeginHorizontal();
                selectedSlotIndex = index;
                ProductionFlowProfileRecordV1 slot = SelectedProfile;
                GUILayout.BeginVertical(cardStyle, GUILayout.ExpandWidth(true));
                GUILayout.Label("SLOT " + (index + 1), titleStyle);
                if (slot == null)
                {
                    GUILayout.Label("EMPTY SLOT", bodyStyle);
                    GUILayout.Label("Create a named character and choose one class.", bodyStyle);
                    if (GUILayout.Button("CREATE CHARACTER", actionStyle, GUILayout.Height(54f)))
                    {
                        ChooseEmptySlot();
                    }
                }
                else
                {
                    GUILayout.Label(slot.DisplayName, titleStyle);
                    GUILayout.Label(slot.Payload.SelectedCharacterStableId + "\n" + slot.Payload.LoadoutProfileStableId, bodyStyle);
                    if (GUILayout.Button("PLAY THIS CHARACTER", actionStyle, GUILayout.Height(54f)))
                    {
                        ChooseExisting();
                    }
                }
                GUILayout.EndVertical();
                if (index % 3 == 2) GUILayout.EndHorizontal();
            }
            GUILayout.Space(16f);
            if (GUILayout.Button("BACK", actionStyle, GUILayout.Height(44f)))
            {
                Back();
            }
        }

        private ProductionFlowProfileRecordV1 SelectedProfile
        {
            get
            {
                return profiles != null
                    && selectedSlotIndex >= 0
                    && selectedSlotIndex < profiles.Count
                    ? profiles[selectedSlotIndex]
                    : null;
            }
        }

        private bool HasAnyProfile()
        {
            if (profiles == null) return false;
            for (int index = 0; index < profiles.Count; index++)
            {
                if (profiles[index] != null) return true;
            }

            return false;
        }

        private void DrawCreation()
        {
            GUILayout.Label("CHARACTER CREATION", titleStyle);
            GUILayout.Label("NAME", bodyStyle);
            characterName = GUILayout.TextField(
                characterName,
                32,
                GUILayout.Height(42f));
            GUILayout.Space(14f);
            GUILayout.Label("CHOOSE ONE CLASS", titleStyle);

            IReadOnlyList<CharacterClassProfileDefinitionV1> profiles =
                selection.Catalog.GetProfiles(
                    selection.HighlightedCharacterStableId);
            GUILayout.BeginHorizontal();
            for (int index = 0; index < profiles.Count; index++)
            {
                DrawClassCard(profiles[index], index);
                if (index + 1 < profiles.Count) GUILayout.Space(10f);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);

            if (!string.IsNullOrEmpty(validationMessage))
            {
                GUILayout.Label(validationMessage, bodyStyle);
            }

            GUI.enabled = !terminal;
            if (GUILayout.Button(
                "CONFIRM CHARACTER",
                actionStyle,
                GUILayout.Height(52f)))
            {
                ConfirmCreation();
            }
            if (GUILayout.Button("BACK", actionStyle, GUILayout.Height(42f)))
            {
                Back();
            }
            GUI.enabled = true;
        }

        private void DrawClassCard(
            CharacterClassProfileDefinitionV1 profile,
            int index)
        {
            bool selected = classExplicitlySelected
                && profile.LoadoutProfileStableId
                    == selection.HighlightedLoadoutProfileStableId;
            GUILayout.BeginVertical(
                selected ? selectedStyle : cardStyle,
                GUILayout.ExpandWidth(true));
            Texture2D texture = GetTexture(
                profile.VisualMetadata.PortraitResourceKey);
            Rect image = GUILayoutUtility.GetRect(
                180f,
                230f,
                GUILayout.ExpandWidth(true));
            if (texture != null)
            {
                GUI.DrawTexture(image, texture, ScaleMode.ScaleAndCrop, false);
            }
            else
            {
                GUI.Box(image, GUIContent.none);
            }

            GUILayout.Label(profile.DisplayName, titleStyle);
            GUILayout.Label(profile.Description, bodyStyle);
            if (GUILayout.Button(
                selected ? "SELECTED" : "SELECT CLASS",
                actionStyle,
                GUILayout.Height(42f)))
            {
                SelectClassByIndex(index);
            }
            GUILayout.EndVertical();
        }

        private void DrawBackdrop(Rect screen)
        {
            string key = Stage
                    == ProductionCharacterSelectionStageV1.CharacterSlots
                ? "CharacterSelect/character_choice_screen"
                : "CharacterSelect/character_creation_choice_screen";
            Texture2D texture = GetTexture(key);
            if (texture != null)
            {
                GUI.DrawTexture(
                    screen,
                    texture,
                    ScaleMode.ScaleAndCrop,
                    false);
            }
            else
            {
                GUI.Box(screen, GUIContent.none);
            }
        }

        private Texture2D GetTexture(string resourceKey)
        {
            Texture2D cached;
            if (textures.TryGetValue(resourceKey, out cached)) return cached;

            TextAsset source = Resources.Load<TextAsset>(resourceKey);
            if (source == null)
            {
                textures.Add(resourceKey, null);
                return null;
            }

            Texture2D texture = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false);
            if (!ImageConversion.LoadImage(texture, source.bytes, false))
            {
                Destroy(texture);
                texture = null;
            }

            textures.Add(resourceKey, texture);
            return texture;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            cardStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 12, 12),
            };
            selectedStyle = new GUIStyle(cardStyle)
            {
                border = new RectOffset(5, 5, 5, 5),
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                wordWrap = true,
            };
            actionStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
            };
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, Texture2D> pair in textures)
            {
                if (pair.Value != null) Destroy(pair.Value);
            }
            textures.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using ShooterMover.Application.Characters.Selection;
using ShooterMover.Content.Definitions.Characters.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Characters.Selection;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.CharacterSelect
{
    public enum CharacterSelectStage
    {
        CharacterChoice = 1,
        ClassChoice = 2,
    }

    public sealed class CharacterSelectionRecordingRouteSink :
        ICharacterSelectionRouteSink
    {
        public CharacterSelectionRouteResult LastResult { get; private set; }

        public int AcceptCount { get; private set; }

        public void Accept(CharacterSelectionRouteResult result)
        {
            LastResult = result ?? throw new ArgumentNullException(nameof(result));
            AcceptCount++;
        }
    }

    /// <summary>
    /// Responsive artwork-backed projection with real code-owned hit regions. It owns only
    /// local highlight/stage state and emits immutable route results through an injected sink.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class CharacterSelectController : MonoBehaviour
    {
        private const float MaximumContentWidth = 1280f;
        private const float CardGap = 16f;

        private readonly Dictionary<string, Texture2D> textures =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        private CharacterSelectionActions service;
        private ICharacterSelectionRouteSink routeSink;
        private CharacterSelectionRouteResult lastRouteResult;
        private CharacterSelectStage stage;
        private bool terminalResultDispatched;

        private GUIStyle titleStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle identityStyle;
        private GUIStyle selectedBoxStyle;
        private GUIStyle normalBoxStyle;
        private GUIStyle actionButtonStyle;

        public CharacterSelectStage CurrentStage
        {
            get
            {
                EnsureInitialized();
                return stage;
            }
        }

        public CharacterSelectionActions Service
        {
            get
            {
                EnsureInitialized();
                return service;
            }
        }

        public CharacterSelectionRouteResult LastRouteResult
        {
            get { return lastRouteResult; }
        }

        public bool TerminalResultDispatched
        {
            get { return terminalResultDispatched; }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            EnsureInitialized();

            bool left = Keyboard.current != null
                && Keyboard.current.leftArrowKey.wasPressedThisFrame;
            bool right = Keyboard.current != null
                && Keyboard.current.rightArrowKey.wasPressedThisFrame;
            bool accept = Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.spaceKey.wasPressedThisFrame);
            bool back = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);

            if (Gamepad.current != null)
            {
                left |= Gamepad.current.dpad.left.wasPressedThisFrame;
                right |= Gamepad.current.dpad.right.wasPressedThisFrame;
                accept |= Gamepad.current.buttonSouth.wasPressedThisFrame;
                back |= Gamepad.current.buttonEast.wasPressedThisFrame;
            }

            if (left)
            {
                Cycle(-1);
            }

            if (right)
            {
                Cycle(1);
            }

            if (accept)
            {
                if (stage == CharacterSelectStage.CharacterChoice)
                {
                    ContinueToClassChoice();
                }
                else
                {
                    ConfirmSelection();
                }
            }

            if (back)
            {
                NavigateBack();
            }
        }

        private void OnGUI()
        {
            EnsureInitialized();
            EnsureStyles();

            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawBackdrop(screen);
            float width = Mathf.Min(
                MaximumContentWidth,
                Mathf.Max(360f, Screen.width - 32f));
            Rect content = new Rect(
                (Screen.width - width) * 0.5f,
                Mathf.Max(12f, Screen.height * 0.04f),
                width,
                Mathf.Max(300f, Screen.height * 0.92f));

            if (stage == CharacterSelectStage.CharacterChoice)
            {
                DrawCharacterChoice(content);
            }
            else
            {
                DrawClassChoice(content);
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, Texture2D> pair in textures)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }

            textures.Clear();
        }

        public void ConfigureForTests(
            PlayerRouteProfilePayload incomingPayload,
            CharacterSelectionCatalog catalog,
            ICharacterSelectionRouteSink sink)
        {
            service = new CharacterSelectionActions(
                catalog ?? throw new ArgumentNullException(nameof(catalog)),
                incomingPayload
                    ?? throw new ArgumentNullException(nameof(incomingPayload)));
            routeSink = sink ?? throw new ArgumentNullException(nameof(sink));
            stage = CharacterSelectStage.CharacterChoice;
            lastRouteResult = null;
            terminalResultDispatched = false;
        }

        public bool SelectCharacterByIndex(int index)
        {
            EnsureInitialized();
            if (index < 0 || index >= service.Catalog.Characters.Count)
            {
                return false;
            }

            CharacterSelectionOperationResult result =
                service.TryHighlightCharacter(
                    service.Catalog.Characters[index].CharacterStableId);
            return result.Status != CharacterSelectionOperationStatus.Rejected;
        }

        public bool ContinueToClassChoice()
        {
            EnsureInitialized();
            if (terminalResultDispatched)
            {
                return false;
            }

            stage = CharacterSelectStage.ClassChoice;
            return true;
        }

        public bool SelectProfileByIndex(int index)
        {
            EnsureInitialized();
            IReadOnlyList<CharacterClassProfileDefinition> profiles =
                service.Catalog.GetProfiles(
                    service.HighlightedCharacterStableId);
            if (index < 0 || index >= profiles.Count)
            {
                return false;
            }

            CharacterSelectionOperationResult result =
                service.TryHighlightProfile(
                    profiles[index].LoadoutProfileStableId);
            return result.Status != CharacterSelectionOperationStatus.Rejected;
        }

        public bool SelectClass(CharacterClassKind classKind)
        {
            EnsureInitialized();
            IReadOnlyList<CharacterClassProfileDefinition> profiles =
                service.Catalog.GetProfiles(
                    service.HighlightedCharacterStableId);
            for (int index = 0; index < profiles.Count; index++)
            {
                if (profiles[index].ClassKind == classKind)
                {
                    CharacterSelectionOperationResult result =
                        service.TryHighlightProfile(
                            profiles[index].LoadoutProfileStableId);
                    return result.Status
                        != CharacterSelectionOperationStatus.Rejected;
                }
            }

            return false;
        }

        public CharacterSelectionRouteResult ConfirmSelection()
        {
            EnsureInitialized();
            lastRouteResult = service.Confirm();
            DispatchTerminalResult(lastRouteResult);
            return lastRouteResult;
        }

        public bool NavigateBack()
        {
            EnsureInitialized();
            if (stage == CharacterSelectStage.ClassChoice
                && !terminalResultDispatched)
            {
                stage = CharacterSelectStage.CharacterChoice;
                return false;
            }

            ReturnToCaller();
            return true;
        }

        public CharacterSelectionRouteResult ReturnToCaller()
        {
            EnsureInitialized();
            lastRouteResult = service.Back();
            DispatchTerminalResult(lastRouteResult);
            return lastRouteResult;
        }

        private void DispatchTerminalResult(
            CharacterSelectionRouteResult result)
        {
            if (terminalResultDispatched)
            {
                return;
            }

            terminalResultDispatched = true;
            routeSink.Accept(result);
        }

        private void Cycle(int direction)
        {
            if (terminalResultDispatched)
            {
                return;
            }

            if (stage == CharacterSelectStage.CharacterChoice)
            {
                IReadOnlyList<CharacterSelectionDefinition> characters =
                    service.Catalog.Characters;
                int current = IndexOfCharacter(
                    characters,
                    service.HighlightedCharacterStableId);
                SelectCharacterByIndex(
                    Wrap(current + direction, characters.Count));
                return;
            }

            IReadOnlyList<CharacterClassProfileDefinition> profiles =
                service.Catalog.GetProfiles(
                    service.HighlightedCharacterStableId);
            int profileIndex = IndexOfProfile(
                profiles,
                service.HighlightedLoadoutProfileStableId);
            SelectProfileByIndex(Wrap(profileIndex + direction, profiles.Count));
        }

        private void DrawBackdrop(Rect screen)
        {
            string key = stage == CharacterSelectStage.CharacterChoice
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
        }

        private void DrawCharacterChoice(Rect content)
        {
            GUI.Label(
                new Rect(content.x, content.y, content.width, 54f),
                "SELECT CHARACTER",
                titleStyle);

            IReadOnlyList<CharacterSelectionDefinition> characters =
                service.Catalog.Characters;
            float cardsTop = content.y + 66f;
            float footerHeight = 130f;
            float cardsHeight = Mathf.Max(
                180f,
                content.height - 66f - footerHeight);
            float cardWidth = (
                content.width - CardGap * (characters.Count - 1))
                / characters.Count;

            for (int index = 0; index < characters.Count; index++)
            {
                CharacterSelectionDefinition character = characters[index];
                Rect card = new Rect(
                    content.x + index * (cardWidth + CardGap),
                    cardsTop,
                    cardWidth,
                    cardsHeight);
                DrawCharacterCard(
                    card,
                    character,
                    character.CharacterStableId
                        == service.HighlightedCharacterStableId,
                    index);
            }

            Rect continueRect = new Rect(
                content.x + content.width * 0.25f,
                content.yMax - 112f,
                content.width * 0.5f,
                52f);
            if (GUI.Button(
                continueRect,
                "CHOOSE CLASS",
                actionButtonStyle))
            {
                ContinueToClassChoice();
            }

            Rect backRect = new Rect(
                content.x + content.width * 0.38f,
                content.yMax - 52f,
                content.width * 0.24f,
                42f);
            if (GUI.Button(backRect, "BACK", actionButtonStyle))
            {
                ReturnToCaller();
            }
        }

        private void DrawClassChoice(Rect content)
        {
            CharacterSelectionDefinition character;
            service.Catalog.TryGetCharacter(
                service.HighlightedCharacterStableId,
                out character);
            GUI.Label(
                new Rect(content.x, content.y, content.width, 54f),
                "SELECT CLASS — "
                    + (character == null
                        ? service.HighlightedCharacterStableId.ToString()
                        : character.DisplayName.ToUpperInvariant()),
                titleStyle);

            IReadOnlyList<CharacterClassProfileDefinition> profiles =
                service.Catalog.GetProfiles(
                    service.HighlightedCharacterStableId);
            float cardsTop = content.y + 66f;
            float footerHeight = 130f;
            float cardsHeight = Mathf.Max(
                180f,
                content.height - 66f - footerHeight);
            float cardWidth = (
                content.width - CardGap * (profiles.Count - 1))
                / profiles.Count;

            for (int index = 0; index < profiles.Count; index++)
            {
                CharacterClassProfileDefinition profile = profiles[index];
                Rect card = new Rect(
                    content.x + index * (cardWidth + CardGap),
                    cardsTop,
                    cardWidth,
                    cardsHeight);
                DrawProfileCard(
                    card,
                    profile,
                    profile.LoadoutProfileStableId
                        == service.HighlightedLoadoutProfileStableId,
                    index);
            }

            Rect confirmRect = new Rect(
                content.x + content.width * 0.25f,
                content.yMax - 112f,
                content.width * 0.5f,
                52f);
            if (GUI.Button(confirmRect, "CONFIRM", actionButtonStyle))
            {
                ConfirmSelection();
            }

            Rect backRect = new Rect(
                content.x + content.width * 0.38f,
                content.yMax - 52f,
                content.width * 0.24f,
                42f);
            if (GUI.Button(backRect, "BACK", actionButtonStyle))
            {
                NavigateBack();
            }
        }

        private void DrawCharacterCard(
            Rect card,
            CharacterSelectionDefinition character,
            bool selected,
            int index)
        {
            GUI.Box(
                card,
                GUIContent.none,
                selected ? selectedBoxStyle : normalBoxStyle);
            Rect image = new Rect(
                card.x + 8f,
                card.y + 8f,
                card.width - 16f,
                Mathf.Max(80f, card.height * 0.62f));
            Texture2D texture = GetTexture(
                character.VisualMetadata.PortraitResourceKey);
            if (texture != null)
            {
                GUI.DrawTexture(image, texture, ScaleMode.ScaleAndCrop, false);
            }

            Rect button = new Rect(card.x, card.y, card.width, card.height);
            if (GUI.Button(button, GUIContent.none, GUIStyle.none))
            {
                SelectCharacterByIndex(index);
            }

            float textTop = image.yMax + 8f;
            GUI.Label(
                new Rect(card.x + 12f, textTop, card.width - 24f, 32f),
                character.DisplayName,
                cardTitleStyle);
            GUI.Label(
                new Rect(card.x + 12f, textTop + 32f, card.width - 24f, 52f),
                character.Description,
                bodyStyle);
            GUI.Label(
                new Rect(card.x + 12f, card.yMax - 34f, card.width - 24f, 24f),
                character.CharacterStableId.ToString(),
                identityStyle);
        }

        private void DrawProfileCard(
            Rect card,
            CharacterClassProfileDefinition profile,
            bool selected,
            int index)
        {
            GUI.Box(
                card,
                GUIContent.none,
                selected ? selectedBoxStyle : normalBoxStyle);
            Rect image = new Rect(
                card.x + 8f,
                card.y + 8f,
                card.width - 16f,
                Mathf.Max(80f, card.height * 0.62f));
            Texture2D texture = GetTexture(
                profile.VisualMetadata.PortraitResourceKey);
            if (texture != null)
            {
                GUI.DrawTexture(image, texture, ScaleMode.ScaleAndCrop, false);
            }

            Rect button = new Rect(card.x, card.y, card.width, card.height);
            if (GUI.Button(button, GUIContent.none, GUIStyle.none))
            {
                SelectProfileByIndex(index);
            }

            float textTop = image.yMax + 8f;
            GUI.Label(
                new Rect(card.x + 12f, textTop, card.width - 24f, 32f),
                profile.DisplayName,
                cardTitleStyle);
            GUI.Label(
                new Rect(card.x + 12f, textTop + 32f, card.width - 24f, 52f),
                profile.Description,
                bodyStyle);
            GUI.Label(
                new Rect(card.x + 12f, card.yMax - 34f, card.width - 24f, 24f),
                profile.LoadoutProfileStableId.ToString(),
                identityStyle);
        }

        private Texture2D GetTexture(string resourceKey)
        {
            Texture2D cached;
            if (textures.TryGetValue(resourceKey, out cached))
            {
                return cached;
            }

            TextAsset source = Resources.Load<TextAsset>(resourceKey);
            if (source == null)
            {
                textures.Add(resourceKey, null);
                return null;
            }

            var texture = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false);
            texture.name = resourceKey;
            if (!ImageConversion.LoadImage(texture, source.bytes, false))
            {
                Destroy(texture);
                texture = null;
            }

            textures.Add(resourceKey, texture);
            return texture;
        }

        private void EnsureInitialized()
        {
            if (service != null)
            {
                return;
            }

            CharacterSelectionCatalog catalog =
                BuiltInCharacterSelectionCatalog.Create();
            PlayerRouteProfilePayload incomingPayload =
                PlayerRouteProfilePayload.Create(
                    StableId.Parse("character.default-pilot"),
                    StableId.Parse("loadout-profile.hub-session-default"),
                    new List<StableId>
                    {
                        StableId.Parse("equipment-instance.character-select-slot-1"),
                        StableId.Parse("equipment-instance.character-select-slot-2"),
                        StableId.Parse("equipment-instance.character-select-slot-3"),
                        StableId.Parse("equipment-instance.character-select-slot-4"),
                    });
            routeSink = new CharacterSelectionRecordingRouteSink();
            service = new CharacterSelectionActions(catalog, incomingPayload);
            stage = CharacterSelectStage.CharacterChoice;
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
            cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 14,
                wordWrap = true,
            };
            identityStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                wordWrap = true,
            };
            selectedBoxStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(4, 4, 4, 4),
            };
            normalBoxStyle = new GUIStyle(GUI.skin.box);
            actionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };
        }

        private static int IndexOfCharacter(
            IReadOnlyList<CharacterSelectionDefinition> characters,
            StableId characterStableId)
        {
            for (int index = 0; index < characters.Count; index++)
            {
                if (characters[index].CharacterStableId == characterStableId)
                {
                    return index;
                }
            }

            return 0;
        }

        private static int IndexOfProfile(
            IReadOnlyList<CharacterClassProfileDefinition> profiles,
            StableId profileStableId)
        {
            for (int index = 0; index < profiles.Count; index++)
            {
                if (profiles[index].LoadoutProfileStableId == profileStableId)
                {
                    return index;
                }
            }

            return 0;
        }

        private static int Wrap(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int result = value % count;
            return result < 0 ? result + count : result;
        }
    }
}

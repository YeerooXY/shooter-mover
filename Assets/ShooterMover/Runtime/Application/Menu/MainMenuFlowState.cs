using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Menu
{
    public enum MainMenuScreen
    {
        Title = 1,
        Armory = 2,
        Shop = 3,
        Crafting = 4,
        Settings = 5,
    }

    /// <summary>
    /// One selectable weapon instance. Instance identity is intentionally distinct
    /// from definition identity so several owned copies of the same definition stay
    /// visible and independently selectable.
    /// </summary>
    public sealed class MenuWeaponOption
    {
        public MenuWeaponOption(
            StableId instanceStableId,
            StableId definitionStableId,
            string displayName,
            int itemLevel = 0)
        {
            InstanceStableId = instanceStableId
                ?? throw new ArgumentNullException(nameof(instanceStableId));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A weapon display name is required.",
                    nameof(displayName));
            }

            if (itemLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(itemLevel));
            }

            DisplayName = displayName.Trim();
            ItemLevel = itemLevel;
        }

        public StableId InstanceStableId { get; }

        public StableId DefinitionStableId { get; }

        public string DisplayName { get; }

        public int ItemLevel { get; }

        public string ToDisplayString()
        {
            string instanceText = InstanceStableId.ToString();
            string suffix = instanceText.Length <= 10
                ? instanceText
                : instanceText.Substring(instanceText.Length - 10);
            return ItemLevel > 0
                ? DisplayName + "  L" + ItemLevel + "  [" + suffix + "]"
                : DisplayName + "  [" + suffix + "]";
        }
    }

    /// <summary>
    /// Deterministic session loadout state for the four stable weapon slots.
    /// Definition identities may repeat; only concrete option instance identities
    /// must be unique.
    /// </summary>
    public sealed class ArmoryLoadoutState
    {
        public const int SlotCount = 4;

        private ReadOnlyCollection<MenuWeaponOption> options;
        private readonly int[] selectedOptionIndices = new int[SlotCount];

        public ArmoryLoadoutState(IEnumerable<MenuWeaponOption> sourceOptions)
        {
            options = CopyAndValidateOptions(sourceOptions);
        }

        public IReadOnlyList<MenuWeaponOption> Options
        {
            get { return options; }
        }

        public int ActiveSlotIndex { get; private set; }

        public int GetSelectedOptionIndex(int slotIndex)
        {
            ValidateSlot(slotIndex);
            return selectedOptionIndices[slotIndex];
        }

        public MenuWeaponOption GetSelectedWeapon(int slotIndex)
        {
            return options[GetSelectedOptionIndex(slotIndex)];
        }

        public bool SelectSlot(int slotIndex)
        {
            ValidateSlot(slotIndex);
            if (ActiveSlotIndex == slotIndex)
            {
                return false;
            }

            ActiveSlotIndex = slotIndex;
            return true;
        }

        public bool CycleActiveSelection(int delta)
        {
            return CycleSelection(ActiveSlotIndex, delta);
        }

        public bool CycleSelection(int slotIndex, int delta)
        {
            ValidateSlot(slotIndex);
            if (delta == 0 || options.Count <= 1)
            {
                return false;
            }

            int current = selectedOptionIndices[slotIndex];
            int next = (current + delta) % options.Count;
            if (next < 0)
            {
                next += options.Count;
            }

            if (next == current)
            {
                return false;
            }

            selectedOptionIndices[slotIndex] = next;
            return true;
        }

        public bool TrySelectInstance(int slotIndex, StableId instanceStableId)
        {
            ValidateSlot(slotIndex);
            if (instanceStableId == null)
            {
                return false;
            }

            for (int index = 0; index < options.Count; index++)
            {
                if (options[index].InstanceStableId != instanceStableId)
                {
                    continue;
                }

                if (selectedOptionIndices[slotIndex] == index)
                {
                    return false;
                }

                selectedOptionIndices[slotIndex] = index;
                return true;
            }

            return false;
        }

        public void ReplaceOptions(IEnumerable<MenuWeaponOption> sourceOptions)
        {
            StableId[] priorSelections = new StableId[SlotCount];
            for (int slot = 0; slot < SlotCount; slot++)
            {
                priorSelections[slot] = GetSelectedWeapon(slot).InstanceStableId;
            }

            ReadOnlyCollection<MenuWeaponOption> replacement =
                CopyAndValidateOptions(sourceOptions);
            options = replacement;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                selectedOptionIndices[slot] = FindOptionIndex(priorSelections[slot]);
            }
        }

        public string ToTraceString()
        {
            string[] slots = new string[SlotCount];
            for (int slot = 0; slot < SlotCount; slot++)
            {
                MenuWeaponOption selected = GetSelectedWeapon(slot);
                slots[slot] = "S"
                    + (slot + 1)
                    + "="
                    + selected.InstanceStableId
                    + "@"
                    + selected.DefinitionStableId;
            }

            return string.Join(";", slots);
        }

        private int FindOptionIndex(StableId instanceStableId)
        {
            for (int index = 0; index < options.Count; index++)
            {
                if (options[index].InstanceStableId == instanceStableId)
                {
                    return index;
                }
            }

            return 0;
        }

        private static ReadOnlyCollection<MenuWeaponOption> CopyAndValidateOptions(
            IEnumerable<MenuWeaponOption> sourceOptions)
        {
            if (sourceOptions == null)
            {
                throw new ArgumentNullException(nameof(sourceOptions));
            }

            List<MenuWeaponOption> copy = new List<MenuWeaponOption>();
            HashSet<StableId> instanceIds = new HashSet<StableId>();
            foreach (MenuWeaponOption option in sourceOptions)
            {
                if (option == null)
                {
                    throw new ArgumentException(
                        "Weapon options cannot contain null.",
                        nameof(sourceOptions));
                }

                if (!instanceIds.Add(option.InstanceStableId))
                {
                    throw new ArgumentException(
                        "Weapon option instance identities must be unique.",
                        nameof(sourceOptions));
                }

                copy.Add(option);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one selectable weapon option is required.",
                    nameof(sourceOptions));
            }

            return new ReadOnlyCollection<MenuWeaponOption>(copy);
        }

        private static void ValidateSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }
        }
    }

    public sealed class MainMenuSettingsState
    {
        public bool ReducedEffects { get; private set; }

        public bool Grayscale { get; private set; }

        public bool SetReducedEffects(bool value)
        {
            if (ReducedEffects == value)
            {
                return false;
            }

            ReducedEffects = value;
            return true;
        }

        public bool SetGrayscale(bool value)
        {
            if (Grayscale == value)
            {
                return false;
            }

            Grayscale = value;
            return true;
        }
    }

    /// <summary>
    /// Engine-independent menu navigation and selection state. It owns no wallet,
    /// holdings, shop, crafting, scene, or gameplay mutation authority.
    /// </summary>
    public sealed class MainMenuFlowState
    {
        public const string PlayScenePath =
            "Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity";

        private readonly List<MainMenuScreen> history =
            new List<MainMenuScreen>();

        public MainMenuFlowState(IEnumerable<MenuWeaponOption> weaponOptions)
        {
            Armory = new ArmoryLoadoutState(weaponOptions);
            Settings = new MainMenuSettingsState();
            CurrentScreen = MainMenuScreen.Title;
        }

        public MainMenuScreen CurrentScreen { get; private set; }

        public ArmoryLoadoutState Armory { get; }

        public MainMenuSettingsState Settings { get; }

        public bool PlayRequested { get; private set; }

        public bool QuitRequested { get; private set; }

        public bool HoldingsConnected { get; private set; }

        public bool ShopConnected { get; private set; }

        public bool CraftingConnected { get; private set; }

        public bool OpenScreen(MainMenuScreen screen)
        {
            if (!Enum.IsDefined(typeof(MainMenuScreen), screen))
            {
                throw new ArgumentOutOfRangeException(nameof(screen));
            }

            if (screen == CurrentScreen)
            {
                return false;
            }

            if (screen == MainMenuScreen.Title)
            {
                history.Clear();
            }
            else
            {
                history.Add(CurrentScreen);
            }

            CurrentScreen = screen;
            return true;
        }

        public bool NavigateBack()
        {
            if (history.Count > 0)
            {
                int last = history.Count - 1;
                CurrentScreen = history[last];
                history.RemoveAt(last);
                return true;
            }

            if (CurrentScreen != MainMenuScreen.Title)
            {
                CurrentScreen = MainMenuScreen.Title;
                return true;
            }

            RequestQuit();
            return false;
        }

        public void RequestPlay()
        {
            PlayRequested = true;
        }

        public void RequestQuit()
        {
            QuitRequested = true;
        }

        public void ClearTransientRequests()
        {
            PlayRequested = false;
            QuitRequested = false;
        }

        public void SetRuntimeConnections(
            bool holdingsConnected,
            bool shopConnected,
            bool craftingConnected)
        {
            HoldingsConnected = holdingsConnected;
            ShopConnected = shopConnected;
            CraftingConnected = craftingConnected;
        }
    }
}

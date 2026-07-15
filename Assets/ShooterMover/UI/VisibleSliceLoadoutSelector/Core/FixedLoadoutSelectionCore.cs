using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.VisibleSliceLoadoutSelector.Core
{
    public enum LoadoutSelectorPhase
    {
        Browsing = 1,
        Confirmed = 2,
        Cancelled = 3,
    }

    public enum LoadoutSelectorCommand
    {
        None = 0,
        Previous = 1,
        Next = 2,
        Confirm = 3,
        Cancel = 4,
    }

    /// <summary>
    /// Immutable identity-only projection of one accepted four-slot fixture.
    /// </summary>
    public sealed class FixedLoadoutOption
    {
        public const int RequiredSlotCount = 4;

        private readonly ReadOnlyCollection<string> weaponIds;

        public FixedLoadoutOption(string fixtureId, IEnumerable<string> orderedWeaponIds)
        {
            FixtureId = RequireIdentity(fixtureId, nameof(fixtureId));
            if (orderedWeaponIds == null)
            {
                throw new ArgumentNullException(nameof(orderedWeaponIds));
            }

            List<string> copy = new List<string>();
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string weaponId in orderedWeaponIds)
            {
                string value = RequireIdentity(weaponId, nameof(orderedWeaponIds));
                if (!unique.Add(value))
                {
                    throw new ArgumentException(
                        "A fixed comparison cannot repeat a weapon identity.",
                        nameof(orderedWeaponIds));
                }

                copy.Add(value);
            }

            if (copy.Count != RequiredSlotCount)
            {
                throw new ArgumentException(
                    "A fixed comparison must contain exactly four ordered weapon identities.",
                    nameof(orderedWeaponIds));
            }

            weaponIds = new ReadOnlyCollection<string>(copy);
        }

        public string FixtureId { get; }

        public IReadOnlyList<string> OrderedWeaponIds
        {
            get { return weaponIds; }
        }

        public string GetWeaponId(int stableIndex)
        {
            if (stableIndex < 0 || stableIndex >= weaponIds.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(stableIndex));
            }

            return weaponIds[stableIndex];
        }

        public string ToTraceString()
        {
            string[] slots = new string[weaponIds.Count];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = "S" + (index + 1) + "=" + weaponIds[index];
            }

            return FixtureId + "[" + string.Join(";", slots) + "]";
        }

        private static string RequireIdentity(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A stable identity is required.", parameterName);
            }

            return value.Trim();
        }
    }

    /// <summary>
    /// Deterministic, bounded, session-only selector state.
    /// </summary>
    public sealed class FixedLoadoutSelectionState
    {
        private readonly ReadOnlyCollection<FixedLoadoutOption> options;
        private readonly int defaultIndex;

        public FixedLoadoutSelectionState(
            IEnumerable<FixedLoadoutOption> sourceOptions,
            string defaultFixtureId)
        {
            if (sourceOptions == null)
            {
                throw new ArgumentNullException(nameof(sourceOptions));
            }

            if (string.IsNullOrWhiteSpace(defaultFixtureId))
            {
                throw new ArgumentException(
                    "A default fixture identity is required.",
                    nameof(defaultFixtureId));
            }

            List<FixedLoadoutOption> copy = new List<FixedLoadoutOption>();
            HashSet<string> fixtureIds = new HashSet<string>(StringComparer.Ordinal);
            int resolvedDefaultIndex = -1;
            foreach (FixedLoadoutOption option in sourceOptions)
            {
                if (option == null)
                {
                    throw new ArgumentException(
                        "Loadout options cannot contain null.",
                        nameof(sourceOptions));
                }

                if (!fixtureIds.Add(option.FixtureId))
                {
                    throw new ArgumentException(
                        "Fixed fixture identities must be unique.",
                        nameof(sourceOptions));
                }

                if (string.Equals(option.FixtureId, defaultFixtureId, StringComparison.Ordinal))
                {
                    resolvedDefaultIndex = copy.Count;
                }

                copy.Add(option);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one fixed fixture is required.",
                    nameof(sourceOptions));
            }

            if (resolvedDefaultIndex < 0)
            {
                throw new ArgumentException(
                    "The default fixture must be one of the supplied options.",
                    nameof(defaultFixtureId));
            }

            options = new ReadOnlyCollection<FixedLoadoutOption>(copy);
            defaultIndex = resolvedDefaultIndex;
            ResetForRestart();
        }

        public IReadOnlyList<FixedLoadoutOption> Options
        {
            get { return options; }
        }

        public int OptionCount
        {
            get { return options.Count; }
        }

        public int SelectedIndex { get; private set; }

        public FixedLoadoutOption Current
        {
            get { return options[SelectedIndex]; }
        }

        public FixedLoadoutOption Confirmed { get; private set; }

        public LoadoutSelectorPhase Phase { get; private set; }

        public bool IsBrowsing
        {
            get { return Phase == LoadoutSelectorPhase.Browsing; }
        }

        public bool Apply(LoadoutSelectorCommand command)
        {
            switch (command)
            {
                case LoadoutSelectorCommand.Previous:
                    return Move(-1);
                case LoadoutSelectorCommand.Next:
                    return Move(1);
                case LoadoutSelectorCommand.Confirm:
                    return Confirm();
                case LoadoutSelectorCommand.Cancel:
                    return Cancel();
                case LoadoutSelectorCommand.None:
                default:
                    return false;
            }
        }

        public bool TrySelectFixture(string fixtureId)
        {
            if (!IsBrowsing || string.IsNullOrWhiteSpace(fixtureId))
            {
                return false;
            }

            for (int index = 0; index < options.Count; index++)
            {
                if (!string.Equals(options[index].FixtureId, fixtureId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (SelectedIndex == index)
                {
                    return false;
                }

                SelectedIndex = index;
                return true;
            }

            return false;
        }

        public bool Confirm()
        {
            if (!IsBrowsing)
            {
                return false;
            }

            Confirmed = Current;
            Phase = LoadoutSelectorPhase.Confirmed;
            return true;
        }

        public bool Cancel()
        {
            if (!IsBrowsing)
            {
                return false;
            }

            Confirmed = null;
            Phase = LoadoutSelectorPhase.Cancelled;
            return true;
        }

        public void ResetForRestart()
        {
            SelectedIndex = defaultIndex;
            Confirmed = null;
            Phase = LoadoutSelectorPhase.Browsing;
        }

        private bool Move(int delta)
        {
            if (!IsBrowsing)
            {
                return false;
            }

            int next = (SelectedIndex + delta) % options.Count;
            if (next < 0)
            {
                next += options.Count;
            }

            if (next == SelectedIndex)
            {
                return false;
            }

            SelectedIndex = next;
            return true;
        }
    }

    public static class FixedLoadoutInput
    {
        public static LoadoutSelectorCommand ReadCurrent()
        {
            return Read(Keyboard.current, Gamepad.current);
        }

        public static LoadoutSelectorCommand Read(Keyboard keyboard, Gamepad gamepad)
        {
            if ((keyboard != null
                    && (keyboard.escapeKey.wasPressedThisFrame
                        || keyboard.backspaceKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
            {
                return LoadoutSelectorCommand.Cancel;
            }

            if ((keyboard != null
                    && (keyboard.enterKey.wasPressedThisFrame
                        || keyboard.spaceKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
            {
                return LoadoutSelectorCommand.Confirm;
            }

            if ((keyboard != null
                    && (keyboard.leftArrowKey.wasPressedThisFrame
                        || keyboard.upArrowKey.wasPressedThisFrame))
                || (gamepad != null
                    && (gamepad.dpad.left.wasPressedThisFrame
                        || gamepad.dpad.up.wasPressedThisFrame
                        || gamepad.leftShoulder.wasPressedThisFrame)))
            {
                return LoadoutSelectorCommand.Previous;
            }

            if ((keyboard != null
                    && (keyboard.rightArrowKey.wasPressedThisFrame
                        || keyboard.downArrowKey.wasPressedThisFrame))
                || (gamepad != null
                    && (gamepad.dpad.right.wasPressedThisFrame
                        || gamepad.dpad.down.wasPressedThisFrame
                        || gamepad.rightShoulder.wasPressedThisFrame)))
            {
                return LoadoutSelectorCommand.Next;
            }

            return LoadoutSelectorCommand.None;
        }
    }
}

using System;
using System.Collections.Generic;
using ShooterMover.ContentPackages.Weapons.Stage1Loadouts;
using ShooterMover.ContentPackages.Weapons.Stage1;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UI.VisibleSliceLoadoutSelector.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.VisibleSliceLoadoutSelector
{
    /// <summary>
    /// Temporary pre-room four-slot loadout editor. It edits only approved Stage 1
    /// weapon identities, emits one immutable confirmed fixture, and owns no live
    /// mount state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisibleSliceLoadoutSelector : MonoBehaviour
    {
        [SerializeField] private bool visibleOnEnable = true;
        [SerializeField] private float panelWidth = 720f;
        [SerializeField] private float panelHeight = 400f;

        private List<Stage1WeaponLoadoutFixture> approvedFixtures;
        private FixedLoadoutSelectionState state;
        private IReadOnlyList<StableId> approvedWeaponIds;
        private StableId[] selectedWeaponIds;
        private int selectedSlot;
        private bool visible;

        public event Action<Stage1WeaponLoadoutFixture> Confirmed;

        public event Action Cancelled;

        public FixedLoadoutSelectionState State
        {
            get
            {
                EnsureInitialized();
                return state;
            }
        }

        public Stage1WeaponLoadoutFixture ConfirmedFixture { get; private set; }

        public bool Visible
        {
            get { return visible; }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (visibleOnEnable)
            {
                ResetForRestart();
            }
            else
            {
                visible = false;
            }
        }

        private void Update()
        {
            if (!visible || !State.IsBrowsing)
            {
                return;
            }

            ApplyEditorInput();
        }

        public void ResetForRestart()
        {
            EnsureInitialized();
            state.ResetForRestart();
            Stage1WeaponLoadoutFixture defaultFixture = Stage1WeaponLoadoutCatalog.Approved.DefaultFixture;
            for (int index = 0; index < selectedWeaponIds.Length; index++)
            {
                selectedWeaponIds[index] = defaultFixture.GetByHudIndex(index).WeaponId;
            }
            selectedSlot = 0;
            ConfirmedFixture = null;
            visible = true;
        }

        public void Hide()
        {
            visible = false;
        }

        public bool TrySelectFixture(string fixtureId)
        {
            EnsureInitialized();
            return visible && state.TrySelectFixture(fixtureId);
        }

        public bool ApplyCommand(LoadoutSelectorCommand command)
        {
            EnsureInitialized();
            if (!visible || command == LoadoutSelectorCommand.None)
            {
                return false;
            }

            bool changed = state.Apply(command);
            if (!changed)
            {
                return false;
            }

            if (state.Phase == LoadoutSelectorPhase.Confirmed)
            {
                Stage1WeaponLoadoutFixture fixture = BuildSelectedFixture();
                ConfirmedFixture = fixture;
                visible = false;

                Action<Stage1WeaponLoadoutFixture> handler = Confirmed;
                if (handler != null)
                {
                    handler(fixture);
                }
            }
            else if (state.Phase == LoadoutSelectorPhase.Cancelled)
            {
                ConfirmedFixture = null;
                visible = false;

                Action handler = Cancelled;
                if (handler != null)
                {
                    handler();
                }
            }

            return true;
        }

        private void OnGUI()
        {
            if (!visible || !State.IsBrowsing)
            {
                return;
            }

            float width = Mathf.Max(1f, Mathf.Min(panelWidth, Screen.width - 32f));
            float height = Mathf.Max(1f, Mathf.Min(panelHeight, Screen.height - 32f));
            Rect area = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label("LOADOUT");
            GUILayout.Label("Choose a weapon for each mount. Duplicates are allowed; all four fire together.");
            GUILayout.Space(10f);

            for (int stableIndex = 0; stableIndex < WeaponMountContractRules.MountCount; stableIndex++)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(
                    stableIndex == selectedSlot ? "> S" + (stableIndex + 1) : "  S" + (stableIndex + 1),
                    GUILayout.Width(55f));
                GUILayout.Label(GetWeaponLabel(selectedWeaponIds[stableIndex]));
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Keyboard: Left/Right choose slot, Up/Down change weapon");
            GUILayout.Label("Enter/Space confirm, Esc/Backspace cancel");
            GUILayout.Label("Controller: D-pad chooses slot/weapon, South confirm, East cancel");
            GUILayout.EndArea();
        }

        private void EnsureInitialized()
        {
            if (state != null)
            {
                return;
            }

            Stage1WeaponLoadoutCatalog catalog = Stage1WeaponLoadoutCatalog.Approved;
            approvedFixtures = new List<Stage1WeaponLoadoutFixture>(catalog.FixedFixtures);
            approvedWeaponIds = Stage1WeaponPackageDescriptor.AcceptedWeaponIds;
            selectedWeaponIds = new StableId[WeaponMountContractRules.MountCount];
            Stage1WeaponLoadoutFixture defaultFixture = catalog.DefaultFixture;
            for (int index = 0; index < selectedWeaponIds.Length; index++)
            {
                selectedWeaponIds[index] = defaultFixture.GetByHudIndex(index).WeaponId;
            }

            if (approvedFixtures.Count == 0)
            {
                throw new InvalidOperationException(
                    "The accepted WP-008 catalog must expose at least one fixed fixture.");
            }

            List<FixedLoadoutOption> options = new List<FixedLoadoutOption>(approvedFixtures.Count);
            for (int fixtureIndex = 0; fixtureIndex < approvedFixtures.Count; fixtureIndex++)
            {
                Stage1WeaponLoadoutFixture fixture = approvedFixtures[fixtureIndex];
                string[] weaponIds = new string[FixedLoadoutOption.RequiredSlotCount];
                for (int stableIndex = 0; stableIndex < weaponIds.Length; stableIndex++)
                {
                    weaponIds[stableIndex] = fixture.GetByHudIndex(stableIndex).WeaponId.ToString();
                }

                options.Add(new FixedLoadoutOption(fixture.FixtureId.ToString(), weaponIds));
            }

            state = new FixedLoadoutSelectionState(
                options,
                catalog.DefaultFixture.FixtureId.ToString());
            ConfirmedFixture = null;
            selectedSlot = 0;
            visible = false;
        }

        private void ApplyEditorInput()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            if ((keyboard != null
                    && (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
            {
                ApplyCommand(LoadoutSelectorCommand.Cancel);
                return;
            }

            if ((keyboard != null
                    && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
            {
                ApplyCommand(LoadoutSelectorCommand.Confirm);
                return;
            }

            bool previousSlot = keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame;
            bool nextSlot = keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame;
            bool previousWeapon = keyboard != null && keyboard.upArrowKey.wasPressedThisFrame;
            bool nextWeapon = keyboard != null && keyboard.downArrowKey.wasPressedThisFrame;
            if (gamepad != null)
            {
                previousSlot |= gamepad.dpad.left.wasPressedThisFrame;
                nextSlot |= gamepad.dpad.right.wasPressedThisFrame;
                previousWeapon |= gamepad.dpad.up.wasPressedThisFrame;
                nextWeapon |= gamepad.dpad.down.wasPressedThisFrame;
            }

            if (previousSlot) selectedSlot = (selectedSlot + selectedWeaponIds.Length - 1) % selectedWeaponIds.Length;
            if (nextSlot) selectedSlot = (selectedSlot + 1) % selectedWeaponIds.Length;
            if (previousWeapon) SelectWeapon(-1);
            if (nextWeapon) SelectWeapon(1);
        }

        private void SelectWeapon(int delta)
        {
            if (approvedWeaponIds == null || approvedWeaponIds.Count == 0) return;
            int currentIndex = 0;
            for (int index = 0; index < approvedWeaponIds.Count; index++)
            {
                if (approvedWeaponIds[index].Equals(selectedWeaponIds[selectedSlot]))
                {
                    currentIndex = index;
                    break;
                }
            }

            currentIndex = (currentIndex + delta + approvedWeaponIds.Count) % approvedWeaponIds.Count;
            selectedWeaponIds[selectedSlot] = approvedWeaponIds[currentIndex];
        }

        private Stage1WeaponLoadoutFixture BuildSelectedFixture()
        {
            return Stage1WeaponLoadoutFixture.Create(
                StableId.Parse("loadout.stage1-custom"),
                new[]
                {
                    Stage1WeaponLoadoutSlot.Create(WeaponMountSlot.MountOne, selectedWeaponIds[0]),
                    Stage1WeaponLoadoutSlot.Create(WeaponMountSlot.MountTwo, selectedWeaponIds[1]),
                    Stage1WeaponLoadoutSlot.Create(WeaponMountSlot.MountThree, selectedWeaponIds[2]),
                    Stage1WeaponLoadoutSlot.Create(WeaponMountSlot.MountFour, selectedWeaponIds[3]),
                });
        }

        private static string GetWeaponLabel(StableId weaponId)
        {
            switch (weaponId.ToString())
            {
                case "weapon.blaster-machine-gun": return "Blaster Machine Gun";
                case "weapon.shotgun": return "Shotgun";
                case "weapon.rocket-launcher": return "Rocket Launcher";
                case "weapon.arc-gun": return "Arc Gun";
                case "weapon.ricochet-gun": return "Ricochet Gun";
                default: return weaponId.ToString();
            }
        }
    }
}

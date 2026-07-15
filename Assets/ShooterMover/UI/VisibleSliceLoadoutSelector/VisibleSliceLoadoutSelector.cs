using System;
using System.Collections.Generic;
using ShooterMover.ContentPackages.Weapons.Stage1Loadouts;
using ShooterMover.UI.VisibleSliceLoadoutSelector.Core;
using UnityEngine;

namespace ShooterMover.UI.VisibleSliceLoadoutSelector
{
    /// <summary>
    /// Temporary pre-room selector. It projects the accepted WP-008 fixed fixtures,
    /// emits the exact immutable confirmed fixture, and owns no live mount state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisibleSliceLoadoutSelector : MonoBehaviour
    {
        [SerializeField] private bool visibleOnEnable = true;
        [SerializeField] private float panelWidth = 720f;
        [SerializeField] private float panelHeight = 400f;

        private List<Stage1WeaponLoadoutFixture> approvedFixtures;
        private FixedLoadoutSelectionState state;
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

            ApplyCommand(FixedLoadoutInput.ReadCurrent());
        }

        public void ResetForRestart()
        {
            EnsureInitialized();
            state.ResetForRestart();
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
                Stage1WeaponLoadoutFixture fixture = ResolveSelectedApprovedFixture();
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
            GUILayout.Label("FIXED LOADOUT SELECTOR");
            GUILayout.Label("Choose one accepted four-slot WP-008 comparison before room entry.");
            GUILayout.Space(10f);

            FixedLoadoutOption option = State.Current;
            GUILayout.Label(
                (State.SelectedIndex + 1)
                + " / "
                + State.OptionCount
                + "   "
                + option.FixtureId);
            GUILayout.Space(8f);

            for (int stableIndex = 0; stableIndex < FixedLoadoutOption.RequiredSlotCount; stableIndex++)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label("S" + (stableIndex + 1), GUILayout.Width(40f));
                GUILayout.Label(option.GetWeaponId(stableIndex));
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Keyboard: arrows browse, Enter/Space confirm, Esc/Backspace cancel");
            GUILayout.Label("Controller: D-pad/shoulders browse, South confirm, East cancel");
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
            visible = false;
        }

        private Stage1WeaponLoadoutFixture ResolveSelectedApprovedFixture()
        {
            if (state.SelectedIndex < 0 || state.SelectedIndex >= approvedFixtures.Count)
            {
                throw new InvalidOperationException("Selected fixture index is outside the accepted WP-008 catalog.");
            }

            Stage1WeaponLoadoutFixture fixture = approvedFixtures[state.SelectedIndex];
            if (state.Confirmed == null
                || !string.Equals(
                    fixture.FixtureId.ToString(),
                    state.Confirmed.FixtureId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Confirmed selector identity no longer matches the accepted WP-008 fixture.");
            }

            return fixture;
        }
    }
}

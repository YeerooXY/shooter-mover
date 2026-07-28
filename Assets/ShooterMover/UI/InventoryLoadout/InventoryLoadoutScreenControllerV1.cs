using System;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.InventoryLoadout
{
    [DisallowMultipleComponent]
    public sealed class InventoryLoadoutScreenControllerV1 :
        MonoBehaviour,
        IHubRouteDestinationAdapterV1
    {
        private IPlayerHoldingsAuthorityV1 holdingsAuthority;
        private IEquipmentCatalogProvider equipmentCatalogProvider;
        private IInventoryLoadoutAuthorityPortV1 loadoutAuthority;
        private ProductionWeaponHoldingsAuthorityV2 canonicalWeaponHoldings;
        private ProductionInventoryLoadoutAuthorityV1 canonicalLoadoutAuthority;
        private ProductionWeaponMountLayoutV1 canonicalMountLayout;
        private WeaponCatalog canonicalWeaponCatalog;
        private InventoryLoadoutScreenServiceV1 legacyService;
        private CanonicalWeaponInventoryScreenServiceV2 canonicalService;
        private Action<PlayerRouteProfilePayloadV1> returnToHub;
        private PlayerRouteProfilePayloadV1 incomingPayload;
        private InventoryLoadoutScreenResultV1 lastResult;
        private StableId activeSlotStableId =
            InventoryLoadoutSlotIdsV1.WeaponOne;
        private bool returnDispatched;
        private Vector2 equipmentScroll;
        private Vector2 slotScroll;
        private Vector2 selectedScroll;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle invalidStyle;

        public event Action<PlayerRouteProfilePayloadV1> Confirmed;

        public InventoryLoadoutScreenSnapshotV1 Snapshot
        {
            get
            {
                if (canonicalService != null)
                {
                    return canonicalService.CompatibilitySnapshot;
                }
                return legacyService == null ? null : legacyService.Snapshot;
            }
        }

        public CanonicalWeaponInventorySnapshotV2 CanonicalSnapshot
        {
            get { return canonicalService == null ? null : canonicalService.Snapshot; }
        }

        public InventoryLoadoutScreenResultV1 LastResult
        {
            get { return lastResult; }
        }

        public PlayerRouteProfilePayloadV1 IncomingPayload
        {
            get { return incomingPayload; }
        }

        public PlayerRouteProfilePayloadV1 LastReturnedPayload
        {
            get;
            private set;
        }

        public int ReturnCount { get; private set; }

        public StableId ActiveSlotStableId
        {
            get { return activeSlotStableId; }
        }

        public bool IsConfigured
        {
            get
            {
                return canonicalWeaponHoldings != null
                    && canonicalLoadoutAuthority != null
                    && canonicalMountLayout != null
                    && canonicalWeaponCatalog != null
                    || holdingsAuthority != null
                    && equipmentCatalogProvider != null
                    && loadoutAuthority != null;
            }
        }

        public void Configure(
            IPlayerHoldingsAuthorityV1 holdingsAuthority,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            IInventoryLoadoutAuthorityPortV1 loadoutAuthority,
            Action<PlayerRouteProfilePayloadV1> returnToHub)
        {
            ConnectAuthorities(
                holdingsAuthority,
                equipmentCatalogProvider,
                loadoutAuthority);
            this.returnToHub = returnToHub;
        }

        public void ConnectAuthorities(
            IPlayerHoldingsAuthorityV1 holdingsAuthority,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            IInventoryLoadoutAuthorityPortV1 loadoutAuthority)
        {
            this.holdingsAuthority = holdingsAuthority
                ?? throw new ArgumentNullException(nameof(holdingsAuthority));
            this.equipmentCatalogProvider = equipmentCatalogProvider
                ?? throw new ArgumentNullException(
                    nameof(equipmentCatalogProvider));
            this.loadoutAuthority = loadoutAuthority
                ?? throw new ArgumentNullException(nameof(loadoutAuthority));
            canonicalWeaponHoldings = null;
            canonicalLoadoutAuthority = null;
            canonicalMountLayout = null;
            canonicalWeaponCatalog = null;
            canonicalService = null;
            if (incomingPayload != null)
            {
                BuildService(incomingPayload);
            }
        }

        public void ConnectCanonicalAuthorities(
            IPlayerHoldingsAuthorityV1 genericHoldings,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            ProductionWeaponHoldingsAuthorityV2 weaponHoldings,
            ProductionInventoryLoadoutAuthorityV1 loadoutAuthority,
            ProductionWeaponMountLayoutV1 mountLayout,
            WeaponCatalog weaponCatalog)
        {
            holdingsAuthority = genericHoldings
                ?? throw new ArgumentNullException(nameof(genericHoldings));
            this.equipmentCatalogProvider = equipmentCatalogProvider
                ?? throw new ArgumentNullException(
                    nameof(equipmentCatalogProvider));
            this.loadoutAuthority = loadoutAuthority
                ?? throw new ArgumentNullException(nameof(loadoutAuthority));
            canonicalWeaponHoldings = weaponHoldings
                ?? throw new ArgumentNullException(nameof(weaponHoldings));
            canonicalLoadoutAuthority = loadoutAuthority;
            canonicalMountLayout = mountLayout
                ?? throw new ArgumentNullException(nameof(mountLayout));
            canonicalWeaponCatalog = weaponCatalog
                ?? throw new ArgumentNullException(nameof(weaponCatalog));
            legacyService = null;
            if (incomingPayload != null)
            {
                BuildService(incomingPayload);
            }
        }

        public void ConfigureWeaponPresentation(
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog)
        {
            if (canonicalWeaponCatalog == null)
            {
                canonicalWeaponCatalog = weaponCatalog;
            }
        }

        public void ConfigureDisconnected(
            Action<PlayerRouteProfilePayloadV1> returnToHub)
        {
            holdingsAuthority = null;
            equipmentCatalogProvider = null;
            loadoutAuthority = null;
            canonicalWeaponHoldings = null;
            canonicalLoadoutAuthority = null;
            canonicalMountLayout = null;
            canonicalWeaponCatalog = null;
            legacyService = null;
            canonicalService = null;
            this.returnToHub = returnToHub
                ?? throw new ArgumentNullException(nameof(returnToHub));
        }

        public void ConfigureForTests(
            IPlayerHoldingsAuthorityV1 holdingsAuthority,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            IInventoryLoadoutAuthorityPortV1 loadoutAuthority,
            Action<PlayerRouteProfilePayloadV1> returnToHub)
        {
            Configure(
                holdingsAuthority,
                equipmentCatalogProvider,
                loadoutAuthority,
                returnToHub);
        }

        public void Present(
            HubRouteV1 route,
            PlayerRouteProfilePayloadV1 payload)
        {
            if (route != HubRouteV1.Inventory)
            {
                throw new ArgumentOutOfRangeException(nameof(route));
            }

            incomingPayload = payload
                ?? throw new ArgumentNullException(nameof(payload));
            if (!payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The presented HUB route payload fingerprint is invalid.",
                    nameof(payload));
            }

            returnDispatched = false;
            LastReturnedPayload = null;
            ReturnCount = 0;
            lastResult = null;
            ProductionWeaponMountLayoutV1 layout =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    payload.LoadoutProfileStableId);
            activeSlotStableId = layout.Positions[0].LoadoutSlotStableId;
            BuildService(payload);
        }

        public bool SelectSlot(StableId slotStableId)
        {
            if (incomingPayload == null || slotStableId == null)
            {
                return false;
            }
            ProductionWeaponMountLayoutV1 layout =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    incomingPayload.LoadoutProfileStableId);
            bool physical = false;
            for (int index = 0; index < layout.Positions.Count; index++)
            {
                if (layout.Positions[index].LoadoutSlotStableId == slotStableId)
                {
                    physical = true;
                    break;
                }
            }
            if (!physical || activeSlotStableId == slotStableId)
            {
                return false;
            }
            activeSlotStableId = slotStableId;
            return true;
        }

        public bool SelectSlotByIndex(int index)
        {
            if (incomingPayload == null)
            {
                return false;
            }
            ProductionWeaponMountLayoutV1 layout =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    incomingPayload.LoadoutProfileStableId);
            if (index < 0 || index >= layout.Positions.Count)
            {
                return false;
            }
            return SelectSlot(layout.Positions[index].LoadoutSlotStableId);
        }

        /// <summary>
        /// Compatibility action: in canonical mode this selects and equips the exact instance.
        /// The UI itself separates selection from the explicit Equip action.
        /// </summary>
        public InventoryLoadoutScreenResultV1 SelectInstance(
            StableId equipmentInstanceStableId)
        {
            if (canonicalService != null)
            {
                lastResult = canonicalService.SelectWeapon(
                    equipmentInstanceStableId);
                if (lastResult.Status
                    != InventoryLoadoutScreenStatusV1.SelectionChanged
                    && lastResult.Status
                        != InventoryLoadoutScreenStatusV1.NoChange)
                {
                    return lastResult;
                }
                lastResult = canonicalService.EquipSelected(
                    activeSlotStableId);
                return lastResult;
            }
            if (legacyService == null)
            {
                return null;
            }
            lastResult = legacyService.TrySelect(
                activeSlotStableId,
                equipmentInstanceStableId);
            return lastResult;
        }

        public InventoryLoadoutScreenResultV1 UnequipActiveSlot()
        {
            if (canonicalService != null)
            {
                lastResult = canonicalService.Unequip(activeSlotStableId);
                return lastResult;
            }
            if (legacyService == null)
            {
                return null;
            }
            lastResult = legacyService.TryUnequip(activeSlotStableId);
            return lastResult;
        }

        public InventoryLoadoutScreenResultV1 Refresh()
        {
            if (canonicalService != null)
            {
                lastResult = canonicalService.Refresh();
                return lastResult;
            }
            if (legacyService == null)
            {
                return null;
            }
            lastResult = legacyService.Refresh();
            return lastResult;
        }

        public InventoryLoadoutScreenResultV1 Confirm()
        {
            if (canonicalService != null)
            {
                lastResult = canonicalService.Confirm();
            }
            else if (legacyService != null)
            {
                lastResult = legacyService.Confirm();
            }
            else
            {
                return null;
            }

            if (lastResult.Status
                == InventoryLoadoutScreenStatusV1.Confirmed)
            {
                Action<PlayerRouteProfilePayloadV1> handler = Confirmed;
                if (handler != null)
                {
                    handler(lastResult.RoutePayload);
                }
                DispatchReturn(lastResult.RoutePayload);
            }
            return lastResult;
        }

        public InventoryLoadoutScreenResultV1 Back()
        {
            if (canonicalService != null)
            {
                lastResult = canonicalService.Back();
            }
            else if (legacyService != null)
            {
                lastResult = legacyService.Back();
            }
            else
            {
                DispatchReturn(incomingPayload);
                return null;
            }

            if (lastResult.Status
                == InventoryLoadoutScreenStatusV1.Cancelled)
            {
                DispatchReturn(lastResult.RoutePayload);
            }
            return lastResult;
        }

        private void Update()
        {
            bool back = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            back |= Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (back)
            {
                Back();
                return;
            }

            bool confirm = Keyboard.current != null
                && Keyboard.current.enterKey.wasPressedThisFrame;
            confirm |= Gamepad.current != null
                && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (confirm)
            {
                Confirm();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.Box(
                new Rect(0f, 0f, Screen.width, Screen.height),
                GUIContent.none);
            float width = Mathf.Min(
                1320f,
                Mathf.Max(520f, Screen.width - 24f));
            float height = Mathf.Min(
                860f,
                Mathf.Max(400f, Screen.height - 24f));
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height),
                GUI.skin.window);
            GUILayout.Label("INVENTORY / LOADOUT", titleStyle);
            if (incomingPayload != null)
            {
                GUILayout.Label(
                    incomingPayload.SelectedCharacterStableId
                    + " / "
                    + incomingPayload.LoadoutProfileStableId,
                    smallStyle);
            }

            if (canonicalService != null)
            {
                DrawCanonical();
            }
            else if (legacyService != null)
            {
                DrawLegacy();
            }
            else
            {
                DrawDisconnected();
            }
            GUILayout.EndArea();
        }

        private void DrawDisconnected()
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "AWAITING INVENTORY AUTHORITY COMPOSITION",
                headingStyle);
            GUILayout.Label(
                "No fallback holdings, starter grant or loadout authority was created.",
                bodyStyle);
            if (GUILayout.Button(
                "BACK TO HUB",
                GUILayout.MinHeight(46f)))
            {
                Back();
            }
            GUILayout.FlexibleSpace();
        }

        private void DrawCanonical()
        {
            CanonicalWeaponInventorySnapshotV2 current =
                canonicalService.Snapshot;
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(340f));
            GUILayout.Label("EQUIPPED WEAPONS", headingStyle);
            slotScroll = GUILayout.BeginScrollView(slotScroll);
            for (int index = 0; index < current.Mounts.Count; index++)
            {
                DrawCanonicalMount(current.Mounts[index]);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(10f);
            GUILayout.BeginVertical(GUILayout.Width(420f));
            GUILayout.Label("OWNED WEAPONS", headingStyle);
            equipmentScroll = GUILayout.BeginScrollView(equipmentScroll);
            for (int index = 0;
                 index < current.OwnedWeapons.Count;
                 index++)
            {
                DrawCanonicalOwnedWeapon(current.OwnedWeapons[index]);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(10f);
            GUILayout.BeginVertical();
            GUILayout.Label("SELECTED WEAPON", headingStyle);
            selectedScroll = GUILayout.BeginScrollView(selectedScroll);
            DrawSelectedWeapon(current.SelectedWeapon, current);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("REFRESH", GUILayout.MinHeight(42f)))
            {
                Refresh();
            }
            GUI.enabled = current.CanConfirm;
            if (GUILayout.Button("CONFIRM", GUILayout.MinHeight(42f)))
            {
                Confirm();
            }
            GUI.enabled = true;
            if (GUILayout.Button("BACK", GUILayout.MinHeight(42f)))
            {
                Back();
            }
            GUILayout.EndHorizontal();
            DrawLastDiagnostic();
            GUILayout.Label(
                "Weapon holdings sequence "
                + current.WeaponHoldingsSequence
                + "  •  Loadout sequence "
                + current.LoadoutSequence,
                smallStyle);
        }

        private void DrawCanonicalMount(
            CanonicalWeaponInventoryMountV2 mount)
        {
            bool selected = mount.Position.LoadoutSlotStableId
                == activeSlotStableId;
            bool locked = mount.Position.Availability
                != ProductionWeaponMountAvailabilityV1.Active;
            string value = mount.EquippedCard == null
                ? "EMPTY"
                : mount.EquippedCard.DisplayName
                    + "\n"
                    + ShortIdentity(mount.EquippedInstanceId);
            string label = (selected ? "▶ " : string.Empty)
                + mount.Position.DisplayName
                + "\n"
                + (locked
                    ? "LOCKED — SKILL REQUIRED"
                    : value);
            if (GUILayout.Button(label, GUILayout.MinHeight(78f)))
            {
                activeSlotStableId = mount.Position.LoadoutSlotStableId;
            }
            if (locked)
            {
                GUILayout.Label(
                    string.IsNullOrWhiteSpace(mount.Position.LockReason)
                        ? "A skill is required to activate this mount."
                        : mount.Position.LockReason,
                    invalidStyle);
            }
        }

        private void DrawCanonicalOwnedWeapon(
            CanonicalWeaponInventoryCardV2 card)
        {
            bool selected = canonicalService.Snapshot.SelectedInstanceId
                == card.Instance.InstanceId;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(
                (selected ? "▶ " : string.Empty) + card.DisplayName,
                headingStyle);
            GUILayout.Label(
                card.Instance.WeaponDefinitionId.Value
                + (string.IsNullOrEmpty(card.Family)
                    ? string.Empty
                    : "  •  " + card.Family),
                smallStyle);
            GUILayout.Label(
                "Exact instance: " + ShortIdentity(card.Instance.InstanceId),
                smallStyle);
            GUILayout.Label(
                card.IsEquipped
                    ? "Equipped on " + card.EquippedMountId
                    : "Unequipped",
                smallStyle);
            if (GUILayout.Button(
                "SELECT THIS INSTANCE",
                GUILayout.MinHeight(34f)))
            {
                lastResult = canonicalService.SelectWeapon(
                    card.Instance.InstanceId);
            }
            GUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private void DrawSelectedWeapon(
            CanonicalWeaponInventoryCardV2 card,
            CanonicalWeaponInventorySnapshotV2 snapshot)
        {
            if (card == null)
            {
                GUILayout.Label("No weapon selected.", bodyStyle);
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(card.DisplayName, headingStyle);
            GUILayout.Label(
                "Definition: " + card.Instance.WeaponDefinitionId.Value,
                bodyStyle);
            GUILayout.Label(
                "Family: "
                + (string.IsNullOrEmpty(card.Family)
                    ? "Unresolved"
                    : card.Family),
                bodyStyle);
            GUILayout.Space(8f);
            GUILayout.Label("AUGMENTS", headingStyle);
            DrawAssignments(card.Instance.AugmentAssignments, "No augments assigned");
            GUILayout.Label("OVERCLOCKS", headingStyle);
            DrawAssignments(
                card.Instance.OverclockAssignments,
                "No overclocks assigned");
            DrawCanonicalSafety(card.Instance);
            GUILayout.Space(8f);
            GUILayout.Label(
                "[DEBUG] EXACT INSTANCE ID\n" + card.Instance.InstanceId,
                smallStyle);
            GUILayout.EndVertical();

            CanonicalWeaponInventoryMountV2 activeMount =
                snapshot.FindMount(activeSlotStableId);
            bool active = activeMount != null
                && activeMount.Position.Availability
                    == ProductionWeaponMountAvailabilityV1.Active;
            GUI.enabled = active;
            if (GUILayout.Button(
                "EQUIP SELECTED INSTANCE",
                GUILayout.MinHeight(42f)))
            {
                lastResult = canonicalService.EquipSelected(
                    activeSlotStableId);
            }
            bool canUnequip = active
                && activeMount.EquippedInstanceId != null;
            GUI.enabled = canUnequip;
            if (GUILayout.Button(
                "UNEQUIP ACTIVE MOUNT",
                GUILayout.MinHeight(42f)))
            {
                UnequipActiveSlot();
            }
            GUI.enabled = true;

            if (activeMount != null
                && activeMount.Position.Availability
                    != ProductionWeaponMountAvailabilityV1.Active)
            {
                GUILayout.Label(
                    "Equip rejected: this mount requires a skill.",
                    invalidStyle);
            }
        }

        private void DrawLegacy()
        {
            InventoryLoadoutScreenSnapshotV1 current = legacyService.Snapshot;
            GUILayout.Label(
                "LEGACY GENERIC EQUIPMENT COMPATIBILITY VIEW",
                headingStyle);
            GUILayout.Label(
                "Production weapon ownership uses the canonical exact-instance view.",
                bodyStyle);
            GUILayout.Label(
                "Held equipment: " + current.Equipment.Count
                + "  •  Loadout sequence " + current.LoadoutSequence,
                bodyStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("REFRESH", GUILayout.MinHeight(42f)))
            {
                Refresh();
            }
            if (GUILayout.Button("CONFIRM", GUILayout.MinHeight(42f)))
            {
                Confirm();
            }
            if (GUILayout.Button("BACK", GUILayout.MinHeight(42f)))
            {
                Back();
            }
            GUILayout.EndHorizontal();
            DrawLastDiagnostic();
        }

        private void DrawCanonicalSafety(WeaponEquipmentInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            ProductionWeaponMarkV1 mark;
            bool definitionResolved = ProductionWeaponCatalogProvider.Current
                .TryGetMark(instance.WeaponDefinitionId.Value, out mark)
                && mark != null;
            CanonicalWeaponOperationAvailabilityV1 upgrade =
                CanonicalWeaponSafetyPolicyV1.EvaluateGenericUpgrade(
                    true,
                    definitionResolved);
            CanonicalWeaponOperationAvailabilityV1 live =
                CanonicalWeaponSafetyPolicyV1.EvaluateLiveExecution(
                    instance,
                    definitionResolved);
            CanonicalWeaponOperationAvailabilityV1 overclock =
                CanonicalWeaponSafetyPolicyV1.EvaluateOverclockInstallation();

            GUILayout.Space(10f);
            GUILayout.Label("WEAPON SAFETY GATE", headingStyle);
            GUI.enabled = false;
            GUILayout.Button(
                "AUGMENT UPGRADE — BLOCKED",
                GUILayout.MinHeight(30f));
            GUI.enabled = true;
            GUILayout.Label(
                upgrade.RejectionCode + " — " + upgrade.Message,
                invalidStyle);

            if (instance.OverclockAssignments.Count == 0)
            {
                GUILayout.Label(
                    "OVERCLOCK INSTALLATION — NOT AVAILABLE\n"
                    + overclock.RejectionCode,
                    invalidStyle);
            }
            else
            {
                GUILayout.Label(
                    "LIVE EXECUTION — BLOCKED\n"
                    + live.RejectionCode + " — " + live.Message,
                    invalidStyle);
            }
        }

        private static void DrawAssignments(
            System.Collections.Generic.IReadOnlyList<StableId> assignments,
            string emptyText)
        {
            if (assignments == null || assignments.Count == 0)
            {
                GUILayout.Label(emptyText);
                return;
            }
            for (int index = 0; index < assignments.Count; index++)
            {
                GUILayout.Label("• " + assignments[index]);
            }
        }

        private void DrawLastDiagnostic()
        {
            if (lastResult != null
                && !string.IsNullOrEmpty(lastResult.RejectionCode))
            {
                GUILayout.Label(lastResult.RejectionCode, invalidStyle);
            }
        }

        private void BuildService(PlayerRouteProfilePayloadV1 payload)
        {
            canonicalService = null;
            legacyService = null;
            if (canonicalWeaponHoldings != null
                && canonicalLoadoutAuthority != null
                && canonicalMountLayout != null
                && canonicalWeaponCatalog != null
                && holdingsAuthority != null)
            {
                canonicalService =
                    new CanonicalWeaponInventoryScreenServiceV2(
                        payload,
                        holdingsAuthority,
                        canonicalWeaponHoldings,
                        canonicalLoadoutAuthority,
                        canonicalMountLayout,
                        canonicalWeaponCatalog);
                return;
            }
            if (holdingsAuthority != null
                && equipmentCatalogProvider != null
                && loadoutAuthority != null)
            {
                legacyService = new InventoryLoadoutScreenServiceV1(
                    payload,
                    holdingsAuthority,
                    equipmentCatalogProvider,
                    loadoutAuthority);
            }
        }

        private void DispatchReturn(PlayerRouteProfilePayloadV1 payload)
        {
            if (returnDispatched || payload == null)
            {
                return;
            }
            returnDispatched = true;
            LastReturnedPayload = payload;
            ReturnCount++;
            if (returnToHub != null)
            {
                returnToHub(payload);
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
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                wordWrap = true,
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                wordWrap = true,
            };
            invalidStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
            };
        }

        private static string ShortIdentity(StableId stableId)
        {
            string text = stableId == null
                ? string.Empty
                : stableId.ToString();
            return text.Length <= 28
                ? text
                : "…" + text.Substring(text.Length - 27);
        }
    }
}

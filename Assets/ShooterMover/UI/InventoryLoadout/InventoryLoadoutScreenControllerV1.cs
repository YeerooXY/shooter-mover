using System;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.InventoryLoadout
{
    /// <summary>
    /// Functional, artwork-independent projection. It renders application snapshots and
    /// returns confirm/back payloads to the composing HUB flow. No local list owns truth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryLoadoutScreenControllerV1 : MonoBehaviour, IHubRouteDestinationAdapterV1
    {
        private const float MaximumPanelWidth = 1180f;
        private const float MaximumPanelHeight = 820f;

        private IPlayerHoldingsAuthorityV1 holdingsAuthority;
        private IEquipmentCatalogProvider equipmentCatalogProvider;
        private IInventoryLoadoutAuthorityPortV1 loadoutAuthority;
        private Action<PlayerRouteProfilePayloadV1> returnToHub;
        private InventoryLoadoutScreenServiceV1 service;
        private PlayerRouteProfilePayloadV1 incomingPayload;
        private InventoryLoadoutScreenResultV1 lastResult;
        private StableId activeSlotStableId = InventoryLoadoutSlotIdsV1.WeaponOne;
        private Vector2 equipmentScroll;
        private Vector2 slotScroll;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle invalidStyle;
        private bool returnDispatched;

        public InventoryLoadoutScreenSnapshotV1 Snapshot { get { return service == null ? null : service.Snapshot; } }
        public InventoryLoadoutScreenResultV1 LastResult { get { return lastResult; } }
        public PlayerRouteProfilePayloadV1 IncomingPayload { get { return incomingPayload; } }
        public PlayerRouteProfilePayloadV1 LastReturnedPayload { get; private set; }
        public int ReturnCount { get; private set; }
        public StableId ActiveSlotStableId { get { return activeSlotStableId; } }
        public bool IsConfigured
        {
            get
            {
                return holdingsAuthority != null
                    && equipmentCatalogProvider != null
                    && loadoutAuthority != null;
            }
        }

        private void Update()
        {
            bool keyboardBack = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame);
            bool gamepadBack = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (keyboardBack || gamepadBack)
            {
                Back();
                return;
            }

            bool keyboardConfirm = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
            bool gamepadConfirm = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (keyboardConfirm || gamepadConfirm) Confirm();
        }

        private void OnGUI()
        {
            EnsureStyles();
            int priorDepth = GUI.depth;
            GUI.depth = -900;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            float width = Mathf.Min(MaximumPanelWidth, Mathf.Max(460f, Screen.width - 24f));
            float height = Mathf.Min(MaximumPanelHeight, Mathf.Max(360f, Screen.height - 24f));
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(panel, GUI.skin.window);
            GUILayout.Label("INVENTORY / LOADOUT", titleStyle);
            if (incomingPayload != null)
            {
                GUILayout.Label(incomingPayload.SelectedCharacterStableId + "  /  " + incomingPayload.LoadoutProfileStableId, smallStyle);
            }
            GUILayout.Space(10f);
            if (service == null) DrawDisconnectedState();
            else DrawConnectedState();
            GUILayout.EndArea();
            GUI.depth = priorDepth;
        }

        public void Configure(
            IPlayerHoldingsAuthorityV1 holdingsAuthority,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            IInventoryLoadoutAuthorityPortV1 loadoutAuthority,
            Action<PlayerRouteProfilePayloadV1> returnToHub)
        {
            this.holdingsAuthority = holdingsAuthority ?? throw new ArgumentNullException(nameof(holdingsAuthority));
            this.equipmentCatalogProvider = equipmentCatalogProvider ?? throw new ArgumentNullException(nameof(equipmentCatalogProvider));
            this.loadoutAuthority = loadoutAuthority ?? throw new ArgumentNullException(nameof(loadoutAuthority));
            this.returnToHub = returnToHub;
            if (incomingPayload != null) BuildService(incomingPayload);
        }

        public void ConfigureForTests(
            IPlayerHoldingsAuthorityV1 holdingsAuthority,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            IInventoryLoadoutAuthorityPortV1 loadoutAuthority,
            Action<PlayerRouteProfilePayloadV1> returnToHub)
        {
            Configure(holdingsAuthority, equipmentCatalogProvider, loadoutAuthority, returnToHub);
        }

        public void Present(HubRouteV1 route, PlayerRouteProfilePayloadV1 payload)
        {
            if (route != HubRouteV1.Inventory)
            {
                throw new ArgumentOutOfRangeException(nameof(route), "The Inventory/Loadout screen presents only the HUB Inventory route.");
            }
            incomingPayload = payload ?? throw new ArgumentNullException(nameof(payload));
            if (!payload.HasValidFingerprint()) throw new ArgumentException("The presented HUB route payload fingerprint is invalid.", nameof(payload));
            returnDispatched = false;
            LastReturnedPayload = null;
            ReturnCount = 0;
            lastResult = null;
            activeSlotStableId = InventoryLoadoutSlotIdsV1.WeaponOne;
            service = IsConfigured ? new InventoryLoadoutScreenServiceV1(payload, holdingsAuthority, equipmentCatalogProvider, loadoutAuthority) : null;
        }

        public bool SelectSlot(StableId slotStableId)
        {
            InventoryLoadoutSlotDescriptorV1 descriptor;
            if (!InventoryLoadoutSlotsV1.TryFind(slotStableId, out descriptor)) return false;
            if (activeSlotStableId == descriptor.SlotStableId) return false;
            activeSlotStableId = descriptor.SlotStableId;
            return true;
        }

        public bool SelectSlotByIndex(int index)
        {
            if (index < 0 || index >= InventoryLoadoutSlotsV1.All.Count) return false;
            return SelectSlot(InventoryLoadoutSlotsV1.All[index].SlotStableId);
        }

        public InventoryLoadoutScreenResultV1 SelectInstance(StableId equipmentInstanceStableId)
        {
            if (service == null) return null;
            lastResult = service.TrySelect(activeSlotStableId, equipmentInstanceStableId);
            return lastResult;
        }

        public InventoryLoadoutScreenResultV1 UnequipActiveSlot()
        {
            if (service == null) return null;
            lastResult = service.TryUnequip(activeSlotStableId);
            return lastResult;
        }

        public InventoryLoadoutScreenResultV1 Refresh()
        {
            if (service == null) return null;
            lastResult = service.Refresh();
            return lastResult;
        }

        public InventoryLoadoutScreenResultV1 Confirm()
        {
            if (service == null) return null;
            lastResult = service.Confirm();
            if (lastResult.Status == InventoryLoadoutScreenStatusV1.Confirmed) DispatchReturn(lastResult.RoutePayload);
            return lastResult;
        }

        public InventoryLoadoutScreenResultV1 Back()
        {
            if (service == null)
            {
                if (incomingPayload != null && !returnDispatched) DispatchReturn(incomingPayload);
                return null;
            }
            lastResult = service.Back();
            if (lastResult.Status == InventoryLoadoutScreenStatusV1.Cancelled) DispatchReturn(lastResult.RoutePayload);
            return lastResult;
        }

        private void BuildService(PlayerRouteProfilePayloadV1 payload)
        {
            service = new InventoryLoadoutScreenServiceV1(payload, holdingsAuthority, equipmentCatalogProvider, loadoutAuthority);
        }

        private void DispatchReturn(PlayerRouteProfilePayloadV1 payload)
        {
            if (returnDispatched || payload == null) return;
            returnDispatched = true;
            LastReturnedPayload = payload;
            ReturnCount++;
            if (returnToHub != null) returnToHub(payload);
        }

        private void DrawDisconnectedState()
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("AWAITING HUB COMPOSITION", headingStyle);
            GUILayout.Label(
                "This standalone scene contains no fallback inventory or loadout truth. A composition root must provide INV-001 holdings, the equipment catalog, and the loadout authority adapter.",
                bodyStyle);
            GUILayout.Space(18f);
            if (GUILayout.Button("BACK TO HUB", GUILayout.MinHeight(46f))) Back();
            GUILayout.FlexibleSpace();
        }

        private void DrawConnectedState()
        {
            InventoryLoadoutScreenSnapshotV1 current = service.Snapshot;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Min(360f, Screen.width * 0.34f)));
            GUILayout.Label("LOADOUT SLOTS", headingStyle);
            slotScroll = GUILayout.BeginScrollView(slotScroll);
            for (int index = 0; index < current.Selections.Count; index++) DrawSlot(current.Selections[index]);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(12f);
            GUILayout.BeginVertical();
            GUILayout.Label("OWNED EQUIPMENT INSTANCES", headingStyle);
            equipmentScroll = GUILayout.BeginScrollView(equipmentScroll);
            InventoryLoadoutSlotDescriptorV1 activeSlot;
            InventoryLoadoutSlotsV1.TryFind(activeSlotStableId, out activeSlot);
            for (int index = 0; index < current.Equipment.Count; index++)
            {
                InventoryLoadoutEquipmentProjectionV1 equipment = current.Equipment[index];
                if (activeSlot != null && equipment.SlotKind.HasValue && equipment.SlotKind.Value != activeSlot.Kind) continue;
                DrawEquipment(equipment);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("REFRESH HOLDINGS", GUILayout.MinHeight(42f))) Refresh();
            if (GUILayout.Button("UNEQUIP SLOT", GUILayout.MinHeight(42f))) UnequipActiveSlot();
            GUI.enabled = current.CanConfirm;
            if (GUILayout.Button("CONFIRM", GUILayout.MinHeight(42f))) Confirm();
            GUI.enabled = true;
            if (GUILayout.Button("BACK", GUILayout.MinHeight(42f))) Back();
            GUILayout.EndHorizontal();
            if (lastResult != null && !string.IsNullOrEmpty(lastResult.RejectionCode)) GUILayout.Label(lastResult.RejectionCode, invalidStyle);
            GUILayout.Label("Holdings sequence " + current.HoldingsSequence + "  •  Loadout sequence " + current.LoadoutSequence, smallStyle);
        }

        private void DrawSlot(InventoryLoadoutSelectionProjectionV1 selection)
        {
            bool active = selection.Slot.SlotStableId == activeSlotStableId;
            string equipmentText = selection.EquipmentInstanceStableId == null ? "UNEQUIPPED" : ShortIdentity(selection.EquipmentInstanceStableId);
            string label = (active ? "▶ " : string.Empty) + selection.Slot.DisplayName + "\n" + equipmentText;
            if (GUILayout.Button(label, GUILayout.MinHeight(54f))) SelectSlot(selection.Slot.SlotStableId);
            if (!selection.IsValid) GUILayout.Label(selection.RejectionCode, invalidStyle);
        }

        private void DrawEquipment(InventoryLoadoutEquipmentProjectionV1 equipment)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(equipment.DisplayName, headingStyle);
            GUILayout.Label("Level " + equipment.ItemLevel + "  •  " + equipment.DefinitionStableId, smallStyle);
            GUILayout.Label("Instance: " + equipment.InstanceStableId, smallStyle);
            if (!equipment.IsSelectable) GUILayout.Label(equipment.RejectionCode, invalidStyle);
            GUI.enabled = equipment.IsSelectable;
            if (GUILayout.Button("EQUIP THIS INSTANCE", GUILayout.MinHeight(36f))) SelectInstance(equipment.InstanceStableId);
            GUI.enabled = true;
            GUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 30, fontStyle = FontStyle.Bold, wordWrap = true };
            headingStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 17, fontStyle = FontStyle.Bold, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, wordWrap = true };
            invalidStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Italic, wordWrap = true };
        }

        private static string ShortIdentity(StableId stableId)
        {
            string text = stableId == null ? string.Empty : stableId.ToString();
            return text.Length <= 24 ? text : "…" + text.Substring(text.Length - 23);
        }
    }
}

using System;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.InventoryLoadout
{
    [DisallowMultipleComponent]
    public sealed class InventoryMenu :
        MonoBehaviour,
        IHubRouteDestinationBridge
    {
        private GunInventoryState canonicalGunInventory;
        private GunSlots canonicalMountLayout;
        private GunCatalog canonicalGunCatalog;
        private GeneratedEquipmentAugmentSignatureState augmentSignatures;
        private InventoryMenuActions canonicalService;
        private Action<PlayerRouteProfilePayload> returnToHub;
        private PlayerRouteProfilePayload incomingPayload;
        private InventoryLoadoutScreenResult lastResult;
        private StableId activeSlotStableId;
        private bool returnDispatched;
        private Vector2 equipmentScroll;
        private Vector2 slotScroll;
        private Vector2 selectedScroll;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle invalidStyle;

        public event Action<PlayerRouteProfilePayload> Confirmed;

        public InventoryMenuState Snapshot
        {
            get { return canonicalService == null ? null : canonicalService.Snapshot; }
        }

        public InventoryMenuState CanonicalSnapshot
        {
            get { return Snapshot; }
        }

        public InventoryLoadoutScreenResult LastResult
        {
            get { return lastResult; }
        }

        public PlayerRouteProfilePayload IncomingPayload
        {
            get { return incomingPayload; }
        }

        public PlayerRouteProfilePayload LastReturnedPayload
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
                return canonicalGunInventory != null
                    && canonicalMountLayout != null
                    && canonicalGunCatalog != null;
            }
        }

        public void ConnectCanonicalAuthorities(
            GunInventoryState gunHoldings,
            GunSlots mountLayout,
            GunCatalog gunCatalog)
        {
            canonicalGunInventory = gunHoldings
                ?? throw new ArgumentNullException(nameof(gunHoldings));
            canonicalMountLayout = mountLayout
                ?? throw new ArgumentNullException(nameof(mountLayout));
            canonicalGunCatalog = gunCatalog
                ?? throw new ArgumentNullException(nameof(gunCatalog));
            canonicalService = null;
            if (incomingPayload != null)
            {
                BuildService(incomingPayload);
            }
        }

        public void ConfigureGunPresentation(
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog)
        {
            if (canonicalGunCatalog == null)
            {
                canonicalGunCatalog = gunCatalog;
            }
        }

        public void ConfigureAugmentPresentation(
            GeneratedEquipmentAugmentSignatureState signatures)
        {
            augmentSignatures = signatures;
        }

        public void ConfigureDisconnected(
            Action<PlayerRouteProfilePayload> returnToHub)
        {
            canonicalGunInventory = null;
            canonicalMountLayout = null;
            canonicalGunCatalog = null;
            augmentSignatures = null;
            canonicalService = null;
            this.returnToHub = returnToHub
                ?? throw new ArgumentNullException(nameof(returnToHub));
        }

        public void ConfigureForTests(
            GunInventoryState gunHoldings,
            GunSlots mountLayout,
            GunCatalog gunCatalog,
            Action<PlayerRouteProfilePayload> returnToHub)
        {
            ConnectCanonicalAuthorities(
                gunHoldings,
                mountLayout,
                gunCatalog);
            this.returnToHub = returnToHub;
        }

        public void Present(
            HubRoute route,
            PlayerRouteProfilePayload payload)
        {
            if (route != HubRoute.Inventory)
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
            GunSlots layout =
                GunMountPolicy.ResolveLayout(
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
            GunSlots layout =
                GunMountPolicy.ResolveLayout(
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
            GunSlots layout =
                GunMountPolicy.ResolveLayout(
                    incomingPayload.LoadoutProfileStableId);
            if (index < 0 || index >= layout.Positions.Count)
            {
                return false;
            }
            return SelectSlot(layout.Positions[index].LoadoutSlotStableId);
        }

        public InventoryLoadoutScreenResult SelectInstance(
            StableId equipmentInstanceStableId)
        {
            if (canonicalService == null)
            {
                return null;
            }
            lastResult = canonicalService.SelectGun(
                equipmentInstanceStableId);
            if (lastResult.Status
                != InventoryLoadoutScreenStatus.SelectionChanged
                && lastResult.Status
                    != InventoryLoadoutScreenStatus.NoChange)
            {
                return lastResult;
            }
            lastResult = canonicalService.EquipSelected(
                activeSlotStableId);
            return lastResult;
        }

        public InventoryLoadoutScreenResult UnequipActiveSlot()
        {
            if (canonicalService == null)
            {
                return null;
            }
            lastResult = canonicalService.Unequip(activeSlotStableId);
            return lastResult;
        }

        public InventoryLoadoutScreenResult Refresh()
        {
            if (canonicalService == null)
            {
                return null;
            }
            lastResult = canonicalService.Refresh();
            return lastResult;
        }

        public InventoryLoadoutScreenResult Confirm()
        {
            if (canonicalService == null)
            {
                return null;
            }
            lastResult = canonicalService.Confirm();
            if (lastResult.Status
                == InventoryLoadoutScreenStatus.Confirmed)
            {
                Action<PlayerRouteProfilePayload> handler = Confirmed;
                if (handler != null)
                {
                    handler(lastResult.RoutePayload);
                }
                DispatchReturn(lastResult.RoutePayload);
            }
            return lastResult;
        }

        public InventoryLoadoutScreenResult Back()
        {
            if (canonicalService == null)
            {
                DispatchReturn(incomingPayload);
                return null;
            }

            lastResult = canonicalService.Back();
            if (lastResult.Status
                == InventoryLoadoutScreenStatus.Cancelled)
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
                "AWAITING CANONICAL GUN AUTHORITIES",
                headingStyle);
            GUILayout.Label(
                "No fallback holdings, starter grant, or fixed-slot loadout is created.",
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
            InventoryMenuState current =
                canonicalService.Snapshot;
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(340f));
            GUILayout.Label("EQUIPPED GUNS", headingStyle);
            slotScroll = GUILayout.BeginScrollView(slotScroll);
            for (int index = 0; index < current.Mounts.Count; index++)
            {
                DrawCanonicalMount(current.Mounts[index]);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(10f);
            GUILayout.BeginVertical(GUILayout.Width(420f));
            GUILayout.Label("OWNED GUNS", headingStyle);
            equipmentScroll = GUILayout.BeginScrollView(equipmentScroll);
            for (int index = 0;
                 index < current.OwnedGuns.Count;
                 index++)
            {
                DrawCanonicalOwnedGun(current.OwnedGuns[index]);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(10f);
            GUILayout.BeginVertical();
            GUILayout.Label("SELECTED GUN", headingStyle);
            selectedScroll = GUILayout.BeginScrollView(selectedScroll);
            DrawSelectedGun(current.SelectedGun, current);
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
                "Gun holdings sequence "
                + current.GunInventorySequence
                + "  •  Loadout sequence "
                + current.LoadoutSequence,
                smallStyle);
        }

        private void DrawCanonicalMount(
            GunInventoryMount mount)
        {
            bool selected = mount.Position.LoadoutSlotStableId
                == activeSlotStableId;
            bool locked = mount.Position.Availability
                != GunMountAvailability.Active;
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

        private void DrawCanonicalOwnedGun(
            GunInventoryCard card)
        {
            bool selected = canonicalService.Snapshot.SelectedInstanceId
                == card.Instance.InstanceId;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(
                (selected ? "▶ " : string.Empty) + card.DisplayName,
                headingStyle);
            GUILayout.Label(
                card.Instance.GunDefinitionId.Value
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
            GeneratedEquipmentAugmentSignature augmentRoll;
            if (TryGetAugmentRoll(
                    card.Instance.InstanceId,
                    out augmentRoll))
            {
                GUILayout.Label(
                    "Augment slots "
                    + FormatSlotMeter(augmentRoll.Capacity)
                    + "  " + augmentRoll.Capacity + "/4"
                    + "    Level " + augmentRoll.SharedLevel,
                    smallStyle);
            }
            if (GUILayout.Button(
                "SELECT THIS INSTANCE",
                GUILayout.MinHeight(34f)))
            {
                lastResult = canonicalService.SelectGun(
                    card.Instance.InstanceId);
            }
            GUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private void DrawSelectedGun(
            GunInventoryCard card,
            InventoryMenuState snapshot)
        {
            if (card == null)
            {
                GUILayout.Label("No gun selected.", bodyStyle);
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(card.DisplayName, headingStyle);
            GUILayout.Label(
                "Definition: " + card.Instance.GunDefinitionId.Value,
                bodyStyle);
            GUILayout.Label(
                "Family: "
                + (string.IsNullOrEmpty(card.Family)
                    ? "Unresolved"
                    : card.Family),
                bodyStyle);
            GUILayout.Space(8f);
            GeneratedEquipmentAugmentSignature augmentRoll;
            if (TryGetAugmentRoll(
                    card.Instance.InstanceId,
                    out augmentRoll))
            {
                GUILayout.Label(
                    "AUGMENTS  "
                    + card.Instance.AugmentAssignments.Count
                    + "/" + augmentRoll.Capacity,
                    headingStyle);
                GUILayout.Label(
                    "SLOT CAPACITY  "
                    + FormatSlotMeter(augmentRoll.Capacity)
                    + "  " + augmentRoll.Capacity + "/4",
                    bodyStyle);
                GUILayout.Label(
                    "SHARED AUGMENT LEVEL  "
                    + augmentRoll.SharedLevel,
                    bodyStyle);
                DrawAugmentSlots(
                    card.Instance.AugmentAssignments,
                    augmentRoll);
            }
            else
            {
                GUILayout.Label("AUGMENTS", headingStyle);
                DrawAssignments(
                    card.Instance.AugmentAssignments,
                    "No augments assigned");
            }
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

            GunInventoryMount activeMount =
                snapshot.FindMount(activeSlotStableId);
            bool active = activeMount != null
                && activeMount.Position.Availability
                    == GunMountAvailability.Active;
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
                    != GunMountAvailability.Active)
            {
                GUILayout.Label(
                    "Equip rejected: this mount requires a skill.",
                    invalidStyle);
            }
        }

        private void DrawCanonicalSafety(GunItem instance)
        {
            if (instance == null)
            {
                return;
            }

            GunMark mark;
            bool definitionResolved = GunCatalogProvider.Current
                .TryGetMark(instance.GunDefinitionId.Value, out mark)
                && mark != null;
            GunOperationAvailability upgrade =
                GunSafetyPolicy.EvaluateGenericUpgrade(
                    true,
                    definitionResolved);
            GunOperationAvailability live =
                GunSafetyPolicy.EvaluateLiveExecution(
                    instance,
                    definitionResolved);
            GunOperationAvailability overclock =
                GunSafetyPolicy.EvaluateOverclockInstallation();

            GUILayout.Space(10f);
            GUILayout.Label("GUN SAFETY GATE", headingStyle);
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

        private bool TryGetAugmentRoll(
            StableId equipmentInstanceStableId,
            out GeneratedEquipmentAugmentSignature signature)
        {
            signature = null;
            return augmentSignatures != null
                && augmentSignatures.TryGet(
                    equipmentInstanceStableId,
                    out signature)
                && signature != null;
        }

        private static void DrawAugmentSlots(
            System.Collections.Generic.IReadOnlyList<StableId> assignments,
            GeneratedEquipmentAugmentSignature signature)
        {
            for (int index = 0; index < signature.Capacity; index++)
            {
                StableId assignment = assignments != null
                    && index < assignments.Count
                        ? assignments[index]
                        : null;
                GUILayout.Label(
                    "SLOT " + (index + 1)
                    + "  [LV " + signature.SharedLevel + "]  "
                    + (assignment == null
                        ? "EMPTY"
                        : assignment.ToString()));
            }
            if (signature.Capacity == 0)
            {
                GUILayout.Label("No augment slots rolled.");
            }
        }

        private static string FormatSlotMeter(int capacity)
        {
            string result = string.Empty;
            for (int index = 0; index < 4; index++)
            {
                result += index < capacity ? "[O]" : "[-]";
            }
            return result;
        }

        private void DrawLastDiagnostic()
        {
            if (lastResult != null
                && !string.IsNullOrEmpty(lastResult.RejectionCode))
            {
                GUILayout.Label(lastResult.RejectionCode, invalidStyle);
            }
        }

        private void BuildService(PlayerRouteProfilePayload payload)
        {
            canonicalService = null;
            if (canonicalGunInventory == null
                || canonicalMountLayout == null
                || canonicalGunCatalog == null)
            {
                return;
            }
            canonicalService = new InventoryMenuActions(
                payload,
                canonicalGunInventory,
                canonicalMountLayout,
                canonicalGunCatalog);
        }

        private void DispatchReturn(PlayerRouteProfilePayload payload)
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

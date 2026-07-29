using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    public sealed class WeaponInventoryCard
    {
        public WeaponInventoryCard(
            WeaponEquipmentInstance instance,
            string displayName,
            string family,
            bool isEquipped,
            StableId equippedMountId)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? instance.WeaponDefinitionId.Value
                : displayName.Trim();
            Family = family ?? string.Empty;
            IsEquipped = isEquipped;
            EquippedMountId = equippedMountId;
        }

        public WeaponEquipmentInstance Instance { get; }
        public string DisplayName { get; }
        public string Family { get; }
        public bool IsEquipped { get; }
        public StableId EquippedMountId { get; }
    }

    public sealed class WeaponInventoryMount
    {
        public WeaponInventoryMount(
            WeaponMountPosition position,
            StableId equippedInstanceId,
            WeaponInventoryCard equippedCard)
        {
            Position = position ?? throw new ArgumentNullException(nameof(position));
            EquippedInstanceId = equippedInstanceId;
            EquippedCard = equippedCard;
        }

        public WeaponMountPosition Position { get; }
        public StableId EquippedInstanceId { get; }
        public WeaponInventoryCard EquippedCard { get; }
    }

    public sealed class WeaponInventorySnapshot
    {
        private readonly ReadOnlyCollection<WeaponInventoryMount> mounts;
        private readonly ReadOnlyCollection<WeaponInventoryCard> owned;

        public WeaponInventorySnapshot(
            IEnumerable<WeaponInventoryMount> mounts,
            IEnumerable<WeaponInventoryCard> owned,
            StableId selectedInstanceId,
            long weaponHoldingsSequence,
            string weaponHoldingsFingerprint,
            long loadoutSequence,
            string loadoutFingerprint,
            bool canConfirm,
            bool isCompleted)
        {
            this.mounts = new ReadOnlyCollection<WeaponInventoryMount>(
                new List<WeaponInventoryMount>(
                    mounts ?? throw new ArgumentNullException(nameof(mounts))));
            this.owned = new ReadOnlyCollection<WeaponInventoryCard>(
                new List<WeaponInventoryCard>(
                    owned ?? throw new ArgumentNullException(nameof(owned))));
            SelectedInstanceId = selectedInstanceId;
            WeaponHoldingsSequence = weaponHoldingsSequence;
            WeaponHoldingsFingerprint = weaponHoldingsFingerprint ?? string.Empty;
            LoadoutSequence = loadoutSequence;
            LoadoutFingerprint = loadoutFingerprint ?? string.Empty;
            CanConfirm = canConfirm;
            IsCompleted = isCompleted;
        }

        public IReadOnlyList<WeaponInventoryMount> Mounts
        {
            get { return mounts; }
        }
        public IReadOnlyList<WeaponInventoryCard> OwnedWeapons
        {
            get { return owned; }
        }
        public StableId SelectedInstanceId { get; }
        public long WeaponHoldingsSequence { get; }
        public string WeaponHoldingsFingerprint { get; }
        public long LoadoutSequence { get; }
        public string LoadoutFingerprint { get; }
        public bool CanConfirm { get; }
        public bool IsCompleted { get; }

        public WeaponInventoryCard SelectedWeapon
        {
            get { return FindWeapon(SelectedInstanceId); }
        }

        public WeaponInventoryCard FindWeapon(StableId instanceId)
        {
            if (instanceId == null)
            {
                return null;
            }
            for (int index = 0; index < owned.Count; index++)
            {
                if (owned[index].Instance.InstanceId == instanceId)
                {
                    return owned[index];
                }
            }
            return null;
        }

        public WeaponInventoryMount FindMount(StableId loadoutSlotId)
        {
            if (loadoutSlotId == null)
            {
                return null;
            }
            for (int index = 0; index < mounts.Count; index++)
            {
                if (mounts[index].Position.LoadoutSlotStableId == loadoutSlotId)
                {
                    return mounts[index];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Exact weapon-only Inventory draft. Canonical physical mount state commits first and the old
    /// fixed-slot authority is updated only as a compatibility route/armor projection. Opening or
    /// refreshing this service never grants, repairs, recreates or auto-equips equipment.
    /// </summary>
    public sealed class WeaponInventoryScreenActions
    {
        private readonly PlayerRouteProfilePayload incomingRoute;
        private readonly IPlayerHoldingsState genericHoldings;
        private readonly WeaponHoldingsState weaponHoldings;
        private readonly WeaponMountLoadoutState mountLoadoutAuthority;
        private readonly InventoryLoadoutState loadoutAuthority;
        private readonly WeaponMountLayout layout;
        private readonly WeaponCatalog weaponCatalog;
        private readonly Dictionary<StableId, StableId> draftBindings =
            new Dictionary<StableId, StableId>();
        private WeaponInventorySnapshot snapshot;
        private StableId selectedInstanceId;
        private bool completed;

        public WeaponInventoryScreenActions(
            PlayerRouteProfilePayload incomingRoute,
            IPlayerHoldingsState genericHoldings,
            WeaponHoldingsState weaponHoldings,
            InventoryLoadoutState loadoutAuthority,
            WeaponMountLayout layout,
            WeaponCatalog weaponCatalog)
        {
            this.incomingRoute = incomingRoute
                ?? throw new ArgumentNullException(nameof(incomingRoute));
            if (!incomingRoute.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The incoming Inventory route payload is invalid.",
                    nameof(incomingRoute));
            }
            this.genericHoldings = genericHoldings
                ?? throw new ArgumentNullException(nameof(genericHoldings));
            this.weaponHoldings = weaponHoldings
                ?? throw new ArgumentNullException(nameof(weaponHoldings));
            this.loadoutAuthority = loadoutAuthority
                ?? throw new ArgumentNullException(nameof(loadoutAuthority));
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.weaponCatalog = weaponCatalog
                ?? throw new ArgumentNullException(nameof(weaponCatalog));

            WeaponMountLoadoutState resolvedMounts;
            if (!WeaponMountLoadoutRegistry.TryResolve(
                    weaponHoldings,
                    out resolvedMounts))
            {
                resolvedMounts = new WeaponMountLoadoutState(
                    layout,
                    weaponHoldings,
                    WeaponMountLoadoutView.MigrateLegacy(
                        layout,
                        weaponHoldings,
                        loadoutAuthority.ExportSnapshot()));
                WeaponMountLoadoutRegistry.Register(
                    weaponHoldings,
                    resolvedMounts);
            }
            mountLoadoutAuthority = resolvedMounts;

            InventoryLoadoutStateSnapshot compatibility =
                WeaponMountLoadoutView.ToLegacyProjection(
                    layout,
                    mountLoadoutAuthority.ExportSnapshot(),
                    loadoutAuthority.ExportSnapshot());
            for (int index = 0; index < compatibility.Bindings.Count; index++)
            {
                InventoryLoadoutSlotBinding binding = compatibility.Bindings[index];
                draftBindings.Add(
                    binding.SlotStableId,
                    binding.EquipmentInstanceStableId);
            }
            Rebuild();
        }

        public WeaponInventorySnapshot Snapshot
        {
            get { return snapshot; }
        }

        public InventoryLoadoutScreenSnapshot CompatibilitySnapshot
        {
            get { return BuildCompatibilitySnapshot(); }
        }

        public InventoryLoadoutScreenResult Refresh()
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            Rebuild();
            return Result(
                InventoryLoadoutScreenStatus.Refreshed,
                string.Empty);
        }

        public InventoryLoadoutScreenResult SelectWeapon(StableId instanceId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            if (instanceId == null || weaponHoldings.Find(instanceId) == null)
            {
                return Result(
                    InventoryLoadoutScreenStatus.MissingEquipment,
                    "inventory-loadout-instance-not-owned");
            }
            if (selectedInstanceId == instanceId)
            {
                return Result(
                    InventoryLoadoutScreenStatus.NoChange,
                    "inventory-loadout-selection-already-current");
            }
            selectedInstanceId = instanceId;
            Rebuild();
            return Result(
                InventoryLoadoutScreenStatus.SelectionChanged,
                string.Empty);
        }

        public InventoryLoadoutScreenResult EquipSelected(
            StableId targetLoadoutSlotId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            WeaponMountPosition mount = FindPhysicalMount(
                targetLoadoutSlotId);
            if (mount == null)
            {
                return Result(
                    InventoryLoadoutScreenStatus.InvalidSlot,
                    "inventory-loadout-mount-not-owned-by-class");
            }
            if (!mount.IsActive)
            {
                return Result(
                    InventoryLoadoutScreenStatus.InvalidSlot,
                    string.IsNullOrWhiteSpace(mount.LockReason)
                        ? "inventory-loadout-mount-locked-by-skill"
                        : mount.LockReason);
            }
            if (selectedInstanceId == null
                || weaponHoldings.Find(selectedInstanceId) == null)
            {
                return Result(
                    InventoryLoadoutScreenStatus.MissingEquipment,
                    "inventory-loadout-instance-not-owned");
            }

            for (int index = 0; index < layout.Positions.Count; index++)
            {
                StableId otherSlot = layout.Positions[index].LoadoutSlotStableId;
                if (otherSlot != targetLoadoutSlotId
                    && draftBindings[otherSlot] == selectedInstanceId)
                {
                    return Result(
                        InventoryLoadoutScreenStatus
                            .DuplicateEquipmentInstance,
                        "inventory-loadout-instance-already-bound-elsewhere");
                }
            }

            StableId current = draftBindings[targetLoadoutSlotId];
            if (current == selectedInstanceId)
            {
                return Result(
                    InventoryLoadoutScreenStatus.NoChange,
                    "inventory-loadout-selection-already-current");
            }

            draftBindings[targetLoadoutSlotId] = selectedInstanceId;
            Rebuild();
            return Result(
                InventoryLoadoutScreenStatus.SelectionChanged,
                string.Empty);
        }

        public InventoryLoadoutScreenResult Unequip(
            StableId targetLoadoutSlotId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            WeaponMountPosition mount = FindPhysicalMount(
                targetLoadoutSlotId);
            if (mount == null)
            {
                return Result(
                    InventoryLoadoutScreenStatus.InvalidSlot,
                    "inventory-loadout-mount-not-owned-by-class");
            }
            if (!mount.IsActive)
            {
                return Result(
                    InventoryLoadoutScreenStatus.InvalidSlot,
                    string.IsNullOrWhiteSpace(mount.LockReason)
                        ? "inventory-loadout-mount-locked-by-skill"
                        : mount.LockReason);
            }
            if (draftBindings[targetLoadoutSlotId] == null)
            {
                return Result(
                    InventoryLoadoutScreenStatus.NoChange,
                    "inventory-loadout-slot-already-empty");
            }

            draftBindings[targetLoadoutSlotId] = null;
            Rebuild();
            return Result(
                InventoryLoadoutScreenStatus.SelectionChanged,
                string.Empty);
        }

        public InventoryLoadoutScreenResult Confirm()
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            Rebuild();
            if (!snapshot.CanConfirm)
            {
                return Result(
                    InventoryLoadoutScreenStatus.InvalidEquipment,
                    "inventory-loadout-draft-invalid");
            }

            WeaponHoldingsSnapshot holdingsBefore =
                weaponHoldings.ExportSnapshot();
            WeaponMountLoadoutSnapshot mountsBefore =
                mountLoadoutAuthority.ExportSnapshot();
            InventoryLoadoutStateSnapshot legacyBefore =
                loadoutAuthority.ExportSnapshot();

            WeaponMountLoadoutImportResult mountResult =
                mountLoadoutAuthority.Apply(
                    mountsBefore.Sequence,
                    BuildMountBindings());
            if (mountResult == null || !mountResult.Succeeded)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AuthorityRejected,
                    mountResult == null
                        ? "weapon-mount-loadout-authority-result-null"
                        : mountResult.RejectionCode);
            }

            IReadOnlyList<InventoryLoadoutSlotBinding> compatibilityBindings =
                WeaponMountLoadoutView.ToLegacyProjection(
                    layout,
                    mountResult.Snapshot,
                    legacyBefore).Bindings;
            var command = new InventoryLoadoutStateCommand(
                legacyBefore.Sequence,
                genericHoldings.Sequence,
                compatibilityBindings);
            InventoryLoadoutStateResult legacyResult =
                loadoutAuthority.Apply(command);
            if (legacyResult == null || !legacyResult.Succeeded)
            {
                RollBack(mountsBefore, legacyBefore);
                return Result(
                    InventoryLoadoutScreenStatus.AuthorityRejected,
                    legacyResult == null
                        ? "inventory-loadout-authority-result-null"
                        : legacyResult.RejectionCode);
            }

            WeaponHoldingsSnapshot holdingsAfter =
                weaponHoldings.ExportSnapshot();
            InventoryLoadoutStateSnapshot legacyAfter =
                legacyResult.Snapshot ?? loadoutAuthority.ExportSnapshot();
            if (holdingsAfter.Sequence != holdingsBefore.Sequence
                || !string.Equals(
                    holdingsAfter.Fingerprint,
                    holdingsBefore.Fingerprint,
                    StringComparison.Ordinal))
            {
                RollBack(mountsBefore, legacyBefore);
                return Result(
                    InventoryLoadoutScreenStatus
                        .HoldingsChangedDuringApply,
                    "inventory-loadout-authority-mutated-weapon-holdings");
            }
            if (!Matches(legacyAfter, compatibilityBindings))
            {
                RollBack(mountsBefore, legacyBefore);
                return Result(
                    InventoryLoadoutScreenStatus
                        .AuthoritySnapshotMismatch,
                    "inventory-loadout-authority-result-mismatch");
            }

            completed = true;
            Rebuild();
            WeaponMountLoadoutSnapshot mountsAfter =
                mountLoadoutAuthority.ExportSnapshot();
            return new InventoryLoadoutScreenResult(
                InventoryLoadoutScreenStatus.Confirmed,
                string.Empty,
                BuildCompatibilitySnapshot(),
                WeaponMountLoadoutView.Route(
                    incomingRoute.SelectedCharacterStableId,
                    incomingRoute.LoadoutProfileStableId,
                    layout,
                    mountsAfter));
        }

        public InventoryLoadoutScreenResult Back()
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            completed = true;
            Rebuild();
            return new InventoryLoadoutScreenResult(
                InventoryLoadoutScreenStatus.Cancelled,
                string.Empty,
                BuildCompatibilitySnapshot(),
                incomingRoute);
        }

        private void RollBack(
            WeaponMountLoadoutSnapshot mounts,
            InventoryLoadoutStateSnapshot legacy)
        {
            WeaponMountLoadoutImportResult mountRollback =
                mountLoadoutAuthority.ImportSnapshot(mounts);
            InventoryLoadoutImportResult legacyRollback =
                loadoutAuthority.ImportSnapshot(legacy);
            if (mountRollback == null
                || !mountRollback.Succeeded
                || legacyRollback == null
                || !legacyRollback.Succeeded)
            {
                throw new InvalidOperationException(
                    "Inventory loadout rollback failed.");
            }
        }

        private void Rebuild()
        {
            WeaponHoldingsSnapshot holdings =
                weaponHoldings.ExportSnapshot();
            var equippedMountByInstance = new Dictionary<StableId, StableId>();
            bool valid = true;

            for (int index = 0; index < layout.Positions.Count; index++)
            {
                WeaponMountPosition position =
                    layout.Positions[index];
                StableId instanceId =
                    draftBindings[position.LoadoutSlotStableId];
                if (!position.IsActive)
                {
                    if (instanceId != null)
                    {
                        valid = false;
                    }
                    continue;
                }
                if (instanceId == null)
                {
                    continue;
                }
                if (weaponHoldings.Find(instanceId) == null
                    || equippedMountByInstance.ContainsKey(instanceId))
                {
                    valid = false;
                    continue;
                }
                equippedMountByInstance.Add(
                    instanceId,
                    position.MountStableId);
            }

            var cards = new List<WeaponInventoryCard>(
                holdings.Instances.Count);
            for (int index = 0; index < holdings.Instances.Count; index++)
            {
                WeaponEquipmentInstance instance = holdings.Instances[index];
                WeaponDefinitionData definition;
                bool definitionResolved = weaponCatalog.TryGetDefinition(
                    instance.WeaponDefinitionId.Value,
                    out definition)
                    && definition != null;
                if (!definitionResolved)
                {
                    valid = false;
                }

                WeaponMark mark;
                bool markResolved = WeaponCatalogProvider.Current
                    .TryGetMark(instance.WeaponDefinitionId.Value, out mark)
                    && mark != null;
                StableId mountId;
                bool equipped = equippedMountByInstance.TryGetValue(
                    instance.InstanceId,
                    out mountId);
                cards.Add(new WeaponInventoryCard(
                    instance,
                    markResolved
                        ? mark.Blueprint.DisplayName
                        : instance.WeaponDefinitionId.Value,
                    markResolved
                        ? mark.Blueprint.WeaponFamily
                        : string.Empty,
                    equipped,
                    mountId));
            }

            var cardById = new Dictionary<StableId, WeaponInventoryCard>();
            for (int index = 0; index < cards.Count; index++)
            {
                cardById.Add(cards[index].Instance.InstanceId, cards[index]);
            }
            var mounts = new List<WeaponInventoryMount>(
                layout.Positions.Count);
            for (int index = 0; index < layout.Positions.Count; index++)
            {
                WeaponMountPosition position = layout.Positions[index];
                StableId instanceId = draftBindings[
                    position.LoadoutSlotStableId];
                WeaponInventoryCard card = null;
                if (instanceId != null)
                {
                    cardById.TryGetValue(instanceId, out card);
                }
                mounts.Add(new WeaponInventoryMount(
                    position,
                    instanceId,
                    card));
            }

            if (selectedInstanceId != null
                && weaponHoldings.Find(selectedInstanceId) == null)
            {
                selectedInstanceId = null;
            }
            if (selectedInstanceId == null && cards.Count > 0)
            {
                selectedInstanceId = cards[0].Instance.InstanceId;
            }

            WeaponMountLoadoutSnapshot authoritativeMounts =
                mountLoadoutAuthority.ExportSnapshot();
            snapshot = new WeaponInventorySnapshot(
                mounts,
                cards,
                selectedInstanceId,
                holdings.Sequence,
                holdings.Fingerprint,
                authoritativeMounts.Sequence,
                authoritativeMounts.Fingerprint,
                valid,
                completed);
        }

        private InventoryLoadoutScreenSnapshot BuildCompatibilitySnapshot()
        {
            var equipment = new List<InventoryLoadoutEquipmentView>(
                snapshot.OwnedWeapons.Count);
            for (int index = 0;
                 index < snapshot.OwnedWeapons.Count;
                 index++)
            {
                WeaponInventoryCard card =
                    snapshot.OwnedWeapons[index];
                equipment.Add(new InventoryLoadoutEquipmentView(
                    card.Instance.InstanceId,
                    StableId.Parse(card.Instance.WeaponDefinitionId.Value),
                    EquipmentCategoryIds.Weapon,
                    card.DisplayName,
                    0,
                    null,
                    card.Instance.InstanceId
                        + "|"
                        + card.Instance.WeaponDefinitionId.Value,
                    true,
                    string.Empty));
            }

            var selections = new List<InventoryLoadoutSelectionView>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlots.All.Count;
                 index++)
            {
                InventoryLoadoutSlotDescriptor slot =
                    InventoryLoadoutSlots.All[index];
                StableId selected = draftBindings[slot.SlotStableId];
                bool physical = slot.Kind != InventoryLoadoutSlotKind.Weapon
                    || FindPhysicalMount(slot.SlotStableId) != null;
                bool active = slot.Kind != InventoryLoadoutSlotKind.Weapon
                    || layout.ContainsLoadoutSlot(slot.SlotStableId);
                bool valid = physical
                    ? active || selected == null
                    : selected == null;
                selections.Add(new InventoryLoadoutSelectionView(
                    slot,
                    selected,
                    valid,
                    valid
                        ? string.Empty
                        : "inventory-loadout-slot-unavailable-for-profile"));
            }

            return new InventoryLoadoutScreenSnapshot(
                incomingRoute,
                snapshot.WeaponHoldingsSequence,
                snapshot.WeaponHoldingsFingerprint,
                snapshot.LoadoutSequence,
                snapshot.LoadoutFingerprint,
                equipment,
                selections,
                snapshot.CanConfirm,
                snapshot.IsCompleted);
        }

        private InventoryLoadoutScreenResult Result(
            InventoryLoadoutScreenStatus status,
            string rejectionCode)
        {
            return new InventoryLoadoutScreenResult(
                status,
                rejectionCode,
                BuildCompatibilitySnapshot(),
                null);
        }

        private WeaponMountPosition FindPhysicalMount(
            StableId slotId)
        {
            if (slotId == null)
            {
                return null;
            }
            for (int index = 0; index < layout.Positions.Count; index++)
            {
                if (layout.Positions[index].LoadoutSlotStableId == slotId)
                {
                    return layout.Positions[index];
                }
            }
            return null;
        }

        private IReadOnlyList<InventoryLoadoutSlotBinding> BuildBindings()
        {
            var bindings = new List<InventoryLoadoutSlotBinding>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlots.All.Count;
                 index++)
            {
                InventoryLoadoutSlotDescriptor slot =
                    InventoryLoadoutSlots.All[index];
                bindings.Add(new InventoryLoadoutSlotBinding(
                    slot.SlotStableId,
                    draftBindings[slot.SlotStableId]));
            }
            return bindings;
        }

        private IReadOnlyList<WeaponMountBinding> BuildMountBindings()
        {
            var bindings = new List<WeaponMountBinding>(
                layout.PhysicalPositions.Count);
            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                WeaponMountPosition position =
                    layout.PhysicalPositions[index];
                bindings.Add(new WeaponMountBinding(
                    position.MountStableId,
                    position.IsActive
                        ? draftBindings[position.LoadoutSlotStableId]
                        : null));
            }
            return bindings;
        }

        private static bool Matches(
            InventoryLoadoutStateSnapshot snapshot,
            IReadOnlyList<InventoryLoadoutSlotBinding> bindings)
        {
            if (snapshot == null
                || !snapshot.HasValidFingerprint()
                || snapshot.Bindings.Count != bindings.Count)
            {
                return false;
            }
            for (int index = 0; index < bindings.Count; index++)
            {
                if (!snapshot.Bindings[index].Equals(bindings[index]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}

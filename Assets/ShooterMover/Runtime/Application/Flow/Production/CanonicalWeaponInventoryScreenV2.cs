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
    public sealed class CanonicalWeaponInventoryCardV2
    {
        public CanonicalWeaponInventoryCardV2(
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

    public sealed class CanonicalWeaponInventoryMountV2
    {
        public CanonicalWeaponInventoryMountV2(
            ProductionWeaponMountPositionV1 position,
            StableId equippedInstanceId,
            CanonicalWeaponInventoryCardV2 equippedCard)
        {
            Position = position ?? throw new ArgumentNullException(nameof(position));
            EquippedInstanceId = equippedInstanceId;
            EquippedCard = equippedCard;
        }

        public ProductionWeaponMountPositionV1 Position { get; }
        public StableId EquippedInstanceId { get; }
        public CanonicalWeaponInventoryCardV2 EquippedCard { get; }
    }

    public sealed class CanonicalWeaponInventorySnapshotV2
    {
        private readonly ReadOnlyCollection<CanonicalWeaponInventoryMountV2> mounts;
        private readonly ReadOnlyCollection<CanonicalWeaponInventoryCardV2> owned;

        public CanonicalWeaponInventorySnapshotV2(
            IEnumerable<CanonicalWeaponInventoryMountV2> mounts,
            IEnumerable<CanonicalWeaponInventoryCardV2> owned,
            StableId selectedInstanceId,
            long weaponHoldingsSequence,
            string weaponHoldingsFingerprint,
            long loadoutSequence,
            string loadoutFingerprint,
            bool canConfirm,
            bool isCompleted)
        {
            this.mounts = new ReadOnlyCollection<CanonicalWeaponInventoryMountV2>(
                new List<CanonicalWeaponInventoryMountV2>(
                    mounts ?? throw new ArgumentNullException(nameof(mounts))));
            this.owned = new ReadOnlyCollection<CanonicalWeaponInventoryCardV2>(
                new List<CanonicalWeaponInventoryCardV2>(
                    owned ?? throw new ArgumentNullException(nameof(owned))));
            SelectedInstanceId = selectedInstanceId;
            WeaponHoldingsSequence = weaponHoldingsSequence;
            WeaponHoldingsFingerprint = weaponHoldingsFingerprint ?? string.Empty;
            LoadoutSequence = loadoutSequence;
            LoadoutFingerprint = loadoutFingerprint ?? string.Empty;
            CanConfirm = canConfirm;
            IsCompleted = isCompleted;
        }

        public IReadOnlyList<CanonicalWeaponInventoryMountV2> Mounts
        {
            get { return mounts; }
        }
        public IReadOnlyList<CanonicalWeaponInventoryCardV2> OwnedWeapons
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

        public CanonicalWeaponInventoryCardV2 SelectedWeapon
        {
            get { return FindWeapon(SelectedInstanceId); }
        }

        public CanonicalWeaponInventoryCardV2 FindWeapon(StableId instanceId)
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

        public CanonicalWeaponInventoryMountV2 FindMount(StableId loadoutSlotId)
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
    /// Exact weapon-only Inventory draft. Opening or refreshing this service never grants,
    /// repairs, recreates or auto-equips equipment.
    /// </summary>
    public sealed class CanonicalWeaponInventoryScreenServiceV2
    {
        private readonly PlayerRouteProfilePayloadV1 incomingRoute;
        private readonly IPlayerHoldingsAuthorityV1 genericHoldings;
        private readonly ProductionWeaponHoldingsAuthorityV2 weaponHoldings;
        private readonly ProductionInventoryLoadoutAuthorityV1 loadoutAuthority;
        private readonly ProductionWeaponMountLayoutV1 layout;
        private readonly WeaponCatalog weaponCatalog;
        private readonly Dictionary<StableId, StableId> draftBindings =
            new Dictionary<StableId, StableId>();
        private CanonicalWeaponInventorySnapshotV2 snapshot;
        private StableId selectedInstanceId;
        private bool completed;

        public CanonicalWeaponInventoryScreenServiceV2(
            PlayerRouteProfilePayloadV1 incomingRoute,
            IPlayerHoldingsAuthorityV1 genericHoldings,
            ProductionWeaponHoldingsAuthorityV2 weaponHoldings,
            ProductionInventoryLoadoutAuthorityV1 loadoutAuthority,
            ProductionWeaponMountLayoutV1 layout,
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

            InventoryLoadoutAuthoritySnapshotV1 authority =
                loadoutAuthority.ExportSnapshot();
            if (authority == null || !authority.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The Inventory loadout authority snapshot is invalid.",
                    nameof(loadoutAuthority));
            }
            for (int index = 0; index < authority.Bindings.Count; index++)
            {
                InventoryLoadoutSlotBindingV1 binding = authority.Bindings[index];
                draftBindings.Add(
                    binding.SlotStableId,
                    binding.EquipmentInstanceStableId);
            }
            Rebuild();
        }

        public CanonicalWeaponInventorySnapshotV2 Snapshot
        {
            get { return snapshot; }
        }

        public InventoryLoadoutScreenSnapshotV1 CompatibilitySnapshot
        {
            get { return BuildCompatibilitySnapshot(); }
        }

        public InventoryLoadoutScreenResultV1 Refresh()
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            Rebuild();
            return Result(
                InventoryLoadoutScreenStatusV1.Refreshed,
                string.Empty);
        }

        public InventoryLoadoutScreenResultV1 SelectWeapon(StableId instanceId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            if (instanceId == null || weaponHoldings.Find(instanceId) == null)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.MissingEquipment,
                    "inventory-loadout-instance-not-owned");
            }
            if (selectedInstanceId == instanceId)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.NoChange,
                    "inventory-loadout-selection-already-current");
            }
            selectedInstanceId = instanceId;
            Rebuild();
            return Result(
                InventoryLoadoutScreenStatusV1.SelectionChanged,
                string.Empty);
        }

        public InventoryLoadoutScreenResultV1 EquipSelected(
            StableId targetLoadoutSlotId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            ProductionWeaponMountPositionV1 mount = FindPhysicalMount(
                targetLoadoutSlotId);
            if (mount == null)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidSlot,
                    "inventory-loadout-mount-not-owned-by-class");
            }
            if (mount.Availability
                != ProductionWeaponMountAvailabilityV1.Active)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidSlot,
                    string.IsNullOrWhiteSpace(mount.LockReason)
                        ? "inventory-loadout-mount-locked-by-skill"
                        : mount.LockReason);
            }
            if (selectedInstanceId == null
                || weaponHoldings.Find(selectedInstanceId) == null)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.MissingEquipment,
                    "inventory-loadout-instance-not-owned");
            }

            foreach (KeyValuePair<StableId, StableId> pair in draftBindings)
            {
                if (pair.Key != targetLoadoutSlotId
                    && pair.Value == selectedInstanceId)
                {
                    return Result(
                        InventoryLoadoutScreenStatusV1
                            .DuplicateEquipmentInstance,
                        "inventory-loadout-instance-already-bound-elsewhere");
                }
            }

            StableId current = draftBindings[targetLoadoutSlotId];
            if (current == selectedInstanceId)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.NoChange,
                    "inventory-loadout-selection-already-current");
            }

            draftBindings[targetLoadoutSlotId] = selectedInstanceId;
            Rebuild();
            return Result(
                InventoryLoadoutScreenStatusV1.SelectionChanged,
                string.Empty);
        }

        public InventoryLoadoutScreenResultV1 Unequip(
            StableId targetLoadoutSlotId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            ProductionWeaponMountPositionV1 mount = FindPhysicalMount(
                targetLoadoutSlotId);
            if (mount == null)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidSlot,
                    "inventory-loadout-mount-not-owned-by-class");
            }
            if (mount.Availability
                != ProductionWeaponMountAvailabilityV1.Active)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidSlot,
                    string.IsNullOrWhiteSpace(mount.LockReason)
                        ? "inventory-loadout-mount-locked-by-skill"
                        : mount.LockReason);
            }
            if (draftBindings[targetLoadoutSlotId] == null)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.NoChange,
                    "inventory-loadout-slot-already-empty");
            }

            draftBindings[targetLoadoutSlotId] = null;
            Rebuild();
            return Result(
                InventoryLoadoutScreenStatusV1.SelectionChanged,
                string.Empty);
        }

        public InventoryLoadoutScreenResultV1 Confirm()
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            Rebuild();
            if (!snapshot.CanConfirm)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidEquipment,
                    "inventory-loadout-draft-invalid");
            }

            WeaponHoldingsSnapshotV2 holdingsBefore =
                weaponHoldings.ExportSnapshot();
            InventoryLoadoutAuthoritySnapshotV1 authorityBefore =
                loadoutAuthority.ExportSnapshot();
            var command = new InventoryLoadoutAuthorityCommandV1(
                authorityBefore.Sequence,
                genericHoldings.Sequence,
                BuildBindings());
            InventoryLoadoutAuthorityResultV1 result =
                loadoutAuthority.Apply(command);
            if (result == null || !result.Succeeded)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AuthorityRejected,
                    result == null
                        ? "inventory-loadout-authority-result-null"
                        : result.RejectionCode);
            }

            WeaponHoldingsSnapshotV2 holdingsAfter =
                weaponHoldings.ExportSnapshot();
            if (holdingsAfter.Sequence != holdingsBefore.Sequence
                || !string.Equals(
                    holdingsAfter.Fingerprint,
                    holdingsBefore.Fingerprint,
                    StringComparison.Ordinal))
            {
                return Result(
                    InventoryLoadoutScreenStatusV1
                        .HoldingsChangedDuringApply,
                    "inventory-loadout-authority-mutated-weapon-holdings");
            }

            InventoryLoadoutAuthoritySnapshotV1 authorityAfter =
                result.Snapshot ?? loadoutAuthority.ExportSnapshot();
            if (!Matches(authorityAfter, command.Bindings))
            {
                return Result(
                    InventoryLoadoutScreenStatusV1
                        .AuthoritySnapshotMismatch,
                    "inventory-loadout-authority-result-mismatch");
            }

            completed = true;
            Rebuild();
            return new InventoryLoadoutScreenResultV1(
                InventoryLoadoutScreenStatusV1.Confirmed,
                string.Empty,
                BuildCompatibilitySnapshot(),
                ProductionWeaponOnboardingV1.RouteFromLoadout(
                    incomingRoute.SelectedCharacterStableId,
                    incomingRoute.LoadoutProfileStableId,
                    authorityAfter));
        }

        public InventoryLoadoutScreenResultV1 Back()
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            completed = true;
            Rebuild();
            return new InventoryLoadoutScreenResultV1(
                InventoryLoadoutScreenStatusV1.Cancelled,
                string.Empty,
                BuildCompatibilitySnapshot(),
                incomingRoute);
        }

        private void Rebuild()
        {
            WeaponHoldingsSnapshotV2 holdings =
                weaponHoldings.ExportSnapshot();
            var equippedMountByInstance = new Dictionary<StableId, StableId>();
            bool valid = true;

            for (int index = 0; index < layout.Positions.Count; index++)
            {
                ProductionWeaponMountPositionV1 position =
                    layout.Positions[index];
                StableId instanceId =
                    draftBindings[position.LoadoutSlotStableId];
                if (position.Availability
                    != ProductionWeaponMountAvailabilityV1.Active)
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
                    position.LoadoutSlotStableId);
            }

            var cards = new List<CanonicalWeaponInventoryCardV2>(
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

                ProductionWeaponMarkV1 mark;
                bool markResolved = ProductionWeaponCatalogProvider.Current
                    .TryGetMark(instance.WeaponDefinitionId.Value, out mark)
                    && mark != null;
                StableId mountId;
                bool equipped = equippedMountByInstance.TryGetValue(
                    instance.InstanceId,
                    out mountId);
                cards.Add(new CanonicalWeaponInventoryCardV2(
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

            var cardById = new Dictionary<StableId, CanonicalWeaponInventoryCardV2>();
            for (int index = 0; index < cards.Count; index++)
            {
                cardById.Add(cards[index].Instance.InstanceId, cards[index]);
            }
            var mounts = new List<CanonicalWeaponInventoryMountV2>(
                layout.Positions.Count);
            for (int index = 0; index < layout.Positions.Count; index++)
            {
                ProductionWeaponMountPositionV1 position = layout.Positions[index];
                StableId instanceId = draftBindings[
                    position.LoadoutSlotStableId];
                CanonicalWeaponInventoryCardV2 card = null;
                if (instanceId != null)
                {
                    cardById.TryGetValue(instanceId, out card);
                }
                mounts.Add(new CanonicalWeaponInventoryMountV2(
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

            InventoryLoadoutAuthoritySnapshotV1 authority =
                loadoutAuthority.ExportSnapshot();
            snapshot = new CanonicalWeaponInventorySnapshotV2(
                mounts,
                cards,
                selectedInstanceId,
                holdings.Sequence,
                holdings.Fingerprint,
                authority.Sequence,
                authority.Fingerprint,
                valid,
                completed);
        }

        private InventoryLoadoutScreenSnapshotV1 BuildCompatibilitySnapshot()
        {
            var equipment = new List<InventoryLoadoutEquipmentProjectionV1>(
                snapshot.OwnedWeapons.Count);
            for (int index = 0;
                 index < snapshot.OwnedWeapons.Count;
                 index++)
            {
                CanonicalWeaponInventoryCardV2 card =
                    snapshot.OwnedWeapons[index];
                equipment.Add(new InventoryLoadoutEquipmentProjectionV1(
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

            var selections = new List<InventoryLoadoutSelectionProjectionV1>(
                InventoryLoadoutSlotsV1.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlotsV1.All.Count;
                 index++)
            {
                InventoryLoadoutSlotDescriptorV1 slot =
                    InventoryLoadoutSlotsV1.All[index];
                StableId selected = draftBindings[slot.SlotStableId];
                bool physical = slot.Kind != InventoryLoadoutSlotKindV1.Weapon
                    || FindPhysicalMount(slot.SlotStableId) != null;
                bool active = slot.Kind != InventoryLoadoutSlotKindV1.Weapon
                    || layout.ContainsLoadoutSlot(slot.SlotStableId);
                bool valid = physical
                    ? active || selected == null
                    : selected == null;
                selections.Add(new InventoryLoadoutSelectionProjectionV1(
                    slot,
                    selected,
                    valid,
                    valid
                        ? string.Empty
                        : "inventory-loadout-slot-unavailable-for-profile"));
            }

            return new InventoryLoadoutScreenSnapshotV1(
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

        private InventoryLoadoutScreenResultV1 Result(
            InventoryLoadoutScreenStatusV1 status,
            string rejectionCode)
        {
            return new InventoryLoadoutScreenResultV1(
                status,
                rejectionCode,
                BuildCompatibilitySnapshot(),
                null);
        }

        private ProductionWeaponMountPositionV1 FindPhysicalMount(
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

        private IReadOnlyList<InventoryLoadoutSlotBindingV1> BuildBindings()
        {
            var bindings = new List<InventoryLoadoutSlotBindingV1>(
                InventoryLoadoutSlotsV1.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlotsV1.All.Count;
                 index++)
            {
                InventoryLoadoutSlotDescriptorV1 slot =
                    InventoryLoadoutSlotsV1.All[index];
                bindings.Add(new InventoryLoadoutSlotBindingV1(
                    slot.SlotStableId,
                    draftBindings[slot.SlotStableId]));
            }
            return bindings;
        }

        private static bool Matches(
            InventoryLoadoutAuthoritySnapshotV1 snapshot,
            IReadOnlyList<InventoryLoadoutSlotBindingV1> bindings)
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

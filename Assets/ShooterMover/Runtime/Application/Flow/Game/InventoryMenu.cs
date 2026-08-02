using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Flow.Game
{
    public sealed class GunInventoryCard
    {
        public GunInventoryCard(
            GunItem instance,
            string displayName,
            string family,
            bool isEquipped,
            StableId equippedMountId)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? instance.GunDefinitionId.Value
                : displayName.Trim();
            Family = family ?? string.Empty;
            IsEquipped = isEquipped;
            EquippedMountId = equippedMountId;
        }

        public GunItem Instance { get; }
        public string DisplayName { get; }
        public string Family { get; }
        public bool IsEquipped { get; }
        public StableId EquippedMountId { get; }
    }

    public sealed class GunInventoryMount
    {
        public GunInventoryMount(
            GunSlot position,
            StableId equippedInstanceId,
            GunInventoryCard equippedCard)
        {
            Position = position ?? throw new ArgumentNullException(nameof(position));
            EquippedInstanceId = equippedInstanceId;
            EquippedCard = equippedCard;
        }

        public GunSlot Position { get; }
        public StableId EquippedInstanceId { get; }
        public GunInventoryCard EquippedCard { get; }
    }

    public sealed class InventoryMenuState
    {
        private readonly ReadOnlyCollection<GunInventoryMount> mounts;
        private readonly ReadOnlyCollection<GunInventoryCard> owned;

        public InventoryMenuState(
            IEnumerable<GunInventoryMount> mounts,
            IEnumerable<GunInventoryCard> owned,
            StableId selectedInstanceId,
            long gunHoldingsSequence,
            string gunHoldingsFingerprint,
            long loadoutSequence,
            string loadoutFingerprint,
            bool canConfirm,
            bool isCompleted)
        {
            this.mounts = new ReadOnlyCollection<GunInventoryMount>(
                new List<GunInventoryMount>(
                    mounts ?? throw new ArgumentNullException(nameof(mounts))));
            this.owned = new ReadOnlyCollection<GunInventoryCard>(
                new List<GunInventoryCard>(
                    owned ?? throw new ArgumentNullException(nameof(owned))));
            SelectedInstanceId = selectedInstanceId;
            GunInventorySequence = gunHoldingsSequence;
            GunInventoryFingerprint = gunHoldingsFingerprint ?? string.Empty;
            LoadoutSequence = loadoutSequence;
            LoadoutFingerprint = loadoutFingerprint ?? string.Empty;
            CanConfirm = canConfirm;
            IsCompleted = isCompleted;
        }

        public IReadOnlyList<GunInventoryMount> Mounts
        {
            get { return mounts; }
        }

        public IReadOnlyList<GunInventoryCard> OwnedGuns
        {
            get { return owned; }
        }

        public StableId SelectedInstanceId { get; }
        public long GunInventorySequence { get; }
        public string GunInventoryFingerprint { get; }
        public long LoadoutSequence { get; }
        public string LoadoutFingerprint { get; }
        public bool CanConfirm { get; }
        public bool IsCompleted { get; }

        public GunInventoryCard SelectedGun
        {
            get { return FindGun(SelectedInstanceId); }
        }

        public GunInventoryCard FindGun(StableId instanceId)
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

        public GunInventoryMount FindMount(StableId loadoutSlotId)
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
    /// Exact gun-only Inventory draft. GunInventoryState is the ownership authority and
    /// LoadoutState is the sole equipped-state authority. Opening or refreshing this service
    /// never grants, repairs, recreates, migrates, or projects another loadout model.
    /// </summary>
    public sealed class InventoryMenuActions
    {
        private readonly PlayerRouteProfilePayload incomingRoute;
        private readonly GunInventoryState gunHoldings;
        private readonly LoadoutState mountLoadoutAuthority;
        private readonly GunSlots layout;
        private readonly GunCatalog gunCatalog;
        private readonly Dictionary<StableId, StableId> draftBindings =
            new Dictionary<StableId, StableId>();
        private InventoryMenuState snapshot;
        private StableId selectedInstanceId;
        private bool completed;

        public InventoryMenuActions(
            PlayerRouteProfilePayload incomingRoute,
            GunInventoryState gunHoldings,
            GunSlots layout,
            GunCatalog gunCatalog)
        {
            this.incomingRoute = incomingRoute
                ?? throw new ArgumentNullException(nameof(incomingRoute));
            if (!incomingRoute.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The incoming Inventory route payload is invalid.",
                    nameof(incomingRoute));
            }
            this.gunHoldings = gunHoldings
                ?? throw new ArgumentNullException(nameof(gunHoldings));
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.gunCatalog = gunCatalog
                ?? throw new ArgumentNullException(nameof(gunCatalog));

            GunSlots routeLayout = GunMountPolicy.ResolveLayout(
                incomingRoute.LoadoutProfileStableId);
            if (!LayoutsMatch(routeLayout, layout))
            {
                throw new InvalidOperationException(
                    "Inventory mount layout does not match the selected character class: "
                    + incomingRoute.LoadoutProfileStableId);
            }

            LoadoutState resolvedMounts;
            if (!LoadoutRegistry.TryResolve(
                    gunHoldings,
                    out resolvedMounts)
                || resolvedMounts == null)
            {
                throw new InvalidOperationException(
                    "The selected character's canonical gun mount authority is unavailable. "
                    + "Inventory will not create or migrate a replacement authority.");
            }
            mountLoadoutAuthority = resolvedMounts;

            LoadoutSnapshot authoritative =
                mountLoadoutAuthority.ExportSnapshot();
            ValidateAuthoritativeMounts(authoritative);
            for (int index = 0; index < layout.Positions.Count; index++)
            {
                GunSlot position = layout.Positions[index];
                EquippedGun binding = authoritative.Find(
                    position.MountStableId);
                draftBindings.Add(
                    position.LoadoutSlotStableId,
                    position.IsActive ? binding.InstanceId : null);
            }
            Rebuild();
        }

        public InventoryMenuState Snapshot
        {
            get { return snapshot; }
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

        public InventoryLoadoutScreenResult SelectGun(StableId instanceId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            if (instanceId == null || gunHoldings.Find(instanceId) == null)
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
            GunSlot mount = FindPhysicalMount(targetLoadoutSlotId);
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
                    "inventory-loadout-mount-locked-by-skill");
            }
            if (selectedInstanceId == null
                || gunHoldings.Find(selectedInstanceId) == null)
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
                        InventoryLoadoutScreenStatus.DuplicateEquipmentInstance,
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
            GunSlot mount = FindPhysicalMount(targetLoadoutSlotId);
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
                    "inventory-loadout-mount-locked-by-skill");
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

            GunInventorySnapshot holdingsBefore =
                gunHoldings.ExportSnapshot();
            LoadoutSnapshot mountsBefore =
                mountLoadoutAuthority.ExportSnapshot();
            IReadOnlyList<EquippedGun> requestedBindings =
                BuildMountBindings();

            LoadoutImportResult mountResult =
                mountLoadoutAuthority.Apply(
                    mountsBefore.Sequence,
                    requestedBindings);
            if (mountResult == null || !mountResult.Succeeded)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AuthorityRejected,
                    mountResult == null
                        ? "gun-mount-loadout-authority-result-null"
                        : mountResult.RejectionCode);
            }

            GunInventorySnapshot holdingsAfter =
                gunHoldings.ExportSnapshot();
            if (holdingsAfter.Sequence != holdingsBefore.Sequence
                || !string.Equals(
                    holdingsAfter.Fingerprint,
                    holdingsBefore.Fingerprint,
                    StringComparison.Ordinal))
            {
                RollBack(mountsBefore);
                return Result(
                    InventoryLoadoutScreenStatus.HoldingsChangedDuringApply,
                    "inventory-loadout-authority-mutated-gun-holdings");
            }

            LoadoutSnapshot mountsAfter = mountResult.Snapshot
                ?? mountLoadoutAuthority.ExportSnapshot();
            if (!MatchesMountBindings(mountsAfter, requestedBindings))
            {
                RollBack(mountsBefore);
                return Result(
                    InventoryLoadoutScreenStatus.AuthorityRejected,
                    "gun-mount-loadout-authority-result-mismatch");
            }

            completed = true;
            Rebuild();
            return new InventoryLoadoutScreenResult(
                InventoryLoadoutScreenStatus.Confirmed,
                string.Empty,
                snapshot,
                LoadoutView.Route(
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
                snapshot,
                incomingRoute);
        }

        private void ValidateAuthoritativeMounts(
            LoadoutSnapshot authoritative)
        {
            if (authoritative == null
                || !authoritative.HasValidFingerprint()
                || authoritative.Bindings.Count
                    != layout.PhysicalPositions.Count)
            {
                throw new InvalidOperationException(
                    "The selected character's canonical gun mount snapshot is invalid.");
            }

            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                GunSlot position = layout.PhysicalPositions[index];
                EquippedGun binding = authoritative.Find(
                    position.MountStableId);
                if (binding == null)
                {
                    throw new InvalidOperationException(
                        "The selected character's canonical loadout is missing mount: "
                        + position.MountStableId);
                }
                if (position.IsLockedBySkill && binding.InstanceId != null)
                {
                    throw new InvalidOperationException(
                        "A skill-locked gun mount is bound: "
                        + position.MountStableId);
                }
            }
        }

        private void RollBack(LoadoutSnapshot mounts)
        {
            LoadoutImportResult rollback =
                mountLoadoutAuthority.ImportSnapshot(mounts);
            if (rollback == null || !rollback.Succeeded)
            {
                throw new InvalidOperationException(
                    "Inventory gun mount rollback failed.");
            }
        }

        private void Rebuild()
        {
            GunInventorySnapshot holdings =
                gunHoldings.ExportSnapshot();
            var equippedMountByInstance = new Dictionary<StableId, StableId>();
            bool valid = true;

            for (int index = 0; index < layout.Positions.Count; index++)
            {
                GunSlot position = layout.Positions[index];
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
                if (gunHoldings.Find(instanceId) == null
                    || equippedMountByInstance.ContainsKey(instanceId))
                {
                    valid = false;
                    continue;
                }
                equippedMountByInstance.Add(
                    instanceId,
                    position.MountStableId);
            }

            var cards = new List<GunInventoryCard>(
                holdings.Instances.Count);
            for (int index = 0; index < holdings.Instances.Count; index++)
            {
                GunItem instance = holdings.Instances[index];
                GunDefinitionData definition;
                bool definitionResolved = gunCatalog.TryGetDefinition(
                    instance.GunDefinitionId.Value,
                    out definition)
                    && definition != null;
                if (!definitionResolved)
                {
                    valid = false;
                }

                GunMark mark;
                bool markResolved = GunCatalogProvider.Current
                    .TryGetMark(instance.GunDefinitionId.Value, out mark)
                    && mark != null;
                StableId mountId;
                bool equipped = equippedMountByInstance.TryGetValue(
                    instance.InstanceId,
                    out mountId);
                cards.Add(new GunInventoryCard(
                    instance,
                    markResolved
                        ? mark.Blueprint.DisplayName
                        : instance.GunDefinitionId.Value,
                    markResolved
                        ? mark.Blueprint.GunFamily
                        : string.Empty,
                    equipped,
                    mountId));
            }

            var cardById = new Dictionary<StableId, GunInventoryCard>();
            for (int index = 0; index < cards.Count; index++)
            {
                cardById.Add(cards[index].Instance.InstanceId, cards[index]);
            }
            var mounts = new List<GunInventoryMount>(
                layout.Positions.Count);
            for (int index = 0; index < layout.Positions.Count; index++)
            {
                GunSlot position = layout.Positions[index];
                StableId instanceId = draftBindings[
                    position.LoadoutSlotStableId];
                GunInventoryCard card = null;
                if (instanceId != null)
                {
                    cardById.TryGetValue(instanceId, out card);
                }
                mounts.Add(new GunInventoryMount(
                    position,
                    instanceId,
                    card));
            }

            if (selectedInstanceId != null
                && gunHoldings.Find(selectedInstanceId) == null)
            {
                selectedInstanceId = null;
            }
            if (selectedInstanceId == null && cards.Count > 0)
            {
                selectedInstanceId = cards[0].Instance.InstanceId;
            }

            LoadoutSnapshot authoritativeMounts =
                mountLoadoutAuthority.ExportSnapshot();
            snapshot = new InventoryMenuState(
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

        private InventoryLoadoutScreenResult Result(
            InventoryLoadoutScreenStatus status,
            string rejectionCode)
        {
            return new InventoryLoadoutScreenResult(
                status,
                rejectionCode,
                snapshot,
                null);
        }

        private GunSlot FindPhysicalMount(StableId slotId)
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

        private IReadOnlyList<EquippedGun> BuildMountBindings()
        {
            var bindings = new List<EquippedGun>(
                layout.PhysicalPositions.Count);
            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                GunSlot position =
                    layout.PhysicalPositions[index];
                bindings.Add(new EquippedGun(
                    position.MountStableId,
                    position.IsActive
                        ? draftBindings[position.LoadoutSlotStableId]
                        : null));
            }
            return bindings;
        }

        private static bool LayoutsMatch(
            GunSlots left,
            GunSlots right)
        {
            if (left == null
                || right == null
                || left.PhysicalPositions.Count
                    != right.PhysicalPositions.Count)
            {
                return false;
            }
            for (int index = 0;
                 index < left.PhysicalPositions.Count;
                 index++)
            {
                GunSlot leftPosition =
                    left.PhysicalPositions[index];
                GunSlot rightPosition =
                    right.PhysicalPositions[index];
                if (leftPosition.MountStableId
                        != rightPosition.MountStableId
                    || leftPosition.LoadoutSlotStableId
                        != rightPosition.LoadoutSlotStableId
                    || leftPosition.Availability
                        != rightPosition.Availability)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool MatchesMountBindings(
            LoadoutSnapshot snapshot,
            IReadOnlyList<EquippedGun> bindings)
        {
            if (snapshot == null
                || bindings == null
                || !snapshot.HasValidFingerprint()
                || snapshot.Bindings.Count != bindings.Count)
            {
                return false;
            }
            for (int index = 0; index < bindings.Count; index++)
            {
                EquippedGun actual = snapshot.Find(bindings[index].MountId);
                if (actual == null
                    || actual.InstanceId != bindings[index].InstanceId)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

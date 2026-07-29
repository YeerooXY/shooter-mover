using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Inventory.LoadoutScreen
{
    public enum InventoryLoadoutSlotKind
    {
        Gun = 1,
        Armor = 2,
    }

    public enum InventoryLoadoutStateMutationStatus
    {
        Applied = 1,
        ExactRepeatNoChange = 2,
        Rejected = 3,
        StaleSnapshot = 4,
    }

    public enum InventoryLoadoutScreenStatus
    {
        Ready = 1,
        Refreshed = 2,
        SelectionChanged = 3,
        NoChange = 4,
        InvalidSlot = 5,
        MissingEquipment = 6,
        InvalidEquipment = 7,
        WrongEquipmentType = 8,
        DuplicateEquipmentInstance = 9,
        IncompleteGunLoadout = 10,
        StaleSelection = 11,
        AuthorityRejected = 12,
        AuthoritySnapshotMismatch = 13,
        HoldingsChangedDuringApply = 14,
        Confirmed = 15,
        Cancelled = 16,
        AlreadyCompleted = 17,
    }

    public static class InventoryLoadoutSlotIds
    {
        public static readonly StableId GunOne =
            StableId.Parse("gun-slot.slot-1");
        public static readonly StableId GunTwo =
            StableId.Parse("gun-slot.slot-2");
        public static readonly StableId GunThree =
            StableId.Parse("gun-slot.slot-3");
        public static readonly StableId GunFour =
            StableId.Parse("gun-slot.slot-4");
        public static readonly StableId ArmorHead =
            StableId.Parse("armor-slot.head");
        public static readonly StableId ArmorBody =
            StableId.Parse("armor-slot.body");
        public static readonly StableId ArmorLegs =
            StableId.Parse("armor-slot.legs");
        public static readonly StableId ArmorFeet =
            StableId.Parse("armor-slot.feet");
    }

    public sealed class InventoryLoadoutSlotDescriptor
    {
        public InventoryLoadoutSlotDescriptor(
            StableId slotStableId,
            InventoryLoadoutSlotKind kind,
            string displayName,
            int ordinal)
        {
            SlotStableId = slotStableId
                ?? throw new ArgumentNullException(nameof(slotStableId));
            if (!Enum.IsDefined(typeof(InventoryLoadoutSlotKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A slot display name is required.",
                    nameof(displayName));
            }
            if (ordinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }
            Kind = kind;
            DisplayName = displayName.Trim();
            Ordinal = ordinal;
        }

        public StableId SlotStableId { get; }
        public InventoryLoadoutSlotKind Kind { get; }
        public string DisplayName { get; }
        public int Ordinal { get; }
    }

    public static class InventoryLoadoutSlots
    {
        private static readonly ReadOnlyCollection<InventoryLoadoutSlotDescriptor>
            all = new ReadOnlyCollection<InventoryLoadoutSlotDescriptor>(
                new List<InventoryLoadoutSlotDescriptor>
                {
                    new InventoryLoadoutSlotDescriptor(
                        InventoryLoadoutSlotIds.GunOne,
                        InventoryLoadoutSlotKind.Gun,
                        "Gun 1",
                        0),
                    new InventoryLoadoutSlotDescriptor(
                        InventoryLoadoutSlotIds.GunTwo,
                        InventoryLoadoutSlotKind.Gun,
                        "Gun 2",
                        1),
                    new InventoryLoadoutSlotDescriptor(
                        InventoryLoadoutSlotIds.GunThree,
                        InventoryLoadoutSlotKind.Gun,
                        "Gun 3",
                        2),
                    new InventoryLoadoutSlotDescriptor(
                        InventoryLoadoutSlotIds.GunFour,
                        InventoryLoadoutSlotKind.Gun,
                        "Gun 4",
                        3),
                    new InventoryLoadoutSlotDescriptor(
                        InventoryLoadoutSlotIds.ArmorHead,
                        InventoryLoadoutSlotKind.Armor,
                        "Head",
                        4),
                    new InventoryLoadoutSlotDescriptor(
                        InventoryLoadoutSlotIds.ArmorBody,
                        InventoryLoadoutSlotKind.Armor,
                        "Body",
                        5),
                    new InventoryLoadoutSlotDescriptor(
                        InventoryLoadoutSlotIds.ArmorLegs,
                        InventoryLoadoutSlotKind.Armor,
                        "Legs",
                        6),
                    new InventoryLoadoutSlotDescriptor(
                        InventoryLoadoutSlotIds.ArmorFeet,
                        InventoryLoadoutSlotKind.Armor,
                        "Feet",
                        7),
                });

        public static IReadOnlyList<InventoryLoadoutSlotDescriptor> All
        {
            get { return all; }
        }

        public static bool TryFind(
            StableId slotStableId,
            out InventoryLoadoutSlotDescriptor descriptor)
        {
            descriptor = null;
            if (slotStableId == null)
            {
                return false;
            }
            for (int index = 0; index < all.Count; index++)
            {
                if (all[index].SlotStableId == slotStableId)
                {
                    descriptor = all[index];
                    return true;
                }
            }
            return false;
        }
    }

    public sealed class InventoryLoadoutSlotBinding :
        IEquatable<InventoryLoadoutSlotBinding>
    {
        public InventoryLoadoutSlotBinding(
            StableId slotStableId,
            StableId equipmentInstanceStableId)
        {
            InventoryLoadoutSlotDescriptor descriptor;
            if (!InventoryLoadoutSlots.TryFind(
                slotStableId,
                out descriptor))
            {
                throw new ArgumentException(
                    "Unknown loadout slot identity.",
                    nameof(slotStableId));
            }
            SlotStableId = descriptor.SlotStableId;
            EquipmentInstanceStableId = equipmentInstanceStableId;
        }

        public StableId SlotStableId { get; }
        public StableId EquipmentInstanceStableId { get; }

        public string ToCanonicalString()
        {
            return SlotStableId
                + "|"
                + (EquipmentInstanceStableId == null
                    ? "unequipped"
                    : EquipmentInstanceStableId.ToString());
        }

        public bool Equals(InventoryLoadoutSlotBinding other)
        {
            return !ReferenceEquals(other, null)
                && SlotStableId == other.SlotStableId
                && EquipmentInstanceStableId
                    == other.EquipmentInstanceStableId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as InventoryLoadoutSlotBinding);
        }

        public override int GetHashCode()
        {
            return InventoryLoadout.OrdinalHash(
                ToCanonicalString());
        }
    }

    public sealed class InventoryLoadoutStateSnapshot
    {
        private readonly ReadOnlyCollection<InventoryLoadoutSlotBinding>
            bindings;

        private InventoryLoadoutStateSnapshot(
            long sequence,
            IEnumerable<InventoryLoadoutSlotBinding> bindings,
            string fingerprint)
        {
            if (sequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }
            Sequence = sequence;
            this.bindings = InventoryLoadout
                .CanonicalizeBindings(bindings);
            Fingerprint = fingerprint ?? string.Empty;
        }

        public long Sequence { get; }
        public IReadOnlyList<InventoryLoadoutSlotBinding> Bindings
        {
            get { return bindings; }
        }
        public string Fingerprint { get; }

        public static InventoryLoadoutStateSnapshot CreateCanonical(
            long sequence,
            IEnumerable<InventoryLoadoutSlotBinding> bindings)
        {
            var preliminary = new InventoryLoadoutStateSnapshot(
                sequence,
                bindings,
                string.Empty);
            string fingerprint = InventoryLoadout
                .ComputeSnapshotFingerprint(
                    preliminary.Sequence,
                    preliminary.Bindings);
            return new InventoryLoadoutStateSnapshot(
                sequence,
                preliminary.Bindings,
                fingerprint);
        }

        public InventoryLoadoutSlotBinding GetBinding(
            StableId slotStableId)
        {
            InventoryLoadoutSlotDescriptor descriptor;
            if (!InventoryLoadoutSlots.TryFind(
                slotStableId,
                out descriptor))
            {
                throw new ArgumentException(
                    "Unknown loadout slot identity.",
                    nameof(slotStableId));
            }
            return bindings[descriptor.Ordinal];
        }

        public bool HasValidFingerprint()
        {
            return string.Equals(
                Fingerprint,
                InventoryLoadout.ComputeSnapshotFingerprint(
                    Sequence,
                    bindings),
                StringComparison.Ordinal);
        }
    }

    public sealed class InventoryLoadoutStateCommand
    {
        private readonly ReadOnlyCollection<InventoryLoadoutSlotBinding>
            bindings;

        public InventoryLoadoutStateCommand(
            long expectedSequence,
            long expectedHoldingsSequence,
            IEnumerable<InventoryLoadoutSlotBinding> bindings)
        {
            if (expectedSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedSequence));
            }
            if (expectedHoldingsSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedHoldingsSequence));
            }
            ExpectedSequence = expectedSequence;
            ExpectedHoldingsSequence = expectedHoldingsSequence;
            this.bindings = InventoryLoadout
                .CanonicalizeBindings(bindings);
            Fingerprint = InventoryLoadout
                .ComputeCommandFingerprint(
                    ExpectedSequence,
                    ExpectedHoldingsSequence,
                    this.bindings);
        }

        public long ExpectedSequence { get; }
        public long ExpectedHoldingsSequence { get; }
        public IReadOnlyList<InventoryLoadoutSlotBinding> Bindings
        {
            get { return bindings; }
        }
        public string Fingerprint { get; }
    }

    public sealed class InventoryLoadoutStateResult
    {
        public InventoryLoadoutStateResult(
            InventoryLoadoutStateMutationStatus status,
            string rejectionCode,
            InventoryLoadoutStateSnapshot snapshot)
        {
            if (!Enum.IsDefined(
                typeof(InventoryLoadoutStateMutationStatus),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public InventoryLoadoutStateMutationStatus Status { get; }
        public string RejectionCode { get; }
        public InventoryLoadoutStateSnapshot Snapshot { get; }
        public bool Succeeded
        {
            get
            {
                return Status
                    == InventoryLoadoutStateMutationStatus.Applied
                    || Status
                    == InventoryLoadoutStateMutationStatus
                        .ExactRepeatNoChange;
            }
        }
    }

    public interface IInventoryLoadoutStatePort
    {
        InventoryLoadoutStateSnapshot ExportSnapshot();
        InventoryLoadoutStateResult Apply(
            InventoryLoadoutStateCommand command);
    }

    public sealed class InventoryLoadoutEquipmentView
    {
        public InventoryLoadoutEquipmentView(
            StableId instanceStableId,
            StableId definitionStableId,
            StableId categoryStableId,
            string displayName,
            int itemLevel,
            StableId qualityStableId,
            string instanceFingerprint,
            bool isSelectable,
            string rejectionCode)
        {
            InstanceStableId = instanceStableId
                ?? throw new ArgumentNullException(nameof(instanceStableId));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            CategoryStableId = categoryStableId;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? definitionStableId.ToString()
                : displayName.Trim();
            ItemLevel = itemLevel;
            QualityStableId = qualityStableId;
            InstanceFingerprint = instanceFingerprint ?? string.Empty;
            IsSelectable = isSelectable;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public StableId InstanceStableId { get; }
        public StableId DefinitionStableId { get; }
        public StableId CategoryStableId { get; }
        public string DisplayName { get; }
        public int ItemLevel { get; }
        public StableId QualityStableId { get; }
        public string InstanceFingerprint { get; }
        public bool IsSelectable { get; }
        public string RejectionCode { get; }

        public InventoryLoadoutSlotKind? SlotKind
        {
            get
            {
                if (CategoryStableId == EquipmentCategoryIds.Gun)
                {
                    return InventoryLoadoutSlotKind.Gun;
                }
                if (CategoryStableId == EquipmentCategoryIds.Armor)
                {
                    return InventoryLoadoutSlotKind.Armor;
                }
                return null;
            }
        }
    }

    public sealed class InventoryLoadoutSelectionView
    {
        public InventoryLoadoutSelectionView(
            InventoryLoadoutSlotDescriptor slot,
            StableId equipmentInstanceStableId,
            bool isValid,
            string rejectionCode)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            EquipmentInstanceStableId = equipmentInstanceStableId;
            IsValid = isValid;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public InventoryLoadoutSlotDescriptor Slot { get; }
        public StableId EquipmentInstanceStableId { get; }
        public bool IsValid { get; }
        public string RejectionCode { get; }
    }

    public sealed class InventoryLoadoutScreenSnapshot
    {
        private readonly ReadOnlyCollection<InventoryLoadoutEquipmentView>
            equipment;
        private readonly ReadOnlyCollection<InventoryLoadoutSelectionView>
            selections;

        public InventoryLoadoutScreenSnapshot(
            PlayerRouteProfilePayload incomingRoutePayload,
            long holdingsSequence,
            string holdingsFingerprint,
            long loadoutSequence,
            string loadoutFingerprint,
            IEnumerable<InventoryLoadoutEquipmentView> equipment,
            IEnumerable<InventoryLoadoutSelectionView> selections,
            bool canConfirm,
            bool isCompleted)
        {
            IncomingRoutePayload = incomingRoutePayload
                ?? throw new ArgumentNullException(
                    nameof(incomingRoutePayload));
            HoldingsSequence = holdingsSequence;
            HoldingsFingerprint = holdingsFingerprint ?? string.Empty;
            LoadoutSequence = loadoutSequence;
            LoadoutFingerprint = loadoutFingerprint ?? string.Empty;
            this.equipment =
                new ReadOnlyCollection<InventoryLoadoutEquipmentView>(
                    new List<InventoryLoadoutEquipmentView>(
                        equipment
                        ?? throw new ArgumentNullException(
                            nameof(equipment))));
            this.selections =
                new ReadOnlyCollection<InventoryLoadoutSelectionView>(
                    new List<InventoryLoadoutSelectionView>(
                        selections
                        ?? throw new ArgumentNullException(
                            nameof(selections))));
            CanConfirm = canConfirm;
            IsCompleted = isCompleted;
        }

        public PlayerRouteProfilePayload IncomingRoutePayload { get; }
        public long HoldingsSequence { get; }
        public string HoldingsFingerprint { get; }
        public long LoadoutSequence { get; }
        public string LoadoutFingerprint { get; }
        public IReadOnlyList<InventoryLoadoutEquipmentView> Equipment
        {
            get { return equipment; }
        }
        public IReadOnlyList<InventoryLoadoutSelectionView> Selections
        {
            get { return selections; }
        }
        public bool CanConfirm { get; }
        public bool IsCompleted { get; }

        public InventoryLoadoutSelectionView GetSelection(
            StableId slotStableId)
        {
            InventoryLoadoutSlotDescriptor descriptor;
            if (!InventoryLoadoutSlots.TryFind(
                slotStableId,
                out descriptor))
            {
                throw new ArgumentException(
                    "Unknown loadout slot identity.",
                    nameof(slotStableId));
            }
            return selections[descriptor.Ordinal];
        }

        public InventoryLoadoutEquipmentView FindEquipment(
            StableId instanceStableId)
        {
            if (instanceStableId == null)
            {
                return null;
            }
            for (int index = 0; index < equipment.Count; index++)
            {
                if (equipment[index].InstanceStableId == instanceStableId)
                {
                    return equipment[index];
                }
            }
            return null;
        }
    }

    public sealed class InventoryLoadoutScreenResult
    {
        public InventoryLoadoutScreenResult(
            InventoryLoadoutScreenStatus status,
            string rejectionCode,
            InventoryLoadoutScreenSnapshot snapshot,
            PlayerRouteProfilePayload routePayload)
        {
            if (!Enum.IsDefined(
                typeof(InventoryLoadoutScreenStatus),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot
                ?? throw new ArgumentNullException(nameof(snapshot));
            RoutePayload = routePayload;
        }

        public InventoryLoadoutScreenStatus Status { get; }
        public string RejectionCode { get; }
        public InventoryLoadoutScreenSnapshot Snapshot { get; }
        public PlayerRouteProfilePayload RoutePayload { get; }
        public bool ChangedSelection
        {
            get
            {
                return Status
                    == InventoryLoadoutScreenStatus.SelectionChanged;
            }
        }
        public bool LeavesScreen
        {
            get
            {
                return Status == InventoryLoadoutScreenStatus.Confirmed
                    || Status == InventoryLoadoutScreenStatus.Cancelled;
            }
        }
    }

    /// <summary>
    /// Engine-independent screen draft. Character mount policy decides which gun
    /// positions are configurable; inactive positions remain null and do not reserve an
    /// equipment instance. Armor behavior remains unchanged.
    /// </summary>
    public sealed class InventoryLoadoutScreenActions
    {
        private readonly PlayerRouteProfilePayload incomingRoutePayload;
        private readonly IPlayerHoldingsState holdingsAuthority;
        private readonly IEquipmentCatalogProvider equipmentCatalogProvider;
        private readonly IInventoryLoadoutStatePort loadoutAuthority;
        private readonly Dictionary<StableId, StableId> draftBindings =
            new Dictionary<StableId, StableId>();
        private InventoryLoadoutScreenSnapshot snapshot;
        private bool completed;

        public InventoryLoadoutScreenActions(
            PlayerRouteProfilePayload incomingRoutePayload,
            IPlayerHoldingsState holdingsAuthority,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            IInventoryLoadoutStatePort loadoutAuthority)
        {
            this.incomingRoutePayload = incomingRoutePayload
                ?? throw new ArgumentNullException(
                    nameof(incomingRoutePayload));
            if (!incomingRoutePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The incoming HUB route payload fingerprint is invalid.",
                    nameof(incomingRoutePayload));
            }
            this.holdingsAuthority = holdingsAuthority
                ?? throw new ArgumentNullException(nameof(holdingsAuthority));
            this.equipmentCatalogProvider = equipmentCatalogProvider
                ?? throw new ArgumentNullException(
                    nameof(equipmentCatalogProvider));
            this.loadoutAuthority = loadoutAuthority
                ?? throw new ArgumentNullException(nameof(loadoutAuthority));
            InitializeDraftBindings();
            RefreshInternal();
        }

        public PlayerRouteProfilePayload IncomingRoutePayload
        {
            get { return incomingRoutePayload; }
        }
        public InventoryLoadoutScreenSnapshot Snapshot
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
            RefreshInternal();
            return Result(
                InventoryLoadoutScreenStatus.Refreshed,
                string.Empty);
        }

        public InventoryLoadoutScreenResult TrySelect(
            StableId slotStableId,
            StableId equipmentInstanceStableId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            InventoryLoadoutSlotDescriptor slot;
            if (!InventoryLoadoutSlots.TryFind(
                slotStableId,
                out slot))
            {
                return Result(
                    InventoryLoadoutScreenStatus.InvalidSlot,
                    "inventory-loadout-slot-unknown");
            }
            if (!IsConfigurable(slot))
            {
                return Result(
                    InventoryLoadoutScreenStatus.InvalidSlot,
                    "inventory-loadout-slot-unavailable-for-profile");
            }
            if (equipmentInstanceStableId == null)
            {
                return Result(
                    InventoryLoadoutScreenStatus.MissingEquipment,
                    "inventory-loadout-instance-missing");
            }

            InventoryLoadoutEquipmentView equipment =
                snapshot.FindEquipment(equipmentInstanceStableId);
            if (equipment == null)
            {
                return Result(
                    InventoryLoadoutScreenStatus.MissingEquipment,
                    "inventory-loadout-instance-not-owned");
            }
            if (!equipment.IsSelectable || !equipment.SlotKind.HasValue)
            {
                return Result(
                    InventoryLoadoutScreenStatus.InvalidEquipment,
                    string.IsNullOrEmpty(equipment.RejectionCode)
                        ? "inventory-loadout-instance-invalid"
                        : equipment.RejectionCode);
            }
            if (equipment.SlotKind.Value != slot.Kind)
            {
                return Result(
                    InventoryLoadoutScreenStatus.WrongEquipmentType,
                    "inventory-loadout-instance-wrong-slot-kind");
            }

            StableId current;
            draftBindings.TryGetValue(slot.SlotStableId, out current);
            if (current == equipmentInstanceStableId)
            {
                return Result(
                    InventoryLoadoutScreenStatus.NoChange,
                    "inventory-loadout-selection-already-current");
            }
            foreach (KeyValuePair<StableId, StableId> pair in draftBindings)
            {
                if (pair.Key != slot.SlotStableId
                    && pair.Value == equipmentInstanceStableId)
                {
                    return Result(
                        InventoryLoadoutScreenStatus
                            .DuplicateEquipmentInstance,
                        "inventory-loadout-instance-already-selected");
                }
            }

            draftBindings[slot.SlotStableId] =
                equipmentInstanceStableId;
            RebuildSnapshot(
                snapshot.HoldingsSequence,
                snapshot.HoldingsFingerprint,
                snapshot.LoadoutSequence,
                snapshot.LoadoutFingerprint,
                snapshot.Equipment);
            return Result(
                InventoryLoadoutScreenStatus.SelectionChanged,
                string.Empty);
        }

        public InventoryLoadoutScreenResult TryUnequip(
            StableId slotStableId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            InventoryLoadoutSlotDescriptor slot;
            if (!InventoryLoadoutSlots.TryFind(
                slotStableId,
                out slot))
            {
                return Result(
                    InventoryLoadoutScreenStatus.InvalidSlot,
                    "inventory-loadout-slot-unknown");
            }
            if (!IsConfigurable(slot))
            {
                return Result(
                    InventoryLoadoutScreenStatus.InvalidSlot,
                    "inventory-loadout-slot-unavailable-for-profile");
            }
            StableId current;
            draftBindings.TryGetValue(slot.SlotStableId, out current);
            if (current == null)
            {
                return Result(
                    InventoryLoadoutScreenStatus.NoChange,
                    "inventory-loadout-slot-already-empty");
            }
            draftBindings[slot.SlotStableId] = null;
            RebuildSnapshot(
                snapshot.HoldingsSequence,
                snapshot.HoldingsFingerprint,
                snapshot.LoadoutSequence,
                snapshot.LoadoutFingerprint,
                snapshot.Equipment);
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
            RefreshInternal();
            InventoryLoadoutScreenResult validationFailure =
                ValidateForConfirm();
            if (validationFailure != null)
            {
                return validationFailure;
            }

            long holdingsSequenceBefore = holdingsAuthority.Sequence;
            string holdingsFingerprintBefore =
                snapshot.HoldingsFingerprint;
            InventoryLoadoutStateSnapshot authorityBefore =
                loadoutAuthority.ExportSnapshot();
            if (authorityBefore == null
                || !authorityBefore.HasValidFingerprint())
            {
                return Result(
                    InventoryLoadoutScreenStatus
                        .AuthoritySnapshotMismatch,
                    "inventory-loadout-authority-snapshot-invalid");
            }

            var command = new InventoryLoadoutStateCommand(
                authorityBefore.Sequence,
                holdingsSequenceBefore,
                BuildDraftBindings());
            InventoryLoadoutStateResult authorityResult =
                loadoutAuthority.Apply(command);
            if (authorityResult == null || !authorityResult.Succeeded)
            {
                return Result(
                    InventoryLoadoutScreenStatus.AuthorityRejected,
                    authorityResult == null
                        ? "inventory-loadout-authority-result-null"
                        : authorityResult.RejectionCode);
            }

            PlayerHoldingsSnapshot holdingsAfter =
                holdingsAuthority.ExportSnapshot();
            if (holdingsAuthority.Sequence != holdingsSequenceBefore
                || !string.Equals(
                    holdingsAfter.Fingerprint,
                    holdingsFingerprintBefore,
                    StringComparison.Ordinal))
            {
                RefreshInternal();
                return Result(
                    InventoryLoadoutScreenStatus
                        .HoldingsChangedDuringApply,
                    "inventory-loadout-authority-mutated-holdings");
            }

            InventoryLoadoutStateSnapshot authorityAfter =
                authorityResult.Snapshot
                ?? loadoutAuthority.ExportSnapshot();
            if (!MatchesCommand(authorityAfter, command))
            {
                RefreshInternal();
                return Result(
                    InventoryLoadoutScreenStatus
                        .AuthoritySnapshotMismatch,
                    "inventory-loadout-authority-result-mismatch");
            }

            var orderedGunInstances = new List<StableId>(
                PlayerRouteProfilePayload.GunSlotCount);
            for (int index = 0;
                index < PlayerRouteProfilePayload.GunSlotCount;
                index++)
            {
                orderedGunInstances.Add(
                    draftBindings[
                        InventoryLoadoutSlots.All[index]
                            .SlotStableId]);
            }

            PlayerRouteProfilePayload confirmedPayload =
                PlayerRouteProfilePayload.Create(
                    incomingRoutePayload.SelectedCharacterStableId,
                    incomingRoutePayload.LoadoutProfileStableId,
                    orderedGunInstances);
            completed = true;
            RebuildSnapshot(
                holdingsSequenceBefore,
                holdingsAfter.Fingerprint,
                authorityAfter.Sequence,
                authorityAfter.Fingerprint,
                snapshot.Equipment);
            return new InventoryLoadoutScreenResult(
                InventoryLoadoutScreenStatus.Confirmed,
                string.Empty,
                snapshot,
                confirmedPayload);
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
            RebuildSnapshot(
                snapshot.HoldingsSequence,
                snapshot.HoldingsFingerprint,
                snapshot.LoadoutSequence,
                snapshot.LoadoutFingerprint,
                snapshot.Equipment);
            return new InventoryLoadoutScreenResult(
                InventoryLoadoutScreenStatus.Cancelled,
                string.Empty,
                snapshot,
                incomingRoutePayload);
        }

        private bool IsConfigurable(
            InventoryLoadoutSlotDescriptor slot)
        {
            return slot.Kind != InventoryLoadoutSlotKind.Gun
                || GunMountPolicy
                    .IsConfigurableLoadoutSlot(
                        incomingRoutePayload.LoadoutProfileStableId,
                        slot.SlotStableId);
        }

        private void InitializeDraftBindings()
        {
            for (int index = 0;
                index < InventoryLoadoutSlots.All.Count;
                index++)
            {
                draftBindings.Add(
                    InventoryLoadoutSlots.All[index].SlotStableId,
                    null);
            }
            for (int index = 0;
                index < incomingRoutePayload.GunSlots.Count;
                index++)
            {
                PlayerRouteGunSlot routeSlot =
                    incomingRoutePayload.GunSlots[index];
                if (GunMountPolicy
                    .IsConfigurableLoadoutSlot(
                        incomingRoutePayload.LoadoutProfileStableId,
                        routeSlot.GunSlotStableId))
                {
                    draftBindings[routeSlot.GunSlotStableId] =
                        routeSlot.EquipmentInstanceStableId;
                }
            }

            InventoryLoadoutStateSnapshot authoritySnapshot =
                loadoutAuthority.ExportSnapshot();
            if (authoritySnapshot == null
                || !authoritySnapshot.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The loadout authority returned an invalid initial snapshot.",
                    nameof(loadoutAuthority));
            }
            for (int index =
                    PlayerRouteProfilePayload.GunSlotCount;
                index < InventoryLoadoutSlots.All.Count;
                index++)
            {
                InventoryLoadoutSlotDescriptor slot =
                    InventoryLoadoutSlots.All[index];
                draftBindings[slot.SlotStableId] =
                    authoritySnapshot.GetBinding(slot.SlotStableId)
                        .EquipmentInstanceStableId;
            }
        }

        private void RefreshInternal()
        {
            PlayerHoldingsSnapshot holdingsSnapshot =
                holdingsAuthority.ExportSnapshot();
            if (holdingsSnapshot == null)
            {
                throw new InvalidOperationException(
                    "The holdings authority returned a null snapshot.");
            }
            EquipmentCatalog catalog = equipmentCatalogProvider.Catalog;
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "The equipment catalog provider returned a null catalog.");
            }
            InventoryLoadoutStateSnapshot loadoutSnapshot =
                loadoutAuthority.ExportSnapshot();
            if (loadoutSnapshot == null
                || !loadoutSnapshot.HasValidFingerprint())
            {
                throw new InvalidOperationException(
                    "The loadout authority returned an invalid snapshot.");
            }

            var equipment =
                new List<InventoryLoadoutEquipmentView>();
            for (int index = 0;
                index < holdingsSnapshot.UniqueHoldings.Count;
                index++)
            {
                UniqueHoldingSnapshot holding =
                    holdingsSnapshot.UniqueHoldings[index];
                if (holding.RewardKind
                    != RewardGrantKind.EquipmentReference)
                {
                    continue;
                }
                EquipmentInstance instance = holding.EquipmentInstance;
                EquipmentDefinition definition = instance == null
                    ? null
                    : catalog.FindEquipmentDefinition(
                        instance.DefinitionId);
                EquipmentValidationResult validation = instance == null
                    ? null
                    : catalog.ValidateInstance(instance);
                StableId categoryId = definition == null
                    ? null
                    : definition.CategoryId;
                bool acceptedCategory =
                    categoryId == EquipmentCategoryIds.Gun
                    || categoryId == EquipmentCategoryIds.Armor;
                bool selectable = instance != null
                    && definition != null
                    && validation != null
                    && validation.IsValid
                    && acceptedCategory;
                string rejectionCode = string.Empty;
                if (instance == null)
                {
                    rejectionCode =
                        "inventory-loadout-equipment-payload-missing";
                }
                else if (definition == null)
                {
                    rejectionCode =
                        "inventory-loadout-equipment-definition-unknown";
                }
                else if (validation == null || !validation.IsValid)
                {
                    rejectionCode =
                        "inventory-loadout-equipment-validation-rejected";
                }
                else if (!acceptedCategory)
                {
                    rejectionCode =
                        "inventory-loadout-equipment-category-unsupported";
                }

                equipment.Add(
                    new InventoryLoadoutEquipmentView(
                        holding.InstanceStableId,
                        holding.DefinitionStableId,
                        categoryId,
                        definition == null
                            ? holding.DefinitionStableId.ToString()
                            : definition.DisplayName,
                        instance == null ? 0 : instance.ItemLevel,
                        instance == null ? null : instance.QualityId,
                        instance == null
                            ? string.Empty
                            : instance.Fingerprint,
                        selectable,
                        rejectionCode));
            }
            equipment.Sort(delegate(
                InventoryLoadoutEquipmentView left,
                InventoryLoadoutEquipmentView right)
            {
                return left.InstanceStableId.CompareTo(
                    right.InstanceStableId);
            });
            RebuildSnapshot(
                holdingsAuthority.Sequence,
                holdingsSnapshot.Fingerprint,
                loadoutSnapshot.Sequence,
                loadoutSnapshot.Fingerprint,
                equipment);
        }

        private void RebuildSnapshot(
            long holdingsSequence,
            string holdingsFingerprint,
            long loadoutSequence,
            string loadoutFingerprint,
            IEnumerable<InventoryLoadoutEquipmentView> equipment)
        {
            var equipmentCopy =
                new List<InventoryLoadoutEquipmentView>(equipment);
            var selections =
                new List<InventoryLoadoutSelectionView>(
                    InventoryLoadoutSlots.All.Count);
            var seen = new HashSet<StableId>();
            bool canConfirm = true;
            for (int index = 0;
                index < InventoryLoadoutSlots.All.Count;
                index++)
            {
                InventoryLoadoutSlotDescriptor slot =
                    InventoryLoadoutSlots.All[index];
                StableId selected;
                draftBindings.TryGetValue(
                    slot.SlotStableId,
                    out selected);
                bool configurable = IsConfigurable(slot);
                bool valid = true;
                string rejectionCode = string.Empty;

                if (!configurable)
                {
                    valid = selected == null;
                    if (!valid)
                    {
                        rejectionCode =
                            "inventory-loadout-slot-unavailable-for-profile";
                    }
                }
                else if (selected == null)
                {
                    if (slot.Kind == InventoryLoadoutSlotKind.Gun)
                    {
                        valid = false;
                        rejectionCode =
                            "inventory-loadout-gun-slot-empty";
                    }
                }
                else
                {
                    InventoryLoadoutEquipmentView projected =
                        FindEquipment(equipmentCopy, selected);
                    if (projected == null)
                    {
                        valid = false;
                        rejectionCode =
                            "inventory-loadout-selection-stale";
                    }
                    else if (!projected.IsSelectable
                        || !projected.SlotKind.HasValue)
                    {
                        valid = false;
                        rejectionCode = string.IsNullOrEmpty(
                            projected.RejectionCode)
                                ? "inventory-loadout-selection-invalid"
                                : projected.RejectionCode;
                    }
                    else if (projected.SlotKind.Value != slot.Kind)
                    {
                        valid = false;
                        rejectionCode =
                            "inventory-loadout-selection-wrong-slot-kind";
                    }
                    else if (!seen.Add(selected))
                    {
                        valid = false;
                        rejectionCode =
                            "inventory-loadout-selection-duplicate-instance";
                    }
                }

                if (!valid)
                {
                    canConfirm = false;
                }
                selections.Add(
                    new InventoryLoadoutSelectionView(
                        slot,
                        selected,
                        valid,
                        rejectionCode));
            }

            snapshot = new InventoryLoadoutScreenSnapshot(
                incomingRoutePayload,
                holdingsSequence,
                holdingsFingerprint,
                loadoutSequence,
                loadoutFingerprint,
                equipmentCopy,
                selections,
                canConfirm && !completed,
                completed);
        }

        private InventoryLoadoutScreenResult ValidateForConfirm()
        {
            for (int index = 0;
                index < snapshot.Selections.Count;
                index++)
            {
                InventoryLoadoutSelectionView selection =
                    snapshot.Selections[index];
                if (selection.IsValid)
                {
                    continue;
                }
                if (selection.Slot.Kind
                        == InventoryLoadoutSlotKind.Gun
                    && selection.EquipmentInstanceStableId == null)
                {
                    return Result(
                        InventoryLoadoutScreenStatus
                            .IncompleteGunLoadout,
                        selection.RejectionCode);
                }
                if (string.Equals(
                    selection.RejectionCode,
                    "inventory-loadout-selection-stale",
                    StringComparison.Ordinal))
                {
                    return Result(
                        InventoryLoadoutScreenStatus.StaleSelection,
                        selection.RejectionCode);
                }
                return Result(
                    InventoryLoadoutScreenStatus.InvalidEquipment,
                    selection.RejectionCode);
            }
            return null;
        }

        private List<InventoryLoadoutSlotBinding> BuildDraftBindings()
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

        private static InventoryLoadoutEquipmentView FindEquipment(
            IList<InventoryLoadoutEquipmentView> equipment,
            StableId instanceStableId)
        {
            for (int index = 0; index < equipment.Count; index++)
            {
                if (equipment[index].InstanceStableId == instanceStableId)
                {
                    return equipment[index];
                }
            }
            return null;
        }

        private static bool MatchesCommand(
            InventoryLoadoutStateSnapshot snapshot,
            InventoryLoadoutStateCommand command)
        {
            if (snapshot == null
                || command == null
                || !snapshot.HasValidFingerprint()
                || snapshot.Bindings.Count != command.Bindings.Count)
            {
                return false;
            }
            for (int index = 0;
                index < command.Bindings.Count;
                index++)
            {
                if (!command.Bindings[index].Equals(
                    snapshot.Bindings[index]))
                {
                    return false;
                }
            }
            return true;
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
    }

    internal static class InventoryLoadout
    {
        public static ReadOnlyCollection<InventoryLoadoutSlotBinding>
            CanonicalizeBindings(
                IEnumerable<InventoryLoadoutSlotBinding> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var canonical = new InventoryLoadoutSlotBinding[
                InventoryLoadoutSlots.All.Count];
            foreach (InventoryLoadoutSlotBinding binding in source)
            {
                if (binding == null)
                {
                    throw new ArgumentException(
                        "Loadout bindings cannot contain null.",
                        nameof(source));
                }
                InventoryLoadoutSlotDescriptor descriptor;
                if (!InventoryLoadoutSlots.TryFind(
                    binding.SlotStableId,
                    out descriptor))
                {
                    throw new ArgumentException(
                        "Loadout binding contains an unknown slot.",
                        nameof(source));
                }
                if (canonical[descriptor.Ordinal] != null)
                {
                    throw new ArgumentException(
                        "Each loadout slot must appear exactly once.",
                        nameof(source));
                }
                canonical[descriptor.Ordinal] =
                    new InventoryLoadoutSlotBinding(
                        binding.SlotStableId,
                        binding.EquipmentInstanceStableId);
            }
            for (int index = 0; index < canonical.Length; index++)
            {
                if (canonical[index] == null)
                {
                    throw new ArgumentException(
                        "Every loadout slot must appear exactly once.",
                        nameof(source));
                }
            }
            return new ReadOnlyCollection<InventoryLoadoutSlotBinding>(
                canonical);
        }

        public static string ComputeSnapshotFingerprint(
            long sequence,
            IReadOnlyList<InventoryLoadoutSlotBinding> bindings)
        {
            return ComputeFingerprint(
                "snapshot",
                sequence,
                -1L,
                bindings);
        }

        public static string ComputeCommandFingerprint(
            long expectedSequence,
            long expectedHoldingsSequence,
            IReadOnlyList<InventoryLoadoutSlotBinding> bindings)
        {
            return ComputeFingerprint(
                "command",
                expectedSequence,
                expectedHoldingsSequence,
                bindings);
        }

        public static int OrdinalHash(string value)
        {
            unchecked
            {
                const uint offset = 2166136261u;
                const uint prime = 16777619u;
                uint hash = offset;
                string source = value ?? string.Empty;
                for (int index = 0; index < source.Length; index++)
                {
                    hash ^= source[index];
                    hash *= prime;
                }
                return (int)hash;
            }
        }

        private static string ComputeFingerprint(
            string kind,
            long sequence,
            long holdingsSequence,
            IReadOnlyList<InventoryLoadoutSlotBinding> bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }
            var builder = new StringBuilder();
            Append(builder, "kind", kind);
            Append(
                builder,
                "sequence",
                sequence.ToString(CultureInfo.InvariantCulture));
            Append(
                builder,
                "holdings-sequence",
                holdingsSequence.ToString(CultureInfo.InvariantCulture));
            Append(
                builder,
                "binding-count",
                bindings.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < bindings.Count; index++)
            {
                Append(
                    builder,
                    "binding-" + index.ToString(
                        "D2",
                        CultureInfo.InvariantCulture),
                    bindings[index].ToCanonicalString());
            }

            byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }
            var result = new StringBuilder("sha256:");
            for (int index = 0; index < digest.Length; index++)
            {
                result.Append(digest[index].ToString(
                    "x2",
                    CultureInfo.InvariantCulture));
            }
            return result.ToString();
        }

        private static void Append(
            StringBuilder builder,
            string name,
            string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(name)
                .Append(':')
                .Append(safe.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safe)
                .Append('\n');
        }
    }
}

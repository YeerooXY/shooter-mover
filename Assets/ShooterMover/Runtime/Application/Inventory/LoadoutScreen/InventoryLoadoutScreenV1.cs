using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Inventory.LoadoutScreen
{
    public enum InventoryLoadoutSlotKindV1
    {
        Weapon = 1,
        Armor = 2,
    }

    public enum InventoryLoadoutAuthorityMutationStatusV1
    {
        Applied = 1,
        ExactRepeatNoChange = 2,
        Rejected = 3,
        StaleSnapshot = 4,
    }

    public enum InventoryLoadoutScreenStatusV1
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
        IncompleteWeaponLoadout = 10,
        StaleSelection = 11,
        AuthorityRejected = 12,
        AuthoritySnapshotMismatch = 13,
        HoldingsChangedDuringApply = 14,
        Confirmed = 15,
        Cancelled = 16,
        AlreadyCompleted = 17,
    }

    public static class InventoryLoadoutSlotIdsV1
    {
        public static readonly StableId WeaponOne =
            StableId.Parse("weapon-slot.slot-1");
        public static readonly StableId WeaponTwo =
            StableId.Parse("weapon-slot.slot-2");
        public static readonly StableId WeaponThree =
            StableId.Parse("weapon-slot.slot-3");
        public static readonly StableId WeaponFour =
            StableId.Parse("weapon-slot.slot-4");
        public static readonly StableId ArmorHead =
            StableId.Parse("armor-slot.head");
        public static readonly StableId ArmorBody =
            StableId.Parse("armor-slot.body");
        public static readonly StableId ArmorLegs =
            StableId.Parse("armor-slot.legs");
        public static readonly StableId ArmorFeet =
            StableId.Parse("armor-slot.feet");
    }

    public sealed class InventoryLoadoutSlotDescriptorV1
    {
        public InventoryLoadoutSlotDescriptorV1(
            StableId slotStableId,
            InventoryLoadoutSlotKindV1 kind,
            string displayName,
            int ordinal)
        {
            SlotStableId = slotStableId
                ?? throw new ArgumentNullException(nameof(slotStableId));
            if (!Enum.IsDefined(typeof(InventoryLoadoutSlotKindV1), kind))
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
        public InventoryLoadoutSlotKindV1 Kind { get; }
        public string DisplayName { get; }
        public int Ordinal { get; }
    }

    public static class InventoryLoadoutSlotsV1
    {
        private static readonly ReadOnlyCollection<InventoryLoadoutSlotDescriptorV1>
            all = new ReadOnlyCollection<InventoryLoadoutSlotDescriptorV1>(
                new List<InventoryLoadoutSlotDescriptorV1>
                {
                    new InventoryLoadoutSlotDescriptorV1(
                        InventoryLoadoutSlotIdsV1.WeaponOne,
                        InventoryLoadoutSlotKindV1.Weapon,
                        "Weapon 1",
                        0),
                    new InventoryLoadoutSlotDescriptorV1(
                        InventoryLoadoutSlotIdsV1.WeaponTwo,
                        InventoryLoadoutSlotKindV1.Weapon,
                        "Weapon 2",
                        1),
                    new InventoryLoadoutSlotDescriptorV1(
                        InventoryLoadoutSlotIdsV1.WeaponThree,
                        InventoryLoadoutSlotKindV1.Weapon,
                        "Weapon 3",
                        2),
                    new InventoryLoadoutSlotDescriptorV1(
                        InventoryLoadoutSlotIdsV1.WeaponFour,
                        InventoryLoadoutSlotKindV1.Weapon,
                        "Weapon 4",
                        3),
                    new InventoryLoadoutSlotDescriptorV1(
                        InventoryLoadoutSlotIdsV1.ArmorHead,
                        InventoryLoadoutSlotKindV1.Armor,
                        "Head",
                        4),
                    new InventoryLoadoutSlotDescriptorV1(
                        InventoryLoadoutSlotIdsV1.ArmorBody,
                        InventoryLoadoutSlotKindV1.Armor,
                        "Body",
                        5),
                    new InventoryLoadoutSlotDescriptorV1(
                        InventoryLoadoutSlotIdsV1.ArmorLegs,
                        InventoryLoadoutSlotKindV1.Armor,
                        "Legs",
                        6),
                    new InventoryLoadoutSlotDescriptorV1(
                        InventoryLoadoutSlotIdsV1.ArmorFeet,
                        InventoryLoadoutSlotKindV1.Armor,
                        "Feet",
                        7),
                });

        public static IReadOnlyList<InventoryLoadoutSlotDescriptorV1> All
        {
            get { return all; }
        }

        public static bool TryFind(
            StableId slotStableId,
            out InventoryLoadoutSlotDescriptorV1 descriptor)
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

    public sealed class InventoryLoadoutSlotBindingV1 :
        IEquatable<InventoryLoadoutSlotBindingV1>
    {
        public InventoryLoadoutSlotBindingV1(
            StableId slotStableId,
            StableId equipmentInstanceStableId)
        {
            InventoryLoadoutSlotDescriptorV1 descriptor;
            if (!InventoryLoadoutSlotsV1.TryFind(
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

        public bool Equals(InventoryLoadoutSlotBindingV1 other)
        {
            return !ReferenceEquals(other, null)
                && SlotStableId == other.SlotStableId
                && EquipmentInstanceStableId
                    == other.EquipmentInstanceStableId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as InventoryLoadoutSlotBindingV1);
        }

        public override int GetHashCode()
        {
            return InventoryLoadoutCanonicalV1.OrdinalHash(
                ToCanonicalString());
        }
    }

    public sealed class InventoryLoadoutAuthoritySnapshotV1
    {
        private readonly ReadOnlyCollection<InventoryLoadoutSlotBindingV1>
            bindings;

        private InventoryLoadoutAuthoritySnapshotV1(
            long sequence,
            IEnumerable<InventoryLoadoutSlotBindingV1> bindings,
            string fingerprint)
        {
            if (sequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }
            Sequence = sequence;
            this.bindings = InventoryLoadoutCanonicalV1
                .CanonicalizeBindings(bindings);
            Fingerprint = fingerprint ?? string.Empty;
        }

        public long Sequence { get; }
        public IReadOnlyList<InventoryLoadoutSlotBindingV1> Bindings
        {
            get { return bindings; }
        }
        public string Fingerprint { get; }

        public static InventoryLoadoutAuthoritySnapshotV1 CreateCanonical(
            long sequence,
            IEnumerable<InventoryLoadoutSlotBindingV1> bindings)
        {
            var preliminary = new InventoryLoadoutAuthoritySnapshotV1(
                sequence,
                bindings,
                string.Empty);
            string fingerprint = InventoryLoadoutCanonicalV1
                .ComputeSnapshotFingerprint(
                    preliminary.Sequence,
                    preliminary.Bindings);
            return new InventoryLoadoutAuthoritySnapshotV1(
                sequence,
                preliminary.Bindings,
                fingerprint);
        }

        public InventoryLoadoutSlotBindingV1 GetBinding(
            StableId slotStableId)
        {
            InventoryLoadoutSlotDescriptorV1 descriptor;
            if (!InventoryLoadoutSlotsV1.TryFind(
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
                InventoryLoadoutCanonicalV1.ComputeSnapshotFingerprint(
                    Sequence,
                    bindings),
                StringComparison.Ordinal);
        }
    }

    public sealed class InventoryLoadoutAuthorityCommandV1
    {
        private readonly ReadOnlyCollection<InventoryLoadoutSlotBindingV1>
            bindings;

        public InventoryLoadoutAuthorityCommandV1(
            long expectedSequence,
            long expectedHoldingsSequence,
            IEnumerable<InventoryLoadoutSlotBindingV1> bindings)
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
            this.bindings = InventoryLoadoutCanonicalV1
                .CanonicalizeBindings(bindings);
            Fingerprint = InventoryLoadoutCanonicalV1
                .ComputeCommandFingerprint(
                    ExpectedSequence,
                    ExpectedHoldingsSequence,
                    this.bindings);
        }

        public long ExpectedSequence { get; }
        public long ExpectedHoldingsSequence { get; }
        public IReadOnlyList<InventoryLoadoutSlotBindingV1> Bindings
        {
            get { return bindings; }
        }
        public string Fingerprint { get; }
    }

    public sealed class InventoryLoadoutAuthorityResultV1
    {
        public InventoryLoadoutAuthorityResultV1(
            InventoryLoadoutAuthorityMutationStatusV1 status,
            string rejectionCode,
            InventoryLoadoutAuthoritySnapshotV1 snapshot)
        {
            if (!Enum.IsDefined(
                typeof(InventoryLoadoutAuthorityMutationStatusV1),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public InventoryLoadoutAuthorityMutationStatusV1 Status { get; }
        public string RejectionCode { get; }
        public InventoryLoadoutAuthoritySnapshotV1 Snapshot { get; }
        public bool Succeeded
        {
            get
            {
                return Status
                    == InventoryLoadoutAuthorityMutationStatusV1.Applied
                    || Status
                    == InventoryLoadoutAuthorityMutationStatusV1
                        .ExactRepeatNoChange;
            }
        }
    }

    public interface IInventoryLoadoutAuthorityPortV1
    {
        InventoryLoadoutAuthoritySnapshotV1 ExportSnapshot();
        InventoryLoadoutAuthorityResultV1 Apply(
            InventoryLoadoutAuthorityCommandV1 command);
    }

    public sealed class InventoryLoadoutEquipmentProjectionV1
    {
        public InventoryLoadoutEquipmentProjectionV1(
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

        public InventoryLoadoutSlotKindV1? SlotKind
        {
            get
            {
                if (CategoryStableId == EquipmentCategoryIds.Weapon)
                {
                    return InventoryLoadoutSlotKindV1.Weapon;
                }
                if (CategoryStableId == EquipmentCategoryIds.Armor)
                {
                    return InventoryLoadoutSlotKindV1.Armor;
                }
                return null;
            }
        }
    }

    public sealed class InventoryLoadoutSelectionProjectionV1
    {
        public InventoryLoadoutSelectionProjectionV1(
            InventoryLoadoutSlotDescriptorV1 slot,
            StableId equipmentInstanceStableId,
            bool isValid,
            string rejectionCode)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            EquipmentInstanceStableId = equipmentInstanceStableId;
            IsValid = isValid;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public InventoryLoadoutSlotDescriptorV1 Slot { get; }
        public StableId EquipmentInstanceStableId { get; }
        public bool IsValid { get; }
        public string RejectionCode { get; }
    }

    public sealed class InventoryLoadoutScreenSnapshotV1
    {
        private readonly ReadOnlyCollection<InventoryLoadoutEquipmentProjectionV1>
            equipment;
        private readonly ReadOnlyCollection<InventoryLoadoutSelectionProjectionV1>
            selections;

        public InventoryLoadoutScreenSnapshotV1(
            PlayerRouteProfilePayloadV1 incomingRoutePayload,
            long holdingsSequence,
            string holdingsFingerprint,
            long loadoutSequence,
            string loadoutFingerprint,
            IEnumerable<InventoryLoadoutEquipmentProjectionV1> equipment,
            IEnumerable<InventoryLoadoutSelectionProjectionV1> selections,
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
                new ReadOnlyCollection<InventoryLoadoutEquipmentProjectionV1>(
                    new List<InventoryLoadoutEquipmentProjectionV1>(
                        equipment
                        ?? throw new ArgumentNullException(
                            nameof(equipment))));
            this.selections =
                new ReadOnlyCollection<InventoryLoadoutSelectionProjectionV1>(
                    new List<InventoryLoadoutSelectionProjectionV1>(
                        selections
                        ?? throw new ArgumentNullException(
                            nameof(selections))));
            CanConfirm = canConfirm;
            IsCompleted = isCompleted;
        }

        public PlayerRouteProfilePayloadV1 IncomingRoutePayload { get; }
        public long HoldingsSequence { get; }
        public string HoldingsFingerprint { get; }
        public long LoadoutSequence { get; }
        public string LoadoutFingerprint { get; }
        public IReadOnlyList<InventoryLoadoutEquipmentProjectionV1> Equipment
        {
            get { return equipment; }
        }
        public IReadOnlyList<InventoryLoadoutSelectionProjectionV1> Selections
        {
            get { return selections; }
        }
        public bool CanConfirm { get; }
        public bool IsCompleted { get; }

        public InventoryLoadoutSelectionProjectionV1 GetSelection(
            StableId slotStableId)
        {
            InventoryLoadoutSlotDescriptorV1 descriptor;
            if (!InventoryLoadoutSlotsV1.TryFind(
                slotStableId,
                out descriptor))
            {
                throw new ArgumentException(
                    "Unknown loadout slot identity.",
                    nameof(slotStableId));
            }
            return selections[descriptor.Ordinal];
        }

        public InventoryLoadoutEquipmentProjectionV1 FindEquipment(
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

    public sealed class InventoryLoadoutScreenResultV1
    {
        public InventoryLoadoutScreenResultV1(
            InventoryLoadoutScreenStatusV1 status,
            string rejectionCode,
            InventoryLoadoutScreenSnapshotV1 snapshot,
            PlayerRouteProfilePayloadV1 routePayload)
        {
            if (!Enum.IsDefined(
                typeof(InventoryLoadoutScreenStatusV1),
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

        public InventoryLoadoutScreenStatusV1 Status { get; }
        public string RejectionCode { get; }
        public InventoryLoadoutScreenSnapshotV1 Snapshot { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public bool ChangedSelection
        {
            get
            {
                return Status
                    == InventoryLoadoutScreenStatusV1.SelectionChanged;
            }
        }
        public bool LeavesScreen
        {
            get
            {
                return Status == InventoryLoadoutScreenStatusV1.Confirmed
                    || Status == InventoryLoadoutScreenStatusV1.Cancelled;
            }
        }
    }

    /// <summary>
    /// Engine-independent screen draft. Character mount policy decides which weapon
    /// positions are configurable; inactive positions remain null and do not reserve an
    /// equipment instance. Armor behavior remains unchanged.
    /// </summary>
    public sealed class InventoryLoadoutScreenServiceV1
    {
        private readonly PlayerRouteProfilePayloadV1 incomingRoutePayload;
        private readonly IPlayerHoldingsAuthorityV1 holdingsAuthority;
        private readonly IEquipmentCatalogProvider equipmentCatalogProvider;
        private readonly IInventoryLoadoutAuthorityPortV1 loadoutAuthority;
        private readonly Dictionary<StableId, StableId> draftBindings =
            new Dictionary<StableId, StableId>();
        private InventoryLoadoutScreenSnapshotV1 snapshot;
        private bool completed;

        public InventoryLoadoutScreenServiceV1(
            PlayerRouteProfilePayloadV1 incomingRoutePayload,
            IPlayerHoldingsAuthorityV1 holdingsAuthority,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            IInventoryLoadoutAuthorityPortV1 loadoutAuthority)
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

        public PlayerRouteProfilePayloadV1 IncomingRoutePayload
        {
            get { return incomingRoutePayload; }
        }
        public InventoryLoadoutScreenSnapshotV1 Snapshot
        {
            get { return snapshot; }
        }

        public InventoryLoadoutScreenResultV1 Refresh()
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            RefreshInternal();
            return Result(
                InventoryLoadoutScreenStatusV1.Refreshed,
                string.Empty);
        }

        public InventoryLoadoutScreenResultV1 TrySelect(
            StableId slotStableId,
            StableId equipmentInstanceStableId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            InventoryLoadoutSlotDescriptorV1 slot;
            if (!InventoryLoadoutSlotsV1.TryFind(
                slotStableId,
                out slot))
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidSlot,
                    "inventory-loadout-slot-unknown");
            }
            if (!IsConfigurable(slot))
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidSlot,
                    "inventory-loadout-slot-unavailable-for-profile");
            }
            if (equipmentInstanceStableId == null)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.MissingEquipment,
                    "inventory-loadout-instance-missing");
            }

            InventoryLoadoutEquipmentProjectionV1 equipment =
                snapshot.FindEquipment(equipmentInstanceStableId);
            if (equipment == null)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.MissingEquipment,
                    "inventory-loadout-instance-not-owned");
            }
            if (!equipment.IsSelectable || !equipment.SlotKind.HasValue)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidEquipment,
                    string.IsNullOrEmpty(equipment.RejectionCode)
                        ? "inventory-loadout-instance-invalid"
                        : equipment.RejectionCode);
            }
            if (equipment.SlotKind.Value != slot.Kind)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.WrongEquipmentType,
                    "inventory-loadout-instance-wrong-slot-kind");
            }

            StableId current;
            draftBindings.TryGetValue(slot.SlotStableId, out current);
            if (current == equipmentInstanceStableId)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.NoChange,
                    "inventory-loadout-selection-already-current");
            }
            foreach (KeyValuePair<StableId, StableId> pair in draftBindings)
            {
                if (pair.Key != slot.SlotStableId
                    && pair.Value == equipmentInstanceStableId)
                {
                    return Result(
                        InventoryLoadoutScreenStatusV1
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
                InventoryLoadoutScreenStatusV1.SelectionChanged,
                string.Empty);
        }

        public InventoryLoadoutScreenResultV1 TryUnequip(
            StableId slotStableId)
        {
            if (completed)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AlreadyCompleted,
                    "inventory-loadout-screen-completed");
            }
            InventoryLoadoutSlotDescriptorV1 slot;
            if (!InventoryLoadoutSlotsV1.TryFind(
                slotStableId,
                out slot))
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidSlot,
                    "inventory-loadout-slot-unknown");
            }
            if (!IsConfigurable(slot))
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidSlot,
                    "inventory-loadout-slot-unavailable-for-profile");
            }
            StableId current;
            draftBindings.TryGetValue(slot.SlotStableId, out current);
            if (current == null)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.NoChange,
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
            RefreshInternal();
            InventoryLoadoutScreenResultV1 validationFailure =
                ValidateForConfirm();
            if (validationFailure != null)
            {
                return validationFailure;
            }

            long holdingsSequenceBefore = holdingsAuthority.Sequence;
            string holdingsFingerprintBefore =
                snapshot.HoldingsFingerprint;
            InventoryLoadoutAuthoritySnapshotV1 authorityBefore =
                loadoutAuthority.ExportSnapshot();
            if (authorityBefore == null
                || !authorityBefore.HasValidFingerprint())
            {
                return Result(
                    InventoryLoadoutScreenStatusV1
                        .AuthoritySnapshotMismatch,
                    "inventory-loadout-authority-snapshot-invalid");
            }

            var command = new InventoryLoadoutAuthorityCommandV1(
                authorityBefore.Sequence,
                holdingsSequenceBefore,
                BuildDraftBindings());
            InventoryLoadoutAuthorityResultV1 authorityResult =
                loadoutAuthority.Apply(command);
            if (authorityResult == null || !authorityResult.Succeeded)
            {
                return Result(
                    InventoryLoadoutScreenStatusV1.AuthorityRejected,
                    authorityResult == null
                        ? "inventory-loadout-authority-result-null"
                        : authorityResult.RejectionCode);
            }

            PlayerHoldingsSnapshotV1 holdingsAfter =
                holdingsAuthority.ExportSnapshot();
            if (holdingsAuthority.Sequence != holdingsSequenceBefore
                || !string.Equals(
                    holdingsAfter.Fingerprint,
                    holdingsFingerprintBefore,
                    StringComparison.Ordinal))
            {
                RefreshInternal();
                return Result(
                    InventoryLoadoutScreenStatusV1
                        .HoldingsChangedDuringApply,
                    "inventory-loadout-authority-mutated-holdings");
            }

            InventoryLoadoutAuthoritySnapshotV1 authorityAfter =
                authorityResult.Snapshot
                ?? loadoutAuthority.ExportSnapshot();
            if (!MatchesCommand(authorityAfter, command))
            {
                RefreshInternal();
                return Result(
                    InventoryLoadoutScreenStatusV1
                        .AuthoritySnapshotMismatch,
                    "inventory-loadout-authority-result-mismatch");
            }

            var orderedWeaponInstances = new List<StableId>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);
            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                orderedWeaponInstances.Add(
                    draftBindings[
                        InventoryLoadoutSlotsV1.All[index]
                            .SlotStableId]);
            }

            PlayerRouteProfilePayloadV1 confirmedPayload =
                PlayerRouteProfilePayloadV1.Create(
                    incomingRoutePayload.SelectedCharacterStableId,
                    incomingRoutePayload.LoadoutProfileStableId,
                    orderedWeaponInstances);
            completed = true;
            RebuildSnapshot(
                holdingsSequenceBefore,
                holdingsAfter.Fingerprint,
                authorityAfter.Sequence,
                authorityAfter.Fingerprint,
                snapshot.Equipment);
            return new InventoryLoadoutScreenResultV1(
                InventoryLoadoutScreenStatusV1.Confirmed,
                string.Empty,
                snapshot,
                confirmedPayload);
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
            RebuildSnapshot(
                snapshot.HoldingsSequence,
                snapshot.HoldingsFingerprint,
                snapshot.LoadoutSequence,
                snapshot.LoadoutFingerprint,
                snapshot.Equipment);
            return new InventoryLoadoutScreenResultV1(
                InventoryLoadoutScreenStatusV1.Cancelled,
                string.Empty,
                snapshot,
                incomingRoutePayload);
        }

        private bool IsConfigurable(
            InventoryLoadoutSlotDescriptorV1 slot)
        {
            return slot.Kind != InventoryLoadoutSlotKindV1.Weapon
                || ProductionWeaponMountPolicyV1
                    .IsConfigurableLoadoutSlot(
                        incomingRoutePayload.LoadoutProfileStableId,
                        slot.SlotStableId);
        }

        private void InitializeDraftBindings()
        {
            for (int index = 0;
                index < InventoryLoadoutSlotsV1.All.Count;
                index++)
            {
                draftBindings.Add(
                    InventoryLoadoutSlotsV1.All[index].SlotStableId,
                    null);
            }
            for (int index = 0;
                index < incomingRoutePayload.WeaponSlots.Count;
                index++)
            {
                PlayerRouteWeaponSlotV1 routeSlot =
                    incomingRoutePayload.WeaponSlots[index];
                if (ProductionWeaponMountPolicyV1
                    .IsConfigurableLoadoutSlot(
                        incomingRoutePayload.LoadoutProfileStableId,
                        routeSlot.WeaponSlotStableId))
                {
                    draftBindings[routeSlot.WeaponSlotStableId] =
                        routeSlot.EquipmentInstanceStableId;
                }
            }

            InventoryLoadoutAuthoritySnapshotV1 authoritySnapshot =
                loadoutAuthority.ExportSnapshot();
            if (authoritySnapshot == null
                || !authoritySnapshot.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The loadout authority returned an invalid initial snapshot.",
                    nameof(loadoutAuthority));
            }
            for (int index =
                    PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index < InventoryLoadoutSlotsV1.All.Count;
                index++)
            {
                InventoryLoadoutSlotDescriptorV1 slot =
                    InventoryLoadoutSlotsV1.All[index];
                draftBindings[slot.SlotStableId] =
                    authoritySnapshot.GetBinding(slot.SlotStableId)
                        .EquipmentInstanceStableId;
            }
        }

        private void RefreshInternal()
        {
            PlayerHoldingsSnapshotV1 holdingsSnapshot =
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
            InventoryLoadoutAuthoritySnapshotV1 loadoutSnapshot =
                loadoutAuthority.ExportSnapshot();
            if (loadoutSnapshot == null
                || !loadoutSnapshot.HasValidFingerprint())
            {
                throw new InvalidOperationException(
                    "The loadout authority returned an invalid snapshot.");
            }

            var equipment =
                new List<InventoryLoadoutEquipmentProjectionV1>();
            for (int index = 0;
                index < holdingsSnapshot.UniqueHoldings.Count;
                index++)
            {
                UniqueHoldingSnapshotV1 holding =
                    holdingsSnapshot.UniqueHoldings[index];
                if (holding.RewardKind
                    != RewardGrantKindV1.EquipmentReference)
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
                    categoryId == EquipmentCategoryIds.Weapon
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
                    new InventoryLoadoutEquipmentProjectionV1(
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
                InventoryLoadoutEquipmentProjectionV1 left,
                InventoryLoadoutEquipmentProjectionV1 right)
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
            IEnumerable<InventoryLoadoutEquipmentProjectionV1> equipment)
        {
            var equipmentCopy =
                new List<InventoryLoadoutEquipmentProjectionV1>(equipment);
            var selections =
                new List<InventoryLoadoutSelectionProjectionV1>(
                    InventoryLoadoutSlotsV1.All.Count);
            var seen = new HashSet<StableId>();
            bool canConfirm = true;
            for (int index = 0;
                index < InventoryLoadoutSlotsV1.All.Count;
                index++)
            {
                InventoryLoadoutSlotDescriptorV1 slot =
                    InventoryLoadoutSlotsV1.All[index];
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
                    if (slot.Kind == InventoryLoadoutSlotKindV1.Weapon)
                    {
                        valid = false;
                        rejectionCode =
                            "inventory-loadout-weapon-slot-empty";
                    }
                }
                else
                {
                    InventoryLoadoutEquipmentProjectionV1 projected =
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
                    new InventoryLoadoutSelectionProjectionV1(
                        slot,
                        selected,
                        valid,
                        rejectionCode));
            }

            snapshot = new InventoryLoadoutScreenSnapshotV1(
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

        private InventoryLoadoutScreenResultV1 ValidateForConfirm()
        {
            for (int index = 0;
                index < snapshot.Selections.Count;
                index++)
            {
                InventoryLoadoutSelectionProjectionV1 selection =
                    snapshot.Selections[index];
                if (selection.IsValid)
                {
                    continue;
                }
                if (selection.Slot.Kind
                        == InventoryLoadoutSlotKindV1.Weapon
                    && selection.EquipmentInstanceStableId == null)
                {
                    return Result(
                        InventoryLoadoutScreenStatusV1
                            .IncompleteWeaponLoadout,
                        selection.RejectionCode);
                }
                if (string.Equals(
                    selection.RejectionCode,
                    "inventory-loadout-selection-stale",
                    StringComparison.Ordinal))
                {
                    return Result(
                        InventoryLoadoutScreenStatusV1.StaleSelection,
                        selection.RejectionCode);
                }
                return Result(
                    InventoryLoadoutScreenStatusV1.InvalidEquipment,
                    selection.RejectionCode);
            }
            return null;
        }

        private List<InventoryLoadoutSlotBindingV1> BuildDraftBindings()
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

        private static InventoryLoadoutEquipmentProjectionV1 FindEquipment(
            IList<InventoryLoadoutEquipmentProjectionV1> equipment,
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
            InventoryLoadoutAuthoritySnapshotV1 snapshot,
            InventoryLoadoutAuthorityCommandV1 command)
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

        private InventoryLoadoutScreenResultV1 Result(
            InventoryLoadoutScreenStatusV1 status,
            string rejectionCode)
        {
            return new InventoryLoadoutScreenResultV1(
                status,
                rejectionCode,
                snapshot,
                null);
        }
    }

    internal static class InventoryLoadoutCanonicalV1
    {
        public static ReadOnlyCollection<InventoryLoadoutSlotBindingV1>
            CanonicalizeBindings(
                IEnumerable<InventoryLoadoutSlotBindingV1> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var canonical = new InventoryLoadoutSlotBindingV1[
                InventoryLoadoutSlotsV1.All.Count];
            foreach (InventoryLoadoutSlotBindingV1 binding in source)
            {
                if (binding == null)
                {
                    throw new ArgumentException(
                        "Loadout bindings cannot contain null.",
                        nameof(source));
                }
                InventoryLoadoutSlotDescriptorV1 descriptor;
                if (!InventoryLoadoutSlotsV1.TryFind(
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
                    new InventoryLoadoutSlotBindingV1(
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
            return new ReadOnlyCollection<InventoryLoadoutSlotBindingV1>(
                canonical);
        }

        public static string ComputeSnapshotFingerprint(
            long sequence,
            IReadOnlyList<InventoryLoadoutSlotBindingV1> bindings)
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
            IReadOnlyList<InventoryLoadoutSlotBindingV1> bindings)
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
            IReadOnlyList<InventoryLoadoutSlotBindingV1> bindings)
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.Production
{
    public enum WeaponMountAvailability
    {
        Active = 1,
        LockedBySkill = 2,
    }

    /// <summary>
    /// One physical class-owned weapon mount. A physical mount may be active and empty, or visible
    /// but skill-locked. Nonexistent mounts are absent from the layout entirely.
    /// </summary>
    public sealed class WeaponMountPosition
    {
        public WeaponMountPosition(
            StableId mountStableId,
            StableId loadoutSlotStableId,
            string displayName,
            double lateralOffset)
            : this(
                mountStableId,
                loadoutSlotStableId,
                displayName,
                WeaponMountAvailability.Active,
                lateralOffset)
        {
        }

        public WeaponMountPosition(
            StableId mountStableId,
            StableId loadoutSlotStableId,
            string displayName,
            WeaponMountAvailability availability,
            double lateralOffset)
        {
            MountStableId = mountStableId
                ?? throw new ArgumentNullException(nameof(mountStableId));
            LoadoutSlotStableId = loadoutSlotStableId
                ?? throw new ArgumentNullException(nameof(loadoutSlotStableId));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A mount display name is required.",
                    nameof(displayName));
            }
            if (!Enum.IsDefined(
                    typeof(WeaponMountAvailability),
                    availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }
            if (double.IsNaN(lateralOffset)
                || double.IsInfinity(lateralOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(lateralOffset));
            }

            DisplayName = displayName.Trim();
            Availability = availability;
            LateralOffset = lateralOffset;
            LockReason = availability
                    == WeaponMountAvailability.LockedBySkill
                ? "A skill is required to activate this mount."
                : string.Empty;
        }

        public StableId MountStableId { get; }
        public StableId LoadoutSlotStableId { get; }
        public string DisplayName { get; }
        public WeaponMountAvailability Availability { get; }
        public double LateralOffset { get; }
        public string LockReason { get; }

        public bool IsActive
        {
            get
            {
                return Availability
                    == WeaponMountAvailability.Active;
            }
        }

        public bool IsLockedBySkill
        {
            get
            {
                return Availability
                    == WeaponMountAvailability.LockedBySkill;
            }
        }
    }

    public sealed class WeaponMountViewBinding
    {
        public WeaponMountViewBinding(
            StableId mountStableId,
            StableId equipmentInstanceStableId)
        {
            MountStableId = mountStableId
                ?? throw new ArgumentNullException(nameof(mountStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId;
        }

        public StableId MountStableId { get; }
        public StableId EquipmentInstanceStableId { get; }
        public bool IsBound { get { return EquipmentInstanceStableId != null; } }
    }

    public sealed class WeaponMountLayout
    {
        private readonly ReadOnlyCollection<WeaponMountPosition>
            physicalPositions;
        private readonly ReadOnlyCollection<WeaponMountPosition>
            activePositions;
        private readonly ReadOnlyCollection<WeaponMountPosition>
            lockedBySkillPositions;

        internal WeaponMountLayout(
            StableId loadoutProfileStableId,
            IEnumerable<WeaponMountPosition> positions)
        {
            LoadoutProfileStableId = loadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(loadoutProfileStableId));
            var physical = new List<WeaponMountPosition>(
                positions ?? throw new ArgumentNullException(nameof(positions)));
            if (physical.Count < 2 || physical.Count > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(positions));
            }

            var mountIds = new HashSet<StableId>();
            var slotIds = new HashSet<StableId>();
            var active = new List<WeaponMountPosition>();
            var locked = new List<WeaponMountPosition>();
            for (int index = 0; index < physical.Count; index++)
            {
                WeaponMountPosition position = physical[index];
                if (position == null
                    || !mountIds.Add(position.MountStableId)
                    || !slotIds.Add(position.LoadoutSlotStableId))
                {
                    throw new ArgumentException(
                        "Physical mount and loadout bridge identities must be unique.",
                        nameof(positions));
                }
                if (position.IsActive)
                {
                    active.Add(position);
                }
                else
                {
                    locked.Add(position);
                }
            }
            if (active.Count == 0)
            {
                throw new ArgumentException(
                    "A production class requires at least one active weapon mount.",
                    nameof(positions));
            }

            physicalPositions =
                new ReadOnlyCollection<WeaponMountPosition>(physical);
            activePositions =
                new ReadOnlyCollection<WeaponMountPosition>(active);
            lockedBySkillPositions =
                new ReadOnlyCollection<WeaponMountPosition>(locked);
        }

        public StableId LoadoutProfileStableId { get; }
        public IReadOnlyList<WeaponMountPosition> PhysicalPositions
        {
            get { return physicalPositions; }
        }
        public IReadOnlyList<WeaponMountPosition> Positions
        {
            get { return physicalPositions; }
        }
        public IReadOnlyList<WeaponMountPosition> ConfigurablePositions
        {
            get { return activePositions; }
        }
        public IReadOnlyList<WeaponMountPosition> LockedBySkillPositions
        {
            get { return lockedBySkillPositions; }
        }
        public int PhysicalMountCount { get { return physicalPositions.Count; } }
        public int ActiveMountCount { get { return activePositions.Count; } }
        public int LockedBySkillMountCount
        {
            get { return lockedBySkillPositions.Count; }
        }
        public int BaselineEnabledMountCount { get { return ActiveMountCount; } }

        public bool ContainsLoadoutSlot(StableId slotStableId)
        {
            return Contains(activePositions, slotStableId);
        }

        public bool ContainsPhysicalLoadoutSlot(StableId slotStableId)
        {
            return Contains(physicalPositions, slotStableId);
        }

        private static bool Contains(
            IReadOnlyList<WeaponMountPosition> positions,
            StableId slotStableId)
        {
            if (slotStableId == null)
            {
                return false;
            }
            for (int index = 0; index < positions.Count; index++)
            {
                if (positions[index].LoadoutSlotStableId == slotStableId)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// One projection per physical mount. Active mounts may be empty. EnabledBindings contains
    /// only active mounts that currently hold an exact instance ID.
    /// </summary>
    public sealed class WeaponMountSet
    {
        private readonly ReadOnlyCollection<WeaponMountViewBinding>
            physicalBindings;
        private readonly ReadOnlyCollection<WeaponMountViewBinding>
            configuredBindings;
        private readonly ReadOnlyCollection<WeaponMountViewBinding>
            enabledBindings;

        internal WeaponMountSet(
            WeaponMountLayout layout,
            IEnumerable<WeaponMountViewBinding> bindings)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            var physical = new List<WeaponMountViewBinding>(
                bindings ?? throw new ArgumentNullException(nameof(bindings)));
            if (physical.Count != layout.PhysicalPositions.Count)
            {
                throw new ArgumentException(
                    "Every physical mount requires one projection binding.",
                    nameof(bindings));
            }

            var active = new List<WeaponMountViewBinding>();
            var enabled = new List<WeaponMountViewBinding>();
            for (int index = 0; index < physical.Count; index++)
            {
                WeaponMountPosition position =
                    layout.PhysicalPositions[index];
                WeaponMountViewBinding binding = physical[index];
                if (binding == null
                    || binding.MountStableId != position.MountStableId)
                {
                    throw new ArgumentException(
                        "Mount bindings must follow the physical layout order.",
                        nameof(bindings));
                }
                if (position.IsLockedBySkill && binding.IsBound)
                {
                    throw new ArgumentException(
                        "A skill-locked weapon mount must remain unbound.",
                        nameof(bindings));
                }
                if (position.IsActive)
                {
                    active.Add(binding);
                    if (binding.IsBound)
                    {
                        enabled.Add(binding);
                    }
                }
            }

            physicalBindings =
                new ReadOnlyCollection<WeaponMountViewBinding>(physical);
            configuredBindings =
                new ReadOnlyCollection<WeaponMountViewBinding>(active);
            enabledBindings =
                new ReadOnlyCollection<WeaponMountViewBinding>(enabled);
        }

        public WeaponMountLayout Layout { get; }
        public IReadOnlyList<WeaponMountViewBinding> PhysicalBindings
        {
            get { return physicalBindings; }
        }
        public IReadOnlyList<WeaponMountViewBinding> ConfiguredBindings
        {
            get { return configuredBindings; }
        }
        public IReadOnlyList<WeaponMountViewBinding> EnabledBindings
        {
            get { return enabledBindings; }
        }
    }

    public static class WeaponMountPolicy
    {
        public const string AggressiveLoadoutProfileId =
            "loadout-profile.striker";
        public const string HealerLoadoutProfileId =
            "loadout-profile.combat-medic";
        public const string DefensiveLoadoutProfileId =
            "loadout-profile.juggernaut";

        private const string AggressiveProfileSuffix = "-aggressive";
        private const string HealerProfileSuffix = "-healer";
        private const string DefensiveProfileSuffix = "-defensive";
        private const string LoadoutProfilePrefix = "loadout-profile.";

        public static readonly StableId OuterLeftMountStableId =
            StableId.Parse("weapon-mount.outer-left");
        public static readonly StableId InnerLeftMountStableId =
            StableId.Parse("weapon-mount.inner-left");
        public static readonly StableId CenterMountStableId =
            StableId.Parse("weapon-mount.center");
        public static readonly StableId InnerRightMountStableId =
            StableId.Parse("weapon-mount.inner-right");
        public static readonly StableId OuterRightMountStableId =
            StableId.Parse("weapon-mount.outer-right");

        private static readonly WeaponMountPosition OuterLeft =
            new WeaponMountPosition(
                OuterLeftMountStableId,
                InventoryLoadoutSlotIds.WeaponOne,
                "Outer Left",
                -0.9d);
        private static readonly WeaponMountPosition InnerLeft =
            new WeaponMountPosition(
                InnerLeftMountStableId,
                InventoryLoadoutSlotIds.WeaponTwo,
                "Inner Left",
                -0.3d);
        private static readonly WeaponMountPosition Center =
            new WeaponMountPosition(
                CenterMountStableId,
                InventoryLoadoutSlotIds.WeaponTwo,
                "Center",
                0d);
        private static readonly WeaponMountPosition
            AggressiveLockedCenter =
                new WeaponMountPosition(
                    CenterMountStableId,
                    InventoryLoadoutSlotIds.WeaponTwo,
                    "Center",
                    WeaponMountAvailability.LockedBySkill,
                    0d);
        private static readonly WeaponMountPosition InnerRight =
            new WeaponMountPosition(
                InnerRightMountStableId,
                InventoryLoadoutSlotIds.WeaponThree,
                "Inner Right",
                0.3d);
        private static readonly WeaponMountPosition OuterRight =
            new WeaponMountPosition(
                OuterRightMountStableId,
                InventoryLoadoutSlotIds.WeaponFour,
                "Outer Right",
                0.9d);

        private static readonly WeaponMountLayout Aggressive =
            new WeaponMountLayout(
                StableId.Parse(AggressiveLoadoutProfileId),
                new[] { OuterLeft, AggressiveLockedCenter, OuterRight });
        private static readonly WeaponMountLayout Healer =
            new WeaponMountLayout(
                StableId.Parse(HealerLoadoutProfileId),
                new[] { OuterLeft, Center, OuterRight });
        private static readonly WeaponMountLayout Defensive =
            new WeaponMountLayout(
                StableId.Parse(DefensiveLoadoutProfileId),
                new[] { OuterLeft, InnerLeft, InnerRight, OuterRight });

        public static WeaponMountLayout ResolveLayout(
            StableId loadoutProfileStableId)
        {
            string value = loadoutProfileStableId == null
                ? string.Empty
                : loadoutProfileStableId.ToString();
            if (string.Equals(
                    value,
                    AggressiveLoadoutProfileId,
                    StringComparison.Ordinal)
                || IsCharacterClassProfile(value, AggressiveProfileSuffix))
            {
                return Aggressive;
            }
            if (string.Equals(
                    value,
                    HealerLoadoutProfileId,
                    StringComparison.Ordinal)
                || IsCharacterClassProfile(value, HealerProfileSuffix))
            {
                return Healer;
            }
            if (string.Equals(
                    value,
                    DefensiveLoadoutProfileId,
                    StringComparison.Ordinal)
                || IsCharacterClassProfile(value, DefensiveProfileSuffix))
            {
                return Defensive;
            }
            throw new ArgumentException(
                "Unsupported production weapon mount profile: "
                    + (string.IsNullOrEmpty(value) ? "<null>" : value),
                nameof(loadoutProfileStableId));
        }

        public static bool IsConfigurableLoadoutSlot(
            StableId loadoutProfileStableId,
            StableId slotStableId)
        {
            return ResolveLayout(loadoutProfileStableId)
                .ContainsLoadoutSlot(slotStableId);
        }

        public static PlayerRouteProfilePayload NormalizeRoutePayload(
            PlayerRouteProfilePayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }
            WeaponMountLayout layout = ResolveLayout(
                payload.LoadoutProfileStableId);
            var instances = new List<StableId>(
                PlayerRouteProfilePayload.WeaponSlotCount);
            for (int index = 0;
                 index < PlayerRouteProfilePayload.WeaponSlotCount;
                 index++)
            {
                PlayerRouteWeaponSlot slot = payload.WeaponSlots[index];
                instances.Add(
                    layout.ContainsLoadoutSlot(slot.WeaponSlotStableId)
                        ? slot.EquipmentInstanceStableId
                        : null);
            }
            return PlayerRouteProfilePayload.Create(
                payload.SelectedCharacterStableId,
                payload.LoadoutProfileStableId,
                instances);
        }

        public static WeaponMountSet BuildMountSet(
            PlayerRouteProfilePayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }
            WeaponMountLayout layout = ResolveLayout(
                payload.LoadoutProfileStableId);
            var bindings = new List<WeaponMountViewBinding>(
                layout.PhysicalPositions.Count);
            for (int positionIndex = 0;
                 positionIndex < layout.PhysicalPositions.Count;
                 positionIndex++)
            {
                WeaponMountPosition position =
                    layout.PhysicalPositions[positionIndex];
                StableId instanceId = null;
                for (int slotIndex = 0;
                     slotIndex < payload.WeaponSlots.Count;
                     slotIndex++)
                {
                    PlayerRouteWeaponSlot slot = payload.WeaponSlots[slotIndex];
                    if (slot.WeaponSlotStableId
                        == position.LoadoutSlotStableId)
                    {
                        instanceId = slot.EquipmentInstanceStableId;
                        break;
                    }
                }
                if (position.IsLockedBySkill && instanceId != null)
                {
                    throw new InvalidOperationException(
                        "A skill-locked weapon mount is bound: "
                        + position.MountStableId);
                }
                bindings.Add(new WeaponMountViewBinding(
                    position.MountStableId,
                    instanceId));
            }
            return new WeaponMountSet(layout, bindings);
        }

        public static WeaponMountPosition FindPosition(
            WeaponMountLayout layout,
            StableId mountStableId)
        {
            if (layout == null || mountStableId == null)
            {
                return null;
            }
            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                WeaponMountPosition position =
                    layout.PhysicalPositions[index];
                if (position.MountStableId == mountStableId)
                {
                    return position;
                }
            }
            return null;
        }

        private static bool IsCharacterClassProfile(
            string value,
            string classSuffix)
        {
            return !string.IsNullOrEmpty(value)
                && value.StartsWith(
                    LoadoutProfilePrefix,
                    StringComparison.Ordinal)
                && value.EndsWith(classSuffix, StringComparison.Ordinal);
        }
    }
}

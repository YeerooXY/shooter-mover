using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.Production
{
    public enum ProductionWeaponMountAvailabilityV1
    {
        Active = 1,
        LockedBySkill = 2,
    }

    /// <summary>
    /// One physical class-owned weapon mount. Availability is explicit so a mount may exist in
    /// the class layout without being configurable, equipped, or enabled yet.
    /// </summary>
    public sealed class ProductionWeaponMountPositionV1
    {
        public ProductionWeaponMountPositionV1(
            StableId mountStableId,
            StableId loadoutSlotStableId,
            string displayName,
            double lateralOffset)
            : this(
                mountStableId,
                loadoutSlotStableId,
                displayName,
                ProductionWeaponMountAvailabilityV1.Active,
                lateralOffset)
        {
        }

        public ProductionWeaponMountPositionV1(
            StableId mountStableId,
            StableId loadoutSlotStableId,
            string displayName,
            ProductionWeaponMountAvailabilityV1 availability,
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
                    typeof(ProductionWeaponMountAvailabilityV1),
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
        }

        public StableId MountStableId { get; }

        /// <summary>
        /// Temporary persistence/input bridge to the legacy loadout record. It is not evidence
        /// that every class owns four physical weapon mounts.
        /// </summary>
        public StableId LoadoutSlotStableId { get; }

        public string DisplayName { get; }

        public ProductionWeaponMountAvailabilityV1 Availability { get; }

        public double LateralOffset { get; }

        public bool IsActive
        {
            get
            {
                return Availability
                    == ProductionWeaponMountAvailabilityV1.Active;
            }
        }

        public bool IsLockedBySkill
        {
            get
            {
                return Availability
                    == ProductionWeaponMountAvailabilityV1.LockedBySkill;
            }
        }
    }

    /// <summary>
    /// Current projection for one physical mount. Locked mounts deliberately retain a null
    /// equipment identity until their owning skill makes them active.
    /// </summary>
    public sealed class ProductionWeaponMountBindingV1
    {
        public ProductionWeaponMountBindingV1(
            StableId mountStableId,
            StableId equipmentInstanceStableId)
        {
            MountStableId = mountStableId
                ?? throw new ArgumentNullException(nameof(mountStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId;
        }

        public StableId MountStableId { get; }

        public StableId EquipmentInstanceStableId { get; }

        public bool IsBound
        {
            get { return EquipmentInstanceStableId != null; }
        }
    }

    public sealed class ProductionWeaponMountLayoutV1
    {
        private readonly ReadOnlyCollection<ProductionWeaponMountPositionV1>
            physicalPositions;
        private readonly ReadOnlyCollection<ProductionWeaponMountPositionV1>
            activePositions;
        private readonly ReadOnlyCollection<ProductionWeaponMountPositionV1>
            lockedBySkillPositions;

        internal ProductionWeaponMountLayoutV1(
            StableId loadoutProfileStableId,
            IEnumerable<ProductionWeaponMountPositionV1> positions)
        {
            LoadoutProfileStableId = loadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(loadoutProfileStableId));

            var physical = new List<ProductionWeaponMountPositionV1>(
                positions ?? throw new ArgumentNullException(nameof(positions)));
            if (physical.Count < 2 || physical.Count > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(positions));
            }

            var mountIds = new HashSet<StableId>();
            var loadoutSlotIds = new HashSet<StableId>();
            var active = new List<ProductionWeaponMountPositionV1>();
            var locked = new List<ProductionWeaponMountPositionV1>();
            for (int index = 0; index < physical.Count; index++)
            {
                ProductionWeaponMountPositionV1 position = physical[index];
                if (position == null
                    || !mountIds.Add(position.MountStableId)
                    || !loadoutSlotIds.Add(position.LoadoutSlotStableId))
                {
                    throw new ArgumentException(
                        "Physical mount and loadout bridge identities must be unique.",
                        nameof(positions));
                }

                if (position.IsActive)
                {
                    active.Add(position);
                }
                else if (position.IsLockedBySkill)
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
                new ReadOnlyCollection<ProductionWeaponMountPositionV1>(physical);
            activePositions =
                new ReadOnlyCollection<ProductionWeaponMountPositionV1>(active);
            lockedBySkillPositions =
                new ReadOnlyCollection<ProductionWeaponMountPositionV1>(locked);
        }

        public StableId LoadoutProfileStableId { get; }

        public IReadOnlyList<ProductionWeaponMountPositionV1> PhysicalPositions
        {
            get { return physicalPositions; }
        }

        /// <summary>
        /// Compatibility name retained for callers that configure or grant only currently active
        /// mounts. Locked physical mounts are intentionally excluded.
        /// </summary>
        public IReadOnlyList<ProductionWeaponMountPositionV1>
            ConfigurablePositions
        {
            get { return activePositions; }
        }

        public IReadOnlyList<ProductionWeaponMountPositionV1>
            LockedBySkillPositions
        {
            get { return lockedBySkillPositions; }
        }

        public int PhysicalMountCount
        {
            get { return physicalPositions.Count; }
        }

        public int ActiveMountCount
        {
            get { return activePositions.Count; }
        }

        public int LockedBySkillMountCount
        {
            get { return lockedBySkillPositions.Count; }
        }

        public int BaselineEnabledMountCount
        {
            get { return ActiveMountCount; }
        }

        public bool ContainsLoadoutSlot(StableId slotStableId)
        {
            return Contains(activePositions, slotStableId);
        }

        public bool ContainsPhysicalLoadoutSlot(StableId slotStableId)
        {
            return Contains(physicalPositions, slotStableId);
        }

        private static bool Contains(
            IReadOnlyList<ProductionWeaponMountPositionV1> positions,
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

    public sealed class ProductionWeaponMountSetV1
    {
        private readonly ReadOnlyCollection<ProductionWeaponMountBindingV1>
            configuredBindings;
        private readonly ReadOnlyCollection<ProductionWeaponMountBindingV1>
            enabledBindings;

        internal ProductionWeaponMountSetV1(
            ProductionWeaponMountLayoutV1 layout,
            IEnumerable<ProductionWeaponMountBindingV1> bindings)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            var configured = new List<ProductionWeaponMountBindingV1>(
                bindings ?? throw new ArgumentNullException(nameof(bindings)));
            if (configured.Count != layout.PhysicalPositions.Count)
            {
                throw new ArgumentException(
                    "Every physical mount requires one projection binding.",
                    nameof(bindings));
            }

            var enabled = new List<ProductionWeaponMountBindingV1>();
            for (int index = 0; index < configured.Count; index++)
            {
                ProductionWeaponMountPositionV1 position =
                    layout.PhysicalPositions[index];
                ProductionWeaponMountBindingV1 binding = configured[index];
                if (binding == null
                    || binding.MountStableId != position.MountStableId)
                {
                    throw new ArgumentException(
                        "Mount bindings must follow the physical layout order.",
                        nameof(bindings));
                }
                if (position.IsActive && !binding.IsBound)
                {
                    throw new ArgumentException(
                        "An active weapon mount must be bound.",
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
                    enabled.Add(binding);
                }
            }

            configuredBindings =
                new ReadOnlyCollection<ProductionWeaponMountBindingV1>(configured);
            enabledBindings =
                new ReadOnlyCollection<ProductionWeaponMountBindingV1>(enabled);
        }

        public ProductionWeaponMountLayoutV1 Layout { get; }

        /// <summary>
        /// One projection per physical class mount, including locked mounts with null instance IDs.
        /// </summary>
        public IReadOnlyList<ProductionWeaponMountBindingV1>
            ConfiguredBindings
        {
            get { return configuredBindings; }
        }

        public IReadOnlyList<ProductionWeaponMountBindingV1> EnabledBindings
        {
            get { return enabledBindings; }
        }
    }

    public static class ProductionWeaponMountPolicyV1
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

        private static readonly ProductionWeaponMountPositionV1 OuterLeft =
            new ProductionWeaponMountPositionV1(
                OuterLeftMountStableId,
                InventoryLoadoutSlotIdsV1.WeaponOne,
                "Outer Left",
                -0.9d);
        private static readonly ProductionWeaponMountPositionV1 InnerLeft =
            new ProductionWeaponMountPositionV1(
                InnerLeftMountStableId,
                InventoryLoadoutSlotIdsV1.WeaponTwo,
                "Inner Left",
                -0.3d);
        private static readonly ProductionWeaponMountPositionV1 Center =
            new ProductionWeaponMountPositionV1(
                CenterMountStableId,
                InventoryLoadoutSlotIdsV1.WeaponTwo,
                "Center",
                0d);
        private static readonly ProductionWeaponMountPositionV1
            AggressiveLockedCenter =
                new ProductionWeaponMountPositionV1(
                    CenterMountStableId,
                    InventoryLoadoutSlotIdsV1.WeaponTwo,
                    "Center",
                    ProductionWeaponMountAvailabilityV1.LockedBySkill,
                    0d);
        private static readonly ProductionWeaponMountPositionV1 InnerRight =
            new ProductionWeaponMountPositionV1(
                InnerRightMountStableId,
                InventoryLoadoutSlotIdsV1.WeaponThree,
                "Inner Right",
                0.3d);
        private static readonly ProductionWeaponMountPositionV1 OuterRight =
            new ProductionWeaponMountPositionV1(
                OuterRightMountStableId,
                InventoryLoadoutSlotIdsV1.WeaponFour,
                "Outer Right",
                0.9d);

        private static readonly ProductionWeaponMountLayoutV1 Aggressive =
            new ProductionWeaponMountLayoutV1(
                StableId.Parse(AggressiveLoadoutProfileId),
                new[] { OuterLeft, AggressiveLockedCenter, OuterRight });
        private static readonly ProductionWeaponMountLayoutV1 Healer =
            new ProductionWeaponMountLayoutV1(
                StableId.Parse(HealerLoadoutProfileId),
                new[] { OuterLeft, Center, OuterRight });
        private static readonly ProductionWeaponMountLayoutV1 Defensive =
            new ProductionWeaponMountLayoutV1(
                StableId.Parse(DefensiveLoadoutProfileId),
                new[] { OuterLeft, InnerLeft, InnerRight, OuterRight });

        public static ProductionWeaponMountLayoutV1 ResolveLayout(
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

            // Non-production and test profiles retain the legacy four-active-position behavior.
            return Defensive;
        }

        public static bool IsConfigurableLoadoutSlot(
            StableId loadoutProfileStableId,
            StableId slotStableId)
        {
            return ResolveLayout(loadoutProfileStableId)
                .ContainsLoadoutSlot(slotStableId);
        }

        public static PlayerRouteProfilePayloadV1 NormalizeRoutePayload(
            PlayerRouteProfilePayloadV1 payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ProductionWeaponMountLayoutV1 layout = ResolveLayout(
                payload.LoadoutProfileStableId);
            var instances = new List<StableId>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);
            for (int index = 0;
                 index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                 index++)
            {
                PlayerRouteWeaponSlotV1 slot = payload.WeaponSlots[index];
                instances.Add(
                    layout.ContainsLoadoutSlot(slot.WeaponSlotStableId)
                        ? slot.EquipmentInstanceStableId
                        : null);
            }

            return PlayerRouteProfilePayloadV1.Create(
                payload.SelectedCharacterStableId,
                payload.LoadoutProfileStableId,
                instances);
        }

        public static ProductionWeaponMountSetV1 BuildMountSet(
            PlayerRouteProfilePayloadV1 payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ProductionWeaponMountLayoutV1 layout = ResolveLayout(
                payload.LoadoutProfileStableId);
            var bindings = new List<ProductionWeaponMountBindingV1>(
                layout.PhysicalPositions.Count);
            for (int positionIndex = 0;
                 positionIndex < layout.PhysicalPositions.Count;
                 positionIndex++)
            {
                ProductionWeaponMountPositionV1 position =
                    layout.PhysicalPositions[positionIndex];
                StableId equipmentInstanceStableId = null;
                for (int slotIndex = 0;
                     slotIndex < payload.WeaponSlots.Count;
                     slotIndex++)
                {
                    PlayerRouteWeaponSlotV1 slot =
                        payload.WeaponSlots[slotIndex];
                    if (slot.WeaponSlotStableId
                        == position.LoadoutSlotStableId)
                    {
                        equipmentInstanceStableId =
                            slot.EquipmentInstanceStableId;
                        break;
                    }
                }

                if (position.IsActive && equipmentInstanceStableId == null)
                {
                    throw new InvalidOperationException(
                        "An active weapon mount is unbound: "
                        + position.MountStableId);
                }
                if (position.IsLockedBySkill
                    && equipmentInstanceStableId != null)
                {
                    throw new InvalidOperationException(
                        "A skill-locked weapon mount is bound: "
                        + position.MountStableId);
                }

                bindings.Add(new ProductionWeaponMountBindingV1(
                    position.MountStableId,
                    equipmentInstanceStableId));
            }

            return new ProductionWeaponMountSetV1(layout, bindings);
        }

        public static ProductionWeaponMountPositionV1 FindPosition(
            ProductionWeaponMountLayoutV1 layout,
            StableId mountStableId)
        {
            if (layout == null || mountStableId == null)
            {
                return null;
            }

            for (int index = 0;
                 index < layout.PhysicalPositions.Count;
                 index++)
            {
                ProductionWeaponMountPositionV1 position =
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
                && value.EndsWith(
                    classSuffix,
                    StringComparison.Ordinal);
        }
    }
}

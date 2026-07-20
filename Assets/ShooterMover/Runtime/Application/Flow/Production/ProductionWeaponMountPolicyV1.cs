using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Physical mount metadata is deliberately separate from the configured equipment
    /// binding. A later ability may change which configured mounts are enabled without
    /// rewriting the profile loadout or moving an equipment instance between positions.
    /// </summary>
    public sealed class ProductionWeaponMountPositionV1
    {
        public ProductionWeaponMountPositionV1(
            StableId mountStableId,
            StableId loadoutSlotStableId,
            string displayName,
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
            if (double.IsNaN(lateralOffset)
                || double.IsInfinity(lateralOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(lateralOffset));
            }

            DisplayName = displayName.Trim();
            LateralOffset = lateralOffset;
        }

        public StableId MountStableId { get; }

        /// <summary>
        /// Compatibility bridge to the existing four-slot Inventory screen. This is
        /// presentation/input routing only; it is not the physical mount identity.
        /// </summary>
        public StableId LoadoutSlotStableId { get; }

        public string DisplayName { get; }

        public double LateralOffset { get; }
    }

    /// <summary>
    /// The persisted/configured fact: one physical mount identity points at one exact
    /// equipment instance. It contains no class, unlock, cooldown, ability, or timing rule.
    /// </summary>
    public sealed class ProductionWeaponMountBindingV1
    {
        public ProductionWeaponMountBindingV1(
            StableId mountStableId,
            StableId equipmentInstanceStableId)
        {
            MountStableId = mountStableId
                ?? throw new ArgumentNullException(nameof(mountStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId
                ?? throw new ArgumentNullException(
                    nameof(equipmentInstanceStableId));
        }

        public StableId MountStableId { get; }

        public StableId EquipmentInstanceStableId { get; }
    }

    public sealed class ProductionWeaponMountLayoutV1
    {
        private readonly ReadOnlyCollection<ProductionWeaponMountPositionV1>
            configurablePositions;

        internal ProductionWeaponMountLayoutV1(
            StableId loadoutProfileStableId,
            IEnumerable<ProductionWeaponMountPositionV1> configurablePositions)
        {
            LoadoutProfileStableId = loadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(loadoutProfileStableId));
            this.configurablePositions =
                new ReadOnlyCollection<ProductionWeaponMountPositionV1>(
                    new List<ProductionWeaponMountPositionV1>(
                        configurablePositions
                        ?? throw new ArgumentNullException(
                            nameof(configurablePositions))));
            if (this.configurablePositions.Count < 2
                || this.configurablePositions.Count > 4)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configurablePositions));
            }
        }

        public StableId LoadoutProfileStableId { get; }

        public IReadOnlyList<ProductionWeaponMountPositionV1>
            ConfigurablePositions
        {
            get { return configurablePositions; }
        }

        public int BaselineEnabledMountCount
        {
            get { return configurablePositions.Count; }
        }

        public bool ContainsLoadoutSlot(StableId slotStableId)
        {
            if (slotStableId == null)
            {
                return false;
            }

            for (int index = 0; index < configurablePositions.Count; index++)
            {
                if (configurablePositions[index].LoadoutSlotStableId
                    == slotStableId)
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
            IEnumerable<ProductionWeaponMountBindingV1> configuredBindings)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            var bindings = new List<ProductionWeaponMountBindingV1>(
                configuredBindings
                ?? throw new ArgumentNullException(nameof(configuredBindings)));
            this.configuredBindings =
                new ReadOnlyCollection<ProductionWeaponMountBindingV1>(
                    bindings);

            // V1 has no temporary activation effects. Keeping this as a distinct
            // projection is the extension seam for the later timed third mount.
            enabledBindings =
                new ReadOnlyCollection<ProductionWeaponMountBindingV1>(
                    new List<ProductionWeaponMountBindingV1>(bindings));
        }

        public ProductionWeaponMountLayoutV1 Layout { get; }

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
                new[] { OuterLeft, OuterRight });
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
                StringComparison.Ordinal))
            {
                return Aggressive;
            }
            if (string.Equals(
                value,
                HealerLoadoutProfileId,
                StringComparison.Ordinal))
            {
                return Healer;
            }
            if (string.Equals(
                value,
                DefensiveLoadoutProfileId,
                StringComparison.Ordinal))
            {
                return Defensive;
            }

            // Unknown/custom profiles fail closed to the smallest current layout.
            return Aggressive;
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
                layout.ConfigurablePositions.Count);
            for (int positionIndex = 0;
                positionIndex < layout.ConfigurablePositions.Count;
                positionIndex++)
            {
                ProductionWeaponMountPositionV1 position =
                    layout.ConfigurablePositions[positionIndex];
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

                if (equipmentInstanceStableId == null)
                {
                    throw new InvalidOperationException(
                        "A configurable weapon mount is unbound: "
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
                index < layout.ConfigurablePositions.Count;
                index++)
            {
                ProductionWeaponMountPositionV1 position =
                    layout.ConfigurablePositions[index];
                if (position.MountStableId == mountStableId)
                {
                    return position;
                }
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Application.Flow.Production
{
    public sealed class ProductionWeaponInventoryStateV2
    {
        public ProductionWeaponInventoryStateV2(
            PlayerRouteProfilePayloadV1 routePayload,
            PlayerHoldingsSnapshotV1 genericHoldings,
            WeaponHoldingsSnapshotV2 weaponHoldings,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            GenericHoldings = genericHoldings
                ?? throw new ArgumentNullException(nameof(genericHoldings));
            WeaponHoldings = weaponHoldings
                ?? throw new ArgumentNullException(nameof(weaponHoldings));
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        }

        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public PlayerHoldingsSnapshotV1 GenericHoldings { get; }
        public WeaponHoldingsSnapshotV2 WeaponHoldings { get; }
        public InventoryLoadoutAuthoritySnapshotV1 Loadout { get; }
    }

    /// <summary>
    /// Fresh-character starter onboarding only. Inventory never invokes this service.
    /// </summary>
    public static class ProductionWeaponOnboardingV2
    {
        private static readonly StableId HoldingsAuthorityStableId =
            StableId.Parse("authority.production-player-holdings");

        public static ProductionWeaponInventoryStateV2 CreateStarter(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            Func<StableId> instanceIdFactory = null)
        {
            if (characterInstanceStableId == null)
            {
                throw new ArgumentNullException(
                    nameof(characterInstanceStableId));
            }
            if (classDefinitionStableId == null)
            {
                throw new ArgumentNullException(nameof(classDefinitionStableId));
            }

            ProductionWeaponMarkV1 starter;
            if (!ProductionWeaponCatalogProvider.Current.TryGetMark(
                    ProductionWeaponOnboardingV1.StarterWeaponDefinitionId,
                    out starter)
                || starter == null)
            {
                throw new InvalidOperationException(
                    "The authored starter weapon is missing.");
            }

            ProductionWeaponMountLayoutV1 layout =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    classDefinitionStableId);
            var owned = new List<WeaponEquipmentInstance>(
                layout.ConfigurablePositions.Count);
            var bindings = EmptyBindings();
            var used = new HashSet<StableId>();
            Func<StableId> factory = instanceIdFactory
                ?? OwnedEquipmentInstanceIdFactory.Create;

            for (int index = 0;
                 index < layout.ConfigurablePositions.Count;
                 index++)
            {
                ProductionWeaponMountPositionV1 position =
                    layout.ConfigurablePositions[index];
                StableId instanceId = NextOpaqueId(factory, used);
                used.Add(instanceId);
                owned.Add(WeaponEquipmentInstance.CreateUnmodified(
                    instanceId,
                    starter.Blueprint.DefinitionId));
                int slotIndex = FindSlotIndex(position.LoadoutSlotStableId);
                bindings[slotIndex] = new InventoryLoadoutSlotBindingV1(
                    position.LoadoutSlotStableId,
                    instanceId);
            }

            InventoryLoadoutAuthoritySnapshotV1 loadout =
                InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                    0L,
                    bindings);
            PlayerRouteProfilePayloadV1 route =
                ProductionWeaponOnboardingV1.RouteFromLoadout(
                    characterInstanceStableId,
                    classDefinitionStableId,
                    loadout);
            var genericHoldings = new PlayerHoldingsService(
                HoldingsAuthorityStableId,
                999L,
                new ProductionEquipmentCatalogAdapterV1(
                    ProductionWeaponCatalogProvider.EquipmentCatalog));

            return new ProductionWeaponInventoryStateV2(
                route,
                genericHoldings.ExportSnapshot(),
                WeaponHoldingsSnapshotV2.CreateCanonical(0L, owned),
                loadout);
        }

        public static ProductionWeaponInventoryStateV2 Restore(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            PlayerHoldingsSnapshotV1 genericHoldings,
            WeaponHoldingsSnapshotV2 canonicalWeaponHoldings,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            if (genericHoldings == null)
            {
                throw new ArgumentNullException(nameof(genericHoldings));
            }
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            WeaponHoldingsSnapshotV2 weapons = canonicalWeaponHoldings
                ?? ProductionWeaponHoldingsMigrationV2.ConvertLegacy(
                    genericHoldings);
            IReadOnlyList<InventoryLoadoutSlotBindingV1> normalized =
                NormalizeLoadout(classDefinitionStableId, weapons, loadout);
            InventoryLoadoutAuthoritySnapshotV1 normalizedLoadout =
                InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                    BindingsEqual(loadout.Bindings, normalized)
                        ? loadout.Sequence
                        : checked(loadout.Sequence + 1L),
                    normalized);
            PlayerRouteProfilePayloadV1 route =
                ProductionWeaponOnboardingV1.RouteFromLoadout(
                    characterInstanceStableId,
                    classDefinitionStableId,
                    normalizedLoadout);
            return new ProductionWeaponInventoryStateV2(
                route,
                genericHoldings,
                weapons,
                normalizedLoadout);
        }

        private static IReadOnlyList<InventoryLoadoutSlotBindingV1>
            NormalizeLoadout(
                StableId classDefinitionStableId,
                WeaponHoldingsSnapshotV2 holdings,
                InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            ProductionWeaponMountLayoutV1 layout =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    classDefinitionStableId);
            var selected = new HashSet<StableId>();
            var bindings = new List<InventoryLoadoutSlotBindingV1>(
                InventoryLoadoutSlotsV1.All.Count);

            for (int index = 0;
                 index < InventoryLoadoutSlotsV1.All.Count;
                 index++)
            {
                InventoryLoadoutSlotDescriptorV1 slot =
                    InventoryLoadoutSlotsV1.All[index];
                StableId instanceId = loadout.GetBinding(slot.SlotStableId)
                    .EquipmentInstanceStableId;

                if (slot.Kind == InventoryLoadoutSlotKindV1.Weapon)
                {
                    if (!layout.ContainsLoadoutSlot(slot.SlotStableId)
                        || instanceId == null
                        || holdings.Find(instanceId) == null
                        || !selected.Add(instanceId))
                    {
                        instanceId = null;
                    }
                }
                bindings.Add(new InventoryLoadoutSlotBindingV1(
                    slot.SlotStableId,
                    instanceId));
            }
            return bindings;
        }

        private static List<InventoryLoadoutSlotBindingV1> EmptyBindings()
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
                    null));
            }
            return bindings;
        }

        private static int FindSlotIndex(StableId slotId)
        {
            for (int index = 0;
                 index < InventoryLoadoutSlotsV1.All.Count;
                 index++)
            {
                if (InventoryLoadoutSlotsV1.All[index].SlotStableId == slotId)
                {
                    return index;
                }
            }
            throw new InvalidOperationException(
                "The physical weapon mount does not bridge to a loadout slot.");
        }

        private static StableId NextOpaqueId(
            Func<StableId> factory,
            HashSet<StableId> used)
        {
            for (int attempt = 0; attempt < 128; attempt++)
            {
                StableId candidate = factory();
                if (candidate != null && !used.Contains(candidate))
                {
                    return candidate;
                }
            }
            throw new InvalidOperationException(
                "Unable to allocate a unique opaque weapon instance ID.");
        }

        private static bool BindingsEqual(
            IReadOnlyList<InventoryLoadoutSlotBindingV1> left,
            IReadOnlyList<InventoryLoadoutSlotBindingV1> right)
        {
            if (left == null
                || right == null
                || left.Count != right.Count)
            {
                return false;
            }
            for (int index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}

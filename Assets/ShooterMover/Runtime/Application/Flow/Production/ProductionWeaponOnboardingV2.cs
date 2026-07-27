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
            WeaponMountLoadoutSnapshotV2 weaponMountLoadout,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            GenericHoldings = genericHoldings
                ?? throw new ArgumentNullException(nameof(genericHoldings));
            WeaponHoldings = weaponHoldings
                ?? throw new ArgumentNullException(nameof(weaponHoldings));
            WeaponMountLoadout = weaponMountLoadout
                ?? throw new ArgumentNullException(nameof(weaponMountLoadout));
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        }

        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public PlayerHoldingsSnapshotV1 GenericHoldings { get; }
        public WeaponHoldingsSnapshotV2 WeaponHoldings { get; }
        public WeaponMountLoadoutSnapshotV2 WeaponMountLoadout { get; }
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
            var equippedByMount = new Dictionary<StableId, StableId>();
            var used = new HashSet<StableId>();
            Func<StableId> factory = instanceIdFactory;
            if (factory == null)
            {
                factory = OwnedEquipmentInstanceIdFactory.Create;
            }

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
                equippedByMount.Add(position.MountStableId, instanceId);
            }

            var mountBindings = new List<WeaponMountBindingV2>(
                layout.PhysicalPositions.Count);
            for (int index = 0;
                 index < layout.PhysicalPositions.Count;
                 index++)
            {
                ProductionWeaponMountPositionV1 position =
                    layout.PhysicalPositions[index];
                StableId instanceId;
                equippedByMount.TryGetValue(position.MountStableId, out instanceId);
                mountBindings.Add(new WeaponMountBindingV2(
                    position.MountStableId,
                    position.IsActive ? instanceId : null));
            }

            WeaponHoldingsSnapshotV2 weaponHoldings =
                WeaponHoldingsSnapshotV2.CreateCanonical(0L, owned);
            WeaponMountLoadoutSnapshotV2 weaponMountLoadout =
                WeaponMountLoadoutSnapshotV2.CreateCanonical(
                    0L,
                    mountBindings);
            InventoryLoadoutAuthoritySnapshotV1 loadout =
                ProductionWeaponMountLoadoutProjectionV2.ToLegacyProjection(
                    layout,
                    weaponMountLoadout,
                    InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                        0L,
                        EmptyBindings()));
            PlayerRouteProfilePayloadV1 route =
                ProductionWeaponMountLoadoutProjectionV2.Route(
                    characterInstanceStableId,
                    classDefinitionStableId,
                    layout,
                    weaponMountLoadout);
            var genericHoldings = new PlayerHoldingsService(
                HoldingsAuthorityStableId,
                999L,
                new ProductionEquipmentCatalogAdapterV1(
                    ProductionWeaponCatalogProvider.EquipmentCatalog));

            return new ProductionWeaponInventoryStateV2(
                route,
                genericHoldings.ExportSnapshot(),
                weaponHoldings,
                weaponMountLoadout,
                loadout);
        }

        public static ProductionWeaponInventoryStateV2 Restore(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            PlayerHoldingsSnapshotV1 genericHoldings,
            WeaponHoldingsSnapshotV2 canonicalWeaponHoldings,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            return Restore(
                characterInstanceStableId,
                classDefinitionStableId,
                genericHoldings,
                canonicalWeaponHoldings,
                null,
                loadout);
        }

        public static ProductionWeaponInventoryStateV2 Restore(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            PlayerHoldingsSnapshotV1 genericHoldings,
            WeaponHoldingsSnapshotV2 canonicalWeaponHoldings,
            WeaponMountLoadoutSnapshotV2 canonicalWeaponMountLoadout,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            if (characterInstanceStableId == null)
            {
                throw new ArgumentNullException(nameof(characterInstanceStableId));
            }
            if (classDefinitionStableId == null)
            {
                throw new ArgumentNullException(nameof(classDefinitionStableId));
            }
            if (genericHoldings == null)
            {
                throw new ArgumentNullException(nameof(genericHoldings));
            }
            if (loadout == null || !loadout.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid legacy loadout/armor projection is required.",
                    nameof(loadout));
            }

            WeaponHoldingsSnapshotV2 weapons = canonicalWeaponHoldings
                ?? ProductionWeaponHoldingsMigrationV2.ConvertLegacy(
                    genericHoldings);
            var weaponAuthority = new ProductionWeaponHoldingsAuthorityV2(weapons);
            ProductionWeaponMountLayoutV1 layout =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    classDefinitionStableId);
            WeaponMountLoadoutSnapshotV2 mounts = canonicalWeaponMountLoadout
                ?? ProductionWeaponMountLoadoutProjectionV2.MigrateLegacy(
                    layout,
                    weaponAuthority,
                    loadout);

            // V2 is strict: unknown, missing, locked, duplicate or unowned mount bindings reject
            // restore instead of being silently repaired. Only the V1 conversion normalizes legacy
            // placeholders as part of deterministic dual-read migration.
            var mountAuthority = new ProductionWeaponMountLoadoutAuthorityV2(
                layout,
                weaponAuthority,
                mounts);
            WeaponMountLoadoutSnapshotV2 canonicalMounts =
                mountAuthority.ExportSnapshot();
            InventoryLoadoutAuthoritySnapshotV1 compatibilityLoadout =
                ProductionWeaponMountLoadoutProjectionV2.ToLegacyProjection(
                    layout,
                    canonicalMounts,
                    loadout);
            PlayerRouteProfilePayloadV1 route =
                ProductionWeaponMountLoadoutProjectionV2.Route(
                    characterInstanceStableId,
                    classDefinitionStableId,
                    layout,
                    canonicalMounts);

            return new ProductionWeaponInventoryStateV2(
                route,
                genericHoldings,
                weapons,
                canonicalMounts,
                compatibilityLoadout);
        }

        private static IEnumerable<InventoryLoadoutSlotBindingV1> EmptyBindings()
        {
            var bindings = new List<InventoryLoadoutSlotBindingV1>(
                InventoryLoadoutSlotsV1.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlotsV1.All.Count;
                 index++)
            {
                bindings.Add(new InventoryLoadoutSlotBindingV1(
                    InventoryLoadoutSlotsV1.All[index].SlotStableId,
                    null));
            }
            return bindings;
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
    }
}

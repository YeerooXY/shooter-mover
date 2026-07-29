using System;
using System.Collections.Generic;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Application.Flow.Production
{
    public sealed class WeaponInventory
    {
        public WeaponInventory(
            PlayerRouteProfilePayload routePayload,
            PlayerHoldingsSnapshot genericHoldings,
            WeaponHoldingsSnapshot weaponHoldings,
            WeaponMountLoadoutSnapshot weaponMountLoadout,
            InventoryLoadoutStateSnapshot loadout)
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

        public PlayerRouteProfilePayload RoutePayload { get; }
        public PlayerHoldingsSnapshot GenericHoldings { get; }
        public WeaponHoldingsSnapshot WeaponHoldings { get; }
        public WeaponMountLoadoutSnapshot WeaponMountLoadout { get; }
        public InventoryLoadoutStateSnapshot Loadout { get; }
    }

    /// <summary>
    /// Fresh-character starter onboarding only. Inventory never invokes this service.
    /// </summary>
    public static class WeaponOnboarding
    {
        public const string SweeperWeaponDefinitionId = "sweeper.mk1";

        private static readonly StableId HoldingsAuthorityStableId =
            StableId.Parse("authority.production-player-holdings");

        public static WeaponInventory CreateStarter(
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

            WeaponMark starter;
            if (!WeaponCatalogProvider.Current.TryGetMark(
                    LegacyWeaponSetup.StarterWeaponDefinitionId,
                    out starter)
                || starter == null)
            {
                throw new InvalidOperationException(
                    "The authored starter weapon is missing.");
            }

            WeaponMark sweeper;
            if (!WeaponCatalogProvider.Current.TryGetMark(
                    SweeperWeaponDefinitionId,
                    out sweeper)
                || sweeper == null)
            {
                throw new InvalidOperationException(
                    "The authored Sweeper weapon is missing.");
            }

            WeaponMountLayout layout =
                WeaponMountPolicy.ResolveLayout(
                    classDefinitionStableId);
            var owned = new List<WeaponEquipmentInstance>(
                layout.ConfigurablePositions.Count + 1);
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
                WeaponMountPosition position =
                    layout.ConfigurablePositions[index];
                StableId instanceId = NextOpaqueId(factory, used);
                used.Add(instanceId);
                owned.Add(WeaponEquipmentInstance.CreateUnmodified(
                    instanceId,
                    starter.Blueprint.DefinitionId));
                equippedByMount.Add(position.MountStableId, instanceId);
            }

            StableId sweeperInstanceId = NextOpaqueId(factory, used);
            used.Add(sweeperInstanceId);
            owned.Add(WeaponEquipmentInstance.CreateUnmodified(
                sweeperInstanceId,
                sweeper.Blueprint.DefinitionId));

            var mountBindings = new List<WeaponMountBinding>(
                layout.PhysicalPositions.Count);
            for (int index = 0;
                 index < layout.PhysicalPositions.Count;
                 index++)
            {
                WeaponMountPosition position =
                    layout.PhysicalPositions[index];
                StableId instanceId;
                equippedByMount.TryGetValue(position.MountStableId, out instanceId);
                mountBindings.Add(new WeaponMountBinding(
                    position.MountStableId,
                    position.IsActive ? instanceId : null));
            }

            WeaponHoldingsSnapshot weaponHoldings =
                WeaponHoldingsSnapshot.CreateCanonical(0L, owned);
            WeaponMountLoadoutSnapshot weaponMountLoadout =
                WeaponMountLoadoutSnapshot.CreateCanonical(
                    0L,
                    mountBindings);
            InventoryLoadoutStateSnapshot loadout =
                WeaponMountLoadoutView.ToLegacyProjection(
                    layout,
                    weaponMountLoadout,
                    InventoryLoadoutStateSnapshot.CreateCanonical(
                        0L,
                        EmptyBindings()));
            PlayerRouteProfilePayload route =
                WeaponMountLoadoutView.Route(
                    characterInstanceStableId,
                    classDefinitionStableId,
                    layout,
                    weaponMountLoadout);
            var genericHoldings = new PlayerHoldingsActions(
                HoldingsAuthorityStableId,
                999L,
                new EquipmentCatalogBridge(
                    WeaponCatalogProvider.EquipmentCatalog));

            // The V2 weapon authority owns canonical weapon state, but the generic holdings
            // ledger remains the compatibility/receipt projection consumed by the run-start
            // boundary and older reward paths. Starter creation must publish every exact
            // canonical instance to both projections without making the receipt ledger authoritative.
            for (int index = 0; index < owned.Count; index++)
            {
                WeaponEquipmentInstance weapon = owned[index];
                WeaponMark ownedMark;
                if (!WeaponCatalogProvider.Current.TryGetMark(
                        weapon.WeaponDefinitionId.Value,
                        out ownedMark)
                    || ownedMark == null)
                {
                    throw new InvalidOperationException(
                        "An authored starter weapon projection is missing: "
                        + weapon.WeaponDefinitionId.Value);
                }

                EquipmentDefinition ownedEquipment =
                    WeaponCatalogProvider.EquipmentCatalog
                        .FindEquipmentDefinition(ownedMark.EquipmentDefinitionId);
                if (ownedEquipment == null
                    || ownedEquipment.QualityTiers == null
                    || ownedEquipment.QualityTiers.Count == 0)
                {
                    throw new InvalidOperationException(
                        "An authored starter equipment projection is invalid: "
                        + weapon.WeaponDefinitionId.Value);
                }

                string token = (index + 1).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                EquipmentInstance receipt = EquipmentInstance.Create(
                    weapon.InstanceId,
                    ownedEquipment.DefinitionId,
                    ownedEquipment.ItemLevelRange.Minimum,
                    ownedEquipment.QualityTiers[0].QualityId,
                    Array.Empty<AugmentInstance>());
                PlayerHoldingsMutationResult result = genericHoldings.Apply(
                    PlayerHoldingsCommand.AddEquipment(
                        StableId.Parse("transaction.weapon-onboarding-v2-" + token),
                        StableId.Parse("operation.weapon-onboarding-v2-" + token),
                        HoldingsAuthorityStableId,
                        receipt,
                        HoldingProvenance.Create(
                            StableId.Parse("grant.weapon-onboarding-v2-" + token),
                            StableId.Parse("source.production-weapon-onboarding-v2")),
                        genericHoldings.Sequence));
                if (result == null
                    || (result.Status != PlayerHoldingsMutationStatus.Applied
                        && result.Status
                            != PlayerHoldingsMutationStatus.ExactDuplicateNoChange))
                {
                    throw new InvalidOperationException(
                        "Unable to publish starter equipment receipt: "
                        + (result == null ? "result-null" : result.RejectionCode));
                }
            }

            return new WeaponInventory(
                route,
                genericHoldings.ExportSnapshot(),
                weaponHoldings,
                weaponMountLoadout,
                loadout);
        }

        public static WeaponInventory Restore(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            PlayerHoldingsSnapshot genericHoldings,
            WeaponHoldingsSnapshot canonicalWeaponHoldings,
            InventoryLoadoutStateSnapshot loadout)
        {
            return Restore(
                characterInstanceStableId,
                classDefinitionStableId,
                genericHoldings,
                canonicalWeaponHoldings,
                null,
                loadout);
        }

        public static WeaponInventory Restore(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            PlayerHoldingsSnapshot genericHoldings,
            WeaponHoldingsSnapshot canonicalWeaponHoldings,
            WeaponMountLoadoutSnapshot canonicalWeaponMountLoadout,
            InventoryLoadoutStateSnapshot loadout)
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

            WeaponHoldingsSnapshot weapons = canonicalWeaponHoldings
                ?? WeaponHoldingsMigration.ConvertLegacy(
                    genericHoldings);
            var weaponAuthority = new WeaponHoldingsState(weapons);
            WeaponMountLayout layout =
                WeaponMountPolicy.ResolveLayout(
                    classDefinitionStableId);
            WeaponMountLoadoutSnapshot mounts = canonicalWeaponMountLoadout
                ?? WeaponMountLoadoutView.MigrateLegacy(
                    layout,
                    weaponAuthority,
                    loadout);

            // V2 is strict: unknown, missing, locked, duplicate or unowned mount bindings reject
            // restore instead of being silently repaired. Only the V1 conversion normalizes legacy
            // placeholders as part of deterministic dual-read migration.
            var mountAuthority = new WeaponMountLoadoutState(
                layout,
                weaponAuthority,
                mounts);
            WeaponMountLoadoutSnapshot canonicalMounts =
                mountAuthority.ExportSnapshot();
            InventoryLoadoutStateSnapshot compatibilityLoadout =
                WeaponMountLoadoutView.ToLegacyProjection(
                    layout,
                    canonicalMounts,
                    loadout);
            PlayerRouteProfilePayload route =
                WeaponMountLoadoutView.Route(
                    characterInstanceStableId,
                    classDefinitionStableId,
                    layout,
                    canonicalMounts);

            return new WeaponInventory(
                route,
                genericHoldings,
                weapons,
                canonicalMounts,
                compatibilityLoadout);
        }

        private static IEnumerable<InventoryLoadoutSlotBinding> EmptyBindings()
        {
            var bindings = new List<InventoryLoadoutSlotBinding>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlots.All.Count;
                 index++)
            {
                bindings.Add(new InventoryLoadoutSlotBinding(
                    InventoryLoadoutSlots.All[index].SlotStableId,
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

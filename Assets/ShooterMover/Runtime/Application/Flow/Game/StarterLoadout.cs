using System;
using System.Collections.Generic;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Flow.Game
{
    public sealed class StarterInventory
    {
        public StarterInventory(
            PlayerRouteProfilePayload routePayload,
            PlayerHoldingsSnapshot genericHoldings,
            GunInventorySnapshot gunHoldings,
            LoadoutSnapshot gunMountLoadout,
            InventoryLoadoutStateSnapshot loadout)
        {
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            GenericHoldings = genericHoldings
                ?? throw new ArgumentNullException(nameof(genericHoldings));
            GunInventory = gunHoldings
                ?? throw new ArgumentNullException(nameof(gunHoldings));
            EquippedGuns = gunMountLoadout
                ?? throw new ArgumentNullException(nameof(gunMountLoadout));
            LegacyLoadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        }

        public PlayerRouteProfilePayload RoutePayload { get; }
        public PlayerHoldingsSnapshot GenericHoldings { get; }
        public GunInventorySnapshot GunInventory { get; }
        public LoadoutSnapshot EquippedGuns { get; }
        public InventoryLoadoutStateSnapshot LegacyLoadout { get; }
    }

    /// <summary>
    /// Fresh-character starter onboarding only. Inventory never invokes this service.
    /// </summary>
    public static class StarterLoadout
    {
        public const string SweeperGunDefinitionId = "sweeper.mk1";

        private static readonly StableId HoldingsAuthorityStableId =
            StableId.Parse("authority.production-player-holdings");

        public static StarterInventory CreateStarter(
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

            GunMark starter;
            if (!GunCatalogProvider.Current.TryGetMark(
                    LegacyGunSetup.StarterGunDefinitionId,
                    out starter)
                || starter == null)
            {
                throw new InvalidOperationException(
                    "The authored starter gun is missing.");
            }

            GunMark sweeper;
            if (!GunCatalogProvider.Current.TryGetMark(
                    SweeperGunDefinitionId,
                    out sweeper)
                || sweeper == null)
            {
                throw new InvalidOperationException(
                    "The authored Sweeper gun is missing.");
            }

            GunSlots layout =
                GunMountPolicy.ResolveLayout(
                    classDefinitionStableId);
            var owned = new List<GunItem>(
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
                GunSlot position =
                    layout.ConfigurablePositions[index];
                StableId instanceId = NextOpaqueId(factory, used);
                used.Add(instanceId);
                owned.Add(GunItem.CreateUnmodified(
                    instanceId,
                    starter.Blueprint.DefinitionId));
                equippedByMount.Add(position.MountStableId, instanceId);
            }

            StableId sweeperInstanceId = NextOpaqueId(factory, used);
            used.Add(sweeperInstanceId);
            owned.Add(GunItem.CreateUnmodified(
                sweeperInstanceId,
                sweeper.Blueprint.DefinitionId));

            var mountBindings = new List<EquippedGun>(
                layout.PhysicalPositions.Count);
            for (int index = 0;
                 index < layout.PhysicalPositions.Count;
                 index++)
            {
                GunSlot position =
                    layout.PhysicalPositions[index];
                StableId instanceId;
                equippedByMount.TryGetValue(position.MountStableId, out instanceId);
                mountBindings.Add(new EquippedGun(
                    position.MountStableId,
                    position.IsActive ? instanceId : null));
            }

            GunInventorySnapshot gunHoldings =
                GunInventorySnapshot.CreateCanonical(0L, owned);
            LoadoutSnapshot gunMountLoadout =
                LoadoutSnapshot.CreateCanonical(
                    0L,
                    mountBindings);
            InventoryLoadoutStateSnapshot loadout =
                LoadoutView.ToLegacyProjection(
                    layout,
                    gunMountLoadout,
                    InventoryLoadoutStateSnapshot.CreateCanonical(
                        0L,
                        EmptyBindings()));
            PlayerRouteProfilePayload route =
                LoadoutView.Route(
                    characterInstanceStableId,
                    classDefinitionStableId,
                    layout,
                    gunMountLoadout);
            var genericHoldings = new PlayerHoldingsActions(
                HoldingsAuthorityStableId,
                999L,
                new EquipmentCatalogBridge(
                    GunCatalogProvider.EquipmentCatalog));

            // The V2 gun authority owns canonical gun state, but the generic holdings
            // ledger remains the compatibility/receipt projection consumed by the run-start
            // boundary and older reward paths. Starter creation must publish every exact
            // canonical instance to both projections without making the receipt ledger authoritative.
            for (int index = 0; index < owned.Count; index++)
            {
                GunItem gun = owned[index];
                GunMark ownedMark;
                if (!GunCatalogProvider.Current.TryGetMark(
                        gun.GunDefinitionId.Value,
                        out ownedMark)
                    || ownedMark == null)
                {
                    throw new InvalidOperationException(
                        "An authored starter gun projection is missing: "
                        + gun.GunDefinitionId.Value);
                }

                EquipmentDefinition ownedEquipment =
                    GunCatalogProvider.EquipmentCatalog
                        .FindEquipmentDefinition(ownedMark.EquipmentDefinitionId);
                if (ownedEquipment == null
                    || ownedEquipment.QualityTiers == null
                    || ownedEquipment.QualityTiers.Count == 0)
                {
                    throw new InvalidOperationException(
                        "An authored starter equipment projection is invalid: "
                        + gun.GunDefinitionId.Value);
                }

                string token = (index + 1).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                EquipmentInstance receipt = EquipmentInstance.Create(
                    gun.InstanceId,
                    ownedEquipment.DefinitionId,
                    ownedEquipment.ItemLevelRange.Minimum,
                    ownedEquipment.QualityTiers[0].QualityId,
                    Array.Empty<AugmentInstance>());
                PlayerHoldingsMutationResult result = genericHoldings.Apply(
                    PlayerHoldingsCommand.AddEquipment(
                        StableId.Parse("transaction.gun-onboarding-v2-" + token),
                        StableId.Parse("operation.gun-onboarding-v2-" + token),
                        HoldingsAuthorityStableId,
                        receipt,
                        HoldingProvenance.Create(
                            StableId.Parse("grant.gun-onboarding-v2-" + token),
                            StableId.Parse("source.production-gun-onboarding-v2")),
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

            return new StarterInventory(
                route,
                genericHoldings.ExportSnapshot(),
                gunHoldings,
                gunMountLoadout,
                loadout);
        }

        public static StarterInventory Restore(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            PlayerHoldingsSnapshot genericHoldings,
            GunInventorySnapshot canonicalGunInventory,
            InventoryLoadoutStateSnapshot loadout)
        {
            return Restore(
                characterInstanceStableId,
                classDefinitionStableId,
                genericHoldings,
                canonicalGunInventory,
                null,
                loadout);
        }

        public static StarterInventory Restore(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            PlayerHoldingsSnapshot genericHoldings,
            GunInventorySnapshot canonicalGunInventory,
            LoadoutSnapshot canonicalLoadout,
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

            GunInventorySnapshot guns = canonicalGunInventory
                ?? GunInventoryMigration.ConvertLegacy(
                    genericHoldings);
            var gunAuthority = new GunInventoryState(guns);
            GunSlots layout =
                GunMountPolicy.ResolveLayout(
                    classDefinitionStableId);
            LoadoutSnapshot mounts = canonicalLoadout
                ?? LoadoutView.MigrateLegacy(
                    layout,
                    gunAuthority,
                    loadout);

            // V2 is strict: unknown, missing, locked, duplicate or unowned mount bindings reject
            // restore instead of being silently repaired. Only the V1 conversion normalizes legacy
            // placeholders as part of deterministic dual-read migration.
            var mountAuthority = new LoadoutState(
                layout,
                gunAuthority,
                mounts);
            LoadoutSnapshot canonicalMounts =
                mountAuthority.ExportSnapshot();
            InventoryLoadoutStateSnapshot compatibilityLoadout =
                LoadoutView.ToLegacyProjection(
                    layout,
                    canonicalMounts,
                    loadout);
            PlayerRouteProfilePayload route =
                LoadoutView.Route(
                    characterInstanceStableId,
                    classDefinitionStableId,
                    layout,
                    canonicalMounts);

            return new StarterInventory(
                route,
                genericHoldings,
                guns,
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
                "Unable to allocate a unique opaque gun instance ID.");
        }
    }
}

using System;
using System.Collections.Generic;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Application.Holdings;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Flow.Game
{
    public sealed class StarterInventory
    {
        public StarterInventory(
            PlayerRouteProfilePayload routePayload,
            PlayerHoldingsSnapshot genericHoldings,
            GunInventorySnapshot gunHoldings,
            LoadoutSnapshot gunMountLoadout)
        {
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            GenericHoldings = genericHoldings
                ?? throw new ArgumentNullException(nameof(genericHoldings));
            GunInventory = gunHoldings
                ?? throw new ArgumentNullException(nameof(gunHoldings));
            EquippedGuns = gunMountLoadout
                ?? throw new ArgumentNullException(nameof(gunMountLoadout));
        }

        public PlayerRouteProfilePayload RoutePayload { get; }
        public PlayerHoldingsSnapshot GenericHoldings { get; }
        public GunInventorySnapshot GunInventory { get; }
        public LoadoutSnapshot EquippedGuns { get; }
    }

    /// <summary>
    /// Fresh-character starter onboarding only. Inventory never invokes this service.
    /// Restores require the canonical gun inventory and physical mount components.
    /// </summary>
    public static class StarterLoadout
    {
        public const string StarterGunDefinitionId = "rattler.mk1";
        public const string SweeperGunDefinitionId = "sweeper.mk1";
        public const string VoltspikeGunDefinitionId = "voltspike.mk1";
        public const string PrismataGunDefinitionId = "prismata.mk1";
        public const string CrownfallGunDefinitionId = "crownfall.mk1";
        public const string NullstarGunDefinitionId = "nullstar.mk1";

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
                    StarterGunDefinitionId,
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

            string[] trialGunDefinitionIds =
            {
                VoltspikeGunDefinitionId,
                PrismataGunDefinitionId,
                CrownfallGunDefinitionId,
                NullstarGunDefinitionId,
            };
            var trialGuns = new List<GunMark>(trialGunDefinitionIds.Length);
            for (int index = 0; index < trialGunDefinitionIds.Length; index++)
            {
                GunMark trial;
                if (!GunCatalogProvider.Current.TryGetMark(
                        trialGunDefinitionIds[index],
                        out trial)
                    || trial == null)
                {
                    throw new InvalidOperationException(
                        "The authored trial gun is missing: "
                        + trialGunDefinitionIds[index]);
                }
                trialGuns.Add(trial);
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

            for (int index = 0; index < trialGuns.Count; index++)
            {
                StableId trialInstanceId = NextOpaqueId(factory, used);
                used.Add(trialInstanceId);
                owned.Add(GunItem.CreateUnmodified(
                    trialInstanceId,
                    trialGuns[index].Blueprint.DefinitionId));
            }

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

            // The gun authority owns canonical gun state, while generic holdings retain
            // immutable reward/onboarding receipts and future non-gun inventory.
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
                gunMountLoadout);
        }

        public static StarterInventory Restore(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            PlayerHoldingsSnapshot genericHoldings,
            GunInventorySnapshot canonicalGunInventory,
            LoadoutSnapshot canonicalLoadout)
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
            if (canonicalGunInventory == null)
            {
                throw new ArgumentNullException(nameof(canonicalGunInventory));
            }
            if (canonicalLoadout == null)
            {
                throw new ArgumentNullException(nameof(canonicalLoadout));
            }

            var gunAuthority = new GunInventoryState(canonicalGunInventory);
            GunSlots layout =
                GunMountPolicy.ResolveLayout(
                    classDefinitionStableId);
            var mountAuthority = new LoadoutState(
                layout,
                gunAuthority,
                canonicalLoadout);
            LoadoutSnapshot canonicalMounts =
                mountAuthority.ExportSnapshot();
            PlayerRouteProfilePayload route =
                LoadoutView.Route(
                    characterInstanceStableId,
                    classDefinitionStableId,
                    layout,
                    canonicalMounts);

            return new StarterInventory(
                route,
                genericHoldings,
                canonicalGunInventory,
                canonicalMounts);
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

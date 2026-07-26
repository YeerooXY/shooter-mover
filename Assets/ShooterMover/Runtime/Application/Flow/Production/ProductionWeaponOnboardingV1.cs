using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Flow.Production
{
    public sealed class ProductionWeaponInventoryStateV1
    {
        internal ProductionWeaponInventoryStateV1(
            PlayerRouteProfilePayloadV1 routePayload,
            PlayerHoldingsSnapshotV1 holdings,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            Holdings = holdings ?? throw new ArgumentNullException(nameof(holdings));
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        }

        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public PlayerHoldingsSnapshotV1 Holdings { get; }
        public InventoryLoadoutAuthoritySnapshotV1 Loadout { get; }
    }

    /// <summary>
    /// Creates or repairs the owned exact weapon instances required by one character's
    /// physical mount layout. All work happens on local authorities; callers publish the
    /// returned holdings and loadout only after the complete state validates.
    /// </summary>
    public static class ProductionWeaponOnboardingV1
    {
        public const string StarterWeaponDefinitionId = "rattler.mk1";

        private static readonly StableId HoldingsAuthorityStableId =
            StableId.Parse("authority.production-player-holdings");
        private static readonly StableId OnboardingSourceStableId =
            StableId.Parse("source.production-weapon-onboarding-v1");

        public static ProductionWeaponInventoryStateV1 CreateStarter(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            Func<StableId> instanceIdFactory = null)
        {
            RequireIdentity(characterInstanceStableId, nameof(characterInstanceStableId));
            RequireIdentity(loadoutProfileStableId, nameof(loadoutProfileStableId));

            ProductionEquipmentCatalogAdapterV1 adapter = CreateAdapter();
            var holdings = new PlayerHoldingsService(
                HoldingsAuthorityStableId,
                999L,
                adapter);
            return Complete(
                characterInstanceStableId,
                loadoutProfileStableId,
                holdings,
                null,
                instanceIdFactory);
        }

        public static ProductionWeaponInventoryStateV1 Repair(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshotV1 holdingsSnapshot,
            InventoryLoadoutAuthoritySnapshotV1 loadoutSnapshot,
            Func<StableId> instanceIdFactory = null)
        {
            RequireIdentity(characterInstanceStableId, nameof(characterInstanceStableId));
            RequireIdentity(loadoutProfileStableId, nameof(loadoutProfileStableId));
            if (holdingsSnapshot == null)
            {
                throw new ArgumentNullException(nameof(holdingsSnapshot));
            }
            if (loadoutSnapshot == null)
            {
                throw new ArgumentNullException(nameof(loadoutSnapshot));
            }

            ProductionEquipmentCatalogAdapterV1 adapter = CreateAdapter();
            var holdings = new PlayerHoldingsService(
                holdingsSnapshot.AuthorityStableId,
                holdingsSnapshot.MaximumStackQuantity,
                adapter);
            PlayerHoldingsImportResultV1 imported =
                holdings.ImportSnapshot(holdingsSnapshot);
            if (imported == null || !imported.Succeeded)
            {
                throw new InvalidOperationException(
                    "Unable to import current holdings for weapon onboarding: "
                    + (imported == null
                        ? "result-null"
                        : imported.RejectionCode));
            }

            return Complete(
                characterInstanceStableId,
                loadoutProfileStableId,
                holdings,
                loadoutSnapshot,
                instanceIdFactory);
        }

        public static PlayerRouteProfilePayloadV1 RouteFromLoadout(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            InventoryLoadoutAuthoritySnapshotV1 loadoutSnapshot)
        {
            RequireIdentity(characterInstanceStableId, nameof(characterInstanceStableId));
            RequireIdentity(loadoutProfileStableId, nameof(loadoutProfileStableId));
            if (loadoutSnapshot == null)
            {
                throw new ArgumentNullException(nameof(loadoutSnapshot));
            }

            var instances = new List<StableId>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);
            for (int index = 0;
                 index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                 index++)
            {
                instances.Add(loadoutSnapshot.GetBinding(
                    InventoryLoadoutSlotsV1.All[index].SlotStableId)
                    .EquipmentInstanceStableId);
            }

            return PlayerRouteProfilePayloadV1.Create(
                characterInstanceStableId,
                loadoutProfileStableId,
                instances);
        }

        private static ProductionWeaponInventoryStateV1 Complete(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsService holdings,
            InventoryLoadoutAuthoritySnapshotV1 originalLoadout,
            Func<StableId> instanceIdFactory)
        {
            ProductionWeaponMountLayoutV1 layout =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    loadoutProfileStableId);
            IReadOnlyList<InventoryLoadoutSlotBindingV1> bindings =
                NormalizeBindings(layout, holdings.ExportSnapshot(), originalLoadout);
            var mutable = new List<InventoryLoadoutSlotBindingV1>(bindings);
            var used = new HashSet<StableId>();
            for (int index = 0; index < mutable.Count; index++)
            {
                StableId value = mutable[index].EquipmentInstanceStableId;
                if (value != null)
                {
                    used.Add(value);
                }
            }

            for (int index = 0;
                 index < layout.ConfigurablePositions.Count;
                 index++)
            {
                ProductionWeaponMountPositionV1 position =
                    layout.ConfigurablePositions[index];
                int bindingIndex = FindSlotIndex(position.LoadoutSlotStableId);
                if (mutable[bindingIndex].EquipmentInstanceStableId != null)
                {
                    continue;
                }

                StableId instanceStableId = NextInstanceId(
                    holdings,
                    used,
                    instanceIdFactory);
                EquipmentInstance instance = CreateStarterInstance(
                    instanceStableId);
                Grant(
                    holdings,
                    characterInstanceStableId,
                    position.LoadoutSlotStableId,
                    instance);
                used.Add(instanceStableId);
                mutable[bindingIndex] = new InventoryLoadoutSlotBindingV1(
                    position.LoadoutSlotStableId,
                    instanceStableId);
            }

            long sequence = originalLoadout == null
                ? 0L
                : BindingsEqual(originalLoadout.Bindings, mutable)
                    ? originalLoadout.Sequence
                    : checked(originalLoadout.Sequence + 1L);
            InventoryLoadoutAuthoritySnapshotV1 loadout =
                InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                    sequence,
                    mutable);
            PlayerRouteProfilePayloadV1 route = RouteFromLoadout(
                characterInstanceStableId,
                loadoutProfileStableId,
                loadout);

            ProductionEquipmentCatalogAdapterV1 adapter = CreateAdapter();
            var authority = new ProductionInventoryLoadoutAuthorityV1(
                route,
                holdings,
                adapter);
            ProductionInventoryLoadoutImportResultV1 loadoutImport =
                authority.ImportSnapshot(loadout);
            if (loadoutImport == null || !loadoutImport.Succeeded)
            {
                throw new InvalidOperationException(
                    "Weapon onboarding produced an invalid loadout: "
                    + (loadoutImport == null
                        ? "result-null"
                        : loadoutImport.RejectionCode));
            }

            return new ProductionWeaponInventoryStateV1(
                route,
                holdings.ExportSnapshot(),
                loadoutImport.Snapshot);
        }

        private static IReadOnlyList<InventoryLoadoutSlotBindingV1>
            NormalizeBindings(
                ProductionWeaponMountLayoutV1 layout,
                PlayerHoldingsSnapshotV1 holdings,
                InventoryLoadoutAuthoritySnapshotV1 original)
        {
            var owned = new Dictionary<StableId, EquipmentInstance>();
            for (int index = 0; index < holdings.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshotV1 holding = holdings.UniqueHoldings[index];
                if (holding == null
                    || holding.RewardKind
                        != RewardGrantKindV1.EquipmentReference
                    || holding.EquipmentInstance == null)
                {
                    continue;
                }

                EquipmentInstance instance = holding.EquipmentInstance;
                EquipmentDefinition definition =
                    ProductionWeaponCatalogProvider.EquipmentCatalog
                        .FindEquipmentDefinition(instance.DefinitionId);
                EquipmentValidationResult validation =
                    ProductionWeaponCatalogProvider.EquipmentCatalog
                        .ValidateInstance(instance);
                if (definition != null
                    && validation != null
                    && validation.IsValid)
                {
                    owned[instance.InstanceId] = instance;
                }
            }

            var selected = new HashSet<StableId>();
            var output = new List<InventoryLoadoutSlotBindingV1>(
                InventoryLoadoutSlotsV1.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlotsV1.All.Count;
                 index++)
            {
                InventoryLoadoutSlotDescriptorV1 slot =
                    InventoryLoadoutSlotsV1.All[index];
                StableId instanceStableId = original == null
                    ? null
                    : original.GetBinding(slot.SlotStableId)
                        .EquipmentInstanceStableId;
                bool configurableWeapon = slot.Kind
                        == InventoryLoadoutSlotKindV1.Weapon
                    && layout.ContainsLoadoutSlot(slot.SlotStableId);
                if (slot.Kind == InventoryLoadoutSlotKindV1.Weapon
                    && !configurableWeapon)
                {
                    instanceStableId = null;
                }

                EquipmentInstance instance;
                EquipmentDefinition definition;
                bool valid = instanceStableId != null
                    && owned.TryGetValue(instanceStableId, out instance)
                    && selected.Add(instanceStableId)
                    && (definition = ProductionWeaponCatalogProvider
                        .EquipmentCatalog.FindEquipmentDefinition(
                            instance.DefinitionId)) != null
                    && (slot.Kind == InventoryLoadoutSlotKindV1.Weapon
                        ? definition.CategoryId == EquipmentCategoryIds.Weapon
                        : definition.CategoryId == EquipmentCategoryIds.Armor);
                if (!valid)
                {
                    if (instanceStableId != null)
                    {
                        selected.Remove(instanceStableId);
                    }
                    instanceStableId = null;
                }

                output.Add(new InventoryLoadoutSlotBindingV1(
                    slot.SlotStableId,
                    instanceStableId));
            }
            return output;
        }

        private static EquipmentInstance CreateStarterInstance(
            StableId instanceStableId)
        {
            ProductionWeaponMarkV1 starter;
            if (!ProductionWeaponCatalogProvider.Current.TryGetMark(
                    StarterWeaponDefinitionId,
                    out starter)
                || starter == null)
            {
                throw new InvalidOperationException(
                    "The authored starter weapon is missing.");
            }

            EquipmentDefinition definition =
                ProductionWeaponCatalogProvider.EquipmentCatalog
                    .FindEquipmentDefinition(
                        starter.EquipmentDefinitionId);
            if (definition == null
                || definition.QualityTiers == null
                || definition.QualityTiers.Count == 0)
            {
                throw new InvalidOperationException(
                    "The authored starter equipment projection is invalid.");
            }

            return EquipmentInstance.Create(
                instanceStableId,
                definition.DefinitionId,
                definition.ItemLevelRange.Minimum,
                definition.QualityTiers[0].QualityId,
                Array.Empty<AugmentInstance>());
        }

        private static void Grant(
            PlayerHoldingsService holdings,
            StableId characterInstanceStableId,
            StableId slotStableId,
            EquipmentInstance instance)
        {
            string token = Hash(
                characterInstanceStableId
                    + "|"
                    + slotStableId
                    + "|"
                    + instance.InstanceId);
            PlayerHoldingsMutationResultV1 result = holdings.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    StableId.Parse("transaction.weapon-onboarding-" + token),
                    StableId.Parse("operation.weapon-onboarding-" + token),
                    holdings.AuthorityStableId,
                    instance,
                    HoldingProvenanceV1.Create(
                        StableId.Parse("grant.weapon-onboarding-" + token),
                        OnboardingSourceStableId),
                    holdings.Sequence));
            if (result == null
                || (result.Status != PlayerHoldingsMutationStatusV1.Applied
                    && result.Status
                        != PlayerHoldingsMutationStatusV1
                            .ExactDuplicateNoChange))
            {
                throw new InvalidOperationException(
                    "Unable to grant starter weapon: "
                    + (result == null
                        ? "result-null"
                        : result.RejectionCode));
            }
        }

        private static StableId NextInstanceId(
            PlayerHoldingsService holdings,
            HashSet<StableId> used,
            Func<StableId> instanceIdFactory)
        {
            Func<StableId> factory = instanceIdFactory
                ?? delegate
                {
                    return StableId.Parse(
                        "equipment-instance.onboarding-"
                        + Guid.NewGuid().ToString("N"));
                };
            for (int attempt = 0; attempt < 64; attempt++)
            {
                StableId candidate = factory();
                UniqueHoldingSnapshotV1 ignored;
                if (candidate != null
                    && !used.Contains(candidate)
                    && !holdings.TryGetUnique(candidate, out ignored))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException(
                "Unable to create a distinct starter equipment identity.");
        }

        private static ProductionEquipmentCatalogAdapterV1 CreateAdapter()
        {
            return new ProductionEquipmentCatalogAdapterV1(
                ProductionWeaponCatalogProvider.EquipmentCatalog);
        }

        private static int FindSlotIndex(StableId slotStableId)
        {
            for (int index = 0;
                 index < InventoryLoadoutSlotsV1.All.Count;
                 index++)
            {
                if (InventoryLoadoutSlotsV1.All[index].SlotStableId
                    == slotStableId)
                {
                    return index;
                }
            }
            throw new InvalidOperationException(
                "Weapon mount references an unknown loadout slot: "
                + slotStableId);
        }

        private static bool BindingsEqual(
            IReadOnlyList<InventoryLoadoutSlotBindingV1> left,
            IReadOnlyList<InventoryLoadoutSlotBindingV1> right)
        {
            if (left == null || right == null || left.Count != right.Count)
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

        private static void RequireIdentity(StableId value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(32);
                for (int index = 0; index < 16; index++)
                {
                    builder.Append(digest[index].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}

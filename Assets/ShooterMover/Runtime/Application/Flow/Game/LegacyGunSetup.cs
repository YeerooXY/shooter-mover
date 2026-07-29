using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Flow.Game
{
    public sealed class LegacyGunInventory
    {
        internal LegacyGunInventory(
            PlayerRouteProfilePayload routePayload,
            PlayerHoldingsSnapshot holdings,
            InventoryLoadoutStateSnapshot loadout)
        {
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            Holdings = holdings ?? throw new ArgumentNullException(nameof(holdings));
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        }

        public PlayerRouteProfilePayload RoutePayload { get; }
        public PlayerHoldingsSnapshot Holdings { get; }
        public InventoryLoadoutStateSnapshot Loadout { get; }
    }

    /// <summary>
    /// Creates or repairs the owned exact gun instances required by one character's
    /// physical mount layout. All work happens on local authorities; callers publish the
    /// returned holdings and loadout only after the complete state validates.
    /// </summary>
    public static class LegacyGunSetup
    {
        public const string StarterGunDefinitionId = "rattler.mk1";

        private static readonly StableId HoldingsAuthorityStableId =
            StableId.Parse("authority.production-player-holdings");
        private static readonly StableId OnboardingSourceStableId =
            StableId.Parse("source.production-gun-onboarding-v1");

        public static LegacyGunInventory CreateStarter(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            Func<StableId> instanceIdFactory = null)
        {
            RequireIdentity(characterInstanceStableId, nameof(characterInstanceStableId));
            RequireIdentity(loadoutProfileStableId, nameof(loadoutProfileStableId));

            EquipmentCatalogBridge adapter = CreateAdapter();
            var holdings = new PlayerHoldingsActions(
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

        public static LegacyGunInventory Repair(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshot holdingsSnapshot,
            InventoryLoadoutStateSnapshot loadoutSnapshot,
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

            EquipmentCatalogBridge adapter = CreateAdapter();
            var holdings = new PlayerHoldingsActions(
                holdingsSnapshot.AuthorityStableId,
                holdingsSnapshot.MaximumStackQuantity,
                adapter);
            PlayerHoldingsImportResult imported =
                holdings.ImportSnapshot(holdingsSnapshot);
            if (imported == null || !imported.Succeeded)
            {
                throw new InvalidOperationException(
                    "Unable to import current holdings for gun onboarding: "
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

        public static PlayerRouteProfilePayload RouteFromLoadout(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            InventoryLoadoutStateSnapshot loadoutSnapshot)
        {
            RequireIdentity(characterInstanceStableId, nameof(characterInstanceStableId));
            RequireIdentity(loadoutProfileStableId, nameof(loadoutProfileStableId));
            if (loadoutSnapshot == null)
            {
                throw new ArgumentNullException(nameof(loadoutSnapshot));
            }

            var instances = new List<StableId>(
                PlayerRouteProfilePayload.GunSlotCount);
            for (int index = 0;
                 index < PlayerRouteProfilePayload.GunSlotCount;
                 index++)
            {
                instances.Add(loadoutSnapshot.GetBinding(
                    InventoryLoadoutSlots.All[index].SlotStableId)
                    .EquipmentInstanceStableId);
            }

            return PlayerRouteProfilePayload.Create(
                characterInstanceStableId,
                loadoutProfileStableId,
                instances);
        }

        private static LegacyGunInventory Complete(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsActions holdings,
            InventoryLoadoutStateSnapshot originalLoadout,
            Func<StableId> instanceIdFactory)
        {
            GunSlots layout =
                GunMountPolicy.ResolveLayout(
                    loadoutProfileStableId);
            IReadOnlyList<InventoryLoadoutSlotBinding> bindings =
                NormalizeBindings(layout, holdings.ExportSnapshot(), originalLoadout);
            var mutable = new List<InventoryLoadoutSlotBinding>(bindings);
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
                GunSlot position =
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
                mutable[bindingIndex] = new InventoryLoadoutSlotBinding(
                    position.LoadoutSlotStableId,
                    instanceStableId);
            }

            long sequence = originalLoadout == null
                ? 0L
                : BindingsEqual(originalLoadout.Bindings, mutable)
                    ? originalLoadout.Sequence
                    : checked(originalLoadout.Sequence + 1L);
            InventoryLoadoutStateSnapshot loadout =
                InventoryLoadoutStateSnapshot.CreateCanonical(
                    sequence,
                    mutable);
            PlayerRouteProfilePayload route = RouteFromLoadout(
                characterInstanceStableId,
                loadoutProfileStableId,
                loadout);

            EquipmentCatalogBridge adapter = CreateAdapter();
            var authority = new InventoryLoadoutState(
                route,
                holdings,
                adapter);
            InventoryLoadoutImportResult loadoutImport =
                authority.ImportSnapshot(loadout);
            if (loadoutImport == null || !loadoutImport.Succeeded)
            {
                throw new InvalidOperationException(
                    "Gun onboarding produced an invalid loadout: "
                    + (loadoutImport == null
                        ? "result-null"
                        : loadoutImport.RejectionCode));
            }

            return new LegacyGunInventory(
                route,
                holdings.ExportSnapshot(),
                loadoutImport.Snapshot);
        }

        private static IReadOnlyList<InventoryLoadoutSlotBinding>
            NormalizeBindings(
                GunSlots layout,
                PlayerHoldingsSnapshot holdings,
                InventoryLoadoutStateSnapshot original)
        {
            var owned = new Dictionary<StableId, EquipmentInstance>();
            for (int index = 0; index < holdings.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshot holding = holdings.UniqueHoldings[index];
                if (holding == null
                    || holding.RewardKind
                        != RewardGrantKind.EquipmentReference
                    || holding.EquipmentInstance == null)
                {
                    continue;
                }

                EquipmentInstance instance = holding.EquipmentInstance;
                EquipmentDefinition definition =
                    GunCatalogProvider.EquipmentCatalog
                        .FindEquipmentDefinition(instance.DefinitionId);
                EquipmentValidationResult validation =
                    GunCatalogProvider.EquipmentCatalog
                        .ValidateInstance(instance);
                if (definition != null
                    && validation != null
                    && validation.IsValid)
                {
                    owned[instance.InstanceId] = instance;
                }
            }

            var selected = new HashSet<StableId>();
            var output = new List<InventoryLoadoutSlotBinding>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlots.All.Count;
                 index++)
            {
                InventoryLoadoutSlotDescriptor slot =
                    InventoryLoadoutSlots.All[index];
                StableId instanceStableId = original == null
                    ? null
                    : original.GetBinding(slot.SlotStableId)
                        .EquipmentInstanceStableId;
                bool configurableGun = slot.Kind
                        == InventoryLoadoutSlotKind.Gun
                    && layout.ContainsLoadoutSlot(slot.SlotStableId);
                if (slot.Kind == InventoryLoadoutSlotKind.Gun
                    && !configurableGun)
                {
                    instanceStableId = null;
                }

                EquipmentInstance instance;
                EquipmentDefinition definition;
                bool valid = instanceStableId != null
                    && owned.TryGetValue(instanceStableId, out instance)
                    && selected.Add(instanceStableId)
                    && (definition = GunCatalogProvider
                        .EquipmentCatalog.FindEquipmentDefinition(
                            instance.DefinitionId)) != null
                    && (slot.Kind == InventoryLoadoutSlotKind.Gun
                        ? definition.CategoryId == EquipmentCategoryIds.Gun
                        : definition.CategoryId == EquipmentCategoryIds.Armor);
                if (!valid)
                {
                    if (instanceStableId != null)
                    {
                        selected.Remove(instanceStableId);
                    }
                    instanceStableId = null;
                }

                output.Add(new InventoryLoadoutSlotBinding(
                    slot.SlotStableId,
                    instanceStableId));
            }
            return output;
        }

        private static EquipmentInstance CreateStarterInstance(
            StableId instanceStableId)
        {
            GunMark starter;
            if (!GunCatalogProvider.Current.TryGetMark(
                    StarterGunDefinitionId,
                    out starter)
                || starter == null)
            {
                throw new InvalidOperationException(
                    "The authored starter gun is missing.");
            }

            EquipmentDefinition definition =
                GunCatalogProvider.EquipmentCatalog
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
            PlayerHoldingsActions holdings,
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
            PlayerHoldingsMutationResult result = holdings.Apply(
                PlayerHoldingsCommand.AddEquipment(
                    StableId.Parse("transaction.gun-onboarding-" + token),
                    StableId.Parse("operation.gun-onboarding-" + token),
                    holdings.AuthorityStableId,
                    instance,
                    HoldingProvenance.Create(
                        StableId.Parse("grant.gun-onboarding-" + token),
                        OnboardingSourceStableId),
                    holdings.Sequence));
            if (result == null
                || (result.Status != PlayerHoldingsMutationStatus.Applied
                    && result.Status
                        != PlayerHoldingsMutationStatus
                            .ExactDuplicateNoChange))
            {
                throw new InvalidOperationException(
                    "Unable to grant starter gun: "
                    + (result == null
                        ? "result-null"
                        : result.RejectionCode));
            }
        }

        private static StableId NextInstanceId(
            PlayerHoldingsActions holdings,
            HashSet<StableId> used,
            Func<StableId> instanceIdFactory)
        {
            Func<StableId> factory = instanceIdFactory
                ?? delegate
                {
                    return ShooterMover.Domain.Guns
                        .OwnedEquipmentInstanceIdFactory.Create();
                };
            for (int attempt = 0; attempt < 64; attempt++)
            {
                StableId candidate = factory();
                UniqueHoldingSnapshot ignored;
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

        private static EquipmentCatalogBridge CreateAdapter()
        {
            return new EquipmentCatalogBridge(
                GunCatalogProvider.EquipmentCatalog);
        }

        private static int FindSlotIndex(StableId slotStableId)
        {
            for (int index = 0;
                 index < InventoryLoadoutSlots.All.Count;
                 index++)
            {
                if (InventoryLoadoutSlots.All[index].SlotStableId
                    == slotStableId)
                {
                    return index;
                }
            }
            throw new InvalidOperationException(
                "Gun mount references an unknown loadout slot: "
                + slotStableId);
        }

        private static bool BindingsEqual(
            IReadOnlyList<InventoryLoadoutSlotBinding> left,
            IReadOnlyList<InventoryLoadoutSlotBinding> right)
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

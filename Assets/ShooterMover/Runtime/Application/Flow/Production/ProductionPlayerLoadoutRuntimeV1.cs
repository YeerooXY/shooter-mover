using System;
using System.Collections.Generic;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Production composition for the current starter inventory. Later save/import,
    /// shop, crafting and strongbox systems can populate the same holdings/loadout
    /// boundary without changing the Hub screen or Level 1 weapon execution.
    /// </summary>
    public sealed class ProductionPlayerLoadoutRuntimeV1
    {
        public ProductionPlayerLoadoutRuntimeV1(
            PlayerRouteProfilePayloadV1 routePayload)
        {
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The production loadout route payload is invalid.",
                    nameof(routePayload));
            }

            EquipmentCatalog = ProductionStarterWeaponCatalogV1
                .BuildEquipmentCatalog();
            CatalogAdapter = new ProductionEquipmentCatalogAdapterV1(
                EquipmentCatalog);
            WeaponCatalog = ProductionStarterWeaponCatalogV1
                .BuildWeaponCatalog();
            Holdings = new PlayerHoldingsService(
                StableId.Parse("authority.production-player-holdings"),
                999L,
                CatalogAdapter);

            SeedStarterInventory(routePayload);
            LoadoutAuthority = new ProductionInventoryLoadoutAuthorityV1(
                routePayload,
                Holdings,
                CatalogAdapter);
        }

        public PlayerRouteProfilePayloadV1 RoutePayload { get; }

        public PlayerHoldingsService Holdings { get; }

        public EquipmentCatalog EquipmentCatalog { get; }

        public ProductionEquipmentCatalogAdapterV1 CatalogAdapter { get; }

        public WeaponCatalog WeaponCatalog { get; }

        public ProductionInventoryLoadoutAuthorityV1 LoadoutAuthority { get; }

        public StableId RicochetEquipmentInstanceStableId
        {
            get
            {
                return ProductionStarterWeaponCatalogV1
                    .RicochetEquipmentInstanceStableId;
            }
        }

        private void SeedStarterInventory(
            PlayerRouteProfilePayloadV1 routePayload)
        {
            StableId common = StableId.Parse("equipment-quality.common");
            var presentDefinitions = new HashSet<StableId>();
            var presentInstances = new HashSet<StableId>();

            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                StableId instanceStableId = routePayload
                    .WeaponSlots[index]
                    .EquipmentInstanceStableId;
                StableId definitionStableId;
                if (!ProductionStarterWeaponCatalogV1
                    .TryResolveDefinitionForInstance(
                        instanceStableId,
                        out definitionStableId))
                {
                    definitionStableId =
                        ProductionStarterWeaponCatalogV1
                            .InitialEquipmentDefinitionStableIds[index];
                }

                AddEquipment(
                    EquipmentInstance.Create(
                        instanceStableId,
                        definitionStableId,
                        1,
                        common,
                        Array.Empty<AugmentInstance>()),
                    "route-slot-" + (index + 1));
                presentInstances.Add(instanceStableId);
                presentDefinitions.Add(definitionStableId);
            }

            for (int index = 0;
                index < ProductionStarterWeaponCatalogV1
                    .AllEquipmentDefinitionStableIds.Count;
                index++)
            {
                StableId definitionStableId =
                    ProductionStarterWeaponCatalogV1
                        .AllEquipmentDefinitionStableIds[index];
                if (presentDefinitions.Contains(definitionStableId))
                {
                    continue;
                }

                StableId reserveInstanceStableId =
                    ProductionStarterWeaponCatalogV1
                        .ReserveInstanceForDefinition(
                            definitionStableId);
                if (!presentInstances.Add(reserveInstanceStableId))
                {
                    throw new InvalidOperationException(
                        "A starter reserve equipment identity collided.");
                }

                AddEquipment(
                    EquipmentInstance.Create(
                        reserveInstanceStableId,
                        definitionStableId,
                        1,
                        common,
                        Array.Empty<AugmentInstance>()),
                    "reserve-" + (index + 1));
                presentDefinitions.Add(definitionStableId);
            }
        }

        private void AddEquipment(
            EquipmentInstance instance,
            string token)
        {
            PlayerHoldingsMutationResultV1 result = Holdings.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    StableId.Parse(
                        "transaction.production-starter-" + token),
                    StableId.Parse(
                        "operation.production-starter-" + token),
                    Holdings.AuthorityStableId,
                    instance,
                    HoldingProvenanceV1.Create(
                        StableId.Parse(
                            "grant.production-starter-" + token),
                        StableId.Parse(
                            "source.production-starter-inventory")),
                    Holdings.Sequence));
            if (result.Status != PlayerHoldingsMutationStatusV1.Applied
                && result.Status
                    != PlayerHoldingsMutationStatusV1
                        .ExactDuplicateNoChange)
            {
                throw new InvalidOperationException(
                    "Unable to seed production starter equipment: "
                    + result.RejectionCode);
            }
        }
    }

    public sealed class ProductionEquipmentCatalogAdapterV1 :
        IEquipmentCatalogProvider,
        IEquipmentInstanceValidator
    {
        public ProductionEquipmentCatalogAdapterV1(
            EquipmentCatalog catalog)
        {
            Catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
        }

        public EquipmentCatalog Catalog { get; }

        public EquipmentInstanceValidationResponse Validate(
            EquipmentInstanceValidationRequest request)
        {
            EquipmentInstance instance = request == null
                ? null
                : request.Instance;
            return EquipmentInstanceValidationResponse.From(
                Catalog,
                instance,
                Catalog.ValidateInstance(instance));
        }
    }

    /// <summary>
    /// Sole equipped-slot truth for the current production profile. It accepts only
    /// exact owned instances and rejects stale holdings/loadout snapshots.
    /// </summary>
    public sealed class ProductionInventoryLoadoutAuthorityV1 :
        IInventoryLoadoutAuthorityPortV1
    {
        private readonly IPlayerHoldingsAuthorityV1 holdings;
        private readonly IEquipmentCatalogProvider catalogProvider;
        private InventoryLoadoutAuthoritySnapshotV1 snapshot;
        private string lastAcceptedCommandFingerprint = string.Empty;

        public ProductionInventoryLoadoutAuthorityV1(
            PlayerRouteProfilePayloadV1 routePayload,
            IPlayerHoldingsAuthorityV1 holdings,
            IEquipmentCatalogProvider catalogProvider)
        {
            if (routePayload == null)
            {
                throw new ArgumentNullException(nameof(routePayload));
            }
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The initial route payload is invalid.",
                    nameof(routePayload));
            }

            this.holdings = holdings
                ?? throw new ArgumentNullException(nameof(holdings));
            this.catalogProvider = catalogProvider
                ?? throw new ArgumentNullException(nameof(catalogProvider));

            var bindings = new List<InventoryLoadoutSlotBindingV1>(
                InventoryLoadoutSlotsV1.All.Count);
            for (int index = 0;
                index < InventoryLoadoutSlotsV1.All.Count;
                index++)
            {
                StableId instanceStableId = index
                    < PlayerRouteProfilePayloadV1.WeaponSlotCount
                    ? routePayload.WeaponSlots[index]
                        .EquipmentInstanceStableId
                    : null;
                bindings.Add(
                    new InventoryLoadoutSlotBindingV1(
                        InventoryLoadoutSlotsV1.All[index]
                            .SlotStableId,
                        instanceStableId));
            }

            snapshot = InventoryLoadoutAuthoritySnapshotV1
                .CreateCanonical(0L, bindings);
            string rejectionCode;
            if (!ValidateBindings(
                    snapshot.Bindings,
                    holdings.ExportSnapshot(),
                    out rejectionCode))
            {
                throw new ArgumentException(
                    "The initial route payload cannot seed the loadout: "
                    + rejectionCode,
                    nameof(routePayload));
            }
        }

        public InventoryLoadoutAuthoritySnapshotV1 ExportSnapshot()
        {
            return snapshot;
        }

        public InventoryLoadoutAuthorityResultV1 Apply(
            InventoryLoadoutAuthorityCommandV1 command)
        {
            if (command == null)
            {
                return Reject("production-loadout-command-null");
            }

            if (string.Equals(
                command.Fingerprint,
                lastAcceptedCommandFingerprint,
                StringComparison.Ordinal))
            {
                return new InventoryLoadoutAuthorityResultV1(
                    InventoryLoadoutAuthorityMutationStatusV1
                        .ExactRepeatNoChange,
                    string.Empty,
                    snapshot);
            }

            if (command.ExpectedSequence != snapshot.Sequence)
            {
                return new InventoryLoadoutAuthorityResultV1(
                    InventoryLoadoutAuthorityMutationStatusV1
                        .StaleSnapshot,
                    "production-loadout-sequence-stale",
                    snapshot);
            }

            PlayerHoldingsSnapshotV1 holdingsSnapshot =
                holdings.ExportSnapshot();
            if (holdingsSnapshot == null
                || command.ExpectedHoldingsSequence
                    != holdings.Sequence)
            {
                return new InventoryLoadoutAuthorityResultV1(
                    InventoryLoadoutAuthorityMutationStatusV1
                        .StaleSnapshot,
                    "production-loadout-holdings-stale",
                    snapshot);
            }

            string rejectionCode;
            if (!ValidateBindings(
                    command.Bindings,
                    holdingsSnapshot,
                    out rejectionCode))
            {
                return Reject(rejectionCode);
            }

            if (BindingsEqual(snapshot.Bindings, command.Bindings))
            {
                return new InventoryLoadoutAuthorityResultV1(
                    InventoryLoadoutAuthorityMutationStatusV1
                        .ExactRepeatNoChange,
                    string.Empty,
                    snapshot);
            }

            snapshot = InventoryLoadoutAuthoritySnapshotV1
                .CreateCanonical(
                    snapshot.Sequence + 1L,
                    command.Bindings);
            lastAcceptedCommandFingerprint = command.Fingerprint;
            return new InventoryLoadoutAuthorityResultV1(
                InventoryLoadoutAuthorityMutationStatusV1.Applied,
                string.Empty,
                snapshot);
        }

        private InventoryLoadoutAuthorityResultV1 Reject(
            string rejectionCode)
        {
            return new InventoryLoadoutAuthorityResultV1(
                InventoryLoadoutAuthorityMutationStatusV1.Rejected,
                rejectionCode,
                snapshot);
        }

        private bool ValidateBindings(
            IReadOnlyList<InventoryLoadoutSlotBindingV1> bindings,
            PlayerHoldingsSnapshotV1 holdingsSnapshot,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (bindings == null
                || bindings.Count != InventoryLoadoutSlotsV1.All.Count)
            {
                rejectionCode = "production-loadout-binding-count-invalid";
                return false;
            }
            if (holdingsSnapshot == null)
            {
                rejectionCode = "production-loadout-holdings-missing";
                return false;
            }

            EquipmentCatalog catalog = catalogProvider.Catalog;
            if (catalog == null)
            {
                rejectionCode = "production-loadout-catalog-missing";
                return false;
            }

            var equipmentByInstance =
                new Dictionary<StableId, EquipmentInstance>();
            for (int index = 0;
                index < holdingsSnapshot.UniqueHoldings.Count;
                index++)
            {
                UniqueHoldingSnapshotV1 holding =
                    holdingsSnapshot.UniqueHoldings[index];
                if (holding == null
                    || holding.RewardKind
                        != RewardGrantKindV1.EquipmentReference
                    || holding.InstanceStableId == null
                    || holding.EquipmentInstance == null)
                {
                    continue;
                }

                equipmentByInstance[holding.InstanceStableId] =
                    holding.EquipmentInstance;
            }

            var selectedInstances = new HashSet<StableId>();
            for (int index = 0; index < bindings.Count; index++)
            {
                InventoryLoadoutSlotDescriptorV1 expectedSlot =
                    InventoryLoadoutSlotsV1.All[index];
                InventoryLoadoutSlotBindingV1 binding = bindings[index];
                if (binding == null
                    || binding.SlotStableId
                        != expectedSlot.SlotStableId)
                {
                    rejectionCode =
                        "production-loadout-slot-order-invalid";
                    return false;
                }

                StableId instanceStableId =
                    binding.EquipmentInstanceStableId;
                if (instanceStableId == null)
                {
                    if (expectedSlot.Kind
                        == InventoryLoadoutSlotKindV1.Weapon)
                    {
                        rejectionCode =
                            "production-loadout-weapon-slot-empty";
                        return false;
                    }

                    continue;
                }

                if (!selectedInstances.Add(instanceStableId))
                {
                    rejectionCode =
                        "production-loadout-instance-duplicate";
                    return false;
                }

                EquipmentInstance instance;
                if (!equipmentByInstance.TryGetValue(
                    instanceStableId,
                    out instance))
                {
                    rejectionCode =
                        "production-loadout-instance-not-owned";
                    return false;
                }

                EquipmentDefinition definition =
                    catalog.FindEquipmentDefinition(
                        instance.DefinitionId);
                EquipmentValidationResult validation =
                    catalog.ValidateInstance(instance);
                if (definition == null
                    || validation == null
                    || !validation.IsValid)
                {
                    rejectionCode =
                        "production-loadout-instance-invalid";
                    return false;
                }

                bool correctKind = expectedSlot.Kind
                    == InventoryLoadoutSlotKindV1.Weapon
                    ? definition.CategoryId
                        == EquipmentCategoryIds.Weapon
                    : definition.CategoryId
                        == EquipmentCategoryIds.Armor;
                if (!correctKind)
                {
                    rejectionCode =
                        "production-loadout-instance-wrong-slot-kind";
                    return false;
                }
            }

            return true;
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

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
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Character-local production inventory composition. Generic holdings retain reward receipts
    /// and non-weapon inventory; WeaponHoldings and MountLoadoutAuthority are the sole canonical
    /// weapon ownership and equipped-state authorities.
    /// </summary>
    public sealed class ProductionPlayerLoadoutRuntimeV1
    {
        public ProductionPlayerLoadoutRuntimeV1(
            PlayerRouteProfilePayloadV1 routePayload)
            : this(CreateStarterState(routePayload))
        {
        }

        private ProductionPlayerLoadoutRuntimeV1(
            ProductionWeaponInventoryStateV2 state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            RoutePayload = state.RoutePayload;
            MountLayout = ProductionWeaponMountPolicyV1.ResolveLayout(
                RoutePayload.LoadoutProfileStableId);
            EquipmentCatalog = ProductionWeaponCatalogProvider.EquipmentCatalog;
            CatalogAdapter = new ProductionEquipmentCatalogAdapterV1(
                EquipmentCatalog);
            WeaponCatalog = ProductionWeaponCatalogProvider.WeaponCatalog;

            LegacyHoldings = new PlayerHoldingsService(
                state.GenericHoldings.AuthorityStableId,
                state.GenericHoldings.MaximumStackQuantity,
                CatalogAdapter);
            PlayerHoldingsImportResultV1 holdingsImport =
                LegacyHoldings.ImportSnapshot(state.GenericHoldings);
            if (holdingsImport == null || !holdingsImport.Succeeded)
            {
                throw new InvalidOperationException(
                    "Unable to restore production generic holdings: "
                    + (holdingsImport == null
                        ? "result-null"
                        : holdingsImport.RejectionCode));
            }

            WeaponHoldings = new ProductionWeaponHoldingsAuthorityV2(
                state.WeaponHoldings);
            Holdings = new CanonicalFirstPlayerHoldingsAuthorityV2(
                LegacyHoldings,
                WeaponHoldings);
            MountLoadoutAuthority = new ProductionWeaponMountLoadoutAuthorityV2(
                MountLayout,
                WeaponHoldings,
                state.WeaponMountLoadout);

            InventoryLoadoutAuthoritySnapshotV1 compatibilityLoadout =
                ProductionWeaponMountLoadoutProjectionV2.ToLegacyProjection(
                    MountLayout,
                    MountLoadoutAuthority.ExportSnapshot(),
                    state.Loadout);
            LoadoutAuthority = new ProductionInventoryLoadoutAuthorityV1(
                ProductionWeaponMountLoadoutProjectionV2.Route(
                    RoutePayload.SelectedCharacterStableId,
                    RoutePayload.LoadoutProfileStableId,
                    MountLayout,
                    MountLoadoutAuthority.ExportSnapshot()),
                Holdings,
                CatalogAdapter,
                WeaponHoldings,
                WeaponCatalog);

            ProductionInventoryLoadoutImportResultV1 loadoutImport =
                LoadoutAuthority.ImportSnapshot(compatibilityLoadout);
            if (loadoutImport == null || !loadoutImport.Succeeded)
            {
                throw new InvalidOperationException(
                    "Unable to restore production loadout projection: "
                    + (loadoutImport == null
                        ? "result-null"
                        : loadoutImport.RejectionCode));
            }
        }

        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public PlayerRouteProfilePayloadV1 CurrentRoutePayload
        {
            get
            {
                return ProductionWeaponMountLoadoutProjectionV2.Route(
                    RoutePayload.SelectedCharacterStableId,
                    RoutePayload.LoadoutProfileStableId,
                    MountLayout,
                    MountLoadoutAuthority.ExportSnapshot());
            }
        }
        public ProductionWeaponMountLayoutV1 MountLayout { get; }
        public PlayerHoldingsService LegacyHoldings { get; }
        public IPlayerHoldingsAuthorityV1 Holdings { get; }
        public ProductionWeaponHoldingsAuthorityV2 WeaponHoldings { get; }
        public ProductionWeaponMountLoadoutAuthorityV2 MountLoadoutAuthority
        {
            get;
        }
        public EquipmentCatalog EquipmentCatalog { get; }
        public ProductionEquipmentCatalogAdapterV1 CatalogAdapter { get; }
        public WeaponCatalog WeaponCatalog { get; }
        public ProductionInventoryLoadoutAuthorityV1 LoadoutAuthority { get; }

        public static ProductionPlayerLoadoutRuntimeV1 Restore(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshotV1 holdings,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            return Restore(
                characterInstanceStableId,
                loadoutProfileStableId,
                holdings,
                null,
                null,
                loadout);
        }

        public static ProductionPlayerLoadoutRuntimeV1 Restore(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshotV1 genericHoldings,
            WeaponHoldingsSnapshotV2 weaponHoldings,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            return Restore(
                characterInstanceStableId,
                loadoutProfileStableId,
                genericHoldings,
                weaponHoldings,
                null,
                loadout);
        }

        public static ProductionPlayerLoadoutRuntimeV1 Restore(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshotV1 genericHoldings,
            WeaponHoldingsSnapshotV2 weaponHoldings,
            WeaponMountLoadoutSnapshotV2 weaponMountLoadout,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            return new ProductionPlayerLoadoutRuntimeV1(
                ProductionWeaponOnboardingV2.Restore(
                    characterInstanceStableId,
                    loadoutProfileStableId,
                    genericHoldings,
                    weaponHoldings,
                    weaponMountLoadout,
                    loadout));
        }

        public bool TryResolveFirstActiveEquippedWeapon(
            out WeaponEquipmentInstance instance,
            out string rejectionCode)
        {
            instance = null;
            rejectionCode = string.Empty;
            WeaponMountLoadoutSnapshotV2 mounts =
                MountLoadoutAuthority.ExportSnapshot();
            for (int index = 0; index < MountLayout.Positions.Count; index++)
            {
                ProductionWeaponMountPositionV1 position =
                    MountLayout.Positions[index];
                if (!position.IsActive)
                {
                    continue;
                }

                WeaponMountBindingV2 binding = mounts.Find(
                    position.MountStableId);
                StableId instanceId = binding == null
                    ? null
                    : binding.InstanceId;
                if (instanceId == null)
                {
                    continue;
                }

                instance = WeaponHoldings.Find(instanceId);
                if (instance == null)
                {
                    rejectionCode =
                        "production-first-active-weapon-not-owned:"
                        + instanceId;
                    return false;
                }
                return true;
            }

            rejectionCode = "production-first-active-weapon-empty";
            return false;
        }

        private static ProductionWeaponInventoryStateV2 CreateStarterState(
            PlayerRouteProfilePayloadV1 routePayload)
        {
            if (routePayload == null)
            {
                throw new ArgumentNullException(nameof(routePayload));
            }
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The production loadout route payload is invalid.",
                    nameof(routePayload));
            }

            return ProductionWeaponOnboardingV2.CreateStarter(
                routePayload.SelectedCharacterStableId,
                routePayload.LoadoutProfileStableId);
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

    public sealed class ProductionInventoryLoadoutImportResultV1
    {
        public ProductionInventoryLoadoutImportResultV1(
            bool succeeded,
            string rejectionCode,
            InventoryLoadoutAuthoritySnapshotV1 snapshot)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public InventoryLoadoutAuthoritySnapshotV1 Snapshot { get; }
    }

    /// <summary>
    /// Compatibility projection for generic armor and fixed route slots. Canonical weapon equipped
    /// truth is owned by ProductionWeaponMountLoadoutAuthorityV2.
    /// </summary>
    public sealed class ProductionInventoryLoadoutAuthorityV1 :
        IInventoryLoadoutAuthorityPortV1
    {
        private readonly IPlayerHoldingsAuthorityV1 genericHoldings;
        private readonly IEquipmentCatalogProvider catalogProvider;
        private readonly ProductionWeaponHoldingsAuthorityV2 weaponHoldings;
        private readonly WeaponCatalog weaponCatalog;
        private readonly ProductionWeaponMountLayoutV1 mountLayout;
        private InventoryLoadoutAuthoritySnapshotV1 snapshot;
        private string lastAcceptedCommandFingerprint = string.Empty;

        public ProductionInventoryLoadoutAuthorityV1(
            PlayerRouteProfilePayloadV1 routePayload,
            IPlayerHoldingsAuthorityV1 holdings,
            IEquipmentCatalogProvider equipmentCatalogProvider)
            : this(
                routePayload,
                holdings,
                equipmentCatalogProvider,
                new ProductionWeaponHoldingsAuthorityV2(
                    ProductionWeaponHoldingsMigrationV2.ConvertLegacy(
                        holdings == null
                            ? throw new ArgumentNullException(nameof(holdings))
                            : holdings.ExportSnapshot())),
                ProductionWeaponCatalogProvider.WeaponCatalog)
        {
        }

        public ProductionInventoryLoadoutAuthorityV1(
            PlayerRouteProfilePayloadV1 routePayload,
            IPlayerHoldingsAuthorityV1 holdings,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            ProductionWeaponHoldingsAuthorityV2 canonicalWeaponHoldings,
            WeaponCatalog canonicalWeaponCatalog)
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

            genericHoldings = holdings
                ?? throw new ArgumentNullException(nameof(holdings));
            catalogProvider = equipmentCatalogProvider
                ?? throw new ArgumentNullException(
                    nameof(equipmentCatalogProvider));
            weaponHoldings = canonicalWeaponHoldings
                ?? throw new ArgumentNullException(
                    nameof(canonicalWeaponHoldings));
            weaponCatalog = canonicalWeaponCatalog
                ?? throw new ArgumentNullException(
                    nameof(canonicalWeaponCatalog));
            mountLayout = ProductionWeaponMountPolicyV1.ResolveLayout(
                routePayload.LoadoutProfileStableId);

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
                bindings.Add(new InventoryLoadoutSlotBindingV1(
                    InventoryLoadoutSlotsV1.All[index].SlotStableId,
                    instanceStableId));
            }

            snapshot = InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                0L,
                bindings);
            string rejectionCode;
            if (!ValidateBindings(snapshot.Bindings, out rejectionCode))
            {
                throw new ArgumentException(
                    "The initial route payload cannot seed the loadout: "
                    + rejectionCode,
                    nameof(routePayload));
            }
        }

        public ProductionWeaponMountLayoutV1 MountLayout
        {
            get { return mountLayout; }
        }

        public InventoryLoadoutAuthoritySnapshotV1 ExportSnapshot()
        {
            return snapshot;
        }

        public ProductionInventoryLoadoutImportResultV1 ImportSnapshot(
            InventoryLoadoutAuthoritySnapshotV1 imported)
        {
            if (imported == null)
            {
                return ImportRejected("production-loadout-import-null");
            }
            if (!imported.HasValidFingerprint())
            {
                return ImportRejected(
                    "production-loadout-import-fingerprint-invalid");
            }

            string rejectionCode;
            if (!ValidateBindings(imported.Bindings, out rejectionCode))
            {
                return ImportRejected(rejectionCode);
            }

            snapshot = imported;
            lastAcceptedCommandFingerprint = string.Empty;
            return new ProductionInventoryLoadoutImportResultV1(
                true,
                string.Empty,
                snapshot);
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
                    InventoryLoadoutAuthorityMutationStatusV1.StaleSnapshot,
                    "production-loadout-sequence-stale",
                    snapshot);
            }

            PlayerHoldingsSnapshotV1 genericSnapshot =
                genericHoldings.ExportSnapshot();
            if (genericSnapshot == null
                || command.ExpectedHoldingsSequence
                    != genericHoldings.Sequence)
            {
                return new InventoryLoadoutAuthorityResultV1(
                    InventoryLoadoutAuthorityMutationStatusV1.StaleSnapshot,
                    "production-loadout-holdings-stale",
                    snapshot);
            }

            string rejectionCode;
            if (!ValidateBindings(command.Bindings, out rejectionCode))
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

            snapshot = InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                checked(snapshot.Sequence + 1L),
                command.Bindings);
            lastAcceptedCommandFingerprint = command.Fingerprint;
            return new InventoryLoadoutAuthorityResultV1(
                InventoryLoadoutAuthorityMutationStatusV1.Applied,
                string.Empty,
                snapshot);
        }

        private ProductionInventoryLoadoutImportResultV1 ImportRejected(
            string rejectionCode)
        {
            return new ProductionInventoryLoadoutImportResultV1(
                false,
                rejectionCode,
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
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (bindings == null
                || bindings.Count != InventoryLoadoutSlotsV1.All.Count)
            {
                rejectionCode = "production-loadout-binding-count-invalid";
                return false;
            }

            EquipmentCatalog equipmentCatalog = catalogProvider.Catalog;
            if (equipmentCatalog == null)
            {
                rejectionCode = "production-loadout-catalog-missing";
                return false;
            }

            PlayerHoldingsSnapshotV1 genericSnapshot =
                genericHoldings.ExportSnapshot();
            if (genericSnapshot == null)
            {
                rejectionCode = "production-loadout-holdings-missing";
                return false;
            }

            var genericEquipment =
                new Dictionary<StableId, EquipmentInstance>();
            for (int index = 0;
                 index < genericSnapshot.UniqueHoldings.Count;
                 index++)
            {
                UniqueHoldingSnapshotV1 holding =
                    genericSnapshot.UniqueHoldings[index];
                if (holding != null
                    && holding.RewardKind
                        == RewardGrantKindV1.EquipmentReference
                    && holding.InstanceStableId != null
                    && holding.EquipmentInstance != null)
                {
                    genericEquipment[holding.InstanceStableId] =
                        holding.EquipmentInstance;
                }
            }

            var selectedInstances = new HashSet<StableId>();
            for (int index = 0; index < bindings.Count; index++)
            {
                InventoryLoadoutSlotDescriptorV1 expectedSlot =
                    InventoryLoadoutSlotsV1.All[index];
                InventoryLoadoutSlotBindingV1 binding = bindings[index];
                if (binding == null
                    || binding.SlotStableId != expectedSlot.SlotStableId)
                {
                    rejectionCode =
                        "production-loadout-slot-order-invalid";
                    return false;
                }

                StableId instanceId = binding.EquipmentInstanceStableId;
                if (expectedSlot.Kind == InventoryLoadoutSlotKindV1.Weapon)
                {
                    bool activePhysicalMount = mountLayout.ContainsLoadoutSlot(
                        expectedSlot.SlotStableId);
                    if (!activePhysicalMount)
                    {
                        if (instanceId != null)
                        {
                            rejectionCode =
                                "production-loadout-slot-unavailable-for-profile";
                            return false;
                        }
                        continue;
                    }

                    if (instanceId == null)
                    {
                        continue;
                    }
                    if (!selectedInstances.Add(instanceId))
                    {
                        rejectionCode =
                            "production-loadout-instance-duplicate";
                        return false;
                    }

                    WeaponEquipmentInstance weapon =
                        weaponHoldings.Find(instanceId);
                    if (weapon == null)
                    {
                        rejectionCode =
                            "production-loadout-instance-not-owned";
                        return false;
                    }

                    WeaponDefinitionData definition;
                    if (!weaponCatalog.TryGetDefinition(
                            weapon.WeaponDefinitionId.Value,
                            out definition)
                        || definition == null)
                    {
                        rejectionCode =
                            "production-loadout-instance-invalid";
                        return false;
                    }
                    continue;
                }

                if (instanceId == null)
                {
                    continue;
                }
                if (!selectedInstances.Add(instanceId))
                {
                    rejectionCode =
                        "production-loadout-instance-duplicate";
                    return false;
                }

                EquipmentInstance armor;
                if (!genericEquipment.TryGetValue(instanceId, out armor))
                {
                    rejectionCode =
                        "production-loadout-instance-not-owned";
                    return false;
                }
                EquipmentDefinition armorDefinition =
                    equipmentCatalog.FindEquipmentDefinition(
                        armor.DefinitionId);
                EquipmentValidationResult armorValidation =
                    equipmentCatalog.ValidateInstance(armor);
                if (armorDefinition == null
                    || armorValidation == null
                    || !armorValidation.IsValid
                    || armorDefinition.CategoryId
                        != EquipmentCategoryIds.Armor)
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

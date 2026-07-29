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
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Flow.Game
{
    /// <summary>
    /// Character-local production inventory composition. Generic holdings retain reward receipts
    /// and non-gun inventory; GunInventory and MountLoadoutAuthority are the sole canonical
    /// gun ownership and equipped-state authorities.
    /// </summary>
    public sealed class PlayerLoadoutLive
    {
        public PlayerLoadoutLive(
            PlayerRouteProfilePayload routePayload)
            : this(CreateStarterState(routePayload))
        {
        }

        private PlayerLoadoutLive(
            StarterInventory state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            RoutePayload = state.RoutePayload;
            MountLayout = GunMountPolicy.ResolveLayout(
                RoutePayload.LoadoutProfileStableId);
            EquipmentCatalog = GunCatalogProvider.EquipmentCatalog;
            CatalogBridge = new EquipmentCatalogBridge(
                EquipmentCatalog);
            GunCatalog = GunCatalogProvider.GunCatalog;

            LegacyHoldings = new PlayerHoldingsActions(
                state.GenericHoldings.AuthorityStableId,
                state.GenericHoldings.MaximumStackQuantity,
                CatalogBridge);
            PlayerHoldingsImportResult holdingsImport =
                LegacyHoldings.ImportSnapshot(state.GenericHoldings);
            if (holdingsImport == null || !holdingsImport.Succeeded)
            {
                throw new InvalidOperationException(
                    "Unable to restore production generic holdings: "
                    + (holdingsImport == null
                        ? "result-null"
                        : holdingsImport.RejectionCode));
            }

            GunInventory = new GunInventoryState(
                state.GunInventory);
            Holdings = new FirstPlayerHoldingsState(
                LegacyHoldings,
                GunInventory);
            MountLoadoutAuthority = new LoadoutState(
                MountLayout,
                GunInventory,
                state.EquippedGuns);

            InventoryLoadoutStateSnapshot compatibilityLoadout =
                LoadoutView.ToLegacyProjection(
                    MountLayout,
                    MountLoadoutAuthority.ExportSnapshot(),
                    state.LegacyLoadout);
            LoadoutAuthority = new InventoryLoadoutState(
                LoadoutView.Route(
                    RoutePayload.SelectedCharacterStableId,
                    RoutePayload.LoadoutProfileStableId,
                    MountLayout,
                    MountLoadoutAuthority.ExportSnapshot()),
                Holdings,
                CatalogBridge,
                GunInventory,
                GunCatalog);

            InventoryLoadoutImportResult loadoutImport =
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

        public PlayerRouteProfilePayload RoutePayload { get; }
        public PlayerRouteProfilePayload CurrentRoutePayload
        {
            get
            {
                return LoadoutView.Route(
                    RoutePayload.SelectedCharacterStableId,
                    RoutePayload.LoadoutProfileStableId,
                    MountLayout,
                    MountLoadoutAuthority.ExportSnapshot());
            }
        }
        public GunSlots MountLayout { get; }
        public PlayerHoldingsActions LegacyHoldings { get; }
        public IPlayerHoldingsState Holdings { get; }
        public GunInventoryState GunInventory { get; }
        public LoadoutState MountLoadoutAuthority
        {
            get;
        }
        public EquipmentCatalog EquipmentCatalog { get; }
        public EquipmentCatalogBridge CatalogBridge { get; }
        public GunCatalog GunCatalog { get; }
        public InventoryLoadoutState LoadoutAuthority { get; }

        public static PlayerLoadoutLive Restore(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshot holdings,
            InventoryLoadoutStateSnapshot loadout)
        {
            return Restore(
                characterInstanceStableId,
                loadoutProfileStableId,
                holdings,
                null,
                null,
                loadout);
        }

        public static PlayerLoadoutLive Restore(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshot genericHoldings,
            GunInventorySnapshot gunHoldings,
            InventoryLoadoutStateSnapshot loadout)
        {
            return Restore(
                characterInstanceStableId,
                loadoutProfileStableId,
                genericHoldings,
                gunHoldings,
                null,
                loadout);
        }

        public static PlayerLoadoutLive Restore(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshot genericHoldings,
            GunInventorySnapshot gunHoldings,
            LoadoutSnapshot gunMountLoadout,
            InventoryLoadoutStateSnapshot loadout)
        {
            return new PlayerLoadoutLive(
                StarterLoadout.Restore(
                    characterInstanceStableId,
                    loadoutProfileStableId,
                    genericHoldings,
                    gunHoldings,
                    gunMountLoadout,
                    loadout));
        }

        public bool TryResolveFirstActiveEquippedGun(
            out GunItem instance,
            out string rejectionCode)
        {
            instance = null;
            rejectionCode = string.Empty;
            LoadoutSnapshot mounts =
                MountLoadoutAuthority.ExportSnapshot();
            for (int index = 0; index < MountLayout.Positions.Count; index++)
            {
                GunSlot position =
                    MountLayout.Positions[index];
                if (!position.IsActive)
                {
                    continue;
                }

                EquippedGun binding = mounts.Find(
                    position.MountStableId);
                StableId instanceId = binding == null
                    ? null
                    : binding.InstanceId;
                if (instanceId == null)
                {
                    continue;
                }

                instance = GunInventory.Find(instanceId);
                if (instance == null)
                {
                    rejectionCode =
                        "production-first-active-gun-not-owned:"
                        + instanceId;
                    return false;
                }
                return true;
            }

            rejectionCode = "production-first-active-gun-empty";
            return false;
        }

        private static StarterInventory CreateStarterState(
            PlayerRouteProfilePayload routePayload)
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

            return StarterLoadout.CreateStarter(
                routePayload.SelectedCharacterStableId,
                routePayload.LoadoutProfileStableId);
        }
    }

    public sealed class EquipmentCatalogBridge :
        IEquipmentCatalogProvider,
        IEquipmentInstanceValidator
    {
        public EquipmentCatalogBridge(
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

    public sealed class InventoryLoadoutImportResult
    {
        public InventoryLoadoutImportResult(
            bool succeeded,
            string rejectionCode,
            InventoryLoadoutStateSnapshot snapshot)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public InventoryLoadoutStateSnapshot Snapshot { get; }
    }

    /// <summary>
    /// Compatibility projection for generic armor and fixed route slots. Canonical gun equipped
    /// truth is owned by LoadoutState.
    /// </summary>
    public sealed class InventoryLoadoutState :
        IInventoryLoadoutStatePort
    {
        private readonly IPlayerHoldingsState genericHoldings;
        private readonly IEquipmentCatalogProvider catalogProvider;
        private readonly GunInventoryState gunHoldings;
        private readonly GunCatalog gunCatalog;
        private readonly GunSlots mountLayout;
        private InventoryLoadoutStateSnapshot snapshot;
        private string lastAcceptedCommandFingerprint = string.Empty;

        public InventoryLoadoutState(
            PlayerRouteProfilePayload routePayload,
            IPlayerHoldingsState holdings,
            IEquipmentCatalogProvider equipmentCatalogProvider)
            : this(
                routePayload,
                holdings,
                equipmentCatalogProvider,
                new GunInventoryState(
                    GunInventoryMigration.ConvertLegacy(
                        holdings == null
                            ? throw new ArgumentNullException(nameof(holdings))
                            : holdings.ExportSnapshot())),
                GunCatalogProvider.GunCatalog)
        {
        }

        public InventoryLoadoutState(
            PlayerRouteProfilePayload routePayload,
            IPlayerHoldingsState holdings,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            GunInventoryState canonicalGunInventory,
            GunCatalog canonicalGunCatalog)
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
            gunHoldings = canonicalGunInventory
                ?? throw new ArgumentNullException(
                    nameof(canonicalGunInventory));
            gunCatalog = canonicalGunCatalog
                ?? throw new ArgumentNullException(
                    nameof(canonicalGunCatalog));
            mountLayout = GunMountPolicy.ResolveLayout(
                routePayload.LoadoutProfileStableId);

            var bindings = new List<InventoryLoadoutSlotBinding>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlots.All.Count;
                 index++)
            {
                StableId instanceStableId = index
                    < PlayerRouteProfilePayload.GunSlotCount
                        ? routePayload.GunSlots[index]
                            .EquipmentInstanceStableId
                        : null;
                bindings.Add(new InventoryLoadoutSlotBinding(
                    InventoryLoadoutSlots.All[index].SlotStableId,
                    instanceStableId));
            }

            snapshot = InventoryLoadoutStateSnapshot.CreateCanonical(
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

        public GunSlots MountLayout
        {
            get { return mountLayout; }
        }

        public InventoryLoadoutStateSnapshot ExportSnapshot()
        {
            return snapshot;
        }

        public InventoryLoadoutImportResult ImportSnapshot(
            InventoryLoadoutStateSnapshot imported)
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
            return new InventoryLoadoutImportResult(
                true,
                string.Empty,
                snapshot);
        }

        public InventoryLoadoutStateResult Apply(
            InventoryLoadoutStateCommand command)
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
                return new InventoryLoadoutStateResult(
                    InventoryLoadoutStateMutationStatus
                        .ExactRepeatNoChange,
                    string.Empty,
                    snapshot);
            }
            if (command.ExpectedSequence != snapshot.Sequence)
            {
                return new InventoryLoadoutStateResult(
                    InventoryLoadoutStateMutationStatus.StaleSnapshot,
                    "production-loadout-sequence-stale",
                    snapshot);
            }

            PlayerHoldingsSnapshot genericSnapshot =
                genericHoldings.ExportSnapshot();
            if (genericSnapshot == null
                || command.ExpectedHoldingsSequence
                    != genericHoldings.Sequence)
            {
                return new InventoryLoadoutStateResult(
                    InventoryLoadoutStateMutationStatus.StaleSnapshot,
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
                return new InventoryLoadoutStateResult(
                    InventoryLoadoutStateMutationStatus
                        .ExactRepeatNoChange,
                    string.Empty,
                    snapshot);
            }

            snapshot = InventoryLoadoutStateSnapshot.CreateCanonical(
                checked(snapshot.Sequence + 1L),
                command.Bindings);
            lastAcceptedCommandFingerprint = command.Fingerprint;
            return new InventoryLoadoutStateResult(
                InventoryLoadoutStateMutationStatus.Applied,
                string.Empty,
                snapshot);
        }

        private InventoryLoadoutImportResult ImportRejected(
            string rejectionCode)
        {
            return new InventoryLoadoutImportResult(
                false,
                rejectionCode,
                snapshot);
        }

        private InventoryLoadoutStateResult Reject(
            string rejectionCode)
        {
            return new InventoryLoadoutStateResult(
                InventoryLoadoutStateMutationStatus.Rejected,
                rejectionCode,
                snapshot);
        }

        private bool ValidateBindings(
            IReadOnlyList<InventoryLoadoutSlotBinding> bindings,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (bindings == null
                || bindings.Count != InventoryLoadoutSlots.All.Count)
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

            PlayerHoldingsSnapshot genericSnapshot =
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
                UniqueHoldingSnapshot holding =
                    genericSnapshot.UniqueHoldings[index];
                if (holding != null
                    && holding.RewardKind
                        == RewardGrantKind.EquipmentReference
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
                InventoryLoadoutSlotDescriptor expectedSlot =
                    InventoryLoadoutSlots.All[index];
                InventoryLoadoutSlotBinding binding = bindings[index];
                if (binding == null
                    || binding.SlotStableId != expectedSlot.SlotStableId)
                {
                    rejectionCode =
                        "production-loadout-slot-order-invalid";
                    return false;
                }

                StableId instanceId = binding.EquipmentInstanceStableId;
                if (expectedSlot.Kind == InventoryLoadoutSlotKind.Gun)
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

                    GunItem gun =
                        gunHoldings.Find(instanceId);
                    if (gun == null)
                    {
                        rejectionCode =
                            "production-loadout-instance-not-owned";
                        return false;
                    }

                    GunDefinitionData definition;
                    if (!gunCatalog.TryGetDefinition(
                            gun.GunDefinitionId.Value,
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
            IReadOnlyList<InventoryLoadoutSlotBinding> left,
            IReadOnlyList<InventoryLoadoutSlotBinding> right)
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

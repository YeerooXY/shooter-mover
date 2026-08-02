using System;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Flow.Game
{
    /// <summary>
    /// Character-local production inventory composition. Generic holdings retain reward receipts
    /// and future non-gun inventory; GunInventory and MountLoadoutAuthority are the sole canonical
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

            // This retained fixed-slot projection exists only for current gun Inventory callers.
            // It accepts gun slots and rejects every former armor slot.
            LoadoutAuthority = new InventoryLoadoutState(
                CurrentRoutePayload,
                Holdings,
                CatalogBridge,
                GunInventory,
                GunCatalog);
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

        /// <summary>
        /// Gun-slot-only compatibility projection. It is not an armor authority.
        /// </summary>
        public InventoryLoadoutState LoadoutAuthority { get; }

        public static PlayerLoadoutLive Restore(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshot holdings,
            InventoryLoadoutStateSnapshot retiredLoadout)
        {
            return Restore(
                characterInstanceStableId,
                loadoutProfileStableId,
                holdings,
                null,
                null,
                retiredLoadout);
        }

        public static PlayerLoadoutLive Restore(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshot genericHoldings,
            GunInventorySnapshot gunHoldings,
            InventoryLoadoutStateSnapshot retiredLoadout)
        {
            return Restore(
                characterInstanceStableId,
                loadoutProfileStableId,
                genericHoldings,
                gunHoldings,
                null,
                retiredLoadout);
        }

        public static PlayerLoadoutLive Restore(
            StableId characterInstanceStableId,
            StableId loadoutProfileStableId,
            PlayerHoldingsSnapshot genericHoldings,
            GunInventorySnapshot gunHoldings,
            LoadoutSnapshot gunMountLoadout,
            InventoryLoadoutStateSnapshot retiredLoadout)
        {
            return new PlayerLoadoutLive(
                StarterLoadout.Restore(
                    characterInstanceStableId,
                    loadoutProfileStableId,
                    genericHoldings,
                    gunHoldings,
                    gunMountLoadout,
                    retiredLoadout));
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
}

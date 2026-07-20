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
    /// Temporary production composition for the current starter inventory. The ownership
    /// boundary is intentionally the same one that later save/import, shop, crafting and
    /// strongbox systems will populate: one holdings authority, one loadout authority and
    /// catalogs consumed read-only by Hub and gameplay.
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
            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                StableId instanceStableId = routePayload
                    .WeaponSlots[index]
                    .EquipmentInstanceStableId;
                StableId definitionStableId =
                    ProductionStarterWeaponCatalogV1
                        .StarterEquipmentDefinitionStableIds[index];
                AddEquipment(
                    EquipmentInstance.Create(
                        instanceStableId,
                        definitionStableId,
                        1,
                        common,
                        Array.Empty<AugmentInstance>()),
                    "route-slot-" + (index + 1));
            }

            AddEquipment(
                EquipmentInstance.Create(
                    ProductionStarterWeaponCatalogV1
                        .RicochetEquipmentInstanceStableId,
                    ProductionStarterWeaponCatalogV1
                        .RicochetEquipmentDefinitionStableId,
                    1,
                    common,
                    Array.Empty<AugmentInstance>()),
                "ricochet-reserve");
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
    /// Sole equipped-slot truth for the current production profile. The authority accepts
    /// only exact owned equipment instances, preserves duplicate definitions as distinct
    /// instances, rejects stale holdings/loadout snapshots and records exact replay.
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
                    != holdingsSnapshot.Sequence)
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

    public static class ProductionStarterWeaponCatalogV1
    {
        public const string ArcWeaponDefinitionId = "weapon.arc-gun";
        public const string RicochetWeaponDefinitionId =
            "weapon.ricochet-gun";

        public static readonly StableId BlasterEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-blaster");
        public static readonly StableId ShotgunEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-shotgun");
        public static readonly StableId RocketEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-rocket-launcher");
        public static readonly StableId ArcEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-arc-gun");
        public static readonly StableId RicochetEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-ricochet-gun");
        public static readonly StableId RicochetEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.production-starter-ricochet");

        private static readonly StableId[] starterEquipmentDefinitionStableIds =
        {
            BlasterEquipmentDefinitionStableId,
            ShotgunEquipmentDefinitionStableId,
            RocketEquipmentDefinitionStableId,
            ArcEquipmentDefinitionStableId,
        };

        public static IReadOnlyList<StableId>
            StarterEquipmentDefinitionStableIds
        {
            get { return starterEquipmentDefinitionStableIds; }
        }

        public static EquipmentCatalog BuildEquipmentCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(
                StableId.Parse("equipment-quality.common"),
                "Common",
                1);
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[]
                {
                    WeaponEquipment(
                        BlasterEquipmentDefinitionStableId,
                        "family.blaster",
                        "Blaster",
                        "weapon.blaster-machine-gun",
                        common),
                    WeaponEquipment(
                        ShotgunEquipmentDefinitionStableId,
                        "family.shotgun",
                        "Shotgun",
                        "weapon.shotgun",
                        common),
                    WeaponEquipment(
                        RocketEquipmentDefinitionStableId,
                        "family.rocket-launcher",
                        "Rocket Launcher",
                        "weapon.rocket-launcher",
                        common),
                    WeaponEquipment(
                        ArcEquipmentDefinitionStableId,
                        "family.arc-gun",
                        "Arc Gun",
                        ArcWeaponDefinitionId,
                        common),
                    WeaponEquipment(
                        RicochetEquipmentDefinitionStableId,
                        "family.ricochet-gun",
                        "Ricochet Gun",
                        RicochetWeaponDefinitionId,
                        common),
                },
                Array.Empty<AugmentDefinition>());
            if (!result.IsValid || result.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The production starter equipment catalog is invalid.");
            }

            return result.Catalog;
        }

        public static WeaponCatalog BuildWeaponCatalog()
        {
            var rules = new WeaponCatalogRules(
                true,
                false,
                "20-25",
                new[] { 75, 105, 135 },
                new[] { "Kinetic", "Energized" },
                10,
                true,
                true,
                true);
            var inputs = new WeaponCatalogInputs(
                12d,
                0.05d,
                0.055d,
                0.06d,
                new Dictionary<string, WeaponRarityInput>(
                    StringComparer.Ordinal)
                {
                    {
                        "Common",
                        new WeaponRarityInput(
                            "Common",
                            1000d,
                            0,
                            4d,
                            13d)
                    },
                });
            var archetype = new WeaponArchetypeDefinition(
                "DemoCutover",
                "Demo Cutover",
                1d,
                1d,
                1,
                1,
                0d,
                10d,
                10d,
                1d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0,
                0,
                0d,
                0d,
                1d);

            WeaponFamilyDefinition[] families =
            {
                Family("production-starter-blaster", "Blaster", "Kinetic"),
                Family("production-starter-shotgun", "Shotgun", "Kinetic"),
                Family(
                    "production-starter-rocket",
                    "Rocket Launcher",
                    "Kinetic"),
                Family("production-starter-arc", "Arc Gun", "Energized"),
                Family(
                    "production-starter-ricochet",
                    "Ricochet Gun",
                    "Kinetic"),
            };
            return new WeaponCatalog(
                "1.0",
                "production-hub-loadout",
                rules,
                inputs,
                new Dictionary<string, WeaponArchetypeDefinition>(
                    StringComparer.Ordinal)
                {
                    { "DemoCutover", archetype },
                },
                families,
                new[]
                {
                    WeaponDefinition(
                        "weapon.blaster-machine-gun",
                        "Blaster",
                        "production-starter-blaster",
                        "Kinetic",
                        10d,
                        1,
                        0d,
                        40d,
                        30d,
                        5d,
                        1),
                    WeaponDefinition(
                        "weapon.shotgun",
                        "Shotgun",
                        "production-starter-shotgun",
                        "Kinetic",
                        2d,
                        7,
                        24d,
                        30d,
                        15d,
                        3d,
                        0),
                    WeaponDefinition(
                        "weapon.rocket-launcher",
                        "Rocket Launcher",
                        "production-starter-rocket",
                        "Kinetic",
                        1d,
                        1,
                        0d,
                        12d,
                        35d,
                        4d,
                        0,
                        20d,
                        3d),
                    WeaponDefinition(
                        ArcWeaponDefinitionId,
                        "Arc Gun",
                        "production-starter-arc",
                        "Energized",
                        1.5d,
                        1,
                        0d,
                        12d,
                        12d,
                        12d,
                        0,
                        0d,
                        0d,
                        3,
                        6d),
                    WeaponDefinition(
                        RicochetWeaponDefinitionId,
                        "Ricochet Gun",
                        "production-starter-ricochet",
                        "Kinetic",
                        2.5d,
                        1,
                        0d,
                        24d,
                        30d,
                        8d,
                        0),
                });
        }

        private static EquipmentDefinition WeaponEquipment(
            StableId definitionStableId,
            string family,
            string displayName,
            string runtime,
            EquipmentQualityTier quality)
        {
            return EquipmentDefinition.Create(
                definitionStableId,
                EquipmentCategoryIds.Weapon,
                StableId.Parse(family),
                displayName,
                StableId.Parse(runtime),
                InclusiveIntRange.Create(1, 100),
                0,
                new[] { quality },
                Array.Empty<StableId>());
        }

        private static WeaponFamilyDefinition Family(
            string id,
            string displayName,
            string damageType)
        {
            return new WeaponFamilyDefinition(
                id,
                displayName,
                "DemoCutover",
                damageType,
                "Universal",
                1,
                20,
                20,
                3,
                "Common",
                "Common",
                "Common",
                1d,
                "Standard",
                "Production vertical slice",
                "Production vertical slice",
                WeaponCatalogAvailability.Live,
                Array.Empty<string>());
        }

        private static WeaponDefinitionData WeaponDefinition(
            string id,
            string displayName,
            string family,
            string damageType,
            double fireRate,
            int projectiles,
            double spread,
            double speed,
            double range,
            double damage,
            int pierce,
            double areaDamage = 0d,
            double explosionRadius = 0d,
            int chainTargets = 0,
            double chainRange = 0d)
        {
            bool explosive = areaDamage > 0d;
            return new WeaponDefinitionData(
                id,
                displayName,
                family,
                1,
                damageType,
                "DemoCutover",
                "Universal",
                1,
                1,
                1,
                "Common",
                1000d,
                1d,
                1000d,
                4d,
                13d,
                "Standard",
                false,
                "Standard",
                1d,
                100d,
                10d,
                explosive ? 0.2d : 1d,
                explosive ? 0.8d : 0d,
                0d,
                fireRate,
                projectiles,
                1,
                damage,
                spread,
                speed,
                range,
                pierce,
                explosionRadius,
                areaDamage,
                0d,
                0d,
                0d,
                0d,
                chainTargets,
                chainRange,
                0.5d,
                1d,
                0d,
                "Production vertical slice",
                "Production vertical slice",
                WeaponCatalogAvailability.Live,
                Array.Empty<string>());
        }
    }
}

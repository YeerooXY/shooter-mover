using System;
using System.Collections.Generic;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Compatibility entry point for production flows that have not yet been renamed. It owns no
    /// weapon content and delegates to ProductionWeaponCatalogueV1, the single provisional
    /// catalogue authority shared by gameplay, equipment, strongboxes, shops, and simulation.
    /// </summary>
    public static class ProductionStarterWeaponCatalogV1
    {
        // Retained only as source-compatibility symbols for unrelated presentation consumers.
        // No retired definition, equipment instance, grant, or runtime package is registered.
        [Obsolete("Use ProductionWeaponCatalogueV1 canonical presentation metadata.")]
        public const string ArcWeaponDefinitionId = "weapon.arc-gun";
        [Obsolete("Use ProductionWeaponCatalogueV1 canonical presentation metadata.")]
        public const string RicochetWeaponDefinitionId = "weapon.ricochet-gun";
        [Obsolete("Use ProductionWeaponCatalogueV1 canonical presentation metadata.")]
        public const string BlasterSideProfileArtId =
            "weapon-art.blaster.side-v1";
        [Obsolete("Use ProductionWeaponCatalogueV1 canonical presentation metadata.")]
        public const string ShotgunSideProfileArtId =
            "weapon-art.shotgun-basic.side-v1";
        [Obsolete("Use ProductionWeaponCatalogueV1 canonical presentation metadata.")]
        public const string RocketSideProfileArtId =
            "weapon-art.rocket-launcher.side-v1";
        [Obsolete("Use ProductionWeaponCatalogueV1 canonical presentation metadata.")]
        public const string ArcSideProfileArtId =
            "weapon-art.arc-rifle.side-v1";
        [Obsolete("Use ProductionWeaponCatalogueV1 canonical presentation metadata.")]
        public const string RicochetSideProfileArtId =
            "weapon-art.ricochet-weapon.side-v1";

        [Obsolete("Retired identity; no definition is registered under this ID.")]
        public static readonly StableId BlasterEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-blaster");
        [Obsolete("Retired identity; no definition is registered under this ID.")]
        public static readonly StableId ShotgunEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-shotgun");
        [Obsolete("Retired identity; no definition is registered under this ID.")]
        public static readonly StableId RocketEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-rocket-launcher");
        [Obsolete("Retired identity; no definition is registered under this ID.")]
        public static readonly StableId ArcEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-arc-gun");
        [Obsolete("Retired identity; no definition is registered under this ID.")]
        public static readonly StableId RicochetEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-ricochet-gun");

        [Obsolete("Fixed starter instances were retired and are never reserved.")]
        public static readonly StableId BlasterEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.retired-starter-blaster");
        [Obsolete("Fixed starter instances were retired and are never reserved.")]
        public static readonly StableId ShotgunEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.retired-starter-shotgun");
        [Obsolete("Fixed starter instances were retired and are never reserved.")]
        public static readonly StableId RocketEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.retired-starter-rocket-launcher");
        [Obsolete("Fixed starter instances were retired and are never reserved.")]
        public static readonly StableId ArcEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.retired-starter-arc-gun");
        [Obsolete("Fixed starter instances were retired and are never reserved.")]
        public static readonly StableId RicochetEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.retired-starter-ricochet-gun");

        private static readonly StableId[] EmptyStableIds =
            Array.Empty<StableId>();

        public static ProductionWeaponCatalogueProjectionV1 Current
        {
            get { return ProductionWeaponCatalogueV1.Current; }
        }

        public static IReadOnlyList<ProductionWeaponFamilyV1> Families
        {
            get { return Current.Families; }
        }

        public static IReadOnlyList<WeaponBlueprint> Blueprints
        {
            get { return Current.Blueprints; }
        }

        /// <summary>
        /// Exact definitions available to catalogue, strongbox, shop, inventory, and simulator
        /// consumers. This is deliberately separate from legacy starter-grant lists.
        /// </summary>
        public static IReadOnlyList<StableId>
            CatalogueEquipmentDefinitionStableIds
        {
            get { return Current.EquipmentDefinitionIds; }
        }

        public static IReadOnlyList<StableId>
            InitialEquipmentDefinitionStableIds
        {
            get { return EmptyStableIds; }
        }

        /// <summary>
        /// Legacy starter bootstrap input. It must stay empty: catalogue membership is not an
        /// instruction to fabricate one owned copy of every weapon.
        /// </summary>
        public static IReadOnlyList<StableId>
            AllEquipmentDefinitionStableIds
        {
            get { return EmptyStableIds; }
        }

        public static bool TryResolveBlueprint(
            string definitionId,
            out WeaponBlueprint blueprint)
        {
            return Current.TryGetBlueprint(definitionId, out blueprint);
        }

        public static bool TryResolveDefinitionForInstance(
            StableId equipmentInstanceStableId,
            out StableId equipmentDefinitionStableId)
        {
            equipmentDefinitionStableId = null;
            return false;
        }

        public static StableId ReserveInstanceForDefinition(
            StableId equipmentDefinitionStableId)
        {
            throw new InvalidOperationException(
                "WEAPON-CATALOGUE-001 registers definitions but does not create "
                + "fixed starter instances. Onboarding must grant exact instances "
                + "through its own policy.");
        }

        public static EquipmentCatalog BuildEquipmentCatalog()
        {
            return Current.EquipmentCatalog;
        }

        public static WeaponCatalog BuildWeaponCatalog()
        {
            return Current.WeaponCatalog;
        }
    }
}

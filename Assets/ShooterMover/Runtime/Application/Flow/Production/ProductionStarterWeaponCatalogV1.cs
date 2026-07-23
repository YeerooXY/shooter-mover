using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Transitional production catalog boundary.
    ///
    /// The previous implementation authored five demo weapons, their equipment records,
    /// fixed instance identities, presentation references, and starter grants directly in
    /// C#. Production now starts with no fabricated weapon content. A canonical authored
    /// catalog must replace this boundary before weapons become available again.
    /// </summary>
    public static class ProductionStarterWeaponCatalogV1
    {
        // Retained temporarily as compile-time compatibility identities for consumers being
        // migrated to the canonical catalog. They are not registered, granted, or resolvable.
        public const string ArcWeaponDefinitionId = "weapon.arc-gun";
        public const string RicochetWeaponDefinitionId = "weapon.ricochet-gun";
        public const string BlasterSideProfileArtId =
            "weapon-art.blaster.side-v1";
        public const string ShotgunSideProfileArtId =
            "weapon-art.shotgun-basic.side-v1";
        public const string RocketSideProfileArtId =
            "weapon-art.rocket-launcher.side-v1";
        public const string ArcSideProfileArtId =
            "weapon-art.arc-rifle.side-v1";
        public const string RicochetSideProfileArtId =
            "weapon-art.ricochet-weapon.side-v1";

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

        public static readonly StableId BlasterEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.retired-starter-blaster");
        public static readonly StableId ShotgunEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.retired-starter-shotgun");
        public static readonly StableId RocketEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.retired-starter-rocket-launcher");
        public static readonly StableId ArcEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.retired-starter-arc-gun");
        public static readonly StableId RicochetEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.retired-starter-ricochet-gun");

        private static readonly StableId[] EmptyStableIds =
            Array.Empty<StableId>();

        public static IReadOnlyList<StableId>
            InitialEquipmentDefinitionStableIds
        {
            get { return EmptyStableIds; }
        }

        public static IReadOnlyList<StableId>
            AllEquipmentDefinitionStableIds
        {
            get { return EmptyStableIds; }
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
                "No production starter weapon definitions are registered. "
                + "Compose the canonical weapon catalog instead.");
        }

        public static EquipmentCatalog BuildEquipmentCatalog()
        {
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                Array.Empty<EquipmentDefinition>(),
                Array.Empty<AugmentDefinition>());
            if (!result.IsValid || result.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The empty production equipment catalog was rejected.");
            }

            return result.Catalog;
        }

        public static WeaponCatalog BuildWeaponCatalog()
        {
            var rules = new WeaponCatalogRules(
                true,
                "20-25",
                new[] { 75, 105, 135 },
                Array.Empty<string>(),
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
                    StringComparer.Ordinal));

            return new WeaponCatalog(
                "1.0",
                "production-empty",
                rules,
                inputs,
                new Dictionary<string, WeaponArchetypeDefinition>(
                    StringComparer.Ordinal),
                Array.Empty<WeaponFamilyDefinition>(),
                Array.Empty<WeaponDefinitionData>());
        }
    }
}

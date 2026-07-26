using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Catalog
{
    /// <summary>
    /// Current production weapon-content authority. Combat tuning is deliberately boring and
    /// provisional except for the confirmed Rattler MK1 starter values. The catalogue exists to
    /// exercise permanent family rarity, MK progression, strongbox anchors, and craft unlocks.
    /// </summary>
    public static partial class ProductionWeaponCatalogueV1
    {
        public const string CatalogueVersion = "weapon-catalogue-001";
        public const string CatalogueStatus = "production-provisional-balance";

        private const string ProvisionalArchetypeId =
            "weapon-archetype.provisional-projectile";
        private const string ProvisionalWeaponCategoryId =
            "weapon-category.provisional-projectile";
        private const double PlaceholderRateOfFire = 4d;
        private const double PlaceholderDamage = 1d;
        private const double PlaceholderProjectileSpeed = 20d;
        private const double PlaceholderRange = 25d;
        private const double PlaceholderProjectileRadius = 0.1d;
        private const double PlaceholderBaseWeight = 1d;

        private static readonly ProductionWeaponCatalogueProjectionV1 current =
            Build();

        public static ProductionWeaponCatalogueProjectionV1 Current
        {
            get { return current; }
        }

        private static ProductionWeaponCatalogueProjectionV1 Build()
        {
            var families = new[]
            {
                BuildFamily(
                    "rattler",
                    "Rattler",
                    "common",
                    new[] { 1, 25, 50 },
                    false),
                BuildFamily(
                    "ironwake",
                    "Ironwake",
                    "common",
                    new[] { 60, 80, 100 },
                    true),
                BuildFamily(
                    "voltspike",
                    "Voltspike",
                    "rare",
                    new[] { 58, 79, 100 },
                    true),
                BuildFamily(
                    "prismata",
                    "Prismata",
                    "epic",
                    new[] { 64, 84, 99 },
                    true),
                BuildFamily(
                    "crownfall",
                    "Crownfall",
                    "legendary",
                    new[] { 68, 88, 103 },
                    true),
                BuildFamily(
                    "nullstar",
                    "Nullstar",
                    "artifact",
                    new[] { 70, 90, 110 },
                    true),
            };

            return new ProductionWeaponCatalogueProjectionV1(
                families,
                BuildWeaponCatalog(families),
                BuildEquipmentCatalog(families));
        }

        private static ProductionWeaponFamilyV1 BuildFamily(
            string slug,
            string displayName,
            string rarity,
            IReadOnlyList<int> anchors,
            bool provisionalFamily)
        {
            if (anchors == null || anchors.Count != 3)
            {
                throw new ArgumentException(
                    "Every weapon family requires exactly three drop anchors.",
                    nameof(anchors));
            }

            string familyId = slug;
            StableId rarityId = StableId.Create("weapon-rarity", rarity);
            var marks = new ProductionWeaponMarkV1[3];
            for (int index = 0; index < marks.Length; index++)
            {
                int mark = index + 1;
                int anchor = anchors[index];
                bool provisionalCombat = provisionalFamily
                    || !string.Equals(slug, "rattler", StringComparison.Ordinal)
                    || mark != 1;
                marks[index] = BuildMark(
                    familyId,
                    slug,
                    displayName,
                    rarityId,
                    mark,
                    anchor,
                    Math.Min(anchor, 100),
                    provisionalCombat);
            }

            return new ProductionWeaponFamilyV1(
                familyId,
                displayName,
                StableId.Parse(ProvisionalWeaponCategoryId),
                rarityId,
                rarity,
                marks);
        }

        private static ProductionWeaponMarkV1 BuildMark(
            string familyId,
            string slug,
            string familyDisplayName,
            StableId rarityId,
            int mark,
            int dropAnchorLevel,
            int craftUnlockLevel,
            bool provisionalCombat)
        {
            string definitionId = familyId + ".mk" + mark;
            string equipmentValue = "weapon-" + slug + "-mk" + mark;
            string presentationKey = slug + ".mk" + mark;
            WeaponBlueprint blueprint = WeaponBlueprint.CreateAuthored(
                new WeaponIdentity(
                    new WeaponDefinitionId(definitionId),
                    familyDisplayName + " MK" + mark,
                    familyId),
                WeaponFireSettings.Automatic(PlaceholderRateOfFire),
                WeaponShotPattern.Canonical(1, 0d),
                new WeaponBaseStats(
                    PlaceholderDamage,
                    WeaponDamageCategory.Physical,
                    null,
                    PierceValue.FromLegacyInteger(1),
                    new RicochetValue(0),
                    0d,
                    WeaponAttackDistance.Limited(PlaceholderRange)),
                WeaponDeliverySpec.Create(
                    WeaponDeliveryType.Normal,
                    new WeaponNormalDeliverySettings(
                        PlaceholderProjectileSpeed,
                        PlaceholderProjectileRadius),
                    null,
                    null,
                    null,
                    null,
                    WeaponGuidanceSpec.Unguided(),
                    StandardTravellingImpact(),
                    WeaponEffects.None()),
                new WeaponPresentation(
                    "weapon-art." + presentationKey + ".side-v1",
                    "weapon-art." + presentationKey + ".mounted-top-v1",
                    "weapon-delivery-art.provisional-projectile.v1",
                    "weapon-trail-art.provisional-projectile.v1",
                    "weapon-impact-art.provisional-projectile.v1",
                    null),
                new WeaponDropMetadata(
                    StableId.Create("equipment", equipmentValue),
                    rarityId,
                    WeaponDropAvailability.Live,
                    dropAnchorLevel,
                    PlaceholderBaseWeight,
                    WeaponStrongboxEligibility.FromMinimumTier(1)));

            return new ProductionWeaponMarkV1(
                mark,
                dropAnchorLevel,
                craftUnlockLevel,
                provisionalCombat,
                blueprint);
        }

        private static WeaponImpactSpec StandardTravellingImpact()
        {
            return WeaponImpactSpec.Create(
                true,
                true,
                true,
                true,
                null,
                null);
        }
    }
}

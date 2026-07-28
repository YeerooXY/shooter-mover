using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Catalog
{
    /// <summary>
    /// Replaces the provisional shotgun matrix slot with the player-facing Sweeper family while
    /// retaining ProductionWeaponCatalogueV1 as the single catalogue authority.
    /// </summary>
    public static partial class ProductionWeaponCatalogueV1
    {
        private const string SweeperFamilyId = "sweeper";
        private const string ReplacedProvisionalShotgunFamilyId = "ironwake";

        static ProductionWeaponCatalogueV1()
        {
            var families = new List<ProductionWeaponFamilyV1>(current.Families.Count);
            bool replaced = false;
            for (int index = 0; index < current.Families.Count; index++)
            {
                ProductionWeaponFamilyV1 family = current.Families[index];
                if (string.Equals(
                        family.FamilyId,
                        ReplacedProvisionalShotgunFamilyId,
                        StringComparison.Ordinal))
                {
                    if (replaced)
                    {
                        throw new InvalidOperationException(
                            "The provisional shotgun catalogue slot is duplicated.");
                    }

                    families.Add(BuildSweeperFamily());
                    replaced = true;
                    continue;
                }

                families.Add(family);
            }

            if (!replaced)
            {
                throw new InvalidOperationException(
                    "The provisional shotgun catalogue slot required by Sweeper is missing.");
            }

            current = new ProductionWeaponCatalogueProjectionV1(
                families,
                BuildWeaponCatalog(families),
                BuildEquipmentCatalog(families));
        }

        private static ProductionWeaponFamilyV1 BuildSweeperFamily()
        {
            StableId rarityId = StableId.Create("weapon-rarity", "common");
            return new ProductionWeaponFamilyV1(
                SweeperFamilyId,
                "Sweeper",
                StableId.Create("weapon-category", "shotgun"),
                rarityId,
                "common",
                new[]
                {
                    BuildSweeperMark(1, 60, 60, rarityId),
                    BuildSweeperMark(2, 80, 80, rarityId),
                    BuildSweeperMark(3, 100, 100, rarityId),
                });
        }

        private static ProductionWeaponMarkV1 BuildSweeperMark(
            int mark,
            int dropAnchorLevel,
            int craftUnlockLevel,
            StableId rarityId)
        {
            string definitionId = SweeperFamilyId + ".mk" + mark;
            string presentationKey = SweeperFamilyId + ".mk" + mark;
            ProvisionalCombatProfile combat = TravellingProfile(
                WeaponFireSettings.Automatic(2d),
                WeaponShotPattern.Canonical(3, 24d),
                1d,
                WeaponDamageCategory.Physical,
                null,
                1,
                6d,
                16d,
                WeaponDeliveryType.Normal,
                28d,
                0.12d,
                WeaponGuidanceSpec.Unguided(),
                StandardTravellingImpact(),
                WeaponEffects.None(),
                "shotgun-physical");
            WeaponBlueprint blueprint = WeaponBlueprint.CreateAuthored(
                new WeaponIdentity(
                    new WeaponDefinitionId(definitionId),
                    "Sweeper MK" + mark,
                    SweeperFamilyId),
                combat.FireSettings,
                combat.ShotPattern,
                combat.BaseStats,
                combat.Delivery,
                new WeaponPresentation(
                    "weapon-art." + presentationKey + ".side-v1",
                    "weapon-art." + presentationKey + ".mounted-top-v1",
                    "weapon-delivery-art." + combat.PresentationKey + ".v1",
                    "weapon-trail-art." + combat.PresentationKey + ".v1",
                    "weapon-impact-art." + combat.PresentationKey + ".v1",
                    null),
                new WeaponDropMetadata(
                    StableId.Create(
                        "equipment",
                        "weapon-sweeper-mk" + mark),
                    rarityId,
                    WeaponDropAvailability.Live,
                    dropAnchorLevel,
                    PlaceholderBaseWeight,
                    WeaponStrongboxEligibility.FromMinimumTier(1)));

            return new ProductionWeaponMarkV1(
                mark,
                dropAnchorLevel,
                craftUnlockLevel,
                true,
                blueprint);
        }
    }
}

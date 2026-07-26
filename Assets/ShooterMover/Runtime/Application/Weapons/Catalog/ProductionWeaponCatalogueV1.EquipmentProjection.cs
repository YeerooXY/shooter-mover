using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Application.Weapons.Catalog
{
    public static partial class ProductionWeaponCatalogueV1
    {
        private static EquipmentCatalog BuildEquipmentCatalog(
            IReadOnlyList<ProductionWeaponFamilyV1> families)
        {
            var definitions = new List<EquipmentDefinition>(
                families.Count * 3);
            for (int familyIndex = 0;
                 familyIndex < families.Count;
                 familyIndex++)
            {
                ProductionWeaponFamilyV1 family = families[familyIndex];
                EquipmentQualityTier quality = EquipmentQualityTier.Create(
                    StableId.Create(
                        "equipment-quality",
                        family.CatalogRarity),
                    UppercaseFirst(family.CatalogRarity),
                    ResolveQualityRank(family.CatalogRarity));

                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    ProductionWeaponMarkV1 mark = family.Marks[markIndex];
                    definitions.Add(EquipmentDefinition.Create(
                        mark.EquipmentDefinitionId,
                        EquipmentCategoryIds.Weapon,
                        StableId.Create(
                            "weapon-family",
                            family.FamilyId),
                        mark.Blueprint.DisplayName,
                        StableId.Parse(
                            mark.Blueprint.DefinitionId.ToString()),
                        InclusiveIntRange.Create(1, 200),
                        4,
                        new[] { quality },
                        Array.Empty<StableId>()));
                }
            }

            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                definitions,
                Array.Empty<AugmentDefinition>());
            if (!result.IsValid || result.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The production weapon/equipment catalogue projection was rejected.");
            }
            return result.Catalog;
        }

        private static int ResolveQualityRank(string rarity)
        {
            switch (rarity)
            {
                case "common":
                    return 1;
                case "rare":
                    return 2;
                case "epic":
                    return 3;
                case "legendary":
                    return 4;
                case "artifact":
                    return 5;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }

        private static string UppercaseFirst(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}

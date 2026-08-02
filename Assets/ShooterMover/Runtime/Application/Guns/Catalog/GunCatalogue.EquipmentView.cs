using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.Application.Items;

namespace ShooterMover.Application.Guns.Catalog
{
    public static partial class GunCatalogue
    {
        private static EquipmentCatalog BuildEquipmentCatalog(
            IReadOnlyList<GunFamily> families)
        {
            var definitions = new List<EquipmentDefinition>(
                families.Count * 3);
            for (int familyIndex = 0;
                 familyIndex < families.Count;
                 familyIndex++)
            {
                GunFamily family = families[familyIndex];
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
                    GunMark mark = family.Marks[markIndex];
                    definitions.Add(EquipmentDefinition.Create(
                        mark.EquipmentDefinitionId,
                        EquipmentCategoryIds.Gun,
                        StableId.Create(
                            "gun-family",
                            StableFamilyToken(family.FamilyId)),
                        mark.Blueprint.DisplayName,
                        mark.Blueprint.DefinitionId.ToRuntimeReference(),
                        InclusiveIntRange.Create(1, 200),
                        4,
                        new[] { quality },
                        Array.Empty<StableId>()));
                }
            }
            AddAuthoredGearDefinitions(definitions);

            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                definitions,
                GunAugments.Definitions);
            if (!result.IsValid || result.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The production gun/equipment catalogue projection was rejected.");
            }
            return result.Catalog;
        }

        private static void AddAuthoredGearDefinitions(
            ICollection<EquipmentDefinition> definitions)
        {
            foreach (ItemPackageDocument package
                in ItemPackageCatalog.Current.Packages.Values)
            {
                if (!string.Equals(
                        package.Kind,
                        "gear-set",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                EquipmentQualityTier quality =
                    EquipmentQualityTier.Create(
                        StableId.Create(
                            "equipment-quality",
                            package.Rarity),
                        UppercaseFirst(package.Rarity),
                        ResolveQualityRank(package.Rarity));
                foreach (ItemMarkPackage mark in package.Marks)
                {
                    if (!mark.Available || mark.Pieces == null)
                    {
                        continue;
                    }

                    foreach (KeyValuePair<string, GearPiecePackage> pair
                        in mark.Pieces)
                    {
                        GearPiecePackage piece = pair.Value;
                        if (piece == null)
                        {
                            throw new InvalidOperationException(
                                "Authored gear package contains a null piece: "
                                + package.Id);
                        }

                        definitions.Add(EquipmentDefinition.Create(
                            StableId.Create(
                                "equipment",
                                "gear-" + package.Id + "-"
                                + pair.Key + "-mk" + mark.Mark),
                            EquipmentCategoryIds.Armor,
                            StableId.Create("gear-set", package.Id),
                            piece.Name,
                            null,
                            InclusiveIntRange.Create(1, 200),
                            piece.MaxAugmentSlots,
                            new[] { quality },
                            new[]
                            {
                                StableId.Create("gear-slot", pair.Key),
                            }));
                    }
                }
            }
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

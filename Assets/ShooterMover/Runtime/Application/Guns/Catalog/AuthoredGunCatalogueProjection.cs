using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Catalog
{
    /// <summary>
    /// Projects validated Weapon Maker families into the compatibility catalogues consumed by
    /// Strongboxes, equipment, Inventory, Shop, and the authoritative simulator.
    /// Content/Weapons remains the only authored gun-content authority.
    /// </summary>
    internal static class AuthoredGunCatalogueProjection
    {
        private const string CatalogueVersion = "gun-catalogue-001";
        private const string CatalogueStatus =
            "production-provisional-system-matrix";
        private const string AuthoredArchetypeId =
            "gun-archetype.provisional-system-matrix";

        public static GunCatalogueView Create(
            IReadOnlyList<GunFamily> families)
        {
            if (families == null)
            {
                throw new ArgumentNullException(nameof(families));
            }
            if (families.Count == 0)
            {
                throw new ArgumentException(
                    "The authored gun catalogue requires at least one family.",
                    nameof(families));
            }

            return new GunCatalogueView(
                families,
                BuildGunCatalog(families),
                BuildEquipmentCatalog(families));
        }

        private static GunCatalog BuildGunCatalog(
            IReadOnlyList<GunFamily> families)
        {
            var rarityInputs = new Dictionary<string, GunRarityInput>(
                StringComparer.Ordinal);
            AddRarityInput(rarityInputs, "common");
            AddRarityInput(rarityInputs, "rare");
            AddRarityInput(rarityInputs, "epic");
            AddRarityInput(rarityInputs, "legendary");
            AddRarityInput(rarityInputs, "artifact");

            var archetypes =
                new Dictionary<string, GunArchetypeDefinition>(
                    StringComparer.Ordinal)
                {
                    {
                        AuthoredArchetypeId,
                        new GunArchetypeDefinition(
                            AuthoredArchetypeId,
                            "Weapon Maker authored gun; canonical mechanics remain authoritative in Gun.",
                            1d,
                            4d,
                            1,
                            1,
                            0d,
                            20d,
                            25d,
                            1d,
                            0d,
                            0d,
                            0d,
                            0d,
                            0d,
                            0d,
                            1,
                            0,
                            0d,
                            0d,
                            0d)
                    },
                };

            var catalogFamilies = new List<GunFamilyDefinition>(
                families.Count);
            var definitions = new List<GunDefinitionData>(
                families.Count * 3);
            for (int familyIndex = 0;
                 familyIndex < families.Count;
                 familyIndex++)
            {
                GunFamily family = families[familyIndex];
                catalogFamilies.Add(BuildFlatFamily(family));
                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    definitions.Add(BuildFlatDefinition(
                        family,
                        family.Marks[markIndex]));
                }
            }

            var rules = new GunCatalogRules(
                true,
                "independently-authored-drop-anchors",
                new[] { 70, 90, 110 },
                new[] { "Physical", "Thermal", "Chemical", "Energy" },
                4,
                true,
                true,
                true);
            var inputs = new GunCatalogInputs(
                4d,
                0d,
                0d,
                0d,
                rarityInputs);
            GunCatalogValidationResult validation =
                GunCatalogValidator.Validate(
                    CatalogueVersion,
                    CatalogueStatus,
                    rules,
                    inputs,
                    archetypes,
                    catalogFamilies,
                    definitions);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    "The authored gun catalogue failed flat-schema validation: "
                    + (validation.Issues.Count == 0
                        ? "unknown"
                        : validation.Issues[0].ToString()));
            }

            return new GunCatalog(
                CatalogueVersion,
                CatalogueStatus,
                rules,
                inputs,
                archetypes,
                catalogFamilies,
                definitions);
        }

        private static void AddRarityInput(
            IDictionary<string, GunRarityInput> output,
            string rarity)
        {
            output.Add(
                rarity,
                new GunRarityInput(
                    rarity,
                    1d,
                    0,
                    0.25d,
                    0.25d));
        }

        private static GunFamilyDefinition BuildFlatFamily(
            GunFamily family)
        {
            GunMark mk1 = family.Marks[0];
            GunMark mk2 = family.Marks[1];
            GunMark mk3 = family.Marks[2];
            string damageCategory = DamageCategory(family);
            return new GunFamilyDefinition(
                family.FamilyId,
                family.DisplayName,
                AuthoredArchetypeId,
                damageCategory,
                "PROVISIONAL",
                mk1.DropAnchorLevel,
                mk2.DropAnchorLevel - mk1.DropAnchorLevel,
                mk3.DropAnchorLevel - mk2.DropAnchorLevel,
                3,
                family.CatalogRarity,
                family.CatalogRarity,
                family.CatalogRarity,
                1d,
                "StrongboxAndCrafting",
                family.GunCategoryId.ToString(),
                "Generated from Content/Weapons; family rarity and canonical mechanics come from authored JSON.",
                GunCatalogAvailability.Live,
                new[]
                {
                    mk1.Blueprint.Presentation.InventorySideProfileReference,
                    mk2.Blueprint.Presentation.InventorySideProfileReference,
                    mk3.Blueprint.Presentation.InventorySideProfileReference,
                });
        }

        private static GunDefinitionData BuildFlatDefinition(
            GunFamily family,
            GunMark mark)
        {
            Gun blueprint = mark.Blueprint;
            int legacyPierce;
            if (!blueprint.BaseStats.Pierce.TryToLegacyInteger(
                    out legacyPierce))
            {
                throw new InvalidOperationException(
                    "The authored flat projection requires whole-number Pierce.");
            }
            if (blueprint.Projectile == null)
            {
                throw new InvalidOperationException(
                    "The authored flat projection currently requires travelling deliveries.");
            }

            double directDps = blueprint.BaseStats.DirectDamage
                * blueprint.FireSettings.RateOfFire
                * blueprint.ShotPattern.ProjectilesPerShot
                * blueprint.FireSettings.ShotsPerBurst;
            string damageCategory =
                GunDamageCategoryConversion.ToCatalogValue(
                    blueprint.BaseStats.DamageCategory);
            return new GunDefinitionData(
                blueprint.DefinitionId.ToString(),
                blueprint.DisplayName,
                family.FamilyId,
                mark.Mark,
                damageCategory,
                AuthoredArchetypeId,
                "PROVISIONAL",
                Math.Max(1, mark.DropAnchorLevel - 15),
                mark.DropAnchorLevel,
                mark.DropAnchorLevel,
                family.CatalogRarity,
                1d,
                1d,
                blueprint.DropMetadata.BaseSelectionWeight,
                0.25d,
                0.25d,
                "StrongboxAndCrafting",
                false,
                "craft-unlock-level:"
                    + mark.CraftUnlockLevel.ToString(
                        CultureInfo.InvariantCulture),
                1d,
                100d,
                directDps,
                1d,
                0d,
                0d,
                blueprint.FireSettings.RateOfFire,
                blueprint.ShotPattern.ProjectilesPerShot,
                blueprint.FireSettings.ShotsPerBurst,
                blueprint.BaseStats.DirectDamage,
                blueprint.ShotPattern.CanonicalSpreadDegrees,
                blueprint.Projectile.Speed,
                blueprint.Projectile.Range,
                legacyPierce,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0,
                0d,
                blueprint.BaseStats.Knockback,
                0d,
                0d,
                family.GunCategoryId.ToString(),
                "Generated from Content/Weapons. Canonical Gun owns runtime mechanics and presentation.",
                GunCatalogAvailability.Live,
                new[]
                {
                    blueprint.Presentation.InventorySideProfileReference,
                });
        }

        private static string DamageCategory(
            GunFamily family)
        {
            GunDamageCategory category =
                family.Marks[0].Blueprint.BaseStats.DamageCategory;
            for (int index = 1; index < family.Marks.Count; index++)
            {
                if (family.Marks[index].Blueprint.BaseStats.DamageCategory
                    != category)
                {
                    throw new InvalidOperationException(
                        "One authored family must use one damage channel: "
                        + family.FamilyId);
                }
            }

            return GunDamageCategoryConversion.ToCatalogValue(category);
        }

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

            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                definitions,
                GunAugments.Definitions);
            if (!result.IsValid || result.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The authored gun/equipment catalogue projection was rejected.");
            }
            return result.Catalog;
        }

        private static string StableFamilyToken(string familyId)
        {
            if (string.IsNullOrWhiteSpace(familyId))
            {
                throw new ArgumentException(
                    "A gun family ID is required.",
                    nameof(familyId));
            }
            return familyId.Replace('_', '-');
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

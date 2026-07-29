using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Catalog
{
    public static partial class GunCatalogue
    {
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
                        ProvisionalArchetypeId,
                        new GunArchetypeDefinition(
                            ProvisionalArchetypeId,
                            "PROVISIONAL canonical system-test matrix; typed mechanics remain authoritative in Gun.",
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
                    "The provisional gun catalogue failed flat-schema validation: "
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
                ProvisionalArchetypeId,
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
                "Permanent family rarity; canonical definitions carry the provisional fire mode, delivery, guidance, effect, and damage-channel test data.",
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
                    "The provisional flat projection requires whole-number Pierce.");
            }
            if (blueprint.Projectile == null)
            {
                throw new InvalidOperationException(
                    "The provisional flat projection currently requires travelling deliveries.");
            }

            double directDps = blueprint.BaseStats.DirectDamage
                * blueprint.FireSettings.RateOfFire
                * blueprint.ShotPattern.ProjectilesPerShot
                * blueprint.FireSettings.ShotsPerBurst;
            string damageCategory =
                GunDamageCategoryConversion.ToCatalogValue(
                    blueprint.BaseStats.DamageCategory);
            string note = mark.IsCombatTuningProvisional
                ? BuildProvisionalCombatNote(blueprint)
                : "Confirmed Rattler MK1 starter values: physical automatic rifle, rate of fire 4, damage 1, Pierce 1, no spread.";
            return new GunDefinitionData(
                blueprint.DefinitionId.ToString(),
                blueprint.DisplayName,
                family.FamilyId,
                mark.Mark,
                damageCategory,
                ProvisionalArchetypeId,
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
                note,
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
                        "The provisional test matrix expects one damage channel per family: "
                        + family.FamilyId);
                }
            }

            return GunDamageCategoryConversion.ToCatalogValue(category);
        }

        private static string BuildProvisionalCombatNote(
            Gun blueprint)
        {
            string behavior = blueprint.Delivery.Type.ToString();
            if (blueprint.ShotPattern.ProjectilesPerShot > 1)
            {
                behavior += " shotgun-spread";
            }
            if (blueprint.Guidance.Mode == GunGuidanceMode.Homing)
            {
                behavior += " seeking";
            }
            if (blueprint.BaseStats.DamageOverTime != null)
            {
                behavior += " damage-over-time";
            }
            if (blueprint.Delivery.Effects.Explosion != null)
            {
                behavior += " explosion";
            }

            return "PROVISIONAL SYSTEM-TEST PROFILE. "
                + blueprint.FireSettings.Mode
                + ", "
                + behavior
                + ", "
                + GunDamageCategoryConversion.ToCatalogValue(
                    blueprint.BaseStats.DamageCategory)
                + " damage. Balance is not approved.";
        }
    }
}

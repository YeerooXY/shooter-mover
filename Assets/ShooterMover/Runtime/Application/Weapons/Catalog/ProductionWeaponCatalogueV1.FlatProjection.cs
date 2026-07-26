using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    public static partial class ProductionWeaponCatalogueV1
    {
        private static WeaponCatalog BuildWeaponCatalog(
            IReadOnlyList<ProductionWeaponFamilyV1> families)
        {
            var rarityInputs = new Dictionary<string, WeaponRarityInput>(
                StringComparer.Ordinal);
            AddRarityInput(rarityInputs, "common");
            AddRarityInput(rarityInputs, "rare");
            AddRarityInput(rarityInputs, "epic");
            AddRarityInput(rarityInputs, "legendary");
            AddRarityInput(rarityInputs, "artifact");

            var archetypes =
                new Dictionary<string, WeaponArchetypeDefinition>(
                    StringComparer.Ordinal)
                {
                    {
                        ProvisionalArchetypeId,
                        new WeaponArchetypeDefinition(
                            ProvisionalArchetypeId,
                            "PROVISIONAL single-projectile automatic profile; not approved balance.",
                            1d,
                            PlaceholderRateOfFire,
                            1,
                            1,
                            0d,
                            PlaceholderProjectileSpeed,
                            PlaceholderRange,
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

            var catalogFamilies = new List<WeaponFamilyDefinition>(
                families.Count);
            var definitions = new List<WeaponDefinitionData>(
                families.Count * 3);
            for (int familyIndex = 0;
                 familyIndex < families.Count;
                 familyIndex++)
            {
                ProductionWeaponFamilyV1 family = families[familyIndex];
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

            var rules = new WeaponCatalogRules(
                true,
                "independently-authored-drop-anchors",
                new[] { 70, 90, 110 },
                new[] { "Physical" },
                4,
                true,
                true,
                true);
            var inputs = new WeaponCatalogInputs(
                4d,
                0d,
                0d,
                0d,
                rarityInputs);
            WeaponCatalogValidationResult validation =
                WeaponCatalogValidator.Validate(
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
                    "The provisional weapon catalogue failed flat-schema validation: "
                    + (validation.Issues.Count == 0
                        ? "unknown"
                        : validation.Issues[0].ToString()));
            }

            return new WeaponCatalog(
                CatalogueVersion,
                CatalogueStatus,
                rules,
                inputs,
                archetypes,
                catalogFamilies,
                definitions);
        }

        private static void AddRarityInput(
            IDictionary<string, WeaponRarityInput> output,
            string rarity)
        {
            output.Add(
                rarity,
                new WeaponRarityInput(
                    rarity,
                    1d,
                    0,
                    0.25d,
                    0.25d));
        }

        private static WeaponFamilyDefinition BuildFlatFamily(
            ProductionWeaponFamilyV1 family)
        {
            ProductionWeaponMarkV1 mk1 = family.Marks[0];
            ProductionWeaponMarkV1 mk2 = family.Marks[1];
            ProductionWeaponMarkV1 mk3 = family.Marks[2];
            return new WeaponFamilyDefinition(
                family.FamilyId,
                family.DisplayName,
                ProvisionalArchetypeId,
                "Physical",
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
                "ProvisionalProjectile",
                "Permanent family rarity; combat values remain provisional unless explicitly noted.",
                WeaponCatalogAvailability.Live,
                new[]
                {
                    mk1.Blueprint.Presentation.InventorySideProfileReference,
                    mk2.Blueprint.Presentation.InventorySideProfileReference,
                    mk3.Blueprint.Presentation.InventorySideProfileReference,
                });
        }

        private static WeaponDefinitionData BuildFlatDefinition(
            ProductionWeaponFamilyV1 family,
            ProductionWeaponMarkV1 mark)
        {
            WeaponBlueprint blueprint = mark.Blueprint;
            int legacyPierce;
            if (!blueprint.BaseStats.Pierce.TryToLegacyInteger(
                    out legacyPierce))
            {
                throw new InvalidOperationException(
                    "The provisional flat projection requires whole-number Pierce.");
            }

            string note = mark.IsCombatTuningProvisional
                ? "PROVISIONAL COMBAT VALUES. Identity, family rarity, Mark, drop anchor, and craft unlock are the intended data under review."
                : "Confirmed Rattler MK1 starter values: kinetic automatic rifle, rate of fire 4, damage 1, Pierce 1, no spread.";
            return new WeaponDefinitionData(
                blueprint.DefinitionId.ToString(),
                blueprint.DisplayName,
                family.FamilyId,
                mark.Mark,
                "Physical",
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
                blueprint.BaseStats.DirectDamage
                    * blueprint.FireSettings.RateOfFire
                    * blueprint.ShotPattern.ProjectilesPerShot,
                1d,
                0d,
                0d,
                blueprint.FireSettings.RateOfFire,
                blueprint.ShotPattern.ProjectilesPerShot,
                blueprint.FireSettings.ShotsPerBurst,
                blueprint.BaseStats.DirectDamage,
                blueprint.ShotPattern.SpreadDegrees,
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
                "ProvisionalProjectile",
                note,
                WeaponCatalogAvailability.Live,
                new[]
                {
                    blueprint.Presentation.InventorySideProfileReference,
                });
        }
    }
}

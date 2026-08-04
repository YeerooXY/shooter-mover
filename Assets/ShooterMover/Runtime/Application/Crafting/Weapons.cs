using System;
using System.Collections.Generic;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Crafting
{
    /// <summary>
    /// Loads weapon crafting content and proves every recipe points at the single
    /// authored weapon authority.
    /// </summary>
    public static class Weapons
    {
        private const int CraftedWeaponLevel = 10;
        private const int CraftedWeaponAugmentSlots = 3;

        public static AllItems Load(IEnumerable<string> jsonDocuments)
        {
            return Load(jsonDocuments, AuthoredGunCatalogue.Current);
        }

        public static AllItems Load(
            IEnumerable<string> jsonDocuments,
            GunCatalogueView guns)
        {
            if (guns == null)
            {
                throw new ArgumentNullException(nameof(guns));
            }

            AllItems items = ItemsLoader.Load(jsonDocuments);
            Validate(items, guns);
            return items;
        }

        public static void Validate(
            AllItems items,
            GunCatalogueView guns)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }
            if (guns == null)
            {
                throw new ArgumentNullException(nameof(guns));
            }

            for (int categoryIndex = 0;
                 categoryIndex < items.Categories.Count;
                 categoryIndex++)
            {
                Category category = items.Categories[categoryIndex];
                string categoryPath = "categories[" + category.Id + "]";
                if (category.CreatedLevel != CraftedWeaponLevel)
                {
                    throw Error(
                        categoryPath
                        + ".createdLevel:weapon-level-must-equal:"
                        + CraftedWeaponLevel);
                }
                if (category.AugmentSlots != CraftedWeaponAugmentSlots)
                {
                    throw Error(
                        categoryPath
                        + ".augmentSlots:weapon-slots-must-equal:"
                        + CraftedWeaponAugmentSlots);
                }

                for (int itemIndex = 0;
                     itemIndex < category.Items.Count;
                     itemIndex++)
                {
                    ValidateItem(
                        category.Items[itemIndex],
                        categoryPath,
                        guns);
                }
            }
        }

        private static void ValidateItem(
            CraftableItem item,
            string categoryPath,
            GunCatalogueView guns)
        {
            string itemPath = categoryPath + ".items[" + item.Id + "]";
            for (int recipeIndex = 0;
                 recipeIndex < item.Recipes.Count;
                 recipeIndex++)
            {
                Recipe recipe = item.Recipes[recipeIndex];
                string recipePath = itemPath
                    + ".recipes["
                    + MarkText(recipe.Mark)
                    + "]";

                GunMark gun;
                if (!guns.TryGetMark(recipe.ItemId, out gun) || gun == null)
                {
                    throw Error(
                        recipePath
                        + ".itemId:weapon-missing:"
                        + recipe.ItemId);
                }
                if (gun.Mark != (int)recipe.Mark)
                {
                    throw Error(
                        recipePath
                        + ".mark:weapon-mark-mismatch:"
                        + gun.Mark);
                }
                if (gun.Blueprint == null)
                {
                    throw Error(
                        recipePath
                        + ".itemId:weapon-blueprint-missing:"
                        + recipe.ItemId);
                }
                if (!string.Equals(
                        gun.Blueprint.DefinitionId.ToString(),
                        recipe.ItemId,
                        StringComparison.Ordinal))
                {
                    throw Error(
                        recipePath
                        + ".itemId:resolved-definition-mismatch:"
                        + gun.Blueprint.DefinitionId);
                }
                if (!string.Equals(
                        gun.Blueprint.GunFamily,
                        item.Id,
                        StringComparison.Ordinal))
                {
                    throw Error(
                        recipePath
                        + ".itemId:weapon-family-mismatch:"
                        + gun.Blueprint.GunFamily);
                }
                if (gun.EquipmentDefinitionId == null
                    || guns.EquipmentCatalog.FindEquipmentDefinition(
                        gun.EquipmentDefinitionId) == null)
                {
                    throw Error(
                        recipePath
                        + ".itemId:weapon-equipment-missing:"
                        + recipe.ItemId);
                }
            }
        }

        private static string MarkText(Mark mark)
        {
            switch (mark)
            {
                case Mark.Mk1:
                    return "mk1";
                case Mark.Mk2:
                    return "mk2";
                case Mark.Mk3:
                    return "mk3";
                default:
                    return mark.ToString();
            }
        }

        private static InvalidOperationException Error(string detail)
        {
            return new InvalidOperationException(
                "crafting-weapons:" + detail);
        }
    }
}

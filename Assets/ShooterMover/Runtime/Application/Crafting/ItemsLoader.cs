using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Domain.Crafting;

namespace ShooterMover.Application.Crafting
{
    /// <summary>
    /// Converts embedded crafting category JSON into immutable game data.
    /// The caller supplies build-included JSON; this loader never reads the filesystem.
    /// </summary>
    public static class ItemsLoader
    {
        public static AllItems Load(IEnumerable<string> jsonDocuments)
        {
            if (jsonDocuments == null)
            {
                throw new ArgumentNullException(nameof(jsonDocuments));
            }

            var categories = new List<Category>();
            var categoryIds = new HashSet<string>(StringComparer.Ordinal);
            var allItemIds = new HashSet<string>(StringComparer.Ordinal);
            var allRecipeItemIds = new HashSet<string>(StringComparer.Ordinal);

            int documentIndex = 0;
            foreach (string json in jsonDocuments)
            {
                string path = "documents[" + documentIndex + "]";
                CategoryDocument document = ReadDocument(json, path);
                string categoryId = RequireText(document.Id, path + ".id");
                if (!categoryIds.Add(categoryId))
                {
                    throw Error(path + ".id:duplicate:" + categoryId);
                }

                categories.Add(BuildCategory(
                    document,
                    path,
                    allItemIds,
                    allRecipeItemIds));
                documentIndex++;
            }

            if (categories.Count == 0)
            {
                throw Error("documents:missing");
            }

            categories.Sort(CompareCategories);
            try
            {
                return new AllItems(categories);
            }
            catch (Exception exception)
            {
                throw Error("all-items:invalid", exception);
            }
        }

        private static CategoryDocument ReadDocument(string json, string path)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw Error(path + ":missing");
            }

            CategoryDocument document;
            try
            {
                var serializer = new DataContractJsonSerializer(
                    typeof(CategoryDocument));
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                using (var stream = new MemoryStream(bytes, false))
                {
                    document = serializer.ReadObject(stream) as CategoryDocument;
                }
            }
            catch (Exception exception)
            {
                throw Error(path + ":invalid-json", exception);
            }

            if (document == null)
            {
                throw Error(path + ":invalid");
            }
            return document;
        }

        private static Category BuildCategory(
            CategoryDocument source,
            string path,
            ISet<string> allItemIds,
            ISet<string> allRecipeItemIds)
        {
            string id = RequireText(source.Id, path + ".id");
            string name = RequireText(source.Name, path + ".name");
            int order = RequireNonNegative(source.Order, path + ".order");
            int createdLevel = RequirePositive(
                source.CreatedLevel,
                path + ".createdLevel");
            int augmentSlots = RequireNonNegative(
                source.AugmentSlots,
                path + ".augmentSlots");

            if (source.Items == null || source.Items.Count == 0)
            {
                throw Error(path + ".items:missing");
            }

            var items = new List<CraftableItem>(source.Items.Count);
            var localItemIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Items.Count; index++)
            {
                string itemPath = path + ".items[" + index + "]";
                ItemDocument itemSource = source.Items[index];
                if (itemSource == null)
                {
                    throw Error(itemPath + ":missing");
                }

                CraftableItem item = BuildItem(
                    itemSource,
                    itemPath,
                    allRecipeItemIds);
                if (!localItemIds.Add(item.Id))
                {
                    throw Error(itemPath + ".id:duplicate-in-category:" + item.Id);
                }
                if (!allItemIds.Add(item.Id))
                {
                    throw Error(itemPath + ".id:already-in-another-category:" + item.Id);
                }
                items.Add(item);
            }

            items.Sort(CompareItems);
            try
            {
                return new Category(
                    id,
                    name,
                    order,
                    createdLevel,
                    augmentSlots,
                    items);
            }
            catch (Exception exception)
            {
                throw Error(path + ":invalid-category", exception);
            }
        }

        private static CraftableItem BuildItem(
            ItemDocument source,
            string path,
            ISet<string> allRecipeItemIds)
        {
            string id = RequireText(source.Id, path + ".id");
            int order = RequireNonNegative(source.Order, path + ".order");
            if (source.Recipes == null || source.Recipes.Count == 0)
            {
                throw Error(path + ".recipes:missing");
            }

            var recipes = new List<Recipe>(source.Recipes.Count);
            var marks = new HashSet<Mark>();
            for (int index = 0; index < source.Recipes.Count; index++)
            {
                string recipePath = path + ".recipes[" + index + "]";
                RecipeDocument recipeSource = source.Recipes[index];
                if (recipeSource == null)
                {
                    throw Error(recipePath + ":missing");
                }

                Recipe recipe = BuildRecipe(recipeSource, recipePath);
                if (!marks.Add(recipe.Mark))
                {
                    throw Error(
                        recipePath
                        + ".mark:duplicate:"
                        + MarkText(recipe.Mark));
                }
                if (!allRecipeItemIds.Add(recipe.ItemId))
                {
                    throw Error(
                        recipePath
                        + ".itemId:duplicate:"
                        + recipe.ItemId);
                }
                recipes.Add(recipe);
            }

            recipes.Sort((left, right) => left.Mark.CompareTo(right.Mark));
            try
            {
                return new CraftableItem(id, order, recipes);
            }
            catch (Exception exception)
            {
                throw Error(path + ":invalid-item", exception);
            }
        }

        private static Recipe BuildRecipe(
            RecipeDocument source,
            string path)
        {
            string itemId = RequireText(source.ItemId, path + ".itemId");
            Mark mark = RequireMark(source.Mark, path + ".mark");
            int unlockLevel = RequireNonNegative(
                source.UnlockLevel,
                path + ".unlockLevel");
            if (source.Costs == null || source.Costs.Count == 0)
            {
                throw Error(path + ".costs:missing");
            }

            var costs = new List<Cost>(source.Costs.Count);
            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Costs.Count; index++)
            {
                string costPath = path + ".costs[" + index + "]";
                CostDocument costSource = source.Costs[index];
                if (costSource == null)
                {
                    throw Error(costPath + ":missing");
                }

                string resourceId = RequireText(
                    costSource.ResourceId,
                    costPath + ".resourceId");
                long amount = RequirePositive(
                    costSource.Amount,
                    costPath + ".amount");
                if (!resourceIds.Add(resourceId))
                {
                    throw Error(
                        costPath
                        + ".resourceId:duplicate:"
                        + resourceId);
                }
                costs.Add(new Cost(resourceId, amount));
            }

            try
            {
                return new Recipe(itemId, mark, unlockLevel, costs);
            }
            catch (Exception exception)
            {
                throw Error(path + ":invalid-recipe", exception);
            }
        }

        private static string RequireText(string value, string path)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Error(path + ":missing");
            }
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw Error(path + ":surrounding-whitespace");
            }
            return value;
        }

        private static int RequireNonNegative(int? value, string path)
        {
            if (!value.HasValue)
            {
                throw Error(path + ":missing");
            }
            if (value.Value < 0)
            {
                throw Error(path + ":must-not-be-negative");
            }
            return value.Value;
        }

        private static int RequirePositive(int? value, string path)
        {
            if (!value.HasValue)
            {
                throw Error(path + ":missing");
            }
            if (value.Value < 1)
            {
                throw Error(path + ":must-be-positive");
            }
            return value.Value;
        }

        private static long RequirePositive(long? value, string path)
        {
            if (!value.HasValue)
            {
                throw Error(path + ":missing");
            }
            if (value.Value < 1L)
            {
                throw Error(path + ":must-be-positive");
            }
            return value.Value;
        }

        private static Mark RequireMark(string value, string path)
        {
            switch (value)
            {
                case "mk1":
                    return Mark.Mk1;
                case "mk2":
                    return Mark.Mk2;
                case "mk3":
                    return Mark.Mk3;
                default:
                    throw Error(
                        path
                        + ":unsupported:"
                        + (value ?? "<null>"));
            }
        }

        private static int CompareCategories(Category left, Category right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0
                ? order
                : string.CompareOrdinal(left.Id, right.Id);
        }

        private static int CompareItems(
            CraftableItem left,
            CraftableItem right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0
                ? order
                : string.CompareOrdinal(left.Id, right.Id);
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
                "crafting-content:" + detail);
        }

        private static InvalidOperationException Error(
            string detail,
            Exception innerException)
        {
            return new InvalidOperationException(
                "crafting-content:" + detail,
                innerException);
        }

        [DataContract]
        private sealed class CategoryDocument
        {
            [DataMember(Name = "id")]
            public string Id { get; set; }

            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "order")]
            public int? Order { get; set; }

            [DataMember(Name = "createdLevel")]
            public int? CreatedLevel { get; set; }

            [DataMember(Name = "augmentSlots")]
            public int? AugmentSlots { get; set; }

            [DataMember(Name = "items")]
            public List<ItemDocument> Items { get; set; }
        }

        [DataContract]
        private sealed class ItemDocument
        {
            [DataMember(Name = "id")]
            public string Id { get; set; }

            [DataMember(Name = "order")]
            public int? Order { get; set; }

            [DataMember(Name = "recipes")]
            public List<RecipeDocument> Recipes { get; set; }
        }

        [DataContract]
        private sealed class RecipeDocument
        {
            [DataMember(Name = "itemId")]
            public string ItemId { get; set; }

            [DataMember(Name = "mark")]
            public string Mark { get; set; }

            [DataMember(Name = "unlockLevel")]
            public int? UnlockLevel { get; set; }

            [DataMember(Name = "costs")]
            public List<CostDocument> Costs { get; set; }
        }

        [DataContract]
        private sealed class CostDocument
        {
            [DataMember(Name = "resourceId")]
            public string ResourceId { get; set; }

            [DataMember(Name = "amount")]
            public long? Amount { get; set; }
        }
    }
}

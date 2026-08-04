using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.Domain.Crafting
{
    public sealed class CraftableItem
    {
        private readonly ReadOnlyCollection<Recipe> recipes;

        public CraftableItem(
            string id,
            int order,
            IEnumerable<Recipe> recipes)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "A craftable item requires an ID.",
                    nameof(id));
            }
            if (!string.Equals(id, id.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A craftable item ID must not contain surrounding whitespace.",
                    nameof(id));
            }
            if (order < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(order),
                    "A craftable item order must not be negative.");
            }
            if (recipes == null)
            {
                throw new ArgumentNullException(nameof(recipes));
            }

            var copiedRecipes = new List<Recipe>();
            var marks = new HashSet<Mark>();
            foreach (Recipe recipe in recipes)
            {
                if (recipe == null)
                {
                    throw new ArgumentException(
                        "Craftable item recipes must not contain null entries.",
                        nameof(recipes));
                }
                if (!marks.Add(recipe.Mark))
                {
                    throw new ArgumentException(
                        "A craftable item must not repeat the same Mark recipe.",
                        nameof(recipes));
                }
                copiedRecipes.Add(recipe);
            }
            if (copiedRecipes.Count == 0)
            {
                throw new ArgumentException(
                    "A craftable item requires at least one recipe.",
                    nameof(recipes));
            }

            Id = id;
            Order = order;
            this.recipes = new ReadOnlyCollection<Recipe>(copiedRecipes);
        }

        public string Id { get; }
        public int Order { get; }
        public IReadOnlyList<Recipe> Recipes { get { return recipes; } }
    }
}

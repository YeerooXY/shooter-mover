using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.Domain.Crafting
{
    public sealed class AllItems
    {
        private readonly ReadOnlyCollection<Category> categories;

        public AllItems(IEnumerable<Category> categories)
        {
            if (categories == null)
            {
                throw new ArgumentNullException(nameof(categories));
            }

            var copiedCategories = new List<Category>();
            var categoryIds = new HashSet<string>(StringComparer.Ordinal);
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Category category in categories)
            {
                if (category == null)
                {
                    throw new ArgumentException(
                        "Crafting categories must not contain null entries.",
                        nameof(categories));
                }
                if (!categoryIds.Add(category.Id))
                {
                    throw new ArgumentException(
                        "Crafting categories must have unique IDs.",
                        nameof(categories));
                }

                for (int index = 0; index < category.Items.Count; index++)
                {
                    CraftableItem item = category.Items[index];
                    if (!itemIds.Add(item.Id))
                    {
                        throw new ArgumentException(
                            "A craftable item must belong to exactly one category.",
                            nameof(categories));
                    }
                }

                copiedCategories.Add(category);
            }
            if (copiedCategories.Count == 0)
            {
                throw new ArgumentException(
                    "Crafting requires at least one category.",
                    nameof(categories));
            }

            this.categories = new ReadOnlyCollection<Category>(copiedCategories);
        }

        public IReadOnlyList<Category> Categories { get { return categories; } }
    }
}

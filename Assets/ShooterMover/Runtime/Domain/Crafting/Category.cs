using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.Domain.Crafting
{
    public sealed class Category
    {
        private readonly ReadOnlyCollection<CraftableItem> items;

        public Category(
            string id,
            string name,
            int order,
            int createdLevel,
            int augmentSlots,
            IEnumerable<CraftableItem> items)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "A crafting category requires an ID.",
                    nameof(id));
            }
            if (!string.Equals(id, id.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A crafting category ID must not contain surrounding whitespace.",
                    nameof(id));
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "A crafting category requires a display name.",
                    nameof(name));
            }
            if (!string.Equals(name, name.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A crafting category name must not contain surrounding whitespace.",
                    nameof(name));
            }
            if (order < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(order),
                    "A crafting category order must not be negative.");
            }
            if (createdLevel < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(createdLevel),
                    "Crafted items require a positive level.");
            }
            if (augmentSlots < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(augmentSlots),
                    "A crafting category augment-slot count must not be negative.");
            }
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var copiedItems = new List<CraftableItem>();
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CraftableItem item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException(
                        "Crafting category items must not contain null entries.",
                        nameof(items));
                }
                if (!itemIds.Add(item.Id))
                {
                    throw new ArgumentException(
                        "A crafting category must not repeat the same item.",
                        nameof(items));
                }
                copiedItems.Add(item);
            }
            if (copiedItems.Count == 0)
            {
                throw new ArgumentException(
                    "A crafting category requires at least one item.",
                    nameof(items));
            }

            Id = id;
            Name = name;
            Order = order;
            CreatedLevel = createdLevel;
            AugmentSlots = augmentSlots;
            this.items = new ReadOnlyCollection<CraftableItem>(copiedItems);
        }

        public string Id { get; }
        public string Name { get; }
        public int Order { get; }
        public int CreatedLevel { get; }
        public int AugmentSlots { get; }
        public IReadOnlyList<CraftableItem> Items { get { return items; } }
    }
}

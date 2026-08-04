using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.Domain.Crafting
{
    public sealed class Recipe
    {
        private readonly ReadOnlyCollection<Cost> costs;

        public Recipe(
            string itemId,
            Mark mark,
            int unlockLevel,
            IEnumerable<Cost> costs)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException(
                    "A recipe requires an exact item ID.",
                    nameof(itemId));
            }
            if (!string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A recipe item ID must not contain surrounding whitespace.",
                    nameof(itemId));
            }
            if (!Enum.IsDefined(typeof(Mark), mark))
            {
                throw new ArgumentOutOfRangeException(nameof(mark));
            }
            if (unlockLevel < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unlockLevel),
                    "A recipe unlock level must not be negative.");
            }
            if (costs == null)
            {
                throw new ArgumentNullException(nameof(costs));
            }

            var copiedCosts = new List<Cost>();
            var resources = new HashSet<string>(StringComparer.Ordinal);
            foreach (Cost cost in costs)
            {
                if (cost == null)
                {
                    throw new ArgumentException(
                        "Recipe costs must not contain null entries.",
                        nameof(costs));
                }
                if (!resources.Add(cost.ResourceId))
                {
                    throw new ArgumentException(
                        "A recipe must not repeat the same resource cost.",
                        nameof(costs));
                }
                copiedCosts.Add(cost);
            }
            if (copiedCosts.Count == 0)
            {
                throw new ArgumentException(
                    "A recipe requires at least one cost.",
                    nameof(costs));
            }

            ItemId = itemId;
            Mark = mark;
            UnlockLevel = unlockLevel;
            this.costs = new ReadOnlyCollection<Cost>(copiedCosts);
        }

        public string ItemId { get; }
        public Mark Mark { get; }
        public int UnlockLevel { get; }
        public IReadOnlyList<Cost> Costs { get { return costs; } }
    }
}

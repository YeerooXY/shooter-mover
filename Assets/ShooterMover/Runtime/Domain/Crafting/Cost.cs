using System;

namespace ShooterMover.Domain.Crafting
{
    public sealed class Cost
    {
        public Cost(string resourceId, long amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                throw new ArgumentException(
                    "A crafting cost requires a resource ID.",
                    nameof(resourceId));
            }
            if (!string.Equals(resourceId, resourceId.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A crafting resource ID must not contain surrounding whitespace.",
                    nameof(resourceId));
            }
            if (amount <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "A crafting cost must be positive.");
            }

            ResourceId = resourceId;
            Amount = amount;
        }

        public string ResourceId { get; }
        public long Amount { get; }
    }
}

using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Guns.Catalog
{
    public enum AugmentPriceStatus
    {
        Success = 1,
        InvalidLevel = 2,
        InvalidItemLevel = 3,
        InvalidQualityRank = 4,
        UnknownAugment = 5,
        Overflow = 6,
    }

    public static class GunAugmentPrices
    {
        /// <summary>
        /// Calculates one augment level. A purchase transaction must derive item level and
        /// quality rank from the exact owned equipment instance and its catalog definition;
        /// caller-provided economy inputs are not authoritative.
        /// </summary>
        public static bool TryGetLevelCost(
            int itemLevel,
            int qualityRank,
            StableId augmentId,
            int level,
            out long cost,
            out AugmentPriceStatus status)
        {
            cost = 0L;
            if (level < 1 || level > GunAugments.MaximumLevel)
            {
                status = AugmentPriceStatus.InvalidLevel;
                return false;
            }
            if (itemLevel < 1)
            {
                status = AugmentPriceStatus.InvalidItemLevel;
                return false;
            }
            if (qualityRank < 1)
            {
                status = AugmentPriceStatus.InvalidQualityRank;
                return false;
            }

            int weight;
            if (!TryGetWeight(augmentId, out weight))
            {
                status = AugmentPriceStatus.UnknownAugment;
                return false;
            }

            try
            {
                long current = checked((long)itemLevel * qualityRank * weight);
                for (int nextLevel = 2; nextLevel <= level; nextLevel++)
                {
                    current = checked((checked(current * 11L) + 9L) / 10L);
                }

                cost = current;
                status = AugmentPriceStatus.Success;
                return true;
            }
            catch (OverflowException)
            {
                status = AugmentPriceStatus.Overflow;
                return false;
            }
        }

        /// <summary>
        /// Sums the exact single-level prices for every purchased level. The authoritative
        /// transaction must source item level and quality rank from the owned equipment record.
        /// </summary>
        public static bool TryGetUpgradeCost(
            int itemLevel,
            int qualityRank,
            StableId augmentId,
            int currentLevel,
            int targetLevel,
            out long cost,
            out AugmentPriceStatus status)
        {
            cost = 0L;
            if (currentLevel < 0
                || targetLevel <= currentLevel
                || targetLevel > GunAugments.MaximumLevel)
            {
                status = AugmentPriceStatus.InvalidLevel;
                return false;
            }

            try
            {
                long total = 0L;
                for (int level = currentLevel + 1; level <= targetLevel; level++)
                {
                    long levelCost;
                    if (!TryGetLevelCost(
                            itemLevel,
                            qualityRank,
                            augmentId,
                            level,
                            out levelCost,
                            out status))
                    {
                        return false;
                    }
                    total = checked(total + levelCost);
                }

                cost = total;
                status = AugmentPriceStatus.Success;
                return true;
            }
            catch (OverflowException)
            {
                status = AugmentPriceStatus.Overflow;
                return false;
            }
        }

        private static bool TryGetWeight(StableId augmentId, out int weight)
        {
            if (augmentId == GunAugments.DamageId
                || augmentId == GunAugments.FireRateId)
            {
                weight = 10;
                return true;
            }
            if (augmentId == GunAugments.RicochetId)
            {
                weight = 20;
                return true;
            }

            weight = 0;
            return false;
        }
    }
}

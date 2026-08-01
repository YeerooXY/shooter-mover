using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Guns.Catalog
{
    public enum GunAugmentPriceStatus
    {
        Calculated = 1,
        InvalidInput = 2,
        UnknownAugment = 3,
        ArithmeticOverflow = 4,
    }

    /// <summary>
    /// Canonical first-pass gun augments. Definitions, combat modifiers, and pricing all use the
    /// same stable IDs so Shop, Inventory, persistence, and live combat do not invent parallel
    /// augment concepts.
    /// </summary>
    public static class GunAugmentCatalog
    {
        public const int MaximumLevel = 11;

        public static readonly StableId DamageId =
            StableId.Parse("augment.gun-damage");
        public static readonly StableId FireRateId =
            StableId.Parse("augment.gun-fire-rate");
        public static readonly StableId RicochetId =
            StableId.Parse("augment.gun-ricochet");

        private static readonly ReadOnlyCollection<AugmentDefinition>
            DefinitionsValue = new ReadOnlyCollection<AugmentDefinition>(
                new List<AugmentDefinition>
                {
                    Definition(
                        DamageId,
                        "damage",
                        "Damage"),
                    Definition(
                        FireRateId,
                        "fire-rate",
                        "Faster Shooting"),
                    Definition(
                        RicochetId,
                        "ricochet",
                        "Ricochet"),
                });

        public static IReadOnlyList<AugmentDefinition> Definitions
        {
            get { return DefinitionsValue; }
        }

        public static bool TryCreateModifierSet(
            EquipmentCatalog catalog,
            AugmentInstance instance,
            out GunAugmentModifierSet modifierSet,
            out string rejectionCode)
        {
            modifierSet = null;
            if (catalog == null
                || instance == null
                || instance.DefinitionId == null
                || instance.Level < 1
                || instance.Level > MaximumLevel)
            {
                rejectionCode = "gun-augment-input-invalid";
                return false;
            }

            AugmentDefinition definition = catalog.FindAugmentDefinition(
                instance.DefinitionId);
            if (definition == null)
            {
                rejectionCode = "gun-augment-definition-missing";
                return false;
            }

            GunStatModifier modifier;
            if (instance.DefinitionId == DamageId)
            {
                modifier = GunStatModifier.AdditivePercent(
                    GunEffectiveStat.DirectDamage,
                    0.10d * instance.Level);
            }
            else if (instance.DefinitionId == FireRateId)
            {
                modifier = GunStatModifier.AdditivePercent(
                    GunEffectiveStat.RateOfFire,
                    0.10d * instance.Level);
            }
            else if (instance.DefinitionId == RicochetId)
            {
                modifier = GunStatModifier.Flat(
                    GunEffectiveStat.RicochetTenths,
                    instance.Level);
            }
            else
            {
                rejectionCode = "gun-augment-definition-unsupported";
                return false;
            }

            modifierSet = GunAugmentModifierSet.Create(
                definition,
                instance,
                new[] { modifier });
            rejectionCode = string.Empty;
            return true;
        }

        /// <summary>
        /// Provisional deterministic economy rule. Item level is the current weapon-strength input,
        /// quality rank is rarity, Damage/Fire Rate use weight 10, and Ricochet uses weight 20.
        /// Every later level costs ceil(previous level cost * 1.1).
        /// </summary>
        public static GunAugmentPriceStatus TryCalculateLevelCost(
            int itemLevel,
            int qualityRank,
            StableId augmentDefinitionId,
            int level,
            out long cost)
        {
            cost = 0L;
            if (itemLevel < 1 || qualityRank < 1 || level < 1)
            {
                return GunAugmentPriceStatus.InvalidInput;
            }

            int typeWeight;
            if (!TryGetTypeWeight(augmentDefinitionId, out typeWeight))
            {
                return GunAugmentPriceStatus.UnknownAugment;
            }

            try
            {
                long current = checked(
                    checked((long)itemLevel * qualityRank)
                    * typeWeight);
                for (int currentLevel = 2;
                     currentLevel <= level;
                     currentLevel++)
                {
                    current = checked(
                        checked(current * 11L + 9L) / 10L);
                }
                if (current < 1L)
                {
                    return GunAugmentPriceStatus.ArithmeticOverflow;
                }
                cost = current;
                return GunAugmentPriceStatus.Calculated;
            }
            catch (OverflowException)
            {
                cost = 0L;
                return GunAugmentPriceStatus.ArithmeticOverflow;
            }
        }

        public static GunAugmentPriceStatus TryCalculateUpgradeCost(
            int itemLevel,
            int qualityRank,
            StableId augmentDefinitionId,
            int currentLevel,
            int targetLevel,
            out long cost)
        {
            cost = 0L;
            if (currentLevel < 0
                || targetLevel <= currentLevel
                || targetLevel > MaximumLevel)
            {
                return GunAugmentPriceStatus.InvalidInput;
            }

            try
            {
                long total = 0L;
                for (int level = currentLevel + 1;
                     level <= targetLevel;
                     level++)
                {
                    long levelCost;
                    GunAugmentPriceStatus status = TryCalculateLevelCost(
                        itemLevel,
                        qualityRank,
                        augmentDefinitionId,
                        level,
                        out levelCost);
                    if (status != GunAugmentPriceStatus.Calculated)
                    {
                        return status;
                    }
                    total = checked(total + levelCost);
                }
                cost = total;
                return GunAugmentPriceStatus.Calculated;
            }
            catch (OverflowException)
            {
                cost = 0L;
                return GunAugmentPriceStatus.ArithmeticOverflow;
            }
        }

        private static bool TryGetTypeWeight(
            StableId definitionId,
            out int weight)
        {
            if (definitionId == DamageId || definitionId == FireRateId)
            {
                weight = 10;
                return true;
            }
            if (definitionId == RicochetId)
            {
                weight = 20;
                return true;
            }
            weight = 0;
            return false;
        }

        private static AugmentDefinition Definition(
            StableId definitionId,
            string family,
            string displayName)
        {
            return AugmentDefinition.Create(
                definitionId,
                StableId.Create("augment-family", "gun-" + family),
                displayName,
                AugmentCompatibility.Create(
                    new[] { EquipmentCategoryIds.Gun },
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>()),
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(1, 1),
                InclusiveIntRange.Create(1, MaximumLevel));
        }
    }
}

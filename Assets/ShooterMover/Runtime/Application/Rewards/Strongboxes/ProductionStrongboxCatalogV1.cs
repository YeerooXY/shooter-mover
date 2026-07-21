using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// Authored V1 balance for the eleven normal strongbox tiers. Negative level
    /// offsets are intentional: early boxes may feel below the player's current
    /// power, while late boxes establish a visibly stronger reward expectation.
    /// Fresh strongbox equipment never materializes installed augments. The
    /// historical slot fields remain part of the versioned tier contract but are
    /// authored as zero; augment capacity is owned by EquipmentDefinition.
    /// </summary>
    public sealed class ProductionStrongboxTierV1
    {
        public ProductionStrongboxTierV1(
            int tierNumber,
            string slug,
            string displayName,
            int levelOffset,
            int itemLevelStandardDeviationMilli,
            int minimumAugmentSlots,
            int maximumAugmentSlots,
            int augmentSlotStandardDeviationMilli,
            long scrapMinimum,
            long scrapMaximum,
            ulong commonWeight,
            ulong rareWeight,
            ulong exceptionalWeight,
            long generationBias,
            long qualityBias,
            long exceptionalRollBias)
        {
            if (tierNumber < 1) throw new ArgumentOutOfRangeException(nameof(tierNumber));
            if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Tier slug is required.", nameof(slug));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Tier display name is required.", nameof(displayName));
            if (itemLevelStandardDeviationMilli <= 0) throw new ArgumentOutOfRangeException(nameof(itemLevelStandardDeviationMilli));
            if (minimumAugmentSlots < 0 || maximumAugmentSlots < minimumAugmentSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAugmentSlots));
            }
            if (augmentSlotStandardDeviationMilli < 0) throw new ArgumentOutOfRangeException(nameof(augmentSlotStandardDeviationMilli));
            if (scrapMinimum < 1L || scrapMaximum < scrapMinimum) throw new ArgumentOutOfRangeException(nameof(scrapMinimum));
            if (commonWeight + rareWeight + exceptionalWeight == 0UL) throw new ArgumentException("At least one quality weight is required.");
            if (generationBias < 1L) throw new ArgumentOutOfRangeException(nameof(generationBias));
            if (qualityBias < 1L) throw new ArgumentOutOfRangeException(nameof(qualityBias));
            if (exceptionalRollBias < 0L) throw new ArgumentOutOfRangeException(nameof(exceptionalRollBias));

            TierNumber = tierNumber;
            Slug = slug;
            DisplayName = displayName;
            TierStableId = StableId.Create("strongbox-tier", slug);
            LevelOffset = levelOffset;
            ItemLevelStandardDeviationMilli = itemLevelStandardDeviationMilli;
            MinimumAugmentSlots = minimumAugmentSlots;
            MaximumAugmentSlots = maximumAugmentSlots;
            AugmentSlotStandardDeviationMilli = augmentSlotStandardDeviationMilli;
            ScrapMinimum = scrapMinimum;
            ScrapMaximum = scrapMaximum;
            CommonWeight = commonWeight;
            RareWeight = rareWeight;
            ExceptionalWeight = exceptionalWeight;
            GenerationBias = generationBias;
            QualityBias = qualityBias;
            ExceptionalRollBias = exceptionalRollBias;
        }

        public int TierNumber { get; }
        public string Slug { get; }
        public string DisplayName { get; }
        public StableId TierStableId { get; }
        public int LevelOffset { get; }
        public int ItemLevelStandardDeviationMilli { get; }
        public int MinimumAugmentSlots { get; }
        public int MaximumAugmentSlots { get; }
        public int AugmentSlotStandardDeviationMilli { get; }
        public long ScrapMinimum { get; }
        public long ScrapMaximum { get; }
        public ulong CommonWeight { get; }
        public ulong RareWeight { get; }
        public ulong ExceptionalWeight { get; }
        public long GenerationBias { get; }
        public long QualityBias { get; }
        public long ExceptionalRollBias { get; }

        /// <summary>
        /// BOX V1 currently accepts a non-negative tier bonus. Negative authored
        /// offsets are applied to the immutable effective player context instead,
        /// yielding the same mean without changing the real player level.
        /// </summary>
        public int ResolveEffectivePlayerLevel(int playerLevel)
        {
            if (playerLevel < 0) throw new ArgumentOutOfRangeException(nameof(playerLevel));
            return Math.Max(0, checked(playerLevel + Math.Min(0, LevelOffset)));
        }

        public StrongboxPowerBudgetPolicyV1 CreatePowerBudgetPolicy()
        {
            return StrongboxPowerBudgetPolicyV1.Create(
                Math.Max(0, LevelOffset),
                ItemLevelStandardDeviationMilli,
                MinimumAugmentSlots,
                MaximumAugmentSlots,
                AugmentSlotStandardDeviationMilli);
        }

        public StrongboxDefinitionV1 CreateDefinition(StableId generationPolicyStableId)
        {
            if (generationPolicyStableId == null)
            {
                throw new ArgumentNullException(nameof(generationPolicyStableId));
            }

            RewardGrantSpecificationV1 equipment = RewardGrantSpecificationV1.CreateFixed(
                StableId.Create("strongbox-grant", Slug + "-equipment"),
                RewardGrantKindV1.EquipmentReference,
                StableId.Parse("equipment-category.weapon"),
                1L);
            RewardProfileV1 profile = RewardProfileV1.Create(
                StableId.Create("strongbox-profile", Slug),
                new[] { equipment },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());

            return StrongboxDefinitionV1.Create(
                TierStableId,
                TierNumber,
                GenerationBias,
                QualityBias,
                ExceptionalRollBias,
                StrongboxRewardCountPolicyV1.Create(2, 2),
                StrongboxMandatoryScrapPolicyV1.Create(
                    StableId.Parse("currency.scrap"),
                    ScrapMinimum,
                    ScrapMaximum),
                generationPolicyStableId,
                profile,
                StableId.Parse("scaling.source-tier"),
                StableId.Parse("scaling.exceptional"));
        }
    }

    public static class ProductionStrongboxCatalogV1
    {
        private static readonly ReadOnlyCollection<ProductionStrongboxTierV1> TiersValue =
            new ReadOnlyCollection<ProductionStrongboxTierV1>(
                new List<ProductionStrongboxTierV1>
                {
                    Tier(1, "steel", "Steel", -6, 5200, 0, 0, 0, 5, 10, 82, 17, 1, 80, 800, 0),
                    Tier(2, "copper", "Copper", -4, 5000, 0, 0, 0, 8, 16, 78, 20, 2, 90, 900, 5),
                    Tier(3, "silver", "Silver", -2, 4700, 0, 0, 0, 12, 24, 72, 25, 3, 100, 1000, 10),
                    Tier(4, "amethyst", "Amethyst", 0, 4400, 0, 0, 0, 18, 36, 64, 31, 5, 115, 1150, 20),
                    Tier(5, "gold", "Gold", 2, 4100, 0, 0, 0, 25, 50, 55, 37, 8, 130, 1300, 35),
                    Tier(6, "black-opal", "Black Opal", 4, 3800, 0, 0, 0, 35, 70, 46, 42, 12, 150, 1500, 55),
                    Tier(7, "blue-sapphire", "Blue Sapphire", 6, 3500, 0, 0, 0, 50, 100, 37, 46, 17, 175, 1750, 80),
                    Tier(8, "emerald", "Emerald", 8, 3200, 0, 0, 0, 70, 140, 29, 49, 22, 205, 2050, 115),
                    Tier(9, "alexandrite", "Alexandrite", 10, 2900, 0, 0, 0, 100, 200, 21, 50, 29, 240, 2400, 160),
                    Tier(10, "red-diamond", "Red Diamond", 12, 2600, 0, 0, 0, 150, 300, 14, 48, 38, 285, 2850, 225),
                    Tier(11, "antimatter", "Antimatter", 14, 2200, 0, 0, 0, 250, 500, 7, 40, 53, 350, 3500, 320),
                });

        public static IReadOnlyList<ProductionStrongboxTierV1> Tiers
        {
            get { return TiersValue; }
        }

        public static ProductionStrongboxTierV1 GetByNumber(int tierNumber)
        {
            if (tierNumber < 1 || tierNumber > TiersValue.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(tierNumber));
            }
            return TiersValue[tierNumber - 1];
        }

        public static bool TryGet(StableId tierStableId, out ProductionStrongboxTierV1 tier)
        {
            for (int index = 0; index < TiersValue.Count; index++)
            {
                if (TiersValue[index].TierStableId == tierStableId)
                {
                    tier = TiersValue[index];
                    return true;
                }
            }
            tier = null;
            return false;
        }

        private static ProductionStrongboxTierV1 Tier(
            int number,
            string slug,
            string name,
            int levelOffset,
            int itemDeviation,
            int minimumSlots,
            int maximumSlots,
            int slotDeviation,
            long scrapMinimum,
            long scrapMaximum,
            ulong commonWeight,
            ulong rareWeight,
            ulong exceptionalWeight,
            long generationBias,
            long qualityBias,
            long exceptionalBias)
        {
            return new ProductionStrongboxTierV1(
                number,
                slug,
                name,
                levelOffset,
                itemDeviation,
                minimumSlots,
                maximumSlots,
                slotDeviation,
                scrapMinimum,
                scrapMaximum,
                commonWeight,
                rareWeight,
                exceptionalWeight,
                generationBias,
                qualityBias,
                exceptionalBias);
        }
    }
}

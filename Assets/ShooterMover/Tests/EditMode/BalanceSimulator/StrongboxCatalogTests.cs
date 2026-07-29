using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed class StrongboxCatalogTests
    {
        [Test]
        public void CatalogDefinesExactlyTheElevenNormalTiersInOrder()
        {
            Assert.That(
                StrongboxCatalog.Tiers.Count,
                Is.EqualTo(11));
            string[] expected =
            {
                "Steel",
                "Copper",
                "Silver",
                "Amethyst",
                "Gold",
                "Black Opal",
                "Blue Sapphire",
                "Emerald",
                "Alexandrite",
                "Red Diamond",
                "Antimatter",
            };
            for (int index = 0; index < expected.Length; index++)
            {
                StrongboxTier tier =
                    StrongboxCatalog.Tiers[index];
                Assert.That(
                    tier.TierNumber,
                    Is.EqualTo(index + 1));
                Assert.That(
                    tier.DisplayName,
                    Is.EqualTo(expected[index]));
                Assert.That(
                    StrongboxCatalog.GetByNumber(
                        index + 1),
                    Is.SameAs(tier));
            }
        }

        [Test]
        public void LowerBoxesCanResolveBelowPlayerLevelAndHigherBoxesResolveAbove()
        {
            const int playerLevel = 30;
            Assert.That(
                StrongboxCatalog.GetByNumber(1)
                    .ResolveEffectivePlayerLevel(playerLevel),
                Is.EqualTo(24));
            Assert.That(
                StrongboxCatalog.GetByNumber(2)
                    .ResolveEffectivePlayerLevel(playerLevel),
                Is.EqualTo(26));
            Assert.That(
                StrongboxCatalog.GetByNumber(3)
                    .ResolveEffectivePlayerLevel(playerLevel),
                Is.EqualTo(28));

            StrongboxTier antimatter =
                StrongboxCatalog.GetByNumber(11);
            Assert.That(
                antimatter.ResolveEffectivePlayerLevel(playerLevel),
                Is.EqualTo(playerLevel));
            Assert.That(
                antimatter.CreatePowerBudgetPolicy()
                    .TierLevelBonus,
                Is.EqualTo(14));
        }

        [Test]
        public void TierPowerQualityAndScrapProgressMonotonically()
        {
            IReadOnlyList<StrongboxTier> tiers =
                StrongboxCatalog.Tiers;
            for (int index = 1; index < tiers.Count; index++)
            {
                Assert.That(
                    tiers[index].LevelOffset,
                    Is.GreaterThan(
                        tiers[index - 1].LevelOffset));
                Assert.That(
                    tiers[index].ScrapMinimum,
                    Is.GreaterThan(
                        tiers[index - 1].ScrapMinimum));
                Assert.That(
                    tiers[index].ExceptionalWeight,
                    Is.GreaterThan(
                        tiers[index - 1].ExceptionalWeight));
                Assert.That(
                    tiers[index].CommonWeight,
                    Is.LessThan(
                        tiers[index - 1].CommonWeight));
            }
        }

        [Test]
        public void EveryProductionTierGeneratesZeroInstalledAugments()
        {
            for (int index = 0;
                index < StrongboxCatalog.Tiers.Count;
                index++)
            {
                StrongboxTier tier =
                    StrongboxCatalog.Tiers[index];
                Assert.That(
                    tier.MinimumAugmentSlots,
                    Is.Zero,
                    tier.DisplayName);
                Assert.That(
                    tier.MaximumAugmentSlots,
                    Is.Zero,
                    tier.DisplayName);
                Assert.That(
                    tier.AugmentSlotStandardDeviationMilli,
                    Is.Zero,
                    tier.DisplayName);
                Assert.That(
                    tier.CreatePowerBudgetPolicy()
                        .MinimumAugmentSlots,
                    Is.Zero,
                    tier.DisplayName);
                Assert.That(
                    tier.CreatePowerBudgetPolicy()
                        .MaximumAugmentSlots,
                    Is.Zero,
                    tier.DisplayName);
            }
        }

        [Test]
        public void SalePlaceholderIsExplicitlyOneThousand()
        {
            Assert.That(
                LootboxSimulatorLive.TemporarySaleValue,
                Is.EqualTo(1000L));
        }
    }
}

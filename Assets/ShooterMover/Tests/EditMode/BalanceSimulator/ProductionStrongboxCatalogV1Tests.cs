using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed class ProductionStrongboxCatalogV1Tests
    {
        [Test]
        public void CatalogDefinesExactlyTheElevenNormalTiersInOrder()
        {
            Assert.That(ProductionStrongboxCatalogV1.Tiers.Count, Is.EqualTo(11));
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
                ProductionStrongboxTierV1 tier =
                    ProductionStrongboxCatalogV1.Tiers[index];
                Assert.That(tier.TierNumber, Is.EqualTo(index + 1));
                Assert.That(tier.DisplayName, Is.EqualTo(expected[index]));
                Assert.That(
                    ProductionStrongboxCatalogV1.GetByNumber(index + 1),
                    Is.SameAs(tier));
            }
        }

        [Test]
        public void LowerBoxesCanResolveBelowPlayerLevelAndHigherBoxesResolveAbove()
        {
            const int playerLevel = 30;
            Assert.That(
                ProductionStrongboxCatalogV1.GetByNumber(1)
                    .ResolveEffectivePlayerLevel(playerLevel),
                Is.EqualTo(24));
            Assert.That(
                ProductionStrongboxCatalogV1.GetByNumber(2)
                    .ResolveEffectivePlayerLevel(playerLevel),
                Is.EqualTo(26));
            Assert.That(
                ProductionStrongboxCatalogV1.GetByNumber(3)
                    .ResolveEffectivePlayerLevel(playerLevel),
                Is.EqualTo(28));

            ProductionStrongboxTierV1 antimatter =
                ProductionStrongboxCatalogV1.GetByNumber(11);
            Assert.That(antimatter.ResolveEffectivePlayerLevel(playerLevel), Is.EqualTo(playerLevel));
            Assert.That(antimatter.CreatePowerBudgetPolicy().TierLevelBonus, Is.EqualTo(14));
        }

        [Test]
        public void TierPowerQualityAndScrapProgressMonotonically()
        {
            IReadOnlyList<ProductionStrongboxTierV1> tiers =
                ProductionStrongboxCatalogV1.Tiers;
            for (int index = 1; index < tiers.Count; index++)
            {
                Assert.That(tiers[index].LevelOffset, Is.GreaterThan(tiers[index - 1].LevelOffset));
                Assert.That(tiers[index].ScrapMinimum, Is.GreaterThan(tiers[index - 1].ScrapMinimum));
                Assert.That(tiers[index].ExceptionalWeight, Is.GreaterThan(tiers[index - 1].ExceptionalWeight));
                Assert.That(tiers[index].CommonWeight, Is.LessThan(tiers[index - 1].CommonWeight));
            }
        }

        [Test]
        public void LateTierSlotPoliciesMakeTheUpgradeVisible()
        {
            Assert.That(ProductionStrongboxCatalogV1.GetByNumber(1).MinimumAugmentSlots, Is.EqualTo(1));
            Assert.That(ProductionStrongboxCatalogV1.GetByNumber(4).MaximumAugmentSlots, Is.EqualTo(3));
            Assert.That(ProductionStrongboxCatalogV1.GetByNumber(8).MinimumAugmentSlots, Is.EqualTo(2));
            Assert.That(ProductionStrongboxCatalogV1.GetByNumber(11).MinimumAugmentSlots, Is.EqualTo(3));
            Assert.That(ProductionStrongboxCatalogV1.GetByNumber(11).MaximumAugmentSlots, Is.EqualTo(3));
        }

        [Test]
        public void SalePlaceholderIsExplicitlyOneThousand()
        {
            Assert.That(LootboxSimulatorRuntimeV1.TemporarySaleValue, Is.EqualTo(1000L));
        }
    }
}

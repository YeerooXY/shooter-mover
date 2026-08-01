using NUnit.Framework;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Tests.EditMode.Guns
{
    public sealed class GunAugmentCatalogTests
    {
        [Test]
        public void ProductionCatalogPublishesThreeGunAugments()
        {
            EquipmentCatalog catalog = GunCatalogProvider.EquipmentCatalog;

            Assert.That(
                catalog.FindAugmentDefinition(GunAugmentCatalog.DamageId),
                Is.Not.Null);
            Assert.That(
                catalog.FindAugmentDefinition(GunAugmentCatalog.FireRateId),
                Is.Not.Null);
            Assert.That(
                catalog.FindAugmentDefinition(GunAugmentCatalog.RicochetId),
                Is.Not.Null);

            AugmentDefinition damage = catalog.FindAugmentDefinition(
                GunAugmentCatalog.DamageId);
            Assert.That(
                damage.DuplicatePolicy,
                Is.EqualTo(AugmentDuplicatePolicy.DisallowSameDefinition));
            Assert.That(damage.LevelRange.Maximum, Is.EqualTo(11));
        }

        [Test]
        public void DamageAndFireRateUseTenPercentPerLevel()
        {
            EquipmentCatalog catalog = GunCatalogProvider.EquipmentCatalog;
            GunAugmentModifierSet damage = Resolve(
                catalog,
                GunAugmentCatalog.DamageId,
                3);
            GunAugmentModifierSet fireRate = Resolve(
                catalog,
                GunAugmentCatalog.FireRateId,
                4);

            Assert.That(
                damage.Modifiers[0].Stat,
                Is.EqualTo(GunEffectiveStat.DirectDamage));
            Assert.That(
                damage.Modifiers[0].Operation,
                Is.EqualTo(GunModifierOperation.AdditivePercentage));
            Assert.That(damage.Modifiers[0].Value, Is.EqualTo(0.30d).Within(0.000000001d));

            Assert.That(
                fireRate.Modifiers[0].Stat,
                Is.EqualTo(GunEffectiveStat.RateOfFire));
            Assert.That(
                fireRate.Modifiers[0].Operation,
                Is.EqualTo(GunModifierOperation.AdditivePercentage));
            Assert.That(fireRate.Modifiers[0].Value, Is.EqualTo(0.40d).Within(0.000000001d));
        }

        [Test]
        public void RicochetAddsOneFixedPointTenthPerLevel()
        {
            GunAugmentModifierSet ricochet = Resolve(
                GunCatalogProvider.EquipmentCatalog,
                GunAugmentCatalog.RicochetId,
                5);

            Assert.That(
                ricochet.Modifiers[0].Stat,
                Is.EqualTo(GunEffectiveStat.RicochetTenths));
            Assert.That(
                ricochet.Modifiers[0].Operation,
                Is.EqualTo(GunModifierOperation.FlatAddition));
            Assert.That(ricochet.Modifiers[0].Value, Is.EqualTo(5d));
        }

        [Test]
        public void PriceUsesStrengthRarityTypeAndOnePointOneGrowth()
        {
            long damageOne;
            long damageTwo;
            long damageThree;
            long ricochetOne;

            Assert.That(
                GunAugmentCatalog.TryCalculateLevelCost(
                    15,
                    3,
                    GunAugmentCatalog.DamageId,
                    1,
                    out damageOne),
                Is.EqualTo(GunAugmentPriceStatus.Calculated));
            Assert.That(
                GunAugmentCatalog.TryCalculateLevelCost(
                    15,
                    3,
                    GunAugmentCatalog.DamageId,
                    2,
                    out damageTwo),
                Is.EqualTo(GunAugmentPriceStatus.Calculated));
            Assert.That(
                GunAugmentCatalog.TryCalculateLevelCost(
                    15,
                    3,
                    GunAugmentCatalog.DamageId,
                    3,
                    out damageThree),
                Is.EqualTo(GunAugmentPriceStatus.Calculated));
            Assert.That(
                GunAugmentCatalog.TryCalculateLevelCost(
                    15,
                    3,
                    GunAugmentCatalog.RicochetId,
                    1,
                    out ricochetOne),
                Is.EqualTo(GunAugmentPriceStatus.Calculated));

            Assert.That(damageOne, Is.EqualTo(450L));
            Assert.That(damageTwo, Is.EqualTo(495L));
            Assert.That(damageThree, Is.EqualTo(545L));
            Assert.That(ricochetOne, Is.EqualTo(900L));
        }

        [Test]
        public void UpgradePriceSumsEveryPurchasedLevel()
        {
            long total;
            GunAugmentPriceStatus status =
                GunAugmentCatalog.TryCalculateUpgradeCost(
                    15,
                    3,
                    GunAugmentCatalog.DamageId,
                    0,
                    3,
                    out total);

            Assert.That(status, Is.EqualTo(GunAugmentPriceStatus.Calculated));
            Assert.That(total, Is.EqualTo(1490L));
        }

        private static GunAugmentModifierSet Resolve(
            EquipmentCatalog catalog,
            StableId definitionId,
            int level)
        {
            var instance = AugmentInstance.Create(
                StableId.Create(
                    "augment-instance",
                    definitionId.ToString().Replace('.', '-')
                    + "-level-" + level),
                definitionId,
                1,
                level);
            GunAugmentModifierSet result;
            string rejectionCode;
            Assert.That(
                GunAugmentCatalog.TryCreateModifierSet(
                    catalog,
                    instance,
                    out result,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(result, Is.Not.Null);
            return result;
        }
    }
}

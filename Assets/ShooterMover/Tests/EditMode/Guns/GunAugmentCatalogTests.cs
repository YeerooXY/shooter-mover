using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Guns.Live;

namespace ShooterMover.Tests.EditMode.Guns
{
    public sealed class GunAugmentTests
    {
        [Test]
        public void ProductionCatalogPublishesThreeGunAugments()
        {
            EquipmentCatalog catalog = GunCatalogProvider.EquipmentCatalog;

            Assert.That(catalog.FindAugmentDefinition(GunAugments.DamageId), Is.Not.Null);
            Assert.That(catalog.FindAugmentDefinition(GunAugments.FireRateId), Is.Not.Null);
            Assert.That(catalog.FindAugmentDefinition(GunAugments.RicochetId), Is.Not.Null);

            AugmentDefinition fireRate = catalog.FindAugmentDefinition(
                GunAugments.FireRateId);
            Assert.That(fireRate.DisplayName, Is.EqualTo("Fire Rate"));
            Assert.That(
                fireRate.DuplicatePolicy,
                Is.EqualTo(AugmentDuplicatePolicy.DisallowSameDefinition));
            Assert.That(fireRate.LevelRange.Maximum, Is.EqualTo(11));
        }

        [Test]
        public void DamageLevelThreeAddsExactlyThirtyPercentThroughFactory()
        {
            GunFixture fixture = ProductionFixture();
            EffectiveGun gun = Resolve(
                fixture,
                Augment(GunAugments.DamageId, 3, "damage-three"));

            Assert.That(
                gun.Damage.DirectDamage,
                Is.EqualTo(fixture.Blueprint.Damage.DirectDamage * 1.30d)
                    .Within(0.000000001d));
        }

        [Test]
        public void FireRateLevelFourAddsExactlyFortyPercentThroughFactory()
        {
            GunFixture fixture = ProductionFixture();
            EffectiveGun gun = Resolve(
                fixture,
                Augment(GunAugments.FireRateId, 4, "fire-four"));

            Assert.That(
                gun.FireSettings.RateOfFire,
                Is.EqualTo(fixture.Blueprint.FireSettings.RateOfFire * 1.40d)
                    .Within(0.000000001d));
        }

        [Test]
        public void RicochetLevelFiveAddsFiveTenthsAndRebuildsImpact()
        {
            GunFixture fixture = RicochetFixture(2);
            EffectiveGun gun = Resolve(
                fixture,
                Augment(GunAugments.RicochetId, 5, "ricochet-five"));

            Assert.That(gun.EffectiveRicochet.Tenths, Is.EqualTo(7));
            Assert.That(
                gun.EffectiveRicochet.Tenths
                    - fixture.Blueprint.Impact.Ricochet.FixedPointBudget.Value.Tenths,
                Is.EqualTo(5));
            Assert.That(gun.Impact.Ricochet, Is.Not.Null);
            Assert.That(gun.Impact.Ricochet.FixedPointBudget.HasValue, Is.True);
            Assert.That(
                gun.Impact.Ricochet.FixedPointBudget.Value,
                Is.EqualTo(gun.EffectiveRicochet));
        }

        [TestCase(-1d)]
        [TestCase(0.5d)]
        public void RicochetRejectsNonWholeOrNegativeAdditions(double value)
        {
            GunFixture fixture = RicochetFixture(2);
            AugmentInstance augment = Augment(
                GunAugments.RicochetId,
                1,
                "invalid-ricochet");
            EquipmentInstance equipment = CreateEquipment(fixture, augment);
            AugmentDefinition definition = fixture.Catalog.FindAugmentDefinition(
                GunAugments.RicochetId);
            GunAugmentModifierSet modifierSet = GunAugmentModifierSet.Create(
                definition,
                augment,
                new[]
                {
                    GunStatModifier.Flat(
                        GunEffectiveStat.RicochetTenths,
                        value),
                });

            Assert.Throws<IncompatibleGunAugmentException>(delegate
            {
                EffectiveGunFactory.Create(
                    fixture.Blueprint,
                    fixture.Catalog,
                    equipment,
                    new[] { modifierSet });
            });
        }

        [Test]
        public void DamageAndFireRateStackAdditivelyOnDifferentStats()
        {
            GunFixture fixture = ProductionFixture();
            EffectiveGun gun = Resolve(
                fixture,
                Augment(GunAugments.DamageId, 3, "damage-stack"),
                Augment(GunAugments.FireRateId, 4, "fire-stack"));

            Assert.That(
                gun.Damage.DirectDamage,
                Is.EqualTo(fixture.Blueprint.Damage.DirectDamage * 1.30d)
                    .Within(0.000000001d));
            Assert.That(
                gun.FireSettings.RateOfFire,
                Is.EqualTo(fixture.Blueprint.FireSettings.RateOfFire * 1.40d)
                    .Within(0.000000001d));
        }

        [Test]
        public void FireRateIsRejectedOnContinuousFire()
        {
            GunFixture fixture = ContinuousFixture();

            Assert.Throws<IncompatibleGunAugmentException>(delegate
            {
                Resolve(
                    fixture,
                    Augment(GunAugments.FireRateId, 1, "continuous-fire"));
            });
        }

        [Test]
        public void RicochetIsRejectedWithoutRicochetStructure()
        {
            GunFixture fixture = ProductionFixture();

            Assert.Throws<IncompatibleGunAugmentException>(delegate
            {
                Resolve(
                    fixture,
                    Augment(GunAugments.RicochetId, 1, "unsupported-ricochet"));
            });
        }

        [Test]
        public void UnknownInstalledAugmentFailsClosed()
        {
            StableId unknownId = StableId.Parse("augment.gun-unknown");
            GunFixture fixture = ProductionFixture(UnknownDefinition(unknownId));
            EquipmentInstance equipment = CreateEquipment(
                fixture,
                Augment(unknownId, 1, "unknown"));
            IReadOnlyList<GunAugmentModifierSet> ignored;
            string rejectionCode;

            bool resolved = new GunAugmentResolver().TryResolve(
                equipment,
                fixture.Catalog,
                out ignored,
                out rejectionCode);

            Assert.That(resolved, Is.False);
            Assert.That(ignored, Is.Null);
            Assert.That(
                rejectionCode,
                Is.EqualTo("gun-augment-definition-unsupported"));
        }

        [Test]
        public void DuplicateAugmentDefinitionsRemainDisallowed()
        {
            GunFixture fixture = ProductionFixture();
            var definitions = new List<AugmentDefinition>(GunAugments.Definitions)
            {
                GunAugments.Definitions[0],
            };

            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { fixture.Definition },
                definitions);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Catalog, Is.Null);
        }

        [Test]
        public void LevelElevenCanBePricedAndLevelTwelveIsRejected()
        {
            long levelEleven;
            AugmentPriceStatus status;
            Assert.That(
                GunAugmentPrices.TryGetLevelCost(
                    15,
                    3,
                    GunAugments.DamageId,
                    11,
                    out levelEleven,
                    out status),
                Is.True);
            Assert.That(status, Is.EqualTo(AugmentPriceStatus.Success));
            Assert.That(levelEleven, Is.GreaterThan(0L));

            long levelTwelve;
            Assert.That(
                GunAugmentPrices.TryGetLevelCost(
                    15,
                    3,
                    GunAugments.DamageId,
                    12,
                    out levelTwelve,
                    out status),
                Is.False);
            Assert.That(status, Is.EqualTo(AugmentPriceStatus.InvalidLevel));
            Assert.That(levelTwelve, Is.Zero);
        }

        [Test]
        public void UpgradePriceEqualsTheSumOfPurchasedLevels()
        {
            long expected = 0L;
            AugmentPriceStatus status;
            for (int level = 1; level <= 3; level++)
            {
                long levelCost;
                Assert.That(
                    GunAugmentPrices.TryGetLevelCost(
                        15,
                        3,
                        GunAugments.DamageId,
                        level,
                        out levelCost,
                        out status),
                    Is.True);
                expected += levelCost;
            }

            long total;
            Assert.That(
                GunAugmentPrices.TryGetUpgradeCost(
                    15,
                    3,
                    GunAugments.DamageId,
                    0,
                    3,
                    out total,
                    out status),
                Is.True);
            Assert.That(status, Is.EqualTo(AugmentPriceStatus.Success));
            Assert.That(total, Is.EqualTo(expected));
            Assert.That(total, Is.EqualTo(1490L));
        }

        [TestCase(0, 3, AugmentPriceStatus.InvalidItemLevel)]
        [TestCase(15, 0, AugmentPriceStatus.InvalidQualityRank)]
        public void PricingRejectsInvalidEquipmentInputs(
            int itemLevel,
            int qualityRank,
            AugmentPriceStatus expected)
        {
            long cost;
            AugmentPriceStatus status;

            Assert.That(
                GunAugmentPrices.TryGetLevelCost(
                    itemLevel,
                    qualityRank,
                    GunAugments.DamageId,
                    1,
                    out cost,
                    out status),
                Is.False);
            Assert.That(status, Is.EqualTo(expected));
            Assert.That(cost, Is.Zero);
        }

        [Test]
        public void PricingRejectsUnknownAugmentsAndOverflow()
        {
            long cost;
            AugmentPriceStatus status;

            Assert.That(
                GunAugmentPrices.TryGetLevelCost(
                    15,
                    3,
                    StableId.Parse("augment.gun-missing"),
                    1,
                    out cost,
                    out status),
                Is.False);
            Assert.That(status, Is.EqualTo(AugmentPriceStatus.UnknownAugment));

            Assert.That(
                GunAugmentPrices.TryGetLevelCost(
                    int.MaxValue,
                    int.MaxValue,
                    GunAugments.RicochetId,
                    1,
                    out cost,
                    out status),
                Is.False);
            Assert.That(status, Is.EqualTo(AugmentPriceStatus.Overflow));
        }

        private static EffectiveGun Resolve(
            GunFixture fixture,
            params AugmentInstance[] augments)
        {
            EquipmentInstance equipment = CreateEquipment(fixture, augments);
            IReadOnlyList<GunAugmentModifierSet> modifierSets;
            string rejectionCode;
            Assert.That(
                new GunAugmentResolver().TryResolve(
                    equipment,
                    fixture.Catalog,
                    out modifierSets,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(modifierSets, Is.Not.Null);

            return EffectiveGunFactory.Create(
                fixture.Blueprint,
                fixture.Catalog,
                equipment,
                modifierSets);
        }

        private static EquipmentInstance CreateEquipment(
            GunFixture fixture,
            params AugmentInstance[] augments)
        {
            return EquipmentInstance.Create(
                StableId.Create(
                    "equipment-instance",
                    fixture.Blueprint.DefinitionId.Value.Replace('.', '-')),
                fixture.Definition.DefinitionId,
                fixture.Definition.ItemLevelRange.Minimum,
                fixture.Definition.QualityTiers[0].QualityId,
                augments);
        }

        private static AugmentInstance Augment(
            StableId augmentId,
            int level,
            string suffix)
        {
            return AugmentInstance.Create(
                StableId.Create("augment-instance", suffix),
                augmentId,
                1,
                level);
        }

        private static GunFixture ProductionFixture(
            params AugmentDefinition[] extraDefinitions)
        {
            GunFamily family = FindFamily("baller");
            Gun blueprint = family.Marks[0].Blueprint;
            EquipmentDefinition definition =
                GunCatalogProvider.EquipmentCatalog.FindEquipmentDefinition(
                    family.Marks[0].EquipmentDefinitionId);
            Assert.That(definition, Is.Not.Null);

            if (extraDefinitions == null || extraDefinitions.Length == 0)
            {
                return new GunFixture(
                    blueprint,
                    GunCatalogProvider.EquipmentCatalog,
                    definition);
            }

            var augments = new List<AugmentDefinition>(GunAugments.Definitions);
            augments.AddRange(extraDefinitions);
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { definition },
                augments);
            Assert.That(result.IsValid, Is.True);
            return new GunFixture(blueprint, result.Catalog, definition);
        }

        private static GunFixture RicochetFixture(int authoredTenths)
        {
            Gun source = FindFamily("baller").Marks[0].Blueprint;
            GunImpactSpec impact = GunImpactSpec.Create(
                true,
                true,
                true,
                true,
                new GunRicochetSpec(
                    new RicochetValue(authoredTenths),
                    0.8d,
                    0d,
                    0d),
                null);
            Gun blueprint = Gun.Create(
                new GunDefinitionId("ricochet-test.mk1"),
                "Ricochet Test Gun",
                "test",
                source.FireSettings,
                source.ShotPattern,
                source.Projectile,
                source.Guidance,
                impact,
                source.Damage,
                source.Effects,
                "drop.test.ricochet",
                "art.test.ricochet");
            return SyntheticFixture(blueprint);
        }

        private static GunFixture ContinuousFixture()
        {
            Gun blueprint = Gun.Create(
                new GunDefinitionId("continuous-test.mk1"),
                "Continuous Test Gun",
                "test",
                FireSettings.Create(
                    GunFireMode.Continuous,
                    0d,
                    0,
                    0,
                    0d,
                    0d,
                    5d),
                GunShotPattern.Create(
                    GunShotPatternKind.Beam,
                    0,
                    0d,
                    0d,
                    1,
                    0d),
                null,
                GunGuidanceSpec.Unguided(),
                GunImpactSpec.Create(true, true, true, true, null, null),
                GunDamageSpec.Create(
                    GunDamageCategory.Energy,
                    1d,
                    0d,
                    0d,
                    0d,
                    0d),
                GunEffects.None(),
                "drop.test.continuous",
                "art.test.continuous");
            return SyntheticFixture(blueprint);
        }

        private static GunFixture SyntheticFixture(Gun blueprint)
        {
            EquipmentQualityTier quality = EquipmentQualityTier.Create(
                StableId.Create("equipment-quality", "test"),
                "Test",
                1);
            EquipmentDefinition definition = EquipmentDefinition.Create(
                StableId.Create(
                    "equipment",
                    blueprint.DefinitionId.Value.Replace('.', '-')),
                EquipmentCategoryIds.Gun,
                StableId.Create("gun-family", "test"),
                blueprint.DisplayName,
                blueprint.DefinitionId.ToRuntimeReference(),
                InclusiveIntRange.Create(1, 100),
                4,
                new[] { quality },
                Array.Empty<StableId>());
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { definition },
                GunAugments.Definitions);
            Assert.That(result.IsValid, Is.True);
            return new GunFixture(blueprint, result.Catalog, definition);
        }

        private static AugmentDefinition UnknownDefinition(StableId augmentId)
        {
            return AugmentDefinition.Create(
                augmentId,
                StableId.Create("augment-family", "gun-unknown"),
                "Unknown",
                AugmentCompatibility.Create(
                    new[] { EquipmentCategoryIds.Gun },
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>()),
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(1, 1),
                InclusiveIntRange.Create(1, GunAugments.MaximumLevel));
        }

        private static GunFamily FindFamily(string familyId)
        {
            for (int index = 0;
                 index < GunCatalogProvider.Current.Families.Count;
                 index++)
            {
                GunFamily family = GunCatalogProvider.Current.Families[index];
                if (family.FamilyId == familyId)
                {
                    return family;
                }
            }

            Assert.Fail("Missing gun family: " + familyId);
            return null;
        }

        private sealed class GunFixture
        {
            public GunFixture(
                Gun blueprint,
                EquipmentCatalog catalog,
                EquipmentDefinition definition)
            {
                Blueprint = blueprint;
                Catalog = catalog;
                Definition = definition;
            }

            public Gun Blueprint { get; }
            public EquipmentCatalog Catalog { get; }
            public EquipmentDefinition Definition { get; }
        }
    }
}

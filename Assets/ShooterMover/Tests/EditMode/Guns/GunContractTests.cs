using System;
using NUnit.Framework;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Tests.EditMode.Guns
{
    public sealed class GunContractTests
    {
        [Test]
        public void BurstShotgun_KeepsBurstShotsSeparateFromProjectilesPerShot()
        {
            FireSettings fire = FireSettings.Create(
                GunFireMode.Burst,
                2d,
                1,
                3,
                0.1d,
                0.5d,
                0d);
            GunShotPattern pattern = GunShotPattern.Create(
                GunShotPatternKind.Spread,
                8,
                35d,
                2d,
                1,
                0d);

            Gun blueprint = Gun.Create(
                new GunDefinitionId("gun.test-burst-shotgun"),
                "Test Burst Shotgun",
                "shotgun",
                fire,
                pattern,
                ProjectileSettings.Create(
                    GunProjectileKind.RegularProjectile,
                    20d,
                    15d,
                    new PierceValue(0),
                    GunProjectileTerminationBehavior.StopOnFirstBlockingImpact),
                GunGuidanceSpec.Unguided(),
                GunImpactSpec.Create(true, true, true, true, null, null),
                GunDamageSpec.Create(
                    GunDamageCategory.Physical,
                    10d,
                    0d,
                    0d,
                    0d,
                    0d),
                GunEffects.None(),
                "gun-drop.test-burst-shotgun",
                "gun-art.test-burst-shotgun");

            Assert.That(blueprint.FireSettings.ShotsPerBurst, Is.EqualTo(3));
            Assert.That(blueprint.ShotPattern.ProjectilesPerShot, Is.EqualTo(8));
        }

        [Test]
        public void ContinuousFire_RequiresExplicitTickRateAndZeroProjectileFields()
        {
            Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                FireSettings.Create(
                    GunFireMode.Continuous,
                    0d,
                    0,
                    0,
                    0d,
                    0d,
                    0d);
            });

            Assert.Throws<ArgumentException>(delegate
            {
                FireSettings.Create(
                    GunFireMode.Continuous,
                    1d,
                    1,
                    1,
                    0d,
                    0d,
                    10d);
            });
        }

        [Test]
        public void Spread_RequiresExplicitProjectileCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                GunShotPattern.Create(
                    GunShotPatternKind.Spread,
                    0,
                    20d,
                    0d,
                    1,
                    0d);
            });
        }

        [Test]
        public void HomingWithoutProjectile_IsRejected()
        {
            Assert.Throws<ArgumentException>(delegate
            {
                Gun.Create(
                    new GunDefinitionId("gun.invalid-homing-beam"),
                    "Invalid Homing Beam",
                    "beam",
                    FireSettings.Create(
                        GunFireMode.Continuous,
                        0d,
                        0,
                        0,
                        0d,
                        0d,
                        10d),
                    GunShotPattern.Create(
                        GunShotPatternKind.Beam,
                        0,
                        0d,
                        0d,
                        1,
                        0d),
                    null,
                    GunGuidanceSpec.Homing(
                        10d,
                        90d,
                        0d,
                        GunTargetPolicy.ClosestToAim,
                        GunReacquisitionMode.None),
                    GunImpactSpec.Create(false, false, true, true, null, null),
                    GunDamageSpec.Create(
                        GunDamageCategory.Energy,
                        1d,
                        0d,
                        0d,
                        0d,
                        0d),
                    GunEffects.None(),
                    "gun-drop.invalid-homing-beam",
                    "gun-art.invalid-homing-beam");
            });
        }

        [Test]
        public void RicochetWithoutWallImpact_IsRejected()
        {
            Assert.Throws<ArgumentException>(delegate
            {
                GunImpactSpec.Create(
                    true,
                    false,
                    true,
                    true,
                    new GunRicochetSpec(1, 0.8d, 0d),
                    null);
            });
        }

        [Test]
        public void ExplosionTriggerWithoutExplosionEffect_IsRejected()
        {
            Assert.Throws<ArgumentException>(delegate
            {
                CreateSingleProjectileBlueprint(
                    GunImpactSpec.Create(
                        true,
                        true,
                        true,
                        true,
                        null,
                        new GunExplosionTriggerSpec(true, false, false, false)),
                    GunDamageSpec.Create(
                        GunDamageCategory.Thermal,
                        10d,
                        0d,
                        0d,
                        0d,
                        0d),
                    GunEffects.None());
            });
        }

        [Test]
        public void DamageOverTimeDataWithoutEffect_IsRejected()
        {
            Assert.Throws<ArgumentException>(delegate
            {
                CreateSingleProjectileBlueprint(
                    GunImpactSpec.Create(true, true, true, true, null, null),
                    GunDamageSpec.Create(
                        GunDamageCategory.Chemical,
                        1d,
                        0d,
                        4d,
                        3d,
                        0d),
                    GunEffects.None());
            });
        }

        [Test]
        public void DamageCategoryConversion_DoesNotReinterpretUnknownStrings()
        {
            GunDamageCategory category;
            Assert.That(
                GunDamageCategoryConversion.TryFromCatalogValue(
                    "Thermal",
                    out category),
                Is.True);
            Assert.That(category, Is.EqualTo(GunDamageCategory.Thermal));
            Assert.That(
                GunDamageCategoryConversion.TryFromCatalogValue(
                    "Fire",
                    out category),
                Is.False);
            Assert.Throws<FormatException>(delegate
            {
                GunDamageCategoryConversion.FromCatalogValue("Fire");
            });
        }

        [Test]
        public void PierceValue_ExposesFractionAndProtectsLegacyIntegerBoundary()
        {
            var fractional = new PierceValue(15);
            int legacy;
            Assert.That(fractional.GuaranteedHits, Is.EqualTo(1));
            Assert.That(fractional.FractionalAdditionalHitChance, Is.EqualTo(0.5d));
            Assert.That(fractional.TryToLegacyInteger(out legacy), Is.False);

            PierceValue exact = PierceValue.FromLegacyInteger(2);
            Assert.That(exact.Tenths, Is.EqualTo(20));
            Assert.That(exact.TryToLegacyInteger(out legacy), Is.True);
            Assert.That(legacy, Is.EqualTo(2));
        }

        private static Gun CreateSingleProjectileBlueprint(
            GunImpactSpec impact,
            GunDamageSpec damage,
            GunEffects effects)
        {
            return Gun.Create(
                new GunDefinitionId("gun.test-single"),
                "Test Single",
                "test-family",
                FireSettings.Create(
                    GunFireMode.SemiAutomatic,
                    2d,
                    1,
                    1,
                    0d,
                    0d,
                    0d),
                GunShotPattern.Create(
                    GunShotPatternKind.Single,
                    1,
                    0d,
                    0d,
                    1,
                    0d),
                ProjectileSettings.Create(
                    GunProjectileKind.RegularProjectile,
                    20d,
                    15d,
                    new PierceValue(0),
                    GunProjectileTerminationBehavior.StopOnFirstBlockingImpact),
                GunGuidanceSpec.Unguided(),
                impact,
                damage,
                effects,
                "gun-drop.test-single",
                "gun-art.test-single");
        }
    }
}

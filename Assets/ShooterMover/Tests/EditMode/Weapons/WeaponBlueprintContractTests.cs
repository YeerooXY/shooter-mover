using System;
using NUnit.Framework;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Tests.EditMode.Weapons
{
    public sealed class WeaponBlueprintContractTests
    {
        [Test]
        public void BurstShotgun_KeepsBurstShotsSeparateFromProjectilesPerShot()
        {
            WeaponFireSettings fire = WeaponFireSettings.Create(
                WeaponFireMode.Burst,
                2d,
                1,
                3,
                0.1d,
                0.5d,
                0d);
            WeaponShotPattern pattern = WeaponShotPattern.Create(
                WeaponShotPatternKind.Spread,
                8,
                35d,
                2d,
                1,
                0d);

            WeaponBlueprint blueprint = WeaponBlueprint.Create(
                new WeaponDefinitionId("weapon.test-burst-shotgun"),
                "Test Burst Shotgun",
                "shotgun",
                fire,
                pattern,
                WeaponProjectileSpec.Create(
                    WeaponProjectileKind.RegularProjectile,
                    20d,
                    15d,
                    new PierceValue(0),
                    WeaponProjectileTerminationBehavior.StopOnFirstBlockingImpact),
                WeaponGuidanceSpec.Unguided(),
                WeaponImpactSpec.Create(true, true, true, true, null, null),
                WeaponDamageSpec.Create(
                    WeaponDamageCategory.Physical,
                    10d,
                    0d,
                    0d,
                    0d,
                    0d),
                WeaponEffects.None(),
                "weapon-drop.test-burst-shotgun",
                "weapon-art.test-burst-shotgun");

            Assert.That(blueprint.FireSettings.ShotsPerBurst, Is.EqualTo(3));
            Assert.That(blueprint.ShotPattern.ProjectilesPerShot, Is.EqualTo(8));
        }

        [Test]
        public void ContinuousFire_RequiresExplicitTickRateAndZeroProjectileFields()
        {
            Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                WeaponFireSettings.Create(
                    WeaponFireMode.Continuous,
                    0d,
                    0,
                    0,
                    0d,
                    0d,
                    0d);
            });

            Assert.Throws<ArgumentException>(delegate
            {
                WeaponFireSettings.Create(
                    WeaponFireMode.Continuous,
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
                WeaponShotPattern.Create(
                    WeaponShotPatternKind.Spread,
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
                WeaponBlueprint.Create(
                    new WeaponDefinitionId("weapon.invalid-homing-beam"),
                    "Invalid Homing Beam",
                    "beam",
                    WeaponFireSettings.Create(
                        WeaponFireMode.Continuous,
                        0d,
                        0,
                        0,
                        0d,
                        0d,
                        10d),
                    WeaponShotPattern.Create(
                        WeaponShotPatternKind.Beam,
                        0,
                        0d,
                        0d,
                        1,
                        0d),
                    null,
                    WeaponGuidanceSpec.Homing(
                        10d,
                        90d,
                        0d,
                        WeaponTargetPolicy.ClosestToAim,
                        WeaponReacquisitionMode.None),
                    WeaponImpactSpec.Create(false, false, true, true, null, null),
                    WeaponDamageSpec.Create(
                        WeaponDamageCategory.Energy,
                        1d,
                        0d,
                        0d,
                        0d,
                        0d),
                    WeaponEffects.None(),
                    "weapon-drop.invalid-homing-beam",
                    "weapon-art.invalid-homing-beam");
            });
        }

        [Test]
        public void RicochetWithoutWallImpact_IsRejected()
        {
            Assert.Throws<ArgumentException>(delegate
            {
                WeaponImpactSpec.Create(
                    true,
                    false,
                    true,
                    true,
                    new WeaponRicochetSpec(1, 0.8d, 0d),
                    null);
            });
        }

        [Test]
        public void ExplosionTriggerWithoutExplosionEffect_IsRejected()
        {
            Assert.Throws<ArgumentException>(delegate
            {
                CreateSingleProjectileBlueprint(
                    WeaponImpactSpec.Create(
                        true,
                        true,
                        true,
                        true,
                        null,
                        new WeaponExplosionTriggerSpec(true, false, false, false)),
                    WeaponDamageSpec.Create(
                        WeaponDamageCategory.Thermal,
                        10d,
                        0d,
                        0d,
                        0d,
                        0d),
                    WeaponEffects.None());
            });
        }

        [Test]
        public void DamageOverTimeDataWithoutEffect_IsRejected()
        {
            Assert.Throws<ArgumentException>(delegate
            {
                CreateSingleProjectileBlueprint(
                    WeaponImpactSpec.Create(true, true, true, true, null, null),
                    WeaponDamageSpec.Create(
                        WeaponDamageCategory.Chemical,
                        1d,
                        0d,
                        4d,
                        3d,
                        0d),
                    WeaponEffects.None());
            });
        }

        [Test]
        public void DamageCategoryConversion_DoesNotReinterpretUnknownStrings()
        {
            WeaponDamageCategory category;
            Assert.That(
                WeaponDamageCategoryConversion.TryFromCatalogValue(
                    "Thermal",
                    out category),
                Is.True);
            Assert.That(category, Is.EqualTo(WeaponDamageCategory.Thermal));
            Assert.That(
                WeaponDamageCategoryConversion.TryFromCatalogValue(
                    "Fire",
                    out category),
                Is.False);
            Assert.Throws<FormatException>(delegate
            {
                WeaponDamageCategoryConversion.FromCatalogValue("Fire");
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

        private static WeaponBlueprint CreateSingleProjectileBlueprint(
            WeaponImpactSpec impact,
            WeaponDamageSpec damage,
            WeaponEffects effects)
        {
            return WeaponBlueprint.Create(
                new WeaponDefinitionId("weapon.test-single"),
                "Test Single",
                "test-family",
                WeaponFireSettings.Create(
                    WeaponFireMode.SemiAutomatic,
                    2d,
                    1,
                    1,
                    0d,
                    0d,
                    0d),
                WeaponShotPattern.Create(
                    WeaponShotPatternKind.Single,
                    1,
                    0d,
                    0d,
                    1,
                    0d),
                WeaponProjectileSpec.Create(
                    WeaponProjectileKind.RegularProjectile,
                    20d,
                    15d,
                    new PierceValue(0),
                    WeaponProjectileTerminationBehavior.StopOnFirstBlockingImpact),
                WeaponGuidanceSpec.Unguided(),
                impact,
                damage,
                effects,
                "weapon-drop.test-single",
                "weapon-art.test-single");
        }
    }
}

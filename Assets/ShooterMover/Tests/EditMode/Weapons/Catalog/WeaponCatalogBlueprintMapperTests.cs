using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Tests.EditMode.Weapons.Catalog
{
    public sealed partial class WeaponCatalogBlueprintMapperTests
    {
        [Test]
        public void Map_PreservesAuthoredCombatAndIdentityFields()
        {
            WeaponCatalog catalog = BuildCatalog(
                damageType: "Thermal",
                burstCount: 1,
                projectiles: 3,
                spread: 14d,
                areaDamage: 8d,
                explosionRadius: 2.5d,
                dotDps: 4d,
                dotDuration: 3d,
                chainTargets: 2,
                chainRange: 5d,
                definitionArt: new[] { "art/weapon.mk1.png" });
            WeaponGuidanceSpec guidance = WeaponGuidanceSpec.Homing(
                24d,
                110d,
                0.2d,
                WeaponTargetPolicy.ClosestToAim,
                WeaponReacquisitionMode.ReuseTargetPolicy);
            WeaponImpactSpec impact = WeaponImpactSpec.Create(
                true,
                true,
                true,
                true,
                null,
                new WeaponExplosionTriggerSpec(true, true, true, true));

            WeaponBlueprintMappingResult result = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent(
                    WeaponFireMode.Automatic,
                    WeaponShotPatternKind.Spread,
                    WeaponCatalogSpreadInterpretation.AuthoredSpread,
                    guidance,
                    impact,
                    new WeaponCatalogExplosionMapping(0.25d),
                    new WeaponCatalogDamageOverTimeMapping(4d, 2, true),
                    new WeaponCatalogChainMapping(0.7d)));

            Assert.That(result.Succeeded, Is.True, JoinIssues(result));
            WeaponBlueprint blueprint = result.Blueprint;
            Assert.That(blueprint.DefinitionId.Value, Is.EqualTo("test_weapon.mk1"));
            Assert.That(blueprint.DisplayName, Is.EqualTo("Test Weapon MK1"));
            Assert.That(blueprint.WeaponFamily, Is.EqualTo("test_weapon"));
            Assert.That(blueprint.DropMetadataReference, Is.EqualTo("test_weapon.mk1"));
            Assert.That(blueprint.PresentationReference, Is.EqualTo("art/weapon.mk1.png"));
            Assert.That(blueprint.FireSettings.ShotsPerSecond, Is.EqualTo(2.5d));
            Assert.That(blueprint.FireSettings.ShotsPerTrigger, Is.EqualTo(1));
            Assert.That(blueprint.FireSettings.ShotsPerBurst, Is.EqualTo(1));
            Assert.That(blueprint.ShotPattern.ProjectilesPerShot, Is.EqualTo(3));
            Assert.That(blueprint.ShotPattern.SpreadDegrees, Is.EqualTo(14d));
            Assert.That(blueprint.Projectile.Kind, Is.EqualTo(WeaponProjectileKind.Rocket));
            Assert.That(blueprint.Projectile.Speed, Is.EqualTo(31d));
            Assert.That(blueprint.Projectile.Range, Is.EqualTo(42d));
            Assert.That(blueprint.Projectile.Pierce.Tenths, Is.EqualTo(20));
            Assert.That(blueprint.Guidance.Mode, Is.EqualTo(WeaponGuidanceMode.Homing));
            Assert.That(blueprint.Guidance.AcquisitionRange, Is.EqualTo(24d));
            Assert.That(blueprint.Damage.Category, Is.EqualTo(WeaponDamageCategory.Thermal));
            Assert.That(blueprint.Damage.DirectDamage, Is.EqualTo(12d));
            Assert.That(blueprint.Damage.AreaDamage, Is.EqualTo(8d));
            Assert.That(blueprint.Damage.DamageOverTimePerSecond, Is.EqualTo(4d));
            Assert.That(blueprint.Effects.Explosion.Radius, Is.EqualTo(2.5d));
            Assert.That(blueprint.Effects.DamageOverTime.MaximumStacks, Is.EqualTo(2));
            Assert.That(blueprint.Effects.ChainArc.MaximumTargets, Is.EqualTo(2));
        }

        [Test]
        public void Map_LegacyDamageTypeRequiresExplicitCategory()
        {
            WeaponCatalog catalog = BuildCatalog(damageType: "Kinetic");

            WeaponBlueprintMappingResult missing = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent(explicitDamageCategory: null));
            AssertIssue(missing, WeaponBlueprintMappingIssueCode.UnsupportedDamageType);

            WeaponBlueprintMappingResult explicitResult = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent(explicitDamageCategory: WeaponDamageCategory.Physical));
            Assert.That(explicitResult.Succeeded, Is.True, JoinIssues(explicitResult));
            Assert.That(
                explicitResult.Blueprint.Damage.Category,
                Is.EqualTo(WeaponDamageCategory.Physical));
        }

        [Test]
        public void Map_BurstRequiresExplicitTimingAndPreservesCounts()
        {
            WeaponCatalog catalog = BuildCatalog(burstCount: 4);

            WeaponBlueprintMappingResult missing = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent(fireMode: WeaponFireMode.Burst));
            AssertIssue(missing, WeaponBlueprintMappingIssueCode.InvalidFireConfiguration);

            WeaponBlueprintMappingResult mapped = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent(
                    fireMode: WeaponFireMode.Burst,
                    burstInterval: 0.08d,
                    burstRecovery: 0.5d));
            Assert.That(mapped.Succeeded, Is.True, JoinIssues(mapped));
            Assert.That(mapped.Blueprint.FireSettings.ShotsPerBurst, Is.EqualTo(4));
            Assert.That(mapped.Blueprint.ShotPattern.ProjectilesPerShot, Is.EqualTo(1));
            Assert.That(mapped.Blueprint.FireSettings.IntervalBetweenBurstShotsSeconds, Is.EqualTo(0.08d));
        }

        [Test]
        public void Map_ContinuousFailsRatherThanDiscardProjectileFields()
        {
            WeaponCatalog catalog = BuildCatalog();

            WeaponBlueprintMappingResult result = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent(fireMode: WeaponFireMode.Continuous));

            AssertIssue(result, WeaponBlueprintMappingIssueCode.UnsupportedContinuousDefinition);
        }

    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Tests.EditMode.Weapons
{
    public sealed class EffectiveWeaponTests
    {
        [Test]
        public void Create_AppliesModifierStagesInRequiredOrderAndSupportsWeaponValues()
        {
            WeaponBlueprint blueprint = CreateFullBlueprint();
            TestEquipmentContext context = CreateEquipmentContext(
                blueprint,
                37,
                "one");

            List<WeaponStatModifier> authoredModifiers = new List<WeaponStatModifier>
            {
                WeaponStatModifier.Flat(WeaponEffectiveStat.DirectDamage, 2d),
                WeaponStatModifier.AdditivePercent(WeaponEffectiveStat.DirectDamage, 0.5d),
                WeaponStatModifier.Multiply(WeaponEffectiveStat.DirectDamage, 3d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.AreaDamage, 100d),
                WeaponStatModifier.Override(WeaponEffectiveStat.AreaDamage, 7d),
                WeaponStatModifier.AdditivePercent(WeaponEffectiveStat.RateOfFire, 1d),
                WeaponStatModifier.Multiply(WeaponEffectiveStat.SpreadDegrees, 0.5d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.RandomnessDegrees, 1d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.ProjectileSpeed, 5d),
                WeaponStatModifier.Multiply(WeaponEffectiveStat.ProjectileRange, 1.5d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.PierceTenths, 5d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.ExplosionRadius, 2d),
                WeaponStatModifier.Multiply(WeaponEffectiveStat.DamageOverTimePerSecond, 2d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.DamageOverTimeDurationSeconds, 1d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.DamageOverTimeTicksPerSecond, 1d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.DamageOverTimeMaximumStacks, 2d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.HomingAcquisitionRange, 5d),
                WeaponStatModifier.Multiply(WeaponEffectiveStat.HomingTurnRateDegreesPerSecond, 2d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.HomingActivationDelaySeconds, -2d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.RicochetMaximumRicochets, 1d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.RicochetRetainedSpeed, 0.5d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.RicochetRandomAngleDegrees, 5d),
                WeaponStatModifier.Flat(WeaponEffectiveStat.ChainMaximumTargets, 1d),
                WeaponStatModifier.Multiply(WeaponEffectiveStat.ChainAcquisitionRange, 2d),
                WeaponStatModifier.Multiply(WeaponEffectiveStat.ChainRetainedDamagePerJump, 4d),
            };
            WeaponAugmentModifierSet modifierSet = WeaponAugmentModifierSet.Create(
                context.AugmentDefinitions[0],
                context.AugmentInstances[0],
                authoredModifiers);

            // Modifier sets must snapshot caller-owned collections.
            authoredModifiers.Add(
                WeaponStatModifier.Override(WeaponEffectiveStat.DirectDamage, 1d));

            EffectiveWeapon effective = EffectiveWeaponFactory.Create(
                blueprint,
                context.Catalog,
                context.EquipmentInstance,
                new[] { modifierSet });

            Assert.That(effective.Damage.DirectDamage, Is.EqualTo(54d));
            Assert.That(effective.Damage.AreaDamage, Is.EqualTo(7d));
            Assert.That(effective.FireSettings.ShotsPerSecond, Is.EqualTo(8d));
            Assert.That(effective.ShotPattern.SpreadDegrees, Is.EqualTo(10d));
            Assert.That(effective.ShotPattern.RandomnessDegrees, Is.EqualTo(6d));
            Assert.That(effective.Projectile.Speed, Is.EqualTo(35d));
            Assert.That(effective.Projectile.Range, Is.EqualTo(60d));
            Assert.That(effective.Projectile.Pierce.Tenths, Is.EqualTo(15));
            Assert.That(effective.Effects.Explosion.Radius, Is.EqualTo(5d));
            Assert.That(effective.Damage.DamageOverTimePerSecond, Is.EqualTo(4d));
            Assert.That(effective.Damage.DamageOverTimeDurationSeconds, Is.EqualTo(5d));
            Assert.That(effective.Effects.DamageOverTime.TicksPerSecond, Is.EqualTo(3d));
            Assert.That(effective.Effects.DamageOverTime.MaximumStacks, Is.EqualTo(5));
            Assert.That(effective.Guidance.AcquisitionRange, Is.EqualTo(30d));
            Assert.That(effective.Guidance.TurnRateDegreesPerSecond, Is.EqualTo(180d));
            Assert.That(effective.Guidance.ActivationDelaySeconds, Is.EqualTo(0d));
            Assert.That(effective.Impact.Ricochet.MaximumRicochets, Is.EqualTo(3));
            Assert.That(effective.Impact.Ricochet.RetainedSpeedPerRicochet, Is.EqualTo(1d));
            Assert.That(effective.Impact.Ricochet.RandomAngleDegrees, Is.EqualTo(15d));
            Assert.That(effective.Effects.ChainArc.MaximumTargets, Is.EqualTo(3));
            Assert.That(effective.Effects.ChainArc.AcquisitionRange, Is.EqualTo(16d));
            Assert.That(effective.Effects.ChainArc.RetainedDamagePerJump, Is.EqualTo(1d));

            Assert.That(effective.EquipmentInstanceId.Value, Is.EqualTo(context.EquipmentInstance.InstanceId));
            Assert.That(effective.ItemLevel, Is.EqualTo(37));
            Assert.That(effective.InstalledAugments.Count, Is.EqualTo(1));

            // Canonical inputs remain untouched.
            Assert.That(blueprint.Damage.DirectDamage, Is.EqualTo(10d));
            Assert.That(blueprint.FireSettings.ShotsPerSecond, Is.EqualTo(4d));
            Assert.That(context.EquipmentInstance.ItemLevel, Is.EqualTo(37));
            Assert.That(context.AugmentDefinitions[0].DisplayName, Is.EqualTo("Test Augment one"));
        }

        [Test]
        public void Create_ItemLevelIsPreservedButDoesNotScaleCombatValues()
        {
            WeaponBlueprint blueprint = CreateFullBlueprint();
            EquipmentDefinition equipmentDefinition = CreateEquipmentDefinition(blueprint, 0);
            EquipmentCatalog catalog = BuildCatalog(
                equipmentDefinition,
                new AugmentDefinition[0]);

            EquipmentInstance lowLevel = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.low-level"),
                equipmentDefinition.DefinitionId,
                1,
                QualityId,
                new AugmentInstance[0]);
            EquipmentInstance highLevel = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.high-level"),
                equipmentDefinition.DefinitionId,
                99,
                QualityId,
                new AugmentInstance[0]);

            EffectiveWeapon low = EffectiveWeaponFactory.Create(
                blueprint,
                catalog,
                lowLevel,
                new WeaponAugmentModifierSet[0]);
            EffectiveWeapon high = EffectiveWeaponFactory.Create(
                blueprint,
                catalog,
                highLevel,
                new WeaponAugmentModifierSet[0]);

            Assert.That(low.ItemLevel, Is.EqualTo(1));
            Assert.That(high.ItemLevel, Is.EqualTo(99));
            Assert.That(low.Damage.DirectDamage, Is.EqualTo(high.Damage.DirectDamage));
            Assert.That(low.FireSettings.ShotsPerSecond, Is.EqualTo(high.FireSettings.ShotsPerSecond));
            Assert.That(low.Projectile.Speed, Is.EqualTo(high.Projectile.Speed));
            Assert.That(low.Projectile.Range, Is.EqualTo(high.Projectile.Range));
        }

        [Test]
        public void Create_RejectsStructuralAugmentWhenFeatureIsAbsent()
        {
            WeaponBlueprint blueprint = CreatePlainProjectileBlueprint();
            TestEquipmentContext context = CreateEquipmentContext(
                blueprint,
                10,
                "explosion");
            WeaponAugmentModifierSet modifierSet = WeaponAugmentModifierSet.Create(
                context.AugmentDefinitions[0],
                context.AugmentInstances[0],
                new[]
                {
                    WeaponStatModifier.Flat(
                        WeaponEffectiveStat.ExplosionRadius,
                        2d),
                });

            IncompatibleWeaponAugmentException exception =
                Assert.Throws<IncompatibleWeaponAugmentException>(delegate
                {
                    EffectiveWeaponFactory.Create(
                        blueprint,
                        context.Catalog,
                        context.EquipmentInstance,
                        new[] { modifierSet });
                });

            Assert.That(exception.Stat, Is.EqualTo(WeaponEffectiveStat.ExplosionRadius));
            Assert.That(exception.AugmentInstanceId, Is.EqualTo(context.AugmentInstances[0].InstanceId));
        }

        [Test]
        public void Create_RejectsConflictingExplicitOverrides()
        {
            WeaponBlueprint blueprint = CreateFullBlueprint();
            TestEquipmentContext context = CreateEquipmentContext(
                blueprint,
                20,
                "override");
            WeaponAugmentModifierSet modifierSet = WeaponAugmentModifierSet.Create(
                context.AugmentDefinitions[0],
                context.AugmentInstances[0],
                new[]
                {
                    WeaponStatModifier.Override(WeaponEffectiveStat.DirectDamage, 20d),
                    WeaponStatModifier.Override(WeaponEffectiveStat.DirectDamage, 30d),
                });

            Assert.Throws<InvalidOperationException>(delegate
            {
                EffectiveWeaponFactory.Create(
                    blueprint,
                    context.Catalog,
                    context.EquipmentInstance,
                    new[] { modifierSet });
            });
        }

        [Test]
        public void Create_RequiresExactInstalledAugmentModifierCoverage()
        {
            WeaponBlueprint blueprint = CreateFullBlueprint();
            TestEquipmentContext context = CreateEquipmentContext(
                blueprint,
                20,
                "coverage");

            Assert.Throws<ArgumentException>(delegate
            {
                EffectiveWeaponFactory.Create(
                    blueprint,
                    context.Catalog,
                    context.EquipmentInstance,
                    new WeaponAugmentModifierSet[0]);
            });
        }

        private static readonly StableId QualityId = StableId.Parse("quality.test");

        private static WeaponBlueprint CreateFullBlueprint()
        {
            return WeaponBlueprint.Create(
                new WeaponDefinitionId("weapon.test-effective"),
                "Test Effective Weapon",
                "test-family",
                WeaponFireSettings.Create(
                    WeaponFireMode.Automatic,
                    4d,
                    1,
                    1,
                    0d,
                    0d,
                    0d),
                WeaponShotPattern.Create(
                    WeaponShotPatternKind.Spread,
                    3,
                    20d,
                    5d,
                    1,
                    0d),
                WeaponProjectileSpec.Create(
                    WeaponProjectileKind.Rocket,
                    30d,
                    40d,
                    new PierceValue(10),
                    WeaponProjectileTerminationBehavior.ContinueUntilRangeExpiry),
                WeaponGuidanceSpec.Homing(
                    25d,
                    90d,
                    1d,
                    WeaponTargetPolicy.ClosestToAim,
                    WeaponReacquisitionMode.ReuseTargetPolicy),
                WeaponImpactSpec.Create(
                    true,
                    true,
                    true,
                    true,
                    new WeaponRicochetSpec(2, 0.8d, 10d),
                    new WeaponExplosionTriggerSpec(true, true, true, true)),
                WeaponDamageSpec.Create(
                    WeaponDamageCategory.Thermal,
                    10d,
                    5d,
                    2d,
                    4d,
                    1d),
                new WeaponEffects(
                    new WeaponExplosionEffect(3d, 0.25d),
                    new WeaponDamageOverTimeEffect(2d, 3, true),
                    new WeaponChainArcEffect(2, 8d, 0.5d)),
                "weapon-drop.test-effective",
                "weapon-art.test-effective");
        }

        private static WeaponBlueprint CreatePlainProjectileBlueprint()
        {
            return WeaponBlueprint.Create(
                new WeaponDefinitionId("weapon.test-plain"),
                "Test Plain Weapon",
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
                WeaponImpactSpec.Create(true, true, true, true, null, null),
                WeaponDamageSpec.Create(
                    WeaponDamageCategory.Physical,
                    5d,
                    0d,
                    0d,
                    0d,
                    0d),
                WeaponEffects.None(),
                "weapon-drop.test-plain",
                "weapon-art.test-plain");
        }

        private static TestEquipmentContext CreateEquipmentContext(
            WeaponBlueprint blueprint,
            int itemLevel,
            params string[] augmentSuffixes)
        {
            EquipmentDefinition equipmentDefinition =
                CreateEquipmentDefinition(blueprint, augmentSuffixes.Length);
            List<AugmentDefinition> definitions = new List<AugmentDefinition>();
            List<AugmentInstance> instances = new List<AugmentInstance>();

            for (int index = 0; index < augmentSuffixes.Length; index++)
            {
                string suffix = augmentSuffixes[index];
                AugmentDefinition definition = AugmentDefinition.Create(
                    StableId.Parse("augment.test-" + suffix),
                    StableId.Parse("augment-family.test"),
                    "Test Augment " + suffix,
                    AugmentCompatibility.Create(
                        new[] { EquipmentCategoryIds.Weapon },
                        new StableId[0],
                        new StableId[0],
                        new StableId[0]),
                    new StableId[0],
                    AugmentDuplicatePolicy.DisallowSameDefinition,
                    InclusiveIntRange.Create(1, 10),
                    InclusiveIntRange.Create(1, 100));
                AugmentInstance instance = AugmentInstance.Create(
                    StableId.Parse("augment-instance.test-" + suffix),
                    definition.DefinitionId,
                    1,
                    1);
                definitions.Add(definition);
                instances.Add(instance);
            }

            EquipmentCatalog catalog = BuildCatalog(
                equipmentDefinition,
                definitions.ToArray());
            EquipmentInstance equipmentInstance = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.test-" + itemLevel),
                equipmentDefinition.DefinitionId,
                itemLevel,
                QualityId,
                instances);

            EquipmentValidationResult validation = catalog.ValidateInstance(equipmentInstance);
            Assert.That(validation.IsValid, Is.True, FormatIssues(validation));

            return new TestEquipmentContext(
                catalog,
                equipmentInstance,
                definitions,
                instances);
        }

        private static EquipmentDefinition CreateEquipmentDefinition(
            WeaponBlueprint blueprint,
            int maximumAugmentSlots)
        {
            return EquipmentDefinition.Create(
                StableId.Parse("equipment.test-effective"),
                EquipmentCategoryIds.Weapon,
                StableId.Parse("weapon-family.test-effective"),
                "Test Effective Equipment",
                StableId.Parse(blueprint.DefinitionId.Value),
                InclusiveIntRange.Create(1, 100),
                maximumAugmentSlots,
                new[]
                {
                    EquipmentQualityTier.Create(QualityId, "Test", 1),
                },
                new StableId[0]);
        }

        private static EquipmentCatalog BuildCatalog(
            EquipmentDefinition equipmentDefinition,
            IEnumerable<AugmentDefinition> augmentDefinitions)
        {
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { equipmentDefinition },
                augmentDefinitions);
            Assert.That(result.IsValid, Is.True, FormatIssues(result.Issues));
            return result.Catalog;
        }

        private static string FormatIssues(EquipmentValidationResult result)
        {
            return FormatIssues(result.Issues);
        }

        private static string FormatIssues(IReadOnlyList<EquipmentModelIssue> issues)
        {
            string value = string.Empty;
            for (int index = 0; index < issues.Count; index++)
            {
                value += issues[index] + Environment.NewLine;
            }
            return value;
        }

        private sealed class TestEquipmentContext
        {
            public TestEquipmentContext(
                EquipmentCatalog catalog,
                EquipmentInstance equipmentInstance,
                IList<AugmentDefinition> augmentDefinitions,
                IList<AugmentInstance> augmentInstances)
            {
                Catalog = catalog;
                EquipmentInstance = equipmentInstance;
                AugmentDefinitions = augmentDefinitions;
                AugmentInstances = augmentInstances;
            }

            public EquipmentCatalog Catalog { get; }
            public EquipmentInstance EquipmentInstance { get; }
            public IList<AugmentDefinition> AugmentDefinitions { get; }
            public IList<AugmentInstance> AugmentInstances { get; }
        }
    }
}

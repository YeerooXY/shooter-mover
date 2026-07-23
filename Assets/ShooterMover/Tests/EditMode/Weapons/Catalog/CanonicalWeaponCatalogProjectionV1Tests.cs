
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Tests.EditMode.Weapons.Catalog
{
    public sealed class CanonicalWeaponCatalogProjectionV1Tests
    {
        private const string BaselinePath =
            "Assets/ShooterMover/Resources/WeaponCatalog/weapon_baseline_v01.json";

        [Test]
        public void Baseline_ProjectsExactCountsAndNormalizedQualities()
        {
            CanonicalWeaponCatalogProjectionV1 projection = CreateProjection();
            Assert.That(projection.WeaponCatalog.Definitions.Count, Is.EqualTo(121));
            Assert.That(projection.WeaponCatalog.Families.Count, Is.EqualTo(44));
            Assert.That(projection.EquipmentCatalog.EquipmentDefinitions.Count, Is.EqualTo(121));

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < projection.Entries.Count; index++)
            {
                CanonicalWeaponCatalogEntryV1 entry = projection.Entries[index];
                int count;
                counts.TryGetValue(entry.NormalizedRarity, out count);
                counts[entry.NormalizedRarity] = count + 1;
                Assert.That(entry.EquipmentDefinition.QualityTiers.Count, Is.EqualTo(1));
                Assert.That(
                    entry.QualityId,
                    Is.EqualTo(WeaponEquipmentQualityIdsV1.ForNormalizedRarity(entry.NormalizedRarity)));
            }
            Assert.That(counts["Common"], Is.EqualTo(73));
            Assert.That(counts["Rare"], Is.EqualTo(28));
            Assert.That(counts["Epic"], Is.EqualTo(8));
            Assert.That(counts["Legendary"], Is.EqualTo(6));
            Assert.That(counts[WeaponRarityNormalizationPolicyV1.MythicArtifact], Is.EqualTo(6));
        }

        [Test]
        public void RuntimeRegistry_ResolvesFiveExactPackages_AndPendingFailsClosed()
        {
            CanonicalWeaponCatalogProjectionV1 projection = CreateProjection();
            ProductionWeaponRuntimePackageRegistryV1 registry =
                ProductionWeaponRuntimePackageRegistryV1.CreateDefault();
            Assert.That(registry.RegisteredDefinitionIds.Count, Is.EqualTo(5));

            AssertResolved(projection, registry, "blaster.mk1", BuiltInWeaponBehaviorIds.Projectile);
            AssertResolved(projection, registry, "shotgun.mk1", BuiltInWeaponBehaviorIds.Projectile);
            AssertResolved(projection, registry, "rocket_launcher.mk1", BuiltInWeaponBehaviorIds.Explosive);
            AssertResolved(projection, registry, "chain_weapon.mk1", BuiltInWeaponBehaviorIds.Chain);
            AssertResolved(projection, registry, "ricochet_weapon.mk1", BuiltInWeaponBehaviorIds.Projectile);

            CanonicalWeaponCatalogEntryV1 pending;
            Assert.That(projection.TryGetByWeaponDefinitionId("thermal_carbine.mk1", out pending), Is.True);
            EquipmentInstance instance = InstanceFor(pending, "equipment-instance.test-pending-runtime");
            var resolver = new WeaponCatalogRuntimeProfileResolver(
                projection.EquipmentCatalog,
                projection.WeaponCatalog,
                registry,
                60);
            WeaponProfileResolution result = resolver.Resolve(
                new EquipmentInstanceId(instance.InstanceId),
                instance);
            Assert.That(result.Status, Is.EqualTo(WeaponProfileResolutionStatus.RuntimeBehaviorPending));
            Assert.That(
                result.RejectionCode,
                Is.EqualTo("weapon-runtime-behavior-pending:thermal_carbine.mk1"));
        }

        [Test]
        public void AuthoredCombatProfile_IsIndependentOfItemLevel()
        {
            CanonicalWeaponCatalogProjectionV1 projection = CreateProjection();
            CanonicalWeaponCatalogEntryV1 entry;
            Assert.That(projection.TryGetByWeaponDefinitionId("blaster.mk1", out entry), Is.True);
            var registry = ProductionWeaponRuntimePackageRegistryV1.CreateDefault();
            var resolver = new WeaponCatalogRuntimeProfileResolver(
                projection.EquipmentCatalog,
                projection.WeaponCatalog,
                registry,
                60);
            EquipmentInstance low = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.test-low-level"),
                entry.EquipmentDefinition.DefinitionId,
                1,
                entry.QualityId,
                Array.Empty<AugmentInstance>());
            EquipmentInstance high = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.test-high-level"),
                entry.EquipmentDefinition.DefinitionId,
                100,
                entry.QualityId,
                Array.Empty<AugmentInstance>());
            WeaponProfileResolution lowResult = resolver.Resolve(new EquipmentInstanceId(low.InstanceId), low);
            WeaponProfileResolution highResult = resolver.Resolve(new EquipmentInstanceId(high.InstanceId), high);
            Assert.That(lowResult.Succeeded, Is.True, lowResult.RejectionCode);
            Assert.That(highResult.Succeeded, Is.True, highResult.RejectionCode);
            AssertProfilesEqual(lowResult.Profile, highResult.Profile);
        }

        [Test]
        public void CompatibilityFacade_FailsClearlyUntilExplicitlyComposed()
        {
            CanonicalWeaponCatalogProjectionV1 existing;
            if (ProductionStarterWeaponCatalogV1.TryGetCanonicalProjection(out existing))
            {
                Assert.Pass("A prior production composition already installed the canonical projection.");
            }
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                delegate { ProductionStarterWeaponCatalogV1.BuildCanonicalProjection(); });
            Assert.That(exception.Message, Is.EqualTo("production-weapon-catalog-not-composed"));
        }

        private static void AssertProfilesEqual(
            WeaponRuntimeFiringProfile expected,
            WeaponRuntimeFiringProfile actual)
        {
            Assert.That(actual.DefinitionId, Is.EqualTo(expected.DefinitionId));
            Assert.That(actual.BehaviorId, Is.EqualTo(expected.BehaviorId));
            Assert.That(actual.CooldownTicks, Is.EqualTo(expected.CooldownTicks));
            Assert.That(actual.ProjectileCount, Is.EqualTo(expected.ProjectileCount));
            Assert.That(actual.SpreadDegrees, Is.EqualTo(expected.SpreadDegrees));
            Assert.That(actual.ProjectileSpeed, Is.EqualTo(expected.ProjectileSpeed));
            Assert.That(actual.ProjectileRange, Is.EqualTo(expected.ProjectileRange));
            Assert.That(actual.DirectDamage, Is.EqualTo(expected.DirectDamage));
            Assert.That(actual.Pierce, Is.EqualTo(expected.Pierce));
            Assert.That(actual.AreaDamage, Is.EqualTo(expected.AreaDamage));
            Assert.That(actual.ExplosionRadius, Is.EqualTo(expected.ExplosionRadius));
            Assert.That(actual.DotDps, Is.EqualTo(expected.DotDps));
            Assert.That(actual.DotDuration, Is.EqualTo(expected.DotDuration));
            Assert.That(actual.PoolRadius, Is.EqualTo(expected.PoolRadius));
            Assert.That(actual.PoolDuration, Is.EqualTo(expected.PoolDuration));
            Assert.That(actual.ChainTargets, Is.EqualTo(expected.ChainTargets));
            Assert.That(actual.ChainRange, Is.EqualTo(expected.ChainRange));
            Assert.That(actual.Knockback, Is.EqualTo(expected.Knockback));
            Assert.That(actual.DamageType, Is.EqualTo(expected.DamageType));
        }

        private static void AssertResolved(
            CanonicalWeaponCatalogProjectionV1 projection,
            IWeaponRuntimePackageRegistryV1 registry,
            string definitionId,
            WeaponBehaviorId expectedBehavior)
        {
            CanonicalWeaponCatalogEntryV1 entry;
            Assert.That(projection.TryGetByWeaponDefinitionId(definitionId, out entry), Is.True);
            EquipmentInstance instance = InstanceFor(
                entry,
                "equipment-instance.test-" + definitionId.Replace('.', '-').Replace('_', '-'));
            var resolver = new WeaponCatalogRuntimeProfileResolver(
                projection.EquipmentCatalog,
                projection.WeaponCatalog,
                registry,
                60);
            WeaponProfileResolution result = resolver.Resolve(
                new EquipmentInstanceId(instance.InstanceId),
                instance);
            Assert.That(result.Succeeded, Is.True, result.RejectionCode);
            Assert.That(result.Profile.BehaviorId, Is.EqualTo(expectedBehavior));
        }

        private static EquipmentInstance InstanceFor(
            CanonicalWeaponCatalogEntryV1 entry,
            string instanceId)
        {
            return EquipmentInstance.Create(
                StableId.Parse(instanceId),
                entry.EquipmentDefinition.DefinitionId,
                1,
                entry.QualityId,
                Array.Empty<AugmentInstance>());
        }

        private static CanonicalWeaponCatalogProjectionV1 CreateProjection()
        {
            string json = File.ReadAllText(BaselinePath);
            CanonicalWeaponCatalogProjectionV1 projection;
            string diagnostic;
            Assert.That(
                CanonicalWeaponCatalogProjectionV1.TryCreate(
                    new StringWeaponCatalogSourceV1("weapon-baseline-v01-tests", json),
                    WeaponRarityNormalizationPolicyV1.CreateBaselineV1(),
                    out projection,
                    out diagnostic),
                Is.True,
                diagnostic);
            return projection;
        }
    }
}

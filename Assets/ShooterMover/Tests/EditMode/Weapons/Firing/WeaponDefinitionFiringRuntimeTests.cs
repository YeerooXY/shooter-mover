using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Firing;
using ShooterMover.ContentPackages.Weapons.Stage1Loadouts;
using ShooterMover.Contracts.Combat;
using ShooterMover.TestSupport.VisibleSlice;

namespace ShooterMover.Tests.EditMode.Weapons.Firing
{
    public sealed class WeaponDefinitionFiringRuntimeTests
    {
        private WeaponDefinitionFiringAdapter adapter;

        [SetUp]
        public void SetUp()
        {
            Stage1WeaponCatalogRuntimeProvider.ResetForTests();
            adapter = new WeaponDefinitionFiringAdapter(
                Stage1WeaponCatalogRuntimeProvider.Load());
        }

        [Test]
        public void JsonDefinition_MapsToExpectedCompleteFiringProfile()
        {
            WeaponDefinitionFiringProfile value = adapter.Resolve("rocket_launcher.mk1");

            Assert.That(value.DefinitionId, Is.EqualTo("rocket_launcher.mk1"));
            Assert.That(value.DamagePerProjectile, Is.EqualTo(12.527801985496335).Within(0.000000001));
            Assert.That(value.FireRate, Is.EqualTo(0.92));
            Assert.That(value.CooldownSeconds, Is.EqualTo(1d / 0.92d).Within(0.000000001));
            Assert.That(value.ProjectileCountPerShot, Is.EqualTo(1));
            Assert.That(value.SpreadDegrees, Is.EqualTo(1.0));
            Assert.That(value.ProjectileSpeed, Is.EqualTo(15.0));
            Assert.That(value.ProjectileLifetimeSeconds, Is.EqualTo(32d / 15d).Within(0.000000001));
            Assert.That(value.Range, Is.EqualTo(32.0));
            Assert.That(value.Pierce, Is.Zero);
            Assert.That(value.ExplosionRadius, Is.EqualTo(2.5));
            Assert.That(value.AreaDamagePerTrigger, Is.EqualTo(17.30029797997113).Within(0.000000001));
            Assert.That(value.DotDps, Is.Zero);
            Assert.That(value.DotDuration, Is.Zero);
            Assert.That(value.ChainTargets, Is.Zero);
            Assert.That(value.ChainRange, Is.Zero);
            Assert.That(value.Knockback, Is.EqualTo(1.15));
            Assert.That(value.ProjectileRadius, Is.GreaterThan(0.08));
        }

        [Test]
        public void DifferentDefinitions_ProduceDifferentCadenceAndProjectileBehavior()
        {
            WeaponDefinitionFiringProfile blaster = adapter.Resolve("blaster.mk1");
            WeaponDefinitionFiringProfile shotgun = adapter.Resolve("shotgun.mk1");
            WeaponDefinitionFiringProfile rocket = adapter.Resolve("rocket_launcher.mk1");

            Assert.That(shotgun.CooldownSeconds, Is.GreaterThan(blaster.CooldownSeconds));
            Assert.That(rocket.CooldownSeconds, Is.GreaterThan(shotgun.CooldownSeconds));
            Assert.That(blaster.ProjectileCountPerShot, Is.EqualTo(1));
            Assert.That(shotgun.ProjectileCountPerShot, Is.EqualTo(7));
            Assert.That(rocket.ProjectileSpeed, Is.LessThan(blaster.ProjectileSpeed));
            Assert.That(rocket.ExplosionRadius, Is.GreaterThan(0d));
        }

        [Test]
        public void PerMountCooldowns_AreIndependentAndFollowEachDefinition()
        {
            var session = CreateDefaultSession();

            IReadOnlyList<WeaponShotPlan> first = session.PlanReadyShots(0d);
            Assert.That(first.Count, Is.EqualTo(4));

            IReadOnlyList<WeaponShotPlan> atPointFourteen = session.PlanReadyShots(0.14d);
            Assert.That(atPointFourteen.Count, Is.EqualTo(2));
            Assert.That(
                DefinitionIds(atPointFourteen),
                Is.EquivalentTo(new[] { "blaster.mk1", "arc_rifle.mk1" }));

            IReadOnlyList<WeaponShotPlan> atPointSeven = session.PlanReadyShots(0.70d);
            Assert.That(atPointSeven.Count, Is.EqualTo(3));
            Assert.That(
                DefinitionIds(atPointSeven),
                Is.EquivalentTo(new[] { "blaster.mk1", "shotgun.mk1", "arc_rifle.mk1" }));
        }

        [Test]
        public void SeededSpread_IsRepeatableButChangesWithShotSequence()
        {
            WeaponDefinitionFiringProfile shotgun = adapter.Resolve("shotgun.mk1");
            var firstIdentity = new WeaponShotIdentity(
                112233UL,
                WeaponMountSlot.MountTwo,
                shotgun.DefinitionId,
                7L);
            var repeatedIdentity = new WeaponShotIdentity(
                112233UL,
                WeaponMountSlot.MountTwo,
                shotgun.DefinitionId,
                7L);
            var nextIdentity = new WeaponShotIdentity(
                112233UL,
                WeaponMountSlot.MountTwo,
                shotgun.DefinitionId,
                8L);

            WeaponShotPlan first = DeterministicWeaponSpread.CreatePlan(shotgun, firstIdentity);
            WeaponShotPlan repeated = DeterministicWeaponSpread.CreatePlan(shotgun, repeatedIdentity);
            WeaponShotPlan next = DeterministicWeaponSpread.CreatePlan(shotgun, nextIdentity);

            Assert.That(repeated.SpreadOffsetsDegrees, Is.EqualTo(first.SpreadOffsetsDegrees));
            Assert.That(next.SpreadOffsetsDegrees, Is.Not.EqualTo(first.SpreadOffsetsDegrees));
            Assert.That(first.SpreadOffsetsDegrees.Count, Is.EqualTo(7));
            for (int index = 0; index < first.SpreadOffsetsDegrees.Count; index++)
            {
                Assert.That(first.SpreadOffsetsDegrees[index], Is.InRange(-9.5d, 9.5d));
            }
        }

        [Test]
        public void ShotgunShotPlan_CreatesConfiguredProjectileCount()
        {
            WeaponDefinitionFiringProfile shotgun = adapter.Resolve("shotgun.mk1");
            WeaponShotPlan plan = DeterministicWeaponSpread.CreatePlan(
                shotgun,
                new WeaponShotIdentity(
                    9988UL,
                    WeaponMountSlot.MountThree,
                    shotgun.DefinitionId,
                    0L));

            Assert.That(plan.SpreadOffsetsDegrees.Count, Is.EqualTo(7));
            Assert.That(plan.Profile.ProjectilesPerTrigger, Is.EqualTo(7));
            Assert.That(plan.Profile.BurstCount, Is.EqualTo(1));
        }

        [Test]
        public void UnknownDefinition_IsRejectedWithoutFallback()
        {
            Assert.That(
                () => adapter.Resolve("not-a-real-weapon.mk1"),
                Throws.TypeOf<KeyNotFoundException>()
                    .With.Message.Contains("Unknown weapon definition ID"));
        }

        [Test]
        public void ChangingSelectedFixture_ChangesConcreteDefinitionInThatMount()
        {
            Stage1WeaponLoadoutFixture defaultFixture =
                Stage1WeaponLoadoutCatalog.Approved.DefaultFixture;
            Stage1WeaponLoadoutFixture ricochetFixture =
                Stage1WeaponLoadoutCatalog.Approved.GetFixedFixture(
                    ShooterMover.Domain.Common.StableId.Parse(
                        Stage1WeaponLoadoutCatalog.RicochetFixtureIdText));

            WeaponDefinitionLoadout before =
                Stage1WeaponRuntimeLoadoutAdapter.FromFixture(defaultFixture);
            WeaponDefinitionLoadout after =
                Stage1WeaponRuntimeLoadoutAdapter.FromFixture(ricochetFixture);

            Assert.That(
                before.GetBySlot(WeaponMountSlot.MountTwo).DefinitionId,
                Is.EqualTo("shotgun.mk1"));
            Assert.That(
                after.GetBySlot(WeaponMountSlot.MountTwo).DefinitionId,
                Is.EqualTo("ricochet_weapon.mk1"));
        }

        [Test]
        public void RestartReset_ClearsCooldownsButPreservesConcreteLoadout()
        {
            Stage1WeaponLoadoutFixture fixture =
                Stage1WeaponLoadoutCatalog.Approved.GetFixedFixture(
                    ShooterMover.Domain.Common.StableId.Parse(
                        Stage1WeaponLoadoutCatalog.RicochetFixtureIdText));
            WeaponDefinitionLoadout loadout =
                Stage1WeaponRuntimeLoadoutAdapter.FromFixture(fixture);
            var session = new WeaponMountFiringSession(adapter, loadout, 991UL);

            Assert.That(session.PlanReadyShots(0d).Count, Is.EqualTo(4));
            Assert.That(session.PlanReadyShots(0.01d).Count, Is.Zero);
            string before = session.Loadout.GetBySlot(WeaponMountSlot.MountTwo).DefinitionId;

            session.ResetCooldowns();

            Assert.That(
                session.Loadout.GetBySlot(WeaponMountSlot.MountTwo).DefinitionId,
                Is.EqualTo(before));
            Assert.That(session.PlanReadyShots(0d).Count, Is.EqualTo(4));
        }

        [Test]
        public void Stage1DamageTypeProjection_IsExplicitAndFailClosed()
        {
            Assert.That(
                Stage1WeaponCombatChannelProjection.Resolve("Kinetic"),
                Is.EqualTo(CombatChannel.Kinetic));
            Assert.That(
                Stage1WeaponCombatChannelProjection.Resolve("Thermal"),
                Is.EqualTo(CombatChannel.Thermal));
            Assert.That(
                Stage1WeaponCombatChannelProjection.Resolve("Energized"),
                Is.EqualTo(CombatChannel.Electrical));
            Assert.That(
                () => Stage1WeaponCombatChannelProjection.Resolve("Mystery"),
                Throws.TypeOf<InvalidOperationException>());
        }

        private WeaponMountFiringSession CreateDefaultSession()
        {
            return new WeaponMountFiringSession(
                adapter,
                Stage1WeaponRuntimeLoadoutAdapter.FromFixture(
                    Stage1WeaponLoadoutCatalog.Approved.DefaultFixture),
                123456UL);
        }

        private static string[] DefinitionIds(IReadOnlyList<WeaponShotPlan> plans)
        {
            var result = new string[plans.Count];
            for (int index = 0; index < plans.Count; index++)
            {
                result[index] = plans[index].Profile.DefinitionId;
            }
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;

namespace ShooterMover.Tests.EditMode.Weapons.Live
{
    public sealed partial class InventoryBackedWeaponExecutionAdapterTests
    {
        private const int TicksPerSecond = 60;

        private static readonly StableId HoldingsAuthorityId =
            StableId.Parse("holdings.test-player");
        private static readonly StableId ActorId =
            StableId.Parse("actor.test-player");
        private static readonly StableId ParticipantId =
            StableId.Parse("participant.test-player");
        private static readonly StableId QualityId =
            StableId.Parse("quality.common");
        private static readonly StableId EquipmentFamilyId =
            StableId.Parse("equipment-family.test-weapons");

        [Test]
        public void CatalogDefinitions_ResolveToCanonicalCoreProfiles()
        {
            EquipmentInstance blaster = Equipment(
                "equipment-instance.blaster",
                "equipment-definition.blaster");
            EquipmentInstance shotgun = Equipment(
                "equipment-instance.shotgun",
                "equipment-definition.shotgun");
            EquipmentInstance rocket = Equipment(
                "equipment-instance.rocket",
                "equipment-definition.rocket");
            EquipmentInstance flamethrower = Equipment(
                "equipment-instance.flamethrower",
                "equipment-definition.flamethrower");
            Harness harness = CreateHarness(blaster, shotgun, rocket, flamethrower);

            AssertProfile(
                harness,
                blaster,
                "fire.profile-blaster",
                "weapon.blaster-machine-gun",
                10d,
                1,
                0d,
                40d,
                30d,
                5d,
                1,
                0d,
                0d,
                6);
            AssertProfile(
                harness,
                shotgun,
                "fire.profile-shotgun",
                "weapon.shotgun",
                2d,
                7,
                24d,
                30d,
                15d,
                3d,
                0,
                0d,
                0d,
                30);
            AssertProfile(
                harness,
                rocket,
                "fire.profile-rocket",
                "weapon.rocket-launcher",
                1d,
                1,
                0d,
                12d,
                35d,
                4d,
                0,
                0d,
                0d,
                60);
            AssertProfile(
                harness,
                flamethrower,
                "fire.profile-flamethrower",
                "weapon.flamethrower",
                5d,
                4,
                12d,
                10d,
                8d,
                1d,
                0,
                4d,
                2d,
                12);

            Assert.That(
                harness.Sink.Batches[2].CoreBatch.Effects[0],
                Is.TypeOf<ExplosiveProjectileEffect>());
            Assert.That(
                harness.Sink.Batches[3].CoreBatch.Effects[0],
                Is.TypeOf<DamageOverTimeProjectileEffect>());
        }

        [Test]
        public void Shotgun_UsesCatalogProjectileCountAndRealSpread()
        {
            EquipmentInstance shotgun = Equipment(
                "equipment-instance.shotgun-spread",
                "equipment-definition.shotgun");
            Harness harness = CreateHarness(shotgun);

            InventoryWeaponExecutionResult result = harness.Adapter.TryExecute(
                Request(shotgun, "fire.shotgun-spread", 0L, 4421UL));

            Assert.That(result.Status, Is.EqualTo(WeaponExecutionStatus.Accepted));
            Assert.That(result.EffectBatch.CoreBatch.EffectCount, Is.EqualTo(7));
            var directions = new HashSet<string>(StringComparer.Ordinal);
            foreach (IWeaponEffectDescription description in result.EffectBatch.CoreBatch.Effects)
            {
                var projectile = (DirectProjectileEffect)description;
                directions.Add(projectile.Direction.ToString());
            }

            Assert.That(directions.Count, Is.GreaterThan(1));
        }

        [Test]
        public void Blaster_DoesNotAccidentallyUseShotgunBehavior()
        {
            EquipmentInstance blaster = Equipment(
                "equipment-instance.blaster-single",
                "equipment-definition.blaster");
            Harness harness = CreateHarness(blaster);

            InventoryWeaponExecutionResult result = harness.Adapter.TryExecute(
                Request(blaster, "fire.blaster-single", 0L));

            Assert.That(result.Status, Is.EqualTo(WeaponExecutionStatus.Accepted));
            Assert.That(result.EffectBatch.CoreBatch.EffectCount, Is.EqualTo(1));
            Assert.That(
                result.EffectBatch.CoreBatch.Effects[0],
                Is.TypeOf<DirectProjectileEffect>());
            Assert.That(result.EffectBatch.Profile.SpreadDegrees, Is.Zero);
        }

        [Test]
        public void ConcreteEquipmentInstances_HaveIndependentCooldowns()
        {
            EquipmentInstance first = Equipment(
                "equipment-instance.blaster-a",
                "equipment-definition.blaster");
            EquipmentInstance second = Equipment(
                "equipment-instance.blaster-b",
                "equipment-definition.blaster");
            Harness harness = CreateHarness(first, second);

            Assert.That(
                harness.Adapter.TryExecute(
                    Request(first, "fire.cooldown-a", 0L)).Succeeded,
                Is.True);
            Assert.That(
                harness.Adapter.TryExecute(
                    Request(second, "fire.cooldown-b", 0L)).Succeeded,
                Is.True);
            Assert.That(
                harness.Adapter.TryExecute(
                    Request(first, "fire.cooldown-a-2", 0L)).Status,
                Is.EqualTo(WeaponExecutionStatus.CooldownActive));
        }

    }
}

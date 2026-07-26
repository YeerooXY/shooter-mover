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
        public void AuthoredFamilyDefinitions_ResolveToCanonicalCoreProfiles()
        {
            EquipmentInstance rattler = Equipment(
                "equipment-instance.rattler",
                "equipment-definition.rattler");
            EquipmentInstance ironwake = Equipment(
                "equipment-instance.ironwake",
                "equipment-definition.ironwake");
            EquipmentInstance crownfall = Equipment(
                "equipment-instance.crownfall",
                "equipment-definition.crownfall");
            EquipmentInstance nullstar = Equipment(
                "equipment-instance.nullstar",
                "equipment-definition.nullstar");
            Harness harness = CreateHarness(rattler, ironwake, crownfall, nullstar);

            AssertProfile(
                harness,
                rattler,
                "fire.profile-rattler",
                "rattler.mk1",
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
                ironwake,
                "fire.profile-ironwake",
                "ironwake.mk1",
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
                crownfall,
                "fire.profile-crownfall",
                "crownfall.mk1",
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
                nullstar,
                "fire.profile-nullstar",
                "nullstar.mk1",
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
        public void Ironwake_UsesCatalogProjectileCountAndRealSpread()
        {
            EquipmentInstance ironwake = Equipment(
                "equipment-instance.ironwake-spread",
                "equipment-definition.ironwake");
            Harness harness = CreateHarness(ironwake);

            InventoryWeaponExecutionResult result = harness.Adapter.TryExecute(
                Request(ironwake, "fire.ironwake-spread", 0L, 4421UL));

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
        public void Rattler_DoesNotAccidentallyUseIronwakeSpread()
        {
            EquipmentInstance rattler = Equipment(
                "equipment-instance.rattler-single",
                "equipment-definition.rattler");
            Harness harness = CreateHarness(rattler);

            InventoryWeaponExecutionResult result = harness.Adapter.TryExecute(
                Request(rattler, "fire.rattler-single", 0L));

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
                "equipment-instance.rattler-a",
                "equipment-definition.rattler");
            EquipmentInstance second = Equipment(
                "equipment-instance.rattler-b",
                "equipment-definition.rattler");
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

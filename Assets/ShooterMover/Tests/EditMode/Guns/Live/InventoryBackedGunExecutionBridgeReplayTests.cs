using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Guns.Live;

namespace ShooterMover.Tests.EditMode.Guns.Live
{
    public sealed partial class InventoryBackedGunExecutionBridgeTests
    {
        [Test]
        public void OperationReplay_RemainsGlobalWhenActiveEquipmentChanges()
        {
            EquipmentInstance first = Equipment(
                "equipment-instance.replay-a",
                "equipment-definition.rattler");
            EquipmentInstance second = Equipment(
                "equipment-instance.replay-b",
                "equipment-definition.ironwake");
            Harness harness = CreateHarness(first, second);
            var active = new MutableActiveGunSource(first);
            var factory = new InventoryGunFireIntentFactory(active);
            InventoryGunFireRequest original = CreateIntent(
                factory,
                "fire.cross-equipment",
                0L);

            Assert.That(harness.Adapter.TryExecute(original).Succeeded, Is.True);
            active.Set(second);

            InventoryGunExecutionResult exactReplay =
                harness.Adapter.TryExecute(original);
            InventoryGunFireRequest conflicting = CreateIntent(
                factory,
                "fire.cross-equipment",
                0L);
            InventoryGunExecutionResult conflict =
                harness.Adapter.TryExecute(conflicting);

            Assert.That(
                original.EquipmentInstanceId.Value,
                Is.EqualTo(first.InstanceId));
            Assert.That(exactReplay.Status, Is.EqualTo(GunExecutionStatus.ReplayAccepted));
            Assert.That(
                exactReplay.EquipmentInstanceId.Value,
                Is.EqualTo(first.InstanceId));
            Assert.That(conflict.Status, Is.EqualTo(GunExecutionStatus.ConflictingDuplicate));
            Assert.That(harness.Sink.Batches.Count, Is.EqualTo(1));
        }

        [Test]
        public void SameEquipmentConflictingReplay_IsRejected()
        {
            EquipmentInstance rattler = Equipment(
                "equipment-instance.conflict",
                "equipment-definition.rattler");
            Harness harness = CreateHarness(rattler);
            const string operation = "fire.same-equipment-conflict";

            Assert.That(
                harness.Adapter.TryExecute(
                    Request(rattler, operation, 0L, 10UL)).Succeeded,
                Is.True);
            Assert.That(
                harness.Adapter.TryExecute(
                    Request(rattler, operation, 1L, 10UL)).Status,
                Is.EqualTo(GunExecutionStatus.ConflictingDuplicate));
            Assert.That(harness.Sink.Batches.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnknownEquipment_FailsClosed()
        {
            EquipmentInstance rattler = Equipment(
                "equipment-instance.known",
                "equipment-definition.rattler");
            EquipmentInstance unknown = Equipment(
                "equipment-instance.unknown",
                "equipment-definition.rattler");
            Harness harness = CreateHarness(rattler);

            InventoryGunExecutionResult result = harness.Adapter.TryExecute(
                Request(unknown, "fire.unknown-equipment", 0L));

            Assert.That(
                result.Status,
                Is.EqualTo(GunExecutionStatus.MissingEquippedEquipment));
            Assert.That(harness.Sink.Batches, Is.Empty);
        }

        [Test]
        public void Nullstar_DotAndPoolAreCanonicalCoreEffects()
        {
            EquipmentInstance nullstar = Equipment(
                "equipment-instance.nullstar-core",
                "equipment-definition.nullstar");
            Harness harness = CreateHarness(nullstar);

            InventoryGunExecutionResult result = harness.Adapter.TryExecute(
                Request(nullstar, "fire.nullstar-core", 0L));

            Assert.That(result.Status, Is.EqualTo(GunExecutionStatus.Accepted));
            Assert.That(result.EffectBatch.CoreBatch.EffectCount, Is.EqualTo(4));
            var effect = (DamageOverTimeProjectileEffect)
                result.EffectBatch.CoreBatch.Effects[0];
            Assert.That(effect.DotDps, Is.EqualTo(4d));
            Assert.That(effect.DotDuration, Is.EqualTo(2d));
            Assert.That(effect.PoolRadius, Is.EqualTo(2d));
            Assert.That(effect.PoolDuration, Is.EqualTo(3d));
            Assert.That(
                result.EffectBatch.CoreBatch.CanonicalText,
                Does.Contain("|4|2|2|3|"));
        }

        [Test]
        public void HoldingsLookup_ExportsOnlyWhenAuthoritySequenceChanges()
        {
            EquipmentInstance first = Equipment(
                "equipment-instance.cache-a",
                "equipment-definition.rattler");
            EquipmentInstance second = Equipment(
                "equipment-instance.cache-b",
                "equipment-definition.ironwake");
            PlayerHoldingsActions service = CreateHoldingsService();
            AddEquipment(service, first, 0L);
            var counted = new CountingHoldingsState(service);
            var lookup = new PlayerHoldingsEquipmentInstanceLookup(counted);
            EquipmentInstance resolved;

            Assert.That(
                lookup.TryResolve(new EquipmentInstanceId(first.InstanceId), out resolved),
                Is.True);
            Assert.That(
                lookup.TryResolve(new EquipmentInstanceId(first.InstanceId), out resolved),
                Is.True);
            Assert.That(counted.ExportCount, Is.EqualTo(1));

            AddEquipment(service, second, 1L);
            Assert.That(
                lookup.TryResolve(new EquipmentInstanceId(second.InstanceId), out resolved),
                Is.True);
            Assert.That(counted.ExportCount, Is.EqualTo(2));
        }

        private static void AssertProfile(
            Harness harness,
            EquipmentInstance equipment,
            string operation,
            string definitionId,
            double fireRate,
            int projectileCount,
            double spread,
            double speed,
            double range,
            double damage,
            int pierce,
            double dotDps,
            double dotDuration,
            int cooldownTicks)
        {
            InventoryGunExecutionResult result = harness.Adapter.TryExecute(
                Request(equipment, operation, 0L));
            Assert.That(result.Status, Is.EqualTo(GunExecutionStatus.Accepted));
            Assert.That(result.GunDefinitionId.Value, Is.EqualTo(definitionId));
            Assert.That(result.EffectBatch.Profile.FireRate, Is.EqualTo(fireRate));
            Assert.That(result.EffectBatch.Profile.ProjectileCount, Is.EqualTo(projectileCount));
            Assert.That(result.EffectBatch.Profile.SpreadDegrees, Is.EqualTo(spread));
            Assert.That(result.EffectBatch.Profile.ProjectileSpeed, Is.EqualTo(speed));
            Assert.That(result.EffectBatch.Profile.Range, Is.EqualTo(range));
            Assert.That(result.EffectBatch.Profile.DirectDamagePerProjectile, Is.EqualTo(damage));
            Assert.That(result.EffectBatch.Profile.Pierce, Is.EqualTo(pierce));
            Assert.That(result.EffectBatch.Profile.DamageOverTimePerSecond, Is.EqualTo(dotDps));
            Assert.That(result.EffectBatch.Profile.DamageOverTimeDuration, Is.EqualTo(dotDuration));
            Assert.That(result.EffectBatch.Profile.CooldownTicks, Is.EqualTo(cooldownTicks));
        }

    }
}

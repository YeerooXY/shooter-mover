using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Production.Stage1.Weapons;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed class Stage1WeaponExecutionV1Tests
    {
        [Test]
        public void Registry_AcceptsUniqueExecutors_AndResolvesDeterministically()
        {
            var sink = new RecordingSink();
            var registry = Stage1WeaponCompositionV1.CreateDefault(sink);

            Assert.That(registry.Count, Is.EqualTo(4));
            AssertResolves<BlasterMachineGunExecutorV1>(registry, "weapon.blaster-machine-gun");
            AssertResolves<ShotgunWeaponExecutorV1>(registry, "weapon.shotgun");
            AssertResolves<RocketLauncherWeaponExecutorV1>(registry, "weapon.rocket-launcher");
            AssertResolves<ArcGunWeaponExecutorV1>(registry, "weapon.arc-gun");
        }

        [Test]
        public void Registry_RejectsDuplicateRuntimeWeaponRegistration()
        {
            var sink = new RecordingSink();
            var registry = new Stage1WeaponExecutionRegistryV1();
            registry.Register(new BlasterMachineGunExecutorV1(sink));

            Assert.Throws<InvalidOperationException>(
                () => registry.Register(new BlasterMachineGunExecutorV1(sink)));
        }

        [Test]
        public void Dispatcher_UnknownRuntimeWeapon_FailsClosedWithoutEffect()
        {
            var sink = new RecordingSink();
            var dispatcher = new Stage1WeaponExecutionDispatcherV1(
                Stage1WeaponCompositionV1.CreateDefault(sink));

            Stage1WeaponExecutionResultV1 result = dispatcher.TryExecute(
                Request("operation.unknown", "weapon.not-registered", new object(), 1d));

            Assert.That(result.Status, Is.EqualTo(
                Stage1WeaponExecutionStatusV1.UnknownRuntimeWeapon));
            Assert.That(sink.Requests, Is.Empty);
        }

        [Test]
        public void Shotgun_ProducesConfiguredSpreadPellets_AndPreservesEquipmentIdentity()
        {
            var sink = new RecordingSink();
            var dispatcher = new Stage1WeaponExecutionDispatcherV1(
                Stage1WeaponCompositionV1.CreateDefault(sink));
            var exactEquipmentInstance = new object();

            Stage1WeaponExecutionResultV1 result = dispatcher.TryExecute(
                Request("operation.shotgun", "weapon.shotgun", exactEquipmentInstance, 1d));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.EffectRequestCount, Is.EqualTo(7));
            Assert.That(sink.Requests.Count, Is.EqualTo(7));
            Assert.That(sink.Requests[0].Direction, Is.Not.EqualTo(sink.Requests[6].Direction));
            Assert.That(sink.Requests[0].EquipmentInstance, Is.SameAs(exactEquipmentInstance));
        }

        [Test]
        public void Rocket_ProducesDistinctAreaDamageProjectileRequest()
        {
            var sink = new RecordingSink();
            var dispatcher = new Stage1WeaponExecutionDispatcherV1(
                Stage1WeaponCompositionV1.CreateDefault(sink));

            Stage1WeaponExecutionResultV1 result = dispatcher.TryExecute(
                Request("operation.rocket", "weapon.rocket-launcher", new object(), 1d));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.Requests.Count, Is.EqualTo(1));
            Assert.That(sink.Requests[0].ProjectileSpeed, Is.EqualTo(8f));
            Assert.That(sink.Requests[0].AreaDamage, Is.GreaterThan(0f));
            Assert.That(sink.Requests[0].ExplosionRadius, Is.GreaterThan(0f));
        }

        [Test]
        public void ArcGun_ProducesArcRequestWithConfigurableChainSettings()
        {
            var sink = new RecordingSink();
            var dispatcher = new Stage1WeaponExecutionDispatcherV1(
                Stage1WeaponCompositionV1.CreateDefault(sink));

            Stage1WeaponExecutionResultV1 result = dispatcher.TryExecute(
                Request("operation.arc", "weapon.arc-gun", new object(), 1d));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.Requests[0].Kind, Is.EqualTo(Stage1WeaponEffectKindV1.Arc));
            Assert.That(sink.Requests[0].ChainCount, Is.EqualTo(3));
            Assert.That(sink.Requests[0].ChainRange, Is.EqualTo(5f));
        }

        [Test]
        public void DuplicateOperation_DoesNotCreateDuplicateEffects()
        {
            var sink = new RecordingSink();
            var dispatcher = new Stage1WeaponExecutionDispatcherV1(
                Stage1WeaponCompositionV1.CreateDefault(sink));
            Stage1WeaponExecutionRequestV1 request =
                Request("operation.duplicate", "weapon.blaster-machine-gun", new object(), 1d);

            Assert.That(dispatcher.TryExecute(request).Succeeded, Is.True);
            Stage1WeaponExecutionResultV1 duplicate = dispatcher.TryExecute(request);

            Assert.That(duplicate.Status, Is.EqualTo(
                Stage1WeaponExecutionStatusV1.DuplicateOperation));
            Assert.That(sink.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void RejectedEffectSink_DoesNotCommitCooldownOrDuplicateState()
        {
            var sink = new RecordingSink { Accept = false };
            var dispatcher = new Stage1WeaponExecutionDispatcherV1(
                Stage1WeaponCompositionV1.CreateDefault(sink));
            Stage1WeaponExecutionRequestV1 request =
                Request("operation.retryable", "weapon.blaster-machine-gun", new object(), 1d);

            Assert.That(dispatcher.TryExecute(request).Status, Is.EqualTo(
                Stage1WeaponExecutionStatusV1.ExecutorRejected));
            sink.Accept = true;

            Assert.That(dispatcher.TryExecute(request).Succeeded, Is.True);
        }

        [Test]
        public void FifthWeapon_RegistersAndExecutesWithoutDispatcherChanges()
        {
            var sink = new RecordingSink();
            var registry = Stage1WeaponCompositionV1.CreateDefault(sink);
            registry.Register(new TestBurstWeaponExecutor(sink));
            var dispatcher = new Stage1WeaponExecutionDispatcherV1(registry);

            Stage1WeaponExecutionResultV1 result = dispatcher.TryExecute(
                Request("operation.test-burst", "weapon.test-burst", new object(), 1d));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(registry.Count, Is.EqualTo(5));
            Assert.That(sink.Requests.Count, Is.EqualTo(3));
        }

        [Test]
        public void ResetTransientState_ClearsCooldownAndDuplicateProtection()
        {
            var sink = new RecordingSink();
            var dispatcher = new Stage1WeaponExecutionDispatcherV1(
                Stage1WeaponCompositionV1.CreateDefault(sink));
            Stage1WeaponExecutionRequestV1 request =
                Request("operation.restart", "weapon.blaster-machine-gun", new object(), 1d);

            Assert.That(dispatcher.TryExecute(request).Succeeded, Is.True);
            dispatcher.ResetTransientState();
            Assert.That(dispatcher.TryExecute(request).Succeeded, Is.True);
            Assert.That(sink.Requests.Count, Is.EqualTo(2));
        }

        private static void AssertResolves<T>(
            Stage1WeaponExecutionRegistryV1 registry,
            string stableId)
        {
            IStage1WeaponExecutorV1 executor;
            Assert.That(registry.TryResolve(StableId.Parse(stableId), out executor), Is.True);
            Assert.That(executor, Is.TypeOf<T>());
        }

        private static Stage1WeaponExecutionRequestV1 Request(
            string operationId,
            string weaponId,
            object equipmentInstance,
            double timestamp)
        {
            return new Stage1WeaponExecutionRequestV1(
                StableId.Parse(operationId),
                equipmentInstance,
                StableId.Parse(weaponId),
                Vector3.zero,
                Vector3.right,
                null,
                timestamp);
        }

        private sealed class RecordingSink : IStage1WeaponEffectSinkV1
        {
            public readonly List<Stage1WeaponEffectRequestV1> Requests =
                new List<Stage1WeaponEffectRequestV1>();
            public bool Accept = true;

            public bool TryRequest(Stage1WeaponEffectRequestV1 request)
            {
                if (!Accept)
                {
                    return false;
                }

                Requests.Add(request);
                return true;
            }
        }

        private sealed class TestBurstWeaponExecutor : Stage1ConfiguredWeaponExecutorV1
        {
            private static readonly StableId TestWeaponStableId =
                StableId.Parse("weapon.test-burst");

            public TestBurstWeaponExecutor(IStage1WeaponEffectSinkV1 sink)
                : base(TestWeaponStableId, new Stage1WeaponTuningV1(
                    0.25d, 3, 4f, 20f, 1f, 1f, 0f, 0f, 0, 0f), sink)
            {
            }
        }
    }
}

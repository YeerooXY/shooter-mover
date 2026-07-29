using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Guns.Live;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Guns.Live
{
    public sealed partial class InventoryGunLivePlayModeTests
    {
        [UnityTest]
        public IEnumerator ConcurrentMountsExecuteTogetherFromDistinctPhysicalOrigins()
        {
            EquipmentInstance[] equipment =
            {
                Equipment(
                    "equipment-instance.concurrent-rattler",
                    "equipment-definition.rattler"),
                Equipment(
                    "equipment-instance.concurrent-crownfall",
                    "equipment-definition.crownfall"),
            };
            var emitterObject = new GameObject(
                "InventoryGunConcurrentMounts_Test");
            var emitter = emitterObject
                .AddComponent<GunEffectEmitter>();
            var actor = new FixedActorSource();
            var adapter = new InventoryBackedGunExecutionBridge(
                new InMemoryEquipmentLookup(equipment),
                EquipmentCatalogFor(equipment),
                GunCatalogFor(),
                actor,
                emitter,
                60);
            var runtime = new InventoryGunLiveSetup(
                actor,
                new[]
                {
                    new InventoryGunMountedLive(
                        StableId.Parse("gun-mount.outer-left"),
                        new EquipmentInstanceId(
                            equipment[0].InstanceId),
                        -1d),
                    new InventoryGunMountedLive(
                        StableId.Parse("gun-mount.outer-right"),
                        new EquipmentInstanceId(
                            equipment[1].InstanceId),
                        1d),
                },
                adapter);
            var fixture = new Fixture(
                emitterObject,
                emitter,
                runtime);

            try
            {
                var operation = new FireOperationId(
                    StableId.Parse("fire.concurrent-mounts"));
                InventoryGunExecutionResult result =
                    runtime.TryFire(
                        operation,
                        0L,
                        51UL,
                        new GunVector2(10d, 20d),
                        new GunVector2(1d, 0d));

                Assert.That(
                    result.Status,
                    Is.EqualTo(GunExecutionStatus.Accepted));
                Assert.That(runtime.IsConcurrentMountMode, Is.True);
                Assert.That(runtime.EnabledMountCount, Is.EqualTo(2));
                Assert.That(emitter.EmittedEffects.Count, Is.EqualTo(2));

                GunEffect leftInstance =
                    emitter.EmittedEffects[0];
                GunEffect rightInstance =
                    emitter.EmittedEffects[1];
                var left = leftInstance.Description
                    as DirectProjectileEffect;
                var right = rightInstance.Description
                    as ExplosiveProjectileEffect;
                Assert.That(left, Is.Not.Null);
                Assert.That(right, Is.Not.Null);
                Assert.That(left.Origin.X, Is.EqualTo(10d));
                Assert.That(left.Origin.Y, Is.EqualTo(19d));
                Assert.That(right.Origin.X, Is.EqualTo(10d));
                Assert.That(right.Origin.Y, Is.EqualTo(21d));
                Assert.That(
                    left.Identity.FireOperationId,
                    Is.Not.EqualTo(right.Identity.FireOperationId));
                Assert.That(leftInstance.IsLaunched, Is.True);
                Assert.That(rightInstance.IsLaunched, Is.True);

                Vector2 leftStart = leftInstance.transform.position;
                Vector2 rightStart = rightInstance.transform.position;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                Assert.That(
                    leftInstance.transform.position.x,
                    Is.GreaterThan(leftStart.x + 0.1f));
                Assert.That(
                    rightInstance.transform.position.x,
                    Is.GreaterThan(rightStart.x + 0.1f));
                Assert.That(
                    leftInstance.transform.position.y,
                    Is.EqualTo(19f).Within(0.05f));
                Assert.That(
                    rightInstance.transform.position.y,
                    Is.EqualTo(21f).Within(0.05f));
                Assert.That(
                    rightInstance.transform.position.y
                        - leftInstance.transform.position.y,
                    Is.EqualTo(2f).Within(0.1f));

                Assert.That(
                    runtime.SelectSlot(3),
                    Is.EqualTo(
                        InventoryGunSlotSelectionStatus
                            .ExactDuplicateNoChange));

                InventoryGunExecutionResult replay =
                    runtime.TryFire(
                        operation,
                        0L,
                        51UL,
                        new GunVector2(10d, 20d),
                        new GunVector2(1d, 0d));
                Assert.That(
                    replay.Status,
                    Is.EqualTo(GunExecutionStatus.ReplayAccepted));
                Assert.That(emitter.EmittedEffects.Count, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }
    }
}

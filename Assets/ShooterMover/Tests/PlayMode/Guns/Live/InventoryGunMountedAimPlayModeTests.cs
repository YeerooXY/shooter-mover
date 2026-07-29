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
        public IEnumerator MountedMuzzlesConvergeOnOneLockedTargetPoint()
        {
            EquipmentInstance[] equipment =
            {
                Equipment(
                    "equipment-instance.aim-rattler",
                    "equipment-definition.rattler"),
                Equipment(
                    "equipment-instance.aim-crownfall",
                    "equipment-definition.crownfall"),
            };
            var emitterObject = new GameObject(
                "InventoryGunMountedAim_Test");
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
                        -0.9d),
                    new InventoryGunMountedLive(
                        StableId.Parse("gun-mount.outer-right"),
                        new EquipmentInstanceId(
                            equipment[1].InstanceId),
                        0.9d),
                },
                adapter);
            var fixture = new Fixture(
                emitterObject,
                emitter,
                runtime);

            try
            {
                var operation = new FireOperationId(
                    StableId.Parse("fire.mounted-target-convergence"));
                var target = new GunVector2(10d, 0d);
                InventoryGunExecutionResult result =
                    runtime.TryFireAtTarget(
                        operation,
                        0L,
                        71UL,
                        new GunVector2(0d, 0d),
                        target);

                Assert.That(
                    result.Status,
                    Is.EqualTo(GunExecutionStatus.Accepted));
                Assert.That(emitter.EmittedEffects.Count, Is.EqualTo(2));

                DirectProjectileEffect left = emitter.EmittedEffects[0]
                    .Description as DirectProjectileEffect;
                ExplosiveProjectileEffect right = emitter.EmittedEffects[1]
                    .Description as ExplosiveProjectileEffect;
                Assert.That(left, Is.Not.Null);
                Assert.That(right, Is.Not.Null);

                Assert.That(left.Origin.X, Is.EqualTo(0d).Within(0.0001d));
                Assert.That(left.Origin.Y, Is.EqualTo(-0.45d).Within(0.0001d));
                Assert.That(right.Origin.X, Is.EqualTo(0d).Within(0.0001d));
                Assert.That(right.Origin.Y, Is.EqualTo(0.45d).Within(0.0001d));
                Assert.That(left.Direction.Y, Is.GreaterThan(0d));
                Assert.That(right.Direction.Y, Is.LessThan(0d));

                AssertPointsAtTarget(
                    left.Origin,
                    left.Direction,
                    target);
                AssertPointsAtTarget(
                    right.Origin,
                    right.Direction,
                    target);

                Vector2 leftStart = emitter.EmittedEffects[0]
                    .transform.position;
                Vector2 rightStart = emitter.EmittedEffects[1]
                    .transform.position;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                Assert.That(
                    emitter.EmittedEffects[0].transform.position.x,
                    Is.GreaterThan(leftStart.x + 0.1f));
                Assert.That(
                    emitter.EmittedEffects[1].transform.position.x,
                    Is.GreaterThan(rightStart.x + 0.1f));
                Assert.That(
                    emitter.EmittedEffects[0].transform.position.y,
                    Is.GreaterThan(leftStart.y));
                Assert.That(
                    emitter.EmittedEffects[1].transform.position.y,
                    Is.LessThan(rightStart.y));

                InventoryGunExecutionResult replay =
                    runtime.TryFireAtTarget(
                        operation,
                        0L,
                        71UL,
                        new GunVector2(0d, 0d),
                        target);
                Assert.That(
                    replay.Status,
                    Is.EqualTo(
                        GunExecutionStatus.ReplayAccepted));
                Assert.That(emitter.EmittedEffects.Count, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void AssertPointsAtTarget(
            GunVector2 origin,
            GunVector2 direction,
            GunVector2 target)
        {
            double deltaX = target.X - origin.X;
            double deltaY = target.Y - origin.Y;
            double distance = System.Math.Sqrt(
                (deltaX * deltaX) + (deltaY * deltaY));
            Assert.That(
                origin.X + (direction.X * distance),
                Is.EqualTo(target.X).Within(0.0001d));
            Assert.That(
                origin.Y + (direction.Y * distance),
                Is.EqualTo(target.Y).Within(0.0001d));
        }
    }
}

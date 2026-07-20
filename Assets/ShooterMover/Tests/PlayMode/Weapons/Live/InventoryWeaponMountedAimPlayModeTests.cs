using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Weapons.Live
{
    public sealed partial class InventoryWeaponRuntimePlayModeTests
    {
        [UnityTest]
        public IEnumerator MountedMuzzlesConvergeOnOneLockedTargetPoint()
        {
            EquipmentInstance[] equipment =
            {
                Equipment(
                    "equipment-instance.aim-blaster",
                    "equipment-definition.blaster"),
                Equipment(
                    "equipment-instance.aim-rocket",
                    "equipment-definition.rocket"),
            };
            var emitterObject = new GameObject(
                "InventoryWeaponMountedAim_Test");
            var emitter = emitterObject
                .AddComponent<InventoryWeaponEffectEmitter2D>();
            var actor = new FixedActorSource();
            var adapter = new InventoryBackedWeaponExecutionAdapter(
                new InMemoryEquipmentLookup(equipment),
                EquipmentCatalogFor(equipment),
                WeaponCatalogFor(),
                actor,
                emitter,
                60);
            var runtime = new InventoryWeaponRuntimeComposition(
                actor,
                new[]
                {
                    new InventoryWeaponMountedRuntimeV1(
                        StableId.Parse("weapon-mount.outer-left"),
                        new EquipmentInstanceId(
                            equipment[0].InstanceId),
                        -0.9d),
                    new InventoryWeaponMountedRuntimeV1(
                        StableId.Parse("weapon-mount.outer-right"),
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
                var target = new WeaponVector2(10d, 0d);
                InventoryWeaponExecutionResult result =
                    runtime.TryFireAtTarget(
                        operation,
                        0L,
                        71UL,
                        new WeaponVector2(0d, 0d),
                        target);

                Assert.That(
                    result.Status,
                    Is.EqualTo(WeaponExecutionStatus.Accepted));
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

                InventoryWeaponExecutionResult replay =
                    runtime.TryFireAtTarget(
                        operation,
                        0L,
                        71UL,
                        new WeaponVector2(0d, 0d),
                        target);
                Assert.That(
                    replay.Status,
                    Is.EqualTo(
                        WeaponExecutionStatus.ReplayAccepted));
                Assert.That(emitter.EmittedEffects.Count, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void AssertPointsAtTarget(
            WeaponVector2 origin,
            WeaponVector2 direction,
            WeaponVector2 target)
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

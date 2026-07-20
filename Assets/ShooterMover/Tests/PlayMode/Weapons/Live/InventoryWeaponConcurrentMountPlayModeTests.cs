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
        public IEnumerator ConcurrentMountsExecuteTogetherFromDistinctPhysicalOrigins()
        {
            EquipmentInstance[] equipment =
            {
                Equipment(
                    "equipment-instance.concurrent-blaster",
                    "equipment-definition.blaster"),
                Equipment(
                    "equipment-instance.concurrent-rocket",
                    "equipment-definition.rocket"),
            };
            var emitterObject = new GameObject(
                "InventoryWeaponConcurrentMounts_Test");
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
                        -1d),
                    new InventoryWeaponMountedRuntimeV1(
                        StableId.Parse("weapon-mount.outer-right"),
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
                InventoryWeaponExecutionResult result =
                    runtime.TryFire(
                        operation,
                        0L,
                        51UL,
                        new WeaponVector2(10d, 20d),
                        new WeaponVector2(1d, 0d));

                Assert.That(
                    result.Status,
                    Is.EqualTo(WeaponExecutionStatus.Accepted));
                Assert.That(runtime.IsConcurrentMountMode, Is.True);
                Assert.That(runtime.EnabledMountCount, Is.EqualTo(2));
                Assert.That(emitter.EmittedEffects.Count, Is.EqualTo(2));

                var left = emitter.EmittedEffects[0].Description
                    as DirectProjectileEffect;
                var right = emitter.EmittedEffects[1].Description
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

                Assert.That(
                    runtime.SelectSlot(3),
                    Is.EqualTo(
                        InventoryWeaponSlotSelectionStatus
                            .ExactDuplicateNoChange));

                InventoryWeaponExecutionResult replay =
                    runtime.TryFire(
                        operation,
                        0L,
                        51UL,
                        new WeaponVector2(10d, 20d),
                        new WeaponVector2(1d, 0d));
                Assert.That(
                    replay.Status,
                    Is.EqualTo(WeaponExecutionStatus.ReplayAccepted));
                Assert.That(emitter.EmittedEffects.Count, Is.EqualTo(2));
                yield return null;
            }
            finally
            {
                fixture.Dispose();
            }
        }
    }
}

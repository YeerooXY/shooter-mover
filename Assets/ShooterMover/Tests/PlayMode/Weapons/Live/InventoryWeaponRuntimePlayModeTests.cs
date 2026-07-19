using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Weapons.Live
{
    public sealed partial class InventoryWeaponRuntimePlayModeTests
    {
        private static readonly StableId ActorId =
            StableId.Parse("actor.playmode-player");
        private static readonly StableId ParticipantId =
            StableId.Parse("participant.playmode-player");
        private static readonly StableId QualityId =
            StableId.Parse("quality.common");

        [UnityTest]
        public IEnumerator RouteSlotSelection_ChangesPhysicalEmittedEffects()
        {
            Fixture fixture = CreateFixture();
            try
            {
                InventoryWeaponExecutionResult blaster = fixture.Runtime.TryFire(
                    new FireOperationId(StableId.Parse("fire.playmode-blaster")),
                    0L,
                    10UL,
                    new WeaponVector2(0d, 0d),
                    new WeaponVector2(1d, 0d));
                Assert.That(blaster.Status, Is.EqualTo(WeaponExecutionStatus.Accepted));
                Assert.That(fixture.Emitter.EmittedEffects.Count, Is.EqualTo(1));
                Assert.That(
                    fixture.Emitter.EmittedEffects[0].Description,
                    Is.TypeOf<DirectProjectileEffect>());

                Assert.That(
                    fixture.Runtime.SelectSlot(1),
                    Is.EqualTo(InventoryWeaponSlotSelectionStatus.Selected));
                InventoryWeaponExecutionResult shotgun = fixture.Runtime.TryFire(
                    new FireOperationId(StableId.Parse("fire.playmode-shotgun")),
                    0L,
                    11UL,
                    new WeaponVector2(0d, 0d),
                    new WeaponVector2(1d, 0d));

                Assert.That(shotgun.Status, Is.EqualTo(WeaponExecutionStatus.Accepted));
                Assert.That(shotgun.EffectBatch.CoreBatch.EffectCount, Is.EqualTo(7));
                Assert.That(fixture.Emitter.EmittedEffects.Count, Is.EqualTo(8));
                Assert.That(
                    fixture.Emitter.EmittedEffects[1].Description.Identity.WeaponDefinitionId.Value,
                    Is.EqualTo("weapon.shotgun"));
                yield return null;
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Flamethrower_EmitsCanonicalPersistentDamagePool()
        {
            Fixture fixture = CreateFixture();
            try
            {
                fixture.Runtime.SelectSlot(3);
                InventoryWeaponExecutionResult result = fixture.Runtime.TryFire(
                    new FireOperationId(StableId.Parse("fire.playmode-flamethrower")),
                    0L,
                    12UL,
                    new WeaponVector2(0d, 0d),
                    new WeaponVector2(1d, 0d));

                Assert.That(result.Status, Is.EqualTo(WeaponExecutionStatus.Accepted));
                Assert.That(
                    result.EffectBatch.CoreBatch.Effects[0],
                    Is.TypeOf<DamageOverTimeProjectileEffect>());

                yield return new WaitForSeconds(0.9f);

                InventoryWeaponPersistentDamageArea2D[] pools =
                    UnityEngine.Object.FindObjectsByType<InventoryWeaponPersistentDamageArea2D>(
                        FindObjectsSortMode.None);
                Assert.That(pools.Length, Is.EqualTo(4));
                Assert.That(pools[0].DamagePerSecond, Is.EqualTo(4d));
                Assert.That(pools[0].Radius, Is.EqualTo(2d));
                for (int index = 0; index < pools.Length; index++)
                {
                    UnityEngine.Object.DestroyImmediate(pools[index].gameObject);
                }
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static Fixture CreateFixture()
        {
            EquipmentInstance[] equipment =
            {
                Equipment("equipment-instance.play-blaster", "equipment-definition.blaster"),
                Equipment("equipment-instance.play-shotgun", "equipment-definition.shotgun"),
                Equipment("equipment-instance.play-rocket", "equipment-definition.rocket"),
                Equipment("equipment-instance.play-flame", "equipment-definition.flamethrower"),
            };
            PlayerRouteProfilePayloadV1 route = PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character.playmode-player"),
                StableId.Parse("loadout.playmode-player"),
                new[]
                {
                    equipment[0].InstanceId,
                    equipment[1].InstanceId,
                    equipment[2].InstanceId,
                    equipment[3].InstanceId,
                });
            var emitterObject = new GameObject("InventoryWeaponEffectEmitter2D_Test");
            var emitter = emitterObject.AddComponent<InventoryWeaponEffectEmitter2D>();
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
                new RouteProfileActiveWeaponSource(route),
                adapter);
            return new Fixture(emitterObject, emitter, runtime);
        }

    }
}

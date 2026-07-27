using NUnit.Framework;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;

namespace ShooterMover.Tests.EditMode
{
    public sealed class CanonicalWeaponProjectileSourceIdentityTests
    {
        [Test]
        public void ExactSourceReplayIsIdempotentAndConflictingMountFailsClosed()
        {
            GameObject owner = new GameObject("canonical-projectile-source-test");
            try
            {
                CanonicalProjectileSourceIdentity2D identity =
                    owner.AddComponent<CanonicalProjectileSourceIdentity2D>();
                var actorId = new WeaponActorInstanceId(
                    StableId.Parse("character.test-canonical-source"));
                var lifecycle = new LifecycleGeneration(7L);
                StableId mountId = StableId.Parse("weapon-mount.test-primary");
                var equipmentId = new EquipmentInstanceId(
                    StableId.Parse("equipment-instance.test-canonical-source"));
                var definitionId = new WeaponDefinitionId("rattler.mk1");

                Assert.That(
                    identity.TryBind(
                        actorId,
                        lifecycle,
                        mountId,
                        equipmentId,
                        definitionId),
                    Is.True);
                Assert.That(
                    identity.TryBind(
                        actorId,
                        lifecycle,
                        mountId,
                        equipmentId,
                        definitionId),
                    Is.True,
                    "An exact presentation replay must not create a second authority.");
                Assert.That(
                    identity.TryBind(
                        actorId,
                        lifecycle,
                        StableId.Parse("weapon-mount.test-conflict"),
                        equipmentId,
                        definitionId),
                    Is.False,
                    "A conflicting mount identity must fail closed.");

                Assert.That(identity.ActorId, Is.EqualTo(actorId));
                Assert.That(identity.LifecycleGeneration, Is.EqualTo(lifecycle));
                Assert.That(identity.MountStableId, Is.EqualTo(mountId));
                Assert.That(identity.EquipmentInstanceId, Is.EqualTo(equipmentId));
                Assert.That(identity.WeaponDefinitionId, Is.EqualTo(definitionId));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void EffectSinkRejectsDeliveryBeforeExactSourceBinding()
        {
            GameObject owner = new GameObject("canonical-projectile-sink-test");
            try
            {
                ProductionCanonicalProjectileEffectSink2D sink =
                    owner.AddComponent<ProductionCanonicalProjectileEffectSink2D>();

                WeaponEffectBatchSinkResult result = sink.TryAccept(null);

                Assert.That(result, Is.Not.Null);
                Assert.That(result.IsAcceptance, Is.False);
                Assert.That(
                    result.RejectionCode,
                    Is.EqualTo("canonical-projectile-source-unbound"));
                Assert.That(sink.AcceptedBatchCount, Is.Zero);
                Assert.That(sink.ActiveProjectileCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}

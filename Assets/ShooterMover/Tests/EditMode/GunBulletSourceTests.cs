using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Guns.Live;
using UnityEngine;

namespace ShooterMover.Tests.EditMode
{
    public sealed class GunBulletSourceTests
    {
        private sealed class ExactBlueprintResolver :
            IGunMappingPolicyResolver,
            IGunResolver
        {
            private readonly GunDefinitionId definitionId;
            private readonly Gun blueprint;

            internal ExactBlueprintResolver(
                GunDefinitionId exactDefinitionId,
                Gun exactBlueprint)
            {
                definitionId = exactDefinitionId;
                blueprint = exactBlueprint;
            }

            public bool TryResolve(
                GunDefinitionId requested,
                out GunCatalogBlueprintMappingIntent mappingIntent)
            {
                mappingIntent = null;
                return false;
            }

            public bool TryResolveCanonical(
                GunDefinitionId requested,
                out Gun resolved)
            {
                bool matches = requested != null
                    && definitionId != null
                    && requested.Equals(definitionId);
                resolved = matches ? blueprint : null;
                return matches;
            }
        }

        [Test]
        public void FreshCanonicalStarterResolvesWithoutLegacyReceiptOwnership()
        {
            PlayerRouteProfilePayload route =
                PlayerRouteProfilePayload.Create(
                    StableId.Parse("character.test-canonical-fire"),
                    StableId.Parse(
                        GunMountPolicy.HealerLoadoutProfileId),
                    new StableId[
                        PlayerRouteProfilePayload.GunSlotCount]);
            var runtime = new PlayerLoadoutLive(route);

            GunItem exact;
            string rejectionCode;
            Assert.That(
                runtime.TryResolveFirstActiveEquippedGun(
                    out exact,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(exact, Is.Not.Null);
            Assert.That(
                runtime.LegacyHoldings.ExportSnapshot().UniqueHoldings,
                Is.Empty,
                "Fresh starter guns must not require a generic receipt record.");

            var equipmentId = new EquipmentInstanceId(exact.InstanceId);
            EquipmentInstance legacyProjection;
            Assert.That(
                new PlayerHoldingsEquipmentInstanceLookup(runtime.Holdings)
                    .TryResolve(equipmentId, out legacyProjection),
                Is.False,
                "The retained receipt-ledger lookup cannot resolve a fresh V2 starter.");

            var canonicalLookup = new GunEquipmentViewLookup(
                runtime.GunInventory,
                runtime.EquipmentCatalog,
                runtime.Holdings);
            EquipmentInstance canonicalProjection;
            Assert.That(
                canonicalLookup.TryResolve(
                    equipmentId,
                    out canonicalProjection),
                Is.True);
            Assert.That(canonicalProjection, Is.Not.Null);
            Assert.That(
                canonicalProjection.InstanceId,
                Is.EqualTo(exact.InstanceId));

            GunMark mark;
            Assert.That(
                GunCatalogProvider.Current.TryGetMark(
                    exact.GunDefinitionId.Value,
                    out mark),
                Is.True);
            Assert.That(mark, Is.Not.Null);

            var effectiveResolver = new InventoryGunEffectiveResolver(
                runtime.EquipmentCatalog,
                runtime.GunCatalog,
                new ExactBlueprintResolver(
                    exact.GunDefinitionId,
                    mark.Blueprint),
                new UnaugmentedGunModifierSetResolver());
            EffectiveGun effective;
            Assert.That(
                effectiveResolver.TryResolve(
                    canonicalProjection,
                    out effective,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(effective, Is.Not.Null);
            Assert.That(
                effective.EquipmentInstanceId.Value,
                Is.EqualTo(exact.InstanceId));
            Assert.That(
                effective.DefinitionId,
                Is.EqualTo(exact.GunDefinitionId));
            Assert.That(effective.Blueprint, Is.SameAs(mark.Blueprint));
        }

        [Test]
        public void ExactSourceReplayIsIdempotentAndIdentityConflictsFailClosed()
        {
            GameObject owner = new GameObject("bullet-source-test");
            try
            {
                BulletSource identity =
                    owner.AddComponent<BulletSource>();
                var actorId = new GunActorInstanceId(
                    StableId.Parse("character.test-canonical-source"));
                var participantId = new RunParticipantId(
                    StableId.Parse("run-participant.test-canonical-source"));
                var lifecycle = new LifecycleGeneration(7L);
                StableId mountId = StableId.Parse("gun-mount.test-primary");
                var equipmentId = new EquipmentInstanceId(
                    StableId.Parse("equipment-instance.test-canonical-source"));
                var definitionId = new GunDefinitionId("rattler.mk1");

                Assert.That(
                    identity.TryBind(
                        actorId,
                        participantId,
                        lifecycle,
                        mountId,
                        equipmentId,
                        definitionId),
                    Is.True);
                Assert.That(
                    identity.TryBind(
                        actorId,
                        participantId,
                        lifecycle,
                        mountId,
                        equipmentId,
                        definitionId),
                    Is.True,
                    "An exact presentation replay must not create a second authority.");
                Assert.That(
                    identity.TryBind(
                        actorId,
                        new RunParticipantId(
                            StableId.Parse("run-participant.test-conflict")),
                        lifecycle,
                        mountId,
                        equipmentId,
                        definitionId),
                    Is.False,
                    "A conflicting participant identity must fail closed.");
                Assert.That(
                    identity.TryBind(
                        actorId,
                        participantId,
                        lifecycle,
                        StableId.Parse("gun-mount.test-conflict"),
                        equipmentId,
                        definitionId),
                    Is.False,
                    "A conflicting mount identity must fail closed.");

                Assert.That(identity.ActorId, Is.EqualTo(actorId));
                Assert.That(identity.ParticipantId, Is.EqualTo(participantId));
                Assert.That(identity.LifecycleGeneration, Is.EqualTo(lifecycle));
                Assert.That(identity.MountStableId, Is.EqualTo(mountId));
                Assert.That(identity.EquipmentInstanceId, Is.EqualTo(equipmentId));
                Assert.That(identity.GunDefinitionId, Is.EqualTo(definitionId));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BulletSpawnerRejectsShotBeforeSourceBinding()
        {
            GameObject owner = new GameObject("bullet-spawner-test");
            try
            {
                BulletSpawner spawner =
                    owner.AddComponent<BulletSpawner>();

                GunEffectBatchSinkResult result = spawner.TryAccept(null);

                Assert.That(result, Is.Not.Null);
                Assert.That(result.IsAcceptance, Is.False);
                Assert.That(
                    result.RejectionCode,
                    Is.EqualTo("canonical-projectile-source-unbound"));
                Assert.That(spawner.AcceptedBatchCount, Is.Zero);
                Assert.That(spawner.ActiveBulletCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}

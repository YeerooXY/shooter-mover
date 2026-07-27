using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;

namespace ShooterMover.Tests.EditMode
{
    public sealed class CanonicalWeaponProjectileSourceIdentityTests
    {
        private sealed class ExactCanonicalBlueprintResolver :
            IWeaponBlueprintMappingPolicyResolver,
            ICanonicalWeaponBlueprintResolver
        {
            private readonly WeaponDefinitionId definitionId;
            private readonly WeaponBlueprint blueprint;

            internal ExactCanonicalBlueprintResolver(
                WeaponDefinitionId exactDefinitionId,
                WeaponBlueprint exactBlueprint)
            {
                definitionId = exactDefinitionId;
                blueprint = exactBlueprint;
            }

            public bool TryResolve(
                WeaponDefinitionId requested,
                out WeaponCatalogBlueprintMappingIntent mappingIntent)
            {
                mappingIntent = null;
                return false;
            }

            public bool TryResolveCanonical(
                WeaponDefinitionId requested,
                out WeaponBlueprint resolved)
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
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    StableId.Parse("character.test-canonical-fire"),
                    StableId.Parse(
                        ProductionWeaponMountPolicyV1.HealerLoadoutProfileId),
                    new StableId[
                        PlayerRouteProfilePayloadV1.WeaponSlotCount]);
            var runtime = new ProductionPlayerLoadoutRuntimeV1(route);

            WeaponEquipmentInstance exact;
            string rejectionCode;
            Assert.That(
                runtime.TryResolveFirstActiveEquippedWeapon(
                    out exact,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(exact, Is.Not.Null);
            Assert.That(
                runtime.LegacyHoldings.ExportSnapshot().UniqueHoldings,
                Is.Empty,
                "Fresh starter weapons must not require a generic receipt record.");

            var equipmentId = new EquipmentInstanceId(exact.InstanceId);
            EquipmentInstance legacyProjection;
            Assert.That(
                new PlayerHoldingsEquipmentInstanceLookup(runtime.Holdings)
                    .TryResolve(equipmentId, out legacyProjection),
                Is.False,
                "The retained receipt-ledger lookup cannot resolve a fresh V2 starter.");

            var canonicalLookup = new CanonicalWeaponEquipmentProjectionLookupV2(
                runtime.WeaponHoldings,
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

            ProductionWeaponMarkV1 mark;
            Assert.That(
                ProductionWeaponCatalogProvider.Current.TryGetMark(
                    exact.WeaponDefinitionId.Value,
                    out mark),
                Is.True);
            Assert.That(mark, Is.Not.Null);

            var effectiveResolver = new InventoryWeaponEffectiveResolver(
                runtime.EquipmentCatalog,
                runtime.WeaponCatalog,
                new ExactCanonicalBlueprintResolver(
                    exact.WeaponDefinitionId,
                    mark.Blueprint),
                new UnaugmentedWeaponModifierSetResolver());
            EffectiveWeapon effective;
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
                Is.EqualTo(exact.WeaponDefinitionId));
            Assert.That(effective.Blueprint, Is.SameAs(mark.Blueprint));
        }

        [Test]
        public void ExactSourceReplayIsIdempotentAndIdentityConflictsFailClosed()
        {
            GameObject owner = new GameObject("canonical-projectile-source-test");
            try
            {
                CanonicalProjectileSourceIdentity2D identity =
                    owner.AddComponent<CanonicalProjectileSourceIdentity2D>();
                var actorId = new WeaponActorInstanceId(
                    StableId.Parse("character.test-canonical-source"));
                var participantId = new RunParticipantId(
                    StableId.Parse("run-participant.test-canonical-source"));
                var lifecycle = new LifecycleGeneration(7L);
                StableId mountId = StableId.Parse("weapon-mount.test-primary");
                var equipmentId = new EquipmentInstanceId(
                    StableId.Parse("equipment-instance.test-canonical-source"));
                var definitionId = new WeaponDefinitionId("rattler.mk1");

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
                        StableId.Parse("weapon-mount.test-conflict"),
                        equipmentId,
                        definitionId),
                    Is.False,
                    "A conflicting mount identity must fail closed.");

                Assert.That(identity.ActorId, Is.EqualTo(actorId));
                Assert.That(identity.ParticipantId, Is.EqualTo(participantId));
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

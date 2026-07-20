using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Players;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Players
{
    public sealed class EnemyToPlayerDamageRouterV1Tests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < objects.Count; index++)
            {
                if (objects[index] != null)
                {
                    Object.DestroyImmediate(objects[index]);
                }
            }
            objects.Clear();
        }

        [Test]
        public void ThreeEnemySourcesUseOneRouterAndForwardConfiguredDamage()
        {
            var requests = new List<PlayerDamageRequest>();
            using (var router = new EnemyToPlayerDamageRouterV1(
                request =>
                {
                    requests.Add(request);
                    return null;
                }))
            {
                StableId targetId = StableId.Parse("actor.router-player");
                Collider2D target = CreateTarget("PlayerTarget");
                double[] damage = { 7d, 10d, 13d };

                for (int index = 0; index < damage.Length; index++)
                {
                    StableId sourceId = StableId.Create(
                        "actor",
                        "router-enemy-" + index);
                    StableId eventId = StableId.Create(
                        "projectile-hit",
                        "router-event-" + index);
                    var hitAdapter = new CombatHit2DAdapter(sourceId);

                    Assert.That(
                        hitAdapter.RegisterTarget(target, targetId),
                        Is.EqualTo(CombatHit2DTargetRegistrationStatus.Registered));
                    Assert.That(
                        router.RegisterDamageSource(hitAdapter, damage[index]),
                        Is.EqualTo(EnemyDamageSourceRegistrationStatus.Registered));
                    Assert.That(
                        router.ObserveEmission(
                            new EnemyProjectileEmissionFactV1(eventId, 4L)),
                        Is.EqualTo(
                            EnemyDamageAdmissionObservationStatus.Accepted));

                    CombatHit2DTranslationResult translated =
                        hitAdapter.TranslateConfirmedHit(
                            eventId,
                            target,
                            CombatChannel.Kinetic,
                            false);
                    Assert.That(
                        translated.Status,
                        Is.EqualTo(CombatHit2DTranslationStatus.Confirmed));
                }

                Assert.That(router.RegisteredSourceCount, Is.EqualTo(3));
                Assert.That(router.ForwardedRequestCount, Is.EqualTo(3));
                Assert.That(requests.Count, Is.EqualTo(3));
                for (int index = 0; index < requests.Count; index++)
                {
                    Assert.That(requests[index].Amount, Is.EqualTo(damage[index]));
                    Assert.That(requests[index].TargetActorId, Is.EqualTo(targetId));
                    Assert.That(requests[index].LifecycleGeneration, Is.EqualTo(4L));
                }
            }
        }

        [Test]
        public void ContactAttackUsesTheSameRouterAdmissionPath()
        {
            PlayerDamageRequest forwarded = null;
            using (var router = new EnemyToPlayerDamageRouterV1(
                request =>
                {
                    forwarded = request;
                    return null;
                }))
            {
                StableId sourceId = StableId.Parse("actor.router-contact-enemy");
                StableId targetId = StableId.Parse("actor.router-contact-player");
                StableId eventId = StableId.Parse("contact-hit.router-contact");
                Collider2D target = CreateTarget("ContactTarget");
                var hitAdapter = new CombatHit2DAdapter(sourceId);
                hitAdapter.RegisterTarget(target, targetId);
                router.RegisterDamageSource(hitAdapter, 9d);

                Assert.That(
                    router.ObserveAttack(
                        new EnemyDamageAdmissionFactV1(
                            eventId,
                            6L,
                            EnemyAttackDeliveryKind.Contact)),
                    Is.EqualTo(EnemyDamageAdmissionObservationStatus.Accepted));
                hitAdapter.TranslateConfirmedHit(
                    eventId,
                    target,
                    CombatChannel.Kinetic,
                    false);

                Assert.That(forwarded, Is.Not.Null);
                Assert.That(forwarded.Amount, Is.EqualTo(9d));
                Assert.That(forwarded.LifecycleGeneration, Is.EqualTo(6L));
            }
        }

        [Test]
        public void HitWithoutEmissionLedgerFailsClosed()
        {
            int forwarded = 0;
            using (var router = new EnemyToPlayerDamageRouterV1(
                request =>
                {
                    forwarded++;
                    return null;
                }))
            {
                StableId sourceId = StableId.Parse("actor.router-no-ledger-enemy");
                StableId targetId = StableId.Parse("actor.router-no-ledger-player");
                StableId eventId = StableId.Parse("projectile-hit.router-no-ledger");
                Collider2D target = CreateTarget("NoLedgerTarget");
                var hitAdapter = new CombatHit2DAdapter(sourceId);
                hitAdapter.RegisterTarget(target, targetId);
                router.RegisterDamageSource(hitAdapter, 10d);

                hitAdapter.TranslateConfirmedHit(
                    eventId,
                    target,
                    CombatChannel.Kinetic,
                    false);

                Assert.That(forwarded, Is.Zero);
                Assert.That(router.ForwardedRequestCount, Is.Zero);
                Assert.That(router.RejectedHitCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void LifecycleClearRejectsPreviouslyEmittedProjectile()
        {
            int forwarded = 0;
            using (var router = new EnemyToPlayerDamageRouterV1(
                request =>
                {
                    forwarded++;
                    return null;
                }))
            {
                StableId sourceId = StableId.Parse("actor.router-stale-enemy");
                StableId targetId = StableId.Parse("actor.router-stale-player");
                StableId eventId = StableId.Parse("projectile-hit.router-stale");
                Collider2D target = CreateTarget("StaleTarget");
                var hitAdapter = new CombatHit2DAdapter(sourceId);
                hitAdapter.RegisterTarget(target, targetId);
                router.RegisterDamageSource(hitAdapter, 10d);
                router.ObserveEmission(
                    new EnemyProjectileEmissionFactV1(eventId, 2L));

                router.ClearLifecycle();
                hitAdapter.TranslateConfirmedHit(
                    eventId,
                    target,
                    CombatChannel.Kinetic,
                    false);

                Assert.That(router.PendingEmissionCount, Is.Zero);
                Assert.That(forwarded, Is.Zero);
                Assert.That(router.RejectedHitCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void ConflictingEmissionGenerationRejectsWithoutReplacingFirstFact()
        {
            using (var router = new EnemyToPlayerDamageRouterV1(request => null))
            {
                StableId eventId = StableId.Parse("projectile-hit.router-conflict");
                Assert.That(
                    router.ObserveEmission(
                        new EnemyProjectileEmissionFactV1(eventId, 3L)),
                    Is.EqualTo(EnemyDamageAdmissionObservationStatus.Accepted));
                Assert.That(
                    router.ObserveEmission(
                        new EnemyProjectileEmissionFactV1(eventId, 4L)),
                    Is.EqualTo(
                        EnemyDamageAdmissionObservationStatus.ConflictingDuplicate));
                Assert.That(router.PendingEmissionCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void SourceRegistrationIsIdempotentButConflictingDamageRejects()
        {
            using (var router = new EnemyToPlayerDamageRouterV1(request => null))
            {
                var hitAdapter = new CombatHit2DAdapter(
                    StableId.Parse("actor.router-idempotent-enemy"));
                Assert.That(
                    router.RegisterDamageSource(hitAdapter, 10d),
                    Is.EqualTo(EnemyDamageSourceRegistrationStatus.Registered));
                Assert.That(
                    router.RegisterDamageSource(hitAdapter, 10d),
                    Is.EqualTo(
                        EnemyDamageSourceRegistrationStatus.AlreadyRegistered));
                Assert.That(
                    router.RegisterDamageSource(hitAdapter, 11d),
                    Is.EqualTo(
                        EnemyDamageSourceRegistrationStatus.ConflictingSource));
                Assert.That(router.RegisteredSourceCount, Is.EqualTo(1));
            }
        }

        private Collider2D CreateTarget(string name)
        {
            var owner = new GameObject(name);
            objects.Add(owner);
            return owner.AddComponent<BoxCollider2D>();
        }
    }
}

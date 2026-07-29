using NUnit.Framework;
using ShooterMover.Combat.HitPolicy;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Players;

namespace ShooterMover.Tests.EditMode.EnemyAttackPatterns
{
    public sealed class EnemyAttackPatternHitRouterTests
    {
        private static readonly StableId SourceActorId =
            Id("enemy-entity.hit-router-source");
        private static readonly StableId SourceParticipantId =
            Id("run-participant.hit-router-source");
        private static readonly StableId PlayerActorId =
            Id("player-entity.hit-router-target");
        private static readonly StableId PlayerParticipantId =
            Id("run-participant.hit-router-target");
        private static readonly StableId PlayerCharacterId =
            Id("character.hit-router-target");
        private static readonly StableId EnemyFactionId =
            Id("faction.hostile-machines");
        private static readonly StableId PlayerFactionId =
            Id("faction.player");

        private sealed class AuthoritativeContext :
            IEnemyAttackPatternCombatContext
        {
            private readonly PlayerActorState player;

            public AuthoritativeContext(PlayerActorState player)
            {
                this.player = player;
            }

            public int DamageCallCount { get; private set; }
            public bool SourceAvailable { get; set; } = true;
            public bool TargetAvailable { get; set; } = true;
            public bool TargetUsesEnemyFaction { get; set; }
            public int RemainingDamageFailures { get; set; }

            public bool TryReadSource(
                EnemyAttackEffectEmission emission,
                out CombatActorSnapshot source)
            {
                source = null;
                if (!SourceAvailable || emission == null)
                    return false;
                source = new CombatActorSnapshot(
                    SourceActorId,
                    new GameplayEntityIdentity(
                        SourceActorId,
                        GameplayEntityOwnership.Create(SourceParticipantId, null),
                        EnemyFactionId),
                    emission.SourceLifecycleGeneration,
                    true,
                    true,
                    new StableId[0]);
                return true;
            }

            public bool TryReadTarget(
                StableId targetEntityStableId,
                out CombatActorSnapshot target)
            {
                target = null;
                PlayerActorSnapshot snapshot = player.ExportSnapshot();
                if (!TargetAvailable
                    || snapshot == null
                    || targetEntityStableId != snapshot.ActorInstanceId)
                    return false;
                GameplayEntityIdentity identity = TargetUsesEnemyFaction
                    ? new GameplayEntityIdentity(
                        snapshot.ActorInstanceId,
                        snapshot.Identity.Ownership,
                        EnemyFactionId)
                    : snapshot.Identity;
                target = new CombatActorSnapshot(
                    snapshot.ActorInstanceId,
                    identity,
                    snapshot.LifecycleGeneration,
                    true,
                    snapshot.IsAlive,
                    new[] { CombatHitCapabilityIds.DamageReceiver });
                return true;
            }

            public DamageReceiverResult ApplyPlayerDamage(
                PlayerDamageRequest request)
            {
                DamageCallCount++;
                if (RemainingDamageFailures > 0)
                {
                    RemainingDamageFailures--;
                    return null;
                }
                return player.ApplyDamage(
                    new DamageReceiverCommand(
                        request.EventId,
                        request.SourceActorId,
                        request.UntrustedSourceRunParticipantId,
                        request.TargetActorId,
                        request.Amount,
                        request.Channel,
                        request.LifecycleGeneration));
            }
        }

        [Test]
        public void AcceptedProjectileRoutesThroughPolicyAndPlayerAuthorityExactlyOnce()
        {
            PlayerActorState player = Player(100d);
            var context = new AuthoritativeContext(player);
            var router = new EnemyAttackPatternHitRouter(context);
            EnemyAttackEffectEmission emission =
                ProjectileEmission("accepted-projectile", 5d);
            StableId hitId = Id("combat-event.accepted-projectile");

            EnemyAttackPatternHitRouteResult first = router.RouteActorContact(
                emission, hitId, PlayerActorId, 1L, 4d);
            EnemyAttackPatternHitRouteResult replay = router.RouteActorContact(
                emission, hitId, PlayerActorId, 1L, 4d);

            Assert.That(first.Status,
                Is.EqualTo(EnemyAttackPatternHitRouteStatus.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatus.AppliedExactReplay));
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(context.DamageCallCount, Is.EqualTo(1));
            Assert.That(player.ExportSnapshot().CurrentHealth, Is.EqualTo(95d));
        }

        [Test]
        public void ConflictingHitEventReuseRejectsWithoutAdditionalDamage()
        {
            PlayerActorState player = Player(100d);
            var context = new AuthoritativeContext(player);
            var router = new EnemyAttackPatternHitRouter(context);
            EnemyAttackEffectEmission emission =
                ProjectileEmission("conflicting-hit", 5d);
            StableId hitId = Id("combat-event.conflicting-hit");
            Assert.That(router.RouteActorContact(
                    emission, hitId, PlayerActorId, 1L, 4d).IsAccepted,
                Is.True);

            EnemyAttackPatternHitRouteResult conflict =
                router.RouteActorContact(
                    emission, hitId, PlayerActorId, 1L, 9d);

            Assert.That(conflict.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatus.ConflictingDuplicate));
            Assert.That(context.DamageCallCount, Is.EqualTo(1));
            Assert.That(player.ExportSnapshot().CurrentHealth, Is.EqualTo(95d));
        }

        [Test]
        public void FriendlyFireRejectionReplayRemainsRejected()
        {
            PlayerActorState player = Player(100d);
            var context = new AuthoritativeContext(player)
            {
                TargetUsesEnemyFaction = true,
            };
            var router = new EnemyAttackPatternHitRouter(context);
            EnemyAttackEffectEmission emission =
                ProjectileEmission("friendly-fire", 5d);
            StableId hitId = Id("combat-event.friendly-fire");

            EnemyAttackPatternHitRouteResult first = router.RouteActorContact(
                emission, hitId, PlayerActorId, 1L, 1d);
            EnemyAttackPatternHitRouteResult replay = router.RouteActorContact(
                emission, hitId, PlayerActorId, 1L, 1d);

            Assert.That(first.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatus.RejectedByPolicy));
            Assert.That(replay.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatus.RejectedByPolicy));
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.IsAccepted, Is.False);
            Assert.That(context.DamageCallCount, Is.Zero);
            Assert.That(player.ExportSnapshot().CurrentHealth, Is.EqualTo(100d));
        }

        [Test]
        public void StaleLifecycleRejectionReplayRemainsRejected()
        {
            PlayerActorState player = Player(100d);
            var context = new AuthoritativeContext(player);
            var router = new EnemyAttackPatternHitRouter(context);
            EnemyAttackEffectEmission emission =
                ProjectileEmission("stale-target", 5d);
            StableId hitId = Id("combat-event.stale-target");

            EnemyAttackPatternHitRouteResult first = router.RouteActorContact(
                emission, hitId, PlayerActorId, 2L, 1d);
            EnemyAttackPatternHitRouteResult replay = router.RouteActorContact(
                emission, hitId, PlayerActorId, 2L, 1d);

            Assert.That(first.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatus.RejectedByPolicy));
            Assert.That(replay.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatus.RejectedByPolicy));
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.IsAccepted, Is.False);
            Assert.That(context.DamageCallCount, Is.Zero);
        }

        [Test]
        public void TemporarilyUnavailableTargetContextRetriesSameHitEvent()
        {
            PlayerActorState player = Player(100d);
            var context = new AuthoritativeContext(player)
            {
                TargetAvailable = false,
            };
            var router = new EnemyAttackPatternHitRouter(context);
            EnemyAttackEffectEmission emission =
                ProjectileEmission("context-retry", 5d);
            StableId hitId = Id("combat-event.context-retry");

            EnemyAttackPatternHitRouteResult failed = router.RouteActorContact(
                emission, hitId, PlayerActorId, 1L, 1d);
            context.TargetAvailable = true;
            EnemyAttackPatternHitRouteResult applied = router.RouteActorContact(
                emission, hitId, PlayerActorId, 1L, 1d);

            Assert.That(failed.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatus.RetryableFailure));
            Assert.That(failed.IsAccepted, Is.False);
            Assert.That(applied.Status,
                Is.EqualTo(EnemyAttackPatternHitRouteStatus.Applied));
            Assert.That(applied.IsReplay, Is.False);
            Assert.That(context.DamageCallCount, Is.EqualTo(1));
            Assert.That(player.ExportSnapshot().CurrentHealth, Is.EqualTo(95d));
        }

        [Test]
        public void TemporarilyUnavailableDamageAuthorityRetriesSameHitEvent()
        {
            PlayerActorState player = Player(100d);
            var context = new AuthoritativeContext(player)
            {
                RemainingDamageFailures = 1,
            };
            var router = new EnemyAttackPatternHitRouter(context);
            EnemyAttackEffectEmission emission =
                ProjectileEmission("damage-retry", 5d);
            StableId hitId = Id("combat-event.damage-retry");

            EnemyAttackPatternHitRouteResult failed = router.RouteActorContact(
                emission, hitId, PlayerActorId, 1L, 1d);
            EnemyAttackPatternHitRouteResult applied = router.RouteActorContact(
                emission, hitId, PlayerActorId, 1L, 1d);

            Assert.That(failed.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatus.RetryableFailure));
            Assert.That(applied.Status,
                Is.EqualTo(EnemyAttackPatternHitRouteStatus.Applied));
            Assert.That(context.DamageCallCount, Is.EqualTo(2));
            Assert.That(player.ExportSnapshot().CurrentHealth, Is.EqualTo(95d));
        }

        [Test]
        public void MeleeHonorsAuthoredHitsPerTargetAndRejectsThirdHit()
        {
            PlayerActorState player = Player(100d);
            var context = new AuthoritativeContext(player);
            var router = new EnemyAttackPatternHitRouter(context);
            EnemyAttackEffectEmission emission =
                MeleeEmission("multi-hit-melee", 3d, 2);

            EnemyAttackPatternHitRouteResult first = router.RouteActorContact(
                emission, Id("combat-event.multi-hit-melee-0"),
                PlayerActorId, 1L, 0d);
            EnemyAttackPatternHitRouteResult second = router.RouteActorContact(
                emission, Id("combat-event.multi-hit-melee-1"),
                PlayerActorId, 1L, 0d);
            EnemyAttackPatternHitRouteResult third = router.RouteActorContact(
                emission, Id("combat-event.multi-hit-melee-2"),
                PlayerActorId, 1L, 0d);

            Assert.That(first.IsAccepted, Is.True);
            Assert.That(second.IsAccepted, Is.True);
            Assert.That(third.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatus.RejectedByPolicy));
            Assert.That(context.DamageCallCount, Is.EqualTo(2));
            Assert.That(player.ExportSnapshot().CurrentHealth, Is.EqualTo(94d));
        }

        [Test]
        public void LethalHitAndAcceptedReplayExposeOneCanonicalDeath()
        {
            PlayerActorState player = Player(10d);
            var context = new AuthoritativeContext(player);
            var router = new EnemyAttackPatternHitRouter(context);
            EnemyAttackEffectEmission emission =
                ProjectileEmission("lethal-hit", 10d);
            StableId hitId = Id("combat-event.lethal-hit");

            EnemyAttackPatternHitRouteResult first = router.RouteActorContact(
                emission, hitId, PlayerActorId, 1L, 1d);
            EnemyAttackPatternHitRouteResult replay = router.RouteActorContact(
                emission, hitId, PlayerActorId, 1L, 1d);

            Assert.That(first.DamageResult.DeathFact, Is.Not.Null);
            Assert.That(replay.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatus.AppliedExactReplay));
            Assert.That(replay.DamageResult.DeathFact,
                Is.SameAs(first.DamageResult.DeathFact));
            Assert.That(context.DamageCallCount, Is.EqualTo(1));
            Assert.That(player.ExportSnapshot().IsDead, Is.True);
        }

        private static PlayerActorState Player(double maximumHealth)
        {
            PlayerActorCreationResult created = PlayerActorState.TryCreate(
                new PlayerActorDefinition(
                    PlayerActorId,
                    PlayerParticipantId,
                    PlayerCharacterId,
                    PlayerFactionId,
                    maximumHealth,
                    1L));
            Assert.That(created.IsCreated, Is.True);
            return created.Authority;
        }

        private static EnemyAttackEffectEmission ProjectileEmission(
            string suffix,
            double damage)
        {
            EnemyAttackCapabilityDescriptor descriptor =
                new EnemyAttackCapabilityDescriptor(
                    Id("enemy-attack-profile." + suffix),
                    Id("enemy-attack.ranged-projectile"),
                    10,
                    120d,
                    0d,
                    5d,
                    12d,
                    damage,
                    Id("damage.kinetic"),
                    new EnemyShootingPattern(
                        1,
                        0d,
                        1,
                        0d,
                        EnemySequenceAimPolicy.LockAtSequenceStart,
                        0d,
                        1d,
                        EnemyAttackInterruptionPolicy
                            .CancelPendingOnLifecycleEnd),
                    new EnemyProjectilePayload(
                        Id("projectile.enemy-blaster"),
                        10d,
                        20d,
                        0.15d,
                        0,
                        null),
                    null);
            return Emission(suffix, descriptor,
                EnemyAttackExecutionKind.Projectile);
        }

        private static EnemyAttackEffectEmission MeleeEmission(
            string suffix,
            double damage,
            int hitsPerTarget)
        {
            EnemyAttackCapabilityDescriptor descriptor =
                new EnemyAttackCapabilityDescriptor(
                    Id("enemy-attack-profile." + suffix),
                    Id("enemy-attack.contact"),
                    10,
                    120d,
                    0d,
                    0.4d,
                    0.8d,
                    damage,
                    Id("damage.impact"),
                    null,
                    null,
                    new EnemyMeleePattern(
                        0d,
                        1d,
                        1,
                        0d,
                        0.8d,
                        0d,
                        EnemyMeleeAimCommitPolicy.LockAtWindUp,
                        0.5d,
                        hitsPerTarget,
                        EnemyMeleeTerminalOnImpactPolicy.ContinueSequence,
                        EnemyAttackInterruptionPolicy
                            .CancelPendingOnLifecycleEnd));
            return Emission(suffix, descriptor,
                EnemyAttackExecutionKind.Contact);
        }

        private static EnemyAttackEffectEmission Emission(
            string suffix,
            EnemyAttackCapabilityDescriptor descriptor,
            EnemyAttackExecutionKind kind)
        {
            var identity = new EnemyLiveIdentity(
                SourceActorId,
                SourceParticipantId,
                Id("run.hit-router"),
                Id("room-runtime.hit-router"),
                Id("room.hit-router"),
                Id("room-placement.hit-router"));
            var intent = new EnemyAttackIntent(
                SourceActorId,
                SourceParticipantId,
                PlayerActorId,
                descriptor.AttackId,
                new EnemyVector2(0d, 0d),
                new EnemyVector2(1d, 0d),
                new EnemyVector2(5d, 0d),
                Id("enemy-decision.hit-router"),
                Id("enemy-phase.ready"),
                Id("enemy-decision-reason.attack-ready"));
            var execution = new EnemyAttackExecutionRequest(
                Id("enemy-operation." + suffix),
                identity,
                1L,
                0d,
                descriptor,
                intent,
                Id("equipment-instance.hit-router"),
                kind,
                descriptor.Damage,
                descriptor.CooldownSeconds);
            EnemyAttackSequence sequence =
                EnemyAttackPatternScheduler.Schedule(execution);
            return EnemyAttackEffectEmissionProjector.Project(
                execution, sequence)[0];
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}

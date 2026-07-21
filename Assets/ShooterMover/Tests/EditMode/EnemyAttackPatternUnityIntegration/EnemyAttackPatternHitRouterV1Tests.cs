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
    public sealed class EnemyAttackPatternHitRouterV1Tests
    {
        private static readonly StableId SourceActorId =
            Id("enemy-entity.hit-router-source");
        private static readonly StableId SourceParticipantId =
            Id("run-participant.hit-router-source");
        private static readonly StableId PlayerActorId =
            Id("player-entity.hit-router-target");
        private static readonly StableId PlayerParticipantId =
            Id("run-participant.hit-router-target");
        private static readonly StableId EnemyFactionId =
            Id("faction.hostile-machines");
        private static readonly StableId PlayerFactionId =
            Id("faction.player");

        private sealed class AuthoritativeContext :
            IEnemyAttackPatternCombatContextV1
        {
            private readonly PlayerActorAuthority player;
            private readonly bool targetUsesEnemyFaction;

            public AuthoritativeContext(
                PlayerActorAuthority player,
                bool targetUsesEnemyFaction = false)
            {
                this.player = player;
                this.targetUsesEnemyFaction = targetUsesEnemyFaction;
            }

            public int DamageCallCount { get; private set; }

            public bool TryReadSource(
                EnemyAttackEffectEmissionV1 emission,
                out CombatActorSnapshotV1 source)
            {
                source = null;
                if (emission == null)
                {
                    return false;
                }
                source = new CombatActorSnapshotV1(
                    SourceActorId,
                    new GameplayEntityIdentity(
                        SourceActorId,
                        GameplayEntityOwnership.Create(
                            SourceParticipantId,
                            null),
                        EnemyFactionId),
                    emission.SourceLifecycleGeneration,
                    true,
                    true,
                    new StableId[0]);
                return true;
            }

            public bool TryReadTarget(
                StableId targetEntityStableId,
                out CombatActorSnapshotV1 target)
            {
                target = null;
                PlayerActorSnapshot snapshot = player.Snapshot;
                if (snapshot == null
                    || targetEntityStableId != snapshot.ActorInstanceId)
                {
                    return false;
                }
                target = new CombatActorSnapshotV1(
                    snapshot.ActorInstanceId,
                    new GameplayEntityIdentity(
                        snapshot.ActorInstanceId,
                        GameplayEntityOwnership.Create(
                            snapshot.RunParticipantId,
                            null),
                        targetUsesEnemyFaction
                            ? EnemyFactionId
                            : PlayerFactionId),
                    snapshot.LifecycleGeneration,
                    true,
                    snapshot.IsAlive,
                    new[]
                    {
                        CombatHitCapabilityIdsV1.DamageReceiver,
                    });
                return true;
            }

            public DamageReceiverResult ApplyPlayerDamage(
                PlayerDamageRequest request)
            {
                DamageCallCount++;
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
            PlayerActorAuthority player = Player(100d);
            var context = new AuthoritativeContext(player);
            var router = new EnemyAttackPatternHitRouterV1(context);
            EnemyAttackEffectEmissionV1 emission =
                ProjectileEmission("accepted-projectile", 5d);
            StableId hitId = Id("combat-event.accepted-projectile");

            EnemyAttackPatternHitRouteResultV1 first =
                router.RouteActorContact(
                    emission,
                    hitId,
                    PlayerActorId,
                    1L,
                    4d);
            EnemyAttackPatternHitRouteResultV1 replay =
                router.RouteActorContact(
                    emission,
                    hitId,
                    PlayerActorId,
                    1L,
                    4d);

            Assert.That(first.Status,
                Is.EqualTo(EnemyAttackPatternHitRouteStatusV1.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(EnemyAttackPatternHitRouteStatusV1.ExactReplay));
            Assert.That(context.DamageCallCount, Is.EqualTo(1));
            Assert.That(player.Snapshot.CurrentHealth, Is.EqualTo(95d));
        }

        [Test]
        public void ConflictingHitEventReuseRejectsWithoutAdditionalDamage()
        {
            PlayerActorAuthority player = Player(100d);
            var context = new AuthoritativeContext(player);
            var router = new EnemyAttackPatternHitRouterV1(context);
            EnemyAttackEffectEmissionV1 emission =
                ProjectileEmission("conflicting-hit", 5d);
            StableId hitId = Id("combat-event.conflicting-hit");
            Assert.That(router.RouteActorContact(
                    emission,
                    hitId,
                    PlayerActorId,
                    1L,
                    4d).IsAccepted,
                Is.True);

            EnemyAttackPatternHitRouteResultV1 conflict =
                router.RouteActorContact(
                    emission,
                    hitId,
                    PlayerActorId,
                    1L,
                    9d);

            Assert.That(conflict.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatusV1
                        .ConflictingDuplicate));
            Assert.That(context.DamageCallCount, Is.EqualTo(1));
            Assert.That(player.Snapshot.CurrentHealth, Is.EqualTo(95d));
        }

        [Test]
        public void FriendlyFirePolicyRejectionDoesNotReachPlayerAuthority()
        {
            PlayerActorAuthority player = Player(100d);
            var context = new AuthoritativeContext(
                player,
                targetUsesEnemyFaction: true);
            var router = new EnemyAttackPatternHitRouterV1(context);

            EnemyAttackPatternHitRouteResultV1 result =
                router.RouteActorContact(
                    ProjectileEmission("friendly-fire", 5d),
                    Id("combat-event.friendly-fire"),
                    PlayerActorId,
                    1L,
                    1d);

            Assert.That(result.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatusV1.RejectedByPolicy));
            Assert.That(context.DamageCallCount, Is.Zero);
            Assert.That(player.Snapshot.CurrentHealth, Is.EqualTo(100d));
        }

        [Test]
        public void StaleTargetLifecycleRejectsWithoutDamage()
        {
            PlayerActorAuthority player = Player(100d);
            var context = new AuthoritativeContext(player);
            var router = new EnemyAttackPatternHitRouterV1(context);

            EnemyAttackPatternHitRouteResultV1 result =
                router.RouteActorContact(
                    ProjectileEmission("stale-target", 5d),
                    Id("combat-event.stale-target"),
                    PlayerActorId,
                    2L,
                    1d);

            Assert.That(result.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatusV1.RejectedByPolicy));
            Assert.That(context.DamageCallCount, Is.Zero);
            Assert.That(player.Snapshot.CurrentHealth, Is.EqualTo(100d));
        }

        [Test]
        public void MeleeHonorsAuthoredHitsPerTargetAndRejectsThirdHit()
        {
            PlayerActorAuthority player = Player(100d);
            var context = new AuthoritativeContext(player);
            var router = new EnemyAttackPatternHitRouterV1(context);
            EnemyAttackEffectEmissionV1 emission =
                MeleeEmission("multi-hit-melee", 3d, 2);

            EnemyAttackPatternHitRouteResultV1 first =
                router.RouteActorContact(
                    emission,
                    Id("combat-event.multi-hit-melee-0"),
                    PlayerActorId,
                    1L,
                    0d);
            EnemyAttackPatternHitRouteResultV1 second =
                router.RouteActorContact(
                    emission,
                    Id("combat-event.multi-hit-melee-1"),
                    PlayerActorId,
                    1L,
                    0d);
            EnemyAttackPatternHitRouteResultV1 third =
                router.RouteActorContact(
                    emission,
                    Id("combat-event.multi-hit-melee-2"),
                    PlayerActorId,
                    1L,
                    0d);

            Assert.That(first.IsAccepted, Is.True);
            Assert.That(second.IsAccepted, Is.True);
            Assert.That(third.Status,
                Is.EqualTo(
                    EnemyAttackPatternHitRouteStatusV1.RejectedByPolicy));
            Assert.That(context.DamageCallCount, Is.EqualTo(2));
            Assert.That(player.Snapshot.CurrentHealth, Is.EqualTo(94d));
        }

        [Test]
        public void LethalHitAndExactReplayEmitOneCanonicalDeath()
        {
            PlayerActorAuthority player = Player(10d);
            var context = new AuthoritativeContext(player);
            var router = new EnemyAttackPatternHitRouterV1(context);
            EnemyAttackEffectEmissionV1 emission =
                ProjectileEmission("lethal-hit", 10d);
            StableId hitId = Id("combat-event.lethal-hit");

            EnemyAttackPatternHitRouteResultV1 first =
                router.RouteActorContact(
                    emission,
                    hitId,
                    PlayerActorId,
                    1L,
                    1d);
            EnemyAttackPatternHitRouteResultV1 replay =
                router.RouteActorContact(
                    emission,
                    hitId,
                    PlayerActorId,
                    1L,
                    1d);

            Assert.That(first.DamageResult.DeathFact, Is.Not.Null);
            Assert.That(replay.Status,
                Is.EqualTo(EnemyAttackPatternHitRouteStatusV1.ExactReplay));
            Assert.That(replay.DamageResult.DeathFact,
                Is.SameAs(first.DamageResult.DeathFact));
            Assert.That(context.DamageCallCount, Is.EqualTo(1));
            Assert.That(player.Snapshot.IsDead, Is.True);
        }

        private static PlayerActorAuthority Player(double maximumHealth)
        {
            PlayerActorConstructionResult constructed =
                PlayerActorAuthority.TryCreate(
                    new PlayerActorDefinition(
                        PlayerActorId,
                        PlayerParticipantId,
                        PlayerFactionId,
                        maximumHealth,
                        1L));
            Assert.That(constructed.Succeeded, Is.True);
            return constructed.Authority;
        }

        private static EnemyAttackEffectEmissionV1 ProjectileEmission(
            string suffix,
            double damage)
        {
            EnemyAttackCapabilityDescriptorV1 descriptor =
                new EnemyAttackCapabilityDescriptorV1(
                    Id("enemy-attack-profile." + suffix),
                    Id("enemy-attack.ranged-projectile"),
                    10,
                    120d,
                    0d,
                    5d,
                    12d,
                    damage,
                    Id("damage.kinetic"),
                    new EnemyShootingPatternV1(
                        1,
                        0d,
                        1,
                        0d,
                        EnemySequenceAimPolicyV1.LockAtSequenceStart,
                        0d,
                        1d,
                        EnemyAttackInterruptionPolicyV1
                            .CancelPendingOnLifecycleEnd),
                    new EnemyProjectilePayloadV1(
                        Id("projectile.enemy-blaster"),
                        10d,
                        20d,
                        0.15d,
                        0,
                        null),
                    null);
            return Emission(
                suffix,
                descriptor,
                EnemyAttackExecutionKindV1.Projectile);
        }

        private static EnemyAttackEffectEmissionV1 MeleeEmission(
            string suffix,
            double damage,
            int hitsPerTarget)
        {
            EnemyAttackCapabilityDescriptorV1 descriptor =
                new EnemyAttackCapabilityDescriptorV1(
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
                    new EnemyMeleePatternV1(
                        0d,
                        1d,
                        1,
                        0d,
                        0.8d,
                        0d,
                        EnemyMeleeAimCommitPolicyV1.LockAtWindUp,
                        0.5d,
                        hitsPerTarget,
                        EnemyMeleeTerminalOnImpactPolicyV1
                            .ContinueSequence,
                        EnemyAttackInterruptionPolicyV1
                            .CancelPendingOnLifecycleEnd));
            return Emission(
                suffix,
                descriptor,
                EnemyAttackExecutionKindV1.Contact);
        }

        private static EnemyAttackEffectEmissionV1 Emission(
            string suffix,
            EnemyAttackCapabilityDescriptorV1 descriptor,
            EnemyAttackExecutionKindV1 kind)
        {
            var identity = new EnemyRuntimeIdentityV1(
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
            var execution = new EnemyAttackExecutionRequestV1(
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
            EnemyAttackSequenceV1 sequence =
                EnemyAttackPatternSchedulerV1.Schedule(execution);
            return EnemyAttackEffectEmissionProjectorV1.Project(
                execution,
                sequence)[0];
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}

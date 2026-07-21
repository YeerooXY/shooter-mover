using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using ShooterMover.Combat.HitPolicy;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.GameplayEntities;

namespace ShooterMover.Tests.EditMode.CombatHitPolicy
{
    public sealed class CombatHitPolicyV1Tests
    {
        private readonly CombatHitPolicyV1 policy = new CombatHitPolicyV1(
            CombatHitPolicyRegistryV1.CreateDefault());

        [Test]
        public void NormalPolicies_UseFactionAndDamageCapability_NotActorType()
        {
            CombatActorSnapshotV1 player = Actor(
                "player-a", "players", "player-a", 2L, true);
            CombatEffectSnapshotV1 playerEffect = Effect(
                "player-shot",
                CombatHitPolicyIdsV1.PlayerNormal,
                player,
                2,
                1);
            CombatHitHistorySnapshotV1 playerHistory =
                CombatHitHistorySnapshotV1.Empty(playerEffect.EffectId);

            CombatHitPolicyResultV1 enemyHit = Evaluate(
                player,
                playerEffect,
                Actor("enemy-a", "enemies", "enemy-a", 4L, true),
                4L,
                playerHistory);
            CombatHitPolicyResultV1 propHit = Evaluate(
                player,
                playerEffect,
                Actor("barrel-a", "neutral", null, 1L, true),
                1L,
                enemyHit.NextHistory);
            CombatHitPolicyResultV1 allyIgnored = Evaluate(
                player,
                playerEffect,
                Actor("player-b", "players", "player-b", 1L, true),
                1L,
                propHit.NextHistory);

            Assert.That(enemyHit.DamageEligible, Is.True);
            Assert.That(propHit.DamageEligible, Is.True);
            Assert.That(allyIgnored.DamageEligible, Is.False);
            Assert.That(
                allyIgnored.RejectionCode,
                Is.EqualTo(CombatHitRejectionCodeV1.FriendlyFireDenied));

            CombatActorSnapshotV1 enemy = Actor(
                "enemy-source", "enemies", "enemy-source", 3L, true);
            CombatEffectSnapshotV1 enemyEffect = Effect(
                "enemy-shot",
                CombatHitPolicyIdsV1.EnemyNormal,
                enemy,
                2,
                1);
            CombatHitHistorySnapshotV1 enemyHistory =
                CombatHitHistorySnapshotV1.Empty(enemyEffect.EffectId);

            CombatHitPolicyResultV1 playerHit = Evaluate(
                enemy,
                enemyEffect,
                player,
                2L,
                enemyHistory);
            CombatHitPolicyResultV1 neutralHit = Evaluate(
                enemy,
                enemyEffect,
                Actor("cover-a", "neutral", null, 1L, true),
                1L,
                playerHit.NextHistory);
            CombatHitPolicyResultV1 enemyAllyIgnored = Evaluate(
                enemy,
                enemyEffect,
                Actor("enemy-b", "enemies", "enemy-b", 1L, true),
                1L,
                neutralHit.NextHistory);

            Assert.That(playerHit.DamageEligible, Is.True);
            Assert.That(neutralHit.DamageEligible, Is.True);
            Assert.That(
                enemyAllyIgnored.RejectionCode,
                Is.EqualTo(CombatHitRejectionCodeV1.FriendlyFireDenied));
        }

        [Test]
        public void ChaoticPolicy_DamagesAllEligibleFactionsExceptSource()
        {
            CombatActorSnapshotV1 source = Actor(
                "enemy-a", "enemies", "enemy-a", 1L, true);
            CombatEffectSnapshotV1 effect = Effect(
                "chaotic",
                CombatHitPolicyIdsV1.ChaoticAllFactions,
                source,
                2,
                1);
            CombatHitHistorySnapshotV1 history =
                CombatHitHistorySnapshotV1.Empty(effect.EffectId);

            CombatHitPolicyResultV1 ally = Evaluate(
                source,
                effect,
                Actor("enemy-b", "enemies", "enemy-b", 1L, true),
                1L,
                history);
            CombatHitPolicyResultV1 opponent = Evaluate(
                source,
                effect,
                Actor("player-a", "players", "player-a", 1L, true),
                1L,
                ally.NextHistory);
            CombatHitPolicyResultV1 self = Evaluate(
                source,
                effect,
                source,
                1L,
                opponent.NextHistory);

            Assert.That(ally.DamageEligible, Is.True);
            Assert.That(opponent.DamageEligible, Is.True);
            Assert.That(self.DamageEligible, Is.False);
            Assert.That(
                self.RejectionCode,
                Is.EqualTo(CombatHitRejectionCodeV1.SelfHitDenied));
        }

        [Test]
        public void WorldBlocker_ReturnsAuthoredTerminateReflectOrIgnore()
        {
            CombatActorSnapshotV1 source = Actor(
                "player-a", "players", "player-a", 1L, true);

            AssertWorld(
                source,
                CombatWorldBlockerBehaviorV1.Terminate,
                CombatHitDispositionV1.Terminate);
            AssertWorld(
                source,
                CombatWorldBlockerBehaviorV1.Reflect,
                CombatHitDispositionV1.Reflect);
            AssertWorld(
                source,
                CombatWorldBlockerBehaviorV1.Ignore,
                CombatHitDispositionV1.Ignore);
        }

        [Test]
        public void HitHistory_EnforcesPerTargetLimitAndPierceBudget()
        {
            CombatActorSnapshotV1 source = Actor(
                "player-a", "players", "player-a", 1L, true);
            CombatActorSnapshotV1 target = Actor(
                "enemy-a", "enemies", "enemy-a", 1L, true);
            CombatEffectSnapshotV1 repeatLimited = Effect(
                "repeat-limited",
                CombatHitPolicyIdsV1.PlayerNormal,
                source,
                3,
                1);

            CombatHitPolicyResultV1 first = Evaluate(
                source,
                repeatLimited,
                target,
                1L,
                CombatHitHistorySnapshotV1.Empty(repeatLimited.EffectId));
            CombatHitPolicyResultV1 repeated = Evaluate(
                source,
                repeatLimited,
                target,
                1L,
                first.NextHistory);

            Assert.That(first.DamageEligible, Is.True);
            Assert.That(
                repeated.RejectionCode,
                Is.EqualTo(CombatHitRejectionCodeV1.AlreadyHitLimitReached));
            Assert.That(repeated.NextHistory.AcceptedActorHitCount, Is.EqualTo(1));

            CombatEffectSnapshotV1 noPierce = Effect(
                "no-pierce",
                CombatHitPolicyIdsV1.PlayerNormal,
                source,
                0,
                1);
            CombatHitPolicyResultV1 terminal = Evaluate(
                source,
                noPierce,
                target,
                1L,
                CombatHitHistorySnapshotV1.Empty(noPierce.EffectId));
            CombatHitPolicyResultV1 exhausted = Evaluate(
                source,
                noPierce,
                Actor("enemy-b", "enemies", "enemy-b", 1L, true),
                1L,
                terminal.NextHistory);

            Assert.That(
                terminal.Disposition,
                Is.EqualTo(CombatHitDispositionV1.ApplyAndTerminate));
            Assert.That(
                exhausted.RejectionCode,
                Is.EqualTo(CombatHitRejectionCodeV1.PierceExhausted));
        }

        [Test]
        public void MalformedAlreadyHitState_FailsClosed()
        {
            CombatActorSnapshotV1 source = Actor(
                "player-a", "players", "player-a", 1L, true);
            CombatActorSnapshotV1 target = Actor(
                "enemy-a", "enemies", "enemy-a", 1L, true);
            CombatEffectSnapshotV1 effect = Effect(
                "bad-history",
                CombatHitPolicyIdsV1.PlayerNormal,
                source,
                3,
                2);
            CombatHitHistorySnapshotV1 malformed =
                new CombatHitHistorySnapshotV1(
                    effect.EffectId,
                    2,
                    new List<CombatHitTargetCountV1>
                    {
                        new CombatHitTargetCountV1(target.ActorId, 1),
                    });

            CombatHitPolicyResultV1 result = Evaluate(
                source,
                effect,
                target,
                1L,
                malformed);

            Assert.That(result.DamageEligible, Is.False);
            Assert.That(
                result.RejectionCode,
                Is.EqualTo(CombatHitRejectionCodeV1.InvalidHistory));
            Assert.That(result.NextHistory, Is.SameAs(malformed));
        }

        [Test]
        public void UnknownInactiveMismatchedAndStaleActors_FailClosed()
        {
            CombatActorSnapshotV1 source = Actor(
                "player-a", "players", "player-a", 2L, true);
            CombatActorSnapshotV1 target = Actor(
                "enemy-a", "enemies", "enemy-a", 2L, true);
            CombatEffectSnapshotV1 effect = Effect(
                "current",
                CombatHitPolicyIdsV1.PlayerNormal,
                source,
                1,
                1);

            AssertRejected(
                CombatActorSnapshotFactoryV1.CreateUnknown(source.ActorId, 2L),
                effect,
                target,
                2L,
                CombatHitRejectionCodeV1.UnknownSourceActor);
            AssertRejected(
                source,
                effect,
                CombatActorSnapshotFactoryV1.CreateUnknown(
                    Id("actor", "missing"),
                    2L),
                2L,
                CombatHitRejectionCodeV1.UnknownTargetActor);
            AssertRejected(
                source,
                effect,
                Actor("enemy-inactive", "enemies", "enemy-inactive", 2L, false),
                2L,
                CombatHitRejectionCodeV1.TargetInactive);
            AssertRejected(
                source,
                effect,
                target,
                1L,
                CombatHitRejectionCodeV1.StaleTargetGeneration);

            CombatEffectSnapshotV1 staleSource = new CombatEffectSnapshotV1(
                Id("effect", "stale-source"),
                CombatHitPolicyIdsV1.PlayerNormal,
                source.ActorId,
                1L,
                CombatEffectGeometryKindV1.Projectile,
                CombatWorldBlockerBehaviorV1.Terminate,
                false,
                false,
                0,
                1);
            AssertRejected(
                source,
                staleSource,
                target,
                2L,
                CombatHitRejectionCodeV1.StaleSourceGeneration);

            CombatActorSnapshotV1 mismatch = new CombatActorSnapshotV1(
                Id("actor", "enemy-b"),
                target.Identity,
                target.LifecycleGeneration,
                true,
                true,
                new List<StableId>
                {
                    CombatHitCapabilityIdsV1.DamageReceiver,
                });
            AssertRejected(
                source,
                effect,
                mismatch,
                2L,
                CombatHitRejectionCodeV1.TargetActorMismatch);
        }

        [Test]
        public void UnknownPolicyAndMissingDamageCapability_FailClosed()
        {
            CombatActorSnapshotV1 source = Actor(
                "player-a", "players", "player-a", 1L, true);
            CombatActorSnapshotV1 target = Actor(
                "enemy-a", "enemies", "enemy-a", 1L, true);
            CombatEffectSnapshotV1 unknownPolicy = Effect(
                "unknown-policy",
                Id("combat-hit-policy", "missing-v1"),
                source,
                0,
                1);

            AssertRejected(
                source,
                unknownPolicy,
                target,
                1L,
                CombatHitRejectionCodeV1.UnknownPolicy);

            CombatActorSnapshotV1 noDamageCapability =
                CombatActorSnapshotFactoryV1.CreateKnown(
                    target.Identity,
                    1L,
                    true,
                    new List<StableId>());
            CombatEffectSnapshotV1 effect = Effect(
                "missing-capability",
                CombatHitPolicyIdsV1.PlayerNormal,
                source,
                0,
                1);
            AssertRejected(
                source,
                effect,
                noDamageCapability,
                1L,
                CombatHitRejectionCodeV1.MissingDamageReceiverCapability);
        }

        [TestCase(CombatEffectGeometryKindV1.Projectile)]
        [TestCase(CombatEffectGeometryKindV1.Explosion)]
        [TestCase(CombatEffectGeometryKindV1.MeleeSwing)]
        [TestCase(CombatEffectGeometryKindV1.ContactAttack)]
        [TestCase(CombatEffectGeometryKindV1.PersistentField)]
        [TestCase(CombatEffectGeometryKindV1.Chain)]
        public void EverySupportedGeometry_ConsumesSamePolicyResult(
            CombatEffectGeometryKindV1 geometry)
        {
            CombatActorSnapshotV1 source = Actor(
                "player-a", "players", "player-a", 1L, true);
            CombatEffectSnapshotV1 effect = new CombatEffectSnapshotV1(
                Id(
                    "effect",
                    "geometry-" + ((int)geometry).ToString(
                        CultureInfo.InvariantCulture)),
                CombatHitPolicyIdsV1.PlayerNormal,
                source.ActorId,
                1L,
                geometry,
                CombatWorldBlockerBehaviorV1.Terminate,
                false,
                false,
                0,
                1);

            CombatHitPolicyResultV1 result = Evaluate(
                source,
                effect,
                Actor("enemy-a", "enemies", "enemy-a", 1L, true),
                1L,
                CombatHitHistorySnapshotV1.Empty(effect.EffectId));

            Assert.That(result.DamageEligible, Is.True);
            Assert.That(
                result.Disposition,
                Is.EqualTo(CombatHitDispositionV1.ApplyAndTerminate));
        }

        [Test]
        public void MultiTargetOrdering_IsDistanceThenBlockerThenStableIdentity()
        {
            IReadOnlyList<CombatHitContactV1> ordered = policy.OrderContacts(
                new List<CombatHitContactV1>
                {
                    CombatHitContactV1.Actor(
                        Actor("actor-b", "enemies", "actor-b", 1L, true),
                        1L,
                        5d),
                    CombatHitContactV1.Actor(
                        Actor("actor-c", "enemies", "actor-c", 1L, true),
                        1L,
                        3d),
                    CombatHitContactV1.Actor(
                        Actor("actor-a", "enemies", "actor-a", 1L, true),
                        1L,
                        5d),
                    CombatHitContactV1.WorldBlocker(
                        Id("blocker", "wall-a"),
                        5d),
                });

            Assert.That(ordered[0].SortId, Is.EqualTo(Id("actor", "actor-c")));
            Assert.That(ordered[1].SortId, Is.EqualTo(Id("blocker", "wall-a")));
            Assert.That(ordered[2].SortId, Is.EqualTo(Id("actor", "actor-a")));
            Assert.That(ordered[3].SortId, Is.EqualTo(Id("actor", "actor-b")));
        }

        [Test]
        public void AcceptedResult_ProjectsExistingDamageAndPropCommands()
        {
            CombatActorSnapshotV1 source = Actor(
                "player-a", "players", "player-a", 3L, true);
            CombatActorSnapshotV1 enemy = Actor(
                "enemy-a", "enemies", "enemy-a", 9L, true);
            CombatEffectSnapshotV1 effect = Effect(
                "damage-command",
                CombatHitPolicyIdsV1.PlayerNormal,
                source,
                1,
                1);
            CombatHitPolicyResultV1 enemyAccepted = Evaluate(
                source,
                effect,
                enemy,
                9L,
                CombatHitHistorySnapshotV1.Empty(effect.EffectId));

            DamageReceiverCommand damage;
            bool damageCreated = CombatHitDamageCommandAdapterV1.TryCreate(
                enemyAccepted,
                Id("damage-event", "hit-a"),
                25d,
                CombatChannel.Kinetic,
                out damage);

            Assert.That(damageCreated, Is.True);
            Assert.That(damage.SourceActorId, Is.EqualTo(source.ActorId));
            Assert.That(damage.TargetActorId, Is.EqualTo(enemy.ActorId));
            Assert.That(damage.LifecycleGeneration, Is.EqualTo(9L));

            CombatActorSnapshotV1 prop = Actor(
                "barrel-a", "neutral", null, 1L, true);
            CombatHitPolicyResultV1 propAccepted = Evaluate(
                source,
                effect,
                prop,
                1L,
                enemyAccepted.NextHistory);
            PropDamageCommandV1 propDamage;
            bool propCreated = CombatHitPropDamageCommandAdapterV1.TryCreate(
                propAccepted,
                Id("operation", "prop-hit-a"),
                Id("damage-channel", "kinetic"),
                30d,
                out propDamage);

            Assert.That(propCreated, Is.True);
            Assert.That(
                propDamage.SourceParticipantId,
                Is.EqualTo(Id("participant", "player-a")));
            Assert.That(
                propDamage.SourceFactionId,
                Is.EqualTo(Id("faction", "players")));
            Assert.That(propDamage.RequestedDamage, Is.EqualTo(30d));
        }

        [Test]
        public void WeaponEffectAdapter_PreservesSourceGenerationAndPierce()
        {
            WeaponEffectIdentity identity = new WeaponEffectIdentity(
                new WeaponActorInstanceId(Id("actor", "player-a")),
                new RunParticipantId(Id("participant", "player-a")),
                new EquipmentInstanceId(Id("equipment-instance", "blaster-a")),
                new WeaponDefinitionId("weapon.blaster-machine-gun"),
                new FireOperationId(Id("fire-operation", "fire-a")),
                new LifecycleGeneration(6L),
                12L,
                new ProjectileOrdinal(0));
            DirectProjectileEffect projectile = new DirectProjectileEffect(
                identity,
                new WeaponVector2(0d, 0d),
                new WeaponVector2(1d, 0d),
                20d,
                15d,
                10d,
                2,
                0d,
                "kinetic");

            CombatEffectSnapshotV1 adapted =
                WeaponEffectHitPolicyAdapterV1.Create(
                    projectile,
                    CombatHitPolicyIdsV1.PlayerNormal,
                    CombatWorldBlockerBehaviorV1.Terminate,
                    false,
                    false,
                    1);
            CombatEffectSnapshotV1 replay =
                WeaponEffectHitPolicyAdapterV1.Create(
                    projectile,
                    CombatHitPolicyIdsV1.PlayerNormal,
                    CombatWorldBlockerBehaviorV1.Terminate,
                    false,
                    false,
                    1);

            Assert.That(adapted, Is.Not.Null);
            Assert.That(adapted.SourceActorId, Is.EqualTo(Id("actor", "player-a")));
            Assert.That(adapted.SourceLifecycleGeneration, Is.EqualTo(6L));
            Assert.That(adapted.Pierce, Is.EqualTo(2));
            Assert.That(adapted.EffectId, Is.EqualTo(replay.EffectId));
        }

        private CombatHitPolicyResultV1 Evaluate(
            CombatActorSnapshotV1 source,
            CombatEffectSnapshotV1 effect,
            CombatActorSnapshotV1 target,
            long observedTargetGeneration,
            CombatHitHistorySnapshotV1 history)
        {
            return policy.Evaluate(new CombatHitPolicyInputV1(
                source,
                effect,
                CombatHitContactV1.Actor(
                    target,
                    observedTargetGeneration,
                    1d),
                history));
        }

        private void AssertRejected(
            CombatActorSnapshotV1 source,
            CombatEffectSnapshotV1 effect,
            CombatActorSnapshotV1 target,
            long observedTargetGeneration,
            CombatHitRejectionCodeV1 expected)
        {
            CombatHitPolicyResultV1 result = Evaluate(
                source,
                effect,
                target,
                observedTargetGeneration,
                CombatHitHistorySnapshotV1.Empty(effect.EffectId));
            Assert.That(result.DamageEligible, Is.False);
            Assert.That(result.RejectionCode, Is.EqualTo(expected));
        }

        private void AssertWorld(
            CombatActorSnapshotV1 source,
            CombatWorldBlockerBehaviorV1 behavior,
            CombatHitDispositionV1 expected)
        {
            CombatEffectSnapshotV1 effect = new CombatEffectSnapshotV1(
                Id("effect", "wall-" + behavior.ToString().ToLowerInvariant()),
                CombatHitPolicyIdsV1.PlayerNormal,
                source.ActorId,
                source.LifecycleGeneration,
                CombatEffectGeometryKindV1.Projectile,
                behavior,
                false,
                false,
                0,
                1);
            CombatHitPolicyResultV1 result = policy.Evaluate(
                new CombatHitPolicyInputV1(
                    source,
                    effect,
                    CombatHitContactV1.WorldBlocker(
                        Id("blocker", "wall-a"),
                        1d),
                    CombatHitHistorySnapshotV1.Empty(effect.EffectId)));

            Assert.That(result.Disposition, Is.EqualTo(expected));
            Assert.That(result.RejectionCode, Is.EqualTo(CombatHitRejectionCodeV1.None));
            Assert.That(result.DamageEligible, Is.False);
        }

        private static CombatActorSnapshotV1 Actor(
            string actor,
            string faction,
            string participant,
            long generation,
            bool active)
        {
            GameplayEntityOwnership ownership = participant == null
                ? GameplayEntityOwnership.None()
                : GameplayEntityOwnership.Create(
                    Id("participant", participant),
                    Id("character", participant));
            GameplayEntityIdentity identity = new GameplayEntityIdentity(
                Id("actor", actor),
                ownership,
                Id("faction", faction));
            return CombatActorSnapshotFactoryV1.CreateDamageReceiver(
                identity,
                generation,
                active);
        }

        private static CombatEffectSnapshotV1 Effect(
            string effect,
            StableId policyId,
            CombatActorSnapshotV1 source,
            int pierce,
            int maximumHitsPerTarget)
        {
            return new CombatEffectSnapshotV1(
                Id("effect", effect),
                policyId,
                source.ActorId,
                source.LifecycleGeneration,
                CombatEffectGeometryKindV1.Projectile,
                CombatWorldBlockerBehaviorV1.Terminate,
                false,
                false,
                pierce,
                maximumHitsPerTarget);
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }
    }
}

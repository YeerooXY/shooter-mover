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
    public sealed class CombatHitRulesTests
    {
        private readonly CombatHitRules policy = new CombatHitRules(
            CombatHitPolicyRegistry.CreateDefault());

        [Test]
        public void NormalPolicies_UseFactionAndDamageCapability_NotActorType()
        {
            CombatActorSnapshot player = Actor(
                "player-a", "players", "player-a", 2L, true);
            CombatEffectSnapshot playerEffect = Effect(
                "player-shot",
                CombatHitPolicyIds.PlayerNormal,
                player,
                2,
                1);
            CombatHitHistorySnapshot playerHistory =
                CombatHitHistorySnapshot.Empty(playerEffect.EffectId);

            CombatHitPolicyResult enemyHit = Evaluate(
                player,
                playerEffect,
                Actor("enemy-a", "enemies", "enemy-a", 4L, true),
                4L,
                playerHistory);
            CombatHitPolicyResult propHit = Evaluate(
                player,
                playerEffect,
                Actor("barrel-a", "neutral", null, 1L, true),
                1L,
                enemyHit.NextHistory);
            CombatHitPolicyResult allyIgnored = Evaluate(
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
                Is.EqualTo(CombatHitRejectionCode.FriendlyFireDenied));

            CombatActorSnapshot enemy = Actor(
                "enemy-source", "enemies", "enemy-source", 3L, true);
            CombatEffectSnapshot enemyEffect = Effect(
                "enemy-shot",
                CombatHitPolicyIds.EnemyNormal,
                enemy,
                2,
                1);
            CombatHitHistorySnapshot enemyHistory =
                CombatHitHistorySnapshot.Empty(enemyEffect.EffectId);

            CombatHitPolicyResult playerHit = Evaluate(
                enemy,
                enemyEffect,
                player,
                2L,
                enemyHistory);
            CombatHitPolicyResult neutralHit = Evaluate(
                enemy,
                enemyEffect,
                Actor("cover-a", "neutral", null, 1L, true),
                1L,
                playerHit.NextHistory);
            CombatHitPolicyResult enemyAllyIgnored = Evaluate(
                enemy,
                enemyEffect,
                Actor("enemy-b", "enemies", "enemy-b", 1L, true),
                1L,
                neutralHit.NextHistory);

            Assert.That(playerHit.DamageEligible, Is.True);
            Assert.That(neutralHit.DamageEligible, Is.True);
            Assert.That(
                enemyAllyIgnored.RejectionCode,
                Is.EqualTo(CombatHitRejectionCode.FriendlyFireDenied));
        }

        [Test]
        public void ChaoticPolicy_DamagesAllEligibleFactionsExceptSource()
        {
            CombatActorSnapshot source = Actor(
                "enemy-a", "enemies", "enemy-a", 1L, true);
            CombatEffectSnapshot effect = Effect(
                "chaotic",
                CombatHitPolicyIds.ChaoticAllFactions,
                source,
                2,
                1);
            CombatHitHistorySnapshot history =
                CombatHitHistorySnapshot.Empty(effect.EffectId);

            CombatHitPolicyResult ally = Evaluate(
                source,
                effect,
                Actor("enemy-b", "enemies", "enemy-b", 1L, true),
                1L,
                history);
            CombatHitPolicyResult opponent = Evaluate(
                source,
                effect,
                Actor("player-a", "players", "player-a", 1L, true),
                1L,
                ally.NextHistory);
            CombatHitPolicyResult self = Evaluate(
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
                Is.EqualTo(CombatHitRejectionCode.SelfHitDenied));
        }

        [Test]
        public void WorldBlocker_ReturnsAuthoredTerminateReflectOrIgnore()
        {
            CombatActorSnapshot source = Actor(
                "player-a", "players", "player-a", 1L, true);

            AssertWorld(
                source,
                CombatWorldBlockerBehavior.Terminate,
                CombatHitDisposition.Terminate);
            AssertWorld(
                source,
                CombatWorldBlockerBehavior.Reflect,
                CombatHitDisposition.Reflect);
            AssertWorld(
                source,
                CombatWorldBlockerBehavior.Ignore,
                CombatHitDisposition.Ignore);
        }

        [Test]
        public void HitHistory_EnforcesPerTargetLimitAndPierceBudget()
        {
            CombatActorSnapshot source = Actor(
                "player-a", "players", "player-a", 1L, true);
            CombatActorSnapshot target = Actor(
                "enemy-a", "enemies", "enemy-a", 1L, true);
            CombatEffectSnapshot repeatLimited = Effect(
                "repeat-limited",
                CombatHitPolicyIds.PlayerNormal,
                source,
                3,
                1);

            CombatHitPolicyResult first = Evaluate(
                source,
                repeatLimited,
                target,
                1L,
                CombatHitHistorySnapshot.Empty(repeatLimited.EffectId));
            CombatHitPolicyResult repeated = Evaluate(
                source,
                repeatLimited,
                target,
                1L,
                first.NextHistory);

            Assert.That(first.DamageEligible, Is.True);
            Assert.That(
                repeated.RejectionCode,
                Is.EqualTo(CombatHitRejectionCode.AlreadyHitLimitReached));
            Assert.That(repeated.NextHistory.AcceptedActorHitCount, Is.EqualTo(1));

            CombatEffectSnapshot noPierce = Effect(
                "no-pierce",
                CombatHitPolicyIds.PlayerNormal,
                source,
                0,
                1);
            CombatHitPolicyResult terminal = Evaluate(
                source,
                noPierce,
                target,
                1L,
                CombatHitHistorySnapshot.Empty(noPierce.EffectId));
            CombatHitPolicyResult exhausted = Evaluate(
                source,
                noPierce,
                Actor("enemy-b", "enemies", "enemy-b", 1L, true),
                1L,
                terminal.NextHistory);

            Assert.That(
                terminal.Disposition,
                Is.EqualTo(CombatHitDisposition.ApplyAndTerminate));
            Assert.That(
                exhausted.RejectionCode,
                Is.EqualTo(CombatHitRejectionCode.PierceExhausted));
        }

        [Test]
        public void MalformedAlreadyHitState_FailsClosed()
        {
            CombatActorSnapshot source = Actor(
                "player-a", "players", "player-a", 1L, true);
            CombatActorSnapshot target = Actor(
                "enemy-a", "enemies", "enemy-a", 1L, true);
            CombatEffectSnapshot effect = Effect(
                "bad-history",
                CombatHitPolicyIds.PlayerNormal,
                source,
                3,
                2);
            CombatHitHistorySnapshot malformed =
                new CombatHitHistorySnapshot(
                    effect.EffectId,
                    2,
                    new List<CombatHitTargetCount>
                    {
                        new CombatHitTargetCount(target.ActorId, 1),
                    });

            CombatHitPolicyResult result = Evaluate(
                source,
                effect,
                target,
                1L,
                malformed);

            Assert.That(result.DamageEligible, Is.False);
            Assert.That(
                result.RejectionCode,
                Is.EqualTo(CombatHitRejectionCode.InvalidHistory));
            Assert.That(result.NextHistory, Is.SameAs(malformed));
        }

        [Test]
        public void UnknownInactiveMismatchedAndStaleActors_FailClosed()
        {
            CombatActorSnapshot source = Actor(
                "player-a", "players", "player-a", 2L, true);
            CombatActorSnapshot target = Actor(
                "enemy-a", "enemies", "enemy-a", 2L, true);
            CombatEffectSnapshot effect = Effect(
                "current",
                CombatHitPolicyIds.PlayerNormal,
                source,
                1,
                1);

            AssertRejected(
                CombatActorSnapshotFactory.CreateUnknown(source.ActorId, 2L),
                effect,
                target,
                2L,
                CombatHitRejectionCode.UnknownSourceActor);
            AssertRejected(
                source,
                effect,
                CombatActorSnapshotFactory.CreateUnknown(
                    Id("actor", "missing"),
                    2L),
                2L,
                CombatHitRejectionCode.UnknownTargetActor);
            AssertRejected(
                source,
                effect,
                Actor("enemy-inactive", "enemies", "enemy-inactive", 2L, false),
                2L,
                CombatHitRejectionCode.TargetInactive);
            AssertRejected(
                source,
                effect,
                target,
                1L,
                CombatHitRejectionCode.StaleTargetGeneration);

            CombatEffectSnapshot staleSource = new CombatEffectSnapshot(
                Id("effect", "stale-source"),
                CombatHitPolicyIds.PlayerNormal,
                source.ActorId,
                1L,
                CombatEffectGeometryKind.Projectile,
                CombatWorldBlockerBehavior.Terminate,
                false,
                false,
                0,
                1);
            AssertRejected(
                source,
                staleSource,
                target,
                2L,
                CombatHitRejectionCode.StaleSourceGeneration);

            CombatActorSnapshot mismatch = new CombatActorSnapshot(
                Id("actor", "enemy-b"),
                target.Identity,
                target.LifecycleGeneration,
                true,
                true,
                new List<StableId>
                {
                    CombatHitCapabilityIds.DamageReceiver,
                });
            AssertRejected(
                source,
                effect,
                mismatch,
                2L,
                CombatHitRejectionCode.TargetActorMismatch);
        }

        [Test]
        public void UnknownPolicyAndMissingDamageCapability_FailClosed()
        {
            CombatActorSnapshot source = Actor(
                "player-a", "players", "player-a", 1L, true);
            CombatActorSnapshot target = Actor(
                "enemy-a", "enemies", "enemy-a", 1L, true);
            CombatEffectSnapshot unknownPolicy = Effect(
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
                CombatHitRejectionCode.UnknownPolicy);

            CombatActorSnapshot noDamageCapability =
                CombatActorSnapshotFactory.CreateKnown(
                    target.Identity,
                    1L,
                    true,
                    new List<StableId>());
            CombatEffectSnapshot effect = Effect(
                "missing-capability",
                CombatHitPolicyIds.PlayerNormal,
                source,
                0,
                1);
            AssertRejected(
                source,
                effect,
                noDamageCapability,
                1L,
                CombatHitRejectionCode.MissingDamageReceiverCapability);
        }

        [TestCase(CombatEffectGeometryKind.Projectile)]
        [TestCase(CombatEffectGeometryKind.Explosion)]
        [TestCase(CombatEffectGeometryKind.MeleeSwing)]
        [TestCase(CombatEffectGeometryKind.ContactAttack)]
        [TestCase(CombatEffectGeometryKind.PersistentField)]
        [TestCase(CombatEffectGeometryKind.Chain)]
        public void EverySupportedGeometry_ConsumesSamePolicyResult(
            CombatEffectGeometryKind geometry)
        {
            CombatActorSnapshot source = Actor(
                "player-a", "players", "player-a", 1L, true);
            CombatEffectSnapshot effect = new CombatEffectSnapshot(
                Id(
                    "effect",
                    "geometry-" + ((int)geometry).ToString(
                        CultureInfo.InvariantCulture)),
                CombatHitPolicyIds.PlayerNormal,
                source.ActorId,
                1L,
                geometry,
                CombatWorldBlockerBehavior.Terminate,
                false,
                false,
                0,
                1);

            CombatHitPolicyResult result = Evaluate(
                source,
                effect,
                Actor("enemy-a", "enemies", "enemy-a", 1L, true),
                1L,
                CombatHitHistorySnapshot.Empty(effect.EffectId));

            Assert.That(result.DamageEligible, Is.True);
            Assert.That(
                result.Disposition,
                Is.EqualTo(CombatHitDisposition.ApplyAndTerminate));
        }

        [Test]
        public void MultiTargetOrdering_IsDistanceThenBlockerThenStableIdentity()
        {
            IReadOnlyList<CombatHitContact> ordered = policy.OrderContacts(
                new List<CombatHitContact>
                {
                    CombatHitContact.Actor(
                        Actor("actor-b", "enemies", "actor-b", 1L, true),
                        1L,
                        5d),
                    CombatHitContact.Actor(
                        Actor("actor-c", "enemies", "actor-c", 1L, true),
                        1L,
                        3d),
                    CombatHitContact.Actor(
                        Actor("actor-a", "enemies", "actor-a", 1L, true),
                        1L,
                        5d),
                    CombatHitContact.WorldBlocker(
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
            CombatActorSnapshot source = Actor(
                "player-a", "players", "player-a", 3L, true);
            CombatActorSnapshot enemy = Actor(
                "enemy-a", "enemies", "enemy-a", 9L, true);
            CombatEffectSnapshot effect = Effect(
                "damage-command",
                CombatHitPolicyIds.PlayerNormal,
                source,
                1,
                1);
            CombatHitPolicyResult enemyAccepted = Evaluate(
                source,
                effect,
                enemy,
                9L,
                CombatHitHistorySnapshot.Empty(effect.EffectId));

            DamageReceiverCommand damage;
            bool damageCreated = CombatHitDamageCommandBridge.TryCreate(
                enemyAccepted,
                Id("damage-event", "hit-a"),
                25d,
                CombatChannel.Kinetic,
                out damage);

            Assert.That(damageCreated, Is.True);
            Assert.That(damage.SourceActorId, Is.EqualTo(source.ActorId));
            Assert.That(damage.TargetActorId, Is.EqualTo(enemy.ActorId));
            Assert.That(damage.LifecycleGeneration, Is.EqualTo(9L));

            CombatActorSnapshot prop = Actor(
                "barrel-a", "neutral", null, 1L, true);
            CombatHitPolicyResult propAccepted = Evaluate(
                source,
                effect,
                prop,
                1L,
                enemyAccepted.NextHistory);
            PropDamageCommand propDamage;
            bool propCreated = CombatHitPropDamageCommandBridge.TryCreate(
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

            CombatEffectSnapshot adapted =
                WeaponEffectHitPolicyBridge.Create(
                    projectile,
                    CombatHitPolicyIds.PlayerNormal,
                    CombatWorldBlockerBehavior.Terminate,
                    false,
                    false,
                    1);
            CombatEffectSnapshot replay =
                WeaponEffectHitPolicyBridge.Create(
                    projectile,
                    CombatHitPolicyIds.PlayerNormal,
                    CombatWorldBlockerBehavior.Terminate,
                    false,
                    false,
                    1);

            Assert.That(adapted, Is.Not.Null);
            Assert.That(adapted.SourceActorId, Is.EqualTo(Id("actor", "player-a")));
            Assert.That(adapted.SourceLifecycleGeneration, Is.EqualTo(6L));
            Assert.That(adapted.Pierce, Is.EqualTo(2));
            Assert.That(adapted.EffectId, Is.EqualTo(replay.EffectId));
        }

        private CombatHitPolicyResult Evaluate(
            CombatActorSnapshot source,
            CombatEffectSnapshot effect,
            CombatActorSnapshot target,
            long observedTargetGeneration,
            CombatHitHistorySnapshot history)
        {
            return policy.Evaluate(new CombatHitPolicyInput(
                source,
                effect,
                CombatHitContact.Actor(
                    target,
                    observedTargetGeneration,
                    1d),
                history));
        }

        private void AssertRejected(
            CombatActorSnapshot source,
            CombatEffectSnapshot effect,
            CombatActorSnapshot target,
            long observedTargetGeneration,
            CombatHitRejectionCode expected)
        {
            CombatHitPolicyResult result = Evaluate(
                source,
                effect,
                target,
                observedTargetGeneration,
                CombatHitHistorySnapshot.Empty(effect.EffectId));
            Assert.That(result.DamageEligible, Is.False);
            Assert.That(result.RejectionCode, Is.EqualTo(expected));
        }

        private void AssertWorld(
            CombatActorSnapshot source,
            CombatWorldBlockerBehavior behavior,
            CombatHitDisposition expected)
        {
            CombatEffectSnapshot effect = new CombatEffectSnapshot(
                Id("effect", "wall-" + behavior.ToString().ToLowerInvariant()),
                CombatHitPolicyIds.PlayerNormal,
                source.ActorId,
                source.LifecycleGeneration,
                CombatEffectGeometryKind.Projectile,
                behavior,
                false,
                false,
                0,
                1);
            CombatHitPolicyResult result = policy.Evaluate(
                new CombatHitPolicyInput(
                    source,
                    effect,
                    CombatHitContact.WorldBlocker(
                        Id("blocker", "wall-a"),
                        1d),
                    CombatHitHistorySnapshot.Empty(effect.EffectId)));

            Assert.That(result.Disposition, Is.EqualTo(expected));
            Assert.That(result.RejectionCode, Is.EqualTo(CombatHitRejectionCode.None));
            Assert.That(result.DamageEligible, Is.False);
        }

        private static CombatActorSnapshot Actor(
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
            return CombatActorSnapshotFactory.CreateDamageReceiver(
                identity,
                generation,
                active);
        }

        private static CombatEffectSnapshot Effect(
            string effect,
            StableId policyId,
            CombatActorSnapshot source,
            int pierce,
            int maximumHitsPerTarget)
        {
            return new CombatEffectSnapshot(
                Id("effect", effect),
                policyId,
                source.ActorId,
                source.LifecycleGeneration,
                CombatEffectGeometryKind.Projectile,
                CombatWorldBlockerBehavior.Terminate,
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

using System;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyCombatRuntime;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.Tests.EditMode.EnemyCombatRuntime
{
    public sealed class EnemyCombatRuntimeTests
    {
        private static readonly StableId EnemyId = StableId.Parse("actor.enemy-live-test");
        private static readonly StableId PlayerId = StableId.Parse("actor.player-live-test");
        private static readonly StableId PlayerParticipantId =
            StableId.Parse("run-participant.player-live-test");
        private static readonly StableId PlayerCharacterId =
            StableId.Parse("character.player-live-test");
        private static readonly StableId PlayerFactionId = StableId.Parse("faction.player");

        [Test]
        public void RangedEnemy_AttacksOnlyInsideAttackArc()
        {
            RecordingRangedExecutor executor = new RecordingRangedExecutor();
            EnemyCombatActorRuntime runtime = RangedRuntime(
                EnemyCombatExampleDefinitions.MobileBlasterDroid(),
                executor);

            EnemyAttackCommitResult behind = runtime.EvaluateAndCommitAttack(
                Perception(runtime.Definition, -5d, 0d, true, 0L),
                new EnemyVector2(0d, 0d),
                1UL);
            Assert.That(behind.Status, Is.EqualTo(EnemyAttackCommitStatus.NoAttack));
            Assert.That(behind.Evaluation.Debug.SelectedTargetWithinVisionArc, Is.True);
            Assert.That(behind.Evaluation.Debug.SelectedTargetWithinAttackArc, Is.False);
            Assert.That(executor.CallCount, Is.Zero);

            EnemyAttackCommitResult ahead = runtime.EvaluateAndCommitAttack(
                Perception(runtime.Definition, 5d, 0d, true, 1L),
                new EnemyVector2(0d, 0d),
                2UL);
            Assert.That(ahead.Status, Is.EqualTo(EnemyAttackCommitStatus.RangedAccepted));
            Assert.That(executor.CallCount, Is.EqualTo(1));
            Assert.That(executor.LastIntent, Is.SameAs(ahead.LockedIntent));
            Assert.That(ahead.LockedIntent.CommittedDirection, Is.EqualTo(new EnemyVector2(1d, 0d)));
        }

        [Test]
        public void MeleeEnemy_CanPounceWithoutOwningWeapon()
        {
            PlayerActorAuthority player = PlayerAuthority();
            EnemyCombatActorRuntime runtime = new EnemyCombatActorRuntime(
                EnemyCombatExampleDefinitions.Pouncer(),
                EnemyId,
                GameplayEntityOwnership.None(),
                2,
                0L,
                null,
                new PlayerActorEnemyDamageRouter(player));

            EnemyAttackCommitResult committed = runtime.EvaluateAndCommitAttack(
                Perception(runtime.Definition, 1.5d, 0d, true, 10L),
                new EnemyVector2(0d, 0d),
                10UL);

            Assert.That(committed.Status, Is.EqualTo(EnemyAttackCommitStatus.PounceCommitted));
            Assert.That(committed.PounceCommitment, Is.Not.Null);
            Assert.That(committed.RangedExecution, Is.Null);
            Assert.That(committed.PounceCommitment.Direction, Is.EqualTo(new EnemyVector2(1d, 0d)));
            Assert.That(committed.PounceCommitment.TargetPoint, Is.EqualTo(new EnemyVector2(1.5d, 0d)));

            EnemyAttackImpactResult impact = runtime.ApplyLockedAttackImpact(
                StableId.Parse("event.pounce-impact"),
                committed.LockedIntent,
                PlayerId,
                true,
                0L);
            Assert.That(impact.Status, Is.EqualTo(EnemyAttackImpactStatus.Applied));
            Assert.That(player.ExportSnapshot().CurrentHealth, Is.EqualTo(92d));
        }

        [Test]
        public void EnemyCannotAttackThroughWalls()
        {
            RecordingRangedExecutor executor = new RecordingRangedExecutor();
            EnemyCombatActorRuntime runtime = RangedRuntime(
                EnemyCombatExampleDefinitions.MobileBlasterDroid(),
                executor);

            EnemyAttackCommitResult result = runtime.EvaluateAndCommitAttack(
                Perception(runtime.Definition, 5d, 0d, false, 0L),
                new EnemyVector2(0d, 0d),
                1UL);

            Assert.That(result.Status, Is.EqualTo(EnemyAttackCommitStatus.NoAttack));
            Assert.That(result.Evaluation.Debug.SelectedTargetHasLineOfSight, Is.False);
            Assert.That(result.Evaluation.Decision.RequestedAttack, Is.Null);
            Assert.That(executor.CallCount, Is.Zero);
        }

        [Test]
        public void EnemyCannotAttackTargetOutsideFacingDirection()
        {
            EnemyCombatDefinition definition = Definition(
                EnemyAttackCapabilityKind.RangedWeapon,
                90d,
                90d,
                8d,
                6);
            RecordingRangedExecutor executor = new RecordingRangedExecutor();
            EnemyCombatActorRuntime runtime = RangedRuntime(definition, executor);

            EnemyAttackCommitResult result = runtime.EvaluateAndCommitAttack(
                Perception(definition, -3d, 0d, true, 0L),
                new EnemyVector2(0d, 0d),
                1UL);

            Assert.That(result.Status, Is.EqualTo(EnemyAttackCommitStatus.NoAttack));
            Assert.That(result.Evaluation.Debug.SelectedTargetWithinVisionArc, Is.False);
            Assert.That(result.Evaluation.Debug.SelectedTargetWithinAttackArc, Is.False);
            Assert.That(executor.CallCount, Is.Zero);
        }

        [Test]
        public void EnemyDeath_IsIdempotentAndEmitsOnlyOnce()
        {
            EnemyCombatActorRuntime runtime = RangedRuntime(
                EnemyCombatExampleDefinitions.MobileBlasterDroid(),
                new RecordingRangedExecutor());
            DamageReceiverCommand lethal = IncomingDamage("enemy-lethal", 100d);

            EnemyIncomingDamageResult first = runtime.ApplyIncomingDamage(lethal);
            EnemyIncomingDamageResult duplicate = runtime.ApplyIncomingDamage(lethal);
            DamageReceiverCommand conflict = new DamageReceiverCommand(
                lethal.EventId,
                lethal.SourceActorId,
                lethal.SourceRunParticipantId,
                lethal.TargetActorId,
                99d,
                lethal.Channel,
                lethal.LifecycleGeneration);
            EnemyIncomingDamageResult conflicting = runtime.ApplyIncomingDamage(conflict);

            Assert.That(first.Status, Is.EqualTo(EnemyIncomingDamageStatus.Applied));
            Assert.That(first.EmittedDeathFact, Is.Not.Null);
            Assert.That(duplicate.Status, Is.EqualTo(EnemyIncomingDamageStatus.Duplicate));
            Assert.That(duplicate.EmittedDeathFact, Is.Null);
            Assert.That(conflicting.Status, Is.EqualTo(EnemyIncomingDamageStatus.ConflictingDuplicate));
            Assert.That(conflicting.EmittedDeathFact, Is.Null);
            Assert.That(runtime.LastDeathFact, Is.SameAs(first.EmittedDeathFact));
        }

        [Test]
        public void DeathFact_PreservesPlayerParticipantIdentity()
        {
            EnemyCombatActorRuntime runtime = RangedRuntime(
                EnemyCombatExampleDefinitions.MobileBlasterDroid(),
                new RecordingRangedExecutor());

            EnemyIncomingDamageResult result = runtime.ApplyIncomingDamage(
                IncomingDamage("enemy-attributed-lethal", 100d));

            Assert.That(result.EmittedDeathFact, Is.Not.Null);
            Assert.That(
                result.EmittedDeathFact.SourceRunParticipantId,
                Is.EqualTo(PlayerParticipantId));
            Assert.That(result.EmittedDeathFact.SourceEntityId, Is.EqualTo(PlayerId));
            Assert.That(result.EmittedDeathFact.TargetEntityId, Is.EqualTo(EnemyId));
        }

        [Test]
        public void XpValueAndDropProfile_ComeFromDefinitionFacts()
        {
            EnemyCombatDefinition definition = EnemyCombatExampleDefinitions.Pouncer();
            EnemyCombatActorRuntime runtime = MeleeRuntime(definition);

            EnemyIncomingDamageResult result = runtime.ApplyIncomingDamage(
                IncomingDamage("pouncer-reward-facts", 100d));

            Assert.That(result.EmittedDeathFact.XpValue, Is.EqualTo(definition.XpValue));
            Assert.That(result.EmittedDeathFact.DropProfileId, Is.EqualTo(definition.DropProfileId));
            Assert.That(result.EmittedDeathFact.Level, Is.EqualTo(definition.Level));
            Assert.That(
                runtime.CurrentProjection.Definition.RewardProfileIds,
                Is.EquivalentTo(new[] { definition.DropProfileId }));
        }

        [Test]
        public void DestroyedEnemy_NoLongerBlocksRoomClear()
        {
            EnemyCombatActorRuntime runtime = RangedRuntime(
                EnemyCombatExampleDefinitions.MobileBlasterDroid(),
                new RecordingRangedExecutor());
            Assert.That(runtime.BlocksRoomClear, Is.True);

            runtime.ApplyIncomingDamage(IncomingDamage("room-clear-lethal", 100d));

            Assert.That(runtime.State.IsDestroyed, Is.True);
            Assert.That(runtime.BlocksRoomClear, Is.False);
            Assert.That(runtime.CurrentProjection.BlocksRoomClear, Is.False);
        }

        [Test]
        public void PounceImpact_UsesPlayerAuthorityReplayAndConflictRules()
        {
            PlayerActorAuthority player = PlayerAuthority();
            EnemyCombatActorRuntime runtime = new EnemyCombatActorRuntime(
                EnemyCombatExampleDefinitions.Pouncer(),
                EnemyId,
                GameplayEntityOwnership.None(),
                2,
                0L,
                null,
                new PlayerActorEnemyDamageRouter(player));
            EnemyAttackCommitResult attack = runtime.EvaluateAndCommitAttack(
                Perception(runtime.Definition, 1d, 0d, true, 0L),
                new EnemyVector2(0d, 0d),
                5UL);
            StableId impactId = StableId.Parse("event.pounce-authority-impact");

            EnemyAttackImpactResult first = runtime.ApplyLockedAttackImpact(
                impactId,
                attack.LockedIntent,
                PlayerId,
                true,
                0L);
            EnemyAttackImpactResult duplicate = runtime.ApplyLockedAttackImpact(
                impactId,
                attack.LockedIntent,
                PlayerId,
                true,
                0L);
            EnemyAttackImpactResult conflicting = runtime.ApplyLockedAttackImpact(
                StableId.Parse("event.pounce-second-impact"),
                attack.LockedIntent,
                PlayerId,
                true,
                0L);

            Assert.That(first.Status, Is.EqualTo(EnemyAttackImpactStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(EnemyAttackImpactStatus.Duplicate));
            Assert.That(conflicting.Status, Is.EqualTo(EnemyAttackImpactStatus.Rejected));
            Assert.That(player.ExportSnapshot().CurrentHealth, Is.EqualTo(92d));
        }

        [Test]
        public void ExampleDefinitions_ContainAllRequiredCombatAndRewardFacts()
        {
            EnemyCombatDefinition ranged = EnemyCombatExampleDefinitions.MobileBlasterDroid();
            EnemyCombatDefinition melee = EnemyCombatExampleDefinitions.Pouncer();

            Assert.That(ranged.AttackKind, Is.EqualTo(EnemyAttackCapabilityKind.RangedWeapon));
            Assert.That(melee.AttackKind, Is.EqualTo(EnemyAttackCapabilityKind.MeleePounce));
            Assert.That(ranged.MaximumHealth, Is.GreaterThan(0d));
            Assert.That(ranged.Level, Is.GreaterThan(0));
            Assert.That(ranged.DetectionRadius, Is.GreaterThanOrEqualTo(ranged.MaximumAttackRange));
            Assert.That(ranged.VisionArcDegrees, Is.InRange(0.0001d, 360d));
            Assert.That(ranged.AttackArcDegrees, Is.InRange(0.0001d, 360d));
            Assert.That(ranged.CooldownTicks, Is.GreaterThan(0));
            Assert.That(ranged.Damage, Is.GreaterThan(0d));
            Assert.That(ranged.XpValue, Is.GreaterThanOrEqualTo(0L));
            Assert.That(ranged.FactionId, Is.Not.Null);
            Assert.That(ranged.PresentationReferenceId, Is.Not.Null);
            Assert.That(ranged.DropProfileId, Is.Not.Null);
            Assert.That(melee.PresentationReferenceId, Is.Not.Null);
            Assert.That(melee.DropProfileId, Is.Not.Null);
        }

        private static EnemyCombatActorRuntime RangedRuntime(
            EnemyCombatDefinition definition,
            IEnemyRangedAttackExecutor executor)
        {
            return new EnemyCombatActorRuntime(
                definition,
                EnemyId,
                GameplayEntityOwnership.None(),
                2,
                0L,
                executor,
                new PlayerActorEnemyDamageRouter(PlayerAuthority()));
        }

        private static EnemyCombatActorRuntime MeleeRuntime(EnemyCombatDefinition definition)
        {
            return new EnemyCombatActorRuntime(
                definition,
                EnemyId,
                GameplayEntityOwnership.None(),
                2,
                0L,
                null,
                new PlayerActorEnemyDamageRouter(PlayerAuthority()));
        }

        private static PlayerActorAuthority PlayerAuthority()
        {
            PlayerActorCreationResult creation = PlayerActorAuthority.TryCreate(
                new PlayerActorDefinition(
                    PlayerId,
                    PlayerParticipantId,
                    PlayerCharacterId,
                    PlayerFactionId,
                    100d,
                    0L));
            Assert.That(creation.IsCreated, Is.True);
            return creation.Authority;
        }

        private static EnemyPerceptionSnapshot Perception(
            EnemyCombatDefinition definition,
            double targetX,
            double targetY,
            bool hasLineOfSight,
            long tick)
        {
            return EnemyPerceptionBuilder.Build(
                new EnemyVector2(0d, 0d),
                new EnemyVector2(1d, 0d),
                new[]
                {
                    new EnemyPerceptionCandidate(
                        PlayerId,
                        PlayerFactionId,
                        EnemyTargetRelationship.Hostile,
                        new EnemyVector2(targetX, targetY),
                        new EnemyVector2(0d, 0d),
                        hasLineOfSight),
                },
                definition.DetectionRadius,
                definition.VisionArcDegrees,
                tick);
        }

        private static DamageReceiverCommand IncomingDamage(string eventId, double amount)
        {
            return new DamageReceiverCommand(
                StableId.Parse("event." + eventId),
                PlayerId,
                PlayerParticipantId,
                EnemyId,
                amount,
                CombatChannel.Kinetic,
                0L);
        }

        private static EnemyCombatDefinition Definition(
            EnemyAttackCapabilityKind kind,
            double visionArc,
            double attackArc,
            double damage,
            int cooldownTicks)
        {
            return new EnemyCombatDefinition(
                StableId.Parse("enemy.test-generic"),
                20d,
                3,
                10d,
                visionArc,
                attackArc,
                0d,
                3d,
                6d,
                cooldownTicks,
                damage,
                kind == EnemyAttackCapabilityKind.MeleePounce
                    ? CombatChannel.Contact
                    : CombatChannel.Kinetic,
                25L,
                StableId.Parse("faction.enemy"),
                EnemyRoomClearRole.RequiredEnemy,
                StableId.Parse("presentation.enemy-test"),
                StableId.Parse("drop-profile.enemy-test"),
                StableId.Parse("module.enemy-test-movement"),
                StableId.Parse("attack.enemy-test"),
                StableId.Parse("enemy-phase.test-ready"),
                StableId.Parse("enemy-phase.test-cooldown"),
                kind);
        }

        private sealed class RecordingRangedExecutor : IEnemyRangedAttackExecutor
        {
            public int CallCount { get; private set; }
            public EnemyAttackIntent LastIntent { get; private set; }

            public EnemyRangedExecutionResult TryExecute(
                EnemyAttackIntent lockedIntent,
                long lifecycleGeneration,
                long simulationTick,
                ulong deterministicSeed)
            {
                CallCount++;
                LastIntent = lockedIntent;
                return EnemyRangedExecutionResult.Accept(null);
            }
        }
    }
}

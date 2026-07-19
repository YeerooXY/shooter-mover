using System;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyRuntimeFoundationTests
    {
        [Test]
        public void SameDefinition_ProducesIndependentStableInstancesAndLifecycleGenerations()
        {
            EnemyRuntimeProjection first = Runtime("enemy-a", EnemyRoomClearRole.RequiredEnemy, 0L);
            EnemyRuntimeProjection second = Runtime("enemy-b", EnemyRoomClearRole.RequiredEnemy, 0L);
            EnemyRuntimeProjection restarted = Runtime("enemy-a", EnemyRoomClearRole.RequiredEnemy, 1L);

            Assert.That(first.Definition.DefinitionId, Is.EqualTo(second.Definition.DefinitionId));
            Assert.That(first.Identity, Is.Not.EqualTo(second.Identity));
            Assert.That(first.ActorState, Is.Not.SameAs(second.ActorState));
            Assert.That(first.Identity, Is.EqualTo(restarted.Identity));
            Assert.That(first.Identity.GetHashCode(), Is.EqualTo(restarted.Identity.GetHashCode()));
            Assert.That(restarted.LifecycleGeneration, Is.EqualTo(1L));
        }

        [Test]
        public void RoomClearProjection_UsesRoleAndCanonicalTerminalState()
        {
            EnemyRuntimeProjection required = Runtime("required", EnemyRoomClearRole.RequiredEnemy, 0L);
            EnemyRuntimeProjection optional = Runtime("optional", EnemyRoomClearRole.OptionalEnemy, 0L);
            EnemyActorStepResult killed = EnemyActorStepper.Step(required.ActorState, new[]
            {
                EnemyActorCommand.Damage(0L, Id("event", "kill"), Id("entity", "player"), 1, 100d),
            });
            EnemyRuntimeProjection terminal = Project(killed.State, EnemyRoomClearRole.RequiredEnemy, 0L);

            Assert.That(required.BlocksRoomClear, Is.True);
            Assert.That(optional.BlocksRoomClear, Is.False);
            Assert.That(terminal.BlocksRoomClear, Is.False);
        }

        [Test]
        public void CanonicalDeathFact_PreservesSeparateMultiplayerAttribution()
        {
            EnemyRuntimeProjection runtime = Runtime("attributed", EnemyRoomClearRole.RequiredEnemy, 3L);
            EnemyActorStepResult killed = EnemyActorStepper.Step(runtime.ActorState, new[]
            {
                EnemyActorCommand.Damage(0L, Id("event", "attributed-kill"),
                    Id("entity", "source-player"), 1, 100d),
            });
            EnemyDestroyedNotification destroyed = null;
            foreach (EnemyActorNotification notification in killed.Notifications)
            {
                destroyed = destroyed ?? notification as EnemyDestroyedNotification;
            }
            EnemyAttributedDeathFact fact = new EnemyAttributedDeathFact(
                destroyed, Id("participant", "player-one"), runtime.LifecycleGeneration);

            Assert.That(fact.EventId, Is.EqualTo(destroyed.EventId));
            Assert.That(fact.SourceEntityId, Is.EqualTo(Id("entity", "source-player")));
            Assert.That(fact.SourceRunParticipantId, Is.EqualTo(Id("participant", "player-one")));
            Assert.That(fact.TargetEntityId, Is.EqualTo(runtime.Identity.EntityInstanceId));
            Assert.That(fact.LifecycleGeneration, Is.EqualTo(3L));
        }

        [Test]
        public void IdenticalPerception_SelectsNearestThenStableIdentity()
        {
            EnemyPerceivedTarget beta = Target("beta", 3d, true, true, true);
            EnemyPerceivedTarget alpha = Target("alpha", 3d, true, true, true);
            EnemyDecisionEvaluation forward = Evaluate(new[] { beta, alpha });
            EnemyDecisionEvaluation reverse = Evaluate(new[] { alpha, beta });

            Assert.That(forward.Decision.SelectedTargetId, Is.EqualTo(Id("entity", "alpha")));
            Assert.That(reverse.Decision.SelectedTargetId, Is.EqualTo(forward.Decision.SelectedTargetId));
            Assert.That(reverse.Decision.RequestedAttack.DecisionId,
                Is.EqualTo(forward.Decision.RequestedAttack.DecisionId));
        }

        [Test]
        public void PerceptionBuilder_ComputesDistanceDirectionAndDetectionFromPositions()
        {
            EnemyPerceptionSnapshot snapshot = EnemyPerceptionBuilder.Build(
                new EnemyVector2(2d, 3d),
                new EnemyVector2(1d, 0d),
                new[]
                {
                    Candidate("boundary", new EnemyVector2(5d, 7d), true),
                    Candidate("outside", new EnemyVector2(5.01d, 7d), false),
                },
                5d,
                360d,
                9L);

            Assert.That(snapshot.Targets[0].Distance, Is.EqualTo(5d));
            Assert.That(snapshot.Targets[0].Direction.X, Is.EqualTo(0.6d).Within(0.000000001d));
            Assert.That(snapshot.Targets[0].Direction.Y, Is.EqualTo(0.8d).Within(0.000000001d));
            Assert.That(snapshot.Targets[0].IsWithinDetectionRange, Is.True);
            Assert.That(snapshot.Targets[0].HasLineOfSight, Is.True);
            Assert.That(snapshot.Targets[1].Distance, Is.GreaterThan(5d));
            Assert.That(snapshot.Targets[1].IsWithinDetectionRange, Is.False);
            Assert.That(snapshot.Targets[1].HasLineOfSight, Is.False);
        }

        [Test]
        public void PerceptionBuilder_ComputesVisionArcIncludingExactBoundary()
        {
            double boundaryRadians = Math.PI / 4d;
            double outsideRadians = boundaryRadians + 0.001d;
            EnemyPerceptionSnapshot snapshot = EnemyPerceptionBuilder.Build(
                new EnemyVector2(0d, 0d),
                new EnemyVector2(1d, 0d),
                new[]
                {
                    Candidate("boundary", new EnemyVector2(
                        Math.Cos(boundaryRadians), Math.Sin(boundaryRadians)), true),
                    Candidate("outside", new EnemyVector2(
                        Math.Cos(outsideRadians), Math.Sin(outsideRadians)), true),
                },
                2d,
                90d,
                10L);

            Assert.That(snapshot.Targets[0].IsWithinVisionArc, Is.True);
            Assert.That(snapshot.Targets[1].IsWithinVisionArc, Is.False);
        }

        [Test]
        public void DecisionEvaluation_IsReadOnlyOverCanonicalEnemyActor()
        {
            EnemyRuntimeProjection runtime = Runtime(
                "read-only-decision",
                EnemyRoomClearRole.RequiredEnemy,
                0L);
            EnemyActorState before = runtime.ActorState;

            EnemyDecisionEvaluation result = EnemyDecisionPolicy.Evaluate(
                runtime,
                new EnemyDecisionProfile(8d, 1d, 3d, 4d, 90d,
                    Id("attack", "melee"), Id("enemy-phase", "ready")),
                EnemyPerceptionBuilder.Build(
                    new EnemyVector2(0d, 0d),
                    new EnemyVector2(1d, 0d),
                    new[] { Candidate("target", new EnemyVector2(3d, 0d), true) },
                    8d,
                    90d,
                    11L));

            Assert.That(result.Decision.RequestedAttack, Is.Not.Null);
            Assert.That(runtime.ActorState, Is.SameAs(before));
            Assert.That(runtime.ActorState.Health, Is.EqualTo(100d));
            Assert.That(runtime.ActorState.ProcessedEventIds, Is.Empty);
        }

        [TestCase(0.99d, EnemyMovementIntentKind.Retreat, false)]
        [TestCase(1d, EnemyMovementIntentKind.Hold, true)]
        [TestCase(4d, EnemyMovementIntentKind.Hold, true)]
        [TestCase(4.01d, EnemyMovementIntentKind.Approach, false)]
        [TestCase(8d, EnemyMovementIntentKind.Approach, false)]
        [TestCase(8.01d, EnemyMovementIntentKind.Hold, false)]
        public void DetectionAndAttackRangeBoundaries_AreExplicit(
            double distance,
            EnemyMovementIntentKind expectedMovement,
            bool expectsAttack)
        {
            EnemyDecisionEvaluation result = Evaluate(new[] { Target("target", distance, true, true, true) });

            Assert.That(result.Decision.MovementKind, Is.EqualTo(expectedMovement));
            Assert.That(result.Decision.RequestedAttack != null, Is.EqualTo(expectsAttack));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void LineOfSightAndArcBoundary_BlockAttack(bool lineOfSight, bool withinArc)
        {
            EnemyDecisionEvaluation result = Evaluate(
                new[] { Target("target", 3d, lineOfSight, true, withinArc) });

            Assert.That(result.Decision.RequestedAttack, Is.Null);
            Assert.That(result.Decision.MovementKind, Is.EqualTo(EnemyMovementIntentKind.Approach));
            Assert.That(result.Debug.SelectedTargetDistance, Is.EqualTo(3d));
            Assert.That(result.Debug.SelectedTargetHasLineOfSight, Is.EqualTo(lineOfSight));
            Assert.That(result.Debug.SelectedTargetWithinDetectionRange, Is.True);
            Assert.That(result.Debug.SelectedTargetWithinVisionArc, Is.EqualTo(withinArc));
        }

        [Test]
        public void VisibleTargetOutsideAttackArc_CannotFire()
        {
            EnemyRuntimeProjection runtime = Runtime(
                "attack-arc-observer",
                EnemyRoomClearRole.RequiredEnemy,
                0L);
            EnemyPerceivedTarget visibleBehind = new EnemyPerceivedTarget(
                Id("entity", "visible-behind"),
                Id("faction", "player"),
                EnemyTargetRelationship.Hostile,
                new EnemyVector2(-3d, 0d),
                new EnemyVector2(0d, 0d),
                3d,
                new EnemyVector2(-1d, 0d),
                true,
                true,
                true);

            EnemyDecisionEvaluation result = EnemyDecisionPolicy.Evaluate(
                runtime,
                new EnemyDecisionProfile(8d, 1d, 3d, 4d, 90d,
                    Id("attack", "melee"), Id("enemy-phase", "ready")),
                new EnemyPerceptionSnapshot(
                    new EnemyVector2(0d, 0d),
                    new EnemyVector2(1d, 0d),
                    new[] { visibleBehind },
                    43L));

            Assert.That(result.Decision.SelectedTargetId, Is.EqualTo(visibleBehind.EntityId));
            Assert.That(result.Decision.RequestedAttack, Is.Null);
            Assert.That(result.Decision.MovementKind, Is.EqualTo(EnemyMovementIntentKind.Approach));
            Assert.That(result.Debug.SelectedTargetWithinVisionArc, Is.True);
            Assert.That(result.Debug.SelectedTargetWithinAttackArc, Is.False);
        }

        [Test]
        public void VisibleTargetInsideAttackArc_CanFire()
        {
            EnemyDecisionEvaluation result = Evaluate(
                new[] { Target("front", 3d, true, true, true) });

            Assert.That(result.Decision.RequestedAttack, Is.Not.Null);
            Assert.That(result.Debug.SelectedTargetWithinAttackArc, Is.True);
        }

        [Test]
        public void NoValidTarget_ProducesNoAttackAndTruthfulDebugSnapshot()
        {
            EnemyDecisionEvaluation result = Evaluate(
                new[] { Target("friendly", 2d, true, true, true, EnemyTargetRelationship.Friendly) });

            Assert.That(result.Decision.SelectedTargetId, Is.Null);
            Assert.That(result.Decision.RequestedAttack, Is.Null);
            Assert.That(result.Debug.SelectedTargetId, Is.EqualTo(result.Decision.SelectedTargetId));
            Assert.That(result.Debug.RequestedAttack, Is.SameAs(result.Decision.RequestedAttack));
            Assert.That(result.Debug.DecisionReasonCode, Is.EqualTo(result.Decision.ReasonCode));
            Assert.That(result.Debug.DesiredMovement, Is.EqualTo(result.Decision.DesiredMovement));
            Assert.That(result.Debug.CurrentFacing, Is.EqualTo(new EnemyVector2(1d, 0d)));
        }

        [Test]
        public void WeaponlessPounce_EmitsGenericIntentAndDoesNotRetargetAfterCommitment()
        {
            EnemyDecisionEvaluation evaluation = Evaluate(
                new[] { Target("original", 3d, true, true, true) }, "pounce");
            EnemyPounceCommitment commitment = new EnemyPounceCommitment(
                evaluation.Decision.RequestedAttack);
            EnemyPounceImpactOpportunity impact = commitment.ObserveImpact(
                Id("entity", "different-target"), true);

            Assert.That(commitment.AttackIntent.AttackId, Is.EqualTo(Id("attack", "pounce")));
            Assert.That(commitment.CommittedTargetId, Is.EqualTo(Id("entity", "original")));
            Assert.That(impact.ContactedEntityId, Is.EqualTo(Id("entity", "different-target")));
            Assert.That(impact.Commitment.Direction, Is.EqualTo(commitment.Direction));
            Assert.That(impact.Commitment.TargetPoint, Is.EqualTo(commitment.TargetPoint));
        }

        private static EnemyDecisionEvaluation Evaluate(
            EnemyPerceivedTarget[] targets,
            string attack = "melee")
        {
            return EnemyDecisionPolicy.Evaluate(
                Runtime("observer", EnemyRoomClearRole.RequiredEnemy, 0L),
                new EnemyDecisionProfile(8d, 1d, 3d, 4d, 90d,
                    Id("attack", attack), Id("enemy-phase", "ready")),
                new EnemyPerceptionSnapshot(new EnemyVector2(0d, 0d),
                    new EnemyVector2(1d, 0d), targets, 42L));
        }

        private static EnemyPerceivedTarget Target(
            string id,
            double distance,
            bool lineOfSight,
            bool detected,
            bool withinArc,
            EnemyTargetRelationship relationship = EnemyTargetRelationship.Hostile)
        {
            return new EnemyPerceivedTarget(Id("entity", id), Id("faction", "player"), relationship,
                new EnemyVector2(distance, 0d), new EnemyVector2(0d, 0d), distance,
                new EnemyVector2(1d, 0d), lineOfSight, detected, withinArc);
        }

        private static EnemyPerceptionCandidate Candidate(
            string id,
            EnemyVector2 position,
            bool lineOfSight)
        {
            return new EnemyPerceptionCandidate(
                Id("entity", id),
                Id("faction", "player"),
                EnemyTargetRelationship.Hostile,
                position,
                new EnemyVector2(0d, 0d),
                lineOfSight);
        }

        private static EnemyRuntimeProjection Runtime(
            string entity,
            EnemyRoomClearRole role,
            long generation)
        {
            return Project(EnemyActorState.Create(Id("entity", entity), Id("enemy", "fixture"), 100d, 2,
                EnemyContactPolicy.Create(EnemyContactMode.OrdinaryDamage, 10d, 0.5d, 0.02d, 4)), role, generation);
        }

        private static EnemyRuntimeProjection Project(
            EnemyActorState state,
            EnemyRoomClearRole role,
            long generation)
        {
            return new EnemyRuntimeProjection(
                new GameplayEntityIdentity(state.ActorId, GameplayEntityOwnership.None(), Id("faction", "enemy")),
                new EnemyDefinitionProjection(state.RoleId, Id("movement", "fixture"),
                    new[] { Id("attack", "melee") }, new[] { Id("reward", "fixture") }, role),
                state, generation, null, Id("enemy-phase", "ready"));
        }

        private static StableId Id(string ns, string value) { return StableId.Create(ns, value); }
    }
}

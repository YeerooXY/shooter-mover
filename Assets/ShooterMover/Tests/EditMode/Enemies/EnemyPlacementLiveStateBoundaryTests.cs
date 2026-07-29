using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyPlacementLiveFactoryTestsStateBoundaries
    {
        [Test]
        public void FabricatedDecision_CannotDriveMovement()
        {
            Fixture fixture = new Fixture("movement-forgery");
            EnemyPlacementDecision fabricated = FabricatedDecisionFor(
                fixture.Runtime,
                new Fixture("movement-other").Runtime.Evaluate(Perception(1L)));

            Assert.Throws<InvalidOperationException>(() => fixture.Runtime.RealizeMovement(
                fabricated,
                MovementContext(fixture.Runtime, 1L)));
        }

        [Test]
        public void FabricatedDecision_CannotExecuteAttack()
        {
            Fixture fixture = new Fixture("attack-forgery");
            EnemyPlacementDecision fabricated = FabricatedDecisionFor(
                fixture.Runtime,
                new Fixture("attack-other").Runtime.Evaluate(Perception(2L)));

            EnemyAttackExecutionResult result = fixture.Runtime.TryExecuteAttack(
                fabricated,
                Id("enemy-operation", "fabricated-decision"),
                1d);

            Assert.That(result.Status, Is.EqualTo(EnemyLiveOperationStatus.Rejected));
            Assert.That(result.Rejection, Is.EqualTo(EnemyLiveRejectionCode.DecisionNotIssued));
            Assert.That(fixture.Ports.AttackEffectCount, Is.Zero);
        }

        [Test]
        public void AlteredCopyOfIssuedDecision_Rejects()
        {
            Fixture fixture = new Fixture("altered-decision");
            EnemyPlacementDecision first = fixture.Runtime.Evaluate(Perception(3L));
            EnemyPlacementDecision second = fixture.Runtime.Evaluate(Perception(4L));
            var altered = new EnemyPlacementDecision(
                fixture.Runtime.SpawnStableId,
                fixture.Runtime.LifecycleGeneration,
                first.Perception,
                second.Evaluation);

            EnemyAttackExecutionResult result = fixture.Runtime.TryExecuteAttack(
                altered,
                Id("enemy-operation", "altered-decision"),
                1d);

            Assert.That(result.Rejection, Is.EqualTo(EnemyLiveRejectionCode.DecisionNotIssued));
            Assert.That(fixture.Ports.AttackEffectCount, Is.Zero);
        }

        [Test]
        public void ExactImmutableCopyOfIssuedDecision_RemainsUsable()
        {
            Fixture fixture = new Fixture("exact-decision-copy");
            EnemyPlacementDecision issued = fixture.Runtime.Evaluate(Perception(5L));
            var exactCopy = new EnemyPlacementDecision(
                issued.EntityInstanceId,
                issued.LifecycleGeneration,
                issued.Perception,
                issued.Evaluation);

            EnemyMovementRealization movement = fixture.Runtime.RealizeMovement(
                exactCopy,
                MovementContext(fixture.Runtime, 5L));
            EnemyAttackExecutionResult attack = fixture.Runtime.TryExecuteAttack(
                exactCopy,
                Id("enemy-operation", "exact-issued-copy"),
                1d);

            Assert.That(movement, Is.Not.Null);
            Assert.That(attack.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(fixture.Ports.AttackEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void ExactAttackReplay_EmitsEffectOnce()
        {
            Fixture fixture = new Fixture("attack-replay");
            EnemyPlacementDecision decision = fixture.Runtime.Evaluate(Perception(6L));
            StableId operation = Id("enemy-operation", "exact-attack-replay");

            EnemyAttackExecutionResult first = fixture.Runtime.TryExecuteAttack(decision, operation, 1d);
            EnemyAttackExecutionResult replay = fixture.Runtime.TryExecuteAttack(decision, operation, 1d);

            Assert.That(first.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(replay.Status, Is.EqualTo(EnemyLiveOperationStatus.ExactReplay));
            Assert.That(replay.Request, Is.SameAs(first.Request));
            Assert.That(fixture.Ports.AttackEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void SameAttackOperationWithChangedIssuedDecisionOrPerception_Conflicts()
        {
            Fixture fixture = new Fixture("changed-decision-replay");
            EnemyPlacementDecision firstDecision = fixture.Runtime.Evaluate(Perception(7L));
            EnemyPlacementDecision changedDecision = fixture.Runtime.Evaluate(Perception(8L));
            StableId operation = Id("enemy-operation", "changed-decision-replay");

            fixture.Runtime.TryExecuteAttack(firstDecision, operation, 1d);
            EnemyAttackExecutionResult conflict = fixture.Runtime.TryExecuteAttack(
                changedDecision,
                operation,
                1d);

            Assert.That(conflict.Rejection, Is.EqualTo(EnemyLiveRejectionCode.ConflictingDuplicate));
            Assert.That(fixture.Ports.AttackEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void SameAttackOperationWithChangedOccurrenceTime_Conflicts()
        {
            Fixture fixture = new Fixture("changed-time-replay");
            EnemyPlacementDecision decision = fixture.Runtime.Evaluate(Perception(9L));
            StableId operation = Id("enemy-operation", "changed-time-replay");

            fixture.Runtime.TryExecuteAttack(decision, operation, 1d);
            EnemyAttackExecutionResult conflict = fixture.Runtime.TryExecuteAttack(
                decision,
                operation,
                2d);

            Assert.That(conflict.Rejection, Is.EqualTo(EnemyLiveRejectionCode.ConflictingDuplicate));
            Assert.That(fixture.Ports.AttackEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void SameAttackOperationWithChangedTargetingFacts_Conflicts()
        {
            Fixture fixture = new Fixture("changed-targeting-replay");
            EnemyPlacementDecision decision = fixture.Runtime.Evaluate(Perception(10L));
            StableId operation = Id("enemy-operation", "changed-targeting-replay");
            var exactContext = new EnemyTargetingAimContext(decision.Perception, 1d);
            var changedContext = new EnemyTargetingAimContext(Perception(10L, 4d), 1d);

            fixture.Runtime.TryExecuteAttack(decision, exactContext, operation, 1d);
            EnemyAttackExecutionResult conflict = fixture.Runtime.TryExecuteAttack(
                decision,
                changedContext,
                operation,
                1d);

            Assert.That(conflict.Rejection, Is.EqualTo(EnemyLiveRejectionCode.ConflictingDuplicate));
            Assert.That(fixture.Ports.AttackEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void FabricatedExecutionRequest_CannotRoutePlayerDamage()
        {
            Fixture fixture = new Fixture("fabricated-execution");
            EnemyAttackExecutionResult accepted = Execute(fixture, 11L, "real-execution");
            EnemyAttackExecutionRequest fabricated = CopyExecution(
                accepted.Request,
                operation: Id("enemy-operation", "never-issued"));

            EnemyPlayerDamagePortResult result = fixture.Runtime.RoutePlayerImpact(
                fabricated,
                Id("enemy-hit", "fabricated-execution"),
                Id("entity", "player"));

            Assert.That(result.Rejection, Is.EqualTo(EnemyLiveRejectionCode.ExecutionNotIssued));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.Zero);
        }

        [Test]
        public void IssuedExecutionWithAlteredDamageOrCooldown_Rejects()
        {
            Fixture fixture = new Fixture("altered-damage");
            EnemyAttackExecutionRequest issued = Execute(fixture, 12L, "altered-damage").Request;
            EnemyAttackExecutionRequest altered = CopyExecution(
                issued,
                damage: issued.ResolvedDamage + 1d);

            EnemyPlayerDamagePortResult damageResult = fixture.Runtime.RoutePlayerImpact(
                altered,
                Id("enemy-hit", "altered-damage"),
                Id("entity", "player"));
            EnemyPlayerDamagePortResult cooldownResult = fixture.Runtime.RoutePlayerImpact(
                CopyExecution(issued, cooldown: issued.ResolvedCooldownSeconds + 1d),
                Id("enemy-hit", "altered-cooldown"),
                Id("entity", "player"));

            Assert.That(damageResult.Rejection, Is.EqualTo(EnemyLiveRejectionCode.InvalidCommand));
            Assert.That(cooldownResult.Rejection, Is.EqualTo(EnemyLiveRejectionCode.InvalidCommand));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.Zero);
        }

        [Test]
        public void IssuedExecutionWithAlteredCommittedIntent_Rejects()
        {
            Fixture fixture = new Fixture("altered-intent");
            EnemyAttackExecutionRequest issued = Execute(fixture, 13L, "altered-intent").Request;
            EnemyAttackIntent original = issued.CommittedIntent;
            var alteredIntent = new EnemyAttackIntent(
                original.AttackerEntityId,
                original.SourceRunParticipantId,
                original.TargetEntityId,
                original.AttackId,
                original.CommittedOrigin,
                new EnemyVector2(0d, 1d),
                original.CommittedTargetPoint,
                original.DecisionId,
                original.BehaviorPhaseId,
                original.ReasonCode);

            EnemyPlayerDamagePortResult result = fixture.Runtime.RoutePlayerImpact(
                CopyExecution(issued, intent: alteredIntent),
                Id("enemy-hit", "altered-intent"),
                Id("entity", "player"));

            Assert.That(result.Rejection, Is.EqualTo(EnemyLiveRejectionCode.InvalidCommand));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.Zero);
        }

        [Test]
        public void IssuedExecutionWithAlteredDescriptorOrAttackId_Rejects()
        {
            Fixture fixture = new Fixture("altered-descriptor");
            EnemyAttackExecutionRequest issued = Execute(fixture, 14L, "altered-descriptor").Request;
            EnemyAttackCapabilityDescriptor original = issued.Descriptor;
            var alteredDescriptor = new EnemyAttackCapabilityDescriptor(
                Id("enemy-attack-profile", "forged-attack-id"),
                original.CapabilityId,
                original.SelectionPriority,
                original.AttackArcDegrees,
                original.MinimumAttackRange,
                original.PreferredAttackRange,
                original.MaximumAttackRange,
                original.CooldownSeconds,
                original.Damage,
                original.DamageChannelId,
                original.Projectile,
                original.Area,
                original.Melee);

            EnemyPlayerDamagePortResult result = fixture.Runtime.RoutePlayerImpact(
                CopyExecution(issued, descriptor: alteredDescriptor),
                Id("enemy-hit", "altered-descriptor"),
                Id("entity", "player"));

            Assert.That(result.Rejection, Is.EqualTo(EnemyLiveRejectionCode.InvalidCommand));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.Zero);
        }

        [Test]
        public void ExactIssuedExecution_RoutesThroughPlayerDamagePort()
        {
            Fixture fixture = new Fixture("exact-execution");
            EnemyAttackExecutionRequest issued = Execute(fixture, 15L, "exact-execution").Request;

            EnemyPlayerDamagePortResult result = fixture.Runtime.RoutePlayerImpact(
                issued,
                Id("enemy-hit", "exact-execution"),
                Id("entity", "player"));

            Assert.That(result.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.EqualTo(1));
        }

        [Test]
        public void ProjectileIssuedBeforeEnemyDeath_StillDamagesAfterDeath()
        {
            Fixture fixture = new Fixture("post-death-projectile");
            EnemyAttackExecutionRequest issued = Execute(
                fixture,
                16L,
                "post-death-projectile").Request;
            Kill(fixture.Runtime, "post-death-projectile");

            EnemyPlayerDamagePortResult result = fixture.Runtime.RoutePlayerImpact(
                issued,
                Id("enemy-hit", "post-death-projectile"),
                Id("entity", "player"));

            Assert.That(fixture.Runtime.ActorState.IsActive, Is.False);
            Assert.That(result.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.EqualTo(1));
        }

        [Test]
        public void OldProjectile_RejectsAfterLifecycleRestart()
        {
            Fixture fixture = new Fixture("restart-projectile");
            EnemyAttackExecutionRequest oldExecution = Execute(
                fixture,
                17L,
                "old-projectile").Request;
            EnemyPlacementLiveInstance restarted = fixture.CreateRuntime(2L);

            EnemyPlayerDamagePortResult result = restarted.RoutePlayerImpact(
                oldExecution,
                Id("enemy-hit", "old-projectile-after-restart"),
                Id("entity", "player"));

            Assert.That(restarted.SpawnStableId, Is.EqualTo(fixture.Runtime.SpawnStableId));
            Assert.That(result.Rejection, Is.EqualTo(EnemyLiveRejectionCode.StaleLifecycle));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.Zero);
        }

        [Test]
        public void NewAttackCannotBeIssuedAfterEnemyDeath()
        {
            Fixture fixture = new Fixture("attack-after-death");
            EnemyPlacementDecision issuedBeforeDeath = fixture.Runtime.Evaluate(Perception(18L));
            Kill(fixture.Runtime, "attack-after-death");

            EnemyAttackExecutionResult result = fixture.Runtime.TryExecuteAttack(
                issuedBeforeDeath,
                Id("enemy-operation", "new-after-death"),
                2d);

            Assert.That(result.Rejection, Is.EqualTo(EnemyLiveRejectionCode.ActorTerminal));
            Assert.That(fixture.Ports.AttackEffectCount, Is.Zero);
        }

        [Test]
        public void ExactHitEventReplay_RoutesPlayerDamageOnce()
        {
            Fixture fixture = new Fixture("hit-replay");
            EnemyAttackExecutionRequest execution = Execute(fixture, 19L, "hit-replay").Request;
            StableId hit = Id("enemy-hit", "exact-hit-replay");

            EnemyPlayerDamagePortResult first = fixture.Runtime.RoutePlayerImpact(
                execution,
                hit,
                Id("entity", "player"));
            EnemyPlayerDamagePortResult replay = fixture.Runtime.RoutePlayerImpact(
                execution,
                hit,
                Id("entity", "player"));

            Assert.That(first.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(replay.Status, Is.EqualTo(EnemyLiveOperationStatus.ExactReplay));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingHitEventReuse_RejectsWithoutMutation()
        {
            Fixture fixture = new Fixture("hit-conflict");
            EnemyAttackExecutionRequest execution = Execute(fixture, 20L, "hit-conflict").Request;
            StableId hit = Id("enemy-hit", "conflicting-hit");
            fixture.Runtime.RoutePlayerImpact(execution, hit, Id("entity", "player-one"));

            EnemyPlayerDamagePortResult conflict = fixture.Runtime.RoutePlayerImpact(
                execution,
                hit,
                Id("entity", "player-two"));

            Assert.That(conflict.Rejection, Is.EqualTo(EnemyLiveRejectionCode.ConflictingDuplicate));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.EqualTo(1));
        }

        [Test]
        public void MultipleDistinctHitIds_CanReferenceSameAcceptedExecution()
        {
            Fixture fixture = new Fixture("multi-hit");
            EnemyAttackExecutionRequest execution = Execute(fixture, 21L, "multi-hit").Request;

            EnemyPlayerDamagePortResult first = fixture.Runtime.RoutePlayerImpact(
                execution,
                Id("enemy-hit", "multi-hit-one"),
                Id("entity", "player-one"));
            EnemyPlayerDamagePortResult second = fixture.Runtime.RoutePlayerImpact(
                execution,
                Id("enemy-hit", "multi-hit-two"),
                Id("entity", "player-two"));

            Assert.That(first.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(second.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.EqualTo(2));
        }

        [Test]
        public void ObserverPositionConfiguration_IsRemovedAndCannotPromiseUnenforcedBehavior()
        {
            Assert.That(
                typeof(EnemyPerceptionPolicyConfiguration).GetProperty(
                    "RequireMatchingObserverPosition"),
                Is.Null);
            Assert.DoesNotThrow(() => new EnemyPerceptionPolicyConfiguration(
                Id("enemy-perception", "authoritative-facts-only")));
            Assert.Throws<ArgumentException>(() => new EnemyPerceptionPolicyConfiguration(
                Id("enemy-perception", "unsupported-position-match"),
                true));
        }

        private static EnemyAttackExecutionResult Execute(
            Fixture fixture,
            long tick,
            string operation)
        {
            EnemyPlacementDecision decision = fixture.Runtime.Evaluate(Perception(tick));
            EnemyAttackExecutionResult result = fixture.Runtime.TryExecuteAttack(
                decision,
                Id("enemy-operation", operation),
                1d);
            Assert.That(result.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            return result;
        }

        private static void Kill(EnemyPlacementLiveInstance runtime, string operation)
        {
            EnemyLiveDamageResult result = runtime.ApplyDamage(
                new EnemyLiveDamageCommand(
                    Id("enemy-damage", operation),
                    Id("entity", "player"),
                    Id("run-participant", "player"),
                    runtime.SpawnStableId,
                    runtime.LifecycleGeneration,
                    0L,
                    1,
                    10000d));
            Assert.That(result.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(result.DeathFact, Is.Not.Null);
        }

        private static EnemyPlacementDecision FabricatedDecisionFor(
            EnemyPlacementLiveInstance runtime,
            EnemyPlacementDecision foreign)
        {
            return new EnemyPlacementDecision(
                runtime.SpawnStableId,
                runtime.LifecycleGeneration,
                foreign.Perception,
                foreign.Evaluation);
        }

        private static EnemyMovementRealizationContext MovementContext(
            EnemyPlacementLiveInstance runtime,
            long tick)
        {
            return new EnemyMovementRealizationContext(
                runtime.SpawnStableId,
                runtime.RoomStableId,
                new EnemyVector2(0d, 0d),
                new EnemyVector2(1d, 0d),
                tick,
                99d,
                null);
        }

        private static EnemyPerceptionSnapshot Perception(long tick, double distance = 3d)
        {
            return new EnemyPerceptionSnapshot(
                new EnemyVector2(0d, 0d),
                new EnemyVector2(1d, 0d),
                new[]
                {
                    new EnemyPerceivedTarget(
                        Id("entity", "player"),
                        Id("faction", "players"),
                        EnemyTargetRelationship.Hostile,
                        new EnemyVector2(distance, 0d),
                        new EnemyVector2(0d, 0d),
                        distance,
                        new EnemyVector2(1d, 0d),
                        true,
                        true,
                        true),
                },
                tick);
        }

        private static EnemyAttackExecutionRequest CopyExecution(
            EnemyAttackExecutionRequest source,
            StableId operation = null,
            EnemyAttackCapabilityDescriptor descriptor = null,
            EnemyAttackIntent intent = null,
            double? damage = null,
            double? cooldown = null)
        {
            return new EnemyAttackExecutionRequest(
                operation ?? source.OperationStableId,
                source.Identity,
                source.LifecycleGeneration,
                source.OccurredAtSeconds,
                descriptor ?? source.Descriptor,
                intent ?? source.CommittedIntent,
                source.ItemInstanceStableId,
                source.ExecutionKind,
                damage ?? source.ResolvedDamage,
                cooldown ?? source.ResolvedCooldownSeconds);
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }

        private sealed class Fixture
        {
            private readonly string name;

            public Fixture(string name)
            {
                this.name = name;
                Ports = new RecordingPorts();
                Definition = CreateDefinition(name);
                Factory = new EnemyPlacementLiveFactory(
                    new RoomContentObjectCatalog(new[]
                    {
                        new RoomContentObjectDefinition(
                            Id("room-object", name),
                            RoomContentObjectKind.Enemy,
                            Definition.DefinitionId,
                            Definition.PresentationId),
                    }),
                    new EnemyCatalog(
                        1,
                        Id("enemy-catalog", name + "-v1"),
                        new[] { Definition }),
                    BuiltInEnemyLivePolicyRegistry.Create(),
                    new DeterministicEnemyLiveIdentityDeriver(),
                    new EnemyDifficultyLiveRegistration(
                        new EnemyDifficultyScalingConfiguration(
                            Id("enemy-difficulty", "test-scalar"),
                            1d,
                            0.5d,
                            0.2d,
                            0.15d),
                        new ScalarEnemyDifficultyScalingPolicy()),
                    new EnemyPerceptionLiveRegistration(
                        new EnemyPerceptionPolicyConfiguration(
                            Id("enemy-perception", "test-validated")),
                        new ValidatedEnemyPerceptionLiveBridge()),
                    Ports.Bundle);
                Runtime = CreateRuntime(1L);
            }

            public RecordingPorts Ports { get; }
            public EnemyDefinition Definition { get; }
            public EnemyPlacementLiveFactory Factory { get; }
            public EnemyPlacementLiveInstance Runtime { get; }

            public EnemyPlacementLiveInstance CreateRuntime(long generation)
            {
                return Factory.Create(
                    new EnemyPlacementLiveRequest(
                        new RoomEnemyPlacementContent(
                            Id("enemy-placement", name),
                            Id("room", "fixture"),
                            Id("room-object", name),
                            1,
                            new RoomVector2(0d, 0d),
                            0d,
                            name),
                        Id("run", "fixture-run"),
                        Id("room-runtime", "fixture-room-runtime"),
                        null,
                        generation,
                        generation,
                        new EnemyDifficultyContext(
                            Id("difficulty", "fixture"),
                            1d))).Runtime;
            }

            private static EnemyDefinition CreateDefinition(string name)
            {
                return new EnemyDefinition(
                    Id("enemy", name),
                    Id("presentation", "enemy-" + name),
                    20d,
                    new EnemyLevelScalingProfile(1, 100, 2d, 1.01d),
                    Id("faction", "hostile-machines"),
                    20d,
                    360d,
                    Id("enemy-movement", "mobile-positioning"),
                    Id("enemy-decision", "ranged-standard"),
                    new[]
                    {
                        new EnemyAttackCapabilityDescriptor(
                            Id("enemy-attack-profile", name + "-primary"),
                            Id("enemy-attack", "ranged-projectile"),
                            10,
                            120d,
                            0d,
                            3d,
                            6d,
                            1d,
                            4d,
                            Id("damage", "kinetic"),
                            new EnemyProjectileAttackParameters(
                                Id("projectile", name),
                                1,
                                12d,
                                16d,
                                0.15d,
                                0d,
                                0),
                            null,
                            null),
                    },
                    Id("xp", "enemy-standard"),
                    Id("drop", "enemy-common"),
                    EnemyCatalogRoomClearRole.RequiredEnemy,
                    Array.Empty<StableId>());
            }
        }

        private sealed class RecordingPorts :
            IEnemyAttackEffectPort,
            IEnemyPlayerDamagePort,
            IEnemyRoomTerminalPort,
            IEnemyExperienceFactConsumer,
            IEnemyDropFactConsumer,
            IEnemyKillStatFactConsumer,
            IEnemyTerminalCollisionBridge
        {
            public RecordingPorts()
            {
                Bundle = new EnemyLiveDownstreamPorts(
                    this,
                    this,
                    this,
                    this,
                    this,
                    this,
                    this);
            }

            public EnemyLiveDownstreamPorts Bundle { get; }
            public int AttackEffectCount { get; private set; }
            public int PlayerDamageCount { get; private set; }

            public void Emit(EnemyAttackExecutionRequest request)
            {
                AttackEffectCount++;
            }

            public EnemyPlayerDamagePortResult Route(EnemyPlayerDamageRequest request)
            {
                PlayerDamageCount++;
                return new EnemyPlayerDamagePortResult(
                    EnemyLiveOperationStatus.Applied,
                    EnemyLiveRejectionCode.None);
            }

            public void Report(
                ReportRoomOccupantTerminalCommand command,
                EnemyDeathFact deathFact)
            {
            }

            void IEnemyExperienceFactConsumer.Consume(EnemyDeathFact fact)
            {
            }

            void IEnemyDropFactConsumer.Consume(EnemyDeathFact fact)
            {
            }

            void IEnemyKillStatFactConsumer.Consume(EnemyDeathFact fact)
            {
            }

            public void SetTerminal(EnemyTerminalCollisionFact fact)
            {
            }
        }
    }
}

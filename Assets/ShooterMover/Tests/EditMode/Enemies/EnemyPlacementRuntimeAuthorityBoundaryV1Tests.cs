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
    public sealed class EnemyPlacementRuntimeFactoryV1Tests_AuthorityBoundaries
    {
        [Test]
        public void FabricatedDecision_CannotDriveMovement()
        {
            Fixture fixture = new Fixture("movement-forgery");
            EnemyPlacementDecisionV1 fabricated = FabricatedDecisionFor(
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
            EnemyPlacementDecisionV1 fabricated = FabricatedDecisionFor(
                fixture.Runtime,
                new Fixture("attack-other").Runtime.Evaluate(Perception(2L)));

            EnemyAttackExecutionResultV1 result = fixture.Runtime.TryExecuteAttack(
                fabricated,
                Id("enemy-operation", "fabricated-decision"),
                1d);

            Assert.That(result.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Rejected));
            Assert.That(result.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.DecisionNotIssued));
            Assert.That(fixture.Ports.AttackEffectCount, Is.Zero);
        }

        [Test]
        public void AlteredCopyOfIssuedDecision_Rejects()
        {
            Fixture fixture = new Fixture("altered-decision");
            EnemyPlacementDecisionV1 first = fixture.Runtime.Evaluate(Perception(3L));
            EnemyPlacementDecisionV1 second = fixture.Runtime.Evaluate(Perception(4L));
            var altered = new EnemyPlacementDecisionV1(
                fixture.Runtime.SpawnStableId,
                fixture.Runtime.LifecycleGeneration,
                first.Perception,
                second.Evaluation);

            EnemyAttackExecutionResultV1 result = fixture.Runtime.TryExecuteAttack(
                altered,
                Id("enemy-operation", "altered-decision"),
                1d);

            Assert.That(result.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.DecisionNotIssued));
            Assert.That(fixture.Ports.AttackEffectCount, Is.Zero);
        }

        [Test]
        public void ExactImmutableCopyOfIssuedDecision_RemainsUsable()
        {
            Fixture fixture = new Fixture("exact-decision-copy");
            EnemyPlacementDecisionV1 issued = fixture.Runtime.Evaluate(Perception(5L));
            var exactCopy = new EnemyPlacementDecisionV1(
                issued.EntityInstanceId,
                issued.LifecycleGeneration,
                issued.Perception,
                issued.Evaluation);

            EnemyMovementRealizationV1 movement = fixture.Runtime.RealizeMovement(
                exactCopy,
                MovementContext(fixture.Runtime, 5L));
            EnemyAttackExecutionResultV1 attack = fixture.Runtime.TryExecuteAttack(
                exactCopy,
                Id("enemy-operation", "exact-issued-copy"),
                1d);

            Assert.That(movement, Is.Not.Null);
            Assert.That(attack.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(fixture.Ports.AttackEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void ExactAttackReplay_EmitsEffectOnce()
        {
            Fixture fixture = new Fixture("attack-replay");
            EnemyPlacementDecisionV1 decision = fixture.Runtime.Evaluate(Perception(6L));
            StableId operation = Id("enemy-operation", "exact-attack-replay");

            EnemyAttackExecutionResultV1 first = fixture.Runtime.TryExecuteAttack(decision, operation, 1d);
            EnemyAttackExecutionResultV1 replay = fixture.Runtime.TryExecuteAttack(decision, operation, 1d);

            Assert.That(first.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(replay.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.ExactReplay));
            Assert.That(replay.Request, Is.SameAs(first.Request));
            Assert.That(fixture.Ports.AttackEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void SameAttackOperationWithChangedIssuedDecisionOrPerception_Conflicts()
        {
            Fixture fixture = new Fixture("changed-decision-replay");
            EnemyPlacementDecisionV1 firstDecision = fixture.Runtime.Evaluate(Perception(7L));
            EnemyPlacementDecisionV1 changedDecision = fixture.Runtime.Evaluate(Perception(8L));
            StableId operation = Id("enemy-operation", "changed-decision-replay");

            fixture.Runtime.TryExecuteAttack(firstDecision, operation, 1d);
            EnemyAttackExecutionResultV1 conflict = fixture.Runtime.TryExecuteAttack(
                changedDecision,
                operation,
                1d);

            Assert.That(conflict.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.ConflictingDuplicate));
            Assert.That(fixture.Ports.AttackEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void SameAttackOperationWithChangedOccurrenceTime_Conflicts()
        {
            Fixture fixture = new Fixture("changed-time-replay");
            EnemyPlacementDecisionV1 decision = fixture.Runtime.Evaluate(Perception(9L));
            StableId operation = Id("enemy-operation", "changed-time-replay");

            fixture.Runtime.TryExecuteAttack(decision, operation, 1d);
            EnemyAttackExecutionResultV1 conflict = fixture.Runtime.TryExecuteAttack(
                decision,
                operation,
                2d);

            Assert.That(conflict.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.ConflictingDuplicate));
            Assert.That(fixture.Ports.AttackEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void SameAttackOperationWithChangedTargetingFacts_Conflicts()
        {
            Fixture fixture = new Fixture("changed-targeting-replay");
            EnemyPlacementDecisionV1 decision = fixture.Runtime.Evaluate(Perception(10L));
            StableId operation = Id("enemy-operation", "changed-targeting-replay");
            var exactContext = new EnemyTargetingAimContextV1(decision.Perception, 1d);
            var changedContext = new EnemyTargetingAimContextV1(Perception(10L, 4d), 1d);

            fixture.Runtime.TryExecuteAttack(decision, exactContext, operation, 1d);
            EnemyAttackExecutionResultV1 conflict = fixture.Runtime.TryExecuteAttack(
                decision,
                changedContext,
                operation,
                1d);

            Assert.That(conflict.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.ConflictingDuplicate));
            Assert.That(fixture.Ports.AttackEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void FabricatedExecutionRequest_CannotRoutePlayerDamage()
        {
            Fixture fixture = new Fixture("fabricated-execution");
            EnemyAttackExecutionResultV1 accepted = Execute(fixture, 11L, "real-execution");
            EnemyAttackExecutionRequestV1 fabricated = CopyExecution(
                accepted.Request,
                operation: Id("enemy-operation", "never-issued"));

            EnemyPlayerDamagePortResultV1 result = fixture.Runtime.RoutePlayerImpact(
                fabricated,
                Id("enemy-hit", "fabricated-execution"),
                Id("entity", "player"));

            Assert.That(result.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.ExecutionNotIssued));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.Zero);
        }

        [Test]
        public void IssuedExecutionWithAlteredDamageOrCooldown_Rejects()
        {
            Fixture fixture = new Fixture("altered-damage");
            EnemyAttackExecutionRequestV1 issued = Execute(fixture, 12L, "altered-damage").Request;
            EnemyAttackExecutionRequestV1 altered = CopyExecution(
                issued,
                damage: issued.ResolvedDamage + 1d);

            EnemyPlayerDamagePortResultV1 damageResult = fixture.Runtime.RoutePlayerImpact(
                altered,
                Id("enemy-hit", "altered-damage"),
                Id("entity", "player"));
            EnemyPlayerDamagePortResultV1 cooldownResult = fixture.Runtime.RoutePlayerImpact(
                CopyExecution(issued, cooldown: issued.ResolvedCooldownSeconds + 1d),
                Id("enemy-hit", "altered-cooldown"),
                Id("entity", "player"));

            Assert.That(damageResult.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.InvalidCommand));
            Assert.That(cooldownResult.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.InvalidCommand));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.Zero);
        }

        [Test]
        public void IssuedExecutionWithAlteredCommittedIntent_Rejects()
        {
            Fixture fixture = new Fixture("altered-intent");
            EnemyAttackExecutionRequestV1 issued = Execute(fixture, 13L, "altered-intent").Request;
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

            EnemyPlayerDamagePortResultV1 result = fixture.Runtime.RoutePlayerImpact(
                CopyExecution(issued, intent: alteredIntent),
                Id("enemy-hit", "altered-intent"),
                Id("entity", "player"));

            Assert.That(result.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.InvalidCommand));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.Zero);
        }

        [Test]
        public void IssuedExecutionWithAlteredDescriptorOrAttackId_Rejects()
        {
            Fixture fixture = new Fixture("altered-descriptor");
            EnemyAttackExecutionRequestV1 issued = Execute(fixture, 14L, "altered-descriptor").Request;
            EnemyAttackCapabilityDescriptorV1 original = issued.Descriptor;
            var alteredDescriptor = new EnemyAttackCapabilityDescriptorV1(
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

            EnemyPlayerDamagePortResultV1 result = fixture.Runtime.RoutePlayerImpact(
                CopyExecution(issued, descriptor: alteredDescriptor),
                Id("enemy-hit", "altered-descriptor"),
                Id("entity", "player"));

            Assert.That(result.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.InvalidCommand));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.Zero);
        }

        [Test]
        public void ExactIssuedExecution_RoutesThroughPlayerDamagePort()
        {
            Fixture fixture = new Fixture("exact-execution");
            EnemyAttackExecutionRequestV1 issued = Execute(fixture, 15L, "exact-execution").Request;

            EnemyPlayerDamagePortResultV1 result = fixture.Runtime.RoutePlayerImpact(
                issued,
                Id("enemy-hit", "exact-execution"),
                Id("entity", "player"));

            Assert.That(result.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.EqualTo(1));
        }

        [Test]
        public void ProjectileIssuedBeforeEnemyDeath_StillDamagesAfterDeath()
        {
            Fixture fixture = new Fixture("post-death-projectile");
            EnemyAttackExecutionRequestV1 issued = Execute(
                fixture,
                16L,
                "post-death-projectile").Request;
            Kill(fixture.Runtime, "post-death-projectile");

            EnemyPlayerDamagePortResultV1 result = fixture.Runtime.RoutePlayerImpact(
                issued,
                Id("enemy-hit", "post-death-projectile"),
                Id("entity", "player"));

            Assert.That(fixture.Runtime.ActorState.IsActive, Is.False);
            Assert.That(result.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.EqualTo(1));
        }

        [Test]
        public void OldProjectile_RejectsAfterLifecycleRestart()
        {
            Fixture fixture = new Fixture("restart-projectile");
            EnemyAttackExecutionRequestV1 oldExecution = Execute(
                fixture,
                17L,
                "old-projectile").Request;
            EnemyPlacementRuntimeInstanceV1 restarted = fixture.CreateRuntime(2L);

            EnemyPlayerDamagePortResultV1 result = restarted.RoutePlayerImpact(
                oldExecution,
                Id("enemy-hit", "old-projectile-after-restart"),
                Id("entity", "player"));

            Assert.That(restarted.SpawnStableId, Is.EqualTo(fixture.Runtime.SpawnStableId));
            Assert.That(result.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.StaleLifecycle));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.Zero);
        }

        [Test]
        public void NewAttackCannotBeIssuedAfterEnemyDeath()
        {
            Fixture fixture = new Fixture("attack-after-death");
            EnemyPlacementDecisionV1 issuedBeforeDeath = fixture.Runtime.Evaluate(Perception(18L));
            Kill(fixture.Runtime, "attack-after-death");

            EnemyAttackExecutionResultV1 result = fixture.Runtime.TryExecuteAttack(
                issuedBeforeDeath,
                Id("enemy-operation", "new-after-death"),
                2d);

            Assert.That(result.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.ActorTerminal));
            Assert.That(fixture.Ports.AttackEffectCount, Is.Zero);
        }

        [Test]
        public void ExactHitEventReplay_RoutesPlayerDamageOnce()
        {
            Fixture fixture = new Fixture("hit-replay");
            EnemyAttackExecutionRequestV1 execution = Execute(fixture, 19L, "hit-replay").Request;
            StableId hit = Id("enemy-hit", "exact-hit-replay");

            EnemyPlayerDamagePortResultV1 first = fixture.Runtime.RoutePlayerImpact(
                execution,
                hit,
                Id("entity", "player"));
            EnemyPlayerDamagePortResultV1 replay = fixture.Runtime.RoutePlayerImpact(
                execution,
                hit,
                Id("entity", "player"));

            Assert.That(first.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(replay.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.ExactReplay));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingHitEventReuse_RejectsWithoutMutation()
        {
            Fixture fixture = new Fixture("hit-conflict");
            EnemyAttackExecutionRequestV1 execution = Execute(fixture, 20L, "hit-conflict").Request;
            StableId hit = Id("enemy-hit", "conflicting-hit");
            fixture.Runtime.RoutePlayerImpact(execution, hit, Id("entity", "player-one"));

            EnemyPlayerDamagePortResultV1 conflict = fixture.Runtime.RoutePlayerImpact(
                execution,
                hit,
                Id("entity", "player-two"));

            Assert.That(conflict.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.ConflictingDuplicate));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.EqualTo(1));
        }

        [Test]
        public void MultipleDistinctHitIds_CanReferenceSameAcceptedExecution()
        {
            Fixture fixture = new Fixture("multi-hit");
            EnemyAttackExecutionRequestV1 execution = Execute(fixture, 21L, "multi-hit").Request;

            EnemyPlayerDamagePortResultV1 first = fixture.Runtime.RoutePlayerImpact(
                execution,
                Id("enemy-hit", "multi-hit-one"),
                Id("entity", "player-one"));
            EnemyPlayerDamagePortResultV1 second = fixture.Runtime.RoutePlayerImpact(
                execution,
                Id("enemy-hit", "multi-hit-two"),
                Id("entity", "player-two"));

            Assert.That(first.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(second.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(fixture.Ports.PlayerDamageCount, Is.EqualTo(2));
        }

        [Test]
        public void ObserverPositionConfiguration_IsRemovedAndCannotPromiseUnenforcedBehavior()
        {
            Assert.That(
                typeof(EnemyPerceptionPolicyConfigurationV1).GetProperty(
                    "RequireMatchingObserverPosition"),
                Is.Null);
            Assert.DoesNotThrow(() => new EnemyPerceptionPolicyConfigurationV1(
                Id("enemy-perception", "authoritative-facts-only")));
            Assert.Throws<ArgumentException>(() => new EnemyPerceptionPolicyConfigurationV1(
                Id("enemy-perception", "unsupported-position-match"),
                true));
        }

        private static EnemyAttackExecutionResultV1 Execute(
            Fixture fixture,
            long tick,
            string operation)
        {
            EnemyPlacementDecisionV1 decision = fixture.Runtime.Evaluate(Perception(tick));
            EnemyAttackExecutionResultV1 result = fixture.Runtime.TryExecuteAttack(
                decision,
                Id("enemy-operation", operation),
                1d);
            Assert.That(result.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            return result;
        }

        private static void Kill(EnemyPlacementRuntimeInstanceV1 runtime, string operation)
        {
            EnemyRuntimeDamageResultV1 result = runtime.ApplyDamage(
                new EnemyRuntimeDamageCommandV1(
                    Id("enemy-damage", operation),
                    Id("entity", "player"),
                    Id("run-participant", "player"),
                    runtime.SpawnStableId,
                    runtime.LifecycleGeneration,
                    0L,
                    1,
                    10000d));
            Assert.That(result.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(result.DeathFact, Is.Not.Null);
        }

        private static EnemyPlacementDecisionV1 FabricatedDecisionFor(
            EnemyPlacementRuntimeInstanceV1 runtime,
            EnemyPlacementDecisionV1 foreign)
        {
            return new EnemyPlacementDecisionV1(
                runtime.SpawnStableId,
                runtime.LifecycleGeneration,
                foreign.Perception,
                foreign.Evaluation);
        }

        private static EnemyMovementRealizationContextV1 MovementContext(
            EnemyPlacementRuntimeInstanceV1 runtime,
            long tick)
        {
            return new EnemyMovementRealizationContextV1(
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

        private static EnemyAttackExecutionRequestV1 CopyExecution(
            EnemyAttackExecutionRequestV1 source,
            StableId operation = null,
            EnemyAttackCapabilityDescriptorV1 descriptor = null,
            EnemyAttackIntent intent = null,
            double? damage = null,
            double? cooldown = null)
        {
            return new EnemyAttackExecutionRequestV1(
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
                Factory = new EnemyPlacementRuntimeFactoryV1(
                    new RoomContentObjectCatalogV1(new[]
                    {
                        new RoomContentObjectDefinitionV1(
                            Id("room-object", name),
                            RoomContentObjectKindV1.Enemy,
                            Definition.DefinitionId,
                            Definition.PresentationId),
                    }),
                    new EnemyCatalogV1(
                        1,
                        Id("enemy-catalog", name + "-v1"),
                        new[] { Definition }),
                    BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                    new DeterministicEnemyRuntimeIdentityDeriverV1(),
                    new EnemyDifficultyRuntimeRegistrationV1(
                        new EnemyDifficultyScalingConfigurationV1(
                            Id("enemy-difficulty", "test-scalar"),
                            1d,
                            0.5d,
                            0.2d,
                            0.15d),
                        new ScalarEnemyDifficultyScalingPolicyV1()),
                    new EnemyPerceptionRuntimeRegistrationV1(
                        new EnemyPerceptionPolicyConfigurationV1(
                            Id("enemy-perception", "test-validated")),
                        new ValidatedEnemyPerceptionRuntimeAdapterV1()),
                    Ports.Bundle);
                Runtime = CreateRuntime(1L);
            }

            public RecordingPorts Ports { get; }
            public EnemyDefinitionV1 Definition { get; }
            public EnemyPlacementRuntimeFactoryV1 Factory { get; }
            public EnemyPlacementRuntimeInstanceV1 Runtime { get; }

            public EnemyPlacementRuntimeInstanceV1 CreateRuntime(long generation)
            {
                return Factory.Create(
                    new EnemyPlacementRuntimeRequestV1(
                        new RoomEnemyPlacementContentV1(
                            Id("enemy-placement", name),
                            Id("room", "fixture"),
                            Id("room-object", name),
                            1,
                            new RoomVector2V1(0d, 0d),
                            0d,
                            name),
                        Id("run", "fixture-run"),
                        Id("room-runtime", "fixture-room-runtime"),
                        null,
                        generation,
                        generation,
                        new EnemyDifficultyContextV1(
                            Id("difficulty", "fixture"),
                            1d))).Runtime;
            }

            private static EnemyDefinitionV1 CreateDefinition(string name)
            {
                return new EnemyDefinitionV1(
                    Id("enemy", name),
                    Id("presentation", "enemy-" + name),
                    20d,
                    new EnemyLevelScalingProfileV1(1, 100, 2d, 1.01d),
                    Id("faction", "hostile-machines"),
                    20d,
                    360d,
                    Id("enemy-movement", "mobile-positioning"),
                    Id("enemy-decision", "ranged-standard"),
                    new[]
                    {
                        new EnemyAttackCapabilityDescriptorV1(
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
                            new EnemyProjectileAttackParametersV1(
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
                    EnemyCatalogRoomClearRoleV1.RequiredEnemy,
                    Array.Empty<StableId>());
            }
        }

        private sealed class RecordingPorts :
            IEnemyAttackEffectPortV1,
            IEnemyPlayerDamagePortV1,
            IEnemyRoomTerminalPortV1,
            IEnemyExperienceFactConsumerV1,
            IEnemyDropFactConsumerV1,
            IEnemyKillStatFactConsumerV1,
            IEnemyTerminalCollisionAdapterV1
        {
            public RecordingPorts()
            {
                Bundle = new EnemyRuntimeDownstreamPortsV1(
                    this,
                    this,
                    this,
                    this,
                    this,
                    this,
                    this);
            }

            public EnemyRuntimeDownstreamPortsV1 Bundle { get; }
            public int AttackEffectCount { get; private set; }
            public int PlayerDamageCount { get; private set; }

            public void Emit(EnemyAttackExecutionRequestV1 request)
            {
                AttackEffectCount++;
            }

            public EnemyPlayerDamagePortResultV1 Route(EnemyPlayerDamageRequestV1 request)
            {
                PlayerDamageCount++;
                return new EnemyPlayerDamagePortResultV1(
                    EnemyRuntimeOperationStatusV1.Applied,
                    EnemyRuntimeRejectionCodeV1.None);
            }

            public void Report(
                ReportRoomOccupantTerminalCommandV1 command,
                EnemyDeathFactV1 deathFact)
            {
            }

            void IEnemyExperienceFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
            }

            void IEnemyDropFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
            }

            void IEnemyKillStatFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
            }

            public void SetTerminal(EnemyTerminalCollisionFactV1 fact)
            {
            }
        }
    }
}

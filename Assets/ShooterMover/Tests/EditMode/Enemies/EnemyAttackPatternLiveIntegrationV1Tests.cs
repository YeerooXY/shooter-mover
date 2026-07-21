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
    public sealed partial class EnemyAttackPatternLiveIntegrationV1Tests
    {
        [Test]
        public void TryExecuteAttack_DispatchesOneAtomicTimedBurstAndOuterReplayDoesNotRedeliver()
        {
            var ports = new RecordingPatternPorts();
            EnemyPlacementRuntimeInstanceV1 runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
            StableId operation = Id("enemy-operation", "live-burst");

            EnemyAttackExecutionResultV1 applied = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                operation,
                10d);
            EnemyAttackExecutionResultV1 replay = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                operation,
                10d);

            Assert.That(applied.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(applied.IsAccepted, Is.True);
            Assert.That(replay.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.ExactReplay));
            Assert.That(ports.LegacyExecutionCount, Is.EqualTo(0));
            Assert.That(ports.DispatchAttempts, Is.EqualTo(1));
            Assert.That(ports.DispatchedSequences, Has.Count.EqualTo(1));
            Assert.That(ports.Emissions, Has.Count.EqualTo(3));
            Assert.That(ports.Emissions[0].ScheduledAtSeconds,
                Is.EqualTo(10.1d).Within(0.0000001d));
            Assert.That(ports.Emissions[1].ScheduledAtSeconds,
                Is.EqualTo(10.3d).Within(0.0000001d));
            Assert.That(ports.Emissions[2].ScheduledAtSeconds,
                Is.EqualTo(10.5d).Within(0.0000001d));
            Assert.That(runtime.AttackPatterns.Sequences, Has.Count.EqualTo(1));

            var ids = new HashSet<StableId>();
            for (int index = 0; index < ports.Emissions.Count; index++)
            {
                Assert.That(ids.Add(ports.Emissions[index].EmissionStableId), Is.True);
                Assert.That(
                    ports.Emissions[index].SequenceStableId,
                    Is.EqualTo(ports.Emissions[0].SequenceStableId));
            }
        }

        [Test]
        public void CancelAttackPatterns_NotifiesAtomicSchedulerAndSuppressesPendingBurstEmissions()
        {
            var ports = new RecordingPatternPorts();
            EnemyPlacementRuntimeInstanceV1 runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);

            EnemyAttackExecutionResultV1 attack = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                Id("enemy-operation", "live-cancel-burst"),
                10d);
            var command = new EnemyAttackLifecycleCancellationCommandV1(
                Id("enemy-pattern-operation", "live-cancel"),
                runtime.SpawnStableId,
                runtime.LifecycleGeneration,
                10.1d);
            EnemyAttackPatternCancellationResultV1 cancellation =
                runtime.CancelAttackPatterns(command);
            EnemyAttackPatternCancellationResultV1 replay =
                runtime.CancelAttackPatterns(command);

            ports.ProcessScheduledEffects(10.5d);

            Assert.That(attack.IsAccepted, Is.True);
            Assert.That(cancellation.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.Applied));
            Assert.That(cancellation.IsAccepted, Is.True);
            Assert.That(cancellation.Dispatch.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(replay.Dispatch.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(ports.AcceptedCancellationCount, Is.EqualTo(1));
            Assert.That(ports.LastCancellation, Is.SameAs(cancellation.Fact));
            Assert.That(cancellation.Fact.CancelledProjectileStableIds,
                Has.Count.EqualTo(2));
            AssertOnlyFirstEmissionExecuted(ports);
        }

        [Test]
        public void LegacyEffectPort_FailsClosedForTimedBurstButAllowsEquivalentImmediateSingle()
        {
            var support = new RecordingPatternPorts();
            var legacy = new RecordingLegacyAttackPort();
            EnemyPlacementRuntimeInstanceV1 burstRuntime = Runtime(
                BurstDefinition(),
                support.WithAttackEffects(legacy));
            EnemyPerceptionSnapshot burstPerception = Perception();
            EnemyPlacementDecisionV1 burstDecision =
                burstRuntime.Evaluate(burstPerception);

            EnemyAttackExecutionResultV1 burst = burstRuntime.TryExecuteAttack(
                burstDecision,
                new EnemyTargetingAimContextV1(burstPerception, 1d),
                Id("enemy-operation", "legacy-burst"),
                10d);

            Assert.That(burst.Status,
                Is.EqualTo(EnemyRuntimeOperationStatusV1.Rejected));
            Assert.That(burst.Rejection,
                Is.EqualTo(EnemyRuntimeRejectionCodeV1.InvalidCommand));
            Assert.That(legacy.ExecutionCount, Is.EqualTo(0));
            Assert.That(burstRuntime.AttackPatterns.Sequences, Is.Empty);

            var immediateSupport = new RecordingPatternPorts();
            var immediateLegacy = new RecordingLegacyAttackPort();
            EnemyPlacementRuntimeInstanceV1 immediateRuntime = Runtime(
                ImmediateSingleDefinition(),
                immediateSupport.WithAttackEffects(immediateLegacy));
            EnemyPerceptionSnapshot immediatePerception = Perception();
            EnemyPlacementDecisionV1 immediateDecision =
                immediateRuntime.Evaluate(immediatePerception);

            EnemyAttackExecutionResultV1 immediate =
                immediateRuntime.TryExecuteAttack(
                    immediateDecision,
                    new EnemyTargetingAimContextV1(immediatePerception, 1d),
                    Id("enemy-operation", "legacy-immediate"),
                    20d);

            Assert.That(immediate.IsAccepted, Is.True);
            Assert.That(immediateLegacy.ExecutionCount, Is.EqualTo(1));
            Assert.That(immediateSupport.Emissions, Is.Empty);
        }

        private static EnemyDefinitionV1 BurstDefinition()
        {
            return ShootingDefinition("live-burst", 3, 0.2d, 1, 0d, 0.1d);
        }

        private static EnemyDefinitionV1 ImmediateSingleDefinition()
        {
            return ShootingDefinition("live-immediate", 1, 0d, 1, 0d, 0d);
        }

        private static EnemyDefinitionV1 ShootingDefinition(
            string name,
            int shotsPerSequence,
            double intervalBetweenShots,
            int projectilesPerShot,
            double spread,
            double windUp)
        {
            var attack = new EnemyAttackCapabilityDescriptorV1(
                Id("enemy-attack-profile", name),
                Id("enemy-attack", "ranged-projectile"),
                10,
                120d,
                0d,
                5d,
                12d,
                3d,
                Id("damage", "kinetic"),
                new EnemyShootingPatternV1(
                    shotsPerSequence,
                    intervalBetweenShots,
                    projectilesPerShot,
                    spread,
                    EnemySequenceAimPolicyV1.LockAtSequenceStart,
                    windUp,
                    0.5d,
                    EnemyAttackInterruptionPolicyV1.CancelPendingOnLifecycleEnd),
                new EnemyProjectilePayloadV1(
                    Id("projectile", name),
                    12d,
                    16d,
                    0.15d,
                    0,
                    null),
                null);
            return new EnemyDefinitionV1(
                Id("enemy", name),
                Id("presentation", "enemy-" + name),
                20d,
                new EnemyLevelScalingProfileV1(1, 100, 1d, 1.01d),
                Id("faction", "hostile-machines"),
                20d,
                360d,
                Id("enemy-movement", "mobile-positioning"),
                Id("enemy-decision", "ranged-standard"),
                new[] { attack },
                Id("xp", "enemy-standard"),
                Id("drop", "enemy-common"),
                EnemyCatalogRoomClearRoleV1.RequiredEnemy,
                Array.Empty<StableId>());
        }

        private static EnemyPlacementRuntimeInstanceV1 Runtime(
            EnemyDefinitionV1 definition,
            EnemyRuntimeDownstreamPortsV1 ports)
        {
            return Factory(definition, ports).Create(Request()).Runtime;
        }

        private static EnemyPlacementRuntimeFactoryV1 Factory(
            EnemyDefinitionV1 definition,
            EnemyRuntimeDownstreamPortsV1 ports)
        {
            var roomObject = new RoomContentObjectDefinitionV1(
                Id("room-object", "live-burst"),
                RoomContentObjectKindV1.Enemy,
                definition.DefinitionId,
                definition.PresentationId);
            return new EnemyPlacementRuntimeFactoryV1(
                new RoomContentObjectCatalogV1(new[] { roomObject }),
                new EnemyCatalogV1(
                    2,
                    Id("enemy-catalog", "live-pattern-integration"),
                    new[] { definition }),
                BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                new DeterministicEnemyRuntimeIdentityDeriverV1(),
                new EnemyDifficultyRuntimeRegistrationV1(
                    new EnemyDifficultyScalingConfigurationV1(
                        Id("enemy-difficulty", "live-pattern-test"),
                        1d,
                        0.5d,
                        0.2d,
                        0.15d),
                    new ScalarEnemyDifficultyScalingPolicyV1()),
                new EnemyPerceptionRuntimeRegistrationV1(
                    new EnemyPerceptionPolicyConfigurationV1(
                        Id("enemy-perception", "live-pattern-test"),
                        false),
                    new ValidatedEnemyPerceptionRuntimeAdapterV1()),
                ports);
        }

        private static EnemyPlacementRuntimeRequestV1 Request()
        {
            var placement = new RoomEnemyPlacementContentV1(
                Id("enemy-placement", "live-burst"),
                Id("room", "live-pattern"),
                Id("room-object", "live-burst"),
                1,
                new RoomVector2V1(0d, 0d),
                0d,
                "live-burst");
            return new EnemyPlacementRuntimeRequestV1(
                placement,
                Id("run", "live-pattern"),
                Id("room-runtime", "live-pattern"),
                null,
                1L,
                1L,
                new EnemyDifficultyContextV1(
                    Id("difficulty", "normal"),
                    1d));
        }

        private static EnemyPerceptionSnapshot Perception()
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
                        new EnemyVector2(5d, 0d),
                        new EnemyVector2(0d, 0d),
                        5d,
                        new EnemyVector2(1d, 0d),
                        true,
                        true,
                        true),
                },
                1L);
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}

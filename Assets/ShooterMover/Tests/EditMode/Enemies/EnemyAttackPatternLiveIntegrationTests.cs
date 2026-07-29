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
    public sealed partial class EnemyAttackPatternLiveIntegrationTests
    {
        [Test]
        public void TryExecuteAttack_DispatchesOneAtomicTimedBurstAndOuterReplayDoesNotRedeliver()
        {
            var ports = new RecordingPatternPorts();
            EnemyInstance runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecision decision = runtime.Evaluate(perception);
            StableId operation = Id("enemy-operation", "live-burst");

            EnemyAttackExecutionResult applied = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                operation,
                10d);
            EnemyAttackExecutionResult replay = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                operation,
                10d);

            Assert.That(applied.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(applied.IsAccepted, Is.True);
            Assert.That(replay.Status, Is.EqualTo(EnemyLiveOperationStatus.ExactReplay));
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
            EnemyInstance runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecision decision = runtime.Evaluate(perception);

            EnemyAttackExecutionResult attack = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                Id("enemy-operation", "live-cancel-burst"),
                10d);
            var command = new EnemyAttackLifecycleCancellationCommand(
                Id("enemy-pattern-operation", "live-cancel"),
                runtime.SpawnStableId,
                runtime.LifecycleGeneration,
                10.1d);
            EnemyAttackPatternCancellationResult cancellation =
                runtime.CancelAttackPatterns(command);
            EnemyAttackPatternCancellationResult replay =
                runtime.CancelAttackPatterns(command);

            ports.ProcessScheduledEffects(10.5d);

            Assert.That(attack.IsAccepted, Is.True);
            Assert.That(cancellation.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatus.Applied));
            Assert.That(cancellation.IsAccepted, Is.True);
            Assert.That(cancellation.Dispatch.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatus.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatus.ExactReplay));
            Assert.That(replay.Dispatch.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatus.ExactReplay));
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
            EnemyInstance burstRuntime = Runtime(
                BurstDefinition(),
                support.WithAttackEffects(legacy));
            EnemyPerceptionSnapshot burstPerception = Perception();
            EnemyPlacementDecision burstDecision =
                burstRuntime.Evaluate(burstPerception);

            EnemyAttackExecutionResult burst = burstRuntime.TryExecuteAttack(
                burstDecision,
                new EnemyTargetingAimContext(burstPerception, 1d),
                Id("enemy-operation", "legacy-burst"),
                10d);

            Assert.That(burst.Status,
                Is.EqualTo(EnemyLiveOperationStatus.Rejected));
            Assert.That(burst.Rejection,
                Is.EqualTo(EnemyLiveRejectionCode.InvalidCommand));
            Assert.That(legacy.ExecutionCount, Is.EqualTo(0));
            Assert.That(burstRuntime.AttackPatterns.Sequences, Is.Empty);

            var immediateSupport = new RecordingPatternPorts();
            var immediateLegacy = new RecordingLegacyAttackPort();
            EnemyInstance immediateRuntime = Runtime(
                ImmediateSingleDefinition(),
                immediateSupport.WithAttackEffects(immediateLegacy));
            EnemyPerceptionSnapshot immediatePerception = Perception();
            EnemyPlacementDecision immediateDecision =
                immediateRuntime.Evaluate(immediatePerception);

            EnemyAttackExecutionResult immediate =
                immediateRuntime.TryExecuteAttack(
                    immediateDecision,
                    new EnemyTargetingAimContext(immediatePerception, 1d),
                    Id("enemy-operation", "legacy-immediate"),
                    20d);

            Assert.That(immediate.IsAccepted, Is.True);
            Assert.That(immediateLegacy.ExecutionCount, Is.EqualTo(1));
            Assert.That(immediateSupport.Emissions, Is.Empty);
        }

        private static EnemyDefinition BurstDefinition()
        {
            return ShootingDefinition("live-burst", 3, 0.2d, 1, 0d, 0.1d);
        }

        private static EnemyDefinition ImmediateSingleDefinition()
        {
            return ShootingDefinition("live-immediate", 1, 0d, 1, 0d, 0d);
        }

        private static EnemyDefinition ShootingDefinition(
            string name,
            int shotsPerSequence,
            double intervalBetweenShots,
            int projectilesPerShot,
            double spread,
            double windUp)
        {
            var attack = new EnemyAttackCapabilityDescriptor(
                Id("enemy-attack-profile", name),
                Id("enemy-attack", "ranged-projectile"),
                10,
                120d,
                0d,
                5d,
                12d,
                3d,
                Id("damage", "kinetic"),
                new EnemyShootingPattern(
                    shotsPerSequence,
                    intervalBetweenShots,
                    projectilesPerShot,
                    spread,
                    EnemySequenceAimPolicy.LockAtSequenceStart,
                    windUp,
                    0.5d,
                    EnemyAttackInterruptionPolicy.CancelPendingOnLifecycleEnd),
                new EnemyProjectilePayload(
                    Id("projectile", name),
                    12d,
                    16d,
                    0.15d,
                    0,
                    null),
                null);
            return new EnemyDefinition(
                Id("enemy", name),
                Id("presentation", "enemy-" + name),
                20d,
                new EnemyLevelScalingProfile(1, 100, 1d, 1.01d),
                Id("faction", "hostile-machines"),
                20d,
                360d,
                Id("enemy-movement", "mobile-positioning"),
                Id("enemy-decision", "ranged-standard"),
                new[] { attack },
                Id("xp", "enemy-standard"),
                Id("drop", "enemy-common"),
                EnemyCatalogRoomClearRole.RequiredEnemy,
                Array.Empty<StableId>());
        }

        private static EnemyInstance Runtime(
            EnemyDefinition definition,
            EnemyLiveDownstreamPorts ports)
        {
            return Factory(definition, ports).Create(Request()).Runtime;
        }

        private static EnemyFactory Factory(
            EnemyDefinition definition,
            EnemyLiveDownstreamPorts ports)
        {
            var roomObject = new RoomContentObjectDefinition(
                Id("room-object", "live-burst"),
                RoomContentObjectKind.Enemy,
                definition.DefinitionId,
                definition.PresentationId);
            return new EnemyFactory(
                new RoomContentObjectCatalog(new[] { roomObject }),
                new EnemyCatalog(
                    2,
                    Id("enemy-catalog", "live-pattern-integration"),
                    new[] { definition }),
                BuiltInEnemyRules.Create(),
                new DeterministicEnemyLiveIdentityDeriver(),
                new EnemyDifficultyLiveRegistration(
                    new EnemyDifficultyScalingConfiguration(
                        Id("enemy-difficulty", "live-pattern-test"),
                        1d,
                        0.5d,
                        0.2d,
                        0.15d),
                    new ScalarEnemyDifficultyScalingPolicy()),
                new EnemyPerceptionLiveRegistration(
                    new EnemyPerceptionPolicyConfiguration(
                        Id("enemy-perception", "live-pattern-test"),
                        false),
                    new ValidatedEnemyPerceptionLiveBridge()),
                ports);
        }

        private static EnemyPlacementLiveRequest Request()
        {
            var placement = new RoomEnemyPlacementContent(
                Id("enemy-placement", "live-burst"),
                Id("room", "live-pattern"),
                Id("room-object", "live-burst"),
                1,
                new RoomVector2(0d, 0d),
                0d,
                "live-burst");
            return new EnemyPlacementLiveRequest(
                placement,
                Id("run", "live-pattern"),
                Id("room-runtime", "live-pattern"),
                null,
                1L,
                1L,
                new EnemyDifficultyContext(
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

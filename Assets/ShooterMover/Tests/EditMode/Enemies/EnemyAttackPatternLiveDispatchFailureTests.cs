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
        public void AtomicDispatch_ThrowDuringSecondEmissionPrevalidationLeavesNoPartialStateAndRetryCommitsOnce()
        {
            var ports = new RecordingPatternPorts
            {
                ThrowOnDispatchEmissionIndex = 1,
            };
            EnemyInstance runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecision decision = runtime.Evaluate(perception);
            StableId operation = Id("enemy-operation", "atomic-retry");

            EnemyAttackExecutionResult failed = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                operation,
                10d);

            Assert.That(failed.Status, Is.EqualTo(EnemyLiveOperationStatus.Rejected));
            Assert.That(failed.Rejection, Is.EqualTo(EnemyLiveRejectionCode.InvalidCommand));
            Assert.That(ports.DispatchedSequences, Is.Empty);
            Assert.That(ports.Emissions, Is.Empty);
            Assert.That(runtime.AttackPatterns.Sequences, Has.Count.EqualTo(1));
            Assert.That(ports.LastAttemptedSequence, Is.Not.Null);
            EnemyPlayerDamagePortResult beforeCommit = runtime.RoutePlayerImpact(
                ports.LastAttemptedSequence.Execution,
                Id("enemy-hit", "before-dispatch-commit"),
                Id("entity", "player"),
                1L);
            Assert.That(beforeCommit.Status,
                Is.EqualTo(EnemyLiveOperationStatus.Rejected));
            Assert.That(beforeCommit.Rejection,
                Is.EqualTo(EnemyLiveRejectionCode.ExecutionNotIssued));

            EnemyAttackExecutionResult retried = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                operation,
                10d);
            EnemyAttackExecutionResult replay = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                operation,
                10d);

            Assert.That(retried.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(retried.IsAccepted, Is.True,
                "A failed dispatch must not consume cooldown or outer replay state.");
            Assert.That(replay.Status, Is.EqualTo(EnemyLiveOperationStatus.ExactReplay));
            Assert.That(replay.Request, Is.SameAs(retried.Request));
            Assert.That(runtime.AttackPatterns.Sequences, Has.Count.EqualTo(1));
            Assert.That(ports.DispatchedSequences, Has.Count.EqualTo(1));
            Assert.That(ports.Emissions, Has.Count.EqualTo(3));
            Assert.That(ports.DispatchAttempts, Is.EqualTo(2));
            EnemyPlayerDamagePortResult afterCommit = runtime.RoutePlayerImpact(
                ports.LastAttemptedSequence.Execution,
                Id("enemy-hit", "after-dispatch-commit"),
                Id("entity", "player"),
                1L);
            Assert.That(afterCommit.Status,
                Is.EqualTo(EnemyLiveOperationStatus.NoEffect));
            Assert.That(afterCommit.Rejection,
                Is.EqualTo(EnemyLiveRejectionCode.None));
        }

        [Test]
        public void CancellationFailure_ExactRetryRedeliversCanonicalFactWithoutDuplicateQueueMutation()
        {
            var ports = new RecordingPatternPorts
            {
                RejectNextCancellation = true,
            };
            EnemyInstance runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecision decision = runtime.Evaluate(perception);
            runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                Id("enemy-operation", "cancel-retry-attack"),
                10d);
            var command = new EnemyAttackLifecycleCancellationCommand(
                Id("enemy-pattern-operation", "cancel-retry"),
                runtime.SpawnStableId,
                runtime.LifecycleGeneration,
                10.1d);

            EnemyAttackPatternCancellationResult failed =
                runtime.CancelAttackPatterns(command);
            EnemyAttackPatternCancellationResult retried =
                runtime.CancelAttackPatterns(command);
            EnemyAttackPatternCancellationResult replay =
                runtime.CancelAttackPatterns(command);

            Assert.That(failed.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatus.Applied));
            Assert.That(failed.IsAuthorityAccepted, Is.True);
            Assert.That(failed.IsAccepted, Is.False);
            Assert.That(failed.Dispatch.Rejection,
                Is.EqualTo(EnemyAttackPatternDispatchRejectionCode.DownstreamFailure));
            Assert.That(retried.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatus.ExactReplay));
            Assert.That(retried.Fact, Is.SameAs(failed.Fact));
            Assert.That(retried.IsAccepted, Is.True);
            Assert.That(replay.Dispatch.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatus.ExactReplay));
            Assert.That(ports.AcceptedCancellationCount, Is.EqualTo(1));
            Assert.That(ports.CancellationAttempts, Is.EqualTo(3));

            ports.ProcessScheduledEffects(10.5d);
            AssertOnlyFirstEmissionExecuted(ports);
        }

        [Test]
        public void LethalDamage_AutomaticallyCancelsPendingSequenceWithoutManualCancellation()
        {
            var ports = new RecordingPatternPorts();
            EnemyInstance runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecision decision = runtime.Evaluate(perception);
            runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                Id("enemy-operation", "death-cancel-attack"),
                10d);
            EnemyLiveDamageCommand damage = LethalDamage(runtime, "death-cancel");

            EnemyLiveDamageResult result = runtime.ApplyDamage(damage, 10.1d);
            ports.ProcessScheduledEffects(10.5d);

            Assert.That(result.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(result.DeathFact, Is.Not.Null);
            Assert.That(runtime.ActorState.IsActive, Is.False);
            Assert.That(ports.AcceptedCancellationCount, Is.EqualTo(1));
            Assert.That(ports.TerminalCollisionCount, Is.EqualTo(1));
            Assert.That(ports.RoomCount, Is.EqualTo(1));
            AssertOnlyFirstEmissionExecuted(ports);
        }

        [Test]
        public void LethalDamage_RetriesAutomaticCancellationAfterFirstDeliveryFailure()
        {
            var ports = new RecordingPatternPorts
            {
                RejectNextCancellation = true,
            };
            EnemyInstance runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecision decision = runtime.Evaluate(perception);
            runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                Id("enemy-operation", "death-cancel-retry-attack"),
                10d);
            EnemyLiveDamageCommand damage =
                LethalDamage(runtime, "death-cancel-retry");

            EnemyLiveDamageResult failed = runtime.ApplyDamage(damage, 10.1d);
            EnemyLiveDamageResult retried = runtime.ApplyDamage(damage, 10.1d);
            EnemyLiveDamageResult replay = runtime.ApplyDamage(damage, 10.1d);
            ports.ProcessScheduledEffects(10.5d);

            Assert.That(failed.Status, Is.EqualTo(EnemyLiveOperationStatus.Rejected));
            Assert.That(failed.DeathFact, Is.Not.Null);
            Assert.That(runtime.ActorState.IsActive, Is.False);
            Assert.That(retried.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(replay.Status, Is.EqualTo(EnemyLiveOperationStatus.ExactReplay));
            Assert.That(ports.AcceptedCancellationCount, Is.EqualTo(1));
            Assert.That(ports.TerminalCollisionCount, Is.EqualTo(1));
            Assert.That(ports.RoomCount, Is.EqualTo(1));
            AssertOnlyFirstEmissionExecuted(ports);
        }

        private static void AssertOnlyFirstEmissionExecuted(RecordingPatternPorts ports)
        {
            Assert.That(ports.ExecutedEmissions, Has.Count.EqualTo(1));
            Assert.That(
                ports.ExecutedEmissions[0].EmissionStableId,
                Is.EqualTo(ports.Emissions[0].EmissionStableId));
            Assert.That(ports.WasExecuted(ports.Emissions[1].EmissionStableId), Is.False);
            Assert.That(ports.WasExecuted(ports.Emissions[2].EmissionStableId), Is.False);
        }

        private static EnemyLiveDamageCommand LethalDamage(
            EnemyInstance runtime,
            string suffix)
        {
            return new EnemyLiveDamageCommand(
                Id("enemy-damage-operation", suffix),
                Id("entity", "player"),
                Id("run-participant", "player"),
                runtime.SpawnStableId,
                runtime.LifecycleGeneration,
                1L,
                1,
                100d);
        }
    }
}

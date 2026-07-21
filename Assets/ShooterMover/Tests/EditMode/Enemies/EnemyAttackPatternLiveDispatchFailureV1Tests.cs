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
        public void AtomicDispatch_ThrowDuringSecondEmissionPrevalidationLeavesNoPartialStateAndRetryCommitsOnce()
        {
            var ports = new RecordingPatternPorts
            {
                ThrowOnDispatchEmissionIndex = 1,
            };
            EnemyPlacementRuntimeInstanceV1 runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
            StableId operation = Id("enemy-operation", "atomic-retry");

            EnemyAttackExecutionResultV1 failed = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                operation,
                10d);

            Assert.That(failed.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Rejected));
            Assert.That(failed.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.InvalidCommand));
            Assert.That(ports.DispatchedSequences, Is.Empty);
            Assert.That(ports.Emissions, Is.Empty);
            Assert.That(runtime.AttackPatterns.Sequences, Has.Count.EqualTo(1));
            Assert.That(ports.LastAttemptedSequence, Is.Not.Null);
            EnemyPlayerDamagePortResultV1 beforeCommit = runtime.RoutePlayerImpact(
                ports.LastAttemptedSequence.Execution,
                Id("enemy-hit", "before-dispatch-commit"),
                Id("entity", "player"),
                1L);
            Assert.That(beforeCommit.Status,
                Is.EqualTo(EnemyRuntimeOperationStatusV1.Rejected));
            Assert.That(beforeCommit.Rejection,
                Is.EqualTo(EnemyRuntimeRejectionCodeV1.ExecutionNotIssued));

            EnemyAttackExecutionResultV1 retried = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                operation,
                10d);
            EnemyAttackExecutionResultV1 replay = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                operation,
                10d);

            Assert.That(retried.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(retried.IsAccepted, Is.True,
                "A failed dispatch must not consume cooldown or outer replay state.");
            Assert.That(replay.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.ExactReplay));
            Assert.That(replay.Request, Is.SameAs(retried.Request));
            Assert.That(runtime.AttackPatterns.Sequences, Has.Count.EqualTo(1));
            Assert.That(ports.DispatchedSequences, Has.Count.EqualTo(1));
            Assert.That(ports.Emissions, Has.Count.EqualTo(3));
            Assert.That(ports.DispatchAttempts, Is.EqualTo(2));
            EnemyPlayerDamagePortResultV1 afterCommit = runtime.RoutePlayerImpact(
                ports.LastAttemptedSequence.Execution,
                Id("enemy-hit", "after-dispatch-commit"),
                Id("entity", "player"),
                1L);
            Assert.That(afterCommit.Status,
                Is.EqualTo(EnemyRuntimeOperationStatusV1.NoEffect));
            Assert.That(afterCommit.Rejection,
                Is.EqualTo(EnemyRuntimeRejectionCodeV1.None));
        }

        [Test]
        public void CancellationFailure_ExactRetryRedeliversCanonicalFactWithoutDuplicateQueueMutation()
        {
            var ports = new RecordingPatternPorts
            {
                RejectNextCancellation = true,
            };
            EnemyPlacementRuntimeInstanceV1 runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
            runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                Id("enemy-operation", "cancel-retry-attack"),
                10d);
            var command = new EnemyAttackLifecycleCancellationCommandV1(
                Id("enemy-pattern-operation", "cancel-retry"),
                runtime.SpawnStableId,
                runtime.LifecycleGeneration,
                10.1d);

            EnemyAttackPatternCancellationResultV1 failed =
                runtime.CancelAttackPatterns(command);
            EnemyAttackPatternCancellationResultV1 retried =
                runtime.CancelAttackPatterns(command);
            EnemyAttackPatternCancellationResultV1 replay =
                runtime.CancelAttackPatterns(command);

            Assert.That(failed.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.Applied));
            Assert.That(failed.IsAuthorityAccepted, Is.True);
            Assert.That(failed.IsAccepted, Is.False);
            Assert.That(failed.Dispatch.Rejection,
                Is.EqualTo(EnemyAttackPatternDispatchRejectionCodeV1.DownstreamFailure));
            Assert.That(retried.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(retried.Fact, Is.SameAs(failed.Fact));
            Assert.That(retried.IsAccepted, Is.True);
            Assert.That(replay.Dispatch.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(ports.AcceptedCancellationCount, Is.EqualTo(1));
            Assert.That(ports.CancellationAttempts, Is.EqualTo(3));

            ports.ProcessScheduledEffects(10.5d);
            AssertOnlyFirstEmissionExecuted(ports);
        }

        [Test]
        public void LethalDamage_AutomaticallyCancelsPendingSequenceWithoutManualCancellation()
        {
            var ports = new RecordingPatternPorts();
            EnemyPlacementRuntimeInstanceV1 runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
            runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                Id("enemy-operation", "death-cancel-attack"),
                10d);
            EnemyRuntimeDamageCommandV1 damage = LethalDamage(runtime, "death-cancel");

            EnemyRuntimeDamageResultV1 result = runtime.ApplyDamage(damage, 10.1d);
            ports.ProcessScheduledEffects(10.5d);

            Assert.That(result.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
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
            EnemyPlacementRuntimeInstanceV1 runtime = Runtime(BurstDefinition(), ports.Bundle);
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
            runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                Id("enemy-operation", "death-cancel-retry-attack"),
                10d);
            EnemyRuntimeDamageCommandV1 damage =
                LethalDamage(runtime, "death-cancel-retry");

            EnemyRuntimeDamageResultV1 failed = runtime.ApplyDamage(damage, 10.1d);
            EnemyRuntimeDamageResultV1 retried = runtime.ApplyDamage(damage, 10.1d);
            EnemyRuntimeDamageResultV1 replay = runtime.ApplyDamage(damage, 10.1d);
            ports.ProcessScheduledEffects(10.5d);

            Assert.That(failed.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Rejected));
            Assert.That(failed.DeathFact, Is.Not.Null);
            Assert.That(runtime.ActorState.IsActive, Is.False);
            Assert.That(retried.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(replay.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.ExactReplay));
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

        private static EnemyRuntimeDamageCommandV1 LethalDamage(
            EnemyPlacementRuntimeInstanceV1 runtime,
            string suffix)
        {
            return new EnemyRuntimeDamageCommandV1(
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

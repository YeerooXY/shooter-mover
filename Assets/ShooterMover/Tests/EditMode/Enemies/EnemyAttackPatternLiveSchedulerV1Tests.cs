using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed partial class EnemyAttackPatternAuthorityV1Tests
    {
        private sealed class RunTimeStub : IEnemyAttackPatternRunTimeV1
        {
            public double CurrentTimeSeconds { get; set; }
            public bool Current { get; set; } = true;
            public bool IsCurrent(EnemyAttackExecutionRequestV1 execution) { return Current; }
        }

        private sealed class RealizerStub : IEnemyAttackPatternEmissionRealizerV1
        {
            public readonly List<EnemyAttackEffectEmissionV1> Emitted =
                new List<EnemyAttackEffectEmissionV1>();
            public readonly List<EnemyAttackEffectEmissionV1> Cancelled =
                new List<EnemyAttackEffectEmissionV1>();
            public StableId RejectEmission;

            public bool CanRealize(EnemyAttackEffectEmissionV1 emission, out string rejectionCode)
            {
                rejectionCode = emission.EmissionStableId == RejectEmission
                    ? "fixture-rejection"
                    : string.Empty;
                return emission.EmissionStableId != RejectEmission;
            }

            public void Realize(EnemyAttackEffectEmissionV1 emission) { Emitted.Add(emission); }
            public void CancelActiveWindow(EnemyAttackEffectEmissionV1 emission) { Cancelled.Add(emission); }
        }

        [Test]
        public void LiveScheduler_AcceptsCompleteSequenceAtomically_AndExactReplayDoesNotDuplicate()
        {
            EnemyAttackSequenceDispatchV1 dispatch = DispatchFixture(
                Shooting("live-atomic", 3, 0.25d, 1, 0d, 0.5d, 0.25d, 8d, null),
                "live-atomic",
                10d);
            var time = new RunTimeStub { CurrentTimeSeconds = 10d };
            var realizer = new RealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(time, realizer);

            EnemyAttackPatternDispatchResultV1 first = scheduler.Dispatch(dispatch);
            EnemyAttackPatternDispatchResultV1 replay = scheduler.Dispatch(dispatch);

            Assert.That(first.Status, Is.EqualTo(EnemyAttackPatternOperationStatusV1.Applied));
            Assert.That(replay.Status, Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            time.CurrentTimeSeconds = 20d;
            scheduler.Tick();
            scheduler.Tick();
            Assert.That(realizer.Emitted.Count, Is.EqualTo(3));
        }

        [Test]
        public void LiveScheduler_PreflightFailure_QueuesAndEmitsNothing()
        {
            EnemyAttackSequenceDispatchV1 dispatch = DispatchFixture(
                Shooting("live-preflight", 3, 0.25d, 1, 0d, 0d, 0.25d, 8d, null),
                "live-preflight",
                2d);
            var time = new RunTimeStub { CurrentTimeSeconds = 20d };
            var realizer = new RealizerStub { RejectEmission = dispatch.Emissions[1].EmissionStableId };
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(time, realizer);

            EnemyAttackPatternDispatchResultV1 result = scheduler.Dispatch(dispatch);
            scheduler.Tick();

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(realizer.Emitted, Is.Empty);
        }

        [Test]
        public void LiveScheduler_VariableTicks_EmitCanonicalTimestampAndIdentityOrder()
        {
            EnemyAttackSequenceDispatchV1 dispatch = DispatchFixture(
                Shooting("live-order", 2, 0.5d, 3, 18d, 1d, 0d, 8d, null),
                "live-order",
                4d);
            var time = new RunTimeStub { CurrentTimeSeconds = 0d };
            var realizer = new RealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(time, realizer);
            Assert.That(scheduler.Dispatch(dispatch).IsAccepted, Is.True);

            time.CurrentTimeSeconds = 4.9d;
            scheduler.Tick();
            Assert.That(realizer.Emitted, Is.Empty);
            time.CurrentTimeSeconds = 99d;
            scheduler.Tick();

            Assert.That(realizer.Emitted.Count, Is.EqualTo(dispatch.Emissions.Count));
            for (int index = 1; index < realizer.Emitted.Count; index++)
            {
                EnemyAttackEffectEmissionV1 previous = realizer.Emitted[index - 1];
                EnemyAttackEffectEmissionV1 current = realizer.Emitted[index];
                Assert.That(previous.ScheduledAtSeconds, Is.LessThanOrEqualTo(current.ScheduledAtSeconds));
                if (previous.ScheduledAtSeconds == current.ScheduledAtSeconds)
                    Assert.That(previous.EmissionStableId.CompareTo(current.EmissionStableId), Is.LessThan(0));
            }
        }

        [Test]
        public void LiveScheduler_UsesCommittedIntentAndSchemaDirectionsWithoutRecalculation()
        {
            EnemyAttackSequenceDispatchV1 dispatch = DispatchFixture(
                Shooting("live-aim", 1, 0d, 3, 30d, 1d, 0d, 8d, null),
                "live-aim",
                5d);
            var time = new RunTimeStub { CurrentTimeSeconds = 50d };
            var realizer = new RealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(time, realizer);
            scheduler.Dispatch(dispatch);
            scheduler.Tick();

            Assert.That(realizer.Emitted.Count, Is.EqualTo(3));
            for (int index = 0; index < realizer.Emitted.Count; index++)
            {
                Assert.That(realizer.Emitted[index].CommittedIntent,
                    Is.SameAs(dispatch.Execution.CommittedIntent));
                Assert.That(realizer.Emitted[index].Projectile.SpreadOffsetDegrees,
                    Is.EqualTo(dispatch.Emissions[index].Projectile.SpreadOffsetDegrees));
            }
        }

        [Test]
        public void LiveScheduler_CancellationSuppressesFutureEmissions_AndReplaysExactly()
        {
            EnemyAttackSequenceDispatchV1 dispatch = DispatchFixture(
                Shooting("live-cancel", 3, 1d, 1, 0d, 0d, 0d, 8d, null),
                "live-cancel",
                10d);
            var time = new RunTimeStub { CurrentTimeSeconds = 10d };
            var realizer = new RealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(time, realizer);
            scheduler.Dispatch(dispatch);
            scheduler.Tick();
            Assert.That(realizer.Emitted.Count, Is.EqualTo(1));

            var cancelledProjectiles = new[]
            {
                dispatch.Emissions[1].EmissionStableId,
                dispatch.Emissions[2].EmissionStableId,
            };
            var cancellation = new EnemyAttackSequenceCancellationFactV1(
                Id("enemy-attack-cancellation.live-cancel"),
                dispatch.Execution.Identity.EntityInstanceId,
                dispatch.Execution.LifecycleGeneration,
                10.5d,
                new StableId[0],
                cancelledProjectiles,
                new StableId[0]);

            EnemyAttackPatternDispatchResultV1 first = scheduler.Cancel(cancellation);
            EnemyAttackPatternDispatchResultV1 replay = scheduler.Cancel(cancellation);
            time.CurrentTimeSeconds = 100d;
            scheduler.Tick();

            Assert.That(first.Status, Is.EqualTo(EnemyAttackPatternOperationStatusV1.Applied));
            Assert.That(replay.Status, Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(realizer.Emitted.Count, Is.EqualTo(1));
        }

        [Test]
        public void LiveScheduler_WrongRunOrLifecycleFailsClosed()
        {
            EnemyAttackSequenceDispatchV1 dispatch = DispatchFixture(
                Shooting("live-stale", 1, 0d, 1, 0d, 0d, 0d, 8d, null),
                "live-stale",
                0d);
            var time = new RunTimeStub { CurrentTimeSeconds = 10d, Current = false };
            var realizer = new RealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(time, realizer);

            Assert.That(scheduler.Dispatch(dispatch).IsAccepted, Is.False);
            scheduler.Tick();
            Assert.That(realizer.Emitted, Is.Empty);
        }

        [Test]
        public void LiveScheduler_MeleeWindowCarriesExactCommittedBounds()
        {
            EnemyAttackSequenceDispatchV1 dispatch = DispatchFixture(
                Melee("live-melee", 0.5d, 0.75d, 1, 0d, 0.4d, 2d, 0.25d,
                    EnemyMeleeAimCommitPolicyV1.LockAtWindUp,
                    EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence),
                "live-melee",
                3d);
            var time = new RunTimeStub { CurrentTimeSeconds = 20d };
            var realizer = new RealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(time, realizer);
            scheduler.Dispatch(dispatch);
            scheduler.Tick();

            Assert.That(realizer.Emitted.Count, Is.EqualTo(1));
            Assert.That(realizer.Emitted[0].Kind,
                Is.EqualTo(EnemyAttackEffectEmissionKindV1.MeleeStrike));
            Assert.That(realizer.Emitted[0].ActiveUntilSeconds,
                Is.EqualTo(realizer.Emitted[0].MeleeStrike.ActiveUntilSeconds));
        }

        private static EnemyAttackSequenceDispatchV1 DispatchFixture(
            EnemyAttackCapabilityDescriptorV1 descriptor,
            string operationSuffix,
            double occurredAtSeconds)
        {
            EnemyRuntimeIdentityV1 identity = Identity();
            EnemyAttackExecutionRequestV1 execution =
                Execution(identity, descriptor, operationSuffix, occurredAtSeconds);
            EnemyAttackSequenceV1 sequence = EnemyAttackPatternSchedulerV1.Schedule(execution);
            return new EnemyAttackSequenceDispatchV1(
                execution,
                sequence,
                EnemyAttackEffectEmissionProjectorV1.Project(execution, sequence));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed partial class EnemyAttackPatternAuthorityV1Tests
    {
        private sealed class LiveRunTimeStub : IEnemyAttackPatternRunTimeV1
        {
            public double CurrentTimeSeconds { get; set; }
            public bool IsCurrentResult { get; set; } = true;

            public bool IsCurrent(EnemyAttackExecutionRequestV1 execution)
            {
                return IsCurrentResult && execution != null;
            }
        }

        private sealed class LiveRealizerStub :
            IEnemyAttackPatternEmissionRealizerV1
        {
            public readonly List<EnemyAttackEffectEmissionV1> Emitted =
                new List<EnemyAttackEffectEmissionV1>();
            public readonly List<EnemyAttackEffectEmissionV1> Cancelled =
                new List<EnemyAttackEffectEmissionV1>();
            public StableId RejectedEmissionStableId;

            public bool CanRealize(
                EnemyAttackEffectEmissionV1 emission,
                out string rejectionCode)
            {
                bool accepted = emission != null
                    && emission.EmissionStableId
                        != RejectedEmissionStableId;
                rejectionCode = accepted
                    ? string.Empty
                    : "fixture-preflight-rejection";
                return accepted;
            }

            public void Realize(EnemyAttackEffectEmissionV1 emission)
            {
                Emitted.Add(emission);
            }

            public void CancelActiveWindow(
                EnemyAttackEffectEmissionV1 emission)
            {
                Cancelled.Add(emission);
            }
        }

        [Test]
        public void LiveScheduler_AcceptsCompleteSequenceAtomically_AndExactReplayDoesNotDuplicate()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Shooting(
                    "live-atomic",
                    3,
                    0.25d,
                    1,
                    0d,
                    0.5d,
                    0.25d,
                    8d,
                    null),
                "live-atomic",
                10d);
            var time = new LiveRunTimeStub { CurrentTimeSeconds = 10d };
            var realizer = new LiveRealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(
                time,
                realizer);

            EnemyAttackPatternDispatchResultV1 first =
                scheduler.Dispatch(dispatch);
            EnemyAttackPatternDispatchResultV1 replay =
                scheduler.Dispatch(dispatch);

            Assert.That(first.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(scheduler.PendingEmissionCount,
                Is.EqualTo(dispatch.Emissions.Count));

            time.CurrentTimeSeconds = 99d;
            scheduler.Tick();
            scheduler.Tick();

            Assert.That(realizer.Emitted.Count,
                Is.EqualTo(dispatch.Emissions.Count));
            Assert.That(realizer.Emitted.Select(item => item.EmissionStableId),
                Is.EquivalentTo(
                    dispatch.Emissions.Select(item => item.EmissionStableId)));
        }

        [Test]
        public void LiveScheduler_PreflightFailureProducesNoPartialQueueOrEffects()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Shooting(
                    "live-preflight",
                    3,
                    0.25d,
                    1,
                    0d,
                    0d,
                    0.25d,
                    8d,
                    null),
                "live-preflight",
                2d);
            var time = new LiveRunTimeStub { CurrentTimeSeconds = 20d };
            var realizer = new LiveRealizerStub
            {
                RejectedEmissionStableId =
                    dispatch.Emissions[1].EmissionStableId,
            };
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(
                time,
                realizer);

            EnemyAttackPatternDispatchResultV1 result =
                scheduler.Dispatch(dispatch);
            scheduler.Tick();

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(scheduler.PendingEmissionCount, Is.Zero);
            Assert.That(realizer.Emitted, Is.Empty);
        }

        [Test]
        public void LiveScheduler_ConflictingSequenceReplayRejectsWithoutMutation()
        {
            EnemyAttackSequenceDispatchV1 first = LiveDispatch(
                Shooting(
                    "live-conflict",
                    2,
                    0.2d,
                    1,
                    0d,
                    0d,
                    0.1d,
                    8d,
                    null),
                "live-conflict",
                3d);
            EnemyAttackSequenceDispatchV1 conflicting = LiveDispatch(
                Shooting(
                    "live-conflict",
                    2,
                    0.2d,
                    1,
                    0d,
                    0d,
                    0.1d,
                    9d,
                    null),
                "live-conflict",
                3d);
            Assert.That(conflicting.DispatchStableId,
                Is.EqualTo(first.DispatchStableId));
            Assert.That(conflicting.Fingerprint,
                Is.Not.EqualTo(first.Fingerprint));

            var time = new LiveRunTimeStub();
            var realizer = new LiveRealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(
                time,
                realizer);
            Assert.That(scheduler.Dispatch(first).IsAccepted, Is.True);
            int pending = scheduler.PendingEmissionCount;

            EnemyAttackPatternDispatchResultV1 result =
                scheduler.Dispatch(conflicting);

            Assert.That(result.Rejection,
                Is.EqualTo(
                    EnemyAttackPatternDispatchRejectionCodeV1
                        .ConflictingDuplicate));
            Assert.That(scheduler.PendingEmissionCount, Is.EqualTo(pending));
        }

        [Test]
        public void LiveScheduler_VariableTickIntervalsProduceSameCanonicalEmissionOrder()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Shooting(
                    "live-variable-ticks",
                    3,
                    0.5d,
                    3,
                    18d,
                    1d,
                    0d,
                    8d,
                    null),
                "live-variable-ticks",
                4d);

            var coarseTime = new LiveRunTimeStub();
            var coarseRealizer = new LiveRealizerStub();
            var coarse = new EnemyAttackPatternLiveSchedulerV1(
                coarseTime,
                coarseRealizer);
            coarse.Dispatch(dispatch);
            coarseTime.CurrentTimeSeconds = 100d;
            coarse.Tick();

            var fineTime = new LiveRunTimeStub();
            var fineRealizer = new LiveRealizerStub();
            var fine = new EnemyAttackPatternLiveSchedulerV1(
                fineTime,
                fineRealizer);
            fine.Dispatch(dispatch);
            double[] ticks = { 4d, 4.9d, 5d, 5.5d, 6d, 100d };
            for (int index = 0; index < ticks.Length; index++)
            {
                fineTime.CurrentTimeSeconds = ticks[index];
                fine.Tick();
            }

            StableId[] coarseIds = coarseRealizer.Emitted
                .Select(item => item.EmissionStableId)
                .ToArray();
            StableId[] fineIds = fineRealizer.Emitted
                .Select(item => item.EmissionStableId)
                .ToArray();
            Assert.That(fineIds, Is.EqualTo(coarseIds));
            for (int index = 1; index < fineRealizer.Emitted.Count; index++)
            {
                EnemyAttackEffectEmissionV1 previous =
                    fineRealizer.Emitted[index - 1];
                EnemyAttackEffectEmissionV1 current =
                    fineRealizer.Emitted[index];
                Assert.That(previous.ScheduledAtSeconds,
                    Is.LessThanOrEqualTo(current.ScheduledAtSeconds));
                if (previous.ScheduledAtSeconds
                    == current.ScheduledAtSeconds)
                {
                    Assert.That(
                        previous.EmissionStableId.CompareTo(
                            current.EmissionStableId),
                        Is.LessThan(0));
                }
            }
        }

        [Test]
        public void LiveScheduler_DelayedBurstUsesCommittedAimAndSchemaScatterWithoutReroll()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Shooting(
                    "live-locked-aim",
                    3,
                    0.4d,
                    3,
                    30d,
                    1d,
                    0d,
                    8d,
                    null),
                "live-locked-aim",
                5d);
            var time = new LiveRunTimeStub { CurrentTimeSeconds = 50d };
            var realizer = new LiveRealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(
                time,
                realizer);

            scheduler.Dispatch(dispatch);
            scheduler.Tick();

            Assert.That(realizer.Emitted.Count,
                Is.EqualTo(9));
            for (int index = 0; index < realizer.Emitted.Count; index++)
            {
                EnemyAttackEffectEmissionV1 emitted = realizer.Emitted[index];
                EnemyAttackEffectEmissionV1 authored = dispatch.Emissions[index];
                Assert.That(emitted.CommittedIntent,
                    Is.SameAs(dispatch.Execution.CommittedIntent));
                Assert.That(emitted.Projectile.SpreadOffsetDegrees,
                    Is.EqualTo(authored.Projectile.SpreadOffsetDegrees));
                Assert.That(emitted.ScheduledAtSeconds,
                    Is.EqualTo(authored.ScheduledAtSeconds));
            }
        }

        [Test]
        public void LiveScheduler_CancellationSuppressesFutureEmissionsAndReplaysExactly()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Shooting(
                    "live-cancel",
                    3,
                    1d,
                    1,
                    0d,
                    0d,
                    0d,
                    8d,
                    null),
                "live-cancel",
                10d);
            var time = new LiveRunTimeStub { CurrentTimeSeconds = 10d };
            var realizer = new LiveRealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(
                time,
                realizer);
            scheduler.Dispatch(dispatch);
            scheduler.Tick();
            Assert.That(realizer.Emitted.Count, Is.EqualTo(1));

            var cancellation = new EnemyAttackSequenceCancellationFactV1(
                Id("enemy-attack-cancellation.live-cancel"),
                dispatch.Execution.Identity.EntityInstanceId,
                dispatch.Execution.LifecycleGeneration,
                10.5d,
                new StableId[0],
                new[]
                {
                    dispatch.Emissions[1].EmissionStableId,
                    dispatch.Emissions[2].EmissionStableId,
                },
                new StableId[0]);
            EnemyAttackPatternDispatchResultV1 first =
                scheduler.Cancel(cancellation);
            EnemyAttackPatternDispatchResultV1 replay =
                scheduler.Cancel(cancellation);
            time.CurrentTimeSeconds = 100d;
            scheduler.Tick();

            Assert.That(first.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(realizer.Emitted.Count, Is.EqualTo(1));
            Assert.That(scheduler.PendingEmissionCount, Is.Zero);
        }

        [Test]
        public void LiveScheduler_ConflictingCancellationRejectsWithoutMutation()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Shooting(
                    "live-cancel-conflict",
                    2,
                    1d,
                    1,
                    0d,
                    0d,
                    0d,
                    8d,
                    null),
                "live-cancel-conflict",
                10d);
            var time = new LiveRunTimeStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(
                time,
                new LiveRealizerStub());
            scheduler.Dispatch(dispatch);

            StableId cancellationId =
                Id("enemy-attack-cancellation.live-conflict");
            var first = new EnemyAttackSequenceCancellationFactV1(
                cancellationId,
                dispatch.Execution.Identity.EntityInstanceId,
                dispatch.Execution.LifecycleGeneration,
                10d,
                new StableId[0],
                new[] { dispatch.Emissions[0].EmissionStableId },
                new StableId[0]);
            var conflicting = new EnemyAttackSequenceCancellationFactV1(
                cancellationId,
                dispatch.Execution.Identity.EntityInstanceId,
                dispatch.Execution.LifecycleGeneration,
                10.1d,
                new StableId[0],
                new[] { dispatch.Emissions[1].EmissionStableId },
                new StableId[0]);
            Assert.That(scheduler.Cancel(first).IsAccepted, Is.True);
            int pending = scheduler.PendingEmissionCount;

            EnemyAttackPatternDispatchResultV1 result =
                scheduler.Cancel(conflicting);

            Assert.That(result.Rejection,
                Is.EqualTo(
                    EnemyAttackPatternDispatchRejectionCodeV1
                        .ConflictingDuplicate));
            Assert.That(scheduler.PendingEmissionCount, Is.EqualTo(pending));
        }

        [Test]
        public void LiveScheduler_TerminalSourceGatePreventsLaterBurstEmissions()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Shooting(
                    "live-terminal-source",
                    3,
                    1d,
                    1,
                    0d,
                    0d,
                    0d,
                    8d,
                    null),
                "live-terminal-source",
                2d);
            var time = new LiveRunTimeStub { CurrentTimeSeconds = 2d };
            var realizer = new LiveRealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(
                time,
                realizer);
            scheduler.Dispatch(dispatch);
            scheduler.Tick();
            Assert.That(realizer.Emitted.Count, Is.EqualTo(1));

            time.IsCurrentResult = false;
            time.CurrentTimeSeconds = 100d;
            scheduler.Tick();

            Assert.That(realizer.Emitted.Count, Is.EqualTo(1));
            Assert.That(scheduler.PendingEmissionCount, Is.EqualTo(2));
        }

        [Test]
        public void LiveScheduler_MeleeWindowOpensClosesAndCancelsAtCommittedBounds()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Melee(
                    "live-melee-window",
                    0.5d,
                    0.75d,
                    1,
                    0d,
                    0.4d,
                    0d,
                    0.25d,
                    EnemyMeleeAimCommitPolicyV1.LockAtWindUp,
                    EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence),
                "live-melee-window",
                3d);
            var time = new LiveRunTimeStub { CurrentTimeSeconds = 3.5d };
            var realizer = new LiveRealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(
                time,
                realizer);
            scheduler.Dispatch(dispatch);
            scheduler.Tick();

            Assert.That(realizer.Emitted.Count, Is.EqualTo(1));
            Assert.That(scheduler.ActiveMeleeWindowCount, Is.EqualTo(1));
            Assert.That(realizer.Emitted[0].ActiveUntilSeconds,
                Is.EqualTo(
                    realizer.Emitted[0].MeleeStrike.ActiveUntilSeconds));

            var cancellation = new EnemyAttackSequenceCancellationFactV1(
                Id("enemy-attack-cancellation.live-melee"),
                dispatch.Execution.Identity.EntityInstanceId,
                dispatch.Execution.LifecycleGeneration,
                3.75d,
                new StableId[0],
                new StableId[0],
                new[] { dispatch.Emissions[0].EmissionStableId });
            Assert.That(scheduler.Cancel(cancellation).IsAccepted, Is.True);
            Assert.That(scheduler.ActiveMeleeWindowCount, Is.Zero);
            Assert.That(realizer.Cancelled.Count, Is.EqualTo(1));
        }

        [Test]
        public void LiveScheduler_WrongRunOrLifecycleFailsClosed()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Shooting(
                    "live-stale",
                    1,
                    0d,
                    1,
                    0d,
                    0d,
                    0d,
                    8d,
                    null),
                "live-stale",
                0d);
            var time = new LiveRunTimeStub
            {
                CurrentTimeSeconds = 10d,
                IsCurrentResult = false,
            };
            var realizer = new LiveRealizerStub();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(
                time,
                realizer);

            EnemyAttackPatternDispatchResultV1 result =
                scheduler.Dispatch(dispatch);
            scheduler.Tick();

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(scheduler.PendingEmissionCount, Is.Zero);
            Assert.That(realizer.Emitted, Is.Empty);
        }

        private static EnemyAttackSequenceDispatchV1 LiveDispatch(
            EnemyAttackCapabilityDescriptorV1 descriptor,
            string operationSuffix,
            double occurredAtSeconds)
        {
            EnemyRuntimeIdentityV1 identity = Identity();
            EnemyAttackExecutionRequestV1 execution =
                Execution(
                    identity,
                    descriptor,
                    operationSuffix,
                    occurredAtSeconds);
            EnemyAttackSequenceV1 sequence =
                EnemyAttackPatternSchedulerV1.Schedule(execution);
            return new EnemyAttackSequenceDispatchV1(
                execution,
                sequence,
                EnemyAttackEffectEmissionProjectorV1.Project(
                    execution,
                    sequence));
        }
    }
}

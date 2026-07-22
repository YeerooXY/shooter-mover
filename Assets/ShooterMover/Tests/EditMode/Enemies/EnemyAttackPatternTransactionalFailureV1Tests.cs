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
        private sealed class FailOnceLegacyRealizer :
            IEnemyAttackPatternEmissionRealizerV1
        {
            public int RemainingRealizeFailures;
            public int RemainingCancelFailures;
            public int RealizeCallCount;
            public int CancelCallCount;
            public readonly List<EnemyAttackEffectEmissionV1> Realized =
                new List<EnemyAttackEffectEmissionV1>();
            public readonly List<EnemyAttackEffectEmissionV1> Cancelled =
                new List<EnemyAttackEffectEmissionV1>();

            public bool CanRealize(
                EnemyAttackEffectEmissionV1 emission,
                out string rejectionCode)
            {
                rejectionCode = string.Empty;
                return emission != null;
            }

            public void Realize(EnemyAttackEffectEmissionV1 emission)
            {
                RealizeCallCount++;
                if (RemainingRealizeFailures > 0)
                {
                    RemainingRealizeFailures--;
                    throw new InvalidOperationException("fixture-realize-failure");
                }
                Realized.Add(emission);
            }

            public void CancelActiveWindow(EnemyAttackEffectEmissionV1 emission)
            {
                CancelCallCount++;
                if (RemainingCancelFailures > 0)
                {
                    RemainingCancelFailures--;
                    throw new InvalidOperationException("fixture-cancel-failure");
                }
                Cancelled.Add(emission);
            }
        }

        [Test]
        public void LiveScheduler_RetryableRealizeFailureKeepsEmissionPendingUntilAccepted()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Shooting(
                    "live-realize-retry",
                    1,
                    0d,
                    1,
                    0d,
                    0d,
                    0d,
                    8d,
                    null),
                "live-realize-retry",
                2d);
            var time = new LiveRunTimeStub { CurrentTimeSeconds = 20d };
            var physical = new FailOnceLegacyRealizer
            {
                RemainingRealizeFailures = 1,
            };
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(time, physical);
            Assert.That(scheduler.Dispatch(dispatch).IsAccepted, Is.True);

            scheduler.Tick();

            Assert.That(scheduler.PendingEmissionCount, Is.EqualTo(1));
            Assert.That(physical.Realized, Is.Empty);
            Assert.That(scheduler.Records[scheduler.Records.Count - 1].State,
                Is.EqualTo(EnemyAttackPatternLiveStateV1.RetryableFailure));

            scheduler.Tick();
            scheduler.Tick();

            Assert.That(scheduler.PendingEmissionCount, Is.Zero);
            Assert.That(physical.Realized.Count, Is.EqualTo(1));
            Assert.That(physical.RealizeCallCount, Is.EqualTo(2));
        }

        [Test]
        public void LiveScheduler_RetryableCancellationKeepsActiveWindowUntilCloseAccepted()
        {
            EnemyAttackSequenceDispatchV1 dispatch = LiveDispatch(
                Melee(
                    "live-cancel-retry",
                    0d,
                    1d,
                    1,
                    0d,
                    0.75d,
                    0d,
                    0.5d,
                    EnemyMeleeAimCommitPolicyV1.LockAtWindUp,
                    EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence),
                "live-cancel-retry",
                3d);
            var time = new LiveRunTimeStub { CurrentTimeSeconds = 3d };
            var physical = new FailOnceLegacyRealizer();
            var scheduler = new EnemyAttackPatternLiveSchedulerV1(time, physical);
            scheduler.Dispatch(dispatch);
            scheduler.Tick();
            Assert.That(scheduler.ActiveMeleeWindowCount, Is.EqualTo(1));

            physical.RemainingCancelFailures = 1;
            var cancellation = new EnemyAttackSequenceCancellationFactV1(
                Id("enemy-attack-cancellation.live-cancel-retry"),
                dispatch.Execution.Identity.EntityInstanceId,
                dispatch.Execution.LifecycleGeneration,
                3.1d,
                new StableId[0],
                new StableId[0],
                new[] { dispatch.Emissions[0].EmissionStableId });

            EnemyAttackPatternDispatchResultV1 failed = scheduler.Cancel(cancellation);

            Assert.That(failed.IsAccepted, Is.False);
            Assert.That(scheduler.ActiveMeleeWindowCount, Is.EqualTo(1));

            EnemyAttackPatternDispatchResultV1 applied = scheduler.Cancel(cancellation);
            EnemyAttackPatternDispatchResultV1 replay = scheduler.Cancel(cancellation);

            Assert.That(applied.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(scheduler.ActiveMeleeWindowCount, Is.Zero);
            Assert.That(physical.CancelCallCount, Is.EqualTo(2));
        }
    }
}

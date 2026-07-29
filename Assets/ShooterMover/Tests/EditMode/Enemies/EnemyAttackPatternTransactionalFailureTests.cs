using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed partial class EnemyAttackPatternStateTests
    {
        private sealed class FailOnceLegacyRealizer :
            IEnemyAttackPatternEmissionRealizer
        {
            public int RemainingRealizeFailures;
            public int RemainingCancelFailures;
            public int RealizeCallCount;
            public int CancelCallCount;
            public readonly List<EnemyAttackEffectEmission> Realized =
                new List<EnemyAttackEffectEmission>();
            public readonly List<EnemyAttackEffectEmission> Cancelled =
                new List<EnemyAttackEffectEmission>();

            public bool CanRealize(
                EnemyAttackEffectEmission emission,
                out string rejectionCode)
            {
                rejectionCode = string.Empty;
                return emission != null;
            }

            public void Realize(EnemyAttackEffectEmission emission)
            {
                RealizeCallCount++;
                if (RemainingRealizeFailures > 0)
                {
                    RemainingRealizeFailures--;
                    throw new InvalidOperationException("fixture-realize-failure");
                }
                Realized.Add(emission);
            }

            public void CancelActiveWindow(EnemyAttackEffectEmission emission)
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
            EnemyAttackSequenceDispatch dispatch = LiveDispatch(
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
            var scheduler = new EnemyAttackPatternLiveScheduler(time, physical);
            Assert.That(scheduler.Dispatch(dispatch).IsAccepted, Is.True);

            scheduler.Tick();

            Assert.That(scheduler.PendingEmissionCount, Is.EqualTo(1));
            Assert.That(physical.Realized, Is.Empty);
            Assert.That(scheduler.Records[scheduler.Records.Count - 1].State,
                Is.EqualTo(EnemyAttackPatternLiveState.RetryableFailure));

            scheduler.Tick();
            scheduler.Tick();

            Assert.That(scheduler.PendingEmissionCount, Is.Zero);
            Assert.That(physical.Realized.Count, Is.EqualTo(1));
            Assert.That(physical.RealizeCallCount, Is.EqualTo(2));
        }

        [Test]
        public void LiveScheduler_RetryableCancellationKeepsActiveWindowUntilCloseAccepted()
        {
            EnemyAttackSequenceDispatch dispatch = LiveDispatch(
                Melee(
                    "live-cancel-retry",
                    0d,
                    1d,
                    1,
                    0d,
                    0.75d,
                    0d,
                    0.5d,
                    EnemyMeleeAimCommitPolicy.LockAtWindUp,
                    EnemyMeleeTerminalOnImpactPolicy.ContinueSequence),
                "live-cancel-retry",
                3d);
            var time = new LiveRunTimeStub { CurrentTimeSeconds = 3d };
            var physical = new FailOnceLegacyRealizer();
            var scheduler = new EnemyAttackPatternLiveScheduler(time, physical);
            scheduler.Dispatch(dispatch);
            scheduler.Tick();
            Assert.That(scheduler.ActiveMeleeWindowCount, Is.EqualTo(1));

            physical.RemainingCancelFailures = 1;
            var cancellation = new EnemyAttackSequenceCancellationFact(
                Id("enemy-attack-cancellation.live-cancel-retry"),
                dispatch.Execution.Identity.EntityInstanceId,
                dispatch.Execution.LifecycleGeneration,
                3.1d,
                new StableId[0],
                new StableId[0],
                new[] { dispatch.Emissions[0].EmissionStableId });

            EnemyAttackPatternDispatchResult failed = scheduler.Cancel(cancellation);

            Assert.That(failed.IsAccepted, Is.False);
            Assert.That(scheduler.ActiveMeleeWindowCount, Is.EqualTo(1));

            EnemyAttackPatternDispatchResult applied = scheduler.Cancel(cancellation);
            EnemyAttackPatternDispatchResult replay = scheduler.Cancel(cancellation);

            Assert.That(applied.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatus.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatus.ExactReplay));
            Assert.That(scheduler.ActiveMeleeWindowCount, Is.Zero);
            Assert.That(physical.CancelCallCount, Is.EqualTo(2));
        }
    }
}

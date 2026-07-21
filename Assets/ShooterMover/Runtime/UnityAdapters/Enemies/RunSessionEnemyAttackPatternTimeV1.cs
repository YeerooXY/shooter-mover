using System;
using System.Globalization;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.UnityAdapters.Enemies
{
    public interface IEnemyAttackPatternSourceLifecycleV1
    {
        bool IsCurrent(StableId sourceEntityStableId, long lifecycleGeneration);
    }

    /// <summary>
    /// Typed Run Session clock projection for the enemy attack scheduler. It never reads Unity
    /// time and never owns a per-enemy clock; callers explicitly advance the one run aggregate.
    /// </summary>
    public sealed class RunSessionEnemyAttackPatternTimeV1 :
        IEnemyAttackPatternRunTimeV1
    {
        private readonly RunSessionAggregateV1 run;
        private readonly IEnemyAttackPatternSourceLifecycleV1 sourceLifecycles;
        private readonly double ticksPerSecond;

        public RunSessionEnemyAttackPatternTimeV1(
            RunSessionAggregateV1 run,
            IEnemyAttackPatternSourceLifecycleV1 sourceLifecycles,
            double ticksPerSecond)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.sourceLifecycles = sourceLifecycles
                ?? throw new ArgumentNullException(nameof(sourceLifecycles));
            if (double.IsNaN(ticksPerSecond)
                || double.IsInfinity(ticksPerSecond)
                || ticksPerSecond <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }
            this.ticksPerSecond = ticksPerSecond;
        }

        public StableId RunStableId
        {
            get { return run.RunStableId; }
        }

        public long RunLifecycleGeneration
        {
            get { return run.LifecycleGeneration; }
        }

        public long AuthoritativeTick
        {
            get { return run.AuthoritativeTick; }
        }

        public double CurrentTimeSeconds
        {
            get { return run.AuthoritativeTick / ticksPerSecond; }
        }

        public bool IsCurrent(EnemyAttackExecutionRequestV1 execution)
        {
            return execution != null
                && execution.Identity != null
                && execution.Identity.RunStableId == run.RunStableId
                && run.LifecycleState == RunSessionLifecycleStateV1.Active
                && sourceLifecycles.IsCurrent(
                    execution.Identity.EntityInstanceId,
                    execution.LifecycleGeneration);
        }

        public RunSessionTimeAdvanceResultV1 AdvanceTo(long authoritativeTick)
        {
            if (authoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));
            }
            StableId operationStableId = StableId.Create(
                "run-time-advance",
                "enemy-pattern-"
                    + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                        run.RunStableId
                        + "|"
                        + run.LifecycleGeneration.ToString(
                            CultureInfo.InvariantCulture)
                        + "|"
                        + authoritativeTick.ToString(
                            CultureInfo.InvariantCulture)));
            return run.AdvanceTime(
                new AdvanceRunSessionTimeCommandV1(
                    operationStableId,
                    run.RunStableId,
                    run.LifecycleGeneration,
                    authoritativeTick));
        }
    }
}

using System;
using System.Globalization;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.UnityAdapters.Enemies
{
    public interface IEnemyAttackPatternSourceLifecycle
    {
        bool IsCurrent(StableId sourceEntityStableId, long lifecycleGeneration);
    }

    /// <summary>
    /// Typed Run Session clock projection for the enemy attack scheduler. It never reads Unity
    /// time and never owns a per-enemy clock; callers explicitly advance the one run aggregate.
    /// </summary>
    public sealed class RunSessionEnemyAttackPatternTime :
        IEnemyAttackPatternRunTime
    {
        private readonly RunSessionAggregate run;
        private readonly IEnemyAttackPatternSourceLifecycle sourceLifecycles;
        private readonly double ticksPerSecond;

        public RunSessionEnemyAttackPatternTime(
            RunSessionAggregate run,
            IEnemyAttackPatternSourceLifecycle sourceLifecycles,
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

        public bool IsCurrent(EnemyAttackExecutionRequest execution)
        {
            return execution != null
                && execution.Identity != null
                && execution.Identity.RunStableId == run.RunStableId
                && run.LifecycleState == RunSessionLifecycleState.Active
                && sourceLifecycles.IsCurrent(
                    execution.Identity.EntityInstanceId,
                    execution.LifecycleGeneration);
        }

        public RunSessionTimeAdvanceResult AdvanceTo(long authoritativeTick)
        {
            if (authoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));
            }
            StableId operationStableId = StableId.Create(
                "run-time-advance",
                "enemy-pattern-"
                    + DeterministicEnemyLiveIdentityDeriver.Hash64(
                        run.RunStableId
                        + "|"
                        + run.LifecycleGeneration.ToString(
                            CultureInfo.InvariantCulture)
                        + "|"
                        + authoritativeTick.ToString(
                            CultureInfo.InvariantCulture)));
            return run.AdvanceTime(
                new AdvanceRunSessionTimeCommand(
                    operationStableId,
                    run.RunStableId,
                    run.LifecycleGeneration,
                    authoritativeTick));
        }
    }
}

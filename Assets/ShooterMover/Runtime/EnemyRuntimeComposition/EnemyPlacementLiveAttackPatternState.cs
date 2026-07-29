using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed partial class EnemyPlacementLiveInstance
    {
        private EnemyAttackPatternState attackPatternAuthority;

        public EnemyAttackPatternState AttackPatterns
        {
            get
            {
                if (attackPatternAuthority == null)
                {
                    attackPatternAuthority = new EnemyAttackPatternState(
                        Identity,
                        LifecycleGeneration);
                }
                return attackPatternAuthority;
            }
        }

        public EnemyAttackPatternCancellationResult CancelAttackPatterns(
            EnemyAttackLifecycleCancellationCommand command)
        {
            EnemyAttackPatternCancellationResult authority =
                AttackPatterns.CancelLifecycle(command);
            if (!authority.IsAuthorityAccepted || authority.Fact == null)
                return authority;

            EnemyAttackPatternDispatchResult dispatch =
                EnemyAttackEffectEmissionDispatch.Cancel(
                    downstream.AttackEffects,
                    authority.Fact);
            return new EnemyAttackPatternCancellationResult(
                authority.Status,
                authority.Rejection,
                authority.Fact,
                dispatch);
        }

        private EnemyAttackPatternStartResult StartAttackPattern(
            EnemyAttackExecutionRequest execution)
        {
            if (!EnemyAttackEffectEmissionDispatch.CanDispatch(
                downstream.AttackEffects,
                execution))
            {
                return new EnemyAttackPatternStartResult(
                    EnemyAttackPatternOperationStatus.Rejected,
                    EnemyAttackPatternRejectionCode.InvalidCommand,
                    null,
                    null);
            }
            return AttackPatterns.Start(execution);
        }

        private EnemyAttackPatternDispatchResult DispatchAttackPattern(
            EnemyAttackExecutionRequest execution,
            EnemyAttackPatternStartResult pattern)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            if (pattern == null || !pattern.IsAccepted)
                throw new ArgumentException(
                    "Only accepted attack patterns may be dispatched.",
                    nameof(pattern));
            return EnemyAttackEffectEmissionDispatch.Dispatch(
                downstream.AttackEffects,
                execution,
                pattern);
        }
    }
}

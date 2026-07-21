using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed partial class EnemyPlacementRuntimeInstanceV1
    {
        private EnemyAttackPatternAuthorityV1 attackPatternAuthority;

        public EnemyAttackPatternAuthorityV1 AttackPatterns
        {
            get
            {
                if (attackPatternAuthority == null)
                {
                    attackPatternAuthority = new EnemyAttackPatternAuthorityV1(
                        Identity,
                        LifecycleGeneration);
                }
                return attackPatternAuthority;
            }
        }

        public EnemyAttackPatternCancellationResultV1 CancelAttackPatterns(
            EnemyAttackLifecycleCancellationCommandV1 command)
        {
            EnemyAttackPatternCancellationResultV1 authority =
                AttackPatterns.CancelLifecycle(command);
            if (!authority.IsAuthorityAccepted || authority.Fact == null)
                return authority;

            EnemyAttackPatternDispatchResultV1 dispatch =
                EnemyAttackEffectEmissionDispatchV1.Cancel(
                    downstream.AttackEffects,
                    authority.Fact);
            return new EnemyAttackPatternCancellationResultV1(
                authority.Status,
                authority.Rejection,
                authority.Fact,
                dispatch);
        }

        private EnemyAttackPatternStartResultV1 StartAttackPattern(
            EnemyAttackExecutionRequestV1 execution)
        {
            if (!EnemyAttackEffectEmissionDispatchV1.CanDispatch(
                downstream.AttackEffects,
                execution))
            {
                return new EnemyAttackPatternStartResultV1(
                    EnemyAttackPatternOperationStatusV1.Rejected,
                    EnemyAttackPatternRejectionCodeV1.InvalidCommand,
                    null,
                    null);
            }
            return AttackPatterns.Start(execution);
        }

        private EnemyAttackPatternDispatchResultV1 DispatchAttackPattern(
            EnemyAttackExecutionRequestV1 execution,
            EnemyAttackPatternStartResultV1 pattern)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            if (pattern == null || !pattern.IsAccepted)
                throw new ArgumentException(
                    "Only accepted attack patterns may be dispatched.",
                    nameof(pattern));
            return EnemyAttackEffectEmissionDispatchV1.Dispatch(
                downstream.AttackEffects,
                execution,
                pattern);
        }
    }
}

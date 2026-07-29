using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.EnemyRuntimeComposition
{
    internal static class EnemyAttackEffectEmissionDispatch
    {
        public static bool CanDispatch(
            IEnemyAttackEffectPort port,
            EnemyAttackExecutionRequest execution)
        {
            if (port == null || execution == null) return false;
            return IsLegacyCompatibilityExecution(execution)
                || port is IEnemyAttackPatternEffectPort
                || IsLegacyEquivalentSingleImmediateEmission(execution);
        }

        public static bool IsLegacyCompatibilityExecution(
            EnemyAttackExecutionRequest execution)
        {
            return execution != null
                && EnemyAttackDescriptorCompatibility.IsLegacyCompatibility(
                    execution.Descriptor);
        }

        public static EnemyAttackPatternDispatchResult DispatchLegacy(
            IEnemyAttackEffectPort port,
            EnemyAttackExecutionRequest execution)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            string fingerprint = EnemyAttackPatternFingerprint.Execution(execution);
            if (!IsLegacyCompatibilityExecution(execution))
            {
                return EnemyAttackPatternDispatchResult.Rejected(
                    execution.OperationStableId,
                    fingerprint,
                    EnemyAttackPatternDispatchRejectionCode.InvalidCommand);
            }

            try
            {
                port.Emit(execution);
                return EnemyAttackPatternDispatchResult.Applied(
                    execution.OperationStableId,
                    fingerprint);
            }
            catch
            {
                return EnemyAttackPatternDispatchResult.Rejected(
                    execution.OperationStableId,
                    fingerprint,
                    EnemyAttackPatternDispatchRejectionCode.DownstreamFailure);
            }
        }

        public static EnemyAttackPatternDispatchResult Dispatch(
            IEnemyAttackEffectPort port,
            EnemyAttackExecutionRequest execution,
            EnemyAttackPatternStartResult pattern)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            if (pattern == null || !pattern.IsAccepted)
                throw new ArgumentException(
                    "Only accepted attack patterns may be dispatched.",
                    nameof(pattern));
            if (IsLegacyCompatibilityExecution(execution))
            {
                return EnemyAttackPatternDispatchResult.Rejected(
                    execution.OperationStableId,
                    EnemyAttackPatternFingerprint.Execution(execution),
                    EnemyAttackPatternDispatchRejectionCode.InvalidCommand);
            }

            var batch = new EnemyAttackSequenceDispatch(
                execution,
                pattern.Sequence,
                pattern.Emissions);
            IEnemyAttackPatternEffectPort scheduled =
                port as IEnemyAttackPatternEffectPort;
            if (scheduled != null)
            {
                try
                {
                    return ValidateResult(
                        scheduled.Dispatch(batch),
                        batch.DispatchStableId,
                        batch.Fingerprint);
                }
                catch
                {
                    return EnemyAttackPatternDispatchResult.Rejected(
                        batch.DispatchStableId,
                        batch.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCode.DownstreamFailure);
                }
            }

            if (!IsLegacyEquivalentSingleImmediateEmission(execution))
            {
                return EnemyAttackPatternDispatchResult.Rejected(
                    batch.DispatchStableId,
                    batch.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCode.UnsupportedPort);
            }

            try
            {
                port.Emit(execution);
                return EnemyAttackPatternDispatchResult.Applied(
                    batch.DispatchStableId,
                    batch.Fingerprint);
            }
            catch
            {
                return EnemyAttackPatternDispatchResult.Rejected(
                    batch.DispatchStableId,
                    batch.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCode.DownstreamFailure);
            }
        }

        public static EnemyAttackPatternDispatchResult Cancel(
            IEnemyAttackEffectPort port,
            EnemyAttackSequenceCancellationFact fact)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (fact == null) throw new ArgumentNullException(nameof(fact));

            IEnemyAttackPatternEffectPort scheduled =
                port as IEnemyAttackPatternEffectPort;
            if (scheduled != null)
            {
                try
                {
                    return ValidateResult(
                        scheduled.Cancel(fact),
                        fact.CancellationStableId,
                        fact.Fingerprint);
                }
                catch
                {
                    return EnemyAttackPatternDispatchResult.Rejected(
                        fact.CancellationStableId,
                        fact.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCode.DownstreamFailure);
                }
            }

            if (fact.CancelledProjectileStableIds.Count != 0
                || fact.CancelledMeleeStrikeStableIds.Count != 0)
            {
                return EnemyAttackPatternDispatchResult.Rejected(
                    fact.CancellationStableId,
                    fact.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCode.UnsupportedPort);
            }
            return EnemyAttackPatternDispatchResult.Applied(
                fact.CancellationStableId,
                fact.Fingerprint);
        }

        private static EnemyAttackPatternDispatchResult ValidateResult(
            EnemyAttackPatternDispatchResult result,
            StableId expectedOperationStableId,
            string expectedFingerprint)
        {
            if (result == null
                || result.OperationStableId != expectedOperationStableId
                || !string.Equals(
                    result.Fingerprint,
                    expectedFingerprint,
                    StringComparison.Ordinal))
            {
                return EnemyAttackPatternDispatchResult.Rejected(
                    expectedOperationStableId,
                    expectedFingerprint,
                    EnemyAttackPatternDispatchRejectionCode.InvalidResult);
            }
            return result;
        }

        private static bool IsLegacyEquivalentSingleImmediateEmission(
            EnemyAttackExecutionRequest execution)
        {
            if (execution == null || execution.Descriptor == null) return false;
            EnemyAttackCapabilityDescriptor descriptor = execution.Descriptor;
            EnemyShootingPattern shooting = descriptor.ShootingPattern;
            EnemyMeleePattern melee = descriptor.MeleePattern;

            if (shooting != null)
            {
                return melee == null
                    && descriptor.ProjectilePayload != null
                    && shooting.ShotsPerSequence == 1
                    && shooting.ProjectilesPerShot == 1
                    && shooting.IntervalBetweenShotsSeconds == 0d
                    && shooting.PerShotSpreadDegrees == 0d
                    && shooting.WindUpSeconds == 0d;
            }

            return melee != null
                && descriptor.ProjectilePayload == null
                && melee.StrikeCount == 1
                && melee.IntervalBetweenStrikesSeconds == 0d
                && melee.WindUpSeconds == 0d
                && melee.ActiveWindowSeconds == 0d
                && melee.HitsPerTarget == 1
                && melee.TerminalOnImpactPolicy
                    == EnemyMeleeTerminalOnImpactPolicy.ContinueSequence;
        }
    }
}

using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.EnemyRuntimeComposition
{
    internal static class EnemyAttackEffectEmissionDispatchV1
    {
        public static bool CanDispatch(
            IEnemyAttackEffectPortV1 port,
            EnemyAttackExecutionRequestV1 execution)
        {
            if (port == null || execution == null) return false;
            return IsLegacyCompatibilityExecution(execution)
                || port is IEnemyAttackPatternEffectPortV1
                || IsLegacyEquivalentSingleImmediateEmission(execution);
        }

        public static bool IsLegacyCompatibilityExecution(
            EnemyAttackExecutionRequestV1 execution)
        {
            return execution != null
                && EnemyAttackDescriptorCompatibilityV1.IsLegacyCompatibility(
                    execution.Descriptor);
        }

        public static EnemyAttackPatternDispatchResultV1 DispatchLegacy(
            IEnemyAttackEffectPortV1 port,
            EnemyAttackExecutionRequestV1 execution)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            string fingerprint = EnemyAttackPatternFingerprintV1.Execution(execution);
            if (!IsLegacyCompatibilityExecution(execution))
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    execution.OperationStableId,
                    fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            try
            {
                port.Emit(execution);
                return EnemyAttackPatternDispatchResultV1.Applied(
                    execution.OperationStableId,
                    fingerprint);
            }
            catch
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    execution.OperationStableId,
                    fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.DownstreamFailure);
            }
        }

        public static EnemyAttackPatternDispatchResultV1 Dispatch(
            IEnemyAttackEffectPortV1 port,
            EnemyAttackExecutionRequestV1 execution,
            EnemyAttackPatternStartResultV1 pattern)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            if (pattern == null || !pattern.IsAccepted)
                throw new ArgumentException(
                    "Only accepted attack patterns may be dispatched.",
                    nameof(pattern));
            if (IsLegacyCompatibilityExecution(execution))
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    execution.OperationStableId,
                    EnemyAttackPatternFingerprintV1.Execution(execution),
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            var batch = new EnemyAttackSequenceDispatchV1(
                execution,
                pattern.Sequence,
                pattern.Emissions);
            IEnemyAttackPatternEffectPortV1 scheduled =
                port as IEnemyAttackPatternEffectPortV1;
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
                    return EnemyAttackPatternDispatchResultV1.Rejected(
                        batch.DispatchStableId,
                        batch.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.DownstreamFailure);
                }
            }

            if (!IsLegacyEquivalentSingleImmediateEmission(execution))
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    batch.DispatchStableId,
                    batch.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.UnsupportedPort);
            }

            try
            {
                port.Emit(execution);
                return EnemyAttackPatternDispatchResultV1.Applied(
                    batch.DispatchStableId,
                    batch.Fingerprint);
            }
            catch
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    batch.DispatchStableId,
                    batch.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.DownstreamFailure);
            }
        }

        public static EnemyAttackPatternDispatchResultV1 Cancel(
            IEnemyAttackEffectPortV1 port,
            EnemyAttackSequenceCancellationFactV1 fact)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (fact == null) throw new ArgumentNullException(nameof(fact));

            IEnemyAttackPatternEffectPortV1 scheduled =
                port as IEnemyAttackPatternEffectPortV1;
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
                    return EnemyAttackPatternDispatchResultV1.Rejected(
                        fact.CancellationStableId,
                        fact.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.DownstreamFailure);
                }
            }

            if (fact.CancelledProjectileStableIds.Count != 0
                || fact.CancelledMeleeStrikeStableIds.Count != 0)
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    fact.CancellationStableId,
                    fact.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.UnsupportedPort);
            }
            return EnemyAttackPatternDispatchResultV1.Applied(
                fact.CancellationStableId,
                fact.Fingerprint);
        }

        private static EnemyAttackPatternDispatchResultV1 ValidateResult(
            EnemyAttackPatternDispatchResultV1 result,
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
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    expectedOperationStableId,
                    expectedFingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidResult);
            }
            return result;
        }

        private static bool IsLegacyEquivalentSingleImmediateEmission(
            EnemyAttackExecutionRequestV1 execution)
        {
            if (execution == null || execution.Descriptor == null) return false;
            EnemyAttackCapabilityDescriptorV1 descriptor = execution.Descriptor;
            EnemyShootingPatternV1 shooting = descriptor.ShootingPattern;
            EnemyMeleePatternV1 melee = descriptor.MeleePattern;

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
                    == EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence;
        }
    }
}

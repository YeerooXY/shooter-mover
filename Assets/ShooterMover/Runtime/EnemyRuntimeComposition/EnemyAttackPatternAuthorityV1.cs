using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed class EnemyAttackPatternAuthorityV1
    {
        private sealed class StartRecord
        {
            public StartRecord(string signature, EnemyAttackPatternStartResultV1 result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyAttackPatternStartResultV1 Result { get; }
        }

        private sealed class CancellationRecord
        {
            public CancellationRecord(
                string signature,
                EnemyAttackPatternCancellationResultV1 result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyAttackPatternCancellationResultV1 Result { get; }
        }

        private readonly EnemyRuntimeIdentityV1 identity;
        private readonly long lifecycleGeneration;
        private readonly Dictionary<StableId, StartRecord> starts;
        private readonly Dictionary<StableId, CancellationRecord> cancellations;
        private readonly List<EnemyAttackSequenceV1> sequences;
        private readonly HashSet<StableId> cancelledEmissionIds;
        private bool isActive;
        private EnemyAttackSequenceCancellationFactV1 terminalCancellationFact;

        public EnemyAttackPatternAuthorityV1(
            EnemyRuntimeIdentityV1 identity,
            long lifecycleGeneration)
        {
            this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            this.lifecycleGeneration = lifecycleGeneration;
            starts = new Dictionary<StableId, StartRecord>();
            cancellations = new Dictionary<StableId, CancellationRecord>();
            sequences = new List<EnemyAttackSequenceV1>();
            cancelledEmissionIds = new HashSet<StableId>();
            isActive = true;
        }

        public bool IsActive { get { return isActive; } }
        public EnemyAttackSequenceCancellationFactV1 TerminalCancellationFact
        {
            get { return terminalCancellationFact; }
        }
        public IReadOnlyList<EnemyAttackSequenceV1> Sequences
        {
            get { return new ReadOnlyCollection<EnemyAttackSequenceV1>(sequences); }
        }

        public EnemyAttackPatternStartResultV1 Start(
            EnemyAttackExecutionRequestV1 execution)
        {
            StableId operation = execution == null ? null : execution.OperationStableId;
            string signature = EnemyAttackPatternFingerprintV1.Execution(execution);
            StartRecord replay;
            if (operation != null && starts.TryGetValue(operation, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                    return RejectedStart(
                        EnemyAttackPatternRejectionCodeV1.ConflictingDuplicate);
                return new EnemyAttackPatternStartResultV1(
                    EnemyAttackPatternOperationStatusV1.ExactReplay,
                    replay.Result.Rejection,
                    replay.Result.Sequence,
                    replay.Result.Emissions);
            }

            EnemyAttackPatternStartResultV1 result;
            if (!IsStructurallyValidExecution(execution))
            {
                result = RejectedStart(
                    EnemyAttackPatternRejectionCodeV1.InvalidCommand);
            }
            else if (!EnemyRuntimeAuthorityFingerprintV1.IdentityEquals(
                execution.Identity,
                identity))
            {
                result = RejectedStart(
                    EnemyAttackPatternRejectionCodeV1.EntityMismatch);
            }
            else if (execution.LifecycleGeneration != lifecycleGeneration)
            {
                result = RejectedStart(
                    EnemyAttackPatternRejectionCodeV1.StaleLifecycle);
            }
            else if (!isActive)
            {
                result = RejectedStart(
                    EnemyAttackPatternRejectionCodeV1.ActorTerminal);
            }
            else if (!HasValidPattern(execution.Descriptor))
            {
                result = RejectedStart(
                    EnemyAttackPatternRejectionCodeV1.InvalidPattern);
            }
            else
            {
                EnemyAttackSequenceV1 sequence =
                    EnemyAttackPatternSchedulerV1.Schedule(execution);
                IReadOnlyList<EnemyAttackEffectEmissionV1> emissions =
                    EnemyAttackEffectEmissionProjectorV1.Project(
                        execution,
                        sequence);
                sequences.Add(sequence);
                result = new EnemyAttackPatternStartResultV1(
                    EnemyAttackPatternOperationStatusV1.Applied,
                    EnemyAttackPatternRejectionCodeV1.None,
                    sequence,
                    emissions);
            }

            if (operation != null)
                starts.Add(operation, new StartRecord(signature, result));
            return result;
        }

        public EnemyAttackPatternCancellationResultV1 CancelLifecycle(
            EnemyAttackLifecycleCancellationCommandV1 command)
        {
            StableId operation = command == null ? null : command.OperationStableId;
            string signature =
                EnemyAttackPatternFingerprintV1.CancellationCommand(command);
            CancellationRecord replay;
            if (operation != null
                && cancellations.TryGetValue(operation, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                {
                    return new EnemyAttackPatternCancellationResultV1(
                        EnemyAttackPatternOperationStatusV1.Rejected,
                        EnemyAttackPatternRejectionCodeV1.ConflictingDuplicate,
                        replay.Result.Fact);
                }
                return new EnemyAttackPatternCancellationResultV1(
                    EnemyAttackPatternOperationStatusV1.ExactReplay,
                    replay.Result.Rejection,
                    replay.Result.Fact);
            }

            EnemyAttackPatternCancellationResultV1 result;
            if (!IsValidCancellation(command))
            {
                result = RejectedCancellation(
                    EnemyAttackPatternRejectionCodeV1.InvalidCommand,
                    null);
            }
            else if (command.SourceEntityStableId != identity.EntityInstanceId)
            {
                result = RejectedCancellation(
                    EnemyAttackPatternRejectionCodeV1.EntityMismatch,
                    null);
            }
            else if (command.SourceLifecycleGeneration != lifecycleGeneration)
            {
                result = RejectedCancellation(
                    EnemyAttackPatternRejectionCodeV1.StaleLifecycle,
                    null);
            }
            else if (!isActive)
            {
                // The first accepted lifecycle cancellation is terminal authority state.
                // Later operation identities cannot recalculate pending work at another time.
                result = RejectedCancellation(
                    EnemyAttackPatternRejectionCodeV1.ActorTerminal,
                    terminalCancellationFact);
            }
            else
            {
                var shotIds = new List<StableId>();
                var projectileIds = new List<StableId>();
                var strikeIds = new List<StableId>();
                for (int sequenceIndex = 0;
                    sequenceIndex < sequences.Count;
                    sequenceIndex++)
                {
                    EnemyAttackSequenceV1 sequence = sequences[sequenceIndex];
                    if (sequence.Descriptor.InterruptionPolicy
                        != EnemyAttackInterruptionPolicyV1
                            .CancelPendingOnLifecycleEnd)
                    {
                        continue;
                    }
                    for (int index = 0; index < sequence.Shots.Count; index++)
                    {
                        EnemyAttackScheduledShotV1 shot = sequence.Shots[index];
                        if (shot.ScheduledAtSeconds > command.OccurredAtSeconds)
                        {
                            shotIds.Add(shot.ShotStableId);
                            cancelledEmissionIds.Add(shot.ShotStableId);
                        }
                    }
                    for (int index = 0;
                        index < sequence.Projectiles.Count;
                        index++)
                    {
                        EnemyAttackScheduledProjectileV1 projectile =
                            sequence.Projectiles[index];
                        if (projectile.ScheduledAtSeconds
                            > command.OccurredAtSeconds)
                        {
                            projectileIds.Add(projectile.ProjectileStableId);
                            cancelledEmissionIds.Add(
                                projectile.ProjectileStableId);
                        }
                    }
                    for (int index = 0;
                        index < sequence.MeleeStrikes.Count;
                        index++)
                    {
                        EnemyAttackScheduledMeleeStrikeV1 strike =
                            sequence.MeleeStrikes[index];
                        if (strike.ActiveUntilSeconds
                            > command.OccurredAtSeconds)
                        {
                            strikeIds.Add(strike.StrikeStableId);
                            cancelledEmissionIds.Add(strike.StrikeStableId);
                        }
                    }
                }

                isActive = false;
                terminalCancellationFact =
                    new EnemyAttackSequenceCancellationFactV1(
                        StableId.Create(
                            "enemy-attack-cancellation",
                            "runtime-"
                            + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                                identity.EntityInstanceId
                                + "|"
                                + lifecycleGeneration.ToString(
                                    CultureInfo.InvariantCulture)
                                + "|"
                                + operation)),
                        identity.EntityInstanceId,
                        lifecycleGeneration,
                        command.OccurredAtSeconds,
                        shotIds,
                        projectileIds,
                        strikeIds);
                result = new EnemyAttackPatternCancellationResultV1(
                    EnemyAttackPatternOperationStatusV1.Applied,
                    EnemyAttackPatternRejectionCodeV1.None,
                    terminalCancellationFact);
            }

            if (operation != null)
                cancellations.Add(
                    operation,
                    new CancellationRecord(signature, result));
            return result;
        }

        public bool IsEmissionCancelled(StableId emissionStableId)
        {
            return emissionStableId != null
                && cancelledEmissionIds.Contains(emissionStableId);
        }

        private static bool IsStructurallyValidExecution(
            EnemyAttackExecutionRequestV1 execution)
        {
            return execution != null
                && execution.OperationStableId != null
                && execution.Identity != null
                && execution.Descriptor != null
                && execution.Descriptor.AttackId != null
                && execution.CommittedIntent != null;
        }

        private static bool IsValidCancellation(
            EnemyAttackLifecycleCancellationCommandV1 command)
        {
            return command != null
                && command.OperationStableId != null
                && command.SourceEntityStableId != null
                && command.SourceLifecycleGeneration > 0L
                && IsFiniteNonNegative(command.OccurredAtSeconds);
        }

        private static bool HasValidPattern(
            EnemyAttackCapabilityDescriptorV1 descriptor)
        {
            if (descriptor == null) return false;
            bool shooting = descriptor.ShootingPattern != null;
            bool melee = descriptor.MeleePattern != null;
            if (shooting == melee) return false;

            if (shooting)
            {
                EnemyShootingPatternV1 pattern = descriptor.ShootingPattern;
                return descriptor.ProjectilePayload != null
                    && pattern.ShotsPerSequence >= 1
                    && pattern.ProjectilesPerShot >= 1
                    && IsFiniteNonNegative(
                        pattern.IntervalBetweenShotsSeconds)
                    && IsFiniteNonNegative(pattern.PerShotSpreadDegrees)
                    && IsFiniteNonNegative(pattern.WindUpSeconds)
                    && IsFiniteNonNegative(
                        pattern.PostSequenceRecoverySeconds)
                    && pattern.SequenceAimPolicy
                        == EnemySequenceAimPolicyV1.LockAtSequenceStart
                    && Enum.IsDefined(
                        typeof(EnemyAttackInterruptionPolicyV1),
                        pattern.InterruptionPolicy);
            }

            EnemyMeleePatternV1 meleePattern = descriptor.MeleePattern;
            return descriptor.ProjectilePayload == null
                && meleePattern.StrikeCount >= 1
                && meleePattern.HitsPerTarget >= 1
                && IsFiniteNonNegative(meleePattern.WindUpSeconds)
                && IsFiniteNonNegative(meleePattern.ActiveWindowSeconds)
                && IsFiniteNonNegative(
                    meleePattern.IntervalBetweenStrikesSeconds)
                && IsFinitePositive(meleePattern.ContactRadius)
                && IsFiniteNonNegative(meleePattern.LungeDistance)
                && IsFiniteNonNegative(meleePattern.RecoverySeconds)
                && meleePattern.AimCommitPolicy
                    == EnemyMeleeAimCommitPolicyV1.LockAtWindUp
                && meleePattern.TerminalOnImpactPolicy
                    == EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence
                && Enum.IsDefined(
                    typeof(EnemyAttackInterruptionPolicyV1),
                    meleePattern.InterruptionPolicy);
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= 0d;
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value > 0d;
        }

        private static EnemyAttackPatternStartResultV1 RejectedStart(
            EnemyAttackPatternRejectionCodeV1 rejection)
        {
            return new EnemyAttackPatternStartResultV1(
                EnemyAttackPatternOperationStatusV1.Rejected,
                rejection,
                null,
                null);
        }

        private static EnemyAttackPatternCancellationResultV1
            RejectedCancellation(
                EnemyAttackPatternRejectionCodeV1 rejection,
                EnemyAttackSequenceCancellationFactV1 fact)
        {
            return new EnemyAttackPatternCancellationResultV1(
                EnemyAttackPatternOperationStatusV1.Rejected,
                rejection,
                fact);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed class EnemyAttackPatternState
    {
        private sealed class StartRecord
        {
            public StartRecord(string signature, EnemyAttackPatternStartResult result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyAttackPatternStartResult Result { get; }
        }

        private sealed class CancellationRecord
        {
            public CancellationRecord(
                string signature,
                EnemyAttackPatternCancellationResult result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyAttackPatternCancellationResult Result { get; }
        }

        private readonly EnemyLiveIdentity identity;
        private readonly long lifecycleGeneration;
        private readonly Dictionary<StableId, StartRecord> starts;
        private readonly Dictionary<StableId, CancellationRecord> cancellations;
        private readonly List<EnemyAttackSequence> sequences;
        private readonly HashSet<StableId> cancelledEmissionIds;
        private bool isActive;
        private EnemyAttackSequenceCancellationFact terminalCancellationFact;

        public EnemyAttackPatternState(
            EnemyLiveIdentity identity,
            long lifecycleGeneration)
        {
            this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            this.lifecycleGeneration = lifecycleGeneration;
            starts = new Dictionary<StableId, StartRecord>();
            cancellations = new Dictionary<StableId, CancellationRecord>();
            sequences = new List<EnemyAttackSequence>();
            cancelledEmissionIds = new HashSet<StableId>();
            isActive = true;
        }

        public bool IsActive { get { return isActive; } }
        public EnemyAttackSequenceCancellationFact TerminalCancellationFact
        {
            get { return terminalCancellationFact; }
        }
        public IReadOnlyList<EnemyAttackSequence> Sequences
        {
            get { return new ReadOnlyCollection<EnemyAttackSequence>(sequences); }
        }

        public EnemyAttackPatternStartResult Start(
            EnemyAttackExecutionRequest execution)
        {
            StableId operation = execution == null ? null : execution.OperationStableId;
            string signature = EnemyAttackPatternFingerprint.Execution(execution);
            StartRecord replay;
            if (operation != null && starts.TryGetValue(operation, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                    return RejectedStart(
                        EnemyAttackPatternRejectionCode.ConflictingDuplicate);
                return new EnemyAttackPatternStartResult(
                    EnemyAttackPatternOperationStatus.ExactReplay,
                    replay.Result.Rejection,
                    replay.Result.Sequence,
                    replay.Result.Emissions);
            }

            EnemyAttackPatternStartResult result;
            if (!IsStructurallyValidExecution(execution))
            {
                result = RejectedStart(
                    EnemyAttackPatternRejectionCode.InvalidCommand);
            }
            else if (!EnemyLiveStateFingerprint.IdentityEquals(
                execution.Identity,
                identity))
            {
                result = RejectedStart(
                    EnemyAttackPatternRejectionCode.EntityMismatch);
            }
            else if (execution.LifecycleGeneration != lifecycleGeneration)
            {
                result = RejectedStart(
                    EnemyAttackPatternRejectionCode.StaleLifecycle);
            }
            else if (!isActive)
            {
                result = RejectedStart(
                    EnemyAttackPatternRejectionCode.ActorTerminal);
            }
            else if (!HasValidPattern(execution.Descriptor))
            {
                result = RejectedStart(
                    EnemyAttackPatternRejectionCode.InvalidPattern);
            }
            else
            {
                EnemyAttackSequence sequence =
                    EnemyAttackPatternScheduler.Schedule(execution);
                IReadOnlyList<EnemyAttackEffectEmission> emissions =
                    EnemyAttackEffectEmissionProjector.Project(
                        execution,
                        sequence);
                sequences.Add(sequence);
                result = new EnemyAttackPatternStartResult(
                    EnemyAttackPatternOperationStatus.Applied,
                    EnemyAttackPatternRejectionCode.None,
                    sequence,
                    emissions);
            }

            if (operation != null)
                starts.Add(operation, new StartRecord(signature, result));
            return result;
        }

        public EnemyAttackPatternCancellationResult CancelLifecycle(
            EnemyAttackLifecycleCancellationCommand command)
        {
            StableId operation = command == null ? null : command.OperationStableId;
            string signature =
                EnemyAttackPatternFingerprint.CancellationCommand(command);
            CancellationRecord replay;
            if (operation != null
                && cancellations.TryGetValue(operation, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                {
                    return new EnemyAttackPatternCancellationResult(
                        EnemyAttackPatternOperationStatus.Rejected,
                        EnemyAttackPatternRejectionCode.ConflictingDuplicate,
                        replay.Result.Fact);
                }
                return new EnemyAttackPatternCancellationResult(
                    EnemyAttackPatternOperationStatus.ExactReplay,
                    replay.Result.Rejection,
                    replay.Result.Fact);
            }

            EnemyAttackPatternCancellationResult result;
            if (!IsValidCancellation(command))
            {
                result = RejectedCancellation(
                    EnemyAttackPatternRejectionCode.InvalidCommand,
                    null);
            }
            else if (command.SourceEntityStableId != identity.EntityInstanceId)
            {
                result = RejectedCancellation(
                    EnemyAttackPatternRejectionCode.EntityMismatch,
                    null);
            }
            else if (command.SourceLifecycleGeneration != lifecycleGeneration)
            {
                result = RejectedCancellation(
                    EnemyAttackPatternRejectionCode.StaleLifecycle,
                    null);
            }
            else if (!isActive)
            {
                // The first accepted lifecycle cancellation is terminal authority state.
                // Later operation identities cannot recalculate pending work at another time.
                result = RejectedCancellation(
                    EnemyAttackPatternRejectionCode.ActorTerminal,
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
                    EnemyAttackSequence sequence = sequences[sequenceIndex];
                    if (sequence.Descriptor.InterruptionPolicy
                        != EnemyAttackInterruptionPolicy
                            .CancelPendingOnLifecycleEnd)
                    {
                        continue;
                    }
                    for (int index = 0; index < sequence.Shots.Count; index++)
                    {
                        EnemyAttackScheduledShot shot = sequence.Shots[index];
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
                        EnemyAttackScheduledProjectile projectile =
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
                        EnemyAttackScheduledMeleeStrike strike =
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
                    new EnemyAttackSequenceCancellationFact(
                        StableId.Create(
                            "enemy-attack-cancellation",
                            "runtime-"
                            + DeterministicEnemyLiveIdentityDeriver.Hash64(
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
                result = new EnemyAttackPatternCancellationResult(
                    EnemyAttackPatternOperationStatus.Applied,
                    EnemyAttackPatternRejectionCode.None,
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
            EnemyAttackExecutionRequest execution)
        {
            return execution != null
                && execution.OperationStableId != null
                && execution.Identity != null
                && execution.Descriptor != null
                && execution.Descriptor.AttackId != null
                && execution.CommittedIntent != null;
        }

        private static bool IsValidCancellation(
            EnemyAttackLifecycleCancellationCommand command)
        {
            return command != null
                && command.OperationStableId != null
                && command.SourceEntityStableId != null
                && command.SourceLifecycleGeneration > 0L
                && IsFiniteNonNegative(command.OccurredAtSeconds);
        }

        private static bool HasValidPattern(
            EnemyAttackCapabilityDescriptor descriptor)
        {
            if (descriptor == null) return false;
            bool shooting = descriptor.ShootingPattern != null;
            bool melee = descriptor.MeleePattern != null;
            if (shooting == melee) return false;

            if (shooting)
            {
                EnemyShootingPattern pattern = descriptor.ShootingPattern;
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
                        == EnemySequenceAimPolicy.LockAtSequenceStart
                    && Enum.IsDefined(
                        typeof(EnemyAttackInterruptionPolicy),
                        pattern.InterruptionPolicy);
            }

            EnemyMeleePattern meleePattern = descriptor.MeleePattern;
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
                    == EnemyMeleeAimCommitPolicy.LockAtWindUp
                && meleePattern.TerminalOnImpactPolicy
                    == EnemyMeleeTerminalOnImpactPolicy.ContinueSequence
                && Enum.IsDefined(
                    typeof(EnemyAttackInterruptionPolicy),
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

        private static EnemyAttackPatternStartResult RejectedStart(
            EnemyAttackPatternRejectionCode rejection)
        {
            return new EnemyAttackPatternStartResult(
                EnemyAttackPatternOperationStatus.Rejected,
                rejection,
                null,
                null);
        }

        private static EnemyAttackPatternCancellationResult
            RejectedCancellation(
                EnemyAttackPatternRejectionCode rejection,
                EnemyAttackSequenceCancellationFact fact)
        {
            return new EnemyAttackPatternCancellationResult(
                EnemyAttackPatternOperationStatus.Rejected,
                rejection,
                fact);
        }
    }
}

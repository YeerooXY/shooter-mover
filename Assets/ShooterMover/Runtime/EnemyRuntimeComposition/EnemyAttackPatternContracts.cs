using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public enum EnemyAttackPatternOperationStatus
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
    }

    public enum EnemyAttackPatternRejectionCode
    {
        None = 0,
        InvalidCommand = 1,
        EntityMismatch = 2,
        StaleLifecycle = 3,
        ActorTerminal = 4,
        ConflictingDuplicate = 5,
        InvalidPattern = 6,
    }

    public sealed class EnemyAttackSequenceIdentity
    {
        public EnemyAttackSequenceIdentity(
            StableId sequenceStableId,
            StableId operationStableId,
            StableId sourceEntityStableId,
            StableId sourceRunParticipantStableId,
            long sourceLifecycleGeneration,
            StableId attackStableId)
        {
            SequenceStableId = sequenceStableId
                ?? throw new ArgumentNullException(nameof(sequenceStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            if (sourceLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceLifecycleGeneration));
            AttackStableId = attackStableId
                ?? throw new ArgumentNullException(nameof(attackStableId));
            SourceRunParticipantStableId = sourceRunParticipantStableId;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
        }

        public StableId SequenceStableId { get; }
        public StableId OperationStableId { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourceRunParticipantStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public StableId AttackStableId { get; }
    }

    public sealed class EnemyAttackScheduledShot
    {
        public EnemyAttackScheduledShot(
            StableId shotStableId,
            StableId sequenceStableId,
            int shotOrdinal,
            double scheduledAtSeconds,
            EnemySequenceAimPolicy aimPolicy)
        {
            ShotStableId = shotStableId ?? throw new ArgumentNullException(nameof(shotStableId));
            SequenceStableId = sequenceStableId
                ?? throw new ArgumentNullException(nameof(sequenceStableId));
            if (shotOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(shotOrdinal));
            RequireFiniteNonNegative(scheduledAtSeconds, nameof(scheduledAtSeconds));
            if (!Enum.IsDefined(typeof(EnemySequenceAimPolicy), aimPolicy))
                throw new ArgumentOutOfRangeException(nameof(aimPolicy));
            ShotOrdinal = shotOrdinal;
            ScheduledAtSeconds = scheduledAtSeconds;
            AimPolicy = aimPolicy;
        }

        public StableId ShotStableId { get; }
        public StableId SequenceStableId { get; }
        public int ShotOrdinal { get; }
        public double ScheduledAtSeconds { get; }
        public EnemySequenceAimPolicy AimPolicy { get; }

        private static void RequireFiniteNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class EnemyAttackScheduledProjectile
    {
        public EnemyAttackScheduledProjectile(
            StableId projectileStableId,
            StableId shotStableId,
            int projectileOrdinal,
            double scheduledAtSeconds,
            double spreadOffsetDegrees,
            EnemyProjectilePayload payload)
        {
            ProjectileStableId = projectileStableId
                ?? throw new ArgumentNullException(nameof(projectileStableId));
            ShotStableId = shotStableId ?? throw new ArgumentNullException(nameof(shotStableId));
            if (projectileOrdinal < 0)
                throw new ArgumentOutOfRangeException(nameof(projectileOrdinal));
            RequireFiniteNonNegative(scheduledAtSeconds, nameof(scheduledAtSeconds));
            if (double.IsNaN(spreadOffsetDegrees) || double.IsInfinity(spreadOffsetDegrees))
                throw new ArgumentOutOfRangeException(nameof(spreadOffsetDegrees));
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            ProjectileOrdinal = projectileOrdinal;
            ScheduledAtSeconds = scheduledAtSeconds;
            SpreadOffsetDegrees = spreadOffsetDegrees;
        }

        public StableId ProjectileStableId { get; }
        public StableId ShotStableId { get; }
        public int ProjectileOrdinal { get; }
        public double ScheduledAtSeconds { get; }
        public double SpreadOffsetDegrees { get; }
        public EnemyProjectilePayload Payload { get; }

        private static void RequireFiniteNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class EnemyAttackScheduledMeleeStrike
    {
        public EnemyAttackScheduledMeleeStrike(
            StableId strikeStableId,
            StableId sequenceStableId,
            int strikeOrdinal,
            double activeFromSeconds,
            double activeUntilSeconds,
            EnemyMeleePattern pattern)
        {
            StrikeStableId = strikeStableId
                ?? throw new ArgumentNullException(nameof(strikeStableId));
            SequenceStableId = sequenceStableId
                ?? throw new ArgumentNullException(nameof(sequenceStableId));
            if (strikeOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(strikeOrdinal));
            RequireFiniteNonNegative(activeFromSeconds, nameof(activeFromSeconds));
            RequireFiniteNonNegative(activeUntilSeconds, nameof(activeUntilSeconds));
            if (activeUntilSeconds < activeFromSeconds)
                throw new ArgumentOutOfRangeException(nameof(activeUntilSeconds));
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            StrikeOrdinal = strikeOrdinal;
            ActiveFromSeconds = activeFromSeconds;
            ActiveUntilSeconds = activeUntilSeconds;
        }

        public StableId StrikeStableId { get; }
        public StableId SequenceStableId { get; }
        public int StrikeOrdinal { get; }
        public double ActiveFromSeconds { get; }
        public double ActiveUntilSeconds { get; }
        public EnemyMeleePattern Pattern { get; }

        private static void RequireFiniteNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class EnemyAttackSequence
    {
        private readonly ReadOnlyCollection<EnemyAttackScheduledShot> shots;
        private readonly ReadOnlyCollection<EnemyAttackScheduledProjectile> projectiles;
        private readonly ReadOnlyCollection<EnemyAttackScheduledMeleeStrike> meleeStrikes;

        public EnemyAttackSequence(
            EnemyAttackSequenceIdentity identity,
            EnemyAttackCapabilityDescriptor descriptor,
            EnemyAttackIntent committedIntent,
            double startedAtSeconds,
            double recoveryEndsAtSeconds,
            IEnumerable<EnemyAttackScheduledShot> shots,
            IEnumerable<EnemyAttackScheduledProjectile> projectiles,
            IEnumerable<EnemyAttackScheduledMeleeStrike> meleeStrikes)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            CommittedIntent = committedIntent ?? throw new ArgumentNullException(nameof(committedIntent));
            RequireFiniteNonNegative(startedAtSeconds, nameof(startedAtSeconds));
            RequireFiniteNonNegative(recoveryEndsAtSeconds, nameof(recoveryEndsAtSeconds));
            if (recoveryEndsAtSeconds < startedAtSeconds)
                throw new ArgumentOutOfRangeException(nameof(recoveryEndsAtSeconds));
            StartedAtSeconds = startedAtSeconds;
            RecoveryEndsAtSeconds = recoveryEndsAtSeconds;
            this.shots = Copy(shots, nameof(shots));
            this.projectiles = Copy(projectiles, nameof(projectiles));
            this.meleeStrikes = Copy(meleeStrikes, nameof(meleeStrikes));
            Fingerprint = EnemyAttackPatternFingerprint.Sequence(this);
        }

        public EnemyAttackSequenceIdentity Identity { get; }
        public EnemyAttackCapabilityDescriptor Descriptor { get; }
        public EnemyAttackIntent CommittedIntent { get; }
        public double StartedAtSeconds { get; }
        public double RecoveryEndsAtSeconds { get; }
        public IReadOnlyList<EnemyAttackScheduledShot> Shots { get { return shots; } }
        public IReadOnlyList<EnemyAttackScheduledProjectile> Projectiles { get { return projectiles; } }
        public IReadOnlyList<EnemyAttackScheduledMeleeStrike> MeleeStrikes { get { return meleeStrikes; } }
        public string Fingerprint { get; }

        private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> source, string name)
            where T : class
        {
            if (source == null) throw new ArgumentNullException(name);
            var values = new List<T>();
            foreach (T value in source)
            {
                if (value == null)
                    throw new ArgumentException("Sequence collections cannot contain null.", name);
                values.Add(value);
            }
            return new ReadOnlyCollection<T>(values);
        }

        private static void RequireFiniteNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}

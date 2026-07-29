using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed class EnemyAttackPatternStartResult
    {
        private readonly ReadOnlyCollection<EnemyAttackEffectEmission> emissions;

        public EnemyAttackPatternStartResult(
            EnemyAttackPatternOperationStatus status,
            EnemyAttackPatternRejectionCode rejection,
            EnemyAttackSequence sequence)
            : this(status, rejection, sequence, null)
        {
        }

        public EnemyAttackPatternStartResult(
            EnemyAttackPatternOperationStatus status,
            EnemyAttackPatternRejectionCode rejection,
            EnemyAttackSequence sequence,
            IEnumerable<EnemyAttackEffectEmission> emissions)
        {
            Status = status;
            Rejection = rejection;
            Sequence = sequence;
            this.emissions = CopyEmissions(emissions);
        }

        public EnemyAttackPatternOperationStatus Status { get; }
        public EnemyAttackPatternRejectionCode Rejection { get; }
        public EnemyAttackSequence Sequence { get; }
        public IReadOnlyList<EnemyAttackEffectEmission> Emissions
        {
            get { return emissions; }
        }
        public bool IsAccepted
        {
            get
            {
                return Sequence != null
                    && Rejection == EnemyAttackPatternRejectionCode.None
                    && (Status == EnemyAttackPatternOperationStatus.Applied
                        || Status == EnemyAttackPatternOperationStatus.ExactReplay);
            }
        }

        private static ReadOnlyCollection<EnemyAttackEffectEmission> CopyEmissions(
            IEnumerable<EnemyAttackEffectEmission> source)
        {
            var values = new List<EnemyAttackEffectEmission>();
            if (source != null)
            {
                foreach (EnemyAttackEffectEmission value in source)
                {
                    if (value == null)
                        throw new ArgumentException(
                            "Pattern start emissions cannot contain null.",
                            nameof(source));
                    values.Add(value);
                }
            }
            return new ReadOnlyCollection<EnemyAttackEffectEmission>(values);
        }
    }

    public sealed class EnemyAttackLifecycleCancellationCommand
    {
        public EnemyAttackLifecycleCancellationCommand(
            StableId operationStableId,
            StableId sourceEntityStableId,
            long sourceLifecycleGeneration,
            double occurredAtSeconds)
        {
            OperationStableId = operationStableId;
            SourceEntityStableId = sourceEntityStableId;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            OccurredAtSeconds = occurredAtSeconds;
        }

        public StableId OperationStableId { get; }
        public StableId SourceEntityStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public double OccurredAtSeconds { get; }
    }

    public sealed class EnemyAttackSequenceCancellationFact
    {
        private readonly ReadOnlyCollection<StableId> cancelledShotStableIds;
        private readonly ReadOnlyCollection<StableId> cancelledProjectileStableIds;
        private readonly ReadOnlyCollection<StableId> cancelledMeleeStrikeStableIds;

        public EnemyAttackSequenceCancellationFact(
            StableId cancellationStableId,
            StableId sourceEntityStableId,
            long sourceLifecycleGeneration,
            double occurredAtSeconds,
            IEnumerable<StableId> cancelledShotStableIds,
            IEnumerable<StableId> cancelledProjectileStableIds,
            IEnumerable<StableId> cancelledMeleeStrikeStableIds)
        {
            CancellationStableId = cancellationStableId
                ?? throw new ArgumentNullException(nameof(cancellationStableId));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            if (sourceLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceLifecycleGeneration));
            if (double.IsNaN(occurredAtSeconds)
                || double.IsInfinity(occurredAtSeconds)
                || occurredAtSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(occurredAtSeconds));
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            OccurredAtSeconds = occurredAtSeconds;
            this.cancelledShotStableIds = CopyIds(
                cancelledShotStableIds,
                nameof(cancelledShotStableIds));
            this.cancelledProjectileStableIds = CopyIds(
                cancelledProjectileStableIds,
                nameof(cancelledProjectileStableIds));
            this.cancelledMeleeStrikeStableIds = CopyIds(
                cancelledMeleeStrikeStableIds,
                nameof(cancelledMeleeStrikeStableIds));
            Fingerprint = EnemyAttackPatternFingerprint.Cancellation(this);
        }

        public StableId CancellationStableId { get; }
        public StableId SourceEntityStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public double OccurredAtSeconds { get; }
        public IReadOnlyList<StableId> CancelledShotStableIds
        {
            get { return cancelledShotStableIds; }
        }
        public IReadOnlyList<StableId> CancelledProjectileStableIds
        {
            get { return cancelledProjectileStableIds; }
        }
        public IReadOnlyList<StableId> CancelledMeleeStrikeStableIds
        {
            get { return cancelledMeleeStrikeStableIds; }
        }
        public string Fingerprint { get; }

        private static ReadOnlyCollection<StableId> CopyIds(
            IEnumerable<StableId> source,
            string name)
        {
            if (source == null) throw new ArgumentNullException(name);
            var result = new List<StableId>();
            foreach (StableId value in source)
            {
                if (value == null)
                    throw new ArgumentException(
                        "Cancellation identities cannot contain null.",
                        name);
                result.Add(value);
            }
            result.Sort();
            return new ReadOnlyCollection<StableId>(result);
        }
    }

    public sealed class EnemyAttackPatternCancellationResult
    {
        public EnemyAttackPatternCancellationResult(
            EnemyAttackPatternOperationStatus status,
            EnemyAttackPatternRejectionCode rejection,
            EnemyAttackSequenceCancellationFact fact)
            : this(status, rejection, fact, null)
        {
        }

        public EnemyAttackPatternCancellationResult(
            EnemyAttackPatternOperationStatus status,
            EnemyAttackPatternRejectionCode rejection,
            EnemyAttackSequenceCancellationFact fact,
            EnemyAttackPatternDispatchResult dispatch)
        {
            Status = status;
            Rejection = rejection;
            Fact = fact;
            Dispatch = dispatch;
        }

        public EnemyAttackPatternOperationStatus Status { get; }
        public EnemyAttackPatternRejectionCode Rejection { get; }
        public EnemyAttackSequenceCancellationFact Fact { get; }
        public EnemyAttackPatternDispatchResult Dispatch { get; }
        public bool IsAuthorityAccepted
        {
            get
            {
                return Fact != null
                    && Rejection == EnemyAttackPatternRejectionCode.None
                    && (Status == EnemyAttackPatternOperationStatus.Applied
                        || Status == EnemyAttackPatternOperationStatus.ExactReplay);
            }
        }
        public bool IsAccepted
        {
            get
            {
                return IsAuthorityAccepted
                    && (Dispatch == null || Dispatch.IsAccepted);
            }
        }
    }
}

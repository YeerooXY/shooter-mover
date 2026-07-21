using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed class EnemyAttackPatternStartResultV1
    {
        private readonly ReadOnlyCollection<EnemyAttackEffectEmissionV1> emissions;

        public EnemyAttackPatternStartResultV1(
            EnemyAttackPatternOperationStatusV1 status,
            EnemyAttackPatternRejectionCodeV1 rejection,
            EnemyAttackSequenceV1 sequence)
            : this(status, rejection, sequence, null)
        {
        }

        public EnemyAttackPatternStartResultV1(
            EnemyAttackPatternOperationStatusV1 status,
            EnemyAttackPatternRejectionCodeV1 rejection,
            EnemyAttackSequenceV1 sequence,
            IEnumerable<EnemyAttackEffectEmissionV1> emissions)
        {
            Status = status;
            Rejection = rejection;
            Sequence = sequence;
            this.emissions = CopyEmissions(emissions);
        }

        public EnemyAttackPatternOperationStatusV1 Status { get; }
        public EnemyAttackPatternRejectionCodeV1 Rejection { get; }
        public EnemyAttackSequenceV1 Sequence { get; }
        public IReadOnlyList<EnemyAttackEffectEmissionV1> Emissions
        {
            get { return emissions; }
        }
        public bool IsAccepted
        {
            get
            {
                return Sequence != null
                    && Rejection == EnemyAttackPatternRejectionCodeV1.None
                    && (Status == EnemyAttackPatternOperationStatusV1.Applied
                        || Status == EnemyAttackPatternOperationStatusV1.ExactReplay);
            }
        }

        private static ReadOnlyCollection<EnemyAttackEffectEmissionV1> CopyEmissions(
            IEnumerable<EnemyAttackEffectEmissionV1> source)
        {
            var values = new List<EnemyAttackEffectEmissionV1>();
            if (source != null)
            {
                foreach (EnemyAttackEffectEmissionV1 value in source)
                {
                    if (value == null)
                        throw new ArgumentException(
                            "Pattern start emissions cannot contain null.",
                            nameof(source));
                    values.Add(value);
                }
            }
            return new ReadOnlyCollection<EnemyAttackEffectEmissionV1>(values);
        }
    }

    public sealed class EnemyAttackLifecycleCancellationCommandV1
    {
        public EnemyAttackLifecycleCancellationCommandV1(
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

    public sealed class EnemyAttackSequenceCancellationFactV1
    {
        private readonly ReadOnlyCollection<StableId> cancelledShotStableIds;
        private readonly ReadOnlyCollection<StableId> cancelledProjectileStableIds;
        private readonly ReadOnlyCollection<StableId> cancelledMeleeStrikeStableIds;

        public EnemyAttackSequenceCancellationFactV1(
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
            Fingerprint = EnemyAttackPatternFingerprintV1.Cancellation(this);
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

    public sealed class EnemyAttackPatternCancellationResultV1
    {
        public EnemyAttackPatternCancellationResultV1(
            EnemyAttackPatternOperationStatusV1 status,
            EnemyAttackPatternRejectionCodeV1 rejection,
            EnemyAttackSequenceCancellationFactV1 fact)
            : this(status, rejection, fact, null)
        {
        }

        public EnemyAttackPatternCancellationResultV1(
            EnemyAttackPatternOperationStatusV1 status,
            EnemyAttackPatternRejectionCodeV1 rejection,
            EnemyAttackSequenceCancellationFactV1 fact,
            EnemyAttackPatternDispatchResultV1 dispatch)
        {
            Status = status;
            Rejection = rejection;
            Fact = fact;
            Dispatch = dispatch;
        }

        public EnemyAttackPatternOperationStatusV1 Status { get; }
        public EnemyAttackPatternRejectionCodeV1 Rejection { get; }
        public EnemyAttackSequenceCancellationFactV1 Fact { get; }
        public EnemyAttackPatternDispatchResultV1 Dispatch { get; }
        public bool IsAuthorityAccepted
        {
            get
            {
                return Fact != null
                    && Rejection == EnemyAttackPatternRejectionCodeV1.None
                    && (Status == EnemyAttackPatternOperationStatusV1.Applied
                        || Status == EnemyAttackPatternOperationStatusV1.ExactReplay);
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

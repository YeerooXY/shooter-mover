using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.EnemyRuntimeComposition
{
    public enum EnemyAttackPatternDispatchRejectionCode
    {
        None = 0,
        InvalidCommand = 1,
        ConflictingDuplicate = 2,
        UnsupportedPort = 3,
        DownstreamFailure = 4,
        InvalidResult = 5,
    }

    public sealed class EnemyAttackPatternDispatchResult
    {
        public EnemyAttackPatternDispatchResult(
            EnemyAttackPatternOperationStatus status,
            EnemyAttackPatternDispatchRejectionCode rejection,
            StableId operationStableId,
            string fingerprint)
        {
            if (!Enum.IsDefined(typeof(EnemyAttackPatternOperationStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (!Enum.IsDefined(typeof(EnemyAttackPatternDispatchRejectionCode), rejection))
                throw new ArgumentOutOfRangeException(nameof(rejection));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            if (string.IsNullOrWhiteSpace(fingerprint))
                throw new ArgumentException("A dispatch fingerprint is required.", nameof(fingerprint));
            if ((status == EnemyAttackPatternOperationStatus.Applied
                    || status == EnemyAttackPatternOperationStatus.ExactReplay)
                != (rejection == EnemyAttackPatternDispatchRejectionCode.None))
            {
                throw new ArgumentException(
                    "Accepted dispatch results require no rejection; rejected results require one.");
            }
            Status = status;
            Rejection = rejection;
            Fingerprint = fingerprint.Trim();
        }

        public EnemyAttackPatternOperationStatus Status { get; }
        public EnemyAttackPatternDispatchRejectionCode Rejection { get; }
        public StableId OperationStableId { get; }
        public string Fingerprint { get; }
        public bool IsAccepted
        {
            get
            {
                return Rejection == EnemyAttackPatternDispatchRejectionCode.None
                    && (Status == EnemyAttackPatternOperationStatus.Applied
                        || Status == EnemyAttackPatternOperationStatus.ExactReplay);
            }
        }

        public static EnemyAttackPatternDispatchResult Applied(
            StableId operationStableId,
            string fingerprint)
        {
            return new EnemyAttackPatternDispatchResult(
                EnemyAttackPatternOperationStatus.Applied,
                EnemyAttackPatternDispatchRejectionCode.None,
                operationStableId,
                fingerprint);
        }

        public static EnemyAttackPatternDispatchResult ExactReplay(
            StableId operationStableId,
            string fingerprint)
        {
            return new EnemyAttackPatternDispatchResult(
                EnemyAttackPatternOperationStatus.ExactReplay,
                EnemyAttackPatternDispatchRejectionCode.None,
                operationStableId,
                fingerprint);
        }

        public static EnemyAttackPatternDispatchResult Rejected(
            StableId operationStableId,
            string fingerprint,
            EnemyAttackPatternDispatchRejectionCode rejection)
        {
            if (rejection == EnemyAttackPatternDispatchRejectionCode.None)
                throw new ArgumentOutOfRangeException(nameof(rejection));
            return new EnemyAttackPatternDispatchResult(
                EnemyAttackPatternOperationStatus.Rejected,
                rejection,
                operationStableId,
                fingerprint);
        }
    }

    /// <summary>
    /// Immutable atomic delivery unit for one complete scheduled attack sequence.
    /// Consumers must prevalidate the entire batch before committing any queued effect.
    /// </summary>
    public sealed class EnemyAttackSequenceDispatch
    {
        private readonly ReadOnlyCollection<EnemyAttackEffectEmission> emissions;

        public EnemyAttackSequenceDispatch(
            EnemyAttackExecutionRequest execution,
            EnemyAttackSequence sequence,
            IEnumerable<EnemyAttackEffectEmission> emissions)
        {
            Execution = execution ?? throw new ArgumentNullException(nameof(execution));
            Sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            if (sequence.Identity.OperationStableId != execution.OperationStableId
                || sequence.Identity.SourceEntityStableId != execution.Identity.EntityInstanceId
                || sequence.Identity.SourceLifecycleGeneration != execution.LifecycleGeneration
                || sequence.Identity.AttackStableId != execution.Descriptor.AttackId)
            {
                throw new ArgumentException(
                    "Sequence dispatch does not match its accepted execution.",
                    nameof(sequence));
            }

            var copy = new List<EnemyAttackEffectEmission>();
            var ids = new HashSet<StableId>();
            if (emissions == null) throw new ArgumentNullException(nameof(emissions));
            foreach (EnemyAttackEffectEmission emission in emissions)
            {
                if (emission == null)
                    throw new ArgumentException("Dispatch emissions cannot contain null.", nameof(emissions));
                if (emission.SequenceStableId != sequence.Identity.SequenceStableId
                    || !string.Equals(
                        emission.SequenceFingerprint,
                        sequence.Fingerprint,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        EnemyAttackPatternFingerprint.Execution(emission.Execution),
                        EnemyAttackPatternFingerprint.Execution(execution),
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Every emission must belong to the dispatched sequence and execution.",
                        nameof(emissions));
                }
                if (!ids.Add(emission.EmissionStableId))
                    throw new ArgumentException(
                        "Dispatch emission identities must be unique.",
                        nameof(emissions));
                copy.Add(emission);
            }
            if (copy.Count == 0)
                throw new ArgumentException("A sequence dispatch requires at least one emission.", nameof(emissions));
            copy.Sort(CompareEmissions);
            this.emissions = new ReadOnlyCollection<EnemyAttackEffectEmission>(copy);
            Fingerprint = BuildFingerprint();
        }

        public StableId DispatchStableId
        {
            get { return Sequence.Identity.SequenceStableId; }
        }
        public EnemyAttackExecutionRequest Execution { get; }
        public EnemyAttackSequence Sequence { get; }
        public IReadOnlyList<EnemyAttackEffectEmission> Emissions
        {
            get { return emissions; }
        }
        public string Fingerprint { get; }

        private string BuildFingerprint()
        {
            var builder = new StringBuilder("enemy-attack-sequence-dispatch-v1");
            EnemyAttackEffectEmission.Append(builder, "dispatch", DispatchStableId);
            EnemyAttackEffectEmission.Append(builder, "sequence", Sequence.Fingerprint);
            EnemyAttackEffectEmission.Append(
                builder,
                "execution",
                EnemyAttackPatternFingerprint.Execution(Execution));
            for (int index = 0; index < emissions.Count; index++)
            {
                EnemyAttackEffectEmission.Append(
                    builder,
                    "emission-" + index.ToString(CultureInfo.InvariantCulture),
                    emissions[index].Fingerprint);
            }
            return EnemyAttackEffectEmission.Hash(builder);
        }

        private static int CompareEmissions(
            EnemyAttackEffectEmission left,
            EnemyAttackEffectEmission right)
        {
            int time = left.ScheduledAtSeconds.CompareTo(right.ScheduledAtSeconds);
            if (time != 0) return time;
            return left.EmissionStableId.CompareTo(right.EmissionStableId);
        }
    }

    public interface IEnemyAttackPatternEffectPort
    {
        EnemyAttackPatternDispatchResult Dispatch(EnemyAttackSequenceDispatch sequence);
        EnemyAttackPatternDispatchResult Cancel(
            EnemyAttackSequenceCancellationFact cancellation);
    }
}

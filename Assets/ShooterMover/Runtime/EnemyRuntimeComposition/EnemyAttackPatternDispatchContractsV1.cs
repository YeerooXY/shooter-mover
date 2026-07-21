using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.EnemyRuntimeComposition
{
    public enum EnemyAttackPatternDispatchRejectionCodeV1
    {
        None = 0,
        InvalidCommand = 1,
        ConflictingDuplicate = 2,
        UnsupportedPort = 3,
        DownstreamFailure = 4,
        InvalidResult = 5,
    }

    public sealed class EnemyAttackPatternDispatchResultV1
    {
        public EnemyAttackPatternDispatchResultV1(
            EnemyAttackPatternOperationStatusV1 status,
            EnemyAttackPatternDispatchRejectionCodeV1 rejection,
            StableId operationStableId,
            string fingerprint)
        {
            if (!Enum.IsDefined(typeof(EnemyAttackPatternOperationStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (!Enum.IsDefined(typeof(EnemyAttackPatternDispatchRejectionCodeV1), rejection))
                throw new ArgumentOutOfRangeException(nameof(rejection));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            if (string.IsNullOrWhiteSpace(fingerprint))
                throw new ArgumentException("A dispatch fingerprint is required.", nameof(fingerprint));
            if ((status == EnemyAttackPatternOperationStatusV1.Applied
                    || status == EnemyAttackPatternOperationStatusV1.ExactReplay)
                != (rejection == EnemyAttackPatternDispatchRejectionCodeV1.None))
            {
                throw new ArgumentException(
                    "Accepted dispatch results require no rejection; rejected results require one.");
            }
            Status = status;
            Rejection = rejection;
            Fingerprint = fingerprint.Trim();
        }

        public EnemyAttackPatternOperationStatusV1 Status { get; }
        public EnemyAttackPatternDispatchRejectionCodeV1 Rejection { get; }
        public StableId OperationStableId { get; }
        public string Fingerprint { get; }
        public bool IsAccepted
        {
            get
            {
                return Rejection == EnemyAttackPatternDispatchRejectionCodeV1.None
                    && (Status == EnemyAttackPatternOperationStatusV1.Applied
                        || Status == EnemyAttackPatternOperationStatusV1.ExactReplay);
            }
        }

        public static EnemyAttackPatternDispatchResultV1 Applied(
            StableId operationStableId,
            string fingerprint)
        {
            return new EnemyAttackPatternDispatchResultV1(
                EnemyAttackPatternOperationStatusV1.Applied,
                EnemyAttackPatternDispatchRejectionCodeV1.None,
                operationStableId,
                fingerprint);
        }

        public static EnemyAttackPatternDispatchResultV1 ExactReplay(
            StableId operationStableId,
            string fingerprint)
        {
            return new EnemyAttackPatternDispatchResultV1(
                EnemyAttackPatternOperationStatusV1.ExactReplay,
                EnemyAttackPatternDispatchRejectionCodeV1.None,
                operationStableId,
                fingerprint);
        }

        public static EnemyAttackPatternDispatchResultV1 Rejected(
            StableId operationStableId,
            string fingerprint,
            EnemyAttackPatternDispatchRejectionCodeV1 rejection)
        {
            if (rejection == EnemyAttackPatternDispatchRejectionCodeV1.None)
                throw new ArgumentOutOfRangeException(nameof(rejection));
            return new EnemyAttackPatternDispatchResultV1(
                EnemyAttackPatternOperationStatusV1.Rejected,
                rejection,
                operationStableId,
                fingerprint);
        }
    }

    /// <summary>
    /// Immutable atomic delivery unit for one complete scheduled attack sequence.
    /// Consumers must prevalidate the entire batch before committing any queued effect.
    /// </summary>
    public sealed class EnemyAttackSequenceDispatchV1
    {
        private readonly ReadOnlyCollection<EnemyAttackEffectEmissionV1> emissions;

        public EnemyAttackSequenceDispatchV1(
            EnemyAttackExecutionRequestV1 execution,
            EnemyAttackSequenceV1 sequence,
            IEnumerable<EnemyAttackEffectEmissionV1> emissions)
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

            var copy = new List<EnemyAttackEffectEmissionV1>();
            var ids = new HashSet<StableId>();
            if (emissions == null) throw new ArgumentNullException(nameof(emissions));
            foreach (EnemyAttackEffectEmissionV1 emission in emissions)
            {
                if (emission == null)
                    throw new ArgumentException("Dispatch emissions cannot contain null.", nameof(emissions));
                if (emission.SequenceStableId != sequence.Identity.SequenceStableId
                    || !string.Equals(
                        emission.SequenceFingerprint,
                        sequence.Fingerprint,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        EnemyAttackPatternFingerprintV1.Execution(emission.Execution),
                        EnemyAttackPatternFingerprintV1.Execution(execution),
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
            this.emissions = new ReadOnlyCollection<EnemyAttackEffectEmissionV1>(copy);
            Fingerprint = BuildFingerprint();
        }

        public StableId DispatchStableId
        {
            get { return Sequence.Identity.SequenceStableId; }
        }
        public EnemyAttackExecutionRequestV1 Execution { get; }
        public EnemyAttackSequenceV1 Sequence { get; }
        public IReadOnlyList<EnemyAttackEffectEmissionV1> Emissions
        {
            get { return emissions; }
        }
        public string Fingerprint { get; }

        private string BuildFingerprint()
        {
            var builder = new StringBuilder("enemy-attack-sequence-dispatch-v1");
            EnemyAttackEffectEmissionV1.Append(builder, "dispatch", DispatchStableId);
            EnemyAttackEffectEmissionV1.Append(builder, "sequence", Sequence.Fingerprint);
            EnemyAttackEffectEmissionV1.Append(
                builder,
                "execution",
                EnemyAttackPatternFingerprintV1.Execution(Execution));
            for (int index = 0; index < emissions.Count; index++)
            {
                EnemyAttackEffectEmissionV1.Append(
                    builder,
                    "emission-" + index.ToString(CultureInfo.InvariantCulture),
                    emissions[index].Fingerprint);
            }
            return EnemyAttackEffectEmissionV1.Hash(builder);
        }

        private static int CompareEmissions(
            EnemyAttackEffectEmissionV1 left,
            EnemyAttackEffectEmissionV1 right)
        {
            int time = left.ScheduledAtSeconds.CompareTo(right.ScheduledAtSeconds);
            if (time != 0) return time;
            return left.EmissionStableId.CompareTo(right.EmissionStableId);
        }
    }

    public interface IEnemyAttackPatternEffectPortV1
    {
        EnemyAttackPatternDispatchResultV1 Dispatch(EnemyAttackSequenceDispatchV1 sequence);
        EnemyAttackPatternDispatchResultV1 Cancel(
            EnemyAttackSequenceCancellationFactV1 cancellation);
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.UnityAdapters.Enemies
{
    public interface IEnemyAttackPatternRunTimeV1
    {
        double CurrentTimeSeconds { get; }
        bool IsCurrent(EnemyAttackExecutionRequestV1 execution);
    }

    public interface IEnemyAttackPatternEmissionRealizerV1
    {
        bool CanRealize(EnemyAttackEffectEmissionV1 emission, out string rejectionCode);
        void Realize(EnemyAttackEffectEmissionV1 emission);
        void CancelActiveWindow(EnemyAttackEffectEmissionV1 emission);
    }

    public enum EnemyAttackPatternRealizationStatusV1
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
        RetryableFailure = 5,
    }

    public sealed class EnemyAttackPatternRealizationResultV1
    {
        public EnemyAttackPatternRealizationResultV1(
            EnemyAttackPatternRealizationStatusV1 status,
            StableId operationStableId,
            StableId emissionStableId,
            string fingerprint,
            string detail)
        {
            if (!Enum.IsDefined(typeof(EnemyAttackPatternRealizationStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            OperationStableId = operationStableId;
            EmissionStableId = emissionStableId;
            Fingerprint = fingerprint ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public EnemyAttackPatternRealizationStatusV1 Status { get; }
        public StableId OperationStableId { get; }
        public StableId EmissionStableId { get; }
        public string Fingerprint { get; }
        public string Detail { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == EnemyAttackPatternRealizationStatusV1.Applied
                    || Status == EnemyAttackPatternRealizationStatusV1.ExactReplay;
            }
        }
        public bool IsRetryable
        {
            get { return Status == EnemyAttackPatternRealizationStatusV1.RetryableFailure; }
        }
    }

    public interface IEnemyAttackPatternTransactionalRealizerV1
    {
        bool CanRealize(EnemyAttackEffectEmissionV1 emission, out string rejectionCode);
        EnemyAttackPatternRealizationResultV1 TryRealize(
            EnemyAttackEffectEmissionV1 emission);
        EnemyAttackPatternRealizationResultV1 TryCancelActiveWindow(
            EnemyAttackSequenceCancellationFactV1 cancellation,
            EnemyAttackEffectEmissionV1 emission);
    }

    public sealed class EnemyAttackPatternTransactionalRealizerV1 :
        IEnemyAttackPatternTransactionalRealizerV1
    {
        private sealed class ReplayRecord
        {
            public ReplayRecord(string fingerprint, EnemyAttackPatternRealizationResultV1 result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }
            public string Fingerprint { get; }
            public EnemyAttackPatternRealizationResultV1 Result { get; }
        }

        private readonly IEnemyAttackPatternEmissionRealizerV1 inner;
        private readonly Dictionary<StableId, ReplayRecord> realizedByEmission =
            new Dictionary<StableId, ReplayRecord>();
        private readonly Dictionary<string, ReplayRecord> cancelledByOperation =
            new Dictionary<string, ReplayRecord>(StringComparer.Ordinal);

        public EnemyAttackPatternTransactionalRealizerV1(
            IEnemyAttackPatternEmissionRealizerV1 inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool CanRealize(
            EnemyAttackEffectEmissionV1 emission,
            out string rejectionCode)
        {
            try
            {
                return inner.CanRealize(emission, out rejectionCode);
            }
            catch (Exception exception)
            {
                rejectionCode = "enemy-pattern-realizer-preflight-exception:"
                    + exception.GetType().Name;
                return false;
            }
        }

        public EnemyAttackPatternRealizationResultV1 TryRealize(
            EnemyAttackEffectEmissionV1 emission)
        {
            if (emission == null || emission.EmissionStableId == null)
                return Result(EnemyAttackPatternRealizationStatusV1.Rejected, null,
                    emission, "enemy-pattern-realization-invalid");

            ReplayRecord replay;
            if (realizedByEmission.TryGetValue(emission.EmissionStableId, out replay))
            {
                return string.Equals(replay.Fingerprint, emission.Fingerprint,
                        StringComparison.Ordinal)
                    ? Result(EnemyAttackPatternRealizationStatusV1.ExactReplay,
                        emission.EmissionStableId, emission, string.Empty)
                    : Result(EnemyAttackPatternRealizationStatusV1.ConflictingDuplicate,
                        emission.EmissionStableId, emission,
                        "enemy-pattern-realization-conflict");
            }

            string rejection;
            if (!CanRealize(emission, out rejection))
            {
                return Result(EnemyAttackPatternRealizationStatusV1.Rejected,
                    emission.EmissionStableId, emission,
                    string.IsNullOrEmpty(rejection)
                        ? "enemy-pattern-realization-rejected"
                        : rejection);
            }

            try
            {
                inner.Realize(emission);
                EnemyAttackPatternRealizationResultV1 applied = Result(
                    EnemyAttackPatternRealizationStatusV1.Applied,
                    emission.EmissionStableId, emission, string.Empty);
                realizedByEmission.Add(emission.EmissionStableId,
                    new ReplayRecord(emission.Fingerprint, applied));
                return applied;
            }
            catch (Exception exception)
            {
                try { inner.CancelActiveWindow(emission); }
                catch (Exception) { }
                return Result(EnemyAttackPatternRealizationStatusV1.RetryableFailure,
                    emission.EmissionStableId, emission,
                    "enemy-pattern-realization-retryable:"
                        + exception.GetType().Name);
            }
        }

        public EnemyAttackPatternRealizationResultV1 TryCancelActiveWindow(
            EnemyAttackSequenceCancellationFactV1 cancellation,
            EnemyAttackEffectEmissionV1 emission)
        {
            if (cancellation == null
                || cancellation.CancellationStableId == null
                || emission == null
                || emission.EmissionStableId == null)
            {
                return Result(EnemyAttackPatternRealizationStatusV1.Rejected,
                    cancellation == null ? null : cancellation.CancellationStableId,
                    emission, "enemy-pattern-cancellation-realization-invalid");
            }

            string operationKey = cancellation.CancellationStableId
                + "|" + emission.EmissionStableId;
            string fingerprint = cancellation.Fingerprint
                + "|" + emission.Fingerprint;
            ReplayRecord replay;
            if (cancelledByOperation.TryGetValue(operationKey, out replay))
            {
                return string.Equals(replay.Fingerprint, fingerprint,
                        StringComparison.Ordinal)
                    ? Result(EnemyAttackPatternRealizationStatusV1.ExactReplay,
                        cancellation.CancellationStableId, emission, string.Empty)
                    : Result(EnemyAttackPatternRealizationStatusV1.ConflictingDuplicate,
                        cancellation.CancellationStableId, emission,
                        "enemy-pattern-cancellation-realization-conflict");
            }

            try
            {
                inner.CancelActiveWindow(emission);
                EnemyAttackPatternRealizationResultV1 applied = Result(
                    EnemyAttackPatternRealizationStatusV1.Applied,
                    cancellation.CancellationStableId, emission, string.Empty);
                cancelledByOperation.Add(operationKey,
                    new ReplayRecord(fingerprint, applied));
                return applied;
            }
            catch (Exception exception)
            {
                try { inner.Realize(emission); }
                catch (Exception) { }
                return Result(EnemyAttackPatternRealizationStatusV1.RetryableFailure,
                    cancellation.CancellationStableId, emission,
                    "enemy-pattern-cancellation-retryable:"
                        + exception.GetType().Name);
            }
        }

        private static EnemyAttackPatternRealizationResultV1 Result(
            EnemyAttackPatternRealizationStatusV1 status,
            StableId operationStableId,
            EnemyAttackEffectEmissionV1 emission,
            string detail)
        {
            return new EnemyAttackPatternRealizationResultV1(status,
                operationStableId,
                emission == null ? null : emission.EmissionStableId,
                emission == null ? string.Empty : emission.Fingerprint,
                detail);
        }
    }

    public enum EnemyAttackPatternLiveStateV1
    {
        Committed = 1,
        Emitted = 2,
        Cancelled = 3,
        Rejected = 4,
        RetryableFailure = 5,
    }

    public sealed class EnemyAttackPatternLiveRecordV1
    {
        public EnemyAttackPatternLiveRecordV1(
            StableId sequenceStableId,
            StableId emissionStableId,
            string fingerprint,
            EnemyAttackPatternLiveStateV1 state,
            double occurredAtSeconds,
            string detail)
        {
            SequenceStableId = sequenceStableId;
            EmissionStableId = emissionStableId;
            Fingerprint = fingerprint ?? string.Empty;
            State = state;
            OccurredAtSeconds = occurredAtSeconds;
            Detail = detail ?? string.Empty;
        }
        public StableId SequenceStableId { get; }
        public StableId EmissionStableId { get; }
        public string Fingerprint { get; }
        public EnemyAttackPatternLiveStateV1 State { get; }
        public double OccurredAtSeconds { get; }
        public string Detail { get; }
    }

    public sealed class EnemyAttackPatternLiveSchedulerV1 : IEnemyAttackPatternEffectPortV1
    {
        private sealed class SequenceState
        {
            public SequenceState(EnemyAttackSequenceDispatchV1 dispatch)
            {
                Dispatch = dispatch;
                Pending = new List<EnemyAttackEffectEmissionV1>(dispatch.Emissions);
            }
            public EnemyAttackSequenceDispatchV1 Dispatch { get; }
            public List<EnemyAttackEffectEmissionV1> Pending { get; }
        }

        private readonly IEnemyAttackPatternRunTimeV1 runTime;
        private readonly IEnemyAttackPatternTransactionalRealizerV1 realizer;
        private readonly Dictionary<StableId, SequenceState> sequences =
            new Dictionary<StableId, SequenceState>();
        private readonly Dictionary<StableId, string> acceptedFingerprints =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, string> cancellationFingerprints =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, string> attemptedCancellationFingerprints =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, EnemyAttackEffectEmissionV1> activeMeleeWindows =
            new Dictionary<StableId, EnemyAttackEffectEmissionV1>();
        private readonly HashSet<StableId> emitted = new HashSet<StableId>();
        private readonly List<EnemyAttackPatternLiveRecordV1> records =
            new List<EnemyAttackPatternLiveRecordV1>();

        public EnemyAttackPatternLiveSchedulerV1(
            IEnemyAttackPatternRunTimeV1 runTime,
            IEnemyAttackPatternEmissionRealizerV1 realizer)
        {
            this.runTime = runTime ?? throw new ArgumentNullException(nameof(runTime));
            this.realizer = new EnemyAttackPatternTransactionalRealizerV1(
                realizer ?? throw new ArgumentNullException(nameof(realizer)));
        }

        public EnemyAttackPatternLiveSchedulerV1(
            IEnemyAttackPatternRunTimeV1 runTime,
            IEnemyAttackPatternTransactionalRealizerV1 realizer)
        {
            this.runTime = runTime ?? throw new ArgumentNullException(nameof(runTime));
            this.realizer = realizer ?? throw new ArgumentNullException(nameof(realizer));
        }

        public IReadOnlyList<EnemyAttackPatternLiveRecordV1> Records
        {
            get
            {
                return new ReadOnlyCollection<EnemyAttackPatternLiveRecordV1>(
                    records.ToArray());
            }
        }

        public int PendingEmissionCount
        {
            get
            {
                int count = 0;
                foreach (SequenceState state in sequences.Values)
                    count += state.Pending.Count;
                return count;
            }
        }

        public int ActiveMeleeWindowCount
        {
            get { return activeMeleeWindows.Count; }
        }

        public EnemyAttackPatternDispatchResultV1 Dispatch(
            EnemyAttackSequenceDispatchV1 sequence)
        {
            if (sequence == null)
            {
                StableId invalid = StableId.Create(
                    "enemy-attack-sequence", "runtime-invalid-dispatch");
                return EnemyAttackPatternDispatchResultV1.Rejected(invalid,
                    "invalid-dispatch",
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            string existing;
            if (acceptedFingerprints.TryGetValue(sequence.DispatchStableId, out existing))
            {
                if (string.Equals(existing, sequence.Fingerprint, StringComparison.Ordinal))
                    return EnemyAttackPatternDispatchResultV1.ExactReplay(
                        sequence.DispatchStableId, sequence.Fingerprint);
                Record(sequence, null, EnemyAttackPatternLiveStateV1.Rejected,
                    "conflicting-sequence-replay");
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    sequence.DispatchStableId, sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.ConflictingDuplicate);
            }

            if (!runTime.IsCurrent(sequence.Execution))
            {
                Record(sequence, null, EnemyAttackPatternLiveStateV1.Rejected,
                    "wrong-run-or-lifecycle");
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    sequence.DispatchStableId, sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            for (int index = 0; index < sequence.Emissions.Count; index++)
            {
                string rejection;
                if (!realizer.CanRealize(sequence.Emissions[index], out rejection))
                {
                    Record(sequence, sequence.Emissions[index],
                        EnemyAttackPatternLiveStateV1.Rejected,
                        string.IsNullOrEmpty(rejection)
                            ? "emission-preflight-rejected" : rejection);
                    return EnemyAttackPatternDispatchResultV1.Rejected(
                        sequence.DispatchStableId, sequence.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.DownstreamFailure);
                }
            }

            acceptedFingerprints.Add(sequence.DispatchStableId, sequence.Fingerprint);
            sequences.Add(sequence.DispatchStableId, new SequenceState(sequence));
            for (int index = 0; index < sequence.Emissions.Count; index++)
                Record(sequence, sequence.Emissions[index],
                    EnemyAttackPatternLiveStateV1.Committed, string.Empty);
            return EnemyAttackPatternDispatchResultV1.Applied(
                sequence.DispatchStableId, sequence.Fingerprint);
        }

        public EnemyAttackPatternDispatchResultV1 Cancel(
            EnemyAttackSequenceCancellationFactV1 cancellation)
        {
            if (cancellation == null)
            {
                StableId invalid = StableId.Create(
                    "enemy-attack-cancellation", "runtime-invalid-cancellation");
                return EnemyAttackPatternDispatchResultV1.Rejected(invalid,
                    "invalid-cancellation",
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            string existing;
            if (cancellationFingerprints.TryGetValue(
                    cancellation.CancellationStableId, out existing))
            {
                return string.Equals(existing, cancellation.Fingerprint,
                        StringComparison.Ordinal)
                    ? EnemyAttackPatternDispatchResultV1.ExactReplay(
                        cancellation.CancellationStableId, cancellation.Fingerprint)
                    : EnemyAttackPatternDispatchResultV1.Rejected(
                        cancellation.CancellationStableId, cancellation.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.ConflictingDuplicate);
            }

            if (attemptedCancellationFingerprints.TryGetValue(
                    cancellation.CancellationStableId, out existing)
                && !string.Equals(existing, cancellation.Fingerprint,
                    StringComparison.Ordinal))
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    cancellation.CancellationStableId, cancellation.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.ConflictingDuplicate);
            }

            var projectileIds = new HashSet<StableId>(
                cancellation.CancelledProjectileStableIds);
            var meleeIds = new HashSet<StableId>(
                cancellation.CancelledMeleeStrikeStableIds);
            var pendingMatches = new List<Tuple<SequenceState, EnemyAttackEffectEmissionV1>>();
            var activeMatches = new List<EnemyAttackEffectEmissionV1>();

            foreach (SequenceState state in sequences.Values)
            {
                for (int index = 0; index < state.Pending.Count; index++)
                {
                    EnemyAttackEffectEmissionV1 emission = state.Pending[index];
                    if (!projectileIds.Contains(emission.EmissionStableId)
                        && !meleeIds.Contains(emission.EmissionStableId))
                        continue;
                    if (!CancellationMatches(cancellation, emission))
                        return EnemyAttackPatternDispatchResultV1.Rejected(
                            cancellation.CancellationStableId,
                            cancellation.Fingerprint,
                            EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
                    pendingMatches.Add(Tuple.Create(state, emission));
                }
            }

            foreach (EnemyAttackEffectEmissionV1 emission in activeMeleeWindows.Values)
            {
                if (!meleeIds.Contains(emission.EmissionStableId))
                    continue;
                if (!CancellationMatches(cancellation, emission))
                    return EnemyAttackPatternDispatchResultV1.Rejected(
                        cancellation.CancellationStableId,
                        cancellation.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
                activeMatches.Add(emission);
            }

            if (!attemptedCancellationFingerprints.ContainsKey(
                    cancellation.CancellationStableId))
            {
                attemptedCancellationFingerprints.Add(
                    cancellation.CancellationStableId, cancellation.Fingerprint);
            }

            for (int index = 0; index < activeMatches.Count; index++)
            {
                EnemyAttackPatternRealizationResultV1 close =
                    TryCancelSafely(cancellation, activeMatches[index]);
                if (!close.IsAccepted)
                {
                    RecordForEmission(activeMatches[index],
                        close.IsRetryable
                            ? EnemyAttackPatternLiveStateV1.RetryableFailure
                            : EnemyAttackPatternLiveStateV1.Rejected,
                        close.Detail);
                    return EnemyAttackPatternDispatchResultV1.Rejected(
                        cancellation.CancellationStableId,
                        cancellation.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.DownstreamFailure);
                }
            }

            cancellationFingerprints.Add(
                cancellation.CancellationStableId, cancellation.Fingerprint);
            attemptedCancellationFingerprints.Remove(
                cancellation.CancellationStableId);

            for (int index = 0; index < pendingMatches.Count; index++)
            {
                SequenceState state = pendingMatches[index].Item1;
                EnemyAttackEffectEmissionV1 emission = pendingMatches[index].Item2;
                state.Pending.Remove(emission);
                Record(state.Dispatch, emission,
                    EnemyAttackPatternLiveStateV1.Cancelled,
                    "pending-emission-cancelled");
            }

            for (int index = 0; index < activeMatches.Count; index++)
            {
                EnemyAttackEffectEmissionV1 emission = activeMatches[index];
                activeMeleeWindows.Remove(emission.EmissionStableId);
                RecordForEmission(emission,
                    EnemyAttackPatternLiveStateV1.Cancelled,
                    "active-window-cancelled");
            }

            return EnemyAttackPatternDispatchResultV1.Applied(
                cancellation.CancellationStableId, cancellation.Fingerprint);
        }

        public void Tick()
        {
            double now = runTime.CurrentTimeSeconds;
            RetireElapsedMeleeWindows(now);
            var due = new List<Tuple<EnemyAttackSequenceDispatchV1,
                EnemyAttackEffectEmissionV1>>();
            foreach (SequenceState state in sequences.Values)
            {
                if (!runTime.IsCurrent(state.Dispatch.Execution))
                    continue;
                for (int index = 0; index < state.Pending.Count; index++)
                {
                    EnemyAttackEffectEmissionV1 emission = state.Pending[index];
                    if (emission.ScheduledAtSeconds <= now)
                        due.Add(Tuple.Create(state.Dispatch, emission));
                }
            }
            due.Sort((left, right) =>
            {
                int time = left.Item2.ScheduledAtSeconds.CompareTo(
                    right.Item2.ScheduledAtSeconds);
                return time != 0 ? time
                    : left.Item2.EmissionStableId.CompareTo(
                        right.Item2.EmissionStableId);
            });

            for (int index = 0; index < due.Count; index++)
            {
                EnemyAttackSequenceDispatchV1 dispatch = due[index].Item1;
                EnemyAttackEffectEmissionV1 emission = due[index].Item2;
                if (!runTime.IsCurrent(dispatch.Execution)
                    || emitted.Contains(emission.EmissionStableId))
                    continue;

                EnemyAttackPatternRealizationResultV1 realization =
                    TryRealizeSafely(emission);
                if (!realization.IsAccepted)
                {
                    Record(dispatch, emission,
                        realization.IsRetryable
                            ? EnemyAttackPatternLiveStateV1.RetryableFailure
                            : EnemyAttackPatternLiveStateV1.Rejected,
                        realization.Detail);
                    continue;
                }

                emitted.Add(emission.EmissionStableId);
                sequences[dispatch.DispatchStableId].Pending.Remove(emission);
                if (emission.Kind == EnemyAttackEffectEmissionKindV1.MeleeStrike
                    && emission.ActiveUntilSeconds > now)
                    activeMeleeWindows[emission.EmissionStableId] = emission;
                Record(dispatch, emission, EnemyAttackPatternLiveStateV1.Emitted,
                    realization.Status == EnemyAttackPatternRealizationStatusV1.ExactReplay
                        ? "downstream-exact-replay" : string.Empty);
            }
        }

        private EnemyAttackPatternRealizationResultV1 TryRealizeSafely(
            EnemyAttackEffectEmissionV1 emission)
        {
            try
            {
                return realizer.TryRealize(emission)
                    ?? new EnemyAttackPatternRealizationResultV1(
                        EnemyAttackPatternRealizationStatusV1.RetryableFailure,
                        emission == null ? null : emission.EmissionStableId,
                        emission == null ? null : emission.EmissionStableId,
                        emission == null ? string.Empty : emission.Fingerprint,
                        "enemy-pattern-realizer-null-result");
            }
            catch (Exception exception)
            {
                return new EnemyAttackPatternRealizationResultV1(
                    EnemyAttackPatternRealizationStatusV1.RetryableFailure,
                    emission == null ? null : emission.EmissionStableId,
                    emission == null ? null : emission.EmissionStableId,
                    emission == null ? string.Empty : emission.Fingerprint,
                    "enemy-pattern-realizer-exception:"
                        + exception.GetType().Name);
            }
        }

        private EnemyAttackPatternRealizationResultV1 TryCancelSafely(
            EnemyAttackSequenceCancellationFactV1 cancellation,
            EnemyAttackEffectEmissionV1 emission)
        {
            try
            {
                return realizer.TryCancelActiveWindow(cancellation, emission)
                    ?? new EnemyAttackPatternRealizationResultV1(
                        EnemyAttackPatternRealizationStatusV1.RetryableFailure,
                        cancellation == null ? null : cancellation.CancellationStableId,
                        emission == null ? null : emission.EmissionStableId,
                        emission == null ? string.Empty : emission.Fingerprint,
                        "enemy-pattern-cancellation-realizer-null-result");
            }
            catch (Exception exception)
            {
                return new EnemyAttackPatternRealizationResultV1(
                    EnemyAttackPatternRealizationStatusV1.RetryableFailure,
                    cancellation == null ? null : cancellation.CancellationStableId,
                    emission == null ? null : emission.EmissionStableId,
                    emission == null ? string.Empty : emission.Fingerprint,
                    "enemy-pattern-cancellation-realizer-exception:"
                        + exception.GetType().Name);
            }
        }

        private void RetireElapsedMeleeWindows(double now)
        {
            var elapsed = new List<StableId>();
            foreach (KeyValuePair<StableId, EnemyAttackEffectEmissionV1> pair
                in activeMeleeWindows)
            {
                if (pair.Value.ActiveUntilSeconds <= now)
                    elapsed.Add(pair.Key);
            }
            for (int index = 0; index < elapsed.Count; index++)
                activeMeleeWindows.Remove(elapsed[index]);
        }

        private static bool CancellationMatches(
            EnemyAttackSequenceCancellationFactV1 cancellation,
            EnemyAttackEffectEmissionV1 emission)
        {
            return cancellation.SourceEntityStableId == emission.SourceEntityStableId
                && cancellation.SourceLifecycleGeneration
                    == emission.SourceLifecycleGeneration;
        }

        private void RecordForEmission(
            EnemyAttackEffectEmissionV1 emission,
            EnemyAttackPatternLiveStateV1 state,
            string detail)
        {
            SequenceState sequence;
            if (emission != null
                && sequences.TryGetValue(emission.SequenceStableId, out sequence))
                Record(sequence.Dispatch, emission, state, detail);
        }

        private void Record(
            EnemyAttackSequenceDispatchV1 dispatch,
            EnemyAttackEffectEmissionV1 emission,
            EnemyAttackPatternLiveStateV1 state,
            string detail)
        {
            records.Add(new EnemyAttackPatternLiveRecordV1(
                dispatch.DispatchStableId,
                emission == null ? null : emission.EmissionStableId,
                emission == null ? dispatch.Fingerprint : emission.Fingerprint,
                state,
                runTime.CurrentTimeSeconds,
                detail));
        }
    }
}

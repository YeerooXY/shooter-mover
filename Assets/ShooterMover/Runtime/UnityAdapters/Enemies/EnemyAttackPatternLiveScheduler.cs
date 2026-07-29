using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.UnityAdapters.Enemies
{
    public interface IEnemyAttackPatternRunTime
    {
        double CurrentTimeSeconds { get; }
        bool IsCurrent(EnemyAttackExecutionRequest execution);
    }

    public interface IEnemyAttackPatternEmissionRealizer
    {
        bool CanRealize(EnemyAttackEffectEmission emission, out string rejectionCode);
        void Realize(EnemyAttackEffectEmission emission);
        void CancelActiveWindow(EnemyAttackEffectEmission emission);
    }

    public enum EnemyAttackPatternRealizationStatus
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
        RetryableFailure = 5,
    }

    public sealed class EnemyAttackPatternRealizationResult
    {
        public EnemyAttackPatternRealizationResult(
            EnemyAttackPatternRealizationStatus status,
            StableId operationStableId,
            StableId emissionStableId,
            string fingerprint,
            string detail)
        {
            if (!Enum.IsDefined(typeof(EnemyAttackPatternRealizationStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            OperationStableId = operationStableId;
            EmissionStableId = emissionStableId;
            Fingerprint = fingerprint ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public EnemyAttackPatternRealizationStatus Status { get; }
        public StableId OperationStableId { get; }
        public StableId EmissionStableId { get; }
        public string Fingerprint { get; }
        public string Detail { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == EnemyAttackPatternRealizationStatus.Applied
                    || Status == EnemyAttackPatternRealizationStatus.ExactReplay;
            }
        }
        public bool IsRetryable
        {
            get { return Status == EnemyAttackPatternRealizationStatus.RetryableFailure; }
        }
    }

    public interface IEnemyAttackPatternTransactionalRealizer
    {
        bool CanRealize(EnemyAttackEffectEmission emission, out string rejectionCode);
        EnemyAttackPatternRealizationResult TryRealize(
            EnemyAttackEffectEmission emission);
        EnemyAttackPatternRealizationResult TryCancelActiveWindow(
            EnemyAttackSequenceCancellationFact cancellation,
            EnemyAttackEffectEmission emission);
    }

    public sealed class EnemyAttackPatternTransactionalRealizer :
        IEnemyAttackPatternTransactionalRealizer
    {
        private sealed class ReplayRecord
        {
            public ReplayRecord(string fingerprint, EnemyAttackPatternRealizationResult result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }
            public string Fingerprint { get; }
            public EnemyAttackPatternRealizationResult Result { get; }
        }

        private readonly IEnemyAttackPatternEmissionRealizer inner;
        private readonly Dictionary<StableId, ReplayRecord> realizedByEmission =
            new Dictionary<StableId, ReplayRecord>();
        private readonly Dictionary<string, ReplayRecord> cancelledByOperation =
            new Dictionary<string, ReplayRecord>(StringComparer.Ordinal);

        public EnemyAttackPatternTransactionalRealizer(
            IEnemyAttackPatternEmissionRealizer inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool CanRealize(
            EnemyAttackEffectEmission emission,
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

        public EnemyAttackPatternRealizationResult TryRealize(
            EnemyAttackEffectEmission emission)
        {
            if (emission == null || emission.EmissionStableId == null)
                return Result(EnemyAttackPatternRealizationStatus.Rejected, null,
                    emission, "enemy-pattern-realization-invalid");

            ReplayRecord replay;
            if (realizedByEmission.TryGetValue(emission.EmissionStableId, out replay))
            {
                return string.Equals(replay.Fingerprint, emission.Fingerprint,
                        StringComparison.Ordinal)
                    ? Result(EnemyAttackPatternRealizationStatus.ExactReplay,
                        emission.EmissionStableId, emission, string.Empty)
                    : Result(EnemyAttackPatternRealizationStatus.ConflictingDuplicate,
                        emission.EmissionStableId, emission,
                        "enemy-pattern-realization-conflict");
            }

            string rejection;
            if (!CanRealize(emission, out rejection))
            {
                return Result(EnemyAttackPatternRealizationStatus.Rejected,
                    emission.EmissionStableId, emission,
                    string.IsNullOrEmpty(rejection)
                        ? "enemy-pattern-realization-rejected"
                        : rejection);
            }

            try
            {
                inner.Realize(emission);
                EnemyAttackPatternRealizationResult applied = Result(
                    EnemyAttackPatternRealizationStatus.Applied,
                    emission.EmissionStableId, emission, string.Empty);
                realizedByEmission.Add(emission.EmissionStableId,
                    new ReplayRecord(emission.Fingerprint, applied));
                return applied;
            }
            catch (Exception exception)
            {
                try { inner.CancelActiveWindow(emission); }
                catch (Exception) { }
                return Result(EnemyAttackPatternRealizationStatus.RetryableFailure,
                    emission.EmissionStableId, emission,
                    "enemy-pattern-realization-retryable:"
                        + exception.GetType().Name);
            }
        }

        public EnemyAttackPatternRealizationResult TryCancelActiveWindow(
            EnemyAttackSequenceCancellationFact cancellation,
            EnemyAttackEffectEmission emission)
        {
            if (cancellation == null
                || cancellation.CancellationStableId == null
                || emission == null
                || emission.EmissionStableId == null)
            {
                return Result(EnemyAttackPatternRealizationStatus.Rejected,
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
                    ? Result(EnemyAttackPatternRealizationStatus.ExactReplay,
                        cancellation.CancellationStableId, emission, string.Empty)
                    : Result(EnemyAttackPatternRealizationStatus.ConflictingDuplicate,
                        cancellation.CancellationStableId, emission,
                        "enemy-pattern-cancellation-realization-conflict");
            }

            try
            {
                inner.CancelActiveWindow(emission);
                EnemyAttackPatternRealizationResult applied = Result(
                    EnemyAttackPatternRealizationStatus.Applied,
                    cancellation.CancellationStableId, emission, string.Empty);
                cancelledByOperation.Add(operationKey,
                    new ReplayRecord(fingerprint, applied));
                return applied;
            }
            catch (Exception exception)
            {
                try { inner.Realize(emission); }
                catch (Exception) { }
                return Result(EnemyAttackPatternRealizationStatus.RetryableFailure,
                    cancellation.CancellationStableId, emission,
                    "enemy-pattern-cancellation-retryable:"
                        + exception.GetType().Name);
            }
        }

        private static EnemyAttackPatternRealizationResult Result(
            EnemyAttackPatternRealizationStatus status,
            StableId operationStableId,
            EnemyAttackEffectEmission emission,
            string detail)
        {
            return new EnemyAttackPatternRealizationResult(status,
                operationStableId,
                emission == null ? null : emission.EmissionStableId,
                emission == null ? string.Empty : emission.Fingerprint,
                detail);
        }
    }

    public enum EnemyAttackPatternLiveState
    {
        Committed = 1,
        Emitted = 2,
        Cancelled = 3,
        Rejected = 4,
        RetryableFailure = 5,
    }

    public sealed class EnemyAttackPatternLiveRecord
    {
        public EnemyAttackPatternLiveRecord(
            StableId sequenceStableId,
            StableId emissionStableId,
            string fingerprint,
            EnemyAttackPatternLiveState state,
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
        public EnemyAttackPatternLiveState State { get; }
        public double OccurredAtSeconds { get; }
        public string Detail { get; }
    }

    public sealed class EnemyAttackPatternLiveScheduler : IEnemyAttackPatternEffectPort
    {
        private sealed class SequenceState
        {
            public SequenceState(EnemyAttackSequenceDispatch dispatch)
            {
                Dispatch = dispatch;
                Pending = new List<EnemyAttackEffectEmission>(dispatch.Emissions);
            }
            public EnemyAttackSequenceDispatch Dispatch { get; }
            public List<EnemyAttackEffectEmission> Pending { get; }
        }

        private readonly IEnemyAttackPatternRunTime runTime;
        private readonly IEnemyAttackPatternTransactionalRealizer realizer;
        private readonly Dictionary<StableId, SequenceState> sequences =
            new Dictionary<StableId, SequenceState>();
        private readonly Dictionary<StableId, string> acceptedFingerprints =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, string> cancellationFingerprints =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, string> attemptedCancellationFingerprints =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, EnemyAttackEffectEmission> activeMeleeWindows =
            new Dictionary<StableId, EnemyAttackEffectEmission>();
        private readonly HashSet<StableId> emitted = new HashSet<StableId>();
        private readonly List<EnemyAttackPatternLiveRecord> records =
            new List<EnemyAttackPatternLiveRecord>();

        public EnemyAttackPatternLiveScheduler(
            IEnemyAttackPatternRunTime runTime,
            IEnemyAttackPatternEmissionRealizer realizer)
        {
            this.runTime = runTime ?? throw new ArgumentNullException(nameof(runTime));
            this.realizer = new EnemyAttackPatternTransactionalRealizer(
                realizer ?? throw new ArgumentNullException(nameof(realizer)));
        }

        public EnemyAttackPatternLiveScheduler(
            IEnemyAttackPatternRunTime runTime,
            IEnemyAttackPatternTransactionalRealizer realizer)
        {
            this.runTime = runTime ?? throw new ArgumentNullException(nameof(runTime));
            this.realizer = realizer ?? throw new ArgumentNullException(nameof(realizer));
        }

        public IReadOnlyList<EnemyAttackPatternLiveRecord> Records
        {
            get
            {
                return new ReadOnlyCollection<EnemyAttackPatternLiveRecord>(
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

        public EnemyAttackPatternDispatchResult Dispatch(
            EnemyAttackSequenceDispatch sequence)
        {
            if (sequence == null)
            {
                StableId invalid = StableId.Create(
                    "enemy-attack-sequence", "runtime-invalid-dispatch");
                return EnemyAttackPatternDispatchResult.Rejected(invalid,
                    "invalid-dispatch",
                    EnemyAttackPatternDispatchRejectionCode.InvalidCommand);
            }

            string existing;
            if (acceptedFingerprints.TryGetValue(sequence.DispatchStableId, out existing))
            {
                if (string.Equals(existing, sequence.Fingerprint, StringComparison.Ordinal))
                    return EnemyAttackPatternDispatchResult.ExactReplay(
                        sequence.DispatchStableId, sequence.Fingerprint);
                Record(sequence, null, EnemyAttackPatternLiveState.Rejected,
                    "conflicting-sequence-replay");
                return EnemyAttackPatternDispatchResult.Rejected(
                    sequence.DispatchStableId, sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCode.ConflictingDuplicate);
            }

            if (!runTime.IsCurrent(sequence.Execution))
            {
                Record(sequence, null, EnemyAttackPatternLiveState.Rejected,
                    "wrong-run-or-lifecycle");
                return EnemyAttackPatternDispatchResult.Rejected(
                    sequence.DispatchStableId, sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCode.InvalidCommand);
            }

            for (int index = 0; index < sequence.Emissions.Count; index++)
            {
                string rejection;
                if (!realizer.CanRealize(sequence.Emissions[index], out rejection))
                {
                    Record(sequence, sequence.Emissions[index],
                        EnemyAttackPatternLiveState.Rejected,
                        string.IsNullOrEmpty(rejection)
                            ? "emission-preflight-rejected" : rejection);
                    return EnemyAttackPatternDispatchResult.Rejected(
                        sequence.DispatchStableId, sequence.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCode.DownstreamFailure);
                }
            }

            acceptedFingerprints.Add(sequence.DispatchStableId, sequence.Fingerprint);
            sequences.Add(sequence.DispatchStableId, new SequenceState(sequence));
            for (int index = 0; index < sequence.Emissions.Count; index++)
                Record(sequence, sequence.Emissions[index],
                    EnemyAttackPatternLiveState.Committed, string.Empty);
            return EnemyAttackPatternDispatchResult.Applied(
                sequence.DispatchStableId, sequence.Fingerprint);
        }

        public EnemyAttackPatternDispatchResult Cancel(
            EnemyAttackSequenceCancellationFact cancellation)
        {
            if (cancellation == null)
            {
                StableId invalid = StableId.Create(
                    "enemy-attack-cancellation", "runtime-invalid-cancellation");
                return EnemyAttackPatternDispatchResult.Rejected(invalid,
                    "invalid-cancellation",
                    EnemyAttackPatternDispatchRejectionCode.InvalidCommand);
            }

            string existing;
            if (cancellationFingerprints.TryGetValue(
                    cancellation.CancellationStableId, out existing))
            {
                return string.Equals(existing, cancellation.Fingerprint,
                        StringComparison.Ordinal)
                    ? EnemyAttackPatternDispatchResult.ExactReplay(
                        cancellation.CancellationStableId, cancellation.Fingerprint)
                    : EnemyAttackPatternDispatchResult.Rejected(
                        cancellation.CancellationStableId, cancellation.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCode.ConflictingDuplicate);
            }

            if (attemptedCancellationFingerprints.TryGetValue(
                    cancellation.CancellationStableId, out existing)
                && !string.Equals(existing, cancellation.Fingerprint,
                    StringComparison.Ordinal))
            {
                return EnemyAttackPatternDispatchResult.Rejected(
                    cancellation.CancellationStableId, cancellation.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCode.ConflictingDuplicate);
            }

            var projectileIds = new HashSet<StableId>(
                cancellation.CancelledProjectileStableIds);
            var meleeIds = new HashSet<StableId>(
                cancellation.CancelledMeleeStrikeStableIds);
            var pendingMatches = new List<Tuple<SequenceState, EnemyAttackEffectEmission>>();
            var activeMatches = new List<EnemyAttackEffectEmission>();

            foreach (SequenceState state in sequences.Values)
            {
                for (int index = 0; index < state.Pending.Count; index++)
                {
                    EnemyAttackEffectEmission emission = state.Pending[index];
                    if (!projectileIds.Contains(emission.EmissionStableId)
                        && !meleeIds.Contains(emission.EmissionStableId))
                        continue;
                    if (!CancellationMatches(cancellation, emission))
                        return EnemyAttackPatternDispatchResult.Rejected(
                            cancellation.CancellationStableId,
                            cancellation.Fingerprint,
                            EnemyAttackPatternDispatchRejectionCode.InvalidCommand);
                    pendingMatches.Add(Tuple.Create(state, emission));
                }
            }

            foreach (EnemyAttackEffectEmission emission in activeMeleeWindows.Values)
            {
                if (!meleeIds.Contains(emission.EmissionStableId))
                    continue;
                if (!CancellationMatches(cancellation, emission))
                    return EnemyAttackPatternDispatchResult.Rejected(
                        cancellation.CancellationStableId,
                        cancellation.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCode.InvalidCommand);
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
                EnemyAttackPatternRealizationResult close =
                    TryCancelSafely(cancellation, activeMatches[index]);
                if (!close.IsAccepted)
                {
                    RecordForEmission(activeMatches[index],
                        close.IsRetryable
                            ? EnemyAttackPatternLiveState.RetryableFailure
                            : EnemyAttackPatternLiveState.Rejected,
                        close.Detail);
                    return EnemyAttackPatternDispatchResult.Rejected(
                        cancellation.CancellationStableId,
                        cancellation.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCode.DownstreamFailure);
                }
            }

            cancellationFingerprints.Add(
                cancellation.CancellationStableId, cancellation.Fingerprint);
            attemptedCancellationFingerprints.Remove(
                cancellation.CancellationStableId);

            for (int index = 0; index < pendingMatches.Count; index++)
            {
                SequenceState state = pendingMatches[index].Item1;
                EnemyAttackEffectEmission emission = pendingMatches[index].Item2;
                state.Pending.Remove(emission);
                Record(state.Dispatch, emission,
                    EnemyAttackPatternLiveState.Cancelled,
                    "pending-emission-cancelled");
            }

            for (int index = 0; index < activeMatches.Count; index++)
            {
                EnemyAttackEffectEmission emission = activeMatches[index];
                activeMeleeWindows.Remove(emission.EmissionStableId);
                RecordForEmission(emission,
                    EnemyAttackPatternLiveState.Cancelled,
                    "active-window-cancelled");
            }

            return EnemyAttackPatternDispatchResult.Applied(
                cancellation.CancellationStableId, cancellation.Fingerprint);
        }

        public void Tick()
        {
            double now = runTime.CurrentTimeSeconds;
            RetireElapsedMeleeWindows(now);
            var due = new List<Tuple<EnemyAttackSequenceDispatch,
                EnemyAttackEffectEmission>>();
            foreach (SequenceState state in sequences.Values)
            {
                if (!runTime.IsCurrent(state.Dispatch.Execution))
                    continue;
                for (int index = 0; index < state.Pending.Count; index++)
                {
                    EnemyAttackEffectEmission emission = state.Pending[index];
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
                EnemyAttackSequenceDispatch dispatch = due[index].Item1;
                EnemyAttackEffectEmission emission = due[index].Item2;
                if (!runTime.IsCurrent(dispatch.Execution)
                    || emitted.Contains(emission.EmissionStableId))
                    continue;

                EnemyAttackPatternRealizationResult realization =
                    TryRealizeSafely(emission);
                if (!realization.IsAccepted)
                {
                    Record(dispatch, emission,
                        realization.IsRetryable
                            ? EnemyAttackPatternLiveState.RetryableFailure
                            : EnemyAttackPatternLiveState.Rejected,
                        realization.Detail);
                    continue;
                }

                emitted.Add(emission.EmissionStableId);
                sequences[dispatch.DispatchStableId].Pending.Remove(emission);
                if (emission.Kind == EnemyAttackEffectEmissionKind.MeleeStrike
                    && emission.ActiveUntilSeconds > now)
                    activeMeleeWindows[emission.EmissionStableId] = emission;
                Record(dispatch, emission, EnemyAttackPatternLiveState.Emitted,
                    realization.Status == EnemyAttackPatternRealizationStatus.ExactReplay
                        ? "downstream-exact-replay" : string.Empty);
            }
        }

        private EnemyAttackPatternRealizationResult TryRealizeSafely(
            EnemyAttackEffectEmission emission)
        {
            try
            {
                return realizer.TryRealize(emission)
                    ?? new EnemyAttackPatternRealizationResult(
                        EnemyAttackPatternRealizationStatus.RetryableFailure,
                        emission == null ? null : emission.EmissionStableId,
                        emission == null ? null : emission.EmissionStableId,
                        emission == null ? string.Empty : emission.Fingerprint,
                        "enemy-pattern-realizer-null-result");
            }
            catch (Exception exception)
            {
                return new EnemyAttackPatternRealizationResult(
                    EnemyAttackPatternRealizationStatus.RetryableFailure,
                    emission == null ? null : emission.EmissionStableId,
                    emission == null ? null : emission.EmissionStableId,
                    emission == null ? string.Empty : emission.Fingerprint,
                    "enemy-pattern-realizer-exception:"
                        + exception.GetType().Name);
            }
        }

        private EnemyAttackPatternRealizationResult TryCancelSafely(
            EnemyAttackSequenceCancellationFact cancellation,
            EnemyAttackEffectEmission emission)
        {
            try
            {
                return realizer.TryCancelActiveWindow(cancellation, emission)
                    ?? new EnemyAttackPatternRealizationResult(
                        EnemyAttackPatternRealizationStatus.RetryableFailure,
                        cancellation == null ? null : cancellation.CancellationStableId,
                        emission == null ? null : emission.EmissionStableId,
                        emission == null ? string.Empty : emission.Fingerprint,
                        "enemy-pattern-cancellation-realizer-null-result");
            }
            catch (Exception exception)
            {
                return new EnemyAttackPatternRealizationResult(
                    EnemyAttackPatternRealizationStatus.RetryableFailure,
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
            foreach (KeyValuePair<StableId, EnemyAttackEffectEmission> pair
                in activeMeleeWindows)
            {
                if (pair.Value.ActiveUntilSeconds <= now)
                    elapsed.Add(pair.Key);
            }
            for (int index = 0; index < elapsed.Count; index++)
                activeMeleeWindows.Remove(elapsed[index]);
        }

        private static bool CancellationMatches(
            EnemyAttackSequenceCancellationFact cancellation,
            EnemyAttackEffectEmission emission)
        {
            return cancellation.SourceEntityStableId == emission.SourceEntityStableId
                && cancellation.SourceLifecycleGeneration
                    == emission.SourceLifecycleGeneration;
        }

        private void RecordForEmission(
            EnemyAttackEffectEmission emission,
            EnemyAttackPatternLiveState state,
            string detail)
        {
            SequenceState sequence;
            if (emission != null
                && sequences.TryGetValue(emission.SequenceStableId, out sequence))
                Record(sequence.Dispatch, emission, state, detail);
        }

        private void Record(
            EnemyAttackSequenceDispatch dispatch,
            EnemyAttackEffectEmission emission,
            EnemyAttackPatternLiveState state,
            string detail)
        {
            records.Add(new EnemyAttackPatternLiveRecord(
                dispatch.DispatchStableId,
                emission == null ? null : emission.EmissionStableId,
                emission == null ? dispatch.Fingerprint : emission.Fingerprint,
                state,
                runTime.CurrentTimeSeconds,
                detail));
        }
    }
}

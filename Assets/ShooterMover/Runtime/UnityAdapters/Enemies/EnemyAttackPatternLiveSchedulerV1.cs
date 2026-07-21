using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Supplies canonical Run Session time and validates that an emission still belongs to the
    /// active run/lifecycle. Unity Update may wake the scheduler, but it never advances this clock.
    /// </summary>
    public interface IEnemyAttackPatternRunTimeV1
    {
        double CurrentTimeSeconds { get; }

        bool IsCurrent(EnemyAttackExecutionRequestV1 execution);
    }

    /// <summary>
    /// Narrow realization boundary. Implementations adapt immutable projectile/melee facts to the
    /// existing projectile and Combat Hit Policy paths. Validate must not mutate presentation or
    /// combat state; Realize is called only after an entire sequence has been accepted atomically.
    /// </summary>
    public interface IEnemyAttackPatternEmissionRealizerV1
    {
        bool CanRealize(EnemyAttackEffectEmissionV1 emission, out string rejectionCode);

        void Realize(EnemyAttackEffectEmissionV1 emission);

        void CancelActiveWindow(EnemyAttackEffectEmissionV1 emission);
    }

    public enum EnemyAttackPatternLiveStateV1
    {
        Committed = 1,
        Emitted = 2,
        Cancelled = 3,
        Rejected = 4,
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

    /// <summary>
    /// Production-safe atomic sequence queue. It consumes schema-v2 dispatch/cancellation facts,
    /// compares ScheduledAtSeconds to authoritative Run Session time, and realizes every emission
    /// at most once in deterministic timestamp/identity order.
    /// </summary>
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
        private readonly IEnemyAttackPatternEmissionRealizerV1 realizer;
        private readonly Dictionary<StableId, SequenceState> sequences =
            new Dictionary<StableId, SequenceState>();
        private readonly Dictionary<StableId, string> acceptedFingerprints =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, string> cancellationFingerprints =
            new Dictionary<StableId, string>();
        private readonly HashSet<StableId> emitted = new HashSet<StableId>();
        private readonly List<EnemyAttackPatternLiveRecordV1> records =
            new List<EnemyAttackPatternLiveRecordV1>();

        public EnemyAttackPatternLiveSchedulerV1(
            IEnemyAttackPatternRunTimeV1 runTime,
            IEnemyAttackPatternEmissionRealizerV1 realizer)
        {
            this.runTime = runTime ?? throw new ArgumentNullException(nameof(runTime));
            this.realizer = realizer ?? throw new ArgumentNullException(nameof(realizer));
        }

        public IReadOnlyList<EnemyAttackPatternLiveRecordV1> Records
        {
            get { return new ReadOnlyCollection<EnemyAttackPatternLiveRecordV1>(records.ToArray()); }
        }

        public EnemyAttackPatternDispatchResultV1 Dispatch(EnemyAttackSequenceDispatchV1 sequence)
        {
            if (sequence == null)
            {
                StableId invalid = StableId.Create("enemy-attack-sequence", "runtime-invalid-dispatch");
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    invalid,
                    "invalid-dispatch",
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            string existing;
            if (acceptedFingerprints.TryGetValue(sequence.DispatchStableId, out existing))
            {
                if (string.Equals(existing, sequence.Fingerprint, StringComparison.Ordinal))
                    return EnemyAttackPatternDispatchResultV1.ExactReplay(
                        sequence.DispatchStableId,
                        sequence.Fingerprint);
                Record(sequence, null, EnemyAttackPatternLiveStateV1.Rejected, "conflicting-sequence-replay");
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    sequence.DispatchStableId,
                    sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.ConflictingDuplicate);
            }

            if (!runTime.IsCurrent(sequence.Execution))
            {
                Record(sequence, null, EnemyAttackPatternLiveStateV1.Rejected, "wrong-run-or-lifecycle");
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    sequence.DispatchStableId,
                    sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            // Atomic preflight: no queue or effect mutation occurs until every emission validates.
            for (int index = 0; index < sequence.Emissions.Count; index++)
            {
                string rejection;
                if (!realizer.CanRealize(sequence.Emissions[index], out rejection))
                {
                    Record(sequence, sequence.Emissions[index], EnemyAttackPatternLiveStateV1.Rejected,
                        string.IsNullOrEmpty(rejection) ? "emission-preflight-rejected" : rejection);
                    return EnemyAttackPatternDispatchResultV1.Rejected(
                        sequence.DispatchStableId,
                        sequence.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.DownstreamFailure);
                }
            }

            acceptedFingerprints.Add(sequence.DispatchStableId, sequence.Fingerprint);
            sequences.Add(sequence.DispatchStableId, new SequenceState(sequence));
            for (int index = 0; index < sequence.Emissions.Count; index++)
                Record(sequence, sequence.Emissions[index], EnemyAttackPatternLiveStateV1.Committed, string.Empty);
            return EnemyAttackPatternDispatchResultV1.Applied(
                sequence.DispatchStableId,
                sequence.Fingerprint);
        }

        public EnemyAttackPatternDispatchResultV1 Cancel(
            EnemyAttackSequenceCancellationFactV1 cancellation)
        {
            if (cancellation == null)
            {
                StableId invalid = StableId.Create("enemy-attack-cancellation", "runtime-invalid-cancellation");
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    invalid,
                    "invalid-cancellation",
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            string existing;
            if (cancellationFingerprints.TryGetValue(cancellation.CancellationStableId, out existing))
            {
                if (string.Equals(existing, cancellation.Fingerprint, StringComparison.Ordinal))
                    return EnemyAttackPatternDispatchResultV1.ExactReplay(
                        cancellation.CancellationStableId,
                        cancellation.Fingerprint);
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    cancellation.CancellationStableId,
                    cancellation.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.ConflictingDuplicate);
            }

            cancellationFingerprints.Add(cancellation.CancellationStableId, cancellation.Fingerprint);
            var projectileIds = new HashSet<StableId>(cancellation.CancelledProjectileStableIds);
            var meleeIds = new HashSet<StableId>(cancellation.CancelledMeleeStrikeStableIds);
            foreach (SequenceState state in sequences.Values)
            {
                for (int index = state.Pending.Count - 1; index >= 0; index--)
                {
                    EnemyAttackEffectEmissionV1 emission = state.Pending[index];
                    bool cancel = projectileIds.Contains(emission.EmissionStableId)
                        || meleeIds.Contains(emission.EmissionStableId);
                    if (!cancel) continue;
                    state.Pending.RemoveAt(index);
                    if (emission.Kind == EnemyAttackEffectEmissionKindV1.MeleeStrike
                        && emitted.Contains(emission.EmissionStableId))
                        realizer.CancelActiveWindow(emission);
                    Record(state.Dispatch, emission, EnemyAttackPatternLiveStateV1.Cancelled, string.Empty);
                }
            }
            return EnemyAttackPatternDispatchResultV1.Applied(
                cancellation.CancellationStableId,
                cancellation.Fingerprint);
        }

        public void Tick()
        {
            double now = runTime.CurrentTimeSeconds;
            var due = new List<Tuple<EnemyAttackSequenceDispatchV1, EnemyAttackEffectEmissionV1>>();
            foreach (SequenceState state in sequences.Values)
            {
                if (!runTime.IsCurrent(state.Dispatch.Execution)) continue;
                for (int index = 0; index < state.Pending.Count; index++)
                {
                    EnemyAttackEffectEmissionV1 emission = state.Pending[index];
                    if (emission.ScheduledAtSeconds <= now)
                        due.Add(Tuple.Create(state.Dispatch, emission));
                }
            }
            due.Sort((left, right) =>
            {
                int time = left.Item2.ScheduledAtSeconds.CompareTo(right.Item2.ScheduledAtSeconds);
                return time != 0 ? time : left.Item2.EmissionStableId.CompareTo(right.Item2.EmissionStableId);
            });

            for (int index = 0; index < due.Count; index++)
            {
                EnemyAttackSequenceDispatchV1 dispatch = due[index].Item1;
                EnemyAttackEffectEmissionV1 emission = due[index].Item2;
                if (!emitted.Add(emission.EmissionStableId)) continue;
                SequenceState state = sequences[dispatch.DispatchStableId];
                state.Pending.Remove(emission);
                realizer.Realize(emission);
                Record(dispatch, emission, EnemyAttackPatternLiveStateV1.Emitted, string.Empty);
            }
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

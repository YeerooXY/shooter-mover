using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Diagnostics
{
    public enum RunInvalidityReason
    {
        DuplicateRunStart = 1,
        RunEndWithoutStart = 2,
        DuplicateRunEnd = 3,
        EventAfterRunEnd = 4,
        MissingRunStart = 5,
        MissingRunEnd = 6,
        CrashBeforeRunEnd = 7,
        RunAborted = 8,
        InvalidatingDiagnosticCommand = 9,
        FaultInjectionUsed = 10,
        MissionStateOverrideUsed = 11,
        ProgressionOverrideUsed = 12,
        PerformanceBudgetBreach = 13,
        UnhandledException = 14,
        Timeout = 15,
        MissingRequiredAsset = 16,
        DiagnosticsCapacityReached = 17,
    }

    public enum RunTechnicalValidityState
    {
        Valid = 1,
        Invalid = 2,
    }

    public enum HumanFunEvidenceOutcome
    {
        NotRecorded = 1,
        Positive = 2,
        Mixed = 3,
        Negative = 4,
    }

    /// <summary>
    /// Immutable manually supplied fun/behavior evidence. The contract contains
    /// no classifier and never derives this outcome from technical diagnostics.
    /// </summary>
    public sealed class HumanFunEvidence : IEquatable<HumanFunEvidence>
    {
        public HumanFunEvidence(HumanFunEvidenceOutcome outcome, StableId observationCode = null)
        {
            if (outcome < HumanFunEvidenceOutcome.NotRecorded
                || outcome > HumanFunEvidenceOutcome.Negative)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome,
                    "Unknown human fun evidence outcome.");
            }

            if (outcome == HumanFunEvidenceOutcome.NotRecorded && observationCode != null)
            {
                throw new ArgumentException(
                    "Unrecorded fun evidence cannot carry an observation code.",
                    nameof(observationCode));
            }

            Outcome = outcome;
            ObservationCode = observationCode;
        }

        public HumanFunEvidenceOutcome Outcome { get; }

        public StableId ObservationCode { get; }

        public bool IsRecorded
        {
            get { return Outcome != HumanFunEvidenceOutcome.NotRecorded; }
        }

        public static HumanFunEvidence NotRecorded
        {
            get { return new HumanFunEvidence(HumanFunEvidenceOutcome.NotRecorded); }
        }

        public string ToCanonicalString()
        {
            return "human_fun_outcome="
                + RunValidityContractFormat.HumanFunOutcomeToken(Outcome)
                + "\nhuman_observation_code="
                + (ObservationCode == null ? "null" : ObservationCode.ToString());
        }

        public bool Equals(HumanFunEvidence other)
        {
            if (ReferenceEquals(other, null) || Outcome != other.Outcome)
            {
                return false;
            }

            if (ReferenceEquals(ObservationCode, other.ObservationCode))
            {
                return true;
            }

            return ObservationCode != null
                && other.ObservationCode != null
                && ObservationCode.Equals(other.ObservationCode);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as HumanFunEvidence);
        }

        public override int GetHashCode()
        {
            return DiagnosticsContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    /// <summary>
    /// Final immutable technical validity. Invalidity reasons are canonical,
    /// distinct, and cannot be removed by later events.
    /// </summary>
    public sealed class RunTechnicalValidity : IEquatable<RunTechnicalValidity>
    {
        private readonly RunInvalidityReason[] _reasons;

        internal RunTechnicalValidity(StableId runId, IEnumerable<RunInvalidityReason> reasons)
        {
            RunId = DiagnosticsContractFormat.RequireNotNull(runId, nameof(runId));
            _reasons = CanonicalizeReasons(reasons);
            State = _reasons.Length == 0
                ? RunTechnicalValidityState.Valid
                : RunTechnicalValidityState.Invalid;
        }

        public StableId RunId { get; }

        public RunTechnicalValidityState State { get; }

        public bool IsTechnicallyValid
        {
            get { return State == RunTechnicalValidityState.Valid; }
        }

        public IReadOnlyList<RunInvalidityReason> Reasons
        {
            get { return Array.AsReadOnly(_reasons); }
        }

        public bool Contains(RunInvalidityReason reason)
        {
            RunValidityContractFormat.RequireKnownInvalidityReason(reason);
            return Array.IndexOf(_reasons, reason) >= 0;
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("run_id=");
            builder.Append(RunId);
            builder.Append("\ntechnical_validity=");
            builder.Append(IsTechnicallyValid ? "valid" : "invalid");
            builder.Append("\ninvalidity_reason_count=");
            builder.Append(_reasons.Length.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < _reasons.Length; index++)
            {
                builder.Append("\ninvalidity_reason[");
                builder.Append(index.ToString(CultureInfo.InvariantCulture));
                builder.Append("]=");
                builder.Append(RunValidityContractFormat.InvalidityReasonToken(_reasons[index]));
            }

            return builder.ToString();
        }

        public bool Equals(RunTechnicalValidity other)
        {
            if (ReferenceEquals(other, null)
                || !RunId.Equals(other.RunId)
                || State != other.State
                || _reasons.Length != other._reasons.Length)
            {
                return false;
            }

            for (int index = 0; index < _reasons.Length; index++)
            {
                if (_reasons[index] != other._reasons[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RunTechnicalValidity);
        }

        public override int GetHashCode()
        {
            return DiagnosticsContractFormat.DeterministicHash(ToCanonicalString());
        }

        private static RunInvalidityReason[] CanonicalizeReasons(
            IEnumerable<RunInvalidityReason> reasons)
        {
            if (reasons == null)
            {
                throw new ArgumentNullException(nameof(reasons));
            }

            RunInvalidityReason[] values = reasons.Distinct().ToArray();
            for (int index = 0; index < values.Length; index++)
            {
                RunValidityContractFormat.RequireKnownInvalidityReason(values[index]);
            }

            Array.Sort(values);
            return values;
        }
    }

    /// <summary>
    /// Immutable accumulator for ordered diagnostic events. Applying an event
    /// returns a new value; previously accumulated invalidity is never cleared.
    /// </summary>
    public sealed class RunValidityAccumulator : IEquatable<RunValidityAccumulator>
    {
        private readonly RunInvalidityReason[] _reasons;

        private RunValidityAccumulator(
            StableId runId,
            bool hasStarted,
            bool hasEnded,
            bool hasCrashed,
            long lastSequence,
            IEnumerable<RunInvalidityReason> reasons)
        {
            RunId = DiagnosticsContractFormat.RequireNotNull(runId, nameof(runId));
            HasStarted = hasStarted;
            HasEnded = hasEnded;
            HasCrashed = hasCrashed;
            LastSequence = lastSequence;
            _reasons = reasons.Distinct().OrderBy(value => value).ToArray();
        }

        public StableId RunId { get; }

        public bool HasStarted { get; }

        public bool HasEnded { get; }

        public bool HasCrashed { get; }

        public long LastSequence { get; }

        public IReadOnlyList<RunInvalidityReason> AccumulatedReasons
        {
            get { return Array.AsReadOnly(_reasons); }
        }

        public static RunValidityAccumulator Empty(StableId runId)
        {
            return new RunValidityAccumulator(
                DiagnosticsContractFormat.RequireNotNull(runId, nameof(runId)),
                false,
                false,
                false,
                0L,
                new RunInvalidityReason[0]);
        }

        public RunValidityAccumulator Apply(DiagnosticEventEnvelope diagnosticEvent)
        {
            DiagnosticEventEnvelope current = DiagnosticsContractFormat.RequireNotNull(
                diagnosticEvent,
                nameof(diagnosticEvent));

            if (!RunId.Equals(current.RunId))
            {
                throw new ArgumentException(
                    "Run validity can consume events only for its own run ID.",
                    nameof(diagnosticEvent));
            }

            if (current.Sequence.Value <= LastSequence)
            {
                throw new ArgumentException(
                    "Run validity events must be applied in strictly increasing sequence order.",
                    nameof(diagnosticEvent));
            }

            bool hasStarted = HasStarted;
            bool hasEnded = HasEnded;
            bool hasCrashed = HasCrashed;
            HashSet<RunInvalidityReason> reasons = new HashSet<RunInvalidityReason>(_reasons);

            if (hasEnded && current.EventKind != DiagnosticEventKind.EvidenceValidity)
            {
                reasons.Add(RunInvalidityReason.EventAfterRunEnd);
            }

            switch (current.EventKind)
            {
                case DiagnosticEventKind.RunStarted:
                    if (hasStarted)
                    {
                        reasons.Add(RunInvalidityReason.DuplicateRunStart);
                    }
                    else
                    {
                        hasStarted = true;
                    }
                    break;

                case DiagnosticEventKind.RunEnded:
                    if (!hasStarted)
                    {
                        reasons.Add(RunInvalidityReason.RunEndWithoutStart);
                    }

                    if (hasEnded)
                    {
                        reasons.Add(RunInvalidityReason.DuplicateRunEnd);
                    }

                    hasEnded = true;
                    break;

                case DiagnosticEventKind.Crash:
                    hasCrashed = true;
                    break;
            }

            RunInvalidityReason? payloadReason = current.Payload.InvalidityReason;
            if (payloadReason.HasValue)
            {
                reasons.Add(payloadReason.Value);
            }

            return new RunValidityAccumulator(
                RunId,
                hasStarted,
                hasEnded,
                hasCrashed,
                current.Sequence.Value,
                reasons);
        }

        public RunTechnicalValidity FinalizeForEvidence()
        {
            HashSet<RunInvalidityReason> reasons = new HashSet<RunInvalidityReason>(_reasons);

            if (!HasStarted)
            {
                reasons.Add(RunInvalidityReason.MissingRunStart);
            }

            if (!HasEnded)
            {
                reasons.Add(
                    HasCrashed
                        ? RunInvalidityReason.CrashBeforeRunEnd
                        : RunInvalidityReason.MissingRunEnd);
            }

            return new RunTechnicalValidity(RunId, reasons);
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("run_id=");
            builder.Append(RunId);
            builder.Append("\nhas_started=");
            builder.Append(DiagnosticsContractFormat.BooleanToken(HasStarted));
            builder.Append("\nhas_ended=");
            builder.Append(DiagnosticsContractFormat.BooleanToken(HasEnded));
            builder.Append("\nhas_crashed=");
            builder.Append(DiagnosticsContractFormat.BooleanToken(HasCrashed));
            builder.Append("\nlast_sequence=");
            builder.Append(LastSequence.ToString(CultureInfo.InvariantCulture));
            builder.Append("\naccumulated_reason_count=");
            builder.Append(_reasons.Length.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < _reasons.Length; index++)
            {
                builder.Append("\naccumulated_reason[");
                builder.Append(index.ToString(CultureInfo.InvariantCulture));
                builder.Append("]=");
                builder.Append(RunValidityContractFormat.InvalidityReasonToken(_reasons[index]));
            }

            return builder.ToString();
        }

        public bool Equals(RunValidityAccumulator other)
        {
            if (ReferenceEquals(other, null)
                || !RunId.Equals(other.RunId)
                || HasStarted != other.HasStarted
                || HasEnded != other.HasEnded
                || HasCrashed != other.HasCrashed
                || LastSequence != other.LastSequence
                || _reasons.Length != other._reasons.Length)
            {
                return false;
            }

            for (int index = 0; index < _reasons.Length; index++)
            {
                if (_reasons[index] != other._reasons[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RunValidityAccumulator);
        }

        public override int GetHashCode()
        {
            return DiagnosticsContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    /// <summary>
    /// Combines independent technical and human evidence without allowing either
    /// side to rewrite or classify the other.
    /// </summary>
    public sealed class EvidenceAssessment : IEquatable<EvidenceAssessment>
    {
        public EvidenceAssessment(
            RunTechnicalValidity technicalValidity,
            HumanFunEvidence humanFunEvidence)
        {
            TechnicalValidity = DiagnosticsContractFormat.RequireNotNull(
                technicalValidity,
                nameof(technicalValidity));
            HumanFunEvidence = DiagnosticsContractFormat.RequireNotNull(
                humanFunEvidence,
                nameof(humanFunEvidence));
        }

        public RunTechnicalValidity TechnicalValidity { get; }

        public HumanFunEvidence HumanFunEvidence { get; }

        public string ToCanonicalString()
        {
            return TechnicalValidity.ToCanonicalString()
                + "\n"
                + HumanFunEvidence.ToCanonicalString();
        }

        public bool Equals(EvidenceAssessment other)
        {
            return !ReferenceEquals(other, null)
                && TechnicalValidity.Equals(other.TechnicalValidity)
                && HumanFunEvidence.Equals(other.HumanFunEvidence);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EvidenceAssessment);
        }

        public override int GetHashCode()
        {
            return DiagnosticsContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    /// <summary>
    /// Diagnostic event payload that records the already-derived technical
    /// evidence validity. It does not itself change validity.
    /// </summary>
    public sealed class EvidenceValidityDiagnosticPayload : DiagnosticEventPayload
    {
        public EvidenceValidityDiagnosticPayload(RunTechnicalValidity technicalValidity)
        {
            TechnicalValidity = DiagnosticsContractFormat.RequireNotNull(
                technicalValidity,
                nameof(technicalValidity));
        }

        public RunTechnicalValidity TechnicalValidity { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.EvidenceValidity; }
        }

        public override string ToCanonicalString()
        {
            return TechnicalValidity.ToCanonicalString();
        }
    }

    internal static class RunValidityContractFormat
    {
        public static void RequireKnownInvalidityReason(RunInvalidityReason value)
        {
            if (value < RunInvalidityReason.DuplicateRunStart
                || value > RunInvalidityReason.DiagnosticsCapacityReached)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unknown run invalidity reason.");
            }
        }

        public static string InvalidityReasonToken(RunInvalidityReason value)
        {
            switch (value)
            {
                case RunInvalidityReason.DuplicateRunStart: return "duplicate-run-start";
                case RunInvalidityReason.RunEndWithoutStart: return "run-end-without-start";
                case RunInvalidityReason.DuplicateRunEnd: return "duplicate-run-end";
                case RunInvalidityReason.EventAfterRunEnd: return "event-after-run-end";
                case RunInvalidityReason.MissingRunStart: return "missing-run-start";
                case RunInvalidityReason.MissingRunEnd: return "missing-run-end";
                case RunInvalidityReason.CrashBeforeRunEnd: return "crash-before-run-end";
                case RunInvalidityReason.RunAborted: return "run-aborted";
                case RunInvalidityReason.InvalidatingDiagnosticCommand:
                    return "invalidating-diagnostic-command";
                case RunInvalidityReason.FaultInjectionUsed: return "fault-injection-used";
                case RunInvalidityReason.MissionStateOverrideUsed:
                    return "mission-state-override-used";
                case RunInvalidityReason.ProgressionOverrideUsed:
                    return "progression-override-used";
                case RunInvalidityReason.PerformanceBudgetBreach:
                    return "performance-budget-breach";
                case RunInvalidityReason.UnhandledException: return "unhandled-exception";
                case RunInvalidityReason.Timeout: return "timeout";
                case RunInvalidityReason.MissingRequiredAsset: return "missing-required-asset";
                case RunInvalidityReason.DiagnosticsCapacityReached:
                    return "diagnostics-capacity-reached";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "Unknown run invalidity reason.");
            }
        }

        public static string HumanFunOutcomeToken(HumanFunEvidenceOutcome value)
        {
            switch (value)
            {
                case HumanFunEvidenceOutcome.NotRecorded: return "not-recorded";
                case HumanFunEvidenceOutcome.Positive: return "positive";
                case HumanFunEvidenceOutcome.Mixed: return "mixed";
                case HumanFunEvidenceOutcome.Negative: return "negative";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "Unknown human fun evidence outcome.");
            }
        }
    }
}

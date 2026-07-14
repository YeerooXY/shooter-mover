using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Diagnostics;
using ShooterMover.Domain.Common;

namespace ShooterMover.TestSupport.EvidenceHarness
{
    public enum EvidenceSessionState
    {
        Configured = 1,
        Starting = 2,
        Running = 3,
        Restarting = 4,
        Ending = 5,
        Ended = 6,
        Invalid = 7
    }

    public enum EvidenceSessionTransitionDisposition
    {
        Applied = 1,
        NoChange = 2,
        Rejected = 3
    }

    public enum EvidenceSessionOperation
    {
        Configure = 1,
        BeginStart = 2,
        CompleteStart = 3,
        BeginRestart = 4,
        CompleteRestart = 5,
        BeginEnd = 6,
        CompleteEnd = 7,
        Invalidate = 8
    }

    /// <summary>
    /// Immutable identity of the canonical Stage 1 evidence start. It binds the
    /// EH-002 configuration to the accepted EH-004 player socket and EH-005 route
    /// start marker without retaining scene-object identity.
    /// </summary>
    public sealed class EvidenceSessionStartIdentity : IEquatable<EvidenceSessionStartIdentity>
    {
        public const string Stage1ArenaStartMarkerId = "socket.player.primary";
        public const string Stage1RouteStartMarkerId = "route.start";

        private EvidenceSessionStartIdentity(
            string configurationFingerprint,
            string identityReference,
            int runSeed,
            int intentFixtureVersion,
            string arenaStartMarkerId,
            string routeStartMarkerId)
        {
            ConfigurationFingerprint = RequireText(
                configurationFingerprint,
                nameof(configurationFingerprint));
            IdentityReference = RequireText(identityReference, nameof(identityReference));
            RunSeed = runSeed;
            IntentFixtureVersion = intentFixtureVersion;
            ArenaStartMarkerId = RequireText(arenaStartMarkerId, nameof(arenaStartMarkerId));
            RouteStartMarkerId = RequireText(routeStartMarkerId, nameof(routeStartMarkerId));
        }

        public string ConfigurationFingerprint { get; }

        public string IdentityReference { get; }

        public int RunSeed { get; }

        public int IntentFixtureVersion { get; }

        public string ArenaStartMarkerId { get; }

        public string RouteStartMarkerId { get; }

        public static EvidenceSessionStartIdentity FromConfiguration(
            EvidenceRunConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return new EvidenceSessionStartIdentity(
                configuration.Fingerprint,
                configuration.IdentityReference,
                configuration.RunSeed,
                configuration.IntentFixtureVersion,
                Stage1ArenaStartMarkerId,
                Stage1RouteStartMarkerId);
        }

        public string ToCanonicalString()
        {
            return "configuration_fingerprint=" + ConfigurationFingerprint
                + "\nidentity_reference=" + IdentityReference
                + "\nrun_seed=" + RunSeed.ToString(CultureInfo.InvariantCulture)
                + "\nintent_fixture_version="
                + IntentFixtureVersion.ToString(CultureInfo.InvariantCulture)
                + "\narena_start_marker=" + ArenaStartMarkerId
                + "\nroute_start_marker=" + RouteStartMarkerId;
        }

        public bool Equals(EvidenceSessionStartIdentity other)
        {
            return other != null
                && string.Equals(
                    ConfigurationFingerprint,
                    other.ConfigurationFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    IdentityReference,
                    other.IdentityReference,
                    StringComparison.Ordinal)
                && RunSeed == other.RunSeed
                && IntentFixtureVersion == other.IntentFixtureVersion
                && string.Equals(
                    ArenaStartMarkerId,
                    other.ArenaStartMarkerId,
                    StringComparison.Ordinal)
                && string.Equals(
                    RouteStartMarkerId,
                    other.RouteStartMarkerId,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EvidenceSessionStartIdentity);
        }

        public override int GetHashCode()
        {
            return DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A canonical start identity value is required.", parameterName);
            }

            return value;
        }

        private static int DeterministicHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }
    }

    /// <summary>
    /// One immutable attempt in a parent evidence session. Restarts preserve the
    /// parent session and canonical start identity while issuing a fresh attempt ID.
    /// </summary>
    public sealed class EvidenceSessionAttemptIdentity
    {
        internal EvidenceSessionAttemptIdentity(
            StableId sessionId,
            StableId attemptId,
            StableId parentAttemptId,
            int ordinal,
            EvidenceSessionStartIdentity startIdentity)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            AttemptId = attemptId ?? throw new ArgumentNullException(nameof(attemptId));
            ParentAttemptId = parentAttemptId;
            if (ordinal < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }

            Ordinal = ordinal;
            StartIdentity = startIdentity ?? throw new ArgumentNullException(nameof(startIdentity));
        }

        public StableId SessionId { get; }

        public StableId AttemptId { get; }

        public StableId ParentAttemptId { get; }

        public int Ordinal { get; }

        public EvidenceSessionStartIdentity StartIdentity { get; }

        public string ToCanonicalString()
        {
            return "session_id=" + SessionId
                + "\nattempt_id=" + AttemptId
                + "\nparent_attempt_id="
                + (ParentAttemptId == null ? "null" : ParentAttemptId.ToString())
                + "\nattempt_ordinal=" + Ordinal.ToString(CultureInfo.InvariantCulture)
                + "\nstart_identity:\n" + StartIdentity.ToCanonicalString();
        }
    }

    public sealed class EvidenceSessionAuditEntry
    {
        internal EvidenceSessionAuditEntry(
            long sequence,
            EvidenceSessionOperation operation,
            EvidenceSessionTransitionDisposition disposition,
            EvidenceSessionState previousState,
            EvidenceSessionState nextState,
            StableId sessionId,
            StableId attemptId,
            StableId parentAttemptId,
            string reasonCode,
            DiagnosticEventKind? diagnosticEventKind,
            string diagnosticPayload)
        {
            Sequence = sequence;
            Operation = operation;
            Disposition = disposition;
            PreviousState = previousState;
            NextState = nextState;
            SessionId = sessionId;
            AttemptId = attemptId;
            ParentAttemptId = parentAttemptId;
            ReasonCode = reasonCode;
            DiagnosticEventKind = diagnosticEventKind;
            DiagnosticPayload = diagnosticPayload;
        }

        public long Sequence { get; }

        public EvidenceSessionOperation Operation { get; }

        public EvidenceSessionTransitionDisposition Disposition { get; }

        public EvidenceSessionState PreviousState { get; }

        public EvidenceSessionState NextState { get; }

        public StableId SessionId { get; }

        public StableId AttemptId { get; }

        public StableId ParentAttemptId { get; }

        public string ReasonCode { get; }

        public DiagnosticEventKind? DiagnosticEventKind { get; }

        public string DiagnosticPayload { get; }

        public string ToCanonicalString()
        {
            return "audit_sequence=" + Sequence.ToString(CultureInfo.InvariantCulture)
                + "|operation=" + Operation
                + "|disposition=" + Disposition
                + "|state=" + PreviousState + "->" + NextState
                + "|session_id=" + SessionId
                + "|attempt_id=" + AttemptId
                + "|parent_attempt_id="
                + (ParentAttemptId == null ? "null" : ParentAttemptId.ToString())
                + "|reason=" + (ReasonCode ?? "none")
                + "|diagnostic_kind="
                + (DiagnosticEventKind.HasValue
                    ? DiagnosticEventKind.Value.ToString()
                    : "none")
                + "|diagnostic_payload="
                + CanonicalizeInline(DiagnosticPayload);
        }

        private static string CanonicalizeInline(string value)
        {
            return value == null
                ? "none"
                : value.Replace("\r", string.Empty).Replace("\n", ";");
        }
    }

    public sealed class EvidenceSessionTransition
    {
        internal EvidenceSessionTransition(EvidenceSessionAuditEntry auditEntry)
        {
            AuditEntry = auditEntry ?? throw new ArgumentNullException(nameof(auditEntry));
        }

        public EvidenceSessionAuditEntry AuditEntry { get; }

        public EvidenceSessionTransitionDisposition Disposition
        {
            get { return AuditEntry.Disposition; }
        }

        public EvidenceSessionState PreviousState
        {
            get { return AuditEntry.PreviousState; }
        }

        public EvidenceSessionState NextState
        {
            get { return AuditEntry.NextState; }
        }

        public bool WasApplied
        {
            get { return Disposition == EvidenceSessionTransitionDisposition.Applied; }
        }

        public bool WasRejected
        {
            get { return Disposition == EvidenceSessionTransitionDisposition.Rejected; }
        }

        public string ToCanonicalString()
        {
            return AuditEntry.ToCanonicalString();
        }
    }

    /// <summary>
    /// Deterministic evidence-session state machine. It owns no scene, save,
    /// gameplay, progression, scoring, or wall-clock state.
    /// </summary>
    public sealed class EvidenceSessionLifecycle
    {
        private readonly List<EvidenceSessionAuditEntry> auditTrail =
            new List<EvidenceSessionAuditEntry>();
        private readonly HashSet<string> issuedAttemptIds =
            new HashSet<string>(StringComparer.Ordinal);

        private EvidenceSessionState state;
        private EvidenceSessionAttemptIdentity currentAttempt;
        private EvidenceSessionAttemptIdentity pendingAttempt;
        private long nextAuditSequence = 1L;

        private EvidenceSessionLifecycle(
            EvidenceRunConfiguration configuration,
            StableId sessionId,
            StableId initialAttemptId)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            StartIdentity = EvidenceSessionStartIdentity.FromConfiguration(configuration);
            currentAttempt = new EvidenceSessionAttemptIdentity(
                sessionId,
                initialAttemptId,
                null,
                1,
                StartIdentity);
            issuedAttemptIds.Add(initialAttemptId.ToString());
            state = EvidenceSessionState.Configured;
            Record(
                EvidenceSessionOperation.Configure,
                EvidenceSessionTransitionDisposition.Applied,
                EvidenceSessionState.Configured,
                EvidenceSessionState.Configured,
                currentAttempt,
                null,
                null,
                null);
        }

        public EvidenceRunConfiguration Configuration { get; }

        public EvidenceSessionStartIdentity StartIdentity { get; }

        public EvidenceSessionState State
        {
            get { return state; }
        }

        public EvidenceSessionAttemptIdentity CurrentAttempt
        {
            get { return currentAttempt; }
        }

        public EvidenceSessionAttemptIdentity PendingAttempt
        {
            get { return pendingAttempt; }
        }

        public IReadOnlyList<EvidenceSessionAuditEntry> AuditTrail
        {
            get { return Array.AsReadOnly(auditTrail.ToArray()); }
        }

        public static EvidenceSessionLifecycle ConfigureFromCanonicalJson(
            string canonicalConfiguration,
            string sessionId,
            string initialAttemptId)
        {
            EvidenceRunConfigurationLoadResult result =
                EvidenceRunConfigurationLoader.Load(canonicalConfiguration);
            if (!result.IsValid)
            {
                throw new InvalidOperationException(
                    "EH-002 evidence configuration was rejected: "
                    + result.ErrorCode + ": " + result.ErrorMessage);
            }

            return Configure(result.Configuration, sessionId, initialAttemptId);
        }

        public static EvidenceSessionLifecycle Configure(
            EvidenceRunConfiguration configuration,
            string sessionId,
            string initialAttemptId)
        {
            return new EvidenceSessionLifecycle(
                configuration,
                ParseRequiredId(sessionId, nameof(sessionId)),
                ParseRequiredId(initialAttemptId, nameof(initialAttemptId)));
        }

        public EvidenceSessionTransition BeginStart()
        {
            if (state != EvidenceSessionState.Configured)
            {
                return Reject(EvidenceSessionOperation.BeginStart);
            }

            return Apply(
                EvidenceSessionOperation.BeginStart,
                EvidenceSessionState.Starting,
                currentAttempt,
                null,
                null);
        }

        public EvidenceSessionTransition CompleteStart()
        {
            if (state != EvidenceSessionState.Starting)
            {
                return Reject(EvidenceSessionOperation.CompleteStart);
            }

            return Apply(
                EvidenceSessionOperation.CompleteStart,
                EvidenceSessionState.Running,
                currentAttempt,
                DiagnosticEventKind.RunStarted,
                "attempt_id=" + currentAttempt.AttemptId);
        }

        public EvidenceSessionTransition BeginRestart(string nextAttemptId)
        {
            if (state != EvidenceSessionState.Running)
            {
                return Reject(EvidenceSessionOperation.BeginRestart);
            }

            StableId parsedAttemptId;
            try
            {
                parsedAttemptId = ParseRequiredId(nextAttemptId, nameof(nextAttemptId));
            }
            catch (Exception exception)
                when (exception is ArgumentException || exception is FormatException)
            {
                return Reject(
                    EvidenceSessionOperation.BeginRestart,
                    "invalid-next-attempt-id");
            }

            if (!issuedAttemptIds.Add(parsedAttemptId.ToString()))
            {
                return Reject(
                    EvidenceSessionOperation.BeginRestart,
                    "duplicate-attempt-id");
            }

            pendingAttempt = new EvidenceSessionAttemptIdentity(
                currentAttempt.SessionId,
                parsedAttemptId,
                currentAttempt.AttemptId,
                currentAttempt.Ordinal + 1,
                StartIdentity);
            return Apply(
                EvidenceSessionOperation.BeginRestart,
                EvidenceSessionState.Restarting,
                pendingAttempt,
                null,
                null);
        }

        public EvidenceSessionTransition CompleteRestart()
        {
            if (state != EvidenceSessionState.Restarting || pendingAttempt == null)
            {
                return Reject(EvidenceSessionOperation.CompleteRestart);
            }

            EvidenceSessionAttemptIdentity previous = currentAttempt;
            EvidenceSessionAttemptIdentity next = pendingAttempt;
            currentAttempt = next;
            pendingAttempt = null;
            return Apply(
                EvidenceSessionOperation.CompleteRestart,
                EvidenceSessionState.Running,
                next,
                DiagnosticEventKind.RunRestarted,
                new RunRestartedDiagnosticPayload(previous.AttemptId).ToCanonicalString());
        }

        public EvidenceSessionTransition BeginEnd()
        {
            if (state == EvidenceSessionState.Ended || state == EvidenceSessionState.Ending)
            {
                return NoChange(EvidenceSessionOperation.BeginEnd);
            }

            if (state != EvidenceSessionState.Configured
                && state != EvidenceSessionState.Starting
                && state != EvidenceSessionState.Running
                && state != EvidenceSessionState.Restarting
                && state != EvidenceSessionState.Invalid)
            {
                return Reject(EvidenceSessionOperation.BeginEnd);
            }

            pendingAttempt = null;
            return Apply(
                EvidenceSessionOperation.BeginEnd,
                EvidenceSessionState.Ending,
                currentAttempt,
                null,
                null);
        }

        public EvidenceSessionTransition CompleteEnd(RunEndKind endKind)
        {
            if (state == EvidenceSessionState.Ended)
            {
                return NoChange(EvidenceSessionOperation.CompleteEnd);
            }

            if (state != EvidenceSessionState.Ending)
            {
                return Reject(EvidenceSessionOperation.CompleteEnd);
            }

            RunEndedDiagnosticPayload payload;
            try
            {
                payload = new RunEndedDiagnosticPayload(endKind);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Reject(
                    EvidenceSessionOperation.CompleteEnd,
                    "invalid-run-end-kind");
            }

            return Apply(
                EvidenceSessionOperation.CompleteEnd,
                EvidenceSessionState.Ended,
                currentAttempt,
                DiagnosticEventKind.RunEnded,
                payload.ToCanonicalString());
        }

        public EvidenceSessionTransition Invalidate(string reasonCode)
        {
            if (state == EvidenceSessionState.Ended)
            {
                return Reject(EvidenceSessionOperation.Invalidate, "session-already-ended");
            }

            if (state == EvidenceSessionState.Invalid)
            {
                return NoChange(EvidenceSessionOperation.Invalidate);
            }

            StableId errorCode;
            try
            {
                errorCode = ParseRequiredId(reasonCode, nameof(reasonCode));
            }
            catch (Exception exception)
                when (exception is ArgumentException || exception is FormatException)
            {
                return Reject(EvidenceSessionOperation.Invalidate, "invalid-reason-code");
            }

            pendingAttempt = null;
            return Apply(
                EvidenceSessionOperation.Invalidate,
                EvidenceSessionState.Invalid,
                currentAttempt,
                DiagnosticEventKind.Exception,
                new ExceptionDiagnosticPayload(errorCode, false).ToCanonicalString(),
                reasonCode);
        }

        public string CaptureAuditSnapshot()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=shooter-mover.evidence-session-audit\n");
            builder.Append("version=1\n");
            builder.Append("state=");
            builder.Append(state);
            builder.Append("\nsession_id=");
            builder.Append(currentAttempt.SessionId);
            builder.Append("\ncurrent_attempt_id=");
            builder.Append(currentAttempt.AttemptId);
            builder.Append("\ncurrent_attempt_ordinal=");
            builder.Append(currentAttempt.Ordinal.ToString(CultureInfo.InvariantCulture));
            builder.Append("\nconfiguration_fingerprint=");
            builder.Append(Configuration.Fingerprint);
            builder.Append("\naudit_count=");
            builder.Append(auditTrail.Count.ToString(CultureInfo.InvariantCulture));
            builder.Append('\n');

            for (int index = 0; index < auditTrail.Count; index++)
            {
                builder.Append(auditTrail[index].ToCanonicalString());
                builder.Append('\n');
            }

            return builder.ToString();
        }

        private EvidenceSessionTransition Apply(
            EvidenceSessionOperation operation,
            EvidenceSessionState nextState,
            EvidenceSessionAttemptIdentity attempt,
            DiagnosticEventKind? diagnosticEventKind,
            string diagnosticPayload,
            string reasonCode = null)
        {
            EvidenceSessionState previous = state;
            state = nextState;
            return Record(
                operation,
                EvidenceSessionTransitionDisposition.Applied,
                previous,
                nextState,
                attempt,
                reasonCode,
                diagnosticEventKind,
                diagnosticPayload);
        }

        private EvidenceSessionTransition NoChange(EvidenceSessionOperation operation)
        {
            return Record(
                operation,
                EvidenceSessionTransitionDisposition.NoChange,
                state,
                state,
                currentAttempt,
                "idempotent-no-change",
                null,
                null);
        }

        private EvidenceSessionTransition Reject(
            EvidenceSessionOperation operation,
            string reasonCode = null)
        {
            EvidenceSessionState previous = state;
            EvidenceSessionState next = state == EvidenceSessionState.Ended
                ? EvidenceSessionState.Ended
                : EvidenceSessionState.Invalid;
            state = next;
            pendingAttempt = null;
            return Record(
                operation,
                EvidenceSessionTransitionDisposition.Rejected,
                previous,
                next,
                currentAttempt,
                reasonCode ?? ("illegal-" + operation + "-from-" + previous),
                null,
                null);
        }

        private EvidenceSessionTransition Record(
            EvidenceSessionOperation operation,
            EvidenceSessionTransitionDisposition disposition,
            EvidenceSessionState previousState,
            EvidenceSessionState nextState,
            EvidenceSessionAttemptIdentity attempt,
            string reasonCode,
            DiagnosticEventKind? diagnosticEventKind,
            string diagnosticPayload)
        {
            EvidenceSessionAttemptIdentity recordedAttempt = attempt ?? currentAttempt;
            var entry = new EvidenceSessionAuditEntry(
                nextAuditSequence++,
                operation,
                disposition,
                previousState,
                nextState,
                recordedAttempt.SessionId,
                recordedAttempt.AttemptId,
                recordedAttempt.ParentAttemptId,
                reasonCode,
                diagnosticEventKind,
                diagnosticPayload);
            auditTrail.Add(entry);
            return new EvidenceSessionTransition(entry);
        }

        private static StableId ParseRequiredId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A stable identity is required.", parameterName);
            }

            return StableId.Parse(value);
        }
    }
}

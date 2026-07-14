using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Contracts.Identity;
using ShooterMover.Contracts.Mission;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Diagnostics
{
    public enum DiagnosticEventKind
    {
        RunStarted = 1,
        RunEnded = 2,
        RunRestarted = 3,
        DiagnosticCommand = 4,
        PerformanceWarning = 5,
        Exception = 6,
        Timeout = 7,
        MissingAsset = 8,
        Crash = 9,
        CapacityReached = 10,
        EvidenceValidity = 11,
    }

    public enum DiagnosticSeverity
    {
        Information = 1,
        Warning = 2,
        Error = 3,
        Critical = 4,
    }

    public enum DiagnosticCommandKind
    {
        DeterministicSetup = 1,
        Inspection = 2,
        PerformanceCapture = 3,
        FaultInjection = 4,
        MissionStateOverride = 5,
        ProgressionOverride = 6,
    }

    public enum DiagnosticCommandEvidenceEffect
    {
        EvidenceSafe = 1,
        InvalidatesTechnicalEvidence = 2,
    }

    public enum PerformanceMetricKind
    {
        CpuFrameTimeMilliseconds = 1,
        GpuFrameTimeMilliseconds = 2,
        TotalFrameTimeMilliseconds = 3,
        ManagedAllocationBytes = 4,
        MemoryBytes = 5,
        LoadingMilliseconds = 6,
    }

    public enum DiagnosticCapacityDimension
    {
        EventCount = 1,
        PayloadBytes = 2,
        FileBytes = 3,
        RetainedRotations = 4,
    }

    public enum DiagnosticRedactionReason
    {
        PersonalData = 1,
        MachineLocalPath = 2,
        SecretOrCredential = 3,
        ExternalIdentifier = 4,
        UnnecessaryFreeText = 5,
    }

    public enum RunEndKind
    {
        Completed = 1,
        Restarted = 2,
        Aborted = 3,
    }

    public sealed class DiagnosticsSchemaVersion : IEquatable<DiagnosticsSchemaVersion>
    {
        public const int CurrentValue = 1;

        public DiagnosticsSchemaVersion(int value)
        {
            if (value != CurrentValue)
            {
                throw new NotSupportedException(
                    "Diagnostics v1 supports schema version "
                    + CurrentValue.ToString(CultureInfo.InvariantCulture)
                    + " only.");
            }

            Value = value;
        }

        public int Value { get; }

        public static DiagnosticsSchemaVersion Current
        {
            get { return new DiagnosticsSchemaVersion(CurrentValue); }
        }

        public string ToCanonicalString()
        {
            return "diagnostics_schema_version=" + Value.ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(DiagnosticsSchemaVersion other)
        {
            return !ReferenceEquals(other, null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DiagnosticsSchemaVersion);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class DiagnosticBounds : IEquatable<DiagnosticBounds>
    {
        public const int HardMaximumEvents = 10000;
        public const int HardMaximumAttributesPerEvent = 32;
        public const int HardMaximumPublicValueLength = 128;
        public const int HardMaximumSupportBundleItems = 128;
        public const long HardMaximumSupportBundleBytes = 1073741824L;

        public DiagnosticBounds(
            int maxEvents,
            int maxAttributesPerEvent,
            int maxPublicValueLength,
            int maxSupportBundleItems,
            long maxSupportBundleBytes)
        {
            MaxEvents = RequireRange(
                maxEvents,
                1,
                HardMaximumEvents,
                nameof(maxEvents));
            MaxAttributesPerEvent = RequireRange(
                maxAttributesPerEvent,
                0,
                HardMaximumAttributesPerEvent,
                nameof(maxAttributesPerEvent));
            MaxPublicValueLength = RequireRange(
                maxPublicValueLength,
                1,
                HardMaximumPublicValueLength,
                nameof(maxPublicValueLength));
            MaxSupportBundleItems = RequireRange(
                maxSupportBundleItems,
                1,
                HardMaximumSupportBundleItems,
                nameof(maxSupportBundleItems));

            if (maxSupportBundleBytes < 1L
                || maxSupportBundleBytes > HardMaximumSupportBundleBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxSupportBundleBytes),
                    maxSupportBundleBytes,
                    "Support-bundle byte bounds must be positive and within the hard v1 limit.");
            }

            MaxSupportBundleBytes = maxSupportBundleBytes;
        }

        public int MaxEvents { get; }

        public int MaxAttributesPerEvent { get; }

        public int MaxPublicValueLength { get; }

        public int MaxSupportBundleItems { get; }

        public long MaxSupportBundleBytes { get; }

        public string ToCanonicalString()
        {
            return "max_events="
                + MaxEvents.ToString(CultureInfo.InvariantCulture)
                + "\nmax_attributes_per_event="
                + MaxAttributesPerEvent.ToString(CultureInfo.InvariantCulture)
                + "\nmax_public_value_length="
                + MaxPublicValueLength.ToString(CultureInfo.InvariantCulture)
                + "\nmax_support_bundle_items="
                + MaxSupportBundleItems.ToString(CultureInfo.InvariantCulture)
                + "\nmax_support_bundle_bytes="
                + MaxSupportBundleBytes.ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(DiagnosticBounds other)
        {
            return !ReferenceEquals(other, null)
                && MaxEvents == other.MaxEvents
                && MaxAttributesPerEvent == other.MaxAttributesPerEvent
                && MaxPublicValueLength == other.MaxPublicValueLength
                && MaxSupportBundleItems == other.MaxSupportBundleItems
                && MaxSupportBundleBytes == other.MaxSupportBundleBytes;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DiagnosticBounds);
        }

        public override int GetHashCode()
        {
            return DiagnosticsContractFormat.DeterministicHash(ToCanonicalString());
        }

        private static int RequireRange(int value, int minimum, int maximum, string parameterName)
        {
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value is outside the supported Diagnostics v1 bound.");
            }

            return value;
        }
    }

    public sealed class DiagnosticEventSequence :
        IEquatable<DiagnosticEventSequence>,
        IComparable<DiagnosticEventSequence>,
        IComparable
    {
        public DiagnosticEventSequence(long value)
        {
            if (value < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Diagnostic event sequence values must be positive.");
            }

            Value = value;
        }

        public long Value { get; }

        public int CompareTo(DiagnosticEventSequence other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return Value.CompareTo(other.Value);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            DiagnosticEventSequence other = obj as DiagnosticEventSequence;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be a DiagnosticEventSequence.",
                    nameof(obj));
            }

            return CompareTo(other);
        }

        public bool Equals(DiagnosticEventSequence other)
        {
            return !ReferenceEquals(other, null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DiagnosticEventSequence);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class DiagnosticAttribute : IEquatable<DiagnosticAttribute>
    {
        private DiagnosticAttribute(
            StableId key,
            string publicValue,
            DiagnosticRedactionReason? redactionReason)
        {
            Key = DiagnosticsContractFormat.RequireNotNull(key, nameof(key));
            PublicValue = publicValue;
            RedactionReason = redactionReason;
        }

        public StableId Key { get; }

        public string PublicValue { get; }

        public DiagnosticRedactionReason? RedactionReason { get; }

        public bool IsRedacted
        {
            get { return RedactionReason.HasValue; }
        }

        public static DiagnosticAttribute CreatePublic(StableId key, string value)
        {
            return new DiagnosticAttribute(
                key,
                DiagnosticsContractFormat.RequirePrivacySafePublicValue(value, nameof(value)),
                null);
        }

        public static DiagnosticAttribute CreateRedacted(
            StableId key,
            DiagnosticRedactionReason reason)
        {
            DiagnosticsContractFormat.RequireKnownRedactionReason(reason);
            return new DiagnosticAttribute(key, null, reason);
        }

        public string ToCanonicalString()
        {
            if (IsRedacted)
            {
                return Key
                    + "=[redacted:"
                    + DiagnosticsContractFormat.RedactionReasonToken(RedactionReason.Value)
                    + "]";
            }

            return Key + "=" + PublicValue;
        }

        public bool Equals(DiagnosticAttribute other)
        {
            return !ReferenceEquals(other, null)
                && Key.Equals(other.Key)
                && string.Equals(PublicValue, other.PublicValue, StringComparison.Ordinal)
                && RedactionReason == other.RedactionReason;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DiagnosticAttribute);
        }

        public override int GetHashCode()
        {
            return DiagnosticsContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public abstract class DiagnosticEventPayload
    {
        internal DiagnosticEventPayload()
        {
        }

        public abstract DiagnosticEventKind EventKind { get; }

        internal virtual RunInvalidityReason? InvalidityReason
        {
            get { return null; }
        }

        public abstract string ToCanonicalString();
    }

    public sealed class RunStartedDiagnosticPayload : DiagnosticEventPayload
    {
        public RunStartedDiagnosticPayload(BuildIdentity buildIdentity)
        {
            BuildIdentity = DiagnosticsContractFormat.RequireNotNull(
                buildIdentity,
                nameof(buildIdentity));
        }

        public BuildIdentity BuildIdentity { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.RunStarted; }
        }

        public override string ToCanonicalString()
        {
            return BuildIdentity.ToCanonicalString();
        }
    }

    public sealed class RunEndedDiagnosticPayload : DiagnosticEventPayload
    {
        public RunEndedDiagnosticPayload(RunEndKind endKind)
        {
            DiagnosticsContractFormat.RequireKnownRunEndKind(endKind);
            EndKind = endKind;
        }

        public RunEndKind EndKind { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.RunEnded; }
        }

        internal override RunInvalidityReason? InvalidityReason
        {
            get
            {
                return EndKind == RunEndKind.Aborted
                    ? RunInvalidityReason.RunAborted
                    : (RunInvalidityReason?)null;
            }
        }

        public override string ToCanonicalString()
        {
            return "run_end_kind=" + DiagnosticsContractFormat.RunEndKindToken(EndKind);
        }
    }

    public sealed class RunRestartedDiagnosticPayload : DiagnosticEventPayload
    {
        public RunRestartedDiagnosticPayload(StableId previousRunId)
        {
            PreviousRunId = DiagnosticsContractFormat.RequireNotNull(
                previousRunId,
                nameof(previousRunId));
        }

        public StableId PreviousRunId { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.RunRestarted; }
        }

        public override string ToCanonicalString()
        {
            return "previous_run_id=" + PreviousRunId;
        }
    }

    public sealed class DiagnosticCommandDiagnosticPayload : DiagnosticEventPayload
    {
        public DiagnosticCommandDiagnosticPayload(
            StableId diagnosticCommandId,
            DiagnosticCommandKind commandKind,
            DiagnosticCommandEvidenceEffect evidenceEffect,
            MissionPayloadVersion missionPayloadVersion,
            MissionSequence observedMissionSequence)
        {
            DiagnosticCommandId = DiagnosticsContractFormat.RequireNotNull(
                diagnosticCommandId,
                nameof(diagnosticCommandId));
            DiagnosticsContractFormat.RequireKnownDiagnosticCommandKind(commandKind);
            DiagnosticsContractFormat.RequireKnownDiagnosticCommandEffect(evidenceEffect);
            MissionPayloadVersion = DiagnosticsContractFormat.RequireNotNull(
                missionPayloadVersion,
                nameof(missionPayloadVersion));
            ObservedMissionSequence = DiagnosticsContractFormat.RequireNotNull(
                observedMissionSequence,
                nameof(observedMissionSequence));

            if ((commandKind == DiagnosticCommandKind.FaultInjection
                    || commandKind == DiagnosticCommandKind.MissionStateOverride
                    || commandKind == DiagnosticCommandKind.ProgressionOverride)
                && evidenceEffect != DiagnosticCommandEvidenceEffect.InvalidatesTechnicalEvidence)
            {
                throw new ArgumentException(
                    "State-altering and fault-injection commands must invalidate technical evidence.",
                    nameof(evidenceEffect));
            }

            CommandKind = commandKind;
            EvidenceEffect = evidenceEffect;
        }

        public StableId DiagnosticCommandId { get; }

        public DiagnosticCommandKind CommandKind { get; }

        public DiagnosticCommandEvidenceEffect EvidenceEffect { get; }

        public MissionPayloadVersion MissionPayloadVersion { get; }

        public MissionSequence ObservedMissionSequence { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.DiagnosticCommand; }
        }

        internal override RunInvalidityReason? InvalidityReason
        {
            get
            {
                if (EvidenceEffect != DiagnosticCommandEvidenceEffect.InvalidatesTechnicalEvidence)
                {
                    return null;
                }

                switch (CommandKind)
                {
                    case DiagnosticCommandKind.FaultInjection:
                        return RunInvalidityReason.FaultInjectionUsed;
                    case DiagnosticCommandKind.MissionStateOverride:
                        return RunInvalidityReason.MissionStateOverrideUsed;
                    case DiagnosticCommandKind.ProgressionOverride:
                        return RunInvalidityReason.ProgressionOverrideUsed;
                    default:
                        return RunInvalidityReason.InvalidatingDiagnosticCommand;
                }
            }
        }

        public override string ToCanonicalString()
        {
            return "diagnostic_command_id="
                + DiagnosticCommandId
                + "\ndiagnostic_command_kind="
                + DiagnosticsContractFormat.DiagnosticCommandKindToken(CommandKind)
                + "\nevidence_effect="
                + DiagnosticsContractFormat.DiagnosticCommandEffectToken(EvidenceEffect)
                + "\n"
                + MissionPayloadVersion.ToCanonicalString()
                + "\nobserved_mission_sequence="
                + ObservedMissionSequence;
        }
    }

    public sealed class PerformanceWarningDiagnosticPayload : DiagnosticEventPayload
    {
        public PerformanceWarningDiagnosticPayload(
            PerformanceMetricKind metricKind,
            double observedValue,
            double warningThreshold,
            string unit,
            bool invalidatesTechnicalEvidence)
        {
            DiagnosticsContractFormat.RequireKnownPerformanceMetric(metricKind);
            DiagnosticsContractFormat.RequireFiniteNonNegative(observedValue, nameof(observedValue));
            DiagnosticsContractFormat.RequireFiniteNonNegative(
                warningThreshold,
                nameof(warningThreshold));

            if (observedValue <= warningThreshold)
            {
                throw new ArgumentException(
                    "A performance warning requires an observed value above its threshold.",
                    nameof(observedValue));
            }

            MetricKind = metricKind;
            ObservedValue = observedValue;
            WarningThreshold = warningThreshold;
            Unit = DiagnosticsContractFormat.RequirePrivacySafePublicValue(unit, nameof(unit));
            InvalidatesTechnicalEvidence = invalidatesTechnicalEvidence;
        }

        public PerformanceMetricKind MetricKind { get; }

        public double ObservedValue { get; }

        public double WarningThreshold { get; }

        public string Unit { get; }

        public bool InvalidatesTechnicalEvidence { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.PerformanceWarning; }
        }

        internal override RunInvalidityReason? InvalidityReason
        {
            get
            {
                return InvalidatesTechnicalEvidence
                    ? RunInvalidityReason.PerformanceBudgetBreach
                    : (RunInvalidityReason?)null;
            }
        }

        public override string ToCanonicalString()
        {
            return "metric_kind="
                + DiagnosticsContractFormat.PerformanceMetricToken(MetricKind)
                + "\nobserved_value="
                + ObservedValue.ToString("R", CultureInfo.InvariantCulture)
                + "\nwarning_threshold="
                + WarningThreshold.ToString("R", CultureInfo.InvariantCulture)
                + "\nunit="
                + Unit
                + "\ninvalidates_technical_evidence="
                + DiagnosticsContractFormat.BooleanToken(InvalidatesTechnicalEvidence);
        }
    }

    public sealed class ExceptionDiagnosticPayload : DiagnosticEventPayload
    {
        public ExceptionDiagnosticPayload(StableId errorCode, bool isUnhandled)
        {
            ErrorCode = DiagnosticsContractFormat.RequireNotNull(errorCode, nameof(errorCode));
            IsUnhandled = isUnhandled;
        }

        public StableId ErrorCode { get; }

        public bool IsUnhandled { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.Exception; }
        }

        internal override RunInvalidityReason? InvalidityReason
        {
            get
            {
                return IsUnhandled
                    ? RunInvalidityReason.UnhandledException
                    : (RunInvalidityReason?)null;
            }
        }

        public override string ToCanonicalString()
        {
            return "error_code="
                + ErrorCode
                + "\nis_unhandled="
                + DiagnosticsContractFormat.BooleanToken(IsUnhandled);
        }
    }

    public sealed class TimeoutDiagnosticPayload : DiagnosticEventPayload
    {
        public TimeoutDiagnosticPayload(
            StableId operationCode,
            long elapsedMilliseconds,
            long limitMilliseconds,
            bool invalidatesTechnicalEvidence)
        {
            OperationCode = DiagnosticsContractFormat.RequireNotNull(
                operationCode,
                nameof(operationCode));

            if (limitMilliseconds < 1L || elapsedMilliseconds <= limitMilliseconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedMilliseconds),
                    elapsedMilliseconds,
                    "A timeout requires a positive limit and elapsed time above that limit.");
            }

            ElapsedMilliseconds = elapsedMilliseconds;
            LimitMilliseconds = limitMilliseconds;
            InvalidatesTechnicalEvidence = invalidatesTechnicalEvidence;
        }

        public StableId OperationCode { get; }

        public long ElapsedMilliseconds { get; }

        public long LimitMilliseconds { get; }

        public bool InvalidatesTechnicalEvidence { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.Timeout; }
        }

        internal override RunInvalidityReason? InvalidityReason
        {
            get
            {
                return InvalidatesTechnicalEvidence
                    ? RunInvalidityReason.Timeout
                    : (RunInvalidityReason?)null;
            }
        }

        public override string ToCanonicalString()
        {
            return "operation_code="
                + OperationCode
                + "\nelapsed_milliseconds="
                + ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)
                + "\nlimit_milliseconds="
                + LimitMilliseconds.ToString(CultureInfo.InvariantCulture)
                + "\ninvalidates_technical_evidence="
                + DiagnosticsContractFormat.BooleanToken(InvalidatesTechnicalEvidence);
        }
    }

    public sealed class MissingAssetDiagnosticPayload : DiagnosticEventPayload
    {
        public MissingAssetDiagnosticPayload(StableId assetId)
        {
            AssetId = DiagnosticsContractFormat.RequireNotNull(assetId, nameof(assetId));
        }

        public StableId AssetId { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.MissingAsset; }
        }

        internal override RunInvalidityReason? InvalidityReason
        {
            get { return RunInvalidityReason.MissingRequiredAsset; }
        }

        public override string ToCanonicalString()
        {
            return "asset_id=" + AssetId;
        }
    }

    public sealed class CrashDiagnosticPayload : DiagnosticEventPayload
    {
        public CrashDiagnosticPayload(StableId crashCode)
        {
            CrashCode = DiagnosticsContractFormat.RequireNotNull(crashCode, nameof(crashCode));
        }

        public StableId CrashCode { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.Crash; }
        }

        public override string ToCanonicalString()
        {
            return "crash_code=" + CrashCode;
        }
    }

    public sealed class CapacityReachedDiagnosticPayload : DiagnosticEventPayload
    {
        public CapacityReachedDiagnosticPayload(DiagnosticCapacityDimension dimension, long limit)
        {
            DiagnosticsContractFormat.RequireKnownCapacityDimension(dimension);
            if (limit < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(limit),
                    limit,
                    "A reached diagnostic capacity must name a positive limit.");
            }

            Dimension = dimension;
            Limit = limit;
        }

        public DiagnosticCapacityDimension Dimension { get; }

        public long Limit { get; }

        public override DiagnosticEventKind EventKind
        {
            get { return DiagnosticEventKind.CapacityReached; }
        }

        internal override RunInvalidityReason? InvalidityReason
        {
            get { return RunInvalidityReason.DiagnosticsCapacityReached; }
        }

        public override string ToCanonicalString()
        {
            return "capacity_dimension="
                + DiagnosticsContractFormat.CapacityDimensionToken(Dimension)
                + "\ncapacity_limit="
                + Limit.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class DiagnosticEventEnvelope : IEquatable<DiagnosticEventEnvelope>
    {
        private readonly DiagnosticAttribute[] _attributes;

        public DiagnosticEventEnvelope(
            DiagnosticsSchemaVersion schemaVersion,
            DiagnosticBounds bounds,
            StableId eventId,
            StableId runId,
            DiagnosticEventSequence sequence,
            DiagnosticSeverity severity,
            DiagnosticEventPayload payload,
            IEnumerable<DiagnosticAttribute> attributes = null)
        {
            SchemaVersion = DiagnosticsContractFormat.RequireNotNull(
                schemaVersion,
                nameof(schemaVersion));
            Bounds = DiagnosticsContractFormat.RequireNotNull(bounds, nameof(bounds));
            EventId = DiagnosticsContractFormat.RequireNotNull(eventId, nameof(eventId));
            RunId = DiagnosticsContractFormat.RequireNotNull(runId, nameof(runId));
            Sequence = DiagnosticsContractFormat.RequireNotNull(sequence, nameof(sequence));
            DiagnosticsContractFormat.RequireKnownSeverity(severity);
            Severity = severity;
            Payload = DiagnosticsContractFormat.RequireNotNull(payload, nameof(payload));
            _attributes = CanonicalizeAttributes(attributes, bounds);
        }

        public DiagnosticsSchemaVersion SchemaVersion { get; }

        public DiagnosticBounds Bounds { get; }

        public StableId EventId { get; }

        public StableId RunId { get; }

        public DiagnosticEventSequence Sequence { get; }

        public DiagnosticSeverity Severity { get; }

        public DiagnosticEventPayload Payload { get; }

        public DiagnosticEventKind EventKind
        {
            get { return Payload.EventKind; }
        }

        public IReadOnlyList<DiagnosticAttribute> Attributes
        {
            get { return Array.AsReadOnly(_attributes); }
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(SchemaVersion.ToCanonicalString());
            builder.Append("\nevent_id=");
            builder.Append(EventId);
            builder.Append("\nrun_id=");
            builder.Append(RunId);
            builder.Append("\nevent_sequence=");
            builder.Append(Sequence);
            builder.Append("\nevent_kind=");
            builder.Append(DiagnosticsContractFormat.EventKindToken(EventKind));
            builder.Append("\nseverity=");
            builder.Append(DiagnosticsContractFormat.SeverityToken(Severity));
            builder.Append("\npayload:\n");
            builder.Append(Payload.ToCanonicalString());
            builder.Append("\nattribute_count=");
            builder.Append(_attributes.Length.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < _attributes.Length; index++)
            {
                builder.Append("\nattribute[");
                builder.Append(index.ToString(CultureInfo.InvariantCulture));
                builder.Append("]=");
                builder.Append(_attributes[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        public bool Equals(DiagnosticEventEnvelope other)
        {
            if (ReferenceEquals(other, null)
                || !SchemaVersion.Equals(other.SchemaVersion)
                || !Bounds.Equals(other.Bounds)
                || !EventId.Equals(other.EventId)
                || !RunId.Equals(other.RunId)
                || !Sequence.Equals(other.Sequence)
                || Severity != other.Severity
                || EventKind != other.EventKind
                || !string.Equals(
                    Payload.ToCanonicalString(),
                    other.Payload.ToCanonicalString(),
                    StringComparison.Ordinal)
                || _attributes.Length != other._attributes.Length)
            {
                return false;
            }

            for (int index = 0; index < _attributes.Length; index++)
            {
                if (!_attributes[index].Equals(other._attributes[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DiagnosticEventEnvelope);
        }

        public override int GetHashCode()
        {
            return DiagnosticsContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        private static DiagnosticAttribute[] CanonicalizeAttributes(
            IEnumerable<DiagnosticAttribute> attributes,
            DiagnosticBounds bounds)
        {
            DiagnosticAttribute[] values = attributes == null
                ? new DiagnosticAttribute[0]
                : attributes.ToArray();

            if (values.Length > bounds.MaxAttributesPerEvent)
            {
                throw new ArgumentException(
                    "Diagnostic event attribute count exceeds its configured bound.",
                    nameof(attributes));
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == null)
                {
                    throw new ArgumentException(
                        "Diagnostic event attributes cannot contain null entries.",
                        nameof(attributes));
                }

                if (!values[index].IsRedacted
                    && values[index].PublicValue.Length > bounds.MaxPublicValueLength)
                {
                    throw new ArgumentException(
                        "Diagnostic event attribute value exceeds its configured bound.",
                        nameof(attributes));
                }
            }

            Array.Sort(
                values,
                delegate(DiagnosticAttribute left, DiagnosticAttribute right)
                {
                    return left.Key.CompareTo(right.Key);
                });

            for (int index = 1; index < values.Length; index++)
            {
                if (values[index - 1].Key.Equals(values[index].Key))
                {
                    throw new ArgumentException(
                        "Diagnostic event attribute keys must be unique.",
                        nameof(attributes));
                }
            }

            return values;
        }
    }

    public sealed class DiagnosticEventBatch : IEquatable<DiagnosticEventBatch>
    {
        private readonly DiagnosticEventEnvelope[] _events;

        public DiagnosticEventBatch(
            DiagnosticsSchemaVersion schemaVersion,
            DiagnosticBounds bounds,
            StableId runId,
            IEnumerable<DiagnosticEventEnvelope> events)
        {
            SchemaVersion = DiagnosticsContractFormat.RequireNotNull(
                schemaVersion,
                nameof(schemaVersion));
            Bounds = DiagnosticsContractFormat.RequireNotNull(bounds, nameof(bounds));
            RunId = DiagnosticsContractFormat.RequireNotNull(runId, nameof(runId));

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            _events = events.ToArray();
            if (_events.Length == 0)
            {
                throw new ArgumentException(
                    "A diagnostic event batch must contain at least one event.",
                    nameof(events));
            }

            if (_events.Length > bounds.MaxEvents)
            {
                throw new ArgumentException(
                    "Diagnostic event count exceeds its configured bound.",
                    nameof(events));
            }

            Array.Sort(
                _events,
                delegate(DiagnosticEventEnvelope left, DiagnosticEventEnvelope right)
                {
                    if (left == null || right == null)
                    {
                        return ReferenceEquals(left, right) ? 0 : (left == null ? -1 : 1);
                    }

                    return left.Sequence.CompareTo(right.Sequence);
                });

            bool capacityReached = false;
            for (int index = 0; index < _events.Length; index++)
            {
                DiagnosticEventEnvelope current = _events[index];
                if (current == null)
                {
                    throw new ArgumentException(
                        "Diagnostic event batches cannot contain null events.",
                        nameof(events));
                }

                if (!current.SchemaVersion.Equals(schemaVersion)
                    || !current.Bounds.Equals(bounds)
                    || !current.RunId.Equals(runId))
                {
                    throw new ArgumentException(
                        "Every event must use the batch schema, bounds, and run identity.",
                        nameof(events));
                }

                if (index > 0 && _events[index - 1].Sequence.Equals(current.Sequence))
                {
                    throw new ArgumentException(
                        "Diagnostic event sequences must be unique.",
                        nameof(events));
                }

                if (current.EventKind == DiagnosticEventKind.CapacityReached)
                {
                    if (index != _events.Length - 1)
                    {
                        throw new ArgumentException(
                            "CapacityReached must be the final retained diagnostic event.",
                            nameof(events));
                    }

                    capacityReached = true;
                    CapacityReachedDiagnosticPayload payload =
                        (CapacityReachedDiagnosticPayload)current.Payload;
                    if (payload.Dimension == DiagnosticCapacityDimension.EventCount
                        && _events.Length != bounds.MaxEvents)
                    {
                        throw new ArgumentException(
                            "Event-count capacity is reached only when the retained batch is full.",
                            nameof(events));
                    }
                }
            }

            if (_events.Length == bounds.MaxEvents && !capacityReached)
            {
                throw new ArgumentException(
                    "A full diagnostic batch must end with an explicit CapacityReached event.",
                    nameof(events));
            }

            CapacityReached = capacityReached;
        }

        public DiagnosticsSchemaVersion SchemaVersion { get; }

        public DiagnosticBounds Bounds { get; }

        public StableId RunId { get; }

        public bool CapacityReached { get; }

        public IReadOnlyList<DiagnosticEventEnvelope> Events
        {
            get { return Array.AsReadOnly(_events); }
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(SchemaVersion.ToCanonicalString());
            builder.Append("\nrun_id=");
            builder.Append(RunId);
            builder.Append("\n");
            builder.Append(Bounds.ToCanonicalString());
            builder.Append("\ncapacity_reached=");
            builder.Append(DiagnosticsContractFormat.BooleanToken(CapacityReached));
            builder.Append("\nevent_count=");
            builder.Append(_events.Length.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < _events.Length; index++)
            {
                builder.Append("\nevent[");
                builder.Append(index.ToString(CultureInfo.InvariantCulture));
                builder.Append("]:\n");
                builder.Append(_events[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        public bool Equals(DiagnosticEventBatch other)
        {
            if (ReferenceEquals(other, null)
                || !SchemaVersion.Equals(other.SchemaVersion)
                || !Bounds.Equals(other.Bounds)
                || !RunId.Equals(other.RunId)
                || CapacityReached != other.CapacityReached
                || _events.Length != other._events.Length)
            {
                return false;
            }

            for (int index = 0; index < _events.Length; index++)
            {
                if (!_events[index].Equals(other._events[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DiagnosticEventBatch);
        }

        public override int GetHashCode()
        {
            return DiagnosticsContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    internal static class DiagnosticsContractFormat
    {
        private const string Sha256Prefix = "sha256:";
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static T RequireNotNull<T>(T value, string parameterName)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        public static string RequireSha256(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (value.Length != Sha256Prefix.Length + 64
                || !value.StartsWith(Sha256Prefix, StringComparison.Ordinal))
            {
                throw new FormatException(
                    parameterName + " must use sha256:<64 lowercase hex characters> form.");
            }

            bool nonZero = false;
            for (int index = Sha256Prefix.Length; index < value.Length; index++)
            {
                char current = value[index];
                bool digit = current >= '0' && current <= '9';
                bool lowerHex = current >= 'a' && current <= 'f';
                if (!digit && !lowerHex)
                {
                    throw new FormatException(parameterName + " must contain lowercase SHA-256 text.");
                }

                nonZero |= current != '0';
            }

            if (!nonZero)
            {
                throw new FormatException(parameterName + " must not be an all-zero placeholder.");
            }

            return value;
        }

        public static string RequirePrivacySafePublicValue(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("A public diagnostic value cannot be empty.", parameterName);
            }

            if (value.Length > DiagnosticBounds.HardMaximumPublicValueLength)
            {
                throw new ArgumentException("A public diagnostic value exceeds the hard v1 bound.", parameterName);
            }

            if (value.IndexOf('@') >= 0
                || value.IndexOf('\\') >= 0
                || value.IndexOf('?') >= 0
                || value.IndexOf('&') >= 0
                || value.IndexOf('%') >= 0
                || value.IndexOf("..", StringComparison.Ordinal) >= 0
                || value.IndexOf("://", StringComparison.Ordinal) >= 0
                || value[0] == '/'
                || (value.Length > 1
                    && IsAsciiLetter(value[0])
                    && value[1] == ':'))
            {
                throw new FormatException(
                    "Public diagnostic values cannot contain personal identifiers, URLs, or machine-local paths; use a redacted attribute.");
            }

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                bool allowed = IsAsciiLetter(current)
                    || (current >= '0' && current <= '9')
                    || current == '.'
                    || current == '_'
                    || current == ':'
                    || current == '/'
                    || current == '+'
                    || current == '-';
                if (!allowed)
                {
                    throw new FormatException(
                        "Public diagnostic values must use the privacy-safe canonical ASCII subset.");
                }
            }

            return value;
        }

        public static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Diagnostic numeric values must be finite and non-negative.");
            }
        }

        public static void RequireKnownSeverity(DiagnosticSeverity value)
        {
            if (value < DiagnosticSeverity.Information || value > DiagnosticSeverity.Critical)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown diagnostic severity.");
            }
        }

        public static void RequireKnownDiagnosticCommandKind(DiagnosticCommandKind value)
        {
            if (value < DiagnosticCommandKind.DeterministicSetup
                || value > DiagnosticCommandKind.ProgressionOverride)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown diagnostic command kind.");
            }
        }

        public static void RequireKnownDiagnosticCommandEffect(DiagnosticCommandEvidenceEffect value)
        {
            if (value < DiagnosticCommandEvidenceEffect.EvidenceSafe
                || value > DiagnosticCommandEvidenceEffect.InvalidatesTechnicalEvidence)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown command evidence effect.");
            }
        }

        public static void RequireKnownPerformanceMetric(PerformanceMetricKind value)
        {
            if (value < PerformanceMetricKind.CpuFrameTimeMilliseconds
                || value > PerformanceMetricKind.LoadingMilliseconds)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown performance metric.");
            }
        }

        public static void RequireKnownCapacityDimension(DiagnosticCapacityDimension value)
        {
            if (value < DiagnosticCapacityDimension.EventCount
                || value > DiagnosticCapacityDimension.RetainedRotations)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown capacity dimension.");
            }
        }

        public static void RequireKnownRedactionReason(DiagnosticRedactionReason value)
        {
            if (value < DiagnosticRedactionReason.PersonalData
                || value > DiagnosticRedactionReason.UnnecessaryFreeText)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown redaction reason.");
            }
        }

        public static void RequireKnownRunEndKind(RunEndKind value)
        {
            if (value < RunEndKind.Completed || value > RunEndKind.Aborted)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown run end kind.");
            }
        }

        public static string BooleanToken(bool value)
        {
            return value ? "true" : "false";
        }

        public static string EventKindToken(DiagnosticEventKind value)
        {
            switch (value)
            {
                case DiagnosticEventKind.RunStarted: return "run-started";
                case DiagnosticEventKind.RunEnded: return "run-ended";
                case DiagnosticEventKind.RunRestarted: return "run-restarted";
                case DiagnosticEventKind.DiagnosticCommand: return "diagnostic-command";
                case DiagnosticEventKind.PerformanceWarning: return "performance-warning";
                case DiagnosticEventKind.Exception: return "exception";
                case DiagnosticEventKind.Timeout: return "timeout";
                case DiagnosticEventKind.MissingAsset: return "missing-asset";
                case DiagnosticEventKind.Crash: return "crash";
                case DiagnosticEventKind.CapacityReached: return "capacity-reached";
                case DiagnosticEventKind.EvidenceValidity: return "evidence-validity";
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown event kind.");
            }
        }

        public static string SeverityToken(DiagnosticSeverity value)
        {
            switch (value)
            {
                case DiagnosticSeverity.Information: return "information";
                case DiagnosticSeverity.Warning: return "warning";
                case DiagnosticSeverity.Error: return "error";
                case DiagnosticSeverity.Critical: return "critical";
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown severity.");
            }
        }

        public static string DiagnosticCommandKindToken(DiagnosticCommandKind value)
        {
            switch (value)
            {
                case DiagnosticCommandKind.DeterministicSetup: return "deterministic-setup";
                case DiagnosticCommandKind.Inspection: return "inspection";
                case DiagnosticCommandKind.PerformanceCapture: return "performance-capture";
                case DiagnosticCommandKind.FaultInjection: return "fault-injection";
                case DiagnosticCommandKind.MissionStateOverride: return "mission-state-override";
                case DiagnosticCommandKind.ProgressionOverride: return "progression-override";
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown command kind.");
            }
        }

        public static string DiagnosticCommandEffectToken(DiagnosticCommandEvidenceEffect value)
        {
            switch (value)
            {
                case DiagnosticCommandEvidenceEffect.EvidenceSafe: return "evidence-safe";
                case DiagnosticCommandEvidenceEffect.InvalidatesTechnicalEvidence:
                    return "invalidates-technical-evidence";
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown evidence effect.");
            }
        }

        public static string PerformanceMetricToken(PerformanceMetricKind value)
        {
            switch (value)
            {
                case PerformanceMetricKind.CpuFrameTimeMilliseconds: return "cpu-frame-time-ms";
                case PerformanceMetricKind.GpuFrameTimeMilliseconds: return "gpu-frame-time-ms";
                case PerformanceMetricKind.TotalFrameTimeMilliseconds: return "total-frame-time-ms";
                case PerformanceMetricKind.ManagedAllocationBytes: return "managed-allocation-bytes";
                case PerformanceMetricKind.MemoryBytes: return "memory-bytes";
                case PerformanceMetricKind.LoadingMilliseconds: return "loading-ms";
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown performance metric.");
            }
        }

        public static string CapacityDimensionToken(DiagnosticCapacityDimension value)
        {
            switch (value)
            {
                case DiagnosticCapacityDimension.EventCount: return "event-count";
                case DiagnosticCapacityDimension.PayloadBytes: return "payload-bytes";
                case DiagnosticCapacityDimension.FileBytes: return "file-bytes";
                case DiagnosticCapacityDimension.RetainedRotations: return "retained-rotations";
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown capacity dimension.");
            }
        }

        public static string RedactionReasonToken(DiagnosticRedactionReason value)
        {
            switch (value)
            {
                case DiagnosticRedactionReason.PersonalData: return "personal-data";
                case DiagnosticRedactionReason.MachineLocalPath: return "machine-local-path";
                case DiagnosticRedactionReason.SecretOrCredential: return "secret-or-credential";
                case DiagnosticRedactionReason.ExternalIdentifier: return "external-identifier";
                case DiagnosticRedactionReason.UnnecessaryFreeText: return "unnecessary-free-text";
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown redaction reason.");
            }
        }

        public static string RunEndKindToken(RunEndKind value)
        {
            switch (value)
            {
                case RunEndKind.Completed: return "completed";
                case RunEndKind.Restarted: return "restarted";
                case RunEndKind.Aborted: return "aborted";
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown run end kind.");
            }
        }

        public static int DeterministicHash(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }

        private static bool IsAsciiLetter(char value)
        {
            return (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');
        }
    }
}

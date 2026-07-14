using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShooterMover.Contracts.Diagnostics;
using ShooterMover.Contracts.Identity;
using ShooterMover.Contracts.Mission;
using ShooterMover.Domain.Common;

namespace ShooterMover.TestSupport.EvidenceHarness
{
    /// <summary>
    /// One diagnostic field supplied to the evidence recorder. Unsafe values are
    /// converted to typed CS-012 redactions before an event is retained or written.
    /// </summary>
    public sealed class EvidenceDiagnosticField
    {
        private EvidenceDiagnosticField(
            StableId key,
            string rawValue,
            DiagnosticRedactionReason? explicitRedactionReason)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            RawValue = rawValue;
            ExplicitRedactionReason = explicitRedactionReason;
        }

        public StableId Key { get; }

        public string RawValue { get; }

        public DiagnosticRedactionReason? ExplicitRedactionReason { get; }

        public static EvidenceDiagnosticField Public(StableId key, string rawValue)
        {
            if (rawValue == null)
            {
                throw new ArgumentNullException(nameof(rawValue));
            }

            return new EvidenceDiagnosticField(key, rawValue, null);
        }

        public static EvidenceDiagnosticField Redacted(
            StableId key,
            DiagnosticRedactionReason reason)
        {
            return new EvidenceDiagnosticField(key, null, reason);
        }

        internal DiagnosticAttribute ToSafeAttribute(int configuredPublicValueLength)
        {
            if (ExplicitRedactionReason.HasValue)
            {
                return DiagnosticAttribute.CreateRedacted(Key, ExplicitRedactionReason.Value);
            }

            DiagnosticRedactionReason reason;
            if (ShouldRedact(Key, RawValue, configuredPublicValueLength, out reason))
            {
                return DiagnosticAttribute.CreateRedacted(Key, reason);
            }

            try
            {
                return DiagnosticAttribute.CreatePublic(Key, RawValue);
            }
            catch (ArgumentException)
            {
                return DiagnosticAttribute.CreateRedacted(
                    Key,
                    ClassifyUnsafeValue(Key, RawValue, configuredPublicValueLength));
            }
            catch (FormatException)
            {
                return DiagnosticAttribute.CreateRedacted(
                    Key,
                    ClassifyUnsafeValue(Key, RawValue, configuredPublicValueLength));
            }
        }

        private static bool ShouldRedact(
            StableId key,
            string value,
            int configuredPublicValueLength,
            out DiagnosticRedactionReason reason)
        {
            reason = ClassifyUnsafeValue(key, value, configuredPublicValueLength);
            if (value.Length > configuredPublicValueLength)
            {
                return true;
            }

            string keyText = key.ToString().ToLowerInvariant();
            string valueText = value.ToLowerInvariant();
            if (ContainsSecretMarker(keyText) || ContainsSecretMarker(valueText))
            {
                reason = DiagnosticRedactionReason.SecretOrCredential;
                return true;
            }

            if (value.IndexOf('@') >= 0)
            {
                reason = DiagnosticRedactionReason.PersonalData;
                return true;
            }

            if (LooksLikeMachinePath(value))
            {
                reason = DiagnosticRedactionReason.MachineLocalPath;
                return true;
            }

            if (value.IndexOf("://", StringComparison.Ordinal) >= 0
                || value.IndexOf('?') >= 0
                || value.IndexOf('&') >= 0
                || value.IndexOf('%') >= 0)
            {
                reason = DiagnosticRedactionReason.ExternalIdentifier;
                return true;
            }

            return false;
        }

        private static DiagnosticRedactionReason ClassifyUnsafeValue(
            StableId key,
            string value,
            int configuredPublicValueLength)
        {
            string keyText = key.ToString().ToLowerInvariant();
            string valueText = value.ToLowerInvariant();
            if (ContainsSecretMarker(keyText) || ContainsSecretMarker(valueText))
            {
                return DiagnosticRedactionReason.SecretOrCredential;
            }

            if (value.IndexOf('@') >= 0)
            {
                return DiagnosticRedactionReason.PersonalData;
            }

            if (LooksLikeMachinePath(value))
            {
                return DiagnosticRedactionReason.MachineLocalPath;
            }

            if (value.IndexOf("://", StringComparison.Ordinal) >= 0
                || value.IndexOf('?') >= 0
                || value.IndexOf('&') >= 0
                || value.IndexOf('%') >= 0)
            {
                return DiagnosticRedactionReason.ExternalIdentifier;
            }

            return DiagnosticRedactionReason.UnnecessaryFreeText;
        }

        private static bool ContainsSecretMarker(string value)
        {
            return value.IndexOf("password", StringComparison.Ordinal) >= 0
                || value.IndexOf("passwd", StringComparison.Ordinal) >= 0
                || value.IndexOf("secret", StringComparison.Ordinal) >= 0
                || value.IndexOf("credential", StringComparison.Ordinal) >= 0
                || value.IndexOf("authorization", StringComparison.Ordinal) >= 0
                || value.IndexOf("bearer", StringComparison.Ordinal) >= 0
                || value.IndexOf("api-key", StringComparison.Ordinal) >= 0
                || value.IndexOf("apikey", StringComparison.Ordinal) >= 0
                || value.IndexOf("access-token", StringComparison.Ordinal) >= 0;
        }

        private static bool LooksLikeMachinePath(string value)
        {
            return value.IndexOf('\\') >= 0
                || value.IndexOf("..", StringComparison.Ordinal) >= 0
                || value.StartsWith("/", StringComparison.Ordinal)
                || (value.Length > 1
                    && ((value[0] >= 'a' && value[0] <= 'z')
                        || (value[0] >= 'A' && value[0] <= 'Z'))
                    && value[1] == ':');
        }
    }

    /// <summary>
    /// Immutable result returned when a local evidence session is finalized.
    /// </summary>
    public sealed class EvidenceDiagnosticsSessionResult
    {
        private readonly DiagnosticEventEnvelope[] events;
        private readonly string[] logPaths;

        internal EvidenceDiagnosticsSessionResult(
            EvidenceAssessment assessment,
            IEnumerable<DiagnosticEventEnvelope> events,
            IEnumerable<string> logPaths,
            bool wasTruncated,
            string writeFailureCode)
        {
            Assessment = assessment ?? throw new ArgumentNullException(nameof(assessment));
            this.events = events == null
                ? throw new ArgumentNullException(nameof(events))
                : events.ToArray();
            this.logPaths = logPaths == null
                ? throw new ArgumentNullException(nameof(logPaths))
                : logPaths.ToArray();
            WasTruncated = wasTruncated;
            WriteFailureCode = writeFailureCode;
        }

        public EvidenceAssessment Assessment { get; }

        public IReadOnlyList<DiagnosticEventEnvelope> Events
        {
            get { return Array.AsReadOnly(events); }
        }

        public IReadOnlyList<string> LogPaths
        {
            get { return Array.AsReadOnly(logPaths); }
        }

        public bool WasTruncated { get; }

        public string WriteFailureCode { get; }
    }

    /// <summary>
    /// Local/offline, append-only recorder for CS-012 diagnostic events.
    /// Ordering is derived only from the diagnostic sequence; wall-clock time is
    /// intentionally excluded so clock changes cannot reorder evidence.
    /// </summary>
    public sealed class EvidenceDiagnosticsRecorder
    {
        public const string LogSchema = "shooter-mover.evidence-diagnostics-log";
        public const int LogVersion = 1;

        private const int MaximumAttributesPerEvent = 32;
        private const int MaximumPublicValueLength = 128;
        private const int MaximumSupportBundleItems = 128;
        private const string EventTerminator = "\n--event-end--\n";

        private readonly object sync = new object();
        private readonly string directoryPath;
        private readonly StableId runId;
        private readonly EvidenceRunConfiguration configuration;
        private readonly DiagnosticBounds contractBounds;
        private readonly List<DiagnosticEventEnvelope> events =
            new List<DiagnosticEventEnvelope>();
        private readonly List<string> logPaths = new List<string>();

        private RunValidityAccumulator validity;
        private long nextSequence = 1L;
        private int currentPartIndex;
        private bool capacityClosed;
        private bool finalized;
        private string writeFailureCode;
        private EvidenceDiagnosticsSessionResult finalResult;

        public EvidenceDiagnosticsRecorder(
            string directoryPath,
            StableId runId,
            EvidenceRunConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException(
                    "A local diagnostics directory is required.",
                    nameof(directoryPath));
            }

            this.runId = runId ?? throw new ArgumentNullException(nameof(runId));
            this.configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));

            if (configuration.Diagnostics.MaxEventCount > DiagnosticBounds.HardMaximumEvents)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuration),
                    "EH-002 maxEventCount exceeds the CS-012 v1 hard maximum.");
            }

            long supportBytes = checked(
                (long)configuration.Diagnostics.MaxLogBytes
                * configuration.Diagnostics.RetainedLogCount);
            supportBytes = Math.Min(
                supportBytes,
                DiagnosticBounds.HardMaximumSupportBundleBytes);

            contractBounds = new DiagnosticBounds(
                configuration.Diagnostics.MaxEventCount,
                MaximumAttributesPerEvent,
                MaximumPublicValueLength,
                Math.Min(
                    MaximumSupportBundleItems,
                    configuration.Diagnostics.RetainedLogCount),
                supportBytes);

            this.directoryPath = Path.GetFullPath(directoryPath);
            Directory.CreateDirectory(this.directoryPath);
            validity = RunValidityAccumulator.Empty(runId);
            CreateLogPart(0);
        }

        public StableId RunId
        {
            get { return runId; }
        }

        public EvidenceRunConfiguration Configuration
        {
            get { return configuration; }
        }

        public DiagnosticBounds ContractBounds
        {
            get { return contractBounds; }
        }

        public IReadOnlyList<DiagnosticEventEnvelope> Events
        {
            get
            {
                lock (sync)
                {
                    return Array.AsReadOnly(events.ToArray());
                }
            }
        }

        public IReadOnlyList<string> LogPaths
        {
            get
            {
                lock (sync)
                {
                    return Array.AsReadOnly(logPaths.ToArray());
                }
            }
        }

        public bool WasTruncated
        {
            get
            {
                lock (sync)
                {
                    return capacityClosed;
                }
            }
        }

        public string WriteFailureCode
        {
            get
            {
                lock (sync)
                {
                    return writeFailureCode;
                }
            }
        }

        public RunTechnicalValidity CurrentTechnicalValidity
        {
            get
            {
                lock (sync)
                {
                    return validity.FinalizeForEvidence();
                }
            }
        }

        public DiagnosticEventEnvelope StartSession(BuildIdentity buildIdentity)
        {
            return Record(
                DiagnosticSeverity.Information,
                new RunStartedDiagnosticPayload(buildIdentity));
        }

        public DiagnosticEventEnvelope EndSession(RunEndKind endKind)
        {
            return Record(
                DiagnosticSeverity.Information,
                new RunEndedDiagnosticPayload(endKind));
        }

        public DiagnosticEventEnvelope RestartSession(StableId previousRunId)
        {
            return Record(
                DiagnosticSeverity.Information,
                new RunRestartedDiagnosticPayload(previousRunId));
        }

        public DiagnosticEventEnvelope RecordDiagnosticCommand(
            StableId diagnosticCommandId,
            DiagnosticCommandKind commandKind,
            DiagnosticCommandEvidenceEffect evidenceEffect,
            MissionPayloadVersion missionPayloadVersion,
            MissionSequence observedMissionSequence,
            IEnumerable<EvidenceDiagnosticField> fields = null)
        {
            DiagnosticSeverity severity =
                evidenceEffect == DiagnosticCommandEvidenceEffect.InvalidatesTechnicalEvidence
                    ? DiagnosticSeverity.Warning
                    : DiagnosticSeverity.Information;
            return Record(
                severity,
                new DiagnosticCommandDiagnosticPayload(
                    diagnosticCommandId,
                    commandKind,
                    evidenceEffect,
                    missionPayloadVersion,
                    observedMissionSequence),
                fields);
        }

        public DiagnosticEventEnvelope RecordException(
            StableId errorCode,
            bool isUnhandled,
            IEnumerable<EvidenceDiagnosticField> fields = null)
        {
            return Record(
                isUnhandled ? DiagnosticSeverity.Critical : DiagnosticSeverity.Error,
                new ExceptionDiagnosticPayload(errorCode, isUnhandled),
                fields);
        }

        public DiagnosticEventEnvelope RecordTimeout(
            StableId operationCode,
            long elapsedMilliseconds,
            long limitMilliseconds,
            bool invalidatesTechnicalEvidence,
            IEnumerable<EvidenceDiagnosticField> fields = null)
        {
            return Record(
                invalidatesTechnicalEvidence
                    ? DiagnosticSeverity.Error
                    : DiagnosticSeverity.Warning,
                new TimeoutDiagnosticPayload(
                    operationCode,
                    elapsedMilliseconds,
                    limitMilliseconds,
                    invalidatesTechnicalEvidence),
                fields);
        }

        public DiagnosticEventEnvelope RecordMissingAsset(
            StableId assetId,
            IEnumerable<EvidenceDiagnosticField> fields = null)
        {
            return Record(
                DiagnosticSeverity.Error,
                new MissingAssetDiagnosticPayload(assetId),
                fields);
        }

        public DiagnosticEventEnvelope RecordPerformanceWarning(
            PerformanceMetricKind metricKind,
            double observedValue,
            double warningThreshold,
            string unit,
            bool invalidatesTechnicalEvidence,
            IEnumerable<EvidenceDiagnosticField> fields = null)
        {
            return Record(
                DiagnosticSeverity.Warning,
                new PerformanceWarningDiagnosticPayload(
                    metricKind,
                    observedValue,
                    warningThreshold,
                    unit,
                    invalidatesTechnicalEvidence),
                fields);
        }

        public DiagnosticEventEnvelope Record(
            DiagnosticSeverity severity,
            DiagnosticEventPayload payload,
            IEnumerable<EvidenceDiagnosticField> fields = null)
        {
            lock (sync)
            {
                EnsureWritable();
                if (payload == null)
                {
                    throw new ArgumentNullException(nameof(payload));
                }

                DiagnosticAttribute[] attributes = CanonicalizeFields(fields);
                return AppendRequestedEvent(severity, payload, attributes);
            }
        }

        /// <summary>
        /// Appends an already-created CS-012 envelope only when it exactly matches
        /// this recorder's run, bounds, and next sequence. Malformed input leaves
        /// both the file and in-memory validity unchanged.
        /// </summary>
        public DiagnosticEventEnvelope Append(DiagnosticEventEnvelope diagnosticEvent)
        {
            lock (sync)
            {
                EnsureWritable();
                if (diagnosticEvent == null)
                {
                    throw new ArgumentNullException(nameof(diagnosticEvent));
                }

                if (!diagnosticEvent.RunId.Equals(runId)
                    || !diagnosticEvent.SchemaVersion.Equals(DiagnosticsSchemaVersion.Current)
                    || !diagnosticEvent.Bounds.Equals(contractBounds)
                    || diagnosticEvent.Sequence.Value != nextSequence)
                {
                    throw new ArgumentException(
                        "The diagnostic event does not match the recorder run, schema, bounds, or next sequence.",
                        nameof(diagnosticEvent));
                }

                int payloadBytes = MeasurePayloadBytes(
                    diagnosticEvent.Payload,
                    diagnosticEvent.Attributes);
                if (payloadBytes > configuration.Diagnostics.MaxEventPayloadBytes)
                {
                    return AppendCapacityEvent(
                        DiagnosticCapacityDimension.PayloadBytes,
                        configuration.Diagnostics.MaxEventPayloadBytes);
                }

                if (events.Count >= configuration.Diagnostics.MaxEventCount - 1)
                {
                    return AppendCapacityEvent(
                        DiagnosticCapacityDimension.EventCount,
                        configuration.Diagnostics.MaxEventCount);
                }

                return AppendEnvelopeWithStorage(diagnosticEvent);
            }
        }

        public EvidenceDiagnosticsSessionResult FinalizeSession(
            HumanFunEvidence humanFunEvidence)
        {
            lock (sync)
            {
                if (humanFunEvidence == null)
                {
                    throw new ArgumentNullException(nameof(humanFunEvidence));
                }

                if (finalized)
                {
                    return finalResult;
                }

                if (!capacityClosed)
                {
                    RunTechnicalValidity beforeSummary = validity.FinalizeForEvidence();
                    AppendRequestedEvent(
                        DiagnosticSeverity.Information,
                        new EvidenceValidityDiagnosticPayload(beforeSummary),
                        new DiagnosticAttribute[0]);
                }

                RunTechnicalValidity technicalValidity = validity.FinalizeForEvidence();
                finalResult = new EvidenceDiagnosticsSessionResult(
                    new EvidenceAssessment(technicalValidity, humanFunEvidence),
                    events,
                    logPaths,
                    capacityClosed,
                    writeFailureCode);
                finalized = true;
                return finalResult;
            }
        }

        /// <summary>
        /// Explicit local export. Existing destinations are never overwritten and
        /// no network endpoint is accepted or contacted.
        /// </summary>
        public IReadOnlyList<string> ExportTo(string destinationDirectory)
        {
            lock (sync)
            {
                if (!finalized)
                {
                    throw new InvalidOperationException(
                        "Finalize the evidence session before exporting its logs.");
                }

                if (string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    throw new ArgumentException(
                        "A local export directory is required.",
                        nameof(destinationDirectory));
                }

                string destination = Path.GetFullPath(destinationDirectory);
                Directory.CreateDirectory(destination);
                List<string> exported = new List<string>();
                foreach (string sourcePath in logPaths)
                {
                    string destinationPath = Path.Combine(
                        destination,
                        Path.GetFileName(sourcePath));
                    using (FileStream source = new FileStream(
                        sourcePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read))
                    using (FileStream target = new FileStream(
                        destinationPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        source.CopyTo(target);
                        target.Flush();
                    }

                    exported.Add(destinationPath);
                }

                return Array.AsReadOnly(exported.ToArray());
            }
        }

        private DiagnosticEventEnvelope AppendRequestedEvent(
            DiagnosticSeverity severity,
            DiagnosticEventPayload payload,
            IEnumerable<DiagnosticAttribute> attributes)
        {
            if (events.Count >= configuration.Diagnostics.MaxEventCount - 1)
            {
                return AppendCapacityEvent(
                    DiagnosticCapacityDimension.EventCount,
                    configuration.Diagnostics.MaxEventCount);
            }

            DiagnosticAttribute[] attributeArray = attributes == null
                ? new DiagnosticAttribute[0]
                : attributes.ToArray();
            int payloadBytes = MeasurePayloadBytes(payload, attributeArray);
            if (payloadBytes > configuration.Diagnostics.MaxEventPayloadBytes)
            {
                return AppendCapacityEvent(
                    DiagnosticCapacityDimension.PayloadBytes,
                    configuration.Diagnostics.MaxEventPayloadBytes);
            }

            DiagnosticEventEnvelope envelope = CreateEnvelope(
                nextSequence,
                severity,
                payload,
                attributeArray);
            return AppendEnvelopeWithStorage(envelope);
        }

        private DiagnosticEventEnvelope AppendEnvelopeWithStorage(
            DiagnosticEventEnvelope envelope)
        {
            byte[] recordBytes = BuildEventRecord(envelope);
            StorageDecision storageDecision = EnsureStorageFor(recordBytes.Length);
            if (storageDecision != StorageDecision.Ready)
            {
                DiagnosticCapacityDimension dimension =
                    storageDecision == StorageDecision.RotationCapacityReached
                        ? DiagnosticCapacityDimension.RetainedRotations
                        : DiagnosticCapacityDimension.FileBytes;
                long limit = dimension == DiagnosticCapacityDimension.RetainedRotations
                    ? configuration.Diagnostics.RetainedLogCount
                    : configuration.Diagnostics.MaxLogBytes;
                return AppendCapacityEvent(dimension, limit);
            }

            if (!TryAppendBytes(CurrentLogPath, recordBytes))
            {
                return RegisterWriteFailure();
            }

            CommitEnvelope(envelope);
            return envelope;
        }

        private DiagnosticEventEnvelope AppendCapacityEvent(
            DiagnosticCapacityDimension dimension,
            long limit)
        {
            if (capacityClosed)
            {
                return events.Count == 0 ? null : events[events.Count - 1];
            }

            DiagnosticEventEnvelope capacity = CreateEnvelope(
                nextSequence,
                DiagnosticSeverity.Error,
                new CapacityReachedDiagnosticPayload(dimension, limit),
                new DiagnosticAttribute[0]);
            byte[] bytes = BuildEventRecord(capacity);

            StorageDecision decision = EnsureStorageForCapacity(bytes.Length);
            if (decision != StorageDecision.Ready || !TryAppendBytes(CurrentLogPath, bytes))
            {
                return RegisterWriteFailure(capacity);
            }

            CommitEnvelope(capacity);
            capacityClosed = true;
            return capacity;
        }

        private DiagnosticEventEnvelope RegisterWriteFailure(
            DiagnosticEventEnvelope prebuiltCapacity = null)
        {
            writeFailureCode = "local-write-failed";
            capacityClosed = true;
            DiagnosticEventEnvelope capacity = prebuiltCapacity ?? CreateEnvelope(
                nextSequence,
                DiagnosticSeverity.Error,
                new CapacityReachedDiagnosticPayload(
                    DiagnosticCapacityDimension.FileBytes,
                    configuration.Diagnostics.MaxLogBytes),
                new DiagnosticAttribute[0]);
            CommitEnvelope(capacity);
            return capacity;
        }

        private void CommitEnvelope(DiagnosticEventEnvelope envelope)
        {
            validity = validity.Apply(envelope);
            events.Add(envelope);
            nextSequence++;
            if (envelope.EventKind == DiagnosticEventKind.CapacityReached)
            {
                capacityClosed = true;
            }
        }

        private StorageDecision EnsureStorageFor(int requestedRecordBytes)
        {
            int reserveBytes = MaximumCapacityRecordBytes(nextSequence + 1L);
            if (FitsCurrentPart(requestedRecordBytes, reserveBytes))
            {
                return StorageDecision.Ready;
            }

            if (CanCreateAnotherPart())
            {
                CreateLogPart(currentPartIndex + 1);
                if (FitsCurrentPart(requestedRecordBytes, reserveBytes))
                {
                    return StorageDecision.Ready;
                }

                return StorageDecision.FileCapacityReached;
            }

            return StorageDecision.RotationCapacityReached;
        }

        private StorageDecision EnsureStorageForCapacity(int capacityRecordBytes)
        {
            if (FitsCurrentPart(capacityRecordBytes, 0))
            {
                return StorageDecision.Ready;
            }

            if (CanCreateAnotherPart())
            {
                CreateLogPart(currentPartIndex + 1);
                return FitsCurrentPart(capacityRecordBytes, 0)
                    ? StorageDecision.Ready
                    : StorageDecision.FileCapacityReached;
            }

            return StorageDecision.RotationCapacityReached;
        }

        private bool FitsCurrentPart(int requestedBytes, int reserveBytes)
        {
            long currentBytes = new FileInfo(CurrentLogPath).Length;
            return currentBytes + requestedBytes + reserveBytes
                <= configuration.Diagnostics.MaxLogBytes;
        }

        private bool CanCreateAnotherPart()
        {
            return currentPartIndex + 1 < configuration.Diagnostics.RetainedLogCount;
        }

        private void CreateLogPart(int partIndex)
        {
            string path = BuildLogPath(partIndex);
            string header = "log_schema="
                + LogSchema
                + "\nlog_version="
                + LogVersion.ToString(CultureInfo.InvariantCulture)
                + "\nrun_id="
                + runId
                + "\nconfiguration_fingerprint="
                + configuration.Fingerprint
                + "\npart_index="
                + partIndex.ToString(CultureInfo.InvariantCulture)
                + "\n---\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            if (headerBytes.Length + MaximumCapacityRecordBytes(nextSequence)
                > configuration.Diagnostics.MaxLogBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuration),
                    "The EH-002 log-byte bound cannot fit the v1 header and one capacity event.");
            }

            using (FileStream stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            {
                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Flush();
            }

            currentPartIndex = partIndex;
            logPaths.Add(path);
        }

        private string BuildLogPath(int partIndex)
        {
            string fileName = runId.Namespace
                + "-"
                + runId.Value
                + ".diagnostics-v1.part-"
                + partIndex.ToString("D3", CultureInfo.InvariantCulture)
                + ".log";
            return Path.Combine(directoryPath, fileName);
        }

        private string CurrentLogPath
        {
            get { return logPaths[logPaths.Count - 1]; }
        }

        private bool TryAppendBytes(string path, byte[] bytes)
        {
            try
            {
                using (FileStream stream = new FileStream(
                    path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private DiagnosticAttribute[] CanonicalizeFields(
            IEnumerable<EvidenceDiagnosticField> fields)
        {
            EvidenceDiagnosticField[] values = fields == null
                ? new EvidenceDiagnosticField[0]
                : fields.ToArray();
            if (values.Length > contractBounds.MaxAttributesPerEvent)
            {
                throw new ArgumentException(
                    "Diagnostic field count exceeds the CS-012 v1 bound.",
                    nameof(fields));
            }

            DiagnosticAttribute[] attributes = new DiagnosticAttribute[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == null)
                {
                    throw new ArgumentException(
                        "Diagnostic fields cannot contain null entries.",
                        nameof(fields));
                }

                attributes[index] = values[index].ToSafeAttribute(
                    contractBounds.MaxPublicValueLength);
            }

            return attributes;
        }

        private DiagnosticEventEnvelope CreateEnvelope(
            long sequence,
            DiagnosticSeverity severity,
            DiagnosticEventPayload payload,
            IEnumerable<DiagnosticAttribute> attributes)
        {
            return new DiagnosticEventEnvelope(
                DiagnosticsSchemaVersion.Current,
                contractBounds,
                StableId.Create(
                    "diagnostic-event",
                    "sequence-" + sequence.ToString("D8", CultureInfo.InvariantCulture)),
                runId,
                new DiagnosticEventSequence(sequence),
                severity,
                payload,
                attributes);
        }

        private int MeasurePayloadBytes(
            DiagnosticEventPayload payload,
            IEnumerable<DiagnosticAttribute> attributes)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(payload.ToCanonicalString());
            foreach (DiagnosticAttribute attribute in attributes)
            {
                builder.Append('\n');
                builder.Append(attribute.ToCanonicalString());
            }

            return Encoding.UTF8.GetByteCount(builder.ToString());
        }

        private static byte[] BuildEventRecord(DiagnosticEventEnvelope envelope)
        {
            string canonical = envelope.ToCanonicalString();
            int eventByteCount = Encoding.UTF8.GetByteCount(canonical);
            string record = "event_byte_count="
                + eventByteCount.ToString(CultureInfo.InvariantCulture)
                + "\n"
                + canonical
                + EventTerminator;
            return Encoding.UTF8.GetBytes(record);
        }

        private int MaximumCapacityRecordBytes(long sequence)
        {
            int maximum = 0;
            DiagnosticCapacityDimension[] dimensions =
            {
                DiagnosticCapacityDimension.EventCount,
                DiagnosticCapacityDimension.PayloadBytes,
                DiagnosticCapacityDimension.FileBytes,
                DiagnosticCapacityDimension.RetainedRotations,
            };
            foreach (DiagnosticCapacityDimension dimension in dimensions)
            {
                long limit;
                switch (dimension)
                {
                    case DiagnosticCapacityDimension.EventCount:
                        limit = configuration.Diagnostics.MaxEventCount;
                        break;
                    case DiagnosticCapacityDimension.PayloadBytes:
                        limit = configuration.Diagnostics.MaxEventPayloadBytes;
                        break;
                    case DiagnosticCapacityDimension.FileBytes:
                        limit = configuration.Diagnostics.MaxLogBytes;
                        break;
                    default:
                        limit = configuration.Diagnostics.RetainedLogCount;
                        break;
                }

                DiagnosticEventEnvelope envelope = CreateEnvelope(
                    sequence,
                    DiagnosticSeverity.Error,
                    new CapacityReachedDiagnosticPayload(dimension, limit),
                    new DiagnosticAttribute[0]);
                maximum = Math.Max(maximum, BuildEventRecord(envelope).Length);
            }

            return maximum;
        }

        private void EnsureWritable()
        {
            if (finalized)
            {
                throw new InvalidOperationException(
                    "The evidence diagnostics session is already finalized.");
            }

            if (capacityClosed)
            {
                throw new InvalidOperationException(
                    "The evidence diagnostics session reached a bound and is closed.");
            }
        }

        private enum StorageDecision
        {
            Ready = 1,
            FileCapacityReached = 2,
            RotationCapacityReached = 3,
        }
    }
}

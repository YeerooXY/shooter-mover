using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Contracts.Diagnostics;
using ShooterMover.Domain.Common;

namespace ShooterMover.TestSupport.EvidenceHarness
{
    public enum EvidencePerformanceCaptureState
    {
        Configured = 1,
        WarmingUp = 2,
        Capturing = 3,
        Completed = 4,
        Invalid = 5,
    }

    public enum EvidencePerformanceIssueKind
    {
        IncompleteData = 1,
        InvalidClock = 2,
        CaptureOverrun = 3,
        SampleCapacityReached = 4,
        QualityObservationCapacityReached = 5,
        CounterUnavailable = 6,
        BudgetExceeded = 7,
    }

    public sealed class EvidencePerformanceObjectBudget : IEquatable<EvidencePerformanceObjectBudget>
    {
        public EvidencePerformanceObjectBudget(StableId counterId, long maximumCount)
        {
            CounterId = counterId ?? throw new ArgumentNullException(nameof(counterId));
            if (maximumCount < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumCount),
                    maximumCount,
                    "An object budget cannot be negative.");
            }

            MaximumCount = maximumCount;
        }

        public StableId CounterId { get; }

        public long MaximumCount { get; }

        public string ToCanonicalString()
        {
            return "counter_id=" + CounterId
                + ";maximum_count="
                + MaximumCount.ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(EvidencePerformanceObjectBudget other)
        {
            return !ReferenceEquals(other, null)
                && CounterId.Equals(other.CounterId)
                && MaximumCount == other.MaximumCount;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EvidencePerformanceObjectBudget);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (CounterId.GetHashCode() * 397) ^ MaximumCount.GetHashCode();
            }
        }
    }

    public sealed class EvidencePerformanceObjectCounterSample
    {
        public EvidencePerformanceObjectCounterSample(StableId counterId, long count)
        {
            CounterId = counterId ?? throw new ArgumentNullException(nameof(counterId));
            if (count < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    count,
                    "An observed object count cannot be negative.");
            }

            Count = count;
        }

        public StableId CounterId { get; }

        public long Count { get; }
    }

    public sealed class EvidencePerformanceBudget
    {
        public const double HardMaximumWarmUpSeconds = 120d;
        public const double HardMaximumCaptureSeconds = 600d;
        public const double HardMaximumCompletionOverrunSeconds = 30d;
        public const int HardMaximumFrameSamples = 100000;
        public const int HardMaximumQualityObservations = 64;
        public const int HardMaximumObjectCounters = 64;

        private readonly EvidencePerformanceObjectBudget[] objectBudgets;

        public EvidencePerformanceBudget(
            double warmUpSeconds,
            double captureSeconds,
            double maximumCompletionOverrunSeconds,
            int maximumFrameSamples,
            int maximumQualityObservations,
            int maximumObjectCounters,
            double p95FrameTimeWarningMilliseconds,
            double p99FrameTimeWarningMilliseconds,
            long totalManagedAllocationWarningBytes,
            long perFrameManagedAllocationWarningBytes,
            double sceneLoadWarningMilliseconds,
            long memoryWarningBytes,
            bool budgetBreachInvalidatesTechnicalEvidence,
            IEnumerable<EvidencePerformanceObjectBudget> objectBudgets)
        {
            WarmUpSeconds = RequireFiniteRange(
                warmUpSeconds,
                0d,
                HardMaximumWarmUpSeconds,
                nameof(warmUpSeconds));
            CaptureSeconds = RequireFiniteRange(
                captureSeconds,
                double.Epsilon,
                HardMaximumCaptureSeconds,
                nameof(captureSeconds));
            MaximumCompletionOverrunSeconds = RequireFiniteRange(
                maximumCompletionOverrunSeconds,
                0d,
                HardMaximumCompletionOverrunSeconds,
                nameof(maximumCompletionOverrunSeconds));
            MaximumFrameSamples = RequireRange(
                maximumFrameSamples,
                1,
                HardMaximumFrameSamples,
                nameof(maximumFrameSamples));
            MaximumQualityObservations = RequireRange(
                maximumQualityObservations,
                1,
                HardMaximumQualityObservations,
                nameof(maximumQualityObservations));
            MaximumObjectCounters = RequireRange(
                maximumObjectCounters,
                0,
                HardMaximumObjectCounters,
                nameof(maximumObjectCounters));
            P95FrameTimeWarningMilliseconds = RequireFinitePositive(
                p95FrameTimeWarningMilliseconds,
                nameof(p95FrameTimeWarningMilliseconds));
            P99FrameTimeWarningMilliseconds = RequireFinitePositive(
                p99FrameTimeWarningMilliseconds,
                nameof(p99FrameTimeWarningMilliseconds));
            if (P99FrameTimeWarningMilliseconds < P95FrameTimeWarningMilliseconds)
            {
                throw new ArgumentException(
                    "The p99 warning threshold cannot be lower than the p95 warning threshold.",
                    nameof(p99FrameTimeWarningMilliseconds));
            }

            TotalManagedAllocationWarningBytes = RequirePositive(
                totalManagedAllocationWarningBytes,
                nameof(totalManagedAllocationWarningBytes));
            PerFrameManagedAllocationWarningBytes = RequirePositive(
                perFrameManagedAllocationWarningBytes,
                nameof(perFrameManagedAllocationWarningBytes));
            SceneLoadWarningMilliseconds = RequireFinitePositive(
                sceneLoadWarningMilliseconds,
                nameof(sceneLoadWarningMilliseconds));
            MemoryWarningBytes = RequirePositive(memoryWarningBytes, nameof(memoryWarningBytes));
            BudgetBreachInvalidatesTechnicalEvidence =
                budgetBreachInvalidatesTechnicalEvidence;

            if (objectBudgets == null)
            {
                throw new ArgumentNullException(nameof(objectBudgets));
            }

            this.objectBudgets = objectBudgets
                .Select(value => value ?? throw new ArgumentException(
                    "Object budgets cannot contain null values.",
                    nameof(objectBudgets)))
                .OrderBy(value => value.CounterId)
                .ToArray();
            if (this.objectBudgets.Length > MaximumObjectCounters)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(objectBudgets),
                    "The configured object budget count exceeds the bounded counter capacity.");
            }

            for (int index = 1; index < this.objectBudgets.Length; index++)
            {
                if (this.objectBudgets[index - 1].CounterId.Equals(
                    this.objectBudgets[index].CounterId))
                {
                    throw new ArgumentException(
                        "Object budget counter IDs must be unique.",
                        nameof(objectBudgets));
                }
            }
        }

        public double WarmUpSeconds { get; }

        public double CaptureSeconds { get; }

        public double MaximumCompletionOverrunSeconds { get; }

        public int MaximumFrameSamples { get; }

        public int MaximumQualityObservations { get; }

        public int MaximumObjectCounters { get; }

        public double P95FrameTimeWarningMilliseconds { get; }

        public double P99FrameTimeWarningMilliseconds { get; }

        public long TotalManagedAllocationWarningBytes { get; }

        public long PerFrameManagedAllocationWarningBytes { get; }

        public double SceneLoadWarningMilliseconds { get; }

        public long MemoryWarningBytes { get; }

        public bool BudgetBreachInvalidatesTechnicalEvidence { get; }

        public IReadOnlyList<EvidencePerformanceObjectBudget> ObjectBudgets
        {
            get { return Array.AsReadOnly(objectBudgets); }
        }

        public EvidencePerformanceObjectBudget FindObjectBudget(StableId counterId)
        {
            if (counterId == null)
            {
                throw new ArgumentNullException(nameof(counterId));
            }

            for (int index = 0; index < objectBudgets.Length; index++)
            {
                if (objectBudgets[index].CounterId.Equals(counterId))
                {
                    return objectBudgets[index];
                }
            }

            return null;
        }

        private static double RequireFiniteRange(
            double value,
            double minimum,
            double maximum,
            string parameterName)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value < minimum
                || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value is outside the bounded performance-capture range.");
            }

            return value;
        }

        private static double RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A performance threshold must be finite and positive.");
            }

            return value;
        }

        private static int RequireRange(int value, int minimum, int maximum, string parameterName)
        {
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value is outside the bounded performance-capture range.");
            }

            return value;
        }

        private static long RequirePositive(long value, string parameterName)
        {
            if (value < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A byte threshold must be positive.");
            }

            return value;
        }
    }

    public sealed class EvidencePerformanceQualityObservation
    {
        internal EvidencePerformanceQualityObservation(
            double observedAtSeconds,
            string profileId,
            bool isChange)
        {
            ObservedAtSeconds = observedAtSeconds;
            ProfileId = profileId;
            IsChange = isChange;
        }

        public double ObservedAtSeconds { get; }

        public string ProfileId { get; }

        public bool IsChange { get; }

        public string ToCanonicalString()
        {
            return "observed_at_seconds="
                + ObservedAtSeconds.ToString("R", CultureInfo.InvariantCulture)
                + ";profile_id=" + ProfileId
                + ";is_change=" + (IsChange ? "true" : "false");
        }
    }

    public sealed class EvidencePerformanceObjectCounterResult
    {
        internal EvidencePerformanceObjectCounterResult(
            StableId counterId,
            long maximumObservedCount,
            long configuredMaximumCount,
            bool wasObserved)
        {
            CounterId = counterId;
            MaximumObservedCount = maximumObservedCount;
            ConfiguredMaximumCount = configuredMaximumCount;
            WasObserved = wasObserved;
        }

        public StableId CounterId { get; }

        public long MaximumObservedCount { get; }

        public long ConfiguredMaximumCount { get; }

        public bool WasObserved { get; }

        public bool IsWithinBudget
        {
            get { return WasObserved && MaximumObservedCount <= ConfiguredMaximumCount; }
        }

        public string ToCanonicalString()
        {
            return "counter_id=" + CounterId
                + ";maximum_observed_count="
                + MaximumObservedCount.ToString(CultureInfo.InvariantCulture)
                + ";configured_maximum_count="
                + ConfiguredMaximumCount.ToString(CultureInfo.InvariantCulture)
                + ";was_observed=" + (WasObserved ? "true" : "false");
        }
    }

    public sealed class EvidencePerformanceIssue
    {
        internal EvidencePerformanceIssue(
            EvidencePerformanceIssueKind kind,
            StableId code,
            StableId subjectId,
            DiagnosticSeverity severity,
            DiagnosticEventPayload payload,
            bool invalidatesTechnicalEvidence)
        {
            Kind = kind;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            SubjectId = subjectId;
            Severity = severity;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            InvalidatesTechnicalEvidence = invalidatesTechnicalEvidence;
        }

        public EvidencePerformanceIssueKind Kind { get; }

        public StableId Code { get; }

        public StableId SubjectId { get; }

        public DiagnosticSeverity Severity { get; }

        public DiagnosticEventPayload Payload { get; }

        public bool InvalidatesTechnicalEvidence { get; }

        public string ToCanonicalString()
        {
            return "kind=" + Kind
                + ";code=" + Code
                + ";subject_id=" + (SubjectId == null ? "null" : SubjectId.ToString())
                + ";invalidates_technical_evidence="
                + (InvalidatesTechnicalEvidence ? "true" : "false")
                + ";payload=" + Payload.ToCanonicalString().Replace("\n", "|");
        }
    }

    public sealed class EvidencePerformanceSummary
    {
        private readonly EvidencePerformanceObjectCounterResult[] objectCounters;
        private readonly EvidencePerformanceQualityObservation[] qualityObservations;
        private readonly EvidencePerformanceIssue[] issues;

        internal EvidencePerformanceSummary(
            EvidencePerformanceCaptureState state,
            double configuredWarmUpSeconds,
            double configuredCaptureSeconds,
            double observedCaptureSeconds,
            int frameSampleCount,
            double p50FrameTimeMilliseconds,
            double p95FrameTimeMilliseconds,
            double p99FrameTimeMilliseconds,
            long totalManagedAllocationBytes,
            long peakManagedAllocationBytes,
            double sceneLoadMilliseconds,
            int sceneLoadSampleCount,
            long peakMemoryBytes,
            IEnumerable<EvidencePerformanceObjectCounterResult> objectCounters,
            IEnumerable<EvidencePerformanceQualityObservation> qualityObservations,
            IEnumerable<EvidencePerformanceIssue> issues)
        {
            State = state;
            ConfiguredWarmUpSeconds = configuredWarmUpSeconds;
            ConfiguredCaptureSeconds = configuredCaptureSeconds;
            ObservedCaptureSeconds = observedCaptureSeconds;
            FrameSampleCount = frameSampleCount;
            P50FrameTimeMilliseconds = p50FrameTimeMilliseconds;
            P95FrameTimeMilliseconds = p95FrameTimeMilliseconds;
            P99FrameTimeMilliseconds = p99FrameTimeMilliseconds;
            TotalManagedAllocationBytes = totalManagedAllocationBytes;
            PeakManagedAllocationBytes = peakManagedAllocationBytes;
            SceneLoadMilliseconds = sceneLoadMilliseconds;
            SceneLoadSampleCount = sceneLoadSampleCount;
            PeakMemoryBytes = peakMemoryBytes;
            this.objectCounters = objectCounters.ToArray();
            this.qualityObservations = qualityObservations.ToArray();
            this.issues = issues.ToArray();
        }

        public EvidencePerformanceCaptureState State { get; }

        public bool IsTechnicallyUsable
        {
            get { return State == EvidencePerformanceCaptureState.Completed; }
        }

        public double ConfiguredWarmUpSeconds { get; }

        public double ConfiguredCaptureSeconds { get; }

        public double ObservedCaptureSeconds { get; }

        public int FrameSampleCount { get; }

        public double P50FrameTimeMilliseconds { get; }

        public double P95FrameTimeMilliseconds { get; }

        public double P99FrameTimeMilliseconds { get; }

        public long TotalManagedAllocationBytes { get; }

        public long PeakManagedAllocationBytes { get; }

        public double SceneLoadMilliseconds { get; }

        public int SceneLoadSampleCount { get; }

        public long PeakMemoryBytes { get; }

        public IReadOnlyList<EvidencePerformanceObjectCounterResult> ObjectCounters
        {
            get { return Array.AsReadOnly(objectCounters); }
        }

        public IReadOnlyList<EvidencePerformanceQualityObservation> QualityObservations
        {
            get { return Array.AsReadOnly(qualityObservations); }
        }

        public IReadOnlyList<EvidencePerformanceIssue> Issues
        {
            get { return Array.AsReadOnly(issues); }
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.Append("state=");
            builder.Append(State.ToString().ToLowerInvariant());
            builder.Append("\nconfigured_warm_up_seconds=");
            builder.Append(ConfiguredWarmUpSeconds.ToString("R", CultureInfo.InvariantCulture));
            builder.Append("\nconfigured_capture_seconds=");
            builder.Append(ConfiguredCaptureSeconds.ToString("R", CultureInfo.InvariantCulture));
            builder.Append("\nobserved_capture_seconds=");
            builder.Append(ObservedCaptureSeconds.ToString("R", CultureInfo.InvariantCulture));
            builder.Append("\nframe_sample_count=");
            builder.Append(FrameSampleCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("\np50_frame_time_milliseconds=");
            builder.Append(P50FrameTimeMilliseconds.ToString("R", CultureInfo.InvariantCulture));
            builder.Append("\np95_frame_time_milliseconds=");
            builder.Append(P95FrameTimeMilliseconds.ToString("R", CultureInfo.InvariantCulture));
            builder.Append("\np99_frame_time_milliseconds=");
            builder.Append(P99FrameTimeMilliseconds.ToString("R", CultureInfo.InvariantCulture));
            builder.Append("\ntotal_managed_allocation_bytes=");
            builder.Append(TotalManagedAllocationBytes.ToString(CultureInfo.InvariantCulture));
            builder.Append("\npeak_managed_allocation_bytes=");
            builder.Append(PeakManagedAllocationBytes.ToString(CultureInfo.InvariantCulture));
            builder.Append("\nscene_load_milliseconds=");
            builder.Append(SceneLoadMilliseconds.ToString("R", CultureInfo.InvariantCulture));
            builder.Append("\nscene_load_sample_count=");
            builder.Append(SceneLoadSampleCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("\npeak_memory_bytes=");
            builder.Append(PeakMemoryBytes.ToString(CultureInfo.InvariantCulture));
            builder.Append("\nobject_counter_count=");
            builder.Append(objectCounters.Length.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < objectCounters.Length; index++)
            {
                builder.Append("\nobject_counter[");
                builder.Append(index.ToString(CultureInfo.InvariantCulture));
                builder.Append("]=");
                builder.Append(objectCounters[index].ToCanonicalString());
            }

            builder.Append("\nquality_observation_count=");
            builder.Append(qualityObservations.Length.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < qualityObservations.Length; index++)
            {
                builder.Append("\nquality_observation[");
                builder.Append(index.ToString(CultureInfo.InvariantCulture));
                builder.Append("]=");
                builder.Append(qualityObservations[index].ToCanonicalString());
            }

            builder.Append("\nissue_count=");
            builder.Append(issues.Length.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < issues.Length; index++)
            {
                builder.Append("\nissue[");
                builder.Append(index.ToString(CultureInfo.InvariantCulture));
                builder.Append("]=");
                builder.Append(issues[index].ToCanonicalString());
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Evidence-only observer for bounded representative performance facts.
    /// Callers supply timestamps and counters. The probe never reads or changes
    /// Unity quality, time scale, simulation, collision, gameplay, or scene state.
    /// </summary>
    public sealed class EvidencePerformanceProbe
    {
        private static readonly StableId PerformanceIssueField =
            StableId.Parse("field.performance-issue-code");
        private static readonly StableId PerformanceSubjectField =
            StableId.Parse("field.performance-subject-id");
        private static readonly StableId PerformanceStateField =
            StableId.Parse("field.performance-capture-state");
        private static readonly StableId CaptureOperation =
            StableId.Parse("operation.performance-capture");

        private readonly EvidencePerformanceBudget budget;
        private readonly EvidenceDiagnosticsRecorder diagnosticsRecorder;
        private readonly List<double> frameTimes = new List<double>();
        private readonly List<EvidencePerformanceQualityObservation> qualityObservations =
            new List<EvidencePerformanceQualityObservation>();
        private readonly List<EvidencePerformanceIssue> issues =
            new List<EvidencePerformanceIssue>();
        private readonly HashSet<string> issueKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<StableId, long> maximumObjectCounts =
            new Dictionary<StableId, long>();
        private readonly HashSet<StableId> observedObjectCounters = new HashSet<StableId>();

        private EvidencePerformanceCaptureState state =
            EvidencePerformanceCaptureState.Configured;
        private double startedAtSeconds;
        private double warmUpEndsAtSeconds;
        private double captureEndsAtSeconds;
        private double lastObservedSeconds;
        private long totalManagedAllocationBytes;
        private long peakManagedAllocationBytes;
        private long peakMemoryBytes;
        private double sceneLoadMilliseconds;
        private int sceneLoadSampleCount;
        private bool diagnosticsPublished;
        private EvidencePerformanceSummary finalSummary;

        public EvidencePerformanceProbe(
            EvidencePerformanceBudget budget,
            EvidenceDiagnosticsRecorder diagnosticsRecorder = null)
        {
            this.budget = budget ?? throw new ArgumentNullException(nameof(budget));
            this.diagnosticsRecorder = diagnosticsRecorder;
            foreach (EvidencePerformanceObjectBudget objectBudget in budget.ObjectBudgets)
            {
                maximumObjectCounts.Add(objectBudget.CounterId, 0L);
            }
        }

        public EvidencePerformanceBudget Budget
        {
            get { return budget; }
        }

        public EvidencePerformanceCaptureState State
        {
            get { return state; }
        }

        public int AcceptedFrameSampleCount
        {
            get { return frameTimes.Count; }
        }

        public void Begin(double nowSeconds, string initialQualityProfileId)
        {
            if (state != EvidencePerformanceCaptureState.Configured)
            {
                throw new InvalidOperationException("A performance probe can begin only once.");
            }

            RequireFiniteNonNegative(nowSeconds, nameof(nowSeconds));
            string profile = RequireProfileId(initialQualityProfileId, nameof(initialQualityProfileId));
            startedAtSeconds = nowSeconds;
            warmUpEndsAtSeconds = nowSeconds + budget.WarmUpSeconds;
            captureEndsAtSeconds = warmUpEndsAtSeconds + budget.CaptureSeconds;
            lastObservedSeconds = nowSeconds;
            qualityObservations.Add(
                new EvidencePerformanceQualityObservation(nowSeconds, profile, false));
            state = budget.WarmUpSeconds > 0d
                ? EvidencePerformanceCaptureState.WarmingUp
                : EvidencePerformanceCaptureState.Capturing;
        }

        public bool RecordFrame(
            double nowSeconds,
            double frameTimeMilliseconds,
            long managedAllocationBytes,
            long memoryBytes,
            IEnumerable<EvidencePerformanceObjectCounterSample> objectCounters)
        {
            EnsureActive();
            RequireFiniteNonNegative(frameTimeMilliseconds, nameof(frameTimeMilliseconds));
            RequireNonNegative(managedAllocationBytes, nameof(managedAllocationBytes));
            RequireNonNegative(memoryBytes, nameof(memoryBytes));

            if (!TryObserveClock(nowSeconds, StableId.Parse("performance.invalid-clock")))
            {
                return false;
            }

            if (nowSeconds < warmUpEndsAtSeconds)
            {
                state = EvidencePerformanceCaptureState.WarmingUp;
                return false;
            }

            state = EvidencePerformanceCaptureState.Capturing;
            if (nowSeconds > captureEndsAtSeconds)
            {
                RegisterCaptureOverrun(nowSeconds);
                return false;
            }

            if (frameTimes.Count >= budget.MaximumFrameSamples)
            {
                RegisterIntegrityIssue(
                    EvidencePerformanceIssueKind.SampleCapacityReached,
                    StableId.Parse("performance.frame-sample-capacity"),
                    null);
                return false;
            }

            Dictionary<StableId, long> suppliedCounters = CanonicalizeObjectCounters(objectCounters);
            foreach (EvidencePerformanceObjectBudget configuredCounter in budget.ObjectBudgets)
            {
                long observedCount;
                if (!suppliedCounters.TryGetValue(configuredCounter.CounterId, out observedCount))
                {
                    RegisterIntegrityIssue(
                        EvidencePerformanceIssueKind.CounterUnavailable,
                        StableId.Parse("performance.object-counter-missing"),
                        configuredCounter.CounterId);
                    continue;
                }

                observedObjectCounters.Add(configuredCounter.CounterId);
                if (observedCount > maximumObjectCounts[configuredCounter.CounterId])
                {
                    maximumObjectCounts[configuredCounter.CounterId] = observedCount;
                }
            }

            frameTimes.Add(frameTimeMilliseconds);
            try
            {
                totalManagedAllocationBytes = checked(
                    totalManagedAllocationBytes + managedAllocationBytes);
            }
            catch (OverflowException)
            {
                totalManagedAllocationBytes = long.MaxValue;
                RegisterIntegrityIssue(
                    EvidencePerformanceIssueKind.SampleCapacityReached,
                    StableId.Parse("performance.allocation-counter-overflow"),
                    null);
            }

            if (managedAllocationBytes > peakManagedAllocationBytes)
            {
                peakManagedAllocationBytes = managedAllocationBytes;
            }

            if (memoryBytes > peakMemoryBytes)
            {
                peakMemoryBytes = memoryBytes;
            }

            return true;
        }

        public void RecordSceneLoad(double startedAtSeconds, double endedAtSeconds)
        {
            EnsureActive();
            RequireFiniteNonNegative(startedAtSeconds, nameof(startedAtSeconds));
            RequireFiniteNonNegative(endedAtSeconds, nameof(endedAtSeconds));
            if (endedAtSeconds < startedAtSeconds)
            {
                RegisterIntegrityIssue(
                    EvidencePerformanceIssueKind.InvalidClock,
                    StableId.Parse("performance.invalid-clock"),
                    StableId.Parse("subject.scene-load"));
                return;
            }

            if (!TryObserveClock(endedAtSeconds, StableId.Parse("performance.invalid-clock")))
            {
                return;
            }

            double durationMilliseconds = (endedAtSeconds - startedAtSeconds) * 1000d;
            sceneLoadMilliseconds += durationMilliseconds;
            sceneLoadSampleCount++;
        }

        public bool RecordQualityProfile(double nowSeconds, string qualityProfileId)
        {
            EnsureActive();
            string profile = RequireProfileId(qualityProfileId, nameof(qualityProfileId));
            if (!TryObserveClock(nowSeconds, StableId.Parse("performance.invalid-clock")))
            {
                return false;
            }

            string current = qualityObservations[qualityObservations.Count - 1].ProfileId;
            if (string.Equals(current, profile, StringComparison.Ordinal))
            {
                return false;
            }

            if (qualityObservations.Count >= budget.MaximumQualityObservations)
            {
                RegisterIntegrityIssue(
                    EvidencePerformanceIssueKind.QualityObservationCapacityReached,
                    StableId.Parse("performance.quality-observation-capacity"),
                    null);
                return false;
            }

            qualityObservations.Add(
                new EvidencePerformanceQualityObservation(nowSeconds, profile, true));
            return true;
        }

        public EvidencePerformanceSummary Complete(double nowSeconds)
        {
            if (finalSummary != null)
            {
                return finalSummary;
            }

            EnsureActive();
            bool clockAccepted = TryObserveClock(
                nowSeconds,
                StableId.Parse("performance.invalid-clock"));
            if (clockAccepted)
            {
                if (nowSeconds < captureEndsAtSeconds)
                {
                    RegisterIntegrityIssue(
                        EvidencePerformanceIssueKind.IncompleteData,
                        StableId.Parse("performance.incomplete-capture-duration"),
                        null);
                }
                else if (nowSeconds > captureEndsAtSeconds + budget.MaximumCompletionOverrunSeconds)
                {
                    RegisterCaptureOverrun(nowSeconds);
                }
            }

            if (frameTimes.Count == 0)
            {
                RegisterIntegrityIssue(
                    EvidencePerformanceIssueKind.IncompleteData,
                    StableId.Parse("performance.incomplete-frame-samples"),
                    null);
            }

            if (sceneLoadSampleCount == 0)
            {
                RegisterIntegrityIssue(
                    EvidencePerformanceIssueKind.IncompleteData,
                    StableId.Parse("performance.incomplete-scene-load"),
                    null);
            }

            foreach (EvidencePerformanceObjectBudget configuredCounter in budget.ObjectBudgets)
            {
                if (!observedObjectCounters.Contains(configuredCounter.CounterId))
                {
                    RegisterIntegrityIssue(
                        EvidencePerformanceIssueKind.CounterUnavailable,
                        StableId.Parse("performance.object-counter-missing"),
                        configuredCounter.CounterId);
                }
            }

            double p50 = CalculatePercentile(frameTimes, 0.50d);
            double p95 = CalculatePercentile(frameTimes, 0.95d);
            double p99 = CalculatePercentile(frameTimes, 0.99d);
            EvaluateBudgets(p95, p99);

            EvidencePerformanceObjectCounterResult[] counterResults = budget.ObjectBudgets
                .Select(value => new EvidencePerformanceObjectCounterResult(
                    value.CounterId,
                    maximumObjectCounts[value.CounterId],
                    value.MaximumCount,
                    observedObjectCounters.Contains(value.CounterId)))
                .ToArray();

            bool hasInvalidatingIssue = issues.Any(value => value.InvalidatesTechnicalEvidence);
            state = hasInvalidatingIssue
                ? EvidencePerformanceCaptureState.Invalid
                : EvidencePerformanceCaptureState.Completed;

            double observedCaptureSeconds = 0d;
            if (clockAccepted && nowSeconds > warmUpEndsAtSeconds)
            {
                observedCaptureSeconds = Math.Min(
                    budget.CaptureSeconds,
                    nowSeconds - warmUpEndsAtSeconds);
            }

            finalSummary = new EvidencePerformanceSummary(
                state,
                budget.WarmUpSeconds,
                budget.CaptureSeconds,
                observedCaptureSeconds,
                frameTimes.Count,
                p50,
                p95,
                p99,
                totalManagedAllocationBytes,
                peakManagedAllocationBytes,
                sceneLoadMilliseconds,
                sceneLoadSampleCount,
                peakMemoryBytes,
                counterResults,
                qualityObservations,
                issues);
            PublishDiagnostics();
            return finalSummary;
        }

        private void EvaluateBudgets(double p95, double p99)
        {
            if (frameTimes.Count > 0 && p95 > budget.P95FrameTimeWarningMilliseconds)
            {
                RegisterBudgetIssue(
                    StableId.Parse("performance.frame-p95-budget"),
                    null,
                    PerformanceMetricKind.TotalFrameTimeMilliseconds,
                    p95,
                    budget.P95FrameTimeWarningMilliseconds,
                    "ms");
            }

            if (frameTimes.Count > 0 && p99 > budget.P99FrameTimeWarningMilliseconds)
            {
                RegisterBudgetIssue(
                    StableId.Parse("performance.frame-p99-budget"),
                    null,
                    PerformanceMetricKind.TotalFrameTimeMilliseconds,
                    p99,
                    budget.P99FrameTimeWarningMilliseconds,
                    "ms");
            }

            if (totalManagedAllocationBytes > budget.TotalManagedAllocationWarningBytes)
            {
                RegisterBudgetIssue(
                    StableId.Parse("performance.total-allocation-budget"),
                    null,
                    PerformanceMetricKind.ManagedAllocationBytes,
                    totalManagedAllocationBytes,
                    budget.TotalManagedAllocationWarningBytes,
                    "bytes");
            }

            if (peakManagedAllocationBytes > budget.PerFrameManagedAllocationWarningBytes)
            {
                RegisterBudgetIssue(
                    StableId.Parse("performance.frame-allocation-budget"),
                    null,
                    PerformanceMetricKind.ManagedAllocationBytes,
                    peakManagedAllocationBytes,
                    budget.PerFrameManagedAllocationWarningBytes,
                    "bytes");
            }

            if (sceneLoadSampleCount > 0
                && sceneLoadMilliseconds > budget.SceneLoadWarningMilliseconds)
            {
                RegisterBudgetIssue(
                    StableId.Parse("performance.scene-load-budget"),
                    null,
                    PerformanceMetricKind.LoadingMilliseconds,
                    sceneLoadMilliseconds,
                    budget.SceneLoadWarningMilliseconds,
                    "ms");
            }

            if (peakMemoryBytes > budget.MemoryWarningBytes)
            {
                RegisterBudgetIssue(
                    StableId.Parse("performance.memory-budget"),
                    null,
                    PerformanceMetricKind.MemoryBytes,
                    peakMemoryBytes,
                    budget.MemoryWarningBytes,
                    "bytes");
            }

            foreach (EvidencePerformanceObjectBudget configuredCounter in budget.ObjectBudgets)
            {
                long observed = maximumObjectCounts[configuredCounter.CounterId];
                if (observedObjectCounters.Contains(configuredCounter.CounterId)
                    && observed > configuredCounter.MaximumCount)
                {
                    RegisterBudgetIssue(
                        StableId.Parse("performance.object-budget"),
                        configuredCounter.CounterId,
                        PerformanceMetricKind.MemoryBytes,
                        observed,
                        configuredCounter.MaximumCount,
                        "count");
                }
            }
        }

        private void RegisterBudgetIssue(
            StableId code,
            StableId subjectId,
            PerformanceMetricKind metricKind,
            double observedValue,
            double threshold,
            string unit)
        {
            string key = BuildIssueKey(code, subjectId);
            if (!issueKeys.Add(key))
            {
                return;
            }

            bool invalidates = budget.BudgetBreachInvalidatesTechnicalEvidence;
            issues.Add(new EvidencePerformanceIssue(
                EvidencePerformanceIssueKind.BudgetExceeded,
                code,
                subjectId,
                DiagnosticSeverity.Warning,
                new PerformanceWarningDiagnosticPayload(
                    metricKind,
                    observedValue,
                    threshold,
                    unit,
                    invalidates),
                invalidates));
        }

        private void RegisterIntegrityIssue(
            EvidencePerformanceIssueKind kind,
            StableId code,
            StableId subjectId)
        {
            string key = BuildIssueKey(code, subjectId);
            if (!issueKeys.Add(key))
            {
                return;
            }

            issues.Add(new EvidencePerformanceIssue(
                kind,
                code,
                subjectId,
                DiagnosticSeverity.Error,
                new PerformanceWarningDiagnosticPayload(
                    PerformanceMetricKind.TotalFrameTimeMilliseconds,
                    1d,
                    0d,
                    "integrity",
                    true),
                true));
        }

        private void RegisterCaptureOverrun(double nowSeconds)
        {
            string key = BuildIssueKey(
                StableId.Parse("performance.capture-overrun"),
                null);
            if (!issueKeys.Add(key))
            {
                return;
            }

            long elapsedMilliseconds = Math.Max(
                2L,
                (long)Math.Ceiling((nowSeconds - startedAtSeconds) * 1000d));
            long limitMilliseconds = Math.Max(
                1L,
                (long)Math.Ceiling(
                    (budget.WarmUpSeconds + budget.CaptureSeconds) * 1000d));
            if (elapsedMilliseconds <= limitMilliseconds)
            {
                elapsedMilliseconds = limitMilliseconds + 1L;
            }

            issues.Add(new EvidencePerformanceIssue(
                EvidencePerformanceIssueKind.CaptureOverrun,
                StableId.Parse("performance.capture-overrun"),
                null,
                DiagnosticSeverity.Error,
                new TimeoutDiagnosticPayload(
                    CaptureOperation,
                    elapsedMilliseconds,
                    limitMilliseconds,
                    true),
                true));
        }

        private bool TryObserveClock(double nowSeconds, StableId issueCode)
        {
            if (double.IsNaN(nowSeconds)
                || double.IsInfinity(nowSeconds)
                || nowSeconds < 0d
                || nowSeconds < lastObservedSeconds)
            {
                RegisterIntegrityIssue(
                    EvidencePerformanceIssueKind.InvalidClock,
                    issueCode,
                    null);
                return false;
            }

            lastObservedSeconds = nowSeconds;
            return true;
        }

        private Dictionary<StableId, long> CanonicalizeObjectCounters(
            IEnumerable<EvidencePerformanceObjectCounterSample> objectCounters)
        {
            if (objectCounters == null)
            {
                objectCounters = new EvidencePerformanceObjectCounterSample[0];
            }

            EvidencePerformanceObjectCounterSample[] samples = objectCounters.ToArray();
            if (samples.Length > budget.MaximumObjectCounters)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(objectCounters),
                    "An object-counter sample exceeds the bounded counter capacity.");
            }

            Dictionary<StableId, long> result = new Dictionary<StableId, long>();
            for (int index = 0; index < samples.Length; index++)
            {
                EvidencePerformanceObjectCounterSample sample = samples[index];
                if (sample == null)
                {
                    throw new ArgumentException(
                        "Object-counter samples cannot contain null values.",
                        nameof(objectCounters));
                }

                if (budget.FindObjectBudget(sample.CounterId) == null)
                {
                    throw new ArgumentException(
                        "Only configured object-budget counters may be sampled.",
                        nameof(objectCounters));
                }

                if (result.ContainsKey(sample.CounterId))
                {
                    throw new ArgumentException(
                        "An object counter may appear only once in a frame sample.",
                        nameof(objectCounters));
                }

                result.Add(sample.CounterId, sample.Count);
            }

            return result;
        }

        private void PublishDiagnostics()
        {
            if (diagnosticsPublished || diagnosticsRecorder == null)
            {
                diagnosticsPublished = true;
                return;
            }

            foreach (EvidencePerformanceIssue issue in issues)
            {
                List<EvidenceDiagnosticField> fields = new List<EvidenceDiagnosticField>
                {
                    EvidenceDiagnosticField.Public(
                        PerformanceIssueField,
                        issue.Code.ToString()),
                    EvidenceDiagnosticField.Public(
                        PerformanceStateField,
                        state.ToString().ToLowerInvariant()),
                };
                if (issue.SubjectId != null)
                {
                    fields.Add(EvidenceDiagnosticField.Public(
                        PerformanceSubjectField,
                        issue.SubjectId.ToString()));
                }

                diagnosticsRecorder.Record(issue.Severity, issue.Payload, fields);
            }

            diagnosticsPublished = true;
        }

        private void EnsureActive()
        {
            if (state != EvidencePerformanceCaptureState.WarmingUp
                && state != EvidencePerformanceCaptureState.Capturing)
            {
                throw new InvalidOperationException(
                    "The performance probe must be active and not yet completed.");
            }
        }

        private static double CalculatePercentile(IList<double> values, double percentile)
        {
            if (values == null || values.Count == 0)
            {
                return 0d;
            }

            double[] sorted = values.OrderBy(value => value).ToArray();
            double rank = percentile * (sorted.Length - 1);
            int lowerIndex = (int)Math.Floor(rank);
            int upperIndex = (int)Math.Ceiling(rank);
            if (lowerIndex == upperIndex)
            {
                return sorted[lowerIndex];
            }

            double weight = rank - lowerIndex;
            return sorted[lowerIndex]
                + ((sorted[upperIndex] - sorted[lowerIndex]) * weight);
        }

        private static string RequireProfileId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
            {
                throw new ArgumentException(
                    "A bounded quality-profile ID is required.",
                    parameterName);
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid = (character >= 'a' && character <= 'z')
                    || (character >= '0' && character <= '9')
                    || character == '-'
                    || character == '_'
                    || character == '.';
                if (!valid)
                {
                    throw new FormatException(
                        "Quality-profile IDs must use lowercase ASCII letters, digits, dot, dash, or underscore.");
                }
            }

            return value;
        }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and non-negative.");
            }
        }

        private static void RequireNonNegative(long value, string parameterName)
        {
            if (value < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value cannot be negative.");
            }
        }

        private static string BuildIssueKey(StableId code, StableId subjectId)
        {
            return code + "|" + (subjectId == null ? string.Empty : subjectId.ToString());
        }
    }
}

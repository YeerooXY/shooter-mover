using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Diagnostics;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.Performance.EvidenceHarness
{
    public sealed class EvidencePerformanceProbeTests
    {
        private const string ProbeTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidencePerformanceProbe";
        private const string BudgetTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidencePerformanceBudget";
        private const string ObjectBudgetTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidencePerformanceObjectBudget";
        private const string CounterSampleTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidencePerformanceObjectCounterSample";
        private const string RecorderTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceDiagnosticsRecorder";
        private const string LoaderTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceRunConfigurationLoader";

        private readonly List<string> temporaryDirectories = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (string directory in temporaryDirectories)
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }

            temporaryDirectories.Clear();
        }

        [Test]
        public void WarmUp_IsExcludedFromFramesPercentilesAndAllocations()
        {
            object probe = CreateProbe(CreateBudget(warmUpSeconds: 1d, captureSeconds: 2d));
            Invoke(probe, "Begin", 0d, "quality.medium");
            Invoke(probe, "RecordSceneLoad", 0.1d, 0.2d);

            Assert.That(
                (bool)Invoke(probe, "RecordFrame", 0.5d, 100d, 1000L, 10000L, EmptyCounters()),
                Is.False);
            Assert.That(
                (bool)Invoke(probe, "RecordFrame", 1d, 10d, 100L, 12000L, EmptyCounters()),
                Is.True);
            Assert.That(
                (bool)Invoke(probe, "RecordFrame", 2d, 30d, 300L, 14000L, EmptyCounters()),
                Is.True);

            object summary = Invoke(probe, "Complete", 3d);

            Assert.That(GetProperty<int>(summary, "FrameSampleCount"), Is.EqualTo(2));
            Assert.That(GetProperty<double>(summary, "P50FrameTimeMilliseconds"), Is.EqualTo(20d));
            Assert.That(GetProperty<double>(summary, "P95FrameTimeMilliseconds"), Is.EqualTo(29d));
            Assert.That(GetProperty<double>(summary, "P99FrameTimeMilliseconds"), Is.EqualTo(29.8d).Within(0.0001d));
            Assert.That(GetProperty<long>(summary, "TotalManagedAllocationBytes"), Is.EqualTo(400L));
            Assert.That(GetProperty<long>(summary, "PeakManagedAllocationBytes"), Is.EqualTo(300L));
            Assert.That(GetProperty<long>(summary, "PeakMemoryBytes"), Is.EqualTo(14000L));
            Assert.That(GetProperty<double>(summary, "SceneLoadMilliseconds"), Is.EqualTo(100d).Within(0.0001d));
            Assert.That(GetProperty<object>(summary, "State").ToString(), Is.EqualTo("Completed"));
        }

        [Test]
        public void BoundedCapture_RejectsSamplesBeyondCapacityAndCreatesInvalidity()
        {
            object probe = CreateProbe(CreateBudget(
                warmUpSeconds: 0d,
                captureSeconds: 1d,
                maximumFrameSamples: 2));
            Invoke(probe, "Begin", 0d, "quality.medium");
            Invoke(probe, "RecordSceneLoad", 0d, 0.01d);

            Assert.That((bool)Invoke(probe, "RecordFrame", 0.1d, 10d, 1L, 100L, EmptyCounters()), Is.True);
            Assert.That((bool)Invoke(probe, "RecordFrame", 0.2d, 20d, 1L, 100L, EmptyCounters()), Is.True);
            Assert.That((bool)Invoke(probe, "RecordFrame", 0.3d, 30d, 1L, 100L, EmptyCounters()), Is.False);

            object summary = Invoke(probe, "Complete", 1d);

            Assert.That(GetProperty<int>(summary, "FrameSampleCount"), Is.EqualTo(2));
            Assert.That(GetProperty<object>(summary, "State").ToString(), Is.EqualTo("Invalid"));
            Assert.That(IssueKinds(summary), Does.Contain("SampleCapacityReached"));
            Assert.That(IssueCodes(summary), Does.Contain("performance.frame-sample-capacity"));
        }

        [Test]
        public void IncompleteData_EmitsCs012PerformanceInvalidity()
        {
            object recorder = CreateRecorder("incomplete");
            object probe = CreateProbe(
                CreateBudget(warmUpSeconds: 1d, captureSeconds: 2d),
                recorder);
            Invoke(probe, "Begin", 0d, "quality.medium");
            Invoke(probe, "RecordSceneLoad", 0d, 0.1d);

            object summary = Invoke(probe, "Complete", 0.5d);
            RunTechnicalValidity validity =
                GetProperty<RunTechnicalValidity>(recorder, "CurrentTechnicalValidity");

            Assert.That(GetProperty<object>(summary, "State").ToString(), Is.EqualTo("Invalid"));
            Assert.That(IssueCodes(summary), Does.Contain("performance.incomplete-capture-duration"));
            Assert.That(IssueCodes(summary), Does.Contain("performance.incomplete-frame-samples"));
            Assert.That(validity.Contains(RunInvalidityReason.PerformanceBudgetBreach), Is.True);
            Assert.That(RecordedEventKinds(recorder), Does.Contain(DiagnosticEventKind.PerformanceWarning));
        }

        [Test]
        public void InvalidClock_RejectsTheSampleAndEmitsCs012Invalidity()
        {
            object recorder = CreateRecorder("invalid-clock");
            object probe = CreateProbe(
                CreateBudget(warmUpSeconds: 0d, captureSeconds: 3d),
                recorder);
            Invoke(probe, "Begin", 10d, "quality.medium");
            Invoke(probe, "RecordSceneLoad", 10d, 10.1d);

            bool accepted = (bool)Invoke(
                probe,
                "RecordFrame",
                9d,
                12d,
                0L,
                100L,
                EmptyCounters());
            object summary = Invoke(probe, "Complete", 13d);
            RunTechnicalValidity validity =
                GetProperty<RunTechnicalValidity>(recorder, "CurrentTechnicalValidity");

            Assert.That(accepted, Is.False);
            Assert.That(IssueKinds(summary), Does.Contain("InvalidClock"));
            Assert.That(IssueCodes(summary), Does.Contain("performance.invalid-clock"));
            Assert.That(validity.Contains(RunInvalidityReason.PerformanceBudgetBreach), Is.True);
        }

        [Test]
        public void OverrunSample_EmitsCs012TimeoutInvalidity()
        {
            object recorder = CreateRecorder("overrun");
            object probe = CreateProbe(
                CreateBudget(
                    warmUpSeconds: 0d,
                    captureSeconds: 1d,
                    maximumCompletionOverrunSeconds: 1d),
                recorder);
            Invoke(probe, "Begin", 0d, "quality.medium");
            Invoke(probe, "RecordSceneLoad", 0d, 0.1d);

            bool accepted = (bool)Invoke(
                probe,
                "RecordFrame",
                1.1d,
                12d,
                0L,
                100L,
                EmptyCounters());
            object summary = Invoke(probe, "Complete", 1.1d);
            RunTechnicalValidity validity =
                GetProperty<RunTechnicalValidity>(recorder, "CurrentTechnicalValidity");

            Assert.That(accepted, Is.False);
            Assert.That(IssueKinds(summary), Does.Contain("CaptureOverrun"));
            Assert.That(IssuePayloads(summary).Any(value => value is TimeoutDiagnosticPayload), Is.True);
            Assert.That(validity.Contains(RunInvalidityReason.Timeout), Is.True);
            Assert.That(RecordedEventKinds(recorder), Does.Contain(DiagnosticEventKind.Timeout));
        }

        [Test]
        public void ObjectBudgetCounters_RecordMaximumAndWarningWithoutHiddenMutation()
        {
            StableId counterId = StableId.Parse("object.test-fixtures");
            object budget = CreateBudget(
                warmUpSeconds: 0d,
                captureSeconds: 1d,
                budgetBreachInvalidates: false,
                objectBudgets: new[] { Tuple.Create(counterId, 2L) });
            object probe = CreateProbe(budget);
            Invoke(probe, "Begin", 0d, "quality.medium");
            Invoke(probe, "RecordSceneLoad", 0d, 0.01d);
            Invoke(probe, "RecordFrame", 0.1d, 10d, 0L, 100L, Counters(Tuple.Create(counterId, 1L)));
            Invoke(probe, "RecordFrame", 0.9d, 20d, 0L, 100L, Counters(Tuple.Create(counterId, 3L)));

            object summary = Invoke(probe, "Complete", 1d);
            object result = CollectionItems(GetProperty<object>(summary, "ObjectCounters")).Single();
            EvidencePerformanceIssueView issue = IssueViews(summary)
                .Single(value => value.Code == "performance.object-budget");

            Assert.That(GetProperty<StableId>(result, "CounterId"), Is.EqualTo(counterId));
            Assert.That(GetProperty<long>(result, "MaximumObservedCount"), Is.EqualTo(3L));
            Assert.That(GetProperty<long>(result, "ConfiguredMaximumCount"), Is.EqualTo(2L));
            Assert.That(GetProperty<bool>(result, "IsWithinBudget"), Is.False);
            Assert.That(issue.SubjectId, Is.EqualTo(counterId));
            Assert.That(issue.Invalidates, Is.False);
            Assert.That(((PerformanceWarningDiagnosticPayload)issue.Payload).Unit, Is.EqualTo("count"));
            Assert.That(GetProperty<object>(summary, "State").ToString(), Is.EqualTo("Completed"));
        }

        [Test]
        public void QualityProfileRecording_IsBoundedEvidenceOnlyAndDoesNotChangeMetrics()
        {
            object probe = CreateProbe(CreateBudget(warmUpSeconds: 0d, captureSeconds: 1d));
            Invoke(probe, "Begin", 0d, "quality.low");
            Invoke(probe, "RecordSceneLoad", 0d, 0.01d);
            Invoke(probe, "RecordFrame", 0.1d, 10d, 1L, 100L, EmptyCounters());
            Assert.That((bool)Invoke(probe, "RecordQualityProfile", 0.2d, "quality.medium"), Is.True);
            Assert.That((bool)Invoke(probe, "RecordQualityProfile", 0.3d, "quality.medium"), Is.False);
            Invoke(probe, "RecordFrame", 0.5d, 20d, 2L, 200L, EmptyCounters());
            Assert.That((bool)Invoke(probe, "RecordQualityProfile", 0.6d, "quality.high"), Is.True);
            Invoke(probe, "RecordFrame", 0.9d, 30d, 3L, 300L, EmptyCounters());

            object summary = Invoke(probe, "Complete", 1d);
            object[] observations = CollectionItems(
                GetProperty<object>(summary, "QualityObservations"));

            Assert.That(observations, Has.Length.EqualTo(3));
            Assert.That(observations.Select(value => GetProperty<string>(value, "ProfileId")),
                Is.EqualTo(new[] { "quality.low", "quality.medium", "quality.high" }));
            Assert.That(GetProperty<double>(summary, "P50FrameTimeMilliseconds"), Is.EqualTo(20d));
            Assert.That(GetProperty<long>(summary, "TotalManagedAllocationBytes"), Is.EqualTo(6L));
            Assert.That(CollectionItems(GetProperty<object>(summary, "Issues")), Is.Empty);
            Assert.That(GetProperty<object>(summary, "State").ToString(), Is.EqualTo("Completed"));
        }

        private object CreateBudget(
            double warmUpSeconds = 0d,
            double captureSeconds = 1d,
            double maximumCompletionOverrunSeconds = 0.25d,
            int maximumFrameSamples = 128,
            bool budgetBreachInvalidates = false,
            IEnumerable<Tuple<StableId, long>> objectBudgets = null)
        {
            Type objectBudgetType = ResolveType(ObjectBudgetTypeName);
            Tuple<StableId, long>[] budgetValues = objectBudgets == null
                ? new Tuple<StableId, long>[0]
                : objectBudgets.ToArray();
            Array budgetArray = Array.CreateInstance(objectBudgetType, budgetValues.Length);
            for (int index = 0; index < budgetValues.Length; index++)
            {
                budgetArray.SetValue(
                    Activator.CreateInstance(
                        objectBudgetType,
                        budgetValues[index].Item1,
                        budgetValues[index].Item2),
                    index);
            }

            return Activator.CreateInstance(
                ResolveType(BudgetTypeName),
                warmUpSeconds,
                captureSeconds,
                maximumCompletionOverrunSeconds,
                maximumFrameSamples,
                8,
                Math.Max(8, budgetValues.Length),
                100d,
                200d,
                1000000L,
                100000L,
                5000d,
                1000000L,
                budgetBreachInvalidates,
                budgetArray);
        }

        private object CreateProbe(object budget, object recorder = null)
        {
            return Activator.CreateInstance(ResolveType(ProbeTypeName), budget, recorder);
        }

        private object CreateRecorder(string suffix)
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "shooter-mover-eh007-" + suffix + "-" + Guid.NewGuid().ToString("N"));
            temporaryDirectories.Add(directory);

            Type loaderType = ResolveType(LoaderTypeName);
            object loadResult = loaderType
                .GetMethod("Load", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { CanonicalConfigurationJson() });
            object configuration = GetProperty<object>(loadResult, "Configuration");
            return Activator.CreateInstance(
                ResolveType(RecorderTypeName),
                directory,
                StableId.Parse("run.eh007-" + suffix),
                configuration);
        }

        private Array EmptyCounters()
        {
            return Array.CreateInstance(ResolveType(CounterSampleTypeName), 0);
        }

        private Array Counters(params Tuple<StableId, long>[] values)
        {
            Type counterType = ResolveType(CounterSampleTypeName);
            Array result = Array.CreateInstance(counterType, values.Length);
            for (int index = 0; index < values.Length; index++)
            {
                result.SetValue(
                    Activator.CreateInstance(counterType, values[index].Item1, values[index].Item2),
                    index);
            }

            return result;
        }

        private static string[] IssueKinds(object summary)
        {
            return IssueViews(summary).Select(value => value.Kind).ToArray();
        }

        private static string[] IssueCodes(object summary)
        {
            return IssueViews(summary).Select(value => value.Code).ToArray();
        }

        private static DiagnosticEventPayload[] IssuePayloads(object summary)
        {
            return IssueViews(summary).Select(value => value.Payload).ToArray();
        }

        private static EvidencePerformanceIssueView[] IssueViews(object summary)
        {
            return CollectionItems(GetProperty<object>(summary, "Issues"))
                .Select(value => new EvidencePerformanceIssueView(
                    GetProperty<object>(value, "Kind").ToString(),
                    GetProperty<StableId>(value, "Code").ToString(),
                    GetProperty<StableId>(value, "SubjectId"),
                    GetProperty<bool>(value, "InvalidatesTechnicalEvidence"),
                    GetProperty<DiagnosticEventPayload>(value, "Payload")))
                .ToArray();
        }

        private static DiagnosticEventKind[] RecordedEventKinds(object recorder)
        {
            return CollectionItems(GetProperty<object>(recorder, "Events"))
                .Select(value => ((DiagnosticEventEnvelope)value).EventKind)
                .ToArray();
        }

        private static object[] CollectionItems(object collection)
        {
            return ((IEnumerable)collection).Cast<object>().ToArray();
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Missing method " + methodName);
            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property " + propertyName);
            return (T)property.GetValue(target, null);
        }

        private static Type ResolveType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail("Could not resolve type " + fullName);
            return null;
        }

        private static string CanonicalConfigurationJson()
        {
            return "{\n"
                + "  \"schema\": \"shooter-mover.evidence-run-configuration\",\n"
                + "  \"version\": 1,\n"
                + "  \"runSeed\": 104729,\n"
                + "  \"identityReference\": \"sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\n"
                + "  \"intentFixtureVersion\": 1,\n"
                + "  \"qualityProfile\": \"Medium\",\n"
                + "  \"locale\": \"en-US\",\n"
                + "  \"viewport\": {\n"
                + "    \"width\": 1280,\n"
                + "    \"height\": 720,\n"
                + "    \"fullscreen\": false\n"
                + "  },\n"
                + "  \"diagnostics\": {\n"
                + "    \"maxEventCount\": 4096,\n"
                + "    \"maxEventPayloadBytes\": 4096,\n"
                + "    \"maxLogBytes\": 8388608,\n"
                + "    \"retainedLogCount\": 3\n"
                + "  },\n"
                + "  \"timeouts\": {\n"
                + "    \"setupSeconds\": 30,\n"
                + "    \"smokeRunSeconds\": 120,\n"
                + "    \"shutdownSeconds\": 15\n"
                + "  }\n"
                + "}\n";
        }

        private sealed class EvidencePerformanceIssueView
        {
            public EvidencePerformanceIssueView(
                string kind,
                string code,
                StableId subjectId,
                bool invalidates,
                DiagnosticEventPayload payload)
            {
                Kind = kind;
                Code = code;
                SubjectId = subjectId;
                Invalidates = invalidates;
                Payload = payload;
            }

            public string Kind { get; }

            public string Code { get; }

            public StableId SubjectId { get; }

            public bool Invalidates { get; }

            public DiagnosticEventPayload Payload { get; }
        }
    }
}

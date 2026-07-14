using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Diagnostics;
using ShooterMover.Contracts.Identity;
using ShooterMover.Contracts.Mission;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.EvidenceHarness
{
    public sealed class EvidenceDiagnosticsRecorderTests
    {
        private const string RecorderTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceDiagnosticsRecorder";
        private const string FieldTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceDiagnosticField";
        private const string LoaderTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceRunConfigurationLoader";

        private const string SourceCommit =
            "1234567890abcdef1234567890abcdef12345678";
        private const string PackageFingerprint =
            "sha256:1c3e5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e7b9d0f2a4c6e8b1d3f";
        private const string ContentFingerprint =
            "sha256:2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e7b9d0f2a4c6e8b1d3f5a7c9e0b2d4f";
        private const string ArtifactFingerprint =
            "sha256:3e5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e7b9d0f2a4c6e8b1d3f5a";
        private const string IdentityReference =
            "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

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
        public void EventOrder_CapturesIdentityLifecycleCommandsFaultsAndWarnings()
        {
            object recorder = CreateRecorder("event-order");
            Invoke(recorder, "StartSession", Build());
            Invoke(recorder, "RestartSession", Id("run.previous-evidence"));
            Invoke(
                recorder,
                "RecordDiagnosticCommand",
                Id("diagnostic.inspect-0001"),
                DiagnosticCommandKind.Inspection,
                DiagnosticCommandEvidenceEffect.EvidenceSafe,
                MissionVersion(),
                new MissionSequence(4L),
                null);
            Invoke(recorder, "RecordException", Id("error.handled"), false, null);
            Invoke(
                recorder,
                "RecordTimeout",
                Id("operation.asset-scan"),
                31L,
                30L,
                false,
                null);
            Invoke(
                recorder,
                "RecordPerformanceWarning",
                PerformanceMetricKind.TotalFrameTimeMilliseconds,
                21d,
                20d,
                "ms",
                false,
                null);
            Invoke(recorder, "RecordMissingAsset", Id("asset.missing-room"), null);
            Invoke(recorder, "EndSession", RunEndKind.Completed);

            DiagnosticEventEnvelope[] events = Events(recorder);
            Assert.That(
                events.Select(value => value.Sequence.Value),
                Is.EqualTo(Enumerable.Range(1, events.Length).Select(value => (long)value)));
            Assert.That(
                events.Select(value => value.EventKind),
                Is.EqualTo(new[]
                {
                    DiagnosticEventKind.RunStarted,
                    DiagnosticEventKind.RunRestarted,
                    DiagnosticEventKind.DiagnosticCommand,
                    DiagnosticEventKind.Exception,
                    DiagnosticEventKind.Timeout,
                    DiagnosticEventKind.PerformanceWarning,
                    DiagnosticEventKind.MissingAsset,
                    DiagnosticEventKind.RunEnded,
                }));
            Assert.That(
                ((RunStartedDiagnosticPayload)events[0].Payload).BuildIdentity,
                Is.EqualTo(Build()));
        }

        [Test]
        public void Rotation_KeepsEveryPartWithinTheConfiguredFileBound()
        {
            object recorder = CreateRecorder(
                "rotation",
                maxEventCount: 64,
                maxEventPayloadBytes: 4096,
                maxLogBytes: 4096,
                retainedLogCount: 3);
            Invoke(recorder, "StartSession", Build());

            for (int index = 0; index < 10; index++)
            {
                Invoke(
                    recorder,
                    "RecordPerformanceWarning",
                    PerformanceMetricKind.ManagedAllocationBytes,
                    2000d + index,
                    1000d,
                    "bytes",
                    false,
                    null);
            }

            Invoke(recorder, "EndSession", RunEndKind.Completed);
            Invoke(recorder, "FinalizeSession", HumanFunEvidence.NotRecorded);

            string[] paths = LogPaths(recorder);
            Assert.That(paths.Length, Is.GreaterThan(1));
            Assert.That(paths.Length, Is.LessThanOrEqualTo(3));
            Assert.That(paths.Select(path => new FileInfo(path).Length),
                Is.All.LessThanOrEqualTo(4096L));
            Assert.That(paths.Select(File.ReadAllText).All(text => text.Contains("run_id=run.rotation")),
                Is.True);
        }

        [Test]
        public void Redaction_NeverWritesMachinePathOrSecretBytes()
        {
            object recorder = CreateRecorder("redaction");
            Invoke(recorder, "StartSession", Build());

            Array fields = CreateFieldArray(
                CreatePublicField(
                    Id("field.path"),
                    "C:\\Users\\Nemo\\evidence\\session.log"),
                CreatePublicField(
                    Id("field.authorization"),
                    "bearer-secret-value"));
            Invoke(
                recorder,
                "RecordException",
                Id("error.redaction-test"),
                false,
                fields);
            Invoke(recorder, "EndSession", RunEndKind.Completed);
            Invoke(recorder, "FinalizeSession", HumanFunEvidence.NotRecorded);

            string log = string.Join("\n", LogPaths(recorder).Select(File.ReadAllText));
            Assert.That(log, Does.Not.Contain("C:\\Users\\Nemo"));
            Assert.That(log, Does.Not.Contain("bearer-secret-value"));
            Assert.That(log, Does.Contain("[redacted:machine-local-path]"));
            Assert.That(log, Does.Contain("[redacted:secret-or-credential]"));
        }

        [Test]
        public void Overflow_AppendsExplicitCapacityEventAndClosesWithoutSilentLoss()
        {
            object recorder = CreateRecorder(
                "overflow",
                maxEventCount: 3,
                maxEventPayloadBytes: 4096,
                maxLogBytes: 4096,
                retainedLogCount: 1);
            Invoke(recorder, "StartSession", Build());
            Invoke(
                recorder,
                "RecordPerformanceWarning",
                PerformanceMetricKind.LoadingMilliseconds,
                51d,
                50d,
                "ms",
                false,
                null);

            DiagnosticEventEnvelope overflow =
                (DiagnosticEventEnvelope)Invoke(recorder, "EndSession", RunEndKind.Completed);
            DiagnosticEventEnvelope[] events = Events(recorder);

            Assert.That(events, Has.Length.EqualTo(3));
            Assert.That(overflow.EventKind, Is.EqualTo(DiagnosticEventKind.CapacityReached));
            Assert.That(events[2], Is.SameAs(overflow));
            Assert.That(
                ((CapacityReachedDiagnosticPayload)overflow.Payload).Dimension,
                Is.EqualTo(DiagnosticCapacityDimension.EventCount));
            Assert.That(GetProperty<bool>(recorder, "WasTruncated"), Is.True);
            Assert.That(
                GetProperty<RunTechnicalValidity>(recorder, "CurrentTechnicalValidity")
                    .Contains(RunInvalidityReason.DiagnosticsCapacityReached),
                Is.True);
            AssertInvocationThrows<InvalidOperationException>(
                () => Invoke(recorder, "RecordMissingAsset", Id("asset.after-overflow"), null));
        }

        [Test]
        public void DuplicateStart_IsRecordedAsMonotonicTechnicalInvalidity()
        {
            object recorder = CreateRecorder("duplicate-start");
            Invoke(recorder, "StartSession", Build());
            Invoke(recorder, "StartSession", Build());
            Invoke(recorder, "EndSession", RunEndKind.Completed);

            object result = Invoke(
                recorder,
                "FinalizeSession",
                new HumanFunEvidence(HumanFunEvidenceOutcome.Positive));
            RunTechnicalValidity validity = Assessment(result).TechnicalValidity;

            Assert.That(validity.IsTechnicallyValid, Is.False);
            Assert.That(validity.Contains(RunInvalidityReason.DuplicateRunStart), Is.True);
            Assert.That(Events(recorder).Count(value => value.EventKind == DiagnosticEventKind.RunStarted),
                Is.EqualTo(2));
        }

        [Test]
        public void EndWithoutStart_IsRetainedAndInvalid()
        {
            object recorder = CreateRecorder("end-without-start");
            Invoke(recorder, "EndSession", RunEndKind.Completed);

            object result = Invoke(
                recorder,
                "FinalizeSession",
                HumanFunEvidence.NotRecorded);
            RunTechnicalValidity validity = Assessment(result).TechnicalValidity;

            Assert.That(validity.Contains(RunInvalidityReason.RunEndWithoutStart), Is.True);
            Assert.That(validity.Contains(RunInvalidityReason.MissingRunStart), Is.True);
            Assert.That(Events(recorder)[0].EventKind, Is.EqualTo(DiagnosticEventKind.RunEnded));
        }

        [Test]
        public void InvalidityCannotBeClearedAndNegativeFunEvidenceDoesNotCauseIt()
        {
            object invalidRecorder = CreateRecorder("invalid-monotonic");
            Invoke(invalidRecorder, "StartSession", Build());
            Invoke(invalidRecorder, "RecordMissingAsset", Id("asset.required-room"), null);
            Invoke(
                invalidRecorder,
                "RecordPerformanceWarning",
                PerformanceMetricKind.TotalFrameTimeMilliseconds,
                21d,
                20d,
                "ms",
                false,
                null);
            Invoke(invalidRecorder, "EndSession", RunEndKind.Completed);
            object invalidResult = Invoke(
                invalidRecorder,
                "FinalizeSession",
                new HumanFunEvidence(
                    HumanFunEvidenceOutcome.Negative,
                    Id("observation.not-fun")));
            EvidenceAssessment invalidAssessment = Assessment(invalidResult);

            Assert.That(invalidAssessment.TechnicalValidity.IsTechnicallyValid, Is.False);
            Assert.That(
                invalidAssessment.TechnicalValidity.Contains(
                    RunInvalidityReason.MissingRequiredAsset),
                Is.True);
            Assert.That(
                invalidAssessment.HumanFunEvidence.Outcome,
                Is.EqualTo(HumanFunEvidenceOutcome.Negative));

            object validRecorder = CreateRecorder("valid-negative-fun");
            Invoke(validRecorder, "StartSession", Build());
            Invoke(validRecorder, "EndSession", RunEndKind.Completed);
            object validResult = Invoke(
                validRecorder,
                "FinalizeSession",
                new HumanFunEvidence(
                    HumanFunEvidenceOutcome.Negative,
                    Id("observation.not-fun")));
            EvidenceAssessment validAssessment = Assessment(validResult);

            Assert.That(validAssessment.TechnicalValidity.IsTechnicallyValid, Is.True);
            Assert.That(
                validAssessment.HumanFunEvidence.Outcome,
                Is.EqualTo(HumanFunEvidenceOutcome.Negative));

            TestContext.WriteLine("VALID EXAMPLE LOG\n" + ReadAllLogs(validRecorder));
            TestContext.WriteLine("INTENTIONALLY INVALID EXAMPLE LOG\n" + ReadAllLogs(invalidRecorder));
        }

        [Test]
        public void MalformedEnvelope_IsRejectedBeforeFileOrValidityMutation()
        {
            object recorder = CreateRecorder("malformed");
            DiagnosticBounds bounds = GetProperty<DiagnosticBounds>(recorder, "ContractBounds");
            DiagnosticEventEnvelope malformed = new DiagnosticEventEnvelope(
                DiagnosticsSchemaVersion.Current,
                bounds,
                Id("diagnostic-event.external-0001"),
                Id("run.other"),
                new DiagnosticEventSequence(1L),
                DiagnosticSeverity.Information,
                new RunStartedDiagnosticPayload(Build()));

            string path = LogPaths(recorder).Single();
            long beforeBytes = new FileInfo(path).Length;
            AssertInvocationThrows<ArgumentException>(
                () => Invoke(recorder, "Append", malformed));

            Assert.That(Events(recorder), Is.Empty);
            Assert.That(new FileInfo(path).Length, Is.EqualTo(beforeBytes));
            Assert.That(
                GetProperty<RunTechnicalValidity>(recorder, "CurrentTechnicalValidity")
                    .Contains(RunInvalidityReason.MissingRunStart),
                Is.True);
        }

        [Test]
        public void Export_IsExplicitLocalCopyAndNeverOverwritesExistingFiles()
        {
            object recorder = CreateRecorder("export");
            Invoke(recorder, "StartSession", Build());
            Invoke(recorder, "EndSession", RunEndKind.Completed);
            Invoke(recorder, "FinalizeSession", HumanFunEvidence.NotRecorded);

            string exportDirectory = NewTemporaryDirectory("export-target");
            IEnumerable<string> exported =
                (IEnumerable<string>)Invoke(recorder, "ExportTo", exportDirectory);
            string[] exportedPaths = exported.ToArray();

            Assert.That(exportedPaths, Has.Length.EqualTo(LogPaths(recorder).Length));
            Assert.That(exportedPaths.All(File.Exists), Is.True);
            AssertInvocationThrows<IOException>(
                () => Invoke(recorder, "ExportTo", exportDirectory));
        }

        private object CreateRecorder(
            string runValue,
            int maxEventCount = 32,
            int maxEventPayloadBytes = 4096,
            int maxLogBytes = 8388608,
            int retainedLogCount = 3)
        {
            object configuration = LoadConfiguration(
                maxEventCount,
                maxEventPayloadBytes,
                maxLogBytes,
                retainedLogCount);
            string directory = NewTemporaryDirectory(runValue);
            Type recorderType = FindType(RecorderTypeName);
            return Activator.CreateInstance(
                recorderType,
                new object[]
                {
                    directory,
                    Id("run." + runValue),
                    configuration,
                });
        }

        private static object LoadConfiguration(
            int maxEventCount,
            int maxEventPayloadBytes,
            int maxLogBytes,
            int retainedLogCount)
        {
            string json =
                "{\n"
                + "  \"schema\": \"shooter-mover.evidence-run-configuration\",\n"
                + "  \"version\": 1,\n"
                + "  \"runSeed\": 104729,\n"
                + "  \"identityReference\": \"" + IdentityReference + "\",\n"
                + "  \"intentFixtureVersion\": 1,\n"
                + "  \"qualityProfile\": \"Medium\",\n"
                + "  \"locale\": \"en-US\",\n"
                + "  \"viewport\": {\n"
                + "    \"width\": 1280,\n"
                + "    \"height\": 720,\n"
                + "    \"fullscreen\": false\n"
                + "  },\n"
                + "  \"diagnostics\": {\n"
                + "    \"maxEventCount\": "
                + maxEventCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\n"
                + "    \"maxEventPayloadBytes\": "
                + maxEventPayloadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\n"
                + "    \"maxLogBytes\": "
                + maxLogBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\n"
                + "    \"retainedLogCount\": "
                + retainedLogCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\n"
                + "  },\n"
                + "  \"timeouts\": {\n"
                + "    \"setupSeconds\": 30,\n"
                + "    \"smokeRunSeconds\": 120,\n"
                + "    \"shutdownSeconds\": 15\n"
                + "  }\n"
                + "}\n";

            Type loaderType = FindType(LoaderTypeName);
            object loadResult = loaderType.GetMethod(
                "Load",
                BindingFlags.Public | BindingFlags.Static).Invoke(
                    null,
                    new object[] { json });
            Assert.That(GetProperty<bool>(loadResult, "IsValid"), Is.True);
            return GetProperty<object>(loadResult, "Configuration");
        }

        private static object CreatePublicField(StableId key, string value)
        {
            Type fieldType = FindType(FieldTypeName);
            return fieldType.GetMethod(
                "Public",
                BindingFlags.Public | BindingFlags.Static).Invoke(
                    null,
                    new object[] { key, value });
        }

        private static Array CreateFieldArray(params object[] fields)
        {
            Type fieldType = FindType(FieldTypeName);
            Array values = Array.CreateInstance(fieldType, fields.Length);
            for (int index = 0; index < fields.Length; index++)
            {
                values.SetValue(fields[index], index);
            }

            return values;
        }

        private string NewTemporaryDirectory(string suffix)
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "ShooterMover-EH003-"
                    + suffix
                    + "-"
                    + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            temporaryDirectories.Add(directory);
            return directory;
        }

        private static DiagnosticEventEnvelope[] Events(object recorder)
        {
            return ((IEnumerable<DiagnosticEventEnvelope>)GetProperty<object>(
                recorder,
                "Events")).ToArray();
        }

        private static string[] LogPaths(object recorder)
        {
            return ((IEnumerable<string>)GetProperty<object>(
                recorder,
                "LogPaths")).ToArray();
        }

        private static string ReadAllLogs(object recorder)
        {
            return string.Join("\n", LogPaths(recorder).Select(File.ReadAllText));
        }

        private static EvidenceAssessment Assessment(object sessionResult)
        {
            return GetProperty<EvidenceAssessment>(sessionResult, "Assessment");
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo[] candidates = target.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo method = candidates.Single(
                value => value.Name == methodName
                    && value.GetParameters().Length == arguments.Length);
            return method.Invoke(target, arguments);
        }

        private static void AssertInvocationThrows<TException>(TestDelegate action)
            where TException : Exception
        {
            TargetInvocationException wrapper =
                Assert.Throws<TargetInvocationException>(action);
            Assert.That(wrapper.InnerException, Is.TypeOf<TException>());
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target, null);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail("Unable to find loaded type " + fullName + ".");
            return null;
        }

        private static BuildIdentity Build()
        {
            return BuildIdentity.CreateDevelopment(
                SourceCommit,
                "6000.3.19f1",
                PackageFingerprint,
                ContentFingerprint,
                1,
                false,
                ArtifactFingerprint);
        }

        private static MissionPayloadVersion MissionVersion()
        {
            return new MissionPayloadVersion(
                1,
                ContentVersion.Create(1, ContentFingerprint));
        }

        private static StableId Id(string text)
        {
            return StableId.Parse(text);
        }
    }
}

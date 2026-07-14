using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Diagnostics;
using ShooterMover.Contracts.Identity;
using ShooterMover.Contracts.Mission;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class DiagnosticsContractTests
    {
        private const string SourceCommit =
            "1234567890abcdef1234567890abcdef12345678";
        private const string PackageFingerprint =
            "sha256:1c3e5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e7b9d0f2a4c6e8b1d3f";
        private const string ContentFingerprint =
            "sha256:2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e7b9d0f2a4c6e8b1d3f5a7c9e0b2d4f";
        private const string ArtifactFingerprint =
            "sha256:3e5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e7b9d0f2a4c6e8b1d3f5a";
        private const string ItemFingerprintA =
            "sha256:4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e7b9d0f2a4c6e8b1d3f5a7c9e0b2d4f6a";
        private const string ItemFingerprintB =
            "sha256:5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e7b9d0f2a4c6e8b1d3f5a7c";

        [Test]
        public void EventBatch_ShuffledInputHasCanonicalSequenceOrder()
        {
            DiagnosticBounds bounds = Bounds();
            DiagnosticEventEnvelope first = Event(
                1L,
                new RunStartedDiagnosticPayload(Build()),
                bounds);
            DiagnosticEventEnvelope second = Event(
                2L,
                new PerformanceWarningDiagnosticPayload(
                    PerformanceMetricKind.TotalFrameTimeMilliseconds,
                    24d,
                    20d,
                    "ms",
                    false),
                bounds,
                DiagnosticSeverity.Warning);
            DiagnosticEventEnvelope third = Event(
                3L,
                new RunEndedDiagnosticPayload(RunEndKind.Completed),
                bounds);

            DiagnosticEventBatch shuffled = new DiagnosticEventBatch(
                DiagnosticsSchemaVersion.Current,
                bounds,
                RunId(),
                new[] { third, first, second });
            DiagnosticEventBatch ordered = new DiagnosticEventBatch(
                DiagnosticsSchemaVersion.Current,
                bounds,
                RunId(),
                new[] { first, second, third });

            Assert.That(
                shuffled.Events.Select(value => value.Sequence.Value),
                Is.EqualTo(new[] { 1L, 2L, 3L }));
            Assert.That(shuffled, Is.EqualTo(ordered));
            Assert.That(shuffled.ToCanonicalString(), Is.EqualTo(ordered.ToCanonicalString()));
            Assert.That(shuffled.GetHashCode(), Is.EqualTo(ordered.GetHashCode()));

            Assert.Throws<ArgumentException>(
                () => new DiagnosticEventBatch(
                    DiagnosticsSchemaVersion.Current,
                    bounds,
                    RunId(),
                    new[] { first, Event(1L, new RunEndedDiagnosticPayload(RunEndKind.Completed), bounds) }));
        }

        [Test]
        public void Bounds_RejectExcessEventsAttributesAndPublicValueLength()
        {
            DiagnosticBounds small = new DiagnosticBounds(2, 1, 4, 2, 100L);
            DiagnosticAttribute first = DiagnosticAttribute.CreatePublic(Id("field.mode"), "test");
            DiagnosticAttribute second = DiagnosticAttribute.CreatePublic(Id("field.seed"), "1234");

            Assert.Throws<ArgumentException>(
                () => Event(
                    1L,
                    new RunStartedDiagnosticPayload(Build()),
                    small,
                    DiagnosticSeverity.Information,
                    new[] { first, second }));

            Assert.Throws<ArgumentException>(
                () => Event(
                    1L,
                    new RunStartedDiagnosticPayload(Build()),
                    small,
                    DiagnosticSeverity.Information,
                    new[] { DiagnosticAttribute.CreatePublic(Id("field.locale"), "en-US") }));

            DiagnosticEventEnvelope start = Event(
                1L,
                new RunStartedDiagnosticPayload(Build()),
                small);
            DiagnosticEventEnvelope end = Event(
                2L,
                new RunEndedDiagnosticPayload(RunEndKind.Completed),
                small);

            Assert.Throws<ArgumentException>(
                () => new DiagnosticEventBatch(
                    DiagnosticsSchemaVersion.Current,
                    small,
                    RunId(),
                    new[] { start, end }));

            Assert.Throws<ArgumentException>(
                () => new DiagnosticEventBatch(
                    DiagnosticsSchemaVersion.Current,
                    small,
                    RunId(),
                    new[]
                    {
                        start,
                        end,
                        Event(3L, new CrashDiagnosticPayload(Id("crash.test")), small),
                    }));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DiagnosticBounds(
                    DiagnosticBounds.HardMaximumEvents + 1,
                    1,
                    4,
                    1,
                    100L));
        }

        [Test]
        public void Redaction_RejectsSensitivePublicValuesAndNeverStoresRawValue()
        {
            Assert.Throws<FormatException>(
                () => DiagnosticAttribute.CreatePublic(
                    Id("field.contact"),
                    "player@example.com"));
            Assert.Throws<FormatException>(
                () => DiagnosticAttribute.CreatePublic(
                    Id("field.path"),
                    "C:\\Users\\Nemo\\log.txt"));
            Assert.Throws<FormatException>(
                () => DiagnosticAttribute.CreatePublic(
                    Id("field.url"),
                    "https://example.com/support?id=1"));

            DiagnosticAttribute redacted = DiagnosticAttribute.CreateRedacted(
                Id("field.contact"),
                DiagnosticRedactionReason.PersonalData);
            string canonical = redacted.ToCanonicalString();

            Assert.That(redacted.IsRedacted, Is.True);
            Assert.That(redacted.PublicValue, Is.Null);
            Assert.That(canonical, Is.EqualTo("field.contact=[redacted:personal-data]"));
            Assert.That(canonical, Does.Not.Contain("player@example.com"));
        }

        [Test]
        public void Version_IsExplicitAndUnsupportedVersionFailsClosed()
        {
            DiagnosticsSchemaVersion version = DiagnosticsSchemaVersion.Current;

            Assert.That(version.Value, Is.EqualTo(1));
            Assert.That(
                version.ToCanonicalString(),
                Is.EqualTo("diagnostics_schema_version=1"));
            Assert.Throws<NotSupportedException>(() => new DiagnosticsSchemaVersion(2));
            Assert.Throws<NotSupportedException>(() => new DiagnosticsSchemaVersion(0));
        }

        [Test]
        public void Validity_AccumulatesInvalidatingCommandAndPerformanceReasonsMonotonically()
        {
            DiagnosticBounds bounds = Bounds();
            RunValidityAccumulator accumulator = RunValidityAccumulator.Empty(RunId());
            accumulator = accumulator.Apply(
                Event(1L, new RunStartedDiagnosticPayload(Build()), bounds));
            accumulator = accumulator.Apply(
                Event(
                    2L,
                    new DiagnosticCommandDiagnosticPayload(
                        Id("diagnostic.fault-injection-0001"),
                        DiagnosticCommandKind.FaultInjection,
                        DiagnosticCommandEvidenceEffect.InvalidatesTechnicalEvidence,
                        MissionVersion(),
                        new MissionSequence(4L)),
                    bounds,
                    DiagnosticSeverity.Warning));
            accumulator = accumulator.Apply(
                Event(
                    3L,
                    new PerformanceWarningDiagnosticPayload(
                        PerformanceMetricKind.TotalFrameTimeMilliseconds,
                        55d,
                        50d,
                        "ms",
                        true),
                    bounds,
                    DiagnosticSeverity.Warning));
            accumulator = accumulator.Apply(
                Event(
                    4L,
                    new RunEndedDiagnosticPayload(RunEndKind.Completed),
                    bounds));

            RunTechnicalValidity initial = accumulator.FinalizeForEvidence();
            Assert.That(initial.IsTechnicallyValid, Is.False);
            Assert.That(initial.Contains(RunInvalidityReason.FaultInjectionUsed), Is.True);
            Assert.That(initial.Contains(RunInvalidityReason.PerformanceBudgetBreach), Is.True);

            accumulator = accumulator.Apply(
                Event(
                    5L,
                    new EvidenceValidityDiagnosticPayload(initial),
                    bounds));
            RunTechnicalValidity afterSummary = accumulator.FinalizeForEvidence();

            Assert.That(afterSummary, Is.EqualTo(initial));
            Assert.That(afterSummary.Contains(RunInvalidityReason.EventAfterRunEnd), Is.False);
        }

        [Test]
        public void EvidenceSafeInspectionCommand_DoesNotInvalidateRun()
        {
            DiagnosticBounds bounds = Bounds();
            RunValidityAccumulator accumulator = RunValidityAccumulator.Empty(RunId())
                .Apply(Event(1L, new RunStartedDiagnosticPayload(Build()), bounds))
                .Apply(
                    Event(
                        2L,
                        new DiagnosticCommandDiagnosticPayload(
                            Id("diagnostic.inspect-0001"),
                            DiagnosticCommandKind.Inspection,
                            DiagnosticCommandEvidenceEffect.EvidenceSafe,
                            MissionVersion(),
                            new MissionSequence(0L)),
                        bounds))
                .Apply(Event(3L, new RunEndedDiagnosticPayload(RunEndKind.Completed), bounds));

            Assert.That(accumulator.FinalizeForEvidence().IsTechnicallyValid, Is.True);
        }

        [Test]
        public void StateAlteringCommand_CannotBeMislabelledEvidenceSafe()
        {
            Assert.Throws<ArgumentException>(
                () => new DiagnosticCommandDiagnosticPayload(
                    Id("diagnostic.override-0001"),
                    DiagnosticCommandKind.MissionStateOverride,
                    DiagnosticCommandEvidenceEffect.EvidenceSafe,
                    MissionVersion(),
                    new MissionSequence(8L)));

            DiagnosticCommandDiagnosticPayload payload = new DiagnosticCommandDiagnosticPayload(
                Id("diagnostic.override-0001"),
                DiagnosticCommandKind.MissionStateOverride,
                DiagnosticCommandEvidenceEffect.InvalidatesTechnicalEvidence,
                MissionVersion(),
                new MissionSequence(8L));

            Assert.That(payload.MissionPayloadVersion, Is.EqualTo(MissionVersion()));
            Assert.That(payload.ObservedMissionSequence.Value, Is.EqualTo(8L));
        }

        [Test]
        public void CrashBeforeRunEnd_FinalizesAsExplicitTechnicalInvalidity()
        {
            DiagnosticBounds bounds = Bounds();
            RunValidityAccumulator accumulator = RunValidityAccumulator.Empty(RunId())
                .Apply(Event(1L, new RunStartedDiagnosticPayload(Build()), bounds))
                .Apply(
                    Event(
                        2L,
                        new CrashDiagnosticPayload(Id("crash.unhandled-0001")),
                        bounds,
                        DiagnosticSeverity.Critical));

            RunTechnicalValidity validity = accumulator.FinalizeForEvidence();

            Assert.That(validity.IsTechnicallyValid, Is.False);
            Assert.That(validity.Contains(RunInvalidityReason.CrashBeforeRunEnd), Is.True);
            Assert.That(validity.Contains(RunInvalidityReason.MissingRunEnd), Is.False);
        }

        [Test]
        public void CapacityReached_MustBeFinalAndInvalidatesEvidence()
        {
            DiagnosticBounds bounds = new DiagnosticBounds(3, 2, 32, 3, 1000L);
            DiagnosticEventEnvelope start = Event(
                1L,
                new RunStartedDiagnosticPayload(Build()),
                bounds);
            DiagnosticEventEnvelope warning = Event(
                2L,
                new PerformanceWarningDiagnosticPayload(
                    PerformanceMetricKind.ManagedAllocationBytes,
                    1d,
                    0d,
                    "bytes",
                    false),
                bounds,
                DiagnosticSeverity.Warning);
            DiagnosticEventEnvelope capacity = Event(
                3L,
                new CapacityReachedDiagnosticPayload(
                    DiagnosticCapacityDimension.EventCount,
                    bounds.MaxEvents),
                bounds,
                DiagnosticSeverity.Error);

            DiagnosticEventBatch batch = new DiagnosticEventBatch(
                DiagnosticsSchemaVersion.Current,
                bounds,
                RunId(),
                new[] { capacity, start, warning });

            Assert.That(batch.CapacityReached, Is.True);
            Assert.That(batch.Events[2].EventKind, Is.EqualTo(DiagnosticEventKind.CapacityReached));

            RunValidityAccumulator accumulator = RunValidityAccumulator.Empty(RunId());
            foreach (DiagnosticEventEnvelope diagnosticEvent in batch.Events)
            {
                accumulator = accumulator.Apply(diagnosticEvent);
            }

            Assert.That(
                accumulator.FinalizeForEvidence()
                    .Contains(RunInvalidityReason.DiagnosticsCapacityReached),
                Is.True);

            Assert.Throws<ArgumentException>(
                () => new DiagnosticEventBatch(
                    DiagnosticsSchemaVersion.Current,
                    bounds,
                    RunId(),
                    new[] { capacity, start }));

            Assert.Throws<ArgumentException>(
                () => new DiagnosticEventBatch(
                    DiagnosticsSchemaVersion.Current,
                    bounds,
                    RunId(),
                    new[]
                    {
                        start,
                        warning,
                        Event(3L, new RunEndedDiagnosticPayload(RunEndKind.Completed), bounds),
                    }));
        }

        [Test]
        public void TechnicalInvalidity_RemainsIndependentFromHumanFunEvidence()
        {
            DiagnosticBounds bounds = Bounds();
            RunTechnicalValidity invalid = RunValidityAccumulator.Empty(RunId())
                .Apply(Event(1L, new RunStartedDiagnosticPayload(Build()), bounds))
                .Apply(
                    Event(
                        2L,
                        new CrashDiagnosticPayload(Id("crash.test-0001")),
                        bounds,
                        DiagnosticSeverity.Critical))
                .FinalizeForEvidence();

            EvidenceAssessment positive = new EvidenceAssessment(
                invalid,
                new HumanFunEvidence(
                    HumanFunEvidenceOutcome.Positive,
                    Id("observation.movement-felt-great")));
            EvidenceAssessment negative = new EvidenceAssessment(
                invalid,
                new HumanFunEvidence(
                    HumanFunEvidenceOutcome.Negative,
                    Id("observation.weapon-readability-poor")));

            Assert.That(positive.TechnicalValidity, Is.EqualTo(negative.TechnicalValidity));
            Assert.That(positive.TechnicalValidity.IsTechnicallyValid, Is.False);
            Assert.That(
                positive.HumanFunEvidence.Outcome,
                Is.EqualTo(HumanFunEvidenceOutcome.Positive));
            Assert.That(
                negative.HumanFunEvidence.Outcome,
                Is.EqualTo(HumanFunEvidenceOutcome.Negative));
            Assert.That(positive.ToCanonicalString(), Does.Contain("technical_validity=invalid"));
            Assert.That(positive.ToCanonicalString(), Does.Contain("human_fun_outcome=positive"));
        }

        [Test]
        public void SupportBundleManifest_IsCanonicalBoundedLocalAndPrivacySafe()
        {
            DiagnosticBounds bounds = new DiagnosticBounds(8, 4, 64, 4, 500L);
            RunTechnicalValidity validity = CompletedValidity(bounds);
            SupportBundleItem events = SupportBundleItem.Included(
                Id("bundle.diagnostic-events"),
                SupportBundleItemKind.DiagnosticEvents,
                200L,
                ItemFingerprintA);
            SupportBundleItem identity = SupportBundleItem.Included(
                Id("bundle.build-identity"),
                SupportBundleItemKind.BuildIdentity,
                100L,
                ItemFingerprintB);
            SupportBundleItem redacted = SupportBundleItem.Redacted(
                Id("bundle.crash-marker"),
                SupportBundleItemKind.CrashMarker,
                DiagnosticRedactionReason.MachineLocalPath);

            SupportBundleManifest shuffled = new SupportBundleManifest(
                DiagnosticsSchemaVersion.Current,
                Build(),
                RunId(),
                validity,
                bounds,
                new[] { redacted, events, identity });
            SupportBundleManifest ordered = new SupportBundleManifest(
                DiagnosticsSchemaVersion.Current,
                Build(),
                RunId(),
                validity,
                bounds,
                new[] { identity, events, redacted });
            string canonical = shuffled.ToCanonicalString();

            Assert.That(shuffled, Is.EqualTo(ordered));
            Assert.That(shuffled.TotalIncludedBytes, Is.EqualTo(300L));
            Assert.That(
                shuffled.Items.Select(value => value.ItemKind),
                Is.EqualTo(
                    new[]
                    {
                        SupportBundleItemKind.BuildIdentity,
                        SupportBundleItemKind.DiagnosticEvents,
                        SupportBundleItemKind.CrashMarker,
                    }));
            Assert.That(canonical, Does.Contain("export_scope=local-explicit-private"));
            Assert.That(canonical, Does.Contain("contains_raw_personal_data=false"));
            Assert.That(canonical, Does.Contain("redaction_reason=machine-local-path"));
            Assert.That(canonical, Does.Not.Contain("C:\\"));
            Assert.That(canonical, Does.Not.Contain("https://"));
            Assert.That(canonical, Does.Not.Contain("@"));
            Assert.That(
                typeof(SupportBundleManifest).GetProperties()
                    .Any(property => property.Name.Contains("Path")
                        || property.Name.Contains("Upload")
                        || property.Name.Contains("Endpoint")),
                Is.False);

            Assert.Throws<ArgumentException>(
                () => new SupportBundleManifest(
                    DiagnosticsSchemaVersion.Current,
                    Build(),
                    RunId(),
                    validity,
                    bounds,
                    new[]
                    {
                        SupportBundleItem.Included(
                            Id("bundle.too-large"),
                            SupportBundleItemKind.DiagnosticEvents,
                            501L,
                            ItemFingerprintA),
                    }));

            Assert.Throws<ArgumentException>(
                () => new SupportBundleManifest(
                    DiagnosticsSchemaVersion.Current,
                    Build(),
                    RunId(),
                    validity,
                    bounds,
                    new[] { identity, events, redacted, SupportBundleItem.Omitted(
                        Id("bundle.performance"),
                        SupportBundleItemKind.PerformanceCapture,
                        DiagnosticRedactionReason.UnnecessaryFreeText), SupportBundleItem.Omitted(
                        Id("bundle.configuration"),
                        SupportBundleItemKind.EvidenceConfiguration,
                        DiagnosticRedactionReason.ExternalIdentifier) }));
        }

        [Test]
        public void SupportBundleManifest_RejectsDuplicateLogicalItemIds()
        {
            DiagnosticBounds bounds = Bounds();
            RunTechnicalValidity validity = CompletedValidity(bounds);

            Assert.Throws<ArgumentException>(
                () => new SupportBundleManifest(
                    DiagnosticsSchemaVersion.Current,
                    Build(),
                    RunId(),
                    validity,
                    bounds,
                    new[]
                    {
                        SupportBundleItem.Included(
                            Id("bundle.same-item"),
                            SupportBundleItemKind.BuildIdentity,
                            10L,
                            ItemFingerprintA),
                        SupportBundleItem.Included(
                            Id("bundle.same-item"),
                            SupportBundleItemKind.CrashMarker,
                            10L,
                            ItemFingerprintB),
                    }));
        }

        [Test]
        public void Contracts_ConsumeIdentityAndMissionMessagesAndRemainImmutableUnityFree()
        {
            DiagnosticCommandDiagnosticPayload command =
                new DiagnosticCommandDiagnosticPayload(
                    Id("diagnostic.inspect-0001"),
                    DiagnosticCommandKind.Inspection,
                    DiagnosticCommandEvidenceEffect.EvidenceSafe,
                    MissionVersion(),
                    new MissionSequence(7L));
            RunStartedDiagnosticPayload start = new RunStartedDiagnosticPayload(Build());

            Assert.That(start.BuildIdentity, Is.EqualTo(Build()));
            Assert.That(command.MissionPayloadVersion, Is.EqualTo(MissionVersion()));
            Assert.That(command.ObservedMissionSequence, Is.EqualTo(new MissionSequence(7L)));

            Type[] immutableTypes =
            {
                typeof(DiagnosticsSchemaVersion),
                typeof(DiagnosticBounds),
                typeof(DiagnosticEventSequence),
                typeof(DiagnosticAttribute),
                typeof(RunStartedDiagnosticPayload),
                typeof(RunEndedDiagnosticPayload),
                typeof(RunRestartedDiagnosticPayload),
                typeof(DiagnosticCommandDiagnosticPayload),
                typeof(PerformanceWarningDiagnosticPayload),
                typeof(ExceptionDiagnosticPayload),
                typeof(TimeoutDiagnosticPayload),
                typeof(MissingAssetDiagnosticPayload),
                typeof(CrashDiagnosticPayload),
                typeof(CapacityReachedDiagnosticPayload),
                typeof(EvidenceValidityDiagnosticPayload),
                typeof(DiagnosticEventEnvelope),
                typeof(DiagnosticEventBatch),
                typeof(RunValidityAccumulator),
                typeof(RunTechnicalValidity),
                typeof(HumanFunEvidence),
                typeof(EvidenceAssessment),
                typeof(SupportBundleItem),
                typeof(SupportBundleManifest),
            };

            foreach (Type type in immutableTypes)
            {
                Assert.That(type.IsSealed, Is.True, type.FullName + " must be sealed.");
                foreach (PropertyInfo property in type.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public))
                {
                    Assert.That(
                        property.CanWrite,
                        Is.False,
                        type.FullName + "." + property.Name + " must not be settable.");
                }
            }

            Assert.That(typeof(DiagnosticEventPayload).IsAbstract, Is.True);
            Assert.That(
                typeof(DiagnosticEventPayload).GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
            Assert.That(
                typeof(DiagnosticEventEnvelope).Assembly.GetReferencedAssemblies()
                    .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);
        }

        private static RunTechnicalValidity CompletedValidity(DiagnosticBounds bounds)
        {
            return RunValidityAccumulator.Empty(RunId())
                .Apply(Event(1L, new RunStartedDiagnosticPayload(Build()), bounds))
                .Apply(Event(2L, new RunEndedDiagnosticPayload(RunEndKind.Completed), bounds))
                .FinalizeForEvidence();
        }

        private static DiagnosticEventEnvelope Event(
            long sequence,
            DiagnosticEventPayload payload,
            DiagnosticBounds bounds,
            DiagnosticSeverity severity = DiagnosticSeverity.Information,
            IEnumerable<DiagnosticAttribute> attributes = null)
        {
            return new DiagnosticEventEnvelope(
                DiagnosticsSchemaVersion.Current,
                bounds,
                Id("diagnostic.event-" + sequence.ToString("D4")),
                RunId(),
                new DiagnosticEventSequence(sequence),
                severity,
                payload,
                attributes);
        }

        private static DiagnosticBounds Bounds()
        {
            return new DiagnosticBounds(8, 4, 64, 6, 1000L);
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

        private static StableId RunId()
        {
            return Id("run.factory-evidence-0001");
        }

        private static StableId Id(string text)
        {
            return StableId.Parse(text);
        }
    }
}

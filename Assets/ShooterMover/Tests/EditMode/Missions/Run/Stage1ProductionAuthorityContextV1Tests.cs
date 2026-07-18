using NUnit.Framework;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed partial class LevelRunCoordinatorV1Tests
    {
        [SetUp]
        public void ClearStage1ProductionAuthorityContext()
        {
            ProductionSessionAuthorityContextV1.ClearForTests();
        }

        [TearDown]
        public void RestoreStage1ProductionAuthorityContext()
        {
            ProductionSessionAuthorityContextV1.ClearForTests();
        }

        [Test]
        public void ProductionAuthorityContext_ConsumesExactBundleOnce()
        {
            Fixture fixture = Fixture.Create("stage1-authority-context");
            Stage1ProductionAuthorityBundleV1 expected = CreateBundle(
                fixture,
                "stage1-authority-context");

            Stage1ProductionAuthorityContextV1.Capture(expected);

            Stage1ProductionAuthorityBundleV1 first;
            Stage1ProductionAuthorityBundleV1 second;
            Assert.That(
                Stage1ProductionAuthorityContextV1.TryConsume(out first),
                Is.True);
            Assert.That(first, Is.SameAs(expected));
            Assert.That(
                Stage1ProductionAuthorityContextV1.TryConsume(out second),
                Is.False);
            Assert.That(second, Is.Null);
            Assert.That(Stage1ProductionAuthorityContextV1.HasPendingBundle, Is.False);
        }

        [Test]
        public void ProductionAuthorityContext_RejectsConflictingPendingBundle()
        {
            Fixture fixture = Fixture.Create("stage1-authority-conflict");
            Stage1ProductionAuthorityBundleV1 first = CreateBundle(
                fixture,
                "stage1-authority-conflict-a");
            Stage1ProductionAuthorityBundleV1 conflicting = CreateBundle(
                fixture,
                "stage1-authority-conflict-b");

            Stage1ProductionAuthorityContextV1.Capture(first);

            Assert.Throws<System.InvalidOperationException>(() =>
                Stage1ProductionAuthorityContextV1.Capture(conflicting));
            Stage1ProductionAuthorityBundleV1 consumed;
            Assert.That(
                Stage1ProductionAuthorityContextV1.TryConsume(out consumed),
                Is.True);
            Assert.That(consumed, Is.SameAs(first));
        }

        [Test]
        public void ProductionSessionContext_PreparesSameBootstrapBundleForStage1()
        {
            Fixture fixture = Fixture.Create("production-session-context");
            Stage1ProductionAuthorityBundleV1 bundle = CreateBundle(
                fixture,
                "production-session-context");
            var token = new object();
            ProductionSessionAuthorityContextV1.CaptureOwner(
                token,
                fixture.Route,
                bundle);

            string rejectionCode;
            Assert.That(
                ProductionSessionAuthorityContextV1.TryPrepareStage1(
                    fixture.Route,
                    out rejectionCode),
                Is.True,
                rejectionCode);

            Stage1ProductionAuthorityBundleV1 consumed;
            Assert.That(
                Stage1ProductionAuthorityContextV1.TryConsume(out consumed),
                Is.True);
            Assert.That(consumed, Is.SameAs(bundle));
            ProductionSessionAuthorityContextV1.ReleaseOwner(token);
        }

        [Test]
        public void ProductionSessionContext_RejectsDifferentAuthorityOwner()
        {
            Fixture fixture = Fixture.Create("production-session-owner-conflict");
            Stage1ProductionAuthorityBundleV1 bundle = CreateBundle(
                fixture,
                "production-session-owner-conflict");
            var first = new object();
            var second = new object();
            ProductionSessionAuthorityContextV1.CaptureOwner(
                first,
                fixture.Route,
                bundle);

            Assert.Throws<System.InvalidOperationException>(() =>
                ProductionSessionAuthorityContextV1.CaptureOwner(
                    second,
                    fixture.Route,
                    bundle));
        }

        private static Stage1ProductionAuthorityBundleV1 CreateBundle(
            Fixture fixture,
            string suffix)
        {
            return new Stage1ProductionAuthorityBundleV1(
                new FixedRunIdFactory(StableId.Create("run", suffix)),
                new HoldingsLevelRunLoadoutResolverV1(
                    fixture.Holdings,
                    fixture.Catalog),
                fixture.Rewards,
                fixture.MissionResults,
                new MissionRunAuthorityCheckpointV1(
                    fixture.Holdings.Sequence,
                    fixture.Holdings.ExportSnapshot().Fingerprint,
                    fixture.ExistingPort.OpeningSequence,
                    fixture.ExistingPort.OpeningFingerprint));
        }
    }
}

using NUnit.Framework;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed partial class LevelRunCoordinatorV1Tests
    {
        [Test]
        public void Stage1ComposerBuildsCanonicalLevelOneSession()
        {
            Fixture fixture = Fixture.Create("stage1-compose");
            var composer = new Stage1ProductionRunComposerV1();
            var request = new Stage1RunCompositionRequestV1(
                fixture.Route,
                fixture.ModeId,
                LevelRunCoordinatorV1.Level1StableId,
                new FixedRunIdFactory(
                    StableId.Parse("run.stage1-compose")),
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

            Stage1RunCompositionResultV1 result = composer.Compose(request);

            Assert.That(result.Succeeded, Is.True, result.RejectionCode);
            Assert.That(
                result.Status,
                Is.EqualTo(Stage1RunCompositionStatusV1.Composed));
            Assert.That(result.Session, Is.Not.Null);
            Assert.That(
                result.Session.Coordinator.SelectedLevelStableId,
                Is.EqualTo(LevelRunCoordinatorV1.Level1StableId));
            Assert.That(
                result.Session.CurrentRoomStableId,
                Is.EqualTo(
                    ShooterMover.Content.Definitions.Missions.Rooms
                        .Level1RoomGraphDefinitionV1.EntryRoomStableId));
            Assert.That(fixture.MissionResults.Sequence, Is.Zero);
        }

        [Test]
        public void Stage1ComposerRejectsUnsupportedModeBeforeCreatingRun()
        {
            Fixture fixture = Fixture.Create("stage1-compose-wrong-mode");
            var composer = new Stage1ProductionRunComposerV1();
            var request = new Stage1RunCompositionRequestV1(
                fixture.Route,
                StableId.Parse("play-mode.multiplayer"),
                LevelRunCoordinatorV1.Level1StableId,
                new FixedRunIdFactory(
                    StableId.Parse("run.stage1-compose-wrong-mode")),
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

            Stage1RunCompositionResultV1 result = composer.Compose(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.Status,
                Is.EqualTo(Stage1RunCompositionStatusV1.InvalidRequest));
            Assert.That(
                result.RejectionCode,
                Is.EqualTo("stage1-selected-mode-unsupported"));
            Assert.That(result.Session, Is.Null);
            Assert.That(fixture.MissionResults.Sequence, Is.Zero);
            Assert.That(fixture.ExistingPort.ProjectCalls, Is.Zero);
        }

        [Test]
        public void Stage1ComposerRejectsUnsupportedLevelBeforeCreatingRun()
        {
            Fixture fixture = Fixture.Create("stage1-compose-wrong-level");
            var composer = new Stage1ProductionRunComposerV1();
            var request = new Stage1RunCompositionRequestV1(
                fixture.Route,
                fixture.ModeId,
                StableId.Parse("level.stage-2"),
                new FixedRunIdFactory(
                    StableId.Parse("run.stage1-compose-wrong-level")),
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

            Stage1RunCompositionResultV1 result = composer.Compose(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.Status,
                Is.EqualTo(Stage1RunCompositionStatusV1.InvalidRequest));
            Assert.That(
                result.RejectionCode,
                Is.EqualTo("stage1-selected-level-unsupported"));
            Assert.That(result.Session, Is.Null);
            Assert.That(fixture.MissionResults.Sequence, Is.Zero);
            Assert.That(fixture.ExistingPort.ProjectCalls, Is.Zero);
        }

        [Test]
        public void Stage1ComposerRejectsMissingAuthorityDependencyBeforeMutation()
        {
            Fixture fixture = Fixture.Create("stage1-compose-missing");
            var composer = new Stage1ProductionRunComposerV1();
            var request = new Stage1RunCompositionRequestV1(
                fixture.Route,
                fixture.ModeId,
                LevelRunCoordinatorV1.Level1StableId,
                new FixedRunIdFactory(
                    StableId.Parse("run.stage1-compose-missing")),
                new HoldingsLevelRunLoadoutResolverV1(
                    fixture.Holdings,
                    fixture.Catalog),
                fixture.Rewards,
                null,
                new MissionRunAuthorityCheckpointV1(
                    fixture.Holdings.Sequence,
                    fixture.Holdings.ExportSnapshot().Fingerprint,
                    fixture.ExistingPort.OpeningSequence,
                    fixture.ExistingPort.OpeningFingerprint));

            Stage1RunCompositionResultV1 result = composer.Compose(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.RejectionCode,
                Is.EqualTo("stage1-mission-results-missing"));
            Assert.That(result.Session, Is.Null);
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(4L));
            Assert.That(fixture.ExistingPort.ProjectCalls, Is.Zero);
        }
    }
}

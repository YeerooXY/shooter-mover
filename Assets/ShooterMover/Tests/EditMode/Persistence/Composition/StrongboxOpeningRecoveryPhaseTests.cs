using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed partial class StrongboxPersistenceCoordinatorV1Tests
    {
        [TestCase(StrongboxOpeningStageV1.Prepared)]
        [TestCase(StrongboxOpeningStageV1.RewardCommitted)]
        [TestCase(StrongboxOpeningStageV1.RewardClaimedPending)]
        [TestCase(StrongboxOpeningStageV1.RewardApplied)]
        public void ReconstructedPersistedOpeningPhaseResumesSameFrozenOutcome(
            StrongboxOpeningStageV1 phase)
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "phase-recovery-owner");

            var template = (ProductionCharacterRuntimeGraphV1)
                factory.CreateRestoreTarget(character);
            BoxFixture templateBox = AddBox(
                template,
                "phase-recovery",
                1200UL);
            StrongboxOpenCommandV1 command = OpenCommand(
                template,
                templateBox,
                "phase-recovery");
            StrongboxOpeningResultRuntimeV1 templateOpened =
                template.StrongboxAuthority.Open(command);
            Assert.That(templateOpened.Status,
                Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened),
                templateOpened.RejectionCode);
            StrongboxOpeningRecordSnapshotV1 terminalRecord =
                template.StrongboxAuthority.ExportSnapshot().Openings.Single();
            var pendingRecord = new StrongboxOpeningRecordSnapshotV1(
                terminalRecord.Command,
                phase,
                terminalRecord.GeneratedOutcome,
                terminalRecord.CommitCommand,
                terminalRecord.ClaimCommand,
                terminalRecord.ConsumeCommand,
                null,
                null);
            StrongboxOpeningSnapshotV1 pendingSnapshot =
                StrongboxOpeningSnapshotV1.CreateCanonical(
                    template.StrongboxCatalog.Fingerprint,
                    0L,
                    new[] { templateBox.Context },
                    new[] { pendingRecord });
            string expectedOutcome =
                terminalRecord.GeneratedOutcome.Fingerprint;
            template.Dispose();

            PlayerAccountSnapshotV1 durable = Account(character);
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                snapshot =>
                {
                    durable = snapshot;
                    return Saved(snapshot);
                });
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var graph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            BoxFixture held = AddBox(graph, "phase-recovery", 1200UL);
            Assert.That(
                graph.StrongboxAuthority.ImportSnapshot(pendingSnapshot)
                    .Succeeded,
                Is.True);
            if (phase != StrongboxOpeningStageV1.RewardCommitted)
            {
                StrongboxOpeningRecoveryResultV1 seeded =
                    graph.StrongboxRecovery.Recover(command);
                Assert.That(seeded.Succeeded, Is.True,
                    seeded.RejectionCode);
            }
            Assert.That(
                composition.PersistActive(
                    Id("operation.persist-phase-" + phase))
                    .Succeeded,
                Is.True);

            composition.Dispose();
            var restarted = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                Saved);
            Assert.That(restarted.Select(0).Succeeded, Is.True);
            var restored = (ProductionCharacterRuntimeGraphV1)
                restarted.ActiveRuntime;
            var opener = new StrongboxDurableOpeningCoordinatorV1(restarted);

            StrongboxOpeningResultRuntimeV1 resumed =
                opener.OpenAndPersist(
                    held.Result,
                    restored.StrongboxAuthority,
                    command);

            Assert.That(resumed.Status,
                Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened),
                resumed.RejectionCode);
            Assert.That(resumed.GeneratedOutcome.Fingerprint,
                Is.EqualTo(expectedOutcome));
            Assert.That(
                restored.StrongboxAuthority.ExportSnapshot().Openings
                    .Single().Stage,
                Is.EqualTo(StrongboxOpeningStageV1.Opened));
            Assert.That(
                restored.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId == held.Context.InstanceStableId),
                Is.False);
        }
    }
}

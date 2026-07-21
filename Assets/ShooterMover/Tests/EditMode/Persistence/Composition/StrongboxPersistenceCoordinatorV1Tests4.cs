using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed partial class StrongboxPersistenceCoordinatorV1Tests
    {
        [Test]
        public void DurableOpeningPersistsTerminalReplayWithoutSecondAward()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "durable-opening-owner");
            PlayerAccountSnapshotV1 durable = Account(character);
            var composition = Composition(factory, durable, snapshot => durable = snapshot);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var graph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            BoxFixture box = AddBox(graph, "durable-opening", 999UL);
            Assert.That(
                composition.PersistActive(Id("operation.persist-unopened-box"))
                    .Succeeded,
                Is.True);
            StrongboxOpenCommandV1 command = OpenCommand(
                graph,
                box,
                "durable-opening");
            var durableOpening = new StrongboxDurableOpeningCoordinatorV1(
                composition);

            StrongboxOpeningResultRuntimeV1 opened =
                durableOpening.OpenAndPersist(
                    box.Result,
                    graph.StrongboxAuthority,
                    command);
            Assert.That(opened.Status,
                Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened),
                opened.RejectionCode);
            int countAfterOpen = graph.LoadoutRuntime.Holdings
                .ExportSnapshot().UniqueHoldings.Count;
            string outcome = opened.GeneratedOutcome.Fingerprint;

            composition.Dispose();
            var restarted = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                Saved);
            Assert.That(restarted.Select(0).Succeeded, Is.True);
            var restored = (ProductionCharacterRuntimeGraphV1)
                restarted.ActiveRuntime;
            var replayCoordinator = new StrongboxDurableOpeningCoordinatorV1(
                restarted);
            StrongboxOpeningResultRuntimeV1 replay =
                replayCoordinator.OpenAndPersist(
                    box.Result,
                    restored.StrongboxAuthority,
                    command);

            Assert.That(replay.Status, Is.EqualTo(
                StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange));
            Assert.That(replay.GeneratedOutcome.Fingerprint,
                Is.EqualTo(outcome));
            Assert.That(
                restored.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count,
                Is.EqualTo(countAfterOpen));
        }
    }
}

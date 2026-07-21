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
        public void OpeningSaveFailureRestoresWholeGraphAndLaterRetryOpens()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "opening-save-failure-owner");
            PlayerAccountSnapshotV1 durable = Account(character);
            bool failSave = false;
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                snapshot =>
                {
                    if (failSave)
                    {
                        return new PlayerAccountStoreResultV1(
                            PlayerAccountStoreStatusV1.IoFailure,
                            "simulated-opening-save-failure",
                            null);
                    }
                    durable = snapshot;
                    return Saved(snapshot);
                });
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var graph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            BoxFixture box = AddBox(graph, "opening-save-failure", 123UL);
            Assert.That(
                composition.PersistActive(Id("operation.persist-opening-input"))
                    .Succeeded,
                Is.True);
            long moneyBefore = graph.MoneyWallet.Balance;
            long scrapBefore = graph.ScrapWallet.Balance;
            string holdingsBefore = graph.LoadoutRuntime.Holdings
                .ExportSnapshot().Fingerprint;
            string boxesBefore = graph.StrongboxAuthority
                .ExportSnapshot().Fingerprint;
            StrongboxOpenCommandV1 command = OpenCommand(
                graph,
                box,
                "opening-save-failure");
            var coordinator = new StrongboxDurableOpeningCoordinatorV1(
                composition);

            failSave = true;
            StrongboxOpeningResultRuntimeV1 failed =
                coordinator.OpenAndPersist(
                    box.Result,
                    graph.StrongboxAuthority,
                    command);
            Assert.That(failed.Status, Is.EqualTo(
                StrongboxOpeningRuntimeStatusV1.SnapshotRejected));
            Assert.That(graph.MoneyWallet.Balance, Is.EqualTo(moneyBefore));
            Assert.That(graph.ScrapWallet.Balance, Is.EqualTo(scrapBefore));
            Assert.That(
                graph.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint,
                Is.EqualTo(holdingsBefore));
            Assert.That(
                graph.StrongboxAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(boxesBefore));

            failSave = false;
            StrongboxOpeningResultRuntimeV1 opened =
                coordinator.OpenAndPersist(
                    box.Result,
                    graph.StrongboxAuthority,
                    command);
            Assert.That(opened.Status,
                Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened),
                opened.RejectionCode);
        }
    }
}

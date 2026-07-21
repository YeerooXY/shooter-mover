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
        public void SaveFailureCompensatesHoldingsAndBoxThenRetrySucceeds()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "transfer-save-failure-owner");
            PlayerAccountSnapshotV1 durable = Account(character);
            bool failSave = true;
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                snapshot =>
                {
                    if (failSave)
                    {
                        return new PlayerAccountStoreResultV1(
                            PlayerAccountStoreStatusV1.IoFailure,
                            "simulated-box-transfer-save-failure",
                            null);
                    }
                    durable = snapshot;
                    return Saved(snapshot);
                });
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var target = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            var source = (ProductionCharacterRuntimeGraphV1)
                factory.CreateStarter(
                    0,
                    character.CharacterInstanceStableId,
                    character.ClassDefinitionStableId,
                    character.DisplayName,
                    target.RoutePayload);
            BoxFixture box = AddBox(source, "transfer-save-failure", 77UL);
            MissionResultPayloadV1 result = TerminalResult(
                source,
                target.RoutePayload,
                Id("run.transfer-save-failure"),
                box.Result);
            var coordinator =
                new StrongboxMissionResultApplicationCoordinatorV1(
                    composition);

            StrongboxMissionResultApplicationResultV1 failed =
                coordinator.Apply(TransferCommand(
                    target,
                    composition.Account,
                    result,
                    source,
                    "transfer-save-failure-first"));
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId == box.Context.InstanceStableId),
                Is.False);
            Assert.That(
                target.StrongboxAuthority.ExportSnapshot().Contexts.Any(item =>
                    item.InstanceStableId == box.Context.InstanceStableId),
                Is.False);

            failSave = false;
            StrongboxMissionResultApplicationResultV1 retried =
                coordinator.Apply(TransferCommand(
                    target,
                    composition.Account,
                    result,
                    source,
                    "transfer-save-failure-retry"));
            Assert.That(retried.Succeeded, Is.True, retried.RejectionCode);
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId == box.Context.InstanceStableId),
                Is.True);
        }
    }
}

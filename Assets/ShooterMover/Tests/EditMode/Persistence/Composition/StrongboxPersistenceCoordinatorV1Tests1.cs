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
        public void TerminalTransferPersistsExactBoxAndExactReplayAddsNothing()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "box-transfer-owner");
            PlayerAccountSnapshotV1 durable = Account(character);
            var composition = Composition(factory, durable, snapshot => durable = snapshot);
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
            BoxFixture box = AddBox(source, "terminal-transfer", 424242UL);
            MissionResultPayloadV1 result = TerminalResult(
                source,
                target.RoutePayload,
                Id("run.terminal-transfer"),
                box.Result);
            var coordinator =
                new StrongboxMissionResultApplicationCoordinatorV1(
                    composition,
                    () => 1L);
            StrongboxMissionResultApplicationCommandV1 command =
                TransferCommand(
                    target,
                    composition.Account,
                    result,
                    source,
                    "terminal-transfer");

            StrongboxMissionResultApplicationResultV1 applied =
                coordinator.Apply(command);
            StrongboxMissionResultApplicationResultV1 replay =
                coordinator.Apply(command);

            Assert.That(applied.Status, Is.EqualTo(
                StrongboxMissionResultApplicationStatusV1.Applied),
                applied.RejectionCode);
            Assert.That(replay.Status, Is.EqualTo(
                StrongboxMissionResultApplicationStatusV1.ExactReplay));
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count(item =>
                        item.InstanceStableId == box.Context.InstanceStableId),
                Is.EqualTo(1));
            Assert.That(
                target.StrongboxAuthority.ExportSnapshot().Contexts.Count(item =>
                    item.InstanceStableId == box.Context.InstanceStableId),
                Is.EqualTo(1));

            composition.Dispose();
            var restarted = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                Saved);
            Assert.That(restarted.Select(0).Succeeded, Is.True);
            var restored = (ProductionCharacterRuntimeGraphV1)
                restarted.ActiveRuntime;
            Assert.That(
                restored.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId == box.Context.InstanceStableId),
                Is.True);
            Assert.That(
                restored.StrongboxAuthority.ExportSnapshot().Contexts.Any(item =>
                    item.InstanceStableId == box.Context.InstanceStableId),
                Is.True);
        }
    }
}

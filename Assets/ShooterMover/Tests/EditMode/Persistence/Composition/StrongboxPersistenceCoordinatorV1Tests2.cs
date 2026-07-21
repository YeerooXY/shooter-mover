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
        public void OneInvalidBoxRejectsCompleteBatchWithoutPartialMutation()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "atomic-batch-owner");
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
            BoxFixture valid = AddBox(source, "atomic-valid", 11UL);
            MissionRunStrongboxCollectionV1 missingCollection =
                new MissionRunStrongboxCollectionV1(
                    valid.Result.DefinitionStableId,
                    Id("strongbox-instance.atomic-missing"),
                    Id("grant.atomic-missing"),
                    Id("source.atomic-missing"),
                    Id("operation.atomic-missing"),
                    source.LoadoutRuntime.Holdings.Sequence,
                    source.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint);
            var missing = new MissionRunStrongboxResultV1(
                missingCollection,
                MissionRunStrongboxStateV1.Unopened,
                null,
                null);
            MissionResultPayloadV1 result = TerminalResult(
                source,
                target.RoutePayload,
                Id("run.atomic-batch"),
                valid.Result,
                missing);
            var coordinator =
                new StrongboxMissionResultApplicationCoordinatorV1(
                    composition,
                    () => 1L);

            StrongboxMissionResultApplicationResultV1 rejected =
                coordinator.Apply(TransferCommand(
                    target,
                    composition.Account,
                    result,
                    source,
                    "atomic-batch"));

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.RejectionCode,
                Does.Contain("box-transfer-source-fact-missing"));
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId == valid.Context.InstanceStableId),
                Is.False);
            Assert.That(
                target.StrongboxAuthority.ExportSnapshot().Contexts.Any(item =>
                        item.InstanceStableId == valid.Context.InstanceStableId),
                Is.False);
        }
    }
}

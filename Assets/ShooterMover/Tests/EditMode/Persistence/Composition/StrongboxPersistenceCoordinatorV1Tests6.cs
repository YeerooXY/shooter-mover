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
        public void TwoSameTierBoxesProduceDistinctEquipmentInstancesAndStayIsolated()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 alpha = StarterCharacter(
                factory,
                0,
                "duplicate-definition-alpha");
            CharacterInstanceSnapshotV1 bravo = StarterCharacter(
                factory,
                1,
                "duplicate-definition-bravo");
            PlayerAccountSnapshotV1 durable = Account(alpha, bravo);
            var composition = Composition(factory, durable, snapshot => durable = snapshot);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var graph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            BoxFixture first = AddBox(graph, "duplicate-definition-1", 555UL);
            BoxFixture second = AddBox(graph, "duplicate-definition-2", 555UL);
            Assert.That(
                composition.PersistActive(Id("operation.persist-two-boxes"))
                    .Succeeded,
                Is.True);
            var opener = new StrongboxDurableOpeningCoordinatorV1(composition);

            StrongboxOpeningResultRuntimeV1 firstOpen = opener.OpenAndPersist(
                first.Result,
                graph.StrongboxAuthority,
                OpenCommand(graph, first, "duplicate-definition-1"));
            StrongboxOpeningResultRuntimeV1 secondOpen = opener.OpenAndPersist(
                second.Result,
                graph.StrongboxAuthority,
                OpenCommand(graph, second, "duplicate-definition-2"));
            var firstEquipment = firstOpen.GeneratedOutcome.Payloads
                .SelectMany(item => item.EquipmentInstances).ToList();
            var secondEquipment = secondOpen.GeneratedOutcome.Payloads
                .SelectMany(item => item.EquipmentInstances).ToList();

            Assert.That(firstEquipment.Count, Is.GreaterThan(0));
            Assert.That(secondEquipment.Count, Is.GreaterThan(0));
            Assert.That(firstEquipment[0].DefinitionId,
                Is.EqualTo(secondEquipment[0].DefinitionId));
            Assert.That(firstEquipment[0].InstanceId,
                Is.Not.EqualTo(secondEquipment[0].InstanceId));
            Assert.That(
                graph.LoadoutRuntime.Holdings.ExportSnapshot().UniqueHoldings
                    .Count(item => item.RewardKind
                        == RewardGrantKindV1.EquipmentReference),
                Is.GreaterThanOrEqualTo(2));

            Assert.That(composition.Select(1).Succeeded, Is.True);
            var bravoGraph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            Assert.That(
                bravoGraph.StrongboxAuthority.ExportSnapshot().Contexts.Count,
                Is.EqualTo(0));
            Assert.That(
                bravoGraph.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId == firstEquipment[0].InstanceId
                        || item.InstanceStableId == secondEquipment[0].InstanceId),
                Is.False);
        }
    }
}

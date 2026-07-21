using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed partial class StrongboxPersistenceCoordinatorV1Tests
    {
        [TestCase(ThrowPointV1.HoldingsAfterMutation)]
        [TestCase(ThrowPointV1.RegistrationAfterMutation)]
        [TestCase(ThrowPointV1.PostMutationSnapshotExport)]
        public void MutationExceptionCompensatesExactAuthoritySnapshots(
            ThrowPointV1 throwPoint)
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "exception-" + throwPoint);
            PlayerAccountSnapshotV1 durable = Account(character);
            var composition = Composition(
                factory,
                durable,
                snapshot => durable = snapshot);
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
            BoxFixture box = AddBox(
                source,
                "exception-source-" + throwPoint,
                900UL + (ulong)throwPoint);
            MissionResultPayloadV1 terminal = TerminalResult(
                source,
                target.RoutePayload,
                Id("run.exception-" + throwPoint),
                box.Result);
            string beforeHoldings = target.LoadoutRuntime.Holdings
                .ExportSnapshot().Fingerprint;
            string beforeBoxes = target.StrongboxAuthority
                .ExportSnapshot().Fingerprint;
            var port = new ThrowingApplicationPortV1(target, throwPoint);
            var coordinator =
                new StrongboxMissionResultApplicationCoordinatorV1(
                    composition,
                    () => 1L,
                    graph => port);
            StrongboxMissionResultApplicationCommandV1 command =
                TransferCommand(
                    target,
                    composition.Account,
                    terminal,
                    source,
                    "exception-" + throwPoint);

            StrongboxMissionResultApplicationResultV1 rejected =
                coordinator.Apply(command);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.ExactRetryAllowed, Is.True,
                rejected.RejectionCode);
            Assert.That(rejected.RejectionCode,
                Does.Contain("box-transfer-transaction-exception"));
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint,
                Is.EqualTo(beforeHoldings));
            Assert.That(
                target.StrongboxAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(beforeBoxes));
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId == box.Context.InstanceStableId),
                Is.False);
        }

        [Test]
        public void ThrowingSaveDelegateCompensatesAndExactRetrySucceeds()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "throwing-save-owner");
            PlayerAccountSnapshotV1 durable = Account(character);
            bool throwSave = true;
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                snapshot =>
                {
                    if (throwSave)
                    {
                        throw new InvalidOperationException(
                            "simulated-disk-exception");
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
            BoxFixture box = AddBox(source, "throwing-save", 901UL);
            MissionResultPayloadV1 terminal = TerminalResult(
                source,
                target.RoutePayload,
                Id("run.throwing-save"),
                box.Result);
            var coordinator =
                new StrongboxMissionResultApplicationCoordinatorV1(
                    composition,
                    () => 1L);
            StrongboxMissionResultApplicationCommandV1 command =
                TransferCommand(
                    target,
                    composition.Account,
                    terminal,
                    source,
                    "throwing-save");

            StrongboxMissionResultApplicationResultV1 failed =
                coordinator.Apply(command);
            Assert.That(failed.ExactRetryAllowed, Is.True,
                failed.RejectionCode);
            Assert.That(failed.RejectionCode,
                Does.Contain("box-transfer-durable-save-rejected")
                    .Or.Contain("box-transfer-transaction-exception"));
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId == box.Context.InstanceStableId),
                Is.False);

            throwSave = false;
            StrongboxMissionResultApplicationResultV1 retried =
                coordinator.Apply(command);
            Assert.That(retried.Succeeded, Is.True,
                retried.RejectionCode);
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count(item =>
                        item.InstanceStableId == box.Context.InstanceStableId),
                Is.EqualTo(1));
        }

        [Test]
        public void CompensationFailureReportsOriginalAndEveryRollbackFailure()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "compensation-failure-owner");
            PlayerAccountSnapshotV1 durable = Account(character);
            var composition = Composition(
                factory,
                durable,
                snapshot => durable = snapshot);
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
            BoxFixture box = AddBox(source, "compensation-failure", 902UL);
            MissionResultPayloadV1 terminal = TerminalResult(
                source,
                target.RoutePayload,
                Id("run.compensation-failure"),
                box.Result);
            var port = new ThrowingApplicationPortV1(
                target,
                ThrowPointV1.HoldingsAfterMutation,
                failCompensation: true);
            var coordinator =
                new StrongboxMissionResultApplicationCoordinatorV1(
                    composition,
                    () => 1L,
                    graph => port);

            StrongboxMissionResultApplicationResultV1 rejected =
                coordinator.Apply(TransferCommand(
                    target,
                    composition.Account,
                    terminal,
                    source,
                    "compensation-failure"));

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.ExactRetryAllowed, Is.False);
            Assert.That(rejected.RejectionCode,
                Does.Contain("box-transfer-transaction-exception"));
            Assert.That(rejected.RejectionCode,
                Does.Contain("compensation="));
            Assert.That(rejected.RejectionCode,
                Does.Contain("box-exception"));
            Assert.That(rejected.RejectionCode,
                Does.Contain("holdings-exception"));
        }

        [Test]
        public void RunLifecycleMismatchRejectsBeforeAnyMutation()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "run-generation-owner");
            PlayerAccountSnapshotV1 durable = Account(character);
            var composition = Composition(
                factory,
                durable,
                snapshot => durable = snapshot);
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
            BoxFixture box = AddBox(source, "run-generation", 903UL);
            MissionResultPayloadV1 terminal = TerminalResult(
                source,
                target.RoutePayload,
                Id("run.run-generation"),
                box.Result);
            var coordinator =
                new StrongboxMissionResultApplicationCoordinatorV1(
                    composition,
                    () => 2L);

            StrongboxMissionResultApplicationResultV1 rejected =
                coordinator.Apply(TransferCommand(
                    target,
                    composition.Account,
                    terminal,
                    source,
                    "run-generation"));

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.ExactRetryAllowed, Is.False);
            Assert.That(rejected.RejectionCode,
                Is.EqualTo("box-transfer-run-generation-stale"));
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId == box.Context.InstanceStableId),
                Is.False);
        }

        public enum ThrowPointV1
        {
            HoldingsAfterMutation = 1,
            RegistrationAfterMutation = 2,
            PostMutationSnapshotExport = 3,
        }

        private sealed class ThrowingApplicationPortV1 :
            IStrongboxMissionResultApplicationAuthorityPortV1
        {
            private readonly ExistingStrongboxMissionResultApplicationAuthorityPortV1
                inner;
            private readonly ThrowPointV1 throwPoint;
            private readonly bool failCompensation;
            private bool mutationObserved;

            public ThrowingApplicationPortV1(
                ProductionCharacterRuntimeGraphV1 graph,
                ThrowPointV1 throwPoint,
                bool failCompensation = false)
            {
                inner = new ExistingStrongboxMissionResultApplicationAuthorityPortV1(
                    graph);
                this.throwPoint = throwPoint;
                this.failCompensation = failCompensation;
            }

            public ShooterMover.Domain.Common.StableId HoldingsAuthorityStableId
            {
                get { return inner.HoldingsAuthorityStableId; }
            }

            public long HoldingsSequence { get { return inner.HoldingsSequence; } }

            public PlayerHoldingsSnapshotV1 ExportHoldings()
            {
                if (throwPoint == ThrowPointV1.PostMutationSnapshotExport
                    && mutationObserved)
                {
                    throw new InvalidOperationException(
                        "post-mutation-holdings-export");
                }
                return inner.ExportHoldings();
            }

            public StrongboxOpeningSnapshotV1 ExportStrongboxes()
            {
                return inner.ExportStrongboxes();
            }

            public PlayerHoldingsMutationResultV1 AddStrongbox(
                PlayerHoldingsCommandV1 command)
            {
                PlayerHoldingsMutationResultV1 result =
                    inner.AddStrongbox(command);
                mutationObserved = true;
                if (throwPoint == ThrowPointV1.HoldingsAfterMutation)
                {
                    throw new InvalidOperationException(
                        "holdings-after-mutation");
                }
                return result;
            }

            public StrongboxRegistrationResultV1 RegisterStrongbox(
                StrongboxInstanceContextV1 context)
            {
                StrongboxRegistrationResultV1 result =
                    inner.RegisterStrongbox(context);
                mutationObserved = true;
                if (throwPoint == ThrowPointV1.RegistrationAfterMutation)
                {
                    throw new InvalidOperationException(
                        "registration-after-mutation");
                }
                return result;
            }

            public PlayerHoldingsImportResultV1 ImportHoldings(
                PlayerHoldingsSnapshotV1 snapshot)
            {
                if (failCompensation)
                {
                    throw new InvalidOperationException(
                        "holdings-compensation-failure");
                }
                PlayerHoldingsImportResultV1 result =
                    inner.ImportHoldings(snapshot);
                mutationObserved = false;
                return result;
            }

            public StrongboxOpeningImportResultV1 ImportStrongboxes(
                StrongboxOpeningSnapshotV1 snapshot)
            {
                if (failCompensation)
                {
                    throw new InvalidOperationException(
                        "box-compensation-failure");
                }
                return inner.ImportStrongboxes(snapshot);
            }
        }
    }
}

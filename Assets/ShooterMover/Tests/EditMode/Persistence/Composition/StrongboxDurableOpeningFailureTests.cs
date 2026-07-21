using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed partial class StrongboxPersistenceCoordinatorV1Tests
    {
        [Test]
        public void DurableOpeningPreflightExportExceptionReturnsSnapshotRejected()
        {
            bool throwExport = false;
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults(
                        graph => new ISaveComponentAdapterV1[]
                        {
                            new ToggleExportAdapterV1(
                                () => throwExport),
                        });
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "durable-opening-preflight-exception");
            PlayerAccountSnapshotV1 durable = Account(character);
            var composition = Composition(
                factory,
                durable,
                snapshot => durable = snapshot);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var graph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            BoxFixture box = AddBox(graph, "preflight-export", 1501UL);
            Assert.That(
                composition.PersistActive(Id("operation.persist-preflight"))
                    .Succeeded,
                Is.True);
            string holdingsBefore = graph.LoadoutRuntime.Holdings
                .ExportSnapshot().Fingerprint;
            string boxesBefore = graph.StrongboxAuthority.ExportSnapshot()
                .Fingerprint;

            throwExport = true;
            StrongboxOpeningResultRuntimeV1 result =
                new StrongboxDurableOpeningCoordinatorV1(composition)
                    .OpenAndPersist(
                        box.Result,
                        graph.StrongboxAuthority,
                        OpenCommand(graph, box, "preflight-export"));
            throwExport = false;

            Assert.That(result.Status, Is.EqualTo(
                StrongboxOpeningRuntimeStatusV1.SnapshotRejected));
            Assert.That(result.RejectionCode, Does.Contain(
                "durable-opening-transaction-exception-invalidoperationexception"));
            Assert.That(graph.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint,
                Is.EqualTo(holdingsBefore));
            Assert.That(graph.StrongboxAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(boxesBefore));
        }

        [Test]
        public void DurableOpeningRollbackExportFailureDoesNotMaskSaveRejection()
        {
            bool throwExport = false;
            bool failSave = false;
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults(
                        graph => new ISaveComponentAdapterV1[]
                        {
                            new ToggleExportAdapterV1(
                                () => throwExport),
                        });
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "durable-opening-rollback-exception");
            PlayerAccountSnapshotV1 durable = Account(character);
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                snapshot =>
                {
                    if (failSave)
                    {
                        throwExport = true;
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
            BoxFixture box = AddBox(graph, "rollback-export", 1502UL);
            Assert.That(
                composition.PersistActive(Id("operation.persist-rollback"))
                    .Succeeded,
                Is.True);

            failSave = true;
            StrongboxOpeningResultRuntimeV1 result =
                new StrongboxDurableOpeningCoordinatorV1(composition)
                    .OpenAndPersist(
                        box.Result,
                        graph.StrongboxAuthority,
                        OpenCommand(graph, box, "rollback-export"));
            throwExport = false;

            Assert.That(result.Status, Is.EqualTo(
                StrongboxOpeningRuntimeStatusV1.SnapshotRejected));
            Assert.That(result.RejectionCode,
                Does.Contain("durable-opening-save-rejected"));
            Assert.That(result.RejectionCode,
                Does.Contain("restore=restore-exception-invalidoperationexception"));
        }

        private sealed class ToggleExportAdapterV1 : ISaveComponentAdapterV1
        {
            private static readonly SaveComponentDefinitionV1 DefinitionValue =
                new SaveComponentDefinitionV1(
                    StableId.Parse("component.tests.durable-opening-toggle"),
                    1,
                    "tests-v1",
                    false,
                    999);
            private readonly Func<bool> shouldThrow;

            public ToggleExportAdapterV1(Func<bool> shouldThrow)
            {
                this.shouldThrow = shouldThrow
                    ?? throw new ArgumentNullException(nameof(shouldThrow));
            }

            public SaveComponentDefinitionV1 Definition
            {
                get { return DefinitionValue; }
            }

            public SaveComponentSnapshotV1 ExportComponent()
            {
                if (shouldThrow())
                {
                    throw new InvalidOperationException("toggle-export-failure");
                }

                return new SaveComponentSnapshotV1(
                    Definition.ComponentStableId,
                    Definition.SchemaVersion,
                    Definition.ContentVersion,
                    "toggle-export-payload");
            }

            public SaveComponentPrepareResultV1 PrepareRestore(
                SaveComponentSnapshotV1 component)
            {
                if (component == null
                    || component.ComponentStableId
                        != Definition.ComponentStableId)
                {
                    return SaveComponentPrepareResultV1.Rejected(
                        "toggle-export-component-mismatch");
                }

                return SaveComponentPrepareResultV1.Prepared(
                    new NoOpPreparedRestoreV1(Definition.ComponentStableId));
            }
        }

        private sealed class NoOpPreparedRestoreV1 :
            IPreparedSaveComponentRestoreV1
        {
            public NoOpPreparedRestoreV1(StableId componentStableId)
            {
                ComponentStableId = componentStableId;
            }

            public StableId ComponentStableId { get; }

            public bool CommitAttempted { get; private set; }

            public bool CommitSucceeded { get; private set; }

            public SaveComponentCommitResultV1 Commit()
            {
                CommitAttempted = true;
                CommitSucceeded = true;
                return new SaveComponentCommitResultV1(
                    SaveComponentCommitStatusV1.Applied,
                    string.Empty,
                    true);
            }

            public SaveComponentRollbackResultV1 Rollback()
            {
                return new SaveComponentRollbackResultV1(true, string.Empty);
            }

            public void Dispose()
            {
            }
        }
    }
}

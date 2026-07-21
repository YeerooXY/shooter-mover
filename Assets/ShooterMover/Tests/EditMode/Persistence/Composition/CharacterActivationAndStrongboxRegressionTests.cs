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
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CharacterActivationAndStrongboxRegressionTests
    {
        [Test]
        public void DirectSelectPersistsUnsavedCharacterBeforeSwitchAndRestart()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshotV1 alpha = StarterCharacter(
                factory,
                0,
                "alpha");
            CharacterInstanceSnapshotV1 bravo = StarterCharacter(
                factory,
                1,
                "bravo");
            PlayerAccountSnapshotV1 durable = Account(alpha, bravo);
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                snapshot =>
                {
                    durable = snapshot;
                    return Saved(snapshot);
                });
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var alphaGraph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            alphaGraph.MoneyWallet.Grant(
                Id("transaction.alpha-unsaved-money"),
                Id("operation.alpha-unsaved-money"),
                73L);
            Assert.That(alphaGraph.MoneyWallet.Balance, Is.EqualTo(73L));

            CharacterCompositionResultV1 switched = composition.Select(1);

            Assert.That(switched.Succeeded, Is.True, switched.Diagnostic);
            Assert.That(alphaGraph.IsDisposed, Is.True);
            Assert.That(composition.ActiveSlotIndex, Is.EqualTo(1));

            composition.Dispose();
            var restarted = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                Saved);
            CharacterCompositionResultV1 restored = restarted.Select(0);

            Assert.That(restored.Succeeded, Is.True, restored.Diagnostic);
            Assert.That(
                ((ProductionCharacterRuntimeGraphV1)restarted.ActiveRuntime)
                    .MoneyWallet.Balance,
                Is.EqualTo(73L));
        }

        [Test]
        public void FailedPreSwitchSaveRejectsSelectAndKeepsCurrentGraphPublished()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshotV1 alpha = StarterCharacter(
                factory,
                0,
                "save-failure-alpha");
            CharacterInstanceSnapshotV1 bravo = StarterCharacter(
                factory,
                1,
                "save-failure-bravo");
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(Account(alpha, bravo)),
                factory,
                snapshot => new PlayerAccountStoreResultV1(
                    PlayerAccountStoreStatusV1.IoFailure,
                    "simulated-switch-save-failure",
                    null));
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var alphaGraph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            alphaGraph.MoneyWallet.Grant(
                Id("transaction.failed-switch-money"),
                Id("operation.failed-switch-money"),
                11L);

            CharacterCompositionResultV1 rejected = composition.Select(1);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(
                rejected.Diagnostic,
                Does.Contain("character-switch-save-rejected"));
            Assert.That(composition.ActiveSlotIndex, Is.EqualTo(0));
            Assert.That(composition.ActiveRuntime, Is.SameAs(alphaGraph));
            Assert.That(alphaGraph.IsDisposed, Is.False);
            Assert.That(alphaGraph.MoneyWallet.Balance, Is.EqualTo(11L));
        }

        [Test]
        public void ProductionStrongboxOpenPersistsRestoresAndReplaysWithoutSecondAward()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "strongbox-owner");
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
            StrongboxDefinitionV1 definition =
                graph.StrongboxCatalog.Definitions[0];
            StableId boxId = Id(
                "strongbox-instance.character-owned-regression");
            StableId grantId = Id("grant.character-owned-strongbox");
            StableId sourceId = Id("source.character-owned-strongbox");
            PlayerHoldingsMutationResultV1 added =
                graph.LoadoutRuntime.Holdings.Apply(
                    PlayerHoldingsCommandV1.AddStrongbox(
                        Id("transaction.add-character-strongbox"),
                        Id("operation.add-character-strongbox"),
                        graph.LoadoutRuntime.Holdings.AuthorityStableId,
                        definition.TierStableId,
                        boxId,
                        HoldingProvenanceV1.Create(grantId, sourceId)));
            Assert.That(
                added.Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));

            StrongboxInstanceContextV1 context =
                StrongboxInstanceContextV1.Create(
                    boxId,
                    definition.TierStableId,
                    424242UL,
                    1,
                    ProgressionContext.Create(
                        1,
                        1,
                        Id("difficulty.normal"),
                        0,
                        new[] { Id("progression-tag.campaign") }),
                    sourceId,
                    grantId,
                    definition.Fingerprint);
            StrongboxRegistrationResultV1 registered =
                graph.StrongboxAuthority.RegisterInstance(context);
            Assert.That(
                registered.Status,
                Is.EqualTo(StrongboxRegistrationStatusV1.Registered));

            StrongboxOpenCommandV1 command = StrongboxOpenCommandV1.Create(
                Id("opening.character-owned-regression"),
                Id("run.character-owned-regression"),
                boxId,
                graph.Character.CharacterInstanceStableId,
                MoneyWalletIdsV1.AuthorityStableId,
                graph.ScrapWallet.AuthorityStableId,
                graph.LoadoutRuntime.Holdings.AuthorityStableId);
            StrongboxOpeningResultRuntimeV1 opened =
                graph.StrongboxAuthority.Open(command);
            Assert.That(
                opened.Status,
                Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened),
                opened.RejectionCode);
            Assert.That(
                graph.LoadoutRuntime.Holdings.ExportSnapshot().UniqueHoldings
                    .Any(item => item.InstanceStableId == boxId),
                Is.False);
            int uniqueCountAfterOpen = graph.LoadoutRuntime.Holdings
                .ExportSnapshot().UniqueHoldings.Count;
            string openingFingerprint = graph.StrongboxAuthority
                .ExportSnapshot().Fingerprint;
            CharacterCompositionResultV1 persisted =
                composition.PersistActive(
                    Id("operation.persist-character-strongbox-opening"));
            Assert.That(persisted.Succeeded, Is.True, persisted.Diagnostic);

            composition.Dispose();
            var restarted = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                Saved);
            CharacterCompositionResultV1 selected = restarted.Select(0);
            Assert.That(selected.Succeeded, Is.True, selected.Diagnostic);
            var restoredGraph = (ProductionCharacterRuntimeGraphV1)
                restarted.ActiveRuntime;
            Assert.That(
                restoredGraph.StrongboxAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(openingFingerprint));
            Assert.That(
                restoredGraph.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count,
                Is.EqualTo(uniqueCountAfterOpen));

            StrongboxOpeningResultRuntimeV1 replay =
                restoredGraph.StrongboxAuthority.Open(command);
            Assert.That(
                replay.Status,
                Is.EqualTo(
                    StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange));
            Assert.That(
                restoredGraph.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count,
                Is.EqualTo(uniqueCountAfterOpen));
            Assert.That(
                replay.GeneratedOutcome.Fingerprint,
                Is.EqualTo(opened.GeneratedOutcome.Fingerprint));
        }

        private static CharacterInstanceSnapshotV1 StarterCharacter(
            ProductionCharacterRuntimeGraphFactoryV1 factory,
            int slotIndex,
            string suffix)
        {
            StableId characterId = Id("character-instance." + suffix);
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    characterId,
                    classId,
                    new[]
                    {
                        ProductionStarterWeaponCatalogV1
                            .BlasterEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .ShotgunEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .RocketEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .ArcEquipmentInstanceStableId,
                    });
            ICharacterRuntimeGraphV1 graph = factory.CreateStarter(
                slotIndex,
                characterId,
                classId,
                suffix,
                route);
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    graph.SaveAdapters);
            graph.Dispose();
            return new CharacterInstanceSnapshotV1(
                characterId,
                classId,
                slotIndex,
                suffix,
                0L,
                components);
        }

        private static PlayerAccountSnapshotV1 Account(
            params CharacterInstanceSnapshotV1[] characters)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            foreach (CharacterInstanceSnapshotV1 character in characters)
            {
                slots[character.SlotIndex] = character;
            }
            return new PlayerAccountSnapshotV1(
                Id("account.character-activation-strongbox-regression"),
                0L,
                slots,
                null);
        }

        private static PlayerAccountStoreResultV1 Saved(
            PlayerAccountSnapshotV1 snapshot)
        {
            return new PlayerAccountStoreResultV1(
                PlayerAccountStoreStatusV1.Saved,
                string.Empty,
                snapshot);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}

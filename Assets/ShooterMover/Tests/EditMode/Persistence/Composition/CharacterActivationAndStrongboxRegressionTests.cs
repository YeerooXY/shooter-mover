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
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshot alpha = StarterCharacter(
                factory,
                0,
                "alpha");
            CharacterInstanceSnapshot bravo = StarterCharacter(
                factory,
                1,
                "bravo");
            PlayerAccountSnapshot durable = Account(alpha, bravo);
            var composition = new CharacterSetupFlow(
                new PlayerAccountSaveState(durable),
                factory,
                snapshot =>
                {
                    durable = snapshot;
                    return Saved(snapshot);
                });
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var alphaGraph = (CharacterLiveGraph)
                composition.ActiveRuntime;
            alphaGraph.MoneyWallet.Grant(
                Id("transaction.alpha-unsaved-money"),
                Id("operation.alpha-unsaved-money"),
                73L);
            Assert.That(alphaGraph.MoneyWallet.Balance, Is.EqualTo(73L));

            CharacterSetupResult switched = composition.Select(1);

            Assert.That(switched.Succeeded, Is.True, switched.Diagnostic);
            Assert.That(alphaGraph.IsDisposed, Is.True);
            Assert.That(composition.ActiveSlotIndex, Is.EqualTo(1));

            composition.Dispose();
            var restarted = new CharacterSetupFlow(
                new PlayerAccountSaveState(durable),
                factory,
                Saved);
            CharacterSetupResult restored = restarted.Select(0);

            Assert.That(restored.Succeeded, Is.True, restored.Diagnostic);
            Assert.That(
                ((CharacterLiveGraph)restarted.ActiveRuntime)
                    .MoneyWallet.Balance,
                Is.EqualTo(73L));
        }

        [Test]
        public void FailedPreSwitchSaveRejectsSelectAndKeepsCurrentGraphPublished()
        {
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshot alpha = StarterCharacter(
                factory,
                0,
                "save-failure-alpha");
            CharacterInstanceSnapshot bravo = StarterCharacter(
                factory,
                1,
                "save-failure-bravo");
            var composition = new CharacterSetupFlow(
                new PlayerAccountSaveState(Account(alpha, bravo)),
                factory,
                snapshot => new PlayerAccountStoreResult(
                    PlayerAccountStoreStatus.IoFailure,
                    "simulated-switch-save-failure",
                    null));
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var alphaGraph = (CharacterLiveGraph)
                composition.ActiveRuntime;
            alphaGraph.MoneyWallet.Grant(
                Id("transaction.failed-switch-money"),
                Id("operation.failed-switch-money"),
                11L);

            CharacterSetupResult rejected = composition.Select(1);

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
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshot character = StarterCharacter(
                factory,
                0,
                "strongbox-owner");
            PlayerAccountSnapshot durable = Account(character);
            var composition = new CharacterSetupFlow(
                new PlayerAccountSaveState(durable),
                factory,
                snapshot =>
                {
                    durable = snapshot;
                    return Saved(snapshot);
                });
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var graph = (CharacterLiveGraph)
                composition.ActiveRuntime;
            StrongboxDefinition definition =
                graph.StrongboxCatalog.Definitions[0];
            StableId boxId = Id(
                "strongbox-instance.character-owned-regression");
            StableId grantId = Id("grant.character-owned-strongbox");
            StableId sourceId = Id("source.character-owned-strongbox");
            PlayerHoldingsMutationResult added =
                graph.LoadoutRuntime.Holdings.Apply(
                    PlayerHoldingsCommand.AddStrongbox(
                        Id("transaction.add-character-strongbox"),
                        Id("operation.add-character-strongbox"),
                        graph.LoadoutRuntime.Holdings.AuthorityStableId,
                        definition.TierStableId,
                        boxId,
                        HoldingProvenance.Create(grantId, sourceId)));
            Assert.That(
                added.Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));

            StrongboxInstanceContext context =
                StrongboxInstanceContext.Create(
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
            StrongboxRegistrationResult registered =
                graph.StrongboxAuthority.RegisterInstance(context);
            Assert.That(
                registered.Status,
                Is.EqualTo(StrongboxRegistrationStatus.Registered));

            StrongboxOpenCommand command = StrongboxOpenCommand.Create(
                Id("opening.character-owned-regression"),
                Id("run.character-owned-regression"),
                boxId,
                graph.Character.CharacterInstanceStableId,
                MoneyWalletIds.AuthorityStableId,
                graph.ScrapWallet.AuthorityStableId,
                graph.LoadoutRuntime.Holdings.AuthorityStableId);
            StrongboxOpeningResultLive opened =
                graph.StrongboxAuthority.Open(command);
            Assert.That(
                opened.Status,
                Is.EqualTo(StrongboxOpeningLiveStatus.Opened),
                opened.RejectionCode);
            Assert.That(
                graph.LoadoutRuntime.Holdings.ExportSnapshot().UniqueHoldings
                    .Any(item => item.InstanceStableId == boxId),
                Is.False);
            int uniqueCountAfterOpen = graph.LoadoutRuntime.Holdings
                .ExportSnapshot().UniqueHoldings.Count;
            string openingFingerprint = graph.StrongboxAuthority
                .ExportSnapshot().Fingerprint;
            CharacterSetupResult persisted =
                composition.PersistActive(
                    Id("operation.persist-character-strongbox-opening"));
            Assert.That(persisted.Succeeded, Is.True, persisted.Diagnostic);

            composition.Dispose();
            var restarted = new CharacterSetupFlow(
                new PlayerAccountSaveState(durable),
                factory,
                Saved);
            CharacterSetupResult selected = restarted.Select(0);
            Assert.That(selected.Succeeded, Is.True, selected.Diagnostic);
            var restoredGraph = (CharacterLiveGraph)
                restarted.ActiveRuntime;
            Assert.That(
                restoredGraph.StrongboxAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(openingFingerprint));
            Assert.That(
                restoredGraph.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count,
                Is.EqualTo(uniqueCountAfterOpen));

            StrongboxOpeningResultLive replay =
                restoredGraph.StrongboxAuthority.Open(command);
            Assert.That(
                replay.Status,
                Is.EqualTo(
                    StrongboxOpeningLiveStatus.ExactDuplicateNoChange));
            Assert.That(
                restoredGraph.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count,
                Is.EqualTo(uniqueCountAfterOpen));
            Assert.That(
                replay.GeneratedOutcome.Fingerprint,
                Is.EqualTo(opened.GeneratedOutcome.Fingerprint));
        }

        private static CharacterInstanceSnapshot StarterCharacter(
            CharacterLiveGraphFactory factory,
            int slotIndex,
            string suffix)
        {
            StableId characterId = Id("character-instance." + suffix);
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayload route =
                PlayerRouteProfilePayload.Create(
                    characterId,
                    classId,
                    new StableId[
                        PlayerRouteProfilePayload.WeaponSlotCount]);
            ICharacterLiveGraph graph = factory.CreateStarter(
                slotIndex,
                characterId,
                classId,
                suffix,
                route);
            IReadOnlyList<SaveComponentSnapshot> components =
                PlayerAccountRestoreFlow.ExportComponents(
                    graph.SaveAdapters);
            graph.Dispose();
            return new CharacterInstanceSnapshot(
                characterId,
                classId,
                slotIndex,
                suffix,
                0L,
                components);
        }

        private static PlayerAccountSnapshot Account(
            params CharacterInstanceSnapshot[] characters)
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            foreach (CharacterInstanceSnapshot character in characters)
            {
                slots[character.SlotIndex] = character;
            }
            return new PlayerAccountSnapshot(
                Id("account.character-activation-strongbox-regression"),
                0L,
                slots,
                null);
        }

        private static PlayerAccountStoreResult Saved(
            PlayerAccountSnapshot snapshot)
        {
            return new PlayerAccountStoreResult(
                PlayerAccountStoreStatus.Saved,
                string.Empty,
                snapshot);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}

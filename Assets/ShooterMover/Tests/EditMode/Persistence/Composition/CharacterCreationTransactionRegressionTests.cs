using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CharacterCreationTransactionRegressionTests
    {
        [Test]
        public void FailedEmptySlotCreationKeepsPersistedActiveAndLeavesNoPartialCharacter()
        {
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshot alpha = StarterCharacter(
                factory,
                0,
                "creation-transaction-alpha");
            PlayerAccountSnapshot durable = Account(alpha);
            var authority = new PlayerAccountSaveState(durable);
            int saveCalls = 0;
            Func<PlayerAccountSnapshot, PlayerAccountStoreResult> save =
                snapshot =>
                {
                    saveCalls++;
                    if (saveCalls == 2)
                    {
                        return new PlayerAccountStoreResult(
                            PlayerAccountStoreStatus.IoFailure,
                            "simulated-character-create-write-failure",
                            null);
                    }

                    durable = snapshot;
                    return Saved(snapshot);
                };
            var composition = new CharacterSetupFlow(
                authority,
                factory,
                save);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var alphaGraph = (CharacterLiveGraph)
                composition.ActiveRuntime;
            alphaGraph.MoneyWallet.Grant(
                Id("transaction.creation-transaction-alpha-money"),
                Id("operation.creation-transaction-alpha-money"),
                73L);
            Assert.That(alphaGraph.MoneyWallet.Balance, Is.EqualTo(73L));

            SaveMigrationResult attempted =
                new SaveMigration(
                    authority,
                    factory,
                    save).Migrate(new[]
                    {
                        LegacyProfile(1, "creation-transaction-bravo"),
                    });

            Assert.That(attempted.Succeeded, Is.False);
            Assert.That(
                attempted.Diagnostic,
                Does.Contain("character-create-transaction-rejected"));
            Assert.That(saveCalls, Is.EqualTo(3));
            Assert.That(composition.ActiveSlotIndex, Is.EqualTo(0));
            Assert.That(composition.ActiveRuntime, Is.SameAs(alphaGraph));
            Assert.That(alphaGraph.IsDisposed, Is.False);
            Assert.That(alphaGraph.MoneyWallet.Balance, Is.EqualTo(73L));
            Assert.That(authority.Current.CharacterAt(1), Is.Null);
            Assert.That(durable.CharacterAt(1), Is.Null);

            composition.Dispose();
            var restarted = new CharacterSetupFlow(
                new PlayerAccountSaveState(durable),
                factory,
                Saved);
            CharacterSetupResult restored = restarted.Select(0);

            Assert.That(restored.Succeeded, Is.True, restored.Diagnostic);
            Assert.That(restarted.Account.CharacterAt(1), Is.Null);
            Assert.That(
                ((CharacterLiveGraph)restarted.ActiveRuntime)
                    .MoneyWallet.Balance,
                Is.EqualTo(73L));
        }

        private static LegacyCharacterProfile LegacyProfile(
            int slotIndex,
            string suffix)
        {
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayload route =
                PlayerRouteProfilePayload.Create(
                    Id("character." + suffix),
                    classId,
                    new StableId[
                        PlayerRouteProfilePayload.GunSlotCount]);
            return new LegacyCharacterProfile(
                slotIndex,
                suffix,
                route.SelectedCharacterStableId,
                classId,
                route.Fingerprint,
                route);
        }

        private static CharacterInstanceSnapshot StarterCharacter(
            CharacterLiveGraphFactory factory,
            int slotIndex,
            string suffix)
        {
            LegacyCharacterProfile profile = LegacyProfile(slotIndex, suffix);
            StableId characterId = Id("character-instance." + suffix);
            ICharacterLiveGraph graph = factory.CreateStarter(
                slotIndex,
                characterId,
                profile.ClassDefinitionStableId,
                suffix,
                profile.LegacyContext);
            IReadOnlyList<SavePartSnapshot> components =
                PlayerAccountRestoreFlow.ExportComponents(
                    graph.SaveAdapters);
            graph.Dispose();
            return new CharacterInstanceSnapshot(
                characterId,
                profile.ClassDefinitionStableId,
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
                Id("account.character-creation-transaction-regression"),
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

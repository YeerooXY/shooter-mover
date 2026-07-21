using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
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
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshotV1 alpha = StarterCharacter(
                factory,
                0,
                "creation-transaction-alpha");
            PlayerAccountSnapshotV1 durable = Account(alpha);
            var authority = new PlayerAccountSaveAuthorityV1(durable);
            int saveCalls = 0;
            Func<PlayerAccountSnapshotV1, PlayerAccountStoreResultV1> save =
                snapshot =>
                {
                    saveCalls++;
                    if (saveCalls == 2)
                    {
                        return new PlayerAccountStoreResultV1(
                            PlayerAccountStoreStatusV1.IoFailure,
                            "simulated-character-create-write-failure",
                            null);
                    }

                    durable = snapshot;
                    return Saved(snapshot);
                };
            var composition = new CharacterCompositionCoordinatorV1(
                authority,
                factory,
                save);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var alphaGraph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            alphaGraph.MoneyWallet.Grant(
                Id("transaction.creation-transaction-alpha-money"),
                Id("operation.creation-transaction-alpha-money"),
                73L);
            Assert.That(alphaGraph.MoneyWallet.Balance, Is.EqualTo(73L));

            LegacyCharacterProfileMigrationResultV1 attempted =
                new LegacyCharacterProfileMigrationV1(
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
            var restarted = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                Saved);
            CharacterCompositionResultV1 restored = restarted.Select(0);

            Assert.That(restored.Succeeded, Is.True, restored.Diagnostic);
            Assert.That(restarted.Account.CharacterAt(1), Is.Null);
            Assert.That(
                ((ProductionCharacterRuntimeGraphV1)restarted.ActiveRuntime)
                    .MoneyWallet.Balance,
                Is.EqualTo(73L));
        }

        private static LegacyCharacterProfileV1 LegacyProfile(
            int slotIndex,
            string suffix)
        {
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    Id("character." + suffix),
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
            return new LegacyCharacterProfileV1(
                slotIndex,
                suffix,
                route.SelectedCharacterStableId,
                classId,
                route.Fingerprint,
                route);
        }

        private static CharacterInstanceSnapshotV1 StarterCharacter(
            ProductionCharacterRuntimeGraphFactoryV1 factory,
            int slotIndex,
            string suffix)
        {
            LegacyCharacterProfileV1 profile = LegacyProfile(slotIndex, suffix);
            StableId characterId = Id("character-instance." + suffix);
            ICharacterRuntimeGraphV1 graph = factory.CreateStarter(
                slotIndex,
                characterId,
                profile.ClassDefinitionStableId,
                suffix,
                profile.LegacyContext);
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    graph.SaveAdapters);
            graph.Dispose();
            return new CharacterInstanceSnapshotV1(
                characterId,
                profile.ClassDefinitionStableId,
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
                Id("account.character-creation-transaction-regression"),
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

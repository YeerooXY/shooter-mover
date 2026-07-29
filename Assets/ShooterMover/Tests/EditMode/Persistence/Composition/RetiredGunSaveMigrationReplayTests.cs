using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class RetiredGunSaveMigrationReplayTests
    {
        private static readonly StableId RetiredDefinition =
            Id("equipment.production-starter-blaster");
        private static readonly StableId RetiredInstance =
            Id("equipment-instance.flow-draft-slot-1");

        [Test]
        public void MigrationPreservesAcceptedReplayHistoryAndStrongboxIdentity()
        {
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshot original = StarterCharacter(
                factory,
                "replay-history");
            PlayerHoldingsSnapshot baseHoldings = DecodeHoldings(original);
            InventoryLoadoutStateSnapshot baseLoadout =
                DecodeLoadout(original);

            var adapter = new EquipmentCatalogBridge(
                GunCatalogProvider.EquipmentCatalog);
            var authority = new PlayerHoldingsActions(
                baseHoldings.AuthorityStableId,
                baseHoldings.MaximumStackQuantity,
                adapter);
            var imported = authority.ImportSnapshot(baseHoldings);
            Assert.That(imported.Succeeded, Is.True, imported.RejectionCode);

            EquipmentInstance preservedEquipment = CurrentGun(
                "equipment-instance.test-preserved-current");
            StableId preservedTransaction =
                Id("transaction.test-preserved-current");
            StableId preservedOperation =
                Id("operation.test-preserved-current");
            PlayerHoldingsCommand preservedCommand =
                PlayerHoldingsCommand.AddEquipment(
                    preservedTransaction,
                    preservedOperation,
                    authority.AuthorityStableId,
                    preservedEquipment,
                    HoldingProvenance.Create(
                        Id("grant.test-preserved-current"),
                        Id("source.test-preserved-current")),
                    authority.Sequence);
            PlayerHoldingsMutationResult preservedResult =
                authority.Apply(preservedCommand);
            Assert.That(
                preservedResult.Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));

            StableId strongboxInstance =
                Id("strongbox-instance.test-preserved");
            PlayerHoldingsMutationResult strongboxResult = authority.Apply(
                PlayerHoldingsCommand.AddStrongbox(
                    Id("transaction.test-preserved-strongbox"),
                    Id("operation.test-preserved-strongbox"),
                    authority.AuthorityStableId,
                    Id("strongbox.tier-1"),
                    strongboxInstance,
                    HoldingProvenance.Create(
                        Id("grant.test-preserved-strongbox"),
                        Id("source.test-preserved-strongbox")),
                    authority.Sequence));
            Assert.That(
                strongboxResult.Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));

            PlayerHoldingsSnapshot beforeLegacyInjection =
                authority.ExportSnapshot();
            long sequenceBeforeMigration = authority.Sequence;
            string preservedStrongboxFingerprint = beforeLegacyInjection
                .UniqueHoldings.Single(item =>
                    item.InstanceStableId == strongboxInstance).Fingerprint;

            PlayerHoldingsSnapshot legacyHoldings =
                AddRetiredHolding(beforeLegacyInjection);
            CharacterInstanceSnapshot legacy = WithInventory(
                original,
                legacyHoldings,
                BindFirstGun(baseLoadout, RetiredInstance));
            PlayerAccountSnapshot account = Account(legacy);

            RetiredGunSaveMigrationResult migration =
                RetiredGunSaveMigration.Migrate(
                    account,
                    () => Id("equipment-instance.test-migration-replacement"));

            Assert.That(migration.Succeeded, Is.True, migration.Diagnostic);
            Assert.That(migration.Changed, Is.True);
            PlayerHoldingsSnapshot migrated = DecodeHoldings(
                migration.Account.CharacterAt(0));
            Assert.That(
                migrated.UniqueHoldings.Any(item =>
                    item.InstanceStableId == RetiredInstance),
                Is.False);
            Assert.That(
                migrated.UniqueHoldings.Single(item =>
                    item.InstanceStableId == strongboxInstance).Fingerprint,
                Is.EqualTo(preservedStrongboxFingerprint));

            var restored = new PlayerHoldingsActions(
                migrated.AuthorityStableId,
                migrated.MaximumStackQuantity,
                adapter);
            var restoredImport = restored.ImportSnapshot(migrated);
            Assert.That(
                restoredImport.Succeeded,
                Is.True,
                restoredImport.RejectionCode);
            Assert.That(
                restored.Sequence,
                Is.GreaterThanOrEqualTo(sequenceBeforeMigration));

            PlayerHoldingsMutationResult replay =
                restored.Apply(preservedCommand);
            Assert.That(
                replay.Status,
                Is.EqualTo(
                    PlayerHoldingsMutationStatus.ExactDuplicateNoChange));

            EquipmentInstance conflictEquipment = CurrentGun(
                "equipment-instance.test-conflicting-replay");
            PlayerHoldingsCommand conflict =
                PlayerHoldingsCommand.AddEquipment(
                    Id("transaction.test-conflicting-replay"),
                    preservedOperation,
                    restored.AuthorityStableId,
                    conflictEquipment,
                    HoldingProvenance.Create(
                        Id("grant.test-conflicting-replay"),
                        Id("source.test-conflicting-replay")),
                    restored.Sequence);
            PlayerHoldingsMutationResult conflictResult =
                restored.Apply(conflict);
            Assert.That(
                conflictResult.Status,
                Is.Not.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(
                conflictResult.Status,
                Is.Not.EqualTo(
                    PlayerHoldingsMutationStatus.ExactDuplicateNoChange));
        }

        [Test]
        public void MigrationFailureReturnsTheOriginalAccountUnchanged()
        {
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshot original = StarterCharacter(
                factory,
                "atomic-failure");
            PlayerHoldingsSnapshot holdings = DecodeHoldings(original);
            InventoryLoadoutStateSnapshot loadout =
                DecodeLoadout(original);
            StableId existingInstance = holdings.UniqueHoldings
                .First(item => item.RewardKind
                    == RewardGrantKind.EquipmentReference)
                .InstanceStableId;
            CharacterInstanceSnapshot legacy = WithInventory(
                original,
                AddRetiredHolding(holdings),
                BindFirstGun(loadout, RetiredInstance));
            PlayerAccountSnapshot account = Account(legacy);

            RetiredGunSaveMigrationResult result =
                RetiredGunSaveMigration.Migrate(
                    account,
                    () => existingInstance);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Account, Is.Not.Null);
            Assert.That(
                result.Account.Fingerprint,
                Is.EqualTo(account.Fingerprint));
        }

        private static CharacterInstanceSnapshot StarterCharacter(
            CharacterLiveGraphFactory factory,
            string suffix)
        {
            StableId characterId = Id("character-instance." + suffix);
            StableId classId = Id(
                GunMountPolicy.AggressiveLoadoutProfileId);
            PlayerRouteProfilePayload route =
                PlayerRouteProfilePayload.Create(
                    characterId,
                    classId,
                    new StableId[
                        PlayerRouteProfilePayload.GunSlotCount]);
            ICharacterLiveGraph graph = factory.CreateStarter(
                0,
                characterId,
                classId,
                suffix,
                route);
            IReadOnlyList<SavePartSnapshot> components =
                PlayerAccountRestoreFlow.ExportComponents(
                    graph.SaveAdapters);
            graph.Dispose();
            return new CharacterInstanceSnapshot(
                characterId,
                classId,
                0,
                suffix,
                0L,
                components);
        }

        private static EquipmentInstance CurrentGun(string instanceId)
        {
            EquipmentDefinition definition =
                GunCatalogProvider.EquipmentCatalog
                    .FindEquipmentDefinition(
                        Id("equipment.gun-rattler-mk1"));
            Assert.That(definition, Is.Not.Null);
            return EquipmentInstance.Create(
                Id(instanceId),
                definition.DefinitionId,
                definition.ItemLevelRange.Minimum,
                definition.QualityTiers[0].QualityId,
                Array.Empty<AugmentInstance>());
        }

        private static PlayerHoldingsSnapshot AddRetiredHolding(
            PlayerHoldingsSnapshot source)
        {
            var unique = new List<UniqueHoldingSnapshot>(
                source.UniqueHoldings);
            EquipmentInstance equipment = EquipmentInstance.Create(
                RetiredInstance,
                RetiredDefinition,
                1,
                Id("equipment-quality.common"),
                Array.Empty<AugmentInstance>());
            unique.Add(UniqueHoldingSnapshot.Create(
                RewardGrantKind.EquipmentReference,
                RetiredDefinition,
                RetiredInstance,
                equipment,
                HoldingProvenance.Create(
                    Id("grant.test-retired-replay"),
                    Id("source.test-retired-replay"))));
            return PlayerHoldingsSnapshot.CreateCanonical(
                source.SchemaVersion,
                source.AuthorityStableId,
                source.MaximumStackQuantity,
                source.LedgerSnapshot,
                unique,
                source.StackHoldings,
                source.Transactions);
        }

        private static InventoryLoadoutStateSnapshot BindFirstGun(
            InventoryLoadoutStateSnapshot source,
            StableId instanceStableId)
        {
            var bindings = source.Bindings.Select(item =>
                new InventoryLoadoutSlotBinding(
                    item.SlotStableId,
                    item.SlotStableId == InventoryLoadoutSlotIds.GunOne
                        ? instanceStableId
                        : item.EquipmentInstanceStableId)).ToArray();
            return InventoryLoadoutStateSnapshot.CreateCanonical(
                source.Sequence + 1L,
                bindings);
        }

        private static CharacterInstanceSnapshot WithInventory(
            CharacterInstanceSnapshot character,
            PlayerHoldingsSnapshot holdings,
            InventoryLoadoutStateSnapshot loadout)
        {
            var components = character.Components.Values.ToDictionary(
                item => item.ComponentStableId,
                item => item);
            SavePartDefinition holdingsDefinition =
                GameSaveParts.PlayerHoldings();
            SavePartDefinition loadoutDefinition =
                GameSaveParts.ExactInstanceLoadout();
            components[holdingsDefinition.ComponentStableId] = Component(
                holdingsDefinition,
                GameSaveFormats.PlayerHoldings.Encode(holdings));
            components[loadoutDefinition.ComponentStableId] = Component(
                loadoutDefinition,
                GameSaveFormats.ExactInstanceLoadout.Encode(loadout));
            return new CharacterInstanceSnapshot(
                character.CharacterInstanceStableId,
                character.ClassDefinitionStableId,
                character.SlotIndex,
                character.DisplayName,
                character.Revision,
                components.Values);
        }

        private static PlayerHoldingsSnapshot DecodeHoldings(
            CharacterInstanceSnapshot character)
        {
            SavePartSnapshot component = character.Components[
                GameSaveParts.PlayerHoldings()
                    .ComponentStableId];
            PlayerHoldingsSnapshot snapshot;
            string rejection;
            Assert.That(
                GameSaveFormats.PlayerHoldings.TryDecode(
                    component.CanonicalPayload,
                    out snapshot,
                    out rejection),
                Is.True,
                rejection);
            return snapshot;
        }

        private static InventoryLoadoutStateSnapshot DecodeLoadout(
            CharacterInstanceSnapshot character)
        {
            SavePartSnapshot component = character.Components[
                GameSaveParts.ExactInstanceLoadout()
                    .ComponentStableId];
            InventoryLoadoutStateSnapshot snapshot;
            string rejection;
            Assert.That(
                GameSaveFormats.ExactInstanceLoadout.TryDecode(
                    component.CanonicalPayload,
                    out snapshot,
                    out rejection),
                Is.True,
                rejection);
            return snapshot;
        }

        private static SavePartSnapshot Component(
            SavePartDefinition definition,
            string payload)
        {
            return new SavePartSnapshot(
                definition.ComponentStableId,
                definition.SchemaVersion,
                definition.ContentVersion,
                payload);
        }

        private static PlayerAccountSnapshot Account(
            CharacterInstanceSnapshot character)
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            slots[0] = character;
            return new PlayerAccountSnapshot(
                Id("account.retired-gun-replay-test"),
                0L,
                slots,
                null);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}

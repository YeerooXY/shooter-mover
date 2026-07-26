using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
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
    public sealed class RetiredWeaponSaveMigrationReplayTests
    {
        private static readonly StableId RetiredDefinition =
            Id("equipment.production-starter-blaster");
        private static readonly StableId RetiredInstance =
            Id("equipment-instance.flow-draft-slot-1");

        [Test]
        public void MigrationPreservesAcceptedReplayHistoryAndStrongboxIdentity()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshotV1 original = StarterCharacter(
                factory,
                "replay-history");
            PlayerHoldingsSnapshotV1 baseHoldings = DecodeHoldings(original);
            InventoryLoadoutAuthoritySnapshotV1 baseLoadout =
                DecodeLoadout(original);

            var adapter = new ProductionEquipmentCatalogAdapterV1(
                ProductionWeaponCatalogProvider.EquipmentCatalog);
            var authority = new PlayerHoldingsService(
                baseHoldings.AuthorityStableId,
                baseHoldings.MaximumStackQuantity,
                adapter);
            var imported = authority.ImportSnapshot(baseHoldings);
            Assert.That(imported.Succeeded, Is.True, imported.RejectionCode);

            EquipmentInstance preservedEquipment = CurrentWeapon(
                "equipment-instance.test-preserved-current");
            StableId preservedTransaction =
                Id("transaction.test-preserved-current");
            StableId preservedOperation =
                Id("operation.test-preserved-current");
            PlayerHoldingsCommandV1 preservedCommand =
                PlayerHoldingsCommandV1.AddEquipment(
                    preservedTransaction,
                    preservedOperation,
                    authority.AuthorityStableId,
                    preservedEquipment,
                    HoldingProvenanceV1.Create(
                        Id("grant.test-preserved-current"),
                        Id("source.test-preserved-current")),
                    authority.Sequence);
            PlayerHoldingsMutationResultV1 preservedResult =
                authority.Apply(preservedCommand);
            Assert.That(
                preservedResult.Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));

            StableId strongboxInstance =
                Id("strongbox-instance.test-preserved");
            PlayerHoldingsMutationResultV1 strongboxResult = authority.Apply(
                PlayerHoldingsCommandV1.AddStrongbox(
                    Id("transaction.test-preserved-strongbox"),
                    Id("operation.test-preserved-strongbox"),
                    authority.AuthorityStableId,
                    Id("strongbox.tier-1"),
                    strongboxInstance,
                    HoldingProvenanceV1.Create(
                        Id("grant.test-preserved-strongbox"),
                        Id("source.test-preserved-strongbox")),
                    authority.Sequence));
            Assert.That(
                strongboxResult.Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));

            PlayerHoldingsSnapshotV1 beforeLegacyInjection =
                authority.ExportSnapshot();
            long sequenceBeforeMigration = authority.Sequence;
            string preservedStrongboxFingerprint = beforeLegacyInjection
                .UniqueHoldings.Single(item =>
                    item.InstanceStableId == strongboxInstance).Fingerprint;

            PlayerHoldingsSnapshotV1 legacyHoldings =
                AddRetiredHolding(beforeLegacyInjection);
            CharacterInstanceSnapshotV1 legacy = WithInventory(
                original,
                legacyHoldings,
                BindFirstWeapon(baseLoadout, RetiredInstance));
            PlayerAccountSnapshotV1 account = Account(legacy);

            RetiredWeaponSaveMigrationResultV1 migration =
                RetiredWeaponSaveMigrationV1.Migrate(
                    account,
                    () => Id("equipment-instance.test-migration-replacement"));

            Assert.That(migration.Succeeded, Is.True, migration.Diagnostic);
            Assert.That(migration.Changed, Is.True);
            PlayerHoldingsSnapshotV1 migrated = DecodeHoldings(
                migration.Account.CharacterAt(0));
            Assert.That(
                migrated.UniqueHoldings.Any(item =>
                    item.InstanceStableId == RetiredInstance),
                Is.False);
            Assert.That(
                migrated.UniqueHoldings.Single(item =>
                    item.InstanceStableId == strongboxInstance).Fingerprint,
                Is.EqualTo(preservedStrongboxFingerprint));

            var restored = new PlayerHoldingsService(
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

            PlayerHoldingsMutationResultV1 replay =
                restored.Apply(preservedCommand);
            Assert.That(
                replay.Status,
                Is.EqualTo(
                    PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange));

            EquipmentInstance conflictEquipment = CurrentWeapon(
                "equipment-instance.test-conflicting-replay");
            PlayerHoldingsCommandV1 conflict =
                PlayerHoldingsCommandV1.AddEquipment(
                    Id("transaction.test-conflicting-replay"),
                    preservedOperation,
                    restored.AuthorityStableId,
                    conflictEquipment,
                    HoldingProvenanceV1.Create(
                        Id("grant.test-conflicting-replay"),
                        Id("source.test-conflicting-replay")),
                    restored.Sequence);
            PlayerHoldingsMutationResultV1 conflictResult =
                restored.Apply(conflict);
            Assert.That(
                conflictResult.Status,
                Is.Not.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(
                conflictResult.Status,
                Is.Not.EqualTo(
                    PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange));
        }

        [Test]
        public void MigrationFailureReturnsTheOriginalAccountUnchanged()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshotV1 original = StarterCharacter(
                factory,
                "atomic-failure");
            PlayerHoldingsSnapshotV1 holdings = DecodeHoldings(original);
            InventoryLoadoutAuthoritySnapshotV1 loadout =
                DecodeLoadout(original);
            StableId existingInstance = holdings.UniqueHoldings
                .First(item => item.RewardKind
                    == RewardGrantKindV1.EquipmentReference)
                .InstanceStableId;
            CharacterInstanceSnapshotV1 legacy = WithInventory(
                original,
                AddRetiredHolding(holdings),
                BindFirstWeapon(loadout, RetiredInstance));
            PlayerAccountSnapshotV1 account = Account(legacy);

            RetiredWeaponSaveMigrationResultV1 result =
                RetiredWeaponSaveMigrationV1.Migrate(
                    account,
                    () => existingInstance);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Account, Is.Not.Null);
            Assert.That(
                result.Account.Fingerprint,
                Is.EqualTo(account.Fingerprint));
        }

        private static CharacterInstanceSnapshotV1 StarterCharacter(
            ProductionCharacterRuntimeGraphFactoryV1 factory,
            string suffix)
        {
            StableId characterId = Id("character-instance." + suffix);
            StableId classId = Id(
                ProductionWeaponMountPolicyV1.AggressiveLoadoutProfileId);
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    characterId,
                    classId,
                    new StableId[
                        PlayerRouteProfilePayloadV1.WeaponSlotCount]);
            ICharacterRuntimeGraphV1 graph = factory.CreateStarter(
                0,
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
                0,
                suffix,
                0L,
                components);
        }

        private static EquipmentInstance CurrentWeapon(string instanceId)
        {
            EquipmentDefinition definition =
                ProductionWeaponCatalogProvider.EquipmentCatalog
                    .FindEquipmentDefinition(
                        Id("equipment.weapon-rattler-mk1"));
            Assert.That(definition, Is.Not.Null);
            return EquipmentInstance.Create(
                Id(instanceId),
                definition.DefinitionId,
                definition.ItemLevelRange.Minimum,
                definition.QualityTiers[0].QualityId,
                Array.Empty<AugmentInstance>());
        }

        private static PlayerHoldingsSnapshotV1 AddRetiredHolding(
            PlayerHoldingsSnapshotV1 source)
        {
            var unique = new List<UniqueHoldingSnapshotV1>(
                source.UniqueHoldings);
            EquipmentInstance equipment = EquipmentInstance.Create(
                RetiredInstance,
                RetiredDefinition,
                1,
                Id("equipment-quality.common"),
                Array.Empty<AugmentInstance>());
            unique.Add(UniqueHoldingSnapshotV1.Create(
                RewardGrantKindV1.EquipmentReference,
                RetiredDefinition,
                RetiredInstance,
                equipment,
                HoldingProvenanceV1.Create(
                    Id("grant.test-retired-replay"),
                    Id("source.test-retired-replay"))));
            return PlayerHoldingsSnapshotV1.CreateCanonical(
                source.SchemaVersion,
                source.AuthorityStableId,
                source.MaximumStackQuantity,
                source.LedgerSnapshot,
                unique,
                source.StackHoldings,
                source.Transactions);
        }

        private static InventoryLoadoutAuthoritySnapshotV1 BindFirstWeapon(
            InventoryLoadoutAuthoritySnapshotV1 source,
            StableId instanceStableId)
        {
            var bindings = source.Bindings.Select(item =>
                new InventoryLoadoutSlotBindingV1(
                    item.SlotStableId,
                    item.SlotStableId == InventoryLoadoutSlotIdsV1.WeaponOne
                        ? instanceStableId
                        : item.EquipmentInstanceStableId)).ToArray();
            return InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                source.Sequence + 1L,
                bindings);
        }

        private static CharacterInstanceSnapshotV1 WithInventory(
            CharacterInstanceSnapshotV1 character,
            PlayerHoldingsSnapshotV1 holdings,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            var components = character.Components.Values.ToDictionary(
                item => item.ComponentStableId,
                item => item);
            SaveComponentDefinitionV1 holdingsDefinition =
                KnownSaveComponentDefinitionsV1.PlayerHoldings();
            SaveComponentDefinitionV1 loadoutDefinition =
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout();
            components[holdingsDefinition.ComponentStableId] = Component(
                holdingsDefinition,
                KnownSaveComponentCodecsV1.PlayerHoldings.Encode(holdings));
            components[loadoutDefinition.ComponentStableId] = Component(
                loadoutDefinition,
                KnownSaveComponentCodecsV1.ExactInstanceLoadout.Encode(loadout));
            return new CharacterInstanceSnapshotV1(
                character.CharacterInstanceStableId,
                character.ClassDefinitionStableId,
                character.SlotIndex,
                character.DisplayName,
                character.Revision,
                components.Values);
        }

        private static PlayerHoldingsSnapshotV1 DecodeHoldings(
            CharacterInstanceSnapshotV1 character)
        {
            SaveComponentSnapshotV1 component = character.Components[
                KnownSaveComponentDefinitionsV1.PlayerHoldings()
                    .ComponentStableId];
            PlayerHoldingsSnapshotV1 snapshot;
            string rejection;
            Assert.That(
                KnownSaveComponentCodecsV1.PlayerHoldings.TryDecode(
                    component.CanonicalPayload,
                    out snapshot,
                    out rejection),
                Is.True,
                rejection);
            return snapshot;
        }

        private static InventoryLoadoutAuthoritySnapshotV1 DecodeLoadout(
            CharacterInstanceSnapshotV1 character)
        {
            SaveComponentSnapshotV1 component = character.Components[
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout()
                    .ComponentStableId];
            InventoryLoadoutAuthoritySnapshotV1 snapshot;
            string rejection;
            Assert.That(
                KnownSaveComponentCodecsV1.ExactInstanceLoadout.TryDecode(
                    component.CanonicalPayload,
                    out snapshot,
                    out rejection),
                Is.True,
                rejection);
            return snapshot;
        }

        private static SaveComponentSnapshotV1 Component(
            SaveComponentDefinitionV1 definition,
            string payload)
        {
            return new SaveComponentSnapshotV1(
                definition.ComponentStableId,
                definition.SchemaVersion,
                definition.ContentVersion,
                payload);
        }

        private static PlayerAccountSnapshotV1 Account(
            CharacterInstanceSnapshotV1 character)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = character;
            return new PlayerAccountSnapshotV1(
                Id("account.retired-weapon-replay-test"),
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

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class ProductionWeaponOnboardingAndMigrationTests
    {
        private static readonly StableId RetiredDefinition =
            Id("equipment.production-starter-blaster");
        private static readonly StableId RetiredInstance =
            Id("equipment-instance.flow-draft-slot-1");

        [Test]
        public void AuthoredCatalogueIsTheOnlyProductionWeaponSource()
        {
            ProductionWeaponCatalogueProjectionV1 projection =
                ProductionWeaponCatalogProvider.Current;
            StableId[] retiredDefinitions =
            {
                Id("equipment.production-starter-blaster"),
                Id("equipment.production-starter-shotgun"),
                Id("equipment.production-starter-rocket-launcher"),
                Id("equipment.production-starter-arc-gun"),
                Id("equipment.production-starter-ricochet-gun"),
            };

            Assert.That(projection.Blueprints.Count, Is.EqualTo(18));
            Assert.That(projection.EquipmentDefinitionIds.Count, Is.EqualTo(18));
            Assert.That(
                projection.EquipmentCatalog.EquipmentDefinitions.Count,
                Is.EqualTo(18));
            Assert.That(
                typeof(ProductionWeaponCatalogProvider).Assembly.GetType(
                    "ShooterMover.Application.Flow.Production.ProductionStarterWeaponCatalogV1"),
                Is.Null);
            foreach (StableId retired in retiredDefinitions)
            {
                Assert.That(
                    projection.EquipmentCatalog.FindEquipmentDefinition(retired),
                    Is.Null);
            }

            foreach (WeaponBlueprint blueprint in projection.Blueprints)
            {
                ProductionWeaponMarkV1 mark;
                Assert.That(
                    projection.TryGetMark(
                        blueprint.DefinitionId.ToString(),
                        out mark),
                    Is.True);
                EquipmentDefinition equipment = projection.EquipmentCatalog
                    .FindEquipmentDefinition(mark.EquipmentDefinitionId);
                Assert.That(equipment, Is.Not.Null);
                Assert.That(
                    equipment.RuntimeWeaponReferenceId.ToString(),
                    Is.EqualTo(blueprint.DefinitionId.ToString()));
            }
        }

        [Test]
        public void RetiredHoldingsAndBindingsAreDeletedThenRequiredMountIsRefilled()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshotV1 original = StarterCharacter(
                factory,
                0,
                "migration-retired-holding",
                ProductionWeaponMountPolicyV1.AggressiveLoadoutProfileId);
            PlayerHoldingsSnapshotV1 originalHoldings = DecodeHoldings(original);
            InventoryLoadoutAuthoritySnapshotV1 originalLoadout =
                DecodeLoadout(original);
            StableId[] currentOwned = originalHoldings.UniqueHoldings
                .Select(item => item.InstanceStableId).ToArray();

            CharacterInstanceSnapshotV1 legacy = WithInventory(
                original,
                AddRetiredHolding(originalHoldings),
                BindFirstWeapon(originalLoadout, RetiredInstance));
            PlayerAccountSnapshotV1 account = Account(legacy);
            Dictionary<StableId, string> unrelated = legacy.Components.Values
                .Where(item => !IsInventoryComponent(item.ComponentStableId))
                .ToDictionary(
                    item => item.ComponentStableId,
                    item => item.Fingerprint);
            int generated = 0;

            RetiredWeaponSaveMigrationResultV1 migrated =
                RetiredWeaponSaveMigrationV1.Migrate(
                    account,
                    () => Id(
                        "equipment-instance.test-migrated-"
                        + (++generated)));

            Assert.That(migrated.Succeeded, Is.True, migrated.Diagnostic);
            Assert.That(migrated.Changed, Is.True);
            Assert.That(migrated.MigratedCharacterCount, Is.EqualTo(1));
            Assert.That(generated, Is.EqualTo(1));
            Assert.That(
                PlayerAccountComponentSemanticsV1.Validate(migrated.Account)
                    .Succeeded,
                Is.True);

            CharacterInstanceSnapshotV1 character =
                migrated.Account.CharacterAt(0);
            PlayerHoldingsSnapshotV1 holdings = DecodeHoldings(character);
            InventoryLoadoutAuthoritySnapshotV1 loadout =
                DecodeLoadout(character);
            StableId[] owned = holdings.UniqueHoldings
                .Select(item => item.InstanceStableId).ToArray();
            StableId[] equipped = loadout.Bindings
                .Where(item => item.EquipmentInstanceStableId != null)
                .Select(item => item.EquipmentInstanceStableId).ToArray();

            Assert.That(owned, Does.Not.Contain(RetiredInstance));
            Assert.That(
                holdings.UniqueHoldings.Any(item =>
                    item.DefinitionStableId == RetiredDefinition),
                Is.False);
            Assert.That(currentOwned.All(owned.Contains), Is.True);
            Assert.That(equipped.Length, Is.EqualTo(2));
            Assert.That(equipped.Distinct().Count(), Is.EqualTo(2));
            Assert.That(equipped.All(owned.Contains), Is.True);
            Assert.That(equipped, Does.Not.Contain(RetiredInstance));
            foreach (KeyValuePair<StableId, string> pair in unrelated)
            {
                Assert.That(
                    character.Components[pair.Key].Fingerprint,
                    Is.EqualTo(pair.Value),
                    pair.Key.ToString());
            }

            ICharacterRuntimeGraphV1 restored =
                factory.CreateRestoreTarget(character);
            Assert.That(restored, Is.Not.Null);
            restored.Dispose();

            RetiredWeaponSaveMigrationResultV1 second =
                RetiredWeaponSaveMigrationV1.Migrate(
                    migrated.Account,
                    () => Id(
                        "equipment-instance.test-duplicate-"
                        + (++generated)));
            Assert.That(second.Succeeded, Is.True, second.Diagnostic);
            Assert.That(second.Changed, Is.False);
            Assert.That(
                second.Account.Fingerprint,
                Is.EqualTo(migrated.Account.Fingerprint));
            Assert.That(generated, Is.EqualTo(1));
        }

        [Test]
        public void RetiredLoadoutOnlyReferenceIsClearedWithoutTranslation()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshotV1 original = StarterCharacter(
                factory,
                0,
                "migration-loadout-only",
                ProductionWeaponMountPolicyV1.AggressiveLoadoutProfileId);
            PlayerHoldingsSnapshotV1 holdings = DecodeHoldings(original);
            CharacterInstanceSnapshotV1 legacy = WithInventory(
                original,
                holdings,
                BindFirstWeapon(DecodeLoadout(original), RetiredInstance));
            int generated = 0;

            RetiredWeaponSaveMigrationResultV1 result =
                RetiredWeaponSaveMigrationV1.Migrate(
                    Account(legacy),
                    () => Id(
                        "equipment-instance.test-loadout-repair-"
                        + (++generated)));

            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            Assert.That(result.Changed, Is.True);
            Assert.That(generated, Is.EqualTo(1));
            PlayerHoldingsSnapshotV1 migratedHoldings =
                DecodeHoldings(result.Account.CharacterAt(0));
            InventoryLoadoutAuthoritySnapshotV1 migratedLoadout =
                DecodeLoadout(result.Account.CharacterAt(0));
            Assert.That(
                migratedLoadout.Bindings.Any(item =>
                    item.EquipmentInstanceStableId == RetiredInstance),
                Is.False);
            Assert.That(
                migratedLoadout.Bindings
                    .Where(item => item.EquipmentInstanceStableId != null)
                    .All(item => migratedHoldings.UniqueHoldings.Any(holding =>
                        holding.InstanceStableId
                        == item.EquipmentInstanceStableId)),
                Is.True);
        }

        private static CharacterInstanceSnapshotV1 StarterCharacter(
            ProductionCharacterRuntimeGraphFactoryV1 factory,
            int slotIndex,
            string suffix,
            string loadoutProfileId)
        {
            StableId characterId = Id("character-instance." + suffix);
            StableId classId = Id(loadoutProfileId);
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    characterId,
                    classId,
                    new StableId[
                        PlayerRouteProfilePayloadV1.WeaponSlotCount]);
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
                    Id("grant.test-retired-weapon"),
                    Id("source.test-retired-save"))));
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

        private static bool IsInventoryComponent(StableId componentId)
        {
            return componentId
                    == KnownSaveComponentDefinitionsV1.PlayerHoldings()
                        .ComponentStableId
                || componentId
                    == KnownSaveComponentDefinitionsV1.ExactInstanceLoadout()
                        .ComponentStableId;
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
            slots[character.SlotIndex] = character;
            return new PlayerAccountSnapshotV1(
                Id("account.production-weapon-migration-test"),
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

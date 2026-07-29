using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class StarterLoadoutAndMigrationTests
    {
        private static readonly StableId RetiredDefinition =
            Id("equipment.production-starter-blaster");
        private static readonly StableId RetiredInstance =
            Id("equipment-instance.flow-draft-slot-1");

        [Test]
        public void AuthoredCatalogueIsTheOnlyProductionGunSource()
        {
            GunCatalogueView projection =
                GunCatalogProvider.Current;
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
                typeof(GunCatalogProvider).Assembly.GetType(
                    "ShooterMover.Application.Flow.Game.StarterGunCatalog"),
                Is.Null);
            foreach (StableId retired in retiredDefinitions)
            {
                Assert.That(
                    projection.EquipmentCatalog.FindEquipmentDefinition(retired),
                    Is.Null);
            }

            foreach (Gun blueprint in projection.Blueprints)
            {
                GunMark mark;
                Assert.That(
                    projection.TryGetMark(
                        blueprint.DefinitionId.ToString(),
                        out mark),
                    Is.True);
                EquipmentDefinition equipment = projection.EquipmentCatalog
                    .FindEquipmentDefinition(mark.EquipmentDefinitionId);
                Assert.That(equipment, Is.Not.Null);
                Assert.That(
                    equipment.RuntimeGunReferenceId.ToString(),
                    Is.EqualTo(blueprint.DefinitionId.ToString()));
            }
        }

        [Test]
        public void RetiredHoldingsAndBindingsAreDeletedThenRequiredMountIsRefilled()
        {
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshot original = StarterCharacter(
                factory,
                0,
                "migration-retired-holding",
                GunMountPolicy.AggressiveLoadoutProfileId);
            PlayerHoldingsSnapshot originalHoldings = DecodeHoldings(original);
            InventoryLoadoutStateSnapshot originalLoadout =
                DecodeLoadout(original);
            StableId[] currentOwned = originalHoldings.UniqueHoldings
                .Select(item => item.InstanceStableId).ToArray();

            CharacterInstanceSnapshot legacy = WithInventory(
                original,
                AddRetiredHolding(originalHoldings),
                BindFirstGun(originalLoadout, RetiredInstance));
            PlayerAccountSnapshot account = Account(legacy);
            Dictionary<StableId, string> unrelated = legacy.Components.Values
                .Where(item => !IsInventoryComponent(item.ComponentStableId))
                .ToDictionary(
                    item => item.ComponentStableId,
                    item => item.Fingerprint);
            int generated = 0;

            RetiredGunSaveMigrationResult migrated =
                RetiredGunSaveMigration.Migrate(
                    account,
                    () => Id(
                        "equipment-instance.test-migrated-"
                        + (++generated)));

            Assert.That(migrated.Succeeded, Is.True, migrated.Diagnostic);
            Assert.That(migrated.Changed, Is.True);
            Assert.That(migrated.MigratedCharacterCount, Is.EqualTo(1));
            Assert.That(generated, Is.EqualTo(1));
            Assert.That(
                GameSaveRules.Validate(migrated.Account)
                    .Succeeded,
                Is.True);

            CharacterInstanceSnapshot character =
                migrated.Account.CharacterAt(0);
            PlayerHoldingsSnapshot holdings = DecodeHoldings(character);
            InventoryLoadoutStateSnapshot loadout =
                DecodeLoadout(character);
            StableId[] owned = holdings.UniqueHoldings
                .Select(item => item.InstanceStableId).ToArray();
            StableId[] equipped = loadout.Bindings
                .Where(item => item.EquipmentInstanceStableId != null)
                .Select(item => item.EquipmentInstanceStableId).ToArray();

            Assert.That(owned.Any(value => value == RetiredInstance), Is.False);
            Assert.That(
                holdings.UniqueHoldings.Any(item =>
                    item.DefinitionStableId == RetiredDefinition),
                Is.False);
            Assert.That(currentOwned.All(owned.Contains), Is.True);
            Assert.That(equipped.Length, Is.EqualTo(2));
            Assert.That(equipped.Distinct().Count(), Is.EqualTo(2));
            Assert.That(equipped.All(owned.Contains), Is.True);
            Assert.That(equipped.Any(value => value == RetiredInstance), Is.False);
            foreach (KeyValuePair<StableId, string> pair in unrelated)
            {
                Assert.That(
                    character.Components[pair.Key].Fingerprint,
                    Is.EqualTo(pair.Value),
                    pair.Key.ToString());
            }

            ICharacterLiveGraph restored =
                factory.CreateRestoreTarget(character);
            Assert.That(restored, Is.Not.Null);
            restored.Dispose();

            RetiredGunSaveMigrationResult second =
                RetiredGunSaveMigration.Migrate(
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
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshot original = StarterCharacter(
                factory,
                0,
                "migration-loadout-only",
                GunMountPolicy.AggressiveLoadoutProfileId);
            PlayerHoldingsSnapshot holdings = DecodeHoldings(original);
            CharacterInstanceSnapshot legacy = WithInventory(
                original,
                holdings,
                BindFirstGun(DecodeLoadout(original), RetiredInstance));
            int generated = 0;

            RetiredGunSaveMigrationResult result =
                RetiredGunSaveMigration.Migrate(
                    Account(legacy),
                    () => Id(
                        "equipment-instance.test-loadout-repair-"
                        + (++generated)));

            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            Assert.That(result.Changed, Is.True);
            Assert.That(generated, Is.EqualTo(1));
            PlayerHoldingsSnapshot migratedHoldings =
                DecodeHoldings(result.Account.CharacterAt(0));
            InventoryLoadoutStateSnapshot migratedLoadout =
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

        private static CharacterInstanceSnapshot StarterCharacter(
            CharacterLiveGraphFactory factory,
            int slotIndex,
            string suffix,
            string loadoutProfileId)
        {
            StableId characterId = Id("character-instance." + suffix);
            StableId classId = Id(loadoutProfileId);
            PlayerRouteProfilePayload route =
                PlayerRouteProfilePayload.Create(
                    characterId,
                    classId,
                    new StableId[
                        PlayerRouteProfilePayload.GunSlotCount]);
            ICharacterLiveGraph graph = factory.CreateStarter(
                slotIndex,
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
                slotIndex,
                suffix,
                0L,
                components);
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
                    Id("grant.test-retired-gun"),
                    Id("source.test-retired-save"))));
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

        private static bool IsInventoryComponent(StableId componentId)
        {
            return componentId
                    == GameSaveParts.PlayerHoldings()
                        .ComponentStableId
                || componentId
                    == GameSaveParts.ExactInstanceLoadout()
                        .ComponentStableId;
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
            slots[character.SlotIndex] = character;
            return new PlayerAccountSnapshot(
                Id("account.production-gun-migration-test"),
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

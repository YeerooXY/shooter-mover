using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CharacterCompositionCoordinatorV1Tests
    {
        [Test]
        public void TwoCharactersMutateDifferentlyAndRestartWithoutCrossSlotLeakage()
        {
            FakeGraphFactory factory;
            PlayerAccountSaveAuthorityV1 authority;
            CharacterCompositionCoordinatorV1 composition = CreateComposition(
                Account(Character(0, "alpha"), Character(1, "bravo")),
                out factory,
                out authority);

            Assert.That(composition.Select(0).Succeeded, Is.True);
            FakeGraph alpha = (FakeGraph)composition.ActiveRuntime;
            alpha.State(KnownSaveComponentDefinitionsV1.MoneyWallet()).Value =
                "alpha-money-mutated";
            Assert.That(
                composition.PersistActive(Id("operation.save-alpha")).Succeeded,
                Is.True);

            Assert.That(composition.Select(1).Succeeded, Is.True);
            Assert.That(alpha.IsDisposed, Is.True);
            FakeGraph bravo = (FakeGraph)composition.ActiveRuntime;
            Assert.That(
                bravo.State(KnownSaveComponentDefinitionsV1.MoneyWallet()).Value,
                Is.EqualTo("bravo-money"));
            bravo.State(KnownSaveComponentDefinitionsV1.PlayerExperience()).Value =
                "bravo-xp-mutated";
            Assert.That(
                composition.PersistActive(Id("operation.save-bravo")).Succeeded,
                Is.True);

            composition.Dispose();
            var restarted = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(authority.Current),
                factory,
                Saved,
                snapshot => SaveComponentValidationResultV1.Accept());

            Assert.That(restarted.Select(0).Succeeded, Is.True);
            FakeGraph restoredAlpha = (FakeGraph)restarted.ActiveRuntime;
            Assert.That(
                restoredAlpha.State(
                    KnownSaveComponentDefinitionsV1.MoneyWallet()).Value,
                Is.EqualTo("alpha-money-mutated"));
            Assert.That(
                restoredAlpha.State(
                    KnownSaveComponentDefinitionsV1.PlayerExperience()).Value,
                Is.EqualTo("alpha-xp"));

            Assert.That(restarted.Select(1).Succeeded, Is.True);
            FakeGraph restoredBravo = (FakeGraph)restarted.ActiveRuntime;
            Assert.That(
                restoredBravo.State(
                    KnownSaveComponentDefinitionsV1.MoneyWallet()).Value,
                Is.EqualTo("bravo-money"));
            Assert.That(
                restoredBravo.State(
                    KnownSaveComponentDefinitionsV1.PlayerExperience()).Value,
                Is.EqualTo("bravo-xp-mutated"));
        }

        [Test]
        public void ReloadRestoresEveryKnownCharacterComponentIncludingStrongboxes()
        {
            FakeGraphFactory factory;
            PlayerAccountSaveAuthorityV1 ignored;
            CharacterCompositionCoordinatorV1 composition = CreateComposition(
                Account(Character(0, "all")),
                out factory,
                out ignored);

            CharacterCompositionResultV1 result = composition.Select(0);

            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            FakeGraph graph = (FakeGraph)composition.ActiveRuntime;
            foreach (SaveComponentDefinitionV1 definition in Definitions())
            {
                Assert.That(
                    graph.State(definition).Value,
                    Is.EqualTo("all-" + Suffix(definition)),
                    definition.ComponentStableId.ToString());
            }
        }

        [Test]
        public void SwitchingDisposesOldGraphBeforeNewGraphFactoryRuns()
        {
            FakeGraphFactory factory;
            PlayerAccountSaveAuthorityV1 ignored;
            CharacterCompositionCoordinatorV1 composition = CreateComposition(
                Account(Character(0, "first"), Character(1, "second")),
                out factory,
                out ignored);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            FakeGraph first = (FakeGraph)composition.ActiveRuntime;
            factory.BeforeCreate = () => Assert.That(first.IsDisposed, Is.True);

            Assert.That(composition.Select(1).Succeeded, Is.True);

            Assert.That(first.IsDisposed, Is.True);
            Assert.That(composition.ActiveSlotIndex, Is.EqualTo(1));
            Assert.That(composition.ActiveRuntime, Is.Not.SameAs(first));
        }

        [Test]
        public void CorruptSelectedCharacterFailsSafelyAndOtherSlotsStayUnchanged()
        {
            CharacterInstanceSnapshotV1 valid = Character(0, "valid");
            CharacterInstanceSnapshotV1 corrupt = ReplaceComponent(
                Character(1, "corrupt"),
                KnownSaveComponentDefinitionsV1.PlayerExperience(),
                "corrupt-payload");
            FakeGraphFactory factory;
            PlayerAccountSaveAuthorityV1 authority;
            CharacterCompositionCoordinatorV1 composition = CreateComposition(
                Account(valid, corrupt),
                out factory,
                out authority);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            FakeGraph previous = (FakeGraph)composition.ActiveRuntime;
            string validFingerprint = authority.Current.CharacterAt(0).Fingerprint;
            string corruptFingerprint = authority.Current.CharacterAt(1).Fingerprint;

            CharacterCompositionResultV1 result = composition.Select(1);

            Assert.That(
                result.Status,
                Is.EqualTo(CharacterCompositionStatusV1.Rejected));
            Assert.That(result.Diagnostic, Does.Contain("test-component-corrupt"));
            Assert.That(previous.IsDisposed, Is.True);
            Assert.That(composition.ActiveRuntime, Is.Null);
            Assert.That(composition.ActiveSlotIndex, Is.EqualTo(-1));
            Assert.That(
                authority.Current.CharacterAt(0).Fingerprint,
                Is.EqualTo(validFingerprint));
            Assert.That(
                authority.Current.CharacterAt(1).Fingerprint,
                Is.EqualTo(corruptFingerprint));
        }

        [Test]
        public void FailedDurableSaveRollsBackToLastValidAggregate()
        {
            var authority = new PlayerAccountSaveAuthorityV1(
                Account(Character(0, "rollback")));
            PlayerAccountSnapshotV1 before = authority.Current;
            var composition = new CharacterCompositionCoordinatorV1(
                authority,
                new FakeGraphFactory(),
                snapshot => new PlayerAccountStoreResultV1(
                    PlayerAccountStoreStatusV1.IoFailure,
                    "simulated-write-failure",
                    null),
                snapshot => SaveComponentValidationResultV1.Accept());
            Assert.That(composition.Select(0).Succeeded, Is.True);
            ((FakeGraph)composition.ActiveRuntime)
                .State(KnownSaveComponentDefinitionsV1.ScrapWallet()).Value =
                    "unsaved-scrap";

            CharacterCompositionResultV1 result = composition.PersistActive(
                Id("operation.failed-save"));

            Assert.That(
                result.Status,
                Is.EqualTo(CharacterCompositionStatusV1.Rejected));
            Assert.That(authority.Current.Fingerprint, Is.EqualTo(before.Fingerprint));
        }

        [Test]
        public void LegacyMigrationRunsOncePreservesClassAndAvoidsDuplicates()
        {
            StableId accountId = Id("account.migration");
            var authority = new PlayerAccountSaveAuthorityV1(
                PlayerAccountSnapshotV1.Empty(accountId));
            var factory = new FakeGraphFactory();
            var migration = new LegacyCharacterProfileMigrationV1(
                authority,
                factory,
                Saved);
            LegacyCharacterProfileV1[] profiles =
            {
                Legacy(0, "Pilot A", "frontier", "aggressive"),
                Legacy(4, "Pilot B", "custom", "healer"),
            };

            LegacyCharacterProfileMigrationResultV1 first =
                migration.Migrate(profiles);
            PlayerAccountSnapshotV1 afterFirst = authority.Current;
            LegacyCharacterProfileMigrationResultV1 second =
                migration.Migrate(profiles);

            Assert.That(
                first.Status,
                Is.EqualTo(CharacterCompositionStatusV1.Migrated));
            Assert.That(
                second.Status,
                Is.EqualTo(CharacterCompositionStatusV1.ExactNoChange));
            Assert.That(authority.Current.Fingerprint, Is.EqualTo(afterFirst.Fingerprint));
            Assert.That(
                afterFirst.CharacterAt(0).ClassDefinitionStableId,
                Is.EqualTo(Id("loadout-profile.aggressive")));
            Assert.That(
                afterFirst.CharacterAt(4).ClassDefinitionStableId,
                Is.EqualTo(Id("loadout-profile.healer")));
            Assert.That(
                afterFirst.CharacterAt(0).Components.Count,
                Is.EqualTo(Definitions().Count));
            Assert.That(
                afterFirst.CharacterAt(4).Components.Count,
                Is.EqualTo(Definitions().Count));
            Assert.That(
                afterFirst.CharacterAt(0).CharacterInstanceStableId,
                Is.EqualTo(LegacyCharacterProfileMigrationV1.ExactCharacterId(
                    accountId,
                    profiles[0])));
            Assert.That(factory.Created.All(item => item.IsDisposed), Is.True);
        }

        [Test]
        public void ProductionFactoryRoundTripsRealAuthoritiesAndExactBindings()
        {
            var factory = ProductionCharacterRuntimeGraphFactoryV1
                .CreateVerticalSliceDefaults();
            StableId characterId = Id("character-instance.real-roundtrip");
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayloadV1 legacyRoute =
                PlayerRouteProfilePayloadV1.Create(
                    Id("character.frontier"),
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
            ICharacterRuntimeGraphV1 starter = factory.CreateStarter(
                2,
                characterId,
                classId,
                "Real Pilot",
                legacyRoute);
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    starter.SaveAdapters);
            starter.Dispose();
            var character = new CharacterInstanceSnapshotV1(
                characterId,
                classId,
                2,
                "Real Pilot",
                0L,
                components);
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(Account(character)),
                factory,
                Saved);

            CharacterCompositionResultV1 selected = composition.Select(2);

            Assert.That(selected.Succeeded, Is.True, selected.Diagnostic);
            var graph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            InventoryLoadoutAuthoritySnapshotV1 loadout =
                graph.LoadoutRuntime.LoadoutAuthority.ExportSnapshot();
            Assert.That(
                loadout.GetBinding(InventoryLoadoutSlotIdsV1.WeaponOne)
                    .EquipmentInstanceStableId,
                Is.EqualTo(ProductionStarterWeaponCatalogV1
                    .BlasterEquipmentInstanceStableId));
            Assert.That(
                loadout.GetBinding(InventoryLoadoutSlotIdsV1.WeaponFour)
                    .EquipmentInstanceStableId,
                Is.EqualTo(ProductionStarterWeaponCatalogV1
                    .ArcEquipmentInstanceStableId));
            Assert.That(
                graph.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId
                            == ProductionStarterWeaponCatalogV1
                                .RocketEquipmentInstanceStableId),
                Is.True);
        }

        private static CharacterCompositionCoordinatorV1 CreateComposition(
            PlayerAccountSnapshotV1 account,
            out FakeGraphFactory factory,
            out PlayerAccountSaveAuthorityV1 authority)
        {
            factory = new FakeGraphFactory();
            authority = new PlayerAccountSaveAuthorityV1(account);
            return new CharacterCompositionCoordinatorV1(
                authority,
                factory,
                Saved,
                snapshot => SaveComponentValidationResultV1.Accept());
        }

        private static PlayerAccountStoreResultV1 Saved(
            PlayerAccountSnapshotV1 snapshot)
        {
            return new PlayerAccountStoreResultV1(
                PlayerAccountStoreStatusV1.Saved,
                string.Empty,
                snapshot);
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
                Id("account.character-composition"),
                0L,
                slots,
                null);
        }

        private static CharacterInstanceSnapshotV1 Character(
            int slotIndex,
            string prefix)
        {
            Dictionary<StableId, MutableState> states = Definitions()
                .ToDictionary(
                    definition => definition.ComponentStableId,
                    definition => new MutableState(
                        prefix + "-" + Suffix(definition)));
            FakeGraph graph = FakeGraph.Create(
                new CharacterInstanceSnapshotV1(
                    Id("character-instance." + prefix),
                    Id("loadout-profile." + prefix),
                    slotIndex,
                    prefix,
                    0L,
                    null),
                states);
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    graph.SaveAdapters);
            graph.Dispose();
            return new CharacterInstanceSnapshotV1(
                Id("character-instance." + prefix),
                Id("loadout-profile." + prefix),
                slotIndex,
                prefix,
                0L,
                components);
        }

        private static CharacterInstanceSnapshotV1 ReplaceComponent(
            CharacterInstanceSnapshotV1 character,
            SaveComponentDefinitionV1 definition,
            string payload)
        {
            Dictionary<StableId, SaveComponentSnapshotV1> components =
                character.Components.Values.ToDictionary(
                    item => item.ComponentStableId,
                    item => item);
            components[definition.ComponentStableId] =
                new SaveComponentSnapshotV1(
                    definition.ComponentStableId,
                    definition.SchemaVersion,
                    definition.ContentVersion,
                    payload);
            return new CharacterInstanceSnapshotV1(
                character.CharacterInstanceStableId,
                character.ClassDefinitionStableId,
                character.SlotIndex,
                character.DisplayName,
                character.Revision,
                components.Values);
        }

        private static LegacyCharacterProfileV1 Legacy(
            int slotIndex,
            string displayName,
            string characterName,
            string className)
        {
            return new LegacyCharacterProfileV1(
                slotIndex,
                displayName,
                Id("character." + characterName),
                Id("loadout-profile." + className),
                "legacy-fingerprint-" + slotIndex,
                "starter-" + slotIndex);
        }

        private static IReadOnlyList<SaveComponentDefinitionV1> Definitions()
        {
            return new[]
            {
                KnownSaveComponentDefinitionsV1.PlayerExperience(),
                KnownSaveComponentDefinitionsV1.PlayerHoldings(),
                KnownSaveComponentDefinitionsV1.MoneyWallet(),
                KnownSaveComponentDefinitionsV1.ScrapWallet(),
                KnownSaveComponentDefinitionsV1.RankedSkillAllocation(),
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout(),
                KnownSaveComponentDefinitionsV1.StrongboxState(),
            };
        }

        private static string Suffix(SaveComponentDefinitionV1 definition)
        {
            string value = definition.ComponentStableId.ToString();
            int separator = value.IndexOf('.');
            return separator < 0 ? value : value.Substring(separator + 1);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class FakeGraphFactory :
            ICharacterRuntimeGraphFactoryV1,
            IStarterCharacterRuntimeGraphFactoryV1
        {
            public List<FakeGraph> Created { get; } = new List<FakeGraph>();

            public Action BeforeCreate { get; set; }

            public ICharacterRuntimeGraphV1 CreateRestoreTarget(
                CharacterInstanceSnapshotV1 character)
            {
                if (BeforeCreate != null)
                {
                    BeforeCreate();
                    BeforeCreate = null;
                }
                Dictionary<StableId, MutableState> states = Definitions()
                    .ToDictionary(
                        definition => definition.ComponentStableId,
                        definition => new MutableState("empty"));
                FakeGraph graph = FakeGraph.Create(character, states);
                Created.Add(graph);
                return graph;
            }

            public ICharacterRuntimeGraphV1 CreateStarter(
                int slotIndex,
                StableId exactCharacterInstanceStableId,
                StableId classDefinitionStableId,
                string displayName,
                object legacyContext)
            {
                string prefix = legacyContext == null
                    ? "starter"
                    : legacyContext.ToString();
                Dictionary<StableId, MutableState> states = Definitions()
                    .ToDictionary(
                        definition => definition.ComponentStableId,
                        definition => new MutableState(
                            prefix + "-" + Suffix(definition)));
                FakeGraph graph = FakeGraph.Create(
                    new CharacterInstanceSnapshotV1(
                        exactCharacterInstanceStableId,
                        classDefinitionStableId,
                        slotIndex,
                        displayName,
                        0L,
                        null),
                    states);
                Created.Add(graph);
                return graph;
            }
        }

        private sealed class FakeGraph : ICharacterRuntimeGraphV1
        {
            private readonly Dictionary<StableId, MutableState> states;

            private FakeGraph(
                CharacterInstanceSnapshotV1 character,
                Dictionary<StableId, MutableState> states,
                IReadOnlyList<ISaveComponentAdapterV1> adapters)
            {
                Character = character;
                this.states = states;
                SaveAdapters = adapters;
            }

            public CharacterInstanceSnapshotV1 Character { get; private set; }

            public IReadOnlyList<ISaveComponentAdapterV1> SaveAdapters { get; }

            public bool IsDisposed { get; private set; }

            public static FakeGraph Create(
                CharacterInstanceSnapshotV1 character,
                Dictionary<StableId, MutableState> states)
            {
                var adapters = new List<ISaveComponentAdapterV1>();
                foreach (SaveComponentDefinitionV1 definition in Definitions())
                {
                    MutableState state = states[definition.ComponentStableId];
                    var codec = new TestCodec();
                    adapters.Add(
                        new AuthoritySnapshotSaveComponentAdapterV1<TestSnapshot>(
                            definition,
                            codec,
                            () => new TestSnapshot(state.Value),
                            snapshot => codec.Validate(snapshot),
                            snapshot =>
                            {
                                if (string.Equals(
                                    snapshot.Value,
                                    "corrupt-payload",
                                    StringComparison.Ordinal))
                                {
                                    return SaveComponentApplyResultV1.Rejected(
                                        "test-component-corrupt");
                                }
                                state.Value = snapshot.Value;
                                return SaveComponentApplyResultV1.Applied();
                            }));
                }
                return new FakeGraph(character, states, adapters);
            }

            public MutableState State(SaveComponentDefinitionV1 definition)
            {
                return states[definition.ComponentStableId];
            }

            public void MarkPersisted(CharacterInstanceSnapshotV1 character)
            {
                Character = character;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class MutableState
        {
            public MutableState(string value)
            {
                Value = value;
            }

            public string Value { get; set; }
        }

        private sealed class TestSnapshot
        {
            public TestSnapshot(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private sealed class TestCodec :
            ISaveComponentPayloadCodecV1<TestSnapshot>
        {
            public string ContractId
            {
                get { return "test-character-component-v1"; }
            }

            public string Encode(TestSnapshot snapshot)
            {
                return snapshot.Value;
            }

            public bool TryDecode(
                string canonicalPayload,
                out TestSnapshot snapshot,
                out string rejectionCode)
            {
                if (string.Equals(
                    canonicalPayload,
                    "corrupt-payload",
                    StringComparison.Ordinal))
                {
                    snapshot = null;
                    rejectionCode = "test-component-corrupt";
                    return false;
                }
                snapshot = new TestSnapshot(canonicalPayload);
                rejectionCode = string.Empty;
                return true;
            }

            public SaveComponentValidationResultV1 Validate(
                TestSnapshot snapshot)
            {
                return snapshot == null || snapshot.Value == null
                    ? SaveComponentValidationResultV1.Reject(
                        "test-component-null")
                    : SaveComponentValidationResultV1.Accept();
            }
        }
    }
}

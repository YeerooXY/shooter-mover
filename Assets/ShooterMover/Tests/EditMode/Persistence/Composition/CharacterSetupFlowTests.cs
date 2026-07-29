using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CharacterSetupFlowTests
    {
        [Test]
        public void TwoCharactersMutateDifferentlyAndRestartWithoutCrossSlotLeakage()
        {
            FakeGraphFactory factory;
            PlayerAccountSaveState authority;
            CharacterSetupFlow composition = CreateComposition(
                Account(Character(0, "alpha"), Character(1, "bravo")),
                out factory,
                out authority);

            Assert.That(composition.Select(0).Succeeded, Is.True);
            FakeGraph alpha = (FakeGraph)composition.ActiveRuntime;
            alpha.State(GameSaveParts.MoneyWallet()).Value =
                "alpha-money-mutated";
            Assert.That(
                composition.PersistActive(Id("operation.save-alpha")).Succeeded,
                Is.True);

            Assert.That(composition.Select(1).Succeeded, Is.True);
            Assert.That(alpha.IsDisposed, Is.True);
            FakeGraph bravo = (FakeGraph)composition.ActiveRuntime;
            Assert.That(
                bravo.State(GameSaveParts.MoneyWallet()).Value,
                Is.EqualTo("bravo-money"));
            bravo.State(GameSaveParts.PlayerExperience()).Value =
                "bravo-xp-mutated";
            Assert.That(
                composition.PersistActive(Id("operation.save-bravo")).Succeeded,
                Is.True);

            composition.Dispose();
            var restarted = new CharacterSetupFlow(
                new PlayerAccountSaveState(authority.Current),
                factory,
                Saved,
                snapshot => SavePartValidationResult.Accept());

            Assert.That(restarted.Select(0).Succeeded, Is.True);
            FakeGraph restoredAlpha = (FakeGraph)restarted.ActiveRuntime;
            Assert.That(
                restoredAlpha.State(
                    GameSaveParts.MoneyWallet()).Value,
                Is.EqualTo("alpha-money-mutated"));
            Assert.That(
                restoredAlpha.State(
                    GameSaveParts.PlayerExperience()).Value,
                Is.EqualTo("alpha-xp"));

            Assert.That(restarted.Select(1).Succeeded, Is.True);
            FakeGraph restoredBravo = (FakeGraph)restarted.ActiveRuntime;
            Assert.That(
                restoredBravo.State(
                    GameSaveParts.MoneyWallet()).Value,
                Is.EqualTo("bravo-money"));
            Assert.That(
                restoredBravo.State(
                    GameSaveParts.PlayerExperience()).Value,
                Is.EqualTo("bravo-xp-mutated"));
        }

        [Test]
        public void ReloadRestoresEveryKnownCharacterComponentIncludingStrongboxes()
        {
            FakeGraphFactory factory;
            PlayerAccountSaveState ignored;
            CharacterSetupFlow composition = CreateComposition(
                Account(Character(0, "all")),
                out factory,
                out ignored);

            CharacterSetupResult result = composition.Select(0);

            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            FakeGraph graph = (FakeGraph)composition.ActiveRuntime;
            foreach (SavePartDefinition definition in Definitions())
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
            PlayerAccountSaveState ignored;
            CharacterSetupFlow composition = CreateComposition(
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
            CharacterInstanceSnapshot valid = Character(0, "valid");
            CharacterInstanceSnapshot corrupt = ReplaceComponent(
                Character(1, "corrupt"),
                GameSaveParts.PlayerExperience(),
                "corrupt-payload");
            FakeGraphFactory factory;
            PlayerAccountSaveState authority;
            CharacterSetupFlow composition = CreateComposition(
                Account(valid, corrupt),
                out factory,
                out authority);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            FakeGraph previous = (FakeGraph)composition.ActiveRuntime;
            string validFingerprint = authority.Current.CharacterAt(0).Fingerprint;
            string corruptFingerprint = authority.Current.CharacterAt(1).Fingerprint;

            CharacterSetupResult result = composition.Select(1);

            Assert.That(
                result.Status,
                Is.EqualTo(CharacterSetupStatus.Rejected));
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
            var authority = new PlayerAccountSaveState(
                Account(Character(0, "rollback")));
            PlayerAccountSnapshot before = authority.Current;
            var composition = new CharacterSetupFlow(
                authority,
                new FakeGraphFactory(),
                snapshot => new PlayerAccountStoreResult(
                    PlayerAccountStoreStatus.IoFailure,
                    "simulated-write-failure",
                    null),
                snapshot => SavePartValidationResult.Accept());
            Assert.That(composition.Select(0).Succeeded, Is.True);
            ((FakeGraph)composition.ActiveRuntime)
                .State(GameSaveParts.ScrapWallet()).Value =
                    "unsaved-scrap";

            CharacterSetupResult result = composition.PersistActive(
                Id("operation.failed-save"));

            Assert.That(
                result.Status,
                Is.EqualTo(CharacterSetupStatus.Rejected));
            Assert.That(authority.Current.Fingerprint, Is.EqualTo(before.Fingerprint));
        }

        [Test]
        public void LegacyMigrationRunsOncePreservesClassAndAvoidsDuplicates()
        {
            StableId accountId = Id("account.migration");
            var authority = new PlayerAccountSaveState(
                PlayerAccountSnapshot.Empty(accountId));
            var factory = new FakeGraphFactory();
            var migration = new SaveMigration(
                authority,
                factory,
                Saved);
            LegacyCharacterProfile[] profiles =
            {
                Legacy(0, "Pilot A", "frontier", "aggressive"),
                Legacy(4, "Pilot B", "custom", "healer"),
            };

            SaveMigrationResult first =
                migration.Migrate(profiles);
            PlayerAccountSnapshot afterFirst = authority.Current;
            SaveMigrationResult second =
                migration.Migrate(profiles);

            Assert.That(
                first.Status,
                Is.EqualTo(CharacterSetupStatus.Migrated));
            Assert.That(
                second.Status,
                Is.EqualTo(CharacterSetupStatus.ExactNoChange));
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
                Is.EqualTo(SaveMigration.ExactCharacterId(
                    accountId,
                    profiles[0])));
            Assert.That(factory.Created.All(item => item.IsDisposed), Is.True);
        }

        [Test]
        public void ProductionFactoryRoundTripsRealAuthoritiesAndExactBindings()
        {
            var factory = CharacterLiveGraphFactory
                .CreateVerticalSliceDefaults();
            StableId characterId = Id("character-instance.real-roundtrip");
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayload draftRoute =
                PlayerRouteProfilePayload.Create(
                    Id("character.frontier"),
                    classId,
                    new StableId[
                        PlayerRouteProfilePayload.GunSlotCount]);
            ICharacterLiveGraph starter = factory.CreateStarter(
                2,
                characterId,
                classId,
                "Real Pilot",
                draftRoute);
            IReadOnlyList<SavePartSnapshot> components =
                PlayerAccountRestoreFlow.ExportComponents(
                    starter.SaveAdapters);
            starter.Dispose();
            var character = new CharacterInstanceSnapshot(
                characterId,
                classId,
                2,
                "Real Pilot",
                0L,
                components);
            var composition = new CharacterSetupFlow(
                new PlayerAccountSaveState(Account(character)),
                factory,
                Saved);

            CharacterSetupResult selected = composition.Select(2);

            Assert.That(selected.Succeeded, Is.True, selected.Diagnostic);
            var graph = (CharacterLiveGraph)
                composition.ActiveRuntime;
            InventoryLoadoutStateSnapshot loadout =
                graph.LoadoutRuntime.LoadoutAuthority.ExportSnapshot();
            var equipped = loadout.Bindings
                .Where(item => item.EquipmentInstanceStableId != null)
                .Select(item => item.EquipmentInstanceStableId)
                .ToArray();
            var owned = graph.LoadoutRuntime.Holdings.ExportSnapshot()
                .UniqueHoldings.Select(item => item.InstanceStableId)
                .ToArray();

            Assert.That(equipped.Length, Is.EqualTo(4));
            Assert.That(equipped.Distinct().Count(), Is.EqualTo(4));
            Assert.That(owned.Length, Is.EqualTo(4));
            Assert.That(equipped.All(owned.Contains), Is.True);
        }

        private static CharacterSetupFlow CreateComposition(
            PlayerAccountSnapshot account,
            out FakeGraphFactory factory,
            out PlayerAccountSaveState authority)
        {
            factory = new FakeGraphFactory();
            authority = new PlayerAccountSaveState(account);
            return new CharacterSetupFlow(
                authority,
                factory,
                Saved,
                snapshot => SavePartValidationResult.Accept());
        }

        private static PlayerAccountStoreResult Saved(
            PlayerAccountSnapshot snapshot)
        {
            return new PlayerAccountStoreResult(
                PlayerAccountStoreStatus.Saved,
                string.Empty,
                snapshot);
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
                Id("account.character-composition"),
                0L,
                slots,
                null);
        }

        private static CharacterInstanceSnapshot Character(
            int slotIndex,
            string prefix)
        {
            Dictionary<StableId, MutableState> states = Definitions()
                .ToDictionary(
                    definition => definition.ComponentStableId,
                    definition => new MutableState(
                        prefix + "-" + Suffix(definition)));
            FakeGraph graph = FakeGraph.Create(
                new CharacterInstanceSnapshot(
                    Id("character-instance." + prefix),
                    Id("loadout-profile." + prefix),
                    slotIndex,
                    prefix,
                    0L,
                    null),
                states);
            IReadOnlyList<SavePartSnapshot> components =
                PlayerAccountRestoreFlow.ExportComponents(
                    graph.SaveAdapters);
            graph.Dispose();
            return new CharacterInstanceSnapshot(
                Id("character-instance." + prefix),
                Id("loadout-profile." + prefix),
                slotIndex,
                prefix,
                0L,
                components);
        }

        private static CharacterInstanceSnapshot ReplaceComponent(
            CharacterInstanceSnapshot character,
            SavePartDefinition definition,
            string payload)
        {
            Dictionary<StableId, SavePartSnapshot> components =
                character.Components.Values.ToDictionary(
                    item => item.ComponentStableId,
                    item => item);
            components[definition.ComponentStableId] =
                new SavePartSnapshot(
                    definition.ComponentStableId,
                    definition.SchemaVersion,
                    definition.ContentVersion,
                    payload);
            return new CharacterInstanceSnapshot(
                character.CharacterInstanceStableId,
                character.ClassDefinitionStableId,
                character.SlotIndex,
                character.DisplayName,
                character.Revision,
                components.Values);
        }

        private static LegacyCharacterProfile Legacy(
            int slotIndex,
            string displayName,
            string characterName,
            string className)
        {
            return new LegacyCharacterProfile(
                slotIndex,
                displayName,
                Id("character." + characterName),
                Id("loadout-profile." + className),
                "legacy-fingerprint-" + slotIndex,
                "starter-" + slotIndex);
        }

        private static IReadOnlyList<SavePartDefinition> Definitions()
        {
            return new[]
            {
                GameSaveParts.PlayerExperience(),
                GameSaveParts.PlayerHoldings(),
                GameSaveParts.MoneyWallet(),
                GameSaveParts.ScrapWallet(),
                GameSaveParts.RankedSkillAllocation(),
                GameSaveParts.ExactInstanceLoadout(),
                GameSaveParts.StrongboxState(),
            };
        }

        private static string Suffix(SavePartDefinition definition)
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
            ICharacterLiveGraphFactory,
            IStarterCharacterLiveGraphFactory
        {
            public List<FakeGraph> Created { get; } = new List<FakeGraph>();

            public Action BeforeCreate { get; set; }

            public ICharacterLiveGraph CreateRestoreTarget(
                CharacterInstanceSnapshot character)
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

            public ICharacterLiveGraph CreateStarter(
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
                    new CharacterInstanceSnapshot(
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

        private sealed class FakeGraph : ICharacterLiveGraph
        {
            private readonly Dictionary<StableId, MutableState> states;

            private FakeGraph(
                CharacterInstanceSnapshot character,
                Dictionary<StableId, MutableState> states,
                IReadOnlyList<ISavePart> adapters)
            {
                Character = character;
                this.states = states;
                SaveAdapters = adapters;
            }

            public CharacterInstanceSnapshot Character { get; private set; }

            public IReadOnlyList<ISavePart> SaveAdapters { get; }

            public bool IsDisposed { get; private set; }

            public static FakeGraph Create(
                CharacterInstanceSnapshot character,
                Dictionary<StableId, MutableState> states)
            {
                var adapters = new List<ISavePart>();
                foreach (SavePartDefinition definition in Definitions())
                {
                    MutableState state = states[definition.ComponentStableId];
                    var codec = new TestCodec();
                    adapters.Add(
                        new SnapshotSavePart<TestSnapshot>(
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
                                    return SavePartApplyResult.Rejected(
                                        "test-component-corrupt");
                                }
                                state.Value = snapshot.Value;
                                return SavePartApplyResult.Applied();
                            }));
                }
                return new FakeGraph(character, states, adapters);
            }

            public MutableState State(SavePartDefinition definition)
            {
                return states[definition.ComponentStableId];
            }

            public void MarkPersisted(CharacterInstanceSnapshot character)
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
            ISavePartFormat<TestSnapshot>
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

            public SavePartValidationResult Validate(
                TestSnapshot snapshot)
            {
                return snapshot == null || snapshot.Value == null
                    ? SavePartValidationResult.Reject(
                        "test-component-null")
                    : SavePartValidationResult.Accept();
            }
        }
    }
}

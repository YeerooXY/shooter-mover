using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CharacterCompositionCoordinatorV1Tests
    {
        [Test]
        public void TwoCharactersMutateDifferentlyAndRestartWithoutCrossSlotLeakage()
        {
            TestGraphFactory factory;
            PlayerAccountSaveAuthorityV1 accountAuthority;
            CharacterCompositionCoordinatorV1 composition = Composition(
                Account(
                    Character(0, "alpha"),
                    Character(1, "bravo")),
                out factory,
                out accountAuthority);

            Assert.That(composition.Select(0).Succeeded, Is.True);
            TestGraph alpha = (TestGraph)composition.ActiveRuntime;
            alpha.State(KnownSaveComponentDefinitionsV1.MoneyWallet())
                .Value = "alpha-money-mutated";
            Assert.That(composition.PersistActive(
                Id("operation.save-alpha")).Status,
                Is.EqualTo(CharacterCompositionStatusV1.Persisted));

            Assert.That(composition.Select(1).Succeeded, Is.True);
            Assert.That(alpha.IsDisposed, Is.True);
            TestGraph bravo = (TestGraph)composition.ActiveRuntime;
            Assert.That(bravo.State(
                KnownSaveComponentDefinitionsV1.MoneyWallet()).Value,
                Is.EqualTo("bravo-money"));
            bravo.State(KnownSaveComponentDefinitionsV1.PlayerExperience())
                .Value = "bravo-xp-mutated";
            Assert.That(composition.PersistActive(
                Id("operation.save-bravo")).Succeeded, Is.True);

            composition.Dispose();
            var restartedAuthority = new PlayerAccountSaveAuthorityV1(
                accountAuthority.Current);
            var restarted = new CharacterCompositionCoordinatorV1(
                restartedAuthority,
                factory,
                snapshot => Saved(snapshot),
                snapshot => SaveComponentValidationResultV1.Accept());

            Assert.That(restarted.Select(0).Succeeded, Is.True);
            TestGraph restoredAlpha = (TestGraph)restarted.ActiveRuntime;
            Assert.That(restoredAlpha.State(
                KnownSaveComponentDefinitionsV1.MoneyWallet()).Value,
                Is.EqualTo("alpha-money-mutated"));
            Assert.That(restoredAlpha.State(
                KnownSaveComponentDefinitionsV1.PlayerExperience()).Value,
                Is.EqualTo("alpha-xp"));

            Assert.That(restarted.Select(1).Succeeded, Is.True);
            TestGraph restoredBravo = (TestGraph)restarted.ActiveRuntime;
            Assert.That(restoredBravo.State(
                KnownSaveComponentDefinitionsV1.MoneyWallet()).Value,
                Is.EqualTo("bravo-money"));
            Assert.That(restoredBravo.State(
                KnownSaveComponentDefinitionsV1.PlayerExperience()).Value,
                Is.EqualTo("bravo-xp-mutated"));
        }

        [Test]
        public void ReloadRestoresEveryKnownCharacterComponentIncludingOptionalBoxes()
        {
            CharacterInstanceSnapshotV1 character = Character(0, "all");
            TestGraphFactory factory;
            PlayerAccountSaveAuthorityV1 ignored;
            CharacterCompositionCoordinatorV1 composition = Composition(
                Account(character),
                out factory,
                out ignored);

            CharacterCompositionResultV1 result = composition.Select(0);

            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            TestGraph graph = (TestGraph)composition.ActiveRuntime;
            foreach (SaveComponentDefinitionV1 definition in Definitions())
            {
                Assert.That(
                    graph.State(definition).Value,
                    Is.EqualTo("all-" + Suffix(definition)),
                    definition.ComponentStableId.ToString());
            }
        }

        [Test]
        public void SwitchingDisposesOldGraphBeforePublishingRestoredGraph()
        {
            TestGraphFactory factory;
            PlayerAccountSaveAuthorityV1 ignored;
            CharacterCompositionCoordinatorV1 composition = Composition(
                Account(Character(0, "first"), Character(1, "second")),
                out factory,
                out ignored);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            TestGraph first = (TestGraph)composition.ActiveRuntime;

            Assert.That(composition.Select(1).Succeeded, Is.True);

            Assert.That(first.IsDisposed, Is.True);
            Assert.That(composition.ActiveSlotIndex, Is.EqualTo(1));
            Assert.That(composition.ActiveRuntime, Is.Not.SameAs(first));
            Assert.That(factory.Created.Count, Is.EqualTo(2));
        }

        [Test]
        public void CorruptSelectedCharacterFailsSafelyAndLeavesOtherSlotsAndActiveGraphUntouched()
        {
            CharacterInstanceSnapshotV1 valid = Character(0, "valid");
            CharacterInstanceSnapshotV1 corrupt = ReplaceComponent(
                Character(1, "corrupt"),
                KnownSaveComponentDefinitionsV1.PlayerExperience(),
                "corrupt-payload");
            PlayerAccountSnapshotV1 account = Account(valid, corrupt);
            TestGraphFactory factory;
            PlayerAccountSaveAuthorityV1 authority;
            CharacterCompositionCoordinatorV1 composition = Composition(
                account,
                out factory,
                out authority);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            TestGraph active = (TestGraph)composition.ActiveRuntime;
            string otherFingerprint = authority.Current.CharacterAt(0).Fingerprint;

            CharacterCompositionResultV1 rejected = composition.Select(1);

            Assert.That(rejected.Status,
                Is.EqualTo(CharacterCompositionStatusV1.Rejected));
            Assert.That(rejected.Diagnostic,
                Does.Contain("test-component-corrupt"));
            Assert.That(composition.ActiveRuntime, Is.SameAs(active));
            Assert.That(active.IsDisposed, Is.False);
            Assert.That(authority.Current.CharacterAt(0).Fingerprint,
                Is.EqualTo(otherFingerprint));
            Assert.That(authority.Current.CharacterAt(1).Fingerprint,
                Is.EqualTo(corrupt.Fingerprint));
        }

        [Test]
        public void FailedDurableSaveRollsAccountAggregateBackToLastValidSnapshot()
        {
            TestGraphFactory factory;
            var accountAuthority = new PlayerAccountSaveAuthorityV1(
                Account(Character(0, "rollback")));
            PlayerAccountSnapshotV1 before = accountAuthority.Current;
            var composition = new CharacterCompositionCoordinatorV1(
                accountAuthority,
                factory = new TestGraphFactory(),
                snapshot => new PlayerAccountStoreResultV1(
                    PlayerAccountStoreStatusV1.IoFailure,
                    "simulated-write-failure",
                    null),
                snapshot => SaveComponentValidationResultV1.Accept());
            Assert.That(composition.Select(0).Succeeded, Is.True);
            ((TestGraph)composition.ActiveRuntime).State(
                KnownSaveComponentDefinitionsV1.ScrapWallet()).Value =
                    "unsaved-scrap";

            CharacterCompositionResultV1 result = composition.PersistActive(
                Id("operation.failed-save"));

            Assert.That(result.Status,
                Is.EqualTo(CharacterCompositionStatusV1.Rejected));
            Assert.That(accountAuthority.Current.Fingerprint,
                Is.EqualTo(before.Fingerprint));
        }

        [Test]
        public void LegacyMigrationRunsOncePreservesClassAndCreatesNoDuplicateStarterComponents()
        {
            StableId accountId = Id("account.migration");
            var authority = new PlayerAccountSaveAuthorityV1(
                PlayerAccountSnapshotV1.Empty(accountId));
            var factory = new TestGraphFactory();
            var migration = new LegacyCharacterProfileMigrationV1(
                authority,
                factory,
                snapshot => Saved(snapshot));
            var profiles = new[]
            {
                Legacy(0, "Pilot A", "frontier", "aggressive"),
                Legacy(4, "Pilot B", "custom", "healer"),
            };

            LegacyCharacterProfileMigrationResultV1 first =
                migration.Migrate(profiles);
            PlayerAccountSnapshotV1 afterFirst = authority.Current;
            LegacyCharacterProfileMigrationResultV1 second =
                migration.Migrate(profiles);

            Assert.That(first.Status,
                Is.EqualTo(CharacterCompositionStatusV1.Migrated));
            Assert.That(second.Status,
                Is.EqualTo(CharacterCompositionStatusV1.ExactNoChange));
            Assert.That(authority.Current.Fingerprint,
                Is.EqualTo(afterFirst.Fingerprint));
            Assert.That(afterFirst.CharacterAt(0).ClassDefinitionStableId,
                Is.EqualTo(Id("loadout-profile.aggressive")));
            Assert.That(afterFirst.CharacterAt(4).ClassDefinitionStableId,
                Is.EqualTo(Id("loadout-profile.healer")));
            Assert.That(afterFirst.CharacterAt(0).Components.Count,
                Is.EqualTo(Definitions().Count));
            Assert.That(afterFirst.CharacterAt(4).Components.Count,
                Is.EqualTo(Definitions().Count));
            Assert.That(
                afterFirst.CharacterAt(0).CharacterInstanceStableId,
                Is.EqualTo(LegacyCharacterProfileMigrationV1.ExactCharacterId(
                    accountId,
                    profiles[0])));
            Assert.That(factory.Created.All(item => item.IsDisposed), Is.True);
        }

        private static CharacterCompositionCoordinatorV1 Composition(
            PlayerAccountSnapshotV1 account,
            out TestGraphFactory factory,
            out PlayerAccountSaveAuthorityV1 authority)
        {
            factory = new TestGraphFactory();
            authority = new PlayerAccountSaveAuthorityV1(account);
            return new CharacterCompositionCoordinatorV1(
                authority,
                factory,
                snapshot => Saved(snapshot),
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
            for (int index = 0; index < characters.Length; index++)
            {
                CharacterInstanceSnapshotV1 character = characters[index];
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
            var states = Definitions().ToDictionary(
                definition => definition.ComponentStableId,
                definition => new MutableState(
                    prefix + "-" + Suffix(definition)));
            TestGraph graph = TestGraph.Create(
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
            var components = character.Components.Values.ToDictionary(
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
            int slot,
            string name,
            string character,
            string className)
        {
            return new LegacyCharacterProfileV1(
                slot,
                name,
                Id("character." + character),
                Id("loadout-profile." + className),
                "legacy-fingerprint-" + slot,
                "starter-" + slot);
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
            string text = definition.ComponentStableId.ToString();
            int separator = text.IndexOf('.');
            return separator < 0 ? text : text.Substring(separator + 1);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class TestGraphFactory :
            ICharacterRuntimeGraphFactoryV1,
            IStarterCharacterRuntimeGraphFactoryV1
        {
            public List<TestGraph> Created { get; } = new List<TestGraph>();

            public ICharacterRuntimeGraphV1 CreateRestoreTarget(
                CharacterInstanceSnapshotV1 character)
            {
                var states = Definitions().ToDictionary(
                    definition => definition.ComponentStableId,
                    definition => new MutableState("empty"));
                TestGraph graph = TestGraph.Create(character, states);
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
                var states = Definitions().ToDictionary(
                    definition => definition.ComponentStableId,
                    definition => new MutableState(
                        prefix + "-" + Suffix(definition)));
                TestGraph graph = TestGraph.Create(
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

        private sealed class TestGraph : ICharacterRuntimeGraphV1
        {
            private readonly Dictionary<StableId, MutableState> states;

            private TestGraph(
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

            public static TestGraph Create(
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
                return new TestGraph(character, states, adapters);
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

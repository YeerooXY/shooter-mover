using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CharacterCreationDiagnosticRegressionTests
    {
        [Test]
        public void SingleProfileMigrationReportsRootStarterFailureWithoutPartialCharacter()
        {
            var authority = new PlayerAccountSaveState(
                PlayerAccountSnapshot.Empty(
                    Id("account.character-creation-diagnostic-regression")));
            var factory = new ThrowingStarterFactory(0);
            int saveCalls = 0;
            var composition = new CharacterSetupFlow(
                authority,
                factory,
                snapshot =>
                {
                    saveCalls++;
                    return Saved(snapshot);
                });

            try
            {
                SaveMigrationResult result =
                    new SaveMigration(
                        authority,
                        factory,
                        snapshot =>
                        {
                            saveCalls++;
                            return Saved(snapshot);
                        }).Migrate(new[]
                        {
                            LegacyProfile(0),
                        });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(
                    result.Diagnostic,
                    Does.Contain(
                        "character-create-transaction-rejected:"
                        + "character-create-threw:"
                        + "TypeInitializationException"
                        + "->InvalidOperationException:"
                        + "starter graph catalogue missing"));
                Assert.That(authority.Current.CharacterAt(0), Is.Null);
                Assert.That(composition.ActiveRuntime, Is.Null);
                Assert.That(composition.ActiveSlotIndex, Is.EqualTo(-1));
                Assert.That(saveCalls, Is.Zero);
            }
            finally
            {
                composition.Dispose();
            }
        }

        [Test]
        public void BatchMigrationReportsRootStarterFailureAndRollsBackEarlierSlot()
        {
            var authority = new PlayerAccountSaveState(
                PlayerAccountSnapshot.Empty(
                    Id("account.batch-migration-diagnostic-regression")));
            var factory = new ThrowingStarterFactory(1);
            int saveCalls = 0;
            var migration = new SaveMigration(
                authority,
                factory,
                snapshot =>
                {
                    saveCalls++;
                    return Saved(snapshot);
                });

            SaveMigrationResult result = migration.Migrate(
                new[]
                {
                    LegacyProfile(0),
                    LegacyProfile(1),
                });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.Diagnostic,
                Does.Contain(
                    "legacy-profile-migration-threw:"
                    + "TypeInitializationException"
                    + "->InvalidOperationException:"
                    + "starter graph catalogue missing"));
            Assert.That(result.MigratedSlots, Is.Empty);
            Assert.That(authority.Current.CharacterAt(0), Is.Null);
            Assert.That(authority.Current.CharacterAt(1), Is.Null);
            Assert.That(saveCalls, Is.Zero);
            Assert.That(factory.Created.Count, Is.EqualTo(1));
            Assert.That(factory.Created[0].IsDisposed, Is.True);
        }

        private static LegacyCharacterProfile LegacyProfile(int slotIndex)
        {
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayload route =
                PlayerRouteProfilePayload.Create(
                    Id(
                        "character.creation-diagnostic-regression-"
                            + slotIndex),
                    classId,
                    new StableId[
                        PlayerRouteProfilePayload.GunSlotCount]);
            return new LegacyCharacterProfile(
                slotIndex,
                "Diagnostic Pilot " + slotIndex,
                route.SelectedCharacterStableId,
                classId,
                route.Fingerprint,
                route);
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
                GunInventorySavePart.Definition(),
                LoadoutSavePart.Definition(),
                GameSaveParts.StrongboxState(),
            };
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

        private sealed class ThrowingStarterFactory :
            ICharacterLiveGraphFactory,
            IStarterCharacterLiveGraphFactory
        {
            private readonly int throwOnSlotIndex;

            public ThrowingStarterFactory(int throwOnSlotIndex)
            {
                this.throwOnSlotIndex = throwOnSlotIndex;
            }

            public List<TestGraph> Created { get; } = new List<TestGraph>();

            public ICharacterLiveGraph CreateRestoreTarget(
                CharacterInstanceSnapshot character)
            {
                throw new InvalidOperationException(
                    "Restore target creation was not expected.");
            }

            public ICharacterLiveGraph CreateStarter(
                int slotIndex,
                StableId exactCharacterInstanceStableId,
                StableId classDefinitionStableId,
                string displayName,
                object legacyContext)
            {
                if (slotIndex == throwOnSlotIndex)
                {
                    throw new TypeInitializationException(
                        "StarterGraphCatalog",
                        new InvalidOperationException(
                            "starter graph catalogue missing"));
                }

                TestGraph graph = TestGraph.Create(
                    new CharacterInstanceSnapshot(
                        exactCharacterInstanceStableId,
                        classDefinitionStableId,
                        slotIndex,
                        displayName,
                        0L,
                        null));
                Created.Add(graph);
                return graph;
            }
        }

        private sealed class TestGraph : ICharacterLiveGraph
        {
            private TestGraph(
                CharacterInstanceSnapshot character,
                IReadOnlyList<ISavePart> saveAdapters)
            {
                Character = character;
                SaveAdapters = saveAdapters;
            }

            public CharacterInstanceSnapshot Character { get; private set; }

            public IReadOnlyList<ISavePart> SaveAdapters { get; }

            public bool IsDisposed { get; private set; }

            public static TestGraph Create(CharacterInstanceSnapshot character)
            {
                var adapters = new List<ISavePart>();
                foreach (SavePartDefinition definition in Definitions())
                {
                    var state = new MutableState(
                        "diagnostic-" + definition.ComponentStableId);
                    var codec = new TestCodec();
                    adapters.Add(
                        new SnapshotSavePart<TestSnapshot>(
                            definition,
                            codec,
                            () => new TestSnapshot(state.Value),
                            snapshot => codec.Validate(snapshot),
                            snapshot =>
                            {
                                state.Value = snapshot.Value;
                                return SavePartApplyResult.Applied();
                            }));
                }
                return new TestGraph(character, adapters);
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
                get { return "character-creation-diagnostic-test-v1"; }
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
                snapshot = new TestSnapshot(canonicalPayload);
                rejectionCode = string.Empty;
                return true;
            }

            public SavePartValidationResult Validate(
                TestSnapshot snapshot)
            {
                return snapshot == null || snapshot.Value == null
                    ? SavePartValidationResult.Reject(
                        "character-creation-diagnostic-test-snapshot-null")
                    : SavePartValidationResult.Accept();
            }
        }
    }
}

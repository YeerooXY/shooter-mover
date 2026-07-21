using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Components
{
    public sealed class AccountCompatibilityV1Tests
    {
        [Test]
        public void SixSlotsAndUnknownOpaqueComponentsRoundTripWithoutCrossContamination()
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = new CharacterInstanceSnapshotV1(
                    Id("character.compatibility-slot-" + index),
                    Id("class.compatibility-" + index),
                    index,
                    "Compatibility " + index,
                    index,
                    new[]
                    {
                        new SaveComponentSnapshotV1(
                            Id("future.opaque-slot-" + index),
                            17,
                            "future-content-v17",
                            "opaque-slot-payload-" + index),
                    });
            }
            var source = new PlayerAccountSnapshotV1(
                Id("account.compatibility-six-slots"),
                5L,
                slots,
                new[]
                {
                    new SaveComponentSnapshotV1(
                        Id("future.account-opaque"),
                        3,
                        "future-account-v3",
                        "opaque-account-payload"),
                });
            var files = new MemoryAtomicFilePort();
            AtomicPlayerAccountStoreV1 store = CreateDefaultStore(files);

            Assert.That(store.Save(source).Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.Saved));
            PlayerAccountStoreResultV1 loaded = store.Load();

            Assert.That(loaded.Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.Loaded));
            Assert.That(loaded.Snapshot.Fingerprint,
                Is.EqualTo(source.Fingerprint));
            for (int index = 0; index < slots.Length; index++)
            {
                CharacterInstanceSnapshotV1 character =
                    loaded.Snapshot.CharacterAt(index);
                Assert.That(character.CharacterInstanceStableId,
                    Is.EqualTo(Id("character.compatibility-slot-" + index)));
                SaveComponentSnapshotV1 component;
                Assert.That(character.TryGetComponent(
                    Id("future.opaque-slot-" + index),
                    out component), Is.True);
                Assert.That(component.CanonicalPayload,
                    Is.EqualTo("opaque-slot-payload-" + index));
                for (int other = 0; other < slots.Length; other++)
                {
                    if (other == index) continue;
                    Assert.That(character.Components.ContainsKey(
                        Id("future.opaque-slot-" + other)), Is.False);
                }
            }
            SaveComponentSnapshotV1 accountComponent;
            Assert.That(loaded.Snapshot.TryGetAccountComponent(
                Id("future.account-opaque"),
                out accountComponent), Is.True);
            Assert.That(accountComponent.CanonicalPayload,
                Is.EqualTo("opaque-account-payload"));
        }

        [Test]
        public void DefaultStoreRejectsUnsupportedKnownSchemaAndLeavesActiveAndBackupUnchanged()
        {
            var files = new MemoryAtomicFilePort();
            AtomicPlayerAccountStoreV1 store = CreateDefaultStore(files);
            SeedActiveAndBackup(store);
            string previousActive = files.ReadAllText("account.active");
            string previousBackup = files.ReadAllText("account.backup");

            SaveComponentDefinitionV1 xp =
                KnownSaveComponentDefinitionsV1.PlayerExperience();
            PlayerAccountSnapshotV1 unsupported = AccountWithCharacterComponent(
                new SaveComponentSnapshotV1(
                    xp.ComponentStableId,
                    xp.SchemaVersion + 1,
                    xp.ContentVersion,
                    "unsupported-known-schema"),
                "schema-unsupported",
                2L);

            PlayerAccountStoreResultV1 rejected = store.Save(unsupported);

            Assert.That(rejected.Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.ValidationRejected));
            Assert.That(rejected.RejectionCode,
                Does.StartWith("known-save-component-version-unsupported"));
            Assert.That(files.ReadAllText("account.active"),
                Is.EqualTo(previousActive));
            Assert.That(files.ReadAllText("account.backup"),
                Is.EqualTo(previousBackup));
            Assert.That(files.Exists("account.temp"), Is.False);
        }

        [Test]
        public void DefaultStoreRejectsUnsupportedKnownContentVersionAndLeavesActiveAndBackupUnchanged()
        {
            var files = new MemoryAtomicFilePort();
            AtomicPlayerAccountStoreV1 store = CreateDefaultStore(files);
            SeedActiveAndBackup(store);
            string previousActive = files.ReadAllText("account.active");
            string previousBackup = files.ReadAllText("account.backup");

            SaveComponentDefinitionV1 xp =
                KnownSaveComponentDefinitionsV1.PlayerExperience();
            PlayerAccountSnapshotV1 unsupported = AccountWithCharacterComponent(
                new SaveComponentSnapshotV1(
                    xp.ComponentStableId,
                    xp.SchemaVersion,
                    xp.ContentVersion + ".unsupported",
                    "unsupported-known-content"),
                "content-unsupported",
                2L);

            PlayerAccountStoreResultV1 rejected = store.Save(unsupported);

            Assert.That(rejected.Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.ValidationRejected));
            Assert.That(rejected.RejectionCode,
                Does.StartWith("known-save-component-version-unsupported"));
            Assert.That(files.ReadAllText("account.active"),
                Is.EqualTo(previousActive));
            Assert.That(files.ReadAllText("account.backup"),
                Is.EqualTo(previousBackup));
            Assert.That(files.Exists("account.temp"), Is.False);
        }

        [Test]
        public void CustomSemanticValidatorCannotBypassMandatoryKnownVersionGuard()
        {
            var files = new MemoryAtomicFilePort();
            int semanticValidationCalls = 0;
            var store = new AtomicPlayerAccountStoreV1(
                files,
                "account.active",
                "account.temp",
                "account.backup",
                account =>
                {
                    semanticValidationCalls++;
                    return SaveComponentValidationResultV1.Accept();
                });
            PlayerAccountSnapshotV1 baseline = UnknownAccount(
                "custom-validator-baseline",
                0L);
            Assert.That(store.Save(baseline).Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.Saved));
            string previousActive = files.ReadAllText("account.active");

            SaveComponentDefinitionV1 holdings =
                KnownSaveComponentDefinitionsV1.PlayerHoldings();
            PlayerAccountSnapshotV1 unsupported = AccountWithCharacterComponent(
                new SaveComponentSnapshotV1(
                    holdings.ComponentStableId,
                    holdings.SchemaVersion + 1,
                    holdings.ContentVersion,
                    "custom-validator-must-not-bypass"),
                "custom-validator-unsupported",
                1L);
            PlayerAccountStoreResultV1 rejected = store.Save(unsupported);

            Assert.That(semanticValidationCalls, Is.GreaterThan(0));
            Assert.That(rejected.Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.ValidationRejected));
            Assert.That(rejected.RejectionCode,
                Does.StartWith("known-save-component-version-unsupported"));
            Assert.That(files.ReadAllText("account.active"),
                Is.EqualTo(previousActive));
            Assert.That(files.Exists("account.temp"), Is.False);
        }

        [Test]
        public void CoordinatorRejectsUnsupportedKnownOptionalWithoutAdapter()
        {
            SaveComponentDefinitionV1 strongbox =
                KnownSaveComponentDefinitionsV1.StrongboxState(required: false);
            StableId characterId = Id("character.optional-known-version");
            PlayerAccountSnapshotV1 account = AccountWithCharacterComponent(
                new SaveComponentSnapshotV1(
                    strongbox.ComponentStableId,
                    strongbox.SchemaVersion,
                    strongbox.ContentVersion + ".unsupported",
                    "optional-known-version"),
                "optional-known-version",
                0L,
                characterId);
            var coordinator = new PlayerAccountRestoreCoordinatorV1();
            var binding = new CharacterSaveRestoreBindingV1(
                0,
                characterId,
                Array.Empty<ISaveComponentAdapterV1>());

            PlayerAccountRestoreResultV1 result = coordinator.Restore(
                account,
                new[] { binding });

            Assert.That(result.Status,
                Is.EqualTo(PlayerAccountRestoreStatusV1.ValidationRejected));
            Assert.That(result.RejectionCode,
                Does.StartWith("known-save-component-version-unsupported"));
            Assert.That(result.RetainedUnknownComponents, Is.Empty);
        }

        [Test]
        public void GenuinelyUnknownFutureComponentRemainsOpaqueThroughStoreAndRestore()
        {
            StableId characterId = Id("character.unknown-future");
            SaveComponentSnapshotV1 future = new SaveComponentSnapshotV1(
                Id("future.component-v42"),
                42,
                "future-content-v42",
                "opaque-future-payload");
            PlayerAccountSnapshotV1 account = AccountWithCharacterComponent(
                future,
                "unknown-future",
                0L,
                characterId);
            var files = new MemoryAtomicFilePort();
            AtomicPlayerAccountStoreV1 store = CreateDefaultStore(files);

            Assert.That(store.Save(account).Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.Saved));
            PlayerAccountSnapshotV1 loaded = store.Load().Snapshot;
            var coordinator = new PlayerAccountRestoreCoordinatorV1();
            var binding = new CharacterSaveRestoreBindingV1(
                0,
                characterId,
                Array.Empty<ISaveComponentAdapterV1>());
            PlayerAccountRestoreResultV1 restored = coordinator.Restore(
                loaded,
                new[] { binding });

            Assert.That(restored.Status,
                Is.EqualTo(PlayerAccountRestoreStatusV1.Restored));
            Assert.That(restored.RetainedUnknownComponents.Count,
                Is.EqualTo(1));
            SaveComponentSnapshotV1 retained =
                restored.RetainedUnknownComponents[0].Component;
            Assert.That(retained.ComponentStableId,
                Is.EqualTo(future.ComponentStableId));
            Assert.That(retained.SchemaVersion,
                Is.EqualTo(future.SchemaVersion));
            Assert.That(retained.ContentVersion,
                Is.EqualTo(future.ContentVersion));
            Assert.That(retained.CanonicalPayload,
                Is.EqualTo(future.CanonicalPayload));
            Assert.That(retained.Fingerprint,
                Is.EqualTo(future.Fingerprint));
        }

        [Test]
        public void DirectCodecRegistryIsAotVisibleAndUnique()
        {
            var contracts = new HashSet<string>(StringComparer.Ordinal)
            {
                KnownSaveComponentCodecsV1.PlayerExperience.ContractId,
                KnownSaveComponentCodecsV1.PlayerHoldings.ContractId,
                KnownSaveComponentCodecsV1.MoneyWallet.ContractId,
                KnownSaveComponentCodecsV1.ScrapWallet.ContractId,
                KnownSaveComponentCodecsV1.RankedSkillAllocation.ContractId,
                KnownSaveComponentCodecsV1.ExactInstanceLoadout.ContractId,
                KnownSaveComponentCodecsV1.StrongboxState.ContractId,
            };

            Assert.That(contracts.Count, Is.EqualTo(7));
            Assert.That(contracts, Does.Contain("player-experience-explicit-v1"));
            Assert.That(contracts, Does.Contain("player-holdings-explicit-v1"));
            Assert.That(contracts, Does.Contain("money-wallet-explicit-v1"));
            Assert.That(contracts, Does.Contain("scrap-wallet-explicit-v1"));
            Assert.That(contracts,
                Does.Contain("ranked-skill-allocation-explicit-v2"));
            Assert.That(contracts,
                Does.Contain("inventory-loadout-explicit-v1"));
            Assert.That(contracts,
                Does.Contain("strongbox-opening-explicit-v1"));
        }

        [Test]
        public void StatisticsComponentIsNotRegisteredUnderGenericContract()
        {
            Assert.That(
                Id("save-component.character-statistics"),
                Is.Not.EqualTo(
                    KnownSaveComponentDefinitionsV1.PlayerExperience()
                        .ComponentStableId));
            Assert.That(
                KnownSaveComponentVersionGuardV1.ValidateComponent(
                    new SaveComponentSnapshotV1(
                        Id("save-component.character-statistics"),
                        1,
                        "unregistered-statistics-contract",
                        "opaque"))
                    .Succeeded,
                Is.True,
                "Unregistered future statistics remain opaque; no arbitrary typed adapter is exposed.");
        }

        private static AtomicPlayerAccountStoreV1 CreateDefaultStore(
            MemoryAtomicFilePort files)
        {
            return new AtomicPlayerAccountStoreV1(
                files,
                "account.active",
                "account.temp",
                "account.backup");
        }

        private static void SeedActiveAndBackup(
            AtomicPlayerAccountStoreV1 store)
        {
            Assert.That(store.Save(UnknownAccount("baseline-one", 0L)).Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.Saved));
            Assert.That(store.Save(UnknownAccount("baseline-two", 1L)).Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.Saved));
        }

        private static PlayerAccountSnapshotV1 UnknownAccount(
            string suffix,
            long revision)
        {
            return AccountWithCharacterComponent(
                new SaveComponentSnapshotV1(
                    Id("future." + suffix),
                    7,
                    "future-v7",
                    "opaque-" + suffix),
                suffix,
                revision);
        }

        private static PlayerAccountSnapshotV1 AccountWithCharacterComponent(
            SaveComponentSnapshotV1 component,
            string suffix,
            long revision,
            StableId characterId = null)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = new CharacterInstanceSnapshotV1(
                characterId ?? Id("character.compatibility-" + suffix),
                Id("class.striker"),
                0,
                "Compatibility " + suffix,
                revision,
                new[] { component });
            return new PlayerAccountSnapshotV1(
                Id("account.compatibility-" + suffix),
                revision,
                slots,
                null);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class MemoryAtomicFilePort : IAtomicSaveFilePortV1
        {
            private readonly Dictionary<string, string> files =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public bool Exists(string path) { return files.ContainsKey(path); }

            public string ReadAllText(string path) { return files[path]; }

            public void WriteAllText(string path, string contents)
            {
                files[path] = contents;
            }

            public void Move(string sourcePath, string destinationPath)
            {
                files[destinationPath] = files[sourcePath];
                files.Remove(sourcePath);
            }

            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath)
            {
                files[backupPath] = files[destinationPath];
                files[destinationPath] = files[sourcePath];
                files.Remove(sourcePath);
            }

            public void Delete(string path) { files.Remove(path); }
        }
    }
}

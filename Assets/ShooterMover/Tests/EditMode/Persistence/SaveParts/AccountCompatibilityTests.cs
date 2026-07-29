using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.SaveParts
{
    public sealed class AccountCompatibilityTests
    {
        [Test]
        public void SixSlotsAndUnknownOpaqueComponentsRoundTripWithoutCrossContamination()
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = new CharacterInstanceSnapshot(
                    Id("character.compatibility-slot-" + index),
                    Id("class.compatibility-" + index),
                    index,
                    "Compatibility " + index,
                    index,
                    new[]
                    {
                        new SavePartSnapshot(
                            Id("future.opaque-slot-" + index),
                            17,
                            "future-content-v17",
                            "opaque-slot-payload-" + index),
                    });
            }
            var source = new PlayerAccountSnapshot(
                Id("account.compatibility-six-slots"),
                5L,
                slots,
                new[]
                {
                    new SavePartSnapshot(
                        Id("future.account-opaque"),
                        3,
                        "future-account-v3",
                        "opaque-account-payload"),
                });
            var files = new MemoryAtomicFilePort();
            GameSaveFile store = CreateDefaultStore(files);

            Assert.That(store.Save(source).Status,
                Is.EqualTo(PlayerAccountStoreStatus.Saved));
            PlayerAccountStoreResult loaded = store.Load();

            Assert.That(loaded.Status,
                Is.EqualTo(PlayerAccountStoreStatus.Loaded));
            Assert.That(loaded.Snapshot.Fingerprint,
                Is.EqualTo(source.Fingerprint));
            for (int index = 0; index < slots.Length; index++)
            {
                CharacterInstanceSnapshot character =
                    loaded.Snapshot.CharacterAt(index);
                Assert.That(character.CharacterInstanceStableId,
                    Is.EqualTo(Id("character.compatibility-slot-" + index)));
                SavePartSnapshot component;
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
            SavePartSnapshot accountComponent;
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
            GameSaveFile store = CreateDefaultStore(files);
            SeedActiveAndBackup(store);
            string previousActive = files.ReadAllText("account.active");
            string previousBackup = files.ReadAllText("account.backup");

            SavePartDefinition xp =
                GameSaveParts.PlayerExperience();
            PlayerAccountSnapshot unsupported = AccountWithCharacterComponent(
                new SavePartSnapshot(
                    xp.ComponentStableId,
                    xp.SchemaVersion + 1,
                    xp.ContentVersion,
                    "unsupported-known-schema"),
                "schema-unsupported",
                2L);

            PlayerAccountStoreResult rejected = store.Save(unsupported);

            Assert.That(rejected.Status,
                Is.EqualTo(PlayerAccountStoreStatus.ValidationRejected));
            Assert.That(rejected.RejectionCode,
                Does.StartWith("known-save-part-version-unsupported"));
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
            GameSaveFile store = CreateDefaultStore(files);
            SeedActiveAndBackup(store);
            string previousActive = files.ReadAllText("account.active");
            string previousBackup = files.ReadAllText("account.backup");

            SavePartDefinition xp =
                GameSaveParts.PlayerExperience();
            PlayerAccountSnapshot unsupported = AccountWithCharacterComponent(
                new SavePartSnapshot(
                    xp.ComponentStableId,
                    xp.SchemaVersion,
                    xp.ContentVersion + ".unsupported",
                    "unsupported-known-content"),
                "content-unsupported",
                2L);

            PlayerAccountStoreResult rejected = store.Save(unsupported);

            Assert.That(rejected.Status,
                Is.EqualTo(PlayerAccountStoreStatus.ValidationRejected));
            Assert.That(rejected.RejectionCode,
                Does.StartWith("known-save-part-version-unsupported"));
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
            var store = new GameSaveFile(
                files,
                "account.active",
                "account.temp",
                "account.backup",
                account =>
                {
                    semanticValidationCalls++;
                    return SavePartValidationResult.Accept();
                });
            PlayerAccountSnapshot baseline = UnknownAccount(
                "custom-validator-baseline",
                0L);
            Assert.That(store.Save(baseline).Status,
                Is.EqualTo(PlayerAccountStoreStatus.Saved));
            string previousActive = files.ReadAllText("account.active");

            SavePartDefinition holdings =
                GameSaveParts.PlayerHoldings();
            PlayerAccountSnapshot unsupported = AccountWithCharacterComponent(
                new SavePartSnapshot(
                    holdings.ComponentStableId,
                    holdings.SchemaVersion + 1,
                    holdings.ContentVersion,
                    "custom-validator-must-not-bypass"),
                "custom-validator-unsupported",
                1L);
            PlayerAccountStoreResult rejected = store.Save(unsupported);

            Assert.That(semanticValidationCalls, Is.GreaterThan(0));
            Assert.That(rejected.Status,
                Is.EqualTo(PlayerAccountStoreStatus.ValidationRejected));
            Assert.That(rejected.RejectionCode,
                Does.StartWith("known-save-part-version-unsupported"));
            Assert.That(files.ReadAllText("account.active"),
                Is.EqualTo(previousActive));
            Assert.That(files.Exists("account.temp"), Is.False);
        }

        [Test]
        public void CoordinatorRejectsUnsupportedKnownOptionalWithoutAdapter()
        {
            SavePartDefinition strongbox =
                GameSaveParts.StrongboxState(required: false);
            StableId characterId = Id("character.optional-known-version");
            PlayerAccountSnapshot account = AccountWithCharacterComponent(
                new SavePartSnapshot(
                    strongbox.ComponentStableId,
                    strongbox.SchemaVersion,
                    strongbox.ContentVersion + ".unsupported",
                    "optional-known-version"),
                "optional-known-version",
                0L,
                characterId);
            var coordinator = new PlayerAccountRestoreFlow();
            var binding = new CharacterSaveRestoreBinding(
                0,
                characterId,
                Array.Empty<ISavePart>());

            PlayerAccountRestoreResult result = coordinator.Restore(
                account,
                new[] { binding });

            Assert.That(result.Status,
                Is.EqualTo(PlayerAccountRestoreStatus.ValidationRejected));
            Assert.That(result.RejectionCode,
                Does.StartWith("known-save-part-version-unsupported"));
            Assert.That(result.RetainedUnknownComponents, Is.Empty);
        }

        [Test]
        public void GenuinelyUnknownFutureComponentRemainsOpaqueThroughStoreAndRestore()
        {
            StableId characterId = Id("character.unknown-future");
            SavePartSnapshot future = new SavePartSnapshot(
                Id("future.component-v42"),
                42,
                "future-content-v42",
                "opaque-future-payload");
            PlayerAccountSnapshot account = AccountWithCharacterComponent(
                future,
                "unknown-future",
                0L,
                characterId);
            var files = new MemoryAtomicFilePort();
            GameSaveFile store = CreateDefaultStore(files);

            Assert.That(store.Save(account).Status,
                Is.EqualTo(PlayerAccountStoreStatus.Saved));
            PlayerAccountSnapshot loaded = store.Load().Snapshot;
            var coordinator = new PlayerAccountRestoreFlow();
            var binding = new CharacterSaveRestoreBinding(
                0,
                characterId,
                Array.Empty<ISavePart>());
            PlayerAccountRestoreResult restored = coordinator.Restore(
                loaded,
                new[] { binding });

            Assert.That(restored.Status,
                Is.EqualTo(PlayerAccountRestoreStatus.Restored));
            Assert.That(restored.RetainedUnknownComponents.Count,
                Is.EqualTo(1));
            SavePartSnapshot retained =
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
                GameSaveFormats.PlayerExperience.ContractId,
                GameSaveFormats.PlayerHoldings.ContractId,
                GameSaveFormats.MoneyWallet.ContractId,
                GameSaveFormats.ScrapWallet.ContractId,
                GameSaveFormats.RankedSkillAllocation.ContractId,
                GameSaveFormats.ExactInstanceLoadout.ContractId,
                GameSaveFormats.StrongboxState.ContractId,
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
                Id("save-part.character-statistics"),
                Is.Not.EqualTo(
                    GameSaveParts.PlayerExperience()
                        .ComponentStableId));
            Assert.That(
                KnownSavePartVersionGuard.ValidateComponent(
                    new SavePartSnapshot(
                        Id("save-part.character-statistics"),
                        1,
                        "unregistered-statistics-contract",
                        "opaque"))
                    .Succeeded,
                Is.True,
                "Unregistered future statistics remain opaque; no arbitrary typed adapter is exposed.");
        }

        private static GameSaveFile CreateDefaultStore(
            MemoryAtomicFilePort files)
        {
            return new GameSaveFile(
                files,
                "account.active",
                "account.temp",
                "account.backup");
        }

        private static void SeedActiveAndBackup(
            GameSaveFile store)
        {
            Assert.That(store.Save(UnknownAccount("baseline-one", 0L)).Status,
                Is.EqualTo(PlayerAccountStoreStatus.Saved));
            Assert.That(store.Save(UnknownAccount("baseline-two", 1L)).Status,
                Is.EqualTo(PlayerAccountStoreStatus.Saved));
        }

        private static PlayerAccountSnapshot UnknownAccount(
            string suffix,
            long revision)
        {
            return AccountWithCharacterComponent(
                new SavePartSnapshot(
                    Id("future." + suffix),
                    7,
                    "future-v7",
                    "opaque-" + suffix),
                suffix,
                revision);
        }

        private static PlayerAccountSnapshot AccountWithCharacterComponent(
            SavePartSnapshot component,
            string suffix,
            long revision,
            StableId characterId = null)
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            slots[0] = new CharacterInstanceSnapshot(
                characterId ?? Id("character.compatibility-" + suffix),
                Id("class.striker"),
                0,
                "Compatibility " + suffix,
                revision,
                new[] { component });
            return new PlayerAccountSnapshot(
                Id("account.compatibility-" + suffix),
                revision,
                slots,
                null);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class MemoryAtomicFilePort : IAtomicSaveFilePort
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

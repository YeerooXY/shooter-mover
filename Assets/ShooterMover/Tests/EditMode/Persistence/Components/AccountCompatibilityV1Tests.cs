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

            string encoded = PlayerAccountFileCodecV1.Encode(source);
            PlayerAccountSnapshotV1 decoded;
            string rejection;
            Assert.That(PlayerAccountFileCodecV1.TryDecode(
                encoded,
                out decoded,
                out rejection), Is.True, rejection);

            Assert.That(decoded.Fingerprint, Is.EqualTo(source.Fingerprint));
            for (int index = 0; index < slots.Length; index++)
            {
                CharacterInstanceSnapshotV1 character = decoded.CharacterAt(index);
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
            Assert.That(decoded.TryGetAccountComponent(
                Id("future.account-opaque"),
                out accountComponent), Is.True);
            Assert.That(accountComponent.CanonicalPayload,
                Is.EqualTo("opaque-account-payload"));
        }

        [Test]
        public void KnownUnsupportedVersionRejectsWithoutReplacingActiveSave()
        {
            var files = new MemoryAtomicFilePort();
            var store = new AtomicPlayerAccountStoreV1(
                files,
                "account.active",
                "account.temp",
                "account.backup",
                KnownSaveComponentVersionGuardV1.Validate);
            PlayerAccountSnapshotV1 valid = AccountWithComponent(
                new SaveComponentSnapshotV1(
                    Id("future.known-guard-baseline"),
                    1,
                    "future-v1",
                    "baseline"));
            Assert.That(store.Save(valid).Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.Saved));
            string previousActive = files.ReadAllText("account.active");

            SaveComponentDefinitionV1 xp =
                KnownSaveComponentDefinitionsV1.PlayerExperience();
            PlayerAccountSnapshotV1 unsupported = AccountWithComponent(
                new SaveComponentSnapshotV1(
                    xp.ComponentStableId,
                    xp.SchemaVersion + 1,
                    xp.ContentVersion,
                    "opaque-but-known-version"));
            PlayerAccountStoreResultV1 rejected = store.Save(unsupported);

            Assert.That(rejected.Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.ValidationRejected));
            Assert.That(rejected.RejectionCode,
                Does.StartWith("known-save-component-version-unsupported"));
            Assert.That(files.ReadAllText("account.active"),
                Is.EqualTo(previousActive));
            Assert.That(store.Load().Snapshot.Fingerprint,
                Is.EqualTo(valid.Fingerprint));
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

        private static PlayerAccountSnapshotV1 AccountWithComponent(
            SaveComponentSnapshotV1 component)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = new CharacterInstanceSnapshotV1(
                Id("character.compatibility-version"),
                Id("class.striker"),
                0,
                "Compatibility Version",
                0L,
                new[] { component });
            return new PlayerAccountSnapshotV1(
                Id("account.compatibility-version"),
                0L,
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

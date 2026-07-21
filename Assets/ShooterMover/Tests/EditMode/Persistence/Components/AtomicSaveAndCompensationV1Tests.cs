using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Components
{
    public sealed class AtomicSaveAndCompensationV1Tests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void MutateThenRejectOrThrowCompensatesFailingAndEarlierAuthorities(
            bool throwAfterMutation)
        {
            var earlier = new TestAuthority(TestSnapshot.Create("earlier", 1L));
            var failing = new TestAuthority(TestSnapshot.Create("failing", 2L))
            {
                FailNextNonRollbackApply = true,
                ThrowAfterMutation = throwAfterMutation,
            };
            string earlierBefore = earlier.Current.Fingerprint;
            string failingBefore = failing.Current.Fingerprint;
            TestSnapshot earlierNext = TestSnapshot.Create("earlier", 10L);
            TestSnapshot failingNext = TestSnapshot.Create("failing", 20L);

            PlayerAccountSnapshotV1 account = Account(
                Component("test-earlier", 10, earlierNext),
                Component("test-failing", 20, failingNext));

            PlayerAccountRestoreResultV1 result =
                new PlayerAccountRestoreCoordinatorV1().Restore(
                    account,
                    new[]
                    {
                        new CharacterSaveRestoreBindingV1(
                            0,
                            Id("character.compensation"),
                            new[]
                            {
                                Adapter("test-earlier", 10, earlier),
                                Adapter("test-failing", 20, failing),
                            }),
                    });

            Assert.That(result.Status,
                Is.EqualTo(PlayerAccountRestoreStatusV1.CommitFailedRolledBack),
                result.RejectionCode);
            Assert.That(earlier.Current.Fingerprint, Is.EqualTo(earlierBefore));
            Assert.That(failing.Current.Fingerprint, Is.EqualTo(failingBefore));
            Assert.That(earlier.Current.Value, Is.EqualTo(1L));
            Assert.That(failing.Current.Value, Is.EqualTo(2L));
        }

        [Test]
        public void FailingCompensationFailureIsReportedSeparately()
        {
            var earlier = new TestAuthority(TestSnapshot.Create("earlier", 1L));
            var failing = new TestAuthority(TestSnapshot.Create("failing", 2L))
            {
                FailNextNonRollbackApply = true,
                FailRollback = true,
            };
            PlayerAccountRestoreResultV1 result = RestoreTwo(
                earlier,
                failing,
                TestSnapshot.Create("earlier", 10L),
                TestSnapshot.Create("failing", 20L));

            Assert.That(result.Status,
                Is.EqualTo(PlayerAccountRestoreStatusV1
                    .CommitFailedCompensationIncomplete));
            Assert.That(earlier.Current.Value, Is.EqualTo(1L));
            Assert.That(failing.Current.Value, Is.EqualTo(20L));
        }

        [Test]
        public void EarlierRollbackFailureIsReportedSeparately()
        {
            var earlier = new TestAuthority(TestSnapshot.Create("earlier", 1L))
            {
                FailRollback = true,
            };
            var failing = new TestAuthority(TestSnapshot.Create("failing", 2L))
            {
                FailNextNonRollbackApply = true,
            };
            PlayerAccountRestoreResultV1 result = RestoreTwo(
                earlier,
                failing,
                TestSnapshot.Create("earlier", 10L),
                TestSnapshot.Create("failing", 20L));

            Assert.That(result.Status,
                Is.EqualTo(PlayerAccountRestoreStatusV1
                    .CommitFailedEarlierRollbackIncomplete));
            Assert.That(earlier.Current.Value, Is.EqualTo(10L));
            Assert.That(failing.Current.Value, Is.EqualTo(2L));
        }

        [Test]
        public void CompensationAndEarlierRollbackFailureAreReportedTogether()
        {
            var earlier = new TestAuthority(TestSnapshot.Create("earlier", 1L))
            {
                FailRollback = true,
            };
            var failing = new TestAuthority(TestSnapshot.Create("failing", 2L))
            {
                FailNextNonRollbackApply = true,
                FailRollback = true,
            };
            PlayerAccountRestoreResultV1 result = RestoreTwo(
                earlier,
                failing,
                TestSnapshot.Create("earlier", 10L),
                TestSnapshot.Create("failing", 20L));

            Assert.That(result.Status,
                Is.EqualTo(PlayerAccountRestoreStatusV1
                    .CommitFailedCompensationAndRollbackIncomplete));
            Assert.That(earlier.Current.Value, Is.EqualTo(10L));
            Assert.That(failing.Current.Value, Is.EqualTo(20L));
        }

        [Test]
        public void BoundedCanonicalParserRejectsDepthCountsAndScalarLength()
        {
            CanonicalNodeV1 ignored;
            string rejection;
            string deep = string.Empty;
            for (int index = 0;
                index < SavePersistenceLimitsV1.MaximumNodeDepth + 2;
                index++)
            {
                deep += "L1:";
            }
            deep += "N;";
            Assert.That(CanonicalNodeCodecV1.TryDecode(
                deep,
                SavePersistenceLimitsV1.MaximumComponentPayloadBytes,
                out ignored,
                out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("canonical-node-depth-exceeded"));

            Assert.That(CanonicalNodeCodecV1.TryDecode(
                "L" + (SavePersistenceLimitsV1.MaximumCollectionCount + 1) + ":",
                SavePersistenceLimitsV1.MaximumComponentPayloadBytes,
                out ignored,
                out rejection), Is.False);
            Assert.That(rejection,
                Is.EqualTo("canonical-collection-count-exceeded"));

            Assert.That(CanonicalNodeCodecV1.TryDecode(
                "O" + (SavePersistenceLimitsV1.MaximumPropertyCount + 1) + ":",
                SavePersistenceLimitsV1.MaximumComponentPayloadBytes,
                out ignored,
                out rejection), Is.False);
            Assert.That(rejection,
                Is.EqualTo("canonical-property-count-exceeded"));

            Assert.That(CanonicalNodeCodecV1.TryDecode(
                "V" + (SavePersistenceLimitsV1.MaximumScalarLength + 1) + ":",
                SavePersistenceLimitsV1.MaximumComponentPayloadBytes,
                out ignored,
                out rejection), Is.False);
            Assert.That(rejection,
                Is.EqualTo("canonical-scalar-length-exceeded"));
        }

        [Test]
        public void OversizedAccountAndComponentPayloadsReturnStableRejections()
        {
            PlayerAccountSnapshotV1 ignored;
            string rejection;
            string oversizedFile = new string(
                'x',
                SavePersistenceLimitsV1.MaximumAccountFileBytes + 1);
            Assert.That(PlayerAccountFileCodecV1.TryDecode(
                oversizedFile,
                out ignored,
                out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("account-file-too-large"));

            CanonicalNodeV1 node;
            string oversizedComponent = new string(
                'x',
                SavePersistenceLimitsV1.MaximumComponentPayloadBytes + 1);
            Assert.That(CanonicalNodeCodecV1.TryDecode(
                oversizedComponent,
                SavePersistenceLimitsV1.MaximumComponentPayloadBytes,
                out node,
                out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("component-payload-too-large"));
        }

        [Test]
        public void TemporaryInterruptionAndCorruptActiveRecoverLastKnownGood()
        {
            var files = new MemoryAtomicFilePort();
            var store = new AtomicPlayerAccountStoreV1(
                files,
                "account.active",
                "account.temp",
                "account.backup");
            PlayerAccountSnapshotV1 first = Account();
            PlayerAccountSnapshotV1 second = first.WithAccountComponent(
                new SaveComponentSnapshotV1(
                    Id("future.opaque-component"),
                    1,
                    "future-v1",
                    "opaque"));
            Assert.That(store.Save(first).Succeeded, Is.True);
            Assert.That(store.Save(second).Succeeded, Is.True);
            string secondActive = files.ReadAllText("account.active");

            files.FailNextReadPath = "account.temp";
            PlayerAccountStoreResultV1 interrupted = store.Save(
                second.WithAccountComponent(new SaveComponentSnapshotV1(
                    Id("future.second-component"),
                    1,
                    "future-v1",
                    "opaque-2")));
            Assert.That(interrupted.Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.IoFailure));
            Assert.That(files.ReadAllText("account.active"), Is.EqualTo(secondActive));

            files.WriteAllText("account.active", "corrupt-active");
            PlayerAccountStoreResultV1 recovered = store.Load();
            Assert.That(recovered.Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.RecoveredLastKnownGood));
            Assert.That(recovered.Snapshot.Fingerprint,
                Is.EqualTo(first.Fingerprint));
        }

        private static PlayerAccountRestoreResultV1 RestoreTwo(
            TestAuthority earlier,
            TestAuthority failing,
            TestSnapshot earlierNext,
            TestSnapshot failingNext)
        {
            return new PlayerAccountRestoreCoordinatorV1().Restore(
                Account(
                    Component("test-earlier", 10, earlierNext),
                    Component("test-failing", 20, failingNext)),
                new[]
                {
                    new CharacterSaveRestoreBindingV1(
                        0,
                        Id("character.compensation"),
                        new[]
                        {
                            Adapter("test-earlier", 10, earlier),
                            Adapter("test-failing", 20, failing),
                        }),
                });
        }

        private static ISaveComponentAdapterV1 Adapter(
            string suffix,
            int order,
            TestAuthority authority)
        {
            var definition = new SaveComponentDefinitionV1(
                Id("test-component." + suffix),
                1,
                "test-snapshot-v1",
                true,
                order);
            return new AuthoritySnapshotSaveComponentAdapterV1<TestSnapshot>(
                definition,
                new TestSnapshotCodec(),
                () => authority.Current,
                snapshot => SaveComponentValidationResultV1.Accept(),
                authority.Apply);
        }

        private static SaveComponentSnapshotV1 Component(
            string suffix,
            int order,
            TestSnapshot snapshot)
        {
            var definition = new SaveComponentDefinitionV1(
                Id("test-component." + suffix),
                1,
                "test-snapshot-v1",
                true,
                order);
            return new SaveComponentSnapshotV1(
                definition.ComponentStableId,
                definition.SchemaVersion,
                definition.ContentVersion,
                new TestSnapshotCodec().Encode(snapshot));
        }

        private static PlayerAccountSnapshotV1 Account(
            params SaveComponentSnapshotV1[] components)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = new CharacterInstanceSnapshotV1(
                Id("character.compensation"),
                Id("class.test"),
                0,
                "Compensation Test",
                0L,
                components ?? Array.Empty<SaveComponentSnapshotV1>());
            return new PlayerAccountSnapshotV1(
                Id("account.compensation"),
                0L,
                slots,
                null);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class TestAuthority
        {
            private readonly string originalFingerprint;

            public TestAuthority(TestSnapshot initial)
            {
                Current = initial;
                originalFingerprint = initial.Fingerprint;
            }

            public TestSnapshot Current { get; private set; }

            public bool FailNextNonRollbackApply { get; set; }

            public bool ThrowAfterMutation { get; set; }

            public bool FailRollback { get; set; }

            public SaveComponentApplyResultV1 Apply(TestSnapshot snapshot)
            {
                bool rollback = snapshot.Fingerprint == originalFingerprint;
                if (rollback && FailRollback)
                {
                    return SaveComponentApplyResultV1.Rejected(
                        "forced-rollback-rejection");
                }
                Current = snapshot;
                if (!rollback && FailNextNonRollbackApply)
                {
                    FailNextNonRollbackApply = false;
                    if (ThrowAfterMutation)
                    {
                        throw new InvalidOperationException(
                            "forced-throw-after-mutation");
                    }
                    return SaveComponentApplyResultV1.Rejected(
                        "forced-reject-after-mutation");
                }
                return SaveComponentApplyResultV1.Applied();
            }
        }

        private sealed class TestSnapshot
        {
            private TestSnapshot(string owner, long value)
            {
                Owner = owner;
                Value = value;
                Fingerprint = Hash(owner + "|" + value);
            }

            public string Owner { get; }

            public long Value { get; }

            public string Fingerprint { get; }

            public static TestSnapshot Create(string owner, long value)
            {
                return new TestSnapshot(owner, value);
            }

            private static string Hash(string value)
            {
                using (SHA256 algorithm = SHA256.Create())
                {
                    return BitConverter.ToString(algorithm.ComputeHash(
                        Encoding.UTF8.GetBytes(value)))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                }
            }
        }

        private sealed class TestSnapshotCodec :
            ExplicitSaveComponentCodecV1<TestSnapshot>
        {
            public TestSnapshotCodec() : base("test-snapshot-v1") { }

            public override SaveComponentValidationResultV1 Validate(
                TestSnapshot snapshot)
            {
                return snapshot == null
                    ? SaveComponentValidationResultV1.Reject("test-snapshot-null")
                    : SaveComponentValidationResultV1.Accept();
            }

            protected override CanonicalNodeV1 EncodeNode(TestSnapshot snapshot)
            {
                return CanonicalNodeV1.Object(
                    CanonicalValueV1.Field(
                        "owner",
                        CanonicalValueV1.RequiredString(snapshot.Owner)),
                    CanonicalValueV1.Field(
                        "value",
                        CanonicalValueV1.Int64(snapshot.Value)));
            }

            protected override TestSnapshot DecodeNode(CanonicalNodeV1 node)
            {
                var reader = new CanonicalObjectReaderV1(
                    node,
                    "owner",
                    "value");
                return TestSnapshot.Create(
                    CanonicalValueV1.ReadRequiredString(reader.Next("owner")),
                    CanonicalValueV1.ReadInt64(reader.Next("value")));
            }
        }

        private sealed class MemoryAtomicFilePort : IAtomicSaveFilePortV1
        {
            private readonly Dictionary<string, string> files =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public string FailNextReadPath { get; set; }

            public bool Exists(string path) { return files.ContainsKey(path); }

            public string ReadAllText(string path)
            {
                if (path == FailNextReadPath)
                {
                    FailNextReadPath = null;
                    throw new InvalidOperationException("forced-read-failure");
                }
                return files[path];
            }

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

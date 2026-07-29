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
    public sealed class AtomicSaveAndCompensationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void MutateThenRejectOrThrowCompensatesFailingAndEarlierAuthorities(
            bool throwAfterMutation)
        {
            var earlier = new TestState(TestSnapshot.Create("earlier", 1L));
            var failing = new TestState(TestSnapshot.Create("failing", 2L))
            {
                FailNextNonRollbackApply = true,
                ThrowAfterMutation = throwAfterMutation,
            };
            string earlierBefore = earlier.Current.Fingerprint;
            string failingBefore = failing.Current.Fingerprint;
            TestSnapshot earlierNext = TestSnapshot.Create("earlier", 10L);
            TestSnapshot failingNext = TestSnapshot.Create("failing", 20L);

            PlayerAccountSnapshot account = Account(
                Component("test-earlier", 10, earlierNext),
                Component("test-failing", 20, failingNext));

            PlayerAccountRestoreResult result =
                new PlayerAccountRestoreFlow().Restore(
                    account,
                    new[]
                    {
                        new CharacterSaveRestoreBinding(
                            0,
                            Id("character.compensation"),
                            new[]
                            {
                                Adapter("test-earlier", 10, earlier),
                                Adapter("test-failing", 20, failing),
                            }),
                    });

            Assert.That(result.Status,
                Is.EqualTo(PlayerAccountRestoreStatus.CommitFailedRolledBack),
                result.RejectionCode);
            Assert.That(earlier.Current.Fingerprint, Is.EqualTo(earlierBefore));
            Assert.That(failing.Current.Fingerprint, Is.EqualTo(failingBefore));
            Assert.That(earlier.Current.Value, Is.EqualTo(1L));
            Assert.That(failing.Current.Value, Is.EqualTo(2L));
        }

        [Test]
        public void FailingCompensationFailureIsReportedSeparately()
        {
            var earlier = new TestState(TestSnapshot.Create("earlier", 1L));
            var failing = new TestState(TestSnapshot.Create("failing", 2L))
            {
                FailNextNonRollbackApply = true,
                FailRollback = true,
            };
            PlayerAccountRestoreResult result = RestoreTwo(
                earlier,
                failing,
                TestSnapshot.Create("earlier", 10L),
                TestSnapshot.Create("failing", 20L));

            Assert.That(result.Status,
                Is.EqualTo(PlayerAccountRestoreStatus
                    .CommitFailedCompensationIncomplete));
            Assert.That(earlier.Current.Value, Is.EqualTo(1L));
            Assert.That(failing.Current.Value, Is.EqualTo(20L));
        }

        [Test]
        public void EarlierRollbackFailureIsReportedSeparately()
        {
            var earlier = new TestState(TestSnapshot.Create("earlier", 1L))
            {
                FailRollback = true,
            };
            var failing = new TestState(TestSnapshot.Create("failing", 2L))
            {
                FailNextNonRollbackApply = true,
            };
            PlayerAccountRestoreResult result = RestoreTwo(
                earlier,
                failing,
                TestSnapshot.Create("earlier", 10L),
                TestSnapshot.Create("failing", 20L));

            Assert.That(result.Status,
                Is.EqualTo(PlayerAccountRestoreStatus
                    .CommitFailedEarlierRollbackIncomplete));
            Assert.That(earlier.Current.Value, Is.EqualTo(10L));
            Assert.That(failing.Current.Value, Is.EqualTo(2L));
        }

        [Test]
        public void CompensationAndEarlierRollbackFailureAreReportedTogether()
        {
            var earlier = new TestState(TestSnapshot.Create("earlier", 1L))
            {
                FailRollback = true,
            };
            var failing = new TestState(TestSnapshot.Create("failing", 2L))
            {
                FailNextNonRollbackApply = true,
                FailRollback = true,
            };
            PlayerAccountRestoreResult result = RestoreTwo(
                earlier,
                failing,
                TestSnapshot.Create("earlier", 10L),
                TestSnapshot.Create("failing", 20L));

            Assert.That(result.Status,
                Is.EqualTo(PlayerAccountRestoreStatus
                    .CommitFailedCompensationAndRollbackIncomplete));
            Assert.That(earlier.Current.Value, Is.EqualTo(10L));
            Assert.That(failing.Current.Value, Is.EqualTo(20L));
        }

        [Test]
        public void BoundedCanonicalParserRejectsDepthCountsAndScalarLength()
        {
            Node ignored;
            string rejection;
            string deep = string.Empty;
            for (int index = 0;
                index < SavePersistenceLimits.MaximumNodeDepth + 2;
                index++)
            {
                deep += "L1:";
            }
            deep += "N;";
            Assert.That(NodeCodec.TryDecode(
                deep,
                SavePersistenceLimits.MaximumComponentPayloadBytes,
                out ignored,
                out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("canonical-node-depth-exceeded"));

            Assert.That(NodeCodec.TryDecode(
                "L" + (SavePersistenceLimits.MaximumCollectionCount + 1) + ":",
                SavePersistenceLimits.MaximumComponentPayloadBytes,
                out ignored,
                out rejection), Is.False);
            Assert.That(rejection,
                Is.EqualTo("canonical-collection-count-exceeded"));

            Assert.That(NodeCodec.TryDecode(
                "O" + (SavePersistenceLimits.MaximumPropertyCount + 1) + ":",
                SavePersistenceLimits.MaximumComponentPayloadBytes,
                out ignored,
                out rejection), Is.False);
            Assert.That(rejection,
                Is.EqualTo("canonical-property-count-exceeded"));

            Assert.That(NodeCodec.TryDecode(
                "V" + (SavePersistenceLimits.MaximumScalarLength + 1) + ":",
                SavePersistenceLimits.MaximumComponentPayloadBytes,
                out ignored,
                out rejection), Is.False);
            Assert.That(rejection,
                Is.EqualTo("canonical-scalar-length-exceeded"));
        }

        [Test]
        public void OversizedAccountAndComponentPayloadsReturnStableRejections()
        {
            PlayerAccountSnapshot ignored;
            string rejection;
            string oversizedFile = new string(
                'x',
                SavePersistenceLimits.MaximumAccountFileBytes + 1);
            Assert.That(PlayerAccountFileCodec.TryDecode(
                oversizedFile,
                out ignored,
                out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("account-file-too-large"));

            Node node;
            string oversizedComponent = new string(
                'x',
                SavePersistenceLimits.MaximumComponentPayloadBytes + 1);
            Assert.That(NodeCodec.TryDecode(
                oversizedComponent,
                SavePersistenceLimits.MaximumComponentPayloadBytes,
                out node,
                out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("component-payload-too-large"));
        }

        [Test]
        public void TemporaryInterruptionAndCorruptActiveRecoverLastKnownGood()
        {
            var files = new MemoryAtomicFilePort();
            var store = new AtomicPlayerAccountStore(
                files,
                "account.active",
                "account.temp",
                "account.backup");
            PlayerAccountSnapshot first = Account();
            PlayerAccountSnapshot second = first.WithAccountComponent(
                new SaveComponentSnapshot(
                    Id("future.opaque-component"),
                    1,
                    "future-v1",
                    "opaque"));
            Assert.That(store.Save(first).Succeeded, Is.True);
            Assert.That(store.Save(second).Succeeded, Is.True);
            string secondActive = files.ReadAllText("account.active");

            files.FailNextReadPath = "account.temp";
            PlayerAccountStoreResult interrupted = store.Save(
                second.WithAccountComponent(new SaveComponentSnapshot(
                    Id("future.second-component"),
                    1,
                    "future-v1",
                    "opaque-2")));
            Assert.That(interrupted.Status,
                Is.EqualTo(PlayerAccountStoreStatus.IoFailure));
            Assert.That(files.ReadAllText("account.active"), Is.EqualTo(secondActive));

            files.WriteAllText("account.active", "corrupt-active");
            PlayerAccountStoreResult recovered = store.Load();
            Assert.That(recovered.Status,
                Is.EqualTo(PlayerAccountStoreStatus.RecoveredLastKnownGood));
            Assert.That(recovered.Snapshot.Fingerprint,
                Is.EqualTo(first.Fingerprint));
        }

        private static PlayerAccountRestoreResult RestoreTwo(
            TestState earlier,
            TestState failing,
            TestSnapshot earlierNext,
            TestSnapshot failingNext)
        {
            return new PlayerAccountRestoreFlow().Restore(
                Account(
                    Component("test-earlier", 10, earlierNext),
                    Component("test-failing", 20, failingNext)),
                new[]
                {
                    new CharacterSaveRestoreBinding(
                        0,
                        Id("character.compensation"),
                        new[]
                        {
                            Adapter("test-earlier", 10, earlier),
                            Adapter("test-failing", 20, failing),
                        }),
                });
        }

        private static ISaveComponentBridge Adapter(
            string suffix,
            int order,
            TestState authority)
        {
            var definition = new SaveComponentDefinition(
                Id("test-component." + suffix),
                1,
                "test-snapshot-v1",
                true,
                order);
            return new StateSnapshotSaveComponentBridge<TestSnapshot>(
                definition,
                new TestSnapshotCodec(),
                () => authority.Current,
                snapshot => SaveComponentValidationResult.Accept(),
                authority.Apply);
        }

        private static SaveComponentSnapshot Component(
            string suffix,
            int order,
            TestSnapshot snapshot)
        {
            var definition = new SaveComponentDefinition(
                Id("test-component." + suffix),
                1,
                "test-snapshot-v1",
                true,
                order);
            return new SaveComponentSnapshot(
                definition.ComponentStableId,
                definition.SchemaVersion,
                definition.ContentVersion,
                new TestSnapshotCodec().Encode(snapshot));
        }

        private static PlayerAccountSnapshot Account(
            params SaveComponentSnapshot[] components)
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            slots[0] = new CharacterInstanceSnapshot(
                Id("character.compensation"),
                Id("class.test"),
                0,
                "Compensation Test",
                0L,
                components ?? Array.Empty<SaveComponentSnapshot>());
            return new PlayerAccountSnapshot(
                Id("account.compensation"),
                0L,
                slots,
                null);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class TestState
        {
            private readonly string originalFingerprint;

            public TestState(TestSnapshot initial)
            {
                Current = initial;
                originalFingerprint = initial.Fingerprint;
            }

            public TestSnapshot Current { get; private set; }

            public bool FailNextNonRollbackApply { get; set; }

            public bool ThrowAfterMutation { get; set; }

            public bool FailRollback { get; set; }

            public SaveComponentApplyResult Apply(TestSnapshot snapshot)
            {
                bool rollback = snapshot.Fingerprint == originalFingerprint;
                if (rollback && FailRollback)
                {
                    return SaveComponentApplyResult.Rejected(
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
                    return SaveComponentApplyResult.Rejected(
                        "forced-reject-after-mutation");
                }
                return SaveComponentApplyResult.Applied();
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
            ExplicitSaveComponentCodec<TestSnapshot>
        {
            public TestSnapshotCodec() : base("test-snapshot-v1") { }

            public override SaveComponentValidationResult Validate(
                TestSnapshot snapshot)
            {
                return snapshot == null
                    ? SaveComponentValidationResult.Reject("test-snapshot-null")
                    : SaveComponentValidationResult.Accept();
            }

            protected override Node EncodeNode(TestSnapshot snapshot)
            {
                return Node.Object(
                    Value.Field(
                        "owner",
                        Value.RequiredString(snapshot.Owner)),
                    Value.Field(
                        "value",
                        Value.Int64(snapshot.Value)));
            }

            protected override TestSnapshot DecodeNode(Node node)
            {
                var reader = new ObjectReader(
                    node,
                    "owner",
                    "value");
                return TestSnapshot.Create(
                    Value.ReadRequiredString(reader.Next("owner")),
                    Value.ReadInt64(reader.Next("value")));
            }
        }

        private sealed class MemoryAtomicFilePort : IAtomicSaveFilePort
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

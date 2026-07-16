using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;

namespace ShooterMover.Tests.EditMode.Economy.Ledger
{
    public sealed class IdempotentLedgerTests
    {
        [Test]
        public void FirstCreditAppliesAndExactDuplicateReturnsOriginalOutcome()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            LedgerEntry<MoneyVocabulary> entry = MoneyEntry();
            LedgerMutation<MoneyVocabulary> mutation =
                Mutation("transaction.credit-001", entry, 25L);

            LedgerMutationResult<MoneyVocabulary> applied = ledger.Apply(mutation);
            LedgerMutationResult<MoneyVocabulary> duplicate = ledger.Apply(mutation);

            Assert.That(applied.Status, Is.EqualTo(LedgerMutationStatus.Applied));
            Assert.That(applied.SequenceBefore, Is.EqualTo(0L));
            Assert.That(applied.SequenceAfter, Is.EqualTo(1L));
            Assert.That(applied.PreviousQuantity, Is.EqualTo(0L));
            Assert.That(applied.CurrentQuantity, Is.EqualTo(25L));
            Assert.That(duplicate.Status, Is.EqualTo(LedgerMutationStatus.DuplicateNoChange));
            Assert.That(duplicate.OriginalStatus, Is.EqualTo(LedgerMutationStatus.Applied));
            Assert.That(duplicate.SequenceBefore, Is.EqualTo(applied.SequenceBefore));
            Assert.That(duplicate.SequenceAfter, Is.EqualTo(applied.SequenceAfter));
            Assert.That(duplicate.CurrentQuantity, Is.EqualTo(applied.CurrentQuantity));
            Assert.That(ledger.Sequence, Is.EqualTo(1L));
            Assert.That(ledger.GetQuantity(entry), Is.EqualTo(25L));
        }

        [TestCase(26L, "money.balance", "wallet.profile", "", null)]
        [TestCase(25L, "scrap.balance", "wallet.profile", "", null)]
        [TestCase(25L, "money.balance", "wallet.other", "", null)]
        [TestCase(25L, "money.balance", "wallet.profile", "changed", null)]
        [TestCase(25L, "money.balance", "wallet.profile", "", 0L)]
        public void ReusedTransactionIdWithChangedPayloadRejects(
            long changedDelta,
            string entryType,
            string target,
            string payload,
            long? expectedSequence)
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            LedgerMutation<MoneyVocabulary> original =
                Mutation("transaction.conflict-001", MoneyEntry(), 25L);
            ledger.Apply(original);

            LedgerMutation<MoneyVocabulary> changed = Mutation(
                "transaction.conflict-001",
                Entry<MoneyVocabulary>(entryType, target, payload),
                changedDelta,
                expectedSequence);

            LedgerMutationResult<MoneyVocabulary> result = ledger.Apply(changed);

            Assert.That(result.Status, Is.EqualTo(LedgerMutationStatus.ConflictingDuplicate));
            Assert.That(result.OriginalStatus, Is.EqualTo(LedgerMutationStatus.Applied));
            Assert.That(result.RejectionCode, Is.EqualTo("transaction-payload-conflict"));
            Assert.That(ledger.Sequence, Is.EqualTo(1L));
            Assert.That(ledger.GetQuantity(MoneyEntry()), Is.EqualTo(25L));
        }

        [Test]
        public void AcceptedDebitAppliesThroughExplicitPolicy()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            LedgerEntry<MoneyVocabulary> entry = MoneyEntry();
            ledger.Apply(Mutation("transaction.credit-002", entry, 40L));

            LedgerMutationResult<MoneyVocabulary> result =
                ledger.Apply(Mutation("transaction.debit-001", entry, -15L, 1L));

            Assert.That(result.Status, Is.EqualTo(LedgerMutationStatus.Applied));
            Assert.That(result.SequenceAfter, Is.EqualTo(2L));
            Assert.That(result.PreviousQuantity, Is.EqualTo(40L));
            Assert.That(result.CurrentQuantity, Is.EqualTo(25L));
            Assert.That(ledger.GetQuantity(entry), Is.EqualTo(25L));
        }

        [Test]
        public void RejectedDebitDoesNotMutateAndDuplicateIsDeterministic()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            LedgerEntry<MoneyVocabulary> entry = MoneyEntry();
            ledger.Apply(Mutation("transaction.credit-003", entry, 10L));
            LedgerMutation<MoneyVocabulary> rejected =
                Mutation("transaction.debit-002", entry, -11L, 1L);

            LedgerMutationResult<MoneyVocabulary> first = ledger.Apply(rejected);
            ledger.Apply(Mutation("transaction.credit-004", entry, 100L, 1L));
            LedgerMutationResult<MoneyVocabulary> duplicate = ledger.Apply(rejected);

            Assert.That(first.Status, Is.EqualTo(LedgerMutationStatus.PolicyRejected));
            Assert.That(first.RejectionCode, Is.EqualTo("insufficient-quantity"));
            Assert.That(first.SequenceBefore, Is.EqualTo(1L));
            Assert.That(first.SequenceAfter, Is.EqualTo(1L));
            Assert.That(duplicate.Status, Is.EqualTo(LedgerMutationStatus.DuplicateNoChange));
            Assert.That(duplicate.OriginalStatus, Is.EqualTo(LedgerMutationStatus.PolicyRejected));
            Assert.That(duplicate.RejectionCode, Is.EqualTo(first.RejectionCode));
            Assert.That(duplicate.CurrentQuantity, Is.EqualTo(10L));
            Assert.That(ledger.GetQuantity(entry), Is.EqualTo(110L));
        }

        [Test]
        public void ValidationRunsBeforePolicyAndRejectedCommandIsRecorded()
        {
            int policyCalls = 0;
            var ledger = new IdempotentLedger<MoneyVocabulary>(
                context => context.Mutation.Entry.CanonicalPayload == "invalid"
                    ? LedgerDecision.Reject("entry-validation-rejected")
                    : LedgerDecision.Accept(),
                context =>
                {
                    policyCalls++;
                    return LedgerDecision.Accept();
                });
            LedgerMutation<MoneyVocabulary> mutation = Mutation(
                "transaction.validation-001",
                Entry<MoneyVocabulary>("money.balance", "wallet.profile", "invalid"),
                5L);

            LedgerMutationResult<MoneyVocabulary> first = ledger.Apply(mutation);
            LedgerMutationResult<MoneyVocabulary> duplicate = ledger.Apply(mutation);

            Assert.That(first.Status, Is.EqualTo(LedgerMutationStatus.ValidationRejected));
            Assert.That(first.RejectionCode, Is.EqualTo("entry-validation-rejected"));
            Assert.That(duplicate.Status, Is.EqualTo(LedgerMutationStatus.DuplicateNoChange));
            Assert.That(duplicate.OriginalStatus, Is.EqualTo(LedgerMutationStatus.ValidationRejected));
            Assert.That(policyCalls, Is.EqualTo(0));
            Assert.That(ledger.Sequence, Is.Zero);
            Assert.That(ledger.TransactionCount, Is.EqualTo(1));
        }

        [Test]
        public void ExpectedSequenceSucceedsAndStaleCommandFailsWithoutMutation()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            LedgerEntry<MoneyVocabulary> entry = MoneyEntry();

            LedgerMutationResult<MoneyVocabulary> first =
                ledger.Apply(Mutation("transaction.sequence-001", entry, 1L, 0L));
            LedgerMutation<MoneyVocabulary> stale =
                Mutation("transaction.sequence-002", entry, 1L, 0L);
            LedgerMutationResult<MoneyVocabulary> conflict = ledger.Apply(stale);
            LedgerMutationResult<MoneyVocabulary> duplicate = ledger.Apply(stale);

            Assert.That(first.Status, Is.EqualTo(LedgerMutationStatus.Applied));
            Assert.That(conflict.Status, Is.EqualTo(LedgerMutationStatus.SequenceConflict));
            Assert.That(conflict.RejectionCode, Is.EqualTo("expected-sequence-conflict"));
            Assert.That(duplicate.Status, Is.EqualTo(LedgerMutationStatus.DuplicateNoChange));
            Assert.That(duplicate.OriginalStatus, Is.EqualTo(LedgerMutationStatus.SequenceConflict));
            Assert.That(ledger.Sequence, Is.EqualTo(1L));
            Assert.That(ledger.GetQuantity(entry), Is.EqualTo(1L));
        }

        [Test]
        public void PrimitiveDoesNotInventNegativeBalanceSemantics()
        {
            var permissive = new IdempotentLedger<MoneyVocabulary>(
                context => LedgerDecision.Accept(),
                context => LedgerDecision.Accept());
            LedgerEntry<MoneyVocabulary> entry = MoneyEntry();

            LedgerMutationResult<MoneyVocabulary> result =
                permissive.Apply(Mutation("transaction.negative-001", entry, -5L));

            Assert.That(result.Status, Is.EqualTo(LedgerMutationStatus.Applied));
            Assert.That(permissive.GetQuantity(entry), Is.EqualTo(-5L));
        }

        [Test]
        public void StructurallyInvalidCommandIsRejectedBeforeAdmission()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();

            LedgerMutationResult<MoneyVocabulary> result = ledger.Apply(
                Mutation("transaction.invalid-001", MoneyEntry(), 0L));

            Assert.That(result.Status, Is.EqualTo(LedgerMutationStatus.ValidationRejected));
            Assert.That(result.RejectionCode, Is.EqualTo("quantity-delta-zero"));
            Assert.That(ledger.TransactionCount, Is.Zero);
            Assert.That(ledger.Sequence, Is.Zero);
        }

        [Test]
        public void QuantityOverflowRejectsAndExactRetryIsDeterministic()
        {
            var ledger = new IdempotentLedger<MoneyVocabulary>(
                context => LedgerDecision.Accept(),
                context => LedgerDecision.Accept());
            LedgerEntry<MoneyVocabulary> entry = MoneyEntry();
            ledger.Apply(Mutation("transaction.max-001", entry, long.MaxValue));
            LedgerMutation<MoneyVocabulary> overflow =
                Mutation("transaction.max-002", entry, 1L);

            LedgerMutationResult<MoneyVocabulary> first = ledger.Apply(overflow);
            LedgerMutationResult<MoneyVocabulary> duplicate = ledger.Apply(overflow);

            Assert.That(first.Status, Is.EqualTo(LedgerMutationStatus.ValidationRejected));
            Assert.That(first.RejectionCode, Is.EqualTo("quantity-overflow"));
            Assert.That(duplicate.Status, Is.EqualTo(LedgerMutationStatus.DuplicateNoChange));
            Assert.That(duplicate.OriginalStatus, Is.EqualTo(LedgerMutationStatus.ValidationRejected));
            Assert.That(ledger.GetQuantity(entry), Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void ExportedSnapshotIsImmutableAndDetached()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            ledger.Apply(Mutation("transaction.snapshot-001", MoneyEntry(), 9L));
            LedgerSnapshot<MoneyVocabulary> snapshot = ledger.ExportSnapshot();

            Assert.Throws<NotSupportedException>(() =>
                ((IList<LedgerSnapshotEntry>)snapshot.Entries)[0] =
                    new LedgerSnapshotEntry("money.balance", "wallet.profile", "", 99L));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<LedgerTransactionSnapshot>)snapshot.Transactions).Clear());

            ledger.Apply(Mutation("transaction.snapshot-002", MoneyEntry(), 1L));

            Assert.That(snapshot.Sequence, Is.EqualTo(1L));
            Assert.That(snapshot.Entries[0].Quantity, Is.EqualTo(9L));
            Assert.That(snapshot.Transactions.Count, Is.EqualTo(1));
        }

        [Test]
        public void CanonicalSnapshotOrderingAndFingerprintIgnoreInputListOrder()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            ledger.Apply(Mutation(
                "transaction.order-b",
                Entry<MoneyVocabulary>("money.balance", "wallet.second", "b"),
                2L));
            ledger.Apply(Mutation(
                "transaction.order-a",
                Entry<MoneyVocabulary>("money.balance", "wallet.first", "a"),
                1L));
            LedgerSnapshot<MoneyVocabulary> exported = ledger.ExportSnapshot();

            LedgerSnapshot<MoneyVocabulary> rebuilt =
                LedgerSnapshot<MoneyVocabulary>.CreateCanonical(
                    exported.SchemaVersion,
                    exported.Sequence,
                    exported.Entries.Reverse(),
                    exported.Transactions.Reverse());

            Assert.That(
                rebuilt.Entries.Select(item => item.TargetId),
                Is.EqualTo(exported.Entries.Select(item => item.TargetId)));
            Assert.That(
                rebuilt.Transactions.Select(item => item.TransactionId),
                Is.EqualTo(exported.Transactions.Select(item => item.TransactionId)));
            Assert.That(rebuilt.Fingerprint, Is.EqualTo(exported.Fingerprint));
        }

        [Test]
        public void SnapshotRoundTripPreservesStateSequenceAndTransactionFacts()
        {
            IdempotentLedger<MoneyVocabulary> source = CreateMoneyLedger();
            LedgerEntry<MoneyVocabulary> entry = MoneyEntry();
            source.Apply(Mutation("transaction.roundtrip-001", entry, 20L));
            LedgerMutation<MoneyVocabulary> rejected =
                Mutation("transaction.roundtrip-002", entry, -25L, 1L);
            source.Apply(rejected);
            source.Apply(Mutation("transaction.roundtrip-003", entry, -5L, 1L));
            LedgerSnapshot<MoneyVocabulary> snapshot = source.ExportSnapshot();

            IdempotentLedger<MoneyVocabulary> restored = CreateMoneyLedger();
            LedgerImportResult import = restored.ImportSnapshot(snapshot);
            LedgerMutationResult<MoneyVocabulary> duplicateApplied =
                restored.Apply(Mutation("transaction.roundtrip-001", entry, 20L));
            LedgerMutationResult<MoneyVocabulary> duplicateRejected =
                restored.Apply(rejected);

            Assert.That(import.Status, Is.EqualTo(LedgerImportStatus.Imported));
            Assert.That(restored.Sequence, Is.EqualTo(source.Sequence));
            Assert.That(restored.GetQuantity(entry), Is.EqualTo(15L));
            Assert.That(restored.TransactionCount, Is.EqualTo(3));
            Assert.That(
                duplicateApplied.OriginalStatus,
                Is.EqualTo(LedgerMutationStatus.Applied));
            Assert.That(
                duplicateRejected.OriginalStatus,
                Is.EqualTo(LedgerMutationStatus.PolicyRejected));
            Assert.That(
                restored.ExportSnapshot().Fingerprint,
                Is.EqualTo(snapshot.Fingerprint));
        }

        [Test]
        public void CorruptFingerprintRejectsAtomically()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            ledger.Apply(Mutation("transaction.existing-001", MoneyEntry(), 7L));
            LedgerSnapshot<MoneyVocabulary> valid = BuildSourceSnapshot();
            var corrupt = new LedgerSnapshot<MoneyVocabulary>(
                valid.SchemaVersion,
                valid.Sequence,
                valid.Entries,
                valid.Transactions,
                new string('0', 64));

            LedgerImportResult result = ledger.ImportSnapshot(corrupt);

            Assert.That(result.Status, Is.EqualTo(LedgerImportStatus.FingerprintMismatch));
            AssertExistingStateWasNotReplaced(ledger);
        }

        [Test]
        public void UnsupportedSchemaRejectsAtomically()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            ledger.Apply(Mutation("transaction.existing-001", MoneyEntry(), 7L));
            LedgerSnapshot<MoneyVocabulary> valid = BuildSourceSnapshot();
            LedgerSnapshot<MoneyVocabulary> unsupported =
                LedgerSnapshot<MoneyVocabulary>.CreateCanonical(
                    LedgerSnapshot<MoneyVocabulary>.CurrentSchemaVersion + 1,
                    valid.Sequence,
                    valid.Entries,
                    valid.Transactions);

            LedgerImportResult result = ledger.ImportSnapshot(unsupported);

            Assert.That(
                result.Status,
                Is.EqualTo(LedgerImportStatus.UnsupportedSchemaVersion));
            AssertExistingStateWasNotReplaced(ledger);
        }

        [Test]
        public void DuplicateTransactionRecordsRejectAtomically()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            ledger.Apply(Mutation("transaction.existing-001", MoneyEntry(), 7L));
            LedgerSnapshot<MoneyVocabulary> valid = BuildSourceSnapshot();
            var duplicateTransactions =
                new List<LedgerTransactionSnapshot>(valid.Transactions)
                {
                    valid.Transactions[0],
                };
            LedgerSnapshot<MoneyVocabulary> corrupt =
                LedgerSnapshot<MoneyVocabulary>.CreateCanonical(
                    valid.SchemaVersion,
                    valid.Sequence,
                    valid.Entries,
                    duplicateTransactions);

            LedgerImportResult result = ledger.ImportSnapshot(corrupt);

            Assert.That(result.Status, Is.EqualTo(LedgerImportStatus.ValidationRejected));
            Assert.That(result.RejectionCode, Is.EqualTo("snapshot-transaction-duplicate"));
            AssertExistingStateWasNotReplaced(ledger);
        }

        [Test]
        public void InvalidCanonicalIdentityRejectsAtomically()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            ledger.Apply(Mutation("transaction.existing-001", MoneyEntry(), 7L));
            LedgerSnapshot<MoneyVocabulary> valid = BuildSourceSnapshot();
            var entries = new[]
            {
                new LedgerSnapshotEntry(
                    "Money.balance",
                    valid.Entries[0].TargetId,
                    valid.Entries[0].CanonicalPayload,
                    valid.Entries[0].Quantity),
            };
            LedgerSnapshot<MoneyVocabulary> corrupt =
                LedgerSnapshot<MoneyVocabulary>.CreateCanonical(
                    valid.SchemaVersion,
                    valid.Sequence,
                    entries,
                    valid.Transactions);

            LedgerImportResult result = ledger.ImportSnapshot(corrupt);

            Assert.That(result.Status, Is.EqualTo(LedgerImportStatus.ValidationRejected));
            Assert.That(result.RejectionCode, Is.EqualTo("snapshot-entry-identity-invalid"));
            AssertExistingStateWasNotReplaced(ledger);
        }

        [Test]
        public void StateThatConflictsWithAcceptedFactsRejectsAtomically()
        {
            IdempotentLedger<MoneyVocabulary> ledger = CreateMoneyLedger();
            ledger.Apply(Mutation("transaction.existing-001", MoneyEntry(), 7L));
            LedgerSnapshot<MoneyVocabulary> valid = BuildSourceSnapshot();
            var entries = new[]
            {
                new LedgerSnapshotEntry(
                    valid.Entries[0].EntryTypeId,
                    valid.Entries[0].TargetId,
                    valid.Entries[0].CanonicalPayload,
                    valid.Entries[0].Quantity + 1L),
            };
            LedgerSnapshot<MoneyVocabulary> corrupt =
                LedgerSnapshot<MoneyVocabulary>.CreateCanonical(
                    valid.SchemaVersion,
                    valid.Sequence,
                    entries,
                    valid.Transactions);

            LedgerImportResult result = ledger.ImportSnapshot(corrupt);

            Assert.That(result.Status, Is.EqualTo(LedgerImportStatus.ValidationRejected));
            Assert.That(result.RejectionCode, Is.EqualTo("snapshot-replayed-balance-mismatch"));
            AssertExistingStateWasNotReplaced(ledger);
        }

        [Test]
        public void LargeRepresentativeLedgerRemainsDeterministic()
        {
            IdempotentLedger<MoneyVocabulary> first = CreateMoneyLedger();
            IdempotentLedger<MoneyVocabulary> second = CreateMoneyLedger();

            for (int index = 0; index < 1000; index++)
            {
                string suffix = index.ToString("D4");
                LedgerEntry<MoneyVocabulary> entry = Entry<MoneyVocabulary>(
                    "money.balance",
                    "wallet.account-" + suffix,
                    "bucket=" + suffix);
                LedgerMutation<MoneyVocabulary> mutation = Mutation(
                    "transaction.large-" + suffix,
                    entry,
                    index + 1L,
                    index);
                Assert.That(first.Apply(mutation).Status, Is.EqualTo(LedgerMutationStatus.Applied));
                Assert.That(second.Apply(mutation).Status, Is.EqualTo(LedgerMutationStatus.Applied));
            }

            LedgerSnapshot<MoneyVocabulary> firstSnapshot = first.ExportSnapshot();
            LedgerSnapshot<MoneyVocabulary> secondSnapshot = second.ExportSnapshot();

            Assert.That(firstSnapshot.Sequence, Is.EqualTo(1000L));
            Assert.That(firstSnapshot.Entries.Count, Is.EqualTo(1000));
            Assert.That(firstSnapshot.Transactions.Count, Is.EqualTo(1000));
            Assert.That(firstSnapshot.Fingerprint, Is.EqualTo(secondSnapshot.Fingerprint));
            Assert.That(
                first.ExportSnapshot().Fingerprint,
                Is.EqualTo(firstSnapshot.Fingerprint));
        }

        [Test]
        public void DistinctVocabularyTypesProduceIncompatibleLedgerEntryTypes()
        {
            LedgerEntry<MoneyVocabulary> money = MoneyEntry();
            LedgerEntry<ScrapVocabulary> scrap =
                Entry<ScrapVocabulary>("scrap.balance", "wallet.profile", "");

            Assert.That(
                typeof(LedgerEntry<MoneyVocabulary>),
                Is.Not.EqualTo(typeof(LedgerEntry<ScrapVocabulary>)));
            Assert.That(
                typeof(IdempotentLedger<MoneyVocabulary>),
                Is.Not.EqualTo(typeof(IdempotentLedger<ScrapVocabulary>)));
            Assert.That(money.EntryTypeId, Is.Not.EqualTo(scrap.EntryTypeId));
        }

        [Test]
        public void DomainAssemblyHasNoUnityEngineReference()
        {
            string[] references = typeof(IdempotentLedger<>)
                .Assembly
                .GetReferencedAssemblies()
                .Select(assembly => assembly.Name)
                .ToArray();

            Assert.That(
                references.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        public void ConstructorRequiresExplicitValidatorAndPolicy()
        {
            LedgerMutationValidator<MoneyVocabulary> validator =
                context => LedgerDecision.Accept();
            LedgerMutationPolicy<MoneyVocabulary> policy =
                context => LedgerDecision.Accept();

            Assert.Throws<ArgumentNullException>(() =>
                new IdempotentLedger<MoneyVocabulary>(null, policy));
            Assert.Throws<ArgumentNullException>(() =>
                new IdempotentLedger<MoneyVocabulary>(validator, null));
        }

        [Test]
        public void MutationFingerprintIsFrozenForRepresentativeInput()
        {
            LedgerMutation<MoneyVocabulary> mutation = Mutation(
                "transaction.fingerprint-001",
                Entry<MoneyVocabulary>(
                    "money.balance",
                    "wallet.profile",
                    "reason=reward"),
                125L,
                7L);

            Assert.That(
                mutation.PayloadFingerprint,
                Is.EqualTo("1833c737a623ff6cc93268d78274e18bb153ea672cc77ca7b635e2356f1466b3"));
        }

        private static IdempotentLedger<MoneyVocabulary> CreateMoneyLedger()
        {
            return new IdempotentLedger<MoneyVocabulary>(
                context => LedgerDecision.Accept(),
                context => context.ProposedQuantity < 0L
                    ? LedgerDecision.Reject("insufficient-quantity")
                    : LedgerDecision.Accept());
        }

        private static LedgerSnapshot<MoneyVocabulary> BuildSourceSnapshot()
        {
            IdempotentLedger<MoneyVocabulary> source = CreateMoneyLedger();
            source.Apply(Mutation(
                "transaction.source-001",
                MoneyEntry(),
                12L));
            return source.ExportSnapshot();
        }

        private static void AssertExistingStateWasNotReplaced(
            IdempotentLedger<MoneyVocabulary> ledger)
        {
            Assert.That(ledger.Sequence, Is.EqualTo(1L));
            Assert.That(ledger.GetQuantity(MoneyEntry()), Is.EqualTo(7L));
            Assert.That(ledger.TransactionCount, Is.EqualTo(1));
        }

        private static LedgerEntry<MoneyVocabulary> MoneyEntry()
        {
            return Entry<MoneyVocabulary>(
                "money.balance",
                "wallet.profile",
                "");
        }

        private static LedgerEntry<TVocabulary> Entry<TVocabulary>(
            string entryType,
            string target,
            string payload)
        {
            return new LedgerEntry<TVocabulary>(
                StableId.Parse(entryType),
                StableId.Parse(target),
                payload);
        }

        private static LedgerMutation<TVocabulary> Mutation<TVocabulary>(
            string transactionId,
            LedgerEntry<TVocabulary> entry,
            long delta,
            long? expectedSequence = null)
        {
            return new LedgerMutation<TVocabulary>(
                StableId.Parse(transactionId),
                entry,
                delta,
                expectedSequence);
        }

        private sealed class MoneyVocabulary
        {
        }

        private sealed class ScrapVocabulary
        {
        }
    }
}

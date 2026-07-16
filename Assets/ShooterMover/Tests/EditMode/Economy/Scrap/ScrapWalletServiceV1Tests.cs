using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Contracts.Economy;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Economy.Scrap;

namespace ShooterMover.Tests.EditMode.Economy.Scrap
{
    public sealed class ScrapWalletServiceV1Tests
    {
        private static readonly StableId AuthorityId = Id("authority.scrap-profile");
        private static readonly StableId ScrapCurrencyId = Id("currency.scrap");

        [Test]
        public void PositiveGrantAndBoundedSpendApplyWithImmutableFacts()
        {
            ScrapWalletServiceV1 wallet = CreateWallet();

            ScrapTransactionResultV1 grant = wallet.Apply(Grant(
                "transaction.grant-001",
                50L,
                expectedSequence: 0L));
            ScrapTransactionResultV1 spend = wallet.Apply(Spend(
                "transaction.spend-001",
                20L,
                expectedSequence: 1L));

            Assert.That(grant.Status, Is.EqualTo(EconomyTransactionStatusV1.Applied));
            Assert.That(spend.Status, Is.EqualTo(EconomyTransactionStatusV1.Applied));
            Assert.That(wallet.Balance, Is.EqualTo(30L));
            Assert.That(wallet.Sequence, Is.EqualTo(2L));
            Assert.That(spend.ChangeFact.OriginalPreviousBalance, Is.EqualTo(50L));
            Assert.That(spend.ChangeFact.OriginalResultingBalance, Is.EqualTo(30L));
            Assert.That(spend.ChangeFact.ReasonStableId, Is.EqualTo(ScrapIdentityV1.CraftingSpendReason));
            Assert.That(ScrapFingerprintV1.IsCanonical(spend.ChangeFact.Fingerprint), Is.True);
            Assert.That(spend.EconomyResult.Fingerprint, Is.Not.Empty);
        }

        [Test]
        public void InsufficientSpendIsRecordedAndExactRetryKeepsOriginalFact()
        {
            ScrapWalletServiceV1 wallet = CreateWallet();
            wallet.Apply(Grant("transaction.seed-001", 10L));
            ScrapTransactionCommandV1 rejectedCommand = Spend(
                "transaction.spend-rejected-001",
                11L,
                expectedSequence: 1L);

            ScrapTransactionResultV1 rejected = wallet.Apply(rejectedCommand);
            wallet.Apply(Grant("transaction.seed-002", 100L, expectedSequence: 1L));
            ScrapTransactionResultV1 duplicate = wallet.Apply(rejectedCommand);

            Assert.That(rejected.Status, Is.EqualTo(EconomyTransactionStatusV1.InsufficientValue));
            Assert.That(rejected.ChangeFact.RejectionCode, Is.EqualTo("insufficient-scrap"));
            Assert.That(duplicate.Status, Is.EqualTo(EconomyTransactionStatusV1.ExactDuplicateNoChange));
            Assert.That(duplicate.ChangeFact.OriginalLedgerStatus, Is.EqualTo(LedgerMutationStatus.PolicyRejected));
            Assert.That(duplicate.ChangeFact.OriginalResultingBalance, Is.EqualTo(10L));
            Assert.That(duplicate.ChangeFact.AuthorityBalance, Is.EqualTo(110L));
            Assert.That(wallet.Sequence, Is.EqualTo(2L));
        }

        [TestCase("currency.money")]
        [TestCase("currency.unknown")]
        public void MoneyAndUnknownCurrencyIdentitiesAreRejected(string currencyId)
        {
            ScrapWalletServiceV1 wallet = CreateWallet();
            ScrapTransactionCommandV1 command = Grant(
                "transaction.wrong-currency-001",
                5L,
                currencyStableId: Id(currencyId));

            ScrapTransactionResultV1 first = wallet.Apply(command);
            ScrapTransactionResultV1 duplicate = wallet.Apply(command);

            Assert.That(first.Status, Is.EqualTo(EconomyTransactionStatusV1.InvalidRequest));
            Assert.That(first.ChangeFact.RejectionCode, Is.EqualTo("wrong-currency"));
            Assert.That(duplicate.Status, Is.EqualTo(EconomyTransactionStatusV1.ExactDuplicateNoChange));
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(wallet.Sequence, Is.Zero);
            Assert.That(wallet.TransactionCount, Is.EqualTo(1));
        }

        [Test]
        public void WrongAuthorityAndMalformedProvenanceAreRejected()
        {
            ScrapWalletServiceV1 wallet = CreateWallet();
            ScrapTransactionResultV1 wrongAuthority = wallet.Apply(Grant(
                "transaction.wrong-authority-001",
                3L,
                authorityStableId: Id("authority.money-profile")));
            ScrapTransactionResultV1 malformed = wallet.Apply(new ScrapTransactionCommandV1(
                Id("transaction.malformed-provenance-001"),
                Id("operation.strongbox-open-001"),
                AuthorityId,
                ScrapCurrencyId,
                ScrapMutationKindV1.Grant,
                4L,
                ScrapIdentityV1.StrongboxOpeningReason,
                new ScrapProvenanceV1(
                    ScrapIdentityV1.RewardSourceKind,
                    Id("operation.strongbox-open-001"),
                    Id("strongbox.box-001"))));

            Assert.That(wrongAuthority.Status, Is.EqualTo(EconomyTransactionStatusV1.InvalidRequest));
            Assert.That(wrongAuthority.ChangeFact.RejectionCode, Is.EqualTo("wrong-authority"));
            Assert.That(malformed.Status, Is.EqualTo(EconomyTransactionStatusV1.InvalidRequest));
            Assert.That(malformed.ChangeFact.RejectionCode, Is.EqualTo("provenance-source-kind-mismatch"));
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(wallet.TransactionCount, Is.EqualTo(2));
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        public void InvalidAmountsFailClosedWithoutChangingBalance(long amount)
        {
            ScrapWalletServiceV1 wallet = CreateWallet();
            ScrapTransactionCommandV1 command = Grant(
                "transaction.invalid-amount-001",
                amount);

            ScrapTransactionResultV1 result = wallet.Apply(command);

            Assert.That(result.Status, Is.EqualTo(EconomyTransactionStatusV1.InvalidRequest));
            Assert.That(result.ChangeFact.RejectionCode, Is.EqualTo("invalid-amount"));
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(wallet.Sequence, Is.Zero);
        }

        [Test]
        public void ExactAndConflictingDuplicatesAreDistinct()
        {
            ScrapWalletServiceV1 wallet = CreateWallet();
            ScrapTransactionCommandV1 original = Grant(
                "transaction.duplicate-001",
                25L);
            ScrapTransactionCommandV1 changed = StrongboxGrant(
                "transaction.duplicate-001",
                25L,
                "strongbox.box-duplicate");

            ScrapTransactionResultV1 applied = wallet.Apply(original);
            ScrapTransactionResultV1 exact = wallet.Apply(original);
            ScrapTransactionResultV1 conflict = wallet.Apply(changed);

            Assert.That(applied.Status, Is.EqualTo(EconomyTransactionStatusV1.Applied));
            Assert.That(exact.Status, Is.EqualTo(EconomyTransactionStatusV1.ExactDuplicateNoChange));
            Assert.That(conflict.Status, Is.EqualTo(EconomyTransactionStatusV1.ConflictingDuplicate));
            Assert.That(conflict.ChangeFact.RejectionCode, Is.EqualTo("transaction-payload-conflict"));
            Assert.That(wallet.Balance, Is.EqualTo(25L));
            Assert.That(wallet.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void ExpectedSequenceConflictIsDeterministic()
        {
            ScrapWalletServiceV1 wallet = CreateWallet();
            ScrapTransactionCommandV1 stale = Grant(
                "transaction.sequence-001",
                1L,
                expectedSequence: 1L);

            ScrapTransactionResultV1 first = wallet.Apply(stale);
            ScrapTransactionResultV1 duplicate = wallet.Apply(stale);

            Assert.That(first.Status, Is.EqualTo(EconomyTransactionStatusV1.ExpectedSequenceConflict));
            Assert.That(first.ChangeFact.RejectionCode, Is.EqualTo("expected-sequence-conflict"));
            Assert.That(duplicate.Status, Is.EqualTo(EconomyTransactionStatusV1.ExactDuplicateNoChange));
            Assert.That(duplicate.ChangeFact.OriginalLedgerStatus, Is.EqualTo(LedgerMutationStatus.SequenceConflict));
            Assert.That(wallet.Sequence, Is.Zero);
        }

        [Test]
        public void BalanceOverflowRejectsAndExactRetryIsStable()
        {
            ScrapWalletServiceV1 wallet = CreateWallet();
            wallet.Apply(Grant("transaction.max-001", long.MaxValue));
            ScrapTransactionCommandV1 overflow = Grant("transaction.max-002", 1L);

            ScrapTransactionResultV1 first = wallet.Apply(overflow);
            ScrapTransactionResultV1 duplicate = wallet.Apply(overflow);

            Assert.That(first.Status, Is.EqualTo(EconomyTransactionStatusV1.InvalidRequest));
            Assert.That(first.ChangeFact.RejectionCode, Is.EqualTo("balance-overflow"));
            Assert.That(duplicate.Status, Is.EqualTo(EconomyTransactionStatusV1.ExactDuplicateNoChange));
            Assert.That(wallet.Balance, Is.EqualTo(long.MaxValue));
            Assert.That(wallet.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void StrongboxAndFutureSalvageReasonsRoundTripExactly()
        {
            ScrapWalletServiceV1 source = CreateWallet();
            source.Apply(StrongboxGrant(
                "transaction.strongbox-001",
                7L,
                "strongbox.box-001"));
            source.Apply(SalvageGrant(
                "transaction.salvage-001",
                3L,
                "equipment-instance.weapon-001"));
            ScrapSnapshotV1 snapshot = source.ExportSnapshot();

            ScrapWalletServiceV1 restored = CreateWallet();
            ScrapSnapshotImportResultV1 imported = restored.ImportSnapshot(snapshot);
            ScrapSnapshotV1 reexported = restored.ExportSnapshot();
            ScrapLedgerPayloadV1[] payloads = reexported.LedgerSnapshot.Transactions
                .Select(transaction => ParsePayload(transaction.CanonicalPayload))
                .ToArray();

            Assert.That(imported.Succeeded, Is.True);
            Assert.That(restored.Balance, Is.EqualTo(10L));
            Assert.That(reexported.Fingerprint, Is.EqualTo(snapshot.Fingerprint));
            Assert.That(payloads.Any(payload =>
                payload.ReasonStableId == ScrapIdentityV1.StrongboxOpeningReason
                && payload.Provenance.SourceKindStableId == ScrapIdentityV1.StrongboxSourceKind), Is.True);
            Assert.That(payloads.Any(payload =>
                payload.ReasonStableId == ScrapIdentityV1.FutureSalvageReason
                && payload.Provenance.SourceKindStableId == ScrapIdentityV1.EquipmentSourceKind), Is.True);
        }

        [Test]
        public void CorruptImportIsRejectedAtomically()
        {
            ScrapWalletServiceV1 source = CreateWallet();
            source.Apply(Grant("transaction.snapshot-source-001", 12L));
            ScrapSnapshotV1 valid = source.ExportSnapshot();
            LedgerSnapshot<ScrapLedgerVocabulary> corruptLedger =
                new LedgerSnapshot<ScrapLedgerVocabulary>(
                    valid.LedgerSnapshot.SchemaVersion,
                    valid.LedgerSnapshot.Sequence,
                    valid.LedgerSnapshot.Entries,
                    valid.LedgerSnapshot.Transactions,
                    "sha256:" + new string('0', 64));
            var corrupt = new ScrapSnapshotV1(
                valid.SchemaVersion,
                valid.AuthorityStableId,
                valid.CurrencyStableId,
                valid.Balance,
                corruptLedger,
                ScrapSnapshotV1.ComputeFingerprint(
                    valid.SchemaVersion,
                    valid.AuthorityStableId,
                    valid.CurrencyStableId,
                    valid.Balance,
                    corruptLedger));

            ScrapWalletServiceV1 target = CreateWallet();
            target.Apply(Grant("transaction.target-existing-001", 5L));
            ScrapSnapshotImportResultV1 result = target.ImportSnapshot(corrupt);

            Assert.That(result.Status, Is.EqualTo(LedgerImportStatus.FingerprintMismatch));
            Assert.That(target.Balance, Is.EqualTo(5L));
            Assert.That(target.Sequence, Is.EqualTo(1L));
            Assert.That(target.TransactionCount, Is.EqualTo(1));
        }

        [Test]
        public void SnapshotCollectionsAreImmutableAndDetached()
        {
            ScrapWalletServiceV1 wallet = CreateWallet();
            wallet.Apply(Grant("transaction.immutable-001", 9L));
            ScrapSnapshotV1 snapshot = wallet.ExportSnapshot();

            Assert.Throws<NotSupportedException>(() =>
                ((IList<LedgerSnapshotEntry>)snapshot.LedgerSnapshot.Entries).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<LedgerTransactionSnapshot>)snapshot.LedgerSnapshot.Transactions).Clear());

            wallet.Apply(Grant("transaction.immutable-002", 1L));

            Assert.That(snapshot.Balance, Is.EqualTo(9L));
            Assert.That(snapshot.LedgerSnapshot.Sequence, Is.EqualTo(1L));
            Assert.That(snapshot.LedgerSnapshot.Transactions.Count, Is.EqualTo(1));
            Assert.That(ScrapFingerprintV1.IsCanonical(snapshot.Fingerprint), Is.True);
        }

        private static ScrapWalletServiceV1 CreateWallet()
        {
            return new ScrapWalletServiceV1(AuthorityId, ScrapCurrencyId);
        }

        private static ScrapTransactionCommandV1 Grant(
            string transactionId,
            long amount,
            long? expectedSequence = null,
            StableId currencyStableId = null,
            StableId authorityStableId = null)
        {
            StableId operationId = Id("operation.reward-001");
            return new ScrapTransactionCommandV1(
                Id(transactionId),
                operationId,
                authorityStableId ?? AuthorityId,
                currencyStableId ?? ScrapCurrencyId,
                ScrapMutationKindV1.Grant,
                amount,
                ScrapIdentityV1.RewardGrantReason,
                new ScrapProvenanceV1(
                    ScrapIdentityV1.RewardSourceKind,
                    operationId,
                    Id("commitment.reward-001")),
                expectedSequence);
        }

        private static ScrapTransactionCommandV1 Spend(
            string transactionId,
            long amount,
            long? expectedSequence = null)
        {
            StableId operationId = Id("operation.craft-001");
            return new ScrapTransactionCommandV1(
                Id(transactionId),
                operationId,
                AuthorityId,
                ScrapCurrencyId,
                ScrapMutationKindV1.Spend,
                amount,
                ScrapIdentityV1.CraftingSpendReason,
                new ScrapProvenanceV1(
                    ScrapIdentityV1.CraftingSourceKind,
                    operationId,
                    Id("recipe.weapon-001")),
                expectedSequence);
        }

        private static ScrapTransactionCommandV1 StrongboxGrant(
            string transactionId,
            long amount,
            string strongboxInstanceId)
        {
            StableId operationId = Id("operation.strongbox-open-001");
            return new ScrapTransactionCommandV1(
                Id(transactionId),
                operationId,
                AuthorityId,
                ScrapCurrencyId,
                ScrapMutationKindV1.Grant,
                amount,
                ScrapIdentityV1.StrongboxOpeningReason,
                new ScrapProvenanceV1(
                    ScrapIdentityV1.StrongboxSourceKind,
                    operationId,
                    Id(strongboxInstanceId)));
        }

        private static ScrapTransactionCommandV1 SalvageGrant(
            string transactionId,
            long amount,
            string equipmentInstanceId)
        {
            StableId operationId = Id("operation.salvage-001");
            return new ScrapTransactionCommandV1(
                Id(transactionId),
                operationId,
                AuthorityId,
                ScrapCurrencyId,
                ScrapMutationKindV1.Grant,
                amount,
                ScrapIdentityV1.FutureSalvageReason,
                new ScrapProvenanceV1(
                    ScrapIdentityV1.EquipmentSourceKind,
                    operationId,
                    Id(equipmentInstanceId)));
        }

        private static ScrapLedgerPayloadV1 ParsePayload(string canonicalPayload)
        {
            ScrapLedgerPayloadV1 payload;
            string rejectionCode;
            Assert.That(
                ScrapLedgerPayloadV1.TryParse(canonicalPayload, out payload, out rejectionCode),
                Is.True,
                rejectionCode);
            return payload;
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }
    }
}

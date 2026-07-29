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
    public sealed class ScrapWalletActionsTests
    {
        private static readonly StableId AuthorityId = Id("authority.scrap-profile");
        private static readonly StableId ScrapCurrencyId = Id("currency.scrap");

        [Test]
        public void PositiveGrantAndBoundedSpendApplyWithImmutableFacts()
        {
            ScrapWalletActions wallet = CreateWallet();

            ScrapTransactionResult grant = wallet.Apply(Grant(
                "transaction.grant-001",
                50L,
                expectedSequence: 0L));
            ScrapTransactionResult spend = wallet.Apply(Spend(
                "transaction.spend-001",
                20L,
                expectedSequence: 1L));

            Assert.That(grant.Status, Is.EqualTo(EconomyTransactionStatus.Applied));
            Assert.That(spend.Status, Is.EqualTo(EconomyTransactionStatus.Applied));
            Assert.That(wallet.Balance, Is.EqualTo(30L));
            Assert.That(wallet.Sequence, Is.EqualTo(2L));
            Assert.That(spend.ChangeFact.OriginalPreviousBalance, Is.EqualTo(50L));
            Assert.That(spend.ChangeFact.OriginalResultingBalance, Is.EqualTo(30L));
            Assert.That(spend.ChangeFact.ReasonStableId, Is.EqualTo(ScrapIdentity.CraftingSpendReason));
            Assert.That(ScrapFingerprint.IsCanonical(spend.ChangeFact.Fingerprint), Is.True);
            Assert.That(spend.EconomyResult.Fingerprint, Is.Not.Empty);
        }

        [Test]
        public void InsufficientSpendIsRecordedAndExactRetryKeepsOriginalFact()
        {
            ScrapWalletActions wallet = CreateWallet();
            wallet.Apply(Grant("transaction.seed-001", 10L));
            ScrapTransactionCommand rejectedCommand = Spend(
                "transaction.spend-rejected-001",
                11L,
                expectedSequence: 1L);

            ScrapTransactionResult rejected = wallet.Apply(rejectedCommand);
            wallet.Apply(Grant("transaction.seed-002", 100L, expectedSequence: 1L));
            ScrapTransactionResult duplicate = wallet.Apply(rejectedCommand);

            Assert.That(rejected.Status, Is.EqualTo(EconomyTransactionStatus.InsufficientValue));
            Assert.That(rejected.ChangeFact.RejectionCode, Is.EqualTo("insufficient-scrap"));
            Assert.That(duplicate.Status, Is.EqualTo(EconomyTransactionStatus.ExactDuplicateNoChange));
            Assert.That(duplicate.ChangeFact.OriginalLedgerStatus, Is.EqualTo(LedgerMutationStatus.PolicyRejected));
            Assert.That(duplicate.ChangeFact.OriginalResultingBalance, Is.EqualTo(10L));
            Assert.That(duplicate.ChangeFact.AuthorityBalance, Is.EqualTo(110L));
            Assert.That(wallet.Sequence, Is.EqualTo(2L));
        }

        [TestCase("currency.money")]
        [TestCase("currency.unknown")]
        public void MoneyAndUnknownCurrencyIdentitiesAreRejected(string currencyId)
        {
            ScrapWalletActions wallet = CreateWallet();
            ScrapTransactionCommand command = Grant(
                "transaction.wrong-currency-001",
                5L,
                currencyStableId: Id(currencyId));

            ScrapTransactionResult first = wallet.Apply(command);
            ScrapTransactionResult duplicate = wallet.Apply(command);

            Assert.That(first.Status, Is.EqualTo(EconomyTransactionStatus.InvalidRequest));
            Assert.That(first.ChangeFact.RejectionCode, Is.EqualTo("wrong-currency"));
            Assert.That(duplicate.Status, Is.EqualTo(EconomyTransactionStatus.ExactDuplicateNoChange));
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(wallet.Sequence, Is.Zero);
            Assert.That(wallet.TransactionCount, Is.EqualTo(1));
        }

        [Test]
        public void WrongAuthorityAndMalformedProvenanceAreRejected()
        {
            ScrapWalletActions wallet = CreateWallet();
            ScrapTransactionResult wrongAuthority = wallet.Apply(Grant(
                "transaction.wrong-authority-001",
                3L,
                authorityStableId: Id("authority.money-profile")));
            ScrapTransactionResult malformed = wallet.Apply(new ScrapTransactionCommand(
                Id("transaction.malformed-provenance-001"),
                Id("operation.strongbox-open-001"),
                AuthorityId,
                ScrapCurrencyId,
                ScrapMutationKind.Grant,
                4L,
                ScrapIdentity.StrongboxOpeningReason,
                new ScrapProvenance(
                    ScrapIdentity.RewardSourceKind,
                    Id("operation.strongbox-open-001"),
                    Id("strongbox.box-001"))));

            Assert.That(wrongAuthority.Status, Is.EqualTo(EconomyTransactionStatus.InvalidRequest));
            Assert.That(wrongAuthority.ChangeFact.RejectionCode, Is.EqualTo("wrong-authority"));
            Assert.That(malformed.Status, Is.EqualTo(EconomyTransactionStatus.InvalidRequest));
            Assert.That(malformed.ChangeFact.RejectionCode, Is.EqualTo("provenance-source-kind-mismatch"));
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(wallet.TransactionCount, Is.EqualTo(2));
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        public void InvalidAmountsFailClosedWithoutChangingBalance(long amount)
        {
            ScrapWalletActions wallet = CreateWallet();
            ScrapTransactionCommand command = Grant(
                "transaction.invalid-amount-001",
                amount);

            ScrapTransactionResult result = wallet.Apply(command);

            Assert.That(result.Status, Is.EqualTo(EconomyTransactionStatus.InvalidRequest));
            Assert.That(result.ChangeFact.RejectionCode, Is.EqualTo("invalid-amount"));
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(wallet.Sequence, Is.Zero);
        }

        [Test]
        public void ExactAndConflictingDuplicatesAreDistinct()
        {
            ScrapWalletActions wallet = CreateWallet();
            ScrapTransactionCommand original = Grant(
                "transaction.duplicate-001",
                25L);
            ScrapTransactionCommand changed = StrongboxGrant(
                "transaction.duplicate-001",
                25L,
                "strongbox.box-duplicate");

            ScrapTransactionResult applied = wallet.Apply(original);
            ScrapTransactionResult exact = wallet.Apply(original);
            ScrapTransactionResult conflict = wallet.Apply(changed);

            Assert.That(applied.Status, Is.EqualTo(EconomyTransactionStatus.Applied));
            Assert.That(exact.Status, Is.EqualTo(EconomyTransactionStatus.ExactDuplicateNoChange));
            Assert.That(conflict.Status, Is.EqualTo(EconomyTransactionStatus.ConflictingDuplicate));
            Assert.That(conflict.ChangeFact.RejectionCode, Is.EqualTo("transaction-payload-conflict"));
            Assert.That(wallet.Balance, Is.EqualTo(25L));
            Assert.That(wallet.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void ExpectedSequenceConflictIsDeterministic()
        {
            ScrapWalletActions wallet = CreateWallet();
            ScrapTransactionCommand stale = Grant(
                "transaction.sequence-001",
                1L,
                expectedSequence: 1L);

            ScrapTransactionResult first = wallet.Apply(stale);
            ScrapTransactionResult duplicate = wallet.Apply(stale);

            Assert.That(first.Status, Is.EqualTo(EconomyTransactionStatus.ExpectedSequenceConflict));
            Assert.That(first.ChangeFact.RejectionCode, Is.EqualTo("expected-sequence-conflict"));
            Assert.That(duplicate.Status, Is.EqualTo(EconomyTransactionStatus.ExactDuplicateNoChange));
            Assert.That(duplicate.ChangeFact.OriginalLedgerStatus, Is.EqualTo(LedgerMutationStatus.SequenceConflict));
            Assert.That(wallet.Sequence, Is.Zero);
        }

        [Test]
        public void BalanceOverflowRejectsAndExactRetryIsStable()
        {
            ScrapWalletActions wallet = CreateWallet();
            wallet.Apply(Grant("transaction.max-001", long.MaxValue));
            ScrapTransactionCommand overflow = Grant("transaction.max-002", 1L);

            ScrapTransactionResult first = wallet.Apply(overflow);
            ScrapTransactionResult duplicate = wallet.Apply(overflow);

            Assert.That(first.Status, Is.EqualTo(EconomyTransactionStatus.InvalidRequest));
            Assert.That(first.ChangeFact.RejectionCode, Is.EqualTo("balance-overflow"));
            Assert.That(duplicate.Status, Is.EqualTo(EconomyTransactionStatus.ExactDuplicateNoChange));
            Assert.That(wallet.Balance, Is.EqualTo(long.MaxValue));
            Assert.That(wallet.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void StrongboxAndFutureSalvageReasonsRoundTripExactly()
        {
            ScrapWalletActions source = CreateWallet();
            source.Apply(StrongboxGrant(
                "transaction.strongbox-001",
                7L,
                "strongbox.box-001"));
            source.Apply(SalvageGrant(
                "transaction.salvage-001",
                3L,
                "equipment-instance.weapon-001"));
            ScrapSnapshot snapshot = source.ExportSnapshot();

            ScrapWalletActions restored = CreateWallet();
            ScrapSnapshotImportResult imported = restored.ImportSnapshot(snapshot);
            ScrapSnapshot reexported = restored.ExportSnapshot();
            ScrapLedgerPayload[] payloads = reexported.LedgerSnapshot.Transactions
                .Select(transaction => ParsePayload(transaction.CanonicalPayload))
                .ToArray();

            Assert.That(imported.Succeeded, Is.True);
            Assert.That(restored.Balance, Is.EqualTo(10L));
            Assert.That(reexported.Fingerprint, Is.EqualTo(snapshot.Fingerprint));
            Assert.That(payloads.Any(payload =>
                payload.ReasonStableId == ScrapIdentity.StrongboxOpeningReason
                && payload.Provenance.SourceKindStableId == ScrapIdentity.StrongboxSourceKind), Is.True);
            Assert.That(payloads.Any(payload =>
                payload.ReasonStableId == ScrapIdentity.FutureSalvageReason
                && payload.Provenance.SourceKindStableId == ScrapIdentity.EquipmentSourceKind), Is.True);
        }

        [Test]
        public void CorruptImportIsRejectedAtomically()
        {
            ScrapWalletActions source = CreateWallet();
            source.Apply(Grant("transaction.snapshot-source-001", 12L));
            ScrapSnapshot valid = source.ExportSnapshot();
            LedgerSnapshot<ScrapLedgerVocabulary> corruptLedger =
                new LedgerSnapshot<ScrapLedgerVocabulary>(
                    valid.LedgerSnapshot.SchemaVersion,
                    valid.LedgerSnapshot.Sequence,
                    valid.LedgerSnapshot.Entries,
                    valid.LedgerSnapshot.Transactions,
                    "sha256:" + new string('0', 64));
            var corrupt = new ScrapSnapshot(
                valid.SchemaVersion,
                valid.AuthorityStableId,
                valid.CurrencyStableId,
                valid.Balance,
                corruptLedger,
                ScrapSnapshot.ComputeFingerprint(
                    valid.SchemaVersion,
                    valid.AuthorityStableId,
                    valid.CurrencyStableId,
                    valid.Balance,
                    corruptLedger));

            ScrapWalletActions target = CreateWallet();
            target.Apply(Grant("transaction.target-existing-001", 5L));
            ScrapSnapshotImportResult result = target.ImportSnapshot(corrupt);

            Assert.That(result.Status, Is.EqualTo(LedgerImportStatus.FingerprintMismatch));
            Assert.That(target.Balance, Is.EqualTo(5L));
            Assert.That(target.Sequence, Is.EqualTo(1L));
            Assert.That(target.TransactionCount, Is.EqualTo(1));
        }

        [Test]
        public void SnapshotCollectionsAreImmutableAndDetached()
        {
            ScrapWalletActions wallet = CreateWallet();
            wallet.Apply(Grant("transaction.immutable-001", 9L));
            ScrapSnapshot snapshot = wallet.ExportSnapshot();

            Assert.Throws<NotSupportedException>(() =>
                ((IList<LedgerSnapshotEntry>)snapshot.LedgerSnapshot.Entries).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<LedgerTransactionSnapshot>)snapshot.LedgerSnapshot.Transactions).Clear());

            wallet.Apply(Grant("transaction.immutable-002", 1L));

            Assert.That(snapshot.Balance, Is.EqualTo(9L));
            Assert.That(snapshot.LedgerSnapshot.Sequence, Is.EqualTo(1L));
            Assert.That(snapshot.LedgerSnapshot.Transactions.Count, Is.EqualTo(1));
            Assert.That(ScrapFingerprint.IsCanonical(snapshot.Fingerprint), Is.True);
        }

        private static ScrapWalletActions CreateWallet()
        {
            return new ScrapWalletActions(AuthorityId, ScrapCurrencyId);
        }

        private static ScrapTransactionCommand Grant(
            string transactionId,
            long amount,
            long? expectedSequence = null,
            StableId currencyStableId = null,
            StableId authorityStableId = null)
        {
            StableId operationId = Id("operation.reward-001");
            return new ScrapTransactionCommand(
                Id(transactionId),
                operationId,
                authorityStableId ?? AuthorityId,
                currencyStableId ?? ScrapCurrencyId,
                ScrapMutationKind.Grant,
                amount,
                ScrapIdentity.RewardGrantReason,
                new ScrapProvenance(
                    ScrapIdentity.RewardSourceKind,
                    operationId,
                    Id("commitment.reward-001")),
                expectedSequence);
        }

        private static ScrapTransactionCommand Spend(
            string transactionId,
            long amount,
            long? expectedSequence = null)
        {
            StableId operationId = Id("operation.craft-001");
            return new ScrapTransactionCommand(
                Id(transactionId),
                operationId,
                AuthorityId,
                ScrapCurrencyId,
                ScrapMutationKind.Spend,
                amount,
                ScrapIdentity.CraftingSpendReason,
                new ScrapProvenance(
                    ScrapIdentity.CraftingSourceKind,
                    operationId,
                    Id("recipe.weapon-001")),
                expectedSequence);
        }

        private static ScrapTransactionCommand StrongboxGrant(
            string transactionId,
            long amount,
            string strongboxInstanceId)
        {
            StableId operationId = Id("operation.strongbox-open-001");
            return new ScrapTransactionCommand(
                Id(transactionId),
                operationId,
                AuthorityId,
                ScrapCurrencyId,
                ScrapMutationKind.Grant,
                amount,
                ScrapIdentity.StrongboxOpeningReason,
                new ScrapProvenance(
                    ScrapIdentity.StrongboxSourceKind,
                    operationId,
                    Id(strongboxInstanceId)));
        }

        private static ScrapTransactionCommand SalvageGrant(
            string transactionId,
            long amount,
            string equipmentInstanceId)
        {
            StableId operationId = Id("operation.salvage-001");
            return new ScrapTransactionCommand(
                Id(transactionId),
                operationId,
                AuthorityId,
                ScrapCurrencyId,
                ScrapMutationKind.Grant,
                amount,
                ScrapIdentity.FutureSalvageReason,
                new ScrapProvenance(
                    ScrapIdentity.EquipmentSourceKind,
                    operationId,
                    Id(equipmentInstanceId)));
        }

        private static ScrapLedgerPayload ParsePayload(string canonicalPayload)
        {
            ScrapLedgerPayload payload;
            string rejectionCode;
            Assert.That(
                ScrapLedgerPayload.TryParse(canonicalPayload, out payload, out rejectionCode),
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

using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Economy.Money;

namespace ShooterMover.Application.Economy.Money
{
    public sealed partial class MoneyWalletService
    {
        public MoneyWalletImportResult ImportSnapshot(
            MoneyWalletSnapshot snapshot)
        {
            MoneyWalletSnapshot previousSnapshot = CurrentSnapshot;
            MoneyWalletImportStatus validationStatus;
            string validationCode;
            if (!TryValidateMoneySnapshot(
                snapshot,
                out validationStatus,
                out validationCode))
            {
                return new MoneyWalletImportResult(
                    validationStatus,
                    validationCode,
                    previousSnapshot,
                    previousSnapshot);
            }

            LedgerImportResult ledgerResult =
                ledger.ImportSnapshot(CreateLedgerSnapshot(snapshot));
            MoneyWalletImportStatus status = MapImportStatus(ledgerResult.Status);
            if (!ledgerResult.Succeeded)
            {
                return new MoneyWalletImportResult(
                    status,
                    ledgerResult.RejectionCode,
                    previousSnapshot,
                    previousSnapshot);
            }

            MoneyWalletSnapshot currentSnapshot = CurrentSnapshot;
            return new MoneyWalletImportResult(
                MoneyWalletImportStatus.Imported,
                null,
                previousSnapshot,
                currentSnapshot);
        }

        private static MoneyWalletSnapshot CreateMoneySnapshot(
            LedgerSnapshot<MoneyLedgerVocabulary> snapshot)
        {
            var contributions =
                new System.Collections.Generic.List<MoneyWalletContributionSnapshot>(
                    snapshot.Entries.Count);
            for (int index = 0; index < snapshot.Entries.Count; index++)
            {
                LedgerSnapshotEntry entry = snapshot.Entries[index];
                contributions.Add(new MoneyWalletContributionSnapshot(
                    entry.TargetId,
                    entry.CanonicalPayload,
                    entry.Quantity));
            }

            var transactions =
                new System.Collections.Generic.List<MoneyWalletTransactionSnapshot>(
                    snapshot.Transactions.Count);
            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                LedgerTransactionSnapshot transaction = snapshot.Transactions[index];
                transactions.Add(new MoneyWalletTransactionSnapshot(
                    transaction.TransactionId,
                    transaction.TargetId,
                    transaction.CanonicalPayload,
                    transaction.QuantityDelta,
                    transaction.ExpectedSequence,
                    transaction.PayloadFingerprint,
                    ToRecordedOutcome(transaction.OriginalStatus),
                    transaction.SequenceBefore,
                    transaction.SequenceAfter,
                    transaction.PreviousQuantity,
                    transaction.CurrentQuantity,
                    transaction.RejectionCode));
            }

            long balance;
            if (!TryReadBalance(snapshot, out balance))
            {
                throw new InvalidOperationException(
                    "The private money ledger contains an invalid aggregate balance.");
            }

            return new MoneyWalletSnapshot(
                snapshot.SchemaVersion,
                snapshot.Sequence,
                balance,
                contributions,
                transactions,
                snapshot.Fingerprint);
        }

        private static LedgerSnapshot<MoneyLedgerVocabulary> CreateLedgerSnapshot(
            MoneyWalletSnapshot snapshot)
        {
            var entries =
                new System.Collections.Generic.List<LedgerSnapshotEntry>(
                    snapshot.Contributions.Count);
            for (int index = 0; index < snapshot.Contributions.Count; index++)
            {
                MoneyWalletContributionSnapshot contribution =
                    snapshot.Contributions[index];
                entries.Add(new LedgerSnapshotEntry(
                    MoneyWalletIdsV1.EntryTypeStableId.ToString(),
                    contribution.CurrencyStableId,
                    contribution.CommandFingerprint,
                    contribution.Quantity));
            }

            var transactions =
                new System.Collections.Generic.List<LedgerTransactionSnapshot>(
                    snapshot.Transactions.Count);
            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                MoneyWalletTransactionSnapshot transaction =
                    snapshot.Transactions[index];
                transactions.Add(new LedgerTransactionSnapshot(
                    transaction.TransactionStableId,
                    MoneyWalletIdsV1.EntryTypeStableId.ToString(),
                    transaction.CurrencyStableId,
                    transaction.CommandFingerprint,
                    transaction.QuantityDelta,
                    transaction.ExpectedSequence,
                    transaction.MutationFingerprint,
                    ToLedgerStatus(transaction.RecordedOutcome),
                    transaction.SequenceBefore,
                    transaction.SequenceAfter,
                    transaction.PreviousContribution,
                    transaction.CurrentContribution,
                    transaction.RejectionCode));
            }

            return new LedgerSnapshot<MoneyLedgerVocabulary>(
                snapshot.SchemaVersion,
                snapshot.Sequence,
                entries,
                transactions,
                snapshot.Fingerprint);
        }

        private static MoneyWalletRecordedOutcome ToRecordedOutcome(
            LedgerMutationStatus status)
        {
            switch (status)
            {
                case LedgerMutationStatus.Applied:
                    return MoneyWalletRecordedOutcome.Applied;
                case LedgerMutationStatus.SequenceConflict:
                    return MoneyWalletRecordedOutcome.SequenceConflict;
                case LedgerMutationStatus.ValidationRejected:
                    return MoneyWalletRecordedOutcome.ValidationRejected;
                case LedgerMutationStatus.PolicyRejected:
                    return MoneyWalletRecordedOutcome.PolicyRejected;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(status),
                        status,
                        "Only original terminal ledger outcomes may be snapshotted.");
            }
        }

        private static LedgerMutationStatus ToLedgerStatus(
            MoneyWalletRecordedOutcome outcome)
        {
            switch (outcome)
            {
                case MoneyWalletRecordedOutcome.Applied:
                    return LedgerMutationStatus.Applied;
                case MoneyWalletRecordedOutcome.SequenceConflict:
                    return LedgerMutationStatus.SequenceConflict;
                case MoneyWalletRecordedOutcome.ValidationRejected:
                    return LedgerMutationStatus.ValidationRejected;
                case MoneyWalletRecordedOutcome.PolicyRejected:
                    return LedgerMutationStatus.PolicyRejected;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(outcome),
                        outcome,
                        "Unknown money wallet recorded outcome.");
            }
        }

        private static bool TryValidateMoneySnapshot(
            MoneyWalletSnapshot snapshot,
            out MoneyWalletImportStatus status,
            out string rejectionCode)
        {
            if (snapshot == null)
            {
                status = MoneyWalletImportStatus.ValidationRejected;
                rejectionCode = "money-snapshot-null";
                return false;
            }

            if (snapshot.SchemaVersion
                != MoneyWalletSnapshot.CurrentSchemaVersion)
            {
                status = MoneyWalletImportStatus.UnsupportedSchemaVersion;
                rejectionCode = "money-snapshot-schema-unsupported";
                return false;
            }

            if (snapshot.Sequence < 0L)
            {
                status = MoneyWalletImportStatus.ValidationRejected;
                rejectionCode = "money-snapshot-sequence-negative";
                return false;
            }

            if (!IsCanonicalLedgerFingerprint(snapshot.Fingerprint))
            {
                status = MoneyWalletImportStatus.FingerprintMismatch;
                rejectionCode = "money-snapshot-fingerprint-invalid";
                return false;
            }

            long computedBalance = 0L;
            for (int index = 0; index < snapshot.Contributions.Count; index++)
            {
                MoneyWalletContributionSnapshot contribution =
                    snapshot.Contributions[index];
                if (contribution == null)
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode = "money-snapshot-contribution-null";
                    return false;
                }

                StableId currencyStableId;
                if (!StableId.TryParse(
                    contribution.CurrencyStableId,
                    out currencyStableId)
                    || currencyStableId != MoneyWalletIdsV1.CurrencyStableId)
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode = "money-snapshot-contribution-currency-invalid";
                    return false;
                }

                if (!IsCanonicalFingerprint(
                    contribution.CommandFingerprint))
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode =
                        "money-snapshot-contribution-fingerprint-invalid";
                    return false;
                }

                if (contribution.Quantity == 0L)
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode = "money-snapshot-contribution-zero";
                    return false;
                }

                try
                {
                    computedBalance = checked(
                        computedBalance + contribution.Quantity);
                }
                catch (OverflowException)
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode = BalanceOverflowCode;
                    return false;
                }
            }

            if (computedBalance < 0L
                || computedBalance != snapshot.Balance)
            {
                status = MoneyWalletImportStatus.ValidationRejected;
                rejectionCode = "money-snapshot-balance-mismatch";
                return false;
            }

            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                MoneyWalletTransactionSnapshot transaction =
                    snapshot.Transactions[index];
                if (transaction == null)
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode = "money-snapshot-transaction-null";
                    return false;
                }

                StableId transactionId;
                StableId currencyId;
                if (!StableId.TryParse(
                    transaction.TransactionStableId,
                    out transactionId)
                    || !StableId.TryParse(
                        transaction.CurrencyStableId,
                        out currencyId))
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode = "money-snapshot-transaction-identity-invalid";
                    return false;
                }

                if (!IsCanonicalFingerprint(transaction.CommandFingerprint)
                    || !IsCanonicalLedgerFingerprint(
                        transaction.MutationFingerprint))
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode =
                        "money-snapshot-transaction-fingerprint-invalid";
                    return false;
                }

                if (!Enum.IsDefined(
                    typeof(MoneyWalletRecordedOutcome),
                    transaction.RecordedOutcome))
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode = "money-snapshot-transaction-outcome-invalid";
                    return false;
                }

                if (transaction.QuantityDelta == 0L
                    || (transaction.ExpectedSequence.HasValue
                        && transaction.ExpectedSequence.Value < 0L)
                    || transaction.SequenceBefore < 0L
                    || transaction.SequenceAfter < transaction.SequenceBefore)
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode = "money-snapshot-transaction-fact-invalid";
                    return false;
                }

                bool applied = transaction.RecordedOutcome
                    == MoneyWalletRecordedOutcome.Applied;
                if ((applied
                        && transaction.SequenceAfter
                            != transaction.SequenceBefore + 1L)
                    || (!applied
                        && transaction.SequenceAfter
                            != transaction.SequenceBefore))
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode = "money-snapshot-transaction-sequence-invalid";
                    return false;
                }

                bool wrongCurrencyRejection =
                    transaction.RecordedOutcome
                        == MoneyWalletRecordedOutcome.ValidationRejected
                    && string.Equals(
                        transaction.RejectionCode,
                        WrongCurrencyCode,
                        StringComparison.Ordinal);
                if (currencyId != MoneyWalletIdsV1.CurrencyStableId
                    && !wrongCurrencyRejection)
                {
                    status = MoneyWalletImportStatus.ValidationRejected;
                    rejectionCode = "money-snapshot-transaction-currency-invalid";
                    return false;
                }
            }

            status = MoneyWalletImportStatus.Imported;
            rejectionCode = null;
            return true;
        }

        private static bool IsCanonicalLedgerFingerprint(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                bool digit = current >= '0' && current <= '9';
                bool lowerHex = current >= 'a' && current <= 'f';
                if (!digit && !lowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static MoneyWalletImportStatus MapImportStatus(
            LedgerImportStatus status)
        {
            switch (status)
            {
                case LedgerImportStatus.Imported:
                    return MoneyWalletImportStatus.Imported;
                case LedgerImportStatus.UnsupportedSchemaVersion:
                    return MoneyWalletImportStatus.UnsupportedSchemaVersion;
                case LedgerImportStatus.FingerprintMismatch:
                    return MoneyWalletImportStatus.FingerprintMismatch;
                default:
                    return MoneyWalletImportStatus.ValidationRejected;
            }
        }
    }
}

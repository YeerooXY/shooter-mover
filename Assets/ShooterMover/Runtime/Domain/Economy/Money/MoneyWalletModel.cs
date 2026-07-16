using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;

namespace ShooterMover.Domain.Economy.Money
{
    /// <summary>
    /// Compile-time marker that keeps money ledger entries separate from scrap and holdings.
    /// </summary>
    public sealed class MoneyLedgerVocabulary
    {
        private MoneyLedgerVocabulary()
        {
        }
    }

    public static class MoneyWalletIdsV1
    {
        public static readonly StableId AuthorityStableId =
            StableId.Parse("authority.money");

        public static readonly StableId CurrencyStableId =
            StableId.Parse("currency.money");

        public static readonly StableId EntryTypeStableId =
            StableId.Parse("money.balance");
    }

    public enum MoneyTransactionKind
    {
        Grant = 1,
        Spend = 2,
    }

    public enum MoneyWalletTransactionStatus
    {
        Applied = 1,
        DuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidRequest = 4,
        InvalidAmount = 5,
        WrongCurrency = 6,
        InsufficientFunds = 7,
        SequenceConflict = 8,
    }

    public enum MoneyWalletRecordedOutcome
    {
        Applied = 1,
        SequenceConflict = 2,
        ValidationRejected = 3,
        PolicyRejected = 4,
    }

    public enum MoneyWalletImportStatus
    {
        Imported = 1,
        ValidationRejected = 2,
        UnsupportedSchemaVersion = 3,
        FingerprintMismatch = 4,
    }

    /// <summary>
    /// One transaction-specific contribution retained for deterministic replay.
    /// Consumers should use MoneyWalletSnapshot.Balance rather than summing these values.
    /// </summary>
    public sealed class MoneyWalletContributionSnapshot
    {
        public MoneyWalletContributionSnapshot(
            string currencyStableId,
            string commandFingerprint,
            long quantity)
        {
            CurrencyStableId = currencyStableId;
            CommandFingerprint = commandFingerprint;
            Quantity = quantity;
        }

        public string CurrencyStableId { get; }

        public string CommandFingerprint { get; }

        public long Quantity { get; }
    }

    /// <summary>
    /// Money-specific immutable transaction fact used by snapshot import/export.
    /// </summary>
    public sealed class MoneyWalletTransactionSnapshot
    {
        public MoneyWalletTransactionSnapshot(
            string transactionStableId,
            string currencyStableId,
            string commandFingerprint,
            long quantityDelta,
            long? expectedSequence,
            string mutationFingerprint,
            MoneyWalletRecordedOutcome recordedOutcome,
            long sequenceBefore,
            long sequenceAfter,
            long previousContribution,
            long currentContribution,
            string rejectionCode)
        {
            TransactionStableId = transactionStableId;
            CurrencyStableId = currencyStableId;
            CommandFingerprint = commandFingerprint;
            QuantityDelta = quantityDelta;
            ExpectedSequence = expectedSequence;
            MutationFingerprint = mutationFingerprint;
            RecordedOutcome = recordedOutcome;
            SequenceBefore = sequenceBefore;
            SequenceAfter = sequenceAfter;
            PreviousContribution = previousContribution;
            CurrentContribution = currentContribution;
            RejectionCode = rejectionCode;
        }

        public string TransactionStableId { get; }

        public string CurrencyStableId { get; }

        public string CommandFingerprint { get; }

        public long QuantityDelta { get; }

        public long? ExpectedSequence { get; }

        public string MutationFingerprint { get; }

        public MoneyWalletRecordedOutcome RecordedOutcome { get; }

        public long SequenceBefore { get; }

        public long SequenceAfter { get; }

        public long PreviousContribution { get; }

        public long CurrentContribution { get; }

        public string RejectionCode { get; }
    }

    /// <summary>
    /// Immutable, deterministic money-only snapshot. The underlying ledger is never
    /// exposed through the public authority surface.
    /// </summary>
    public sealed class MoneyWalletSnapshot
    {
        public const int CurrentSchemaVersion =
            LedgerSnapshot<MoneyLedgerVocabulary>.CurrentSchemaVersion;

        public MoneyWalletSnapshot(
            int schemaVersion,
            long sequence,
            long balance,
            IEnumerable<MoneyWalletContributionSnapshot> contributions,
            IEnumerable<MoneyWalletTransactionSnapshot> transactions,
            string fingerprint)
        {
            if (contributions == null)
            {
                throw new ArgumentNullException(nameof(contributions));
            }

            if (transactions == null)
            {
                throw new ArgumentNullException(nameof(transactions));
            }

            SchemaVersion = schemaVersion;
            Sequence = sequence;
            Balance = balance;
            Contributions = CopyAndOrderContributions(contributions);
            Transactions = CopyAndOrderTransactions(transactions);
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }

        public long Sequence { get; }

        public long Balance { get; }

        public IReadOnlyList<MoneyWalletContributionSnapshot> Contributions { get; }

        public IReadOnlyList<MoneyWalletTransactionSnapshot> Transactions { get; }

        public string Fingerprint { get; }

        public static MoneyWalletSnapshot CreateCanonical(
            int schemaVersion,
            long sequence,
            IEnumerable<MoneyWalletContributionSnapshot> contributions,
            IEnumerable<MoneyWalletTransactionSnapshot> transactions)
        {
            if (contributions == null)
            {
                throw new ArgumentNullException(nameof(contributions));
            }

            if (transactions == null)
            {
                throw new ArgumentNullException(nameof(transactions));
            }

            var contributionCopy = new List<MoneyWalletContributionSnapshot>(contributions);
            var transactionCopy = new List<MoneyWalletTransactionSnapshot>(transactions);
            var provisional = new MoneyWalletSnapshot(
                schemaVersion,
                sequence,
                ComputeBalance(contributionCopy),
                contributionCopy,
                transactionCopy,
                string.Empty);
            LedgerSnapshot<MoneyLedgerVocabulary> ledgerSnapshot =
                provisional.ToLedgerSnapshot(string.Empty);
            LedgerSnapshot<MoneyLedgerVocabulary> canonical =
                LedgerSnapshot<MoneyLedgerVocabulary>.CreateCanonical(
                    ledgerSnapshot.SchemaVersion,
                    ledgerSnapshot.Sequence,
                    ledgerSnapshot.Entries,
                    ledgerSnapshot.Transactions);

            return new MoneyWalletSnapshot(
                schemaVersion,
                sequence,
                provisional.Balance,
                provisional.Contributions,
                provisional.Transactions,
                canonical.Fingerprint);
        }

        private LedgerSnapshot<MoneyLedgerVocabulary> ToLedgerSnapshot(
            string fingerprint)
        {
            var entries = new List<LedgerSnapshotEntry>(Contributions.Count);
            for (int index = 0; index < Contributions.Count; index++)
            {
                MoneyWalletContributionSnapshot contribution = Contributions[index];
                entries.Add(new LedgerSnapshotEntry(
                    MoneyWalletIdsV1.EntryTypeStableId.ToString(),
                    contribution.CurrencyStableId,
                    contribution.CommandFingerprint,
                    contribution.Quantity));
            }

            var transactions =
                new List<LedgerTransactionSnapshot>(Transactions.Count);
            for (int index = 0; index < Transactions.Count; index++)
            {
                MoneyWalletTransactionSnapshot transaction = Transactions[index];
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
                SchemaVersion,
                Sequence,
                entries,
                transactions,
                fingerprint);
        }

        private static IReadOnlyList<MoneyWalletContributionSnapshot>
            CopyAndOrderContributions(
                IEnumerable<MoneyWalletContributionSnapshot> source)
        {
            var copy = new List<MoneyWalletContributionSnapshot>(source);
            copy.Sort(CompareContributions);
            return new ReadOnlyCollection<MoneyWalletContributionSnapshot>(copy);
        }

        private static IReadOnlyList<MoneyWalletTransactionSnapshot>
            CopyAndOrderTransactions(
                IEnumerable<MoneyWalletTransactionSnapshot> source)
        {
            var copy = new List<MoneyWalletTransactionSnapshot>(source);
            copy.Sort(CompareTransactions);
            return new ReadOnlyCollection<MoneyWalletTransactionSnapshot>(copy);
        }

        private static int CompareContributions(
            MoneyWalletContributionSnapshot left,
            MoneyWalletContributionSnapshot right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int currency = string.CompareOrdinal(
                left.CurrencyStableId,
                right.CurrencyStableId);
            return currency != 0
                ? currency
                : string.CompareOrdinal(
                    left.CommandFingerprint,
                    right.CommandFingerprint);
        }

        private static int CompareTransactions(
            MoneyWalletTransactionSnapshot left,
            MoneyWalletTransactionSnapshot right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            return string.CompareOrdinal(
                left.TransactionStableId,
                right.TransactionStableId);
        }

        private static long ComputeBalance(
            IEnumerable<MoneyWalletContributionSnapshot> contributions)
        {
            if (contributions == null)
            {
                throw new ArgumentNullException(nameof(contributions));
            }

            long balance = 0L;
            foreach (MoneyWalletContributionSnapshot contribution in contributions)
            {
                if (contribution == null)
                {
                    throw new ArgumentException(
                        "Money snapshot contributions must not contain null entries.",
                        nameof(contributions));
                }

                balance = checked(balance + contribution.Quantity);
            }

            return balance;
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
    }

    /// <summary>
    /// Immutable UI-ready fact returned for every submitted money transaction.
    /// </summary>
    public sealed class MoneyWalletChangeFact
    {
        public MoneyWalletChangeFact(
            StableId transactionStableId,
            StableId operationStableId,
            StableId currencyStableId,
            MoneyTransactionKind transactionKind,
            long amount,
            string commandFingerprint,
            MoneyWalletTransactionStatus status,
            MoneyWalletTransactionStatus originalStatus,
            string rejectionCode,
            MoneyWalletSnapshot previousSnapshot,
            MoneyWalletSnapshot currentSnapshot)
        {
            TransactionStableId = transactionStableId;
            OperationStableId = operationStableId;
            CurrencyStableId = currencyStableId;
            TransactionKind = transactionKind;
            Amount = amount;
            CommandFingerprint = commandFingerprint;
            Status = status;
            OriginalStatus = originalStatus;
            RejectionCode = rejectionCode;
            PreviousSnapshot = previousSnapshot;
            CurrentSnapshot = currentSnapshot;
        }

        public StableId TransactionStableId { get; }

        public StableId OperationStableId { get; }

        public StableId CurrencyStableId { get; }

        public MoneyTransactionKind TransactionKind { get; }

        public long Amount { get; }

        public string CommandFingerprint { get; }

        public MoneyWalletTransactionStatus Status { get; }

        public MoneyWalletTransactionStatus OriginalStatus { get; }

        public string RejectionCode { get; }

        public MoneyWalletSnapshot PreviousSnapshot { get; }

        public MoneyWalletSnapshot CurrentSnapshot { get; }

        public bool Changed =>
            Status == MoneyWalletTransactionStatus.Applied;
    }

    public sealed class MoneyWalletImportResult
    {
        public MoneyWalletImportResult(
            MoneyWalletImportStatus status,
            string rejectionCode,
            MoneyWalletSnapshot previousSnapshot,
            MoneyWalletSnapshot currentSnapshot)
        {
            Status = status;
            RejectionCode = rejectionCode;
            PreviousSnapshot = previousSnapshot;
            CurrentSnapshot = currentSnapshot;
        }

        public MoneyWalletImportStatus Status { get; }

        public string RejectionCode { get; }

        public MoneyWalletSnapshot PreviousSnapshot { get; }

        public MoneyWalletSnapshot CurrentSnapshot { get; }

        public bool Succeeded =>
            Status == MoneyWalletImportStatus.Imported;
    }
}

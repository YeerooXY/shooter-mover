using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Economy.Money;

namespace ShooterMover.Application.Economy.Money
{
    /// <summary>
    /// Sole engine-independent money authority. The shared ledger is private and
    /// all callers interact through money-specific commands, facts, and snapshots.
    /// </summary>
    public sealed partial class MoneyWalletService
    {
        private const string WrongCurrencyCode = "money-wrong-currency";
        private const string InsufficientFundsCode = "money-insufficient-funds";
        private const string BalanceOverflowCode = "money-balance-overflow";
        private const string InvalidCommandFingerprintCode =
            "money-command-fingerprint-invalid";

        private readonly IdempotentLedger<MoneyLedgerVocabulary> ledger;

        public MoneyWalletService()
        {
            ledger = new IdempotentLedger<MoneyLedgerVocabulary>(
                ValidateMutation,
                EnforceMoneyPolicy);
        }

        public long Balance =>
            CurrentSnapshot.Balance;

        public long Sequence =>
            ledger.Sequence;

        public MoneyWalletSnapshot CurrentSnapshot =>
            CreateMoneySnapshot(ledger.ExportSnapshot());

        public MoneyWalletChangeFact Grant(
            StableId transactionStableId,
            StableId operationStableId,
            long amount,
            long? expectedSequence = null)
        {
            if (amount <= 0L)
            {
                return CreateInvalidAmountFact(
                    transactionStableId,
                    operationStableId,
                    MoneyTransactionKind.Grant,
                    amount);
            }

            return Apply(MoneyTransactionCommand.CreateGrant(
                transactionStableId,
                operationStableId,
                amount,
                expectedSequence));
        }

        public MoneyWalletChangeFact Spend(
            StableId transactionStableId,
            StableId operationStableId,
            long amount,
            long? expectedSequence = null)
        {
            if (amount <= 0L)
            {
                return CreateInvalidAmountFact(
                    transactionStableId,
                    operationStableId,
                    MoneyTransactionKind.Spend,
                    amount);
            }

            return Apply(MoneyTransactionCommand.CreateSpend(
                transactionStableId,
                operationStableId,
                amount,
                expectedSequence));
        }

        public MoneyWalletChangeFact Apply(MoneyTransactionCommand command)
        {
            MoneyWalletSnapshot previousSnapshot = CurrentSnapshot;
            if (command == null)
            {
                return new MoneyWalletChangeFact(
                    null,
                    null,
                    null,
                    default(MoneyTransactionKind),
                    0L,
                    null,
                    MoneyWalletTransactionStatus.InvalidRequest,
                    MoneyWalletTransactionStatus.InvalidRequest,
                    "money-command-null",
                    previousSnapshot,
                    previousSnapshot);
            }

            long quantityDelta = command.TransactionKind == MoneyTransactionKind.Grant
                ? command.Amount
                : -command.Amount;
            var entry = new LedgerEntry<MoneyLedgerVocabulary>(
                MoneyWalletIdsV1.EntryTypeStableId,
                command.CurrencyStableId,
                command.Fingerprint);
            var mutation = new LedgerMutation<MoneyLedgerVocabulary>(
                command.TransactionStableId,
                entry,
                quantityDelta,
                command.ExpectedSequence);

            LedgerMutationResult<MoneyLedgerVocabulary> ledgerResult =
                ledger.Apply(mutation);
            MoneyWalletSnapshot currentSnapshot = ledgerResult.ChangedState
                ? CurrentSnapshot
                : previousSnapshot;
            MoneyWalletTransactionStatus status = MapStatus(
                ledgerResult.Status,
                ledgerResult.RejectionCode);
            MoneyWalletTransactionStatus originalStatus = MapStatus(
                ledgerResult.OriginalStatus,
                ledgerResult.RejectionCode);

            return new MoneyWalletChangeFact(
                command.TransactionStableId,
                command.OperationStableId,
                command.CurrencyStableId,
                command.TransactionKind,
                command.Amount,
                command.Fingerprint,
                status,
                originalStatus,
                ledgerResult.RejectionCode,
                previousSnapshot,
                currentSnapshot);
        }

        private MoneyWalletChangeFact CreateInvalidAmountFact(
            StableId transactionStableId,
            StableId operationStableId,
            MoneyTransactionKind transactionKind,
            long amount)
        {
            MoneyWalletSnapshot snapshot = CurrentSnapshot;
            return new MoneyWalletChangeFact(
                transactionStableId,
                operationStableId,
                MoneyWalletIdsV1.CurrencyStableId,
                transactionKind,
                amount,
                null,
                MoneyWalletTransactionStatus.InvalidAmount,
                MoneyWalletTransactionStatus.InvalidAmount,
                "money-amount-not-positive",
                snapshot,
                snapshot);
        }

        private LedgerDecision ValidateMutation(
            LedgerMutationContext<MoneyLedgerVocabulary> context)
        {
            if (context.Mutation.Entry.EntryTypeId
                != MoneyWalletIdsV1.EntryTypeStableId)
            {
                return LedgerDecision.Reject("money-entry-type-invalid");
            }

            if (context.Mutation.Entry.TargetId
                != MoneyWalletIdsV1.CurrencyStableId)
            {
                return LedgerDecision.Reject(WrongCurrencyCode);
            }

            if (!IsCanonicalFingerprint(
                context.Mutation.Entry.CanonicalPayload))
            {
                return LedgerDecision.Reject(
                    InvalidCommandFingerprintCode);
            }

            return LedgerDecision.Accept();
        }

        private LedgerDecision EnforceMoneyPolicy(
            LedgerMutationContext<MoneyLedgerVocabulary> context)
        {
            long balance;
            if (!TryReadBalance(ledger.ExportSnapshot(), out balance))
            {
                return LedgerDecision.Reject(BalanceOverflowCode);
            }

            long proposedBalance;
            try
            {
                proposedBalance = checked(
                    balance + context.Mutation.QuantityDelta);
            }
            catch (OverflowException)
            {
                return LedgerDecision.Reject(BalanceOverflowCode);
            }

            if (proposedBalance < 0L)
            {
                return LedgerDecision.Reject(InsufficientFundsCode);
            }

            return LedgerDecision.Accept();
        }

        private static bool TryReadBalance(
            LedgerSnapshot<MoneyLedgerVocabulary> snapshot,
            out long balance)
        {
            balance = 0L;
            try
            {
                for (int index = 0; index < snapshot.Entries.Count; index++)
                {
                    balance = checked(
                        balance + snapshot.Entries[index].Quantity);
                }
            }
            catch (OverflowException)
            {
                balance = 0L;
                return false;
            }

            return balance >= 0L;
        }

        private static bool IsCanonicalFingerprint(string value)
        {
            if (value == null
                || value.Length != 71
                || !value.StartsWith("sha256:", StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = 7; index < value.Length; index++)
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

        private static MoneyWalletTransactionStatus MapStatus(
            LedgerMutationStatus status,
            string rejectionCode)
        {
            switch (status)
            {
                case LedgerMutationStatus.Applied:
                    return MoneyWalletTransactionStatus.Applied;
                case LedgerMutationStatus.DuplicateNoChange:
                    return MoneyWalletTransactionStatus.DuplicateNoChange;
                case LedgerMutationStatus.ConflictingDuplicate:
                    return MoneyWalletTransactionStatus.ConflictingDuplicate;
                case LedgerMutationStatus.SequenceConflict:
                    return MoneyWalletTransactionStatus.SequenceConflict;
                case LedgerMutationStatus.ValidationRejected:
                    if (string.Equals(
                        rejectionCode,
                        WrongCurrencyCode,
                        StringComparison.Ordinal))
                    {
                        return MoneyWalletTransactionStatus.WrongCurrency;
                    }

                    if (string.Equals(
                        rejectionCode,
                        BalanceOverflowCode,
                        StringComparison.Ordinal)
                        || string.Equals(
                            rejectionCode,
                            "quantity-overflow",
                            StringComparison.Ordinal))
                    {
                        return MoneyWalletTransactionStatus.InvalidAmount;
                    }

                    return MoneyWalletTransactionStatus.InvalidRequest;
                case LedgerMutationStatus.PolicyRejected:
                    if (string.Equals(
                        rejectionCode,
                        InsufficientFundsCode,
                        StringComparison.Ordinal))
                    {
                        return MoneyWalletTransactionStatus.InsufficientFunds;
                    }

                    if (string.Equals(
                        rejectionCode,
                        BalanceOverflowCode,
                        StringComparison.Ordinal))
                    {
                        return MoneyWalletTransactionStatus.InvalidAmount;
                    }

                    return MoneyWalletTransactionStatus.InvalidRequest;
                default:
                    return MoneyWalletTransactionStatus.InvalidRequest;
            }
        }
    }
}

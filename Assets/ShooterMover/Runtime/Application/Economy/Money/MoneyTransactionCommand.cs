using System;
using ShooterMover.Contracts.Economy;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;

namespace ShooterMover.Application.Economy.Money
{
    /// <summary>
    /// Typed money request. The generic economy command remains an internal
    /// implementation detail of the money authority.
    /// </summary>
    public sealed class MoneyTransactionCommand
    {
        private readonly EconomyTransactionCommandV1 economyCommand;

        private MoneyTransactionCommand(
            StableId transactionStableId,
            StableId operationStableId,
            StableId currencyStableId,
            MoneyTransactionKind transactionKind,
            long amount,
            long? expectedSequence)
        {
            if (amount <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "Money grants and spends require a positive amount.");
            }

            EconomyTransactionOperationV1 operation;
            switch (transactionKind)
            {
                case MoneyTransactionKind.Grant:
                    operation = EconomyTransactionOperationV1.Credit;
                    break;
                case MoneyTransactionKind.Spend:
                    operation = EconomyTransactionOperationV1.Debit;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(transactionKind),
                        transactionKind,
                        "Unknown money transaction kind.");
            }

            economyCommand = EconomyTransactionCommandV1.Create(
                transactionStableId,
                operationStableId,
                MoneyWalletIdsV1.AuthorityStableId,
                operation,
                EconomyResourceKindV1.Currency,
                currencyStableId,
                null,
                amount,
                expectedSequence);
            TransactionKind = transactionKind;
        }

        public StableId TransactionStableId =>
            economyCommand.TransactionStableId;

        public StableId OperationStableId =>
            economyCommand.OperationStableId;

        public StableId CurrencyStableId =>
            economyCommand.ResourceStableId;

        public MoneyTransactionKind TransactionKind { get; }

        public long Amount =>
            economyCommand.Quantity;

        public long? ExpectedSequence =>
            economyCommand.ExpectedSequence;

        public string Fingerprint =>
            economyCommand.PayloadFingerprint;

        internal EconomyTransactionCommandV1 EconomyCommand =>
            economyCommand;

        public static MoneyTransactionCommand CreateGrant(
            StableId transactionStableId,
            StableId operationStableId,
            long amount,
            long? expectedSequence = null)
        {
            return CreateGrant(
                transactionStableId,
                operationStableId,
                MoneyWalletIdsV1.CurrencyStableId,
                amount,
                expectedSequence);
        }

        public static MoneyTransactionCommand CreateGrant(
            StableId transactionStableId,
            StableId operationStableId,
            StableId currencyStableId,
            long amount,
            long? expectedSequence = null)
        {
            return new MoneyTransactionCommand(
                transactionStableId,
                operationStableId,
                currencyStableId,
                MoneyTransactionKind.Grant,
                amount,
                expectedSequence);
        }

        public static MoneyTransactionCommand CreateSpend(
            StableId transactionStableId,
            StableId operationStableId,
            long amount,
            long? expectedSequence = null)
        {
            return CreateSpend(
                transactionStableId,
                operationStableId,
                MoneyWalletIdsV1.CurrencyStableId,
                amount,
                expectedSequence);
        }

        public static MoneyTransactionCommand CreateSpend(
            StableId transactionStableId,
            StableId operationStableId,
            StableId currencyStableId,
            long amount,
            long? expectedSequence = null)
        {
            return new MoneyTransactionCommand(
                transactionStableId,
                operationStableId,
                currencyStableId,
                MoneyTransactionKind.Spend,
                amount,
                expectedSequence);
        }
    }
}

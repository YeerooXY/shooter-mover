using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Economy;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Contracts.Holdings
{
    /// <summary>
    /// Complete first-attempt transaction fact. Exact duplicate calls return the
    /// original fact but do not append another record.
    /// </summary>
    public sealed class PlayerHoldingsTransactionRecord :
        IEquatable<PlayerHoldingsTransactionRecord>,
        IComparable<PlayerHoldingsTransactionRecord>
    {
        private readonly string canonicalText;

        private PlayerHoldingsTransactionRecord(
            PlayerHoldingsCommand command,
            PlayerHoldingsMutationStatus originalStatus,
            LedgerMutationStatus ledgerOriginalStatus,
            long sequenceBefore,
            long sequenceAfter,
            long ledgerPreviousQuantity,
            long ledgerCurrentQuantity,
            long holdingPreviousQuantity,
            long holdingCurrentQuantity,
            string rejectionCode)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (!Enum.IsDefined(typeof(PlayerHoldingsMutationStatus), originalStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(originalStatus));
            }

            if (!Enum.IsDefined(typeof(LedgerMutationStatus), ledgerOriginalStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(ledgerOriginalStatus));
            }

            if (sequenceBefore < 0L || sequenceAfter < sequenceBefore)
            {
                throw new ArgumentOutOfRangeException(nameof(sequenceAfter));
            }

            if (holdingPreviousQuantity < 0L || holdingCurrentQuantity < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(holdingCurrentQuantity));
            }

            OriginalStatus = originalStatus;
            LedgerOriginalStatus = ledgerOriginalStatus;
            SequenceBefore = sequenceBefore;
            SequenceAfter = sequenceAfter;
            LedgerPreviousQuantity = ledgerPreviousQuantity;
            LedgerCurrentQuantity = ledgerCurrentQuantity;
            HoldingPreviousQuantity = holdingPreviousQuantity;
            HoldingCurrentQuantity = holdingCurrentQuantity;
            RejectionCode = rejectionCode;

            var builder = new StringBuilder();
            HoldingsFormat.AppendToken(
                builder,
                "command",
                Command.ToCanonicalString());
            HoldingsFormat.AppendToken(
                builder,
                "original_status",
                ((int)OriginalStatus).ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "ledger_original_status",
                ((int)LedgerOriginalStatus).ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "sequence_before",
                SequenceBefore.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "sequence_after",
                SequenceAfter.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "ledger_previous_quantity",
                LedgerPreviousQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "ledger_current_quantity",
                LedgerCurrentQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "holding_previous_quantity",
                HoldingPreviousQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "holding_current_quantity",
                HoldingCurrentQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "rejection_code",
                RejectionCode ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = HoldingsFormat.ComputeSha256(canonicalText);
        }

        public PlayerHoldingsCommand Command { get; }

        public PlayerHoldingsMutationStatus OriginalStatus { get; }

        public LedgerMutationStatus LedgerOriginalStatus { get; }

        public long SequenceBefore { get; }

        public long SequenceAfter { get; }

        public long LedgerPreviousQuantity { get; }

        public long LedgerCurrentQuantity { get; }

        public long HoldingPreviousQuantity { get; }

        public long HoldingCurrentQuantity { get; }

        public string RejectionCode { get; }

        public string Fingerprint { get; }

        public static PlayerHoldingsTransactionRecord Create(
            PlayerHoldingsCommand command,
            PlayerHoldingsMutationStatus originalStatus,
            LedgerMutationStatus ledgerOriginalStatus,
            long sequenceBefore,
            long sequenceAfter,
            long ledgerPreviousQuantity,
            long ledgerCurrentQuantity,
            long holdingPreviousQuantity,
            long holdingCurrentQuantity,
            string rejectionCode)
        {
            return new PlayerHoldingsTransactionRecord(
                command,
                originalStatus,
                ledgerOriginalStatus,
                sequenceBefore,
                sequenceAfter,
                ledgerPreviousQuantity,
                ledgerCurrentQuantity,
                holdingPreviousQuantity,
                holdingCurrentQuantity,
                rejectionCode);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public int CompareTo(PlayerHoldingsTransactionRecord other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return Command.Transaction.TransactionStableId.CompareTo(
                other.Command.Transaction.TransactionStableId);
        }

        public bool Equals(PlayerHoldingsTransactionRecord other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerHoldingsTransactionRecord);
        }

        public override int GetHashCode()
        {
            return HoldingsFormat.DeterministicHash(canonicalText);
        }
    }

}

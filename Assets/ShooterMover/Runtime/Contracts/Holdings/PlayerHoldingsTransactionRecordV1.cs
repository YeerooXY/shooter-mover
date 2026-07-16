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
    public sealed class PlayerHoldingsTransactionRecordV1 :
        IEquatable<PlayerHoldingsTransactionRecordV1>,
        IComparable<PlayerHoldingsTransactionRecordV1>
    {
        private readonly string canonicalText;

        private PlayerHoldingsTransactionRecordV1(
            PlayerHoldingsCommandV1 command,
            PlayerHoldingsMutationStatusV1 originalStatus,
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
            if (!Enum.IsDefined(typeof(PlayerHoldingsMutationStatusV1), originalStatus))
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
            HoldingsCanonicalV1.AppendToken(
                builder,
                "command",
                Command.ToCanonicalString());
            HoldingsCanonicalV1.AppendToken(
                builder,
                "original_status",
                ((int)OriginalStatus).ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "ledger_original_status",
                ((int)LedgerOriginalStatus).ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "sequence_before",
                SequenceBefore.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "sequence_after",
                SequenceAfter.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "ledger_previous_quantity",
                LedgerPreviousQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "ledger_current_quantity",
                LedgerCurrentQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "holding_previous_quantity",
                HoldingPreviousQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "holding_current_quantity",
                HoldingCurrentQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "rejection_code",
                RejectionCode ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = HoldingsCanonicalV1.ComputeSha256(canonicalText);
        }

        public PlayerHoldingsCommandV1 Command { get; }

        public PlayerHoldingsMutationStatusV1 OriginalStatus { get; }

        public LedgerMutationStatus LedgerOriginalStatus { get; }

        public long SequenceBefore { get; }

        public long SequenceAfter { get; }

        public long LedgerPreviousQuantity { get; }

        public long LedgerCurrentQuantity { get; }

        public long HoldingPreviousQuantity { get; }

        public long HoldingCurrentQuantity { get; }

        public string RejectionCode { get; }

        public string Fingerprint { get; }

        public static PlayerHoldingsTransactionRecordV1 Create(
            PlayerHoldingsCommandV1 command,
            PlayerHoldingsMutationStatusV1 originalStatus,
            LedgerMutationStatus ledgerOriginalStatus,
            long sequenceBefore,
            long sequenceAfter,
            long ledgerPreviousQuantity,
            long ledgerCurrentQuantity,
            long holdingPreviousQuantity,
            long holdingCurrentQuantity,
            string rejectionCode)
        {
            return new PlayerHoldingsTransactionRecordV1(
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

        public int CompareTo(PlayerHoldingsTransactionRecordV1 other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return Command.Transaction.TransactionStableId.CompareTo(
                other.Command.Transaction.TransactionStableId);
        }

        public bool Equals(PlayerHoldingsTransactionRecordV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerHoldingsTransactionRecordV1);
        }

        public override int GetHashCode()
        {
            return HoldingsCanonicalV1.DeterministicHash(canonicalText);
        }
    }

}

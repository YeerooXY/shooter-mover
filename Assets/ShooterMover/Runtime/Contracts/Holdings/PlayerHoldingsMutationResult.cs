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
    public sealed class PlayerHoldingsMutationResult :
        IEquatable<PlayerHoldingsMutationResult>
    {
        private readonly string canonicalText;

        private PlayerHoldingsMutationResult(
            StableId transactionStableId,
            PlayerHoldingsMutationStatus status,
            PlayerHoldingsMutationStatus originalStatus,
            string commandFingerprint,
            long previousSequence,
            long currentSequence,
            long previousQuantity,
            long currentQuantity,
            string rejectionCode)
        {
            TransactionStableId = transactionStableId;
            if (!Enum.IsDefined(typeof(PlayerHoldingsMutationStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (!Enum.IsDefined(typeof(PlayerHoldingsMutationStatus), originalStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(originalStatus));
            }

            if (commandFingerprint != null
                && !HoldingsFormat.IsCanonicalFingerprint(commandFingerprint))
            {
                throw new ArgumentException(
                    "Command fingerprint must be canonical when supplied.",
                    nameof(commandFingerprint));
            }

            if (previousSequence < 0L || currentSequence < previousSequence)
            {
                throw new ArgumentOutOfRangeException(nameof(currentSequence));
            }

            if (previousQuantity < 0L || currentQuantity < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(currentQuantity));
            }

            bool carriesOriginalFact =
                status == PlayerHoldingsMutationStatus.Applied
                || status == PlayerHoldingsMutationStatus.ExactDuplicateNoChange
                || status == PlayerHoldingsMutationStatus.ConflictingDuplicate;
            bool originalApplied =
                originalStatus == PlayerHoldingsMutationStatus.Applied;
            if (status == PlayerHoldingsMutationStatus.Applied
                && !originalApplied)
            {
                throw new ArgumentException(
                    "An applied result must carry an applied original status.");
            }

            long expectedCurrentSequence =
                carriesOriginalFact && originalApplied
                    ? previousSequence + 1L
                    : previousSequence;
            if (currentSequence != expectedCurrentSequence)
            {
                throw new ArgumentException(
                    "Result sequence fields must represent the original terminal fact.");
            }

            Status = status;
            OriginalStatus = originalStatus;
            CommandFingerprint = commandFingerprint;
            PreviousSequence = previousSequence;
            CurrentSequence = currentSequence;
            PreviousQuantity = previousQuantity;
            CurrentQuantity = currentQuantity;
            RejectionCode = rejectionCode;

            var builder = new StringBuilder();
            HoldingsFormat.AppendToken(
                builder,
                "transaction_stable_id",
                TransactionStableId == null
                    ? "null"
                    : TransactionStableId.ToString());
            HoldingsFormat.AppendToken(
                builder,
                "status",
                ((int)Status).ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "original_status",
                ((int)OriginalStatus).ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "command_fingerprint",
                CommandFingerprint ?? "null");
            HoldingsFormat.AppendToken(
                builder,
                "previous_sequence",
                PreviousSequence.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "current_sequence",
                CurrentSequence.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "previous_quantity",
                PreviousQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "current_quantity",
                CurrentQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "rejection_code",
                RejectionCode ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = HoldingsFormat.ComputeSha256(canonicalText);
        }

        public StableId TransactionStableId { get; }

        public PlayerHoldingsMutationStatus Status { get; }

        public PlayerHoldingsMutationStatus OriginalStatus { get; }

        public string CommandFingerprint { get; }

        public long PreviousSequence { get; }

        public long CurrentSequence { get; }

        public long PreviousQuantity { get; }

        public long CurrentQuantity { get; }

        public string RejectionCode { get; }

        public string Fingerprint { get; }

        public bool ChangedState
        {
            get { return Status == PlayerHoldingsMutationStatus.Applied; }
        }

        public static PlayerHoldingsMutationResult Create(
            StableId transactionStableId,
            PlayerHoldingsMutationStatus status,
            PlayerHoldingsMutationStatus originalStatus,
            string commandFingerprint,
            long previousSequence,
            long currentSequence,
            long previousQuantity,
            long currentQuantity,
            string rejectionCode)
        {
            return new PlayerHoldingsMutationResult(
                transactionStableId,
                status,
                originalStatus,
                commandFingerprint,
                previousSequence,
                currentSequence,
                previousQuantity,
                currentQuantity,
                rejectionCode);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(PlayerHoldingsMutationResult other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerHoldingsMutationResult);
        }

        public override int GetHashCode()
        {
            return HoldingsFormat.DeterministicHash(canonicalText);
        }
    }

}

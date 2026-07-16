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
    public sealed class PlayerHoldingsMutationResultV1 :
        IEquatable<PlayerHoldingsMutationResultV1>
    {
        private readonly string canonicalText;

        private PlayerHoldingsMutationResultV1(
            StableId transactionStableId,
            PlayerHoldingsMutationStatusV1 status,
            PlayerHoldingsMutationStatusV1 originalStatus,
            string commandFingerprint,
            long previousSequence,
            long currentSequence,
            long previousQuantity,
            long currentQuantity,
            string rejectionCode)
        {
            TransactionStableId = transactionStableId;
            if (!Enum.IsDefined(typeof(PlayerHoldingsMutationStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (!Enum.IsDefined(typeof(PlayerHoldingsMutationStatusV1), originalStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(originalStatus));
            }

            if (commandFingerprint != null
                && !HoldingsCanonicalV1.IsCanonicalFingerprint(commandFingerprint))
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
                status == PlayerHoldingsMutationStatusV1.Applied
                || status == PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange
                || status == PlayerHoldingsMutationStatusV1.ConflictingDuplicate;
            bool originalApplied =
                originalStatus == PlayerHoldingsMutationStatusV1.Applied;
            if (status == PlayerHoldingsMutationStatusV1.Applied
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
            HoldingsCanonicalV1.AppendToken(
                builder,
                "transaction_stable_id",
                TransactionStableId == null
                    ? "null"
                    : TransactionStableId.ToString());
            HoldingsCanonicalV1.AppendToken(
                builder,
                "status",
                ((int)Status).ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "original_status",
                ((int)OriginalStatus).ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "command_fingerprint",
                CommandFingerprint ?? "null");
            HoldingsCanonicalV1.AppendToken(
                builder,
                "previous_sequence",
                PreviousSequence.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "current_sequence",
                CurrentSequence.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "previous_quantity",
                PreviousQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "current_quantity",
                CurrentQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "rejection_code",
                RejectionCode ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = HoldingsCanonicalV1.ComputeSha256(canonicalText);
        }

        public StableId TransactionStableId { get; }

        public PlayerHoldingsMutationStatusV1 Status { get; }

        public PlayerHoldingsMutationStatusV1 OriginalStatus { get; }

        public string CommandFingerprint { get; }

        public long PreviousSequence { get; }

        public long CurrentSequence { get; }

        public long PreviousQuantity { get; }

        public long CurrentQuantity { get; }

        public string RejectionCode { get; }

        public string Fingerprint { get; }

        public bool ChangedState
        {
            get { return Status == PlayerHoldingsMutationStatusV1.Applied; }
        }

        public static PlayerHoldingsMutationResultV1 Create(
            StableId transactionStableId,
            PlayerHoldingsMutationStatusV1 status,
            PlayerHoldingsMutationStatusV1 originalStatus,
            string commandFingerprint,
            long previousSequence,
            long currentSequence,
            long previousQuantity,
            long currentQuantity,
            string rejectionCode)
        {
            return new PlayerHoldingsMutationResultV1(
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

        public bool Equals(PlayerHoldingsMutationResultV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerHoldingsMutationResultV1);
        }

        public override int GetHashCode()
        {
            return HoldingsCanonicalV1.DeterministicHash(canonicalText);
        }
    }

}

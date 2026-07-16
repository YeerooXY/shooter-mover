using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Equipment.Upgrades
{
    public sealed class AugmentUpgradeFactV1 : IEquatable<AugmentUpgradeFactV1>
    {
        private readonly string canonicalText;

        private AugmentUpgradeFactV1(
            AugmentUpgradeConfirmationStatusV1 status,
            AugmentUpgradeConfirmationStatusV1 originalStatus,
            StableId confirmationStableId,
            string confirmationFingerprint,
            string quoteFingerprint,
            StableId moneyTransactionStableId,
            StableId holdingsRemoveTransactionStableId,
            StableId replacementEquipmentInstanceStableId,
            string replacementEquipmentFingerprint,
            StableId rewardCommitmentStableId,
            StableId rewardClaimStableId,
            long moneyCost,
            long walletSequenceBefore,
            long walletSequenceAfter,
            long holdingsSequenceBefore,
            long holdingsSequenceAfter,
            string rejectionCode)
        {
            Status = status;
            OriginalStatus = originalStatus;
            ConfirmationStableId = confirmationStableId;
            ConfirmationFingerprint = confirmationFingerprint;
            QuoteFingerprint = quoteFingerprint;
            MoneyTransactionStableId = moneyTransactionStableId;
            HoldingsRemoveTransactionStableId = holdingsRemoveTransactionStableId;
            ReplacementEquipmentInstanceStableId = replacementEquipmentInstanceStableId;
            ReplacementEquipmentFingerprint = replacementEquipmentFingerprint;
            RewardCommitmentStableId = rewardCommitmentStableId;
            RewardClaimStableId = rewardClaimStableId;
            MoneyCost = moneyCost;
            WalletSequenceBefore = walletSequenceBefore;
            WalletSequenceAfter = walletSequenceAfter;
            HoldingsSequenceBefore = holdingsSequenceBefore;
            HoldingsSequenceAfter = holdingsSequenceAfter;
            RejectionCode = rejectionCode;

            var builder = new StringBuilder();
            AppendId(builder, "confirmation_stable_id", ConfirmationStableId);
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "status",
                ((int)Status).ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "original_status",
                ((int)OriginalStatus).ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "confirmation_fingerprint",
                ConfirmationFingerprint ?? "null");
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "quote_fingerprint",
                QuoteFingerprint ?? "null");
            AppendId(builder, "money_transaction_stable_id", MoneyTransactionStableId);
            AppendId(
                builder,
                "holdings_remove_transaction_stable_id",
                HoldingsRemoveTransactionStableId);
            AppendId(
                builder,
                "replacement_equipment_instance_stable_id",
                ReplacementEquipmentInstanceStableId);
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "replacement_equipment_fingerprint",
                ReplacementEquipmentFingerprint ?? "null");
            AppendId(builder, "reward_commitment_stable_id", RewardCommitmentStableId);
            AppendId(builder, "reward_claim_stable_id", RewardClaimStableId);
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "money_cost",
                MoneyCost.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "wallet_sequence_before",
                WalletSequenceBefore.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "wallet_sequence_after",
                WalletSequenceAfter.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "holdings_sequence_before",
                HoldingsSequenceBefore.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "holdings_sequence_after",
                HoldingsSequenceAfter.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "rejection_code",
                RejectionCode ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = AugmentUpgradeCanonicalV1.Fingerprint(canonicalText);
        }

        public AugmentUpgradeConfirmationStatusV1 Status { get; }
        public AugmentUpgradeConfirmationStatusV1 OriginalStatus { get; }
        public StableId ConfirmationStableId { get; }
        public string ConfirmationFingerprint { get; }
        public string QuoteFingerprint { get; }
        public StableId MoneyTransactionStableId { get; }
        public StableId HoldingsRemoveTransactionStableId { get; }
        public StableId ReplacementEquipmentInstanceStableId { get; }
        public string ReplacementEquipmentFingerprint { get; }
        public StableId RewardCommitmentStableId { get; }
        public StableId RewardClaimStableId { get; }
        public long MoneyCost { get; }
        public long WalletSequenceBefore { get; }
        public long WalletSequenceAfter { get; }
        public long HoldingsSequenceBefore { get; }
        public long HoldingsSequenceAfter { get; }
        public string RejectionCode { get; }
        public string Fingerprint { get; }

        public bool Succeeded
        {
            get { return OriginalStatus == AugmentUpgradeConfirmationStatusV1.Applied; }
        }

        public bool IsPending
        {
            get { return Status == AugmentUpgradeConfirmationStatusV1.PendingRetry; }
        }

        public static AugmentUpgradeFactV1 Create(
            AugmentUpgradeConfirmationStatusV1 status,
            AugmentUpgradeConfirmationStatusV1 originalStatus,
            StableId confirmationStableId,
            string confirmationFingerprint,
            string quoteFingerprint,
            StableId moneyTransactionStableId,
            StableId holdingsRemoveTransactionStableId,
            StableId replacementEquipmentInstanceStableId,
            string replacementEquipmentFingerprint,
            StableId rewardCommitmentStableId,
            StableId rewardClaimStableId,
            long moneyCost,
            long walletSequenceBefore,
            long walletSequenceAfter,
            long holdingsSequenceBefore,
            long holdingsSequenceAfter,
            string rejectionCode)
        {
            return new AugmentUpgradeFactV1(
                status,
                originalStatus,
                confirmationStableId,
                confirmationFingerprint,
                quoteFingerprint,
                moneyTransactionStableId,
                holdingsRemoveTransactionStableId,
                replacementEquipmentInstanceStableId,
                replacementEquipmentFingerprint,
                rewardCommitmentStableId,
                rewardClaimStableId,
                moneyCost,
                walletSequenceBefore,
                walletSequenceAfter,
                holdingsSequenceBefore,
                holdingsSequenceAfter,
                rejectionCode);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(AugmentUpgradeFactV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AugmentUpgradeFactV1);
        }

        public override int GetHashCode()
        {
            return AugmentUpgradeCanonicalV1.DeterministicHash(canonicalText);
        }

        private static void AppendId(
            StringBuilder builder,
            string name,
            StableId value)
        {
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                name,
                value == null ? "null" : value.ToString());
        }
    }
}

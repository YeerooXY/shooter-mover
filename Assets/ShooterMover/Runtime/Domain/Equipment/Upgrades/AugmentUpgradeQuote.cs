using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Equipment.Upgrades
{
    public sealed class AugmentUpgradeQuoteRequest
    {
        public AugmentUpgradeQuoteRequest(
            StableId equipmentInstanceStableId,
            StableId augmentInstanceStableId,
            int targetLevel)
        {
            EquipmentInstanceStableId = equipmentInstanceStableId;
            AugmentInstanceStableId = augmentInstanceStableId;
            TargetLevel = targetLevel;
        }

        public StableId EquipmentInstanceStableId { get; }
        public StableId AugmentInstanceStableId { get; }
        public int TargetLevel { get; }
    }

    public sealed class AugmentUpgradeQuote : IEquatable<AugmentUpgradeQuote>
    {
        private readonly string canonicalText;

        private AugmentUpgradeQuote(
            StableId equipmentInstanceStableId,
            string equipmentFingerprint,
            int augmentSlotIndex,
            StableId augmentInstanceStableId,
            StableId augmentDefinitionStableId,
            int augmentTier,
            int currentLevel,
            int targetLevel,
            long currentWalletBalance,
            long walletSequence,
            long holdingsSequence,
            long moneyCost,
            string catalogFingerprint,
            string costPolicyFingerprint)
        {
            EquipmentInstanceStableId = equipmentInstanceStableId;
            EquipmentFingerprint = equipmentFingerprint;
            AugmentSlotIndex = augmentSlotIndex;
            AugmentInstanceStableId = augmentInstanceStableId;
            AugmentDefinitionStableId = augmentDefinitionStableId;
            AugmentTier = augmentTier;
            CurrentLevel = currentLevel;
            TargetLevel = targetLevel;
            CurrentWalletBalance = currentWalletBalance;
            WalletSequence = walletSequence;
            HoldingsSequence = holdingsSequence;
            MoneyCost = moneyCost;
            CatalogFingerprint = catalogFingerprint;
            CostPolicyFingerprint = costPolicyFingerprint;

            var builder = new StringBuilder();
            AugmentUpgrade.AppendToken(
                builder,
                "equipment_instance_stable_id",
                EquipmentInstanceStableId == null
                    ? "null"
                    : EquipmentInstanceStableId.ToString());
            AugmentUpgrade.AppendToken(
                builder,
                "equipment_fingerprint",
                EquipmentFingerprint ?? "null");
            AugmentUpgrade.AppendToken(
                builder,
                "augment_slot_index",
                AugmentSlotIndex.ToString(CultureInfo.InvariantCulture));
            AugmentUpgrade.AppendToken(
                builder,
                "augment_instance_stable_id",
                AugmentInstanceStableId == null
                    ? "null"
                    : AugmentInstanceStableId.ToString());
            AugmentUpgrade.AppendToken(
                builder,
                "augment_definition_stable_id",
                AugmentDefinitionStableId == null
                    ? "null"
                    : AugmentDefinitionStableId.ToString());
            AugmentUpgrade.AppendToken(
                builder,
                "augment_tier",
                AugmentTier.ToString(CultureInfo.InvariantCulture));
            AugmentUpgrade.AppendToken(
                builder,
                "current_level",
                CurrentLevel.ToString(CultureInfo.InvariantCulture));
            AugmentUpgrade.AppendToken(
                builder,
                "target_level",
                TargetLevel.ToString(CultureInfo.InvariantCulture));
            AugmentUpgrade.AppendToken(
                builder,
                "current_wallet_balance",
                CurrentWalletBalance.ToString(CultureInfo.InvariantCulture));
            AugmentUpgrade.AppendToken(
                builder,
                "wallet_sequence",
                WalletSequence.ToString(CultureInfo.InvariantCulture));
            AugmentUpgrade.AppendToken(
                builder,
                "holdings_sequence",
                HoldingsSequence.ToString(CultureInfo.InvariantCulture));
            AugmentUpgrade.AppendToken(
                builder,
                "money_cost",
                MoneyCost.ToString(CultureInfo.InvariantCulture));
            AugmentUpgrade.AppendToken(
                builder,
                "catalog_fingerprint",
                CatalogFingerprint ?? "null");
            AugmentUpgrade.AppendToken(
                builder,
                "cost_policy_fingerprint",
                CostPolicyFingerprint ?? "null");
            canonicalText = builder.ToString();
            QuoteFingerprint = AugmentUpgrade.Fingerprint(canonicalText);
            QuoteStableId = AugmentUpgrade.DeriveStableId(
                "augquote",
                QuoteFingerprint);
        }

        public StableId EquipmentInstanceStableId { get; }
        public string EquipmentFingerprint { get; }
        public int AugmentSlotIndex { get; }
        public StableId AugmentInstanceStableId { get; }
        public StableId AugmentDefinitionStableId { get; }
        public int AugmentTier { get; }
        public int CurrentLevel { get; }
        public int TargetLevel { get; }
        public long CurrentWalletBalance { get; }
        public long WalletSequence { get; }
        public long HoldingsSequence { get; }
        public long MoneyCost { get; }
        public string CatalogFingerprint { get; }
        public string CostPolicyFingerprint { get; }
        public StableId QuoteStableId { get; }
        public string QuoteFingerprint { get; }

        public static AugmentUpgradeQuote Create(
            StableId equipmentInstanceStableId,
            string equipmentFingerprint,
            int augmentSlotIndex,
            StableId augmentInstanceStableId,
            StableId augmentDefinitionStableId,
            int augmentTier,
            int currentLevel,
            int targetLevel,
            long currentWalletBalance,
            long walletSequence,
            long holdingsSequence,
            long moneyCost,
            string catalogFingerprint,
            string costPolicyFingerprint)
        {
            return new AugmentUpgradeQuote(
                equipmentInstanceStableId,
                equipmentFingerprint,
                augmentSlotIndex,
                augmentInstanceStableId,
                augmentDefinitionStableId,
                augmentTier,
                currentLevel,
                targetLevel,
                currentWalletBalance,
                walletSequence,
                holdingsSequence,
                moneyCost,
                catalogFingerprint,
                costPolicyFingerprint);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(AugmentUpgradeQuote other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AugmentUpgradeQuote);
        }

        public override int GetHashCode()
        {
            return AugmentUpgrade.DeterministicHash(canonicalText);
        }
    }

    public sealed class AugmentUpgradeQuoteResult
    {
        private AugmentUpgradeQuoteResult(
            AugmentUpgradeQuoteStatus status,
            AugmentUpgradeQuote quote,
            string rejectionCode)
        {
            Status = status;
            Quote = quote;
            RejectionCode = rejectionCode;
        }

        public AugmentUpgradeQuoteStatus Status { get; }
        public AugmentUpgradeQuote Quote { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Status == AugmentUpgradeQuoteStatus.Quoted; } }

        public static AugmentUpgradeQuoteResult Create(
            AugmentUpgradeQuoteStatus status,
            AugmentUpgradeQuote quote,
            string rejectionCode)
        {
            return new AugmentUpgradeQuoteResult(status, quote, rejectionCode);
        }
    }
}

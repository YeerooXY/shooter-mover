using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Equipment.Upgrades
{
    public sealed class AugmentUpgradeQuoteRequestV1
    {
        public AugmentUpgradeQuoteRequestV1(
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

    public sealed class AugmentUpgradeQuoteV1 : IEquatable<AugmentUpgradeQuoteV1>
    {
        private readonly string canonicalText;

        private AugmentUpgradeQuoteV1(
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
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "equipment_instance_stable_id",
                EquipmentInstanceStableId == null
                    ? "null"
                    : EquipmentInstanceStableId.ToString());
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "equipment_fingerprint",
                EquipmentFingerprint ?? "null");
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "augment_slot_index",
                AugmentSlotIndex.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "augment_instance_stable_id",
                AugmentInstanceStableId == null
                    ? "null"
                    : AugmentInstanceStableId.ToString());
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "augment_definition_stable_id",
                AugmentDefinitionStableId == null
                    ? "null"
                    : AugmentDefinitionStableId.ToString());
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "augment_tier",
                AugmentTier.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "current_level",
                CurrentLevel.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "target_level",
                TargetLevel.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "current_wallet_balance",
                CurrentWalletBalance.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "wallet_sequence",
                WalletSequence.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "holdings_sequence",
                HoldingsSequence.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "money_cost",
                MoneyCost.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "catalog_fingerprint",
                CatalogFingerprint ?? "null");
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "cost_policy_fingerprint",
                CostPolicyFingerprint ?? "null");
            canonicalText = builder.ToString();
            QuoteFingerprint = AugmentUpgradeCanonicalV1.Fingerprint(canonicalText);
            QuoteStableId = AugmentUpgradeCanonicalV1.DeriveStableId(
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

        public static AugmentUpgradeQuoteV1 Create(
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
            return new AugmentUpgradeQuoteV1(
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

        public bool Equals(AugmentUpgradeQuoteV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AugmentUpgradeQuoteV1);
        }

        public override int GetHashCode()
        {
            return AugmentUpgradeCanonicalV1.DeterministicHash(canonicalText);
        }
    }

    public sealed class AugmentUpgradeQuoteResultV1
    {
        private AugmentUpgradeQuoteResultV1(
            AugmentUpgradeQuoteStatusV1 status,
            AugmentUpgradeQuoteV1 quote,
            string rejectionCode)
        {
            Status = status;
            Quote = quote;
            RejectionCode = rejectionCode;
        }

        public AugmentUpgradeQuoteStatusV1 Status { get; }
        public AugmentUpgradeQuoteV1 Quote { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Status == AugmentUpgradeQuoteStatusV1.Quoted; } }

        public static AugmentUpgradeQuoteResultV1 Create(
            AugmentUpgradeQuoteStatusV1 status,
            AugmentUpgradeQuoteV1 quote,
            string rejectionCode)
        {
            return new AugmentUpgradeQuoteResultV1(status, quote, rejectionCode);
        }
    }
}

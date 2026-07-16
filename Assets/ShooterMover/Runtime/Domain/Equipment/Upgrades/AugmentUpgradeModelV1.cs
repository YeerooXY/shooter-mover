using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Equipment.Upgrades
{
    public enum AugmentUpgradeCostStatusV1
    {
        Calculated = 1,
        InvalidTarget = 2,
        TierNotConfigured = 3,
        ArithmeticOverflow = 4,
    }

    public enum AugmentUpgradeQuoteStatusV1
    {
        Quoted = 1,
        InvalidRequest = 2,
        MissingEquipment = 3,
        MissingAugment = 4,
        UnknownAugmentDefinition = 5,
        InvalidLevelJump = 6,
        MaximumLevel = 7,
        MissingCostCurve = 8,
        CostOverflow = 9,
        InvalidCatalog = 10,
    }

    public enum AugmentUpgradeConfirmationStatusV1
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        PendingRetry = 4,
        InvalidRequest = 5,
        MissingEquipment = 6,
        MissingAugment = 7,
        StaleEquipmentFingerprint = 8,
        CurrentLevelMismatch = 9,
        InvalidLevelJump = 10,
        MaximumLevel = 11,
        StaleQuote = 12,
        StaleCostPolicy = 13,
        StaleCatalog = 14,
        WalletSequenceConflict = 15,
        HoldingsSequenceConflict = 16,
        InsufficientFunds = 17,
        EquipmentValidationRejected = 18,
        RewardCommitRejected = 19,
        MoneyAuthorityRejected = 20,
        HoldingsAuthorityRejected = 21,
        RewardApplicationRejected = 22,
        UnknownConfirmation = 23,
    }

    public sealed class AugmentTierCostCurveV1 :
        IComparable<AugmentTierCostCurveV1>,
        IEquatable<AugmentTierCostCurveV1>
    {
        private readonly string canonicalText;

        private AugmentTierCostCurveV1(int tier, long baseCost, long perTargetLevelCost)
        {
            if (tier < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tier));
            }

            if (baseCost < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(baseCost));
            }

            if (perTargetLevelCost < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(perTargetLevelCost));
            }

            Tier = tier;
            BaseCost = baseCost;
            PerTargetLevelCost = perTargetLevelCost;
            canonicalText = "tier=" + Tier.ToString(CultureInfo.InvariantCulture)
                + "\nbase_cost=" + BaseCost.ToString(CultureInfo.InvariantCulture)
                + "\nper_target_level_cost="
                + PerTargetLevelCost.ToString(CultureInfo.InvariantCulture);
            Fingerprint = AugmentUpgradeCanonicalV1.Fingerprint(canonicalText);
        }

        public int Tier { get; }
        public long BaseCost { get; }
        public long PerTargetLevelCost { get; }
        public string Fingerprint { get; }

        public static AugmentTierCostCurveV1 Create(
            int tier,
            long baseCost,
            long perTargetLevelCost)
        {
            return new AugmentTierCostCurveV1(tier, baseCost, perTargetLevelCost);
        }

        public bool TryGetStepCost(int targetLevel, out long cost)
        {
            cost = 0L;
            if (targetLevel < 1)
            {
                return false;
            }

            try
            {
                cost = checked(
                    BaseCost
                    + checked(PerTargetLevelCost * (long)(targetLevel - 1)));
                return cost > 0L;
            }
            catch (OverflowException)
            {
                cost = 0L;
                return false;
            }
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public int CompareTo(AugmentTierCostCurveV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : Tier.CompareTo(other.Tier);
        }

        public bool Equals(AugmentTierCostCurveV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AugmentTierCostCurveV1);
        }

        public override int GetHashCode()
        {
            return AugmentUpgradeCanonicalV1.DeterministicHash(canonicalText);
        }
    }

    public sealed class AugmentUpgradeCostPolicyV1 :
        IEquatable<AugmentUpgradeCostPolicyV1>
    {
        private readonly ReadOnlyCollection<AugmentTierCostCurveV1> curves;
        private readonly Dictionary<int, AugmentTierCostCurveV1> curvesByTier;
        private readonly string canonicalText;

        private AugmentUpgradeCostPolicyV1(
            StableId policyStableId,
            int version,
            bool permitsMultiLevelTargets,
            IEnumerable<AugmentTierCostCurveV1> curves)
        {
            PolicyStableId = policyStableId
                ?? throw new ArgumentNullException(nameof(policyStableId));
            if (version < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            if (curves == null)
            {
                throw new ArgumentNullException(nameof(curves));
            }

            Version = version;
            PermitsMultiLevelTargets = permitsMultiLevelTargets;
            var copy = new List<AugmentTierCostCurveV1>();
            curvesByTier = new Dictionary<int, AugmentTierCostCurveV1>();
            foreach (AugmentTierCostCurveV1 curve in curves)
            {
                if (curve == null)
                {
                    throw new ArgumentException(
                        "Cost curves must not contain null entries.",
                        nameof(curves));
                }

                if (curvesByTier.ContainsKey(curve.Tier))
                {
                    throw new ArgumentException(
                        "Cost curves contain duplicate tier "
                        + curve.Tier.ToString(CultureInfo.InvariantCulture)
                        + ".",
                        nameof(curves));
                }

                curvesByTier.Add(curve.Tier, curve);
                copy.Add(curve);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one augment tier cost curve is required.",
                    nameof(curves));
            }

            copy.Sort();
            this.curves = new ReadOnlyCollection<AugmentTierCostCurveV1>(copy);
            var builder = new StringBuilder();
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "policy_stable_id",
                PolicyStableId.ToString());
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "version",
                Version.ToString(CultureInfo.InvariantCulture));
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "permits_multi_level_targets",
                PermitsMultiLevelTargets ? "true" : "false");
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "curve_count",
                this.curves.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.curves.Count; index++)
            {
                AugmentUpgradeCanonicalV1.AppendToken(
                    builder,
                    "curve_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    this.curves[index].ToCanonicalString());
            }

            canonicalText = builder.ToString();
            Fingerprint = AugmentUpgradeCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId PolicyStableId { get; }
        public int Version { get; }
        public bool PermitsMultiLevelTargets { get; }
        public IReadOnlyList<AugmentTierCostCurveV1> Curves { get { return curves; } }
        public string Fingerprint { get; }

        public static AugmentUpgradeCostPolicyV1 Create(
            StableId policyStableId,
            int version,
            bool permitsMultiLevelTargets,
            IEnumerable<AugmentTierCostCurveV1> curves)
        {
            return new AugmentUpgradeCostPolicyV1(
                policyStableId,
                version,
                permitsMultiLevelTargets,
                curves);
        }

        public AugmentUpgradeCostStatusV1 TryCalculateCost(
            int tier,
            int currentLevel,
            int targetLevel,
            out long cost)
        {
            cost = 0L;
            if (currentLevel < 1
                || targetLevel <= currentLevel
                || (!PermitsMultiLevelTargets && targetLevel != currentLevel + 1))
            {
                return AugmentUpgradeCostStatusV1.InvalidTarget;
            }

            AugmentTierCostCurveV1 curve;
            if (!curvesByTier.TryGetValue(tier, out curve))
            {
                return AugmentUpgradeCostStatusV1.TierNotConfigured;
            }

            try
            {
                long total = 0L;
                for (long level = (long)currentLevel + 1L; level <= targetLevel; level++)
                {
                    long stepCost;
                    if (!curve.TryGetStepCost((int)level, out stepCost))
                    {
                        return AugmentUpgradeCostStatusV1.ArithmeticOverflow;
                    }

                    total = checked(total + stepCost);
                }

                if (total < 1L)
                {
                    return AugmentUpgradeCostStatusV1.ArithmeticOverflow;
                }

                cost = total;
                return AugmentUpgradeCostStatusV1.Calculated;
            }
            catch (OverflowException)
            {
                cost = 0L;
                return AugmentUpgradeCostStatusV1.ArithmeticOverflow;
            }
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(AugmentUpgradeCostPolicyV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AugmentUpgradeCostPolicyV1);
        }

        public override int GetHashCode()
        {
            return AugmentUpgradeCanonicalV1.DeterministicHash(canonicalText);
        }
    }
}

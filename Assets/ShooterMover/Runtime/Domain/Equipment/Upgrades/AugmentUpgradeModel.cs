using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Equipment.Upgrades
{
    public enum AugmentUpgradeCostStatus
    {
        Calculated = 1,
        InvalidTarget = 2,
        TierNotConfigured = 3,
        ArithmeticOverflow = 4,
    }

    public enum AugmentUpgradeQuoteStatus
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

    public enum AugmentUpgradeConfirmationStatus
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

    public sealed class AugmentTierCostCurve :
        IComparable<AugmentTierCostCurve>,
        IEquatable<AugmentTierCostCurve>
    {
        private readonly string canonicalText;

        private AugmentTierCostCurve(int tier, long baseCost, long perTargetLevelCost)
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
            Fingerprint = AugmentUpgrade.Fingerprint(canonicalText);
        }

        public int Tier { get; }
        public long BaseCost { get; }
        public long PerTargetLevelCost { get; }
        public string Fingerprint { get; }

        public static AugmentTierCostCurve Create(
            int tier,
            long baseCost,
            long perTargetLevelCost)
        {
            return new AugmentTierCostCurve(tier, baseCost, perTargetLevelCost);
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

        public int CompareTo(AugmentTierCostCurve other)
        {
            return ReferenceEquals(other, null) ? 1 : Tier.CompareTo(other.Tier);
        }

        public bool Equals(AugmentTierCostCurve other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AugmentTierCostCurve);
        }

        public override int GetHashCode()
        {
            return AugmentUpgrade.DeterministicHash(canonicalText);
        }
    }

    public sealed class AugmentUpgradeCostPolicy :
        IEquatable<AugmentUpgradeCostPolicy>
    {
        private readonly ReadOnlyCollection<AugmentTierCostCurve> curves;
        private readonly Dictionary<int, AugmentTierCostCurve> curvesByTier;
        private readonly string canonicalText;

        private AugmentUpgradeCostPolicy(
            StableId policyStableId,
            int version,
            bool permitsMultiLevelTargets,
            IEnumerable<AugmentTierCostCurve> curves)
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
            var copy = new List<AugmentTierCostCurve>();
            curvesByTier = new Dictionary<int, AugmentTierCostCurve>();
            foreach (AugmentTierCostCurve curve in curves)
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
            this.curves = new ReadOnlyCollection<AugmentTierCostCurve>(copy);
            var builder = new StringBuilder();
            AugmentUpgrade.AppendToken(
                builder,
                "policy_stable_id",
                PolicyStableId.ToString());
            AugmentUpgrade.AppendToken(
                builder,
                "version",
                Version.ToString(CultureInfo.InvariantCulture));
            AugmentUpgrade.AppendToken(
                builder,
                "permits_multi_level_targets",
                PermitsMultiLevelTargets ? "true" : "false");
            AugmentUpgrade.AppendToken(
                builder,
                "curve_count",
                this.curves.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.curves.Count; index++)
            {
                AugmentUpgrade.AppendToken(
                    builder,
                    "curve_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    this.curves[index].ToCanonicalString());
            }

            canonicalText = builder.ToString();
            Fingerprint = AugmentUpgrade.Fingerprint(canonicalText);
        }

        public StableId PolicyStableId { get; }
        public int Version { get; }
        public bool PermitsMultiLevelTargets { get; }
        public IReadOnlyList<AugmentTierCostCurve> Curves { get { return curves; } }
        public string Fingerprint { get; }

        public static AugmentUpgradeCostPolicy Create(
            StableId policyStableId,
            int version,
            bool permitsMultiLevelTargets,
            IEnumerable<AugmentTierCostCurve> curves)
        {
            return new AugmentUpgradeCostPolicy(
                policyStableId,
                version,
                permitsMultiLevelTargets,
                curves);
        }

        public AugmentUpgradeCostStatus TryCalculateCost(
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
                return AugmentUpgradeCostStatus.InvalidTarget;
            }

            AugmentTierCostCurve curve;
            if (!curvesByTier.TryGetValue(tier, out curve))
            {
                return AugmentUpgradeCostStatus.TierNotConfigured;
            }

            try
            {
                long total = 0L;
                for (long level = (long)currentLevel + 1L; level <= targetLevel; level++)
                {
                    long stepCost;
                    if (!curve.TryGetStepCost((int)level, out stepCost))
                    {
                        return AugmentUpgradeCostStatus.ArithmeticOverflow;
                    }

                    total = checked(total + stepCost);
                }

                if (total < 1L)
                {
                    return AugmentUpgradeCostStatus.ArithmeticOverflow;
                }

                cost = total;
                return AugmentUpgradeCostStatus.Calculated;
            }
            catch (OverflowException)
            {
                cost = 0L;
                return AugmentUpgradeCostStatus.ArithmeticOverflow;
            }
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(AugmentUpgradeCostPolicy other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AugmentUpgradeCostPolicy);
        }

        public override int GetHashCode()
        {
            return AugmentUpgrade.DeterministicHash(canonicalText);
        }
    }
}

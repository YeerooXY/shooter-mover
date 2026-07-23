using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Domain.Rewards.Drops
{
    public enum RewardOutcomeDispositionV1
    {
        Grant = 1,
        ExplicitNoDrop = 2,
    }

    /// <summary>
    /// One authored outcome inside a reward roll group. A grant outcome retains the
    /// existing REW-001 specification, including its independently authored quantity
    /// range and scaling-input descriptors.
    /// </summary>
    public sealed class RewardOutcomeV1 :
        IComparable<RewardOutcomeV1>,
        IEquatable<RewardOutcomeV1>
    {
        private readonly string canonicalText;

        private RewardOutcomeV1(
            StableId outcomeStableId,
            RewardOutcomeDispositionV1 disposition,
            RewardGrantSpecificationV1 grant,
            ulong weight)
        {
            OutcomeStableId = outcomeStableId
                ?? throw new ArgumentNullException(nameof(outcomeStableId));
            if (!Enum.IsDefined(typeof(RewardOutcomeDispositionV1), disposition))
            {
                throw new ArgumentOutOfRangeException(nameof(disposition));
            }
            if (weight == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }
            if ((disposition == RewardOutcomeDispositionV1.Grant) != (grant != null))
            {
                throw new ArgumentException(
                    "Grant outcomes require a grant and explicit no-drop outcomes must not carry one.",
                    nameof(grant));
            }

            Disposition = disposition;
            Grant = grant;
            Weight = weight;
            var builder = new StringBuilder("schema=reward-outcome-v1");
            builder.Append("\noutcome_id=").Append(OutcomeStableId)
                .Append("\ndisposition=").Append(((int)Disposition).ToString(CultureInfo.InvariantCulture))
                .Append("\nweight=").Append(Weight.ToString(CultureInfo.InvariantCulture))
                .Append("\ngrant=").Append(Grant == null ? "none" : Grant.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public StableId OutcomeStableId { get; }
        public RewardOutcomeDispositionV1 Disposition { get; }
        public RewardGrantSpecificationV1 Grant { get; }
        public ulong Weight { get; }
        public string Fingerprint { get; }
        public bool IsExplicitNoDrop
        {
            get { return Disposition == RewardOutcomeDispositionV1.ExplicitNoDrop; }
        }

        public static RewardOutcomeV1 CreateGrant(
            StableId outcomeStableId,
            RewardGrantSpecificationV1 grant,
            ulong weight)
        {
            return new RewardOutcomeV1(
                outcomeStableId,
                RewardOutcomeDispositionV1.Grant,
                grant ?? throw new ArgumentNullException(nameof(grant)),
                weight);
        }

        public static RewardOutcomeV1 CreateExplicitNoDrop(
            StableId outcomeStableId,
            ulong weight)
        {
            return new RewardOutcomeV1(
                outcomeStableId,
                RewardOutcomeDispositionV1.ExplicitNoDrop,
                null,
                weight);
        }

        public RewardOutcomeV1 WithGrant(
            StableId resultOutcomeStableId,
            RewardGrantSpecificationV1 resultGrant)
        {
            if (IsExplicitNoDrop)
            {
                return new RewardOutcomeV1(
                    resultOutcomeStableId,
                    Disposition,
                    null,
                    Weight);
            }
            return new RewardOutcomeV1(
                resultOutcomeStableId,
                Disposition,
                resultGrant,
                Weight);
        }

        public int CompareTo(RewardOutcomeV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : OutcomeStableId.CompareTo(other.OutcomeStableId);
        }

        public bool Equals(RewardOutcomeV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardOutcomeV1);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(canonicalText);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }
    }
}

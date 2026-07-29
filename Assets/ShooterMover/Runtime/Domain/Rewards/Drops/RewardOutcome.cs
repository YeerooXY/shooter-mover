using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Domain.Rewards.Drops
{
    public enum RewardOutcomeDisposition
    {
        Grant = 1,
        ExplicitNoDrop = 2,
    }

    /// <summary>
    /// One authored outcome inside a reward roll group. A grant outcome retains the
    /// existing REW-001 specification, including its independently authored quantity
    /// range and scaling-input descriptors.
    /// </summary>
    public sealed class RewardOutcome :
        IComparable<RewardOutcome>,
        IEquatable<RewardOutcome>
    {
        private readonly string canonicalText;

        private RewardOutcome(
            StableId outcomeStableId,
            RewardOutcomeDisposition disposition,
            RewardGrantSpecification grant,
            ulong weight)
        {
            OutcomeStableId = outcomeStableId
                ?? throw new ArgumentNullException(nameof(outcomeStableId));
            if (!Enum.IsDefined(typeof(RewardOutcomeDisposition), disposition))
            {
                throw new ArgumentOutOfRangeException(nameof(disposition));
            }
            if (weight == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }
            if ((disposition == RewardOutcomeDisposition.Grant) != (grant != null))
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
            Fingerprint = RewardGenerationFingerprint.Compute(canonicalText);
        }

        public StableId OutcomeStableId { get; }
        public RewardOutcomeDisposition Disposition { get; }
        public RewardGrantSpecification Grant { get; }
        public ulong Weight { get; }
        public string Fingerprint { get; }
        public bool IsExplicitNoDrop
        {
            get { return Disposition == RewardOutcomeDisposition.ExplicitNoDrop; }
        }

        public static RewardOutcome CreateGrant(
            StableId outcomeStableId,
            RewardGrantSpecification grant,
            ulong weight)
        {
            return new RewardOutcome(
                outcomeStableId,
                RewardOutcomeDisposition.Grant,
                grant ?? throw new ArgumentNullException(nameof(grant)),
                weight);
        }

        public static RewardOutcome CreateExplicitNoDrop(
            StableId outcomeStableId,
            ulong weight)
        {
            return new RewardOutcome(
                outcomeStableId,
                RewardOutcomeDisposition.ExplicitNoDrop,
                null,
                weight);
        }

        public RewardOutcome WithGrant(
            StableId resultOutcomeStableId,
            RewardGrantSpecification resultGrant)
        {
            if (IsExplicitNoDrop)
            {
                return new RewardOutcome(
                    resultOutcomeStableId,
                    Disposition,
                    null,
                    Weight);
            }
            return new RewardOutcome(
                resultOutcomeStableId,
                Disposition,
                resultGrant,
                Weight);
        }

        public int CompareTo(RewardOutcome other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : OutcomeStableId.CompareTo(other.OutcomeStableId);
        }

        public bool Equals(RewardOutcome other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardOutcome);
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

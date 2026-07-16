using System;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Equipment.Upgrades
{
    public sealed class AugmentUpgradeConfirmationV1 :
        IEquatable<AugmentUpgradeConfirmationV1>
    {
        private readonly string canonicalText;

        private AugmentUpgradeConfirmationV1(
            StableId confirmationStableId,
            AugmentUpgradeQuoteV1 quote,
            string quotedFingerprint)
        {
            ConfirmationStableId = confirmationStableId
                ?? throw new ArgumentNullException(nameof(confirmationStableId));
            Quote = quote ?? throw new ArgumentNullException(nameof(quote));
            QuotedFingerprint = quotedFingerprint;
            var builder = new StringBuilder();
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "confirmation_stable_id",
                ConfirmationStableId.ToString());
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "quote",
                Quote.ToCanonicalString());
            AugmentUpgradeCanonicalV1.AppendToken(
                builder,
                "quoted_fingerprint",
                QuotedFingerprint ?? "null");
            canonicalText = builder.ToString();
            Fingerprint = AugmentUpgradeCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId ConfirmationStableId { get; }
        public AugmentUpgradeQuoteV1 Quote { get; }
        public string QuotedFingerprint { get; }
        public string Fingerprint { get; }

        public static AugmentUpgradeConfirmationV1 Create(
            StableId confirmationStableId,
            AugmentUpgradeQuoteV1 quote)
        {
            return new AugmentUpgradeConfirmationV1(
                confirmationStableId,
                quote,
                quote == null ? null : quote.QuoteFingerprint);
        }

        public static AugmentUpgradeConfirmationV1 Create(
            StableId confirmationStableId,
            AugmentUpgradeQuoteV1 quote,
            string quotedFingerprint)
        {
            return new AugmentUpgradeConfirmationV1(
                confirmationStableId,
                quote,
                quotedFingerprint);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(AugmentUpgradeConfirmationV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AugmentUpgradeConfirmationV1);
        }

        public override int GetHashCode()
        {
            return AugmentUpgradeCanonicalV1.DeterministicHash(canonicalText);
        }
    }

    public sealed class AugmentUpgradeRetryCommandV1
    {
        public AugmentUpgradeRetryCommandV1(StableId confirmationStableId)
        {
            ConfirmationStableId = confirmationStableId;
        }

        public StableId ConfirmationStableId { get; }
    }

    public sealed class AugmentUpgradeIdentityContextV1
    {
        public AugmentUpgradeIdentityContextV1(
            StableId runStableId,
            StableId sourceInstanceStableId,
            StableId claimantStableId,
            StableId rewardProfileStableId,
            StableId scrapAuthorityStableId)
        {
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            SourceInstanceStableId = sourceInstanceStableId
                ?? throw new ArgumentNullException(nameof(sourceInstanceStableId));
            ClaimantStableId = claimantStableId
                ?? throw new ArgumentNullException(nameof(claimantStableId));
            RewardProfileStableId = rewardProfileStableId
                ?? throw new ArgumentNullException(nameof(rewardProfileStableId));
            ScrapAuthorityStableId = scrapAuthorityStableId
                ?? throw new ArgumentNullException(nameof(scrapAuthorityStableId));
        }

        public StableId RunStableId { get; }
        public StableId SourceInstanceStableId { get; }
        public StableId ClaimantStableId { get; }
        public StableId RewardProfileStableId { get; }
        public StableId ScrapAuthorityStableId { get; }
    }
}

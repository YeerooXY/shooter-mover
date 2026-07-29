using System;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Equipment.Upgrades
{
    public sealed class AugmentUpgradeConfirmation :
        IEquatable<AugmentUpgradeConfirmation>
    {
        private readonly string canonicalText;

        private AugmentUpgradeConfirmation(
            StableId confirmationStableId,
            AugmentUpgradeQuote quote,
            string quotedFingerprint)
        {
            ConfirmationStableId = confirmationStableId
                ?? throw new ArgumentNullException(nameof(confirmationStableId));
            Quote = quote ?? throw new ArgumentNullException(nameof(quote));
            QuotedFingerprint = quotedFingerprint;
            var builder = new StringBuilder();
            AugmentUpgrade.AppendToken(
                builder,
                "confirmation_stable_id",
                ConfirmationStableId.ToString());
            AugmentUpgrade.AppendToken(
                builder,
                "quote",
                Quote.ToCanonicalString());
            AugmentUpgrade.AppendToken(
                builder,
                "quoted_fingerprint",
                QuotedFingerprint ?? "null");
            canonicalText = builder.ToString();
            Fingerprint = AugmentUpgrade.Fingerprint(canonicalText);
        }

        public StableId ConfirmationStableId { get; }
        public AugmentUpgradeQuote Quote { get; }
        public string QuotedFingerprint { get; }
        public string Fingerprint { get; }

        public static AugmentUpgradeConfirmation Create(
            StableId confirmationStableId,
            AugmentUpgradeQuote quote)
        {
            return new AugmentUpgradeConfirmation(
                confirmationStableId,
                quote,
                quote == null ? null : quote.QuoteFingerprint);
        }

        public static AugmentUpgradeConfirmation Create(
            StableId confirmationStableId,
            AugmentUpgradeQuote quote,
            string quotedFingerprint)
        {
            return new AugmentUpgradeConfirmation(
                confirmationStableId,
                quote,
                quotedFingerprint);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(AugmentUpgradeConfirmation other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AugmentUpgradeConfirmation);
        }

        public override int GetHashCode()
        {
            return AugmentUpgrade.DeterministicHash(canonicalText);
        }
    }

    public sealed class AugmentUpgradeRetryCommand
    {
        public AugmentUpgradeRetryCommand(StableId confirmationStableId)
        {
            ConfirmationStableId = confirmationStableId;
        }

        public StableId ConfirmationStableId { get; }
    }

    public sealed class AugmentUpgradeIdentityContext
    {
        public AugmentUpgradeIdentityContext(
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

using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public sealed class StrongboxInstanceLevelRollV1 :
        IEquatable<StrongboxInstanceLevelRollV1>
    {
        private readonly string canonicalText;

        internal StrongboxInstanceLevelRollV1(
            StrongboxTargetLevelRollV1 targetRoll,
            int definitionPeakLevel,
            StableId rarityId,
            int hybridCenterLevel,
            int variationOffset,
            int itemLevel,
            ulong samplesConsumed,
            string policyFingerprint)
        {
            TargetRoll = targetRoll ?? throw new ArgumentNullException(nameof(targetRoll));
            RarityId = rarityId ?? throw new ArgumentNullException(nameof(rarityId));
            if (definitionPeakLevel < 1 || hybridCenterLevel < 1 || itemLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(definitionPeakLevel));
            }
            if (!StrongboxCanonicalV1.IsFingerprint(policyFingerprint))
            {
                throw new ArgumentException(
                    "A canonical hybrid-loot policy fingerprint is required.",
                    nameof(policyFingerprint));
            }

            DefinitionPeakLevel = definitionPeakLevel;
            HybridCenterLevel = hybridCenterLevel;
            VariationOffset = variationOffset;
            ItemLevel = itemLevel;
            SamplesConsumed = samplesConsumed;
            PolicyFingerprint = policyFingerprint;

            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "target_roll", TargetRoll.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "definition_peak_level", DefinitionPeakLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "rarity_id", RarityId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "hybrid_center_level", HybridCenterLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "variation_offset", VariationOffset.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "item_level", ItemLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "samples_consumed", SamplesConsumed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "policy_fingerprint", PolicyFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StrongboxTargetLevelRollV1 TargetRoll { get; }
        public int DefinitionPeakLevel { get; }
        public StableId RarityId { get; }
        public int HybridCenterLevel { get; }
        public int VariationOffset { get; }
        public int ItemLevel { get; }
        public ulong SamplesConsumed { get; }
        public string PolicyFingerprint { get; }
        public string Fingerprint { get; }

        public int DefinitionDistanceFromTarget
        {
            get { return DefinitionPeakLevel - TargetRoll.TargetLevel; }
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxInstanceLevelRollV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxInstanceLevelRollV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }
    }
}

using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public sealed class StrongboxInstanceLevelRoll :
        IEquatable<StrongboxInstanceLevelRoll>
    {
        private readonly string canonicalText;

        internal StrongboxInstanceLevelRoll(
            StrongboxTargetLevelRoll targetRoll,
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
            if (!Strongbox.IsFingerprint(policyFingerprint))
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
            Strongbox.AppendToken(builder, "target_roll", TargetRoll.ToCanonicalString());
            Strongbox.AppendToken(builder, "definition_peak_level", DefinitionPeakLevel.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "rarity_id", RarityId.ToString());
            Strongbox.AppendToken(builder, "hybrid_center_level", HybridCenterLevel.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "variation_offset", VariationOffset.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "item_level", ItemLevel.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "samples_consumed", SamplesConsumed.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "policy_fingerprint", PolicyFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public StrongboxTargetLevelRoll TargetRoll { get; }
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

        public bool Equals(StrongboxInstanceLevelRoll other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxInstanceLevelRoll);
        }

        public override int GetHashCode()
        {
            return Strongbox.DeterministicHash(canonicalText);
        }
    }
}

using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public sealed class StrongboxAugmentSignatureV1 :
        IEquatable<StrongboxAugmentSignatureV1>
    {
        private readonly string canonicalText;

        internal StrongboxAugmentSignatureV1(
            StableId policyId,
            StableId rarityId,
            int playerLevel,
            int itemLevel,
            int effectiveBiasLevels,
            int normalMaximumSlots,
            int absoluteMaximumSlots,
            int authoredSlotOutcome,
            int slotCount,
            int sharedLevel,
            ulong slotSamplesConsumed,
            ulong levelSamplesConsumed,
            string policyFingerprint)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            RarityId = rarityId ?? throw new ArgumentNullException(nameof(rarityId));
            if (playerLevel < 0 || itemLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }
            if (normalMaximumSlots < 0
                || absoluteMaximumSlots < normalMaximumSlots
                || slotCount < 0
                || slotCount > absoluteMaximumSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount));
            }
            if ((slotCount == 0 && sharedLevel != 0)
                || (slotCount > 0 && sharedLevel < 1))
            {
                throw new ArgumentOutOfRangeException(nameof(sharedLevel));
            }
            if (!StrongboxCanonicalV1.IsFingerprint(policyFingerprint))
            {
                throw new ArgumentException(
                    "A canonical hybrid-loot policy fingerprint is required.",
                    nameof(policyFingerprint));
            }

            PlayerLevel = playerLevel;
            ItemLevel = itemLevel;
            EffectiveBiasLevels = effectiveBiasLevels;
            NormalMaximumSlots = normalMaximumSlots;
            AbsoluteMaximumSlots = absoluteMaximumSlots;
            AuthoredSlotOutcome = authoredSlotOutcome;
            SlotCount = slotCount;
            SharedLevel = sharedLevel;
            SlotSamplesConsumed = slotSamplesConsumed;
            LevelSamplesConsumed = levelSamplesConsumed;
            PolicyFingerprint = policyFingerprint;

            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "policy_id", PolicyId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "rarity_id", RarityId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "player_level", PlayerLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "item_level", ItemLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "effective_bias_levels", EffectiveBiasLevels.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "normal_maximum_slots", NormalMaximumSlots.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "absolute_maximum_slots", AbsoluteMaximumSlots.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "authored_slot_outcome", AuthoredSlotOutcome.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "slot_count", SlotCount.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "shared_level", SharedLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "slot_samples_consumed", SlotSamplesConsumed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "level_samples_consumed", LevelSamplesConsumed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "policy_fingerprint", PolicyFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId PolicyId { get; }
        public StableId RarityId { get; }
        public int PlayerLevel { get; }
        public int ItemLevel { get; }
        public int EffectiveBiasLevels { get; }
        public int NormalMaximumSlots { get; }
        public int AbsoluteMaximumSlots { get; }
        public int AuthoredSlotOutcome { get; }
        public int SlotCount { get; }
        public int SharedLevel { get; }
        public ulong SlotSamplesConsumed { get; }
        public ulong LevelSamplesConsumed { get; }
        public string PolicyFingerprint { get; }
        public string Fingerprint { get; }

        public bool HasAugmentCapacity { get { return SlotCount > 0; } }
        public bool HasOvercapSlot { get { return SlotCount > NormalMaximumSlots; } }
        public bool HasOvercapLevel { get { return SharedLevel > 10; } }

        public string DisplaySignature
        {
            get
            {
                return SlotCount == 0
                    ? "0/0"
                    : SharedLevel.ToString(CultureInfo.InvariantCulture)
                        + "/"
                        + SlotCount.ToString(CultureInfo.InvariantCulture);
            }
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxAugmentSignatureV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxAugmentSignatureV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }
    }
}

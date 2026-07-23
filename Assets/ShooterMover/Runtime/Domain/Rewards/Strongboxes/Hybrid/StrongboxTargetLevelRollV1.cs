using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public sealed class StrongboxTargetLevelRollV1 :
        IEquatable<StrongboxTargetLevelRollV1>
    {
        private readonly string canonicalText;

        internal StrongboxTargetLevelRollV1(
            StableId policyId,
            int playerLevel,
            int rolledDelta,
            int unclampedTargetLevel,
            int targetLevel,
            ulong samplesConsumed,
            string policyFingerprint)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            if (playerLevel < 0 || targetLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }
            if (!StrongboxCanonicalV1.IsFingerprint(policyFingerprint))
            {
                throw new ArgumentException(
                    "A canonical hybrid-loot policy fingerprint is required.",
                    nameof(policyFingerprint));
            }

            PlayerLevel = playerLevel;
            RolledDelta = rolledDelta;
            UnclampedTargetLevel = unclampedTargetLevel;
            TargetLevel = targetLevel;
            SamplesConsumed = samplesConsumed;
            PolicyFingerprint = policyFingerprint;

            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "policy_id", PolicyId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "player_level", PlayerLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "rolled_delta", RolledDelta.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "unclamped_target_level", UnclampedTargetLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "target_level", TargetLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "samples_consumed", SamplesConsumed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "policy_fingerprint", PolicyFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId PolicyId { get; }
        public int PlayerLevel { get; }
        public int RolledDelta { get; }
        public int UnclampedTargetLevel { get; }
        public int TargetLevel { get; }
        public ulong SamplesConsumed { get; }
        public string PolicyFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxTargetLevelRollV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxTargetLevelRollV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }
    }
}

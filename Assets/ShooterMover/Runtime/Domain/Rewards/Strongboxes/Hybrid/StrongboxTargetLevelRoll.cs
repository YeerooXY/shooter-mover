using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public sealed class StrongboxTargetLevelRoll :
        IEquatable<StrongboxTargetLevelRoll>
    {
        private readonly string canonicalText;

        internal StrongboxTargetLevelRoll(
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
            if (!Strongbox.IsFingerprint(policyFingerprint))
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
            Strongbox.AppendToken(builder, "policy_id", PolicyId.ToString());
            Strongbox.AppendToken(builder, "player_level", PlayerLevel.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "rolled_delta", RolledDelta.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "unclamped_target_level", UnclampedTargetLevel.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "target_level", TargetLevel.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "samples_consumed", SamplesConsumed.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "policy_fingerprint", PolicyFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
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

        public bool Equals(StrongboxTargetLevelRoll other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxTargetLevelRoll);
        }

        public override int GetHashCode()
        {
            return Strongbox.DeterministicHash(canonicalText);
        }
    }
}

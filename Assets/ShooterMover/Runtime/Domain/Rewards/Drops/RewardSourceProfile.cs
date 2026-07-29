using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Rewards.Drops
{
    /// <summary>
    /// Engine-neutral ordered reward-source profile. Enemy, prop, treasure, room
    /// placement and player-authored content reference this stable profile ID; they
    /// do not subclass a reward-producing runtime type.
    /// </summary>
    public sealed class RewardSourceProfile :
        IEquatable<RewardSourceProfile>
    {
        private readonly ReadOnlyCollection<RewardRollGroup> groups;
        private readonly string canonicalText;

        private RewardSourceProfile(
            StableId profileStableId,
            bool explicitNoDrop,
            StableId defaultStrongboxTierSelectionProfileId,
            IEnumerable<RewardRollGroup> groups)
        {
            ProfileStableId = profileStableId
                ?? throw new ArgumentNullException(nameof(profileStableId));
            ExplicitNoDrop = explicitNoDrop;
            DefaultStrongboxTierSelectionProfileId =
                defaultStrongboxTierSelectionProfileId;
            this.groups = CopyGroups(groups);
            if (ExplicitNoDrop && this.groups.Count != 0)
            {
                throw new ArgumentException(
                    "Explicit no-drop profiles must not contain groups.",
                    nameof(groups));
            }
            if (!ExplicitNoDrop && this.groups.Count == 0)
            {
                throw new ArgumentException(
                    "Configured profiles require at least one roll group.",
                    nameof(groups));
            }

            bool hasStrongbox = false;
            for (int index = 0; index < this.groups.Count; index++)
            {
                hasStrongbox |= this.groups[index].ContainsStrongbox;
            }
            if (hasStrongbox != (DefaultStrongboxTierSelectionProfileId != null))
            {
                throw new ArgumentException(
                    "Strongbox-producing profiles require exactly one default tier-selection profile.");
            }

            var builder = new StringBuilder("schema=reward-source-profile-v1");
            builder.Append("\nprofile_id=").Append(ProfileStableId)
                .Append("\nexplicit_no_drop=").Append(ExplicitNoDrop ? "1" : "0")
                .Append("\ndefault_tier_profile=")
                .Append(DefaultStrongboxTierSelectionProfileId == null
                    ? "none"
                    : DefaultStrongboxTierSelectionProfileId.ToString())
                .Append("\ngroup_count=").Append(this.groups.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.groups.Count; index++)
            {
                builder.Append("\ngroup_").Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n").Append(this.groups[index].ToCanonicalString());
            }
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprint.Compute(canonicalText);
        }

        public StableId ProfileStableId { get; }
        public bool ExplicitNoDrop { get; }
        public StableId DefaultStrongboxTierSelectionProfileId { get; }
        public IReadOnlyList<RewardRollGroup> Groups { get { return groups; } }
        public string Fingerprint { get; }

        public static RewardSourceProfile Create(
            StableId profileStableId,
            StableId defaultStrongboxTierSelectionProfileId,
            IEnumerable<RewardRollGroup> groups)
        {
            return new RewardSourceProfile(
                profileStableId,
                false,
                defaultStrongboxTierSelectionProfileId,
                groups);
        }

        public static RewardSourceProfile CreateExplicitNoDrop(
            StableId profileStableId)
        {
            return new RewardSourceProfile(
                profileStableId,
                true,
                null,
                Array.Empty<RewardRollGroup>());
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(RewardSourceProfile other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardSourceProfile);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(canonicalText);
        }

        private static ReadOnlyCollection<RewardRollGroup> CopyGroups(
            IEnumerable<RewardRollGroup> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var copy = new List<RewardRollGroup>();
            var ids = new HashSet<StableId>();
            foreach (RewardRollGroup group in source)
            {
                if (group == null || !ids.Add(group.GroupStableId))
                {
                    throw new ArgumentException(
                        "Reward groups must be non-null and have unique identities.",
                        nameof(source));
                }
                copy.Add(group);
            }
            copy.Sort();
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index].Ordinal != index)
                {
                    throw new ArgumentException(
                        "Reward groups must use contiguous ordered ordinals beginning at zero.",
                        nameof(source));
                }
            }
            return new ReadOnlyCollection<RewardRollGroup>(copy);
        }
    }
}

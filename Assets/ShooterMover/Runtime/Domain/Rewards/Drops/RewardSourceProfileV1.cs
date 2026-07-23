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
    public sealed class RewardSourceProfileV1 :
        IEquatable<RewardSourceProfileV1>
    {
        private readonly ReadOnlyCollection<RewardRollGroupV1> groups;
        private readonly string canonicalText;

        private RewardSourceProfileV1(
            StableId profileStableId,
            bool explicitNoDrop,
            StableId defaultStrongboxTierSelectionProfileId,
            IEnumerable<RewardRollGroupV1> groups)
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
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public StableId ProfileStableId { get; }
        public bool ExplicitNoDrop { get; }
        public StableId DefaultStrongboxTierSelectionProfileId { get; }
        public IReadOnlyList<RewardRollGroupV1> Groups { get { return groups; } }
        public string Fingerprint { get; }

        public static RewardSourceProfileV1 Create(
            StableId profileStableId,
            StableId defaultStrongboxTierSelectionProfileId,
            IEnumerable<RewardRollGroupV1> groups)
        {
            return new RewardSourceProfileV1(
                profileStableId,
                false,
                defaultStrongboxTierSelectionProfileId,
                groups);
        }

        public static RewardSourceProfileV1 CreateExplicitNoDrop(
            StableId profileStableId)
        {
            return new RewardSourceProfileV1(
                profileStableId,
                true,
                null,
                Array.Empty<RewardRollGroupV1>());
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(RewardSourceProfileV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardSourceProfileV1);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(canonicalText);
        }

        private static ReadOnlyCollection<RewardRollGroupV1> CopyGroups(
            IEnumerable<RewardRollGroupV1> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var copy = new List<RewardRollGroupV1>();
            var ids = new HashSet<StableId>();
            foreach (RewardRollGroupV1 group in source)
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
            return new ReadOnlyCollection<RewardRollGroupV1>(copy);
        }
    }
}

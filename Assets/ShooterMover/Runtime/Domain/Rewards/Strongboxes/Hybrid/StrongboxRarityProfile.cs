using System;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public sealed class StrongboxRarityProfile :
        IComparable<StrongboxRarityProfile>,
        IEquatable<StrongboxRarityProfile>
    {
        public StrongboxRarityProfile(
            StableId rarityId,
            int selectionMultiplierMilli,
            int augmentBiasLevels)
        {
            RarityId = rarityId ?? throw new ArgumentNullException(nameof(rarityId));
            if (selectionMultiplierMilli < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(selectionMultiplierMilli));
            }
            if (augmentBiasLevels < -12 || augmentBiasLevels > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(augmentBiasLevels));
            }

            SelectionMultiplierMilli = selectionMultiplierMilli;
            AugmentBiasLevels = augmentBiasLevels;
        }

        public StableId RarityId { get; }
        public int SelectionMultiplierMilli { get; }
        public int AugmentBiasLevels { get; }

        public int CompareTo(StrongboxRarityProfile other)
        {
            return ReferenceEquals(other, null) ? 1 : RarityId.CompareTo(other.RarityId);
        }

        public bool Equals(StrongboxRarityProfile other)
        {
            return !ReferenceEquals(other, null)
                && RarityId == other.RarityId
                && SelectionMultiplierMilli == other.SelectionMultiplierMilli
                && AugmentBiasLevels == other.AugmentBiasLevels;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxRarityProfile);
        }

        public override int GetHashCode()
        {
            return Strongbox.DeterministicHash(ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return RarityId
                + ":"
                + SelectionMultiplierMilli.ToString(CultureInfo.InvariantCulture)
                + ":"
                + AugmentBiasLevels.ToString(CultureInfo.InvariantCulture);
        }
    }
}

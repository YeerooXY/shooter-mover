using System;
using System.Globalization;

namespace ShooterMover.Application.Flow.LevelSelection
{
    public sealed class LevelRecommendationV1 : IEquatable<LevelRecommendationV1>
    {
        public LevelRecommendationV1(
            int recommendedPlayerLevel,
            int recommendedEquipmentLevel,
            int recommendedPartySize,
            string difficultyLabel)
        {
            if (recommendedPlayerLevel <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recommendedPlayerLevel));
            }

            if (recommendedEquipmentLevel <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recommendedEquipmentLevel));
            }

            if (recommendedPartySize <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recommendedPartySize));
            }

            if (string.IsNullOrWhiteSpace(difficultyLabel))
            {
                throw new ArgumentException(
                    "A difficulty label is required.",
                    nameof(difficultyLabel));
            }

            RecommendedPlayerLevel = recommendedPlayerLevel;
            RecommendedEquipmentLevel = recommendedEquipmentLevel;
            RecommendedPartySize = recommendedPartySize;
            DifficultyLabel = difficultyLabel.Trim();
        }

        public int RecommendedPlayerLevel { get; }

        public int RecommendedEquipmentLevel { get; }

        public int RecommendedPartySize { get; }

        public string DifficultyLabel { get; }

        public bool Equals(LevelRecommendationV1 other)
        {
            return !ReferenceEquals(other, null)
                && RecommendedPlayerLevel == other.RecommendedPlayerLevel
                && RecommendedEquipmentLevel == other.RecommendedEquipmentLevel
                && RecommendedPartySize == other.RecommendedPartySize
                && string.Equals(
                    DifficultyLabel,
                    other.DifficultyLabel,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LevelRecommendationV1);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + RecommendedPlayerLevel;
                hash = (hash * 31) + RecommendedEquipmentLevel;
                hash = (hash * 31) + RecommendedPartySize;
                hash = (hash * 31)
                    + StringComparer.Ordinal.GetHashCode(DifficultyLabel);
                return hash;
            }
        }

        internal string ToCanonicalString()
        {
            return RecommendedPlayerLevel.ToString(CultureInfo.InvariantCulture)
                + "|"
                + RecommendedEquipmentLevel.ToString(
                    CultureInfo.InvariantCulture)
                + "|"
                + RecommendedPartySize.ToString(CultureInfo.InvariantCulture)
                + "|"
                + CanonicalField(DifficultyLabel);
        }

        internal static string CanonicalField(string value)
        {
            string normalized = value ?? string.Empty;
            return normalized.Length.ToString(CultureInfo.InvariantCulture)
                + ":"
                + normalized;
        }
    }
}

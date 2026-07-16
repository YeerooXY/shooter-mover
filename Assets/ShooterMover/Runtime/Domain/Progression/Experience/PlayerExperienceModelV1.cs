using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;

namespace ShooterMover.Domain.Progression.Experience
{
    public static class PlayerExperienceIdsV1
    {
        public static readonly StableId AuthorityStableId =
            StableId.Parse("authority.player-experience");
    }

    /// <summary>
    /// Configurable deterministic level-cost curve for player levels 1 through 100.
    /// The existing soft-activation curve family supplies the normalized shape; this
    /// type maps it to positive integer XP costs and precomputes all 99 thresholds.
    /// </summary>
    public sealed class PlayerExperienceCurveV1 : IEquatable<PlayerExperienceCurveV1>
    {
        public const int MinimumLevel = 1;
        public const int MaximumLevel = 100;

        private const string SchemaId = "player-experience-curve-v1";
        private readonly long[] cumulativeThresholds;
        private readonly long[] experienceToAdvance;
        private readonly string canonicalString;

        public PlayerExperienceCurveV1(
            long minimumExperienceToAdvance,
            long maximumExperienceToAdvance,
            int nominalFullCostLevel,
            SoftActivationCurveParameters shape)
        {
            if (minimumExperienceToAdvance <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumExperienceToAdvance),
                    "Minimum XP-to-advance must be positive.");
            }

            if (maximumExperienceToAdvance < minimumExperienceToAdvance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumExperienceToAdvance),
                    "Maximum XP-to-advance must be at least the minimum.");
            }

            if (nominalFullCostLevel < MinimumLevel
                || nominalFullCostLevel >= MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nominalFullCostLevel),
                    "Nominal full-cost level must be inside [1, 99].");
            }

            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            MinimumExperienceToAdvance = minimumExperienceToAdvance;
            MaximumExperienceToAdvance = maximumExperienceToAdvance;
            NominalFullCostLevel = nominalFullCostLevel;

            experienceToAdvance = new long[MaximumLevel + 1];
            cumulativeThresholds = new long[MaximumLevel + 1];
            cumulativeThresholds[MinimumLevel] = 0L;

            try
            {
                for (int level = MinimumLevel; level < MaximumLevel; level++)
                {
                    long cost = EvaluateCost(level);
                    experienceToAdvance[level] = cost;
                    cumulativeThresholds[level + 1] =
                        checked(cumulativeThresholds[level] + cost);
                }
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumExperienceToAdvance),
                    "The configured XP curve exceeds the Int64 cumulative range.");
            }

            var builder = new StringBuilder();
            PlayerExperienceFormatV1.AppendToken(builder, "schema", SchemaId);
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "minimum_experience_to_advance",
                MinimumExperienceToAdvance.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "maximum_experience_to_advance",
                MaximumExperienceToAdvance.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "nominal_full_cost_level",
                NominalFullCostLevel.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "early_tail_weight",
                Shape.EarlyTailWeight.ToString("R", CultureInfo.InvariantCulture));
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "early_tail_levels",
                Shape.EarlyTailLevels.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "post_nominal_activation_levels",
                Shape.PostNominalActivationLevels.ToString(CultureInfo.InvariantCulture));
            for (int level = MinimumLevel; level < MaximumLevel; level++)
            {
                PlayerExperienceFormatV1.AppendToken(
                    builder,
                    "level_" + level.ToString("D3", CultureInfo.InvariantCulture) + "_cost",
                    experienceToAdvance[level].ToString(CultureInfo.InvariantCulture));
            }

            canonicalString = builder.ToString();
            Fingerprint = PlayerExperienceFormatV1.ComputeSha256(canonicalString);
        }

        public long MinimumExperienceToAdvance { get; }

        public long MaximumExperienceToAdvance { get; }

        public int NominalFullCostLevel { get; }

        public SoftActivationCurveParameters Shape { get; }

        public string Fingerprint { get; }

        public long MaximumProgressionExperience =>
            cumulativeThresholds[MaximumLevel];

        public long GetExperienceToAdvance(int currentLevel)
        {
            if (currentLevel < MinimumLevel || currentLevel >= MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentLevel),
                    "XP-to-advance is defined only for levels 1 through 99.");
            }

            return experienceToAdvance[currentLevel];
        }

        public long GetCumulativeExperienceForLevel(int level)
        {
            EnsureLevel(level, nameof(level));
            return cumulativeThresholds[level];
        }

        public PlayerExperienceStateV1 Evaluate(long cumulativeExperience)
        {
            if (cumulativeExperience < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cumulativeExperience),
                    "Cumulative XP must not be negative.");
            }

            long progressionExperience = cumulativeExperience;
            long overflowExperience = 0L;
            if (progressionExperience > MaximumProgressionExperience)
            {
                overflowExperience =
                    progressionExperience - MaximumProgressionExperience;
                progressionExperience = MaximumProgressionExperience;
            }

            int level = FindLevel(progressionExperience);
            if (level == MaximumLevel)
            {
                return new PlayerExperienceStateV1(
                    level,
                    cumulativeExperience,
                    progressionExperience,
                    overflowExperience,
                    0L,
                    0L,
                    0L,
                    level);
            }

            long levelStart = cumulativeThresholds[level];
            long required = experienceToAdvance[level];
            long intoLevel = progressionExperience - levelStart;
            long remaining = required - intoLevel;
            return new PlayerExperienceStateV1(
                level,
                cumulativeExperience,
                progressionExperience,
                overflowExperience,
                intoLevel,
                required,
                remaining,
                level);
        }

        public string ToCanonicalString()
        {
            return canonicalString;
        }

        public bool Equals(PlayerExperienceCurveV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalString,
                    other.canonicalString,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerExperienceCurveV1);
        }

        public override int GetHashCode()
        {
            return PlayerExperienceFormatV1.DeterministicHash(canonicalString);
        }

        private long EvaluateCost(int level)
        {
            if (MinimumExperienceToAdvance == MaximumExperienceToAdvance)
            {
                return MinimumExperienceToAdvance;
            }

            double normalized = ProgressionCurveMath.EvaluateSoftActivation(
                level,
                NominalFullCostLevel,
                Shape);
            double range =
                (double)MaximumExperienceToAdvance - MinimumExperienceToAdvance;
            double value = MinimumExperienceToAdvance + (range * normalized);
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value > long.MaxValue)
            {
                throw new OverflowException("The XP-to-advance value is not finite.");
            }

            long rounded = checked((long)Math.Round(
                value,
                MidpointRounding.AwayFromZero));
            if (rounded < MinimumExperienceToAdvance)
            {
                return MinimumExperienceToAdvance;
            }

            return rounded > MaximumExperienceToAdvance
                ? MaximumExperienceToAdvance
                : rounded;
        }

        private int FindLevel(long progressionExperience)
        {
            int low = MinimumLevel;
            int high = MaximumLevel;
            while (low < high)
            {
                int middle = low + ((high - low + 1) / 2);
                if (cumulativeThresholds[middle] <= progressionExperience)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return low;
        }

        private static void EnsureLevel(int level, string parameterName)
        {
            if (level < MinimumLevel || level > MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Player level must be inside [1, 100].");
            }
        }
    }

    /// <summary>
    /// Immutable player-XP projection. CumulativeExperience includes accepted
    /// over-cap XP; ProgressionExperience is clamped to the level-100 threshold.
    /// </summary>
    public sealed class PlayerExperienceStateV1 : IEquatable<PlayerExperienceStateV1>
    {
        internal PlayerExperienceStateV1(
            int level,
            long cumulativeExperience,
            long progressionExperience,
            long overflowExperience,
            long experienceIntoCurrentLevel,
            long experienceRequiredForNextLevel,
            long experienceToNextLevel,
            int totalSkillPointsAwarded)
        {
            Level = level;
            CumulativeExperience = cumulativeExperience;
            ProgressionExperience = progressionExperience;
            OverflowExperience = overflowExperience;
            ExperienceIntoCurrentLevel = experienceIntoCurrentLevel;
            ExperienceRequiredForNextLevel = experienceRequiredForNextLevel;
            ExperienceToNextLevel = experienceToNextLevel;
            TotalSkillPointsAwarded = totalSkillPointsAwarded;
        }

        public int Level { get; }

        public long CumulativeExperience { get; }

        public long ProgressionExperience { get; }

        public long OverflowExperience { get; }

        public long ExperienceIntoCurrentLevel { get; }

        public long ExperienceRequiredForNextLevel { get; }

        public long ExperienceToNextLevel { get; }

        public int TotalSkillPointsAwarded { get; }

        public bool IsAtLevelCap =>
            Level == PlayerExperienceCurveV1.MaximumLevel;

        public ProgressionContext ProjectContext(ProgressionContext template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            return ProgressionContext.Create(
                Level,
                template.RegionLevel,
                template.DifficultyId,
                template.DifficultyValue,
                template.ProgressionTags);
        }

        public bool Equals(PlayerExperienceStateV1 other)
        {
            return !ReferenceEquals(other, null)
                && Level == other.Level
                && CumulativeExperience == other.CumulativeExperience
                && ProgressionExperience == other.ProgressionExperience
                && OverflowExperience == other.OverflowExperience
                && ExperienceIntoCurrentLevel == other.ExperienceIntoCurrentLevel
                && ExperienceRequiredForNextLevel == other.ExperienceRequiredForNextLevel
                && ExperienceToNextLevel == other.ExperienceToNextLevel
                && TotalSkillPointsAwarded == other.TotalSkillPointsAwarded;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerExperienceStateV1);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Level;
                hash = (hash * 397) ^ CumulativeExperience.GetHashCode();
                hash = (hash * 397) ^ ProgressionExperience.GetHashCode();
                hash = (hash * 397) ^ OverflowExperience.GetHashCode();
                hash = (hash * 397) ^ ExperienceIntoCurrentLevel.GetHashCode();
                hash = (hash * 397) ^ ExperienceRequiredForNextLevel.GetHashCode();
                hash = (hash * 397) ^ ExperienceToNextLevel.GetHashCode();
                hash = (hash * 397) ^ TotalSkillPointsAwarded;
                return hash;
            }
        }
    }

    public static class PlayerExperienceFormatV1
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static void AppendToken(
            StringBuilder builder,
            string key,
            string value)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            string canonicalValue = value ?? string.Empty;
            builder.Append(key.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(key)
                .Append('=')
                .Append(canonicalValue.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(canonicalValue)
                .Append('\n');
        }

        public static string ComputeSha256(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            byte[] input = Encoding.UTF8.GetBytes(canonicalText);
            byte[] digest;
            using (SHA256 algorithm = SHA256.Create())
            {
                digest = algorithm.ComputeHash(input);
            }

            var builder = new StringBuilder("sha256:", 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(
                    digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static int DeterministicHash(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }
    }
}

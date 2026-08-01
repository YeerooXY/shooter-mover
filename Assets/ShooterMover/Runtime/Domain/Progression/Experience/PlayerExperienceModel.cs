using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;

namespace ShooterMover.Domain.Progression.Experience
{
    public static class PlayerExperienceIds
    {
        public static readonly StableId AuthorityStableId =
            StableId.Parse("authority.player-experience");
    }

    /// <summary>
    /// Configurable deterministic level-cost curve for player levels 1 through 100.
    /// The existing soft-activation curve family supplies the normalized shape; this
    /// type maps it to positive integer XP costs and precomputes all 99 thresholds.
    /// </summary>
    public sealed class PlayerExperienceCurve : IEquatable<PlayerExperienceCurve>
    {
        public const int MinimumLevel = 1;
        public const int MaximumLevel = 100;

        private const string SchemaId = "player-experience-curve-v1";
        private const string LinearSchemaId = "player-experience-curve-linear-v2";
        private const double ProductionBaseExperience = 100d;
        private const double ProductionGrowthPerLevel = 47.4335d;
        private readonly long[] cumulativeThresholds;
        private readonly long[] experienceToAdvance;
        private readonly string canonicalString;
        private readonly bool usesLinearGrowth;
        private readonly double linearGrowthPerLevel;

        public PlayerExperienceCurve(
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
            usesLinearGrowth = false;
            linearGrowthPerLevel = 0d;
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

            canonicalString = BuildCanonicalString();
            Fingerprint = PlayerExperienceFormat.ComputeSha256(canonicalString);
        }

        private PlayerExperienceCurve(
            long baseExperienceToAdvance,
            double growthPerLevel,
            bool linearGrowth)
            : this(
                baseExperienceToAdvance,
                checked((long)Math.Round(
                    baseExperienceToAdvance
                        + (growthPerLevel * (MaximumLevel - 2)),
                    MidpointRounding.AwayFromZero)),
                MaximumLevel - 1,
                new SoftActivationCurveParameters(0.1, 10L, 10L))
        {
            if (!linearGrowth)
            {
                throw new ArgumentException(
                    "The linear XP constructor requires the linear-growth marker.",
                    nameof(linearGrowth));
            }
            if (double.IsNaN(growthPerLevel)
                || double.IsInfinity(growthPerLevel)
                || growthPerLevel <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(growthPerLevel));
            }

            usesLinearGrowth = true;
            linearGrowthPerLevel = growthPerLevel;
            cumulativeThresholds[MinimumLevel] = 0L;
            for (int level = MinimumLevel; level < MaximumLevel; level++)
            {
                long cost = EvaluateCost(level);
                experienceToAdvance[level] = cost;
                cumulativeThresholds[level + 1] = checked(
                    cumulativeThresholds[level] + cost);
            }

            canonicalString = BuildCanonicalString();
            Fingerprint = PlayerExperienceFormat.ComputeSha256(canonicalString);
        }

        /// <summary>
        /// The authoritative production curve. Level 1 to 2 costs 100 XP and each
        /// subsequent level cost grows by 47.4335 XP before deterministic rounding.
        /// The cumulative level-1-to-100 cost is exactly 240,000 XP.
        /// </summary>
        public static PlayerExperienceCurve CreateProduction()
        {
            return new PlayerExperienceCurve(
                checked((long)ProductionBaseExperience),
                ProductionGrowthPerLevel,
                true);
        }

        public static PlayerExperienceCurve CreateLegacyPlaceholder()
        {
            return new PlayerExperienceCurve(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
        }

        public static bool IsLegacyPlaceholderFingerprint(string fingerprint)
        {
            return string.Equals(
                fingerprint,
                CreateLegacyPlaceholder().Fingerprint,
                StringComparison.Ordinal);
        }

        private string BuildCanonicalString()
        {
            var builder = new StringBuilder();
            if (usesLinearGrowth)
            {
                PlayerExperienceFormat.AppendToken(builder, "schema", LinearSchemaId);
                PlayerExperienceFormat.AppendToken(
                    builder,
                    "base_experience_to_advance",
                    MinimumExperienceToAdvance.ToString(CultureInfo.InvariantCulture));
                PlayerExperienceFormat.AppendToken(
                    builder,
                    "growth_per_level",
                    linearGrowthPerLevel.ToString("R", CultureInfo.InvariantCulture));
            }
            else
            {
            PlayerExperienceFormat.AppendToken(builder, "schema", SchemaId);
            PlayerExperienceFormat.AppendToken(
                builder,
                "minimum_experience_to_advance",
                MinimumExperienceToAdvance.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormat.AppendToken(
                builder,
                "maximum_experience_to_advance",
                MaximumExperienceToAdvance.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormat.AppendToken(
                builder,
                "nominal_full_cost_level",
                NominalFullCostLevel.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormat.AppendToken(
                builder,
                "early_tail_weight",
                Shape.EarlyTailWeight.ToString("R", CultureInfo.InvariantCulture));
            PlayerExperienceFormat.AppendToken(
                builder,
                "early_tail_levels",
                Shape.EarlyTailLevels.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormat.AppendToken(
                builder,
                "post_nominal_activation_levels",
                Shape.PostNominalActivationLevels.ToString(CultureInfo.InvariantCulture));
            }
            for (int level = MinimumLevel; level < MaximumLevel; level++)
            {
                PlayerExperienceFormat.AppendToken(
                    builder,
                    "level_" + level.ToString("D3", CultureInfo.InvariantCulture) + "_cost",
                    experienceToAdvance[level].ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
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

        public PlayerExperienceState Evaluate(long cumulativeExperience)
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
                return new PlayerExperienceState(
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
            return new PlayerExperienceState(
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

        public bool Equals(PlayerExperienceCurve other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalString,
                    other.canonicalString,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerExperienceCurve);
        }

        public override int GetHashCode()
        {
            return PlayerExperienceFormat.DeterministicHash(canonicalString);
        }

        private long EvaluateCost(int level)
        {
            if (usesLinearGrowth)
            {
                double linearValue = MinimumExperienceToAdvance
                    + (linearGrowthPerLevel * (level - MinimumLevel));
                if (double.IsNaN(linearValue)
                    || double.IsInfinity(linearValue)
                    || linearValue > long.MaxValue)
                {
                    throw new OverflowException(
                        "The linear XP-to-advance value is not finite.");
                }
                return checked((long)Math.Round(
                    linearValue,
                    MidpointRounding.AwayFromZero));
            }

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
    public sealed class PlayerExperienceState : IEquatable<PlayerExperienceState>
    {
        internal PlayerExperienceState(
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
            Level == PlayerExperienceCurve.MaximumLevel;

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

        public bool Equals(PlayerExperienceState other)
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
            return Equals(obj as PlayerExperienceState);
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

    public static class PlayerExperienceFormat
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

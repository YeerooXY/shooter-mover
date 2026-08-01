using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Experience;

namespace ShooterMover.Application.Progression.Experience
{
    public static class MissionExperienceProfileIds
    {
        public static readonly StableId Light =
            StableId.Parse("xp.enemy-light");
        public static readonly StableId Standard =
            StableId.Parse("xp.enemy-standard");
        public static readonly StableId Turret =
            StableId.Parse("xp.enemy-turret");
    }

    /// <summary>
    /// Authoritative mission XP arithmetic. Enemy tier, not player level, scales an
    /// authored XP profile. Decimal arithmetic keeps authored multipliers deterministic.
    /// </summary>
    public sealed class MissionExperienceRewardPolicy
    {
        private static readonly decimal[] TierMultipliers =
        {
            0m,
            1m,
            1.5m,
            2.25m,
            3.5m,
        };

        public MissionExperienceRewardPolicy(decimal modeMultiplier = 1m)
        {
            if (modeMultiplier <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(modeMultiplier));
            }
            ModeMultiplier = modeMultiplier;
        }

        public decimal ModeMultiplier { get; }

        public long CalculateEnemyExperience(
            StableId experienceProfileStableId,
            int tier)
        {
            long baseExperience = ResolveBaseExperience(
                experienceProfileStableId);
            if (tier < 1 || tier > 4)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tier),
                    "Enemy XP tier must be between 1 and 4.");
            }

            decimal value = baseExperience
                * TierMultipliers[tier]
                * ModeMultiplier;
            return checked((long)decimal.Round(
                value,
                0,
                MidpointRounding.AwayFromZero));
        }

        public static long CalculateCompletionExperience(int completedRooms)
        {
            if (completedRooms < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(completedRooms));
            }
            return checked(25L + (15L * completedRooms));
        }

        public static long CalculateFailedMissionExperience(
            long earnedEnemyExperience)
        {
            if (earnedEnemyExperience < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(earnedEnemyExperience));
            }
            return checked((long)decimal.Round(
                earnedEnemyExperience * 0.25m,
                0,
                MidpointRounding.AwayFromZero));
        }

        public static long ResolveBaseExperience(
            StableId experienceProfileStableId)
        {
            if (experienceProfileStableId == MissionExperienceProfileIds.Light)
            {
                return 7L;
            }
            if (experienceProfileStableId == MissionExperienceProfileIds.Standard)
            {
                return 10L;
            }
            if (experienceProfileStableId == MissionExperienceProfileIds.Turret)
            {
                return 12L;
            }
            throw new KeyNotFoundException(
                "Unknown enemy XP profile: " + experienceProfileStableId);
        }
    }

    public sealed class MissionExperienceResult
    {
        public MissionExperienceResult(
            StableId participantStableId,
            int enemiesKilled,
            long enemyExperience,
            int completedRooms,
            long completionExperience,
            int previousLevel,
            int newLevel,
            int skillPointsEarned,
            PlayerExperienceGrantStatus? grantStatus)
        {
            ParticipantStableId = participantStableId
                ?? throw new ArgumentNullException(nameof(participantStableId));
            if (enemiesKilled < 0 || enemyExperience < 0L
                || completedRooms < 0 || completionExperience < 0L
                || previousLevel < 1 || newLevel < previousLevel
                || skillPointsEarned < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemiesKilled),
                    "Mission XP result values must be non-negative and ordered.");
            }
            ParticipantStableId = participantStableId;
            EnemiesKilled = enemiesKilled;
            EnemyExperience = enemyExperience;
            CompletedRooms = completedRooms;
            CompletionExperience = completionExperience;
            TotalExperience = checked(enemyExperience + completionExperience);
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
            SkillPointsEarned = skillPointsEarned;
            GrantStatus = grantStatus;
        }

        public StableId ParticipantStableId { get; }
        public int EnemiesKilled { get; }
        public long EnemyExperience { get; }
        public int CompletedRooms { get; }
        public long CompletionExperience { get; }
        public long TotalExperience { get; }
        public int PreviousLevel { get; }
        public int NewLevel { get; }
        public int SkillPointsEarned { get; }
        public PlayerExperienceGrantStatus? GrantStatus { get; }
        public bool PersistentAwardApplied =>
            GrantStatus == PlayerExperienceGrantStatus.Applied
            || GrantStatus == PlayerExperienceGrantStatus.DuplicateNoChange;
    }

    public sealed class ExperienceEnemyCount
    {
        public ExperienceEnemyCount(
            StableId profileStableId,
            int tier,
            int count)
        {
            ProfileStableId = profileStableId
                ?? throw new ArgumentNullException(nameof(profileStableId));
            if (tier < 1 || tier > 4) throw new ArgumentOutOfRangeException(nameof(tier));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            Tier = tier;
            Count = count;
        }

        public StableId ProfileStableId { get; }
        public int Tier { get; }
        public int Count { get; }
    }

    public sealed class ExperienceLevelProjection
    {
        public ExperienceLevelProjection(
            int level,
            long costToNextLevel,
            long cumulativeExperience,
            long missionsToReach,
            double hoursToReach)
        {
            Level = level;
            CostToNextLevel = costToNextLevel;
            CumulativeExperience = cumulativeExperience;
            MissionsToReach = missionsToReach;
            HoursToReach = hoursToReach;
        }

        public int Level { get; }
        public long CostToNextLevel { get; }
        public long CumulativeExperience { get; }
        public long MissionsToReach { get; }
        public double HoursToReach { get; }
    }

    public sealed class ExperienceBalanceReport
    {
        internal ExperienceBalanceReport(
            long experiencePerMission,
            double experiencePerHour,
            int simulatedMissions,
            int simulatedFinalLevel,
            double totalHoursToLevel100,
            double casualHoursToLevel100,
            double efficientHoursToLevel100,
            double identicalRewardFiveMinuteHoursToLevel100,
            IEnumerable<ExperienceLevelProjection> levels)
        {
            ExperiencePerMission = experiencePerMission;
            ExperiencePerHour = experiencePerHour;
            SimulatedMissions = simulatedMissions;
            SimulatedFinalLevel = simulatedFinalLevel;
            TotalHoursToLevel100 = totalHoursToLevel100;
            CasualHoursToLevel100 = casualHoursToLevel100;
            EfficientHoursToLevel100 = efficientHoursToLevel100;
            IdenticalRewardFiveMinuteHoursToLevel100 =
                identicalRewardFiveMinuteHoursToLevel100;
            Levels = new ReadOnlyCollection<ExperienceLevelProjection>(
                new List<ExperienceLevelProjection>(levels));
        }

        public long ExperiencePerMission { get; }
        public double ExperiencePerHour { get; }
        public int SimulatedMissions { get; }
        public int SimulatedFinalLevel { get; }
        public double TotalHoursToLevel100 { get; }
        public double CasualHoursToLevel100 { get; }
        public double EfficientHoursToLevel100 { get; }
        public double IdenticalRewardFiveMinuteHoursToLevel100 { get; }
        public IReadOnlyList<ExperienceLevelProjection> Levels { get; }
    }

    public static class ExperienceBalanceSimulator
    {
        public static ExperienceBalanceReport Simulate(
            int startingLevel,
            double missionDurationMinutes,
            int completedRooms,
            IEnumerable<ExperienceEnemyCount> enemyCounts,
            decimal modeMultiplier,
            int simulatedMissions)
        {
            PlayerExperienceCurve curve = PlayerExperienceCurve.CreateProduction();
            if (startingLevel < PlayerExperienceCurve.MinimumLevel
                || startingLevel > PlayerExperienceCurve.MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(startingLevel));
            }
            if (double.IsNaN(missionDurationMinutes)
                || double.IsInfinity(missionDurationMinutes)
                || missionDurationMinutes <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(missionDurationMinutes));
            }
            if (simulatedMissions < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(simulatedMissions));
            }

            var policy = new MissionExperienceRewardPolicy(modeMultiplier);
            long enemyExperience = 0L;
            foreach (ExperienceEnemyCount input in enemyCounts
                ?? throw new ArgumentNullException(nameof(enemyCounts)))
            {
                if (input == null) throw new ArgumentException("Enemy counts cannot contain null.", nameof(enemyCounts));
                enemyExperience = checked(enemyExperience
                    + (policy.CalculateEnemyExperience(
                        input.ProfileStableId,
                        input.Tier) * input.Count));
            }
            long experiencePerMission = checked(
                enemyExperience
                + MissionExperienceRewardPolicy.CalculateCompletionExperience(
                    completedRooms));
            double experiencePerHour = experiencePerMission
                * (60d / missionDurationMinutes);
            long startingExperience = curve.GetCumulativeExperienceForLevel(startingLevel);
            long remaining = curve.MaximumProgressionExperience - startingExperience;
            int finalLevel = curve.Evaluate(checked(startingExperience
                + (experiencePerMission * simulatedMissions))).Level;
            var levels = new List<ExperienceLevelProjection>();
            for (int level = startingLevel; level <= PlayerExperienceCurve.MaximumLevel; level++)
            {
                long needed = curve.GetCumulativeExperienceForLevel(level)
                    - startingExperience;
                long missions = needed == 0L
                    ? 0L
                    : checked((needed + experiencePerMission - 1L) / experiencePerMission);
                levels.Add(new ExperienceLevelProjection(
                    level,
                    level == PlayerExperienceCurve.MaximumLevel
                        ? 0L
                        : curve.GetExperienceToAdvance(level),
                    curve.GetCumulativeExperienceForLevel(level),
                    missions,
                    missions * missionDurationMinutes / 60d));
            }

            return new ExperienceBalanceReport(
                experiencePerMission,
                experiencePerHour,
                simulatedMissions,
                finalLevel,
                experiencePerHour <= 0d ? double.PositiveInfinity : remaining / experiencePerHour,
                remaining / 600d,
                remaining / 960d,
                remaining / 1200d,
                levels);
        }
    }
}

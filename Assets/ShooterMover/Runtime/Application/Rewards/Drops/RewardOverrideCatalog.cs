using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>
    /// Production-authored contextual overrides. Adding another entry changes live,
    /// multiplayer and simulator resolution through the same catalog without editing
    /// enemy, prop or reward-generation algorithms.
    /// </summary>
    public static class RewardOverrideCatalog
    {
        public static readonly StableId SurvivalModeId =
            StableId.Parse("game-mode.survival");
        public static readonly StableId BossRushMissionId =
            StableId.Parse("mission-layout.boss-rush");
        public static readonly StableId HardDifficultyId =
            StableId.Parse("difficulty.hard");
        public static readonly StableId NightmareDifficultyId =
            StableId.Parse("difficulty.nightmare");
        public static readonly StableId DoubleRewardsEventId =
            StableId.Parse("event.double-rewards");
        public static readonly StableId BoxFrenzyEventId =
            StableId.Parse("event.box-frenzy");
        public static readonly StableId LockedVaultPlacementId =
            StableId.Parse("placement.locked-vault");

        public static readonly StableId SurvivalBossOverrideId =
            StableId.Parse("game-mode.survival-boss-override");

        private static readonly RewardProfileOverride SurvivalBossOverride =
            RewardProfileOverride.Replace(
                SurvivalBossOverrideId,
                RewardSourceCatalog.Get(
                    RewardSourceCatalog.ExtraBossEnemyId));
        private static readonly RewardProfileOverride BossRushOverride =
            RewardProfileOverride.Modify(
                StableId.Parse("mission-override.boss-rush"),
                1000,
                1100,
                StrongboxTierSelectionCatalog
                    .TreasureSourceProfileId);
        private static readonly RewardProfileOverride HardOverride =
            RewardProfileOverride.Modify(
                StableId.Parse("difficulty-override.hard"),
                1150,
                1100,
                null);
        private static readonly RewardProfileOverride NightmareOverride =
            RewardProfileOverride.Modify(
                StableId.Parse("difficulty-override.nightmare"),
                1400,
                1250,
                StrongboxTierSelectionCatalog
                    .ImprovedSourceProfileId);
        private static readonly RewardProfileOverride DoubleRewardsOverride =
            RewardProfileOverride.Modify(
                StableId.Parse("event-override.double-rewards"),
                1000,
                2000,
                null);
        private static readonly RewardProfileOverride BoxFrenzyOverride =
            RewardProfileOverride.Modify(
                StableId.Parse("event-override.box-frenzy"),
                2000,
                1000,
                StrongboxTierSelectionCatalog
                    .TreasureSourceProfileId);
        private static readonly RewardProfileOverride LockedVaultOverride =
            RewardProfileOverride.Replace(
                StableId.Parse("placement-override.locked-vault"),
                RewardSourceCatalog.Get(
                    RewardSourceCatalog.LargeTreasureLootId));

        public static RewardContextOverrideResolution Resolve(
            StableId sourceProfileReferenceId,
            StableId gameModeStableId,
            StableId missionStableId,
            StableId difficultyStableId,
            IEnumerable<StableId> eventModifierIds,
            StableId placementStableId)
        {
            RewardProfileOverride mode = ResolveMode(
                sourceProfileReferenceId,
                gameModeStableId);
            RewardProfileOverride mission = ResolveMission(
                sourceProfileReferenceId,
                missionStableId);
            RewardProfileOverride difficulty = ResolveDifficulty(
                difficultyStableId);
            var events = new List<RewardProfileOverride>();
            if (eventModifierIds != null)
            {
                foreach (StableId eventId in eventModifierIds)
                {
                    RewardProfileOverride value = ResolveEvent(eventId);
                    if (value != null)
                    {
                        events.Add(value);
                    }
                }
            }
            RewardProfileOverride placement =
                placementStableId == LockedVaultPlacementId
                    ? LockedVaultOverride
                    : null;
            return new RewardContextOverrideResolution(
                mode,
                mission,
                difficulty,
                events,
                placement);
        }

        private static RewardProfileOverride ResolveMode(
            StableId sourceProfileReferenceId,
            StableId gameModeStableId)
        {
            if (gameModeStableId != SurvivalModeId)
            {
                return null;
            }
            return sourceProfileReferenceId
                    == RewardSourceCatalog.BossEnemyId
                ? SurvivalBossOverride
                : null;
        }

        private static RewardProfileOverride ResolveMission(
            StableId sourceProfileReferenceId,
            StableId missionStableId)
        {
            if (missionStableId != BossRushMissionId)
            {
                return null;
            }
            return sourceProfileReferenceId
                    == RewardSourceCatalog.BossEnemyId
                || sourceProfileReferenceId
                    == RewardSourceCatalog.ExtraBossEnemyId
                ? BossRushOverride
                : null;
        }

        private static RewardProfileOverride ResolveDifficulty(
            StableId difficultyStableId)
        {
            if (difficultyStableId == HardDifficultyId)
            {
                return HardOverride;
            }
            return difficultyStableId == NightmareDifficultyId
                ? NightmareOverride
                : null;
        }

        private static RewardProfileOverride ResolveEvent(StableId eventId)
        {
            if (eventId == DoubleRewardsEventId)
            {
                return DoubleRewardsOverride;
            }
            return eventId == BoxFrenzyEventId
                ? BoxFrenzyOverride
                : null;
        }
    }
}

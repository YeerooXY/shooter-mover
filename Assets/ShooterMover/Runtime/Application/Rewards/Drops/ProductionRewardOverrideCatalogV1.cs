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
    public static class ProductionRewardOverrideCatalogV1
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

        private static readonly RewardProfileOverrideV1 SurvivalBossOverride =
            RewardProfileOverrideV1.Replace(
                SurvivalBossOverrideId,
                ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.ExtraBossEnemyId));
        private static readonly RewardProfileOverrideV1 BossRushOverride =
            RewardProfileOverrideV1.Modify(
                StableId.Parse("mission-override.boss-rush"),
                1000,
                1100,
                ProductionStrongboxTierSelectionCatalogV1
                    .TreasureSourceProfileId);
        private static readonly RewardProfileOverrideV1 HardOverride =
            RewardProfileOverrideV1.Modify(
                StableId.Parse("difficulty-override.hard"),
                1150,
                1100,
                null);
        private static readonly RewardProfileOverrideV1 NightmareOverride =
            RewardProfileOverrideV1.Modify(
                StableId.Parse("difficulty-override.nightmare"),
                1400,
                1250,
                ProductionStrongboxTierSelectionCatalogV1
                    .ImprovedSourceProfileId);
        private static readonly RewardProfileOverrideV1 DoubleRewardsOverride =
            RewardProfileOverrideV1.Modify(
                StableId.Parse("event-override.double-rewards"),
                1000,
                2000,
                null);
        private static readonly RewardProfileOverrideV1 BoxFrenzyOverride =
            RewardProfileOverrideV1.Modify(
                StableId.Parse("event-override.box-frenzy"),
                2000,
                1000,
                ProductionStrongboxTierSelectionCatalogV1
                    .TreasureSourceProfileId);
        private static readonly RewardProfileOverrideV1 LockedVaultOverride =
            RewardProfileOverrideV1.Replace(
                StableId.Parse("placement-override.locked-vault"),
                ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.LargeTreasureLootId));

        public static RewardContextOverrideResolutionV1 Resolve(
            StableId sourceProfileReferenceId,
            StableId gameModeStableId,
            StableId missionStableId,
            StableId difficultyStableId,
            IEnumerable<StableId> eventModifierIds,
            StableId placementStableId)
        {
            RewardProfileOverrideV1 mode = ResolveMode(
                sourceProfileReferenceId,
                gameModeStableId);
            RewardProfileOverrideV1 mission = ResolveMission(
                sourceProfileReferenceId,
                missionStableId);
            RewardProfileOverrideV1 difficulty = ResolveDifficulty(
                difficultyStableId);
            var events = new List<RewardProfileOverrideV1>();
            if (eventModifierIds != null)
            {
                foreach (StableId eventId in eventModifierIds)
                {
                    RewardProfileOverrideV1 value = ResolveEvent(eventId);
                    if (value != null)
                    {
                        events.Add(value);
                    }
                }
            }
            RewardProfileOverrideV1 placement =
                placementStableId == LockedVaultPlacementId
                    ? LockedVaultOverride
                    : null;
            return new RewardContextOverrideResolutionV1(
                mode,
                mission,
                difficulty,
                events,
                placement);
        }

        private static RewardProfileOverrideV1 ResolveMode(
            StableId sourceProfileReferenceId,
            StableId gameModeStableId)
        {
            if (gameModeStableId != SurvivalModeId)
            {
                return null;
            }
            return sourceProfileReferenceId
                    == ProductionRewardSourceCatalogV1.BossEnemyId
                ? SurvivalBossOverride
                : null;
        }

        private static RewardProfileOverrideV1 ResolveMission(
            StableId sourceProfileReferenceId,
            StableId missionStableId)
        {
            if (missionStableId != BossRushMissionId)
            {
                return null;
            }
            return sourceProfileReferenceId
                    == ProductionRewardSourceCatalogV1.BossEnemyId
                || sourceProfileReferenceId
                    == ProductionRewardSourceCatalogV1.ExtraBossEnemyId
                ? BossRushOverride
                : null;
        }

        private static RewardProfileOverrideV1 ResolveDifficulty(
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

        private static RewardProfileOverrideV1 ResolveEvent(StableId eventId)
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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.Application.Missions.Run
{
    public sealed partial class LevelRunCoordinatorV1
    {
        public LevelRunEnemyDestructionResultV1 RecordEnemyDestroyed(
            StableId roomStableId,
            StableId enemyDefinitionStableId,
            int enemyLevel,
            EnemyDestroyedNotification destruction)
        {
            if (roomStableId == null
                || enemyDefinitionStableId == null
                || destruction == null
                || destruction.EventId == null
                || destruction.SourceId == null
                || destruction.TargetId == null)
            {
                return new LevelRunEnemyDestructionResultV1(
                    LevelRunEnemyDestructionStatusV1.InvalidRequest,
                    "level-run-destruction-invalid",
                    null,
                    0L,
                    false);
            }

            HashSet<StableId> registered;
            if (!enemiesByRoom.TryGetValue(roomStableId, out registered))
            {
                return new LevelRunEnemyDestructionResultV1(
                    LevelRunEnemyDestructionStatusV1.UnknownRoom,
                    "level-run-room-not-registered",
                    null,
                    0L,
                    false);
            }

            StableId registeredRoom;
            if (!registered.Contains(destruction.TargetId)
                || !roomByEnemy.TryGetValue(
                    destruction.TargetId,
                    out registeredRoom)
                || registeredRoom != roomStableId)
            {
                return new LevelRunEnemyDestructionResultV1(
                    LevelRunEnemyDestructionStatusV1.UnregisteredEnemy,
                    "level-run-enemy-not-registered",
                    null,
                    0L,
                    false);
            }

            if (processedDestructionEvents.Contains(destruction.EventId)
                || destroyedEnemies.Contains(destruction.TargetId))
            {
                return new LevelRunEnemyDestructionResultV1(
                    LevelRunEnemyDestructionStatusV1.DuplicateNoChange,
                    "level-run-destruction-duplicate",
                    null,
                    0L,
                    false);
            }

            processedDestructionEvents.Add(destruction.EventId);
            destroyedEnemies.Add(destruction.TargetId);
            bool roomBecameClear = TryCompleteRoomIfClear(roomStableId);

            StableId playerStableId;
            if (!playerBySource.TryGetValue(
                destruction.SourceId,
                out playerStableId))
            {
                return new LevelRunEnemyDestructionResultV1(
                    LevelRunEnemyDestructionStatusV1.Unattributed,
                    "level-run-destruction-source-unattributed",
                    null,
                    0L,
                    roomBecameClear);
            }

            MutableContribution contribution = contributions[playerStableId];
            contribution.KillCount = checked(contribution.KillCount + 1);
            EnemyExperienceRewardFactV1 experience =
                experienceRewards.ProcessDestruction(
                    runStableId,
                    enemyDefinitionStableId,
                    enemyLevel,
                    destruction);
            long earned = experience != null && experience.Changed
                ? experience.ExperienceAmount
                : 0L;
            contribution.ExperienceEarned =
                checked(contribution.ExperienceEarned + earned);

            return new LevelRunEnemyDestructionResultV1(
                LevelRunEnemyDestructionStatusV1.Applied,
                experience == null ? "level-run-xp-result-null" : experience.RejectionCode,
                playerStableId,
                earned,
                roomBecameClear);
        }

        public IReadOnlyList<LevelRunPlayerContributionV1> ExportContributions()
        {
            var ordered = new List<LevelRunPlayerContributionV1>();
            foreach (KeyValuePair<StableId, MutableContribution> pair
                in contributions)
            {
                ordered.Add(new LevelRunPlayerContributionV1(
                    pair.Value.PlayerStableId,
                    pair.Value.KillCount,
                    pair.Value.ExperienceEarned));
            }

            ordered.Sort();
            return new ReadOnlyCollection<LevelRunPlayerContributionV1>(ordered);
        }

        public LevelRunExtractionResultV1 RequestExtraction()
        {
            if (terminalExtraction != null)
            {
                return terminalExtraction;
            }

            if (roomLayout.CurrentRoomState.RoomStableId
                != extractionRoomStableId)
            {
                return new LevelRunExtractionResultV1(
                    LevelRunExtractionStatusV1.WrongRoom,
                    "level-run-extraction-wrong-room",
                    null,
                    null);
            }

            if (!roomLayout.CurrentRoomState.IsCompleted)
            {
                return new LevelRunExtractionResultV1(
                    LevelRunExtractionStatusV1.RoomNotClear,
                    "level-run-extraction-room-not-clear",
                    null,
                    null);
            }

            if (completionRequested)
            {
                return terminalExtraction ?? new LevelRunExtractionResultV1(
                    LevelRunExtractionStatusV1.AuthorityRejected,
                    "level-run-end-already-requested",
                    null,
                    null);
            }

            completionRequested = true;
            EndMissionRunCommandV1 command = EndMissionRunCommandV1.Create(
                StableId.Create(
                    "run-operation",
                    runStableId.Value + "-complete"),
                runStableId,
                routePayload,
                MissionRunCompletionStateV1.Completed,
                missionResults.Sequence,
                checkpoint.HoldingsSequence,
                checkpoint.HoldingsFingerprint,
                checkpoint.StrongboxOpeningSequence,
                checkpoint.StrongboxOpeningFingerprint);
            MissionRunAuthorityResultV1 result = missionResults.EndRun(command);
            if (result == null
                || !result.Succeeded
                || result.ResultPayload == null)
            {
                terminalExtraction = new LevelRunExtractionResultV1(
                    LevelRunExtractionStatusV1.AuthorityRejected,
                    result == null
                        ? "level-run-end-result-null"
                        : result.RejectionCode,
                    null,
                    null);
                return terminalExtraction;
            }

            LevelRunSummaryV1 summary = LevelRunSummaryV1.Create(
                runStableId,
                routePayload,
                selectedModeStableId,
                selectedLevelStableId,
                MissionRunCompletionStateV1.Completed,
                ExportContributions());
            terminalExtraction = new LevelRunExtractionResultV1(
                LevelRunExtractionStatusV1.Completed,
                string.Empty,
                result.ResultPayload,
                summary);
            return terminalExtraction;
        }

        private bool TryCompleteRoomIfClear(StableId roomStableId)
        {
            HashSet<StableId> registered = enemiesByRoom[roomStableId];
            foreach (StableId enemyId in registered)
            {
                if (!destroyedEnemies.Contains(enemyId))
                {
                    return false;
                }
            }

            if (roomLayout.CurrentRoomState.RoomStableId != roomStableId)
            {
                return false;
            }

            bool wasCompleted = roomLayout.CurrentRoomState.IsCompleted;
            roomLayout.CompleteCurrentRoom();
            return !wasCompleted && roomLayout.CurrentRoomState.IsCompleted;
        }
    }
}

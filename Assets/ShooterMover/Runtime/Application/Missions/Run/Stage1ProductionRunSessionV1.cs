using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.Application.Missions.Run
{
    public enum Stage1RunRegistrationStatusV1
    {
        Registered = 1,
        ExactDuplicateNoChange = 2,
        InvalidRequest = 3,
        UnsupportedRoom = 4,
        ConflictingDuplicate = 5,
        CoordinatorRejected = 6,
    }

    public enum Stage1RunCompletionStatusV1
    {
        CompletedAndCaptured = 1,
        ExactDuplicateNoChange = 2,
        NotReady = 3,
        AuthorityRejected = 4,
    }

    public sealed class Stage1RunEnemyRegistrationV1
    {
        public Stage1RunEnemyRegistrationV1(
            StableId actorStableId,
            StableId definitionStableId,
            int level)
        {
            ActorStableId = actorStableId
                ?? throw new ArgumentNullException(nameof(actorStableId));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            Level = level;
        }

        public StableId ActorStableId { get; }
        public StableId DefinitionStableId { get; }
        public int Level { get; }
    }

    public sealed class Stage1RunRoomRegistrationV1
    {
        private readonly ReadOnlyCollection<Stage1RunEnemyRegistrationV1> enemies;

        public Stage1RunRoomRegistrationV1(
            StableId roomStableId,
            IEnumerable<Stage1RunEnemyRegistrationV1> enemies)
        {
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            var copy = new List<Stage1RunEnemyRegistrationV1>(
                enemies ?? throw new ArgumentNullException(nameof(enemies)));
            var seen = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                Stage1RunEnemyRegistrationV1 enemy = copy[index];
                if (enemy == null || !seen.Add(enemy.ActorStableId))
                {
                    throw new ArgumentException(
                        "Room enemies must be non-null and uniquely keyed.",
                        nameof(enemies));
                }
            }

            this.enemies =
                new ReadOnlyCollection<Stage1RunEnemyRegistrationV1>(copy);
        }

        public StableId RoomStableId { get; }
        public IReadOnlyList<Stage1RunEnemyRegistrationV1> Enemies
        {
            get { return enemies; }
        }
    }

    public sealed class Stage1RunCompletionResultV1
    {
        internal Stage1RunCompletionResultV1(
            Stage1RunCompletionStatusV1 status,
            string rejectionCode,
            LevelRunExtractionResultV1 extraction)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Extraction = extraction;
        }

        public Stage1RunCompletionStatusV1 Status { get; }
        public string RejectionCode { get; }
        public LevelRunExtractionResultV1 Extraction { get; }
        public bool Succeeded
        {
            get
            {
                return Status == Stage1RunCompletionStatusV1.CompletedAndCaptured
                    || Status == Stage1RunCompletionStatusV1.ExactDuplicateNoChange;
            }
        }
    }

    /// <summary>
    /// Production Stage 1 application session. Presentation registers the concrete
    /// actors it spawned and reports accepted destruction facts; this class owns the
    /// run-facing room registry, traversal, completion and frozen Results handoff.
    /// It does not spawn objects, poll renderers, award XP directly, or generate loot.
    /// </summary>
    public sealed class Stage1ProductionRunSessionV1
    {
        private readonly LevelRunCoordinatorV1 coordinator;
        private readonly Dictionary<StableId, Stage1RunRoomRegistrationV1> rooms =
            new Dictionary<StableId, Stage1RunRoomRegistrationV1>();
        private readonly Dictionary<StableId, Stage1RunEnemyRegistrationV1> enemies =
            new Dictionary<StableId, Stage1RunEnemyRegistrationV1>();
        private bool resultsCaptured;
        private Stage1RunCompletionResultV1 terminalCompletion;

        public Stage1ProductionRunSessionV1(LevelRunCoordinatorV1 coordinator)
        {
            this.coordinator = coordinator
                ?? throw new ArgumentNullException(nameof(coordinator));
            if (coordinator.SelectedLevelStableId != LevelRunCoordinatorV1.Level1StableId)
            {
                throw new ArgumentException(
                    "The Stage 1 production session requires the Level 1 coordinator.",
                    nameof(coordinator));
            }
        }

        public LevelRunCoordinatorV1 Coordinator { get { return coordinator; } }
        public StableId CurrentRoomStableId
        {
            get { return coordinator.RoomLayout.CurrentRoomState.RoomStableId; }
        }
        public bool HasTerminalResult { get { return terminalCompletion != null; } }
        public bool ResultsCaptured { get { return resultsCaptured; } }
        public int RegisteredRoomCount { get { return rooms.Count; } }
        public int RegisteredEnemyCount { get { return enemies.Count; } }

        public bool RegisterPlayerSource(
            StableId sourceActorStableId,
            StableId playerStableId)
        {
            return coordinator.RegisterPlayerSource(
                sourceActorStableId,
                playerStableId);
        }

        public Stage1RunRegistrationStatusV1 RegisterRoom(
            Stage1RunRoomRegistrationV1 registration)
        {
            if (registration == null)
            {
                return Stage1RunRegistrationStatusV1.InvalidRequest;
            }

            if (!IsSupportedRoom(registration.RoomStableId))
            {
                return Stage1RunRegistrationStatusV1.UnsupportedRoom;
            }

            Stage1RunRoomRegistrationV1 existing;
            if (rooms.TryGetValue(registration.RoomStableId, out existing))
            {
                return RegistrationsMatch(existing, registration)
                    ? Stage1RunRegistrationStatusV1.ExactDuplicateNoChange
                    : Stage1RunRegistrationStatusV1.ConflictingDuplicate;
            }

            for (int index = 0; index < registration.Enemies.Count; index++)
            {
                Stage1RunEnemyRegistrationV1 enemy = registration.Enemies[index];
                Stage1RunEnemyRegistrationV1 prior;
                if (enemies.TryGetValue(enemy.ActorStableId, out prior))
                {
                    return Stage1RunRegistrationStatusV1.ConflictingDuplicate;
                }
            }

            var actorIds = new List<StableId>(registration.Enemies.Count);
            for (int index = 0; index < registration.Enemies.Count; index++)
            {
                actorIds.Add(registration.Enemies[index].ActorStableId);
            }

            if (!coordinator.RegisterRoomEnemies(
                    registration.RoomStableId,
                    actorIds))
            {
                return Stage1RunRegistrationStatusV1.CoordinatorRejected;
            }

            rooms.Add(registration.RoomStableId, registration);
            for (int index = 0; index < registration.Enemies.Count; index++)
            {
                Stage1RunEnemyRegistrationV1 enemy = registration.Enemies[index];
                enemies.Add(enemy.ActorStableId, enemy);
            }

            return Stage1RunRegistrationStatusV1.Registered;
        }

        public LevelRunEnemyDestructionResultV1 RecordEnemyDestroyed(
            StableId roomStableId,
            EnemyDestroyedNotification destruction)
        {
            if (roomStableId == null
                || destruction == null
                || destruction.TargetId == null)
            {
                return InvalidDestruction("stage1-destruction-invalid");
            }

            Stage1RunRoomRegistrationV1 room;
            if (!rooms.TryGetValue(roomStableId, out room))
            {
                return InvalidDestruction("stage1-room-not-registered");
            }

            Stage1RunEnemyRegistrationV1 enemy;
            if (!enemies.TryGetValue(destruction.TargetId, out enemy))
            {
                return InvalidDestruction("stage1-enemy-not-registered");
            }

            return coordinator.RecordEnemyDestroyed(
                roomStableId,
                enemy.DefinitionStableId,
                enemy.Level,
                destruction);
        }

        public RoomGraphOperationResultV1 Traverse(StableId exitStableId)
        {
            return coordinator.Traverse(exitStableId);
        }

        public Stage1RunCompletionResultV1 CompleteAndCaptureResults()
        {
            if (terminalCompletion != null)
            {
                return new Stage1RunCompletionResultV1(
                    terminalCompletion.Succeeded
                        ? Stage1RunCompletionStatusV1.ExactDuplicateNoChange
                        : terminalCompletion.Status,
                    terminalCompletion.RejectionCode,
                    terminalCompletion.Extraction);
            }

            LevelRunExtractionResultV1 extraction = coordinator.RequestExtraction();
            if (extraction.Status == LevelRunExtractionStatusV1.WrongRoom
                || extraction.Status == LevelRunExtractionStatusV1.RoomNotClear)
            {
                return new Stage1RunCompletionResultV1(
                    Stage1RunCompletionStatusV1.NotReady,
                    extraction.RejectionCode,
                    extraction);
            }

            if (extraction.Status != LevelRunExtractionStatusV1.Completed
                || extraction.MissionResult == null
                || extraction.Summary == null)
            {
                terminalCompletion = new Stage1RunCompletionResultV1(
                    Stage1RunCompletionStatusV1.AuthorityRejected,
                    extraction.RejectionCode,
                    extraction);
                return terminalCompletion;
            }

            if (!resultsCaptured)
            {
                MissionResultsRouteContextV1.Capture(
                    new MissionResultsSessionV1(extraction.MissionResult),
                    coordinator.RoutePayload,
                    coordinator.SelectedModeStableId,
                    coordinator.SelectedLevelStableId,
                    extraction.Summary);
                resultsCaptured = true;
            }

            terminalCompletion = new Stage1RunCompletionResultV1(
                Stage1RunCompletionStatusV1.CompletedAndCaptured,
                string.Empty,
                extraction);
            return terminalCompletion;
        }

        private static bool IsSupportedRoom(StableId roomStableId)
        {
            return roomStableId == Level1RoomGraphDefinitionV1.EntryRoomStableId
                || roomStableId == Level1RoomGraphDefinitionV1.TerminalRoomStableId;
        }

        private static bool RegistrationsMatch(
            Stage1RunRoomRegistrationV1 left,
            Stage1RunRoomRegistrationV1 right)
        {
            if (left.Enemies.Count != right.Enemies.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Enemies.Count; index++)
            {
                Stage1RunEnemyRegistrationV1 expected = left.Enemies[index];
                bool found = false;
                for (int candidateIndex = 0;
                    candidateIndex < right.Enemies.Count;
                    candidateIndex++)
                {
                    Stage1RunEnemyRegistrationV1 candidate =
                        right.Enemies[candidateIndex];
                    if (candidate.ActorStableId == expected.ActorStableId
                        && candidate.DefinitionStableId == expected.DefinitionStableId
                        && candidate.Level == expected.Level)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private static LevelRunEnemyDestructionResultV1 InvalidDestruction(
            string rejectionCode)
        {
            return new LevelRunEnemyDestructionResultV1(
                LevelRunEnemyDestructionStatusV1.InvalidRequest,
                rejectionCode,
                null,
                0L,
                false);
        }
    }
}

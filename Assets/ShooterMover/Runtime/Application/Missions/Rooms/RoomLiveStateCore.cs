using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms
{
    /// <summary>
    /// Coordinated live-room command boundary. ROOM-RUNTIME-001 remains the sole
    /// occupancy/terminal authority; ROOM-001 remains traversal state authority.
    /// Mutable collaborators are private and callers receive immutable projections only.
    /// </summary>
    public sealed class RoomFlowState : IRoomLiveQuery
    {
        private readonly RoomOperationJournal operationJournal =
            new RoomOperationJournal();
        private readonly RoomCompletionEvaluator completionEvaluator =
            new RoomCompletionEvaluator();
        private readonly RoomDoorGatePolicy doorGatePolicy =
            new RoomDoorGatePolicy();
        private readonly RoomLiveViewBuilder projectionBuilder =
            new RoomLiveViewBuilder();
        private readonly Dictionary<StableId, RoomCompletionEvaluation> evaluations =
            new Dictionary<StableId, RoomCompletionEvaluation>();
        private readonly RoomRetainedFactStore retainedFacts;
        private readonly RoomTraversalFlow traversal;
        private long sequence;
        private StableId currentSpawnPointStableId;
        private bool finalExitReached;
        private RoomLiveView currentProjection;

        public RoomFlowState(
            StableId runtimeInstanceStableId,
            AuthorableRoomGraphDefinition definition)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            retainedFacts = new RoomRetainedFactStore(Definition);
            traversal = new RoomTraversalFlow(
                RuntimeInstanceStableId,
                Definition);
            for (int index = 0; index < Definition.Rooms.Count; index++)
            {
                RegisterAuthoredOccupants(Definition.Rooms[index]);
            }

            currentSpawnPointStableId = ResolveInitialSpawnPoint(
                Definition.GetRoom(Definition.StartRoomStableId));
            SynchronizeAllRoomFacts();
            RefreshProjection();
        }

        public StableId RuntimeInstanceStableId { get; }

        public AuthorableRoomGraphDefinition Definition { get; }

        public RoomLiveView CurrentProjection
        {
            get { return currentProjection; }
        }

        public RoomLiveRoomView GetRoomProjection(StableId roomStableId)
        {
            return currentProjection.GetRoom(roomStableId);
        }

        public RoomLiveOperationResult ReportOccupantTerminal(
            StableId operationStableId,
            StableId roomStableId,
            StableId occupantInstanceStableId)
        {
            RoomLiveView previous = currentProjection;
            string payload = "terminal|" + roomStableId + "|" + occupantInstanceStableId;
            RoomOperationInspection inspection = operationJournal.Inspect(
                operationStableId,
                payload);
            if (inspection == RoomOperationInspection.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatus.DuplicateNoChange,
                    string.Empty,
                    previous);
            }

            if (inspection == RoomOperationInspection.Conflict)
            {
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    "room-live-operation-id-conflict",
                    previous);
            }

            AuthorableRoomDefinition room;
            RoomPlacedEntityDefinition placement;
            if (!Definition.TryGetRoom(roomStableId, out room))
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    "room-live-room-unknown",
                    previous);
            }

            if (!room.TryGetPlacement(occupantInstanceStableId, out placement))
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    "room-live-occupant-unknown",
                    previous);
            }

            RoomLiveOperationResult occupancy =
                traversal.OccupancyAuthority.ReportTerminal(
                    new ReportRoomOccupantTerminalCommand(
                        RuntimeInstanceStableId,
                        InternalOperation(operationStableId, "occupancy-terminal"),
                        traversal.OccupancyAuthority.CurrentProjection.LifecycleGeneration,
                        roomStableId,
                        occupantInstanceStableId));
            operationJournal.Record(operationStableId, payload);
            if (occupancy.Status == RoomLiveOperationStatus.Rejected)
            {
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    occupancy.RejectionCode,
                    previous);
            }

            if (occupancy.Status != RoomLiveOperationStatus.Applied)
            {
                RefreshProjection();
                return Result(RoomLiveOperationStatus.NoChange, string.Empty, previous);
            }

            SynchronizeRoomFacts(roomStableId);
            sequence = checked(sequence + 1L);
            RefreshProjection();
            return Result(RoomLiveOperationStatus.Applied, string.Empty, previous);
        }

        /// <summary>
        /// Accepts a concrete drop identity only after another pickup/drop authority has
        /// accepted collection. This coordinator retains that accepted fact and evaluates
        /// any authored CollectedDrop conditions; it does not generate drops or rewards.
        /// </summary>
        public RoomLiveOperationResult ReportDropCollected(
            StableId operationStableId,
            StableId roomStableId,
            StableId dropInstanceStableId)
        {
            RoomLiveView previous = currentProjection;
            string payload = "drop|" + roomStableId + "|" + dropInstanceStableId;
            RoomOperationInspection inspection = operationJournal.Inspect(
                operationStableId,
                payload);
            if (inspection == RoomOperationInspection.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatus.DuplicateNoChange,
                    string.Empty,
                    previous);
            }

            if (inspection == RoomOperationInspection.Conflict)
            {
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    "room-live-operation-id-conflict",
                    previous);
            }

            if (dropInstanceStableId == null)
            {
                throw new ArgumentNullException(nameof(dropInstanceStableId));
            }

            AuthorableRoomDefinition ignored;
            if (!Definition.TryGetRoom(roomStableId, out ignored))
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    "room-live-room-unknown",
                    previous);
            }

            operationJournal.Record(operationStableId, payload);
            if (!retainedFacts.AddCollectedDrop(roomStableId, dropInstanceStableId))
            {
                return Result(RoomLiveOperationStatus.NoChange, string.Empty, previous);
            }

            SynchronizeRoomFacts(roomStableId);
            sequence = checked(sequence + 1L);
            RefreshProjection();
            return Result(RoomLiveOperationStatus.Applied, string.Empty, previous);
        }

        public RoomLiveOperationResult Traverse(
            StableId operationStableId,
            StableId exitStableId)
        {
            RoomLiveView previous = currentProjection;
            string payload = "traverse|" + exitStableId;
            RoomOperationInspection inspection = operationJournal.Inspect(
                operationStableId,
                payload);
            if (inspection == RoomOperationInspection.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatus.DuplicateNoChange,
                    string.Empty,
                    previous,
                    exitStableId);
            }

            if (inspection == RoomOperationInspection.Conflict)
            {
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    "room-live-operation-id-conflict",
                    previous,
                    exitStableId);
            }

            AuthorableRoomDefinition owner;
            RoomExitLinkDefinition exit;
            if (!Definition.TryGetExitOwner(exitStableId, out owner)
                || !owner.TryGetExit(exitStableId, out exit))
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    "room-live-exit-unknown",
                    previous,
                    exitStableId);
            }

            if (owner.RoomStableId != currentProjection.CurrentRoomStableId)
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    "room-live-exit-not-from-current-room",
                    previous,
                    exitStableId);
            }

            if (!retainedFacts.IsDoorOpen(
                owner.RoomStableId,
                exit.DoorInstanceStableId))
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    "room-live-door-closed",
                    previous,
                    exitStableId);
            }

            operationJournal.Record(operationStableId, payload);
            if (exit.LinkKind == RoomLiveLinkKind.FinalExit)
            {
                if (finalExitReached)
                {
                    return Result(
                        RoomLiveOperationStatus.NoChange,
                        string.Empty,
                        previous,
                        exitStableId);
                }

                finalExitReached = true;
                sequence = checked(sequence + 1L);
                RefreshProjection();
                return Result(
                    RoomLiveOperationStatus.FinalExitReached,
                    string.Empty,
                    previous,
                    exitStableId);
            }

            RoomTraversalResult traversalResult = traversal.Traverse(
                exit,
                InternalOperation(operationStableId, "occupancy-activate"));
            if (!traversalResult.Applied)
            {
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    traversalResult.RejectionCode,
                    previous,
                    exitStableId);
            }

            currentSpawnPointStableId = traversalResult.TargetSpawnPointStableId;
            SynchronizeRoomFacts(traversalResult.TargetRoomStableId);
            sequence = checked(sequence + 1L);
            RefreshProjection();
            return Result(
                RoomLiveOperationStatus.Applied,
                string.Empty,
                previous,
                exitStableId,
                traversalResult.TargetRoomStableId,
                traversalResult.TargetSpawnPointStableId);
        }

        public RoomLiveOperationResult Restart(StableId operationStableId)
        {
            RoomLiveView previous = currentProjection;
            string payload = "restart|" + currentProjection.LifecycleGeneration;
            RoomOperationInspection inspection = operationJournal.Inspect(
                operationStableId,
                payload);
            if (inspection == RoomOperationInspection.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatus.DuplicateNoChange,
                    string.Empty,
                    previous);
            }

            if (inspection == RoomOperationInspection.Conflict)
            {
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    "room-live-operation-id-conflict",
                    previous);
            }

            RoomLiveOperationResult occupancy = traversal.Restart(
                InternalOperation(operationStableId, "occupancy-restart"));
            operationJournal.Record(operationStableId, payload);
            if (occupancy.Status == RoomLiveOperationStatus.Rejected)
            {
                return Result(
                    RoomLiveOperationStatus.Rejected,
                    occupancy.RejectionCode,
                    previous);
            }

            retainedFacts.Clear();
            evaluations.Clear();
            finalExitReached = false;
            currentSpawnPointStableId = ResolveInitialSpawnPoint(
                Definition.GetRoom(Definition.StartRoomStableId));
            SynchronizeAllRoomFacts();
            sequence = checked(sequence + 1L);
            RefreshProjection();
            return Result(RoomLiveOperationStatus.Applied, string.Empty, previous);
        }

        private void RegisterAuthoredOccupants(AuthorableRoomDefinition room)
        {
            var occupants = new List<RoomOccupantRegistration>();
            for (int index = 0; index < room.Placements.Count; index++)
            {
                RoomPlacedEntityDefinition placement = room.Placements[index];
                occupants.Add(new RoomOccupantRegistration(
                    placement.InstanceStableId,
                    placement.DefinitionStableId,
                    placement.ClearRole));
            }

            RoomLiveOperationResult result =
                traversal.OccupancyAuthority.RegisterOccupants(
                    new RegisterRoomOccupantsCommand(
                        RuntimeInstanceStableId,
                        CreateInternalOperationStableId(
                            "register|" + room.RoomStableId),
                        traversal.OccupancyAuthority.CurrentProjection.LifecycleGeneration,
                        room.RoomStableId,
                        occupants));
            if (result.Status != RoomLiveOperationStatus.Applied)
            {
                throw new InvalidOperationException(
                    "Authored room occupancy registration failed: "
                    + result.RejectionCode);
            }
        }

        private void SynchronizeAllRoomFacts()
        {
            for (int index = 0; index < Definition.Rooms.Count; index++)
            {
                SynchronizeRoomFacts(Definition.Rooms[index].RoomStableId);
            }
        }

        private void SynchronizeRoomFacts(StableId roomStableId)
        {
            AuthorableRoomDefinition room = Definition.GetRoom(roomStableId);
            RoomOccupancyView occupancy =
                traversal.OccupancyAuthority.GetRoomProjection(roomStableId);
            RoomCompletionEvaluation evaluation = completionEvaluator.Evaluate(
                room,
                occupancy,
                retainedFacts.GetCollectedDrops(roomStableId));
            evaluations[roomStableId] = evaluation;

            RoomLiveState layoutState = traversal.MissionLayout.GetRoomState(
                roomStableId);
            if (occupancy.IsActive
                && layoutState.IsCurrent
                && !layoutState.IsCompleted
                && evaluation.IsRoomCompletionSatisfied)
            {
                traversal.CompleteCurrentRoom(roomStableId);
                layoutState = traversal.MissionLayout.GetRoomState(roomStableId);
            }

            IReadOnlyList<StableId> openDoors = doorGatePolicy.EvaluateOpenDoors(
                room,
                evaluation,
                layoutState.IsVisited);
            for (int index = 0; index < openDoors.Count; index++)
            {
                retainedFacts.OpenDoor(roomStableId, openDoors[index]);
            }
        }

        private void RefreshProjection()
        {
            currentProjection = projectionBuilder.Build(
                RuntimeInstanceStableId,
                Definition,
                traversal.OccupancyAuthority,
                traversal.MissionLayout,
                retainedFacts,
                evaluations,
                sequence,
                currentSpawnPointStableId,
                finalExitReached);
        }

        private RoomLiveOperationResult Result(
            RoomLiveOperationStatus status,
            string rejectionCode,
            RoomLiveView previous,
            StableId traversedExitStableId = null,
            StableId targetRoomStableId = null,
            StableId targetSpawnPointStableId = null)
        {
            return new RoomLiveOperationResult(
                status,
                rejectionCode,
                previous,
                currentProjection,
                traversedExitStableId,
                targetRoomStableId,
                targetSpawnPointStableId);
        }

        private static StableId InternalOperation(
            StableId externalOperationStableId,
            string suffix)
        {
            return CreateInternalOperationStableId(
                externalOperationStableId + "|" + suffix);
        }

        private static StableId CreateInternalOperationStableId(string payload)
        {
            using (System.Security.Cryptography.SHA256 sha =
                System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                var token = new StringBuilder(32);
                for (int index = 0; index < 16; index++)
                {
                    token.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }

                return StableId.Create(
                    "operation",
                    "room-live-" + token.ToString());
            }
        }

        private static StableId ResolveInitialSpawnPoint(
            AuthorableRoomDefinition room)
        {
            for (int index = 0; index < room.SpawnPoints.Count; index++)
            {
                if (room.SpawnPoints[index].Kind == RoomSpawnPointKind.ForwardEntry)
                {
                    return room.SpawnPoints[index].SpawnPointStableId;
                }
            }

            return room.SpawnPoints[0].SpawnPointStableId;
        }
    }
}

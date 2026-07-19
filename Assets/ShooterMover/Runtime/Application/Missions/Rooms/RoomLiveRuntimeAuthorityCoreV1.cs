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
    public sealed class RoomLiveRuntimeAuthorityV1 : IRoomLiveRuntimeQueryV1
    {
        private readonly RoomOperationJournalV1 operationJournal =
            new RoomOperationJournalV1();
        private readonly RoomCompletionEvaluatorV1 completionEvaluator =
            new RoomCompletionEvaluatorV1();
        private readonly RoomDoorGatePolicyV1 doorGatePolicy =
            new RoomDoorGatePolicyV1();
        private readonly RoomLiveProjectionBuilderV1 projectionBuilder =
            new RoomLiveProjectionBuilderV1();
        private readonly Dictionary<StableId, RoomCompletionEvaluationV1> evaluations =
            new Dictionary<StableId, RoomCompletionEvaluationV1>();
        private readonly RoomRetainedFactStoreV1 retainedFacts;
        private readonly RoomTraversalCoordinatorV1 traversal;
        private long sequence;
        private StableId currentSpawnPointStableId;
        private bool finalExitReached;
        private RoomLiveRuntimeProjectionV1 currentProjection;

        public RoomLiveRuntimeAuthorityV1(
            StableId runtimeInstanceStableId,
            AuthorableRoomGraphDefinitionV1 definition)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            retainedFacts = new RoomRetainedFactStoreV1(Definition);
            traversal = new RoomTraversalCoordinatorV1(
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

        public AuthorableRoomGraphDefinitionV1 Definition { get; }

        public RoomLiveRuntimeProjectionV1 CurrentProjection
        {
            get { return currentProjection; }
        }

        public RoomLiveRoomProjectionV1 GetRoomProjection(StableId roomStableId)
        {
            return currentProjection.GetRoom(roomStableId);
        }

        public RoomLiveOperationResultV1 ReportOccupantTerminal(
            StableId operationStableId,
            StableId roomStableId,
            StableId occupantInstanceStableId)
        {
            RoomLiveRuntimeProjectionV1 previous = currentProjection;
            string payload = "terminal|" + roomStableId + "|" + occupantInstanceStableId;
            RoomOperationInspectionV1 inspection = operationJournal.Inspect(
                operationStableId,
                payload);
            if (inspection == RoomOperationInspectionV1.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatusV1.DuplicateNoChange,
                    string.Empty,
                    previous);
            }

            if (inspection == RoomOperationInspectionV1.Conflict)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-operation-id-conflict",
                    previous);
            }

            AuthorableRoomDefinitionV1 room;
            RoomPlacedEntityDefinitionV1 placement;
            if (!Definition.TryGetRoom(roomStableId, out room))
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-room-unknown",
                    previous);
            }

            if (!room.TryGetPlacement(occupantInstanceStableId, out placement))
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-occupant-unknown",
                    previous);
            }

            RoomRuntimeOperationResultV1 occupancy =
                traversal.OccupancyAuthority.ReportTerminal(
                    new ReportRoomOccupantTerminalCommandV1(
                        RuntimeInstanceStableId,
                        InternalOperation(operationStableId, "occupancy-terminal"),
                        traversal.OccupancyAuthority.CurrentProjection.LifecycleGeneration,
                        roomStableId,
                        occupantInstanceStableId));
            operationJournal.Record(operationStableId, payload);
            if (occupancy.Status == RoomRuntimeOperationStatusV1.Rejected)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    occupancy.RejectionCode,
                    previous);
            }

            if (occupancy.Status != RoomRuntimeOperationStatusV1.Applied)
            {
                RefreshProjection();
                return Result(RoomLiveOperationStatusV1.NoChange, string.Empty, previous);
            }

            SynchronizeRoomFacts(roomStableId);
            sequence = checked(sequence + 1L);
            RefreshProjection();
            return Result(RoomLiveOperationStatusV1.Applied, string.Empty, previous);
        }

        /// <summary>
        /// Accepts a concrete drop identity only after another pickup/drop authority has
        /// accepted collection. This coordinator retains that accepted fact and evaluates
        /// any authored CollectedDrop conditions; it does not generate drops or rewards.
        /// </summary>
        public RoomLiveOperationResultV1 ReportDropCollected(
            StableId operationStableId,
            StableId roomStableId,
            StableId dropInstanceStableId)
        {
            RoomLiveRuntimeProjectionV1 previous = currentProjection;
            string payload = "drop|" + roomStableId + "|" + dropInstanceStableId;
            RoomOperationInspectionV1 inspection = operationJournal.Inspect(
                operationStableId,
                payload);
            if (inspection == RoomOperationInspectionV1.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatusV1.DuplicateNoChange,
                    string.Empty,
                    previous);
            }

            if (inspection == RoomOperationInspectionV1.Conflict)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-operation-id-conflict",
                    previous);
            }

            if (dropInstanceStableId == null)
            {
                throw new ArgumentNullException(nameof(dropInstanceStableId));
            }

            AuthorableRoomDefinitionV1 ignored;
            if (!Definition.TryGetRoom(roomStableId, out ignored))
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-room-unknown",
                    previous);
            }

            operationJournal.Record(operationStableId, payload);
            if (!retainedFacts.AddCollectedDrop(roomStableId, dropInstanceStableId))
            {
                return Result(RoomLiveOperationStatusV1.NoChange, string.Empty, previous);
            }

            SynchronizeRoomFacts(roomStableId);
            sequence = checked(sequence + 1L);
            RefreshProjection();
            return Result(RoomLiveOperationStatusV1.Applied, string.Empty, previous);
        }

        public RoomLiveOperationResultV1 Traverse(
            StableId operationStableId,
            StableId exitStableId)
        {
            RoomLiveRuntimeProjectionV1 previous = currentProjection;
            string payload = "traverse|" + exitStableId;
            RoomOperationInspectionV1 inspection = operationJournal.Inspect(
                operationStableId,
                payload);
            if (inspection == RoomOperationInspectionV1.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatusV1.DuplicateNoChange,
                    string.Empty,
                    previous,
                    exitStableId);
            }

            if (inspection == RoomOperationInspectionV1.Conflict)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-operation-id-conflict",
                    previous,
                    exitStableId);
            }

            AuthorableRoomDefinitionV1 owner;
            RoomExitLinkDefinitionV1 exit;
            if (!Definition.TryGetExitOwner(exitStableId, out owner)
                || !owner.TryGetExit(exitStableId, out exit))
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-exit-unknown",
                    previous,
                    exitStableId);
            }

            if (owner.RoomStableId != currentProjection.CurrentRoomStableId)
            {
                operationJournal.Record(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
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
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-door-closed",
                    previous,
                    exitStableId);
            }

            operationJournal.Record(operationStableId, payload);
            if (exit.LinkKind == RoomLiveLinkKindV1.FinalExit)
            {
                if (finalExitReached)
                {
                    return Result(
                        RoomLiveOperationStatusV1.NoChange,
                        string.Empty,
                        previous,
                        exitStableId);
                }

                finalExitReached = true;
                sequence = checked(sequence + 1L);
                RefreshProjection();
                return Result(
                    RoomLiveOperationStatusV1.FinalExitReached,
                    string.Empty,
                    previous,
                    exitStableId);
            }

            RoomTraversalResultV1 traversalResult = traversal.Traverse(
                exit,
                InternalOperation(operationStableId, "occupancy-activate"));
            if (!traversalResult.Applied)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    traversalResult.RejectionCode,
                    previous,
                    exitStableId);
            }

            currentSpawnPointStableId = traversalResult.TargetSpawnPointStableId;
            SynchronizeRoomFacts(traversalResult.TargetRoomStableId);
            sequence = checked(sequence + 1L);
            RefreshProjection();
            return Result(
                RoomLiveOperationStatusV1.Applied,
                string.Empty,
                previous,
                exitStableId,
                traversalResult.TargetRoomStableId,
                traversalResult.TargetSpawnPointStableId);
        }

        public RoomLiveOperationResultV1 Restart(StableId operationStableId)
        {
            RoomLiveRuntimeProjectionV1 previous = currentProjection;
            string payload = "restart|" + currentProjection.LifecycleGeneration;
            RoomOperationInspectionV1 inspection = operationJournal.Inspect(
                operationStableId,
                payload);
            if (inspection == RoomOperationInspectionV1.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatusV1.DuplicateNoChange,
                    string.Empty,
                    previous);
            }

            if (inspection == RoomOperationInspectionV1.Conflict)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-operation-id-conflict",
                    previous);
            }

            RoomRuntimeOperationResultV1 occupancy = traversal.Restart(
                InternalOperation(operationStableId, "occupancy-restart"));
            operationJournal.Record(operationStableId, payload);
            if (occupancy.Status == RoomRuntimeOperationStatusV1.Rejected)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
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
            return Result(RoomLiveOperationStatusV1.Applied, string.Empty, previous);
        }

        private void RegisterAuthoredOccupants(AuthorableRoomDefinitionV1 room)
        {
            var occupants = new List<RoomOccupantRegistrationV1>();
            for (int index = 0; index < room.Placements.Count; index++)
            {
                RoomPlacedEntityDefinitionV1 placement = room.Placements[index];
                occupants.Add(new RoomOccupantRegistrationV1(
                    placement.InstanceStableId,
                    placement.DefinitionStableId,
                    placement.ClearRole));
            }

            RoomRuntimeOperationResultV1 result =
                traversal.OccupancyAuthority.RegisterOccupants(
                    new RegisterRoomOccupantsCommandV1(
                        RuntimeInstanceStableId,
                        CreateInternalOperationStableId(
                            "register|" + room.RoomStableId),
                        traversal.OccupancyAuthority.CurrentProjection.LifecycleGeneration,
                        room.RoomStableId,
                        occupants));
            if (result.Status != RoomRuntimeOperationStatusV1.Applied)
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
            AuthorableRoomDefinitionV1 room = Definition.GetRoom(roomStableId);
            RoomOccupancyProjectionV1 occupancy =
                traversal.OccupancyAuthority.GetRoomProjection(roomStableId);
            RoomCompletionEvaluationV1 evaluation = completionEvaluator.Evaluate(
                room,
                occupancy,
                retainedFacts.GetCollectedDrops(roomStableId));
            evaluations[roomStableId] = evaluation;

            RoomRuntimeStateV1 layoutState = traversal.MissionLayout.GetRoomState(
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

        private RoomLiveOperationResultV1 Result(
            RoomLiveOperationStatusV1 status,
            string rejectionCode,
            RoomLiveRuntimeProjectionV1 previous,
            StableId traversedExitStableId = null,
            StableId targetRoomStableId = null,
            StableId targetSpawnPointStableId = null)
        {
            return new RoomLiveOperationResultV1(
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
            AuthorableRoomDefinitionV1 room)
        {
            for (int index = 0; index < room.SpawnPoints.Count; index++)
            {
                if (room.SpawnPoints[index].Kind == RoomSpawnPointKindV1.ForwardEntry)
                {
                    return room.SpawnPoints[index].SpawnPointStableId;
                }
            }

            return room.SpawnPoints[0].SpawnPointStableId;
        }
    }
}

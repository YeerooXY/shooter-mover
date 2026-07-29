using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Application.Missions.Rooms
{
    /// <summary>
    /// Engine-independent authority for graph-bound room occupancy and clear state.
    /// It owns registration, activation, retained terminal facts, clear transitions,
    /// lifecycle restart, and immutable exit eligibility projections. It deliberately
    /// does not traverse the graph or decide mission completion.
    /// </summary>
    public sealed class RoomOccupancy : IRoomOccupancy
    {
        private readonly Dictionary<StableId, MutableRoomState> rooms;
        private readonly Dictionary<StableId, string> operationPayloads;
        private long lifecycleGeneration;
        private long sequence;
        private RoomOccupancySnapshot currentProjection;

        public RoomOccupancy(
            StableId runtimeInstanceStableId,
            RoomGraphDefinition definition,
            long initialLifecycleGeneration = 1L)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            if (initialLifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialLifecycleGeneration));
            }

            lifecycleGeneration = initialLifecycleGeneration;
            rooms = new Dictionary<StableId, MutableRoomState>();
            operationPayloads = new Dictionary<StableId, string>();
            for (int index = 0; index < Definition.Rooms.Count; index++)
            {
                RoomDefinition roomDefinition = Definition.Rooms[index];
                rooms.Add(
                    roomDefinition.RoomStableId,
                    new MutableRoomState(
                        roomDefinition.RoomStableId,
                        roomDefinition.RoomStableId == Definition.StartRoomStableId));
            }

            RefreshProjection();
        }

        public StableId RuntimeInstanceStableId { get; }

        public RoomGraphDefinition Definition { get; }

        public RoomOccupancySnapshot CurrentProjection
        {
            get { return currentProjection; }
        }

        public RoomOccupancyView GetRoomProjection(StableId roomStableId)
        {
            return currentProjection.GetRoom(roomStableId);
        }

        public RoomOccupancyResult RegisterOccupants(
            RegisterRoomOccupantsCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            string payload = BuildRegisterPayload(command);
            OperationInspection inspection = InspectOperation(
                command.OperationStableId,
                payload);
            if (inspection == OperationInspection.Duplicate)
            {
                return DuplicateResult();
            }

            if (inspection == OperationInspection.Conflict)
            {
                return RejectedResult("room-operation-id-conflict");
            }

            RoomOccupancySnapshot previous = currentProjection;
            string commonRejection = ValidateCommon(
                command.RuntimeInstanceStableId,
                command.LifecycleGeneration);
            if (commonRejection.Length != 0)
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    commonRejection,
                    previous,
                    null);
            }

            MutableRoomState room;
            if (!rooms.TryGetValue(command.RoomStableId, out room))
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    "room-runtime-room-unknown",
                    previous,
                    null);
            }

            if (room.IsOccupancyRegistered)
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    "room-runtime-occupancy-already-registered",
                    previous,
                    null);
            }

            var registrations = new List<RoomOccupantRegistration>();
            var seenEntities = new HashSet<StableId>();
            for (int index = 0; index < command.Occupants.Count; index++)
            {
                RoomOccupantRegistration registration = command.Occupants[index];
                if (registration == null)
                {
                    return RecordResult(
                        command.OperationStableId,
                        payload,
                        RoomOccupancyStatus.Rejected,
                        "room-runtime-occupant-null",
                        previous,
                        null);
                }

                if (!seenEntities.Add(registration.EntityStableId))
                {
                    return RecordResult(
                        command.OperationStableId,
                        payload,
                        RoomOccupancyStatus.Rejected,
                        "room-runtime-occupant-entity-duplicate",
                        previous,
                        null);
                }

                registrations.Add(registration);
            }

            registrations.Sort((left, right) => left.EntityStableId.CompareTo(
                right.EntityStableId));
            room.RegisterInitialOccupants(registrations);
            sequence = checked(sequence + 1L);
            RefreshProjection();
            RoomClearTransition transition = room.IsCleared
                ? new RoomClearTransition(
                    RuntimeInstanceStableId,
                    room.RoomStableId,
                    command.OperationStableId,
                    lifecycleGeneration,
                    sequence)
                : null;
            return RecordResult(
                command.OperationStableId,
                payload,
                RoomOccupancyStatus.Applied,
                string.Empty,
                previous,
                transition);
        }

        public RoomOccupancyResult ActivateRoom(
            ActivateRoomCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            string payload = BuildActivatePayload(command);
            OperationInspection inspection = InspectOperation(
                command.OperationStableId,
                payload);
            if (inspection == OperationInspection.Duplicate)
            {
                return DuplicateResult();
            }

            if (inspection == OperationInspection.Conflict)
            {
                return RejectedResult("room-operation-id-conflict");
            }

            RoomOccupancySnapshot previous = currentProjection;
            string commonRejection = ValidateCommon(
                command.RuntimeInstanceStableId,
                command.LifecycleGeneration);
            if (commonRejection.Length != 0)
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    commonRejection,
                    previous,
                    null);
            }

            MutableRoomState target;
            if (!rooms.TryGetValue(command.RoomStableId, out target))
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    "room-runtime-room-unknown",
                    previous,
                    null);
            }

            if (!target.IsOccupancyRegistered)
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    "room-runtime-occupancy-not-registered",
                    previous,
                    null);
            }

            if (target.IsActive)
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.NoChange,
                    "room-runtime-room-already-active",
                    previous,
                    null);
            }

            foreach (KeyValuePair<StableId, MutableRoomState> pair in rooms)
            {
                pair.Value.IsActive = false;
            }

            target.IsActive = true;
            sequence = checked(sequence + 1L);
            RefreshProjection();
            return RecordResult(
                command.OperationStableId,
                payload,
                RoomOccupancyStatus.Applied,
                string.Empty,
                previous,
                null);
        }

        public RoomOccupancyResult ReportTerminal(
            ReportRoomOccupantTerminalCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            string payload = BuildTerminalPayload(command);
            OperationInspection inspection = InspectOperation(
                command.OperationStableId,
                payload);
            if (inspection == OperationInspection.Duplicate)
            {
                return DuplicateResult();
            }

            if (inspection == OperationInspection.Conflict)
            {
                return RejectedResult("room-operation-id-conflict");
            }

            RoomOccupancySnapshot previous = currentProjection;
            string commonRejection = ValidateCommon(
                command.RuntimeInstanceStableId,
                command.LifecycleGeneration);
            if (commonRejection.Length != 0)
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    commonRejection,
                    previous,
                    null);
            }

            MutableRoomState room;
            if (!rooms.TryGetValue(command.RoomStableId, out room))
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    "room-runtime-room-unknown",
                    previous,
                    null);
            }

            if (!room.IsOccupancyRegistered)
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    "room-runtime-occupancy-not-registered",
                    previous,
                    null);
            }

            MutableOccupantState occupant;
            if (!room.Occupants.TryGetValue(
                command.OccupantEntityStableId,
                out occupant))
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    "room-runtime-occupant-unknown",
                    previous,
                    null);
            }

            if (occupant.IsTerminal)
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.NoChange,
                    "room-runtime-occupant-already-terminal",
                    previous,
                    null);
            }

            bool wasCleared = room.IsCleared;
            occupant.IsTerminal = true;
            room.RecalculateClear();
            sequence = checked(sequence + 1L);
            RefreshProjection();
            RoomClearTransition transition = !wasCleared && room.IsCleared
                ? new RoomClearTransition(
                    RuntimeInstanceStableId,
                    room.RoomStableId,
                    command.OperationStableId,
                    lifecycleGeneration,
                    sequence)
                : null;
            return RecordResult(
                command.OperationStableId,
                payload,
                RoomOccupancyStatus.Applied,
                string.Empty,
                previous,
                transition);
        }

        public RoomOccupancyResult Restart(
            RestartRoomOccupancyCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            string payload = BuildRestartPayload(command);
            OperationInspection inspection = InspectOperation(
                command.OperationStableId,
                payload);
            if (inspection == OperationInspection.Duplicate)
            {
                return DuplicateResult();
            }

            if (inspection == OperationInspection.Conflict)
            {
                return RejectedResult("room-operation-id-conflict");
            }

            RoomOccupancySnapshot previous = currentProjection;
            string commonRejection = ValidateCommon(
                command.RuntimeInstanceStableId,
                command.LifecycleGeneration);
            if (commonRejection.Length != 0)
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    commonRejection,
                    previous,
                    null);
            }

            if (lifecycleGeneration == long.MaxValue)
            {
                return RecordResult(
                    command.OperationStableId,
                    payload,
                    RoomOccupancyStatus.Rejected,
                    "room-runtime-generation-exhausted",
                    previous,
                    null);
            }

            lifecycleGeneration++;
            foreach (KeyValuePair<StableId, MutableRoomState> pair in rooms)
            {
                pair.Value.RestoreInitial(
                    pair.Key == Definition.StartRoomStableId);
            }

            sequence = checked(sequence + 1L);
            RefreshProjection();
            return RecordResult(
                command.OperationStableId,
                payload,
                RoomOccupancyStatus.Applied,
                string.Empty,
                previous,
                null);
        }

        private string ValidateCommon(
            StableId commandRuntimeInstanceStableId,
            long commandLifecycleGeneration)
        {
            if (commandRuntimeInstanceStableId != RuntimeInstanceStableId)
            {
                return "room-runtime-instance-mismatch";
            }

            if (commandLifecycleGeneration != lifecycleGeneration)
            {
                return "room-runtime-generation-stale";
            }

            return string.Empty;
        }

        private OperationInspection InspectOperation(
            StableId operationStableId,
            string payload)
        {
            string existing;
            if (!operationPayloads.TryGetValue(operationStableId, out existing))
            {
                return OperationInspection.New;
            }

            return string.Equals(existing, payload, StringComparison.Ordinal)
                ? OperationInspection.Duplicate
                : OperationInspection.Conflict;
        }

        private RoomOccupancyResult RecordResult(
            StableId operationStableId,
            string payload,
            RoomOccupancyStatus status,
            string rejectionCode,
            RoomOccupancySnapshot previous,
            RoomClearTransition transition)
        {
            operationPayloads.Add(operationStableId, payload);
            return new RoomOccupancyResult(
                status,
                rejectionCode,
                previous,
                currentProjection,
                transition);
        }

        private RoomOccupancyResult DuplicateResult()
        {
            return new RoomOccupancyResult(
                RoomOccupancyStatus.DuplicateNoChange,
                "room-operation-duplicate",
                currentProjection,
                currentProjection,
                null);
        }

        private RoomOccupancyResult RejectedResult(string rejectionCode)
        {
            return new RoomOccupancyResult(
                RoomOccupancyStatus.Rejected,
                rejectionCode,
                currentProjection,
                currentProjection,
                null);
        }

        private void RefreshProjection()
        {
            var projectedRooms = new List<RoomOccupancyView>();
            for (int roomIndex = 0;
                roomIndex < Definition.Rooms.Count;
                roomIndex++)
            {
                RoomDefinition roomDefinition = Definition.Rooms[roomIndex];
                MutableRoomState room = rooms[roomDefinition.RoomStableId];
                var projectedOccupants = new List<RoomOccupantView>();
                foreach (KeyValuePair<StableId, MutableOccupantState> pair
                    in room.Occupants)
                {
                    MutableOccupantState occupant = pair.Value;
                    projectedOccupants.Add(new RoomOccupantView(
                        occupant.Registration.EntityStableId,
                        occupant.Registration.DefinitionStableId,
                        occupant.Registration.ClearRole,
                        occupant.IsTerminal));
                }

                IReadOnlyList<RoomExitDefinition> exits =
                    Definition.GetExitsFromRoom(room.RoomStableId);
                var projectedExits = new List<RoomExitEligibilityView>();
                for (int exitIndex = 0; exitIndex < exits.Count; exitIndex++)
                {
                    projectedExits.Add(new RoomExitEligibilityView(
                        exits[exitIndex].ExitStableId,
                        room.IsActive
                            && room.IsOccupancyRegistered
                            && room.IsCleared));
                }

                projectedRooms.Add(new RoomOccupancyView(
                    RuntimeInstanceStableId,
                    room.RoomStableId,
                    lifecycleGeneration,
                    room.IsActive,
                    room.IsOccupancyRegistered,
                    room.IsCleared,
                    projectedOccupants,
                    projectedExits));
            }

            currentProjection = new RoomOccupancySnapshot(
                RuntimeInstanceStableId,
                Definition.LayoutStableId,
                Definition.Fingerprint,
                lifecycleGeneration,
                sequence,
                projectedRooms);
        }

        private static string BuildRegisterPayload(
            RegisterRoomOccupantsCommand command)
        {
            var occupants = new List<RoomOccupantRegistration>();
            for (int index = 0; index < command.Occupants.Count; index++)
            {
                occupants.Add(command.Occupants[index]);
            }

            occupants.Sort(CompareRegistrations);
            var builder = new StringBuilder();
            AppendCommon(
                builder,
                "register",
                command.RuntimeInstanceStableId,
                command.LifecycleGeneration);
            builder.Append('|').Append(command.RoomStableId);
            for (int index = 0; index < occupants.Count; index++)
            {
                RoomOccupantRegistration occupant = occupants[index];
                builder.Append('|');
                if (occupant == null)
                {
                    builder.Append("null");
                    continue;
                }

                builder.Append(occupant.EntityStableId)
                    .Append(':')
                    .Append(occupant.DefinitionStableId)
                    .Append(':')
                    .Append(((int)occupant.ClearRole).ToString(
                        CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string BuildActivatePayload(ActivateRoomCommand command)
        {
            var builder = new StringBuilder();
            AppendCommon(
                builder,
                "activate",
                command.RuntimeInstanceStableId,
                command.LifecycleGeneration);
            builder.Append('|').Append(command.RoomStableId);
            return builder.ToString();
        }

        private static string BuildTerminalPayload(
            ReportRoomOccupantTerminalCommand command)
        {
            var builder = new StringBuilder();
            AppendCommon(
                builder,
                "terminal",
                command.RuntimeInstanceStableId,
                command.LifecycleGeneration);
            builder.Append('|').Append(command.RoomStableId)
                .Append('|').Append(command.OccupantEntityStableId);
            return builder.ToString();
        }

        private static string BuildRestartPayload(RestartRoomOccupancyCommand command)
        {
            var builder = new StringBuilder();
            AppendCommon(
                builder,
                "restart",
                command.RuntimeInstanceStableId,
                command.LifecycleGeneration);
            return builder.ToString();
        }

        private static void AppendCommon(
            StringBuilder builder,
            string kind,
            StableId runtimeInstanceStableId,
            long generation)
        {
            builder.Append(kind)
                .Append('|')
                .Append(runtimeInstanceStableId)
                .Append('|')
                .Append(generation.ToString(CultureInfo.InvariantCulture));
        }

        private static int CompareRegistrations(
            RoomOccupantRegistration left,
            RoomOccupantRegistration right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            return left.EntityStableId.CompareTo(right.EntityStableId);
        }

        private enum OperationInspection
        {
            New = 1,
            Duplicate = 2,
            Conflict = 3,
        }

        private sealed class MutableRoomState
        {
            private readonly List<RoomOccupantRegistration> initialOccupants;

            public MutableRoomState(StableId roomStableId, bool isActive)
            {
                RoomStableId = roomStableId
                    ?? throw new ArgumentNullException(nameof(roomStableId));
                IsActive = isActive;
                Occupants = new Dictionary<StableId, MutableOccupantState>();
                initialOccupants = new List<RoomOccupantRegistration>();
            }

            public StableId RoomStableId { get; }

            public bool IsActive { get; set; }

            public bool IsOccupancyRegistered { get; private set; }

            public bool IsCleared { get; private set; }

            public Dictionary<StableId, MutableOccupantState> Occupants { get; }

            public void RegisterInitialOccupants(
                IEnumerable<RoomOccupantRegistration> registrations)
            {
                initialOccupants.Clear();
                initialOccupants.AddRange(registrations);
                IsOccupancyRegistered = true;
                RestoreOccupants();
            }

            public void RestoreInitial(bool isActive)
            {
                IsActive = isActive;
                if (!IsOccupancyRegistered)
                {
                    Occupants.Clear();
                    IsCleared = false;
                    return;
                }

                RestoreOccupants();
            }

            public void RecalculateClear()
            {
                if (!IsOccupancyRegistered)
                {
                    IsCleared = false;
                    return;
                }

                foreach (KeyValuePair<StableId, MutableOccupantState> pair
                    in Occupants)
                {
                    MutableOccupantState occupant = pair.Value;
                    if (occupant.Registration.BlocksRoomClear
                        && !occupant.IsTerminal)
                    {
                        IsCleared = false;
                        return;
                    }
                }

                IsCleared = true;
            }

            private void RestoreOccupants()
            {
                Occupants.Clear();
                for (int index = 0; index < initialOccupants.Count; index++)
                {
                    RoomOccupantRegistration registration =
                        initialOccupants[index];
                    Occupants.Add(
                        registration.EntityStableId,
                        new MutableOccupantState(registration));
                }

                RecalculateClear();
            }
        }

        private sealed class MutableOccupantState
        {
            public MutableOccupantState(RoomOccupantRegistration registration)
            {
                Registration = registration
                    ?? throw new ArgumentNullException(nameof(registration));
            }

            public RoomOccupantRegistration Registration { get; }

            public bool IsTerminal { get; set; }
        }
    }
}

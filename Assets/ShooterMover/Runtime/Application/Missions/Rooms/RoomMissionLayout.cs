using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Application.Missions.Rooms
{
    /// <summary>
    /// Engine-independent state owner for one validated immutable room graph.
    /// Topology remains exclusively in Definition; mutable state contains only
    /// room/exit progress and a definition-bound deterministic snapshot.
    /// </summary>
    public sealed class RoomMissionLayout : IRoomMissionLayout
    {
        private Dictionary<StableId, RoomLiveState> roomStates;
        private Dictionary<StableId, RoomExitLiveState> exitStates;
        private long sequence;
        private RoomGraphSnapshot currentSnapshot;

        public RoomMissionLayout(RoomGraphDefinition definition)
        {
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            ResetToInitialState();
        }

        public RoomGraphDefinition Definition { get; }

        public RoomLiveState CurrentRoomState
        {
            get
            {
                for (int index = 0; index < Definition.Rooms.Count; index++)
                {
                    RoomLiveState state =
                        roomStates[Definition.Rooms[index].RoomStableId];
                    if (state.IsCurrent)
                    {
                        return state;
                    }
                }

                throw new InvalidOperationException(
                    "Validated room state must contain exactly one current room.");
            }
        }

        public IReadOnlyList<RoomLiveState> RoomStates
        {
            get
            {
                var result = new List<RoomLiveState>();
                for (int index = 0; index < Definition.Rooms.Count; index++)
                {
                    result.Add(
                        roomStates[Definition.Rooms[index].RoomStableId]);
                }

                return new ReadOnlyCollection<RoomLiveState>(result);
            }
        }

        public IReadOnlyList<RoomExitLiveState> ExitStates
        {
            get
            {
                var result = new List<RoomExitLiveState>();
                var ids = new List<StableId>(exitStates.Keys);
                ids.Sort();
                for (int index = 0; index < ids.Count; index++)
                {
                    result.Add(exitStates[ids[index]]);
                }

                return new ReadOnlyCollection<RoomExitLiveState>(result);
            }
        }

        public RoomGraphSnapshot CurrentSnapshot
        {
            get { return currentSnapshot; }
        }

        public RoomLiveState GetRoomState(StableId roomStableId)
        {
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }

            RoomLiveState state;
            if (!roomStates.TryGetValue(roomStableId, out state))
            {
                throw new KeyNotFoundException(
                    "Unknown room identity: " + roomStableId);
            }

            return state;
        }

        public RoomExitLiveState GetExitState(StableId exitStableId)
        {
            if (exitStableId == null)
            {
                throw new ArgumentNullException(nameof(exitStableId));
            }

            RoomExitLiveState state;
            if (!exitStates.TryGetValue(exitStableId, out state))
            {
                throw new KeyNotFoundException(
                    "Unknown exit identity: " + exitStableId);
            }

            return state;
        }

        public RoomGraphOperationResult CompleteCurrentRoom()
        {
            RoomGraphSnapshot previous = currentSnapshot;
            RoomLiveState current = CurrentRoomState;
            if (current.IsCompleted)
            {
                return OperationResult(
                    RoomGraphOperationStatus.NoChange,
                    "room-current-already-completed",
                    null,
                    previous);
            }

            roomStates[current.RoomStableId] = current.With(
                RoomAvailabilityState.Available,
                true,
                true,
                true);

            UnlockSatisfiedExits();
            PromoteAvailableTargets();
            sequence = checked(sequence + 1L);
            RefreshSnapshot();
            return OperationResult(
                RoomGraphOperationStatus.Applied,
                string.Empty,
                null,
                previous);
        }

        public RoomGraphOperationResult Traverse(StableId exitStableId)
        {
            RoomGraphSnapshot previous = currentSnapshot;
            RoomExitDefinition exit;
            if (!Definition.TryGetExit(exitStableId, out exit))
            {
                return OperationResult(
                    RoomGraphOperationStatus.UnknownExit,
                    "room-exit-unknown",
                    exitStableId,
                    previous);
            }

            RoomLiveState current = CurrentRoomState;
            if (exit.SourceRoomStableId != current.RoomStableId)
            {
                return OperationResult(
                    RoomGraphOperationStatus.ExitNotFromCurrentRoom,
                    "room-exit-not-from-current-room",
                    exitStableId,
                    previous);
            }

            RoomExitLiveState exitState = exitStates[exit.ExitStableId];
            if (!exitState.IsAvailable)
            {
                return OperationResult(
                    RoomGraphOperationStatus.ExitLocked,
                    "room-exit-locked",
                    exitStableId,
                    previous);
            }

            RoomDefinition targetDefinition = Definition.GetTargetRoom(exit);
            RoomLiveState target =
                roomStates[targetDefinition.RoomStableId];
            if (target.Availability != RoomAvailabilityState.Available)
            {
                return OperationResult(
                    RoomGraphOperationStatus.TargetRoomLocked,
                    "room-target-locked",
                    exitStableId,
                    previous);
            }

            roomStates[current.RoomStableId] = current.With(
                RoomAvailabilityState.Available,
                false,
                true,
                current.IsCompleted);
            roomStates[target.RoomStableId] = target.With(
                RoomAvailabilityState.Available,
                true,
                true,
                target.IsCompleted);

            sequence = checked(sequence + 1L);
            RefreshSnapshot();
            return OperationResult(
                RoomGraphOperationStatus.Applied,
                string.Empty,
                exitStableId,
                previous);
        }

        public RoomGraphOperationResult Restart()
        {
            RoomGraphSnapshot previous = currentSnapshot;
            Dictionary<StableId, RoomLiveState> initialRooms;
            Dictionary<StableId, RoomExitLiveState> initialExits;
            BuildInitialState(out initialRooms, out initialExits);
            RoomGraphSnapshot initialSnapshot = CreateSnapshot(
                0L,
                initialRooms,
                initialExits);

            if (string.Equals(
                initialSnapshot.Fingerprint,
                currentSnapshot.Fingerprint,
                StringComparison.Ordinal))
            {
                return OperationResult(
                    RoomGraphOperationStatus.NoChange,
                    "room-layout-already-initial",
                    null,
                    previous);
            }

            roomStates = initialRooms;
            exitStates = initialExits;
            sequence = 0L;
            currentSnapshot = initialSnapshot;
            return OperationResult(
                RoomGraphOperationStatus.Applied,
                string.Empty,
                null,
                previous);
        }

        public RoomGraphImportResult TryImport(RoomGraphSnapshot snapshot)
        {
            RoomGraphSnapshot previous = currentSnapshot;
            if (snapshot == null)
            {
                return ImportResult(
                    RoomGraphImportStatus.NullSnapshot,
                    "room-snapshot-null",
                    previous);
            }

            if (snapshot.SchemaVersion
                != RoomGraphSnapshot.CurrentSchemaVersion)
            {
                return ImportResult(
                    RoomGraphImportStatus.UnsupportedSchemaVersion,
                    "room-snapshot-schema-unsupported",
                    previous);
            }

            if (!string.Equals(
                snapshot.LayoutStableId,
                Definition.LayoutStableId.ToString(),
                StringComparison.Ordinal))
            {
                return ImportResult(
                    RoomGraphImportStatus.LayoutMismatch,
                    "room-snapshot-layout-mismatch",
                    previous);
            }

            if (!string.Equals(
                snapshot.DefinitionFingerprint,
                Definition.Fingerprint,
                StringComparison.Ordinal))
            {
                return ImportResult(
                    RoomGraphImportStatus.DefinitionFingerprintMismatch,
                    "room-snapshot-definition-mismatch",
                    previous);
            }

            if (!snapshot.HasValidFingerprint())
            {
                return ImportResult(
                    RoomGraphImportStatus.FingerprintMismatch,
                    "room-snapshot-fingerprint-mismatch",
                    previous);
            }

            Dictionary<StableId, RoomLiveState> importedRooms;
            Dictionary<StableId, RoomExitLiveState> importedExits;
            string rejectionCode;
            if (!TryValidateSnapshotState(
                snapshot,
                out importedRooms,
                out importedExits,
                out rejectionCode))
            {
                return ImportResult(
                    RoomGraphImportStatus.ValidationRejected,
                    rejectionCode,
                    previous);
            }

            RoomGraphSnapshot canonical = CreateSnapshot(
                snapshot.Sequence,
                importedRooms,
                importedExits);
            if (!string.Equals(
                canonical.Fingerprint,
                snapshot.Fingerprint,
                StringComparison.Ordinal))
            {
                return ImportResult(
                    RoomGraphImportStatus.ValidationRejected,
                    "room-snapshot-canonical-mismatch",
                    previous);
            }

            if (string.Equals(
                currentSnapshot.Fingerprint,
                canonical.Fingerprint,
                StringComparison.Ordinal))
            {
                return ImportResult(
                    RoomGraphImportStatus.DuplicateNoChange,
                    "room-snapshot-already-current",
                    previous);
            }

            roomStates = importedRooms;
            exitStates = importedExits;
            sequence = snapshot.Sequence;
            currentSnapshot = canonical;
            return ImportResult(
                RoomGraphImportStatus.Imported,
                string.Empty,
                previous);
        }

        public string CreateDebugProjection()
        {
            var builder = new StringBuilder();
            builder.Append("layout=")
                .Append(Definition.LayoutStableId)
                .Append(" definition=")
                .Append(Definition.Fingerprint)
                .Append(" sequence=")
                .Append(sequence.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            builder.Append("start=")
                .Append(Definition.StartRoomStableId)
                .Append(" terminal=")
                .Append(Definition.TerminalRoomStableId)
                .Append(" current=")
                .Append(CurrentRoomState.RoomStableId)
                .Append('\n');

            for (int roomIndex = 0;
                roomIndex < Definition.Rooms.Count;
                roomIndex++)
            {
                RoomDefinition room = Definition.Rooms[roomIndex];
                RoomLiveState state = roomStates[room.RoomStableId];
                builder.Append("room[")
                    .Append(room.Order.ToString(CultureInfo.InvariantCulture))
                    .Append("]=")
                    .Append(room.RoomStableId)
                    .Append(" availability=")
                    .Append(state.Availability)
                    .Append(" current=")
                    .Append(state.IsCurrent ? "1" : "0")
                    .Append(" visited=")
                    .Append(state.IsVisited ? "1" : "0")
                    .Append(" completed=")
                    .Append(state.IsCompleted ? "1" : "0")
                    .Append('\n');

                IReadOnlyList<RoomExitDefinition> exits =
                    Definition.GetExitsFromRoom(room.RoomStableId);
                for (int exitIndex = 0; exitIndex < exits.Count; exitIndex++)
                {
                    RoomExitDefinition exit = exits[exitIndex];
                    RoomDefinition target = Definition.GetTargetRoom(exit);
                    builder.Append("  exit[")
                        .Append(exit.Order.ToString(CultureInfo.InvariantCulture))
                        .Append("]=")
                        .Append(exit.ExitStableId)
                        .Append(" type=")
                        .Append(exit.ExitType)
                        .Append(" target=")
                        .Append(target.RoomStableId)
                        .Append(" available=")
                        .Append(
                            exitStates[exit.ExitStableId].IsAvailable
                                ? "1"
                                : "0")
                        .Append('\n');
                }
            }

            for (int connectionIndex = 0;
                connectionIndex < Definition.Connections.Count;
                connectionIndex++)
            {
                RoomConnectionDefinition connection =
                    Definition.Connections[connectionIndex];
                builder.Append("connection=")
                    .Append(connection.ConnectionStableId)
                    .Append(" directionality=")
                    .Append(connection.Directionality)
                    .Append(" door_link=")
                    .Append(
                        connection.DoorLinkStableId == null
                            ? "none"
                            : connection.DoorLinkStableId.ToString())
                    .Append('\n');
            }

            return builder.ToString();
        }

        private void ResetToInitialState()
        {
            BuildInitialState(out roomStates, out exitStates);
            sequence = 0L;
            currentSnapshot = CreateSnapshot(
                sequence,
                roomStates,
                exitStates);
        }

        private void BuildInitialState(
            out Dictionary<StableId, RoomLiveState> initialRooms,
            out Dictionary<StableId, RoomExitLiveState> initialExits)
        {
            initialRooms =
                new Dictionary<StableId, RoomLiveState>();
            initialExits =
                new Dictionary<StableId, RoomExitLiveState>();

            for (int index = 0; index < Definition.Rooms.Count; index++)
            {
                RoomDefinition room = Definition.Rooms[index];
                bool isStart =
                    room.RoomStableId == Definition.StartRoomStableId;
                RoomAvailabilityState availability =
                    isStart
                    || room.InitialAvailability
                        == RoomInitialAvailability.Available
                        ? RoomAvailabilityState.Available
                        : RoomAvailabilityState.Locked;
                initialRooms.Add(
                    room.RoomStableId,
                    new RoomLiveState(
                        room.RoomStableId,
                        availability,
                        isStart,
                        isStart,
                        false));
            }

            for (int connectionIndex = 0;
                connectionIndex < Definition.Connections.Count;
                connectionIndex++)
            {
                RoomConnectionDefinition connection =
                    Definition.Connections[connectionIndex];
                for (int exitIndex = 0;
                    exitIndex < connection.Exits.Count;
                    exitIndex++)
                {
                    RoomExitDefinition exit =
                        connection.Exits[exitIndex];
                    initialExits.Add(
                        exit.ExitStableId,
                        new RoomExitLiveState(
                            exit.ExitStableId,
                            !exit.InitiallyLocked));
                }
            }

            PromoteAvailableTargets(initialRooms, initialExits);
        }

        private void UnlockSatisfiedExits()
        {
            foreach (KeyValuePair<StableId, RoomExitLiveState> pair
                in new List<KeyValuePair<StableId, RoomExitLiveState>>(
                    exitStates))
            {
                if (pair.Value.IsAvailable)
                {
                    continue;
                }

                RoomExitDefinition exit;
                Definition.TryGetExit(pair.Key, out exit);
                StableId requirement =
                    exit.UnlockRequiredCompletedRoomStableId;
                if (requirement != null
                    && roomStates[requirement].IsCompleted)
                {
                    exitStates[pair.Key] =
                        pair.Value.WithAvailability(true);
                }
            }
        }

        private void PromoteAvailableTargets()
        {
            PromoteAvailableTargets(roomStates, exitStates);
        }

        private void PromoteAvailableTargets(
            Dictionary<StableId, RoomLiveState> rooms,
            Dictionary<StableId, RoomExitLiveState> exits)
        {
            bool changed;
            do
            {
                changed = false;
                for (int connectionIndex = 0;
                    connectionIndex < Definition.Connections.Count;
                    connectionIndex++)
                {
                    RoomConnectionDefinition connection =
                        Definition.Connections[connectionIndex];
                    for (int exitIndex = 0;
                        exitIndex < connection.Exits.Count;
                        exitIndex++)
                    {
                        RoomExitDefinition exit =
                            connection.Exits[exitIndex];
                        if (!exits[exit.ExitStableId].IsAvailable)
                        {
                            continue;
                        }

                        RoomLiveState source =
                            rooms[exit.SourceRoomStableId];
                        if (source.Availability
                            != RoomAvailabilityState.Available)
                        {
                            continue;
                        }

                        RoomDefinition targetDefinition =
                            Definition.GetTargetRoom(exit);
                        RoomLiveState target =
                            rooms[targetDefinition.RoomStableId];
                        if (target.Availability
                            == RoomAvailabilityState.Locked)
                        {
                            rooms[target.RoomStableId] = target.With(
                                RoomAvailabilityState.Available,
                                target.IsCurrent,
                                target.IsVisited,
                                target.IsCompleted);
                            changed = true;
                        }
                    }
                }
            }
            while (changed);
        }

        private bool TryValidateSnapshotState(
            RoomGraphSnapshot snapshot,
            out Dictionary<StableId, RoomLiveState> importedRooms,
            out Dictionary<StableId, RoomExitLiveState> importedExits,
            out string rejectionCode)
        {
            importedRooms =
                new Dictionary<StableId, RoomLiveState>();
            importedExits =
                new Dictionary<StableId, RoomExitLiveState>();
            rejectionCode = string.Empty;

            if (snapshot.Sequence < 0L)
            {
                rejectionCode = "room-snapshot-sequence-negative";
                return false;
            }

            if (snapshot.Rooms == null
                || snapshot.Rooms.Count != Definition.Rooms.Count)
            {
                rejectionCode = "room-snapshot-room-count-mismatch";
                return false;
            }

            int currentCount = 0;
            for (int index = 0; index < snapshot.Rooms.Count; index++)
            {
                RoomStateSnapshot record = snapshot.Rooms[index];
                if (record == null)
                {
                    rejectionCode = "room-snapshot-null-room";
                    return false;
                }

                StableId roomId;
                if (!StableId.TryParse(record.RoomStableId, out roomId))
                {
                    rejectionCode = "room-snapshot-room-id-invalid";
                    return false;
                }

                RoomDefinition definition;
                if (!Definition.TryGetRoom(roomId, out definition))
                {
                    rejectionCode = "room-snapshot-room-id-unknown";
                    return false;
                }

                if (importedRooms.ContainsKey(roomId))
                {
                    rejectionCode = "room-snapshot-room-id-duplicate";
                    return false;
                }

                RoomAvailabilityState availability =
                    (RoomAvailabilityState)record.Availability;
                if (!Enum.IsDefined(
                    typeof(RoomAvailabilityState),
                    availability))
                {
                    rejectionCode = "room-snapshot-room-availability-invalid";
                    return false;
                }

                if (record.IsCurrent)
                {
                    currentCount++;
                }

                if ((record.IsCurrent
                        || record.IsVisited
                        || record.IsCompleted)
                    && availability != RoomAvailabilityState.Available)
                {
                    rejectionCode = "room-snapshot-locked-room-has-progress";
                    return false;
                }

                if (record.IsCurrent && !record.IsVisited)
                {
                    rejectionCode = "room-snapshot-current-not-visited";
                    return false;
                }

                if (record.IsCompleted && !record.IsVisited)
                {
                    rejectionCode = "room-snapshot-completed-not-visited";
                    return false;
                }

                importedRooms.Add(
                    roomId,
                    new RoomLiveState(
                        roomId,
                        availability,
                        record.IsCurrent,
                        record.IsVisited,
                        record.IsCompleted));
            }

            if (currentCount != 1)
            {
                rejectionCode = "room-snapshot-current-count-invalid";
                return false;
            }

            if (snapshot.Exits == null
                || snapshot.Exits.Count != exitStates.Count)
            {
                rejectionCode = "room-snapshot-exit-count-mismatch";
                return false;
            }

            for (int index = 0; index < snapshot.Exits.Count; index++)
            {
                RoomExitStateSnapshot record = snapshot.Exits[index];
                if (record == null)
                {
                    rejectionCode = "room-snapshot-null-exit";
                    return false;
                }

                StableId exitId;
                if (!StableId.TryParse(record.ExitStableId, out exitId))
                {
                    rejectionCode = "room-snapshot-exit-id-invalid";
                    return false;
                }

                RoomExitDefinition exit;
                if (!Definition.TryGetExit(exitId, out exit))
                {
                    rejectionCode = "room-snapshot-exit-id-unknown";
                    return false;
                }

                if (importedExits.ContainsKey(exitId))
                {
                    rejectionCode = "room-snapshot-exit-id-duplicate";
                    return false;
                }

                if (!exit.InitiallyLocked && !record.IsAvailable)
                {
                    rejectionCode = "room-snapshot-unlocked-exit-regressed";
                    return false;
                }

                if (record.IsAvailable
                    && exit.UnlockRequiredCompletedRoomStableId != null
                    && !importedRooms[
                        exit.UnlockRequiredCompletedRoomStableId].IsCompleted)
                {
                    rejectionCode = "room-snapshot-exit-prerequisite-incomplete";
                    return false;
                }

                importedExits.Add(
                    exitId,
                    new RoomExitLiveState(
                        exitId,
                        record.IsAvailable));
            }

            foreach (RoomLiveState state in importedRooms.Values)
            {
                if (state.RoomStableId == Definition.StartRoomStableId
                    && !state.IsVisited)
                {
                    rejectionCode = "room-snapshot-start-not-visited";
                    return false;
                }
            }

            foreach (RoomExitLiveState state in importedExits.Values)
            {
                if (!state.IsAvailable)
                {
                    continue;
                }

                RoomExitDefinition exit;
                Definition.TryGetExit(state.ExitStableId, out exit);
                RoomLiveState source =
                    importedRooms[exit.SourceRoomStableId];
                if (source.Availability != RoomAvailabilityState.Available)
                {
                    continue;
                }

                RoomDefinition target = Definition.GetTargetRoom(exit);
                if (importedRooms[target.RoomStableId].Availability
                    != RoomAvailabilityState.Available)
                {
                    rejectionCode = "room-snapshot-available-target-locked";
                    return false;
                }
            }

            return true;
        }

        private RoomGraphSnapshot CreateSnapshot(
            long snapshotSequence,
            Dictionary<StableId, RoomLiveState> rooms,
            Dictionary<StableId, RoomExitLiveState> exits)
        {
            var roomRecords = new List<RoomStateSnapshot>();
            foreach (RoomLiveState state in rooms.Values)
            {
                roomRecords.Add(new RoomStateSnapshot(
                    state.RoomStableId.ToString(),
                    (int)state.Availability,
                    state.IsCurrent,
                    state.IsVisited,
                    state.IsCompleted));
            }

            var exitRecords = new List<RoomExitStateSnapshot>();
            foreach (RoomExitLiveState state in exits.Values)
            {
                exitRecords.Add(new RoomExitStateSnapshot(
                    state.ExitStableId.ToString(),
                    state.IsAvailable));
            }

            return RoomGraphSnapshot.CreateCanonical(
                Definition.LayoutStableId.ToString(),
                Definition.Fingerprint,
                snapshotSequence,
                roomRecords,
                exitRecords);
        }

        private void RefreshSnapshot()
        {
            currentSnapshot = CreateSnapshot(
                sequence,
                roomStates,
                exitStates);
        }

        private RoomGraphOperationResult OperationResult(
            RoomGraphOperationStatus status,
            string rejectionCode,
            StableId exitStableId,
            RoomGraphSnapshot previous)
        {
            return new RoomGraphOperationResult(
                status,
                rejectionCode,
                exitStableId,
                previous,
                currentSnapshot);
        }

        private RoomGraphImportResult ImportResult(
            RoomGraphImportStatus status,
            string rejectionCode,
            RoomGraphSnapshot previous)
        {
            return new RoomGraphImportResult(
                status,
                rejectionCode,
                previous,
                currentSnapshot);
        }
    }
}

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
    public sealed class RoomMissionLayoutV1 : IRoomMissionLayoutV1
    {
        private Dictionary<StableId, RoomRuntimeStateV1> roomStates;
        private Dictionary<StableId, RoomExitRuntimeStateV1> exitStates;
        private long sequence;
        private RoomGraphSnapshotV1 currentSnapshot;

        public RoomMissionLayoutV1(RoomGraphDefinitionV1 definition)
        {
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            ResetToInitialState();
        }

        public RoomGraphDefinitionV1 Definition { get; }

        public RoomRuntimeStateV1 CurrentRoomState
        {
            get
            {
                for (int index = 0; index < Definition.Rooms.Count; index++)
                {
                    RoomRuntimeStateV1 state =
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

        public IReadOnlyList<RoomRuntimeStateV1> RoomStates
        {
            get
            {
                var result = new List<RoomRuntimeStateV1>();
                for (int index = 0; index < Definition.Rooms.Count; index++)
                {
                    result.Add(
                        roomStates[Definition.Rooms[index].RoomStableId]);
                }

                return new ReadOnlyCollection<RoomRuntimeStateV1>(result);
            }
        }

        public IReadOnlyList<RoomExitRuntimeStateV1> ExitStates
        {
            get
            {
                var result = new List<RoomExitRuntimeStateV1>();
                var ids = new List<StableId>(exitStates.Keys);
                ids.Sort();
                for (int index = 0; index < ids.Count; index++)
                {
                    result.Add(exitStates[ids[index]]);
                }

                return new ReadOnlyCollection<RoomExitRuntimeStateV1>(result);
            }
        }

        public RoomGraphSnapshotV1 CurrentSnapshot
        {
            get { return currentSnapshot; }
        }

        public RoomRuntimeStateV1 GetRoomState(StableId roomStableId)
        {
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }

            RoomRuntimeStateV1 state;
            if (!roomStates.TryGetValue(roomStableId, out state))
            {
                throw new KeyNotFoundException(
                    "Unknown room identity: " + roomStableId);
            }

            return state;
        }

        public RoomExitRuntimeStateV1 GetExitState(StableId exitStableId)
        {
            if (exitStableId == null)
            {
                throw new ArgumentNullException(nameof(exitStableId));
            }

            RoomExitRuntimeStateV1 state;
            if (!exitStates.TryGetValue(exitStableId, out state))
            {
                throw new KeyNotFoundException(
                    "Unknown exit identity: " + exitStableId);
            }

            return state;
        }

        public RoomGraphOperationResultV1 CompleteCurrentRoom()
        {
            RoomGraphSnapshotV1 previous = currentSnapshot;
            RoomRuntimeStateV1 current = CurrentRoomState;
            if (current.IsCompleted)
            {
                return OperationResult(
                    RoomGraphOperationStatusV1.NoChange,
                    "room-current-already-completed",
                    null,
                    previous);
            }

            roomStates[current.RoomStableId] = current.With(
                RoomAvailabilityStateV1.Available,
                true,
                true,
                true);

            UnlockSatisfiedExits();
            PromoteAvailableTargets();
            sequence = checked(sequence + 1L);
            RefreshSnapshot();
            return OperationResult(
                RoomGraphOperationStatusV1.Applied,
                string.Empty,
                null,
                previous);
        }

        public RoomGraphOperationResultV1 Traverse(StableId exitStableId)
        {
            RoomGraphSnapshotV1 previous = currentSnapshot;
            RoomExitDefinitionV1 exit;
            if (!Definition.TryGetExit(exitStableId, out exit))
            {
                return OperationResult(
                    RoomGraphOperationStatusV1.UnknownExit,
                    "room-exit-unknown",
                    exitStableId,
                    previous);
            }

            RoomRuntimeStateV1 current = CurrentRoomState;
            if (exit.SourceRoomStableId != current.RoomStableId)
            {
                return OperationResult(
                    RoomGraphOperationStatusV1.ExitNotFromCurrentRoom,
                    "room-exit-not-from-current-room",
                    exitStableId,
                    previous);
            }

            RoomExitRuntimeStateV1 exitState = exitStates[exit.ExitStableId];
            if (!exitState.IsAvailable)
            {
                return OperationResult(
                    RoomGraphOperationStatusV1.ExitLocked,
                    "room-exit-locked",
                    exitStableId,
                    previous);
            }

            RoomDefinitionV1 targetDefinition = Definition.GetTargetRoom(exit);
            RoomRuntimeStateV1 target =
                roomStates[targetDefinition.RoomStableId];
            if (target.Availability != RoomAvailabilityStateV1.Available)
            {
                return OperationResult(
                    RoomGraphOperationStatusV1.TargetRoomLocked,
                    "room-target-locked",
                    exitStableId,
                    previous);
            }

            roomStates[current.RoomStableId] = current.With(
                RoomAvailabilityStateV1.Available,
                false,
                true,
                current.IsCompleted);
            roomStates[target.RoomStableId] = target.With(
                RoomAvailabilityStateV1.Available,
                true,
                true,
                target.IsCompleted);

            sequence = checked(sequence + 1L);
            RefreshSnapshot();
            return OperationResult(
                RoomGraphOperationStatusV1.Applied,
                string.Empty,
                exitStableId,
                previous);
        }

        public RoomGraphOperationResultV1 Restart()
        {
            RoomGraphSnapshotV1 previous = currentSnapshot;
            Dictionary<StableId, RoomRuntimeStateV1> initialRooms;
            Dictionary<StableId, RoomExitRuntimeStateV1> initialExits;
            BuildInitialState(out initialRooms, out initialExits);
            RoomGraphSnapshotV1 initialSnapshot = CreateSnapshot(
                0L,
                initialRooms,
                initialExits);

            if (string.Equals(
                initialSnapshot.Fingerprint,
                currentSnapshot.Fingerprint,
                StringComparison.Ordinal))
            {
                return OperationResult(
                    RoomGraphOperationStatusV1.NoChange,
                    "room-layout-already-initial",
                    null,
                    previous);
            }

            roomStates = initialRooms;
            exitStates = initialExits;
            sequence = 0L;
            currentSnapshot = initialSnapshot;
            return OperationResult(
                RoomGraphOperationStatusV1.Applied,
                string.Empty,
                null,
                previous);
        }

        public RoomGraphImportResultV1 TryImport(RoomGraphSnapshotV1 snapshot)
        {
            RoomGraphSnapshotV1 previous = currentSnapshot;
            if (snapshot == null)
            {
                return ImportResult(
                    RoomGraphImportStatusV1.NullSnapshot,
                    "room-snapshot-null",
                    previous);
            }

            if (snapshot.SchemaVersion
                != RoomGraphSnapshotV1.CurrentSchemaVersion)
            {
                return ImportResult(
                    RoomGraphImportStatusV1.UnsupportedSchemaVersion,
                    "room-snapshot-schema-unsupported",
                    previous);
            }

            if (!string.Equals(
                snapshot.LayoutStableId,
                Definition.LayoutStableId.ToString(),
                StringComparison.Ordinal))
            {
                return ImportResult(
                    RoomGraphImportStatusV1.LayoutMismatch,
                    "room-snapshot-layout-mismatch",
                    previous);
            }

            if (!string.Equals(
                snapshot.DefinitionFingerprint,
                Definition.Fingerprint,
                StringComparison.Ordinal))
            {
                return ImportResult(
                    RoomGraphImportStatusV1.DefinitionFingerprintMismatch,
                    "room-snapshot-definition-mismatch",
                    previous);
            }

            if (!snapshot.HasValidFingerprint())
            {
                return ImportResult(
                    RoomGraphImportStatusV1.FingerprintMismatch,
                    "room-snapshot-fingerprint-mismatch",
                    previous);
            }

            Dictionary<StableId, RoomRuntimeStateV1> importedRooms;
            Dictionary<StableId, RoomExitRuntimeStateV1> importedExits;
            string rejectionCode;
            if (!TryValidateSnapshotState(
                snapshot,
                out importedRooms,
                out importedExits,
                out rejectionCode))
            {
                return ImportResult(
                    RoomGraphImportStatusV1.ValidationRejected,
                    rejectionCode,
                    previous);
            }

            RoomGraphSnapshotV1 canonical = CreateSnapshot(
                snapshot.Sequence,
                importedRooms,
                importedExits);
            if (!string.Equals(
                canonical.Fingerprint,
                snapshot.Fingerprint,
                StringComparison.Ordinal))
            {
                return ImportResult(
                    RoomGraphImportStatusV1.ValidationRejected,
                    "room-snapshot-canonical-mismatch",
                    previous);
            }

            if (string.Equals(
                currentSnapshot.Fingerprint,
                canonical.Fingerprint,
                StringComparison.Ordinal))
            {
                return ImportResult(
                    RoomGraphImportStatusV1.DuplicateNoChange,
                    "room-snapshot-already-current",
                    previous);
            }

            roomStates = importedRooms;
            exitStates = importedExits;
            sequence = snapshot.Sequence;
            currentSnapshot = canonical;
            return ImportResult(
                RoomGraphImportStatusV1.Imported,
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
                RoomDefinitionV1 room = Definition.Rooms[roomIndex];
                RoomRuntimeStateV1 state = roomStates[room.RoomStableId];
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

                IReadOnlyList<RoomExitDefinitionV1> exits =
                    Definition.GetExitsFromRoom(room.RoomStableId);
                for (int exitIndex = 0; exitIndex < exits.Count; exitIndex++)
                {
                    RoomExitDefinitionV1 exit = exits[exitIndex];
                    RoomDefinitionV1 target = Definition.GetTargetRoom(exit);
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
                RoomConnectionDefinitionV1 connection =
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
            out Dictionary<StableId, RoomRuntimeStateV1> initialRooms,
            out Dictionary<StableId, RoomExitRuntimeStateV1> initialExits)
        {
            initialRooms =
                new Dictionary<StableId, RoomRuntimeStateV1>();
            initialExits =
                new Dictionary<StableId, RoomExitRuntimeStateV1>();

            for (int index = 0; index < Definition.Rooms.Count; index++)
            {
                RoomDefinitionV1 room = Definition.Rooms[index];
                bool isStart =
                    room.RoomStableId == Definition.StartRoomStableId;
                RoomAvailabilityStateV1 availability =
                    isStart
                    || room.InitialAvailability
                        == RoomInitialAvailabilityV1.Available
                        ? RoomAvailabilityStateV1.Available
                        : RoomAvailabilityStateV1.Locked;
                initialRooms.Add(
                    room.RoomStableId,
                    new RoomRuntimeStateV1(
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
                RoomConnectionDefinitionV1 connection =
                    Definition.Connections[connectionIndex];
                for (int exitIndex = 0;
                    exitIndex < connection.Exits.Count;
                    exitIndex++)
                {
                    RoomExitDefinitionV1 exit =
                        connection.Exits[exitIndex];
                    initialExits.Add(
                        exit.ExitStableId,
                        new RoomExitRuntimeStateV1(
                            exit.ExitStableId,
                            !exit.InitiallyLocked));
                }
            }

            PromoteAvailableTargets(initialRooms, initialExits);
        }

        private void UnlockSatisfiedExits()
        {
            foreach (KeyValuePair<StableId, RoomExitRuntimeStateV1> pair
                in new List<KeyValuePair<StableId, RoomExitRuntimeStateV1>>(
                    exitStates))
            {
                if (pair.Value.IsAvailable)
                {
                    continue;
                }

                RoomExitDefinitionV1 exit;
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
            Dictionary<StableId, RoomRuntimeStateV1> rooms,
            Dictionary<StableId, RoomExitRuntimeStateV1> exits)
        {
            bool changed;
            do
            {
                changed = false;
                for (int connectionIndex = 0;
                    connectionIndex < Definition.Connections.Count;
                    connectionIndex++)
                {
                    RoomConnectionDefinitionV1 connection =
                        Definition.Connections[connectionIndex];
                    for (int exitIndex = 0;
                        exitIndex < connection.Exits.Count;
                        exitIndex++)
                    {
                        RoomExitDefinitionV1 exit =
                            connection.Exits[exitIndex];
                        if (!exits[exit.ExitStableId].IsAvailable)
                        {
                            continue;
                        }

                        RoomRuntimeStateV1 source =
                            rooms[exit.SourceRoomStableId];
                        if (source.Availability
                            != RoomAvailabilityStateV1.Available)
                        {
                            continue;
                        }

                        RoomDefinitionV1 targetDefinition =
                            Definition.GetTargetRoom(exit);
                        RoomRuntimeStateV1 target =
                            rooms[targetDefinition.RoomStableId];
                        if (target.Availability
                            == RoomAvailabilityStateV1.Locked)
                        {
                            rooms[target.RoomStableId] = target.With(
                                RoomAvailabilityStateV1.Available,
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
            RoomGraphSnapshotV1 snapshot,
            out Dictionary<StableId, RoomRuntimeStateV1> importedRooms,
            out Dictionary<StableId, RoomExitRuntimeStateV1> importedExits,
            out string rejectionCode)
        {
            importedRooms =
                new Dictionary<StableId, RoomRuntimeStateV1>();
            importedExits =
                new Dictionary<StableId, RoomExitRuntimeStateV1>();
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
                RoomStateSnapshotV1 record = snapshot.Rooms[index];
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

                RoomDefinitionV1 definition;
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

                RoomAvailabilityStateV1 availability =
                    (RoomAvailabilityStateV1)record.Availability;
                if (!Enum.IsDefined(
                    typeof(RoomAvailabilityStateV1),
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
                    && availability != RoomAvailabilityStateV1.Available)
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
                    new RoomRuntimeStateV1(
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
                RoomExitStateSnapshotV1 record = snapshot.Exits[index];
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

                RoomExitDefinitionV1 exit;
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
                    new RoomExitRuntimeStateV1(
                        exitId,
                        record.IsAvailable));
            }

            foreach (RoomRuntimeStateV1 state in importedRooms.Values)
            {
                if (state.RoomStableId == Definition.StartRoomStableId
                    && !state.IsVisited)
                {
                    rejectionCode = "room-snapshot-start-not-visited";
                    return false;
                }
            }

            foreach (RoomExitRuntimeStateV1 state in importedExits.Values)
            {
                if (!state.IsAvailable)
                {
                    continue;
                }

                RoomExitDefinitionV1 exit;
                Definition.TryGetExit(state.ExitStableId, out exit);
                RoomRuntimeStateV1 source =
                    importedRooms[exit.SourceRoomStableId];
                if (source.Availability != RoomAvailabilityStateV1.Available)
                {
                    continue;
                }

                RoomDefinitionV1 target = Definition.GetTargetRoom(exit);
                if (importedRooms[target.RoomStableId].Availability
                    != RoomAvailabilityStateV1.Available)
                {
                    rejectionCode = "room-snapshot-available-target-locked";
                    return false;
                }
            }

            return true;
        }

        private RoomGraphSnapshotV1 CreateSnapshot(
            long snapshotSequence,
            Dictionary<StableId, RoomRuntimeStateV1> rooms,
            Dictionary<StableId, RoomExitRuntimeStateV1> exits)
        {
            var roomRecords = new List<RoomStateSnapshotV1>();
            foreach (RoomRuntimeStateV1 state in rooms.Values)
            {
                roomRecords.Add(new RoomStateSnapshotV1(
                    state.RoomStableId.ToString(),
                    (int)state.Availability,
                    state.IsCurrent,
                    state.IsVisited,
                    state.IsCompleted));
            }

            var exitRecords = new List<RoomExitStateSnapshotV1>();
            foreach (RoomExitRuntimeStateV1 state in exits.Values)
            {
                exitRecords.Add(new RoomExitStateSnapshotV1(
                    state.ExitStableId.ToString(),
                    state.IsAvailable));
            }

            return RoomGraphSnapshotV1.CreateCanonical(
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

        private RoomGraphOperationResultV1 OperationResult(
            RoomGraphOperationStatusV1 status,
            string rejectionCode,
            StableId exitStableId,
            RoomGraphSnapshotV1 previous)
        {
            return new RoomGraphOperationResultV1(
                status,
                rejectionCode,
                exitStableId,
                previous,
                currentSnapshot);
        }

        private RoomGraphImportResultV1 ImportResult(
            RoomGraphImportStatusV1 status,
            string rejectionCode,
            RoomGraphSnapshotV1 previous)
        {
            return new RoomGraphImportResultV1(
                status,
                rejectionCode,
                previous,
                currentSnapshot);
        }
    }
}

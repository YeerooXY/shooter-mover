using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Missions.Rooms
{
    public enum RoomInitialAvailability
    {
        Locked = 1,
        Available = 2,
    }

    public enum RoomConnectionDirectionality
    {
        OneWay = 1,
        Bidirectional = 2,
    }

    public enum RoomExitType
    {
        Progression = 1,
        Return = 2,
        Optional = 3,
        Secret = 4,
    }

    public enum RoomGraphValidationCode
    {
        MissingLayoutStableId = 1,
        MissingStartRoomStableId = 2,
        MissingTerminalRoomStableId = 3,
        MissingRooms = 4,
        MissingEntries = 5,
        MissingConnections = 6,
        MissingDoorLinks = 7,
        NullRoom = 8,
        MissingRoomStableId = 9,
        DuplicateRoomStableId = 10,
        DuplicateRoomOrder = 11,
        InvalidRoomAvailability = 12,
        InvalidStartRoom = 13,
        InvalidTerminalRoom = 14,
        StartEqualsTerminal = 15,
        NullEntry = 16,
        MissingEntryStableId = 17,
        DuplicateEntryStableId = 18,
        MissingEntryRoomStableId = 19,
        MissingEntryRoomReference = 20,
        DuplicateEntryOrder = 21,
        RoomHasNoEntry = 22,
        NullConnection = 23,
        MissingConnectionStableId = 24,
        DuplicateConnectionStableId = 25,
        InvalidConnectionDirectionality = 26,
        InvalidConnectionExitCount = 27,
        NullExit = 28,
        MissingExitStableId = 29,
        DuplicateExitStableId = 30,
        MissingExitSourceRoomStableId = 31,
        MissingExitSourceRoomReference = 32,
        MissingExitTargetEntryStableId = 33,
        MissingExitTargetEntryReference = 34,
        InvalidExitType = 35,
        DuplicateExitOrder = 36,
        SelfLink = 37,
        InvalidUnlockRule = 38,
        MissingUnlockRoomReference = 39,
        MismatchedReverseLink = 40,
        NullDoorLink = 41,
        MissingDoorLinkStableId = 42,
        DuplicateDoorLinkStableId = 43,
        DanglingDoorLink = 44,
        DoorLinkUsedByMultipleConnections = 45,
        UnusedDoorLink = 46,
        UnreachableRequiredRoom = 47,
        UnreachableTerminalRoom = 48,
    }

    public sealed class RoomGraphValidationIssue
    {
        public RoomGraphValidationIssue(
            RoomGraphValidationCode code,
            string subject,
            string message)
        {
            Code = code;
            Subject = subject ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public RoomGraphValidationCode Code { get; }

        public string Subject { get; }

        public string Message { get; }
    }

    public sealed class RoomGraphValidationResult
    {
        private readonly ReadOnlyCollection<RoomGraphValidationIssue> issues;

        internal RoomGraphValidationResult(
            RoomGraphDefinition definition,
            IEnumerable<RoomGraphValidationIssue> issues)
        {
            Definition = definition;
            this.issues = new ReadOnlyCollection<RoomGraphValidationIssue>(
                new List<RoomGraphValidationIssue>(
                    issues ?? throw new ArgumentNullException(nameof(issues))));
        }

        public RoomGraphDefinition Definition { get; }

        public IReadOnlyList<RoomGraphValidationIssue> Issues
        {
            get { return issues; }
        }

        public bool IsValid
        {
            get { return Definition != null && issues.Count == 0; }
        }

        public bool HasCode(RoomGraphValidationCode code)
        {
            for (int index = 0; index < issues.Count; index++)
            {
                if (issues[index].Code == code)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class RoomDefinition
    {
        public RoomDefinition(
            StableId roomStableId,
            int order,
            RoomInitialAvailability initialAvailability,
            bool isRequired)
        {
            RoomStableId = roomStableId;
            Order = order;
            InitialAvailability = initialAvailability;
            IsRequired = isRequired;
        }

        public StableId RoomStableId { get; }

        public int Order { get; }

        public RoomInitialAvailability InitialAvailability { get; }

        public bool IsRequired { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormat.AppendToken(
                builder,
                "room_stable_id",
                RoomStableId == null ? string.Empty : RoomStableId.ToString());
            RoomGraphFormat.AppendToken(
                builder,
                "order",
                Order.ToString(CultureInfo.InvariantCulture));
            RoomGraphFormat.AppendToken(
                builder,
                "initial_availability",
                ((int)InitialAvailability).ToString(CultureInfo.InvariantCulture));
            RoomGraphFormat.AppendToken(
                builder,
                "is_required",
                IsRequired ? "1" : "0");
            return builder.ToString();
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    public sealed class RoomEntryDefinition
    {
        public RoomEntryDefinition(
            StableId entryStableId,
            StableId roomStableId,
            int order)
        {
            EntryStableId = entryStableId;
            RoomStableId = roomStableId;
            Order = order;
        }

        public StableId EntryStableId { get; }

        public StableId RoomStableId { get; }

        public int Order { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormat.AppendToken(
                builder,
                "entry_stable_id",
                EntryStableId == null ? string.Empty : EntryStableId.ToString());
            RoomGraphFormat.AppendToken(
                builder,
                "room_stable_id",
                RoomStableId == null ? string.Empty : RoomStableId.ToString());
            RoomGraphFormat.AppendToken(
                builder,
                "order",
                Order.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    public sealed class RoomExitDefinition
    {
        public RoomExitDefinition(
            StableId exitStableId,
            StableId sourceRoomStableId,
            StableId targetEntryStableId,
            int order,
            RoomExitType exitType,
            bool initiallyLocked,
            StableId unlockRequiredCompletedRoomStableId)
        {
            ExitStableId = exitStableId;
            SourceRoomStableId = sourceRoomStableId;
            TargetEntryStableId = targetEntryStableId;
            Order = order;
            ExitType = exitType;
            InitiallyLocked = initiallyLocked;
            UnlockRequiredCompletedRoomStableId =
                unlockRequiredCompletedRoomStableId;
        }

        public StableId ExitStableId { get; }

        public StableId SourceRoomStableId { get; }

        public StableId TargetEntryStableId { get; }

        public int Order { get; }

        public RoomExitType ExitType { get; }

        public bool InitiallyLocked { get; }

        public StableId UnlockRequiredCompletedRoomStableId { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormat.AppendToken(
                builder,
                "exit_stable_id",
                ExitStableId == null ? string.Empty : ExitStableId.ToString());
            RoomGraphFormat.AppendToken(
                builder,
                "source_room_stable_id",
                SourceRoomStableId == null
                    ? string.Empty
                    : SourceRoomStableId.ToString());
            RoomGraphFormat.AppendToken(
                builder,
                "target_entry_stable_id",
                TargetEntryStableId == null
                    ? string.Empty
                    : TargetEntryStableId.ToString());
            RoomGraphFormat.AppendToken(
                builder,
                "order",
                Order.ToString(CultureInfo.InvariantCulture));
            RoomGraphFormat.AppendToken(
                builder,
                "exit_type",
                ((int)ExitType).ToString(CultureInfo.InvariantCulture));
            RoomGraphFormat.AppendToken(
                builder,
                "initially_locked",
                InitiallyLocked ? "1" : "0");
            RoomGraphFormat.AppendToken(
                builder,
                "unlock_required_completed_room_stable_id",
                UnlockRequiredCompletedRoomStableId == null
                    ? string.Empty
                    : UnlockRequiredCompletedRoomStableId.ToString());
            return builder.ToString();
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    public sealed class RoomDoorLinkDefinition
    {
        public RoomDoorLinkDefinition(StableId doorLinkStableId)
        {
            DoorLinkStableId = doorLinkStableId;
        }

        public StableId DoorLinkStableId { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormat.AppendToken(
                builder,
                "door_link_stable_id",
                DoorLinkStableId == null
                    ? string.Empty
                    : DoorLinkStableId.ToString());
            return builder.ToString();
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    public sealed class RoomConnectionDefinition
    {
        private readonly ReadOnlyCollection<RoomExitDefinition> exits;

        public RoomConnectionDefinition(
            StableId connectionStableId,
            RoomConnectionDirectionality directionality,
            StableId doorLinkStableId,
            IEnumerable<RoomExitDefinition> exits)
        {
            ConnectionStableId = connectionStableId;
            Directionality = directionality;
            DoorLinkStableId = doorLinkStableId;
            this.exits = exits == null
                ? null
                : new ReadOnlyCollection<RoomExitDefinition>(
                    new List<RoomExitDefinition>(exits));
        }

        public StableId ConnectionStableId { get; }

        public RoomConnectionDirectionality Directionality { get; }

        public StableId DoorLinkStableId { get; }

        public IReadOnlyList<RoomExitDefinition> Exits
        {
            get { return exits; }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormat.AppendToken(
                builder,
                "connection_stable_id",
                ConnectionStableId == null
                    ? string.Empty
                    : ConnectionStableId.ToString());
            RoomGraphFormat.AppendToken(
                builder,
                "directionality",
                ((int)Directionality).ToString(CultureInfo.InvariantCulture));
            RoomGraphFormat.AppendToken(
                builder,
                "door_link_stable_id",
                DoorLinkStableId == null
                    ? string.Empty
                    : DoorLinkStableId.ToString());
            RoomGraphFormat.AppendToken(
                builder,
                "exit_count",
                exits == null
                    ? "-1"
                    : exits.Count.ToString(CultureInfo.InvariantCulture));
            if (exits != null)
            {
                var ordered = new List<RoomExitDefinition>(exits);
                ordered.Sort(RoomGraphDefinition.CompareExits);
                for (int index = 0; index < ordered.Count; index++)
                {
                    RoomGraphFormat.AppendToken(
                        builder,
                        "exit_" + index.ToString("D4", CultureInfo.InvariantCulture),
                        ordered[index] == null
                            ? string.Empty
                            : ordered[index].ToCanonicalString());
                }
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// Immutable validated mission layout. It is the single graph-truth owner:
    /// runtime state and snapshots reference its fingerprint instead of copying topology.
    /// </summary>
    public sealed class RoomGraphDefinition
    {
        private const string SchemaId = "room-graph-definition-v1";

        private readonly ReadOnlyCollection<RoomDefinition> rooms;
        private readonly ReadOnlyCollection<RoomEntryDefinition> entries;
        private readonly ReadOnlyCollection<RoomConnectionDefinition> connections;
        private readonly ReadOnlyCollection<RoomDoorLinkDefinition> doorLinks;
        private readonly Dictionary<StableId, RoomDefinition> roomsById;
        private readonly Dictionary<StableId, RoomEntryDefinition> entriesById;
        private readonly Dictionary<StableId, RoomExitDefinition> exitsById;
        private readonly Dictionary<StableId, ReadOnlyCollection<RoomExitDefinition>>
            exitsBySourceRoom;

        private RoomGraphDefinition(
            StableId layoutStableId,
            StableId startRoomStableId,
            StableId terminalRoomStableId,
            IEnumerable<RoomDefinition> rooms,
            IEnumerable<RoomEntryDefinition> entries,
            IEnumerable<RoomConnectionDefinition> connections,
            IEnumerable<RoomDoorLinkDefinition> doorLinks)
        {
            LayoutStableId = layoutStableId;
            StartRoomStableId = startRoomStableId;
            TerminalRoomStableId = terminalRoomStableId;

            var orderedRooms = new List<RoomDefinition>(rooms);
            orderedRooms.Sort(CompareRooms);
            this.rooms = new ReadOnlyCollection<RoomDefinition>(orderedRooms);

            var orderedEntries = new List<RoomEntryDefinition>(entries);
            orderedEntries.Sort(CompareEntries);
            this.entries = new ReadOnlyCollection<RoomEntryDefinition>(orderedEntries);

            var orderedConnections = new List<RoomConnectionDefinition>(connections);
            orderedConnections.Sort(CompareConnections);
            this.connections =
                new ReadOnlyCollection<RoomConnectionDefinition>(orderedConnections);

            var orderedDoorLinks = new List<RoomDoorLinkDefinition>(doorLinks);
            orderedDoorLinks.Sort(CompareDoorLinks);
            this.doorLinks =
                new ReadOnlyCollection<RoomDoorLinkDefinition>(orderedDoorLinks);

            roomsById = new Dictionary<StableId, RoomDefinition>();
            entriesById = new Dictionary<StableId, RoomEntryDefinition>();
            exitsById = new Dictionary<StableId, RoomExitDefinition>();
            var mutableExitsByRoom =
                new Dictionary<StableId, List<RoomExitDefinition>>();

            for (int index = 0; index < orderedRooms.Count; index++)
            {
                RoomDefinition room = orderedRooms[index];
                roomsById.Add(room.RoomStableId, room);
                mutableExitsByRoom.Add(
                    room.RoomStableId,
                    new List<RoomExitDefinition>());
            }

            for (int index = 0; index < orderedEntries.Count; index++)
            {
                RoomEntryDefinition entry = orderedEntries[index];
                entriesById.Add(entry.EntryStableId, entry);
            }

            for (int connectionIndex = 0;
                connectionIndex < orderedConnections.Count;
                connectionIndex++)
            {
                RoomConnectionDefinition connection =
                    orderedConnections[connectionIndex];
                for (int exitIndex = 0;
                    exitIndex < connection.Exits.Count;
                    exitIndex++)
                {
                    RoomExitDefinition exit = connection.Exits[exitIndex];
                    exitsById.Add(exit.ExitStableId, exit);
                    mutableExitsByRoom[exit.SourceRoomStableId].Add(exit);
                }
            }

            exitsBySourceRoom =
                new Dictionary<StableId, ReadOnlyCollection<RoomExitDefinition>>();
            foreach (KeyValuePair<StableId, List<RoomExitDefinition>> pair
                in mutableExitsByRoom)
            {
                pair.Value.Sort(CompareExits);
                exitsBySourceRoom.Add(
                    pair.Key,
                    new ReadOnlyCollection<RoomExitDefinition>(pair.Value));
            }

            Fingerprint = RoomGraphFormat.ComputeSha256(ToCanonicalString());
        }

        public StableId LayoutStableId { get; }

        public StableId StartRoomStableId { get; }

        public StableId TerminalRoomStableId { get; }

        public IReadOnlyList<RoomDefinition> Rooms
        {
            get { return rooms; }
        }

        public IReadOnlyList<RoomEntryDefinition> Entries
        {
            get { return entries; }
        }

        public IReadOnlyList<RoomConnectionDefinition> Connections
        {
            get { return connections; }
        }

        public IReadOnlyList<RoomDoorLinkDefinition> DoorLinks
        {
            get { return doorLinks; }
        }

        public string Fingerprint { get; }

        public static RoomGraphValidationResult ValidateAndCreate(
            StableId layoutStableId,
            StableId startRoomStableId,
            StableId terminalRoomStableId,
            IEnumerable<RoomDefinition> rooms,
            IEnumerable<RoomEntryDefinition> entries,
            IEnumerable<RoomConnectionDefinition> connections,
            IEnumerable<RoomDoorLinkDefinition> doorLinks)
        {
            var issues = new List<RoomGraphValidationIssue>();
            if (layoutStableId == null)
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCode.MissingLayoutStableId,
                    "layout",
                    "Mission layout identity is required.");
            }

            if (startRoomStableId == null)
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCode.MissingStartRoomStableId,
                    "start-room",
                    "Start room identity is required.");
            }

            if (terminalRoomStableId == null)
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCode.MissingTerminalRoomStableId,
                    "terminal-room",
                    "Terminal room identity is required.");
            }

            List<RoomDefinition> roomList = CopyOrIssue(
                rooms,
                issues,
                RoomGraphValidationCode.MissingRooms,
                "rooms");
            List<RoomEntryDefinition> entryList = CopyOrIssue(
                entries,
                issues,
                RoomGraphValidationCode.MissingEntries,
                "entries");
            List<RoomConnectionDefinition> connectionList = CopyOrIssue(
                connections,
                issues,
                RoomGraphValidationCode.MissingConnections,
                "connections");
            List<RoomDoorLinkDefinition> doorLinkList = CopyOrIssue(
                doorLinks,
                issues,
                RoomGraphValidationCode.MissingDoorLinks,
                "door-links");

            if (roomList == null
                || entryList == null
                || connectionList == null
                || doorLinkList == null)
            {
                return new RoomGraphValidationResult(null, issues);
            }

            var roomMap = new Dictionary<StableId, RoomDefinition>();
            var roomOrders = new Dictionary<int, StableId>();
            for (int index = 0; index < roomList.Count; index++)
            {
                RoomDefinition room = roomList[index];
                if (room == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.NullRoom,
                        "rooms[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Room definitions cannot contain null values.");
                    continue;
                }

                if (room.RoomStableId == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.MissingRoomStableId,
                        "rooms[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every room requires a stable identity.");
                }
                else if (roomMap.ContainsKey(room.RoomStableId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.DuplicateRoomStableId,
                        room.RoomStableId.ToString(),
                        "Room identities must be unique.");
                }
                else
                {
                    roomMap.Add(room.RoomStableId, room);
                }

                StableId existingOrderOwner;
                if (roomOrders.TryGetValue(room.Order, out existingOrderOwner))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.DuplicateRoomOrder,
                        room.Order.ToString(CultureInfo.InvariantCulture),
                        "Room order values must be unique.");
                }
                else
                {
                    roomOrders.Add(room.Order, room.RoomStableId);
                }

                if (!Enum.IsDefined(
                    typeof(RoomInitialAvailability),
                    room.InitialAvailability))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.InvalidRoomAvailability,
                        room.RoomStableId == null
                            ? "unknown-room"
                            : room.RoomStableId.ToString(),
                        "Room initial availability is not supported.");
                }
            }

            if (startRoomStableId != null && !roomMap.ContainsKey(startRoomStableId))
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCode.InvalidStartRoom,
                    startRoomStableId.ToString(),
                    "Start room must reference a defined room.");
            }

            if (terminalRoomStableId != null
                && !roomMap.ContainsKey(terminalRoomStableId))
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCode.InvalidTerminalRoom,
                    terminalRoomStableId.ToString(),
                    "Terminal room must reference a defined room.");
            }

            if (startRoomStableId != null
                && terminalRoomStableId != null
                && startRoomStableId == terminalRoomStableId)
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCode.StartEqualsTerminal,
                    startRoomStableId.ToString(),
                    "Start and terminal rooms must be distinct.");
            }

            var entryMap = new Dictionary<StableId, RoomEntryDefinition>();
            var entryOrdersByRoom = new Dictionary<StableId, HashSet<int>>();
            var entryCountByRoom = new Dictionary<StableId, int>();
            foreach (StableId roomId in roomMap.Keys)
            {
                entryOrdersByRoom.Add(roomId, new HashSet<int>());
                entryCountByRoom.Add(roomId, 0);
            }

            for (int index = 0; index < entryList.Count; index++)
            {
                RoomEntryDefinition entry = entryList[index];
                if (entry == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.NullEntry,
                        "entries[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Room entries cannot contain null values.");
                    continue;
                }

                if (entry.EntryStableId == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.MissingEntryStableId,
                        "entries[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every entry requires a stable identity.");
                }
                else if (entryMap.ContainsKey(entry.EntryStableId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.DuplicateEntryStableId,
                        entry.EntryStableId.ToString(),
                        "Entry identities must be unique.");
                }
                else
                {
                    entryMap.Add(entry.EntryStableId, entry);
                }

                if (entry.RoomStableId == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.MissingEntryRoomStableId,
                        entry.EntryStableId == null
                            ? "unknown-entry"
                            : entry.EntryStableId.ToString(),
                        "Every entry requires an owning room identity.");
                    continue;
                }

                if (!roomMap.ContainsKey(entry.RoomStableId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.MissingEntryRoomReference,
                        entry.RoomStableId.ToString(),
                        "Entry owner must reference a defined room.");
                    continue;
                }

                entryCountByRoom[entry.RoomStableId]++;
                if (!entryOrdersByRoom[entry.RoomStableId].Add(entry.Order))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.DuplicateEntryOrder,
                        entry.RoomStableId + ":" + entry.Order.ToString(
                            CultureInfo.InvariantCulture),
                        "Entry order values must be unique within each room.");
                }
            }

            foreach (KeyValuePair<StableId, int> pair in entryCountByRoom)
            {
                if (pair.Value == 0)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.RoomHasNoEntry,
                        pair.Key.ToString(),
                        "Every room requires at least one stable entry.");
                }
            }

            var doorLinkMap =
                new Dictionary<StableId, RoomDoorLinkDefinition>();
            for (int index = 0; index < doorLinkList.Count; index++)
            {
                RoomDoorLinkDefinition doorLink = doorLinkList[index];
                if (doorLink == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.NullDoorLink,
                        "door-links[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Door links cannot contain null values.");
                    continue;
                }

                if (doorLink.DoorLinkStableId == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.MissingDoorLinkStableId,
                        "door-links[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every door link requires a stable identity.");
                }
                else if (doorLinkMap.ContainsKey(doorLink.DoorLinkStableId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.DuplicateDoorLinkStableId,
                        doorLink.DoorLinkStableId.ToString(),
                        "Door-link identities must be unique.");
                }
                else
                {
                    doorLinkMap.Add(doorLink.DoorLinkStableId, doorLink);
                }
            }

            var connectionMap =
                new Dictionary<StableId, RoomConnectionDefinition>();
            var exitMap = new Dictionary<StableId, RoomExitDefinition>();
            var exitOrdersByRoom = new Dictionary<StableId, HashSet<int>>();
            var doorUseCount = new Dictionary<StableId, int>();
            var validTargetsBySource =
                new Dictionary<StableId, List<StableId>>();
            foreach (StableId roomId in roomMap.Keys)
            {
                exitOrdersByRoom.Add(roomId, new HashSet<int>());
                validTargetsBySource.Add(roomId, new List<StableId>());
            }

            for (int connectionIndex = 0;
                connectionIndex < connectionList.Count;
                connectionIndex++)
            {
                RoomConnectionDefinition connection =
                    connectionList[connectionIndex];
                if (connection == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.NullConnection,
                        "connections[" + connectionIndex.ToString(
                            CultureInfo.InvariantCulture) + "]",
                        "Connections cannot contain null values.");
                    continue;
                }

                if (connection.ConnectionStableId == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.MissingConnectionStableId,
                        "connections[" + connectionIndex.ToString(
                            CultureInfo.InvariantCulture) + "]",
                        "Every connection requires a stable identity.");
                }
                else if (connectionMap.ContainsKey(connection.ConnectionStableId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.DuplicateConnectionStableId,
                        connection.ConnectionStableId.ToString(),
                        "Connection identities must be unique.");
                }
                else
                {
                    connectionMap.Add(
                        connection.ConnectionStableId,
                        connection);
                }

                if (!Enum.IsDefined(
                    typeof(RoomConnectionDirectionality),
                    connection.Directionality))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.InvalidConnectionDirectionality,
                        connection.ConnectionStableId == null
                            ? "unknown-connection"
                            : connection.ConnectionStableId.ToString(),
                        "Connection directionality is not supported.");
                }

                int expectedExitCount =
                    connection.Directionality
                        == RoomConnectionDirectionality.OneWay
                    ? 1
                    : connection.Directionality
                        == RoomConnectionDirectionality.Bidirectional
                        ? 2
                        : -1;
                int actualExitCount =
                    connection.Exits == null ? -1 : connection.Exits.Count;
                if (expectedExitCount < 0 || actualExitCount != expectedExitCount)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.InvalidConnectionExitCount,
                        connection.ConnectionStableId == null
                            ? "unknown-connection"
                            : connection.ConnectionStableId.ToString(),
                        "One-way connections require one exit and bidirectional "
                            + "connections require exactly two exits.");
                }

                if (connection.DoorLinkStableId != null)
                {
                    if (!doorLinkMap.ContainsKey(connection.DoorLinkStableId))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.DanglingDoorLink,
                            connection.DoorLinkStableId.ToString(),
                            "Connection references an undefined door link.");
                    }
                    else
                    {
                        int useCount;
                        doorUseCount.TryGetValue(
                            connection.DoorLinkStableId,
                            out useCount);
                        doorUseCount[connection.DoorLinkStableId] = useCount + 1;
                    }
                }

                if (connection.Exits == null)
                {
                    continue;
                }

                var connectionTargetRooms = new List<StableId>();
                for (int exitIndex = 0;
                    exitIndex < connection.Exits.Count;
                    exitIndex++)
                {
                    RoomExitDefinition exit = connection.Exits[exitIndex];
                    if (exit == null)
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.NullExit,
                            (connection.ConnectionStableId == null
                                ? "unknown-connection"
                                : connection.ConnectionStableId.ToString())
                                + ":exit-"
                                + exitIndex.ToString(CultureInfo.InvariantCulture),
                            "Connections cannot contain null exits.");
                        connectionTargetRooms.Add(null);
                        continue;
                    }

                    if (exit.ExitStableId == null)
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.MissingExitStableId,
                            connection.ConnectionStableId == null
                                ? "unknown-connection"
                                : connection.ConnectionStableId.ToString(),
                            "Every exit requires a stable identity.");
                    }
                    else if (exitMap.ContainsKey(exit.ExitStableId))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.DuplicateExitStableId,
                            exit.ExitStableId.ToString(),
                            "Exit identities must be globally unique.");
                    }
                    else
                    {
                        exitMap.Add(exit.ExitStableId, exit);
                    }

                    bool sourceExists = false;
                    if (exit.SourceRoomStableId == null)
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.MissingExitSourceRoomStableId,
                            exit.ExitStableId == null
                                ? "unknown-exit"
                                : exit.ExitStableId.ToString(),
                            "Every exit requires a source room identity.");
                    }
                    else if (!roomMap.ContainsKey(exit.SourceRoomStableId))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.MissingExitSourceRoomReference,
                            exit.SourceRoomStableId.ToString(),
                            "Exit source must reference a defined room.");
                    }
                    else
                    {
                        sourceExists = true;
                        if (!exitOrdersByRoom[exit.SourceRoomStableId].Add(exit.Order))
                        {
                            AddIssue(
                                issues,
                                RoomGraphValidationCode.DuplicateExitOrder,
                                exit.SourceRoomStableId + ":"
                                    + exit.Order.ToString(
                                        CultureInfo.InvariantCulture),
                                "Exit order values must be unique within each room.");
                        }
                    }

                    RoomEntryDefinition targetEntry = null;
                    if (exit.TargetEntryStableId == null)
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.MissingExitTargetEntryStableId,
                            exit.ExitStableId == null
                                ? "unknown-exit"
                                : exit.ExitStableId.ToString(),
                            "Every exit requires a target entry identity.");
                    }
                    else if (!entryMap.TryGetValue(
                        exit.TargetEntryStableId,
                        out targetEntry))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.MissingExitTargetEntryReference,
                            exit.TargetEntryStableId.ToString(),
                            "Exit target must reference a defined entry.");
                    }

                    if (!Enum.IsDefined(typeof(RoomExitType), exit.ExitType))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.InvalidExitType,
                            exit.ExitStableId == null
                                ? "unknown-exit"
                                : exit.ExitStableId.ToString(),
                            "Exit type is not supported.");
                    }

                    if (!exit.InitiallyLocked
                        && exit.UnlockRequiredCompletedRoomStableId != null)
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.InvalidUnlockRule,
                            exit.ExitStableId == null
                                ? "unknown-exit"
                                : exit.ExitStableId.ToString(),
                            "An already-available exit cannot require a completed room.");
                    }

                    if (exit.UnlockRequiredCompletedRoomStableId != null
                        && !roomMap.ContainsKey(
                            exit.UnlockRequiredCompletedRoomStableId))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.MissingUnlockRoomReference,
                            exit.UnlockRequiredCompletedRoomStableId.ToString(),
                            "Exit unlock rule must reference a defined room.");
                    }

                    StableId targetRoomId =
                        targetEntry == null ? null : targetEntry.RoomStableId;
                    connectionTargetRooms.Add(targetRoomId);
                    if (sourceExists && targetRoomId != null)
                    {
                        if (exit.SourceRoomStableId == targetRoomId)
                        {
                            AddIssue(
                                issues,
                                RoomGraphValidationCode.SelfLink,
                                exit.ExitStableId == null
                                    ? "unknown-exit"
                                    : exit.ExitStableId.ToString(),
                                "Room exits cannot target their source room.");
                        }
                        else
                        {
                            validTargetsBySource[exit.SourceRoomStableId].Add(
                                targetRoomId);
                        }
                    }
                }

                if (connection.Directionality
                    == RoomConnectionDirectionality.Bidirectional
                    && connection.Exits != null
                    && connection.Exits.Count == 2
                    && connection.Exits[0] != null
                    && connection.Exits[1] != null
                    && connectionTargetRooms.Count == 2)
                {
                    RoomExitDefinition first = connection.Exits[0];
                    RoomExitDefinition second = connection.Exits[1];
                    StableId firstTarget = connectionTargetRooms[0];
                    StableId secondTarget = connectionTargetRooms[1];
                    if (firstTarget == null
                        || secondTarget == null
                        || first.SourceRoomStableId == null
                        || second.SourceRoomStableId == null
                        || first.SourceRoomStableId != secondTarget
                        || second.SourceRoomStableId != firstTarget)
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.MismatchedReverseLink,
                            connection.ConnectionStableId == null
                                ? "unknown-connection"
                                : connection.ConnectionStableId.ToString(),
                            "Bidirectional exits must point to each other's source rooms.");
                    }
                }
            }

            foreach (KeyValuePair<StableId, int> pair in doorUseCount)
            {
                if (pair.Value > 1)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.DoorLinkUsedByMultipleConnections,
                        pair.Key.ToString(),
                        "A door link may belong to only one connection.");
                }
            }

            foreach (StableId doorLinkId in doorLinkMap.Keys)
            {
                if (!doorUseCount.ContainsKey(doorLinkId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.UnusedDoorLink,
                        doorLinkId.ToString(),
                        "Defined door links must be referenced by one connection.");
                }
            }

            if (startRoomStableId != null
                && roomMap.ContainsKey(startRoomStableId))
            {
                HashSet<StableId> reachable = ComputeReachable(
                    startRoomStableId,
                    validTargetsBySource);
                foreach (RoomDefinition room in roomMap.Values)
                {
                    if (room.IsRequired && !reachable.Contains(room.RoomStableId))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCode.UnreachableRequiredRoom,
                            room.RoomStableId.ToString(),
                            "Every required room must be reachable from the start.");
                    }
                }

                if (terminalRoomStableId != null
                    && roomMap.ContainsKey(terminalRoomStableId)
                    && !reachable.Contains(terminalRoomStableId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCode.UnreachableTerminalRoom,
                        terminalRoomStableId.ToString(),
                        "Terminal room must be reachable from the start.");
                }
            }

            if (issues.Count > 0)
            {
                issues.Sort(CompareIssues);
                return new RoomGraphValidationResult(null, issues);
            }

            var definition = new RoomGraphDefinition(
                layoutStableId,
                startRoomStableId,
                terminalRoomStableId,
                roomList,
                entryList,
                connectionList,
                doorLinkList);
            return new RoomGraphValidationResult(
                definition,
                new RoomGraphValidationIssue[0]);
        }

        public RoomDefinition GetRoom(StableId roomStableId)
        {
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }

            RoomDefinition room;
            if (!roomsById.TryGetValue(roomStableId, out room))
            {
                throw new KeyNotFoundException(
                    "Unknown room identity: " + roomStableId);
            }

            return room;
        }

        public bool TryGetRoom(
            StableId roomStableId,
            out RoomDefinition room)
        {
            room = null;
            return roomStableId != null
                && roomsById.TryGetValue(roomStableId, out room);
        }

        public bool TryGetEntry(
            StableId entryStableId,
            out RoomEntryDefinition entry)
        {
            entry = null;
            return entryStableId != null
                && entriesById.TryGetValue(entryStableId, out entry);
        }

        public bool TryGetExit(
            StableId exitStableId,
            out RoomExitDefinition exit)
        {
            exit = null;
            return exitStableId != null
                && exitsById.TryGetValue(exitStableId, out exit);
        }

        public IReadOnlyList<RoomExitDefinition> GetExitsFromRoom(
            StableId roomStableId)
        {
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }

            ReadOnlyCollection<RoomExitDefinition> result;
            if (!exitsBySourceRoom.TryGetValue(roomStableId, out result))
            {
                throw new KeyNotFoundException(
                    "Unknown room identity: " + roomStableId);
            }

            return result;
        }

        public RoomDefinition GetTargetRoom(RoomExitDefinition exit)
        {
            if (exit == null)
            {
                throw new ArgumentNullException(nameof(exit));
            }

            RoomEntryDefinition entry;
            if (!entriesById.TryGetValue(exit.TargetEntryStableId, out entry))
            {
                throw new ArgumentException(
                    "Exit is not part of this validated graph.",
                    nameof(exit));
            }

            return roomsById[entry.RoomStableId];
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormat.AppendToken(builder, "schema", SchemaId);
            RoomGraphFormat.AppendToken(
                builder,
                "layout_stable_id",
                LayoutStableId.ToString());
            RoomGraphFormat.AppendToken(
                builder,
                "start_room_stable_id",
                StartRoomStableId.ToString());
            RoomGraphFormat.AppendToken(
                builder,
                "terminal_room_stable_id",
                TerminalRoomStableId.ToString());
            AppendItems(builder, "room", rooms);
            AppendItems(builder, "entry", entries);
            AppendItems(builder, "connection", connections);
            AppendItems(builder, "door_link", doorLinks);
            return builder.ToString();
        }

        internal static int CompareRooms(
            RoomDefinition left,
            RoomDefinition right)
        {
            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }

            return left.RoomStableId.CompareTo(right.RoomStableId);
        }

        internal static int CompareEntries(
            RoomEntryDefinition left,
            RoomEntryDefinition right)
        {
            int room = left.RoomStableId.CompareTo(right.RoomStableId);
            if (room != 0)
            {
                return room;
            }

            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }

            return left.EntryStableId.CompareTo(right.EntryStableId);
        }

        internal static int CompareConnections(
            RoomConnectionDefinition left,
            RoomConnectionDefinition right)
        {
            return left.ConnectionStableId.CompareTo(right.ConnectionStableId);
        }

        internal static int CompareDoorLinks(
            RoomDoorLinkDefinition left,
            RoomDoorLinkDefinition right)
        {
            return left.DoorLinkStableId.CompareTo(right.DoorLinkStableId);
        }

        internal static int CompareExits(
            RoomExitDefinition left,
            RoomExitDefinition right)
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

            int source = CompareNullableStableIds(
                left.SourceRoomStableId,
                right.SourceRoomStableId);
            if (source != 0)
            {
                return source;
            }

            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }

            return CompareNullableStableIds(
                left.ExitStableId,
                right.ExitStableId);
        }

        private static int CompareNullableStableIds(
            StableId left,
            StableId right)
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

            return left.CompareTo(right);
        }

        private static int CompareIssues(
            RoomGraphValidationIssue left,
            RoomGraphValidationIssue right)
        {
            int code = ((int)left.Code).CompareTo((int)right.Code);
            if (code != 0)
            {
                return code;
            }

            return string.CompareOrdinal(left.Subject, right.Subject);
        }

        private static List<T> CopyOrIssue<T>(
            IEnumerable<T> source,
            List<RoomGraphValidationIssue> issues,
            RoomGraphValidationCode code,
            string subject)
        {
            if (source == null)
            {
                AddIssue(
                    issues,
                    code,
                    subject,
                    "Definition collection is required.");
                return null;
            }

            return new List<T>(source);
        }

        private static void AddIssue(
            List<RoomGraphValidationIssue> issues,
            RoomGraphValidationCode code,
            string subject,
            string message)
        {
            issues.Add(new RoomGraphValidationIssue(
                code,
                subject,
                message));
        }

        private static HashSet<StableId> ComputeReachable(
            StableId startRoomStableId,
            Dictionary<StableId, List<StableId>> targetsBySource)
        {
            var reachable = new HashSet<StableId>();
            var queue = new Queue<StableId>();
            reachable.Add(startRoomStableId);
            queue.Enqueue(startRoomStableId);
            while (queue.Count > 0)
            {
                StableId source = queue.Dequeue();
                List<StableId> targets;
                if (!targetsBySource.TryGetValue(source, out targets))
                {
                    continue;
                }

                for (int index = 0; index < targets.Count; index++)
                {
                    StableId target = targets[index];
                    if (target != null && reachable.Add(target))
                    {
                        queue.Enqueue(target);
                    }
                }
            }

            return reachable;
        }

        private static void AppendItems<T>(
            StringBuilder builder,
            string prefix,
            IReadOnlyList<T> items)
        {
            RoomGraphFormat.AppendToken(
                builder,
                prefix + "_count",
                items.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < items.Count; index++)
            {
                object value = items[index];
                RoomGraphFormat.AppendToken(
                    builder,
                    prefix + "_"
                        + index.ToString("D4", CultureInfo.InvariantCulture),
                    value == null ? string.Empty : value.ToString());
            }
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    public static class RoomGraphFormat
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static void AppendToken(
            StringBuilder builder,
            string key,
            string value)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            string canonicalValue = value ?? string.Empty;
            builder.Append(key.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(key)
                .Append('=')
                .Append(canonicalValue.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(canonicalValue)
                .Append('\n');
        }

        public static string ComputeSha256(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            byte[] input = Encoding.UTF8.GetBytes(canonicalText);
            byte[] digest;
            using (SHA256 algorithm = SHA256.Create())
            {
                digest = algorithm.ComputeHash(input);
            }

            var builder = new StringBuilder("sha256:");
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(
                    digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static int DeterministicHash(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Missions.Rooms
{
    public enum RoomInitialAvailabilityV1
    {
        Locked = 1,
        Available = 2,
    }

    public enum RoomConnectionDirectionalityV1
    {
        OneWay = 1,
        Bidirectional = 2,
    }

    public enum RoomExitTypeV1
    {
        Progression = 1,
        Return = 2,
        Optional = 3,
        Secret = 4,
    }

    public enum RoomGraphValidationCodeV1
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

    public sealed class RoomGraphValidationIssueV1
    {
        public RoomGraphValidationIssueV1(
            RoomGraphValidationCodeV1 code,
            string subject,
            string message)
        {
            Code = code;
            Subject = subject ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public RoomGraphValidationCodeV1 Code { get; }

        public string Subject { get; }

        public string Message { get; }
    }

    public sealed class RoomGraphValidationResultV1
    {
        private readonly ReadOnlyCollection<RoomGraphValidationIssueV1> issues;

        internal RoomGraphValidationResultV1(
            RoomGraphDefinitionV1 definition,
            IEnumerable<RoomGraphValidationIssueV1> issues)
        {
            Definition = definition;
            this.issues = new ReadOnlyCollection<RoomGraphValidationIssueV1>(
                new List<RoomGraphValidationIssueV1>(
                    issues ?? throw new ArgumentNullException(nameof(issues))));
        }

        public RoomGraphDefinitionV1 Definition { get; }

        public IReadOnlyList<RoomGraphValidationIssueV1> Issues
        {
            get { return issues; }
        }

        public bool IsValid
        {
            get { return Definition != null && issues.Count == 0; }
        }

        public bool HasCode(RoomGraphValidationCodeV1 code)
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

    public sealed class RoomDefinitionV1
    {
        public RoomDefinitionV1(
            StableId roomStableId,
            int order,
            RoomInitialAvailabilityV1 initialAvailability,
            bool isRequired)
        {
            RoomStableId = roomStableId;
            Order = order;
            InitialAvailability = initialAvailability;
            IsRequired = isRequired;
        }

        public StableId RoomStableId { get; }

        public int Order { get; }

        public RoomInitialAvailabilityV1 InitialAvailability { get; }

        public bool IsRequired { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormatV1.AppendToken(
                builder,
                "room_stable_id",
                RoomStableId == null ? string.Empty : RoomStableId.ToString());
            RoomGraphFormatV1.AppendToken(
                builder,
                "order",
                Order.ToString(CultureInfo.InvariantCulture));
            RoomGraphFormatV1.AppendToken(
                builder,
                "initial_availability",
                ((int)InitialAvailability).ToString(CultureInfo.InvariantCulture));
            RoomGraphFormatV1.AppendToken(
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

    public sealed class RoomEntryDefinitionV1
    {
        public RoomEntryDefinitionV1(
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
            RoomGraphFormatV1.AppendToken(
                builder,
                "entry_stable_id",
                EntryStableId == null ? string.Empty : EntryStableId.ToString());
            RoomGraphFormatV1.AppendToken(
                builder,
                "room_stable_id",
                RoomStableId == null ? string.Empty : RoomStableId.ToString());
            RoomGraphFormatV1.AppendToken(
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

    public sealed class RoomExitDefinitionV1
    {
        public RoomExitDefinitionV1(
            StableId exitStableId,
            StableId sourceRoomStableId,
            StableId targetEntryStableId,
            int order,
            RoomExitTypeV1 exitType,
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

        public RoomExitTypeV1 ExitType { get; }

        public bool InitiallyLocked { get; }

        public StableId UnlockRequiredCompletedRoomStableId { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormatV1.AppendToken(
                builder,
                "exit_stable_id",
                ExitStableId == null ? string.Empty : ExitStableId.ToString());
            RoomGraphFormatV1.AppendToken(
                builder,
                "source_room_stable_id",
                SourceRoomStableId == null
                    ? string.Empty
                    : SourceRoomStableId.ToString());
            RoomGraphFormatV1.AppendToken(
                builder,
                "target_entry_stable_id",
                TargetEntryStableId == null
                    ? string.Empty
                    : TargetEntryStableId.ToString());
            RoomGraphFormatV1.AppendToken(
                builder,
                "order",
                Order.ToString(CultureInfo.InvariantCulture));
            RoomGraphFormatV1.AppendToken(
                builder,
                "exit_type",
                ((int)ExitType).ToString(CultureInfo.InvariantCulture));
            RoomGraphFormatV1.AppendToken(
                builder,
                "initially_locked",
                InitiallyLocked ? "1" : "0");
            RoomGraphFormatV1.AppendToken(
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

    public sealed class RoomDoorLinkDefinitionV1
    {
        public RoomDoorLinkDefinitionV1(StableId doorLinkStableId)
        {
            DoorLinkStableId = doorLinkStableId;
        }

        public StableId DoorLinkStableId { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormatV1.AppendToken(
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

    public sealed class RoomConnectionDefinitionV1
    {
        private readonly ReadOnlyCollection<RoomExitDefinitionV1> exits;

        public RoomConnectionDefinitionV1(
            StableId connectionStableId,
            RoomConnectionDirectionalityV1 directionality,
            StableId doorLinkStableId,
            IEnumerable<RoomExitDefinitionV1> exits)
        {
            ConnectionStableId = connectionStableId;
            Directionality = directionality;
            DoorLinkStableId = doorLinkStableId;
            this.exits = exits == null
                ? null
                : new ReadOnlyCollection<RoomExitDefinitionV1>(
                    new List<RoomExitDefinitionV1>(exits));
        }

        public StableId ConnectionStableId { get; }

        public RoomConnectionDirectionalityV1 Directionality { get; }

        public StableId DoorLinkStableId { get; }

        public IReadOnlyList<RoomExitDefinitionV1> Exits
        {
            get { return exits; }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormatV1.AppendToken(
                builder,
                "connection_stable_id",
                ConnectionStableId == null
                    ? string.Empty
                    : ConnectionStableId.ToString());
            RoomGraphFormatV1.AppendToken(
                builder,
                "directionality",
                ((int)Directionality).ToString(CultureInfo.InvariantCulture));
            RoomGraphFormatV1.AppendToken(
                builder,
                "door_link_stable_id",
                DoorLinkStableId == null
                    ? string.Empty
                    : DoorLinkStableId.ToString());
            RoomGraphFormatV1.AppendToken(
                builder,
                "exit_count",
                exits == null
                    ? "-1"
                    : exits.Count.ToString(CultureInfo.InvariantCulture));
            if (exits != null)
            {
                var ordered = new List<RoomExitDefinitionV1>(exits);
                ordered.Sort(RoomGraphDefinitionV1.CompareExits);
                for (int index = 0; index < ordered.Count; index++)
                {
                    RoomGraphFormatV1.AppendToken(
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
    public sealed class RoomGraphDefinitionV1
    {
        private const string SchemaId = "room-graph-definition-v1";

        private readonly ReadOnlyCollection<RoomDefinitionV1> rooms;
        private readonly ReadOnlyCollection<RoomEntryDefinitionV1> entries;
        private readonly ReadOnlyCollection<RoomConnectionDefinitionV1> connections;
        private readonly ReadOnlyCollection<RoomDoorLinkDefinitionV1> doorLinks;
        private readonly Dictionary<StableId, RoomDefinitionV1> roomsById;
        private readonly Dictionary<StableId, RoomEntryDefinitionV1> entriesById;
        private readonly Dictionary<StableId, RoomExitDefinitionV1> exitsById;
        private readonly Dictionary<StableId, ReadOnlyCollection<RoomExitDefinitionV1>>
            exitsBySourceRoom;

        private RoomGraphDefinitionV1(
            StableId layoutStableId,
            StableId startRoomStableId,
            StableId terminalRoomStableId,
            IEnumerable<RoomDefinitionV1> rooms,
            IEnumerable<RoomEntryDefinitionV1> entries,
            IEnumerable<RoomConnectionDefinitionV1> connections,
            IEnumerable<RoomDoorLinkDefinitionV1> doorLinks)
        {
            LayoutStableId = layoutStableId;
            StartRoomStableId = startRoomStableId;
            TerminalRoomStableId = terminalRoomStableId;

            var orderedRooms = new List<RoomDefinitionV1>(rooms);
            orderedRooms.Sort(CompareRooms);
            this.rooms = new ReadOnlyCollection<RoomDefinitionV1>(orderedRooms);

            var orderedEntries = new List<RoomEntryDefinitionV1>(entries);
            orderedEntries.Sort(CompareEntries);
            this.entries = new ReadOnlyCollection<RoomEntryDefinitionV1>(orderedEntries);

            var orderedConnections = new List<RoomConnectionDefinitionV1>(connections);
            orderedConnections.Sort(CompareConnections);
            this.connections =
                new ReadOnlyCollection<RoomConnectionDefinitionV1>(orderedConnections);

            var orderedDoorLinks = new List<RoomDoorLinkDefinitionV1>(doorLinks);
            orderedDoorLinks.Sort(CompareDoorLinks);
            this.doorLinks =
                new ReadOnlyCollection<RoomDoorLinkDefinitionV1>(orderedDoorLinks);

            roomsById = new Dictionary<StableId, RoomDefinitionV1>();
            entriesById = new Dictionary<StableId, RoomEntryDefinitionV1>();
            exitsById = new Dictionary<StableId, RoomExitDefinitionV1>();
            var mutableExitsByRoom =
                new Dictionary<StableId, List<RoomExitDefinitionV1>>();

            for (int index = 0; index < orderedRooms.Count; index++)
            {
                RoomDefinitionV1 room = orderedRooms[index];
                roomsById.Add(room.RoomStableId, room);
                mutableExitsByRoom.Add(
                    room.RoomStableId,
                    new List<RoomExitDefinitionV1>());
            }

            for (int index = 0; index < orderedEntries.Count; index++)
            {
                RoomEntryDefinitionV1 entry = orderedEntries[index];
                entriesById.Add(entry.EntryStableId, entry);
            }

            for (int connectionIndex = 0;
                connectionIndex < orderedConnections.Count;
                connectionIndex++)
            {
                RoomConnectionDefinitionV1 connection =
                    orderedConnections[connectionIndex];
                for (int exitIndex = 0;
                    exitIndex < connection.Exits.Count;
                    exitIndex++)
                {
                    RoomExitDefinitionV1 exit = connection.Exits[exitIndex];
                    exitsById.Add(exit.ExitStableId, exit);
                    mutableExitsByRoom[exit.SourceRoomStableId].Add(exit);
                }
            }

            exitsBySourceRoom =
                new Dictionary<StableId, ReadOnlyCollection<RoomExitDefinitionV1>>();
            foreach (KeyValuePair<StableId, List<RoomExitDefinitionV1>> pair
                in mutableExitsByRoom)
            {
                pair.Value.Sort(CompareExits);
                exitsBySourceRoom.Add(
                    pair.Key,
                    new ReadOnlyCollection<RoomExitDefinitionV1>(pair.Value));
            }

            Fingerprint = RoomGraphFormatV1.ComputeSha256(ToCanonicalString());
        }

        public StableId LayoutStableId { get; }

        public StableId StartRoomStableId { get; }

        public StableId TerminalRoomStableId { get; }

        public IReadOnlyList<RoomDefinitionV1> Rooms
        {
            get { return rooms; }
        }

        public IReadOnlyList<RoomEntryDefinitionV1> Entries
        {
            get { return entries; }
        }

        public IReadOnlyList<RoomConnectionDefinitionV1> Connections
        {
            get { return connections; }
        }

        public IReadOnlyList<RoomDoorLinkDefinitionV1> DoorLinks
        {
            get { return doorLinks; }
        }

        public string Fingerprint { get; }

        public static RoomGraphValidationResultV1 ValidateAndCreate(
            StableId layoutStableId,
            StableId startRoomStableId,
            StableId terminalRoomStableId,
            IEnumerable<RoomDefinitionV1> rooms,
            IEnumerable<RoomEntryDefinitionV1> entries,
            IEnumerable<RoomConnectionDefinitionV1> connections,
            IEnumerable<RoomDoorLinkDefinitionV1> doorLinks)
        {
            var issues = new List<RoomGraphValidationIssueV1>();
            if (layoutStableId == null)
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCodeV1.MissingLayoutStableId,
                    "layout",
                    "Mission layout identity is required.");
            }

            if (startRoomStableId == null)
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCodeV1.MissingStartRoomStableId,
                    "start-room",
                    "Start room identity is required.");
            }

            if (terminalRoomStableId == null)
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCodeV1.MissingTerminalRoomStableId,
                    "terminal-room",
                    "Terminal room identity is required.");
            }

            List<RoomDefinitionV1> roomList = CopyOrIssue(
                rooms,
                issues,
                RoomGraphValidationCodeV1.MissingRooms,
                "rooms");
            List<RoomEntryDefinitionV1> entryList = CopyOrIssue(
                entries,
                issues,
                RoomGraphValidationCodeV1.MissingEntries,
                "entries");
            List<RoomConnectionDefinitionV1> connectionList = CopyOrIssue(
                connections,
                issues,
                RoomGraphValidationCodeV1.MissingConnections,
                "connections");
            List<RoomDoorLinkDefinitionV1> doorLinkList = CopyOrIssue(
                doorLinks,
                issues,
                RoomGraphValidationCodeV1.MissingDoorLinks,
                "door-links");

            if (roomList == null
                || entryList == null
                || connectionList == null
                || doorLinkList == null)
            {
                return new RoomGraphValidationResultV1(null, issues);
            }

            var roomMap = new Dictionary<StableId, RoomDefinitionV1>();
            var roomOrders = new Dictionary<int, StableId>();
            for (int index = 0; index < roomList.Count; index++)
            {
                RoomDefinitionV1 room = roomList[index];
                if (room == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.NullRoom,
                        "rooms[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Room definitions cannot contain null values.");
                    continue;
                }

                if (room.RoomStableId == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.MissingRoomStableId,
                        "rooms[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every room requires a stable identity.");
                }
                else if (roomMap.ContainsKey(room.RoomStableId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.DuplicateRoomStableId,
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
                        RoomGraphValidationCodeV1.DuplicateRoomOrder,
                        room.Order.ToString(CultureInfo.InvariantCulture),
                        "Room order values must be unique.");
                }
                else
                {
                    roomOrders.Add(room.Order, room.RoomStableId);
                }

                if (!Enum.IsDefined(
                    typeof(RoomInitialAvailabilityV1),
                    room.InitialAvailability))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.InvalidRoomAvailability,
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
                    RoomGraphValidationCodeV1.InvalidStartRoom,
                    startRoomStableId.ToString(),
                    "Start room must reference a defined room.");
            }

            if (terminalRoomStableId != null
                && !roomMap.ContainsKey(terminalRoomStableId))
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCodeV1.InvalidTerminalRoom,
                    terminalRoomStableId.ToString(),
                    "Terminal room must reference a defined room.");
            }

            if (startRoomStableId != null
                && terminalRoomStableId != null
                && startRoomStableId == terminalRoomStableId)
            {
                AddIssue(
                    issues,
                    RoomGraphValidationCodeV1.StartEqualsTerminal,
                    startRoomStableId.ToString(),
                    "Start and terminal rooms must be distinct.");
            }

            var entryMap = new Dictionary<StableId, RoomEntryDefinitionV1>();
            var entryOrdersByRoom = new Dictionary<StableId, HashSet<int>>();
            var entryCountByRoom = new Dictionary<StableId, int>();
            foreach (StableId roomId in roomMap.Keys)
            {
                entryOrdersByRoom.Add(roomId, new HashSet<int>());
                entryCountByRoom.Add(roomId, 0);
            }

            for (int index = 0; index < entryList.Count; index++)
            {
                RoomEntryDefinitionV1 entry = entryList[index];
                if (entry == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.NullEntry,
                        "entries[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Room entries cannot contain null values.");
                    continue;
                }

                if (entry.EntryStableId == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.MissingEntryStableId,
                        "entries[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every entry requires a stable identity.");
                }
                else if (entryMap.ContainsKey(entry.EntryStableId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.DuplicateEntryStableId,
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
                        RoomGraphValidationCodeV1.MissingEntryRoomStableId,
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
                        RoomGraphValidationCodeV1.MissingEntryRoomReference,
                        entry.RoomStableId.ToString(),
                        "Entry owner must reference a defined room.");
                    continue;
                }

                entryCountByRoom[entry.RoomStableId]++;
                if (!entryOrdersByRoom[entry.RoomStableId].Add(entry.Order))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.DuplicateEntryOrder,
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
                        RoomGraphValidationCodeV1.RoomHasNoEntry,
                        pair.Key.ToString(),
                        "Every room requires at least one stable entry.");
                }
            }

            var doorLinkMap =
                new Dictionary<StableId, RoomDoorLinkDefinitionV1>();
            for (int index = 0; index < doorLinkList.Count; index++)
            {
                RoomDoorLinkDefinitionV1 doorLink = doorLinkList[index];
                if (doorLink == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.NullDoorLink,
                        "door-links[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Door links cannot contain null values.");
                    continue;
                }

                if (doorLink.DoorLinkStableId == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.MissingDoorLinkStableId,
                        "door-links[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        "Every door link requires a stable identity.");
                }
                else if (doorLinkMap.ContainsKey(doorLink.DoorLinkStableId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.DuplicateDoorLinkStableId,
                        doorLink.DoorLinkStableId.ToString(),
                        "Door-link identities must be unique.");
                }
                else
                {
                    doorLinkMap.Add(doorLink.DoorLinkStableId, doorLink);
                }
            }

            var connectionMap =
                new Dictionary<StableId, RoomConnectionDefinitionV1>();
            var exitMap = new Dictionary<StableId, RoomExitDefinitionV1>();
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
                RoomConnectionDefinitionV1 connection =
                    connectionList[connectionIndex];
                if (connection == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.NullConnection,
                        "connections[" + connectionIndex.ToString(
                            CultureInfo.InvariantCulture) + "]",
                        "Connections cannot contain null values.");
                    continue;
                }

                if (connection.ConnectionStableId == null)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.MissingConnectionStableId,
                        "connections[" + connectionIndex.ToString(
                            CultureInfo.InvariantCulture) + "]",
                        "Every connection requires a stable identity.");
                }
                else if (connectionMap.ContainsKey(connection.ConnectionStableId))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.DuplicateConnectionStableId,
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
                    typeof(RoomConnectionDirectionalityV1),
                    connection.Directionality))
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.InvalidConnectionDirectionality,
                        connection.ConnectionStableId == null
                            ? "unknown-connection"
                            : connection.ConnectionStableId.ToString(),
                        "Connection directionality is not supported.");
                }

                int expectedExitCount =
                    connection.Directionality
                        == RoomConnectionDirectionalityV1.OneWay
                    ? 1
                    : connection.Directionality
                        == RoomConnectionDirectionalityV1.Bidirectional
                        ? 2
                        : -1;
                int actualExitCount =
                    connection.Exits == null ? -1 : connection.Exits.Count;
                if (expectedExitCount < 0 || actualExitCount != expectedExitCount)
                {
                    AddIssue(
                        issues,
                        RoomGraphValidationCodeV1.InvalidConnectionExitCount,
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
                            RoomGraphValidationCodeV1.DanglingDoorLink,
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
                    RoomExitDefinitionV1 exit = connection.Exits[exitIndex];
                    if (exit == null)
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCodeV1.NullExit,
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
                            RoomGraphValidationCodeV1.MissingExitStableId,
                            connection.ConnectionStableId == null
                                ? "unknown-connection"
                                : connection.ConnectionStableId.ToString(),
                            "Every exit requires a stable identity.");
                    }
                    else if (exitMap.ContainsKey(exit.ExitStableId))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCodeV1.DuplicateExitStableId,
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
                            RoomGraphValidationCodeV1.MissingExitSourceRoomStableId,
                            exit.ExitStableId == null
                                ? "unknown-exit"
                                : exit.ExitStableId.ToString(),
                            "Every exit requires a source room identity.");
                    }
                    else if (!roomMap.ContainsKey(exit.SourceRoomStableId))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCodeV1.MissingExitSourceRoomReference,
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
                                RoomGraphValidationCodeV1.DuplicateExitOrder,
                                exit.SourceRoomStableId + ":"
                                    + exit.Order.ToString(
                                        CultureInfo.InvariantCulture),
                                "Exit order values must be unique within each room.");
                        }
                    }

                    RoomEntryDefinitionV1 targetEntry = null;
                    if (exit.TargetEntryStableId == null)
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCodeV1.MissingExitTargetEntryStableId,
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
                            RoomGraphValidationCodeV1.MissingExitTargetEntryReference,
                            exit.TargetEntryStableId.ToString(),
                            "Exit target must reference a defined entry.");
                    }

                    if (!Enum.IsDefined(typeof(RoomExitTypeV1), exit.ExitType))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCodeV1.InvalidExitType,
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
                            RoomGraphValidationCodeV1.InvalidUnlockRule,
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
                            RoomGraphValidationCodeV1.MissingUnlockRoomReference,
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
                                RoomGraphValidationCodeV1.SelfLink,
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
                    == RoomConnectionDirectionalityV1.Bidirectional
                    && connection.Exits != null
                    && connection.Exits.Count == 2
                    && connection.Exits[0] != null
                    && connection.Exits[1] != null
                    && connectionTargetRooms.Count == 2)
                {
                    RoomExitDefinitionV1 first = connection.Exits[0];
                    RoomExitDefinitionV1 second = connection.Exits[1];
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
                            RoomGraphValidationCodeV1.MismatchedReverseLink,
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
                        RoomGraphValidationCodeV1.DoorLinkUsedByMultipleConnections,
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
                        RoomGraphValidationCodeV1.UnusedDoorLink,
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
                foreach (RoomDefinitionV1 room in roomMap.Values)
                {
                    if (room.IsRequired && !reachable.Contains(room.RoomStableId))
                    {
                        AddIssue(
                            issues,
                            RoomGraphValidationCodeV1.UnreachableRequiredRoom,
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
                        RoomGraphValidationCodeV1.UnreachableTerminalRoom,
                        terminalRoomStableId.ToString(),
                        "Terminal room must be reachable from the start.");
                }
            }

            if (issues.Count > 0)
            {
                issues.Sort(CompareIssues);
                return new RoomGraphValidationResultV1(null, issues);
            }

            var definition = new RoomGraphDefinitionV1(
                layoutStableId,
                startRoomStableId,
                terminalRoomStableId,
                roomList,
                entryList,
                connectionList,
                doorLinkList);
            return new RoomGraphValidationResultV1(
                definition,
                new RoomGraphValidationIssueV1[0]);
        }

        public RoomDefinitionV1 GetRoom(StableId roomStableId)
        {
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }

            RoomDefinitionV1 room;
            if (!roomsById.TryGetValue(roomStableId, out room))
            {
                throw new KeyNotFoundException(
                    "Unknown room identity: " + roomStableId);
            }

            return room;
        }

        public bool TryGetRoom(
            StableId roomStableId,
            out RoomDefinitionV1 room)
        {
            room = null;
            return roomStableId != null
                && roomsById.TryGetValue(roomStableId, out room);
        }

        public bool TryGetEntry(
            StableId entryStableId,
            out RoomEntryDefinitionV1 entry)
        {
            entry = null;
            return entryStableId != null
                && entriesById.TryGetValue(entryStableId, out entry);
        }

        public bool TryGetExit(
            StableId exitStableId,
            out RoomExitDefinitionV1 exit)
        {
            exit = null;
            return exitStableId != null
                && exitsById.TryGetValue(exitStableId, out exit);
        }

        public IReadOnlyList<RoomExitDefinitionV1> GetExitsFromRoom(
            StableId roomStableId)
        {
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }

            ReadOnlyCollection<RoomExitDefinitionV1> result;
            if (!exitsBySourceRoom.TryGetValue(roomStableId, out result))
            {
                throw new KeyNotFoundException(
                    "Unknown room identity: " + roomStableId);
            }

            return result;
        }

        public RoomDefinitionV1 GetTargetRoom(RoomExitDefinitionV1 exit)
        {
            if (exit == null)
            {
                throw new ArgumentNullException(nameof(exit));
            }

            RoomEntryDefinitionV1 entry;
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
            RoomGraphFormatV1.AppendToken(builder, "schema", SchemaId);
            RoomGraphFormatV1.AppendToken(
                builder,
                "layout_stable_id",
                LayoutStableId.ToString());
            RoomGraphFormatV1.AppendToken(
                builder,
                "start_room_stable_id",
                StartRoomStableId.ToString());
            RoomGraphFormatV1.AppendToken(
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
            RoomDefinitionV1 left,
            RoomDefinitionV1 right)
        {
            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }

            return left.RoomStableId.CompareTo(right.RoomStableId);
        }

        internal static int CompareEntries(
            RoomEntryDefinitionV1 left,
            RoomEntryDefinitionV1 right)
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
            RoomConnectionDefinitionV1 left,
            RoomConnectionDefinitionV1 right)
        {
            return left.ConnectionStableId.CompareTo(right.ConnectionStableId);
        }

        internal static int CompareDoorLinks(
            RoomDoorLinkDefinitionV1 left,
            RoomDoorLinkDefinitionV1 right)
        {
            return left.DoorLinkStableId.CompareTo(right.DoorLinkStableId);
        }

        internal static int CompareExits(
            RoomExitDefinitionV1 left,
            RoomExitDefinitionV1 right)
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
            RoomGraphValidationIssueV1 left,
            RoomGraphValidationIssueV1 right)
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
            List<RoomGraphValidationIssueV1> issues,
            RoomGraphValidationCodeV1 code,
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
            List<RoomGraphValidationIssueV1> issues,
            RoomGraphValidationCodeV1 code,
            string subject,
            string message)
        {
            issues.Add(new RoomGraphValidationIssueV1(
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
            RoomGraphFormatV1.AppendToken(
                builder,
                prefix + "_count",
                items.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < items.Count; index++)
            {
                object value = items[index];
                RoomGraphFormatV1.AppendToken(
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

    public static class RoomGraphFormatV1
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

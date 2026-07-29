using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Contracts.Missions.Rooms
{
    /// <summary>
    /// Immutable authorable room graph. Placement and gate data remain engine-neutral;
    /// the existing RoomGraphDefinition remains topology truth for traversal state.
    /// Current ROOM-001 requires distinct start and terminal rooms, which is documented
    /// as an inherited foundation limitation rather than inferred from room ordering.
    /// </summary>
    public sealed class AuthorableRoomGraphDefinition
    {
        private readonly ReadOnlyCollection<AuthorableRoomDefinition> rooms;
        private readonly Dictionary<StableId, AuthorableRoomDefinition> roomsById;
        private readonly Dictionary<StableId, AuthorableRoomDefinition> exitOwners;

        public const int CurrentSchemaVersion = 2;

        public AuthorableRoomGraphDefinition(
            StableId layoutStableId,
            StableId startRoomStableId,
            StableId terminalRoomStableId,
            IEnumerable<AuthorableRoomDefinition> rooms)
        {
            LayoutStableId = layoutStableId
                ?? throw new ArgumentNullException(nameof(layoutStableId));
            StartRoomStableId = startRoomStableId
                ?? throw new ArgumentNullException(nameof(startRoomStableId));
            TerminalRoomStableId = terminalRoomStableId
                ?? throw new ArgumentNullException(nameof(terminalRoomStableId));
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));

            var orderedRooms = new List<AuthorableRoomDefinition>(rooms);
            for (int index = 0; index < orderedRooms.Count; index++)
            {
                if (orderedRooms[index] == null)
                {
                    throw new ArgumentException(
                        "Authorable room graphs cannot contain null rooms.",
                        nameof(rooms));
                }
            }

            if (orderedRooms.Count == 0)
            {
                throw new ArgumentException(
                    "Authorable room graphs require at least one room.",
                    nameof(rooms));
            }

            orderedRooms.Sort(CompareRooms);
            this.rooms = new ReadOnlyCollection<AuthorableRoomDefinition>(orderedRooms);
            roomsById = new Dictionary<StableId, AuthorableRoomDefinition>();
            exitOwners = new Dictionary<StableId, AuthorableRoomDefinition>();
            var roomOrders = new HashSet<int>();
            var placementIds = new HashSet<StableId>();
            var doorIds = new HashSet<StableId>();

            for (int roomIndex = 0; roomIndex < this.rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinition room = this.rooms[roomIndex];
                if (roomsById.ContainsKey(room.RoomStableId))
                {
                    throw new ArgumentException(
                        "room-live-room-duplicate:" + room.RoomStableId);
                }

                if (!roomOrders.Add(room.Order))
                {
                    throw new ArgumentException(
                        "room-live-room-order-duplicate:" + room.Order);
                }

                roomsById.Add(room.RoomStableId, room);
                for (int placementIndex = 0;
                    placementIndex < room.Placements.Count;
                    placementIndex++)
                {
                    StableId placementId = room.Placements[placementIndex].InstanceStableId;
                    if (!placementIds.Add(placementId))
                    {
                        throw new ArgumentException(
                            "room-live-placement-instance-global-duplicate:" + placementId);
                    }
                }

                for (int doorIndex = 0; doorIndex < room.Doors.Count; doorIndex++)
                {
                    StableId doorId = room.Doors[doorIndex].DoorInstanceStableId;
                    if (!doorIds.Add(doorId))
                    {
                        throw new ArgumentException(
                            "room-live-door-instance-global-duplicate:" + doorId);
                    }
                }

                for (int exitIndex = 0; exitIndex < room.Exits.Count; exitIndex++)
                {
                    StableId exitId = room.Exits[exitIndex].ExitStableId;
                    if (exitOwners.ContainsKey(exitId))
                    {
                        throw new ArgumentException(
                            "room-live-exit-global-duplicate:" + exitId);
                    }

                    exitOwners.Add(exitId, room);
                }
            }

            if (!roomsById.ContainsKey(StartRoomStableId))
            {
                throw new ArgumentException(
                    "room-live-start-room-unknown:" + StartRoomStableId);
            }

            if (!roomsById.ContainsKey(TerminalRoomStableId))
            {
                throw new ArgumentException(
                    "room-live-terminal-room-unknown:" + TerminalRoomStableId);
            }

            ValidateLinks();
            RoomGraphDefinition = BuildRoomGraphDefinition();
            CanonicalJson = BuildCanonicalJson();
            Fingerprint = RoomLiveJson.ComputeSha256(CanonicalJson);
        }

        public StableId LayoutStableId { get; }

        public StableId StartRoomStableId { get; }

        public StableId TerminalRoomStableId { get; }

        public IReadOnlyList<AuthorableRoomDefinition> Rooms
        {
            get { return rooms; }
        }

        public RoomGraphDefinition RoomGraphDefinition { get; }

        public string CanonicalJson { get; }

        public string Fingerprint { get; }

        public AuthorableRoomDefinition GetRoom(StableId roomStableId)
        {
            AuthorableRoomDefinition room;
            if (roomStableId == null || !roomsById.TryGetValue(roomStableId, out room))
            {
                throw new KeyNotFoundException(
                    "Unknown live room identity: " + roomStableId);
            }

            return room;
        }

        public bool TryGetRoom(
            StableId roomStableId,
            out AuthorableRoomDefinition room)
        {
            if (roomStableId == null)
            {
                room = null;
                return false;
            }

            return roomsById.TryGetValue(roomStableId, out room);
        }

        public bool TryGetExitOwner(
            StableId exitStableId,
            out AuthorableRoomDefinition room)
        {
            if (exitStableId == null)
            {
                room = null;
                return false;
            }

            return exitOwners.TryGetValue(exitStableId, out room);
        }

        public string ToCanonicalJson()
        {
            return CanonicalJson;
        }

        private void ValidateLinks()
        {
            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinition room = rooms[roomIndex];
                for (int exitIndex = 0; exitIndex < room.Exits.Count; exitIndex++)
                {
                    RoomExitLinkDefinition exit = room.Exits[exitIndex];
                    if (exit.LinkKind == RoomLiveLinkKind.FinalExit)
                    {
                        continue;
                    }

                    AuthorableRoomDefinition targetRoom;
                    if (!roomsById.TryGetValue(exit.TargetRoomStableId, out targetRoom))
                    {
                        throw new ArgumentException(
                            "room-live-link-target-room-unknown:"
                            + exit.TargetRoomStableId);
                    }

                    if (!targetRoom.HasSpawnPoint(exit.TargetSpawnPointStableId))
                    {
                        throw new ArgumentException(
                            "room-live-link-target-spawn-unknown:"
                            + exit.TargetSpawnPointStableId);
                    }
                }
            }
        }

        private static StableId CreateDerivedStableId(
            string namespaceName,
            string purpose,
            StableId sourceStableId)
        {
            using (System.Security.Cryptography.SHA256 sha =
                System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(
                    purpose + "|" + sourceStableId);
                byte[] hash = sha.ComputeHash(bytes);
                var token = new StringBuilder(32);
                for (int index = 0; index < 16; index++)
                {
                    token.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }

                return StableId.Create(
                    namespaceName,
                    purpose + "-" + token.ToString());
            }
        }

        private RoomGraphDefinition BuildRoomGraphDefinition()
        {
            var graphRooms = new List<RoomDefinition>();
            var entries = new List<RoomEntryDefinition>();
            var connections = new List<RoomConnectionDefinition>();
            var doorLinks = new List<RoomDoorLinkDefinition>();

            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinition room = rooms[roomIndex];
                graphRooms.Add(new RoomDefinition(
                    room.RoomStableId,
                    room.Order,
                    room.RoomStableId == StartRoomStableId
                        ? RoomInitialAvailability.Available
                        : RoomInitialAvailability.Locked,
                    true));
                for (int spawnIndex = 0;
                    spawnIndex < room.SpawnPoints.Count;
                    spawnIndex++)
                {
                    entries.Add(new RoomEntryDefinition(
                        room.SpawnPoints[spawnIndex].SpawnPointStableId,
                        room.RoomStableId,
                        spawnIndex));
                }

                for (int exitIndex = 0; exitIndex < room.Exits.Count; exitIndex++)
                {
                    RoomExitLinkDefinition liveExit = room.Exits[exitIndex];
                    if (liveExit.LinkKind != RoomLiveLinkKind.Room)
                    {
                        continue;
                    }

                    StableId doorLinkId = CreateDerivedStableId(
                        "door-link",
                        "live",
                        liveExit.ExitStableId);
                    StableId connectionId = CreateDerivedStableId(
                        "connection",
                        "live",
                        liveExit.ExitStableId);
                    var graphExit = new RoomExitDefinition(
                        liveExit.ExitStableId,
                        room.RoomStableId,
                        liveExit.TargetSpawnPointStableId,
                        exitIndex,
                        liveExit.ExitType,
                        false,
                        null);
                    doorLinks.Add(new RoomDoorLinkDefinition(doorLinkId));
                    connections.Add(new RoomConnectionDefinition(
                        connectionId,
                        RoomConnectionDirectionality.OneWay,
                        doorLinkId,
                        new[] { graphExit }));
                }
            }

            RoomGraphValidationResult validation =
                RoomGraphDefinition.ValidateAndCreate(
                    LayoutStableId,
                    StartRoomStableId,
                    TerminalRoomStableId,
                    graphRooms,
                    entries,
                    connections,
                    doorLinks);
            if (!validation.IsValid)
            {
                var messages = new List<string>();
                for (int index = 0; index < validation.Issues.Count; index++)
                {
                    messages.Add(
                        validation.Issues[index].Code
                        + ":"
                        + validation.Issues[index].Subject);
                }

                throw new ArgumentException(
                    "room-live-room-graph-invalid:" + string.Join(",", messages));
            }

            return validation.Definition;
        }

        private string BuildCanonicalJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"schema_version\":")
                .Append(CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\"layout_id\":");
            RoomLiveJson.AppendString(builder, LayoutStableId.ToString());
            builder.Append(",\"start_room_id\":");
            RoomLiveJson.AppendString(builder, StartRoomStableId.ToString());
            builder.Append(",\"terminal_room_id\":");
            RoomLiveJson.AppendString(builder, TerminalRoomStableId.ToString());
            builder.Append(",\"rooms\":[");
            for (int index = 0; index < rooms.Count; index++)
            {
                if (index != 0) builder.Append(',');
                rooms[index].AppendCanonicalJson(builder);
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private static int CompareRooms(
            AuthorableRoomDefinition left,
            AuthorableRoomDefinition right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0
                ? order
                : left.RoomStableId.CompareTo(right.RoomStableId);
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public sealed class LevelGridEditorRoomProjectionV2
    {
        public LevelGridEditorRoomProjectionV2(
            LevelRoomAuthoring2D room,
            LevelDoorEndpointAuthoring2D[] doors,
            bool overlapsAnotherRoom,
            bool hasValidationProblem)
        {
            Room = room;
            Doors = doors ?? Array.Empty<LevelDoorEndpointAuthoring2D>();
            OverlapsAnotherRoom = overlapsAnotherRoom;
            HasValidationProblem = hasValidationProblem;
        }

        public LevelRoomAuthoring2D Room { get; }

        public IReadOnlyList<LevelDoorEndpointAuthoring2D> Doors { get; }

        public bool OverlapsAnotherRoom { get; }

        public bool HasValidationProblem { get; }
    }

    public sealed class LevelGridEditorProjectionV2
    {
        private static readonly LevelGridEditorProjectionV2 EmptyProjection =
            new LevelGridEditorProjectionV2(
                Array.Empty<LevelGridEditorRoomProjectionV2>(),
                Array.Empty<LevelDoorEndpointAuthoring2D>(),
                Array.Empty<LevelDoorLinkAuthoring2D>(),
                new Dictionary<LevelDoorEndpointAuthoring2D, LevelDoorLinkAuthoring2D>());

        private readonly Dictionary<LevelDoorEndpointAuthoring2D, LevelDoorLinkAuthoring2D>
            connectionByDoor;

        private LevelGridEditorProjectionV2(
            LevelGridEditorRoomProjectionV2[] rooms,
            LevelDoorEndpointAuthoring2D[] doors,
            LevelDoorLinkAuthoring2D[] connections,
            Dictionary<LevelDoorEndpointAuthoring2D, LevelDoorLinkAuthoring2D>
                connectionByDoor)
        {
            Rooms = rooms;
            Doors = doors;
            Connections = connections;
            this.connectionByDoor = connectionByDoor;
        }

        public IReadOnlyList<LevelGridEditorRoomProjectionV2> Rooms { get; }

        public IReadOnlyList<LevelDoorEndpointAuthoring2D> Doors { get; }

        public IReadOnlyList<LevelDoorLinkAuthoring2D> Connections { get; }

        public static LevelGridEditorProjectionV2 Empty
        {
            get { return EmptyProjection; }
        }

        public bool IsConnected(LevelDoorEndpointAuthoring2D door)
        {
            return door != null && connectionByDoor.ContainsKey(door);
        }

        public LevelDoorLinkAuthoring2D GetConnection(
            LevelDoorEndpointAuthoring2D door)
        {
            if (door == null)
            {
                return null;
            }

            LevelDoorLinkAuthoring2D connection;
            return connectionByDoor.TryGetValue(door, out connection)
                ? connection
                : null;
        }

        public static LevelGridEditorProjectionV2 Build(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (root == null)
            {
                return Empty;
            }

            LevelRoomAuthoring2D[] rooms =
                root.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
            LevelDoorEndpointAuthoring2D[] doors =
                root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            LevelDoorLinkAuthoring2D[] links =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            Array.Sort(rooms, CompareRooms);
            Array.Sort(doors, CompareDoors);
            Array.Sort(links, CompareLinks);

            Dictionary<LevelDoorEndpointAuthoring2D, LevelDoorLinkAuthoring2D>
                connectionByDoor =
                    new Dictionary<LevelDoorEndpointAuthoring2D, LevelDoorLinkAuthoring2D>();
            for (int index = 0; index < links.Length; index++)
            {
                LevelDoorLinkAuthoring2D link = links[index];
                if (link.SourceDoor != null && !connectionByDoor.ContainsKey(link.SourceDoor))
                {
                    connectionByDoor.Add(link.SourceDoor, link);
                }
                if (link.DestinationDoor != null
                    && !connectionByDoor.ContainsKey(link.DestinationDoor))
                {
                    connectionByDoor.Add(link.DestinationDoor, link);
                }
            }

            HashSet<LevelRoomAuthoring2D> overlapping = FindOverlappingRooms(rooms);
            HashSet<string> problemIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<LevelGridProblemV2> problems = root.LastGridValidation.Problems;
            for (int index = 0; index < problems.Count; index++)
            {
                if (!string.IsNullOrEmpty(problems[index].AuthoredId))
                {
                    problemIds.Add(problems[index].AuthoredId);
                }
            }

            List<LevelGridEditorRoomProjectionV2> roomProjections =
                new List<LevelGridEditorRoomProjectionV2>(rooms.Length);
            for (int roomIndex = 0; roomIndex < rooms.Length; roomIndex++)
            {
                LevelRoomAuthoring2D room = rooms[roomIndex];
                List<LevelDoorEndpointAuthoring2D> ownedDoors =
                    new List<LevelDoorEndpointAuthoring2D>();
                bool hasProblem = problemIds.Contains(room.RoomIdText);
                for (int doorIndex = 0; doorIndex < doors.Length; doorIndex++)
                {
                    if (doors[doorIndex].OwningRoom == room)
                    {
                        ownedDoors.Add(doors[doorIndex]);
                        hasProblem |= problemIds.Contains(doors[doorIndex].DoorIdText);
                    }
                }

                roomProjections.Add(new LevelGridEditorRoomProjectionV2(
                    room,
                    ownedDoors.ToArray(),
                    overlapping.Contains(room),
                    hasProblem));
            }

            return new LevelGridEditorProjectionV2(
                roomProjections.ToArray(),
                doors,
                links,
                connectionByDoor);
        }

        private static HashSet<LevelRoomAuthoring2D> FindOverlappingRooms(
            LevelRoomAuthoring2D[] rooms)
        {
            HashSet<LevelRoomAuthoring2D> result =
                new HashSet<LevelRoomAuthoring2D>();
            for (int first = 0; first < rooms.Length; first++)
            {
                RectInt firstRect = ToGridRect(rooms[first]);
                for (int second = first + 1; second < rooms.Length; second++)
                {
                    if (firstRect.Overlaps(ToGridRect(rooms[second])))
                    {
                        result.Add(rooms[first]);
                        result.Add(rooms[second]);
                    }
                }
            }
            return result;
        }

        private static RectInt ToGridRect(LevelRoomAuthoring2D room)
        {
            Vector2Int footprint = room.FootprintCells;
            return new RectInt(
                room.GridCoordinate.x,
                room.GridCoordinate.y,
                Mathf.Max(1, footprint.x),
                Mathf.Max(1, footprint.y));
        }

        private static int CompareRooms(
            LevelRoomAuthoring2D left,
            LevelRoomAuthoring2D right)
        {
            int x = left.GridCoordinate.x.CompareTo(right.GridCoordinate.x);
            if (x != 0)
            {
                return x;
            }
            int y = left.GridCoordinate.y.CompareTo(right.GridCoordinate.y);
            if (y != 0)
            {
                return y;
            }
            int slot = left.FolderSlot.CompareTo(right.FolderSlot);
            return slot != 0
                ? slot
                : string.CompareOrdinal(left.RoomIdText, right.RoomIdText);
        }

        private static int CompareDoors(
            LevelDoorEndpointAuthoring2D left,
            LevelDoorEndpointAuthoring2D right)
        {
            int room = string.CompareOrdinal(
                left.OwningRoom == null ? string.Empty : left.OwningRoom.RoomIdText,
                right.OwningRoom == null ? string.Empty : right.OwningRoom.RoomIdText);
            return room != 0
                ? room
                : string.CompareOrdinal(left.DoorIdText, right.DoorIdText);
        }

        private static int CompareLinks(
            LevelDoorLinkAuthoring2D left,
            LevelDoorLinkAuthoring2D right)
        {
            return string.CompareOrdinal(
                left.ConnectionIdText,
                right.ConnectionIdText);
        }
    }

    public static class LevelGridEditorProblemLocatorV2
    {
        public static Component FindExact(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridProblemV2 problem)
        {
            if (root == null || problem == null)
            {
                return null;
            }

            Component exact = FindExactByType<LevelRoomAuthoring2D>(
                root,
                problem,
                delegate(LevelRoomAuthoring2D room) { return room.RoomIdText; });
            if (exact != null)
            {
                return exact;
            }

            exact = FindExactByType<LevelDoorEndpointAuthoring2D>(
                root,
                problem,
                delegate(LevelDoorEndpointAuthoring2D door) { return door.DoorIdText; });
            if (exact != null)
            {
                return exact;
            }

            return FindExactByType<LevelDoorLinkAuthoring2D>(
                root,
                problem,
                delegate(LevelDoorLinkAuthoring2D link)
                {
                    return link.ConnectionIdText;
                });
        }

        public static Component FindByStableId(
            LevelDesignSceneAuthoringRoot2D root,
            string authoredId)
        {
            if (root == null || string.IsNullOrEmpty(authoredId))
            {
                return null;
            }

            LevelRoomAuthoring2D[] rooms =
                root.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
            for (int index = 0; index < rooms.Length; index++)
            {
                if (string.Equals(
                    rooms[index].RoomIdText,
                    authoredId,
                    StringComparison.Ordinal))
                {
                    return rooms[index];
                }
            }

            LevelDoorEndpointAuthoring2D[] doors =
                root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            for (int index = 0; index < doors.Length; index++)
            {
                if (string.Equals(
                    doors[index].DoorIdText,
                    authoredId,
                    StringComparison.Ordinal))
                {
                    return doors[index];
                }
            }

            LevelDoorLinkAuthoring2D[] links =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            for (int index = 0; index < links.Length; index++)
            {
                if (string.Equals(
                    links[index].ConnectionIdText,
                    authoredId,
                    StringComparison.Ordinal))
                {
                    return links[index];
                }
            }

            return null;
        }

        public static Component SelectExact(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridProblemV2 problem)
        {
            Component exact = FindExact(root, problem);
            if (exact == null)
            {
                exact = FindByStableId(root, problem == null ? null : problem.AuthoredId);
            }
            if (exact != null)
            {
                Selection.activeGameObject = exact.gameObject;
                EditorGUIUtility.PingObject(exact);
            }
            return exact;
        }

        public static string BuildDiagnosticLocation(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            return transform.gameObject.scene.name + ":" + GetHierarchyPath(transform);
        }

        private static Component FindExactByType<T>(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridProblemV2 problem,
            Func<T, string> getId)
            where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < components.Length; index++)
            {
                T component = components[index];
                if (string.Equals(
                        getId(component),
                        problem.AuthoredId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        BuildDiagnosticLocation(component.transform),
                        problem.DiagnosticLocation,
                        StringComparison.Ordinal))
                {
                    return component;
                }
            }
            return null;
        }

        private static string GetHierarchyPath(Transform current)
        {
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }
    }
}
#endif

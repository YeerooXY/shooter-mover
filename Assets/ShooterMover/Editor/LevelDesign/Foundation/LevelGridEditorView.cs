#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public sealed class LevelGridEditorRoomView
    {
        public LevelGridEditorRoomView(
            LevelRoom room,
            DoorEndpoint[] doors,
            bool overlapsAnotherRoom,
            bool hasValidationProblem)
        {
            Room = room;
            Doors = doors ?? Array.Empty<DoorEndpoint>();
            OverlapsAnotherRoom = overlapsAnotherRoom;
            HasValidationProblem = hasValidationProblem;
        }

        public LevelRoom Room { get; }

        public IReadOnlyList<DoorEndpoint> Doors { get; }

        public bool OverlapsAnotherRoom { get; }

        public bool HasValidationProblem { get; }
    }

    public sealed class LevelGridEditorView
    {
        private static readonly LevelGridEditorView EmptyProjection =
            new LevelGridEditorView(
                Array.Empty<LevelGridEditorRoomView>(),
                Array.Empty<DoorEndpoint>(),
                Array.Empty<DoorLink>(),
                new Dictionary<DoorEndpoint, DoorLink>());

        private readonly Dictionary<DoorEndpoint, DoorLink>
            connectionByDoor;

        private LevelGridEditorView(
            LevelGridEditorRoomView[] rooms,
            DoorEndpoint[] doors,
            DoorLink[] connections,
            Dictionary<DoorEndpoint, DoorLink>
                connectionByDoor)
        {
            Rooms = rooms;
            Doors = doors;
            Connections = connections;
            this.connectionByDoor = connectionByDoor;
        }

        public IReadOnlyList<LevelGridEditorRoomView> Rooms { get; }

        public IReadOnlyList<DoorEndpoint> Doors { get; }

        public IReadOnlyList<DoorLink> Connections { get; }

        public static LevelGridEditorView Empty
        {
            get { return EmptyProjection; }
        }

        public bool IsConnected(DoorEndpoint door)
        {
            return door != null && connectionByDoor.ContainsKey(door);
        }

        public DoorLink GetConnection(
            DoorEndpoint door)
        {
            if (door == null)
            {
                return null;
            }

            DoorLink connection;
            return connectionByDoor.TryGetValue(door, out connection)
                ? connection
                : null;
        }

        public static LevelGridEditorView Build(
            LevelDraft root)
        {
            if (root == null)
            {
                return Empty;
            }

            LevelRoom[] rooms =
                root.GetComponentsInChildren<LevelRoom>(true);
            DoorEndpoint[] doors =
                root.GetComponentsInChildren<DoorEndpoint>(true);
            DoorLink[] links =
                root.GetComponentsInChildren<DoorLink>(true);
            Array.Sort(rooms, CompareRooms);
            Array.Sort(doors, CompareDoors);
            Array.Sort(links, CompareLinks);

            Dictionary<DoorEndpoint, DoorLink>
                connectionByDoor =
                    new Dictionary<DoorEndpoint, DoorLink>();
            for (int index = 0; index < links.Length; index++)
            {
                DoorLink link = links[index];
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

            HashSet<LevelRoom> overlapping = FindOverlappingRooms(rooms);
            HashSet<string> problemIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<LevelRoom> problemRooms =
                new HashSet<LevelRoom>();

            IReadOnlyList<LevelGridProblem> gridProblems =
                root.LastGridValidation.Problems;
            for (int index = 0; index < gridProblems.Count; index++)
            {
                LevelGridProblem problem = gridProblems[index];
                if (!string.IsNullOrEmpty(problem.AuthoredId))
                {
                    problemIds.Add(problem.AuthoredId);
                }
                Component affected =
                    LevelGridEditorProblemLocator.FindExact(root, problem)
                    ?? LevelGridEditorProblemLocator.FindByStableId(
                        root,
                        problem.AuthoredId);
                AddRelatedRooms(affected, problemRooms);
            }

            IReadOnlyList<LevelDesignValidationIssue> foundationIssues =
                root.LastValidation.Issues;
            for (int index = 0; index < foundationIssues.Count; index++)
            {
                LevelDesignValidationIssue issue = foundationIssues[index];
                if (!string.IsNullOrEmpty(issue.AuthoredId))
                {
                    problemIds.Add(issue.AuthoredId);
                }
                Component affected =
                    LevelGridEditorProblemLocator.FindExact(root, issue)
                    ?? LevelGridEditorProblemLocator.FindFoundationByStableId(
                        root,
                        issue.AuthoredId);
                AddRelatedRooms(affected, problemRooms);
            }

            List<LevelGridEditorRoomView> roomProjections =
                new List<LevelGridEditorRoomView>(rooms.Length);
            for (int roomIndex = 0; roomIndex < rooms.Length; roomIndex++)
            {
                LevelRoom room = rooms[roomIndex];
                List<DoorEndpoint> ownedDoors =
                    new List<DoorEndpoint>();
                bool hasProblem = problemIds.Contains(room.RoomIdText)
                    || problemRooms.Contains(room);
                for (int doorIndex = 0; doorIndex < doors.Length; doorIndex++)
                {
                    if (doors[doorIndex].OwningRoom == room)
                    {
                        ownedDoors.Add(doors[doorIndex]);
                        hasProblem |= problemIds.Contains(doors[doorIndex].DoorIdText);
                    }
                }

                roomProjections.Add(new LevelGridEditorRoomView(
                    room,
                    ownedDoors.ToArray(),
                    overlapping.Contains(room),
                    hasProblem));
            }

            return new LevelGridEditorView(
                roomProjections.ToArray(),
                doors,
                links,
                connectionByDoor);
        }

        private static void AddRelatedRooms(
            Component affected,
            HashSet<LevelRoom> rooms)
        {
            if (affected == null)
            {
                return;
            }

            LevelRoom room = affected as LevelRoom;
            if (room != null)
            {
                rooms.Add(room);
                return;
            }

            DoorEndpoint endpoint =
                affected as DoorEndpoint;
            if (endpoint != null && endpoint.OwningRoom != null)
            {
                rooms.Add(endpoint.OwningRoom);
                return;
            }

            DoorLink link = affected as DoorLink;
            if (link != null)
            {
                if (link.SourceRoom != null)
                {
                    rooms.Add(link.SourceRoom);
                }
                if (link.DestinationRoom != null)
                {
                    rooms.Add(link.DestinationRoom);
                }
                return;
            }

            LevelObject placement =
                affected as LevelObject;
            if (placement != null && placement.Room != null)
            {
                rooms.Add(placement.Room);
                return;
            }

            VoidArea voidRegion =
                affected as VoidArea;
            if (voidRegion != null && voidRegion.Room != null)
            {
                rooms.Add(voidRegion.Room);
                return;
            }

            DoorConnection legacyDoor =
                affected as DoorConnection;
            if (legacyDoor != null)
            {
                if (legacyDoor.SourceRoom != null)
                {
                    rooms.Add(legacyDoor.SourceRoom);
                }
                if (legacyDoor.DestinationRoom != null)
                {
                    rooms.Add(legacyDoor.DestinationRoom);
                }
                return;
            }

            LevelRoom parentRoom =
                affected.GetComponentInParent<LevelRoom>();
            if (parentRoom != null)
            {
                rooms.Add(parentRoom);
            }
        }

        private static HashSet<LevelRoom> FindOverlappingRooms(
            LevelRoom[] rooms)
        {
            HashSet<LevelRoom> result =
                new HashSet<LevelRoom>();
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

        private static RectInt ToGridRect(LevelRoom room)
        {
            Vector2Int footprint = room.FootprintCells;
            return new RectInt(
                room.GridCoordinate.x,
                room.GridCoordinate.y,
                Mathf.Max(1, footprint.x),
                Mathf.Max(1, footprint.y));
        }

        private static int CompareRooms(
            LevelRoom left,
            LevelRoom right)
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
            DoorEndpoint left,
            DoorEndpoint right)
        {
            int room = string.CompareOrdinal(
                left.OwningRoom == null ? string.Empty : left.OwningRoom.RoomIdText,
                right.OwningRoom == null ? string.Empty : right.OwningRoom.RoomIdText);
            return room != 0
                ? room
                : string.CompareOrdinal(left.DoorIdText, right.DoorIdText);
        }

        private static int CompareLinks(
            DoorLink left,
            DoorLink right)
        {
            return string.CompareOrdinal(
                left.ConnectionIdText,
                right.ConnectionIdText);
        }
    }

    public static class LevelGridEditorProblemLocator
    {
        public static Component FindExact(
            LevelDraft root,
            LevelGridProblem problem)
        {
            if (root == null || problem == null)
            {
                return null;
            }

            Component exact = FindExactByType<LevelRoom>(
                root,
                problem.AuthoredId,
                problem.DiagnosticLocation,
                delegate(LevelRoom room)
                {
                    return string.Equals(
                        room.RoomIdText,
                        problem.AuthoredId,
                        StringComparison.Ordinal);
                });
            if (exact != null)
            {
                return exact;
            }

            exact = FindExactByType<DoorEndpoint>(
                root,
                problem.AuthoredId,
                problem.DiagnosticLocation,
                delegate(DoorEndpoint door)
                {
                    return string.Equals(
                        door.DoorIdText,
                        problem.AuthoredId,
                        StringComparison.Ordinal);
                });
            if (exact != null)
            {
                return exact;
            }

            return FindExactByType<DoorLink>(
                root,
                problem.AuthoredId,
                problem.DiagnosticLocation,
                delegate(DoorLink link)
                {
                    return string.Equals(
                        link.ConnectionIdText,
                        problem.AuthoredId,
                        StringComparison.Ordinal);
                });
        }

        public static Component FindExact(
            LevelDraft root,
            LevelDesignValidationIssue issue)
        {
            if (root == null || issue == null)
            {
                return null;
            }

            if (string.Equals(
                    root.LevelIdText,
                    issue.AuthoredId,
                    StringComparison.Ordinal)
                && LocationMatches(
                    issue.DiagnosticLocation,
                    BuildDiagnosticLocation(root.transform)))
            {
                return root;
            }

            Component exact = FindExactByType<LevelRoom>(
                root,
                issue.AuthoredId,
                issue.DiagnosticLocation,
                delegate(LevelRoom room)
                {
                    return string.Equals(
                        room.RoomIdText,
                        issue.AuthoredId,
                        StringComparison.Ordinal);
                });
            if (exact != null)
            {
                return exact;
            }

            exact = FindExactByType<LevelObject>(
                root,
                issue.AuthoredId,
                issue.DiagnosticLocation,
                delegate(LevelObject placement)
                {
                    return string.Equals(
                            placement.AuthoredIdText,
                            issue.AuthoredId,
                            StringComparison.Ordinal)
                        || string.Equals(
                            placement.SocketIdText,
                            issue.AuthoredId,
                            StringComparison.Ordinal);
                });
            if (exact != null)
            {
                return exact;
            }

            exact = FindExactByType<DoorConnection>(
                root,
                issue.AuthoredId,
                issue.DiagnosticLocation,
                delegate(DoorConnection door)
                {
                    return string.Equals(
                            door.DoorIdText,
                            issue.AuthoredId,
                            StringComparison.Ordinal)
                        || string.Equals(
                            door.SourceSocketIdText,
                            issue.AuthoredId,
                            StringComparison.Ordinal)
                        || string.Equals(
                            door.DestinationSocketIdText,
                            issue.AuthoredId,
                            StringComparison.Ordinal);
                });
            if (exact != null)
            {
                return exact;
            }

            return FindExactByType<VoidArea>(
                root,
                issue.AuthoredId,
                issue.DiagnosticLocation,
                delegate(VoidArea voidRegion)
                {
                    return string.Equals(
                        voidRegion.VoidRegionIdText,
                        issue.AuthoredId,
                        StringComparison.Ordinal);
                });
        }

        public static Component FindByStableId(
            LevelDraft root,
            string authoredId)
        {
            if (root == null || string.IsNullOrEmpty(authoredId))
            {
                return null;
            }

            LevelRoom[] rooms =
                root.GetComponentsInChildren<LevelRoom>(true);
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

            DoorEndpoint[] doors =
                root.GetComponentsInChildren<DoorEndpoint>(true);
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

            DoorLink[] links =
                root.GetComponentsInChildren<DoorLink>(true);
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

        public static Component FindFoundationByStableId(
            LevelDraft root,
            string authoredId)
        {
            if (root == null || string.IsNullOrEmpty(authoredId))
            {
                return null;
            }
            if (string.Equals(root.LevelIdText, authoredId, StringComparison.Ordinal))
            {
                return root;
            }

            LevelRoom[] rooms =
                root.GetComponentsInChildren<LevelRoom>(true);
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

            LevelObject[] placements =
                root.GetComponentsInChildren<LevelObject>(true);
            for (int index = 0; index < placements.Length; index++)
            {
                if (string.Equals(
                        placements[index].AuthoredIdText,
                        authoredId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        placements[index].SocketIdText,
                        authoredId,
                        StringComparison.Ordinal))
                {
                    return placements[index];
                }
            }

            DoorConnection[] legacyDoors =
                root.GetComponentsInChildren<DoorConnection>(true);
            for (int index = 0; index < legacyDoors.Length; index++)
            {
                if (string.Equals(
                        legacyDoors[index].DoorIdText,
                        authoredId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        legacyDoors[index].SourceSocketIdText,
                        authoredId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        legacyDoors[index].DestinationSocketIdText,
                        authoredId,
                        StringComparison.Ordinal))
                {
                    return legacyDoors[index];
                }
            }

            VoidArea[] voids =
                root.GetComponentsInChildren<VoidArea>(true);
            for (int index = 0; index < voids.Length; index++)
            {
                if (string.Equals(
                    voids[index].VoidRegionIdText,
                    authoredId,
                    StringComparison.Ordinal))
                {
                    return voids[index];
                }
            }

            return null;
        }

        public static Component SelectExact(
            LevelDraft root,
            LevelGridProblem problem)
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

        public static Component SelectExact(
            LevelDraft root,
            LevelDesignValidationIssue issue)
        {
            Component exact = FindExact(root, issue);
            if (exact == null)
            {
                exact = FindFoundationByStableId(
                    root,
                    issue == null ? null : issue.AuthoredId);
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
            LevelDraft root,
            string authoredId,
            string diagnosticLocation,
            Func<T, bool> matchesId)
            where T : Component
        {
            if (string.IsNullOrEmpty(authoredId))
            {
                return null;
            }

            T[] components = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < components.Length; index++)
            {
                T component = components[index];
                if (matchesId(component)
                    && LocationMatches(
                        diagnosticLocation,
                        BuildDiagnosticLocation(component.transform)))
                {
                    return component;
                }
            }
            return null;
        }

        private static bool LocationMatches(string expected, string actual)
        {
            return string.IsNullOrEmpty(expected)
                || string.Equals(expected, actual, StringComparison.Ordinal);
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

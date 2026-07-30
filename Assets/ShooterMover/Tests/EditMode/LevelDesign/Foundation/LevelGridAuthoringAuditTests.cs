#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Editor.LevelDesign.Foundation;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.LevelDesign.Foundation
{
    public sealed class LevelGridAuthoringAuditTests
    {
        [Test]
        public void DeleteDoor_RemovesLinkPreservesOppositeAndUndoesAtomically()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDraft root =
                    rootObject.AddComponent<LevelDraft>();
                root.ConfigureForTests("level.door-delete");
                LevelRoom left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1,
                    true);
                LevelRoom right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1,
                    true);
                DoorEndpoint leftDoor = CreateDoor(
                    left,
                    "door.left-east",
                    LevelDoorSide.East);
                DoorEndpoint rightDoor = CreateDoor(
                    right,
                    "door.right-west",
                    LevelDoorSide.West);
                CreateConnection(root.transform, left, leftDoor, right, rightDoor);

                MethodInfo delete = typeof(LevelGridDoorOperations).GetMethod(
                    "DeleteDoorUndoable",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(delete, Is.Not.Null);
                delete.Invoke(null, new object[] { leftDoor });

                Assert.That(
                    root.GetComponentsInChildren<DoorLink>(true),
                    Is.Empty);
                Assert.That(
                    root.GetComponentsInChildren<DoorEndpoint>(true),
                    Has.Member(rightDoor));
                Assert.That(root.LastGridValidation.UnconnectedTraversableDoorCount, Is.EqualTo(1));

                Undo.PerformUndo();
                Assert.That(
                    root.GetComponentsInChildren<DoorLink>(true).Length,
                    Is.EqualTo(1));
                Assert.That(
                    root.GetComponentsInChildren<DoorEndpoint>(true).Length,
                    Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void FixedDoor_CapturesDraggedLocalPosition()
        {
            GameObject roomObject = new GameObject("Room");
            try
            {
                LevelRoom room =
                    roomObject.AddComponent<LevelRoom>();
                room.ConfigureForTests(
                    "room.fixed",
                    Vector2Int.zero,
                    Vector2.one,
                    Vector2Int.one,
                    null);
                DoorEndpoint door = CreateDoor(
                    room,
                    "door.fixed",
                    LevelDoorSide.North,
                    LevelDoorPlacementMode.Fixed);

                door.transform.localPosition = new Vector3(3.25f, -1.5f, 0f);
                door.CaptureCurrentFixedPosition();

                Assert.That(door.FixedLocalPosition, Is.EqualTo(new Vector2(3.25f, -1.5f)));
                Assert.That(door.ResolveTargetLocalPosition().x, Is.EqualTo(3.25f));
                Assert.That(door.ResolveTargetLocalPosition().y, Is.EqualTo(-1.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
            }
        }

        [Test]
        public void MovingConnectedRoom_ReflowsAutoFacingEdgeDoor()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDraft root =
                    rootObject.AddComponent<LevelDraft>();
                root.ConfigureForTests("level.reflow");
                LevelRoom source = CreateRoom(
                    root.transform,
                    "room.source",
                    Vector2Int.zero,
                    1,
                    true);
                LevelRoom destination = CreateRoom(
                    root.transform,
                    "room.destination",
                    new Vector2Int(0, 3),
                    1,
                    true);
                DoorEndpoint sourceDoor = CreateDoor(
                    source,
                    "door.source",
                    LevelDoorSide.East);
                DoorEndpoint destinationDoor = CreateDoor(
                    destination,
                    "door.destination",
                    LevelDoorSide.West);
                CreateConnection(
                    root.transform,
                    source,
                    sourceDoor,
                    destination,
                    destinationDoor);

                Assert.That(LevelGridDoorOperations.ReflowDoor(root, sourceDoor), Is.True);
                Assert.That(sourceDoor.Side, Is.EqualTo(LevelDoorSide.North));
                Assert.That(LevelGridDoorOperations.ReflowDoor(root, destinationDoor), Is.True);
                Assert.That(destinationDoor.Side, Is.EqualTo(LevelDoorSide.South));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DuplicateCoordinateSlot_IsRejectedIndependentlyOfGlobalOrdering()
        {
            LevelRoomRecord roomA = RoomRecord("room.a", new Vector2Int(50, 51));
            LevelRoomRecord roomB = RoomRecord("room.b", new Vector2Int(50, 51));
            LevelGridRoomRecord gridA = new LevelGridRoomRecord(
                roomA.RoomId,
                roomA.GridCoordinate,
                Vector2Int.one,
                1,
                "room-a");
            LevelGridRoomRecord gridB = new LevelGridRoomRecord(
                roomB.RoomId,
                roomB.GridCoordinate,
                Vector2Int.one,
                1,
                "room-b");

            LevelGridValidationResult result =
                LevelGridAuthoringCompositeValidator.Validate(
                    new[] { roomA, roomB },
                    new[] { gridA, gridB },
                    Array.Empty<LevelGridDoorRecord>(),
                    Array.Empty<LevelGridConnectionRecord>(),
                    LevelGridValidationPurpose.Draft);

            Assert.That(
                result.Problems.Count(
                    issue => issue.Code == LevelGridProblemCode.DuplicateRoomFolderSlot),
                Is.EqualTo(2));
        }

        private static LevelRoom CreateRoom(
            Transform parent,
            string id,
            Vector2Int coordinate,
            int slot,
            bool withBounds)
        {
            GameObject roomObject = new GameObject(id);
            roomObject.transform.SetParent(parent, false);
            BoxCollider2D bounds = withBounds
                ? roomObject.AddComponent<BoxCollider2D>()
                : null;
            if (bounds != null)
            {
                bounds.size = Vector2.one;
            }

            LevelRoom room =
                roomObject.AddComponent<LevelRoom>();
            room.ConfigureForTests(
                id,
                coordinate,
                Vector2.one,
                Vector2Int.one,
                bounds);
            room.ConfigureFolderSlotForTests(slot);
            room.SnapToAuthoredGrid();
            return room;
        }

        private static DoorEndpoint CreateDoor(
            LevelRoom room,
            string id,
            LevelDoorSide side,
            LevelDoorPlacementMode mode = LevelDoorPlacementMode.EdgeManaged)
        {
            GameObject doorObject = new GameObject(id);
            doorObject.transform.SetParent(room.transform, false);
            DoorEndpoint door =
                doorObject.AddComponent<DoorEndpoint>();
            door.ConfigureAuthoring(
                id,
                room,
                side,
                mode,
                0.5f,
                Vector2.zero,
                true,
                true);
            door.SnapToPlacement();
            return door;
        }

        private static DoorLink CreateConnection(
            Transform parent,
            LevelRoom sourceRoom,
            DoorEndpoint sourceDoor,
            LevelRoom destinationRoom,
            DoorEndpoint destinationDoor)
        {
            GameObject linkObject = new GameObject("connection.test");
            linkObject.transform.SetParent(parent, false);
            DoorLink link =
                linkObject.AddComponent<DoorLink>();
            link.ConfigureConnection(
                "connection.test",
                sourceRoom,
                sourceDoor,
                destinationRoom,
                destinationDoor);
            return link;
        }

        private static LevelRoomRecord RoomRecord(string id, Vector2Int coordinate)
        {
            return new LevelRoomRecord(
                id,
                coordinate,
                Vector2.one,
                Vector2Int.one,
                LevelRoomAlignment.GridOrigin,
                new Rect(coordinate.x, coordinate.y, 1f, 1f),
                true,
                0,
                coordinate,
                true,
                id);
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.LevelDesign.Foundation
{
    public sealed class LevelGridAuthoringTests
    {
        [Test]
        public void RoomLabels_AreOptionalAndDefaultToCoordinates()
        {
            GameObject roomObject = new GameObject("RoomObject");
            try
            {
                LevelRoom room =
                    roomObject.AddComponent<LevelRoom>();
                room.ConfigureForTests(
                    "room.stable",
                    new Vector2Int(50, 51),
                    Vector2.one,
                    Vector2Int.one,
                    null);

                Assert.That(room.DisplayName, Is.Empty);
                Assert.That(room.EditorLabel, Is.EqualTo("Room 50,51"));

                room.ConfigureDisplayNameForTests("Optional Storage Hall");
                Assert.That(room.EditorLabel, Is.EqualTo("Optional Storage Hall"));
                Assert.That(room.RoomIdText, Is.EqualTo("room.stable"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
            }
        }

        [Test]
        public void MovingRoom_DoesNotChangeStableIdentity()
        {
            GameObject roomObject = new GameObject("RoomObject");
            try
            {
                LevelRoom room =
                    roomObject.AddComponent<LevelRoom>();
                room.ConfigureForTests(
                    "room.immutable-identity",
                    new Vector2Int(2, 3),
                    Vector2.one,
                    Vector2Int.one,
                    null);

                roomObject.transform.position = new Vector3(200f, -100f, 0f);
                roomObject.name = "Renamed";

                Assert.That(
                    room.BuildRecord().RoomId,
                    Is.EqualTo("room.immutable-identity"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
            }
        }

        [Test]
        public void MultipleDoorsOnSameSide_AreAllowed()
        {
            LevelRoomRecord room = Room("room.alpha", Vector2Int.zero);
            LevelGridDoorRecord first = Door(
                "door.alpha-east-1",
                room.RoomId,
                LevelDoorSide.East,
                0.25f,
                false);
            LevelGridDoorRecord second = Door(
                "door.alpha-east-2",
                room.RoomId,
                LevelDoorSide.East,
                0.75f,
                false);

            LevelGridValidationResult result =
                LevelGridAuthoringValidator.Validate(
                    new[] { room },
                    new[] { first, second },
                    Array.Empty<LevelGridConnectionRecord>(),
                    LevelGridValidationPurpose.ProductionPublish);

            Assert.That(result.ErrorCount, Is.Zero);
            Assert.That(result.CanPublish, Is.True);
        }

        [Test]
        public void UnconnectedTraversableDoor_WarnsInDraftAndBlocksProduction()
        {
            LevelRoomRecord room = Room("room.alpha", Vector2Int.zero);
            LevelGridDoorRecord door = Door(
                "door.alpha-east",
                room.RoomId,
                LevelDoorSide.East,
                0.5f,
                true);

            LevelGridValidationResult draft =
                LevelGridAuthoringValidator.Validate(
                    new[] { room },
                    new[] { door },
                    Array.Empty<LevelGridConnectionRecord>(),
                    LevelGridValidationPurpose.Draft);
            LevelGridValidationResult production =
                LevelGridAuthoringValidator.Validate(
                    new[] { room },
                    new[] { door },
                    Array.Empty<LevelGridConnectionRecord>(),
                    LevelGridValidationPurpose.ProductionPublish);

            Assert.That(draft.CanSaveDraft, Is.True);
            Assert.That(draft.ErrorCount, Is.Zero);
            Assert.That(draft.WarningCount, Is.EqualTo(1));
            Assert.That(production.CanPublish, Is.False);
            Assert.That(production.ErrorCount, Is.EqualTo(1));
            Assert.That(
                production.Problems.Single().Code,
                Is.EqualTo(LevelGridProblemCode.UnconnectedTraversableDoor));
        }

        [Test]
        public void ConnectionsReferenceStableRoomAndDoorEndpoints()
        {
            LevelRoomRecord roomA = Room("room.alpha", Vector2Int.zero);
            LevelRoomRecord roomB = Room("room.beta", Vector2Int.right);
            LevelGridDoorRecord doorA = Door(
                "door.alpha-east",
                roomA.RoomId,
                LevelDoorSide.East,
                0.5f,
                true);
            LevelGridDoorRecord doorB = Door(
                "door.beta-west",
                roomB.RoomId,
                LevelDoorSide.West,
                0.5f,
                true);
            LevelGridConnectionRecord connection =
                new LevelGridConnectionRecord(
                    "connection.alpha-beta",
                    roomA.RoomId,
                    doorA.DoorId,
                    roomB.RoomId,
                    doorB.DoorId,
                    LevelDoorTravelPolicy.Bidirectional,
                    "connection");

            LevelGridValidationResult result =
                LevelGridAuthoringValidator.Validate(
                    new[] { roomA, roomB },
                    new[] { doorA, doorB },
                    new[] { connection },
                    LevelGridValidationPurpose.ProductionPublish);

            Assert.That(result.ErrorCount, Is.Zero);
            Assert.That(result.UnconnectedTraversableDoorCount, Is.Zero);
            Assert.That(result.CanPublish, Is.True);
        }

        [Test]
        public void EdgeManagedAndFixedDoorPlacement_AreBothValid()
        {
            LevelRoomRecord room = Room("room.alpha", Vector2Int.zero);
            LevelGridDoorRecord managed = Door(
                "door.managed",
                room.RoomId,
                LevelDoorSide.North,
                0.1f,
                false);
            LevelGridDoorRecord fixedDoor = new LevelGridDoorRecord(
                "door.fixed",
                room.RoomId,
                LevelDoorSide.South,
                LevelDoorPlacementMode.Fixed,
                0f,
                new Vector2(3.5f, -2f),
                false,
                true,
                "fixed");

            LevelGridValidationResult result =
                LevelGridAuthoringValidator.Validate(
                    new[] { room },
                    new[] { managed, fixedDoor },
                    Array.Empty<LevelGridConnectionRecord>(),
                    LevelGridValidationPurpose.ProductionPublish);

            Assert.That(result.ErrorCount, Is.Zero);
        }

        private static LevelRoomRecord Room(string id, Vector2Int coordinate)
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

        private static LevelGridDoorRecord Door(
            string id,
            string roomId,
            LevelDoorSide side,
            float offset,
            bool traversable)
        {
            return new LevelGridDoorRecord(
                id,
                roomId,
                side,
                LevelDoorPlacementMode.EdgeManaged,
                offset,
                Vector2.zero,
                traversable,
                true,
                id);
        }
    }
}
#endif

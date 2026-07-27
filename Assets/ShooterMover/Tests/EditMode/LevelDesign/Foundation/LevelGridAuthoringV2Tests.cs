#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.LevelDesign.Foundation
{
    public sealed class LevelGridAuthoringV2Tests
    {
        [Test]
        public void RoomLabels_AreOptionalAndDefaultToCoordinates()
        {
            GameObject roomObject = new GameObject("RoomObject");
            try
            {
                LevelRoomAuthoring2D room =
                    roomObject.AddComponent<LevelRoomAuthoring2D>();
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
                LevelRoomAuthoring2D room =
                    roomObject.AddComponent<LevelRoomAuthoring2D>();
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
            LevelGridDoorRecordV2 first = Door(
                "door.alpha-east-1",
                room.RoomId,
                LevelDoorSideV2.East,
                0.25f,
                false);
            LevelGridDoorRecordV2 second = Door(
                "door.alpha-east-2",
                room.RoomId,
                LevelDoorSideV2.East,
                0.75f,
                false);

            LevelGridValidationResultV2 result =
                LevelGridAuthoringV2Validator.Validate(
                    new[] { room },
                    new[] { first, second },
                    Array.Empty<LevelGridConnectionRecordV2>(),
                    LevelGridValidationPurposeV2.ProductionPublish);

            Assert.That(result.ErrorCount, Is.Zero);
            Assert.That(result.CanPublish, Is.True);
        }

        [Test]
        public void UnconnectedTraversableDoor_WarnsInDraftAndBlocksProduction()
        {
            LevelRoomRecord room = Room("room.alpha", Vector2Int.zero);
            LevelGridDoorRecordV2 door = Door(
                "door.alpha-east",
                room.RoomId,
                LevelDoorSideV2.East,
                0.5f,
                true);

            LevelGridValidationResultV2 draft =
                LevelGridAuthoringV2Validator.Validate(
                    new[] { room },
                    new[] { door },
                    Array.Empty<LevelGridConnectionRecordV2>(),
                    LevelGridValidationPurposeV2.Draft);
            LevelGridValidationResultV2 production =
                LevelGridAuthoringV2Validator.Validate(
                    new[] { room },
                    new[] { door },
                    Array.Empty<LevelGridConnectionRecordV2>(),
                    LevelGridValidationPurposeV2.ProductionPublish);

            Assert.That(draft.CanSaveDraft, Is.True);
            Assert.That(draft.ErrorCount, Is.Zero);
            Assert.That(draft.WarningCount, Is.EqualTo(1));
            Assert.That(production.CanPublish, Is.False);
            Assert.That(production.ErrorCount, Is.EqualTo(1));
            Assert.That(
                production.Problems.Single().Code,
                Is.EqualTo(LevelGridProblemCodeV2.UnconnectedTraversableDoor));
        }

        [Test]
        public void ConnectionsReferenceStableRoomAndDoorEndpoints()
        {
            LevelRoomRecord roomA = Room("room.alpha", Vector2Int.zero);
            LevelRoomRecord roomB = Room("room.beta", Vector2Int.right);
            LevelGridDoorRecordV2 doorA = Door(
                "door.alpha-east",
                roomA.RoomId,
                LevelDoorSideV2.East,
                0.5f,
                true);
            LevelGridDoorRecordV2 doorB = Door(
                "door.beta-west",
                roomB.RoomId,
                LevelDoorSideV2.West,
                0.5f,
                true);
            LevelGridConnectionRecordV2 connection =
                new LevelGridConnectionRecordV2(
                    "connection.alpha-beta",
                    roomA.RoomId,
                    doorA.DoorId,
                    roomB.RoomId,
                    doorB.DoorId,
                    LevelDoorTravelPolicy.Bidirectional,
                    "connection");

            LevelGridValidationResultV2 result =
                LevelGridAuthoringV2Validator.Validate(
                    new[] { roomA, roomB },
                    new[] { doorA, doorB },
                    new[] { connection },
                    LevelGridValidationPurposeV2.ProductionPublish);

            Assert.That(result.ErrorCount, Is.Zero);
            Assert.That(result.UnconnectedTraversableDoorCount, Is.Zero);
            Assert.That(result.CanPublish, Is.True);
        }

        [Test]
        public void EdgeManagedAndFixedDoorPlacement_AreBothValid()
        {
            LevelRoomRecord room = Room("room.alpha", Vector2Int.zero);
            LevelGridDoorRecordV2 managed = Door(
                "door.managed",
                room.RoomId,
                LevelDoorSideV2.North,
                0.1f,
                false);
            LevelGridDoorRecordV2 fixedDoor = new LevelGridDoorRecordV2(
                "door.fixed",
                room.RoomId,
                LevelDoorSideV2.South,
                LevelDoorPlacementModeV2.Fixed,
                0f,
                new Vector2(3.5f, -2f),
                false,
                true,
                "fixed");

            LevelGridValidationResultV2 result =
                LevelGridAuthoringV2Validator.Validate(
                    new[] { room },
                    new[] { managed, fixedDoor },
                    Array.Empty<LevelGridConnectionRecordV2>(),
                    LevelGridValidationPurposeV2.ProductionPublish);

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

        private static LevelGridDoorRecordV2 Door(
            string id,
            string roomId,
            LevelDoorSideV2 side,
            float offset,
            bool traversable)
        {
            return new LevelGridDoorRecordV2(
                id,
                roomId,
                side,
                LevelDoorPlacementModeV2.EdgeManaged,
                offset,
                Vector2.zero,
                traversable,
                true,
                id);
        }
    }
}
#endif

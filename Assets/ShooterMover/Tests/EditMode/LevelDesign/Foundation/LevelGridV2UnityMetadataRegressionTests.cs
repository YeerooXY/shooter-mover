#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.LevelDesign.Foundation
{
    public sealed class LevelGridV2UnityMetadataRegressionTests
    {
        [Test]
        public void MovingRoomFolder_PreservesItsUnityFolderGuidMetadata()
        {
            string root = TempRoot("move-meta");
            string roomsRoot = Path.Combine(root, "Rooms");
            string oldFolder = Path.Combine(roomsRoot, "Room_1_0_01");
            GameObject roomObject = new GameObject("Moved Room");
            try
            {
                Directory.CreateDirectory(oldFolder);
                File.WriteAllText(
                    Path.Combine(oldFolder, "room.json"),
                    "{\"room_id\":\"room.moved\"}");
                File.WriteAllText(
                    oldFolder + ".meta",
                    "fileFormatVersion: 2\nguid: survivor-folder-guid\n");

                LevelRoomAuthoring2D room = roomObject.AddComponent<LevelRoomAuthoring2D>();
                room.ConfigureForTests(
                    "room.moved",
                    new Vector2Int(4, 0),
                    Vector2.one,
                    Vector2Int.one,
                    null);

                IReadOnlyDictionary<string, string> result =
                    LevelGridV2RoomFolderMigration.Prepare(
                        new[] { room },
                        roomsRoot);
                string movedFolder = result["room.moved"];

                Assert.That(File.Exists(oldFolder + ".meta"), Is.False);
                Assert.That(File.Exists(movedFolder + ".meta"), Is.True);
                Assert.That(
                    File.ReadAllText(movedFolder + ".meta"),
                    Does.Contain("survivor-folder-guid"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
                DeleteRoot(root);
            }
        }

        [Test]
        public void ReusingDeletedCoordinate_DoesNotAdoptDeletedRoomFolderGuid()
        {
            string root = TempRoot("deleted-meta");
            string roomsRoot = Path.Combine(root, "Rooms");
            string survivorOld = Path.Combine(roomsRoot, "Room_0_0_01");
            string deletedOld = Path.Combine(roomsRoot, "Room_1_0_01");
            GameObject roomObject = new GameObject("Surviving Room");
            try
            {
                Directory.CreateDirectory(survivorOld);
                Directory.CreateDirectory(deletedOld);
                File.WriteAllText(
                    Path.Combine(survivorOld, "room.json"),
                    "{\"room_id\":\"room.survivor\"}");
                File.WriteAllText(
                    Path.Combine(deletedOld, "room.json"),
                    "{\"room_id\":\"room.deleted\"}");
                File.WriteAllText(
                    survivorOld + ".meta",
                    "fileFormatVersion: 2\nguid: survivor-folder-guid\n");
                File.WriteAllText(
                    deletedOld + ".meta",
                    "fileFormatVersion: 2\nguid: deleted-folder-guid\n");

                LevelRoomAuthoring2D room = roomObject.AddComponent<LevelRoomAuthoring2D>();
                room.ConfigureForTests(
                    "room.survivor",
                    new Vector2Int(1, 0),
                    Vector2.one,
                    Vector2Int.one,
                    null);

                IReadOnlyDictionary<string, string> result =
                    LevelGridV2RoomFolderMigration.Prepare(
                        new[] { room },
                        roomsRoot);
                string movedFolder = result["room.survivor"];
                string metadata = File.ReadAllText(movedFolder + ".meta");

                Assert.That(metadata, Does.Contain("survivor-folder-guid"));
                Assert.That(metadata, Does.Not.Contain("deleted-folder-guid"));
                Assert.That(File.Exists(survivorOld + ".meta"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
                DeleteRoot(root);
            }
        }

        private static string TempRoot(string suffix)
        {
            return Path.Combine(
                Path.GetTempPath(),
                "shooter-mover-grid-v2-" + suffix + "-" + Guid.NewGuid().ToString("N"));
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
#endif

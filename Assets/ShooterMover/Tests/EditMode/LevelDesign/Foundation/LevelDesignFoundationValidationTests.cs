#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.LevelDesign.Foundation
{
    public sealed class LevelDesignFoundationValidationTests
    {
        private const string PrefabRoot =
            "Assets/ShooterMover/ContentPackages/LevelDesign/Foundation/Prefabs/";

        [Test]
        public void ValidFoundation_HasNoErrors()
        {
            LevelRoomRecord roomA = Room(
                "room.alpha",
                new Rect(0f, 0f, 10f, 10f),
                new Vector2Int(0, 0));
            LevelRoomRecord roomB = Room(
                "room.beta",
                new Rect(10f, 0f, 10f, 10f),
                new Vector2Int(1, 0));
            LevelPlacementRecord sourceSocket = Socket(
                "transition.alpha-exit",
                roomA.RoomId,
                "socket.alpha-exit",
                LevelPlacementKind.Exit);
            LevelPlacementRecord destinationSocket = Socket(
                "transition.beta-entry",
                roomB.RoomId,
                "socket.beta-entry",
                LevelPlacementKind.Entry);
            LevelDoorRecord door = new LevelDoorRecord(
                "door.alpha-beta",
                roomA.RoomId,
                roomB.RoomId,
                sourceSocket.SocketId,
                destinationSocket.SocketId,
                Vector2Int.zero,
                Vector2Int.right,
                true,
                LevelDoorTravelPolicy.Bidirectional,
                true,
                true,
                true,
                true,
                true,
                "door");

            LevelDesignValidationResult result =
                LevelDesignFoundationValidator.Validate(
                    "level.foundation-test",
                    new[] { roomA, roomB },
                    new[] { sourceSocket, destinationSocket },
                    new[] { door },
                    Array.Empty<LevelVoidRecord>());

            Assert.That(result.ErrorCount, Is.Zero);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void DuplicateIds_AreReportedAtEveryLocation()
        {
            LevelRoomRecord room = Room(
                "room.duplicate",
                new Rect(0f, 0f, 5f, 5f),
                Vector2Int.zero);
            LevelPlacementRecord placement = Placement(
                "room.duplicate",
                "room.duplicate",
                "socket.enemy",
                LevelPlacementKind.EnemySpawn,
                new Rect(1f, 1f, 1f, 1f));

            LevelDesignValidationResult result =
                LevelDesignFoundationValidator.Validate(
                    "level.duplicates",
                    new[] { room },
                    new[] { placement },
                    Array.Empty<LevelDoorRecord>(),
                    Array.Empty<LevelVoidRecord>());

            Assert.That(
                result.Issues.Count(
                    issue => issue.Code
                        == LevelDesignValidationCode.DuplicateAuthoredIdentity),
                Is.EqualTo(2));
        }

        [Test]
        public void DuplicateSocketIds_AreRejected()
        {
            LevelRoomRecord room = Room(
                "room.socket-duplicates",
                new Rect(0f, 0f, 10f, 10f),
                Vector2Int.zero);
            LevelPlacementRecord first = Socket(
                "transition.first",
                room.RoomId,
                "socket.shared",
                LevelPlacementKind.Entry);
            LevelPlacementRecord second = Socket(
                "transition.second",
                room.RoomId,
                "socket.shared",
                LevelPlacementKind.Exit);

            LevelDesignValidationResult result =
                LevelDesignFoundationValidator.Validate(
                    "level.socket-duplicates",
                    new[] { room },
                    new[] { first, second },
                    Array.Empty<LevelDoorRecord>(),
                    Array.Empty<LevelVoidRecord>());

            Assert.That(
                result.Issues.Count(
                    issue => issue.Code
                        == LevelDesignValidationCode.DuplicateSocketIdentity),
                Is.EqualTo(2));
        }

        [Test]
        public void MissingDefinitionPrefabAndRoom_AreActionable()
        {
            LevelPlacementRecord placement = new LevelPlacementRecord(
                "spawn.enemy-missing",
                "socket.enemy-missing",
                LevelPlacementKind.EnemySpawn,
                "room.missing",
                Vector2Int.zero,
                new Rect(0f, 0f, 1f, 1f),
                false,
                false,
                false,
                false,
                LevelCollisionPolicy.Solid,
                LevelRestartPolicy.ResetProjection,
                string.Empty,
                false,
                0,
                "enemy-spawn");

            LevelDesignValidationResult result =
                LevelDesignFoundationValidator.Validate(
                    "level.missing-references",
                    Array.Empty<LevelRoomRecord>(),
                    new[] { placement },
                    Array.Empty<LevelDoorRecord>(),
                    Array.Empty<LevelVoidRecord>());

            AssertCode(result, LevelDesignValidationCode.MissingRoomReference);
            AssertCode(result, LevelDesignValidationCode.MissingDefinitionReference);
            AssertCode(result, LevelDesignValidationCode.MissingPrefabReference);
            AssertCode(result, LevelDesignValidationCode.MissingCollider);
        }

        [Test]
        public void RoomAndSolidPlacementOverlaps_AreRejected()
        {
            LevelRoomRecord roomA = Room(
                "room.overlap-a",
                new Rect(0f, 0f, 10f, 10f),
                Vector2Int.zero);
            LevelRoomRecord roomB = Room(
                "room.overlap-b",
                new Rect(5f, 0f, 10f, 10f),
                Vector2Int.right);
            LevelPlacementRecord left = Placement(
                "prop.left",
                roomA.RoomId,
                "socket.left",
                LevelPlacementKind.PropPlacement,
                new Rect(1f, 1f, 2f, 2f));
            LevelPlacementRecord right = Placement(
                "prop.right",
                roomA.RoomId,
                "socket.right",
                LevelPlacementKind.PropPlacement,
                new Rect(2f, 1f, 2f, 2f));

            LevelDesignValidationResult result =
                LevelDesignFoundationValidator.Validate(
                    "level.overlaps",
                    new[] { roomA, roomB },
                    new[] { left, right },
                    Array.Empty<LevelDoorRecord>(),
                    Array.Empty<LevelVoidRecord>());

            AssertCode(result, LevelDesignValidationCode.RoomOverlap);
            AssertCode(result, LevelDesignValidationCode.PlacementOverlap);
        }

        [Test]
        public void InvalidDoorConnection_ReportsRoomsGridAndPackage()
        {
            LevelRoomRecord room = Room(
                "room.single",
                new Rect(0f, 0f, 10f, 10f),
                Vector2Int.zero);
            LevelDoorRecord door = new LevelDoorRecord(
                "door.invalid",
                room.RoomId,
                room.RoomId,
                "socket.source",
                "socket.destination",
                Vector2Int.zero,
                new Vector2Int(3, 3),
                true,
                LevelDoorTravelPolicy.Bidirectional,
                false,
                false,
                false,
                false,
                false,
                "door-invalid");

            LevelDesignValidationResult result =
                LevelDesignFoundationValidator.Validate(
                    "level.invalid-door",
                    new[] { room },
                    Array.Empty<LevelPlacementRecord>(),
                    new[] { door },
                    Array.Empty<LevelVoidRecord>());

            AssertCode(result, LevelDesignValidationCode.InvalidRoomConnection);
            AssertCode(result, LevelDesignValidationCode.MissingDoorPackage);
            AssertCode(result, LevelDesignValidationCode.MissingDoorPresentation);
            AssertCode(result, LevelDesignValidationCode.MissingDoorCollision);
        }

        [Test]
        public void SpawnInsideVoid_IsRejected()
        {
            LevelRoomRecord room = Room(
                "room.void-test",
                new Rect(0f, 0f, 10f, 10f),
                Vector2Int.zero);
            LevelPlacementRecord spawn = Placement(
                "spawn.enemy-void",
                room.RoomId,
                "socket.enemy-void",
                LevelPlacementKind.EnemySpawn,
                new Rect(2f, 2f, 1f, 1f));
            LevelVoidRecord region = new LevelVoidRecord(
                "void.central",
                room.RoomId,
                new Rect(1f, 1f, 4f, 4f),
                true,
                true,
                LevelVoidEffect.RespawnAtCheckpoint,
                LevelRestartPolicy.ResetProjection,
                "void");

            LevelDesignValidationResult result =
                LevelDesignFoundationValidator.Validate(
                    "level.void-test",
                    new[] { room },
                    new[] { spawn },
                    Array.Empty<LevelDoorRecord>(),
                    new[] { region });

            AssertCode(result, LevelDesignValidationCode.SpawnInsideVoid);
        }

        [Test]
        public void AuthoredIdentity_DoesNotDependOnGameObjectNameOrTransform()
        {
            GameObject roomObject = new GameObject("Initial Name");
            try
            {
                LevelRoomAuthoring2D room =
                    roomObject.AddComponent<LevelRoomAuthoring2D>();
                room.ConfigureForTests(
                    "room.stable-authored",
                    Vector2Int.zero,
                    Vector2.one,
                    Vector2Int.one,
                    null);

                string before = room.BuildRecord().RoomId;
                roomObject.name = "Renamed Completely";
                roomObject.transform.position = new Vector3(123f, -41f, 0f);
                roomObject.transform.SetSiblingIndex(0);
                string after = room.BuildRecord().RoomId;

                Assert.That(after, Is.EqualTo(before));
                Assert.That(after, Is.EqualTo("room.stable-authored"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
            }
        }

        [Test]
        public void AuthoringPrefabs_LoadWithExpectedFoundationComponents()
        {
            AssertPrefabComponent(
                "LevelDesignSceneRoot.prefab",
                typeof(LevelDesignSceneAuthoringRoot2D));
            AssertPrefabComponent(
                "RoomAnchor.prefab",
                typeof(LevelRoomAuthoring2D));
            AssertPrefabComponent(
                "ConfiguredDoor.prefab",
                typeof(LevelDoorConnectionAuthoring2D));
            AssertPrefabComponent(
                "PlayerSpawn.prefab",
                typeof(LevelPlacementAuthoring2D));
            AssertPrefabComponent(
                "EnemySpawn.prefab",
                typeof(LevelPlacementAuthoring2D));
            AssertPrefabComponent(
                "PropPlacement.prefab",
                typeof(LevelPlacementAuthoring2D));
            AssertPrefabComponent(
                "PickupSpawn.prefab",
                typeof(LevelPlacementAuthoring2D));
            AssertPrefabComponent(
                "RewardSocket.prefab",
                typeof(LevelPlacementAuthoring2D));
            AssertPrefabComponent(
                "EntryExit.prefab",
                typeof(LevelPlacementAuthoring2D));
            AssertPrefabComponent(
                "VoidRegion.prefab",
                typeof(LevelVoidRegionAuthoring2D));

            GameObject configuredDoor = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabRoot + "ConfiguredDoor.prefab");
            Assert.That(configuredDoor, Is.Not.Null);
            MonoBehaviour foundationAdapter = configuredDoor
                .GetComponents<MonoBehaviour>()
                .FirstOrDefault(component => component != null
                    && component.GetType().FullName
                    == "ShooterMover.ContentPackages.LevelDesign.Foundation.ConfiguredDoorAuthoring2D");
            Assert.That(
                foundationAdapter,
                Is.Not.Null,
                "Configured door prefab must consume the LEVELDES door package adapter.");
            SerializedProperty openDoorSprite = new SerializedObject(foundationAdapter)
                .FindProperty("openDoorSprite");
            Assert.That(openDoorSprite, Is.Not.Null);
            Assert.That(
                openDoorSprite.objectReferenceValue,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/ShooterMover/Art/Environment/Doors/UserIntake/door_open.png")),
                "Configured door prefab must reference the supplied open-door art.");
            Assert.That(
                configuredDoor.GetComponents<MonoBehaviour>()
                    .Any(component => component != null
                        && component.GetType().FullName
                        == "ShooterMover.ContentPackages.Environment.Doors.DoorController2D"),
                Is.True,
                "Configured door prefab must consume DOOR-001.");
            Assert.That(
                configuredDoor.GetComponents<MonoBehaviour>()
                    .Any(component => component != null
                        && component.GetType().FullName
                        == "ShooterMover.UnityAdapters.Authoring.PlacedObjectAuthoring2D"),
                Is.True,
                "Configured door prefab must consume OBJ-001.");
        }

        [Test]
        public void DoorOpenAsset_IsByteIdenticalToExactIntakeBlob()
        {
            const string assetPath =
                "Assets/ShooterMover/Art/Environment/Doors/UserIntake/door_open.png";
            byte[] bytes = File.ReadAllBytes(assetPath);
            Assert.That(
                ComputeGitBlobSha(bytes),
                Is.EqualTo("4c0388ff741c23d9e1eb4ff6666d0f4cf669f969"));

            Sprite imported = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Assert.That(imported, Is.Not.Null);
            Assert.That(imported.texture, Is.Not.Null);
            Assert.That(imported.pivot.x, Is.EqualTo(imported.rect.width * 0.5f));
            Assert.That(imported.pivot.y, Is.EqualTo(imported.rect.height * 0.5f));
            Assert.That(imported.pixelsPerUnit, Is.EqualTo(100f));
        }

        [Test]
        public void ProductionRuntimeSources_AvoidGlobalAndNameBasedAuthority()
        {
            string root = Path.Combine(
                Application.dataPath,
                "ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign");
            string source = string.Join(
                "\n",
                Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));

            string[] forbidden =
            {
                "FindFirstObjectByType",
                "FindObjectsByType",
                "FindObjectOfType",
                "GameObject.Find",
                "CompareTag",
                "SceneManager.LoadScene",
                "Stage1VisibleSlice",
                "UnityEngine.Random",
                "GetInstanceID",
                "siblingIndex",
            };

            for (int index = 0; index < forbidden.Length; index++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbidden[index]),
                    forbidden[index]);
            }

            Assert.That(source, Does.Contain("StableId.TryParse"));
            Assert.That(source, Does.Contain("GetComponentsInChildren"));
            Assert.That(source, Does.Contain("ILevelDoorPackageAdapter"));
        }

        private static LevelRoomRecord Room(
            string id,
            Rect bounds,
            Vector2Int grid)
        {
            return new LevelRoomRecord(
                id,
                grid,
                Vector2.one,
                Vector2Int.one,
                LevelRoomAlignment.GridOrigin,
                bounds,
                true,
                0,
                grid,
                true,
                id);
        }

        private static LevelPlacementRecord Socket(
            string id,
            string roomId,
            string socketId,
            LevelPlacementKind kind)
        {
            return new LevelPlacementRecord(
                id,
                socketId,
                kind,
                roomId,
                Vector2Int.zero,
                new Rect(0f, 0f, 0.25f, 0.25f),
                false,
                false,
                false,
                false,
                LevelCollisionPolicy.None,
                LevelRestartPolicy.Persistent,
                string.Empty,
                true,
                0,
                id);
        }

        private static LevelPlacementRecord Placement(
            string id,
            string roomId,
            string socketId,
            LevelPlacementKind kind,
            Rect bounds)
        {
            return new LevelPlacementRecord(
                id,
                socketId,
                kind,
                roomId,
                Vector2Int.zero,
                bounds,
                true,
                true,
                true,
                true,
                LevelCollisionPolicy.Solid,
                LevelRestartPolicy.ResetProjection,
                string.Empty,
                false,
                0,
                id);
        }

        private static string ComputeGitBlobSha(byte[] bytes)
        {
            byte[] prefix = Encoding.UTF8.GetBytes(
                "blob " + bytes.Length + "\0");
            byte[] input = new byte[prefix.Length + bytes.Length];
            Buffer.BlockCopy(prefix, 0, input, 0, prefix.Length);
            Buffer.BlockCopy(bytes, 0, input, prefix.Length, bytes.Length);

            using (SHA1 sha = SHA1.Create())
            {
                return string.Concat(
                    sha.ComputeHash(input)
                        .Select(value => value.ToString("x2")));
            }
        }

        private static void AssertCode(
            LevelDesignValidationResult result,
            LevelDesignValidationCode code)
        {
            Assert.That(
                result.Issues.Any(issue => issue.Code == code),
                Is.True,
                "Expected validation code " + code + ".");
        }

        private static void AssertPrefabComponent(
            string fileName,
            Type componentType)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabRoot + fileName);
            Assert.That(prefab, Is.Not.Null, fileName);
            Assert.That(prefab.GetComponent(componentType), Is.Not.Null, fileName);
        }
    }
}
#endif

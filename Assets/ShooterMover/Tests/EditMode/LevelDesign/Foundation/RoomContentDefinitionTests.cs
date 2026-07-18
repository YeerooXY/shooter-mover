#if UNITY_EDITOR
using NUnit.Framework;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.LevelDesign.Foundation
{
    public sealed class RoomContentDefinitionTests
    {
        [Test]
        public void ValidRoomContent_PreservesPortableIdsAndUnityPrefabProjection()
        {
            GameObject prefab = new GameObject("moving_droid");
            RoomContentDefinition2D definition =
                ScriptableObject.CreateInstance<RoomContentDefinition2D>();
            try
            {
                var placement = new RoomContentPlacement2D();
                placement.ConfigureForTests(
                    "spawn.test-moving-droid",
                    LevelPlacementKind.EnemySpawn,
                    "enemy.mobile-blaster-droid",
                    prefab,
                    new Vector2(2f, 3f),
                    15f);
                definition.ConfigureForTests(
                    "room.test-arena",
                    "TEST ARENA",
                    new Vector2(-10f, 0f),
                    new Vector2(10f, 0f),
                    new[] { placement });

                Assert.That(definition.TryValidate(out string error), Is.True, error);
                Assert.That(definition.RoomStableIdText, Is.EqualTo("room.test-arena"));
                Assert.That(definition.Placements[0].ContentStableIdText,
                    Is.EqualTo("enemy.mobile-blaster-droid"));
                Assert.That(definition.Placements[0].Prefab, Is.SameAs(prefab));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void DuplicatePlacementIdentity_FailsClosed()
        {
            GameObject prefab = new GameObject("moving_droid");
            RoomContentDefinition2D definition =
                ScriptableObject.CreateInstance<RoomContentDefinition2D>();
            try
            {
                var first = new RoomContentPlacement2D();
                first.ConfigureForTests(
                    "spawn.duplicate",
                    LevelPlacementKind.EnemySpawn,
                    "enemy.mobile-blaster-droid",
                    prefab,
                    Vector2.zero,
                    0f);
                var second = new RoomContentPlacement2D();
                second.ConfigureForTests(
                    "spawn.duplicate",
                    LevelPlacementKind.EnemySpawn,
                    "enemy.mobile-blaster-droid",
                    prefab,
                    Vector2.one,
                    0f);
                definition.ConfigureForTests(
                    "room.test-arena",
                    "TEST ARENA",
                    Vector2.zero,
                    Vector2.zero,
                    new[] { first, second });

                Assert.That(definition.TryValidate(out string error), Is.False);
                Assert.That(error, Is.EqualTo(
                    "room-content-instance-id-duplicate:spawn.duplicate"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(prefab);
            }
        }
    }
}
#endif

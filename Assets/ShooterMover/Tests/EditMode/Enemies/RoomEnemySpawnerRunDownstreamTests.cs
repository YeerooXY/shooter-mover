using System;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class RoomEnemySpawnerRunDownstreamTests
    {
        [Test]
        public void ConfigureRunDownstream_AcceptsOneExactTypedComposition()
        {
            GameObject owner = new GameObject("room-enemy-spawner-test");
            try
            {
                RoomEnemySpawner2D spawner = owner.AddComponent<RoomEnemySpawner2D>();
                var consumers = new TypedNoOpConsumers();

                Assert.DoesNotThrow(() => spawner.ConfigureRunDownstream(
                    StableId.Parse("run.test-exact-composition"),
                    consumers,
                    consumers,
                    consumers));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ConfigureRunDownstream_RejectsSecondWriteAuthority()
        {
            GameObject owner = new GameObject("room-enemy-spawner-test");
            try
            {
                RoomEnemySpawner2D spawner = owner.AddComponent<RoomEnemySpawner2D>();
                var first = new TypedNoOpConsumers();
                var second = new TypedNoOpConsumers();
                spawner.ConfigureRunDownstream(
                    StableId.Parse("run.test-first-composition"),
                    first,
                    first,
                    first);

                InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                    () => spawner.ConfigureRunDownstream(
                        StableId.Parse("run.test-second-composition"),
                        second,
                        second,
                        second));

                StringAssert.Contains("already frozen", error.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ConfigureRunDownstream_RejectsMissingExactRunIdentity()
        {
            GameObject owner = new GameObject("room-enemy-spawner-test");
            try
            {
                RoomEnemySpawner2D spawner = owner.AddComponent<RoomEnemySpawner2D>();
                var consumers = new TypedNoOpConsumers();

                Assert.Throws<ArgumentNullException>(() => spawner.ConfigureRunDownstream(
                    null,
                    consumers,
                    consumers,
                    consumers));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private sealed class TypedNoOpConsumers :
            IEnemyExperienceFactConsumerV1,
            IEnemyDropFactConsumerV1,
            IEnemyKillStatFactConsumerV1
        {
            public void Consume(EnemyDeathFactV1 fact)
            {
                if (fact == null) throw new ArgumentNullException(nameof(fact));
            }
        }
    }
}
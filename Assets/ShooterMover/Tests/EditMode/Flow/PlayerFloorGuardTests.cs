using NUnit.Framework;
using ShooterMover.UI.Game;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Flow
{
    public sealed class PlayerFloorGuardTests
    {
        private GameObject player;
        private Rigidbody2D body;
        private PlayerFloorGuard guard;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("Player Floor Guard Test");
            body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            CircleCollider2D collider = player.AddComponent<CircleCollider2D>();
            collider.radius = 0.4f;
            guard = player.AddComponent<PlayerFloorGuard>();
            guard.Bind(body, collider);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
        }

        [Test]
        public void GuardLimitsLivePlayerVelocity()
        {
            guard.LoadFloor(
                new[] { Vector2Int.zero },
                Vector2.zero);
            body.linearVelocity = new Vector2(100f, 0f);

            guard.ApplyMovement(0.02f);

            Assert.That(body.linearVelocity.x, Is.LessThan(10f));
            Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void PlayerCanMoveBackOutAfterCrossingBoundary()
        {
            guard.LoadFloor(
                new[] { Vector2Int.zero },
                Vector2.zero);
            body.position = new Vector2(0.2f, 0f);
            body.linearVelocity = new Vector2(-2f, 0f);

            guard.ApplyMovement(0.02f);

            Assert.That(body.position, Is.EqualTo(Vector2.zero));
            Assert.That(body.linearVelocity.x, Is.LessThan(-1.9f));
            Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void RoomLoadMovesInvalidSpawnToNearestFloor()
        {
            guard.LoadFloor(
                new[]
                {
                    new Vector2Int(-2, 0),
                    new Vector2Int(3, 0),
                },
                new Vector2(2.6f, 1f));

            Assert.That(body.position, Is.EqualTo(new Vector2(3f, 0f)));
        }

        [Test]
        public void EmptyRoomAnchorsPlayerAtRequestedPosition()
        {
            Vector2 requested = new Vector2(4f, -2f);
            guard.LoadFloor(new Vector2Int[0], requested);
            body.linearVelocity = new Vector2(5f, 2f);

            guard.ApplyMovement(0.02f);

            Assert.That(body.position, Is.EqualTo(requested));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        }
    }
}

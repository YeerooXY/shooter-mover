using NUnit.Framework;
using ShooterMover.UI.Game;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Flow
{
    public sealed class FloorGridTests
    {
        [Test]
        public void EmptyFloorBlocksMovement()
        {
            var floor = new FloorGrid(new Vector2Int[0]);

            Vector2 velocity = floor.LimitVelocity(
                Vector2.zero,
                new Vector2(12f, 3f),
                0.02f,
                0.4f);

            Assert.That(floor.HasCells, Is.False);
            Assert.That(velocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void AdjacentCellsAllowCrossingTheirSharedEdge()
        {
            var floor = new FloorGrid(new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
            });
            Vector2 requested = new Vector2(20f, 0f);

            Vector2 velocity = floor.LimitVelocity(
                Vector2.zero,
                requested,
                0.02f,
                0.4f);

            Assert.That(velocity.x, Is.EqualTo(requested.x).Within(0.001f));
            Assert.That(velocity.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void MissingCellStopsDashBeforeGap()
        {
            var floor = new FloorGrid(new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(2, 0),
            });

            Vector2 velocity = floor.LimitVelocity(
                Vector2.zero,
                new Vector2(100f, 0f),
                0.02f,
                0.4f);
            Vector2 resultingPosition = velocity * 0.02f;

            Assert.That(resultingPosition.x, Is.LessThan(0.2f));
            Assert.That(resultingPosition.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void CompleteCircleMustRemainOnFloor()
        {
            var floor = new FloorGrid(new[]
            {
                Vector2Int.zero,
            });

            Assert.That(floor.FitsCircle(Vector2.zero, 0.4f), Is.True);
            Assert.That(floor.FitsCircle(new Vector2(0.2f, 0f), 0.4f), Is.False);
        }

        [Test]
        public void DiagonalMovementSlidesAlongSupportedEdge()
        {
            var floor = new FloorGrid(new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
            });

            Vector2 velocity = floor.LimitVelocity(
                new Vector2(0f, 0.1f),
                new Vector2(20f, 20f),
                0.02f,
                0.4f);

            Assert.That(velocity.x, Is.GreaterThan(0f));
            Assert.That(velocity.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void EqualSpawnChoicesUseStableGridOrder()
        {
            var floor = new FloorGrid(new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
            });

            Vector2 position;
            bool found = floor.TryFindNearestPosition(
                Vector2.zero,
                0.4f,
                out position);

            Assert.That(found, Is.True);
            Assert.That(position, Is.EqualTo(new Vector2(-1f, 0f)));
        }

        [Test]
        public void UnreasonableMovementFailsClosed()
        {
            var floor = new FloorGrid(new[]
            {
                Vector2Int.zero,
            });

            Vector2 velocity = floor.LimitVelocity(
                Vector2.zero,
                new Vector2(100000f, 0f),
                0.02f,
                0.4f);

            Assert.That(velocity, Is.EqualTo(Vector2.zero));
        }
    }
}

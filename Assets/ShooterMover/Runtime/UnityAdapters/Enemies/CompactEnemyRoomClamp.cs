using System;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Keeps a kinematic compact enemy inside its authored room without turning the complete
    /// room outline into a wall collider. This preserves door traversal for the player while
    /// preventing enemy steering from escaping the active room presentation.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class CompactEnemyRoomClamp : MonoBehaviour
    {
        private Transform roomRoot;
        private Rigidbody2D body;
        private RoomBounds roomBounds;
        private float clearanceX;
        private float clearanceY;
        private bool configured;

        public void Configure(
            LevelRooms roomOwner,
            StableId roomStableId)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "compact-enemy-room-clamp-duplicate-configuration");
            }
            if (roomOwner == null)
            {
                throw new ArgumentNullException(nameof(roomOwner));
            }
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }
            if (roomOwner.Definition == null)
            {
                throw new InvalidOperationException(
                    "compact-enemy-room-clamp-definition-missing");
            }

            AuthorableRoomDefinition room =
                roomOwner.Definition.GetRoom(roomStableId);
            roomBounds = room.Bounds;
            roomRoot = transform.parent;
            if (roomRoot == null)
            {
                throw new InvalidOperationException(
                    "compact-enemy-room-clamp-root-missing");
            }

            body = GetComponent<Rigidbody2D>();
            if (body == null)
            {
                throw new InvalidOperationException(
                    "compact-enemy-room-clamp-body-missing");
            }

            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                clearanceX = collider.radius + Mathf.Abs(collider.offset.x);
                clearanceY = collider.radius + Mathf.Abs(collider.offset.y);
            }

            configured = true;
            ClampToRoom();
        }

        private void FixedUpdate()
        {
            if (configured)
            {
                ClampToRoom();
            }
        }

        private void ClampToRoom()
        {
            Vector3 local = roomRoot.InverseTransformPoint(
                new Vector3(body.position.x, body.position.y, transform.position.z));
            float centerX = (float)roomBounds.Center.X;
            float centerY = (float)roomBounds.Center.Y;
            float halfWidth = (float)roomBounds.Size.X * 0.5f;
            float halfHeight = (float)roomBounds.Size.Y * 0.5f;
            float minX = centerX - halfWidth + clearanceX;
            float maxX = centerX + halfWidth - clearanceX;
            float minY = centerY - halfHeight + clearanceY;
            float maxY = centerY + halfHeight - clearanceY;

            if (minX > maxX)
            {
                minX = centerX;
                maxX = centerX;
            }
            if (minY > maxY)
            {
                minY = centerY;
                maxY = centerY;
            }

            float x = Mathf.Clamp(local.x, minX, maxX);
            float y = Mathf.Clamp(local.y, minY, maxY);
            if (Mathf.Approximately(x, local.x)
                && Mathf.Approximately(y, local.y))
            {
                return;
            }

            Vector3 world = roomRoot.TransformPoint(
                new Vector3(x, y, local.z));
            body.position = new Vector2(world.x, world.y);
            body.linearVelocity = Vector2.zero;
        }
    }
}

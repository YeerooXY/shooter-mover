using System;
using ShooterMover.Domain.Movement;
using UnityEngine;

namespace ShooterMover.Presentation.VisibleSliceCameraReadability
{
    /// <summary>
    /// Immutable result of one camera framing calculation.
    /// </summary>
    public sealed class VisibleSliceCameraFrame
    {
        public VisibleSliceCameraFrame(
            Vector2 center,
            Vector2 halfExtents,
            Vector2 actorViewportPosition,
            bool roomFullyContainsView,
            bool actorInsideHudSafeViewport,
            bool usedThrusterLookAhead)
        {
            Center = center;
            HalfExtents = halfExtents;
            ActorViewportPosition = actorViewportPosition;
            RoomFullyContainsView = roomFullyContainsView;
            ActorInsideHudSafeViewport = actorInsideHudSafeViewport;
            UsedThrusterLookAhead = usedThrusterLookAhead;
        }

        public Vector2 Center { get; }

        public Vector2 HalfExtents { get; }

        public Vector2 ActorViewportPosition { get; }

        public bool RoomFullyContainsView { get; }

        public bool ActorInsideHudSafeViewport { get; }

        public bool UsedThrusterLookAhead { get; }

        public Rect WorldViewport
        {
            get
            {
                return Rect.MinMaxRect(
                    Center.x - HalfExtents.x,
                    Center.y - HalfExtents.y,
                    Center.x + HalfExtents.x,
                    Center.y + HalfExtents.y);
            }
        }
    }

    /// <summary>
    /// Stateless orthographic framing solver. It reads immutable actor/thruster data
    /// and returns a camera center; it cannot mutate movement or any global setting.
    /// </summary>
    public static class VisibleSliceCameraFrameSolver
    {
        private const float DirectionEpsilon = 0.000001f;

        public static VisibleSliceCameraFrame Solve(
            VisibleSliceCameraConfiguration configuration,
            Vector2 actorWorldPosition,
            ThrusterStatusSnapshot thrusterStatus,
            float aspect)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            ValidateFinite(actorWorldPosition.x, nameof(actorWorldPosition));
            ValidateFinite(actorWorldPosition.y, nameof(actorWorldPosition));
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(aspect),
                    aspect,
                    "Camera aspect must be finite and positive.");
            }

            Vector2 halfExtents = new Vector2(
                configuration.OrthographicSize * aspect,
                configuration.OrthographicSize);

            Vector2 requestedLookAhead = ResolveThrusterLookAhead(
                configuration,
                thrusterStatus);
            Vector2 safeLookAhead = ClampLookAheadToHudSafeViewport(
                requestedLookAhead,
                halfExtents,
                configuration.HudSafeViewport);
            Vector2 desiredCenter = actorWorldPosition + safeLookAhead;

            bool containsHorizontal;
            bool containsVertical;
            Rect room = configuration.RoomBounds;
            Vector2 center = new Vector2(
                ClampCenterToRoom(
                    desiredCenter.x,
                    room.xMin,
                    room.xMax,
                    halfExtents.x,
                    out containsHorizontal),
                ClampCenterToRoom(
                    desiredCenter.y,
                    room.yMin,
                    room.yMax,
                    halfExtents.y,
                    out containsVertical));

            Vector2 actorViewportPosition = new Vector2(
                0.5f + ((actorWorldPosition.x - center.x) / (2f * halfExtents.x)),
                0.5f + ((actorWorldPosition.y - center.y) / (2f * halfExtents.y)));

            bool actorInsideSafeViewport = ContainsInclusive(
                configuration.HudSafeViewport,
                actorViewportPosition);

            return new VisibleSliceCameraFrame(
                center,
                halfExtents,
                actorViewportPosition,
                containsHorizontal && containsVertical,
                actorInsideSafeViewport,
                safeLookAhead.sqrMagnitude > DirectionEpsilon);
        }

        private static Vector2 ResolveThrusterLookAhead(
            VisibleSliceCameraConfiguration configuration,
            ThrusterStatusSnapshot status)
        {
            if (status == null
                || !status.IsRuntimeAvailable
                || !status.IsBursting
                || configuration.ThrusterLookAheadWorldUnits <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 direction = new Vector2(
                (float)status.BurstDirectionX,
                (float)status.BurstDirectionY);
            if (direction.sqrMagnitude <= DirectionEpsilon)
            {
                direction = new Vector2(
                    (float)status.VelocityX,
                    (float)status.VelocityY);
            }

            if (direction.sqrMagnitude <= DirectionEpsilon)
            {
                return Vector2.zero;
            }

            return direction.normalized * configuration.ThrusterLookAheadWorldUnits;
        }

        private static Vector2 ClampLookAheadToHudSafeViewport(
            Vector2 requestedOffset,
            Vector2 halfExtents,
            Rect safeViewport)
        {
            float minimumX = 2f * halfExtents.x * (0.5f - safeViewport.xMax);
            float maximumX = 2f * halfExtents.x * (0.5f - safeViewport.xMin);
            float minimumY = 2f * halfExtents.y * (0.5f - safeViewport.yMax);
            float maximumY = 2f * halfExtents.y * (0.5f - safeViewport.yMin);

            return new Vector2(
                Mathf.Clamp(requestedOffset.x, minimumX, maximumX),
                Mathf.Clamp(requestedOffset.y, minimumY, maximumY));
        }

        private static float ClampCenterToRoom(
            float desiredCenter,
            float minimum,
            float maximum,
            float halfExtent,
            out bool roomContainsView)
        {
            float roomSpan = maximum - minimum;
            float viewSpan = 2f * halfExtent;
            roomContainsView = roomSpan + DirectionEpsilon >= viewSpan;
            if (!roomContainsView)
            {
                return (minimum + maximum) * 0.5f;
            }

            return Mathf.Clamp(
                desiredCenter,
                minimum + halfExtent,
                maximum - halfExtent);
        }

        private static bool ContainsInclusive(Rect rect, Vector2 point)
        {
            return point.x >= rect.xMin
                && point.x <= rect.xMax
                && point.y >= rect.yMin
                && point.y <= rect.yMax;
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "World positions must be finite.");
            }
        }
    }
}

using System;
using UnityEngine;

namespace ShooterMover.Presentation.VisibleSliceCameraReadability
{
    /// <summary>
    /// Immutable task-local camera/readability configuration. It contains presentation
    /// geometry only and has no authority over movement, combat, quality, or persistence.
    /// </summary>
    [Serializable]
    public sealed class VisibleSliceCameraConfiguration
    {
        public VisibleSliceCameraConfiguration(
            float orthographicSize,
            float referenceAspect,
            Rect roomBounds,
            Rect hudSafeViewport,
            float warningEdgeMarginNormalized,
            float thrusterLookAheadWorldUnits)
        {
            ValidateFinitePositive(orthographicSize, nameof(orthographicSize));
            ValidateFinitePositive(referenceAspect, nameof(referenceAspect));
            ValidateRect(roomBounds, nameof(roomBounds), false);
            ValidateRect(hudSafeViewport, nameof(hudSafeViewport), true);
            ValidateFiniteRange(
                warningEdgeMarginNormalized,
                0f,
                0.45f,
                nameof(warningEdgeMarginNormalized));
            ValidateFiniteNonNegative(
                thrusterLookAheadWorldUnits,
                nameof(thrusterLookAheadWorldUnits));

            OrthographicSize = orthographicSize;
            ReferenceAspect = referenceAspect;
            RoomBounds = roomBounds;
            HudSafeViewport = hudSafeViewport;
            WarningEdgeMarginNormalized = warningEdgeMarginNormalized;
            ThrusterLookAheadWorldUnits = thrusterLookAheadWorldUnits;

            Rect warningViewport = WarningViewport;
            if (warningViewport.width <= 0f || warningViewport.height <= 0f)
            {
                throw new ArgumentException(
                    "HUD-safe margins and warning-edge margins must leave a non-empty warning viewport.");
            }
        }

        public float OrthographicSize { get; }

        public float ReferenceAspect { get; }

        public Rect RoomBounds { get; }

        /// <summary>
        /// Normalized viewport region kept clear of reserved HUD margins.
        /// </summary>
        public Rect HudSafeViewport { get; }

        public float WarningEdgeMarginNormalized { get; }

        public float ThrusterLookAheadWorldUnits { get; }

        /// <summary>
        /// Normalized viewport region in which an edge warning may be placed.
        /// It is the intersection of the HUD-safe region and the explicit edge inset.
        /// </summary>
        public Rect WarningViewport
        {
            get
            {
                float xMin = Mathf.Max(HudSafeViewport.xMin, WarningEdgeMarginNormalized);
                float yMin = Mathf.Max(HudSafeViewport.yMin, WarningEdgeMarginNormalized);
                float xMax = Mathf.Min(HudSafeViewport.xMax, 1f - WarningEdgeMarginNormalized);
                float yMax = Mathf.Min(HudSafeViewport.yMax, 1f - WarningEdgeMarginNormalized);
                return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }
        }

        public static VisibleSliceCameraConfiguration CreateDefault(Rect roomBounds)
        {
            return new VisibleSliceCameraConfiguration(
                5.625f,
                16f / 9f,
                roomBounds,
                Rect.MinMaxRect(0.08f, 0.10f, 0.92f, 0.88f),
                0.06f,
                1.25f);
        }

        public float ResolveAspect(int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pixelWidth),
                    pixelWidth,
                    "Pixel width must be positive.");
            }

            if (pixelHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pixelHeight),
                    pixelHeight,
                    "Pixel height must be positive.");
            }

            return (float)pixelWidth / pixelHeight;
        }

        private static void ValidateRect(Rect value, string parameterName, bool normalized)
        {
            ValidateFinite(value.xMin, parameterName);
            ValidateFinite(value.yMin, parameterName);
            ValidateFinite(value.xMax, parameterName);
            ValidateFinite(value.yMax, parameterName);

            if (value.width <= 0f || value.height <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Rectangle dimensions must be positive.");
            }

            if (normalized
                && (value.xMin < 0f
                    || value.yMin < 0f
                    || value.xMax > 1f
                    || value.yMax > 1f))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Normalized viewport rectangles must remain within zero and one.");
            }
        }

        private static void ValidateFinitePositive(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and positive.");
            }
        }

        private static void ValidateFiniteNonNegative(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and non-negative.");
            }
        }

        private static void ValidateFiniteRange(
            float value,
            float minimum,
            float maximum,
            string parameterName)
        {
            if (!IsFinite(value) || value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value is outside the accepted finite range.");
            }
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Rectangle values must be finite.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

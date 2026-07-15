using System;
using UnityEngine;

namespace ShooterMover.Presentation.VisibleSliceCameraReadability
{
    public enum VisibleSliceWarningShape
    {
        OutlinedTriangle = 1,
    }

    public enum VisibleSliceWarningMotion
    {
        Static = 1,
        Pulsing = 2,
    }

    /// <summary>
    /// Immutable warning fact supplied by an accepted gameplay/presentation owner.
    /// </summary>
    public sealed class VisibleSliceWarningSignal
    {
        public VisibleSliceWarningSignal(
            string warningId,
            string label,
            bool isActive,
            float secondsRemaining,
            int priority)
        {
            if (string.IsNullOrWhiteSpace(warningId))
            {
                throw new ArgumentException("Warning identity is required.", nameof(warningId));
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Warning label is required.", nameof(label));
            }

            if (float.IsNaN(secondsRemaining)
                || float.IsInfinity(secondsRemaining)
                || secondsRemaining < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(secondsRemaining),
                    secondsRemaining,
                    "Warning time must be finite and non-negative.");
            }

            if (priority < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(priority),
                    priority,
                    "Warning priority cannot be negative.");
            }

            WarningId = warningId;
            Label = label;
            IsActive = isActive;
            SecondsRemaining = secondsRemaining;
            Priority = priority;
        }

        public string WarningId { get; }

        public string Label { get; }

        public bool IsActive { get; }

        public float SecondsRemaining { get; }

        public int Priority { get; }
    }

    /// <summary>
    /// Color-independent edge-warning presentation. Shape, glyph, label, countdown,
    /// position, and timing remain identical in default and reduced-effects modes.
    /// </summary>
    public sealed class VisibleSliceWarningPresentation
    {
        internal VisibleSliceWarningPresentation(
            VisibleSliceWarningSignal signal,
            Vector2 viewportPosition,
            Vector2 arrowDirection,
            bool sourceBehindCamera,
            bool edgeClamped,
            VisibleSliceWarningMotion motion,
            Color foregroundTone,
            Color backplateTone)
        {
            WarningId = signal.WarningId;
            Label = signal.Label;
            IsVisible = signal.IsActive;
            SecondsRemaining = signal.SecondsRemaining;
            Priority = signal.Priority;
            ViewportPosition = viewportPosition;
            ArrowDirection = arrowDirection;
            SourceBehindCamera = sourceBehindCamera;
            IsEdgeClamped = edgeClamped;
            Motion = motion;
            ForegroundTone = foregroundTone;
            BackplateTone = backplateTone;
            Shape = VisibleSliceWarningShape.OutlinedTriangle;
            Glyph = "!";
            CountdownText = signal.IsActive && signal.SecondsRemaining > 0f
                ? Mathf.CeilToInt(signal.SecondsRemaining).ToString()
                : string.Empty;
            LuminanceContrastRatio = CalculateContrastRatio(
                foregroundTone,
                backplateTone);
        }

        public string WarningId { get; }

        public string Label { get; }

        public bool IsVisible { get; }

        public float SecondsRemaining { get; }

        public int Priority { get; }

        public Vector2 ViewportPosition { get; }

        public Vector2 ArrowDirection { get; }

        public bool SourceBehindCamera { get; }

        public bool IsEdgeClamped { get; }

        public VisibleSliceWarningShape Shape { get; }

        public VisibleSliceWarningMotion Motion { get; }

        public string Glyph { get; }

        public string CountdownText { get; }

        public Color ForegroundTone { get; }

        public Color BackplateTone { get; }

        public float LuminanceContrastRatio { get; }

        public bool HasColorIndependentCue
        {
            get
            {
                return !string.IsNullOrEmpty(Label)
                    && !string.IsNullOrEmpty(Glyph)
                    && Enum.IsDefined(typeof(VisibleSliceWarningShape), Shape);
            }
        }

        private static float CalculateContrastRatio(Color foreground, Color background)
        {
            float foregroundLuminance = RelativeLuminance(foreground);
            float backgroundLuminance = RelativeLuminance(background);
            float lighter = Mathf.Max(foregroundLuminance, backgroundLuminance);
            float darker = Mathf.Min(foregroundLuminance, backgroundLuminance);
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        private static float RelativeLuminance(Color color)
        {
            return (0.2126f * Linearize(color.r))
                + (0.7152f * Linearize(color.g))
                + (0.0722f * Linearize(color.b));
        }

        private static float Linearize(float channel)
        {
            return channel <= 0.04045f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }

    /// <summary>
    /// Stateless world/viewport warning projection. Reduced effects changes motion only;
    /// warning timing and all essential non-color information are preserved.
    /// </summary>
    public static class VisibleSliceWarningProjector
    {
        private const float DirectionEpsilon = 0.000001f;

        public static VisibleSliceWarningPresentation ProjectWorld(
            Camera camera,
            Vector3 warningWorldPosition,
            VisibleSliceWarningSignal signal,
            VisibleSliceCameraConfiguration configuration,
            bool reducedEffects)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            Vector3 projected = camera.WorldToViewportPoint(warningWorldPosition);
            bool behindCamera = projected.z <= 0f;
            Vector2 viewportPosition = new Vector2(projected.x, projected.y);
            if (behindCamera)
            {
                viewportPosition = Vector2.one - viewportPosition;
            }

            return ProjectViewport(
                viewportPosition,
                behindCamera,
                signal,
                configuration,
                reducedEffects);
        }

        public static VisibleSliceWarningPresentation ProjectViewport(
            Vector2 sourceViewportPosition,
            bool sourceBehindCamera,
            VisibleSliceWarningSignal signal,
            VisibleSliceCameraConfiguration configuration,
            bool reducedEffects)
        {
            if (signal == null)
            {
                throw new ArgumentNullException(nameof(signal));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            ValidateFinite(sourceViewportPosition.x, nameof(sourceViewportPosition));
            ValidateFinite(sourceViewportPosition.y, nameof(sourceViewportPosition));

            Rect constraint = configuration.WarningViewport;
            Vector2 clamped = new Vector2(
                Mathf.Clamp(sourceViewportPosition.x, constraint.xMin, constraint.xMax),
                Mathf.Clamp(sourceViewportPosition.y, constraint.yMin, constraint.yMax));
            bool edgeClamped = sourceBehindCamera
                || !ContainsInclusive(constraint, sourceViewportPosition);

            Vector2 direction = sourceViewportPosition - new Vector2(0.5f, 0.5f);
            if (direction.sqrMagnitude <= DirectionEpsilon)
            {
                direction = Vector2.up;
            }
            else
            {
                direction.Normalize();
            }

            return new VisibleSliceWarningPresentation(
                signal,
                clamped,
                direction,
                sourceBehindCamera,
                edgeClamped,
                reducedEffects
                    ? VisibleSliceWarningMotion.Static
                    : VisibleSliceWarningMotion.Pulsing,
                Color.white,
                Color.black);
        }

        public static bool IsWithinWarningConstraints(
            VisibleSliceCameraConfiguration configuration,
            Vector2 viewportPosition)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return ContainsInclusive(configuration.WarningViewport, viewportPosition);
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
                    "Viewport coordinates must be finite.");
            }
        }
    }
}

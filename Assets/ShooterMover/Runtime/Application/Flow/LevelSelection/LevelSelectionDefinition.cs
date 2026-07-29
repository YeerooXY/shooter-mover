using System;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.LevelSelection
{
    public sealed class LevelSelectionDefinition :
        IEquatable<LevelSelectionDefinition>
    {
        public LevelSelectionDefinition(
            StableId levelStableId,
            string displayName,
            string description,
            string scenePath,
            LevelAvailability availability,
            LevelReleaseState releaseState,
            LevelRouteKind routeKind,
            LevelRecommendation recommendation,
            int sortOrder)
        {
            LevelStableId = levelStableId
                ?? throw new ArgumentNullException(nameof(levelStableId));

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A level display name is required.",
                    nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException(
                    "A level description is required.",
                    nameof(description));
            }

            bool requiresSceneRoute =
                availability == LevelAvailability.Unlocked;
            if (requiresSceneRoute && !IsValidScenePath(scenePath))
            {
                throw new ArgumentException(
                    "An unlocked level requires a canonical Assets/.../*.unity scene route.",
                    nameof(scenePath));
            }
            if (!requiresSceneRoute
                && !string.IsNullOrWhiteSpace(scenePath)
                && !IsValidScenePath(scenePath))
            {
                throw new ArgumentException(
                    "A retained locked-level scene route must be canonical.",
                    nameof(scenePath));
            }

            if (!Enum.IsDefined(typeof(LevelAvailability), availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }

            if (!Enum.IsDefined(typeof(LevelReleaseState), releaseState))
            {
                throw new ArgumentOutOfRangeException(nameof(releaseState));
            }

            if (!Enum.IsDefined(typeof(LevelRouteKind), routeKind))
            {
                throw new ArgumentOutOfRangeException(nameof(routeKind));
            }

            if (releaseState == LevelReleaseState.Live
                && routeKind != LevelRouteKind.Gameplay)
            {
                throw new ArgumentException(
                    "A live level must use a gameplay route.",
                    nameof(routeKind));
            }

            if (releaseState == LevelReleaseState.Prototype
                && routeKind != LevelRouteKind.Prototype)
            {
                throw new ArgumentException(
                    "A prototype level must use a prototype route.",
                    nameof(routeKind));
            }

            if (sortOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sortOrder));
            }

            DisplayName = displayName.Trim();
            Description = description.Trim();
            ScenePath = string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : scenePath.Trim();
            Availability = availability;
            ReleaseState = releaseState;
            RouteKind = routeKind;
            Recommendation = recommendation
                ?? throw new ArgumentNullException(nameof(recommendation));
            SortOrder = sortOrder;
        }

        public StableId LevelStableId { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public string ScenePath { get; }

        public LevelAvailability Availability { get; }

        public LevelReleaseState ReleaseState { get; }

        public LevelRouteKind RouteKind { get; }

        public LevelRecommendation Recommendation { get; }

        public int SortOrder { get; }

        public bool Equals(LevelSelectionDefinition other)
        {
            return !ReferenceEquals(other, null)
                && LevelStableId == other.LevelStableId
                && string.Equals(
                    DisplayName,
                    other.DisplayName,
                    StringComparison.Ordinal)
                && string.Equals(
                    Description,
                    other.Description,
                    StringComparison.Ordinal)
                && string.Equals(
                    ScenePath,
                    other.ScenePath,
                    StringComparison.Ordinal)
                && Availability == other.Availability
                && ReleaseState == other.ReleaseState
                && RouteKind == other.RouteKind
                && Recommendation.Equals(other.Recommendation)
                && SortOrder == other.SortOrder;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LevelSelectionDefinition);
        }

        public override int GetHashCode()
        {
            return LevelSelectionCatalog.OrdinalHash(ToCanonicalString());
        }

        internal string ToCanonicalString()
        {
            return LevelRecommendation.CanonicalField(
                    LevelStableId.ToString())
                + "|"
                + LevelRecommendation.CanonicalField(DisplayName)
                + "|"
                + LevelRecommendation.CanonicalField(Description)
                + "|"
                + LevelRecommendation.CanonicalField(ScenePath)
                + "|"
                + ((int)Availability).ToString(CultureInfo.InvariantCulture)
                + "|"
                + ((int)ReleaseState).ToString(CultureInfo.InvariantCulture)
                + "|"
                + ((int)RouteKind).ToString(CultureInfo.InvariantCulture)
                + "|"
                + Recommendation.ToCanonicalString()
                + "|"
                + SortOrder.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsValidScenePath(string scenePath)
        {
            return !string.IsNullOrWhiteSpace(scenePath)
                && scenePath.StartsWith("Assets/", StringComparison.Ordinal)
                && scenePath.EndsWith(".unity", StringComparison.Ordinal)
                && scenePath.IndexOf('\\') < 0
                && scenePath.IndexOf("..", StringComparison.Ordinal) < 0;
        }
    }
}

using System;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.LevelSelection
{
    public sealed class LevelSelectionDefinitionV1 :
        IEquatable<LevelSelectionDefinitionV1>
    {
        public LevelSelectionDefinitionV1(
            StableId levelStableId,
            string displayName,
            string description,
            string scenePath,
            LevelAvailabilityV1 availability,
            LevelReleaseStateV1 releaseState,
            LevelRouteKindV1 routeKind,
            LevelRecommendationV1 recommendation,
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
                availability == LevelAvailabilityV1.Unlocked;
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

            if (!Enum.IsDefined(typeof(LevelAvailabilityV1), availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }

            if (!Enum.IsDefined(typeof(LevelReleaseStateV1), releaseState))
            {
                throw new ArgumentOutOfRangeException(nameof(releaseState));
            }

            if (!Enum.IsDefined(typeof(LevelRouteKindV1), routeKind))
            {
                throw new ArgumentOutOfRangeException(nameof(routeKind));
            }

            if (releaseState == LevelReleaseStateV1.Live
                && routeKind != LevelRouteKindV1.Gameplay)
            {
                throw new ArgumentException(
                    "A live level must use a gameplay route.",
                    nameof(routeKind));
            }

            if (releaseState == LevelReleaseStateV1.Prototype
                && routeKind != LevelRouteKindV1.Prototype)
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

        public LevelAvailabilityV1 Availability { get; }

        public LevelReleaseStateV1 ReleaseState { get; }

        public LevelRouteKindV1 RouteKind { get; }

        public LevelRecommendationV1 Recommendation { get; }

        public int SortOrder { get; }

        public bool Equals(LevelSelectionDefinitionV1 other)
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
            return Equals(obj as LevelSelectionDefinitionV1);
        }

        public override int GetHashCode()
        {
            return LevelSelectionCatalogV1.OrdinalHash(ToCanonicalString());
        }

        internal string ToCanonicalString()
        {
            return LevelRecommendationV1.CanonicalField(
                    LevelStableId.ToString())
                + "|"
                + LevelRecommendationV1.CanonicalField(DisplayName)
                + "|"
                + LevelRecommendationV1.CanonicalField(Description)
                + "|"
                + LevelRecommendationV1.CanonicalField(ScenePath)
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

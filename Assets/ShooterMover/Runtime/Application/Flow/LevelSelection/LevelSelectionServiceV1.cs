using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.LevelSelection
{
    public enum LevelAvailabilityV1
    {
        Locked = 1,
        Unlocked = 2,
    }

    public enum LevelReleaseStateV1
    {
        Live = 1,
        Prototype = 2,
    }

    public enum LevelRouteKindV1
    {
        Gameplay = 1,
        Prototype = 2,
    }

    public enum LevelSelectionRouteV1
    {
        None = 0,
        PlaySelection = 1,
        GameplayScene = 2,
        PrototypeScene = 3,
    }

    public enum LevelSelectionStatusV1
    {
        RouteEmitted = 1,
        LevelLocked = 2,
        UnknownLevel = 3,
        InvalidContext = 4,
        InputLocked = 5,
    }

    public sealed class LevelRecommendationV1 : IEquatable<LevelRecommendationV1>
    {
        public LevelRecommendationV1(
            int recommendedPlayerLevel,
            int recommendedEquipmentLevel,
            int recommendedPartySize,
            string difficultyLabel)
        {
            if (recommendedPlayerLevel <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recommendedPlayerLevel));
            }

            if (recommendedEquipmentLevel <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recommendedEquipmentLevel));
            }

            if (recommendedPartySize <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recommendedPartySize));
            }

            if (string.IsNullOrWhiteSpace(difficultyLabel))
            {
                throw new ArgumentException(
                    "A difficulty label is required.",
                    nameof(difficultyLabel));
            }

            RecommendedPlayerLevel = recommendedPlayerLevel;
            RecommendedEquipmentLevel = recommendedEquipmentLevel;
            RecommendedPartySize = recommendedPartySize;
            DifficultyLabel = difficultyLabel.Trim();
        }

        public int RecommendedPlayerLevel { get; }

        public int RecommendedEquipmentLevel { get; }

        public int RecommendedPartySize { get; }

        public string DifficultyLabel { get; }

        public bool Equals(LevelRecommendationV1 other)
        {
            return !ReferenceEquals(other, null)
                && RecommendedPlayerLevel == other.RecommendedPlayerLevel
                && RecommendedEquipmentLevel == other.RecommendedEquipmentLevel
                && RecommendedPartySize == other.RecommendedPartySize
                && string.Equals(
                    DifficultyLabel,
                    other.DifficultyLabel,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LevelRecommendationV1);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + RecommendedPlayerLevel;
                hash = (hash * 31) + RecommendedEquipmentLevel;
                hash = (hash * 31) + RecommendedPartySize;
                hash = (hash * 31)
                    + StringComparer.Ordinal.GetHashCode(DifficultyLabel);
                return hash;
            }
        }

        internal string ToCanonicalString()
        {
            return RecommendedPlayerLevel.ToString(CultureInfo.InvariantCulture)
                + "|"
                + RecommendedEquipmentLevel.ToString(
                    CultureInfo.InvariantCulture)
                + "|"
                + RecommendedPartySize.ToString(CultureInfo.InvariantCulture)
                + "|"
                + CanonicalField(DifficultyLabel);
        }

        internal static string CanonicalField(string value)
        {
            string normalized = value ?? string.Empty;
            return normalized.Length.ToString(CultureInfo.InvariantCulture)
                + ":"
                + normalized;
        }
    }

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

            if (!IsValidScenePath(scenePath))
            {
                throw new ArgumentException(
                    "A canonical Assets/.../*.unity scene route is required.",
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
            ScenePath = scenePath.Trim();
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

    public sealed class LevelSelectionCatalogV1
    {
        private sealed class DefinitionComparer :
            IComparer<LevelSelectionDefinitionV1>
        {
            public int Compare(
                LevelSelectionDefinitionV1 left,
                LevelSelectionDefinitionV1 right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (ReferenceEquals(left, null))
                {
                    return -1;
                }

                if (ReferenceEquals(right, null))
                {
                    return 1;
                }

                int orderComparison = left.SortOrder.CompareTo(right.SortOrder);
                if (orderComparison != 0)
                {
                    return orderComparison;
                }

                return string.Compare(
                    left.LevelStableId.ToString(),
                    right.LevelStableId.ToString(),
                    StringComparison.Ordinal);
            }
        }

        private readonly ReadOnlyCollection<LevelSelectionDefinitionV1> levels;
        private readonly Dictionary<StableId, LevelSelectionDefinitionV1>
            levelsById;

        public LevelSelectionCatalogV1(
            IEnumerable<LevelSelectionDefinitionV1> levels)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            var ordered = new List<LevelSelectionDefinitionV1>(levels);
            if (ordered.Count == 0)
            {
                throw new ArgumentException(
                    "At least one level definition is required.",
                    nameof(levels));
            }

            ordered.Sort(new DefinitionComparer());
            levelsById =
                new Dictionary<StableId, LevelSelectionDefinitionV1>();

            for (int index = 0; index < ordered.Count; index++)
            {
                LevelSelectionDefinitionV1 definition = ordered[index];
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Level definitions cannot contain null.",
                        nameof(levels));
                }

                if (levelsById.ContainsKey(definition.LevelStableId))
                {
                    throw new ArgumentException(
                        "Level identities must be unique.",
                        nameof(levels));
                }

                levelsById.Add(definition.LevelStableId, definition);
            }

            this.levels =
                new ReadOnlyCollection<LevelSelectionDefinitionV1>(ordered);
            Fingerprint = ComputeFingerprint(ordered);
        }

        public IReadOnlyList<LevelSelectionDefinitionV1> Levels
        {
            get { return levels; }
        }

        public string Fingerprint { get; }

        public bool TryGet(
            StableId levelStableId,
            out LevelSelectionDefinitionV1 definition)
        {
            if (levelStableId == null)
            {
                definition = null;
                return false;
            }

            return levelsById.TryGetValue(levelStableId, out definition);
        }

        internal static int OrdinalHash(string value)
        {
            return StringComparer.Ordinal.GetHashCode(value ?? string.Empty);
        }

        private static string ComputeFingerprint(
            IList<LevelSelectionDefinitionV1> ordered)
        {
            var canonical = new StringBuilder();
            canonical.Append("level-selection-catalog-v1");
            canonical.Append('|');
            canonical.Append(
                ordered.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < ordered.Count; index++)
            {
                canonical.Append('|');
                canonical.Append(ordered[index].ToCanonicalString());
            }

            byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes);
                var hex = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    hex.Append(
                        digest[index].ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return "sha256:" + hex;
            }
        }
    }

    public sealed class LevelSelectionResultV1
    {
        internal LevelSelectionResultV1(
            LevelSelectionStatusV1 status,
            LevelSelectionRouteV1 route,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            PlayerRouteProfilePayloadV1 payload,
            string destinationScenePath,
            string feedbackCode)
        {
            Status = status;
            Route = route;
            SelectedModeStableId = selectedModeStableId;
            SelectedLevelStableId = selectedLevelStableId;
            Payload = payload;
            DestinationScenePath = destinationScenePath ?? string.Empty;
            FeedbackCode = feedbackCode ?? string.Empty;
        }

        public LevelSelectionStatusV1 Status { get; }

        public LevelSelectionRouteV1 Route { get; }

        public StableId SelectedModeStableId { get; }

        public StableId SelectedLevelStableId { get; }

        public PlayerRouteProfilePayloadV1 Payload { get; }

        public string DestinationScenePath { get; }

        public string FeedbackCode { get; }

        public bool RouteEmitted
        {
            get { return Status == LevelSelectionStatusV1.RouteEmitted; }
        }
    }

    public interface ILevelSelectionRouteAdapterV1
    {
        void Present(LevelSelectionResultV1 result);
    }

    /// <summary>
    /// Engine-independent route decision owner. It consumes level metadata and retains
    /// the exact incoming immutable profile/loadout payload and selected play mode.
    /// It does not mutate XP, inventory, equipment, rewards, wallets, or gameplay.
    /// </summary>
    public sealed class LevelSelectionServiceV1
    {
        public const string PlaySelectionScenePath =
            "Assets/ShooterMover/Scenes/Flow/PlaySelection/PlaySelection.unity";

        private readonly PlayerRouteProfilePayloadV1 payload;
        private readonly StableId selectedModeStableId;
        private readonly LevelSelectionCatalogV1 catalog;
        private LevelSelectionResultV1 terminalResult;

        public LevelSelectionServiceV1(
            PlayerRouteProfilePayloadV1 payload,
            StableId selectedModeStableId,
            LevelSelectionCatalogV1 catalog)
        {
            this.payload = payload;
            this.selectedModeStableId = selectedModeStableId;
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
        }

        public PlayerRouteProfilePayloadV1 Payload
        {
            get { return payload; }
        }

        public StableId SelectedModeStableId
        {
            get { return selectedModeStableId; }
        }

        public LevelSelectionCatalogV1 Catalog
        {
            get { return catalog; }
        }

        public bool IsInputLocked
        {
            get { return terminalResult != null; }
        }

        public LevelSelectionResultV1 TerminalResult
        {
            get { return terminalResult; }
        }

        public LevelSelectionResultV1 SelectLevel(StableId levelStableId)
        {
            if (terminalResult != null)
            {
                return Result(
                    LevelSelectionStatusV1.InputLocked,
                    LevelSelectionRouteV1.None,
                    levelStableId,
                    string.Empty,
                    "level-selection-input-locked");
            }

            if (!HasValidContext())
            {
                return Result(
                    LevelSelectionStatusV1.InvalidContext,
                    LevelSelectionRouteV1.None,
                    levelStableId,
                    string.Empty,
                    "level-selection-context-invalid");
            }

            LevelSelectionDefinitionV1 definition;
            if (!catalog.TryGet(levelStableId, out definition))
            {
                return Result(
                    LevelSelectionStatusV1.UnknownLevel,
                    LevelSelectionRouteV1.None,
                    levelStableId,
                    string.Empty,
                    "level-selection-level-unknown");
            }

            if (definition.Availability != LevelAvailabilityV1.Unlocked)
            {
                return Result(
                    LevelSelectionStatusV1.LevelLocked,
                    LevelSelectionRouteV1.None,
                    definition.LevelStableId,
                    definition.ScenePath,
                    "level-selection-level-locked");
            }

            LevelSelectionRouteV1 route =
                definition.RouteKind == LevelRouteKindV1.Gameplay
                    ? LevelSelectionRouteV1.GameplayScene
                    : LevelSelectionRouteV1.PrototypeScene;

            terminalResult = Result(
                LevelSelectionStatusV1.RouteEmitted,
                route,
                definition.LevelStableId,
                definition.ScenePath,
                string.Empty);
            return terminalResult;
        }

        public LevelSelectionResultV1 NavigateBack()
        {
            if (terminalResult != null)
            {
                return Result(
                    LevelSelectionStatusV1.InputLocked,
                    LevelSelectionRouteV1.None,
                    null,
                    string.Empty,
                    "level-selection-input-locked");
            }

            if (!HasValidContext())
            {
                return Result(
                    LevelSelectionStatusV1.InvalidContext,
                    LevelSelectionRouteV1.None,
                    null,
                    string.Empty,
                    "level-selection-context-invalid");
            }

            terminalResult = Result(
                LevelSelectionStatusV1.RouteEmitted,
                LevelSelectionRouteV1.PlaySelection,
                null,
                PlaySelectionScenePath,
                string.Empty);
            return terminalResult;
        }

        private bool HasValidContext()
        {
            return payload != null
                && payload.HasValidFingerprint()
                && selectedModeStableId != null;
        }

        private LevelSelectionResultV1 Result(
            LevelSelectionStatusV1 status,
            LevelSelectionRouteV1 route,
            StableId selectedLevelStableId,
            string destinationScenePath,
            string feedbackCode)
        {
            return new LevelSelectionResultV1(
                status,
                route,
                selectedModeStableId,
                selectedLevelStableId,
                payload,
                destinationScenePath,
                feedbackCode);
        }
    }
}

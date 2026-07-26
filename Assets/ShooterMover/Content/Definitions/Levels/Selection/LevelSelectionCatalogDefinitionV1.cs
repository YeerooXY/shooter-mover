using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Levels.Selection
{
    [Serializable]
    public sealed class LevelSelectionDefinitionRecordV1
    {
        [SerializeField] private string levelStableId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] [TextArea] private string description = string.Empty;
        [SerializeField] private string scenePath = string.Empty;
        [SerializeField] private LevelAvailabilityV1 availability =
            LevelAvailabilityV1.Locked;
        [SerializeField] private LevelReleaseStateV1 releaseState =
            LevelReleaseStateV1.Prototype;
        [SerializeField] private LevelRouteKindV1 routeKind =
            LevelRouteKindV1.Prototype;
        [SerializeField] private int recommendedPlayerLevel = 1;
        [SerializeField] private int recommendedEquipmentLevel = 1;
        [SerializeField] private int recommendedPartySize = 1;
        [SerializeField] private string difficultyLabel = "STANDARD";
        [SerializeField] private int sortOrder;

        public LevelSelectionDefinitionV1 Build()
        {
            StableId parsedLevelStableId;
            if (!StableId.TryParse(levelStableId, out parsedLevelStableId))
            {
                throw new InvalidOperationException(
                    "Level identity is missing or malformed: " + levelStableId);
            }

            return new LevelSelectionDefinitionV1(
                parsedLevelStableId,
                displayName,
                description,
                scenePath,
                availability,
                releaseState,
                routeKind,
                new LevelRecommendationV1(
                    recommendedPlayerLevel,
                    recommendedEquipmentLevel,
                    recommendedPartySize,
                    difficultyLabel),
                sortOrder);
        }
    }

    /// <summary>
    /// Immutable production registration for one authored playable level. It contains no
    /// mutable room, character, holdings, loadout, or scene state.
    /// </summary>
    public sealed class ProductionPlayableLevelDefinitionV1
    {
        public ProductionPlayableLevelDefinitionV1(
            StableId levelStableId,
            string displayName,
            string description,
            string gameplayScenePath,
            string roomContentResourcePath,
            string enemyCatalogResourcePath,
            StableId playerPresentationStableId,
            LevelRecommendationV1 recommendation,
            int sortOrder)
        {
            LevelStableId = levelStableId
                ?? throw new ArgumentNullException(nameof(levelStableId));
            DisplayName = Require(displayName, nameof(displayName));
            Description = Require(description, nameof(description));
            GameplayScenePath = Require(gameplayScenePath, nameof(gameplayScenePath));
            RoomContentResourcePath = Require(
                roomContentResourcePath,
                nameof(roomContentResourcePath));
            EnemyCatalogResourcePath = Require(
                enemyCatalogResourcePath,
                nameof(enemyCatalogResourcePath));
            PlayerPresentationStableId = playerPresentationStableId
                ?? throw new ArgumentNullException(nameof(playerPresentationStableId));
            Recommendation = recommendation
                ?? throw new ArgumentNullException(nameof(recommendation));
            if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
            SortOrder = sortOrder;
        }

        public StableId LevelStableId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string GameplayScenePath { get; }
        public string RoomContentResourcePath { get; }
        public string EnemyCatalogResourcePath { get; }
        public StableId PlayerPresentationStableId { get; }
        public LevelRecommendationV1 Recommendation { get; }
        public int SortOrder { get; }

        public LevelSelectionDefinitionV1 ToSelectionDefinition()
        {
            return new LevelSelectionDefinitionV1(
                LevelStableId,
                DisplayName,
                Description,
                GameplayScenePath,
                LevelAvailabilityV1.Unlocked,
                LevelReleaseStateV1.Live,
                LevelRouteKindV1.Gameplay,
                Recommendation,
                SortOrder);
        }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty playable-level value is required.",
                    parameterName);
            }
            return value.Trim();
        }
    }

    public static class ProductionPlayableLevelCatalogV1
    {
        public const string PlayableLevelScenePath =
            "Assets/ShooterMover/Scenes/Gameplay/PlayableLevel.unity";
        public static readonly StableId FirstLevelStableId =
            StableId.Parse("level.authored-json-1");
        public static readonly StableId AuthoredCombatLoopTestLevelStableId =
            StableId.Parse("level.authored-json-combat-loop-test");

        private static readonly ProductionPlayableLevelDefinitionV1[] Entries =
        {
            new ProductionPlayableLevelDefinitionV1(
                FirstLevelStableId,
                "LEVEL 1",
                "Traverse the first authored two-room JSON level.",
                PlayableLevelScenePath,
                "ProductionLevels/Level1RoomContent",
                "ProductionLevels/Level1EnemyCatalog",
                StableId.Parse("presentation.player-production-default"),
                new LevelRecommendationV1(1, 1, 1, "STANDARD"),
                10),
            new ProductionPlayableLevelDefinitionV1(
                AuthoredCombatLoopTestLevelStableId,
                "COMBAT LOOP TEST",
                "Traverse an authored three-room combat and return loop.",
                PlayableLevelScenePath,
                "ProductionLevels/CombatLoopTestRoomContent",
                "ProductionLevels/Level1EnemyCatalog",
                StableId.Parse("presentation.player-production-default"),
                new LevelRecommendationV1(1, 1, 1, "TEST"),
                20),
        };

        public static IReadOnlyList<ProductionPlayableLevelDefinitionV1> All
        {
            get
            {
                return Array.AsReadOnly(
                    (ProductionPlayableLevelDefinitionV1[])Entries.Clone());
            }
        }

        public static bool TryResolve(
            StableId levelStableId,
            out ProductionPlayableLevelDefinitionV1 definition)
        {
            definition = null;
            if (levelStableId == null) return false;
            for (int index = 0; index < Entries.Length; index++)
            {
                if (Entries[index].LevelStableId == levelStableId)
                {
                    definition = Entries[index];
                    return true;
                }
            }
            return false;
        }

        public static LevelSelectionCatalogV1 CreateSelectionCatalog()
        {
            var definitions = new List<LevelSelectionDefinitionV1>(Entries.Length);
            for (int index = 0; index < Entries.Length; index++)
            {
                definitions.Add(Entries[index].ToSelectionDefinition());
            }
            return new LevelSelectionCatalogV1(definitions);
        }
    }

    [CreateAssetMenu(
        fileName = "LevelSelectionCatalogV1",
        menuName = "Shooter Mover/Flow/Level Selection Catalog V1")]
    public sealed class LevelSelectionCatalogDefinitionV1 : ScriptableObject
    {
        [SerializeField] private List<LevelSelectionDefinitionRecordV1> levels =
            new List<LevelSelectionDefinitionRecordV1>();

        public LevelSelectionCatalogV1 BuildCatalog()
        {
            var definitions = new List<LevelSelectionDefinitionV1>(levels.Count);
            for (int index = 0; index < levels.Count; index++)
            {
                LevelSelectionDefinitionRecordV1 record = levels[index];
                if (record == null)
                {
                    throw new InvalidOperationException(
                        "Level selection entries cannot contain null.");
                }
                definitions.Add(record.Build());
            }
            return new LevelSelectionCatalogV1(definitions);
        }

        public static LevelSelectionCatalogV1 CreateDefaultCatalog()
        {
            return ProductionPlayableLevelCatalogV1.CreateSelectionCatalog();
        }
    }
}

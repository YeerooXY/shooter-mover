using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Levels.Selection
{
    [Serializable]
    public sealed class LevelSelectionDefinitionRecord
    {
        [SerializeField] private string levelStableId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] [TextArea] private string description = string.Empty;
        [SerializeField] private string scenePath = string.Empty;
        [SerializeField] private LevelAvailability availability =
            LevelAvailability.Locked;
        [SerializeField] private LevelReleaseState releaseState =
            LevelReleaseState.Prototype;
        [SerializeField] private LevelRouteKind routeKind =
            LevelRouteKind.Prototype;
        [SerializeField] private int recommendedPlayerLevel = 1;
        [SerializeField] private int recommendedEquipmentLevel = 1;
        [SerializeField] private int recommendedPartySize = 1;
        [SerializeField] private string difficultyLabel = "STANDARD";
        [SerializeField] private int sortOrder;

        public LevelSelectionDefinition Build()
        {
            StableId parsedLevelStableId;
            if (!StableId.TryParse(levelStableId, out parsedLevelStableId))
            {
                throw new InvalidOperationException(
                    "Level identity is missing or malformed: " + levelStableId);
            }

            return new LevelSelectionDefinition(
                parsedLevelStableId,
                displayName,
                description,
                scenePath,
                availability,
                releaseState,
                routeKind,
                new LevelRecommendation(
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
    public sealed class PlayableLevelDefinition
    {
        public PlayableLevelDefinition(
            StableId levelStableId,
            string displayName,
            string description,
            string gameplayScenePath,
            string roomContentResourcePath,
            string enemyCatalogResourcePath,
            StableId playerPresentationStableId,
            LevelRecommendation recommendation,
            int sortOrder,
            bool awardsPersistentExperience)
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
            AwardsPersistentExperience = awardsPersistentExperience;
        }

        public StableId LevelStableId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string GameplayScenePath { get; }
        public string RoomContentResourcePath { get; }
        public string EnemyCatalogResourcePath { get; }
        public StableId PlayerPresentationStableId { get; }
        public LevelRecommendation Recommendation { get; }
        public int SortOrder { get; }
        public bool AwardsPersistentExperience { get; }

        public LevelSelectionDefinition ToSelectionDefinition()
        {
            return new LevelSelectionDefinition(
                LevelStableId,
                DisplayName,
                Description,
                GameplayScenePath,
                LevelAvailability.Unlocked,
                LevelReleaseState.Live,
                LevelRouteKind.Gameplay,
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

    public static class PlayableLevelCatalog
    {
        public const string PlayableLevelScenePath =
            "Assets/ShooterMover/Scenes/Gameplay/PlayableLevel.unity";
        public static readonly StableId FirstLevelStableId =
            StableId.Parse("level.level-1");

        private static PlayableLevelDefinition[] entries;

        private static PlayableLevelDefinition[] Entries
        {
            get
            {
                if (entries == null) entries = LoadEntries();
                return entries;
            }
        }

        public static IReadOnlyList<PlayableLevelDefinition> All
        {
            get
            {
                return Array.AsReadOnly(
                    (PlayableLevelDefinition[])Entries.Clone());
            }
        }

        public static bool TryResolve(
            StableId levelStableId,
            out PlayableLevelDefinition definition)
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

        public static LevelSelectionCatalog CreateSelectionCatalog()
        {
            var definitions = new List<LevelSelectionDefinition>(Entries.Length);
            for (int index = 0; index < Entries.Length; index++)
            {
                definitions.Add(Entries[index].ToSelectionDefinition());
            }
            return new LevelSelectionCatalog(definitions);
        }

        private static PlayableLevelDefinition[] LoadEntries()
        {
            TextAsset asset = Resources.Load<TextAsset>("Levels/PlayableLevelCatalog");
            if (asset == null) return Array.Empty<PlayableLevelDefinition>();

            PlayableLevelCatalogJson payload =
                JsonUtility.FromJson<PlayableLevelCatalogJson>(asset.text);
            if (payload == null || payload.levels == null || payload.levels.Length == 0)
            {
                return Array.Empty<PlayableLevelDefinition>();
            }

            var values = new List<PlayableLevelDefinition>(payload.levels.Length);
            for (int index = 0; index < payload.levels.Length; index++)
            {
                PlayableLevelJson value = payload.levels[index];
                StableId levelId;
                StableId playerPresentation;
                if (value == null
                    || !StableId.TryParse(value.level_id, out levelId)
                    || !StableId.TryParse(value.player_presentation, out playerPresentation))
                {
                    throw new InvalidOperationException(
                        "The published playable-level catalogue contains an invalid identity.");
                }
                values.Add(new PlayableLevelDefinition(
                    levelId,
                    value.display_name,
                    value.description,
                    PlayableLevelScenePath,
                    value.room_content_resource,
                    value.enemy_catalog_resource,
                    playerPresentation,
                    new LevelRecommendation(
                        value.recommended_player_level,
                        value.recommended_equipment_level,
                        value.recommended_party_size,
                        value.difficulty_label),
                    value.sort_order,
                    value.awards_persistent_xp));
            }
            return values.ToArray();
        }

        [Serializable]
        private sealed class PlayableLevelCatalogJson
        {
            public int schema_version;
            public PlayableLevelJson[] levels;
        }

        [Serializable]
        private sealed class PlayableLevelJson
        {
            public string level_id;
            public string display_name;
            public string description;
            public string room_content_resource;
            public string enemy_catalog_resource;
            public string player_presentation;
            public int recommended_player_level;
            public int recommended_equipment_level;
            public int recommended_party_size;
            public string difficulty_label;
            public int sort_order;
            public bool awards_persistent_xp;
        }
    }

    [CreateAssetMenu(
        fileName = "LevelSelectionCatalog",
        menuName = "Shooter Mover/Flow/Level Selection Catalog V1")]
    public sealed class LevelSelectionCatalogDefinition : ScriptableObject
    {
        [SerializeField] private List<LevelSelectionDefinitionRecord> levels =
            new List<LevelSelectionDefinitionRecord>();

        public LevelSelectionCatalog BuildCatalog()
        {
            var definitions = new List<LevelSelectionDefinition>(levels.Count);
            for (int index = 0; index < levels.Count; index++)
            {
                LevelSelectionDefinitionRecord record = levels[index];
                if (record == null)
                {
                    throw new InvalidOperationException(
                        "Level selection entries cannot contain null.");
                }
                definitions.Add(record.Build());
            }
            return new LevelSelectionCatalog(definitions);
        }

        public static LevelSelectionCatalog CreateDefaultCatalog()
        {
            return PlayableLevelCatalog.CreateSelectionCatalog();
        }
    }
}

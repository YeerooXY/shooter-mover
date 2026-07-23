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
        [SerializeField]
        private string levelStableId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [SerializeField]
        [TextArea]
        private string description = string.Empty;

        [SerializeField]
        private string scenePath = string.Empty;

        [SerializeField]
        private LevelAvailabilityV1 availability = LevelAvailabilityV1.Locked;

        [SerializeField]
        private LevelReleaseStateV1 releaseState =
            LevelReleaseStateV1.Prototype;

        [SerializeField]
        private LevelRouteKindV1 routeKind = LevelRouteKindV1.Prototype;

        [SerializeField]
        private int recommendedPlayerLevel = 1;

        [SerializeField]
        private int recommendedEquipmentLevel = 1;

        [SerializeField]
        private int recommendedPartySize = 1;

        [SerializeField]
        private string difficultyLabel = "STANDARD";

        [SerializeField]
        private int sortOrder;

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

    [CreateAssetMenu(
        fileName = "LevelSelectionCatalogV1",
        menuName = "Shooter Mover/Flow/Level Selection Catalog V1")]
    public sealed class LevelSelectionCatalogDefinitionV1 : ScriptableObject
    {
        [SerializeField]
        private List<LevelSelectionDefinitionRecordV1> levels =
            new List<LevelSelectionDefinitionRecordV1>();

        public LevelSelectionCatalogV1 BuildCatalog()
        {
            var definitions =
                new List<LevelSelectionDefinitionV1>(levels.Count);
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
            return new LevelSelectionCatalogV1(
                Array.Empty<LevelSelectionDefinitionV1>());
        }
    }
}

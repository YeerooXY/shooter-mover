using System;
using System.Collections.Generic;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Progression.Experience.EnemyRewards
{
    [Serializable]
    public sealed class EnemyExperienceRewardBandAuthoring
    {
        [SerializeField] private string enemyDefinitionStableId;
        [SerializeField] private int minimumEnemyLevel = 1;
        [SerializeField] private int maximumEnemyLevel = 100;
        [SerializeField] private long experienceAmount;

        public EnemyExperienceRewardBandAuthoring(
            string enemyDefinitionStableId,
            int minimumEnemyLevel,
            int maximumEnemyLevel,
            long experienceAmount)
        {
            this.enemyDefinitionStableId = enemyDefinitionStableId;
            this.minimumEnemyLevel = minimumEnemyLevel;
            this.maximumEnemyLevel = maximumEnemyLevel;
            this.experienceAmount = experienceAmount;
        }

        public string EnemyDefinitionStableId
        {
            get { return enemyDefinitionStableId; }
        }

        public int MinimumEnemyLevel
        {
            get { return minimumEnemyLevel; }
        }

        public int MaximumEnemyLevel
        {
            get { return maximumEnemyLevel; }
        }

        public long ExperienceAmount
        {
            get { return experienceAmount; }
        }
    }

    /// <summary>
    /// Companion authoring catalog keyed by the canonical stable IDs exposed by the
    /// existing enemy definitions. It does not mutate enemy package or combat data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EnemyExperienceRewardCatalog",
        menuName = "Shooter Mover/Progression/Enemy XP Reward Catalog")]
    public sealed class EnemyExperienceRewardCatalogAsset : ScriptableObject
    {
        [SerializeField]
        private List<EnemyExperienceRewardBandAuthoring> rewardBands =
            CreateStage1DefaultBands();

        [SerializeField, HideInInspector] private string validationError = string.Empty;

        public IReadOnlyList<EnemyExperienceRewardBandAuthoring> RewardBands
        {
            get { return rewardBands; }
        }

        public string ValidationError
        {
            get { return validationError ?? string.Empty; }
        }

        public EnemyExperienceRewardCatalog BuildCatalogOrThrow()
        {
            if (rewardBands == null || rewardBands.Count == 0)
            {
                throw new InvalidOperationException(
                    "Enemy XP authoring requires at least one level band.");
            }

            var grouped = new SortedDictionary<string, List<EnemyExperienceRewardBand>>(
                StringComparer.Ordinal);
            for (int index = 0; index < rewardBands.Count; index++)
            {
                EnemyExperienceRewardBandAuthoring authored = rewardBands[index];
                if (authored == null)
                {
                    throw new InvalidOperationException(
                        "Enemy XP authoring cannot contain null entries.");
                }

                StableId definitionId;
                if (!StableId.TryParse(
                    authored.EnemyDefinitionStableId,
                    out definitionId))
                {
                    throw new InvalidOperationException(
                        "Enemy XP authoring contains a malformed enemy definition StableId.");
                }

                var band = new EnemyExperienceRewardBand(
                    authored.MinimumEnemyLevel,
                    authored.MaximumEnemyLevel,
                    authored.ExperienceAmount);
                List<EnemyExperienceRewardBand> definitionBands;
                if (!grouped.TryGetValue(
                    definitionId.ToString(),
                    out definitionBands))
                {
                    definitionBands = new List<EnemyExperienceRewardBand>();
                    grouped.Add(definitionId.ToString(), definitionBands);
                }

                definitionBands.Add(band);
            }

            var definitions = new List<IEnemyExperienceRewardDefinition>(grouped.Count);
            foreach (KeyValuePair<string, List<EnemyExperienceRewardBand>> entry in grouped)
            {
                definitions.Add(new EnemyExperienceRewardDefinition(
                    StableId.Parse(entry.Key),
                    entry.Value));
            }

            return new EnemyExperienceRewardCatalog(definitions);
        }

        public void ValidateOrThrow()
        {
            BuildCatalogOrThrow();
        }

        public static EnemyExperienceRewardCatalogAsset CreateRuntime(
            IEnumerable<EnemyExperienceRewardBandAuthoring> authoredBands)
        {
            if (authoredBands == null)
            {
                throw new ArgumentNullException(nameof(authoredBands));
            }

            var asset = CreateInstance<EnemyExperienceRewardCatalogAsset>();
            asset.rewardBands = new List<EnemyExperienceRewardBandAuthoring>(
                authoredBands);
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.ValidateOrThrow();
            return asset;
        }

        public static EnemyExperienceRewardCatalogAsset CreateStage1DefaultsRuntime()
        {
            return CreateRuntime(CreateStage1DefaultBands());
        }

        private static List<EnemyExperienceRewardBandAuthoring>
            CreateStage1DefaultBands()
        {
            return new List<EnemyExperienceRewardBandAuthoring>
            {
                Band("enemy.blaster-turret", 1, 25, 35L),
                Band("enemy.blaster-turret", 26, 50, 50L),
                Band("enemy.blaster-turret", 51, 75, 75L),
                Band("enemy.blaster-turret", 76, 100, 105L),

                Band("enemy.mobile-blaster-droid", 1, 25, 25L),
                Band("enemy.mobile-blaster-droid", 26, 50, 40L),
                Band("enemy.mobile-blaster-droid", 51, 75, 60L),
                Band("enemy.mobile-blaster-droid", 76, 100, 85L),

                Band("enemy.pursuer-drone", 1, 25, 20L),
                Band("enemy.pursuer-drone", 26, 50, 30L),
                Band("enemy.pursuer-drone", 51, 75, 45L),
                Band("enemy.pursuer-drone", 76, 100, 65L),

                Band("enemy.ram-droid", 1, 25, 30L),
                Band("enemy.ram-droid", 26, 50, 45L),
                Band("enemy.ram-droid", 51, 75, 65L),
                Band("enemy.ram-droid", 76, 100, 90L),
            };
        }

        private static EnemyExperienceRewardBandAuthoring Band(
            string enemyDefinitionStableId,
            int minimumEnemyLevel,
            int maximumEnemyLevel,
            long experienceAmount)
        {
            return new EnemyExperienceRewardBandAuthoring(
                enemyDefinitionStableId,
                minimumEnemyLevel,
                maximumEnemyLevel,
                experienceAmount);
        }

        private void OnValidate()
        {
            try
            {
                ValidateOrThrow();
                validationError = string.Empty;
            }
            catch (Exception exception)
            {
                validationError = exception.Message;
            }
        }
    }
}

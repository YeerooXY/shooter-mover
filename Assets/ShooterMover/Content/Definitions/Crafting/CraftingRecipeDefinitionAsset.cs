using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Progression.Curves;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Crafting
{
    [Serializable]
    public sealed class CraftingWeightedDefinitionAuthoring
    {
        [SerializeField] private string definitionId = string.Empty;
        [Min(1)] [SerializeField] private long weight = 1L;

        public string DefinitionId { get { return definitionId; } }
        public long Weight { get { return weight; } }
    }

    public sealed class CraftingRecipeAssetBuildResult
    {
        private readonly ReadOnlyCollection<string> errors;

        internal CraftingRecipeAssetBuildResult(
            CraftingRecipe recipe,
            IEnumerable<string> errors)
        {
            Recipe = recipe;
            this.errors = new ReadOnlyCollection<string>(
                new List<string>(errors ?? Array.Empty<string>()));
        }

        public CraftingRecipe Recipe { get; }
        public IReadOnlyList<string> Errors { get { return errors; } }
        public bool IsValid { get { return Recipe != null && errors.Count == 0; } }
    }

    [CreateAssetMenu(
        fileName = "CraftingRecipe",
        menuName = "Shooter Mover/Crafting/Crafting Recipe")]
    public sealed class CraftingRecipeDefinitionAsset : ScriptableObject
    {
        [Min(1)] [SerializeField] private int version = 1;
        [SerializeField] private string recipeId = string.Empty;
        [SerializeField] private string targetEquipmentDefinitionId = string.Empty;
        [Tooltip("Stable identity of the authored progression source that supplied the natural/discovery levels.")]
        [SerializeField] private string naturalDiscoverySourceId = string.Empty;
        [Min(0)] [SerializeField] private int naturalDiscoveryLevel;
        [Min(0)] [SerializeField] private int ordinaryDiscoveryActivationLevel;
        [Min(1)] [SerializeField] private int craftingDelayLevels = 1;
        [Min(0)] [SerializeField] private int minimumAdditionalDelayLevels;
        [Min(0)] [SerializeField] private int maximumAdditionalDelayLevels;
        [Min(1)] [SerializeField] private long scrapCost = 1L;

        [SerializeField] private CraftingQualityPolicyKind qualityPolicy = CraftingQualityPolicyKind.Fixed;
        [SerializeField] private CraftingWeightedDefinitionAuthoring[] qualityOptions =
            new CraftingWeightedDefinitionAuthoring[0];

        [Min(1)] [SerializeField] private int minimumItemLevel = 1;
        [Min(1)] [SerializeField] private int maximumItemLevel = 1;
        [Min(0)] [SerializeField] private int minimumAugmentSlots;
        [Min(0)] [SerializeField] private int maximumAugmentSlots;
        [Min(1)] [SerializeField] private int maximumAugmentTier = 1;
        [Min(1)] [SerializeField] private int maximumAugmentLevel = 1;
        [SerializeField] private CraftingWeightedDefinitionAuthoring[] augmentOptions =
            new CraftingWeightedDefinitionAuthoring[0];

        [SerializeField] private string generatorPolicyId = string.Empty;
        [Min(1)] [SerializeField] private int randomAlgorithmVersion = 1;
        [Range(0.000001f, 0.999999f)] [SerializeField] private float earlyTailWeight = 0.25f;
        [Min(1)] [SerializeField] private long earlyTailLevels = 1L;
        [Min(1)] [SerializeField] private long postNominalActivationLevels = 1L;
        [Min(0)] [SerializeField] private long decayStartsAfterLevels = 100L;
        [Min(0.000001f)] [SerializeField] private float halfLifeLevels = 100f;
        [Range(0.000001f, 1f)] [SerializeField] private float minimumRetention = 1f;

        public CraftingRecipeAssetBuildResult BuildRecipe()
        {
            var errors = new List<string>();
            StableId parsedRecipeId = ParseRequired(recipeId, "recipe_id", errors);
            StableId parsedTargetId = ParseRequired(
                targetEquipmentDefinitionId,
                "target_equipment_definition_id",
                errors);
            StableId parsedDiscoverySourceId = ParseRequired(
                naturalDiscoverySourceId,
                "natural_discovery_source_id",
                errors);
            StableId parsedGeneratorPolicyId = ParseRequired(
                generatorPolicyId,
                "generator_policy_id",
                errors);
            List<CraftingWeightedDefinition> parsedQualities = ParseOptions(
                qualityOptions,
                "quality_options",
                errors);
            List<CraftingWeightedDefinition> parsedAugments = ParseOptions(
                augmentOptions,
                "augment_options",
                errors);
            if (errors.Count > 0)
            {
                return new CraftingRecipeAssetBuildResult(null, errors);
            }

            try
            {
                var generatorPolicy = new CraftingGeneratorPolicy(
                    parsedGeneratorPolicyId,
                    randomAlgorithmVersion,
                    new SoftActivationCurveParameters(
                        earlyTailWeight,
                        earlyTailLevels,
                        postNominalActivationLevels),
                    new ObsolescenceCurveParameters(
                        decayStartsAfterLevels,
                        halfLifeLevels,
                        minimumRetention));
                var recipe = new CraftingRecipe(
                    version,
                    parsedRecipeId,
                    parsedTargetId,
                    parsedDiscoverySourceId,
                    naturalDiscoveryLevel,
                    ordinaryDiscoveryActivationLevel,
                    craftingDelayLevels,
                    new CraftingDelayVariance(
                        minimumAdditionalDelayLevels,
                        maximumAdditionalDelayLevels),
                    scrapCost,
                    qualityPolicy,
                    parsedQualities,
                    minimumItemLevel,
                    maximumItemLevel,
                    minimumAugmentSlots,
                    maximumAugmentSlots,
                    maximumAugmentTier,
                    maximumAugmentLevel,
                    parsedAugments,
                    generatorPolicy);
                return new CraftingRecipeAssetBuildResult(recipe, errors);
            }
            catch (Exception exception)
            {
                errors.Add("recipe: " + exception.Message);
                return new CraftingRecipeAssetBuildResult(null, errors);
            }
        }

        private static List<CraftingWeightedDefinition> ParseOptions(
            CraftingWeightedDefinitionAuthoring[] values,
            string field,
            ICollection<string> errors)
        {
            var parsed = new List<CraftingWeightedDefinition>();
            if (values == null)
            {
                errors.Add(field + ": collection is null");
                return parsed;
            }
            for (int index = 0; index < values.Length; index++)
            {
                CraftingWeightedDefinitionAuthoring value = values[index];
                if (value == null)
                {
                    errors.Add(field + "[" + index + "]: entry is null");
                    continue;
                }
                StableId definitionId = ParseRequired(
                    value.DefinitionId,
                    field + "[" + index + "].definition_id",
                    errors);
                if (definitionId == null) { continue; }
                if (value.Weight < 1L)
                {
                    errors.Add(field + "[" + index + "].weight: must be positive");
                    continue;
                }
                parsed.Add(new CraftingWeightedDefinition(
                    definitionId,
                    checked((ulong)value.Weight)));
            }
            return parsed;
        }

        private static StableId ParseRequired(
            string text,
            string field,
            ICollection<string> errors)
        {
            StableId value;
            if (!StableId.TryParse(text, out value))
            {
                errors.Add(field + ": invalid StableId '" + (text ?? "null") + "'");
                return null;
            }
            return value;
        }
    }
}

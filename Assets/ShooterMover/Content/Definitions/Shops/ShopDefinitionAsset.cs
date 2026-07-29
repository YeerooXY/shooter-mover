using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Content.Definitions.Equipment;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Shops;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Shops
{
    [Serializable]
    public sealed class ShopEquipmentCandidateAuthoring
    {
        [SerializeField] private string equipmentDefinitionId = string.Empty;
        [Min(0)] [SerializeField] private int minimumCharacterLevel;
        [Min(0)] [SerializeField] private int maximumCharacterLevel = 100;
        [Min(0)] [SerializeField] private int minimumRegionLevel;
        [Min(0)] [SerializeField] private int maximumRegionLevel = 100;
        [SerializeField] private string[] requiredProgressionTags = new string[0];
        [Min(0)] [SerializeField] private long nominalActivationLevel;
        [Min(1)] [SerializeField] private int minimumGeneratedItemLevel = 1;
        [Min(1)] [SerializeField] private int maximumGeneratedItemLevel = 1;
        [Min(0.000001f)] [SerializeField] private double baseWeight = 1.0;
        [Min(0.000001f)] [SerializeField] private double sourceBias = 1.0;

        public EquipmentGenerationCandidate Build(ICollection<string> errors, string field)
        {
            StableId definitionId = ParseRequired(equipmentDefinitionId, field + ".equipment_definition_id", errors);
            List<StableId> tags = ParseArray(requiredProgressionTags, field + ".required_progression_tags", errors);
            if (definitionId == null)
            {
                return null;
            }

            try
            {
                return EquipmentGenerationCandidate.Create(
                    definitionId,
                    minimumCharacterLevel,
                    maximumCharacterLevel,
                    minimumRegionLevel,
                    maximumRegionLevel,
                    tags,
                    nominalActivationLevel,
                    InclusiveIntRange.Create(minimumGeneratedItemLevel, maximumGeneratedItemLevel),
                    baseWeight,
                    sourceBias);
            }
            catch (Exception exception)
            {
                errors.Add(field + ": " + exception.Message);
                return null;
            }
        }

        internal static StableId ParseRequired(string text, string field, ICollection<string> errors)
        {
            StableId value;
            if (!StableId.TryParse(text, out value))
            {
                errors.Add(field + ": invalid StableId '" + (text ?? "null") + "'");
                return null;
            }

            return value;
        }

        internal static List<StableId> ParseArray(string[] values, string field, ICollection<string> errors)
        {
            List<StableId> result = new List<StableId>();
            if (values == null)
            {
                errors.Add(field + ": collection is null");
                return result;
            }

            for (int index = 0; index < values.Length; index++)
            {
                StableId value = ParseRequired(values[index], field + "[" + index + "]", errors);
                if (value != null)
                {
                    result.Add(value);
                }
            }

            return result;
        }
    }

    [Serializable]
    public sealed class ShopQualityCandidateAuthoring
    {
        [SerializeField] private string qualityId = string.Empty;
        [Min(0)] [SerializeField] private long nominalAvailabilityLevel;
        [Min(1)] [SerializeField] private long weight = 1L;

        public EquipmentQualityCandidate Build(ICollection<string> errors, string field)
        {
            StableId id = ShopEquipmentCandidateAuthoring.ParseRequired(
                qualityId,
                field + ".quality_id",
                errors);
            if (id == null)
            {
                return null;
            }

            try
            {
                return EquipmentQualityCandidate.Create(id, nominalAvailabilityLevel, checked((ulong)weight));
            }
            catch (Exception exception)
            {
                errors.Add(field + ": " + exception.Message);
                return null;
            }
        }
    }

    [Serializable]
    public sealed class ShopAugmentCandidateAuthoring
    {
        [SerializeField] private string augmentDefinitionId = string.Empty;
        [Min(0)] [SerializeField] private int minimumCharacterLevel;
        [Min(0)] [SerializeField] private int maximumCharacterLevel = 100;
        [Min(1)] [SerializeField] private long weight = 1L;

        public AugmentGenerationCandidate Build(ICollection<string> errors, string field)
        {
            StableId id = ShopEquipmentCandidateAuthoring.ParseRequired(
                augmentDefinitionId,
                field + ".augment_definition_id",
                errors);
            if (id == null)
            {
                return null;
            }

            try
            {
                return AugmentGenerationCandidate.Create(
                    id,
                    minimumCharacterLevel,
                    maximumCharacterLevel,
                    checked((ulong)weight));
            }
            catch (Exception exception)
            {
                errors.Add(field + ": " + exception.Message);
                return null;
            }
        }
    }

    [Serializable]
    public sealed class ShopPricingPolicyAuthoring
    {
        [SerializeField] private string policyId = "shop-pricing.unconfigured";
        [Min(1)] [SerializeField] private long minimumPrice = 1L;
        [Min(0)] [SerializeField] private long basePrice;
        [Min(0)] [SerializeField] private long perItemLevel;
        [Min(0)] [SerializeField] private long perQualityRank;
        [Min(0)] [SerializeField] private long perAugment;
        [Min(0)] [SerializeField] private long perAugmentTier;
        [Min(0)] [SerializeField] private long perAugmentLevel;

        public ShopPricingPolicy Build(ICollection<string> errors)
        {
            StableId id = ShopEquipmentCandidateAuthoring.ParseRequired(
                policyId,
                "pricing_policy.policy_id",
                errors);
            if (id == null)
            {
                return null;
            }

            try
            {
                return ShopPricingPolicy.Create(
                    id,
                    minimumPrice,
                    basePrice,
                    perItemLevel,
                    perQualityRank,
                    perAugment,
                    perAugmentTier,
                    perAugmentLevel);
            }
            catch (Exception exception)
            {
                errors.Add("pricing_policy: " + exception.Message);
                return null;
            }
        }
    }

    public sealed class ShopDefinitionAssetBuildResult
    {
        private readonly ReadOnlyCollection<string> errors;

        public ShopDefinitionAssetBuildResult(
            ShopDefinition definition,
            IEnumerable<string> errors)
        {
            Definition = definition;
            this.errors = new ReadOnlyCollection<string>(
                new List<string>(errors ?? Array.Empty<string>()));
        }

        public ShopDefinition Definition { get; }
        public IReadOnlyList<string> Errors { get { return errors; } }
        public bool IsValid { get { return Definition != null && errors.Count == 0; } }
    }

    [CreateAssetMenu(
        fileName = "ShopDefinition",
        menuName = "Shooter Mover/Shops/Shop Definition")]
    public sealed class ShopDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string shopStableId = string.Empty;
        [Min(1)] [SerializeField] private int inventorySize = 1;
        [SerializeField] private string[] eligibleCategoryIds = new string[0];
        [SerializeField] private string[] requiredEquipmentTags = new string[0];
        [SerializeField] private string[] excludedEquipmentTags = new string[0];

        [Header("Generation")]
        [SerializeField] private string generationPolicyId = "shop-generation.unconfigured";
        [SerializeField] private ShopEquipmentCandidateAuthoring[] equipmentCandidates =
            new ShopEquipmentCandidateAuthoring[0];
        [SerializeField] private ShopQualityCandidateAuthoring[] qualityCandidates =
            new ShopQualityCandidateAuthoring[0];
        [SerializeField] private ShopAugmentCandidateAuthoring[] augmentCandidates =
            new ShopAugmentCandidateAuthoring[0];
        [Min(0)] [SerializeField] private int minimumAugmentSlots;
        [Min(0)] [SerializeField] private int maximumAugmentSlots;
        [SerializeField] private bool requireExactAugmentSlotCount;
        [Min(0.0f)] [SerializeField] private double earlyTailWeight = 0.1;
        [Min(0)] [SerializeField] private long earlyTailLevels = 5L;
        [Min(0)] [SerializeField] private long postNominalActivationLevels = 5L;
        [Min(0)] [SerializeField] private long decayStartsAfterLevels = 25L;
        [Min(0.000001f)] [SerializeField] private double halfLifeLevels = 15.0;
        [Range(0.0f, 1.0f)] [SerializeField] private double minimumRetention = 0.2;

        [Header("Runtime Policies")]
        [SerializeField] private ShopProgressionContextPolicy progressionContextPolicy =
            ShopProgressionContextPolicy.FreezeOnFirstOpen;
        [SerializeField] private ShopPricingPolicyAuthoring pricingPolicy =
            new ShopPricingPolicyAuthoring();
        [SerializeField] private ShopRefreshPolicy refreshPolicy = ShopRefreshPolicy.Disabled;
        [Min(0)] [SerializeField] private int maximumRunRefreshCount;
        [Min(0)] [SerializeField] private int baseLockCapacity;
        [Min(1)] [SerializeField] private int algorithmVersion = 1;
        [Min(1)] [SerializeField] private int definitionSchemaVersion = 1;

        public ShopDefinitionAssetBuildResult BuildDefinition()
        {
            List<string> errors = new List<string>();
            StableId shopId = ShopEquipmentCandidateAuthoring.ParseRequired(
                shopStableId,
                "shop_stable_id",
                errors);
            StableId policyId = ShopEquipmentCandidateAuthoring.ParseRequired(
                generationPolicyId,
                "generation_policy_id",
                errors);
            List<StableId> categories = ShopEquipmentCandidateAuthoring.ParseArray(
                eligibleCategoryIds,
                "eligible_category_ids",
                errors);
            List<StableId> requiredTags = ShopEquipmentCandidateAuthoring.ParseArray(
                requiredEquipmentTags,
                "required_equipment_tags",
                errors);
            List<StableId> excludedTags = ShopEquipmentCandidateAuthoring.ParseArray(
                excludedEquipmentTags,
                "excluded_equipment_tags",
                errors);

            List<EquipmentGenerationCandidate> equipment = BuildEquipmentCandidates(errors);
            List<EquipmentQualityCandidate> qualities = BuildQualityCandidates(errors);
            List<AugmentGenerationCandidate> augments = BuildAugmentCandidates(errors);
            ShopPricingPolicy domainPricing = pricingPolicy == null
                ? null
                : pricingPolicy.Build(errors);
            if (pricingPolicy == null)
            {
                errors.Add("pricing_policy: entry is null");
            }

            if (errors.Count > 0)
            {
                return new ShopDefinitionAssetBuildResult(null, errors);
            }

            try
            {
                EquipmentGenerationPolicy generation = EquipmentGenerationPolicy.Create(
                    policyId,
                    equipment,
                    qualities,
                    augments,
                    minimumAugmentSlots,
                    maximumAugmentSlots,
                    requireExactAugmentSlotCount,
                    new SoftActivationCurveParameters(
                        earlyTailWeight,
                        earlyTailLevels,
                        postNominalActivationLevels),
                    new ObsolescenceCurveParameters(
                        decayStartsAfterLevels,
                        halfLifeLevels,
                        minimumRetention));
                ShopDefinition definition = ShopDefinition.Create(
                    shopId,
                    inventorySize,
                    categories,
                    requiredTags,
                    excludedTags,
                    generation,
                    progressionContextPolicy,
                    domainPricing,
                    refreshPolicy,
                    maximumRunRefreshCount,
                    baseLockCapacity,
                    algorithmVersion,
                    definitionSchemaVersion);
                return new ShopDefinitionAssetBuildResult(definition, errors);
            }
            catch (Exception exception)
            {
                errors.Add("shop_definition: " + exception.Message);
                return new ShopDefinitionAssetBuildResult(null, errors);
            }
        }

        private List<EquipmentGenerationCandidate> BuildEquipmentCandidates(
            ICollection<string> errors)
        {
            List<EquipmentGenerationCandidate> result = new List<EquipmentGenerationCandidate>();
            if (equipmentCandidates == null)
            {
                errors.Add("equipment_candidates: collection is null");
                return result;
            }

            for (int index = 0; index < equipmentCandidates.Length; index++)
            {
                ShopEquipmentCandidateAuthoring value = equipmentCandidates[index];
                if (value == null)
                {
                    errors.Add("equipment_candidates[" + index + "]: entry is null");
                    continue;
                }

                EquipmentGenerationCandidate built = value.Build(
                    errors,
                    "equipment_candidates[" + index + "]");
                if (built != null) { result.Add(built); }
            }

            return result;
        }

        private List<EquipmentQualityCandidate> BuildQualityCandidates(
            ICollection<string> errors)
        {
            List<EquipmentQualityCandidate> result = new List<EquipmentQualityCandidate>();
            if (qualityCandidates == null)
            {
                errors.Add("quality_candidates: collection is null");
                return result;
            }

            for (int index = 0; index < qualityCandidates.Length; index++)
            {
                ShopQualityCandidateAuthoring value = qualityCandidates[index];
                if (value == null)
                {
                    errors.Add("quality_candidates[" + index + "]: entry is null");
                    continue;
                }

                EquipmentQualityCandidate built = value.Build(
                    errors,
                    "quality_candidates[" + index + "]");
                if (built != null) { result.Add(built); }
            }

            return result;
        }

        private List<AugmentGenerationCandidate> BuildAugmentCandidates(
            ICollection<string> errors)
        {
            List<AugmentGenerationCandidate> result = new List<AugmentGenerationCandidate>();
            if (augmentCandidates == null)
            {
                errors.Add("augment_candidates: collection is null");
                return result;
            }

            for (int index = 0; index < augmentCandidates.Length; index++)
            {
                ShopAugmentCandidateAuthoring value = augmentCandidates[index];
                if (value == null)
                {
                    errors.Add("augment_candidates[" + index + "]: entry is null");
                    continue;
                }

                AugmentGenerationCandidate built = value.Build(
                    errors,
                    "augment_candidates[" + index + "]");
                if (built != null) { result.Add(built); }
            }

            return result;
        }
    }
}

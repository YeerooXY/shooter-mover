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
    public sealed class ShopEquipmentCandidateAuthoringV1
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

        public EquipmentGenerationCandidateV1 Build(ICollection<string> errors, string field)
        {
            StableId definitionId = ParseRequired(equipmentDefinitionId, field + ".equipment_definition_id", errors);
            List<StableId> tags = ParseArray(requiredProgressionTags, field + ".required_progression_tags", errors);
            if (definitionId == null)
            {
                return null;
            }

            try
            {
                return EquipmentGenerationCandidateV1.Create(
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
    public sealed class ShopQualityCandidateAuthoringV1
    {
        [SerializeField] private string qualityId = string.Empty;
        [Min(0)] [SerializeField] private long nominalAvailabilityLevel;
        [Min(1)] [SerializeField] private long weight = 1L;

        public EquipmentQualityCandidateV1 Build(ICollection<string> errors, string field)
        {
            StableId id = ShopEquipmentCandidateAuthoringV1.ParseRequired(
                qualityId,
                field + ".quality_id",
                errors);
            if (id == null)
            {
                return null;
            }

            try
            {
                return EquipmentQualityCandidateV1.Create(id, nominalAvailabilityLevel, checked((ulong)weight));
            }
            catch (Exception exception)
            {
                errors.Add(field + ": " + exception.Message);
                return null;
            }
        }
    }

    [Serializable]
    public sealed class ShopAugmentCandidateAuthoringV1
    {
        [SerializeField] private string augmentDefinitionId = string.Empty;
        [Min(0)] [SerializeField] private int minimumCharacterLevel;
        [Min(0)] [SerializeField] private int maximumCharacterLevel = 100;
        [Min(1)] [SerializeField] private long weight = 1L;

        public AugmentGenerationCandidateV1 Build(ICollection<string> errors, string field)
        {
            StableId id = ShopEquipmentCandidateAuthoringV1.ParseRequired(
                augmentDefinitionId,
                field + ".augment_definition_id",
                errors);
            if (id == null)
            {
                return null;
            }

            try
            {
                return AugmentGenerationCandidateV1.Create(
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
    public sealed class ShopPricingPolicyAuthoringV1
    {
        [SerializeField] private string policyId = "shop-pricing.unconfigured";
        [Min(1)] [SerializeField] private long minimumPrice = 1L;
        [Min(0)] [SerializeField] private long basePrice;
        [Min(0)] [SerializeField] private long perItemLevel;
        [Min(0)] [SerializeField] private long perQualityRank;
        [Min(0)] [SerializeField] private long perAugment;
        [Min(0)] [SerializeField] private long perAugmentTier;
        [Min(0)] [SerializeField] private long perAugmentLevel;

        public ShopPricingPolicyV1 Build(ICollection<string> errors)
        {
            StableId id = ShopEquipmentCandidateAuthoringV1.ParseRequired(
                policyId,
                "pricing_policy.policy_id",
                errors);
            if (id == null)
            {
                return null;
            }

            try
            {
                return ShopPricingPolicyV1.Create(
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

    public sealed class ShopDefinitionAssetBuildResultV1
    {
        private readonly ReadOnlyCollection<string> errors;

        public ShopDefinitionAssetBuildResultV1(
            ShopDefinitionV1 definition,
            IEnumerable<string> errors)
        {
            Definition = definition;
            this.errors = new ReadOnlyCollection<string>(
                new List<string>(errors ?? Array.Empty<string>()));
        }

        public ShopDefinitionV1 Definition { get; }
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
        [SerializeField] private ShopEquipmentCandidateAuthoringV1[] equipmentCandidates =
            new ShopEquipmentCandidateAuthoringV1[0];
        [SerializeField] private ShopQualityCandidateAuthoringV1[] qualityCandidates =
            new ShopQualityCandidateAuthoringV1[0];
        [SerializeField] private ShopAugmentCandidateAuthoringV1[] augmentCandidates =
            new ShopAugmentCandidateAuthoringV1[0];
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
        [SerializeField] private ShopProgressionContextPolicyV1 progressionContextPolicy =
            ShopProgressionContextPolicyV1.FreezeOnFirstOpen;
        [SerializeField] private ShopPricingPolicyAuthoringV1 pricingPolicy =
            new ShopPricingPolicyAuthoringV1();
        [SerializeField] private ShopRefreshPolicyV1 refreshPolicy = ShopRefreshPolicyV1.Disabled;
        [Min(0)] [SerializeField] private int maximumRunRefreshCount;
        [Min(0)] [SerializeField] private int baseLockCapacity;
        [Min(1)] [SerializeField] private int algorithmVersion = 1;
        [Min(1)] [SerializeField] private int definitionSchemaVersion = 1;

        public ShopDefinitionAssetBuildResultV1 BuildDefinition()
        {
            List<string> errors = new List<string>();
            StableId shopId = ShopEquipmentCandidateAuthoringV1.ParseRequired(
                shopStableId,
                "shop_stable_id",
                errors);
            StableId policyId = ShopEquipmentCandidateAuthoringV1.ParseRequired(
                generationPolicyId,
                "generation_policy_id",
                errors);
            List<StableId> categories = ShopEquipmentCandidateAuthoringV1.ParseArray(
                eligibleCategoryIds,
                "eligible_category_ids",
                errors);
            List<StableId> requiredTags = ShopEquipmentCandidateAuthoringV1.ParseArray(
                requiredEquipmentTags,
                "required_equipment_tags",
                errors);
            List<StableId> excludedTags = ShopEquipmentCandidateAuthoringV1.ParseArray(
                excludedEquipmentTags,
                "excluded_equipment_tags",
                errors);

            List<EquipmentGenerationCandidateV1> equipment = BuildEquipmentCandidates(errors);
            List<EquipmentQualityCandidateV1> qualities = BuildQualityCandidates(errors);
            List<AugmentGenerationCandidateV1> augments = BuildAugmentCandidates(errors);
            ShopPricingPolicyV1 domainPricing = pricingPolicy == null
                ? null
                : pricingPolicy.Build(errors);
            if (pricingPolicy == null)
            {
                errors.Add("pricing_policy: entry is null");
            }

            if (errors.Count > 0)
            {
                return new ShopDefinitionAssetBuildResultV1(null, errors);
            }

            try
            {
                EquipmentGenerationPolicyV1 generation = EquipmentGenerationPolicyV1.Create(
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
                ShopDefinitionV1 definition = ShopDefinitionV1.Create(
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
                return new ShopDefinitionAssetBuildResultV1(definition, errors);
            }
            catch (Exception exception)
            {
                errors.Add("shop_definition: " + exception.Message);
                return new ShopDefinitionAssetBuildResultV1(null, errors);
            }
        }

        private List<EquipmentGenerationCandidateV1> BuildEquipmentCandidates(
            ICollection<string> errors)
        {
            List<EquipmentGenerationCandidateV1> result = new List<EquipmentGenerationCandidateV1>();
            if (equipmentCandidates == null)
            {
                errors.Add("equipment_candidates: collection is null");
                return result;
            }

            for (int index = 0; index < equipmentCandidates.Length; index++)
            {
                ShopEquipmentCandidateAuthoringV1 value = equipmentCandidates[index];
                if (value == null)
                {
                    errors.Add("equipment_candidates[" + index + "]: entry is null");
                    continue;
                }

                EquipmentGenerationCandidateV1 built = value.Build(
                    errors,
                    "equipment_candidates[" + index + "]");
                if (built != null) { result.Add(built); }
            }

            return result;
        }

        private List<EquipmentQualityCandidateV1> BuildQualityCandidates(
            ICollection<string> errors)
        {
            List<EquipmentQualityCandidateV1> result = new List<EquipmentQualityCandidateV1>();
            if (qualityCandidates == null)
            {
                errors.Add("quality_candidates: collection is null");
                return result;
            }

            for (int index = 0; index < qualityCandidates.Length; index++)
            {
                ShopQualityCandidateAuthoringV1 value = qualityCandidates[index];
                if (value == null)
                {
                    errors.Add("quality_candidates[" + index + "]: entry is null");
                    continue;
                }

                EquipmentQualityCandidateV1 built = value.Build(
                    errors,
                    "quality_candidates[" + index + "]");
                if (built != null) { result.Add(built); }
            }

            return result;
        }

        private List<AugmentGenerationCandidateV1> BuildAugmentCandidates(
            ICollection<string> errors)
        {
            List<AugmentGenerationCandidateV1> result = new List<AugmentGenerationCandidateV1>();
            if (augmentCandidates == null)
            {
                errors.Add("augment_candidates: collection is null");
                return result;
            }

            for (int index = 0; index < augmentCandidates.Length; index++)
            {
                ShopAugmentCandidateAuthoringV1 value = augmentCandidates[index];
                if (value == null)
                {
                    errors.Add("augment_candidates[" + index + "]: entry is null");
                    continue;
                }

                AugmentGenerationCandidateV1 built = value.Build(
                    errors,
                    "augment_candidates[" + index + "]");
                if (built != null) { result.Add(built); }
            }

            return result;
        }
    }
}

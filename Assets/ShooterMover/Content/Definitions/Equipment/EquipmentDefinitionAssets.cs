using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Equipment
{
    [Serializable]
    public sealed class EquipmentQualityTierAuthoring
    {
        [SerializeField] private string qualityId = string.Empty;
        [SerializeField] private string label = string.Empty;
        [SerializeField] private int rank = 1;

        public string QualityId { get { return qualityId; } }
        public string Label { get { return label; } }
        public int Rank { get { return rank; } }
    }

    [Serializable]
    public sealed class AugmentCompatibilityAuthoring
    {
        [SerializeField] private string[] categoryIds = new string[0];
        [SerializeField] private string[] familyIds = new string[0];
        [SerializeField] private string[] requiredTags = new string[0];
        [SerializeField] private string[] excludedTags = new string[0];

        public string[] CategoryIds { get { return categoryIds; } }
        public string[] FamilyIds { get { return familyIds; } }
        public string[] RequiredTags { get { return requiredTags; } }
        public string[] ExcludedTags { get { return excludedTags; } }
    }

    public sealed class EquipmentAuthoringConversionResult<T>
        where T : class
    {
        private readonly ReadOnlyCollection<string> errors;

        internal EquipmentAuthoringConversionResult(T value, IEnumerable<string> errors)
        {
            Value = value;
            this.errors = new ReadOnlyCollection<string>(new List<string>(errors ?? new string[0]));
        }

        public T Value { get; }
        public bool IsValid { get { return Value != null && errors.Count == 0; } }
        public IReadOnlyList<string> Errors { get { return errors; } }
    }

    [CreateAssetMenu(
        fileName = "EquipmentDefinition",
        menuName = "Shooter Mover/Equipment/Equipment Definition")]
    public sealed class EquipmentDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string definitionId = string.Empty;
        [SerializeField] private string categoryId = "equipment-category.weapon";
        [SerializeField] private string familyId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [Tooltip("Only weapon equipment uses this field. It references an existing weapon.* package identity; it never duplicates firing behavior.")]
        [SerializeField] private string runtimeWeaponReferenceId = string.Empty;
        [Min(1)] [SerializeField] private int minimumItemLevel = 1;
        [Min(1)] [SerializeField] private int maximumItemLevel = 1;
        [Min(0)] [SerializeField] private int maximumAugmentSlots;
        [SerializeField] private EquipmentQualityTierAuthoring[] qualityTiers = new EquipmentQualityTierAuthoring[0];
        [SerializeField] private string[] tags = new string[0];

        public string DefinitionId { get { return definitionId; } }
        public string CategoryId { get { return categoryId; } }
        public string FamilyId { get { return familyId; } }
        public string RuntimeWeaponReferenceId { get { return runtimeWeaponReferenceId; } }
        public int MaximumAugmentSlots { get { return maximumAugmentSlots; } }

        public EquipmentAuthoringConversionResult<EquipmentDefinition> BuildDefinition()
        {
            List<string> errors = new List<string>();
            StableId parsedDefinitionId = ParseRequired(definitionId, "definition_id", errors);
            StableId parsedCategoryId = ParseRequired(categoryId, "category_id", errors);
            StableId parsedFamilyId = ParseRequired(familyId, "family_id", errors);
            StableId parsedRuntimeReference = ParseOptional(runtimeWeaponReferenceId, "runtime_weapon_reference_id", errors);

            List<EquipmentQualityTier> parsedQualities = new List<EquipmentQualityTier>();
            if (qualityTiers == null)
            {
                errors.Add("quality_tiers: collection is null");
            }
            else
            {
                for (int index = 0; index < qualityTiers.Length; index++)
                {
                    EquipmentQualityTierAuthoring quality = qualityTiers[index];
                    if (quality == null)
                    {
                        errors.Add("quality_tiers[" + index + "]: entry is null");
                        continue;
                    }

                    StableId qualityId = ParseRequired(
                        quality.QualityId,
                        "quality_tiers[" + index + "].quality_id",
                        errors);
                    if (qualityId != null)
                    {
                        parsedQualities.Add(EquipmentQualityTier.Create(qualityId, quality.Label, quality.Rank));
                    }
                }
            }

            List<StableId> parsedTags = ParseArray(tags, "tags", errors);
            if (errors.Count > 0)
            {
                return new EquipmentAuthoringConversionResult<EquipmentDefinition>(null, errors);
            }

            EquipmentDefinition value = EquipmentDefinition.Create(
                parsedDefinitionId,
                parsedCategoryId,
                parsedFamilyId,
                displayName,
                parsedRuntimeReference,
                InclusiveIntRange.Create(minimumItemLevel, maximumItemLevel),
                maximumAugmentSlots,
                parsedQualities,
                parsedTags);
            return new EquipmentAuthoringConversionResult<EquipmentDefinition>(value, errors);
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

        internal static StableId ParseOptional(string text, string field, ICollection<string> errors)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return ParseRequired(text, field, errors);
        }

        internal static List<StableId> ParseArray(string[] values, string field, ICollection<string> errors)
        {
            List<StableId> parsed = new List<StableId>();
            if (values == null)
            {
                errors.Add(field + ": collection is null");
                return parsed;
            }

            for (int index = 0; index < values.Length; index++)
            {
                StableId value = ParseRequired(values[index], field + "[" + index + "]", errors);
                if (value != null)
                {
                    parsed.Add(value);
                }
            }

            return parsed;
        }
    }

    [CreateAssetMenu(
        fileName = "AugmentDefinition",
        menuName = "Shooter Mover/Equipment/Augment Definition")]
    public sealed class AugmentDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string definitionId = string.Empty;
        [SerializeField] private string familyId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private AugmentCompatibilityAuthoring compatibility = new AugmentCompatibilityAuthoring();
        [SerializeField] private string[] exclusionGroupIds = new string[0];
        [SerializeField] private AugmentDuplicatePolicy duplicatePolicy = AugmentDuplicatePolicy.DisallowSameDefinition;
        [Min(1)] [SerializeField] private int minimumTier = 1;
        [Min(1)] [SerializeField] private int maximumTier = 1;
        [Min(1)] [SerializeField] private int minimumLevel = 1;
        [Min(1)] [SerializeField] private int maximumLevel = 1;

        public EquipmentAuthoringConversionResult<AugmentDefinition> BuildDefinition()
        {
            List<string> errors = new List<string>();
            StableId parsedDefinitionId = EquipmentDefinitionAsset.ParseRequired(definitionId, "definition_id", errors);
            StableId parsedFamilyId = EquipmentDefinitionAsset.ParseRequired(familyId, "family_id", errors);

            if (compatibility == null)
            {
                errors.Add("compatibility: entry is null");
            }

            List<StableId> categories = compatibility == null
                ? new List<StableId>()
                : EquipmentDefinitionAsset.ParseArray(compatibility.CategoryIds, "compatibility.category_ids", errors);
            List<StableId> families = compatibility == null
                ? new List<StableId>()
                : EquipmentDefinitionAsset.ParseArray(compatibility.FamilyIds, "compatibility.family_ids", errors);
            List<StableId> requiredTags = compatibility == null
                ? new List<StableId>()
                : EquipmentDefinitionAsset.ParseArray(compatibility.RequiredTags, "compatibility.required_tags", errors);
            List<StableId> excludedTags = compatibility == null
                ? new List<StableId>()
                : EquipmentDefinitionAsset.ParseArray(compatibility.ExcludedTags, "compatibility.excluded_tags", errors);
            List<StableId> exclusionGroups = EquipmentDefinitionAsset.ParseArray(exclusionGroupIds, "exclusion_group_ids", errors);

            if (errors.Count > 0)
            {
                return new EquipmentAuthoringConversionResult<AugmentDefinition>(null, errors);
            }

            AugmentDefinition value = AugmentDefinition.Create(
                parsedDefinitionId,
                parsedFamilyId,
                displayName,
                AugmentCompatibility.Create(categories, families, requiredTags, excludedTags),
                exclusionGroups,
                duplicatePolicy,
                InclusiveIntRange.Create(minimumTier, maximumTier),
                InclusiveIntRange.Create(minimumLevel, maximumLevel));
            return new EquipmentAuthoringConversionResult<AugmentDefinition>(value, errors);
        }
    }

    public sealed class EquipmentCatalogAssetBuildResult
    {
        private readonly ReadOnlyCollection<string> conversionErrors;

        internal EquipmentCatalogAssetBuildResult(
            EquipmentCatalogBuildResult domainResult,
            IEnumerable<string> conversionErrors)
        {
            DomainResult = domainResult;
            this.conversionErrors = new ReadOnlyCollection<string>(
                new List<string>(conversionErrors ?? new string[0]));
        }

        public EquipmentCatalogBuildResult DomainResult { get; }
        public IReadOnlyList<string> ConversionErrors { get { return conversionErrors; } }
        public bool IsValid
        {
            get
            {
                return conversionErrors.Count == 0
                    && DomainResult != null
                    && DomainResult.IsValid;
            }
        }
    }

    [CreateAssetMenu(
        fileName = "EquipmentCatalog",
        menuName = "Shooter Mover/Equipment/Equipment Catalog")]
    public sealed class EquipmentCatalogAsset : ScriptableObject
    {
        [SerializeField] private EquipmentDefinitionAsset[] equipmentDefinitions = new EquipmentDefinitionAsset[0];
        [SerializeField] private AugmentDefinitionAsset[] augmentDefinitions = new AugmentDefinitionAsset[0];

        public EquipmentCatalogAssetBuildResult BuildCatalog()
        {
            List<string> errors = new List<string>();
            List<EquipmentDefinition> equipment = new List<EquipmentDefinition>();
            List<AugmentDefinition> augments = new List<AugmentDefinition>();

            if (equipmentDefinitions == null)
            {
                errors.Add("equipment_definitions: collection is null");
            }
            else
            {
                for (int index = 0; index < equipmentDefinitions.Length; index++)
                {
                    EquipmentDefinitionAsset asset = equipmentDefinitions[index];
                    if (asset == null)
                    {
                        errors.Add("equipment_definitions[" + index + "]: asset is null");
                        continue;
                    }

                    EquipmentAuthoringConversionResult<EquipmentDefinition> result = asset.BuildDefinition();
                    if (result.IsValid)
                    {
                        equipment.Add(result.Value);
                    }
                    else
                    {
                        AddPrefixed(errors, "equipment_definitions[" + index + "]", result.Errors);
                    }
                }
            }

            if (augmentDefinitions == null)
            {
                errors.Add("augment_definitions: collection is null");
            }
            else
            {
                for (int index = 0; index < augmentDefinitions.Length; index++)
                {
                    AugmentDefinitionAsset asset = augmentDefinitions[index];
                    if (asset == null)
                    {
                        errors.Add("augment_definitions[" + index + "]: asset is null");
                        continue;
                    }

                    EquipmentAuthoringConversionResult<AugmentDefinition> result = asset.BuildDefinition();
                    if (result.IsValid)
                    {
                        augments.Add(result.Value);
                    }
                    else
                    {
                        AddPrefixed(errors, "augment_definitions[" + index + "]", result.Errors);
                    }
                }
            }

            EquipmentCatalogBuildResult domainResult = errors.Count == 0
                ? EquipmentCatalog.Build(equipment, augments)
                : null;
            return new EquipmentCatalogAssetBuildResult(domainResult, errors);
        }

        private static void AddPrefixed(
            ICollection<string> target,
            string prefix,
            IReadOnlyList<string> source)
        {
            for (int index = 0; index < source.Count; index++)
            {
                target.Add(prefix + "." + source[index]);
            }
        }
    }
}

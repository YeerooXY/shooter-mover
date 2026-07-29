using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShooterMover.Domain.Guns.Catalog
{
    public static partial class GunCatalogValidator
    {
        private const double RelativeTolerance = 0.000001;

        public static GunCatalogValidationResult Validate(
            string version,
            string status,
            GunCatalogRules rules,
            GunCatalogInputs inputs,
            IDictionary<string, GunArchetypeDefinition> archetypes,
            IEnumerable<GunFamilyDefinition> families,
            IEnumerable<GunDefinitionData> definitions)
        {
            List<GunCatalogIssue> issues = new List<GunCatalogIssue>();
            RequireText(version, "$.version", issues);
            RequireText(status, "$.status", issues);

            ValidateRules(rules, issues);
            ValidateInputs(inputs, issues);

            Dictionary<string, GunArchetypeDefinition> archetypeMap =
                archetypes == null
                    ? new Dictionary<string, GunArchetypeDefinition>(StringComparer.Ordinal)
                    : new Dictionary<string, GunArchetypeDefinition>(archetypes, StringComparer.Ordinal);
            ValidateArchetypes(archetypeMap, issues);

            List<GunFamilyDefinition> familyList = families == null
                ? new List<GunFamilyDefinition>()
                : new List<GunFamilyDefinition>(families);
            Dictionary<string, GunFamilyDefinition> familyMap =
                ValidateFamilies(familyList, inputs, archetypeMap, rules, issues);

            List<GunDefinitionData> definitionList = definitions == null
                ? new List<GunDefinitionData>()
                : new List<GunDefinitionData>(definitions);
            ValidateDefinitions(definitionList, familyMap, inputs, archetypeMap, rules, issues);

            return new GunCatalogValidationResult(issues);
        }

        private static void ValidateRules(GunCatalogRules rules, List<GunCatalogIssue> issues)
        {
            if (rules == null)
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.MissingRequiredField,
                    "$.rules",
                    "Rules are required."));
                return;
            }

            RequireText(rules.OrdinaryMarkGap, "$.rules.ordinary_mark_gap", issues);
            if (rules.MaxAugments < 0)
            {
                Range("$.rules.max_augments", "Max augments cannot be negative.", issues);
            }

            HashSet<string> damageTypes = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < rules.DamageTypes.Count; index++)
            {
                string path = "$.rules.damage_types[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                string value = rules.DamageTypes[index];
                RequireText(value, path, issues);
                if (!string.IsNullOrWhiteSpace(value) && !damageTypes.Add(value))
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.DuplicateId,
                        path,
                        "Duplicate damage type."));
                }
            }

            if (rules.DamageTypes.Count == 0)
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.MissingRequiredField,
                    "$.rules.damage_types",
                    "At least one damage type is required."));
            }
        }

        private static void ValidateInputs(GunCatalogInputs inputs, List<GunCatalogIssue> issues)
        {
            if (inputs == null)
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.MissingRequiredField,
                    "$.inputs",
                    "Inputs are required."));
                return;
            }

            if (inputs.Rarities.Count == 0)
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.MissingRequiredField,
                    "$.inputs.rarities",
                    "At least one rarity is required."));
            }

            List<string> ids = new List<string>(inputs.Rarities.Keys);
            ids.Sort(StringComparer.Ordinal);
            for (int index = 0; index < ids.Count; index++)
            {
                string id = ids[index];
                GunRarityInput rarity = inputs.Rarities[id];
                string path = "$.inputs.rarities." + id;
                RequireText(id, path, issues);
                if (rarity == null)
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.MissingRequiredField,
                        path,
                        "Rarity data is required."));
                    continue;
                }
                if (!string.Equals(id, rarity.Rarity, StringComparison.Ordinal))
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.InvalidValue,
                        path,
                        "Dictionary key and rarity identity differ."));
                }
                Positive(rarity.Weight, path + ".weight", issues);
                NonNegative(rarity.EarlyTail, path + ".early_tail", issues);
                NonNegative(rarity.LateTail, path + ".late_tail", issues);
            }
        }

        private static void ValidateArchetypes(
            IDictionary<string, GunArchetypeDefinition> archetypes,
            List<GunCatalogIssue> issues)
        {
            if (archetypes.Count == 0)
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.MissingRequiredField,
                    "$.archetypes",
                    "At least one archetype is required."));
            }

            List<string> ids = new List<string>(archetypes.Keys);
            ids.Sort(StringComparer.Ordinal);
            for (int index = 0; index < ids.Count; index++)
            {
                string id = ids[index];
                GunArchetypeDefinition value = archetypes[id];
                string path = "$.archetypes." + id;
                RequireText(id, path, issues);
                if (value == null)
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.MissingRequiredField,
                        path,
                        "Archetype data is required."));
                    continue;
                }
                if (!string.Equals(id, value.ArchetypeId, StringComparison.Ordinal))
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.InvalidValue,
                        path,
                        "Dictionary key and archetype identity differ."));
                }
                RequireText(value.Description, path + ".description", issues);
                Positive(value.FireRate, path + ".fire_rate", issues);
                Positive(value.Projectiles, path + ".projectiles", issues);
                Positive(value.Burst, path + ".burst", issues);
                NonNegative(value.Spread, path + ".spread", issues);
                Positive(value.Speed, path + ".speed", issues);
                Positive(value.Range, path + ".range", issues);
                NonNegative(value.Radius, path + ".radius", issues);
                NonNegative(value.DotDuration, path + ".dot_duration", issues);
                NonNegative(value.PoolRadius, path + ".pool_radius", issues);
                NonNegative(value.PoolDuration, path + ".pool_duration", issues);
                NonNegative(value.Pierce, path + ".pierce", issues);
                NonNegative(value.ChainTargets, path + ".chain_targets", issues);
                NonNegative(value.ChainRange, path + ".chain_range", issues);
                NonNegative(value.Knockback, path + ".knockback", issues);
                NonNegative(value.PowerCost, path + ".power_cost", issues);
            }
        }

        private static Dictionary<string, GunFamilyDefinition> ValidateFamilies(
            IList<GunFamilyDefinition> families,
            GunCatalogInputs inputs,
            IDictionary<string, GunArchetypeDefinition> archetypes,
            GunCatalogRules rules,
            List<GunCatalogIssue> issues)
        {
            Dictionary<string, GunFamilyDefinition> result =
                new Dictionary<string, GunFamilyDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < families.Count; index++)
            {
                GunFamilyDefinition family = families[index];
                string path = "$.families[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (family == null)
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.MissingRequiredField,
                        path,
                        "Family is required."));
                    continue;
                }

                ValidateFamilyId(family.FamilyId, path + ".FamilyId", issues);
                if (!result.ContainsKey(family.FamilyId))
                {
                    result.Add(family.FamilyId, family);
                }
                else
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.DuplicateId,
                        path + ".FamilyId",
                        "Duplicate family ID '" + family.FamilyId + "'."));
                }

                RequireText(family.DisplayName, path + ".DisplayName", issues);
                RequireText(family.Archetype, path + ".Archetype", issues);
                RequireText(family.DamageType, path + ".DamageType", issues);
                RequireText(family.BuildAffinity, path + ".BuildAffinity", issues);
                RequireText(family.AcquisitionClass, path + ".AcquisitionClass", issues);
                RequireText(family.PrimaryEffect, path + ".PrimaryEffect", issues);
                if (!archetypes.ContainsKey(family.Archetype))
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.UnsupportedArchetype,
                        path + ".Archetype",
                        "Unknown archetype '" + family.Archetype + "'."));
                }
                if (rules != null && !ContainsOrdinal(rules.DamageTypes, family.DamageType))
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.UnsupportedDamageType,
                        path + ".DamageType",
                        "Unsupported damage type '" + family.DamageType + "'."));
                }
                Positive(family.Mk1Peak, path + ".MK1Peak", issues);
                NonNegative(family.GapMk1To2, path + ".GapMK1To2", issues);
                NonNegative(family.GapMk2To3, path + ".GapMK2To3", issues);
                if (family.MaxPlannedMark < 1 || family.MaxPlannedMark > 3)
                {
                    Range(path + ".MaxPlannedMark", "Max planned mark must be between 1 and 3 for the current schema.", issues);
                }
                Positive(family.DefinitionWeightModifier, path + ".DefinitionWeightModifier", issues);

                ValidateFamilyRarity(family.Mk1Rarity, 1, family.MaxPlannedMark, inputs, path + ".MK1Rarity", issues);
                ValidateFamilyRarity(family.Mk2Rarity, 2, family.MaxPlannedMark, inputs, path + ".MK2Rarity", issues);
                ValidateFamilyRarity(family.Mk3Rarity, 3, family.MaxPlannedMark, inputs, path + ".MK3Rarity", issues);
                ValidateArtReferences(family.SideProfileArtReferences, path, issues);
            }

            return result;
        }
    }
}

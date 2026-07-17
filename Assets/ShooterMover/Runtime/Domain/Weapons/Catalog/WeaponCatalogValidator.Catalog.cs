using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ShooterMover.Domain.Weapons.Catalog
{
    public static partial class WeaponCatalogValidator
    {
        private const double RelativeTolerance = 0.000001;

        public static WeaponCatalogValidationResult Validate(
            string version,
            string status,
            WeaponCatalogRules rules,
            WeaponCatalogInputs inputs,
            IDictionary<string, WeaponArchetypeDefinition> archetypes,
            IEnumerable<WeaponFamilyDefinition> families,
            IEnumerable<WeaponDefinitionData> definitions)
        {
            List<WeaponCatalogIssue> issues = new List<WeaponCatalogIssue>();
            RequireText(version, "$.version", issues);
            RequireText(status, "$.status", issues);

            ValidateRules(rules, issues);
            ValidateInputs(inputs, issues);

            Dictionary<string, WeaponArchetypeDefinition> archetypeMap =
                archetypes == null
                    ? new Dictionary<string, WeaponArchetypeDefinition>(StringComparer.Ordinal)
                    : new Dictionary<string, WeaponArchetypeDefinition>(archetypes, StringComparer.Ordinal);
            ValidateArchetypes(archetypeMap, issues);

            List<WeaponFamilyDefinition> familyList = families == null
                ? new List<WeaponFamilyDefinition>()
                : new List<WeaponFamilyDefinition>(families);
            Dictionary<string, WeaponFamilyDefinition> familyMap =
                ValidateFamilies(familyList, inputs, archetypeMap, rules, issues);

            List<WeaponDefinitionData> definitionList = definitions == null
                ? new List<WeaponDefinitionData>()
                : new List<WeaponDefinitionData>(definitions);
            ValidateDefinitions(definitionList, familyMap, inputs, archetypeMap, rules, issues);

            return new WeaponCatalogValidationResult(issues);
        }

        private static void ValidateRules(WeaponCatalogRules rules, List<WeaponCatalogIssue> issues)
        {
            if (rules == null)
            {
                issues.Add(new WeaponCatalogIssue(
                    WeaponCatalogIssueCode.MissingRequiredField,
                    "$.rules",
                    "Rules are required."));
                return;
            }

            RequireText(rules.OrdinaryMarkGap, "$.rules.ordinary_mark_gap", issues);
            if (rules.MaxAugments < 0)
            {
                Range("$.rules.max_augments", "Max augments cannot be negative.", issues);
            }

            HashSet<int> anchors = new HashSet<int>();
            for (int index = 0; index < rules.ApexPowerAnchors.Count; index++)
            {
                int value = rules.ApexPowerAnchors[index];
                string path = "$.rules.apex_power_anchors[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (value < 1)
                {
                    Range(path, "Power anchors must be positive.", issues);
                }
                if (!anchors.Add(value))
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.DuplicateId,
                        path,
                        "Duplicate apex power anchor."));
                }
            }

            HashSet<string> damageTypes = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < rules.DamageTypes.Count; index++)
            {
                string path = "$.rules.damage_types[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                string value = rules.DamageTypes[index];
                RequireText(value, path, issues);
                if (!string.IsNullOrWhiteSpace(value) && !damageTypes.Add(value))
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.DuplicateId,
                        path,
                        "Duplicate damage type."));
                }
            }

            if (rules.DamageTypes.Count == 0)
            {
                issues.Add(new WeaponCatalogIssue(
                    WeaponCatalogIssueCode.MissingRequiredField,
                    "$.rules.damage_types",
                    "At least one damage type is required."));
            }
        }

        private static void ValidateInputs(WeaponCatalogInputs inputs, List<WeaponCatalogIssue> issues)
        {
            if (inputs == null)
            {
                issues.Add(new WeaponCatalogIssue(
                    WeaponCatalogIssueCode.MissingRequiredField,
                    "$.inputs",
                    "Inputs are required."));
                return;
            }

            Positive(inputs.BaseDps, "$.inputs.base_dps", issues);
            NonNegative(inputs.Growth1To30, "$.inputs.growth_1_30", issues);
            NonNegative(inputs.Growth31To70, "$.inputs.growth_31_70", issues);
            NonNegative(inputs.Growth71Plus, "$.inputs.growth_71_plus", issues);
            if (inputs.Rarities.Count == 0)
            {
                issues.Add(new WeaponCatalogIssue(
                    WeaponCatalogIssueCode.MissingRequiredField,
                    "$.inputs.rarities",
                    "At least one rarity is required."));
            }

            List<string> ids = new List<string>(inputs.Rarities.Keys);
            ids.Sort(StringComparer.Ordinal);
            for (int index = 0; index < ids.Count; index++)
            {
                string id = ids[index];
                WeaponRarityInput rarity = inputs.Rarities[id];
                string path = "$.inputs.rarities." + id;
                RequireText(id, path, issues);
                if (rarity == null)
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.MissingRequiredField,
                        path,
                        "Rarity data is required."));
                    continue;
                }
                if (!string.Equals(id, rarity.Rarity, StringComparison.Ordinal))
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.InvalidValue,
                        path,
                        "Dictionary key and rarity identity differ."));
                }
                Positive(rarity.Weight, path + ".weight", issues);
                if (rarity.PowerBonus < 0)
                {
                    Range(path + ".power_bonus", "Power bonus cannot be negative.", issues);
                }
                NonNegative(rarity.EarlyTail, path + ".early_tail", issues);
                NonNegative(rarity.LateTail, path + ".late_tail", issues);
            }
        }

        private static void ValidateArchetypes(
            IDictionary<string, WeaponArchetypeDefinition> archetypes,
            List<WeaponCatalogIssue> issues)
        {
            if (archetypes.Count == 0)
            {
                issues.Add(new WeaponCatalogIssue(
                    WeaponCatalogIssueCode.MissingRequiredField,
                    "$.archetypes",
                    "At least one archetype is required."));
            }

            List<string> ids = new List<string>(archetypes.Keys);
            ids.Sort(StringComparer.Ordinal);
            for (int index = 0; index < ids.Count; index++)
            {
                string id = ids[index];
                WeaponArchetypeDefinition value = archetypes[id];
                string path = "$.archetypes." + id;
                RequireText(id, path, issues);
                if (value == null)
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.MissingRequiredField,
                        path,
                        "Archetype data is required."));
                    continue;
                }
                if (!string.Equals(id, value.ArchetypeId, StringComparison.Ordinal))
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.InvalidValue,
                        path,
                        "Dictionary key and archetype identity differ."));
                }
                RequireText(value.Description, path + ".description", issues);
                Positive(value.DpsFactor, path + ".dps_factor", issues);
                Positive(value.FireRate, path + ".fire_rate", issues);
                Positive(value.Projectiles, path + ".projectiles", issues);
                Positive(value.Burst, path + ".burst", issues);
                NonNegative(value.Spread, path + ".spread", issues);
                Positive(value.Speed, path + ".speed", issues);
                Positive(value.Range, path + ".range", issues);
                Share(value.DirectShare, path + ".direct_share", issues);
                Share(value.AreaShare, path + ".area_share", issues);
                Share(value.DotShare, path + ".dot_share", issues);
                ValidateShareTotal(value.DirectShare, value.AreaShare, value.DotShare, path, issues);
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

        private static Dictionary<string, WeaponFamilyDefinition> ValidateFamilies(
            IList<WeaponFamilyDefinition> families,
            WeaponCatalogInputs inputs,
            IDictionary<string, WeaponArchetypeDefinition> archetypes,
            WeaponCatalogRules rules,
            List<WeaponCatalogIssue> issues)
        {
            Dictionary<string, WeaponFamilyDefinition> result =
                new Dictionary<string, WeaponFamilyDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < families.Count; index++)
            {
                WeaponFamilyDefinition family = families[index];
                string path = "$.families[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (family == null)
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.MissingRequiredField,
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
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.DuplicateId,
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
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.UnsupportedArchetype,
                        path + ".Archetype",
                        "Unknown archetype '" + family.Archetype + "'."));
                }
                if (rules != null && !ContainsOrdinal(rules.DamageTypes, family.DamageType))
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.UnsupportedDamageType,
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

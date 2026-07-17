using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ShooterMover.Domain.Weapons.Catalog
{
    public static partial class WeaponCatalogValidator
    {
        private static void ValidateDefinitions(
            IList<WeaponDefinitionData> definitions,
            IDictionary<string, WeaponFamilyDefinition> families,
            WeaponCatalogInputs inputs,
            IDictionary<string, WeaponArchetypeDefinition> archetypes,
            WeaponCatalogRules rules,
            List<WeaponCatalogIssue> issues)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> pairs = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < definitions.Count; index++)
            {
                WeaponDefinitionData value = definitions[index];
                string path = "$.definitions[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (value == null)
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.MissingRequiredField,
                        path,
                        "Definition is required."));
                    continue;
                }

                ValidateDefinitionId(value.DefinitionId, path + ".DefinitionId", issues);
                if (!ids.Add(value.DefinitionId))
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.DuplicateId,
                        path + ".DefinitionId",
                        "Duplicate definition ID '" + value.DefinitionId + "'."));
                }

                string pair = value.FamilyId + "#" + value.Mark.ToString(CultureInfo.InvariantCulture);
                if (!pairs.Add(pair))
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.DuplicateFamilyMark,
                        path,
                        "Duplicate family/mark pair '" + pair + "'."));
                }

                RequireText(value.DisplayName, path + ".DisplayName", issues);
                ValidateFamilyId(value.FamilyId, path + ".FamilyId", issues);
                Positive(value.Mark, path + ".Mark", issues);
                RequireText(value.DamageType, path + ".DamageType", issues);
                RequireText(value.Archetype, path + ".Archetype", issues);
                RequireText(value.BuildAffinity, path + ".BuildAffinity", issues);
                RequireText(value.Rarity, path + ".Rarity", issues);
                RequireText(value.AcquisitionClass, path + ".AcquisitionClass", issues);
                RequireText(value.CraftingRoute, path + ".CraftingRoute", issues);
                RequireText(value.PrimaryEffect, path + ".PrimaryEffect", issues);

                WeaponFamilyDefinition family;
                if (!families.TryGetValue(value.FamilyId, out family))
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.UnknownFamily,
                        path + ".FamilyId",
                        "Unknown family '" + value.FamilyId + "'."));
                }
                else
                {
                    string expectedId = value.FamilyId + ".mk" + value.Mark.ToString(CultureInfo.InvariantCulture);
                    if (!string.Equals(expectedId, value.DefinitionId, StringComparison.Ordinal))
                    {
                        issues.Add(new WeaponCatalogIssue(
                            WeaponCatalogIssueCode.FamilyMarkMismatch,
                            path + ".DefinitionId",
                            "Definition ID must be '" + expectedId + "'."));
                    }
                    if (value.Mark > family.MaxPlannedMark)
                    {
                        issues.Add(new WeaponCatalogIssue(
                            WeaponCatalogIssueCode.FamilyMarkMismatch,
                            path + ".Mark",
                            "Mark exceeds the family's max planned mark."));
                    }
                    CompareText(value.Archetype, family.Archetype, path + ".Archetype", "family archetype", issues);
                    CompareText(value.DamageType, family.DamageType, path + ".DamageType", "family damage type", issues);
                    CompareText(value.BuildAffinity, family.BuildAffinity, path + ".BuildAffinity", "family build affinity", issues);
                    CompareText(value.Rarity, family.RarityForMark(value.Mark), path + ".Rarity", "family mark rarity", issues);
                    CompareNumber(value.DefinitionWeightModifier, family.DefinitionWeightModifier, path + ".DefinitionWeightModifier", "family weight modifier", issues);
                }

                if (!archetypes.ContainsKey(value.Archetype))
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.UnsupportedArchetype,
                        path + ".Archetype",
                        "Unknown archetype '" + value.Archetype + "'."));
                }
                if (rules != null && !ContainsOrdinal(rules.DamageTypes, value.DamageType))
                {
                    issues.Add(new WeaponCatalogIssue(
                        WeaponCatalogIssueCode.UnsupportedDamageType,
                        path + ".DamageType",
                        "Unsupported damage type '" + value.DamageType + "'."));
                }

                Positive(value.FirstAppearance, path + ".FirstAppearance", issues);
                Positive(value.PeakDropLevel, path + ".PeakDropLevel", issues);
                Positive(value.PowerAnchor, path + ".PowerAnchor", issues);
                if (value.PeakDropLevel < value.FirstAppearance)
                {
                    Range(path + ".PeakDropLevel", "Peak drop level cannot precede first appearance.", issues);
                }
                if (value.PowerAnchor < value.PeakDropLevel)
                {
                    Range(path + ".PowerAnchor", "Power anchor cannot precede peak drop level.", issues);
                }

                Positive(value.RarityWeight, path + ".RarityWeight", issues);
                Positive(value.DefinitionWeightModifier, path + ".DefinitionWeightModifier", issues);
                Positive(value.FinalBaseWeight, path + ".FinalBaseWeight", issues);
                NonNegative(value.EarlyTail, path + ".EarlyTail", issues);
                NonNegative(value.LateTail, path + ".LateTail", issues);
                Positive(value.ArchetypeDpsFactor, path + ".ArchetypeDPSFactor", issues);
                Positive(value.PowerIndex, path + ".PowerIndex", issues);
                Positive(value.TargetDps, path + ".TargetDPS", issues);
                Share(value.DirectShare, path + ".DirectShare", issues);
                Share(value.AreaShare, path + ".AreaShare", issues);
                Share(value.DotShare, path + ".DoTShare", issues);
                ValidateShareTotal(value.DirectShare, value.AreaShare, value.DotShare, path, issues);
                Positive(value.FireRate, path + ".FireRate", issues);
                Positive(value.ProjectilesPerTrigger, path + ".ProjectilesPerTrigger", issues);
                Positive(value.BurstCount, path + ".BurstCount", issues);
                NonNegative(value.DamagePerProjectile, path + ".DamagePerProjectile", issues);
                NonNegative(value.SpreadDegrees, path + ".SpreadDegrees", issues);
                Positive(value.ProjectileSpeed, path + ".ProjectileSpeed", issues);
                Positive(value.Range, path + ".Range", issues);
                NonNegative(value.Pierce, path + ".Pierce", issues);
                NonNegative(value.ExplosionRadius, path + ".ExplosionRadius", issues);
                NonNegative(value.AreaDamagePerTrigger, path + ".AreaDamagePerTrigger", issues);
                NonNegative(value.DotDps, path + ".DoTDPS", issues);
                NonNegative(value.DotDuration, path + ".DoTDuration", issues);
                NonNegative(value.PoolRadius, path + ".PoolRadius", issues);
                NonNegative(value.PoolDuration, path + ".PoolDuration", issues);
                NonNegative(value.ChainTargets, path + ".ChainTargets", issues);
                NonNegative(value.ChainRange, path + ".ChainRange", issues);
                NonNegative(value.Knockback, path + ".Knockback", issues);
                NonNegative(value.PowerCost, path + ".PowerCost", issues);
                NonNegative(value.HealingPerSecond, path + ".HealingPerSecond", issues);
                ValidateArtReferences(value.SideProfileArtReferences, path, issues);

                if (inputs != null)
                {
                    WeaponRarityInput rarity;
                    if (!inputs.Rarities.TryGetValue(value.Rarity, out rarity))
                    {
                        issues.Add(new WeaponCatalogIssue(
                            WeaponCatalogIssueCode.InvalidValue,
                            path + ".Rarity",
                            "Unknown rarity '" + value.Rarity + "'."));
                    }
                    else
                    {
                        CompareNumber(value.RarityWeight, rarity.Weight, path + ".RarityWeight", "rarity input weight", issues);
                        CompareNumber(value.EarlyTail, rarity.EarlyTail, path + ".EarlyTail", "rarity early tail", issues);
                        CompareNumber(value.LateTail, rarity.LateTail, path + ".LateTail", "rarity late tail", issues);
                    }

                    CompareNumber(
                        value.FinalBaseWeight,
                        value.RarityWeight * value.DefinitionWeightModifier,
                        path + ".FinalBaseWeight",
                        "rarity weight × definition modifier",
                        issues);

                    double expectedPowerIndex = inputs.CalculatePowerIndex(value.PowerAnchor);
                    CompareNumber(value.PowerIndex, expectedPowerIndex, path + ".PowerIndex", "power curve", issues);
                    double expectedTargetDps = inputs.BaseDps * expectedPowerIndex * value.ArchetypeDpsFactor / 100.0;
                    CompareNumber(value.TargetDps, expectedTargetDps, path + ".TargetDPS", "base DPS × power curve × archetype factor", issues);
                }

                WeaponArchetypeDefinition archetype;
                if (archetypes.TryGetValue(value.Archetype, out archetype))
                {
                    CompareNumber(value.ArchetypeDpsFactor, archetype.DpsFactor, path + ".ArchetypeDPSFactor", "archetype DPS factor", issues);
                }

                double directDps = value.DamagePerProjectile
                    * value.FireRate
                    * value.ProjectilesPerTrigger
                    * value.BurstCount;
                double areaDps = value.AreaDamagePerTrigger * value.FireRate;
                CompareNumber(directDps, value.TargetDps * value.DirectShare, path + ".DamagePerProjectile", "direct DPS share", issues);
                CompareNumber(areaDps, value.TargetDps * value.AreaShare, path + ".AreaDamagePerTrigger", "area DPS share", issues);
                CompareNumber(value.DotDps, value.TargetDps * value.DotShare, path + ".DoTDPS", "DoT DPS share", issues);
            }
        }

    }
}

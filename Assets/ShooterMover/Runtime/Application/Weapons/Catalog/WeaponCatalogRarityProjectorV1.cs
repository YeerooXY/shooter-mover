using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    public static class WeaponCatalogRarityProjectorV1
    {
        public static bool TryProject(
            WeaponCatalog source,
            IWeaponRarityNormalizationPolicyV1 policy,
            out WeaponCatalog projected,
            out string diagnostic)
        {
            projected = null;
            diagnostic = string.Empty;
            if (source == null)
            {
                diagnostic = "weapon-catalog-source-null";
                return false;
            }
            if (policy == null)
            {
                diagnostic = "weapon-rarity-normalization-policy-null";
                return false;
            }

            var archetypes =
                new Dictionary<string, WeaponArchetypeDefinition>(
                    StringComparer.Ordinal);
            foreach (
                KeyValuePair<string, WeaponArchetypeDefinition> pair
                    in source.Archetypes)
            {
                archetypes.Add(pair.Key, pair.Value);
            }

            var families = new List<WeaponFamilyDefinition>(
                source.Families.Count);
            for (int index = 0; index < source.Families.Count; index++)
            {
                WeaponFamilyDefinition family = source.Families[index];
                string mk1;
                string mk2;
                string mk3;
                if (!TryNormalizeRequired(
                        policy,
                        family.Mk1Rarity,
                        out mk1,
                        out diagnostic)
                    || !TryNormalizeOptional(
                        policy,
                        family.Mk2Rarity,
                        out mk2,
                        out diagnostic)
                    || !TryNormalizeOptional(
                        policy,
                        family.Mk3Rarity,
                        out mk3,
                        out diagnostic))
                {
                    diagnostic = "weapon-family-rarity-normalization-failed:"
                        + family.FamilyId
                        + ":"
                        + diagnostic;
                    return false;
                }

                families.Add(new WeaponFamilyDefinition(
                    family.FamilyId,
                    family.DisplayName,
                    family.Archetype,
                    family.DamageType,
                    family.BuildAffinity,
                    family.Mk1Peak,
                    family.GapMk1To2,
                    family.GapMk2To3,
                    family.MaxPlannedMark,
                    mk1,
                    mk2,
                    mk3,
                    family.DefinitionWeightModifier,
                    family.AcquisitionClass,
                    family.PrimaryEffect,
                    family.Notes,
                    family.Availability,
                    family.SideProfileArtReferences));
            }

            var definitions = new List<WeaponDefinitionData>(
                source.Definitions.Count);
            for (int index = 0; index < source.Definitions.Count; index++)
            {
                WeaponDefinitionData definition = source.Definitions[index];
                string normalized;
                if (!TryNormalizeRequired(
                    policy,
                    definition.Rarity,
                    out normalized,
                    out diagnostic))
                {
                    diagnostic =
                        "weapon-definition-rarity-normalization-failed:"
                        + definition.DefinitionId
                        + ":"
                        + diagnostic;
                    return false;
                }
                definitions.Add(CloneDefinition(
                    definition,
                    normalized,
                    definition.SideProfileArtReferences));
            }

            projected = new WeaponCatalog(
                source.Version,
                source.Status,
                source.Rules,
                source.Inputs,
                archetypes,
                families,
                definitions,
                policy.Fingerprint);
            return true;
        }

        internal static WeaponDefinitionData CloneDefinition(
            WeaponDefinitionData source,
            string rarity,
            IEnumerable<string> artReferences)
        {
            return new WeaponDefinitionData(
                source.DefinitionId,
                source.DisplayName,
                source.FamilyId,
                source.Mark,
                source.DamageType,
                source.Archetype,
                source.BuildAffinity,
                source.FirstAppearance,
                source.PeakDropLevel,
                source.PowerAnchor,
                rarity,
                source.RarityWeight,
                source.DefinitionWeightModifier,
                source.FinalBaseWeight,
                source.EarlyTail,
                source.LateTail,
                source.AcquisitionClass,
                source.TopBoxOnly,
                source.CraftingRoute,
                source.ArchetypeDpsFactor,
                source.PowerIndex,
                source.TargetDps,
                source.DirectShare,
                source.AreaShare,
                source.DotShare,
                source.FireRate,
                source.ProjectilesPerTrigger,
                source.BurstCount,
                source.DamagePerProjectile,
                source.SpreadDegrees,
                source.ProjectileSpeed,
                source.Range,
                source.Pierce,
                source.ExplosionRadius,
                source.AreaDamagePerTrigger,
                source.DotDps,
                source.DotDuration,
                source.PoolRadius,
                source.PoolDuration,
                source.ChainTargets,
                source.ChainRange,
                source.Knockback,
                source.PowerCost,
                source.HealingPerSecond,
                source.PrimaryEffect,
                source.Notes,
                source.Availability,
                artReferences);
        }

        private static bool TryNormalizeRequired(
            IWeaponRarityNormalizationPolicyV1 policy,
            string source,
            out string normalized,
            out string diagnostic)
        {
            if (!string.IsNullOrWhiteSpace(source)
                && policy.TryNormalize(source.Trim(), out normalized)
                && !string.IsNullOrWhiteSpace(normalized))
            {
                diagnostic = string.Empty;
                return true;
            }

            normalized = null;
            diagnostic = "unsupported-rarity:" + (source ?? string.Empty);
            return false;
        }

        private static bool TryNormalizeOptional(
            IWeaponRarityNormalizationPolicyV1 policy,
            string source,
            out string normalized,
            out string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                normalized = string.Empty;
                diagnostic = string.Empty;
                return true;
            }
            return TryNormalizeRequired(
                policy,
                source,
                out normalized,
                out diagnostic);
        }
    }
}

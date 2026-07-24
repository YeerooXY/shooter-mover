using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    public static partial class WeaponCatalogBlueprintMapper
    {
        private static void ResolveDamageCategory(
            WeaponDefinitionData definition,
            WeaponCatalogBlueprintMappingIntent intent,
            IList<WeaponBlueprintMappingIssue> issues,
            out WeaponDamageCategory category)
        {
            WeaponDamageCategory exact;
            bool hasExact = WeaponDamageCategoryConversion.TryFromCatalogValue(
                definition.DamageType,
                out exact);
            if (hasExact)
            {
                if (intent.ExplicitDamageCategory.HasValue
                    && intent.ExplicitDamageCategory.Value != exact)
                {
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.ConflictingDamageCategory,
                        Path(definition, ".DamageType"),
                        "Catalog value '" + definition.DamageType
                        + "' maps exactly to " + exact
                        + " but mapping intent requested "
                        + intent.ExplicitDamageCategory.Value + ".");
                }
                category = exact;
                return;
            }

            if (!intent.ExplicitDamageCategory.HasValue)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnsupportedDamageType,
                    Path(definition, ".DamageType"),
                    "Catalog damage type '" + definition.DamageType
                    + "' has no exact typed conversion. Supply an explicit category mapping.");
                category = default(WeaponDamageCategory);
                return;
            }

            category = intent.ExplicitDamageCategory.Value;
        }


        private static string ResolvePresentationReference(
            WeaponDefinitionData definition,
            WeaponFamilyDefinition family,
            string selectedReference,
            IList<WeaponBlueprintMappingIssue> issues)
        {
            var authored = new HashSet<string>(StringComparer.Ordinal);
            AddReferences(authored, definition.SideProfileArtReferences);
            if (family != null)
            {
                AddReferences(authored, family.SideProfileArtReferences);
            }

            if (!string.IsNullOrWhiteSpace(selectedReference))
            {
                if (!authored.Contains(selectedReference))
                {
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.UnauthoredPresentationReference,
                        Path(definition, ".SideProfileArtReferences"),
                        "Selected presentation reference '" + selectedReference
                        + "' is not authored by the definition or its family.");
                    return null;
                }
                return selectedReference;
            }

            if (authored.Count == 0)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingPresentationReference,
                    Path(definition, ".SideProfileArtReferences"),
                    "WeaponBlueprint requires one presentation reference, but none is authored.");
                return null;
            }
            if (authored.Count > 1)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.AmbiguousPresentationReference,
                    Path(definition, ".SideProfileArtReferences"),
                    "Multiple presentation references are authored. Select one explicitly without changing the catalog schema.");
                return null;
            }

            foreach (string value in authored)
            {
                return value;
            }
            return null;
        }

        private static void AddReferences(HashSet<string> destination, IReadOnlyList<string> values)
        {
            if (values == null)
            {
                return;
            }
            for (int index = 0; index < values.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    destination.Add(values[index]);
                }
            }
        }

        private static string Path(WeaponDefinitionData definition, string suffix)
        {
            return "weapon[" + definition.DefinitionId + "]" + suffix;
        }

        private static void Add(
            IList<WeaponBlueprintMappingIssue> issues,
            WeaponBlueprintMappingIssueCode code,
            string path,
            string detail)
        {
            issues.Add(new WeaponBlueprintMappingIssue(code, path, detail));
        }

    }
}

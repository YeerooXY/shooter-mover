using System;
using System.Collections.Generic;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Guns.Catalog
{
    public static partial class GunCatalogBlueprintMapper
    {
        private static void ResolveDamageCategory(
            GunDefinitionData definition,
            GunCatalogBlueprintMappingIntent intent,
            IList<GunMappingIssue> issues,
            out GunDamageCategory category)
        {
            GunDamageCategory exact;
            bool hasExact = GunDamageCategoryConversion.TryFromCatalogValue(
                definition.DamageType,
                out exact);
            if (hasExact)
            {
                if (intent.ExplicitDamageCategory.HasValue
                    && intent.ExplicitDamageCategory.Value != exact)
                {
                    Add(
                        issues,
                        GunMappingIssueCode.ConflictingDamageCategory,
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
                    GunMappingIssueCode.UnsupportedDamageType,
                    Path(definition, ".DamageType"),
                    "Catalog damage type '" + definition.DamageType
                    + "' has no exact typed conversion. Supply an explicit category mapping.");
                category = default(GunDamageCategory);
                return;
            }

            category = intent.ExplicitDamageCategory.Value;
        }


        private static string ResolvePresentationReference(
            GunDefinitionData definition,
            GunFamilyDefinition family,
            string selectedReference,
            IList<GunMappingIssue> issues)
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
                        GunMappingIssueCode.UnauthoredPresentationReference,
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
                    GunMappingIssueCode.MissingPresentationReference,
                    Path(definition, ".SideProfileArtReferences"),
                    "Gun requires one presentation reference, but none is authored.");
                return null;
            }
            if (authored.Count > 1)
            {
                Add(
                    issues,
                    GunMappingIssueCode.AmbiguousPresentationReference,
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

        private static string Path(GunDefinitionData definition, string suffix)
        {
            return "gun[" + definition.DefinitionId + "]" + suffix;
        }

        private static void Add(
            IList<GunMappingIssue> issues,
            GunMappingIssueCode code,
            string path,
            string detail)
        {
            issues.Add(new GunMappingIssue(code, path, detail));
        }

    }
}

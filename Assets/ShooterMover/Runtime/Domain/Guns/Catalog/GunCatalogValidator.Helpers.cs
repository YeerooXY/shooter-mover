using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShooterMover.Domain.Guns.Catalog
{
    public static partial class GunCatalogValidator
    {
        private static void ValidateFamilyRarity(
            string rarity,
            int mark,
            int maxPlannedMark,
            GunCatalogInputs inputs,
            string path,
            List<GunCatalogIssue> issues)
        {
            bool shouldExist = mark <= maxPlannedMark;
            if (shouldExist)
            {
                RequireText(rarity, path, issues);
                if (inputs != null && !string.IsNullOrWhiteSpace(rarity) && !inputs.Rarities.ContainsKey(rarity))
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.InvalidValue,
                        path,
                        "Unknown rarity '" + rarity + "'."));
                }
            }
            else if (!string.IsNullOrEmpty(rarity))
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.FamilyMarkMismatch,
                    path,
                    "Rarity must be empty for an unplanned mark."));
            }
        }

        private static void ValidateArtReferences(
            IReadOnlyList<string> values,
            string path,
            List<GunCatalogIssue> issues)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Count; index++)
            {
                string value = values[index];
                string itemPath = path + ".SideProfileArtReferences[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (string.IsNullOrWhiteSpace(value))
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.InvalidArtReference,
                        itemPath,
                        "Art reference cannot be empty."));
                }
                else if (!seen.Add(value))
                {
                    issues.Add(new GunCatalogIssue(
                        GunCatalogIssueCode.InvalidArtReference,
                        itemPath,
                        "Duplicate art reference '" + value + "'."));
                }
            }
        }

        private static void ValidateFamilyId(string value, string path, List<GunCatalogIssue> issues)
        {
            if (!IsFamilyId(value))
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.InvalidId,
                    path,
                    "Family IDs must use lower_snake_case and start with a letter."));
            }
        }

        private static void ValidateDefinitionId(string value, string path, List<GunCatalogIssue> issues)
        {
            if (string.IsNullOrEmpty(value))
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.InvalidId,
                    path,
                    "Definition ID is required."));
                return;
            }

            int separator = value.LastIndexOf(".mk", StringComparison.Ordinal);
            if (separator <= 0 || separator + 3 >= value.Length || !IsFamilyId(value.Substring(0, separator)))
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.InvalidId,
                    path,
                    "Definition IDs must use '<family>.mk<positive integer>'."));
                return;
            }

            int mark;
            if (!int.TryParse(value.Substring(separator + 3), NumberStyles.None, CultureInfo.InvariantCulture, out mark) || mark < 1)
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.InvalidId,
                    path,
                    "Definition mark suffix must be a positive integer."));
            }
        }

        private static bool IsFamilyId(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }
            for (int index = 1; index < value.Length; index++)
            {
                char current = value[index];
                bool valid = (current >= 'a' && current <= 'z')
                    || (current >= '0' && current <= '9')
                    || current == '_';
                if (!valid)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool ContainsOrdinal(IReadOnlyList<string> values, string expected)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void CompareText(
            string actual,
            string expected,
            string path,
            string source,
            List<GunCatalogIssue> issues)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.FamilyMarkMismatch,
                    path,
                    "Value must match " + source + " '" + expected + "'."));
            }
        }

        private static void CompareNumber(
            double actual,
            double expected,
            string path,
            string source,
            List<GunCatalogIssue> issues)
        {
            if (!IsFinite(actual) || !IsFinite(expected) || !NearlyEqual(actual, expected))
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.DerivedValueMismatch,
                    path,
                    "Value must match " + source + "; expected "
                        + expected.ToString("R", CultureInfo.InvariantCulture)
                        + ", actual " + actual.ToString("R", CultureInfo.InvariantCulture) + "."));
            }
        }

        private static bool NearlyEqual(double left, double right)
        {
            double scale = Math.Max(1.0, Math.Max(Math.Abs(left), Math.Abs(right)));
            return Math.Abs(left - right) <= scale * RelativeTolerance;
        }

        private static void RequireText(string value, string path, List<GunCatalogIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add(new GunCatalogIssue(
                    GunCatalogIssueCode.MissingRequiredField,
                    path,
                    "A non-empty string is required."));
            }
        }

        private static void Positive(double value, string path, List<GunCatalogIssue> issues)
        {
            if (!IsFinite(value) || value <= 0.0)
            {
                Range(path, "Value must be finite and greater than zero.", issues);
            }
        }

        private static void Positive(int value, string path, List<GunCatalogIssue> issues)
        {
            if (value <= 0)
            {
                Range(path, "Value must be greater than zero.", issues);
            }
        }

        private static void NonNegative(double value, string path, List<GunCatalogIssue> issues)
        {
            if (!IsFinite(value) || value < 0.0)
            {
                Range(path, "Value must be finite and non-negative.", issues);
            }
        }

        private static void NonNegative(int value, string path, List<GunCatalogIssue> issues)
        {
            if (value < 0)
            {
                Range(path, "Value must be non-negative.", issues);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void Range(string path, string detail, List<GunCatalogIssue> issues)
        {
            issues.Add(new GunCatalogIssue(GunCatalogIssueCode.RangeViolation, path, detail));
        }
    }
}

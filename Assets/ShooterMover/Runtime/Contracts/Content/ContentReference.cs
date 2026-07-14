using System;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Content
{
    /// <summary>
    /// Closed v1 taxonomy for registry-addressable content definitions.
    /// </summary>
    public enum ContentDefinitionKind
    {
        Unknown = 0,
        Weapon = 1,
        Enemy = 2,
        Room = 3,
        Encounter = 4,
        Environment = 5,
        SharedModule = 6
    }

    /// <summary>
    /// Immutable engine-independent reference to one expected content definition.
    /// </summary>
    public sealed class ContentReference : IEquatable<ContentReference>, IComparable<ContentReference>, IComparable
    {
        public const int SupportedDefinitionVersion = 1;

        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        private ContentReference(
            StableId definitionId,
            ContentDefinitionKind expectedKind,
            int expectedVersion)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            ExpectedKind = ContentDefinitionKindFormat.RequireKnown(expectedKind, nameof(expectedKind));
            ExpectedVersion = RequirePositiveVersion(expectedVersion, nameof(expectedVersion));
        }

        public StableId DefinitionId { get; }

        public ContentDefinitionKind ExpectedKind { get; }

        public int ExpectedVersion { get; }

        public static ContentReference Create(
            StableId definitionId,
            ContentDefinitionKind expectedKind,
            int expectedVersion)
        {
            return new ContentReference(definitionId, expectedKind, expectedVersion);
        }

        public static ContentReference ParseCanonical(string text)
        {
            string[] lines = SplitCanonicalLines(text, 3, nameof(ContentReference));
            StableId definitionId = StableId.Parse(ReadField(lines[0], "definition_id"));
            ContentDefinitionKind expectedKind = ContentDefinitionKindFormat.Parse(
                ReadField(lines[1], "expected_kind"));
            int expectedVersion = ParsePositiveVersion(
                ReadField(lines[2], "expected_version"),
                "expected_version");

            return Create(definitionId, expectedKind, expectedVersion);
        }

        public string ToCanonicalString()
        {
            return "definition_id="
                + DefinitionId
                + "\nexpected_kind="
                + ContentDefinitionKindFormat.ToCanonicalName(ExpectedKind)
                + "\nexpected_version="
                + ExpectedVersion.ToString(CultureInfo.InvariantCulture);
        }

        internal string ToCanonicalToken()
        {
            return ContentDefinitionKindFormat.ToCanonicalName(ExpectedKind)
                + "|"
                + DefinitionId
                + "|"
                + ExpectedVersion.ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(ContentReference other)
        {
            return !ReferenceEquals(other, null)
                && DefinitionId.Equals(other.DefinitionId)
                && ExpectedKind == other.ExpectedKind
                && ExpectedVersion == other.ExpectedVersion;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ContentReference);
        }

        public override int GetHashCode()
        {
            return DeterministicHash(ToCanonicalString());
        }

        public int CompareTo(ContentReference other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int kindComparison = string.CompareOrdinal(
                ContentDefinitionKindFormat.ToCanonicalName(ExpectedKind),
                ContentDefinitionKindFormat.ToCanonicalName(other.ExpectedKind));
            if (kindComparison != 0)
            {
                return kindComparison;
            }

            int idComparison = DefinitionId.CompareTo(other.DefinitionId);
            if (idComparison != 0)
            {
                return idComparison;
            }

            return ExpectedVersion.CompareTo(other.ExpectedVersion);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            ContentReference other = obj as ContentReference;
            if (other == null)
            {
                throw new ArgumentException(
                    $"Object must be of type {nameof(ContentReference)}.",
                    nameof(obj));
            }

            return CompareTo(other);
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        public static bool operator ==(ContentReference left, ContentReference right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(ContentReference left, ContentReference right)
        {
            return !(left == right);
        }

        internal static int RequirePositiveVersion(int value, string parameterName)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Content definition versions must be positive integers.");
            }

            return value;
        }

        internal static int DeterministicHash(string canonicalText)
        {
            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }

        private static int ParsePositiveVersion(string text, string fieldName)
        {
            if (string.IsNullOrEmpty(text) || text[0] < '1' || text[0] > '9')
            {
                throw new FormatException(
                    fieldName + " must be canonical positive decimal text without signs or leading zeroes.");
            }

            for (int index = 1; index < text.Length; index++)
            {
                if (text[index] < '0' || text[index] > '9')
                {
                    throw new FormatException(fieldName + " must contain decimal digits only.");
                }
            }

            int value;
            if (!int.TryParse(
                text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value)
                || value < 1)
            {
                throw new FormatException(fieldName + " is outside the supported positive integer range.");
            }

            return value;
        }

        private static string[] SplitCanonicalLines(string text, int expectedCount, string typeName)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.Length == 0 || text.IndexOf('\r') >= 0)
            {
                throw new FormatException(typeName + " is not canonical line-manifest text.");
            }

            string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
            if (lines.Length != expectedCount)
            {
                throw new FormatException(
                    typeName + " must contain exactly " + expectedCount + " ordered fields.");
            }

            return lines;
        }

        private static string ReadField(string line, string expectedName)
        {
            string prefix = expectedName + "=";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new FormatException(
                    "Expected canonical field " + expectedName + " in its defined position.");
            }

            return line.Substring(prefix.Length);
        }
    }

    internal static class ContentDefinitionKindFormat
    {
        public static ContentDefinitionKind RequireKnown(
            ContentDefinitionKind value,
            string parameterName)
        {
            if (!IsKnown(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Content definition kind is not supported by Content Definitions v1.");
            }

            return value;
        }

        public static bool IsKnown(ContentDefinitionKind value)
        {
            return value == ContentDefinitionKind.Weapon
                || value == ContentDefinitionKind.Enemy
                || value == ContentDefinitionKind.Room
                || value == ContentDefinitionKind.Encounter
                || value == ContentDefinitionKind.Environment
                || value == ContentDefinitionKind.SharedModule;
        }

        public static string ToCanonicalName(ContentDefinitionKind value)
        {
            switch (RequireKnown(value, nameof(value)))
            {
                case ContentDefinitionKind.Weapon:
                    return "weapon";
                case ContentDefinitionKind.Enemy:
                    return "enemy";
                case ContentDefinitionKind.Room:
                    return "room";
                case ContentDefinitionKind.Encounter:
                    return "encounter";
                case ContentDefinitionKind.Environment:
                    return "environment";
                case ContentDefinitionKind.SharedModule:
                    return "shared-module";
                default:
                    throw new InvalidOperationException("Unreachable content definition kind.");
            }
        }

        public static ContentDefinitionKind Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            switch (text)
            {
                case "weapon":
                    return ContentDefinitionKind.Weapon;
                case "enemy":
                    return ContentDefinitionKind.Enemy;
                case "room":
                    return ContentDefinitionKind.Room;
                case "encounter":
                    return ContentDefinitionKind.Encounter;
                case "environment":
                    return ContentDefinitionKind.Environment;
                case "shared-module":
                    return ContentDefinitionKind.SharedModule;
                default:
                    throw new FormatException(
                        "Unknown canonical content definition kind: " + text + ".");
            }
        }
    }
}

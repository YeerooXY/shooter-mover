using System;
using System.Globalization;

namespace ShooterMover.Contracts.Identity
{
    /// <summary>
    /// Immutable identity for one accepted content catalog snapshot.
    /// </summary>
    public sealed class ContentVersion : IEquatable<ContentVersion>
    {
        private ContentVersion(int catalogVersion, string definitionFingerprint)
        {
            CatalogVersion = IdentityContractFormat.RequirePositiveVersion(
                catalogVersion,
                nameof(catalogVersion));
            DefinitionFingerprint = IdentityContractFormat.RequireSha256(
                definitionFingerprint,
                nameof(definitionFingerprint));
        }

        public int CatalogVersion { get; }

        public string DefinitionFingerprint { get; }

        public static ContentVersion Create(int catalogVersion, string definitionFingerprint)
        {
            return new ContentVersion(catalogVersion, definitionFingerprint);
        }

        public static ContentVersion ParseCanonical(string text)
        {
            string[] lines = IdentityContractFormat.SplitCanonicalLines(
                text,
                2,
                nameof(ContentVersion));

            int catalogVersion = IdentityContractFormat.ParsePositiveVersion(
                IdentityContractFormat.ReadField(lines[0], "catalog_version"),
                "catalog_version");
            string definitionFingerprint = IdentityContractFormat.ReadField(
                lines[1],
                "definition_fingerprint");

            return Create(catalogVersion, definitionFingerprint);
        }

        public string ToCanonicalString()
        {
            return "catalog_version="
                + CatalogVersion.ToString(CultureInfo.InvariantCulture)
                + "\ndefinition_fingerprint="
                + DefinitionFingerprint;
        }

        public bool Equals(ContentVersion other)
        {
            return !ReferenceEquals(other, null)
                && CatalogVersion == other.CatalogVersion
                && string.Equals(
                    DefinitionFingerprint,
                    other.DefinitionFingerprint,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ContentVersion);
        }

        public override int GetHashCode()
        {
            return IdentityContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        public static bool operator ==(ContentVersion left, ContentVersion right)
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

        public static bool operator !=(ContentVersion left, ContentVersion right)
        {
            return !(left == right);
        }
    }

    internal static class IdentityContractFormat
    {
        public const string NullToken = "null";

        private const string Sha256Prefix = "sha256:";
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static int RequirePositiveVersion(int value, string parameterName)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Identity versions must be positive integers.");
            }

            return value;
        }

        public static int ParsePositiveVersion(string text, string fieldName)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new FormatException(fieldName + " must be a positive integer.");
            }

            if (text[0] < '1' || text[0] > '9')
            {
                throw new FormatException(
                    fieldName + " must be canonical decimal text without signs or leading zeroes.");
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

        public static string RequireSha256(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (value.Length != Sha256Prefix.Length + 64
                || !value.StartsWith(Sha256Prefix, StringComparison.Ordinal))
            {
                throw new FormatException(
                    parameterName + " must use canonical sha256:<64 lowercase hex characters> form.");
            }

            bool hasNonZeroDigit = false;
            for (int index = Sha256Prefix.Length; index < value.Length; index++)
            {
                char current = value[index];
                bool isDigit = current >= '0' && current <= '9';
                bool isLowerHex = current >= 'a' && current <= 'f';
                if (!isDigit && !isLowerHex)
                {
                    throw new FormatException(
                        parameterName + " must contain lowercase hexadecimal SHA-256 text only.");
                }

                if (current != '0')
                {
                    hasNonZeroDigit = true;
                }
            }

            if (!hasNonZeroDigit)
            {
                throw new FormatException(parameterName + " must not be an all-zero placeholder.");
            }

            return value;
        }

        public static string RequireSourceCommit(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (value.Length != 40)
            {
                throw new FormatException(
                    parameterName + " must be one complete 40-character lowercase Git commit SHA.");
            }

            bool hasNonZeroDigit = false;
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                bool isDigit = current >= '0' && current <= '9';
                bool isLowerHex = current >= 'a' && current <= 'f';
                if (!isDigit && !isLowerHex)
                {
                    throw new FormatException(
                        parameterName + " must contain lowercase hexadecimal Git commit text only.");
                }

                if (current != '0')
                {
                    hasNonZeroDigit = true;
                }
            }

            if (!hasNonZeroDigit)
            {
                throw new FormatException(parameterName + " must not be an all-zero placeholder.");
            }

            return value;
        }

        public static string RequireUnityVersion(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            int firstDot = value.IndexOf('.');
            int secondDot = firstDot < 0 ? -1 : value.IndexOf('.', firstDot + 1);
            if (firstDot <= 0 || secondDot <= firstDot + 1)
            {
                throw new FormatException(
                    parameterName + " must use canonical Unity version form such as 6000.3.19f1.");
            }

            int streamIndex = secondDot + 1;
            while (streamIndex < value.Length && IsAsciiDigit(value[streamIndex]))
            {
                streamIndex++;
            }

            if (!AreAsciiDigits(value, 0, firstDot)
                || !AreAsciiDigits(value, firstDot + 1, secondDot)
                || streamIndex == secondDot + 1
                || streamIndex >= value.Length - 1
                || !IsUnityReleaseStream(value[streamIndex])
                || !AreAsciiDigits(value, streamIndex + 1, value.Length))
            {
                throw new FormatException(
                    parameterName + " must use canonical Unity version form such as 6000.3.19f1.");
            }

            return value;
        }

        public static string[] SplitCanonicalLines(string text, int expectedCount, string typeName)
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

        public static string ReadField(string line, string expectedName)
        {
            string prefix = expectedName + "=";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new FormatException(
                    "Expected canonical field " + expectedName + " in its defined position.");
            }

            return line.Substring(prefix.Length);
        }

        public static int DeterministicHash(string canonicalText)
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

        private static bool AreAsciiDigits(string value, int startInclusive, int endExclusive)
        {
            if (startInclusive >= endExclusive)
            {
                return false;
            }

            for (int index = startInclusive; index < endExclusive; index++)
            {
                if (!IsAsciiDigit(value[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        private static bool IsUnityReleaseStream(char value)
        {
            return value == 'a' || value == 'b' || value == 'f' || value == 'p';
        }
    }
}

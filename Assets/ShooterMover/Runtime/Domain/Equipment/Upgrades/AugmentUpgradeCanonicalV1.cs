using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Equipment.Upgrades
{
    public static class AugmentUpgradeCanonicalV1
    {
        public static void AppendToken(
            StringBuilder builder,
            string name,
            string value)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            string normalized = value ?? "null";
            builder.Append(name)
                .Append(':')
                .Append(normalized.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(normalized)
                .Append('\n');
        }

        public static string Fingerprint(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonicalText));
            }

            var builder = new StringBuilder("sha256:");
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static StableId DeriveStableId(string prefix, string canonicalInput)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                throw new ArgumentException("Identity prefix is required.", nameof(prefix));
            }

            string fingerprint = Fingerprint(canonicalInput ?? "null");
            return StableId.Parse(prefix + "." + fingerprint.Substring(7, 48));
        }

        public static bool IsCanonicalFingerprint(string value)
        {
            if (value == null
                || value.Length != 71
                || !value.StartsWith("sha256:", StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = 7; index < value.Length; index++)
            {
                char current = value[index];
                if (!((current >= '0' && current <= '9')
                    || (current >= 'a' && current <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        public static int DeterministicHash(string value)
        {
            unchecked
            {
                int hash = 17;
                string normalized = value ?? string.Empty;
                for (int index = 0; index < normalized.Length; index++)
                {
                    hash = (hash * 31) + normalized[index];
                }

                return hash;
            }
        }
    }
}

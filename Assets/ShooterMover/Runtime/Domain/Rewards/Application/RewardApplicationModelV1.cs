using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Application
{
    /// <summary>
    /// Monotonic durable lifecycle owned exclusively by RAP-001.
    /// </summary>
    public enum RewardCommitmentStateV1
    {
        Generated = 1,
        Projected = 2,
        Claimed = 3,
        Applied = 4,
        Cancelled = 5,
    }

    public enum RewardChildResolutionStateV1
    {
        Pending = 1,
        Applied = 2,
    }

    /// <summary>
    /// Shared deterministic canonicalization helpers for reward-application contracts.
    /// </summary>
    public static class RewardApplicationCanonicalV1
    {
        private const string FingerprintPrefix = "sha256:";
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static void AppendToken(StringBuilder builder, string name, string value)
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
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalText));
            }

            var builder = new StringBuilder(FingerprintPrefix, 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static bool IsCanonicalFingerprint(string value)
        {
            if (value == null
                || value.Length != 71
                || !value.StartsWith(FingerprintPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = FingerprintPrefix.Length; index < value.Length; index++)
            {
                char current = value[index];
                bool digit = current >= '0' && current <= '9';
                bool lowerHex = current >= 'a' && current <= 'f';
                if (!digit && !lowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        public static StableId DeriveStableId(string namespaceName, params string[] components)
        {
            if (namespaceName == null)
            {
                throw new ArgumentNullException(nameof(namespaceName));
            }

            if (components == null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            var builder = new StringBuilder();
            AppendToken(builder, "namespace", namespaceName);
            for (int index = 0; index < components.Length; index++)
            {
                AppendToken(
                    builder,
                    "component_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    components[index]);
            }

            string fingerprint = Fingerprint(builder.ToString());
            return StableId.Create(namespaceName, fingerprint.Substring(7, 48));
        }

        public static int DeterministicHash(string canonicalText)
        {
            if (canonicalText == null)
            {
                return 0;
            }

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

        public static string OptionalId(StableId stableId)
        {
            return stableId == null ? "none" : stableId.ToString();
        }

        public static string OptionalLong(long? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "none";
        }
    }
}

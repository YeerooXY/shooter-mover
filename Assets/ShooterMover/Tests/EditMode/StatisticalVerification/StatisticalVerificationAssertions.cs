using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace ShooterMover.Tests.EditMode.StatisticalVerification
{
    internal static class StatisticalVerificationAssertions
    {
        public static void Proportion(
            string label,
            long observed,
            long total,
            double minimumInclusive,
            double maximumInclusive)
        {
            Assert.That(total, Is.GreaterThan(0L), label + " requires a non-empty sample.");
            double actual = (double)observed / total;
            Assert.That(
                actual,
                Is.InRange(minimumInclusive, maximumInclusive),
                label + " observed " + actual.ToString("P3", CultureInfo.InvariantCulture)
                    + " from " + observed.ToString(CultureInfo.InvariantCulture)
                    + "/" + total.ToString(CultureInfo.InvariantCulture)
                    + ", expected band "
                    + minimumInclusive.ToString("P1", CultureInfo.InvariantCulture)
                    + ".." + maximumInclusive.ToString("P1", CultureInfo.InvariantCulture) + ".");
        }

        public static double Mean(long total, long count)
        {
            Assert.That(count, Is.GreaterThan(0L), "Mean requires a non-empty sample.");
            return (double)total / count;
        }

        public static string Fingerprint(IEnumerable<string> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            StringBuilder canonical = new StringBuilder();
            int index = 0;
            foreach (string value in values)
            {
                canonical.Append(index.ToString("D6", CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(value ?? "<null>")
                    .Append('\n');
                index++;
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                StringBuilder hex = new StringBuilder(digest.Length * 2);
                for (int byteIndex = 0; byteIndex < digest.Length; byteIndex++)
                {
                    hex.Append(digest[byteIndex].ToString("x2", CultureInfo.InvariantCulture));
                }

                return "sha256:" + hex;
            }
        }

        public static ulong Seed(ulong rootSeed, int ordinal)
        {
            unchecked
            {
                ulong mixed = rootSeed + 0x9E3779B97F4A7C15UL * (ulong)(ordinal + 1);
                mixed ^= mixed >> 30;
                mixed *= 0xBF58476D1CE4E5B9UL;
                mixed ^= mixed >> 27;
                mixed *= 0x94D049BB133111EBUL;
                return mixed ^ (mixed >> 31);
            }
        }
    }
}

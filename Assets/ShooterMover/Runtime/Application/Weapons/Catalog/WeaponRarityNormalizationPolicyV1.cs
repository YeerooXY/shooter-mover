using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ShooterMover.Application.Weapons.Catalog
{
    public sealed class WeaponRarityNormalizationRuleV1
    {
        public WeaponRarityNormalizationRuleV1(
            string sourceRarity,
            string normalizedRarity)
        {
            if (string.IsNullOrWhiteSpace(sourceRarity))
            {
                throw new ArgumentException(
                    "A source rarity identity is required.",
                    nameof(sourceRarity));
            }
            if (string.IsNullOrWhiteSpace(normalizedRarity))
            {
                throw new ArgumentException(
                    "A normalized rarity identity is required.",
                    nameof(normalizedRarity));
            }
            SourceRarity = sourceRarity.Trim();
            NormalizedRarity = normalizedRarity.Trim();
        }

        public string SourceRarity { get; }
        public string NormalizedRarity { get; }
    }

    public interface IWeaponRarityNormalizationPolicyV1
    {
        string PolicyId { get; }
        string Fingerprint { get; }
        IReadOnlyList<WeaponRarityNormalizationRuleV1> Rules { get; }

        bool TryNormalize(
            string sourceRarity,
            out string normalizedRarity);
    }

    public sealed class WeaponRarityNormalizationPolicyV1 :
        IWeaponRarityNormalizationPolicyV1
    {
        public const string MythicArtifact = "MythicArtifact";

        private readonly ReadOnlyCollection<
            WeaponRarityNormalizationRuleV1> rules;
        private readonly Dictionary<string, string>
            normalizedBySource;

        public WeaponRarityNormalizationPolicyV1(
            string policyId,
            IEnumerable<WeaponRarityNormalizationRuleV1>
                normalizationRules)
        {
            if (string.IsNullOrWhiteSpace(policyId))
            {
                throw new ArgumentException(
                    "A rarity normalization policy identity is required.",
                    nameof(policyId));
            }

            PolicyId = policyId.Trim();
            var ordered =
                new List<WeaponRarityNormalizationRuleV1>(
                    normalizationRules
                    ?? throw new ArgumentNullException(
                        nameof(normalizationRules)));
            ordered.Sort(delegate(
                WeaponRarityNormalizationRuleV1 left,
                WeaponRarityNormalizationRuleV1 right)
            {
                return string.CompareOrdinal(
                    left.SourceRarity,
                    right.SourceRarity);
            });

            normalizedBySource =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);
            for (int index = 0; index < ordered.Count; index++)
            {
                WeaponRarityNormalizationRuleV1 rule =
                    ordered[index]
                    ?? throw new ArgumentException(
                        "Rarity normalization rules must be non-null.",
                        nameof(normalizationRules));
                if (normalizedBySource.ContainsKey(
                        rule.SourceRarity))
                {
                    throw new ArgumentException(
                        "Duplicate rarity normalization source '"
                        + rule.SourceRarity
                        + "'.",
                        nameof(normalizationRules));
                }
                normalizedBySource.Add(
                    rule.SourceRarity,
                    rule.NormalizedRarity);
            }
            if (ordered.Count == 0)
            {
                throw new ArgumentException(
                    "At least one rarity normalization rule is required.",
                    nameof(normalizationRules));
            }

            rules = new ReadOnlyCollection<
                WeaponRarityNormalizationRuleV1>(ordered);
            Fingerprint = CalculateFingerprint(
                PolicyId,
                rules);
        }

        public string PolicyId { get; }
        public string Fingerprint { get; }
        public IReadOnlyList<WeaponRarityNormalizationRuleV1> Rules
        {
            get { return rules; }
        }

        public bool TryNormalize(
            string sourceRarity,
            out string normalizedRarity)
        {
            return normalizedBySource.TryGetValue(
                (sourceRarity ?? string.Empty).Trim(),
                out normalizedRarity);
        }

        public static WeaponRarityNormalizationPolicyV1
            CreateBaselineV1()
        {
            return new WeaponRarityNormalizationPolicyV1(
                "weapon-rarity-normalization-v1",
                new[]
                {
                    new WeaponRarityNormalizationRuleV1(
                        "Common",
                        "Common"),
                    new WeaponRarityNormalizationRuleV1(
                        "Uncommon",
                        "Common"),
                    new WeaponRarityNormalizationRuleV1(
                        "Rare",
                        "Rare"),
                    new WeaponRarityNormalizationRuleV1(
                        "Epic",
                        "Epic"),
                    new WeaponRarityNormalizationRuleV1(
                        "Legendary",
                        "Legendary"),
                    new WeaponRarityNormalizationRuleV1(
                        "Mythic",
                        MythicArtifact),
                    new WeaponRarityNormalizationRuleV1(
                        "Artifact",
                        MythicArtifact),
                });
        }

        private static string CalculateFingerprint(
            string policyId,
            IReadOnlyList<WeaponRarityNormalizationRuleV1> values)
        {
            var builder = new StringBuilder();
            Append(builder, "policy", policyId);
            for (int index = 0; index < values.Count; index++)
            {
                Append(
                    builder,
                    "source",
                    values[index].SourceRarity);
                Append(
                    builder,
                    "normalized",
                    values[index].NormalizedRarity);
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(builder.ToString()));
                var hex = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    hex.Append(digest[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return "sha256:" + hex;
            }
        }

        private static void Append(
            StringBuilder builder,
            string name,
            string value)
        {
            string text = value ?? string.Empty;
            builder.Append(
                    name.Length.ToString(
                        CultureInfo.InvariantCulture))
                .Append(':')
                .Append(name)
                .Append('=')
                .Append(
                    text.Length.ToString(
                        CultureInfo.InvariantCulture))
                .Append(':')
                .Append(text)
                .Append('\n');
        }
    }
}

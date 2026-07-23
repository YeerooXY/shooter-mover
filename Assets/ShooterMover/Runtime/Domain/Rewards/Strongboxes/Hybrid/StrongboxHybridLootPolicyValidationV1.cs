using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    internal static class StrongboxHybridLootPolicyValidationV1
    {
        internal static ReadOnlyCollection<StrongboxDistanceWeightV1> CopyDistanceWeights(
            IEnumerable<StrongboxDistanceWeightV1> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var copy = new List<StrongboxDistanceWeightV1>();
            foreach (StrongboxDistanceWeightV1 value in values)
            {
                if (value == null)
                {
                    throw new ArgumentException(
                        "Definition bell weights must not contain null entries.",
                        nameof(values));
                }
                copy.Add(value);
            }
            copy.Sort();
            if (copy.Count == 0 || copy[0].Distance != 0)
            {
                throw new ArgumentException(
                    "Definition bell weights must begin at distance zero.",
                    nameof(values));
            }
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index].Distance != index)
                {
                    throw new ArgumentException(
                        "Definition bell weights must cover every distance contiguously.",
                        nameof(values));
                }
            }
            return new ReadOnlyCollection<StrongboxDistanceWeightV1>(copy);
        }

        internal static ReadOnlyCollection<StrongboxWeightedIntOutcomeV1> CopyOutcomes(
            IEnumerable<StrongboxWeightedIntOutcomeV1> values,
            string parameterName,
            int minimumValue)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<StrongboxWeightedIntOutcomeV1>();
            var seen = new HashSet<int>();
            foreach (StrongboxWeightedIntOutcomeV1 value in values)
            {
                if (value == null || !seen.Add(value.Value))
                {
                    throw new ArgumentException(
                        "Weighted outcomes must be non-null and have unique values.",
                        parameterName);
                }
                if (value.Value < minimumValue)
                {
                    throw new ArgumentOutOfRangeException(parameterName);
                }
                copy.Add(value);
            }
            copy.Sort();
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one weighted outcome is required.",
                    parameterName);
            }
            return new ReadOnlyCollection<StrongboxWeightedIntOutcomeV1>(copy);
        }

        internal static ReadOnlyCollection<StrongboxRarityProfileV1> CopyRarities(
            IEnumerable<StrongboxRarityProfileV1> values,
            out Dictionary<StableId, StrongboxRarityProfileV1> byId)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var copy = new List<StrongboxRarityProfileV1>();
            byId = new Dictionary<StableId, StrongboxRarityProfileV1>();
            foreach (StrongboxRarityProfileV1 value in values)
            {
                if (value == null || byId.ContainsKey(value.RarityId))
                {
                    throw new ArgumentException(
                        "Rarity profiles must be non-null and unique.",
                        nameof(values));
                }
                byId.Add(value.RarityId, value);
                copy.Add(value);
            }
            copy.Sort();
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one rarity profile is required.",
                    nameof(values));
            }
            return new ReadOnlyCollection<StrongboxRarityProfileV1>(copy);
        }

        internal static void ValidateOutcomeValues(
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> augmentSlotOutcomes,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> augmentLevelOutcomes)
        {
            for (int index = 0; index < augmentLevelOutcomes.Count; index++)
            {
                if (augmentLevelOutcomes[index].Value > 11)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(augmentLevelOutcomes),
                        "V1 supports shared augment levels through level 11.");
                }
            }
            for (int index = 0; index < augmentSlotOutcomes.Count; index++)
            {
                if (augmentSlotOutcomes[index].Value > 4)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(augmentSlotOutcomes),
                        "V1 supports authored weapon slot outcomes through four slots.");
                }
            }
        }

        internal static string BuildCanonicalText(
            StableId policyId,
            int minimumTargetDelta,
            int mostLikelyTargetDelta,
            int maximumTargetDelta,
            int targetBlendPermille,
            IReadOnlyList<StrongboxDistanceWeightV1> definitionBellWeights,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> instanceLevelOffsets,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> augmentSlotOutcomes,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> augmentLevelOutcomes,
            IReadOnlyList<StrongboxRarityProfileV1> rarityProfiles)
        {
            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "schema", "strongbox-hybrid-loot-policy-v1");
            StrongboxCanonicalV1.AppendToken(builder, "policy_id", policyId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "minimum_target_delta", minimumTargetDelta.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "most_likely_target_delta", mostLikelyTargetDelta.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "maximum_target_delta", maximumTargetDelta.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "target_blend_permille", targetBlendPermille.ToString(CultureInfo.InvariantCulture));
            AppendDistanceWeights(builder, definitionBellWeights);
            AppendOutcomes(builder, "instance_level_offset", instanceLevelOffsets);
            AppendOutcomes(builder, "augment_slot", augmentSlotOutcomes);
            AppendOutcomes(builder, "augment_level", augmentLevelOutcomes);
            StrongboxCanonicalV1.AppendToken(builder, "rarity_count", rarityProfiles.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < rarityProfiles.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    "rarity_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    rarityProfiles[index].ToCanonicalString());
            }
            return builder.ToString();
        }

        private static void AppendDistanceWeights(
            StringBuilder builder,
            IReadOnlyList<StrongboxDistanceWeightV1> values)
        {
            StrongboxCanonicalV1.AppendToken(builder, "bell_weight_count", values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    "bell_weight_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    values[index].ToCanonicalString());
            }
        }

        private static void AppendOutcomes(
            StringBuilder builder,
            string prefix,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> values)
        {
            StrongboxCanonicalV1.AppendToken(builder, prefix + "_count", values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    prefix + "_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    values[index].ToCanonicalString());
            }
        }
    }
}

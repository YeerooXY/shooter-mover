using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    internal static class StrongboxHybridLootPolicyValidation
    {
        internal static ReadOnlyCollection<StrongboxDistanceWeight> CopyDistanceWeights(
            IEnumerable<StrongboxDistanceWeight> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var copy = new List<StrongboxDistanceWeight>();
            foreach (StrongboxDistanceWeight value in values)
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
            return new ReadOnlyCollection<StrongboxDistanceWeight>(copy);
        }

        internal static ReadOnlyCollection<StrongboxWeightedIntOutcome> CopyOutcomes(
            IEnumerable<StrongboxWeightedIntOutcome> values,
            string parameterName,
            int minimumValue)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<StrongboxWeightedIntOutcome>();
            var seen = new HashSet<int>();
            foreach (StrongboxWeightedIntOutcome value in values)
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
            return new ReadOnlyCollection<StrongboxWeightedIntOutcome>(copy);
        }

        internal static ReadOnlyCollection<StrongboxRarityProfile> CopyRarities(
            IEnumerable<StrongboxRarityProfile> values,
            out Dictionary<StableId, StrongboxRarityProfile> byId)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var copy = new List<StrongboxRarityProfile>();
            byId = new Dictionary<StableId, StrongboxRarityProfile>();
            foreach (StrongboxRarityProfile value in values)
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
            return new ReadOnlyCollection<StrongboxRarityProfile>(copy);
        }

        internal static void ValidateOutcomeValues(
            IReadOnlyList<StrongboxWeightedIntOutcome> augmentSlotOutcomes,
            IReadOnlyList<StrongboxWeightedIntOutcome> augmentLevelOutcomes)
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
            IReadOnlyList<StrongboxDistanceWeight> definitionBellWeights,
            IReadOnlyList<StrongboxWeightedIntOutcome> instanceLevelOffsets,
            IReadOnlyList<StrongboxWeightedIntOutcome> augmentSlotOutcomes,
            IReadOnlyList<StrongboxWeightedIntOutcome> augmentLevelOutcomes,
            IReadOnlyList<StrongboxRarityProfile> rarityProfiles)
        {
            var builder = new StringBuilder();
            Strongbox.AppendToken(builder, "schema", "strongbox-hybrid-loot-policy-v1");
            Strongbox.AppendToken(builder, "policy_id", policyId.ToString());
            Strongbox.AppendToken(builder, "minimum_target_delta", minimumTargetDelta.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "most_likely_target_delta", mostLikelyTargetDelta.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "maximum_target_delta", maximumTargetDelta.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "target_blend_permille", targetBlendPermille.ToString(CultureInfo.InvariantCulture));
            AppendDistanceWeights(builder, definitionBellWeights);
            AppendOutcomes(builder, "instance_level_offset", instanceLevelOffsets);
            AppendOutcomes(builder, "augment_slot", augmentSlotOutcomes);
            AppendOutcomes(builder, "augment_level", augmentLevelOutcomes);
            Strongbox.AppendToken(builder, "rarity_count", rarityProfiles.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < rarityProfiles.Count; index++)
            {
                Strongbox.AppendToken(
                    builder,
                    "rarity_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    rarityProfiles[index].ToCanonicalString());
            }
            return builder.ToString();
        }

        private static void AppendDistanceWeights(
            StringBuilder builder,
            IReadOnlyList<StrongboxDistanceWeight> values)
        {
            Strongbox.AppendToken(builder, "bell_weight_count", values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                Strongbox.AppendToken(
                    builder,
                    "bell_weight_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    values[index].ToCanonicalString());
            }
        }

        private static void AppendOutcomes(
            StringBuilder builder,
            string prefix,
            IReadOnlyList<StrongboxWeightedIntOutcome> values)
        {
            Strongbox.AppendToken(builder, prefix + "_count", values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                Strongbox.AppendToken(
                    builder,
                    prefix + "_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    values[index].ToCanonicalString());
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    /// <summary>
    /// Temporary source-compatible bridge. New Strongbox loot code uses LootValidator.
    /// Remove this bridge after the remaining policy caller is migrated and Unity compiles.
    /// </summary>
    [Obsolete("Use LootValidator. This compatibility bridge will be removed.")]
    internal static class StrongboxHybridLootPolicyValidation
    {
        internal static ReadOnlyCollection<StrongboxDistanceWeight> CopyDistanceWeights(
            IEnumerable<StrongboxDistanceWeight> values)
        {
            return LootValidator.CopyDistanceWeights(values);
        }

        internal static ReadOnlyCollection<StrongboxWeightedIntOutcome> CopyOutcomes(
            IEnumerable<StrongboxWeightedIntOutcome> values,
            string parameterName,
            int minimumValue)
        {
            return LootValidator.CopyOutcomes(values, parameterName, minimumValue);
        }

        internal static ReadOnlyCollection<StrongboxRarityProfile> CopyRarities(
            IEnumerable<StrongboxRarityProfile> values,
            out Dictionary<StableId, StrongboxRarityProfile> byId)
        {
            return LootValidator.CopyRarities(values, out byId);
        }

        internal static void ValidateOutcomeValues(
            IReadOnlyList<StrongboxWeightedIntOutcome> augmentSlotOutcomes,
            IReadOnlyList<StrongboxWeightedIntOutcome> augmentLevelOutcomes)
        {
            LootValidator.ValidateOutcomeValues(
                augmentSlotOutcomes,
                augmentLevelOutcomes);
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
            return LootValidator.BuildCanonicalText(
                policyId,
                minimumTargetDelta,
                mostLikelyTargetDelta,
                maximumTargetDelta,
                targetBlendPermille,
                definitionBellWeights,
                instanceLevelOffsets,
                augmentSlotOutcomes,
                augmentLevelOutcomes,
                rarityProfiles);
        }
    }
}

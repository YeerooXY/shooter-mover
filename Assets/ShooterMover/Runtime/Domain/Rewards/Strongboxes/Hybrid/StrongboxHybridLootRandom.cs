using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    internal static class StrongboxHybridLootRandom
    {
        private const int SlotBiasPerStepMilli = 55;
        private const int LevelBiasPerStepMilli = 35;
        private const int MinimumBiasMultiplierMilli = 50;
        private const int MaximumBiasMultiplierMilli = 5000;

        private static readonly StableId TargetPurposeId =
            StableId.Parse("strongbox-rng.hybrid-target-v1");
        private static readonly StableId InstanceLevelPurposeId =
            StableId.Parse("strongbox-rng.hybrid-instance-level-v1");
        private static readonly StableId AugmentSlotsPurposeId =
            StableId.Parse("strongbox-rng.hybrid-augment-slots-v1");
        private static readonly StableId AugmentLevelPurposeId =
            StableId.Parse("strongbox-rng.hybrid-augment-level-v1");

        internal static StrongboxTargetLevelRoll RollTargetLevel(
            StrongboxHybridLootPolicy policy,
            int playerLevel,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            List<StrongboxWeightedIntOutcome> triangular =
                BuildTriangularTargetOutcomes(policy);
            DeterministicRandom stream = DeterministicRandom.CreateSubstream(
                rootSeed,
                algorithmVersion,
                TargetPurposeId,
                equipmentSlotOrdinal);
            int delta;
            stream = RollWeighted(stream, triangular, out delta);
            int unclamped = checked(playerLevel + delta);
            int target = Math.Max(1, unclamped);
            return new StrongboxTargetLevelRoll(
                policy.PolicyId,
                playerLevel,
                delta,
                unclamped,
                target,
                stream.GetTrace().SamplesConsumed,
                policy.Fingerprint);
        }

        internal static StrongboxInstanceLevelRoll RollInstanceLevel(
            StrongboxHybridLootPolicy policy,
            StrongboxTargetLevelRoll targetRoll,
            int definitionPeakLevel,
            StableId rarityId,
            IReadOnlyList<StrongboxWeightedIntOutcome> instanceLevelOffsets,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            long blended = checked(
                (long)targetRoll.TargetLevel * policy.TargetBlendPermille
                + (long)definitionPeakLevel
                    * (StrongboxHybridLootPolicy.BlendScale - policy.TargetBlendPermille));
            int center = Math.Max(1, CheckedInt(DivideRounded(
                blended,
                StrongboxHybridLootPolicy.BlendScale)));

            DeterministicRandom stream = DeterministicRandom.CreateSubstream(
                rootSeed,
                algorithmVersion,
                InstanceLevelPurposeId,
                equipmentSlotOrdinal);
            int offset;
            stream = RollWeighted(stream, instanceLevelOffsets, out offset);
            int itemLevel = Math.Max(1, checked(center + offset));
            return new StrongboxInstanceLevelRoll(
                targetRoll,
                definitionPeakLevel,
                rarityId,
                center,
                offset,
                itemLevel,
                stream.GetTrace().SamplesConsumed,
                policy.Fingerprint);
        }

        internal static StrongboxAugmentSignature RollAugmentSignature(
            StrongboxHybridLootPolicy policy,
            int playerLevel,
            int itemLevel,
            StrongboxRarityProfile rarity,
            int normalMaximumSlots,
            int absoluteMaximumSlots,
            IReadOnlyList<StrongboxWeightedIntOutcome> augmentSlotOutcomes,
            IReadOnlyList<StrongboxWeightedIntOutcome> augmentLevelOutcomes,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            int bias = Clamp(
                checked(playerLevel - itemLevel + rarity.AugmentBiasLevels),
                -12,
                12);
            List<StrongboxWeightedIntOutcome> mappedSlots =
                MapAndAdjustSlotOutcomes(
                    augmentSlotOutcomes,
                    normalMaximumSlots,
                    absoluteMaximumSlots,
                    bias);
            DeterministicRandom slotStream = DeterministicRandom.CreateSubstream(
                rootSeed,
                algorithmVersion,
                AugmentSlotsPurposeId,
                equipmentSlotOrdinal);
            int mappedSlotCount;
            slotStream = RollWeighted(slotStream, mappedSlots, out mappedSlotCount);
            int authoredSlotOutcome = ResolveRepresentativeAuthoredSlot(
                augmentSlotOutcomes,
                mappedSlotCount,
                normalMaximumSlots,
                absoluteMaximumSlots);

            int sharedLevel = 0;
            ulong levelSamples = 0UL;
            if (mappedSlotCount > 0)
            {
                List<StrongboxWeightedIntOutcome> adjustedLevels =
                    AdjustOutcomes(augmentLevelOutcomes, bias, LevelBiasPerStepMilli);
                DeterministicRandom levelStream = DeterministicRandom.CreateSubstream(
                    rootSeed,
                    algorithmVersion,
                    AugmentLevelPurposeId,
                    equipmentSlotOrdinal);
                levelStream = RollWeighted(levelStream, adjustedLevels, out sharedLevel);
                levelSamples = levelStream.GetTrace().SamplesConsumed;
            }

            return new StrongboxAugmentSignature(
                policy.PolicyId,
                rarity.RarityId,
                playerLevel,
                itemLevel,
                bias,
                normalMaximumSlots,
                absoluteMaximumSlots,
                authoredSlotOutcome,
                mappedSlotCount,
                sharedLevel,
                slotStream.GetTrace().SamplesConsumed,
                levelSamples,
                policy.Fingerprint);
        }

        private static List<StrongboxWeightedIntOutcome> BuildTriangularTargetOutcomes(
            StrongboxHybridLootPolicy policy)
        {
            var outcomes = new List<StrongboxWeightedIntOutcome>();
            int leftScale = checked(
                policy.MaximumTargetDelta - policy.MostLikelyTargetDelta + 1);
            int rightScale = checked(
                policy.MostLikelyTargetDelta - policy.MinimumTargetDelta + 1);
            for (int delta = policy.MinimumTargetDelta;
                 delta <= policy.MaximumTargetDelta;
                 delta++)
            {
                long weight = delta <= policy.MostLikelyTargetDelta
                    ? checked((long)(delta - policy.MinimumTargetDelta + 1) * leftScale)
                    : checked((long)(policy.MaximumTargetDelta - delta + 1) * rightScale);
                outcomes.Add(new StrongboxWeightedIntOutcome(
                    delta,
                    checked((ulong)weight)));
            }
            return outcomes;
        }

        private static List<StrongboxWeightedIntOutcome> MapAndAdjustSlotOutcomes(
            IReadOnlyList<StrongboxWeightedIntOutcome> augmentSlotOutcomes,
            int normalMaximumSlots,
            int absoluteMaximumSlots,
            int bias)
        {
            var accumulated = new SortedDictionary<int, ulong>();
            for (int index = 0; index < augmentSlotOutcomes.Count; index++)
            {
                StrongboxWeightedIntOutcome authored = augmentSlotOutcomes[index];
                int mapped = MapAuthoredSlotCount(
                    authored.Value,
                    normalMaximumSlots,
                    absoluteMaximumSlots);
                ulong existing;
                accumulated.TryGetValue(mapped, out existing);
                accumulated[mapped] = checked(existing + authored.Weight);
            }

            var mappedOutcomes = new List<StrongboxWeightedIntOutcome>();
            foreach (KeyValuePair<int, ulong> pair in accumulated)
            {
                mappedOutcomes.Add(new StrongboxWeightedIntOutcome(pair.Key, pair.Value));
            }
            return AdjustOutcomes(mappedOutcomes, bias, SlotBiasPerStepMilli);
        }

        private static int MapAuthoredSlotCount(
            int authoredSlotCount,
            int normalMaximumSlots,
            int absoluteMaximumSlots)
        {
            if (authoredSlotCount <= StrongboxHybridLootPolicy.AuthoredNormalGunSlots)
            {
                return Math.Min(authoredSlotCount, normalMaximumSlots);
            }

            int overcapSteps =
                authoredSlotCount - StrongboxHybridLootPolicy.AuthoredNormalGunSlots;
            return Math.Min(
                checked(normalMaximumSlots + overcapSteps),
                absoluteMaximumSlots);
        }

        private static int ResolveRepresentativeAuthoredSlot(
            IReadOnlyList<StrongboxWeightedIntOutcome> augmentSlotOutcomes,
            int mappedSlotCount,
            int normalMaximumSlots,
            int absoluteMaximumSlots)
        {
            for (int index = 0; index < augmentSlotOutcomes.Count; index++)
            {
                int authored = augmentSlotOutcomes[index].Value;
                if (MapAuthoredSlotCount(
                    authored,
                    normalMaximumSlots,
                    absoluteMaximumSlots) == mappedSlotCount)
                {
                    return authored;
                }
            }
            return mappedSlotCount;
        }

        private static List<StrongboxWeightedIntOutcome> AdjustOutcomes(
            IReadOnlyList<StrongboxWeightedIntOutcome> source,
            int bias,
            int slopeMilli)
        {
            var adjusted = new List<StrongboxWeightedIntOutcome>(source.Count);
            int minimumValue = source[0].Value;
            for (int index = 0; index < source.Count; index++)
            {
                StrongboxWeightedIntOutcome outcome = source[index];
                int steps = outcome.Value - minimumValue;
                int multiplier = Clamp(
                    checked(
                        StrongboxHybridLootPolicy.RarityMultiplierScale
                        + bias * slopeMilli * steps),
                    MinimumBiasMultiplierMilli,
                    MaximumBiasMultiplierMilli);
                ulong weight = checked(outcome.Weight * checked((ulong)multiplier));
                adjusted.Add(new StrongboxWeightedIntOutcome(outcome.Value, weight));
            }
            return adjusted;
        }

        private static DeterministicRandom RollWeighted(
            DeterministicRandom stream,
            IReadOnlyList<StrongboxWeightedIntOutcome> outcomes,
            out int value)
        {
            if (outcomes == null || outcomes.Count == 0)
            {
                throw new ArgumentException(
                    "At least one weighted outcome is required.",
                    nameof(outcomes));
            }

            ulong total = 0UL;
            for (int index = 0; index < outcomes.Count; index++)
            {
                total = checked(total + outcomes[index].Weight);
            }

            ulong selected;
            DeterministicRandom next = stream.NextBoundedUInt64(total, out selected);
            ulong cumulative = 0UL;
            for (int index = 0; index < outcomes.Count; index++)
            {
                cumulative = checked(cumulative + outcomes[index].Weight);
                if (selected < cumulative)
                {
                    value = outcomes[index].Value;
                    return next;
                }
            }

            throw new InvalidOperationException(
                "Weighted selection did not resolve an outcome.");
        }

        private static long DivideRounded(long numerator, long positiveDenominator)
        {
            if (positiveDenominator <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(positiveDenominator));
            }
            if (numerator >= 0L)
            {
                return checked(
                    (numerator + positiveDenominator / 2L) / positiveDenominator);
            }
            return checked(
                -((-numerator + positiveDenominator / 2L) / positiveDenominator));
        }

        private static int CheckedInt(long value)
        {
            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new OverflowException(
                    "Hybrid strongbox value exceeds Int32 range.");
            }
            return (int)value;
        }

        internal static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Min(maximum, Math.Max(minimum, value));
        }
    }
}

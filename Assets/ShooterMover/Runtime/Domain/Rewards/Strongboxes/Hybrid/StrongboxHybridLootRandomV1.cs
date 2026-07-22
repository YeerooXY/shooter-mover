using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    internal static class StrongboxHybridLootRandomV1
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

        internal static StrongboxTargetLevelRollV1 RollTargetLevel(
            StrongboxHybridLootPolicyV1 policy,
            int playerLevel,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            List<StrongboxWeightedIntOutcomeV1> triangular =
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
            return new StrongboxTargetLevelRollV1(
                policy.PolicyId,
                playerLevel,
                delta,
                unclamped,
                target,
                stream.GetTrace().SamplesConsumed,
                policy.Fingerprint);
        }

        internal static StrongboxInstanceLevelRollV1 RollInstanceLevel(
            StrongboxHybridLootPolicyV1 policy,
            StrongboxTargetLevelRollV1 targetRoll,
            int definitionPeakLevel,
            StableId rarityId,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> instanceLevelOffsets,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            long blended = checked(
                (long)targetRoll.TargetLevel * policy.TargetBlendPermille
                + (long)definitionPeakLevel
                    * (StrongboxHybridLootPolicyV1.BlendScale - policy.TargetBlendPermille));
            int center = Math.Max(1, CheckedInt(DivideRounded(
                blended,
                StrongboxHybridLootPolicyV1.BlendScale)));

            DeterministicRandom stream = DeterministicRandom.CreateSubstream(
                rootSeed,
                algorithmVersion,
                InstanceLevelPurposeId,
                equipmentSlotOrdinal);
            int offset;
            stream = RollWeighted(stream, instanceLevelOffsets, out offset);
            int itemLevel = Math.Max(1, checked(center + offset));
            return new StrongboxInstanceLevelRollV1(
                targetRoll,
                definitionPeakLevel,
                rarityId,
                center,
                offset,
                itemLevel,
                stream.GetTrace().SamplesConsumed,
                policy.Fingerprint);
        }

        internal static StrongboxAugmentSignatureV1 RollAugmentSignature(
            StrongboxHybridLootPolicyV1 policy,
            int playerLevel,
            int itemLevel,
            StrongboxRarityProfileV1 rarity,
            int normalMaximumSlots,
            int absoluteMaximumSlots,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> augmentSlotOutcomes,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> augmentLevelOutcomes,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            int bias = Clamp(
                checked(playerLevel - itemLevel + rarity.AugmentBiasLevels),
                -12,
                12);
            List<StrongboxWeightedIntOutcomeV1> mappedSlots =
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
                List<StrongboxWeightedIntOutcomeV1> adjustedLevels =
                    AdjustOutcomes(augmentLevelOutcomes, bias, LevelBiasPerStepMilli);
                DeterministicRandom levelStream = DeterministicRandom.CreateSubstream(
                    rootSeed,
                    algorithmVersion,
                    AugmentLevelPurposeId,
                    equipmentSlotOrdinal);
                levelStream = RollWeighted(levelStream, adjustedLevels, out sharedLevel);
                levelSamples = levelStream.GetTrace().SamplesConsumed;
            }

            return new StrongboxAugmentSignatureV1(
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

        private static List<StrongboxWeightedIntOutcomeV1> BuildTriangularTargetOutcomes(
            StrongboxHybridLootPolicyV1 policy)
        {
            var outcomes = new List<StrongboxWeightedIntOutcomeV1>();
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
                outcomes.Add(new StrongboxWeightedIntOutcomeV1(
                    delta,
                    checked((ulong)weight)));
            }
            return outcomes;
        }

        private static List<StrongboxWeightedIntOutcomeV1> MapAndAdjustSlotOutcomes(
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> augmentSlotOutcomes,
            int normalMaximumSlots,
            int absoluteMaximumSlots,
            int bias)
        {
            var accumulated = new SortedDictionary<int, ulong>();
            for (int index = 0; index < augmentSlotOutcomes.Count; index++)
            {
                StrongboxWeightedIntOutcomeV1 authored = augmentSlotOutcomes[index];
                int mapped = MapAuthoredSlotCount(
                    authored.Value,
                    normalMaximumSlots,
                    absoluteMaximumSlots);
                ulong existing;
                accumulated.TryGetValue(mapped, out existing);
                accumulated[mapped] = checked(existing + authored.Weight);
            }

            var mappedOutcomes = new List<StrongboxWeightedIntOutcomeV1>();
            foreach (KeyValuePair<int, ulong> pair in accumulated)
            {
                mappedOutcomes.Add(new StrongboxWeightedIntOutcomeV1(pair.Key, pair.Value));
            }
            return AdjustOutcomes(mappedOutcomes, bias, SlotBiasPerStepMilli);
        }

        private static int MapAuthoredSlotCount(
            int authoredSlotCount,
            int normalMaximumSlots,
            int absoluteMaximumSlots)
        {
            if (authoredSlotCount <= StrongboxHybridLootPolicyV1.AuthoredNormalWeaponSlots)
            {
                return Math.Min(authoredSlotCount, normalMaximumSlots);
            }

            int overcapSteps =
                authoredSlotCount - StrongboxHybridLootPolicyV1.AuthoredNormalWeaponSlots;
            return Math.Min(
                checked(normalMaximumSlots + overcapSteps),
                absoluteMaximumSlots);
        }

        private static int ResolveRepresentativeAuthoredSlot(
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> augmentSlotOutcomes,
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

        private static List<StrongboxWeightedIntOutcomeV1> AdjustOutcomes(
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> source,
            int bias,
            int slopeMilli)
        {
            var adjusted = new List<StrongboxWeightedIntOutcomeV1>(source.Count);
            int minimumValue = source[0].Value;
            for (int index = 0; index < source.Count; index++)
            {
                StrongboxWeightedIntOutcomeV1 outcome = source[index];
                int steps = outcome.Value - minimumValue;
                int multiplier = Clamp(
                    checked(
                        StrongboxHybridLootPolicyV1.RarityMultiplierScale
                        + bias * slopeMilli * steps),
                    MinimumBiasMultiplierMilli,
                    MaximumBiasMultiplierMilli);
                ulong weight = checked(outcome.Weight * checked((ulong)multiplier));
                adjusted.Add(new StrongboxWeightedIntOutcomeV1(outcome.Value, weight));
            }
            return adjusted;
        }

        private static DeterministicRandom RollWeighted(
            DeterministicRandom stream,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> outcomes,
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

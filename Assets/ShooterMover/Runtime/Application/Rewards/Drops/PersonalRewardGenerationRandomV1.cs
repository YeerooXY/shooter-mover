using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Drops
{
    internal static class PersonalRewardGenerationRandomV1
    {
        private static readonly StableId ChancePurposeId = StableId.Parse("reward-rng.personal-chance-v1");
        private static readonly StableId OutcomePurposeId = StableId.Parse("reward-rng.personal-outcome-v1");
        private static readonly StableId QuantityPurposeId = StableId.Parse("reward-rng.personal-quantity-v1");
        internal static bool RollChance(PersonalRewardRollContextV1 context, RewardRollGroupV1 group, int probabilityMillionths, ulong subordinal)
        {
            if (probabilityMillionths <= 0) return false; if (probabilityMillionths >= RewardRollGroupV1.ProbabilityScale) return true;
            DeterministicRandom stream = DeterministicRandom.CreateSubstream(context.RootSeed, context.AlgorithmVersion, ChancePurposeId, Ordinal(context, group, subordinal)); ulong selected; stream = stream.NextBoundedUInt64(RewardRollGroupV1.ProbabilityScale, out selected); return selected < checked((ulong)probabilityMillionths);
        }
        internal static RewardOutcomeV1 RollWeighted(PersonalRewardRollContextV1 context, RewardRollGroupV1 group, IReadOnlyList<RewardOutcomeV1> outcomes, ulong subordinal)
        {
            if (outcomes == null || outcomes.Count == 0) return null; ulong total = 0UL; for (int index = 0; index < outcomes.Count; index++) total = checked(total + outcomes[index].Weight);
            DeterministicRandom stream = DeterministicRandom.CreateSubstream(context.RootSeed, context.AlgorithmVersion, OutcomePurposeId, Ordinal(context, group, subordinal)); ulong selected; stream = stream.NextBoundedUInt64(total, out selected); ulong cumulative = 0UL;
            for (int index = 0; index < outcomes.Count; index++) { cumulative = checked(cumulative + outcomes[index].Weight); if (selected < cumulative) return outcomes[index]; }
            throw new InvalidOperationException("Personal reward weighted selection did not resolve.");
        }
        internal static long RollQuantity(PersonalRewardRollContextV1 context, RewardRollGroupV1 group, RewardOutcomeV1 outcome, ulong subordinal)
        {
            RewardQuantityRangeV1 range = outcome.Grant.Quantity; if (range.IsFixed) return range.Minimum;
            ulong width = checked((ulong)(range.Maximum - range.Minimum + 1L)); DeterministicRandom stream = DeterministicRandom.CreateSubstream(context.RootSeed, context.AlgorithmVersion, QuantityPurposeId, Ordinal(context, group, subordinal)); ulong selected; stream = stream.NextBoundedUInt64(width, out selected); return checked(range.Minimum + (long)selected);
        }
        internal static int RawStrongboxProbabilityMillionths(RewardRollGroupV1 group)
        {
            if (group.BoxPacingMode != RewardBoxPacingModeV1.RandomBox) return 0;
            if (group.Behavior == RewardRollGroupBehaviorV1.IndependentProbabilityRoll) return group.ProbabilityMillionths;
            ulong total = 0UL; ulong boxes = 0UL;
            for (int index = 0; index < group.Outcomes.Count; index++) { RewardOutcomeV1 outcome = group.Outcomes[index]; total = checked(total + outcome.Weight); if (outcome.Grant != null && outcome.Grant.Kind == RewardGrantKindV1.Strongbox) boxes = checked(boxes + outcome.Weight); }
            return total == 0UL ? 0 : (int)Math.Min(RewardRollGroupV1.ProbabilityScale, checked(boxes * RewardRollGroupV1.ProbabilityScale / total));
        }
        private static ulong Ordinal(PersonalRewardRollContextV1 context, RewardRollGroupV1 group, ulong subordinal) { return RewardGenerationFingerprintV1.StableOrdinal(context.OperationStableId) ^ checked((ulong)group.Ordinal << 40) ^ subordinal; }
    }
}

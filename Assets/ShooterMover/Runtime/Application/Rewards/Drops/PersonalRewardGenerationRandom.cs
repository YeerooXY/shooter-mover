using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Drops
{
    internal static class PersonalRewardGenerationRandom
    {
        private static readonly StableId ChancePurposeId = StableId.Parse("reward-rng.personal-chance-v1");
        private static readonly StableId OutcomePurposeId = StableId.Parse("reward-rng.personal-outcome-v1");
        private static readonly StableId QuantityPurposeId = StableId.Parse("reward-rng.personal-quantity-v1");
        internal static bool RollChance(PersonalRewardRollContext context, RewardRollGroup group, int probabilityMillionths, ulong subordinal)
        {
            if (probabilityMillionths <= 0) return false; if (probabilityMillionths >= RewardRollGroup.ProbabilityScale) return true;
            DeterministicRandom stream = DeterministicRandom.CreateSubstream(context.RootSeed, context.AlgorithmVersion, ChancePurposeId, Ordinal(context, group, subordinal)); ulong selected; stream = stream.NextBoundedUInt64(RewardRollGroup.ProbabilityScale, out selected); return selected < checked((ulong)probabilityMillionths);
        }
        internal static RewardOutcome RollWeighted(PersonalRewardRollContext context, RewardRollGroup group, IReadOnlyList<RewardOutcome> outcomes, ulong subordinal)
        {
            if (outcomes == null || outcomes.Count == 0) return null; ulong total = 0UL; for (int index = 0; index < outcomes.Count; index++) total = checked(total + outcomes[index].Weight);
            DeterministicRandom stream = DeterministicRandom.CreateSubstream(context.RootSeed, context.AlgorithmVersion, OutcomePurposeId, Ordinal(context, group, subordinal)); ulong selected; stream = stream.NextBoundedUInt64(total, out selected); ulong cumulative = 0UL;
            for (int index = 0; index < outcomes.Count; index++) { cumulative = checked(cumulative + outcomes[index].Weight); if (selected < cumulative) return outcomes[index]; }
            throw new InvalidOperationException("Personal reward weighted selection did not resolve.");
        }
        internal static long RollQuantity(PersonalRewardRollContext context, RewardRollGroup group, RewardOutcome outcome, ulong subordinal)
        {
            RewardQuantityRange range = outcome.Grant.Quantity; if (range.IsFixed) return range.Minimum;
            ulong width = checked((ulong)(range.Maximum - range.Minimum + 1L)); DeterministicRandom stream = DeterministicRandom.CreateSubstream(context.RootSeed, context.AlgorithmVersion, QuantityPurposeId, Ordinal(context, group, subordinal)); ulong selected; stream = stream.NextBoundedUInt64(width, out selected); return checked(range.Minimum + (long)selected);
        }
        internal static int RawStrongboxProbabilityMillionths(RewardRollGroup group)
        {
            if (group.BoxPacingMode != RewardBoxPacingMode.RandomBox) return 0;
            if (group.Behavior == RewardRollGroupBehavior.IndependentProbabilityRoll) return group.ProbabilityMillionths;
            ulong total = 0UL; ulong boxes = 0UL;
            for (int index = 0; index < group.Outcomes.Count; index++) { RewardOutcome outcome = group.Outcomes[index]; total = checked(total + outcome.Weight); if (outcome.Grant != null && outcome.Grant.Kind == RewardGrantKind.Strongbox) boxes = checked(boxes + outcome.Weight); }
            return total == 0UL ? 0 : (int)Math.Min(RewardRollGroup.ProbabilityScale, checked(boxes * RewardRollGroup.ProbabilityScale / total));
        }
        private static ulong Ordinal(PersonalRewardRollContext context, RewardRollGroup group, ulong subordinal) { return RewardGenerationFingerprint.StableOrdinal(context.OperationStableId) ^ checked((ulong)group.Ordinal << 40) ^ subordinal; }
    }
}

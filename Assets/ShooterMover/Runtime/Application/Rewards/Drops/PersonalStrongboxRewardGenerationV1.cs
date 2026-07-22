using System;
using System.Globalization;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>Freezes one exact canonical tier and participant-owned strongbox grant identity.</summary>
    internal static class PersonalStrongboxRewardGenerationV1
    {
        internal static RewardGrantV1 CreateGrant(PersonalRewardRollContextV1 context, RewardRollGroupV1 group, RewardOutcomeV1 outcome, int unitOrdinal, StableId tierProfileId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context)); if (tierProfileId == null) throw new ArgumentNullException(nameof(tierProfileId)); if (unitOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(unitOrdinal));
            StableId exactTier = ProductionStrongboxTierSelectionCatalogV1.SelectExactTier(tierProfileId, context.EnumerateTierSelectionContexts(), context.RootSeed, context.AlgorithmVersion, RewardGenerationFingerprintV1.StableOrdinal(context.OperationStableId) ^ checked((ulong)(group == null ? 0 : group.Ordinal) << 32) ^ checked((ulong)unitOrdinal));
            return RewardGrantV1.Create(DeriveGrantId(context, group, outcome, unitOrdinal), RewardGrantKindV1.Strongbox, exactTier, 1L);
        }
        private static StableId DeriveGrantId(PersonalRewardRollContextV1 context, RewardRollGroupV1 group, RewardOutcomeV1 outcome, int unitOrdinal)
        {
            return RewardGenerationFingerprintV1.DeriveStableId("personalrewardgrant", context.OperationStableId.ToString(), group == null ? "run-minimum" : group.GroupStableId.ToString(), outcome == null ? "completion" : outcome.OutcomeStableId.ToString(), unitOrdinal.ToString(CultureInfo.InvariantCulture), context.ParticipantStableId.ToString());
        }
    }
}

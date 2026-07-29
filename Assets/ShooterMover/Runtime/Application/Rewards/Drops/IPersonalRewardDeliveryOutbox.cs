using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>
    /// Run-local exactly-once handoff between personal generation and the participant's
    /// pickup or network delivery authority.
    /// </summary>
    public interface IPersonalRewardDeliveryOutbox
    {
        bool TryEnqueue(
            PersonalRewardGenerationResult result,
            out PersonalRewardDeliveryEnvelope envelope,
            out string diagnostic);

        bool TryGet(
            StableId operationStableId,
            StableId participantStableId,
            out PersonalRewardDeliveryEnvelope envelope);

        bool TryMarkDelivered(
            StableId operationStableId,
            StableId participantStableId,
            string resultFingerprint,
            string deliveryFingerprint,
            out PersonalRewardDeliveryEnvelope envelope,
            out string diagnostic);

        IReadOnlyList<PersonalRewardDeliveryEnvelope> ExportPending(
            StableId participantStableId);
    }
}

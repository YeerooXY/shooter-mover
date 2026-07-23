using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>
    /// Run-local exactly-once handoff between personal generation and the participant's
    /// pickup or network delivery authority.
    /// </summary>
    public interface IPersonalRewardDeliveryOutboxV1
    {
        bool TryEnqueue(
            PersonalRewardGenerationResultV1 result,
            out PersonalRewardDeliveryEnvelopeV1 envelope,
            out string diagnostic);

        bool TryGet(
            StableId operationStableId,
            StableId participantStableId,
            out PersonalRewardDeliveryEnvelopeV1 envelope);

        bool TryMarkDelivered(
            StableId operationStableId,
            StableId participantStableId,
            string resultFingerprint,
            string deliveryFingerprint,
            out PersonalRewardDeliveryEnvelopeV1 envelope,
            out string diagnostic);

        IReadOnlyList<PersonalRewardDeliveryEnvelopeV1> ExportPending(
            StableId participantStableId);
    }
}

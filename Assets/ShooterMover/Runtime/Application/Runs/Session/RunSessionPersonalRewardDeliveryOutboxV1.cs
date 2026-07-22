using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    /// <summary>
    /// Run-backed personal reward delivery outbox. Remote participant results survive
    /// service recreation and reconnect until that participant's delivery authority
    /// acknowledges the exact immutable result.
    /// </summary>
    public sealed class RunSessionPersonalRewardDeliveryOutboxV1 :
        IPersonalRewardDeliveryOutboxV1
    {
        private readonly RunSessionAggregateV1 run;

        public RunSessionPersonalRewardDeliveryOutboxV1(
            RunSessionAggregateV1 run)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public bool TryEnqueue(
            PersonalRewardGenerationResultV1 result,
            out PersonalRewardDeliveryEnvelopeV1 envelope,
            out string diagnostic)
        {
            return run.TryEnqueuePersonalReward(
                result,
                out envelope,
                out diagnostic);
        }

        public bool TryMarkDelivered(
            StableId operationStableId,
            StableId participantStableId,
            string resultFingerprint,
            string deliveryFingerprint,
            out PersonalRewardDeliveryEnvelopeV1 envelope,
            out string diagnostic)
        {
            return run.TryMarkPersonalRewardDelivered(
                operationStableId,
                participantStableId,
                resultFingerprint,
                deliveryFingerprint,
                out envelope,
                out diagnostic);
        }

        public IReadOnlyList<PersonalRewardDeliveryEnvelopeV1> ExportPending(
            StableId participantStableId)
        {
            return run.ExportPendingPersonalRewards(participantStableId);
        }
    }
}

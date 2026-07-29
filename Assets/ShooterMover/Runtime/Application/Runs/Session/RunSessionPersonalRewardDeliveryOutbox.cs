using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Runs.Session
{
    /// <summary>
    /// Run-backed personal reward delivery outbox. Remote participant results survive
    /// service recreation and reconnect until that participant's delivery authority
    /// acknowledges the exact immutable result.
    /// </summary>
    public sealed class RunSessionPersonalRewardDeliveryOutbox :
        IPersonalRewardDeliveryOutbox
    {
        private readonly RunSessionAggregate run;

        public RunSessionPersonalRewardDeliveryOutbox(
            RunSessionAggregate run)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public bool TryEnqueue(
            PersonalRewardGenerationResult result,
            out PersonalRewardDeliveryEnvelope envelope,
            out string diagnostic)
        {
            return run.TryEnqueuePersonalReward(
                result,
                out envelope,
                out diagnostic);
        }

        public bool TryGet(
            StableId operationStableId,
            StableId participantStableId,
            out PersonalRewardDeliveryEnvelope envelope)
        {
            envelope = null;
            if (operationStableId == null || participantStableId == null)
            {
                return false;
            }
            RunRewardLiveSnapshot snapshot =
                run.ExportRewardRuntimeSnapshot();
            for (int index = 0; index < snapshot.Deliveries.Count; index++)
            {
                PersonalRewardDeliveryEnvelope value =
                    snapshot.Deliveries[index];
                if (value.Result.Context.OperationStableId
                        == operationStableId
                    && value.Result.Context.ParticipantStableId
                        == participantStableId)
                {
                    envelope = value;
                    return true;
                }
            }
            return false;
        }

        public bool TryMarkDelivered(
            StableId operationStableId,
            StableId participantStableId,
            string resultFingerprint,
            string deliveryFingerprint,
            out PersonalRewardDeliveryEnvelope envelope,
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

        public IReadOnlyList<PersonalRewardDeliveryEnvelope> ExportPending(
            StableId participantStableId)
        {
            return run.ExportPendingPersonalRewards(participantStableId);
        }
    }
}

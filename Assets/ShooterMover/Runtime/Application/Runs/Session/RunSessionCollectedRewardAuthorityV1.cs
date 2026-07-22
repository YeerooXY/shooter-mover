using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public sealed partial class RunSessionAggregateV1 :
        IRunSessionCollectedRewardAuthorityV1
    {
        private readonly Dictionary<StableId, RunSessionCollectedRewardV1>
            collectedRunRewardsByOperation =
                new Dictionary<StableId, RunSessionCollectedRewardV1>();
        private readonly Dictionary<StableId, RunSessionCollectedRewardV1>
            collectedRunRewardsByPickup =
                new Dictionary<StableId, RunSessionCollectedRewardV1>();
        private readonly Dictionary<StableId, RunSessionCollectedRewardV1>
            collectedRunRewardsByChild =
                new Dictionary<StableId, RunSessionCollectedRewardV1>();

        public bool IsActive
        {
            get { return lifecycleState == RunSessionLifecycleStateV1.Active; }
        }

        public StableId PlayerActorStableId
        {
            get { return ExportPlayerSnapshot().ActorInstanceStableId; }
        }

        public StableId PlayerParticipantStableId
        {
            get { return ExportPlayerSnapshot().ParticipantStableId; }
        }

        public long NextCollectedRewardOrder
        {
            get { return checked(CurrentLifecycleCollectionCount() + 1L); }
        }

        public RunSessionRewardCollectionResultV1 RecordCollectedRunReward(
            RunSessionCollectedRewardV1 reward)
        {
            if (reward == null)
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatusV1.Rejected,
                    null,
                    "run-session-collected-reward-null");
            if (reward.RunStableId != RunStableId)
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatusV1.WrongRun,
                    reward,
                    "run-session-collected-reward-wrong-run");
            if (reward.RunLifecycleGeneration != lifecycleGeneration)
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatusV1.StaleLifecycle,
                    reward,
                    reward.RunLifecycleGeneration < lifecycleGeneration
                        ? "run-session-collected-reward-stale-generation"
                        : "run-session-collected-reward-future-generation");
            if (lifecycleState == RunSessionLifecycleStateV1.Ended)
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatusV1.RunEnded,
                    reward,
                    "run-session-collected-reward-after-end");

            RunSessionCollectedRewardV1 existing;
            if (collectedRunRewardsByOperation.TryGetValue(
                reward.CollectionOperationStableId,
                out existing))
            {
                return string.Equals(
                    existing.Fingerprint,
                    reward.Fingerprint,
                    StringComparison.Ordinal)
                    ? RewardCollectionResult(
                        RunSessionRewardCollectionStatusV1.ExactReplay,
                        existing,
                        string.Empty)
                    : RewardCollectionResult(
                        RunSessionRewardCollectionStatusV1.ConflictingDuplicate,
                        existing,
                        "run-session-collected-reward-operation-conflict");
            }

            RunPlayerRuntimeSnapshotV1 player = ExportPlayerSnapshot();
            if (reward.CollectorEntityStableId != player.ActorInstanceStableId
                || reward.CollectorParticipantStableId != player.ParticipantStableId
                || reward.AttributedParticipantStableId != player.ParticipantStableId)
            {
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatusV1.UnauthorizedCollector,
                    reward,
                    "run-session-collected-reward-collector-unauthorized");
            }
            if (reward.CollectionOrder != NextCollectedRewardOrder)
            {
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatusV1.ConflictingDuplicate,
                    reward,
                    "run-session-collected-reward-order-conflict");
            }
            if (collectedRunRewardsByPickup.TryGetValue(
                reward.PickupStableId,
                out existing))
            {
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatusV1.ConflictingDuplicate,
                    existing,
                    "run-session-collected-reward-pickup-already-collected");
            }
            if (collectedRunRewardsByChild.TryGetValue(
                reward.GeneratedRewardChildStableId,
                out existing))
            {
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatusV1.ConflictingDuplicate,
                    existing,
                    "run-session-collected-reward-child-already-collected");
            }

            RunSessionFactAdmissionResultV1 fact = AdmitFact(
                new RunSessionFactEnvelopeV1(
                    reward.CollectionOperationStableId,
                    reward.RunStableId,
                    reward.RunLifecycleGeneration,
                    RunSessionFactKindV1.Contact,
                    reward.Fingerprint));
            if (fact.Status != RunSessionFactAdmissionStatusV1.Accepted)
            {
                return RewardCollectionResult(
                    fact.Status == RunSessionFactAdmissionStatusV1.ExactReplay
                        || fact.Status == RunSessionFactAdmissionStatusV1.ConflictingDuplicate
                        ? RunSessionRewardCollectionStatusV1.ConflictingDuplicate
                        : RunSessionRewardCollectionStatusV1.Rejected,
                    reward,
                    string.IsNullOrWhiteSpace(fact.RejectionCode)
                        ? "run-session-collected-reward-fact-inconsistent"
                        : fact.RejectionCode);
            }

            collectedRunRewardsByOperation.Add(
                reward.CollectionOperationStableId,
                reward);
            collectedRunRewardsByPickup.Add(reward.PickupStableId, reward);
            collectedRunRewardsByChild.Add(
                reward.GeneratedRewardChildStableId,
                reward);
            return RewardCollectionResult(
                RunSessionRewardCollectionStatusV1.Collected,
                reward,
                string.Empty);
        }

        public IReadOnlyList<RunSessionCollectedRewardV1> ExportCollectedRunRewards()
        {
            var copy = new List<RunSessionCollectedRewardV1>();
            foreach (RunSessionCollectedRewardV1 reward in
                collectedRunRewardsByOperation.Values)
            {
                if (reward.RunStableId == RunStableId
                    && reward.RunLifecycleGeneration == lifecycleGeneration)
                {
                    copy.Add(reward);
                }
            }
            copy.Sort(delegate(
                RunSessionCollectedRewardV1 left,
                RunSessionCollectedRewardV1 right)
            {
                int order = left.CollectionOrder.CompareTo(right.CollectionOrder);
                return order != 0
                    ? order
                    : left.PickupStableId.CompareTo(right.PickupStableId);
            });
            return new ReadOnlyCollection<RunSessionCollectedRewardV1>(copy);
        }

        private long CurrentLifecycleCollectionCount()
        {
            long count = 0L;
            foreach (RunSessionCollectedRewardV1 reward in
                collectedRunRewardsByOperation.Values)
            {
                if (reward.RunStableId == RunStableId
                    && reward.RunLifecycleGeneration == lifecycleGeneration)
                {
                    count++;
                }
            }
            return count;
        }

        private RunPlayerRuntimeSnapshotV1 ExportPlayerSnapshot()
        {
            RunPlayerRuntimeSnapshotV1 snapshot =
                RuntimePorts.Player.ExportSnapshot();
            if (snapshot == null)
            {
                throw new InvalidOperationException(
                    "The Run Session player port returned no snapshot.");
            }
            return snapshot;
        }

        private static RunSessionRewardCollectionResultV1 RewardCollectionResult(
            RunSessionRewardCollectionStatusV1 status,
            RunSessionCollectedRewardV1 reward,
            string rejectionCode)
        {
            return new RunSessionRewardCollectionResultV1(
                status,
                reward,
                rejectionCode);
        }
    }
}

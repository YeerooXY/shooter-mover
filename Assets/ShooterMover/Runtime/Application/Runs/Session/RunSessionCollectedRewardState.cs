using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Runs.Session
{
    public sealed partial class RunSessionAggregate :
        IRunSessionCollectedRewardState
    {
        private readonly Dictionary<StableId, RunSessionCollectedReward>
            collectedRunRewardsByOperation =
                new Dictionary<StableId, RunSessionCollectedReward>();
        private readonly Dictionary<StableId, RunSessionCollectedReward>
            collectedRunRewardsByPickup =
                new Dictionary<StableId, RunSessionCollectedReward>();
        private readonly Dictionary<StableId, RunSessionCollectedReward>
            collectedRunRewardsByChild =
                new Dictionary<StableId, RunSessionCollectedReward>();

        public bool IsActive
        {
            get { return lifecycleState == RunSessionLifecycleState.Active; }
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

        public RunSessionRewardCollectionResult RecordRewardClaim(
            RunSessionCollectedReward reward)
        {
            if (reward == null)
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatus.Rejected,
                    null,
                    "run-session-collected-reward-null");
            if (reward.RunStableId != RunStableId)
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatus.WrongRun,
                    reward,
                    "run-session-collected-reward-wrong-run");
            if (reward.RunLifecycleGeneration != lifecycleGeneration)
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatus.StaleLifecycle,
                    reward,
                    reward.RunLifecycleGeneration < lifecycleGeneration
                        ? "run-session-collected-reward-stale-generation"
                        : "run-session-collected-reward-future-generation");
            if (lifecycleState == RunSessionLifecycleState.Ended)
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatus.RunEnded,
                    reward,
                    "run-session-collected-reward-after-end");

            RunSessionCollectedReward existing;
            if (collectedRunRewardsByOperation.TryGetValue(
                reward.CollectionOperationStableId,
                out existing))
            {
                if (!string.Equals(
                    existing.Fingerprint,
                    reward.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return RewardCollectionResult(
                        RunSessionRewardCollectionStatus.ConflictingDuplicate,
                        existing,
                        "run-session-collected-reward-operation-conflict");
                }

                string resultsError = AddStrongboxToResults(existing);
                return RewardCollectionResult(
                    string.IsNullOrEmpty(resultsError)
                        ? RunSessionRewardCollectionStatus.ExactReplay
                        : RunSessionRewardCollectionStatus.Rejected,
                    existing,
                    resultsError);
            }

            RunPlayerSnapshot player = ExportPlayerSnapshot();
            if (reward.CollectorEntityStableId != player.ActorInstanceStableId
                || reward.CollectorParticipantStableId != player.ParticipantStableId
                || reward.AttributedParticipantStableId != player.ParticipantStableId)
            {
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatus.UnauthorizedCollector,
                    reward,
                    "run-session-collected-reward-collector-unauthorized");
            }
            if (reward.CollectionOrder != NextCollectedRewardOrder)
            {
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatus.ConflictingDuplicate,
                    reward,
                    "run-session-collected-reward-order-conflict");
            }
            if (collectedRunRewardsByPickup.TryGetValue(
                reward.PickupStableId,
                out existing))
            {
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatus.ConflictingDuplicate,
                    existing,
                    "run-session-collected-reward-pickup-already-collected");
            }
            if (collectedRunRewardsByChild.TryGetValue(
                reward.GeneratedRewardChildStableId,
                out existing))
            {
                return RewardCollectionResult(
                    RunSessionRewardCollectionStatus.ConflictingDuplicate,
                    existing,
                    "run-session-collected-reward-child-already-collected");
            }

            RunSessionFactAdmissionResult fact = AdmitFact(
                new RunSessionFactEnvelope(
                    reward.CollectionOperationStableId,
                    reward.RunStableId,
                    reward.RunLifecycleGeneration,
                    RunSessionFactKind.Contact,
                    reward.Fingerprint));
            if (fact.Status != RunSessionFactAdmissionStatus.Accepted)
            {
                return RewardCollectionResult(
                    fact.Status == RunSessionFactAdmissionStatus.ExactReplay
                        || fact.Status == RunSessionFactAdmissionStatus.ConflictingDuplicate
                        ? RunSessionRewardCollectionStatus.ConflictingDuplicate
                        : RunSessionRewardCollectionStatus.Rejected,
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

            string strongboxError = AddStrongboxToResults(reward);
            return RewardCollectionResult(
                string.IsNullOrEmpty(strongboxError)
                    ? RunSessionRewardCollectionStatus.Collected
                    : RunSessionRewardCollectionStatus.Rejected,
                reward,
                strongboxError);
        }

        public IReadOnlyList<RunSessionCollectedReward> ExportRewardClaims()
        {
            var copy = new List<RunSessionCollectedReward>();
            foreach (RunSessionCollectedReward reward in
                collectedRunRewardsByOperation.Values)
            {
                if (reward.RunStableId == RunStableId
                    && reward.RunLifecycleGeneration == lifecycleGeneration)
                {
                    copy.Add(reward);
                }
            }
            copy.Sort(delegate(
                RunSessionCollectedReward left,
                RunSessionCollectedReward right)
            {
                int order = left.CollectionOrder.CompareTo(right.CollectionOrder);
                return order != 0
                    ? order
                    : left.PickupStableId.CompareTo(right.PickupStableId);
            });
            return new ReadOnlyCollection<RunSessionCollectedReward>(copy);
        }

        private string AddStrongboxToResults(
            RunSessionCollectedReward reward)
        {
            if (reward.RewardKind != RewardGrantKind.Strongbox)
            {
                return string.Empty;
            }

            try
            {
                var added = RecordCollectedStrongbox(
                    new RunStrongboxCollectionRequest(
                        reward.CollectionOperationStableId,
                        reward.RunStableId,
                        reward.RunLifecycleGeneration,
                        reward.ContentStableId,
                        reward.GeneratedRewardChildStableId,
                        reward.GeneratedRewardChildStableId,
                        reward.DropOperationStableId));
                if (added == null)
                {
                    return "run-strongbox-results-null";
                }
                if (added.Succeeded)
                {
                    return string.Empty;
                }
                return string.IsNullOrWhiteSpace(added.RejectionCode)
                    ? "run-strongbox-results-rejected"
                    : added.RejectionCode;
            }
            catch (Exception exception)
            {
                return "run-strongbox-results-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
            }
        }

        private long CurrentLifecycleCollectionCount()
        {
            long count = 0L;
            foreach (RunSessionCollectedReward reward in
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

        private RunPlayerSnapshot ExportPlayerSnapshot()
        {
            RunPlayerSnapshot snapshot =
                RuntimePorts.Player.ExportSnapshot();
            if (snapshot == null)
            {
                throw new InvalidOperationException(
                    "The Run Session player port returned no snapshot.");
            }
            return snapshot;
        }

        private static RunSessionRewardCollectionResult RewardCollectionResult(
            RunSessionRewardCollectionStatus status,
            RunSessionCollectedReward reward,
            string rejectionCode)
        {
            return new RunSessionRewardCollectionResult(
                status,
                reward,
                rejectionCode);
        }
    }
}

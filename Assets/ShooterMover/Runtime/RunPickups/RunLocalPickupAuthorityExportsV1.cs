using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    public sealed partial class RunLocalPickupAuthorityV1
    {
        public IReadOnlyList<RunPickupSnapshotV1> ExportPickups()
        {
            lock (gate)
            {
                var copy = new List<RunPickupSnapshotV1>();
                foreach (RunPickupSnapshotV1 pickup in byPickup.Values)
                {
                    if (IsCurrentLifecycle(pickup))
                        copy.Add(pickup);
                }
                SortPickups(copy);
                return new ReadOnlyCollection<RunPickupSnapshotV1>(copy);
            }
        }

        public IReadOnlyList<RunPickupSnapshotV1> ExportAvailablePickups()
        {
            lock (gate)
            {
                var copy = new List<RunPickupSnapshotV1>();
                foreach (RunPickupSnapshotV1 pickup in byPickup.Values)
                {
                    if (pickup.State == RunPickupStateV1.Available
                        && IsCurrentLifecycle(pickup))
                    {
                        copy.Add(pickup);
                    }
                }
                SortPickups(copy);
                return new ReadOnlyCollection<RunPickupSnapshotV1>(copy);
            }
        }

        public bool TryGetPickup(
            StableId pickupStableId,
            out RunPickupSnapshotV1 pickup)
        {
            pickup = null;
            if (pickupStableId == null) return false;
            lock (gate)
            {
                RunPickupSnapshotV1 found;
                if (!byPickup.TryGetValue(pickupStableId, out found)
                    || !IsCurrentLifecycle(found))
                {
                    return false;
                }
                pickup = found;
                return true;
            }
        }

        private string ValidateBatchContext(RunPickupGeneratedBatchV1 batch)
        {
            if (batch.RunStableId != runSession.RunStableId)
                return "run-pickup-realization-wrong-run";
            if (batch.RunLifecycleGeneration != runSession.LifecycleGeneration)
            {
                return batch.RunLifecycleGeneration < runSession.LifecycleGeneration
                    ? "run-pickup-realization-stale-generation"
                    : "run-pickup-realization-future-generation";
            }
            if (!runSession.IsActive)
                return "run-pickup-realization-run-ended";
            if (batch.AttributedParticipantStableId
                != runSession.PlayerParticipantStableId)
            {
                return "run-pickup-realization-participant-mismatch";
            }
            return string.Empty;
        }

        private RunPickupSnapshotV1 CreatePendingSnapshot(
            RunPickupGeneratedBatchV1 batch,
            RunPickupGeneratedRewardV1 reward,
            StableId pickupId,
            string diagnostic = "run-pickup-awaiting-source-position")
        {
            return new RunPickupSnapshotV1(
                pickupId,
                batch,
                reward,
                RunPickupStateV1.PendingSourcePosition,
                null,
                null,
                null,
                null,
                0L,
                0L,
                diagnostic);
        }

        private IReadOnlyList<RunPickupSnapshotV1> ExportBatchPickupsUnsafe(
            StableId dropOperationStableId)
        {
            var copy = new List<RunPickupSnapshotV1>();
            foreach (RunPickupSnapshotV1 pickup in byPickup.Values)
            {
                if (pickup.Batch.DropOperationStableId == dropOperationStableId
                    && IsCurrentLifecycle(pickup))
                {
                    copy.Add(pickup);
                }
            }
            SortPickups(copy);
            return new ReadOnlyCollection<RunPickupSnapshotV1>(copy);
        }

        private bool IsCurrentLifecycle(RunPickupSnapshotV1 pickup)
        {
            return pickup.Batch.RunStableId == runSession.RunStableId
                && pickup.Batch.RunLifecycleGeneration
                    == runSession.LifecycleGeneration;
        }

        private int CountCurrentLifecycle(RunPickupStateV1? state)
        {
            int count = 0;
            foreach (RunPickupSnapshotV1 pickup in byPickup.Values)
            {
                if (IsCurrentLifecycle(pickup)
                    && (!state.HasValue || pickup.State == state.Value))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool AllNonPending(
            IReadOnlyList<RunPickupSnapshotV1> pickups)
        {
            for (int index = 0; index < pickups.Count; index++)
            {
                if (pickups[index].State == RunPickupStateV1.PendingSourcePosition)
                    return false;
            }
            return true;
        }

        private static void SortPickups(List<RunPickupSnapshotV1> pickups)
        {
            pickups.Sort(delegate(RunPickupSnapshotV1 left, RunPickupSnapshotV1 right)
            {
                int operation = left.Batch.DropOperationStableId.CompareTo(
                    right.Batch.DropOperationStableId);
                if (operation != 0) return operation;
                int ordinal = left.Reward.Ordinal.CompareTo(right.Reward.Ordinal);
                if (ordinal != 0) return ordinal;
                return left.PickupStableId.CompareTo(right.PickupStableId);
            });
        }

        private static RunPickupCollectionResultV1 RejectedCollection(
            RunPickupCollectionStatusV1 status,
            RunPickupCollectionCommandV1 command,
            RunPickupSnapshotV1 pickup,
            string diagnostic)
        {
            return new RunPickupCollectionResultV1(
                status,
                command,
                pickup,
                null,
                diagnostic);
        }

        private static RunPickupCollectionStatusV1 MapSessionRejection(
            RunPickupSessionRecordResultV1 sessionResult)
        {
            if (sessionResult == null)
                return RunPickupCollectionStatusV1.Rejected;
            switch (sessionResult.Status)
            {
                case RunPickupSessionRecordStatusV1.ConflictingDuplicate:
                    return RunPickupCollectionStatusV1.ConflictingDuplicate;
                case RunPickupSessionRecordStatusV1.WrongRun:
                    return RunPickupCollectionStatusV1.WrongRun;
                case RunPickupSessionRecordStatusV1.StaleLifecycle:
                    return RunPickupCollectionStatusV1.StaleLifecycle;
                case RunPickupSessionRecordStatusV1.UnauthorizedCollector:
                    return RunPickupCollectionStatusV1.UnauthorizedCollector;
                default:
                    return RunPickupCollectionStatusV1.Rejected;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    public sealed partial class RunLocalPickupState
    {
        public IReadOnlyList<RunPickupSnapshot> ExportPickups()
        {
            lock (gate)
            {
                var copy = new List<RunPickupSnapshot>();
                foreach (RunPickupSnapshot pickup in byPickup.Values)
                {
                    if (IsCurrentLifecycle(pickup))
                        copy.Add(pickup);
                }
                SortPickups(copy);
                return new ReadOnlyCollection<RunPickupSnapshot>(copy);
            }
        }

        public IReadOnlyList<RunPickupSnapshot> ExportAvailablePickups()
        {
            lock (gate)
            {
                var copy = new List<RunPickupSnapshot>();
                foreach (RunPickupSnapshot pickup in byPickup.Values)
                {
                    if (pickup.State == RunPickupState.Available
                        && IsCurrentLifecycle(pickup))
                    {
                        copy.Add(pickup);
                    }
                }
                SortPickups(copy);
                return new ReadOnlyCollection<RunPickupSnapshot>(copy);
            }
        }

        public bool TryGetPickup(
            StableId pickupStableId,
            out RunPickupSnapshot pickup)
        {
            pickup = null;
            if (pickupStableId == null) return false;
            lock (gate)
            {
                RunPickupSnapshot found;
                if (!byPickup.TryGetValue(pickupStableId, out found)
                    || !IsCurrentLifecycle(found))
                {
                    return false;
                }
                pickup = found;
                return true;
            }
        }

        private bool TryReadRunSessionContext(
            out RunPickupRunSessionContext context,
            out string diagnostic)
        {
            context = null;
            diagnostic = string.Empty;
            try
            {
                if (!runSession.TryReadContext(out context, out diagnostic)
                    || context == null)
                {
                    diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                        ? "run-pickup-session-context-unavailable"
                        : diagnostic;
                    context = null;
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                context = null;
                diagnostic = "run-pickup-session-context-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return false;
            }
        }

        private static string ValidateBatchContext(
            RunPickupGeneratedBatch batch,
            RunPickupRunSessionContext sessionContext)
        {
            if (batch.RunStableId != sessionContext.RunStableId)
                return "run-pickup-realization-wrong-run";
            if (batch.RunLifecycleGeneration != sessionContext.LifecycleGeneration)
            {
                return batch.RunLifecycleGeneration < sessionContext.LifecycleGeneration
                    ? "run-pickup-realization-stale-generation"
                    : "run-pickup-realization-future-generation";
            }
            if (!sessionContext.IsActive)
                return "run-pickup-realization-run-ended";
            if (batch.AttributedParticipantStableId
                != sessionContext.PlayerParticipantStableId)
            {
                return "run-pickup-realization-participant-mismatch";
            }
            return string.Empty;
        }

        private RunPickupSnapshot CreatePendingSnapshot(
            RunPickupGeneratedBatch batch,
            RunPickupGeneratedReward reward,
            StableId pickupId,
            string diagnostic = "run-pickup-awaiting-source-position")
        {
            return new RunPickupSnapshot(
                pickupId,
                batch,
                reward,
                RunPickupState.PendingSourcePosition,
                null,
                null,
                null,
                null,
                0L,
                0L,
                diagnostic);
        }

        private IReadOnlyList<RunPickupSnapshot> ExportBatchPickupsUnsafe(
            StableId dropOperationStableId)
        {
            var copy = new List<RunPickupSnapshot>();
            foreach (RunPickupSnapshot pickup in byPickup.Values)
            {
                if (pickup.Batch.DropOperationStableId == dropOperationStableId
                    && IsCurrentLifecycle(pickup))
                {
                    copy.Add(pickup);
                }
            }
            SortPickups(copy);
            return new ReadOnlyCollection<RunPickupSnapshot>(copy);
        }

        private bool IsCurrentLifecycle(RunPickupSnapshot pickup)
        {
            RunPickupRunSessionContext context;
            string ignored;
            return pickup != null
                && TryReadRunSessionContext(out context, out ignored)
                && pickup.Batch.RunStableId == context.RunStableId
                && pickup.Batch.RunLifecycleGeneration == context.LifecycleGeneration;
        }

        private int CountCurrentLifecycle(RunPickupState? state)
        {
            RunPickupRunSessionContext context;
            string ignored;
            if (!TryReadRunSessionContext(out context, out ignored))
                return 0;

            int count = 0;
            foreach (RunPickupSnapshot pickup in byPickup.Values)
            {
                if (pickup.Batch.RunStableId == context.RunStableId
                    && pickup.Batch.RunLifecycleGeneration == context.LifecycleGeneration
                    && (!state.HasValue || pickup.State == state.Value))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool AllNonPending(
            IReadOnlyList<RunPickupSnapshot> pickups)
        {
            for (int index = 0; index < pickups.Count; index++)
            {
                if (pickups[index].State == RunPickupState.PendingSourcePosition)
                    return false;
            }
            return true;
        }

        private static void SortPickups(List<RunPickupSnapshot> pickups)
        {
            pickups.Sort(delegate(RunPickupSnapshot left, RunPickupSnapshot right)
            {
                int operation = left.Batch.DropOperationStableId.CompareTo(
                    right.Batch.DropOperationStableId);
                if (operation != 0) return operation;
                int ordinal = left.Reward.Ordinal.CompareTo(right.Reward.Ordinal);
                if (ordinal != 0) return ordinal;
                return left.PickupStableId.CompareTo(right.PickupStableId);
            });
        }

        private static RunPickupCollectionResult RejectedCollection(
            RunPickupCollectionStatus status,
            RunPickupCollectionCommand command,
            RunPickupSnapshot pickup,
            string diagnostic)
        {
            return new RunPickupCollectionResult(
                status,
                command,
                pickup,
                null,
                diagnostic);
        }

        private static RunPickupCollectionStatus MapSessionRejection(
            RunPickupSessionRecordResult sessionResult)
        {
            if (sessionResult == null)
                return RunPickupCollectionStatus.Rejected;
            switch (sessionResult.Status)
            {
                case RunPickupSessionRecordStatus.ConflictingDuplicate:
                    return RunPickupCollectionStatus.ConflictingDuplicate;
                case RunPickupSessionRecordStatus.WrongRun:
                    return RunPickupCollectionStatus.WrongRun;
                case RunPickupSessionRecordStatus.StaleLifecycle:
                    return RunPickupCollectionStatus.StaleLifecycle;
                case RunPickupSessionRecordStatus.UnauthorizedCollector:
                    return RunPickupCollectionStatus.UnauthorizedCollector;
                default:
                    return RunPickupCollectionStatus.Rejected;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunLoot
{
    public sealed partial class RunLocalPickupState
    {
        public IReadOnlyList<RunLootSnapshot> ExportPickups()
        {
            lock (gate)
            {
                var copy = new List<RunLootSnapshot>();
                foreach (RunLootSnapshot pickup in byPickup.Values)
                {
                    if (IsCurrentLifecycle(pickup))
                        copy.Add(pickup);
                }
                SortPickups(copy);
                return new ReadOnlyCollection<RunLootSnapshot>(copy);
            }
        }

        public IReadOnlyList<RunLootSnapshot> ExportAvailablePickups()
        {
            lock (gate)
            {
                var copy = new List<RunLootSnapshot>();
                foreach (RunLootSnapshot pickup in byPickup.Values)
                {
                    if (pickup.State == RunLootState.Available
                        && IsCurrentLifecycle(pickup))
                    {
                        copy.Add(pickup);
                    }
                }
                SortPickups(copy);
                return new ReadOnlyCollection<RunLootSnapshot>(copy);
            }
        }

        public bool TryGetPickup(
            StableId pickupStableId,
            out RunLootSnapshot pickup)
        {
            pickup = null;
            if (pickupStableId == null) return false;
            lock (gate)
            {
                RunLootSnapshot found;
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
            out RunLootRunSessionContext context,
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
            RunLootGeneratedBatch batch,
            RunLootRunSessionContext sessionContext)
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

        private RunLootSnapshot CreatePendingSnapshot(
            RunLootGeneratedBatch batch,
            RunLootGeneratedReward reward,
            StableId pickupId,
            string diagnostic = "run-pickup-awaiting-source-position")
        {
            return new RunLootSnapshot(
                pickupId,
                batch,
                reward,
                RunLootState.PendingSourcePosition,
                null,
                null,
                null,
                null,
                0L,
                0L,
                diagnostic);
        }

        private IReadOnlyList<RunLootSnapshot> ExportBatchPickupsUnsafe(
            StableId dropOperationStableId)
        {
            var copy = new List<RunLootSnapshot>();
            foreach (RunLootSnapshot pickup in byPickup.Values)
            {
                if (pickup.Batch.DropOperationStableId == dropOperationStableId
                    && IsCurrentLifecycle(pickup))
                {
                    copy.Add(pickup);
                }
            }
            SortPickups(copy);
            return new ReadOnlyCollection<RunLootSnapshot>(copy);
        }

        private bool IsCurrentLifecycle(RunLootSnapshot pickup)
        {
            RunLootRunSessionContext context;
            string ignored;
            return pickup != null
                && TryReadRunSessionContext(out context, out ignored)
                && pickup.Batch.RunStableId == context.RunStableId
                && pickup.Batch.RunLifecycleGeneration == context.LifecycleGeneration;
        }

        private int CountCurrentLifecycle(RunLootState? state)
        {
            RunLootRunSessionContext context;
            string ignored;
            if (!TryReadRunSessionContext(out context, out ignored))
                return 0;

            int count = 0;
            foreach (RunLootSnapshot pickup in byPickup.Values)
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
            IReadOnlyList<RunLootSnapshot> pickups)
        {
            for (int index = 0; index < pickups.Count; index++)
            {
                if (pickups[index].State == RunLootState.PendingSourcePosition)
                    return false;
            }
            return true;
        }

        private static void SortPickups(List<RunLootSnapshot> pickups)
        {
            pickups.Sort(delegate(RunLootSnapshot left, RunLootSnapshot right)
            {
                int operation = left.Batch.DropOperationStableId.CompareTo(
                    right.Batch.DropOperationStableId);
                if (operation != 0) return operation;
                int ordinal = left.Reward.Ordinal.CompareTo(right.Reward.Ordinal);
                if (ordinal != 0) return ordinal;
                return left.PickupStableId.CompareTo(right.PickupStableId);
            });
        }

        private static RunLootCollectionResult RejectedCollection(
            RunLootCollectionStatus status,
            RunLootCollectionCommand command,
            RunLootSnapshot pickup,
            string diagnostic)
        {
            return new RunLootCollectionResult(
                status,
                command,
                pickup,
                null,
                diagnostic);
        }

        private static RunLootCollectionStatus MapSessionRejection(
            RunLootSessionRecordResult sessionResult)
        {
            if (sessionResult == null)
                return RunLootCollectionStatus.Rejected;
            switch (sessionResult.Status)
            {
                case RunLootSessionRecordStatus.ConflictingDuplicate:
                    return RunLootCollectionStatus.ConflictingDuplicate;
                case RunLootSessionRecordStatus.WrongRun:
                    return RunLootCollectionStatus.WrongRun;
                case RunLootSessionRecordStatus.StaleLifecycle:
                    return RunLootCollectionStatus.StaleLifecycle;
                case RunLootSessionRecordStatus.UnauthorizedCollector:
                    return RunLootCollectionStatus.UnauthorizedCollector;
                default:
                    return RunLootCollectionStatus.Rejected;
            }
        }
    }
}

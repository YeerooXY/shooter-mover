using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    /// <summary>
    /// Engine-neutral run-local authority for exact generated reward children.
    /// Unity objects are projections only; this authority owns realization, availability,
    /// collection replay, and the exact typed collection journal.
    /// </summary>
    public sealed partial class RunLocalPickupAuthorityV1 : IRunPickupCollectionAuthorityV1
    {
        private sealed class CollectionReplayRecord
        {
            public CollectionReplayRecord(
                string commandFingerprint,
                RunPickupCollectionResultV1 result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunPickupCollectionResultV1 Result { get; }
        }

        private readonly object gate = new object();
        private readonly IRunPickupRunSessionPortV1 runSession;
        private readonly IRunPickupSourcePositionPortV1 sourcePositions;
        private readonly Dictionary<StableId, RunPickupSnapshotV1> byPickup =
            new Dictionary<StableId, RunPickupSnapshotV1>();
        private readonly Dictionary<StableId, StableId> pickupByGeneratedChild =
            new Dictionary<StableId, StableId>();
        private readonly Dictionary<StableId, string> batchIdentityFingerprintByDropOperation =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, CollectionReplayRecord> collectionReplay =
            new Dictionary<StableId, CollectionReplayRecord>();

        private long collectionSequence;

        public RunLocalPickupAuthorityV1(
            IRunPickupRunSessionPortV1 runSession,
            IRunPickupSourcePositionPortV1 sourcePositions)
        {
            this.runSession = runSession
                ?? throw new ArgumentNullException(nameof(runSession));
            this.sourcePositions = sourcePositions
                ?? throw new ArgumentNullException(nameof(sourcePositions));
        }

        public StableId RunStableId { get { return runSession.RunStableId; } }
        public long LifecycleGeneration { get { return runSession.LifecycleGeneration; } }

        public int PickupCount
        {
            get
            {
                lock (gate)
                {
                    return CountCurrentLifecycle(null);
                }
            }
        }

        public int AvailablePickupCount
        {
            get
            {
                lock (gate)
                {
                    return CountCurrentLifecycle(RunPickupStateV1.Available);
                }
            }
        }

        public int CollectedPickupCount
        {
            get
            {
                lock (gate)
                {
                    return CountCurrentLifecycle(RunPickupStateV1.Collected);
                }
            }
        }

        public RunPickupRealizationResultV1 Realize(
            RunPickupGeneratedBatchV1 batch)
        {
            if (batch == null)
            {
                return new RunPickupRealizationResultV1(
                    RunPickupRealizationStatusV1.Rejected,
                    null,
                    Array.Empty<RunPickupSnapshotV1>(),
                    "run-pickup-batch-null");
            }

            lock (gate)
            {
                string contextRejection = ValidateBatchContext(batch);
                if (!string.IsNullOrEmpty(contextRejection))
                {
                    return new RunPickupRealizationResultV1(
                        RunPickupRealizationStatusV1.Rejected,
                        batch,
                        Array.Empty<RunPickupSnapshotV1>(),
                        contextRejection);
                }

                string existingBatchFingerprint;
                if (batchIdentityFingerprintByDropOperation.TryGetValue(
                    batch.DropOperationStableId,
                    out existingBatchFingerprint)
                    && !string.Equals(
                        existingBatchFingerprint,
                        batch.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return new RunPickupRealizationResultV1(
                        RunPickupRealizationStatusV1.ConflictingDuplicate,
                        batch,
                        ExportBatchPickupsUnsafe(batch.DropOperationStableId),
                        "run-pickup-drop-operation-conflict");
                }

                var pickupIds = new List<StableId>(batch.GeneratedRewards.Count);
                for (int index = 0; index < batch.GeneratedRewards.Count; index++)
                {
                    RunPickupGeneratedRewardV1 reward = batch.GeneratedRewards[index];
                    StableId pickupId = RunPickupIdentityV1.DerivePickupStableId(
                        batch,
                        reward);
                    pickupIds.Add(pickupId);

                    StableId existingPickupForChild;
                    if (pickupByGeneratedChild.TryGetValue(
                        reward.RewardInstanceStableId,
                        out existingPickupForChild)
                        && existingPickupForChild != pickupId)
                    {
                        return new RunPickupRealizationResultV1(
                            RunPickupRealizationStatusV1.ConflictingDuplicate,
                            batch,
                            ExportBatchPickupsUnsafe(batch.DropOperationStableId),
                            "run-pickup-generated-child-identity-conflict");
                    }

                    RunPickupSnapshotV1 existingPickup;
                    if (byPickup.TryGetValue(pickupId, out existingPickup))
                    {
                        var expected = CreatePendingSnapshot(batch, reward, pickupId);
                        if (!string.Equals(
                            existingPickup.IdentityFingerprint,
                            expected.IdentityFingerprint,
                            StringComparison.Ordinal))
                        {
                            return new RunPickupRealizationResultV1(
                                RunPickupRealizationStatusV1.ConflictingDuplicate,
                                batch,
                                ExportBatchPickupsUnsafe(batch.DropOperationStableId),
                                "run-pickup-stable-id-context-conflict");
                        }
                    }
                }

                RunPickupWorldSpawnContextV1 worldSpawnContext;
                string positionDiagnostic;
                bool positionResolved;
                try
                {
                    positionResolved = sourcePositions.TryResolve(
                        batch.RunStableId,
                        batch.RunLifecycleGeneration,
                        batch.SourceEntityStableId,
                        batch.SourcePlacementStableId,
                        out worldSpawnContext,
                        out positionDiagnostic);
                }
                catch (Exception exception)
                {
                    positionResolved = false;
                    worldSpawnContext = null;
                    positionDiagnostic =
                        "run-pickup-source-position-exception:" + exception.Message;
                }

                if (!positionResolved || worldSpawnContext == null)
                {
                    bool createdPending = false;
                    for (int index = 0; index < batch.GeneratedRewards.Count; index++)
                    {
                        RunPickupGeneratedRewardV1 reward = batch.GeneratedRewards[index];
                        StableId pickupId = pickupIds[index];
                        RunPickupSnapshotV1 existing;
                        if (byPickup.TryGetValue(pickupId, out existing))
                        {
                            continue;
                        }

                        RunPickupSnapshotV1 pending = CreatePendingSnapshot(
                            batch,
                            reward,
                            pickupId,
                            string.IsNullOrWhiteSpace(positionDiagnostic)
                                ? "run-pickup-source-position-unresolved"
                                : positionDiagnostic);
                        byPickup.Add(pickupId, pending);
                        pickupByGeneratedChild.Add(
                            reward.RewardInstanceStableId,
                            pickupId);
                        createdPending = true;
                    }

                    if (!batchIdentityFingerprintByDropOperation.ContainsKey(
                        batch.DropOperationStableId))
                    {
                        batchIdentityFingerprintByDropOperation.Add(
                            batch.DropOperationStableId,
                            batch.Fingerprint);
                    }

                    IReadOnlyList<RunPickupSnapshotV1> pendingPickups =
                        ExportBatchPickupsUnsafe(batch.DropOperationStableId);
                    bool alreadyRealized = AllNonPending(pendingPickups);
                    return new RunPickupRealizationResultV1(
                        alreadyRealized && !createdPending
                            ? RunPickupRealizationStatusV1.ExactReplay
                            : RunPickupRealizationStatusV1.PendingSourcePosition,
                        batch,
                        pendingPickups,
                        string.IsNullOrWhiteSpace(positionDiagnostic)
                            ? "run-pickup-source-position-unresolved"
                            : positionDiagnostic);
                }

                for (int index = 0; index < pickupIds.Count; index++)
                {
                    RunPickupSnapshotV1 existing;
                    if (!byPickup.TryGetValue(pickupIds[index], out existing))
                    {
                        continue;
                    }
                    if (existing.WorldSpawnContext != null
                        && !string.Equals(
                            existing.WorldSpawnContext.Fingerprint,
                            worldSpawnContext.Fingerprint,
                            StringComparison.Ordinal))
                    {
                        return new RunPickupRealizationResultV1(
                            RunPickupRealizationStatusV1.ConflictingDuplicate,
                            batch,
                            ExportBatchPickupsUnsafe(batch.DropOperationStableId),
                            "run-pickup-source-position-conflict");
                    }
                }

                bool mutated = false;
                for (int index = 0; index < batch.GeneratedRewards.Count; index++)
                {
                    RunPickupGeneratedRewardV1 reward = batch.GeneratedRewards[index];
                    StableId pickupId = pickupIds[index];
                    RunPickupSnapshotV1 existing;
                    if (!byPickup.TryGetValue(pickupId, out existing))
                    {
                        RunPickupSnapshotV1 available = new RunPickupSnapshotV1(
                            pickupId,
                            batch,
                            reward,
                            RunPickupStateV1.Available,
                            worldSpawnContext,
                            null,
                            null,
                            null,
                            0L,
                            0L,
                            string.Empty);
                        byPickup.Add(pickupId, available);
                        pickupByGeneratedChild.Add(
                            reward.RewardInstanceStableId,
                            pickupId);
                        mutated = true;
                    }
                    else if (existing.State == RunPickupStateV1.PendingSourcePosition)
                    {
                        byPickup[pickupId] = existing.WithAvailable(worldSpawnContext);
                        mutated = true;
                    }
                }

                if (!batchIdentityFingerprintByDropOperation.ContainsKey(
                    batch.DropOperationStableId))
                {
                    batchIdentityFingerprintByDropOperation.Add(
                        batch.DropOperationStableId,
                        batch.Fingerprint);
                }

                return new RunPickupRealizationResultV1(
                    mutated
                        ? RunPickupRealizationStatusV1.Realized
                        : RunPickupRealizationStatusV1.ExactReplay,
                    batch,
                    ExportBatchPickupsUnsafe(batch.DropOperationStableId),
                    string.Empty);
            }
        }

    }
}

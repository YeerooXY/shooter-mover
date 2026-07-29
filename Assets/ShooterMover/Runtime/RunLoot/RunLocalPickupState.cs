using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunLoot
{
    /// <summary>
    /// Engine-neutral run-local authority for exact generated reward children.
    /// Unity objects are projections only; this authority owns realization, availability,
    /// collection replay, and the exact typed collection journal.
    /// </summary>
    public sealed partial class RunLocalPickupState : IRunLootCollectionState
    {
        private sealed class CollectionReplayRecord
        {
            public CollectionReplayRecord(
                string commandFingerprint,
                RunLootCollectionResult result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunLootCollectionResult Result { get; }
        }

        private readonly object gate = new object();
        private readonly IRunLootRunSessionPort runSession;
        private readonly IRunLootSourcePositionPort sourcePositions;
        private readonly Dictionary<StableId, RunLootSnapshot> byPickup =
            new Dictionary<StableId, RunLootSnapshot>();
        private readonly Dictionary<StableId, StableId> pickupByGeneratedChild =
            new Dictionary<StableId, StableId>();
        private readonly Dictionary<StableId, string> batchIdentityFingerprintByDropOperation =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, CollectionReplayRecord> collectionReplay =
            new Dictionary<StableId, CollectionReplayRecord>();

        public RunLocalPickupState(
            IRunLootRunSessionPort runSession,
            IRunLootSourcePositionPort sourcePositions)
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
                    return CountCurrentLifecycle(RunLootState.Available);
                }
            }
        }

        public int CollectedPickupCount
        {
            get
            {
                lock (gate)
                {
                    return CountCurrentLifecycle(RunLootState.Collected);
                }
            }
        }

        public RunLootRealizationResult Realize(
            RunLootGeneratedBatch batch)
        {
            if (batch == null)
            {
                return new RunLootRealizationResult(
                    RunLootRealizationStatus.Rejected,
                    null,
                    Array.Empty<RunLootSnapshot>(),
                    "run-pickup-batch-null");
            }

            lock (gate)
            {
                RunLootRunSessionContext sessionContext;
                string sessionDiagnostic;
                if (!TryReadRunSessionContext(
                    out sessionContext,
                    out sessionDiagnostic))
                {
                    return new RunLootRealizationResult(
                        RunLootRealizationStatus.Rejected,
                        batch,
                        Array.Empty<RunLootSnapshot>(),
                        sessionDiagnostic);
                }

                string contextRejection = ValidateBatchContext(
                    batch,
                    sessionContext);
                if (!string.IsNullOrEmpty(contextRejection))
                {
                    return new RunLootRealizationResult(
                        RunLootRealizationStatus.Rejected,
                        batch,
                        Array.Empty<RunLootSnapshot>(),
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
                    return new RunLootRealizationResult(
                        RunLootRealizationStatus.ConflictingDuplicate,
                        batch,
                        ExportBatchPickupsUnsafe(batch.DropOperationStableId),
                        "run-pickup-drop-operation-conflict");
                }

                var pickupIds = new List<StableId>(batch.GeneratedRewards.Count);
                for (int index = 0; index < batch.GeneratedRewards.Count; index++)
                {
                    RunLootGeneratedReward reward = batch.GeneratedRewards[index];
                    StableId pickupId = RunLootIdentity.DerivePickupStableId(
                        batch,
                        reward);
                    pickupIds.Add(pickupId);

                    StableId existingPickupForChild;
                    if (pickupByGeneratedChild.TryGetValue(
                        reward.RewardInstanceStableId,
                        out existingPickupForChild)
                        && existingPickupForChild != pickupId)
                    {
                        return new RunLootRealizationResult(
                            RunLootRealizationStatus.ConflictingDuplicate,
                            batch,
                            ExportBatchPickupsUnsafe(batch.DropOperationStableId),
                            "run-pickup-generated-child-identity-conflict");
                    }

                    RunLootSnapshot existingPickup;
                    if (byPickup.TryGetValue(pickupId, out existingPickup))
                    {
                        var expected = CreatePendingSnapshot(batch, reward, pickupId);
                        if (!string.Equals(
                            existingPickup.IdentityFingerprint,
                            expected.IdentityFingerprint,
                            StringComparison.Ordinal))
                        {
                            return new RunLootRealizationResult(
                                RunLootRealizationStatus.ConflictingDuplicate,
                                batch,
                                ExportBatchPickupsUnsafe(batch.DropOperationStableId),
                                "run-pickup-stable-id-context-conflict");
                        }
                    }
                }

                RunLootWorldSpawnContext worldSpawnContext;
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
                        "run-pickup-source-position-exception:"
                        + exception.GetType().Name
                        + ":"
                        + exception.Message;
                }

                if (!positionResolved || worldSpawnContext == null)
                {
                    bool createdPending = false;
                    for (int index = 0; index < batch.GeneratedRewards.Count; index++)
                    {
                        RunLootGeneratedReward reward = batch.GeneratedRewards[index];
                        StableId pickupId = pickupIds[index];
                        RunLootSnapshot existing;
                        if (byPickup.TryGetValue(pickupId, out existing))
                        {
                            continue;
                        }

                        RunLootSnapshot pending = CreatePendingSnapshot(
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

                    IReadOnlyList<RunLootSnapshot> pendingPickups =
                        ExportBatchPickupsUnsafe(batch.DropOperationStableId);
                    bool alreadyRealized = AllNonPending(pendingPickups);
                    return new RunLootRealizationResult(
                        alreadyRealized && !createdPending
                            ? RunLootRealizationStatus.ExactReplay
                            : RunLootRealizationStatus.PendingSourcePosition,
                        batch,
                        pendingPickups,
                        string.IsNullOrWhiteSpace(positionDiagnostic)
                            ? "run-pickup-source-position-unresolved"
                            : positionDiagnostic);
                }

                for (int index = 0; index < pickupIds.Count; index++)
                {
                    RunLootSnapshot existing;
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
                        return new RunLootRealizationResult(
                            RunLootRealizationStatus.ConflictingDuplicate,
                            batch,
                            ExportBatchPickupsUnsafe(batch.DropOperationStableId),
                            "run-pickup-source-position-conflict");
                    }
                }

                bool mutated = false;
                for (int index = 0; index < batch.GeneratedRewards.Count; index++)
                {
                    RunLootGeneratedReward reward = batch.GeneratedRewards[index];
                    StableId pickupId = pickupIds[index];
                    RunLootSnapshot existing;
                    if (!byPickup.TryGetValue(pickupId, out existing))
                    {
                        RunLootSnapshot available = new RunLootSnapshot(
                            pickupId,
                            batch,
                            reward,
                            RunLootState.Available,
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
                    else if (existing.State == RunLootState.PendingSourcePosition)
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

                return new RunLootRealizationResult(
                    mutated
                        ? RunLootRealizationStatus.Realized
                        : RunLootRealizationStatus.ExactReplay,
                    batch,
                    ExportBatchPickupsUnsafe(batch.DropOperationStableId),
                    string.Empty);
            }
        }
    }
}

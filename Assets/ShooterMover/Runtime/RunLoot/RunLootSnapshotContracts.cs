using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunLoot
{
    public sealed class RunLootSnapshot
    {
        public RunLootSnapshot(
            StableId pickupStableId,
            RunLootGeneratedBatch batch,
            RunLootGeneratedReward reward,
            RunLootState state,
            RunLootWorldSpawnContext worldSpawnContext,
            StableId collectorEntityStableId,
            StableId collectorParticipantStableId,
            StableId collectionOperationStableId,
            long collectionOrder,
            long collectedAtAuthoritativeTick,
            string diagnostic)
        {
            PickupStableId = pickupStableId
                ?? throw new ArgumentNullException(nameof(pickupStableId));
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            Reward = reward ?? throw new ArgumentNullException(nameof(reward));
            if (!Enum.IsDefined(typeof(RunLootState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (collectionOrder < 0L)
                throw new ArgumentOutOfRangeException(nameof(collectionOrder));
            if (collectedAtAuthoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(collectedAtAuthoritativeTick));
            }
            if (state == RunLootState.Available && worldSpawnContext == null)
            {
                throw new ArgumentException(
                    "Available pickups require an authoritative world-spawn context.",
                    nameof(worldSpawnContext));
            }
            if (state == RunLootState.Collected
                && (collectorEntityStableId == null
                    || collectorParticipantStableId == null
                    || collectionOperationStableId == null
                    || collectionOrder < 1L))
            {
                throw new ArgumentException(
                    "Collected pickups require collector, operation, and order facts.");
            }

            State = state;
            WorldSpawnContext = worldSpawnContext;
            CollectorEntityStableId = collectorEntityStableId;
            CollectorParticipantStableId = collectorParticipantStableId;
            CollectionOperationStableId = collectionOperationStableId;
            CollectionOrder = collectionOrder;
            CollectedAtAuthoritativeTick = collectedAtAuthoritativeTick;
            Diagnostic = diagnostic ?? string.Empty;
            IdentityFingerprint = RunLoot.Hash(ToIdentityCanonicalString());
            Fingerprint = RunLoot.Hash(ToCanonicalString());
        }

        public StableId PickupStableId { get; }
        public RunLootGeneratedBatch Batch { get; }
        public RunLootGeneratedReward Reward { get; }
        public RunLootState State { get; }
        public RunLootWorldSpawnContext WorldSpawnContext { get; }
        public StableId CollectorEntityStableId { get; }
        public StableId CollectorParticipantStableId { get; }
        public StableId CollectionOperationStableId { get; }
        public long CollectionOrder { get; }
        public long CollectedAtAuthoritativeTick { get; }
        public string Diagnostic { get; }
        public string IdentityFingerprint { get; }
        public string Fingerprint { get; }

        public RunLootSnapshot WithAvailable(
            RunLootWorldSpawnContext worldSpawnContext)
        {
            return new RunLootSnapshot(
                PickupStableId,
                Batch,
                Reward,
                RunLootState.Available,
                worldSpawnContext,
                null,
                null,
                null,
                0L,
                0L,
                string.Empty);
        }

        public RunLootSnapshot WithCollected(
            StableId collectorEntityStableId,
            StableId collectorParticipantStableId,
            StableId collectionOperationStableId,
            long collectionOrder,
            long collectedAtAuthoritativeTick)
        {
            return new RunLootSnapshot(
                PickupStableId,
                Batch,
                Reward,
                RunLootState.Collected,
                WorldSpawnContext,
                collectorEntityStableId,
                collectorParticipantStableId,
                collectionOperationStableId,
                collectionOrder,
                collectedAtAuthoritativeTick,
                string.Empty);
        }

        public string ToIdentityCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-identity-v1");
            RunLoot.Append(builder, "pickup", PickupStableId);
            RunLoot.Append(builder, "batch", Batch.Fingerprint);
            RunLoot.Append(builder, "reward", Reward.Fingerprint);
            return builder.ToString();
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder(ToIdentityCanonicalString());
            RunLoot.Append(builder, "state", (int)State);
            RunLoot.Append(
                builder,
                "world",
                WorldSpawnContext == null ? null : WorldSpawnContext.Fingerprint);
            RunLoot.Append(
                builder,
                "collector-entity",
                CollectorEntityStableId);
            RunLoot.Append(
                builder,
                "collector-participant",
                CollectorParticipantStableId);
            RunLoot.Append(
                builder,
                "collection-operation",
                CollectionOperationStableId);
            RunLoot.Append(builder, "collection-order", CollectionOrder);
            RunLoot.Append(
                builder,
                "collected-tick",
                CollectedAtAuthoritativeTick);
            RunLoot.Append(builder, "diagnostic", Diagnostic);
            return builder.ToString();
        }
    }

    public sealed class RunLootRealizationResult
    {
        private readonly ReadOnlyCollection<RunLootSnapshot> pickups;

        public RunLootRealizationResult(
            RunLootRealizationStatus status,
            RunLootGeneratedBatch batch,
            IEnumerable<RunLootSnapshot> realizedPickups,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunLootRealizationStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Batch = batch;
            var copy = new List<RunLootSnapshot>();
            if (realizedPickups != null)
            {
                foreach (RunLootSnapshot pickup in realizedPickups)
                {
                    if (pickup == null)
                        throw new ArgumentException(
                            "Realization results cannot contain null pickups.",
                            nameof(realizedPickups));
                    copy.Add(pickup);
                }
            }
            copy.Sort(delegate(RunLootSnapshot left, RunLootSnapshot right)
            {
                return left.Reward.Ordinal.CompareTo(right.Reward.Ordinal);
            });
            pickups = new ReadOnlyCollection<RunLootSnapshot>(copy);
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunLootRealizationStatus Status { get; }
        public RunLootGeneratedBatch Batch { get; }
        public IReadOnlyList<RunLootSnapshot> Pickups { get { return pickups; } }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == RunLootRealizationStatus.Realized
                    || Status == RunLootRealizationStatus.ExactReplay
                    || Status == RunLootRealizationStatus.PendingSourcePosition;
            }
        }
    }
}

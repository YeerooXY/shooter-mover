using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    public sealed class RunPickupSnapshotV1
    {
        public RunPickupSnapshotV1(
            StableId pickupStableId,
            RunPickupGeneratedBatchV1 batch,
            RunPickupGeneratedRewardV1 reward,
            RunPickupStateV1 state,
            RunPickupWorldSpawnContextV1 worldSpawnContext,
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
            if (!Enum.IsDefined(typeof(RunPickupStateV1), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (collectionOrder < 0L)
                throw new ArgumentOutOfRangeException(nameof(collectionOrder));
            if (collectedAtAuthoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(collectedAtAuthoritativeTick));
            }
            if (state == RunPickupStateV1.Available && worldSpawnContext == null)
            {
                throw new ArgumentException(
                    "Available pickups require an authoritative world-spawn context.",
                    nameof(worldSpawnContext));
            }
            if (state == RunPickupStateV1.Collected
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
            IdentityFingerprint = RunPickupCanonicalV1.Hash(ToIdentityCanonicalString());
            Fingerprint = RunPickupCanonicalV1.Hash(ToCanonicalString());
        }

        public StableId PickupStableId { get; }
        public RunPickupGeneratedBatchV1 Batch { get; }
        public RunPickupGeneratedRewardV1 Reward { get; }
        public RunPickupStateV1 State { get; }
        public RunPickupWorldSpawnContextV1 WorldSpawnContext { get; }
        public StableId CollectorEntityStableId { get; }
        public StableId CollectorParticipantStableId { get; }
        public StableId CollectionOperationStableId { get; }
        public long CollectionOrder { get; }
        public long CollectedAtAuthoritativeTick { get; }
        public string Diagnostic { get; }
        public string IdentityFingerprint { get; }
        public string Fingerprint { get; }

        public RunPickupSnapshotV1 WithAvailable(
            RunPickupWorldSpawnContextV1 worldSpawnContext)
        {
            return new RunPickupSnapshotV1(
                PickupStableId,
                Batch,
                Reward,
                RunPickupStateV1.Available,
                worldSpawnContext,
                null,
                null,
                null,
                0L,
                0L,
                string.Empty);
        }

        public RunPickupSnapshotV1 WithCollected(
            StableId collectorEntityStableId,
            StableId collectorParticipantStableId,
            StableId collectionOperationStableId,
            long collectionOrder,
            long collectedAtAuthoritativeTick)
        {
            return new RunPickupSnapshotV1(
                PickupStableId,
                Batch,
                Reward,
                RunPickupStateV1.Collected,
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
            RunPickupCanonicalV1.Append(builder, "pickup", PickupStableId);
            RunPickupCanonicalV1.Append(builder, "batch", Batch.Fingerprint);
            RunPickupCanonicalV1.Append(builder, "reward", Reward.Fingerprint);
            return builder.ToString();
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder(ToIdentityCanonicalString());
            RunPickupCanonicalV1.Append(builder, "state", (int)State);
            RunPickupCanonicalV1.Append(
                builder,
                "world",
                WorldSpawnContext == null ? null : WorldSpawnContext.Fingerprint);
            RunPickupCanonicalV1.Append(
                builder,
                "collector-entity",
                CollectorEntityStableId);
            RunPickupCanonicalV1.Append(
                builder,
                "collector-participant",
                CollectorParticipantStableId);
            RunPickupCanonicalV1.Append(
                builder,
                "collection-operation",
                CollectionOperationStableId);
            RunPickupCanonicalV1.Append(builder, "collection-order", CollectionOrder);
            RunPickupCanonicalV1.Append(
                builder,
                "collected-tick",
                CollectedAtAuthoritativeTick);
            RunPickupCanonicalV1.Append(builder, "diagnostic", Diagnostic);
            return builder.ToString();
        }
    }

    public sealed class RunPickupRealizationResultV1
    {
        private readonly ReadOnlyCollection<RunPickupSnapshotV1> pickups;

        public RunPickupRealizationResultV1(
            RunPickupRealizationStatusV1 status,
            RunPickupGeneratedBatchV1 batch,
            IEnumerable<RunPickupSnapshotV1> realizedPickups,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunPickupRealizationStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Batch = batch;
            var copy = new List<RunPickupSnapshotV1>();
            if (realizedPickups != null)
            {
                foreach (RunPickupSnapshotV1 pickup in realizedPickups)
                {
                    if (pickup == null)
                        throw new ArgumentException(
                            "Realization results cannot contain null pickups.",
                            nameof(realizedPickups));
                    copy.Add(pickup);
                }
            }
            copy.Sort(delegate(RunPickupSnapshotV1 left, RunPickupSnapshotV1 right)
            {
                return left.Reward.Ordinal.CompareTo(right.Reward.Ordinal);
            });
            pickups = new ReadOnlyCollection<RunPickupSnapshotV1>(copy);
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunPickupRealizationStatusV1 Status { get; }
        public RunPickupGeneratedBatchV1 Batch { get; }
        public IReadOnlyList<RunPickupSnapshotV1> Pickups { get { return pickups; } }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == RunPickupRealizationStatusV1.Realized
                    || Status == RunPickupRealizationStatusV1.ExactReplay
                    || Status == RunPickupRealizationStatusV1.PendingSourcePosition;
            }
        }
    }
}

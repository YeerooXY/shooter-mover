using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    public sealed class RunPickupSnapshot
    {
        public RunPickupSnapshot(
            StableId pickupStableId,
            RunPickupGeneratedBatch batch,
            RunPickupGeneratedReward reward,
            RunPickupState state,
            RunPickupWorldSpawnContext worldSpawnContext,
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
            if (!Enum.IsDefined(typeof(RunPickupState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (collectionOrder < 0L)
                throw new ArgumentOutOfRangeException(nameof(collectionOrder));
            if (collectedAtAuthoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(collectedAtAuthoritativeTick));
            }
            if (state == RunPickupState.Available && worldSpawnContext == null)
            {
                throw new ArgumentException(
                    "Available pickups require an authoritative world-spawn context.",
                    nameof(worldSpawnContext));
            }
            if (state == RunPickupState.Collected
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
            IdentityFingerprint = RunPickup.Hash(ToIdentityCanonicalString());
            Fingerprint = RunPickup.Hash(ToCanonicalString());
        }

        public StableId PickupStableId { get; }
        public RunPickupGeneratedBatch Batch { get; }
        public RunPickupGeneratedReward Reward { get; }
        public RunPickupState State { get; }
        public RunPickupWorldSpawnContext WorldSpawnContext { get; }
        public StableId CollectorEntityStableId { get; }
        public StableId CollectorParticipantStableId { get; }
        public StableId CollectionOperationStableId { get; }
        public long CollectionOrder { get; }
        public long CollectedAtAuthoritativeTick { get; }
        public string Diagnostic { get; }
        public string IdentityFingerprint { get; }
        public string Fingerprint { get; }

        public RunPickupSnapshot WithAvailable(
            RunPickupWorldSpawnContext worldSpawnContext)
        {
            return new RunPickupSnapshot(
                PickupStableId,
                Batch,
                Reward,
                RunPickupState.Available,
                worldSpawnContext,
                null,
                null,
                null,
                0L,
                0L,
                string.Empty);
        }

        public RunPickupSnapshot WithCollected(
            StableId collectorEntityStableId,
            StableId collectorParticipantStableId,
            StableId collectionOperationStableId,
            long collectionOrder,
            long collectedAtAuthoritativeTick)
        {
            return new RunPickupSnapshot(
                PickupStableId,
                Batch,
                Reward,
                RunPickupState.Collected,
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
            RunPickup.Append(builder, "pickup", PickupStableId);
            RunPickup.Append(builder, "batch", Batch.Fingerprint);
            RunPickup.Append(builder, "reward", Reward.Fingerprint);
            return builder.ToString();
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder(ToIdentityCanonicalString());
            RunPickup.Append(builder, "state", (int)State);
            RunPickup.Append(
                builder,
                "world",
                WorldSpawnContext == null ? null : WorldSpawnContext.Fingerprint);
            RunPickup.Append(
                builder,
                "collector-entity",
                CollectorEntityStableId);
            RunPickup.Append(
                builder,
                "collector-participant",
                CollectorParticipantStableId);
            RunPickup.Append(
                builder,
                "collection-operation",
                CollectionOperationStableId);
            RunPickup.Append(builder, "collection-order", CollectionOrder);
            RunPickup.Append(
                builder,
                "collected-tick",
                CollectedAtAuthoritativeTick);
            RunPickup.Append(builder, "diagnostic", Diagnostic);
            return builder.ToString();
        }
    }

    public sealed class RunPickupRealizationResult
    {
        private readonly ReadOnlyCollection<RunPickupSnapshot> pickups;

        public RunPickupRealizationResult(
            RunPickupRealizationStatus status,
            RunPickupGeneratedBatch batch,
            IEnumerable<RunPickupSnapshot> realizedPickups,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunPickupRealizationStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Batch = batch;
            var copy = new List<RunPickupSnapshot>();
            if (realizedPickups != null)
            {
                foreach (RunPickupSnapshot pickup in realizedPickups)
                {
                    if (pickup == null)
                        throw new ArgumentException(
                            "Realization results cannot contain null pickups.",
                            nameof(realizedPickups));
                    copy.Add(pickup);
                }
            }
            copy.Sort(delegate(RunPickupSnapshot left, RunPickupSnapshot right)
            {
                return left.Reward.Ordinal.CompareTo(right.Reward.Ordinal);
            });
            pickups = new ReadOnlyCollection<RunPickupSnapshot>(copy);
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunPickupRealizationStatus Status { get; }
        public RunPickupGeneratedBatch Batch { get; }
        public IReadOnlyList<RunPickupSnapshot> Pickups { get { return pickups; } }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == RunPickupRealizationStatus.Realized
                    || Status == RunPickupRealizationStatus.ExactReplay
                    || Status == RunPickupRealizationStatus.PendingSourcePosition;
            }
        }
    }
}

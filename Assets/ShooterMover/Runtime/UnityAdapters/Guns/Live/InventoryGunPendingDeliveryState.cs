using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    public enum InventoryGunPendingAdmissionStatus
    {
        Accepted = 1,
        ExactDuplicate = 2,
        ConflictingDuplicate = 3,
        CapacityExceeded = 4,
        InvalidEntry = 5,
    }

    /// <summary>
    /// One already scheduler-authorized and already adapted emission. This value contains no
    /// firing-admission policy; it is only the immutable delivery payload retained by the live
    /// runtime until the scheduler-authored tick is due and the existing sink accepts it.
    /// </summary>
    public sealed class InventoryGunPendingDeliveryEntry
    {
        private InventoryGunPendingDeliveryEntry(
            long scheduledTick,
            FireOperationId emissionFireOperationId,
            FireOperationId sourceFireOperationId,
            string acceptedEmissionFingerprint,
            string effectiveGunFingerprint,
            GunActorInstanceId actorId,
            RunParticipantId participantId,
            EquipmentInstanceId equipmentInstanceId,
            GunDefinitionId gunDefinitionId,
            LifecycleGeneration lifecycleGeneration,
            long cadenceOrdinal,
            long shotSequence,
            ProjectileOrdinal projectileOrdinal,
            int emissionOrdinal,
            int triggerGroupOrdinal,
            int burstShotOrdinal,
            int pulseOrdinal,
            InventoryGunEffectBatch projectedBatch)
        {
            if (scheduledTick < 0L
                || cadenceOrdinal < 0L
                || shotSequence < 0L
                || emissionOrdinal < 0
                || triggerGroupOrdinal < 0
                || burstShotOrdinal < 0
                || pulseOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scheduledTick),
                    "Pending delivery timing and ordinals must be non-negative.");
            }

            ScheduledTick = scheduledTick;
            EmissionFireOperationId = emissionFireOperationId
                ?? throw new ArgumentNullException(nameof(emissionFireOperationId));
            SourceFireOperationId = sourceFireOperationId
                ?? throw new ArgumentNullException(nameof(sourceFireOperationId));
            AcceptedEmissionFingerprint = acceptedEmissionFingerprint
                ?? throw new ArgumentNullException(nameof(acceptedEmissionFingerprint));
            EffectiveGunFingerprint = effectiveGunFingerprint
                ?? throw new ArgumentNullException(nameof(effectiveGunFingerprint));
            ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            ParticipantId = participantId
                ?? throw new ArgumentNullException(nameof(participantId));
            EquipmentInstanceId = equipmentInstanceId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
            GunDefinitionId = gunDefinitionId
                ?? throw new ArgumentNullException(nameof(gunDefinitionId));
            LifecycleGeneration = lifecycleGeneration
                ?? throw new ArgumentNullException(nameof(lifecycleGeneration));
            ProjectileOrdinal = projectileOrdinal
                ?? throw new ArgumentNullException(nameof(projectileOrdinal));
            ProjectedBatch = projectedBatch
                ?? throw new ArgumentNullException(nameof(projectedBatch));
            CadenceOrdinal = cadenceOrdinal;
            ShotSequence = shotSequence;
            EmissionOrdinal = emissionOrdinal;
            TriggerGroupOrdinal = triggerGroupOrdinal;
            BurstShotOrdinal = burstShotOrdinal;
            PulseOrdinal = pulseOrdinal;
            IdentityKey = BuildIdentityKey(
                ActorId,
                LifecycleGeneration,
                EmissionFireOperationId);
            CanonicalText = BuildCanonicalText();
            Fingerprint = GunExecutionFingerprint.Compute(CanonicalText);

            if (!HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The projected batch does not match the scheduler-authorized emission.",
                    nameof(projectedBatch));
            }
        }

        public long ScheduledTick { get; }
        public FireOperationId EmissionFireOperationId { get; }
        public FireOperationId SourceFireOperationId { get; }
        public string AcceptedEmissionFingerprint { get; }
        public string EffectiveGunFingerprint { get; }
        public GunActorInstanceId ActorId { get; }
        public RunParticipantId ParticipantId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public GunDefinitionId GunDefinitionId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }
        public long CadenceOrdinal { get; }
        public long ShotSequence { get; }
        public ProjectileOrdinal ProjectileOrdinal { get; }
        public int EmissionOrdinal { get; }
        public int TriggerGroupOrdinal { get; }
        public int BurstShotOrdinal { get; }
        public int PulseOrdinal { get; }
        public InventoryGunEffectBatch ProjectedBatch { get; }
        public string IdentityKey { get; }
        public string CanonicalText { get; }
        public string Fingerprint { get; }

        public static InventoryGunPendingDeliveryEntry From(
            GunFiringScheduler.AcceptedEmission emission,
            InventoryGunEffectBatch projectedBatch)
        {
            if (emission == null)
            {
                throw new ArgumentNullException(nameof(emission));
            }
            if (projectedBatch == null || projectedBatch.Identity == null)
            {
                throw new ArgumentNullException(nameof(projectedBatch));
            }

            return new InventoryGunPendingDeliveryEntry(
                emission.ScheduledTick,
                emission.EmissionFireOperationId,
                emission.SourceFireOperationId,
                emission.Fingerprint,
                emission.EffectiveGunFingerprint,
                emission.Command.ActorId,
                emission.ParticipantId,
                emission.EquipmentInstanceId,
                emission.GunDefinitionId,
                emission.Command.LifecycleGeneration,
                emission.CadenceOrdinal,
                emission.ShotSequence,
                projectedBatch.Identity.ProjectileOrdinal,
                emission.EmissionOrdinal,
                emission.TriggerGroupOrdinal,
                emission.BurstShotOrdinal,
                emission.PulseOrdinal,
                projectedBatch);
        }

        public bool HasValidFingerprint()
        {
            GunEffectIdentity identity = ProjectedBatch == null
                ? null
                : ProjectedBatch.Identity;
            return ScheduledTick >= 0L
                && EmissionFireOperationId != null
                && SourceFireOperationId != null
                && !string.IsNullOrWhiteSpace(AcceptedEmissionFingerprint)
                && !string.IsNullOrWhiteSpace(EffectiveGunFingerprint)
                && ActorId != null
                && ParticipantId != null
                && EquipmentInstanceId != null
                && GunDefinitionId != null
                && LifecycleGeneration != null
                && CadenceOrdinal >= 0L
                && ShotSequence >= 0L
                && ProjectileOrdinal != null
                && EmissionOrdinal >= 0
                && TriggerGroupOrdinal >= 0
                && BurstShotOrdinal >= 0
                && PulseOrdinal >= 0
                && ProjectedBatch != null
                && identity != null
                && identity.ActorId.Equals(ActorId)
                && identity.ParticipantId.Equals(ParticipantId)
                && identity.EquipmentInstanceId.Equals(EquipmentInstanceId)
                && identity.GunDefinitionId.Equals(GunDefinitionId)
                && identity.FireOperationId.Equals(EmissionFireOperationId)
                && identity.LifecycleGeneration.Equals(LifecycleGeneration)
                && identity.ShotSequence == ShotSequence
                && identity.ProjectileOrdinal.Equals(ProjectileOrdinal)
                && ProjectedBatch.Profile.DefinitionId.Equals(GunDefinitionId)
                && string.Equals(
                    IdentityKey,
                    BuildIdentityKey(
                        ActorId,
                        LifecycleGeneration,
                        EmissionFireOperationId),
                    StringComparison.Ordinal)
                && string.Equals(
                    Fingerprint,
                    GunExecutionFingerprint.Compute(BuildCanonicalText()),
                    StringComparison.Ordinal);
        }

        internal static int Compare(
            InventoryGunPendingDeliveryEntry left,
            InventoryGunPendingDeliveryEntry right)
        {
            int tick = left.ScheduledTick.CompareTo(right.ScheduledTick);
            if (tick != 0) { return tick; }
            int cadence = left.CadenceOrdinal.CompareTo(right.CadenceOrdinal);
            if (cadence != 0) { return cadence; }
            int group = left.TriggerGroupOrdinal.CompareTo(right.TriggerGroupOrdinal);
            if (group != 0) { return group; }
            int burst = left.BurstShotOrdinal.CompareTo(right.BurstShotOrdinal);
            if (burst != 0) { return burst; }
            int pulse = left.PulseOrdinal.CompareTo(right.PulseOrdinal);
            if (pulse != 0) { return pulse; }
            int emission = left.EmissionOrdinal.CompareTo(right.EmissionOrdinal);
            return emission != 0
                ? emission
                : string.CompareOrdinal(
                    left.EmissionFireOperationId.ToString(),
                    right.EmissionFireOperationId.ToString());
        }

        internal static string BuildIdentityKey(
            GunActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration,
            FireOperationId emissionFireOperationId)
        {
            return actorId + "|" + lifecycleGeneration + "|" + emissionFireOperationId;
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            Append(builder, "scheduled_tick", ScheduledTick.ToString(CultureInfo.InvariantCulture));
            Append(builder, "emission_fire_operation_id", EmissionFireOperationId.ToString());
            Append(builder, "source_fire_operation_id", SourceFireOperationId.ToString());
            Append(builder, "accepted_emission_fingerprint", AcceptedEmissionFingerprint);
            Append(builder, "effective_gun_fingerprint", EffectiveGunFingerprint);
            Append(builder, "actor_id", ActorId.ToString());
            Append(builder, "participant_id", ParticipantId.ToString());
            Append(builder, "equipment_instance_id", EquipmentInstanceId.ToString());
            Append(builder, "gun_definition_id", GunDefinitionId.ToString());
            Append(builder, "lifecycle_generation", LifecycleGeneration.ToString());
            Append(builder, "cadence_ordinal", CadenceOrdinal.ToString(CultureInfo.InvariantCulture));
            Append(builder, "shot_sequence", ShotSequence.ToString(CultureInfo.InvariantCulture));
            Append(builder, "projectile_ordinal", ProjectileOrdinal.ToString());
            Append(builder, "emission_ordinal", EmissionOrdinal.ToString(CultureInfo.InvariantCulture));
            Append(builder, "trigger_group_ordinal", TriggerGroupOrdinal.ToString(CultureInfo.InvariantCulture));
            Append(builder, "burst_shot_ordinal", BurstShotOrdinal.ToString(CultureInfo.InvariantCulture));
            Append(builder, "pulse_ordinal", PulseOrdinal.ToString(CultureInfo.InvariantCulture));
            Append(builder, "projected_batch_fingerprint", ProjectedBatch.Fingerprint);
            Append(builder, "inventory_effect_profile_fingerprint", ProjectedBatch.Profile.Fingerprint);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string name, string value)
        {
            builder.Append(name).Append('=').Append(value ?? "null").Append('\n');
        }
    }

    public sealed class InventoryGunPendingAdmissionResult
    {
        internal InventoryGunPendingAdmissionResult(
            InventoryGunPendingAdmissionStatus status,
            InventoryGunPendingDeliveryState nextState,
            int addedCount,
            string rejectionCode)
        {
            Status = status;
            NextState = nextState
                ?? throw new ArgumentNullException(nameof(nextState));
            AddedCount = addedCount;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public InventoryGunPendingAdmissionStatus Status { get; }
        public InventoryGunPendingDeliveryState NextState { get; }
        public int AddedCount { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == InventoryGunPendingAdmissionStatus.Accepted
                    || Status == InventoryGunPendingAdmissionStatus.ExactDuplicate;
            }
        }
    }

    /// <summary>
    /// Caller-owned immutable outbox. Capacity limits only actual pending work. Delivered receipts
    /// prevent reconstruction only while the canonical scheduler retains the matching accepted
    /// schedule replay record; receipt pruning never removes pending entries or interprets firing.
    /// </summary>
    public sealed class InventoryGunPendingDeliveryState
    {
        public const int DefaultCapacity = 65536;

        private readonly ReadOnlyCollection<InventoryGunPendingDeliveryEntry> pending;
        private readonly Dictionary<string, InventoryGunPendingDeliveryEntry> pendingByIdentity;
        private readonly Dictionary<string, string> deliveredByIdentity;

        private InventoryGunPendingDeliveryState(
            int capacity,
            IList<InventoryGunPendingDeliveryEntry> pendingEntries,
            IDictionary<string, string> deliveredFingerprints)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            if (pendingEntries == null)
            {
                throw new ArgumentNullException(nameof(pendingEntries));
            }
            if (deliveredFingerprints == null)
            {
                throw new ArgumentNullException(nameof(deliveredFingerprints));
            }
            if (pendingEntries.Count > capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(pendingEntries));
            }

            Capacity = capacity;
            var ordered = new List<InventoryGunPendingDeliveryEntry>(pendingEntries);
            ordered.Sort(InventoryGunPendingDeliveryEntry.Compare);
            pending = new ReadOnlyCollection<InventoryGunPendingDeliveryEntry>(ordered);
            pendingByIdentity = new Dictionary<string, InventoryGunPendingDeliveryEntry>(
                StringComparer.Ordinal);
            for (int index = 0; index < ordered.Count; index++)
            {
                InventoryGunPendingDeliveryEntry entry = ordered[index];
                if (entry == null
                    || !entry.HasValidFingerprint()
                    || pendingByIdentity.ContainsKey(entry.IdentityKey))
                {
                    throw new ArgumentException(
                        "Pending delivery entries must be valid and identity-unique.",
                        nameof(pendingEntries));
                }
                pendingByIdentity.Add(entry.IdentityKey, entry);
            }

            deliveredByIdentity = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> receipt in deliveredFingerprints)
            {
                if (string.IsNullOrWhiteSpace(receipt.Key)
                    || string.IsNullOrWhiteSpace(receipt.Value)
                    || pendingByIdentity.ContainsKey(receipt.Key))
                {
                    throw new ArgumentException(
                        "Delivered receipts must be valid and disjoint from pending work.",
                        nameof(deliveredFingerprints));
                }
                deliveredByIdentity.Add(receipt.Key, receipt.Value);
            }
        }

        public static InventoryGunPendingDeliveryState Empty
        {
            get
            {
                return new InventoryGunPendingDeliveryState(
                    DefaultCapacity,
                    new InventoryGunPendingDeliveryEntry[0],
                    new Dictionary<string, string>(StringComparer.Ordinal));
            }
        }

        /// <summary>Maximum number of actual pending delivery entries.</summary>
        public int Capacity { get; }
        public int PendingCount { get { return pending.Count; } }
        public int DeliveredReceiptCount { get { return deliveredByIdentity.Count; } }
        public int RetainedCount { get { return PendingCount + DeliveredReceiptCount; } }
        public IReadOnlyList<InventoryGunPendingDeliveryEntry> PendingEntries
        {
            get { return pending; }
        }

        public bool TryPeekDue(
            long simulationTick,
            out InventoryGunPendingDeliveryEntry entry)
        {
            entry = null;
            if (simulationTick < 0L || pending.Count == 0)
            {
                return false;
            }
            if (pending[0].ScheduledTick > simulationTick)
            {
                return false;
            }
            entry = pending[0];
            return true;
        }

        public InventoryGunPendingAdmissionResult Admit(
            IEnumerable<InventoryGunPendingDeliveryEntry> entries)
        {
            if (entries == null)
            {
                return RejectAdmission(
                    InventoryGunPendingAdmissionStatus.InvalidEntry,
                    "gun-live-pending-entries-null");
            }

            var nextPending = new List<InventoryGunPendingDeliveryEntry>(pending);
            var nextPendingByIdentity = new Dictionary<
                string,
                InventoryGunPendingDeliveryEntry>(
                    pendingByIdentity,
                    StringComparer.Ordinal);
            int addedCount = 0;
            bool sawExactDuplicate = false;

            foreach (InventoryGunPendingDeliveryEntry entry in entries)
            {
                if (entry == null || !entry.HasValidFingerprint())
                {
                    return RejectAdmission(
                        InventoryGunPendingAdmissionStatus.InvalidEntry,
                        "gun-live-pending-entry-invalid");
                }

                InventoryGunPendingDeliveryEntry existingPending;
                if (nextPendingByIdentity.TryGetValue(
                        entry.IdentityKey,
                        out existingPending))
                {
                    if (!string.Equals(
                            existingPending.Fingerprint,
                            entry.Fingerprint,
                            StringComparison.Ordinal))
                    {
                        return RejectAdmission(
                            InventoryGunPendingAdmissionStatus.ConflictingDuplicate,
                            "gun-live-pending-conflicting-duplicate");
                    }
                    sawExactDuplicate = true;
                    continue;
                }

                string deliveredFingerprint;
                if (deliveredByIdentity.TryGetValue(
                        entry.IdentityKey,
                        out deliveredFingerprint))
                {
                    if (!string.Equals(
                            deliveredFingerprint,
                            entry.Fingerprint,
                            StringComparison.Ordinal))
                    {
                        return RejectAdmission(
                            InventoryGunPendingAdmissionStatus.ConflictingDuplicate,
                            "gun-live-delivered-conflicting-duplicate");
                    }
                    sawExactDuplicate = true;
                    continue;
                }

                if (nextPending.Count >= Capacity)
                {
                    return RejectAdmission(
                        InventoryGunPendingAdmissionStatus.CapacityExceeded,
                        "gun-live-pending-capacity-exceeded");
                }

                nextPending.Add(entry);
                nextPendingByIdentity.Add(entry.IdentityKey, entry);
                addedCount++;
            }

            if (addedCount == 0)
            {
                return new InventoryGunPendingAdmissionResult(
                    sawExactDuplicate
                        ? InventoryGunPendingAdmissionStatus.ExactDuplicate
                        : InventoryGunPendingAdmissionStatus.Accepted,
                    this,
                    0,
                    string.Empty);
            }

            return new InventoryGunPendingAdmissionResult(
                InventoryGunPendingAdmissionStatus.Accepted,
                new InventoryGunPendingDeliveryState(
                    Capacity,
                    nextPending,
                    deliveredByIdentity),
                addedCount,
                string.Empty);
        }

        public InventoryGunPendingDeliveryState MarkDelivered(
            InventoryGunPendingDeliveryEntry entry)
        {
            if (entry == null || !entry.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid pending delivery entry is required.",
                    nameof(entry));
            }

            InventoryGunPendingDeliveryEntry retained;
            if (!pendingByIdentity.TryGetValue(entry.IdentityKey, out retained)
                || !string.Equals(
                    retained.Fingerprint,
                    entry.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Only the exact retained pending delivery can be marked delivered.");
            }

            var nextPending = new List<InventoryGunPendingDeliveryEntry>(pending.Count - 1);
            for (int index = 0; index < pending.Count; index++)
            {
                if (!string.Equals(
                        pending[index].IdentityKey,
                        entry.IdentityKey,
                        StringComparison.Ordinal))
                {
                    nextPending.Add(pending[index]);
                }
            }

            var nextDelivered = new Dictionary<string, string>(
                deliveredByIdentity,
                StringComparer.Ordinal);
            string existing;
            if (nextDelivered.TryGetValue(entry.IdentityKey, out existing)
                && !string.Equals(existing, entry.Fingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A conflicting delivered fingerprint already exists.");
            }
            nextDelivered[entry.IdentityKey] = entry.Fingerprint;
            return new InventoryGunPendingDeliveryState(
                Capacity,
                nextPending,
                nextDelivered);
        }

        /// <summary>
        /// Removes only delivered receipts whose accepted schedules are no longer present in the
        /// canonical scheduler replay state. Pending work is always retained unchanged.
        /// </summary>
        public InventoryGunPendingDeliveryState PruneDeliveredReceipts(
            GunFiringSessionState retainedSchedulerState)
        {
            if (retainedSchedulerState == null
                || !retainedSchedulerState.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid scheduler state is required for receipt pruning.",
                    nameof(retainedSchedulerState));
            }
            if (deliveredByIdentity.Count == 0)
            {
                return this;
            }

            var requiredReceiptKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int replayIndex = 0;
                replayIndex < retainedSchedulerState.ReplayRecords.Count;
                replayIndex++)
            {
                GunFiringReplayRecord replay =
                    retainedSchedulerState.ReplayRecords[replayIndex];
                if (replay == null
                    || !replay.HasAcceptedSchedule
                    || replay.AcceptedSchedule == null)
                {
                    continue;
                }

                for (int emissionIndex = 0;
                    emissionIndex < replay.AcceptedSchedule.Emissions.Count;
                    emissionIndex++)
                {
                    GunFiringScheduler.AcceptedEmission emission =
                        replay.AcceptedSchedule.Emissions[emissionIndex];
                    if (emission == null)
                    {
                        throw new InvalidOperationException(
                            "A retained scheduler replay contains an invalid emission.");
                    }
                    requiredReceiptKeys.Add(
                        InventoryGunPendingDeliveryEntry.BuildIdentityKey(
                            emission.Command.ActorId,
                            emission.Command.LifecycleGeneration,
                            emission.EmissionFireOperationId));
                }
            }

            var nextDelivered = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> receipt in deliveredByIdentity)
            {
                if (requiredReceiptKeys.Contains(receipt.Key))
                {
                    nextDelivered.Add(receipt.Key, receipt.Value);
                }
            }

            return nextDelivered.Count == deliveredByIdentity.Count
                ? this
                : new InventoryGunPendingDeliveryState(
                    Capacity,
                    pending,
                    nextDelivered);
        }

        private InventoryGunPendingAdmissionResult RejectAdmission(
            InventoryGunPendingAdmissionStatus status,
            string code)
        {
            return new InventoryGunPendingAdmissionResult(
                status,
                this,
                0,
                code);
        }
    }
}

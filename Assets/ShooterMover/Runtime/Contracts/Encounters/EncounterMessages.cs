using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Mission;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Encounters
{
    public enum EncounterRetreatReason
    {
        TacticalWithdrawal = 1,
        RouteExit = 2,
        ObjectiveAbort = 3,
        RuntimeShutdown = 4,
    }

    public enum EncounterLockdownState
    {
        Released = 1,
        Engaged = 2,
    }

    public enum EncounterLockdownReason
    {
        EncounterRule = 1,
        ReinforcementArrival = 2,
        RouteControl = 3,
        VerificationFixture = 4,
    }

    public enum EncounterWithdrawalReason
    {
        Retreat = 1,
        RouteExit = 2,
        RuntimeUnload = 3,
    }

    public enum EncounterBudgetViolation
    {
        ConcurrentParticipantsExceeded = 1,
        PendingReinforcementEntriesExceeded = 2,
        CombatMessagesPerTickExceeded = 3,
        FrameTimeExceeded = 4,
    }

    /// <summary>
    /// Identifies one loaded encounter runtime separately from the durable
    /// encounter definition, mission run, and room projection identities.
    /// </summary>
    public sealed class EncounterRuntimeIdentity : IEquatable<EncounterRuntimeIdentity>
    {
        public EncounterRuntimeIdentity(
            StableId encounterId,
            StableId runtimeId,
            StableId runId,
            RoomProjectionIdentity room)
        {
            EncounterId = EncounterContractFormat.RequireNotNull(encounterId, nameof(encounterId));
            RuntimeId = EncounterContractFormat.RequireNotNull(runtimeId, nameof(runtimeId));
            RunId = EncounterContractFormat.RequireNotNull(runId, nameof(runId));
            Room = EncounterContractFormat.RequireNotNull(room, nameof(room));
        }

        public StableId EncounterId { get; }

        public StableId RuntimeId { get; }

        public StableId RunId { get; }

        public RoomProjectionIdentity Room { get; }

        public string ToCanonicalString()
        {
            return "encounter_id="
                + EncounterId
                + "\nruntime_id="
                + RuntimeId
                + "\nrun_id="
                + RunId
                + "\nroom:\n"
                + Room.ToCanonicalString();
        }

        public bool Equals(EncounterRuntimeIdentity other)
        {
            return !ReferenceEquals(other, null)
                && EncounterId.Equals(other.EncounterId)
                && RuntimeId.Equals(other.RuntimeId)
                && RunId.Equals(other.RunId)
                && Room.Equals(other.Room);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterRuntimeIdentity);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// One generic enemy/runtime participant entry. Role IDs keep ordinary
    /// enemies, elites, and future route encounters on the same envelope.
    /// </summary>
    public sealed class EncounterParticipantEntry : IEquatable<EncounterParticipantEntry>
    {
        public EncounterParticipantEntry(
            StableId entryId,
            StableId actorId,
            StableId roleId,
            int order)
        {
            if (order < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(order),
                    order,
                    "Encounter entry order cannot be negative.");
            }

            EntryId = EncounterContractFormat.RequireNotNull(entryId, nameof(entryId));
            ActorId = EncounterContractFormat.RequireNotNull(actorId, nameof(actorId));
            RoleId = EncounterContractFormat.RequireNotNull(roleId, nameof(roleId));
            Order = order;
        }

        public StableId EntryId { get; }

        public StableId ActorId { get; }

        public StableId RoleId { get; }

        public int Order { get; }

        public string ToCanonicalString()
        {
            return "order="
                + Order.ToString(CultureInfo.InvariantCulture)
                + "\nentry_id="
                + EntryId
                + "\nactor_id="
                + ActorId
                + "\nrole_id="
                + RoleId;
        }

        public bool Equals(EncounterParticipantEntry other)
        {
            return !ReferenceEquals(other, null)
                && EntryId.Equals(other.EntryId)
                && ActorId.Equals(other.ActorId)
                && RoleId.Equals(other.RoleId)
                && Order == other.Order;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterParticipantEntry);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// Declarative verification budget. It does not change encounter behavior;
    /// runtime and evidence tooling only compare observations against it.
    /// </summary>
    public sealed class EncounterPerformanceBudget : IEquatable<EncounterPerformanceBudget>
    {
        public EncounterPerformanceBudget(
            int maximumConcurrentParticipants,
            int maximumPendingReinforcementEntries,
            int maximumCombatMessagesPerTick,
            double maximumFrameTimeMilliseconds)
        {
            if (maximumConcurrentParticipants <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumConcurrentParticipants),
                    maximumConcurrentParticipants,
                    "Maximum concurrent participants must be positive.");
            }

            if (maximumPendingReinforcementEntries < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumPendingReinforcementEntries),
                    maximumPendingReinforcementEntries,
                    "Maximum pending reinforcement entries cannot be negative.");
            }

            if (maximumCombatMessagesPerTick <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumCombatMessagesPerTick),
                    maximumCombatMessagesPerTick,
                    "Maximum combat messages per tick must be positive.");
            }

            EncounterContractFormat.RequireFinitePositive(
                maximumFrameTimeMilliseconds,
                nameof(maximumFrameTimeMilliseconds));

            MaximumConcurrentParticipants = maximumConcurrentParticipants;
            MaximumPendingReinforcementEntries = maximumPendingReinforcementEntries;
            MaximumCombatMessagesPerTick = maximumCombatMessagesPerTick;
            MaximumFrameTimeMilliseconds = maximumFrameTimeMilliseconds;
        }

        public int MaximumConcurrentParticipants { get; }

        public int MaximumPendingReinforcementEntries { get; }

        public int MaximumCombatMessagesPerTick { get; }

        public double MaximumFrameTimeMilliseconds { get; }

        public string ToCanonicalString()
        {
            return "maximum_concurrent_participants="
                + MaximumConcurrentParticipants.ToString(CultureInfo.InvariantCulture)
                + "\nmaximum_pending_reinforcement_entries="
                + MaximumPendingReinforcementEntries.ToString(CultureInfo.InvariantCulture)
                + "\nmaximum_combat_messages_per_tick="
                + MaximumCombatMessagesPerTick.ToString(CultureInfo.InvariantCulture)
                + "\nmaximum_frame_time_ms="
                + MaximumFrameTimeMilliseconds.ToString("R", CultureInfo.InvariantCulture);
        }

        public bool Equals(EncounterPerformanceBudget other)
        {
            return !ReferenceEquals(other, null)
                && MaximumConcurrentParticipants == other.MaximumConcurrentParticipants
                && MaximumPendingReinforcementEntries
                    == other.MaximumPendingReinforcementEntries
                && MaximumCombatMessagesPerTick == other.MaximumCombatMessagesPerTick
                && MaximumFrameTimeMilliseconds == other.MaximumFrameTimeMilliseconds;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterPerformanceBudget);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class EncounterBudgetSample : IEquatable<EncounterBudgetSample>
    {
        public EncounterBudgetSample(
            EncounterRuntimeIdentity encounter,
            StableId sampleId,
            int concurrentParticipants,
            int pendingReinforcementEntries,
            int combatMessagesThisTick,
            double frameTimeMilliseconds)
        {
            Encounter = EncounterContractFormat.RequireNotNull(encounter, nameof(encounter));
            SampleId = EncounterContractFormat.RequireNotNull(sampleId, nameof(sampleId));
            ConcurrentParticipants = EncounterContractFormat.RequireNonNegative(
                concurrentParticipants,
                nameof(concurrentParticipants));
            PendingReinforcementEntries = EncounterContractFormat.RequireNonNegative(
                pendingReinforcementEntries,
                nameof(pendingReinforcementEntries));
            CombatMessagesThisTick = EncounterContractFormat.RequireNonNegative(
                combatMessagesThisTick,
                nameof(combatMessagesThisTick));
            EncounterContractFormat.RequireFiniteNonNegative(
                frameTimeMilliseconds,
                nameof(frameTimeMilliseconds));
            FrameTimeMilliseconds = frameTimeMilliseconds;
        }

        public EncounterRuntimeIdentity Encounter { get; }

        public StableId SampleId { get; }

        public int ConcurrentParticipants { get; }

        public int PendingReinforcementEntries { get; }

        public int CombatMessagesThisTick { get; }

        public double FrameTimeMilliseconds { get; }

        public string ToCanonicalString()
        {
            return Encounter.ToCanonicalString()
                + "\nsample_id="
                + SampleId
                + "\nconcurrent_participants="
                + ConcurrentParticipants.ToString(CultureInfo.InvariantCulture)
                + "\npending_reinforcement_entries="
                + PendingReinforcementEntries.ToString(CultureInfo.InvariantCulture)
                + "\ncombat_messages_this_tick="
                + CombatMessagesThisTick.ToString(CultureInfo.InvariantCulture)
                + "\nframe_time_ms="
                + FrameTimeMilliseconds.ToString("R", CultureInfo.InvariantCulture);
        }

        public bool Equals(EncounterBudgetSample other)
        {
            return !ReferenceEquals(other, null)
                && Encounter.Equals(other.Encounter)
                && SampleId.Equals(other.SampleId)
                && ConcurrentParticipants == other.ConcurrentParticipants
                && PendingReinforcementEntries == other.PendingReinforcementEntries
                && CombatMessagesThisTick == other.CombatMessagesThisTick
                && FrameTimeMilliseconds == other.FrameTimeMilliseconds;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterBudgetSample);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class EncounterBudgetEvaluation
    {
        private readonly ReadOnlyCollection<EncounterBudgetViolation> violations;

        private EncounterBudgetEvaluation(
            EncounterPerformanceBudget budget,
            EncounterBudgetSample sample,
            IList<EncounterBudgetViolation> violations)
        {
            Budget = EncounterContractFormat.RequireNotNull(budget, nameof(budget));
            Sample = EncounterContractFormat.RequireNotNull(sample, nameof(sample));
            this.violations = new ReadOnlyCollection<EncounterBudgetViolation>(
                new List<EncounterBudgetViolation>(violations));
        }

        public EncounterPerformanceBudget Budget { get; }

        public EncounterBudgetSample Sample { get; }

        public IReadOnlyList<EncounterBudgetViolation> Violations
        {
            get { return violations; }
        }

        public bool IsWithinBudget
        {
            get { return violations.Count == 0; }
        }

        public static EncounterBudgetEvaluation Evaluate(
            EncounterPerformanceBudget budget,
            EncounterBudgetSample sample)
        {
            EncounterPerformanceBudget validatedBudget =
                EncounterContractFormat.RequireNotNull(budget, nameof(budget));
            EncounterBudgetSample validatedSample =
                EncounterContractFormat.RequireNotNull(sample, nameof(sample));
            List<EncounterBudgetViolation> found = new List<EncounterBudgetViolation>();

            if (validatedSample.ConcurrentParticipants
                > validatedBudget.MaximumConcurrentParticipants)
            {
                found.Add(EncounterBudgetViolation.ConcurrentParticipantsExceeded);
            }

            if (validatedSample.PendingReinforcementEntries
                > validatedBudget.MaximumPendingReinforcementEntries)
            {
                found.Add(EncounterBudgetViolation.PendingReinforcementEntriesExceeded);
            }

            if (validatedSample.CombatMessagesThisTick
                > validatedBudget.MaximumCombatMessagesPerTick)
            {
                found.Add(EncounterBudgetViolation.CombatMessagesPerTickExceeded);
            }

            if (validatedSample.FrameTimeMilliseconds
                > validatedBudget.MaximumFrameTimeMilliseconds)
            {
                found.Add(EncounterBudgetViolation.FrameTimeExceeded);
            }

            return new EncounterBudgetEvaluation(validatedBudget, validatedSample, found);
        }
    }

    public sealed class EncounterStartMessage : IEquatable<EncounterStartMessage>
    {
        private readonly ReadOnlyCollection<EncounterParticipantEntry> entries;

        public EncounterStartMessage(
            EncounterRuntimeIdentity encounter,
            StableId messageId,
            EncounterPerformanceBudget budget,
            IEnumerable<EncounterParticipantEntry> entries)
        {
            Encounter = EncounterContractFormat.RequireNotNull(encounter, nameof(encounter));
            MessageId = EncounterContractFormat.RequireNotNull(messageId, nameof(messageId));
            Budget = EncounterContractFormat.RequireNotNull(budget, nameof(budget));
            this.entries = EncounterContractFormat.CopyOrderedEntries(entries, nameof(entries));

            if (this.entries.Count == 0)
            {
                throw new ArgumentException(
                    "An encounter start requires at least one participant entry.",
                    nameof(entries));
            }

            if (this.entries.Count > Budget.MaximumConcurrentParticipants)
            {
                throw new ArgumentException(
                    "Initial participants exceed the encounter concurrent-participant budget.",
                    nameof(entries));
            }
        }

        public EncounterRuntimeIdentity Encounter { get; }

        public StableId MessageId { get; }

        public EncounterPerformanceBudget Budget { get; }

        public IReadOnlyList<EncounterParticipantEntry> Entries
        {
            get { return entries; }
        }

        public string ToCanonicalString()
        {
            return Encounter.ToCanonicalString()
                + "\nmessage_id="
                + MessageId
                + "\nbudget:\n"
                + Budget.ToCanonicalString()
                + "\nentries:\n"
                + EncounterContractFormat.EntriesCanonicalString(entries);
        }

        public bool Equals(EncounterStartMessage other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterStartMessage);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class EncounterReinforcementMessage :
        IEquatable<EncounterReinforcementMessage>
    {
        private readonly ReadOnlyCollection<EncounterParticipantEntry> entries;

        public EncounterReinforcementMessage(
            EncounterRuntimeIdentity encounter,
            StableId messageId,
            long reinforcementIndex,
            IEnumerable<EncounterParticipantEntry> entries)
        {
            if (reinforcementIndex < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reinforcementIndex),
                    reinforcementIndex,
                    "Reinforcement index cannot be negative.");
            }

            Encounter = EncounterContractFormat.RequireNotNull(encounter, nameof(encounter));
            MessageId = EncounterContractFormat.RequireNotNull(messageId, nameof(messageId));
            ReinforcementIndex = reinforcementIndex;
            this.entries = EncounterContractFormat.CopyOrderedEntries(entries, nameof(entries));

            if (this.entries.Count == 0)
            {
                throw new ArgumentException(
                    "A reinforcement message requires at least one participant entry.",
                    nameof(entries));
            }
        }

        public EncounterRuntimeIdentity Encounter { get; }

        public StableId MessageId { get; }

        public long ReinforcementIndex { get; }

        public IReadOnlyList<EncounterParticipantEntry> Entries
        {
            get { return entries; }
        }

        public string ToCanonicalString()
        {
            return Encounter.ToCanonicalString()
                + "\nmessage_id="
                + MessageId
                + "\nreinforcement_index="
                + ReinforcementIndex.ToString(CultureInfo.InvariantCulture)
                + "\nentries:\n"
                + EncounterContractFormat.EntriesCanonicalString(entries);
        }

        public bool Equals(EncounterReinforcementMessage other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterReinforcementMessage);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class EncounterRetreatMessage : IEquatable<EncounterRetreatMessage>
    {
        public EncounterRetreatMessage(
            EncounterRuntimeIdentity encounter,
            StableId messageId,
            StableId sourceId,
            EncounterRetreatReason reason)
        {
            EncounterContractFormat.RequireKnownRetreatReason(reason);
            Encounter = EncounterContractFormat.RequireNotNull(encounter, nameof(encounter));
            MessageId = EncounterContractFormat.RequireNotNull(messageId, nameof(messageId));
            SourceId = EncounterContractFormat.RequireNotNull(sourceId, nameof(sourceId));
            Reason = reason;
        }

        public EncounterRuntimeIdentity Encounter { get; }

        public StableId MessageId { get; }

        public StableId SourceId { get; }

        public EncounterRetreatReason Reason { get; }

        public string ToCanonicalString()
        {
            return Encounter.ToCanonicalString()
                + "\nmessage_id="
                + MessageId
                + "\nsource_id="
                + SourceId
                + "\nreason="
                + ((int)Reason).ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(EncounterRetreatMessage other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterRetreatMessage);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class EncounterLockdownMessage : IEquatable<EncounterLockdownMessage>
    {
        public EncounterLockdownMessage(
            EncounterRuntimeIdentity encounter,
            StableId messageId,
            EncounterLockdownState state,
            EncounterLockdownReason reason)
        {
            EncounterContractFormat.RequireKnownLockdownState(state);
            EncounterContractFormat.RequireKnownLockdownReason(reason);
            Encounter = EncounterContractFormat.RequireNotNull(encounter, nameof(encounter));
            MessageId = EncounterContractFormat.RequireNotNull(messageId, nameof(messageId));
            State = state;
            Reason = reason;
        }

        public EncounterRuntimeIdentity Encounter { get; }

        public StableId MessageId { get; }

        public EncounterLockdownState State { get; }

        public EncounterLockdownReason Reason { get; }

        public string ToCanonicalString()
        {
            return Encounter.ToCanonicalString()
                + "\nmessage_id="
                + MessageId
                + "\nstate="
                + ((int)State).ToString(CultureInfo.InvariantCulture)
                + "\nreason="
                + ((int)Reason).ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(EncounterLockdownMessage other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterLockdownMessage);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class EncounterWithdrawalMessage : IEquatable<EncounterWithdrawalMessage>
    {
        public EncounterWithdrawalMessage(
            EncounterRuntimeIdentity encounter,
            StableId messageId,
            StableId actorId,
            EncounterWithdrawalReason reason)
        {
            EncounterContractFormat.RequireKnownWithdrawalReason(reason);
            Encounter = EncounterContractFormat.RequireNotNull(encounter, nameof(encounter));
            MessageId = EncounterContractFormat.RequireNotNull(messageId, nameof(messageId));
            ActorId = EncounterContractFormat.RequireNotNull(actorId, nameof(actorId));
            Reason = reason;
        }

        public EncounterRuntimeIdentity Encounter { get; }

        public StableId MessageId { get; }

        public StableId ActorId { get; }

        public EncounterWithdrawalReason Reason { get; }

        public string ToCanonicalString()
        {
            return Encounter.ToCanonicalString()
                + "\nmessage_id="
                + MessageId
                + "\nactor_id="
                + ActorId
                + "\nreason="
                + ((int)Reason).ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(EncounterWithdrawalMessage other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterWithdrawalMessage);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    /// <summary>
    /// Converts one Combat Messages v1 terminal vital fact into encounter-local
    /// participant resolution without introducing a second damage DTO.
    /// </summary>
    public sealed class EncounterCombatResolutionMessage :
        IEquatable<EncounterCombatResolutionMessage>
    {
        public EncounterCombatResolutionMessage(
            EncounterRuntimeIdentity encounter,
            VitalMessage vital)
        {
            Encounter = EncounterContractFormat.RequireNotNull(encounter, nameof(encounter));
            Vital = EncounterContractFormat.RequireNotNull(vital, nameof(vital));

            if (vital.Result != VitalResult.Destroyed || !vital.State.IsDestroyed)
            {
                throw new ArgumentException(
                    "Encounter combat resolution requires a destroyed Combat Messages v1 vital fact.",
                    nameof(vital));
            }
        }

        public EncounterRuntimeIdentity Encounter { get; }

        public VitalMessage Vital { get; }

        public StableId ActorId
        {
            get { return Vital.TargetId; }
        }

        public string ToCanonicalString()
        {
            return Encounter.ToCanonicalString()
                + "\ncombat_event_id="
                + Vital.EventId
                + "\nsource_id="
                + Vital.SourceId
                + "\ntarget_id="
                + Vital.TargetId
                + "\nchannel="
                + ((int)Vital.Channel).ToString(CultureInfo.InvariantCulture)
                + "\nresult="
                + ((int)Vital.Result).ToString(CultureInfo.InvariantCulture)
                + "\nhealth="
                + Vital.State.Health.ToString("R", CultureInfo.InvariantCulture)
                + "\nmaximum_health="
                + Vital.State.MaximumHealth.ToString("R", CultureInfo.InvariantCulture)
                + "\nshield="
                + Vital.State.Shield.ToString("R", CultureInfo.InvariantCulture)
                + "\nmaximum_shield="
                + Vital.State.MaximumShield.ToString("R", CultureInfo.InvariantCulture);
        }

        public bool Equals(EncounterCombatResolutionMessage other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterCombatResolutionMessage);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    /// <summary>
    /// Wraps the durable Mission Messages v1 room-cleared event that records
    /// encounter completion. The encounter runtime itself remains non-durable.
    /// </summary>
    public sealed class EncounterCompletionMessage :
        IEquatable<EncounterCompletionMessage>
    {
        public EncounterCompletionMessage(
            EncounterRuntimeIdentity encounter,
            MissionEventEnvelope durableEvent)
        {
            Encounter = EncounterContractFormat.RequireNotNull(encounter, nameof(encounter));
            DurableEvent = EncounterContractFormat.RequireNotNull(
                durableEvent,
                nameof(durableEvent));

            RoomClearedEvent roomCleared = durableEvent.Payload as RoomClearedEvent;
            if (roomCleared == null)
            {
                throw new ArgumentException(
                    "Encounter completion requires a Mission Messages v1 RoomCleared event.",
                    nameof(durableEvent));
            }

            if (!durableEvent.RunId.Equals(encounter.RunId)
                || !roomCleared.RoomId.Equals(encounter.Room.RoomId)
                || !roomCleared.EncounterId.Equals(encounter.EncounterId))
            {
                throw new ArgumentException(
                    "The durable room-cleared event must match the encounter run, room, and encounter IDs.",
                    nameof(durableEvent));
            }
        }

        public EncounterRuntimeIdentity Encounter { get; }

        public MissionEventEnvelope DurableEvent { get; }

        public string ToCanonicalString()
        {
            return Encounter.ToCanonicalString()
                + "\ndurable_event:\n"
                + DurableEvent.ToCanonicalString();
        }

        public bool Equals(EncounterCompletionMessage other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterCompletionMessage);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    internal static class EncounterContractFormat
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static T RequireNotNull<T>(T value, string parameterName)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        public static int RequireNonNegative(int value, string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value cannot be negative.");
            }

            return value;
        }

        public static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and positive.");
            }
        }

        public static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and non-negative.");
            }
        }

        public static ReadOnlyCollection<EncounterParticipantEntry> CopyOrderedEntries(
            IEnumerable<EncounterParticipantEntry> source,
            string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            List<EncounterParticipantEntry> entries =
                new List<EncounterParticipantEntry>(source);
            entries.Sort(CompareEntries);

            HashSet<StableId> entryIds = new HashSet<StableId>();
            HashSet<StableId> actorIds = new HashSet<StableId>();

            for (int index = 0; index < entries.Count; index++)
            {
                EncounterParticipantEntry entry = entries[index];
                if (entry == null)
                {
                    throw new ArgumentException(
                        "Encounter entry collections cannot contain null.",
                        parameterName);
                }

                if (entry.Order != index)
                {
                    throw new ArgumentException(
                        "Encounter entry order must be unique and contiguous from zero.",
                        parameterName);
                }

                if (!entryIds.Add(entry.EntryId))
                {
                    throw new ArgumentException(
                        "Encounter entry IDs must be unique within a message.",
                        parameterName);
                }

                if (!actorIds.Add(entry.ActorId))
                {
                    throw new ArgumentException(
                        "Encounter actor IDs must be unique within a message.",
                        parameterName);
                }
            }

            return new ReadOnlyCollection<EncounterParticipantEntry>(entries);
        }

        public static string EntriesCanonicalString(
            IEnumerable<EncounterParticipantEntry> entries)
        {
            StringBuilder builder = new StringBuilder();
            bool first = true;
            foreach (EncounterParticipantEntry entry in entries)
            {
                if (!first)
                {
                    builder.Append('\n');
                }

                builder.Append("entry:\n");
                builder.Append(entry.ToCanonicalString());
                first = false;
            }

            return builder.ToString();
        }

        public static void RequireKnownRetreatReason(EncounterRetreatReason reason)
        {
            if (reason != EncounterRetreatReason.TacticalWithdrawal
                && reason != EncounterRetreatReason.RouteExit
                && reason != EncounterRetreatReason.ObjectiveAbort
                && reason != EncounterRetreatReason.RuntimeShutdown)
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }

        public static void RequireKnownLockdownState(EncounterLockdownState state)
        {
            if (state != EncounterLockdownState.Released
                && state != EncounterLockdownState.Engaged)
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        public static void RequireKnownLockdownReason(EncounterLockdownReason reason)
        {
            if (reason != EncounterLockdownReason.EncounterRule
                && reason != EncounterLockdownReason.ReinforcementArrival
                && reason != EncounterLockdownReason.RouteControl
                && reason != EncounterLockdownReason.VerificationFixture)
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }

        public static void RequireKnownWithdrawalReason(EncounterWithdrawalReason reason)
        {
            if (reason != EncounterWithdrawalReason.Retreat
                && reason != EncounterWithdrawalReason.RouteExit
                && reason != EncounterWithdrawalReason.RuntimeUnload)
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }

        public static int DeterministicHash(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }

        private static int CompareEntries(
            EncounterParticipantEntry left,
            EncounterParticipantEntry right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (ReferenceEquals(left, null))
            {
                return -1;
            }

            if (ReferenceEquals(right, null))
            {
                return 1;
            }

            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }

            return left.EntryId.CompareTo(right.EntryId);
        }
    }
}

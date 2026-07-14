using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Encounters
{
    public enum EncounterLifecyclePhase
    {
        Ready = 0,
        Active = 1,
        Retreating = 2,
        Completed = 3,
    }

    public enum EncounterLifecycleOperation
    {
        Start = 1,
        Reinforcement = 2,
        Retreat = 3,
        Lockdown = 4,
        Withdrawal = 5,
        CombatResolution = 6,
        Completion = 7,
    }

    public enum EncounterTransitionKind
    {
        NoChange = 1,
        Applied = 2,
        Rejected = 3,
    }

    public enum EncounterTransitionRejection
    {
        None = 0,
        EncounterMismatch = 1,
        NotStarted = 2,
        AlreadyStarted = 3,
        AlreadyCompleted = 4,
        ReinforcementOutOfOrder = 5,
        ReinforcementConflict = 6,
        RetreatInProgress = 7,
        RetreatAlreadyStarted = 8,
        LockdownActive = 9,
        UnknownParticipant = 10,
        ParticipantAlreadyResolved = 11,
        DuplicateParticipant = 12,
        BudgetExceeded = 13,
        ParticipantsRemain = 14,
    }

    public enum EncounterActorResolutionKind
    {
        Withdrawal = 1,
        Destroyed = 2,
    }

    public sealed class EncounterActorResolution : IEquatable<EncounterActorResolution>
    {
        private EncounterActorResolution(
            StableId actorId,
            EncounterActorResolutionKind kind,
            EncounterWithdrawalMessage withdrawal,
            EncounterCombatResolutionMessage combat)
        {
            ActorId = EncounterContractFormat.RequireNotNull(actorId, nameof(actorId));
            Kind = kind;
            Withdrawal = withdrawal;
            Combat = combat;

            if (kind == EncounterActorResolutionKind.Withdrawal)
            {
                if (withdrawal == null || combat != null)
                {
                    throw new ArgumentException(
                        "Withdrawal resolution requires only a withdrawal message.");
                }
            }
            else if (kind == EncounterActorResolutionKind.Destroyed)
            {
                if (combat == null || withdrawal != null)
                {
                    throw new ArgumentException(
                        "Destroyed resolution requires only a combat resolution message.");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public StableId ActorId { get; }

        public EncounterActorResolutionKind Kind { get; }

        public EncounterWithdrawalMessage Withdrawal { get; }

        public EncounterCombatResolutionMessage Combat { get; }

        public string ToCanonicalString()
        {
            if (Kind == EncounterActorResolutionKind.Withdrawal)
            {
                return "resolution_kind=withdrawal\n" + Withdrawal.ToCanonicalString();
            }

            return "resolution_kind=destroyed\n" + Combat.ToCanonicalString();
        }

        public bool Equals(EncounterActorResolution other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EncounterActorResolution);
        }

        public override int GetHashCode()
        {
            return EncounterContractFormat.DeterministicHash(ToCanonicalString());
        }

        internal static EncounterActorResolution FromWithdrawal(
            EncounterWithdrawalMessage message)
        {
            EncounterWithdrawalMessage validated =
                EncounterContractFormat.RequireNotNull(message, nameof(message));
            return new EncounterActorResolution(
                validated.ActorId,
                EncounterActorResolutionKind.Withdrawal,
                validated,
                null);
        }

        internal static EncounterActorResolution FromCombat(
            EncounterCombatResolutionMessage message)
        {
            EncounterCombatResolutionMessage validated =
                EncounterContractFormat.RequireNotNull(message, nameof(message));
            return new EncounterActorResolution(
                validated.ActorId,
                EncounterActorResolutionKind.Destroyed,
                null,
                validated);
        }
    }

    /// <summary>
    /// Immutable runtime-local encounter lifecycle. Durable room completion is
    /// represented only by EncounterCompletionMessage's MissionEventEnvelope.
    /// </summary>
    public sealed class EncounterLifecycle
    {
        private readonly ReadOnlyCollection<EncounterReinforcementMessage> reinforcements;
        private readonly ReadOnlyCollection<EncounterActorResolution> resolutions;

        private EncounterLifecycle(
            EncounterRuntimeIdentity identity,
            EncounterLifecyclePhase phase,
            EncounterStartMessage start,
            IList<EncounterReinforcementMessage> reinforcements,
            EncounterRetreatMessage retreat,
            EncounterLockdownState lockdownState,
            EncounterLockdownMessage lastLockdown,
            IList<EncounterActorResolution> resolutions,
            EncounterCompletionMessage completion)
        {
            Identity = EncounterContractFormat.RequireNotNull(identity, nameof(identity));
            Phase = phase;
            StartMessage = start;
            this.reinforcements = new ReadOnlyCollection<EncounterReinforcementMessage>(
                new List<EncounterReinforcementMessage>(reinforcements));
            RetreatMessage = retreat;
            LockdownState = lockdownState;
            LastLockdownMessage = lastLockdown;
            this.resolutions = new ReadOnlyCollection<EncounterActorResolution>(
                new List<EncounterActorResolution>(resolutions));
            CompletionMessage = completion;
            ValidateState();
        }

        public EncounterRuntimeIdentity Identity { get; }

        public EncounterLifecyclePhase Phase { get; }

        public EncounterStartMessage StartMessage { get; }

        public IReadOnlyList<EncounterReinforcementMessage> Reinforcements
        {
            get { return reinforcements; }
        }

        public EncounterRetreatMessage RetreatMessage { get; }

        public EncounterLockdownState LockdownState { get; }

        public EncounterLockdownMessage LastLockdownMessage { get; }

        public IReadOnlyList<EncounterActorResolution> Resolutions
        {
            get { return resolutions; }
        }

        public EncounterCompletionMessage CompletionMessage { get; }

        public bool IsStarted
        {
            get { return StartMessage != null; }
        }

        public bool IsCompleted
        {
            get { return Phase == EncounterLifecyclePhase.Completed; }
        }

        public int ParticipantCount
        {
            get
            {
                if (StartMessage == null)
                {
                    return 0;
                }

                int count = StartMessage.Entries.Count;
                foreach (EncounterReinforcementMessage reinforcement in reinforcements)
                {
                    count += reinforcement.Entries.Count;
                }

                return count;
            }
        }

        public int ActiveParticipantCount
        {
            get { return ParticipantCount - resolutions.Count; }
        }

        public static EncounterLifecycle Create(EncounterRuntimeIdentity identity)
        {
            return new EncounterLifecycle(
                EncounterContractFormat.RequireNotNull(identity, nameof(identity)),
                EncounterLifecyclePhase.Ready,
                null,
                new EncounterReinforcementMessage[0],
                null,
                EncounterLockdownState.Released,
                null,
                new EncounterActorResolution[0],
                null);
        }

        public EncounterLifecycleTransition Start(EncounterStartMessage message)
        {
            EncounterStartMessage validated =
                EncounterContractFormat.RequireNotNull(message, nameof(message));
            EncounterLifecycleTransition mismatch = RejectMismatch(
                EncounterLifecycleOperation.Start,
                validated.Encounter);
            if (mismatch != null)
            {
                return mismatch;
            }

            if (StartMessage != null)
            {
                if (StartMessage.Equals(validated))
                {
                    return EncounterLifecycleTransition.NoChange(
                        EncounterLifecycleOperation.Start,
                        this);
                }

                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Start,
                    this,
                    EncounterTransitionRejection.AlreadyStarted);
            }

            EncounterLifecycle next = New(
                EncounterLifecyclePhase.Active,
                validated,
                reinforcements,
                null,
                EncounterLockdownState.Released,
                null,
                resolutions,
                null);
            return EncounterLifecycleTransition.Applied(
                EncounterLifecycleOperation.Start,
                this,
                next);
        }

        public EncounterLifecycleTransition AddReinforcement(
            EncounterReinforcementMessage message)
        {
            EncounterReinforcementMessage validated =
                EncounterContractFormat.RequireNotNull(message, nameof(message));
            EncounterLifecycleTransition mismatch = RejectMismatch(
                EncounterLifecycleOperation.Reinforcement,
                validated.Encounter);
            if (mismatch != null)
            {
                return mismatch;
            }

            if (!IsStarted)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Reinforcement,
                    this,
                    EncounterTransitionRejection.NotStarted);
            }

            if (IsCompleted)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Reinforcement,
                    this,
                    EncounterTransitionRejection.AlreadyCompleted);
            }

            if (Phase == EncounterLifecyclePhase.Retreating)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Reinforcement,
                    this,
                    EncounterTransitionRejection.RetreatInProgress);
            }

            if (validated.ReinforcementIndex < reinforcements.Count)
            {
                EncounterReinforcementMessage existing =
                    reinforcements[(int)validated.ReinforcementIndex];
                if (existing.Equals(validated))
                {
                    return EncounterLifecycleTransition.NoChange(
                        EncounterLifecycleOperation.Reinforcement,
                        this);
                }

                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Reinforcement,
                    this,
                    EncounterTransitionRejection.ReinforcementConflict);
            }

            if (validated.ReinforcementIndex != reinforcements.Count)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Reinforcement,
                    this,
                    EncounterTransitionRejection.ReinforcementOutOfOrder);
            }

            if (validated.Entries.Count
                > StartMessage.Budget.MaximumPendingReinforcementEntries
                || ActiveParticipantCount + validated.Entries.Count
                    > StartMessage.Budget.MaximumConcurrentParticipants)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Reinforcement,
                    this,
                    EncounterTransitionRejection.BudgetExceeded);
            }

            if (ContainsAnyParticipant(validated.Entries))
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Reinforcement,
                    this,
                    EncounterTransitionRejection.DuplicateParticipant);
            }

            List<EncounterReinforcementMessage> nextReinforcements =
                new List<EncounterReinforcementMessage>(reinforcements);
            nextReinforcements.Add(validated);
            EncounterLifecycle next = New(
                Phase,
                StartMessage,
                nextReinforcements,
                RetreatMessage,
                LockdownState,
                LastLockdownMessage,
                resolutions,
                null);
            return EncounterLifecycleTransition.Applied(
                EncounterLifecycleOperation.Reinforcement,
                this,
                next);
        }

        public EncounterLifecycleTransition BeginRetreat(EncounterRetreatMessage message)
        {
            EncounterRetreatMessage validated =
                EncounterContractFormat.RequireNotNull(message, nameof(message));
            EncounterLifecycleTransition mismatch = RejectMismatch(
                EncounterLifecycleOperation.Retreat,
                validated.Encounter);
            if (mismatch != null)
            {
                return mismatch;
            }

            if (!IsStarted)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Retreat,
                    this,
                    EncounterTransitionRejection.NotStarted);
            }

            if (IsCompleted)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Retreat,
                    this,
                    EncounterTransitionRejection.AlreadyCompleted);
            }

            if (RetreatMessage != null)
            {
                if (RetreatMessage.Equals(validated))
                {
                    return EncounterLifecycleTransition.NoChange(
                        EncounterLifecycleOperation.Retreat,
                        this);
                }

                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Retreat,
                    this,
                    EncounterTransitionRejection.RetreatAlreadyStarted);
            }

            if (LockdownState == EncounterLockdownState.Engaged)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Retreat,
                    this,
                    EncounterTransitionRejection.LockdownActive);
            }

            EncounterLifecycle next = New(
                EncounterLifecyclePhase.Retreating,
                StartMessage,
                reinforcements,
                validated,
                LockdownState,
                LastLockdownMessage,
                resolutions,
                null);
            return EncounterLifecycleTransition.Applied(
                EncounterLifecycleOperation.Retreat,
                this,
                next);
        }

        public EncounterLifecycleTransition ApplyLockdown(EncounterLockdownMessage message)
        {
            EncounterLockdownMessage validated =
                EncounterContractFormat.RequireNotNull(message, nameof(message));
            EncounterLifecycleTransition mismatch = RejectMismatch(
                EncounterLifecycleOperation.Lockdown,
                validated.Encounter);
            if (mismatch != null)
            {
                return mismatch;
            }

            if (!IsStarted)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Lockdown,
                    this,
                    EncounterTransitionRejection.NotStarted);
            }

            if (IsCompleted)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Lockdown,
                    this,
                    EncounterTransitionRejection.AlreadyCompleted);
            }

            if (LockdownState == validated.State)
            {
                return EncounterLifecycleTransition.NoChange(
                    EncounterLifecycleOperation.Lockdown,
                    this);
            }

            EncounterLifecycle next = New(
                Phase,
                StartMessage,
                reinforcements,
                RetreatMessage,
                validated.State,
                validated,
                resolutions,
                null);
            return EncounterLifecycleTransition.Applied(
                EncounterLifecycleOperation.Lockdown,
                this,
                next);
        }

        public EncounterLifecycleTransition RecordWithdrawal(
            EncounterWithdrawalMessage message)
        {
            EncounterWithdrawalMessage validated =
                EncounterContractFormat.RequireNotNull(message, nameof(message));
            EncounterLifecycleTransition mismatch = RejectMismatch(
                EncounterLifecycleOperation.Withdrawal,
                validated.Encounter);
            if (mismatch != null)
            {
                return mismatch;
            }

            EncounterActorResolution resolution =
                EncounterActorResolution.FromWithdrawal(validated);
            EncounterLifecycleTransition admission = ValidateResolutionAdmission(
                EncounterLifecycleOperation.Withdrawal,
                validated.ActorId,
                resolution,
                true);
            if (admission != null)
            {
                return admission;
            }

            return ApplyResolution(
                EncounterLifecycleOperation.Withdrawal,
                resolution);
        }

        public EncounterLifecycleTransition RecordCombatResolution(
            EncounterCombatResolutionMessage message)
        {
            EncounterCombatResolutionMessage validated =
                EncounterContractFormat.RequireNotNull(message, nameof(message));
            EncounterLifecycleTransition mismatch = RejectMismatch(
                EncounterLifecycleOperation.CombatResolution,
                validated.Encounter);
            if (mismatch != null)
            {
                return mismatch;
            }

            EncounterActorResolution resolution =
                EncounterActorResolution.FromCombat(validated);
            EncounterLifecycleTransition admission = ValidateResolutionAdmission(
                EncounterLifecycleOperation.CombatResolution,
                validated.ActorId,
                resolution,
                false);
            if (admission != null)
            {
                return admission;
            }

            return ApplyResolution(
                EncounterLifecycleOperation.CombatResolution,
                resolution);
        }

        public EncounterLifecycleTransition Complete(EncounterCompletionMessage message)
        {
            EncounterCompletionMessage validated =
                EncounterContractFormat.RequireNotNull(message, nameof(message));
            EncounterLifecycleTransition mismatch = RejectMismatch(
                EncounterLifecycleOperation.Completion,
                validated.Encounter);
            if (mismatch != null)
            {
                return mismatch;
            }

            if (IsCompleted)
            {
                if (CompletionMessage.Equals(validated))
                {
                    return EncounterLifecycleTransition.NoChange(
                        EncounterLifecycleOperation.Completion,
                        this);
                }

                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Completion,
                    this,
                    EncounterTransitionRejection.AlreadyCompleted);
            }

            if (!IsStarted)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Completion,
                    this,
                    EncounterTransitionRejection.NotStarted);
            }

            if (ActiveParticipantCount != 0)
            {
                return EncounterLifecycleTransition.Rejected(
                    EncounterLifecycleOperation.Completion,
                    this,
                    EncounterTransitionRejection.ParticipantsRemain);
            }

            EncounterLifecycle next = New(
                EncounterLifecyclePhase.Completed,
                StartMessage,
                reinforcements,
                RetreatMessage,
                LockdownState,
                LastLockdownMessage,
                resolutions,
                validated);
            return EncounterLifecycleTransition.Applied(
                EncounterLifecycleOperation.Completion,
                this,
                next);
        }

        private EncounterLifecycleTransition ValidateResolutionAdmission(
            EncounterLifecycleOperation operation,
            StableId actorId,
            EncounterActorResolution candidate,
            bool obeyLockdown)
        {
            if (!IsStarted)
            {
                return EncounterLifecycleTransition.Rejected(
                    operation,
                    this,
                    EncounterTransitionRejection.NotStarted);
            }

            if (IsCompleted)
            {
                return EncounterLifecycleTransition.Rejected(
                    operation,
                    this,
                    EncounterTransitionRejection.AlreadyCompleted);
            }

            if (obeyLockdown && LockdownState == EncounterLockdownState.Engaged)
            {
                return EncounterLifecycleTransition.Rejected(
                    operation,
                    this,
                    EncounterTransitionRejection.LockdownActive);
            }

            if (!ContainsParticipant(actorId))
            {
                return EncounterLifecycleTransition.Rejected(
                    operation,
                    this,
                    EncounterTransitionRejection.UnknownParticipant);
            }

            EncounterActorResolution existing = FindResolution(actorId);
            if (existing != null)
            {
                if (existing.Equals(candidate))
                {
                    return EncounterLifecycleTransition.NoChange(operation, this);
                }

                return EncounterLifecycleTransition.Rejected(
                    operation,
                    this,
                    EncounterTransitionRejection.ParticipantAlreadyResolved);
            }

            return null;
        }

        private EncounterLifecycleTransition ApplyResolution(
            EncounterLifecycleOperation operation,
            EncounterActorResolution resolution)
        {
            List<EncounterActorResolution> nextResolutions =
                new List<EncounterActorResolution>(resolutions);
            nextResolutions.Add(resolution);
            nextResolutions.Sort(
                delegate(EncounterActorResolution left, EncounterActorResolution right)
                {
                    return left.ActorId.CompareTo(right.ActorId);
                });

            EncounterLifecycle next = New(
                Phase,
                StartMessage,
                reinforcements,
                RetreatMessage,
                LockdownState,
                LastLockdownMessage,
                nextResolutions,
                null);
            return EncounterLifecycleTransition.Applied(operation, this, next);
        }

        private EncounterLifecycleTransition RejectMismatch(
            EncounterLifecycleOperation operation,
            EncounterRuntimeIdentity candidate)
        {
            if (!Identity.Equals(candidate))
            {
                return EncounterLifecycleTransition.Rejected(
                    operation,
                    this,
                    EncounterTransitionRejection.EncounterMismatch);
            }

            return null;
        }

        private bool ContainsAnyParticipant(
            IEnumerable<EncounterParticipantEntry> candidates)
        {
            foreach (EncounterParticipantEntry candidate in candidates)
            {
                if (ContainsParticipant(candidate.ActorId))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsParticipant(StableId actorId)
        {
            if (StartMessage == null)
            {
                return false;
            }

            foreach (EncounterParticipantEntry entry in StartMessage.Entries)
            {
                if (entry.ActorId.Equals(actorId))
                {
                    return true;
                }
            }

            foreach (EncounterReinforcementMessage reinforcement in reinforcements)
            {
                foreach (EncounterParticipantEntry entry in reinforcement.Entries)
                {
                    if (entry.ActorId.Equals(actorId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private EncounterActorResolution FindResolution(StableId actorId)
        {
            foreach (EncounterActorResolution resolution in resolutions)
            {
                if (resolution.ActorId.Equals(actorId))
                {
                    return resolution;
                }
            }

            return null;
        }

        private EncounterLifecycle New(
            EncounterLifecyclePhase phase,
            EncounterStartMessage start,
            IList<EncounterReinforcementMessage> nextReinforcements,
            EncounterRetreatMessage retreat,
            EncounterLockdownState lockdownState,
            EncounterLockdownMessage lastLockdown,
            IList<EncounterActorResolution> nextResolutions,
            EncounterCompletionMessage completion)
        {
            return new EncounterLifecycle(
                Identity,
                phase,
                start,
                nextReinforcements,
                retreat,
                lockdownState,
                lastLockdown,
                nextResolutions,
                completion);
        }

        private void ValidateState()
        {
            if (LockdownState != EncounterLockdownState.Released
                && LockdownState != EncounterLockdownState.Engaged)
            {
                throw new ArgumentOutOfRangeException(nameof(LockdownState));
            }

            if (Phase == EncounterLifecyclePhase.Ready)
            {
                if (StartMessage != null
                    || reinforcements.Count != 0
                    || RetreatMessage != null
                    || LastLockdownMessage != null
                    || resolutions.Count != 0
                    || CompletionMessage != null)
                {
                    throw new ArgumentException(
                        "A ready encounter cannot retain runtime lifecycle messages.");
                }

                return;
            }

            if (Phase != EncounterLifecyclePhase.Active
                && Phase != EncounterLifecyclePhase.Retreating
                && Phase != EncounterLifecyclePhase.Completed)
            {
                throw new ArgumentOutOfRangeException(nameof(Phase));
            }

            if (StartMessage == null)
            {
                throw new ArgumentNullException(nameof(StartMessage));
            }

            if (!Identity.Equals(StartMessage.Encounter))
            {
                throw new ArgumentException(
                    "The start message must target the lifecycle identity.");
            }

            if (Phase == EncounterLifecyclePhase.Retreating && RetreatMessage == null)
            {
                throw new ArgumentException(
                    "A retreating encounter requires a retreat message.");
            }

            if (Phase == EncounterLifecyclePhase.Completed)
            {
                if (CompletionMessage == null || ActiveParticipantCount != 0)
                {
                    throw new ArgumentException(
                        "A completed encounter requires one durable completion and no active participants.");
                }
            }
            else if (CompletionMessage != null)
            {
                throw new ArgumentException(
                    "Only a completed encounter may retain a completion message.");
            }
        }
    }

    public sealed class EncounterLifecycleTransition
    {
        private EncounterLifecycleTransition(
            EncounterLifecycleOperation operation,
            EncounterTransitionKind kind,
            EncounterTransitionRejection rejection,
            EncounterLifecycle current,
            EncounterLifecycle next)
        {
            if (kind != EncounterTransitionKind.NoChange
                && kind != EncounterTransitionKind.Applied
                && kind != EncounterTransitionKind.Rejected)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (kind == EncounterTransitionKind.Rejected
                && rejection == EncounterTransitionRejection.None)
            {
                throw new ArgumentException(
                    "Rejected encounter transitions require an explicit reason.",
                    nameof(rejection));
            }

            if (kind != EncounterTransitionKind.Rejected
                && rejection != EncounterTransitionRejection.None)
            {
                throw new ArgumentException(
                    "Only rejected encounter transitions may carry a reason.",
                    nameof(rejection));
            }

            Operation = operation;
            Kind = kind;
            Rejection = rejection;
            Current = EncounterContractFormat.RequireNotNull(current, nameof(current));
            Next = EncounterContractFormat.RequireNotNull(next, nameof(next));
        }

        public EncounterLifecycleOperation Operation { get; }

        public EncounterTransitionKind Kind { get; }

        public EncounterTransitionRejection Rejection { get; }

        public EncounterLifecycle Current { get; }

        public EncounterLifecycle Next { get; }

        public bool WasApplied
        {
            get { return Kind == EncounterTransitionKind.Applied; }
        }

        internal static EncounterLifecycleTransition NoChange(
            EncounterLifecycleOperation operation,
            EncounterLifecycle state)
        {
            return new EncounterLifecycleTransition(
                operation,
                EncounterTransitionKind.NoChange,
                EncounterTransitionRejection.None,
                state,
                state);
        }

        internal static EncounterLifecycleTransition Applied(
            EncounterLifecycleOperation operation,
            EncounterLifecycle current,
            EncounterLifecycle next)
        {
            return new EncounterLifecycleTransition(
                operation,
                EncounterTransitionKind.Applied,
                EncounterTransitionRejection.None,
                current,
                next);
        }

        internal static EncounterLifecycleTransition Rejected(
            EncounterLifecycleOperation operation,
            EncounterLifecycle state,
            EncounterTransitionRejection rejection)
        {
            return new EncounterLifecycleTransition(
                operation,
                EncounterTransitionKind.Rejected,
                rejection,
                state,
                state);
        }
    }
}

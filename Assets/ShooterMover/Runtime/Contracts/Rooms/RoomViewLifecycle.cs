using System;
using ShooterMover.Contracts.Mission;

namespace ShooterMover.Contracts.Rooms
{
    public enum RoomViewLifecyclePhase
    {
        Unloaded = 0,
        Loaded = 1,
        Unloading = 2,
    }

    public enum RoomViewLifecycleOperation
    {
        Load = 1,
        Refresh = 2,
        Reload = 3,
        BeginUnload = 4,
        CompleteUnload = 5,
        ResumeInterruptedUnload = 6,
    }

    public enum RoomViewTransitionKind
    {
        NoChange = 1,
        Applied = 2,
        Rejected = 3,
    }

    public enum RoomViewTransitionRejection
    {
        None = 0,
        NotLoaded = 1,
        InvalidTransition = 2,
        DifferentRun = 3,
        StaleProjectionKey = 4,
    }

    /// <summary>
    /// Immutable lifecycle state for one room projection. It tracks presentation
    /// availability and the last authoritative projection key only; it contains
    /// no permanent room, reward, route, checkpoint or objective truth.
    /// </summary>
    public sealed class RoomViewLifecycle
    {
        private RoomViewLifecycle(
            RoomViewIdentity identity,
            RoomViewLifecyclePhase phase,
            RoomViewKey activeKey)
        {
            Identity = RoomContractFormat.RequireNotNull(identity, nameof(identity));

            if (phase == RoomViewLifecyclePhase.Unloaded)
            {
                if (activeKey != null)
                {
                    throw new ArgumentException(
                        "An unloaded room projection cannot retain an active projection key.",
                        nameof(activeKey));
                }
            }
            else if (phase == RoomViewLifecyclePhase.Loaded
                || phase == RoomViewLifecyclePhase.Unloading)
            {
                if (activeKey == null)
                {
                    throw new ArgumentNullException(nameof(activeKey));
                }

                EnsureKeyTargetsIdentity(identity, activeKey);
            }
            else
            {
                throw new ArgumentOutOfRangeException(
                    nameof(phase),
                    phase,
                    "Unknown room projection lifecycle phase.");
            }

            Phase = phase;
            ActiveKey = activeKey;
        }

        public RoomViewIdentity Identity { get; }

        public RoomViewLifecyclePhase Phase { get; }

        public RoomViewKey ActiveKey { get; }

        public bool IsLoaded
        {
            get { return Phase == RoomViewLifecyclePhase.Loaded; }
        }

        public static RoomViewLifecycle Create(RoomViewIdentity identity)
        {
            return new RoomViewLifecycle(
                RoomContractFormat.RequireNotNull(identity, nameof(identity)),
                RoomViewLifecyclePhase.Unloaded,
                null);
        }

        public RoomViewTransition Load(RoomViewKey key)
        {
            RoomViewKey validated = ValidateKey(key);

            if (Phase == RoomViewLifecyclePhase.Loaded)
            {
                if (ActiveKey.Equals(validated))
                {
                    return RoomViewTransition.NoChange(
                        RoomViewLifecycleOperation.Load,
                        this);
                }

                return RoomViewTransition.Rejected(
                    RoomViewLifecycleOperation.Load,
                    this,
                    RoomViewTransitionRejection.InvalidTransition);
            }

            if (Phase == RoomViewLifecyclePhase.Unloading)
            {
                return RoomViewTransition.Rejected(
                    RoomViewLifecycleOperation.Load,
                    this,
                    RoomViewTransitionRejection.InvalidTransition);
            }

            return RoomViewTransition.Applied(
                RoomViewLifecycleOperation.Load,
                this,
                NewLoaded(validated));
        }

        public RoomViewTransition Refresh(RoomViewKey key)
        {
            RoomViewKey validated = ValidateKey(key);

            if (Phase != RoomViewLifecyclePhase.Loaded)
            {
                return RoomViewTransition.Rejected(
                    RoomViewLifecycleOperation.Refresh,
                    this,
                    RoomViewTransitionRejection.NotLoaded);
            }

            if (!ActiveKey.RunId.Equals(validated.RunId))
            {
                return RoomViewTransition.Rejected(
                    RoomViewLifecycleOperation.Refresh,
                    this,
                    RoomViewTransitionRejection.DifferentRun);
            }

            MissionSequenceRelation relation = validated.Sequence.RelateTo(ActiveKey.Sequence);
            if (relation == MissionSequenceRelation.Stale)
            {
                return RoomViewTransition.Rejected(
                    RoomViewLifecycleOperation.Refresh,
                    this,
                    RoomViewTransitionRejection.StaleProjectionKey);
            }

            if (ActiveKey.Equals(validated))
            {
                return RoomViewTransition.NoChange(
                    RoomViewLifecycleOperation.Refresh,
                    this);
            }

            return RoomViewTransition.Applied(
                RoomViewLifecycleOperation.Refresh,
                this,
                NewLoaded(validated));
        }

        public RoomViewTransition Reload(RoomViewKey key)
        {
            RoomViewKey validated = ValidateKey(key);

            if (Phase == RoomViewLifecyclePhase.Loaded
                && ActiveKey.Equals(validated))
            {
                return RoomViewTransition.NoChange(
                    RoomViewLifecycleOperation.Reload,
                    this);
            }

            if (ActiveKey != null
                && ActiveKey.RunId.Equals(validated.RunId)
                && validated.Sequence.RelateTo(ActiveKey.Sequence)
                    == MissionSequenceRelation.Stale)
            {
                return RoomViewTransition.Rejected(
                    RoomViewLifecycleOperation.Reload,
                    this,
                    RoomViewTransitionRejection.StaleProjectionKey);
            }

            return RoomViewTransition.Applied(
                RoomViewLifecycleOperation.Reload,
                this,
                NewLoaded(validated));
        }

        public RoomViewTransition BeginUnload()
        {
            if (Phase == RoomViewLifecyclePhase.Unloaded
                || Phase == RoomViewLifecyclePhase.Unloading)
            {
                return RoomViewTransition.NoChange(
                    RoomViewLifecycleOperation.BeginUnload,
                    this);
            }

            RoomViewLifecycle next = new RoomViewLifecycle(
                Identity,
                RoomViewLifecyclePhase.Unloading,
                ActiveKey);
            return RoomViewTransition.Applied(
                RoomViewLifecycleOperation.BeginUnload,
                this,
                next);
        }

        public RoomViewTransition CompleteUnload()
        {
            if (Phase == RoomViewLifecyclePhase.Unloaded)
            {
                return RoomViewTransition.NoChange(
                    RoomViewLifecycleOperation.CompleteUnload,
                    this);
            }

            if (Phase != RoomViewLifecyclePhase.Unloading)
            {
                return RoomViewTransition.Rejected(
                    RoomViewLifecycleOperation.CompleteUnload,
                    this,
                    RoomViewTransitionRejection.InvalidTransition);
            }

            RoomViewLifecycle next = new RoomViewLifecycle(
                Identity,
                RoomViewLifecyclePhase.Unloaded,
                null);
            return RoomViewTransition.Applied(
                RoomViewLifecycleOperation.CompleteUnload,
                this,
                next);
        }

        public RoomViewTransition ResumeAfterInterruptedUnload()
        {
            if (Phase == RoomViewLifecyclePhase.Loaded)
            {
                return RoomViewTransition.NoChange(
                    RoomViewLifecycleOperation.ResumeInterruptedUnload,
                    this);
            }

            if (Phase == RoomViewLifecyclePhase.Unloaded)
            {
                return RoomViewTransition.Rejected(
                    RoomViewLifecycleOperation.ResumeInterruptedUnload,
                    this,
                    RoomViewTransitionRejection.NotLoaded);
            }

            return RoomViewTransition.Applied(
                RoomViewLifecycleOperation.ResumeInterruptedUnload,
                this,
                NewLoaded(ActiveKey));
        }

        private RoomViewKey ValidateKey(RoomViewKey key)
        {
            RoomViewKey validated = RoomContractFormat.RequireNotNull(key, nameof(key));
            EnsureKeyTargetsIdentity(Identity, validated);
            return validated;
        }

        private RoomViewLifecycle NewLoaded(RoomViewKey key)
        {
            return new RoomViewLifecycle(
                Identity,
                RoomViewLifecyclePhase.Loaded,
                key);
        }

        private static void EnsureKeyTargetsIdentity(
            RoomViewIdentity identity,
            RoomViewKey key)
        {
            if (!identity.RoomId.Equals(key.RoomId))
            {
                throw new ArgumentException(
                    "A room projection key must target the lifecycle identity's durable room ID.",
                    nameof(key));
            }
        }
    }

    /// <summary>
    /// Immutable functional transition. Repeating a transition that already
    /// reached its target returns NoChange and preserves the same state object.
    /// </summary>
    public sealed class RoomViewTransition
    {
        private RoomViewTransition(
            RoomViewLifecycleOperation operation,
            RoomViewTransitionKind kind,
            RoomViewTransitionRejection rejection,
            RoomViewLifecycle current,
            RoomViewLifecycle next)
        {
            if (kind != RoomViewTransitionKind.NoChange
                && kind != RoomViewTransitionKind.Applied
                && kind != RoomViewTransitionKind.Rejected)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (kind == RoomViewTransitionKind.Rejected
                && rejection == RoomViewTransitionRejection.None)
            {
                throw new ArgumentException(
                    "Rejected transitions require an explicit rejection reason.",
                    nameof(rejection));
            }

            if (kind != RoomViewTransitionKind.Rejected
                && rejection != RoomViewTransitionRejection.None)
            {
                throw new ArgumentException(
                    "Only rejected transitions may carry a rejection reason.",
                    nameof(rejection));
            }

            Operation = operation;
            Kind = kind;
            Rejection = rejection;
            Current = RoomContractFormat.RequireNotNull(current, nameof(current));
            Next = RoomContractFormat.RequireNotNull(next, nameof(next));
        }

        public RoomViewLifecycleOperation Operation { get; }

        public RoomViewTransitionKind Kind { get; }

        public RoomViewTransitionRejection Rejection { get; }

        public RoomViewLifecycle Current { get; }

        public RoomViewLifecycle Next { get; }

        public bool WasApplied
        {
            get { return Kind == RoomViewTransitionKind.Applied; }
        }

        internal static RoomViewTransition NoChange(
            RoomViewLifecycleOperation operation,
            RoomViewLifecycle state)
        {
            return new RoomViewTransition(
                operation,
                RoomViewTransitionKind.NoChange,
                RoomViewTransitionRejection.None,
                state,
                state);
        }

        internal static RoomViewTransition Applied(
            RoomViewLifecycleOperation operation,
            RoomViewLifecycle current,
            RoomViewLifecycle next)
        {
            return new RoomViewTransition(
                operation,
                RoomViewTransitionKind.Applied,
                RoomViewTransitionRejection.None,
                current,
                next);
        }

        internal static RoomViewTransition Rejected(
            RoomViewLifecycleOperation operation,
            RoomViewLifecycle state,
            RoomViewTransitionRejection rejection)
        {
            return new RoomViewTransition(
                operation,
                RoomViewTransitionKind.Rejected,
                rejection,
                state,
                state);
        }
    }
}

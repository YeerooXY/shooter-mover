using System;
using ShooterMover.Contracts.Mission;

namespace ShooterMover.Contracts.Rooms
{
    public enum RoomProjectionLifecyclePhase
    {
        Unloaded = 0,
        Loaded = 1,
        Unloading = 2,
    }

    public enum RoomProjectionLifecycleOperation
    {
        Load = 1,
        Refresh = 2,
        Reload = 3,
        BeginUnload = 4,
        CompleteUnload = 5,
        ResumeInterruptedUnload = 6,
    }

    public enum RoomProjectionTransitionKind
    {
        NoChange = 1,
        Applied = 2,
        Rejected = 3,
    }

    public enum RoomProjectionTransitionRejection
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
    public sealed class RoomProjectionLifecycle
    {
        private RoomProjectionLifecycle(
            RoomProjectionIdentity identity,
            RoomProjectionLifecyclePhase phase,
            RoomProjectionKey activeKey)
        {
            Identity = RoomContractFormat.RequireNotNull(identity, nameof(identity));

            if (phase == RoomProjectionLifecyclePhase.Unloaded)
            {
                if (activeKey != null)
                {
                    throw new ArgumentException(
                        "An unloaded room projection cannot retain an active projection key.",
                        nameof(activeKey));
                }
            }
            else if (phase == RoomProjectionLifecyclePhase.Loaded
                || phase == RoomProjectionLifecyclePhase.Unloading)
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

        public RoomProjectionIdentity Identity { get; }

        public RoomProjectionLifecyclePhase Phase { get; }

        public RoomProjectionKey ActiveKey { get; }

        public bool IsLoaded
        {
            get { return Phase == RoomProjectionLifecyclePhase.Loaded; }
        }

        public static RoomProjectionLifecycle Create(RoomProjectionIdentity identity)
        {
            return new RoomProjectionLifecycle(
                RoomContractFormat.RequireNotNull(identity, nameof(identity)),
                RoomProjectionLifecyclePhase.Unloaded,
                null);
        }

        public RoomProjectionTransition Load(RoomProjectionKey key)
        {
            RoomProjectionKey validated = ValidateKey(key);

            if (Phase == RoomProjectionLifecyclePhase.Loaded)
            {
                if (ActiveKey.Equals(validated))
                {
                    return RoomProjectionTransition.NoChange(
                        RoomProjectionLifecycleOperation.Load,
                        this);
                }

                return RoomProjectionTransition.Rejected(
                    RoomProjectionLifecycleOperation.Load,
                    this,
                    RoomProjectionTransitionRejection.InvalidTransition);
            }

            if (Phase == RoomProjectionLifecyclePhase.Unloading)
            {
                return RoomProjectionTransition.Rejected(
                    RoomProjectionLifecycleOperation.Load,
                    this,
                    RoomProjectionTransitionRejection.InvalidTransition);
            }

            return RoomProjectionTransition.Applied(
                RoomProjectionLifecycleOperation.Load,
                this,
                NewLoaded(validated));
        }

        public RoomProjectionTransition Refresh(RoomProjectionKey key)
        {
            RoomProjectionKey validated = ValidateKey(key);

            if (Phase != RoomProjectionLifecyclePhase.Loaded)
            {
                return RoomProjectionTransition.Rejected(
                    RoomProjectionLifecycleOperation.Refresh,
                    this,
                    RoomProjectionTransitionRejection.NotLoaded);
            }

            if (!ActiveKey.RunId.Equals(validated.RunId))
            {
                return RoomProjectionTransition.Rejected(
                    RoomProjectionLifecycleOperation.Refresh,
                    this,
                    RoomProjectionTransitionRejection.DifferentRun);
            }

            MissionSequenceRelation relation = validated.Sequence.RelateTo(ActiveKey.Sequence);
            if (relation == MissionSequenceRelation.Stale)
            {
                return RoomProjectionTransition.Rejected(
                    RoomProjectionLifecycleOperation.Refresh,
                    this,
                    RoomProjectionTransitionRejection.StaleProjectionKey);
            }

            if (ActiveKey.Equals(validated))
            {
                return RoomProjectionTransition.NoChange(
                    RoomProjectionLifecycleOperation.Refresh,
                    this);
            }

            return RoomProjectionTransition.Applied(
                RoomProjectionLifecycleOperation.Refresh,
                this,
                NewLoaded(validated));
        }

        public RoomProjectionTransition Reload(RoomProjectionKey key)
        {
            RoomProjectionKey validated = ValidateKey(key);

            if (Phase == RoomProjectionLifecyclePhase.Loaded
                && ActiveKey.Equals(validated))
            {
                return RoomProjectionTransition.NoChange(
                    RoomProjectionLifecycleOperation.Reload,
                    this);
            }

            if (ActiveKey != null
                && ActiveKey.RunId.Equals(validated.RunId)
                && validated.Sequence.RelateTo(ActiveKey.Sequence)
                    == MissionSequenceRelation.Stale)
            {
                return RoomProjectionTransition.Rejected(
                    RoomProjectionLifecycleOperation.Reload,
                    this,
                    RoomProjectionTransitionRejection.StaleProjectionKey);
            }

            return RoomProjectionTransition.Applied(
                RoomProjectionLifecycleOperation.Reload,
                this,
                NewLoaded(validated));
        }

        public RoomProjectionTransition BeginUnload()
        {
            if (Phase == RoomProjectionLifecyclePhase.Unloaded
                || Phase == RoomProjectionLifecyclePhase.Unloading)
            {
                return RoomProjectionTransition.NoChange(
                    RoomProjectionLifecycleOperation.BeginUnload,
                    this);
            }

            RoomProjectionLifecycle next = new RoomProjectionLifecycle(
                Identity,
                RoomProjectionLifecyclePhase.Unloading,
                ActiveKey);
            return RoomProjectionTransition.Applied(
                RoomProjectionLifecycleOperation.BeginUnload,
                this,
                next);
        }

        public RoomProjectionTransition CompleteUnload()
        {
            if (Phase == RoomProjectionLifecyclePhase.Unloaded)
            {
                return RoomProjectionTransition.NoChange(
                    RoomProjectionLifecycleOperation.CompleteUnload,
                    this);
            }

            if (Phase != RoomProjectionLifecyclePhase.Unloading)
            {
                return RoomProjectionTransition.Rejected(
                    RoomProjectionLifecycleOperation.CompleteUnload,
                    this,
                    RoomProjectionTransitionRejection.InvalidTransition);
            }

            RoomProjectionLifecycle next = new RoomProjectionLifecycle(
                Identity,
                RoomProjectionLifecyclePhase.Unloaded,
                null);
            return RoomProjectionTransition.Applied(
                RoomProjectionLifecycleOperation.CompleteUnload,
                this,
                next);
        }

        public RoomProjectionTransition ResumeAfterInterruptedUnload()
        {
            if (Phase == RoomProjectionLifecyclePhase.Loaded)
            {
                return RoomProjectionTransition.NoChange(
                    RoomProjectionLifecycleOperation.ResumeInterruptedUnload,
                    this);
            }

            if (Phase == RoomProjectionLifecyclePhase.Unloaded)
            {
                return RoomProjectionTransition.Rejected(
                    RoomProjectionLifecycleOperation.ResumeInterruptedUnload,
                    this,
                    RoomProjectionTransitionRejection.NotLoaded);
            }

            return RoomProjectionTransition.Applied(
                RoomProjectionLifecycleOperation.ResumeInterruptedUnload,
                this,
                NewLoaded(ActiveKey));
        }

        private RoomProjectionKey ValidateKey(RoomProjectionKey key)
        {
            RoomProjectionKey validated = RoomContractFormat.RequireNotNull(key, nameof(key));
            EnsureKeyTargetsIdentity(Identity, validated);
            return validated;
        }

        private RoomProjectionLifecycle NewLoaded(RoomProjectionKey key)
        {
            return new RoomProjectionLifecycle(
                Identity,
                RoomProjectionLifecyclePhase.Loaded,
                key);
        }

        private static void EnsureKeyTargetsIdentity(
            RoomProjectionIdentity identity,
            RoomProjectionKey key)
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
    public sealed class RoomProjectionTransition
    {
        private RoomProjectionTransition(
            RoomProjectionLifecycleOperation operation,
            RoomProjectionTransitionKind kind,
            RoomProjectionTransitionRejection rejection,
            RoomProjectionLifecycle current,
            RoomProjectionLifecycle next)
        {
            if (kind != RoomProjectionTransitionKind.NoChange
                && kind != RoomProjectionTransitionKind.Applied
                && kind != RoomProjectionTransitionKind.Rejected)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (kind == RoomProjectionTransitionKind.Rejected
                && rejection == RoomProjectionTransitionRejection.None)
            {
                throw new ArgumentException(
                    "Rejected transitions require an explicit rejection reason.",
                    nameof(rejection));
            }

            if (kind != RoomProjectionTransitionKind.Rejected
                && rejection != RoomProjectionTransitionRejection.None)
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

        public RoomProjectionLifecycleOperation Operation { get; }

        public RoomProjectionTransitionKind Kind { get; }

        public RoomProjectionTransitionRejection Rejection { get; }

        public RoomProjectionLifecycle Current { get; }

        public RoomProjectionLifecycle Next { get; }

        public bool WasApplied
        {
            get { return Kind == RoomProjectionTransitionKind.Applied; }
        }

        internal static RoomProjectionTransition NoChange(
            RoomProjectionLifecycleOperation operation,
            RoomProjectionLifecycle state)
        {
            return new RoomProjectionTransition(
                operation,
                RoomProjectionTransitionKind.NoChange,
                RoomProjectionTransitionRejection.None,
                state,
                state);
        }

        internal static RoomProjectionTransition Applied(
            RoomProjectionLifecycleOperation operation,
            RoomProjectionLifecycle current,
            RoomProjectionLifecycle next)
        {
            return new RoomProjectionTransition(
                operation,
                RoomProjectionTransitionKind.Applied,
                RoomProjectionTransitionRejection.None,
                current,
                next);
        }

        internal static RoomProjectionTransition Rejected(
            RoomProjectionLifecycleOperation operation,
            RoomProjectionLifecycle state,
            RoomProjectionTransitionRejection rejection)
        {
            return new RoomProjectionTransition(
                operation,
                RoomProjectionTransitionKind.Rejected,
                rejection,
                state,
                state);
        }
    }
}

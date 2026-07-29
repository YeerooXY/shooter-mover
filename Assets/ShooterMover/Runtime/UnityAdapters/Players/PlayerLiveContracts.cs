using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.GameplayEntities;

namespace ShooterMover.UnityAdapters.Players
{
    public enum PlayerLiveConstructionStatus
    {
        Constructed = 1,
        RejectedDuplicate = 2,
        RejectedInvalid = 3,
        RejectedOwnership = 4,
    }

    public enum PlayerLiveConstructionRejectionCode
    {
        None = 0,
        AlreadyConstructed = 1,
        MissingConfiguration = 2,
        MissingActorDefinition = 3,
        MissingMovementAdapter = 4,
        MissingPresentationAdapter = 5,
        MissingInputAdapter = 6,
        MissingAttributionResolver = 7,
        MissingRunCoordinator = 8,
        ActorDefinitionRejected = 9,
        InitialGenerationMismatch = 10,
        InputOwnershipUnavailable = 11,
    }

    public sealed class PlayerLiveConfiguration
    {
        public PlayerLiveConfiguration(PlayerActorDefinition actorDefinition)
        {
            ActorDefinition = actorDefinition;
        }

        public PlayerActorDefinition ActorDefinition { get; }
    }

    public sealed class PlayerInputOwnership : IEquatable<PlayerInputOwnership>
    {
        public PlayerInputOwnership(StableId actorInstanceId, StableId runParticipantId)
        {
            ActorInstanceId = actorInstanceId ?? throw new ArgumentNullException(nameof(actorInstanceId));
            RunParticipantId = runParticipantId ?? throw new ArgumentNullException(nameof(runParticipantId));
        }

        public StableId ActorInstanceId { get; }
        public StableId RunParticipantId { get; }

        public bool Equals(PlayerInputOwnership other)
        {
            return !ReferenceEquals(other, null)
                && ActorInstanceId == other.ActorInstanceId
                && RunParticipantId == other.RunParticipantId;
        }

        public override bool Equals(object obj) { return Equals(obj as PlayerInputOwnership); }
        public override int GetHashCode()
        {
            unchecked { return (ActorInstanceId.GetHashCode() * 397) ^ RunParticipantId.GetHashCode(); }
        }
    }

    public sealed class PlayerMovementSnapshot
    {
        public PlayerMovementSnapshot(
            long generation,
            double positionX,
            double positionY,
            double velocityX,
            double velocityY,
            ThrusterStatusSnapshot thrusterStatus)
        {
            if (generation < 0L) throw new ArgumentOutOfRangeException(nameof(generation));
            RequireFinite(positionX, nameof(positionX));
            RequireFinite(positionY, nameof(positionY));
            RequireFinite(velocityX, nameof(velocityX));
            RequireFinite(velocityY, nameof(velocityY));
            Generation = generation;
            PositionX = positionX;
            PositionY = positionY;
            VelocityX = velocityX;
            VelocityY = velocityY;
            ThrusterStatus = thrusterStatus ?? throw new ArgumentNullException(nameof(thrusterStatus));
        }

        public long Generation { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public double VelocityX { get; }
        public double VelocityY { get; }
        public ThrusterStatusSnapshot ThrusterStatus { get; }
        public bool IsBoosting { get { return ThrusterStatus.State == ThrusterStatusState.Burst; } }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }

    public sealed class PlayerLiveSnapshot
    {
        public PlayerLiveSnapshot(PlayerActorSnapshot player, PlayerMovementSnapshot movement)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        public PlayerActorSnapshot Player { get; }
        public PlayerMovementSnapshot Movement { get; }
    }

    public sealed class PlayerHudHealthSnapshot
    {
        internal PlayerHudHealthSnapshot(PlayerActorSnapshot source)
        {
            ActorInstanceId = source.ActorInstanceId;
            LifecycleGeneration = source.LifecycleGeneration;
            CurrentHealth = source.CurrentHealth;
            MaximumHealth = source.MaximumHealth;
            NormalizedHealth = source.MaximumHealth <= 0d ? 0d : source.CurrentHealth / source.MaximumHealth;
            IsDead = source.IsDead;
            AcceptedSequence = source.AcceptedSequence;
        }

        public StableId ActorInstanceId { get; }
        public long LifecycleGeneration { get; }
        public double CurrentHealth { get; }
        public double MaximumHealth { get; }
        public double NormalizedHealth { get; }
        public bool IsDead { get; }
        public long AcceptedSequence { get; }
    }

    public static class PlayerHudHealthProjector
    {
        public static PlayerHudHealthSnapshot Project(PlayerActorSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return new PlayerHudHealthSnapshot(snapshot);
        }
    }

    public sealed class PlayerDamageRequest
    {
        public PlayerDamageRequest(
            StableId eventId,
            StableId sourceActorId,
            StableId untrustedSourceRunParticipantId,
            StableId targetActorId,
            double amount,
            CombatChannel channel,
            long lifecycleGeneration)
        {
            EventId = eventId;
            SourceActorId = sourceActorId;
            UntrustedSourceRunParticipantId = untrustedSourceRunParticipantId;
            TargetActorId = targetActorId;
            Amount = amount;
            Channel = channel;
            LifecycleGeneration = lifecycleGeneration;
        }

        public StableId EventId { get; }
        public StableId SourceActorId { get; }
        public StableId UntrustedSourceRunParticipantId { get; }
        public StableId TargetActorId { get; }
        public double Amount { get; }
        public CombatChannel Channel { get; }
        public long LifecycleGeneration { get; }
    }

    public sealed class PlayerHealingRequest
    {
        public PlayerHealingRequest(
            StableId operationId,
            StableId sourceActorId,
            StableId untrustedSourceRunParticipantId,
            StableId targetActorId,
            double amount,
            long lifecycleGeneration)
        {
            OperationId = operationId;
            SourceActorId = sourceActorId;
            UntrustedSourceRunParticipantId = untrustedSourceRunParticipantId;
            TargetActorId = targetActorId;
            Amount = amount;
            LifecycleGeneration = lifecycleGeneration;
        }

        public StableId OperationId { get; }
        public StableId SourceActorId { get; }
        public StableId UntrustedSourceRunParticipantId { get; }
        public StableId TargetActorId { get; }
        public double Amount { get; }
        public long LifecycleGeneration { get; }
    }

    public enum PlayerLiveRestartStatus
    {
        Applied = 1,
        Duplicate = 2,
        RejectedInvalid = 3,
        RejectedByLifecycle = 4,
        RejectedByMovement = 5,
    }

    public enum PlayerLiveRestartRejectionCode
    {
        None = 0,
        NullCommand = 1,
        MissingOperationId = 2,
        MissingTargetActorId = 3,
        TargetMismatch = 4,
        InvalidGeneration = 5,
        StaleGeneration = 6,
        FutureGeneration = 7,
        ReplacementGenerationMustIncrement = 8,
        MovementGenerationMismatch = 9,
        ConflictingDuplicate = 10,
        MovementRejected = 11,
        Disposed = 12,
    }

    public sealed class PlayerLiveRestartCommand : IEquatable<PlayerLiveRestartCommand>
    {
        public PlayerLiveRestartCommand(
            StableId operationId,
            StableId targetActorId,
            long retiringGeneration,
            long replacementGeneration)
        {
            OperationId = operationId;
            TargetActorId = targetActorId;
            RetiringGeneration = retiringGeneration;
            ReplacementGeneration = replacementGeneration;
        }

        public StableId OperationId { get; }
        public StableId TargetActorId { get; }
        public long RetiringGeneration { get; }
        public long ReplacementGeneration { get; }

        public bool Equals(PlayerLiveRestartCommand other)
        {
            return !ReferenceEquals(other, null)
                && OperationId == other.OperationId
                && TargetActorId == other.TargetActorId
                && RetiringGeneration == other.RetiringGeneration
                && ReplacementGeneration == other.ReplacementGeneration;
        }

        public override bool Equals(object obj) { return Equals(obj as PlayerLiveRestartCommand); }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (OperationId == null ? 0 : OperationId.GetHashCode());
                hash = (hash * 31) + (TargetActorId == null ? 0 : TargetActorId.GetHashCode());
                hash = (hash * 31) + RetiringGeneration.GetHashCode();
                return (hash * 31) + ReplacementGeneration.GetHashCode();
            }
        }
    }

    public sealed class PlayerLiveRestartResult
    {
        public PlayerLiveRestartResult(
            PlayerLiveRestartStatus status,
            PlayerLiveRestartRejectionCode rejectionCode,
            PlayerLiveRestartCommand command,
            PlayerLiveSnapshot snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode;
            Command = command;
            Snapshot = snapshot;
        }

        public PlayerLiveRestartStatus Status { get; }
        public PlayerLiveRestartRejectionCode RejectionCode { get; }
        public PlayerLiveRestartCommand Command { get; }
        public PlayerLiveSnapshot Snapshot { get; }
    }

    public interface IPlayerMovementLive : IDisposable
    {
        bool IsDisposed { get; }
        PlayerMovementSnapshot ExportSnapshot();
        bool TryRestart(long retiringGeneration, long replacementGeneration);
    }

    public interface IPlayerPresentationLive : IDisposable
    {
        void RefreshContinuousBoost(PlayerMovementSnapshot movementSnapshot);
        void Restart(PlayerLiveSnapshot runtimeSnapshot);
    }

    public interface IPlayerInputLive : IDisposable
    {
        bool TryAcquire(PlayerInputOwnership ownership);
        bool Release(PlayerInputOwnership ownership);
    }

    public interface ITrustedPlayerAttributionResolver
    {
        StableId ResolveSourceRunParticipant(StableId sourceActorId);
    }

    public interface IPlayerRunFlow
    {
        void ObservePlayerDeath(GameplayEntityDeathFact deathFact);
    }

    public sealed class PlayerLiveAttachments
    {
        public PlayerLiveAttachments(
            IPlayerMovementLive movement,
            IPlayerPresentationLive presentation,
            IPlayerInputLive input,
            ITrustedPlayerAttributionResolver attributionResolver,
            IPlayerRunFlow runCoordinator)
        {
            Movement = movement;
            Presentation = presentation;
            Input = input;
            AttributionResolver = attributionResolver;
            RunCoordinator = runCoordinator;
        }

        public IPlayerMovementLive Movement { get; }
        public IPlayerPresentationLive Presentation { get; }
        public IPlayerInputLive Input { get; }
        public ITrustedPlayerAttributionResolver AttributionResolver { get; }
        public IPlayerRunFlow RunCoordinator { get; }
    }

    public sealed class PlayerLiveConstructionResult
    {
        internal PlayerLiveConstructionResult(
            PlayerLiveConstructionStatus status,
            PlayerLiveConstructionRejectionCode rejectionCode,
            PlayerActorCreationRejectionCode actorRejectionCode,
            PlayerLiveSetup runtime)
        {
            Status = status;
            RejectionCode = rejectionCode;
            ActorRejectionCode = actorRejectionCode;
            Runtime = runtime;
        }

        public PlayerLiveConstructionStatus Status { get; }
        public PlayerLiveConstructionRejectionCode RejectionCode { get; }
        public PlayerActorCreationRejectionCode ActorRejectionCode { get; }
        public PlayerLiveSetup Runtime { get; }
        public bool IsConstructed { get { return Status == PlayerLiveConstructionStatus.Constructed; } }
    }
}

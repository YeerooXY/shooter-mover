using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.GameplayEntities
{
    public enum PlayerActorLifecycleState
    {
        Alive = 1,
        Dead = 2,
    }

    public enum PlayerActorCreationStatus
    {
        Created = 1,
        RejectedInvalid = 2,
    }

    public enum PlayerActorCreationRejectionCode
    {
        None = 0,
        MissingActorInstanceId = 1,
        MissingRunParticipantId = 2,
        MissingCharacterId = 3,
        MissingFactionId = 4,
        InvalidMaximumHealth = 5,
        InvalidInitialGeneration = 6,
    }

    public sealed class PlayerActorDefinition
    {
        public PlayerActorDefinition(
            StableId actorInstanceId,
            StableId runParticipantId,
            StableId characterId,
            StableId factionId,
            double maximumHealth,
            long initialLifecycleGeneration)
        {
            ActorInstanceId = actorInstanceId;
            RunParticipantId = runParticipantId;
            CharacterId = characterId;
            FactionId = factionId;
            MaximumHealth = maximumHealth;
            InitialLifecycleGeneration = initialLifecycleGeneration;
        }

        public StableId ActorInstanceId { get; }

        public StableId RunParticipantId { get; }

        public StableId CharacterId { get; }

        public StableId FactionId { get; }

        public double MaximumHealth { get; }

        public long InitialLifecycleGeneration { get; }
    }

    public sealed class PlayerActorCreationResult
    {
        internal PlayerActorCreationResult(
            PlayerActorCreationStatus status,
            PlayerActorCreationRejectionCode rejectionCode,
            PlayerActorAuthority authority)
        {
            Status = status;
            RejectionCode = rejectionCode;
            Authority = authority;
        }

        public PlayerActorCreationStatus Status { get; }

        public PlayerActorCreationRejectionCode RejectionCode { get; }

        public PlayerActorAuthority Authority { get; }

        public bool IsCreated
        {
            get { return Status == PlayerActorCreationStatus.Created; }
        }
    }

    /// <summary>
    /// Detached immutable read model. It is safe for local/network input adapters and presentation.
    /// </summary>
    public sealed class PlayerActorSnapshot : IEquatable<PlayerActorSnapshot>
    {
        internal PlayerActorSnapshot(
            GameplayEntityIdentity identity,
            double maximumHealth,
            double currentHealth,
            PlayerActorLifecycleState lifecycleState,
            long acceptedSequence)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            MaximumHealth = maximumHealth;
            CurrentHealth = currentHealth;
            LifecycleState = lifecycleState;
            AcceptedSequence = acceptedSequence;
            VitalState = new VitalState(currentHealth, maximumHealth, 0d, 0d);
        }

        public GameplayEntityIdentity Identity { get; }

        public StableId ActorInstanceId
        {
            get { return Identity.ActorInstanceId; }
        }

        public StableId RunParticipantId
        {
            get { return Identity.Ownership.RunParticipantId; }
        }

        public StableId CharacterId
        {
            get { return Identity.Ownership.SourceCharacterId; }
        }

        public StableId FactionId
        {
            get { return Identity.FactionId; }
        }

        public double MaximumHealth { get; }

        public double CurrentHealth { get; }

        public PlayerActorLifecycleState LifecycleState { get; }

        public long LifecycleGeneration
        {
            get { return Identity.LifecycleGeneration; }
        }

        public long AcceptedSequence { get; }

        public VitalState VitalState { get; }

        public bool IsAlive
        {
            get { return LifecycleState == PlayerActorLifecycleState.Alive; }
        }

        public bool IsDead
        {
            get { return LifecycleState == PlayerActorLifecycleState.Dead; }
        }

        public bool Equals(PlayerActorSnapshot other)
        {
            return !ReferenceEquals(other, null)
                && Identity.Equals(other.Identity)
                && MaximumHealth == other.MaximumHealth
                && CurrentHealth == other.CurrentHealth
                && LifecycleState == other.LifecycleState
                && AcceptedSequence == other.AcceptedSequence;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerActorSnapshot);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Identity.GetHashCode();
                hash = (hash * 31) + MaximumHealth.GetHashCode();
                hash = (hash * 31) + CurrentHealth.GetHashCode();
                hash = (hash * 31) + LifecycleState.GetHashCode();
                hash = (hash * 31) + AcceptedSequence.GetHashCode();
                return hash;
            }
        }
    }
}

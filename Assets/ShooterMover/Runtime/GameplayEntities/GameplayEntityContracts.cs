using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.GameplayEntities
{
    /// <summary>
    /// Explicit optional ownership for a gameplay entity. Neutral props may use None;
    /// player actors require both a run participant and a source character.
    /// </summary>
    public sealed class GameplayEntityOwnership : IEquatable<GameplayEntityOwnership>
    {
        private GameplayEntityOwnership(
            StableId runParticipantId,
            StableId sourceCharacterId)
        {
            RunParticipantId = runParticipantId;
            SourceCharacterId = sourceCharacterId;
        }

        public StableId RunParticipantId { get; }

        public StableId SourceCharacterId { get; }

        public bool HasRunParticipant
        {
            get { return RunParticipantId != null; }
        }

        public bool HasSourceCharacter
        {
            get { return SourceCharacterId != null; }
        }

        public static GameplayEntityOwnership None()
        {
            return new GameplayEntityOwnership(null, null);
        }

        public static GameplayEntityOwnership Create(
            StableId runParticipantId,
            StableId sourceCharacterId)
        {
            return new GameplayEntityOwnership(runParticipantId, sourceCharacterId);
        }

        public bool TryGetRunParticipantId(out StableId runParticipantId)
        {
            runParticipantId = RunParticipantId;
            return runParticipantId != null;
        }

        public bool TryGetSourceCharacterId(out StableId sourceCharacterId)
        {
            sourceCharacterId = SourceCharacterId;
            return sourceCharacterId != null;
        }

        public bool Equals(GameplayEntityOwnership other)
        {
            return !ReferenceEquals(other, null)
                && RunParticipantId == other.RunParticipantId
                && SourceCharacterId == other.SourceCharacterId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GameplayEntityOwnership);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (RunParticipantId == null ? 0 : RunParticipantId.GetHashCode());
                hash = (hash * 31) + (SourceCharacterId == null ? 0 : SourceCharacterId.GetHashCode());
                return hash;
            }
        }
    }

    /// <summary>
    /// Stable engine-neutral identity shared by actors, props, attacks, and future room logic.
    /// Lifecycle generation is intentionally projected separately by concrete state snapshots.
    /// </summary>
    public sealed class GameplayEntityIdentity : IEquatable<GameplayEntityIdentity>
    {
        public GameplayEntityIdentity(
            StableId entityInstanceId,
            GameplayEntityOwnership ownership,
            StableId factionId)
        {
            EntityInstanceId = entityInstanceId
                ?? throw new ArgumentNullException(nameof(entityInstanceId));
            Ownership = ownership
                ?? throw new ArgumentNullException(nameof(ownership));
            FactionId = factionId
                ?? throw new ArgumentNullException(nameof(factionId));
        }

        public StableId EntityInstanceId { get; }

        public GameplayEntityOwnership Ownership { get; }

        public StableId FactionId { get; }

        public bool Equals(GameplayEntityIdentity other)
        {
            return !ReferenceEquals(other, null)
                && EntityInstanceId == other.EntityInstanceId
                && Ownership.Equals(other.Ownership)
                && FactionId == other.FactionId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GameplayEntityIdentity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + EntityInstanceId.GetHashCode();
                hash = (hash * 31) + Ownership.GetHashCode();
                hash = (hash * 31) + FactionId.GetHashCode();
                return hash;
            }
        }
    }

    public enum DamageReceiverStatus
    {
        Applied = 1,
        Duplicate = 2,
        RejectedInvalid = 3,
        RejectedByLifecycle = 4,
    }

    public enum DamageReceiverRejectionCode
    {
        None = 0,
        NullCommand = 1,
        MissingEventId = 2,
        MissingSourceActorId = 3,
        MissingTargetActorId = 4,
        InvalidAmount = 5,
        InvalidChannel = 6,
        TargetMismatch = 7,
        InvalidGeneration = 8,
        StaleGeneration = 9,
        FutureGeneration = 10,
        ActorDead = 11,
        ConflictingDuplicate = 12,
    }

    /// <summary>
    /// Immutable damage request. Validation belongs to the receiving authority so malformed
    /// adapter/network input fails closed with a deterministic result instead of throwing.
    /// </summary>
    public sealed class DamageReceiverCommand : ICombatEventMessage, IEquatable<DamageReceiverCommand>
    {
        public DamageReceiverCommand(
            StableId eventId,
            StableId sourceActorId,
            StableId sourceRunParticipantId,
            StableId targetActorId,
            double amount,
            CombatChannel channel,
            long lifecycleGeneration)
        {
            EventId = eventId;
            SourceActorId = sourceActorId;
            SourceRunParticipantId = sourceRunParticipantId;
            TargetActorId = targetActorId;
            Amount = amount;
            Channel = channel;
            LifecycleGeneration = lifecycleGeneration;
        }

        public StableId EventId { get; }

        public StableId SourceActorId { get; }

        public StableId SourceRunParticipantId { get; }

        public bool HasSourceRunParticipant
        {
            get { return SourceRunParticipantId != null; }
        }

        public StableId TargetActorId { get; }

        public double Amount { get; }

        public CombatChannel Channel { get; }

        public long LifecycleGeneration { get; }

        public bool TryGetSourceRunParticipantId(out StableId sourceRunParticipantId)
        {
            sourceRunParticipantId = SourceRunParticipantId;
            return sourceRunParticipantId != null;
        }

        StableId ICombatEventMessage.SourceId
        {
            get { return SourceActorId; }
        }

        StableId ICombatEventMessage.TargetId
        {
            get { return TargetActorId; }
        }

        public bool Equals(DamageReceiverCommand other)
        {
            return !ReferenceEquals(other, null)
                && EventId == other.EventId
                && SourceActorId == other.SourceActorId
                && SourceRunParticipantId == other.SourceRunParticipantId
                && TargetActorId == other.TargetActorId
                && Amount == other.Amount
                && Channel == other.Channel
                && LifecycleGeneration == other.LifecycleGeneration;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DamageReceiverCommand);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (EventId == null ? 0 : EventId.GetHashCode());
                hash = (hash * 31) + (SourceActorId == null ? 0 : SourceActorId.GetHashCode());
                hash = (hash * 31) + (SourceRunParticipantId == null ? 0 : SourceRunParticipantId.GetHashCode());
                hash = (hash * 31) + (TargetActorId == null ? 0 : TargetActorId.GetHashCode());
                hash = (hash * 31) + Amount.GetHashCode();
                hash = (hash * 31) + Channel.GetHashCode();
                hash = (hash * 31) + LifecycleGeneration.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class GameplayEntityDeathFact
    {
        internal GameplayEntityDeathFact(
            StableId eventId,
            StableId sourceActorId,
            StableId sourceRunParticipantId,
            StableId targetActorId,
            double requestedAmount,
            double appliedAmount,
            CombatChannel channel,
            long lifecycleGeneration,
            long acceptedSequence)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            SourceActorId = sourceActorId ?? throw new ArgumentNullException(nameof(sourceActorId));
            SourceRunParticipantId = sourceRunParticipantId;
            TargetActorId = targetActorId ?? throw new ArgumentNullException(nameof(targetActorId));
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            Channel = channel;
            LifecycleGeneration = lifecycleGeneration;
            AcceptedSequence = acceptedSequence;
        }

        public StableId EventId { get; }

        public StableId SourceActorId { get; }

        public StableId SourceRunParticipantId { get; }

        public bool HasSourceRunParticipant
        {
            get { return SourceRunParticipantId != null; }
        }

        public StableId TargetActorId { get; }

        public double RequestedAmount { get; }

        public double AppliedAmount { get; }

        public CombatChannel Channel { get; }

        public long LifecycleGeneration { get; }

        public long AcceptedSequence { get; }
    }

    public sealed class DamageReceiverResult
    {
        internal DamageReceiverResult(
            DamageReceiverStatus status,
            DamageReceiverRejectionCode rejectionCode,
            DamageReceiverCommand command,
            GameplayEntityIdentity identity,
            DamageMessage damageMessage,
            GameplayEntityDeathFact deathFact,
            long acceptedSequence)
        {
            Status = status;
            RejectionCode = rejectionCode;
            Command = command;
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            DamageMessage = damageMessage;
            DeathFact = deathFact;
            AcceptedSequence = acceptedSequence;
        }

        public DamageReceiverStatus Status { get; }

        public DamageReceiverRejectionCode RejectionCode { get; }

        public DamageReceiverCommand Command { get; }

        public GameplayEntityIdentity Identity { get; }

        public DamageMessage DamageMessage { get; }

        public GameplayEntityDeathFact DeathFact { get; }

        public long AcceptedSequence { get; }

        public bool StateChanged
        {
            get { return Status == DamageReceiverStatus.Applied; }
        }
    }

    /// <summary>
    /// Small capability port; consumers do not depend on a concrete player, enemy, or prop type.
    /// </summary>
    public interface IDamageReceiver
    {
        GameplayEntityIdentity Identity { get; }

        DamageReceiverResult ApplyDamage(DamageReceiverCommand command);
    }
}

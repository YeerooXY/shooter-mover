using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.GameplayEntities
{
    public enum PlayerActorOperationStatus
    {
        Applied = 1,
        Duplicate = 2,
        RejectedInvalid = 3,
        RejectedByLifecycle = 4,
    }

    public enum PlayerActorOperationRejectionCode
    {
        None = 0,
        NullCommand = 1,
        MissingOperationId = 2,
        MissingSourceActorId = 3,
        MissingTargetActorId = 4,
        InvalidAmount = 5,
        TargetMismatch = 6,
        InvalidGeneration = 7,
        StaleGeneration = 8,
        FutureGeneration = 9,
        ActorDead = 10,
        ConflictingDuplicate = 11,
        RetiringGenerationMismatch = 12,
        ReplacementGenerationDidNotAdvance = 13,
    }

    public sealed class PlayerActorHealingCommand : IEquatable<PlayerActorHealingCommand>
    {
        public PlayerActorHealingCommand(
            StableId operationId,
            StableId sourceActorId,
            StableId targetActorId,
            double amount,
            long lifecycleGeneration)
        {
            OperationId = operationId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            Amount = amount;
            LifecycleGeneration = lifecycleGeneration;
        }

        public StableId OperationId { get; }

        public StableId SourceActorId { get; }

        public StableId TargetActorId { get; }

        public double Amount { get; }

        public long LifecycleGeneration { get; }

        public bool Equals(PlayerActorHealingCommand other)
        {
            return !ReferenceEquals(other, null)
                && OperationId == other.OperationId
                && SourceActorId == other.SourceActorId
                && TargetActorId == other.TargetActorId
                && Amount == other.Amount
                && LifecycleGeneration == other.LifecycleGeneration;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerActorHealingCommand);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (OperationId == null ? 0 : OperationId.GetHashCode());
                hash = (hash * 31) + (SourceActorId == null ? 0 : SourceActorId.GetHashCode());
                hash = (hash * 31) + (TargetActorId == null ? 0 : TargetActorId.GetHashCode());
                hash = (hash * 31) + Amount.GetHashCode();
                hash = (hash * 31) + LifecycleGeneration.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class PlayerActorHealingResult
    {
        internal PlayerActorHealingResult(
            PlayerActorOperationStatus status,
            PlayerActorOperationRejectionCode rejectionCode,
            PlayerActorHealingCommand command,
            double appliedAmount,
            PlayerActorSnapshot snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode;
            Command = command;
            AppliedAmount = appliedAmount;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public PlayerActorOperationStatus Status { get; }

        public PlayerActorOperationRejectionCode RejectionCode { get; }

        public PlayerActorHealingCommand Command { get; }

        public double AppliedAmount { get; }

        public PlayerActorSnapshot Snapshot { get; }

        public bool StateChanged
        {
            get { return Status == PlayerActorOperationStatus.Applied && AppliedAmount > 0d; }
        }
    }

    public sealed class PlayerActorRestartCommand : IEquatable<PlayerActorRestartCommand>
    {
        public PlayerActorRestartCommand(
            StableId operationId,
            StableId targetActorId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration)
        {
            OperationId = operationId;
            TargetActorId = targetActorId;
            RetiringLifecycleGeneration = retiringLifecycleGeneration;
            ReplacementLifecycleGeneration = replacementLifecycleGeneration;
        }

        public StableId OperationId { get; }

        public StableId TargetActorId { get; }

        public long RetiringLifecycleGeneration { get; }

        public long ReplacementLifecycleGeneration { get; }

        public bool Equals(PlayerActorRestartCommand other)
        {
            return !ReferenceEquals(other, null)
                && OperationId == other.OperationId
                && TargetActorId == other.TargetActorId
                && RetiringLifecycleGeneration == other.RetiringLifecycleGeneration
                && ReplacementLifecycleGeneration == other.ReplacementLifecycleGeneration;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerActorRestartCommand);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (OperationId == null ? 0 : OperationId.GetHashCode());
                hash = (hash * 31) + (TargetActorId == null ? 0 : TargetActorId.GetHashCode());
                hash = (hash * 31) + RetiringLifecycleGeneration.GetHashCode();
                hash = (hash * 31) + ReplacementLifecycleGeneration.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class PlayerActorRestartResult
    {
        internal PlayerActorRestartResult(
            PlayerActorOperationStatus status,
            PlayerActorOperationRejectionCode rejectionCode,
            PlayerActorRestartCommand command,
            PlayerActorSnapshot snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode;
            Command = command;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public PlayerActorOperationStatus Status { get; }

        public PlayerActorOperationRejectionCode RejectionCode { get; }

        public PlayerActorRestartCommand Command { get; }

        public PlayerActorSnapshot Snapshot { get; }
    }
}

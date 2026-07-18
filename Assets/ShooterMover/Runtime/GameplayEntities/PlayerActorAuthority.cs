using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.GameplayEntities
{
    /// <summary>
    /// Engine-neutral authority for one player actor instance. It owns health, lifecycle,
    /// generation-scoped combat deduplication, and accepted-state sequence only.
    /// </summary>
    public sealed class PlayerActorAuthority : IDamageReceiver
    {
        private enum AcceptedOperationKind
        {
            Damage = 1,
            Healing = 2,
        }

        private sealed class AcceptedOperation
        {
            public AcceptedOperation(
                AcceptedOperationKind kind,
                DamageReceiverCommand damage,
                PlayerActorHealingCommand healing)
            {
                Kind = kind;
                Damage = damage;
                Healing = healing;
            }

            public AcceptedOperationKind Kind { get; }

            public DamageReceiverCommand Damage { get; }

            public PlayerActorHealingCommand Healing { get; }
        }

        private readonly StableId actorInstanceId;
        private readonly StableId runParticipantId;
        private readonly StableId characterId;
        private readonly StableId factionId;
        private readonly double maximumHealth;
        private readonly Dictionary<StableId, AcceptedOperation> acceptedOperations =
            new Dictionary<StableId, AcceptedOperation>();

        private double currentHealth;
        private PlayerActorLifecycleState lifecycleState;
        private long lifecycleGeneration;
        private long acceptedSequence;
        private PlayerActorRestartCommand lastRestartCommand;

        private PlayerActorAuthority(PlayerActorDefinition definition)
        {
            actorInstanceId = definition.ActorInstanceId;
            runParticipantId = definition.RunParticipantId;
            characterId = definition.CharacterId;
            factionId = definition.FactionId;
            maximumHealth = definition.MaximumHealth;
            currentHealth = maximumHealth;
            lifecycleState = PlayerActorLifecycleState.Alive;
            lifecycleGeneration = definition.InitialLifecycleGeneration;
            acceptedSequence = 0L;
        }

        public GameplayEntityIdentity Identity
        {
            get
            {
                return new GameplayEntityIdentity(
                    actorInstanceId,
                    GameplayEntityOwnership.Create(runParticipantId, characterId),
                    factionId,
                    lifecycleGeneration);
            }
        }

        public static PlayerActorCreationResult TryCreate(PlayerActorDefinition definition)
        {
            PlayerActorCreationRejectionCode rejection = ValidateDefinition(definition);
            if (rejection != PlayerActorCreationRejectionCode.None)
            {
                return new PlayerActorCreationResult(
                    PlayerActorCreationStatus.RejectedInvalid,
                    rejection,
                    null);
            }

            return new PlayerActorCreationResult(
                PlayerActorCreationStatus.Created,
                PlayerActorCreationRejectionCode.None,
                new PlayerActorAuthority(definition));
        }

        public PlayerActorSnapshot ExportSnapshot()
        {
            return new PlayerActorSnapshot(
                Identity,
                maximumHealth,
                currentHealth,
                lifecycleState,
                acceptedSequence);
        }

        public DamageReceiverResult ApplyDamage(DamageReceiverCommand command)
        {
            DamageReceiverRejectionCode invalid = ValidateDamage(command);
            if (invalid != DamageReceiverRejectionCode.None)
            {
                return DamageResult(
                    invalid == DamageReceiverRejectionCode.StaleGeneration
                        || invalid == DamageReceiverRejectionCode.FutureGeneration
                        || invalid == DamageReceiverRejectionCode.ActorDead
                        ? DamageReceiverStatus.RejectedByLifecycle
                        : DamageReceiverStatus.RejectedInvalid,
                    invalid,
                    command,
                    null,
                    null);
            }

            AcceptedOperation existing;
            if (acceptedOperations.TryGetValue(command.EventId, out existing))
            {
                bool exact = existing.Kind == AcceptedOperationKind.Damage
                    && CombatEventIdentity.Classify(existing.Damage, command)
                        == CombatEventIdentityResult.Duplicate
                    && existing.Damage.Equals(command);
                return DamageResult(
                    exact ? DamageReceiverStatus.Duplicate : DamageReceiverStatus.RejectedInvalid,
                    exact
                        ? DamageReceiverRejectionCode.None
                        : DamageReceiverRejectionCode.ConflictingDuplicate,
                    command,
                    null,
                    null);
            }

            if (lifecycleState == PlayerActorLifecycleState.Dead)
            {
                return DamageResult(
                    DamageReceiverStatus.RejectedByLifecycle,
                    DamageReceiverRejectionCode.ActorDead,
                    command,
                    null,
                    null);
            }

            PlayerActorSnapshot beforeSnapshot = ExportSnapshot();
            double applied = Math.Min(currentHealth, command.Amount);
            double afterHealth = currentHealth - applied;
            double unapplied = command.Amount - applied;

            currentHealth = afterHealth;
            if (afterHealth == 0d)
            {
                lifecycleState = PlayerActorLifecycleState.Dead;
            }

            acceptedSequence++;
            acceptedOperations.Add(
                command.EventId,
                new AcceptedOperation(AcceptedOperationKind.Damage, command, null));

            PlayerActorSnapshot afterSnapshot = ExportSnapshot();
            DamageMessage message = new DamageMessage(
                command.EventId,
                command.SourceActorId,
                command.TargetActorId,
                command.Channel,
                command.Amount,
                ShooterMover.Contracts.Combat.DamageResult.Applied,
                beforeSnapshot.VitalState,
                afterSnapshot.VitalState,
                0d,
                command.Amount,
                applied,
                unapplied);

            GameplayEntityDeathFact death = afterSnapshot.IsDead
                ? new GameplayEntityDeathFact(
                    command.EventId,
                    command.SourceActorId,
                    command.SourceRunParticipantId,
                    command.TargetActorId,
                    command.Amount,
                    applied,
                    command.Channel,
                    lifecycleGeneration,
                    acceptedSequence)
                : null;

            return DamageResult(
                DamageReceiverStatus.Applied,
                DamageReceiverRejectionCode.None,
                command,
                message,
                death);
        }

        public PlayerActorHealingResult ApplyHealing(PlayerActorHealingCommand command)
        {
            PlayerActorOperationRejectionCode invalid = ValidateHealing(command);
            if (invalid != PlayerActorOperationRejectionCode.None)
            {
                return HealingResult(
                    invalid == PlayerActorOperationRejectionCode.StaleGeneration
                        || invalid == PlayerActorOperationRejectionCode.FutureGeneration
                        || invalid == PlayerActorOperationRejectionCode.ActorDead
                        ? PlayerActorOperationStatus.RejectedByLifecycle
                        : PlayerActorOperationStatus.RejectedInvalid,
                    invalid,
                    command,
                    0d);
            }

            AcceptedOperation existing;
            if (acceptedOperations.TryGetValue(command.OperationId, out existing))
            {
                bool exact = existing.Kind == AcceptedOperationKind.Healing
                    && existing.Healing.Equals(command);
n                return HealingResult(
                    exact ? PlayerActorOperationStatus.Duplicate : PlayerActorOperationStatus.RejectedInvalid,
                    exact
                        ? PlayerActorOperationRejectionCode.None
                        : PlayerActorOperationRejectionCode.ConflictingDuplicate,
                    command,
                    0d);
            }

            if (lifecycleState == PlayerActorLifecycleState.Dead)
            {
                return HealingResult(
                    PlayerActorOperationStatus.RejectedByLifecycle,
                    PlayerActorOperationRejectionCode.ActorDead,
                    command,
                    0d);
            }

            double available = maximumHealth - currentHealth;
            double applied = Math.Min(available, command.Amount);
            currentHealth += applied;
            acceptedSequence++;
            acceptedOperations.Add(
                command.OperationId,
                new AcceptedOperation(AcceptedOperationKind.Healing, null, command));

            return HealingResult(
                PlayerActorOperationStatus.Applied,
                PlayerActorOperationRejectionCode.None,
                command,
                applied);
        }

        public PlayerActorRestartResult Restart(PlayerActorRestartCommand command)
        {
            if (command == null)
            {
                return RestartResult(
                    PlayerActorOperationStatus.RejectedInvalid,
                    PlayerActorOperationRejectionCode.NullCommand,
                    null);
            }

            if (lastRestartCommand != null
                && lastRestartCommand.OperationId == command.OperationId)
            {
                bool exact = lastRestartCommand.Equals(command);
                return RestartResult(
                    exact ? PlayerActorOperationStatus.Duplicate : PlayerActorOperationStatus.RejectedInvalid,
                    exact
                        ? PlayerActorOperationRejectionCode.None
                        : PlayerActorOperationRejectionCode.ConflictingDuplicate,
                    command);
            }

            PlayerActorOperationRejectionCode invalid = ValidateRestart(command);
            if (invalid != PlayerActorOperationRejectionCode.None)
            {
                return RestartResult(
                    invalid == PlayerActorOperationRejectionCode.StaleGeneration
                        || invalid == PlayerActorOperationRejectionCode.FutureGeneration
                        || invalid == PlayerActorOperationRejectionCode.RetiringGenerationMismatch
                        ? PlayerActorOperationStatus.RejectedByLifecycle
                        : PlayerActorOperationStatus.RejectedInvalid,
                    invalid,
                    command);
            }

            lifecycleGeneration = command.ReplacementLifecycleGeneration;
            currentHealth = maximumHealth;
            lifecycleState = PlayerActorLifecycleState.Alive;
            acceptedOperations.Clear();
            acceptedSequence++;
            lastRestartCommand = command;

            return RestartResult(
                PlayerActorOperationStatus.Applied,
                PlayerActorOperationRejectionCode.None,
                command);
        }

        private static PlayerActorCreationRejectionCode ValidateDefinition(
            PlayerActorDefinition definition)
        {
            if (definition == null)
            {
                return PlayerActorCreationRejectionCode.MissingActorInstanceId;
            }

            if (definition.ActorInstanceId == null)
            {
                return PlayerActorCreationRejectionCode.MissingActorInstanceId;
            }

            if (definition.RunParticipantId == null)
            {
                return PlayerActorCreationRejectionCode.MissingRunParticipantId;
            }

            if (definition.CharacterId == null)
            {
                return PlayerActorCreationRejectionCode.MissingCharacterId;
            }

            if (definition.FactionId == null)
            {
                return PlayerActorCreationRejectionCode.MissingFactionId;
            }

            if (!IsFinitePositive(definition.MaximumHealth))
            {
                return PlayerActorCreationRejectionCode.InvalidMaximumHealth;
            }

            if (definition.InitialLifecycleGeneration < 0L)
            {
                return PlayerActorCreationRejectionCode.InvalidInitialGeneration;
            }

            return PlayerActorCreationRejectionCode.None;
        }

        private DamageReceiverRejectionCode ValidateDamage(DamageReceiverCommand command)
        {
            if (command == null)
            {
                return DamageReceiverRejectionCode.NullCommand;
            }

            if (command.EventId == null)
            {
                return DamageReceiverRejectionCode.MissingEventId;
            }

            if (command.SourceActorId == null)
            {
                return DamageReceiverRejectionCode.MissingSourceActorId;
            }

            if (command.TargetActorId == null)
            {
                return DamageReceiverRejectionCode.MissingTargetActorId;
            }

            if (!IsFinitePositive(command.Amount))
            {
                return DamageReceiverRejectionCode.InvalidAmount;
            }

            if (!Enum.IsDefined(typeof(CombatChannel), command.Channel)
                || command.Channel == CombatChannel.System)
            {
                return DamageReceiverRejectionCode.InvalidChannel;
            }

            if (command.TargetActorId != actorInstanceId)
            {
                return DamageReceiverRejectionCode.TargetMismatch;
            }

            if (command.LifecycleGeneration < 0L)
            {
                return DamageReceiverRejectionCode.InvalidGeneration;
            }

            if (command.LifecycleGeneration < lifecycleGeneration)
            {
                return DamageReceiverRejectionCode.StaleGeneration;
            }

            if (command.LifecycleGeneration > lifecycleGeneration)
            {
                return DamageReceiverRejectionCode.FutureGeneration;
            }

            return DamageReceiverRejectionCode.None;
        }

        private PlayerActorOperationRejectionCode ValidateHealing(
            PlayerActorHealingCommand command)
        {
            if (command == null)
            {
                return PlayerActorOperationRejectionCode.NullCommand;
            }

            if (command.OperationId == null)
            {
                return PlayerActorOperationRejectionCode.MissingOperationId;
            }

            if (command.SourceActorId == null)
            {
                return PlayerActorOperationRejectionCode.MissingSourceActorId;
            }

            if (command.TargetActorId == null)
            {
                return PlayerActorOperationRejectionCode.MissingTargetActorId;
            }

            if (!IsFinitePositive(command.Amount))
            {
                return PlayerActorOperationRejectionCode.InvalidAmount;
            }

            if (command.TargetActorId != actorInstanceId)
            {
                return PlayerActorOperationRejectionCode.TargetMismatch;
            }

            if (command.LifecycleGeneration < 0L)
            {
                return PlayerActorOperationRejectionCode.InvalidGeneration;
            }

            if (command.LifecycleGeneration < lifecycleGeneration)
            {
                return PlayerActorOperationRejectionCode.StaleGeneration;
            }

            if (command.LifecycleGeneration > lifecycleGeneration)
            {
                return PlayerActorOperationRejectionCode.FutureGeneration;
            }

            return PlayerActorOperationRejectionCode.None;
        }

        private PlayerActorOperationRejectionCode ValidateRestart(
            PlayerActorRestartCommand command)
        {
            if (command.OperationId == null)
            {
                return PlayerActorOperationRejectionCode.MissingOperationId;
            }

            if (command.TargetActorId == null)
            {
                return PlayerActorOperationRejectionCode.MissingTargetActorId;
            }

            if (command.TargetActorId != actorInstanceId)
            {
                return PlayerActorOperationRejectionCode.TargetMismatch;
            }

            if (command.RetiringLifecycleGeneration < 0L
                || command.ReplacementLifecycleGeneration < 0L)
            {
                return PlayerActorOperationRejectionCode.InvalidGeneration;
            }

            if (command.RetiringLifecycleGeneration < lifecycleGeneration)
            {
                return PlayerActorOperationRejectionCode.StaleGeneration;
            }

            if (command.RetiringLifecycleGeneration > lifecycleGeneration)
            {
                return PlayerActorOperationRejectionCode.FutureGeneration;
            }

            if (command.RetiringLifecycleGeneration != lifecycleGeneration)
            {
                return PlayerActorOperationRejectionCode.RetiringGenerationMismatch;
            }

            if (command.ReplacementLifecycleGeneration <= lifecycleGeneration)
            {
                return PlayerActorOperationRejectionCode.ReplacementGenerationDidNotAdvance;
            }

            return PlayerActorOperationRejectionCode.None;
        }

        private DamageReceiverResult DamageResult(
            DamageReceiverStatus status,
            DamageReceiverRejectionCode rejectionCode,
            DamageReceiverCommand command,
            DamageMessage message,
            GameplayEntityDeathFact death)
        {
            return new DamageReceiverResult(
                status,
                rejectionCode,
                command,
                Identity,
                message,
                death,
                acceptedSequence);
        }

        private PlayerActorHealingResult HealingResult(
            PlayerActorOperationStatus status,
            PlayerActorOperationRejectionCode rejectionCode,
            PlayerActorHealingCommand command,
            double appliedAmount)
        {
            return new PlayerActorHealingResult(
                status,
                rejectionCode,
                command,
                appliedAmount,
                ExportSnapshot());
        }

        private PlayerActorRestartResult RestartResult(
            PlayerActorOperationStatus status,
            PlayerActorOperationRejectionCode rejectionCode,
            PlayerActorRestartCommand command)
        {
            return new PlayerActorRestartResult(
                status,
                rejectionCode,
                command,
                ExportSnapshot());
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }
    }
}

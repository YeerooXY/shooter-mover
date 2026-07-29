using System;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;

namespace ShooterMover.UnityAdapters.Players
{
    /// <summary>Exactly-once, non-static construction owner for one player runtime.</summary>
    public sealed class PlayerSetupRoot : IDisposable
    {
        private PlayerSetup runtime;
        private bool disposed;

        public bool IsConstructed { get { return runtime != null; } }
        public PlayerSetup Runtime { get { return runtime; } }

        public PlayerSetupResult TryConstruct(
            PlayerConfig configuration,
            PlayerParts attachments)
        {
            if (runtime != null || disposed)
            {
                return Result(
                    PlayerSetupStatus.RejectedDuplicate,
                    PlayerSetupRejectionCode.AlreadyConstructed,
                    PlayerActorCreationRejectionCode.None,
                    runtime);
            }

            PlayerSetupRejectionCode invalid = Validate(configuration, attachments);
            if (invalid != PlayerSetupRejectionCode.None)
            {
                return Result(PlayerSetupStatus.RejectedInvalid, invalid,
                    PlayerActorCreationRejectionCode.None, null);
            }

            PlayerActorCreationResult actorCreation = PlayerActorState.TryCreate(
                configuration.ActorDefinition);
            if (!actorCreation.IsCreated)
            {
                return Result(
                    PlayerSetupStatus.RejectedInvalid,
                    PlayerSetupRejectionCode.ActorDefinitionRejected,
                    actorCreation.RejectionCode,
                    null);
            }

            PlayerMovementSnapshot movement = attachments.Movement.ExportSnapshot();
            if (movement.Generation != configuration.ActorDefinition.InitialLifecycleGeneration)
            {
                return Result(
                    PlayerSetupStatus.RejectedInvalid,
                    PlayerSetupRejectionCode.InitialGenerationMismatch,
                    PlayerActorCreationRejectionCode.None,
                    null);
            }

            PlayerControlsOwnership ownership = new PlayerControlsOwnership(
                configuration.ActorDefinition.ActorInstanceId,
                configuration.ActorDefinition.RunParticipantId);
            if (!attachments.Input.TryAcquire(ownership))
            {
                return Result(
                    PlayerSetupStatus.RejectedOwnership,
                    PlayerSetupRejectionCode.InputOwnershipUnavailable,
                    PlayerActorCreationRejectionCode.None,
                    null);
            }

            runtime = new PlayerSetup(actorCreation.Authority, attachments, ownership);
            return Result(PlayerSetupStatus.Constructed,
                PlayerSetupRejectionCode.None,
                PlayerActorCreationRejectionCode.None,
                runtime);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (runtime != null) runtime.Dispose();
        }

        private static PlayerSetupRejectionCode Validate(
            PlayerConfig configuration,
            PlayerParts attachments)
        {
            if (configuration == null) return PlayerSetupRejectionCode.MissingConfiguration;
            if (configuration.ActorDefinition == null) return PlayerSetupRejectionCode.MissingActorDefinition;
            if (attachments == null || attachments.Movement == null || attachments.Movement.IsDisposed)
                return PlayerSetupRejectionCode.MissingMovementAdapter;
            if (attachments.Presentation == null) return PlayerSetupRejectionCode.MissingPresentationAdapter;
            if (attachments.Input == null) return PlayerSetupRejectionCode.MissingInputAdapter;
            if (attachments.AttributionResolver == null) return PlayerSetupRejectionCode.MissingAttributionResolver;
            if (attachments.RunCoordinator == null) return PlayerSetupRejectionCode.MissingRunCoordinator;
            return PlayerSetupRejectionCode.None;
        }

        private static PlayerSetupResult Result(
            PlayerSetupStatus status,
            PlayerSetupRejectionCode rejection,
            PlayerActorCreationRejectionCode actorRejection,
            PlayerSetup runtime)
        {
            return new PlayerSetupResult(status, rejection, actorRejection, runtime);
        }
    }

    /// <summary>
    /// Composes health/death authority with separate movement, presentation, input and run ports.
    /// It has no reward, inventory, routing, scene lookup or global-player responsibility.
    /// </summary>
    public sealed class PlayerSetup : IDisposable
    {
        private readonly PlayerActorState authority;
        private readonly IPlayerMovement movement;
        private readonly IPlayerView presentation;
        private readonly IPlayerControls input;
        private readonly ITrustedPlayerAttributionResolver attribution;
        private readonly IPlayerRunFlow runCoordinator;
        private readonly PlayerControlsOwnership inputOwnership;
        private PlayerRestartCommand lastRestart;
        private bool disposed;

        internal PlayerSetup(
            PlayerActorState authority,
            PlayerParts attachments,
            PlayerControlsOwnership inputOwnership)
        {
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
            movement = attachments.Movement;
            presentation = attachments.Presentation;
            input = attachments.Input;
            attribution = attachments.AttributionResolver;
            runCoordinator = attachments.RunCoordinator;
            this.inputOwnership = inputOwnership ?? throw new ArgumentNullException(nameof(inputOwnership));
        }

        public bool IsDisposed { get { return disposed; } }

        public PlayerSnapshot ExportSnapshot()
        {
            ThrowIfDisposed();
            return new PlayerSnapshot(authority.ExportSnapshot(), movement.ExportSnapshot());
        }

        public PlayerHudHealthSnapshot ExportHudHealth()
        {
            ThrowIfDisposed();
            return PlayerHudHealthProjector.Project(authority.ExportSnapshot());
        }

        public DamageReceiverResult ApplyDamage(PlayerDamageRequest request)
        {
            ThrowIfDisposed();
            DamageReceiverCommand command = null;
            if (request != null)
            {
                StableId trusted = request.SourceActorId == null
                    ? null
                    : attribution.ResolveSourceRunParticipant(request.SourceActorId);
                command = new DamageReceiverCommand(
                    request.EventId,
                    request.SourceActorId,
                    trusted,
                    request.TargetActorId,
                    request.Amount,
                    request.Channel,
                    request.LifecycleGeneration);
            }

            DamageReceiverResult result = authority.ApplyDamage(command);
            if (result.DeathFact != null) runCoordinator.ObservePlayerDeath(result.DeathFact);
            return result;
        }

        public PlayerActorHealingResult ApplyHealing(PlayerHealingRequest request)
        {
            ThrowIfDisposed();
            PlayerActorHealingCommand command = null;
            if (request != null)
            {
                StableId trusted = request.SourceActorId == null
                    ? null
                    : attribution.ResolveSourceRunParticipant(request.SourceActorId);
                command = new PlayerActorHealingCommand(
                    request.OperationId,
                    request.SourceActorId,
                    trusted,
                    request.TargetActorId,
                    request.Amount,
                    request.LifecycleGeneration);
            }

            return authority.ApplyHealing(command);
        }

        public bool RefreshContinuousPresentation()
        {
            ThrowIfDisposed();
            presentation.RefreshContinuousBoost(movement.ExportSnapshot());
            return true;
        }

        public PlayerRestartResult Restart(PlayerRestartCommand command)
        {
            if (disposed)
            {
                return RestartResult(PlayerRestartStatus.RejectedByLifecycle,
                    PlayerRestartRejectionCode.Disposed, command, null);
            }

            if (lastRestart != null && command != null && lastRestart.OperationId == command.OperationId)
            {
                bool exact = lastRestart.Equals(command);
                return RestartResult(
                    exact ? PlayerRestartStatus.Duplicate : PlayerRestartStatus.RejectedInvalid,
                    exact ? PlayerRestartRejectionCode.None : PlayerRestartRejectionCode.ConflictingDuplicate,
                    command,
                    ExportSnapshot());
            }

            PlayerSnapshot before = ExportSnapshot();
            PlayerRestartRejectionCode invalid = ValidateRestart(command, before);
            if (invalid != PlayerRestartRejectionCode.None)
            {
                bool lifecycle = invalid == PlayerRestartRejectionCode.StaleGeneration
                    || invalid == PlayerRestartRejectionCode.FutureGeneration
                    || invalid == PlayerRestartRejectionCode.MovementGenerationMismatch;
                return RestartResult(
                    lifecycle ? PlayerRestartStatus.RejectedByLifecycle : PlayerRestartStatus.RejectedInvalid,
                    invalid,
                    command,
                    before);
            }

            if (!movement.TryRestart(command.RetiringGeneration, command.ReplacementGeneration))
            {
                return RestartResult(PlayerRestartStatus.RejectedByMovement,
                    PlayerRestartRejectionCode.MovementRejected, command, ExportSnapshot());
            }

            PlayerActorRestartResult actorResult = authority.Restart(new PlayerActorRestartCommand(
                command.OperationId,
                command.TargetActorId,
                command.RetiringGeneration,
                command.ReplacementGeneration));
            if (actorResult.Status != PlayerActorOperationStatus.Applied)
            {
                throw new InvalidOperationException(
                    "Player authority rejected a restart after coordinated movement acceptance.");
            }

            lastRestart = command;
            PlayerSnapshot after = ExportSnapshot();
            presentation.Restart(after);
            return RestartResult(PlayerRestartStatus.Applied,
                PlayerRestartRejectionCode.None, command, after);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { input.Release(inputOwnership); }
            finally
            {
                try { presentation.Dispose(); }
                finally
                {
                    try { movement.Dispose(); }
                    finally { input.Dispose(); }
                }
            }
        }

        private static PlayerRestartRejectionCode ValidateRestart(
            PlayerRestartCommand command,
            PlayerSnapshot current)
        {
            if (command == null) return PlayerRestartRejectionCode.NullCommand;
            if (command.OperationId == null) return PlayerRestartRejectionCode.MissingOperationId;
            if (command.TargetActorId == null) return PlayerRestartRejectionCode.MissingTargetActorId;
            if (command.TargetActorId != current.Player.ActorInstanceId) return PlayerRestartRejectionCode.TargetMismatch;
            if (command.RetiringGeneration < 0L || command.ReplacementGeneration < 0L)
                return PlayerRestartRejectionCode.InvalidGeneration;
            if (current.Player.LifecycleGeneration != current.Movement.Generation)
                return PlayerRestartRejectionCode.MovementGenerationMismatch;
            if (command.RetiringGeneration < current.Player.LifecycleGeneration)
                return PlayerRestartRejectionCode.StaleGeneration;
            if (command.RetiringGeneration > current.Player.LifecycleGeneration)
                return PlayerRestartRejectionCode.FutureGeneration;
            if (command.RetiringGeneration == long.MaxValue
                || command.ReplacementGeneration != command.RetiringGeneration + 1L)
                return PlayerRestartRejectionCode.ReplacementGenerationMustIncrement;
            return PlayerRestartRejectionCode.None;
        }

        private static PlayerRestartResult RestartResult(
            PlayerRestartStatus status,
            PlayerRestartRejectionCode rejection,
            PlayerRestartCommand command,
            PlayerSnapshot snapshot)
        {
            return new PlayerRestartResult(status, rejection, command, snapshot);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayerSetup));
        }
    }
}

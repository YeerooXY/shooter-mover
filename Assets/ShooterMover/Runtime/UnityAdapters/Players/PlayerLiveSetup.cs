using System;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;

namespace ShooterMover.UnityAdapters.Players
{
    /// <summary>Exactly-once, non-static construction owner for one player runtime.</summary>
    public sealed class PlayerLiveSetupRoot : IDisposable
    {
        private PlayerLiveSetup runtime;
        private bool disposed;

        public bool IsConstructed { get { return runtime != null; } }
        public PlayerLiveSetup Runtime { get { return runtime; } }

        public PlayerLiveConstructionResult TryConstruct(
            PlayerLiveConfiguration configuration,
            PlayerLiveAttachments attachments)
        {
            if (runtime != null || disposed)
            {
                return Result(
                    PlayerLiveConstructionStatus.RejectedDuplicate,
                    PlayerLiveConstructionRejectionCode.AlreadyConstructed,
                    PlayerActorCreationRejectionCode.None,
                    runtime);
            }

            PlayerLiveConstructionRejectionCode invalid = Validate(configuration, attachments);
            if (invalid != PlayerLiveConstructionRejectionCode.None)
            {
                return Result(PlayerLiveConstructionStatus.RejectedInvalid, invalid,
                    PlayerActorCreationRejectionCode.None, null);
            }

            PlayerActorCreationResult actorCreation = PlayerActorState.TryCreate(
                configuration.ActorDefinition);
            if (!actorCreation.IsCreated)
            {
                return Result(
                    PlayerLiveConstructionStatus.RejectedInvalid,
                    PlayerLiveConstructionRejectionCode.ActorDefinitionRejected,
                    actorCreation.RejectionCode,
                    null);
            }

            PlayerMovementSnapshot movement = attachments.Movement.ExportSnapshot();
            if (movement.Generation != configuration.ActorDefinition.InitialLifecycleGeneration)
            {
                return Result(
                    PlayerLiveConstructionStatus.RejectedInvalid,
                    PlayerLiveConstructionRejectionCode.InitialGenerationMismatch,
                    PlayerActorCreationRejectionCode.None,
                    null);
            }

            PlayerInputOwnership ownership = new PlayerInputOwnership(
                configuration.ActorDefinition.ActorInstanceId,
                configuration.ActorDefinition.RunParticipantId);
            if (!attachments.Input.TryAcquire(ownership))
            {
                return Result(
                    PlayerLiveConstructionStatus.RejectedOwnership,
                    PlayerLiveConstructionRejectionCode.InputOwnershipUnavailable,
                    PlayerActorCreationRejectionCode.None,
                    null);
            }

            runtime = new PlayerLiveSetup(actorCreation.Authority, attachments, ownership);
            return Result(PlayerLiveConstructionStatus.Constructed,
                PlayerLiveConstructionRejectionCode.None,
                PlayerActorCreationRejectionCode.None,
                runtime);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (runtime != null) runtime.Dispose();
        }

        private static PlayerLiveConstructionRejectionCode Validate(
            PlayerLiveConfiguration configuration,
            PlayerLiveAttachments attachments)
        {
            if (configuration == null) return PlayerLiveConstructionRejectionCode.MissingConfiguration;
            if (configuration.ActorDefinition == null) return PlayerLiveConstructionRejectionCode.MissingActorDefinition;
            if (attachments == null || attachments.Movement == null || attachments.Movement.IsDisposed)
                return PlayerLiveConstructionRejectionCode.MissingMovementAdapter;
            if (attachments.Presentation == null) return PlayerLiveConstructionRejectionCode.MissingPresentationAdapter;
            if (attachments.Input == null) return PlayerLiveConstructionRejectionCode.MissingInputAdapter;
            if (attachments.AttributionResolver == null) return PlayerLiveConstructionRejectionCode.MissingAttributionResolver;
            if (attachments.RunCoordinator == null) return PlayerLiveConstructionRejectionCode.MissingRunCoordinator;
            return PlayerLiveConstructionRejectionCode.None;
        }

        private static PlayerLiveConstructionResult Result(
            PlayerLiveConstructionStatus status,
            PlayerLiveConstructionRejectionCode rejection,
            PlayerActorCreationRejectionCode actorRejection,
            PlayerLiveSetup runtime)
        {
            return new PlayerLiveConstructionResult(status, rejection, actorRejection, runtime);
        }
    }

    /// <summary>
    /// Composes health/death authority with separate movement, presentation, input and run ports.
    /// It has no reward, inventory, routing, scene lookup or global-player responsibility.
    /// </summary>
    public sealed class PlayerLiveSetup : IDisposable
    {
        private readonly PlayerActorState authority;
        private readonly IPlayerMovementLive movement;
        private readonly IPlayerPresentationLive presentation;
        private readonly IPlayerInputLive input;
        private readonly ITrustedPlayerAttributionResolver attribution;
        private readonly IPlayerRunFlow runCoordinator;
        private readonly PlayerInputOwnership inputOwnership;
        private PlayerLiveRestartCommand lastRestart;
        private bool disposed;

        internal PlayerLiveSetup(
            PlayerActorState authority,
            PlayerLiveAttachments attachments,
            PlayerInputOwnership inputOwnership)
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

        public PlayerLiveSnapshot ExportSnapshot()
        {
            ThrowIfDisposed();
            return new PlayerLiveSnapshot(authority.ExportSnapshot(), movement.ExportSnapshot());
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

        public PlayerLiveRestartResult Restart(PlayerLiveRestartCommand command)
        {
            if (disposed)
            {
                return RestartResult(PlayerLiveRestartStatus.RejectedByLifecycle,
                    PlayerLiveRestartRejectionCode.Disposed, command, null);
            }

            if (lastRestart != null && command != null && lastRestart.OperationId == command.OperationId)
            {
                bool exact = lastRestart.Equals(command);
                return RestartResult(
                    exact ? PlayerLiveRestartStatus.Duplicate : PlayerLiveRestartStatus.RejectedInvalid,
                    exact ? PlayerLiveRestartRejectionCode.None : PlayerLiveRestartRejectionCode.ConflictingDuplicate,
                    command,
                    ExportSnapshot());
            }

            PlayerLiveSnapshot before = ExportSnapshot();
            PlayerLiveRestartRejectionCode invalid = ValidateRestart(command, before);
            if (invalid != PlayerLiveRestartRejectionCode.None)
            {
                bool lifecycle = invalid == PlayerLiveRestartRejectionCode.StaleGeneration
                    || invalid == PlayerLiveRestartRejectionCode.FutureGeneration
                    || invalid == PlayerLiveRestartRejectionCode.MovementGenerationMismatch;
                return RestartResult(
                    lifecycle ? PlayerLiveRestartStatus.RejectedByLifecycle : PlayerLiveRestartStatus.RejectedInvalid,
                    invalid,
                    command,
                    before);
            }

            if (!movement.TryRestart(command.RetiringGeneration, command.ReplacementGeneration))
            {
                return RestartResult(PlayerLiveRestartStatus.RejectedByMovement,
                    PlayerLiveRestartRejectionCode.MovementRejected, command, ExportSnapshot());
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
            PlayerLiveSnapshot after = ExportSnapshot();
            presentation.Restart(after);
            return RestartResult(PlayerLiveRestartStatus.Applied,
                PlayerLiveRestartRejectionCode.None, command, after);
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

        private static PlayerLiveRestartRejectionCode ValidateRestart(
            PlayerLiveRestartCommand command,
            PlayerLiveSnapshot current)
        {
            if (command == null) return PlayerLiveRestartRejectionCode.NullCommand;
            if (command.OperationId == null) return PlayerLiveRestartRejectionCode.MissingOperationId;
            if (command.TargetActorId == null) return PlayerLiveRestartRejectionCode.MissingTargetActorId;
            if (command.TargetActorId != current.Player.ActorInstanceId) return PlayerLiveRestartRejectionCode.TargetMismatch;
            if (command.RetiringGeneration < 0L || command.ReplacementGeneration < 0L)
                return PlayerLiveRestartRejectionCode.InvalidGeneration;
            if (current.Player.LifecycleGeneration != current.Movement.Generation)
                return PlayerLiveRestartRejectionCode.MovementGenerationMismatch;
            if (command.RetiringGeneration < current.Player.LifecycleGeneration)
                return PlayerLiveRestartRejectionCode.StaleGeneration;
            if (command.RetiringGeneration > current.Player.LifecycleGeneration)
                return PlayerLiveRestartRejectionCode.FutureGeneration;
            if (command.RetiringGeneration == long.MaxValue
                || command.ReplacementGeneration != command.RetiringGeneration + 1L)
                return PlayerLiveRestartRejectionCode.ReplacementGenerationMustIncrement;
            return PlayerLiveRestartRejectionCode.None;
        }

        private static PlayerLiveRestartResult RestartResult(
            PlayerLiveRestartStatus status,
            PlayerLiveRestartRejectionCode rejection,
            PlayerLiveRestartCommand command,
            PlayerLiveSnapshot snapshot)
        {
            return new PlayerLiveRestartResult(status, rejection, command, snapshot);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayerLiveSetup));
        }
    }
}

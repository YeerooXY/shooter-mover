using System;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;

namespace ShooterMover.UnityAdapters.Players
{
    /// <summary>Exactly-once, non-static construction owner for one player runtime.</summary>
    public sealed class PlayerRuntimeCompositionRoot : IDisposable
    {
        private PlayerRuntimeComposition runtime;
        private bool disposed;

        public bool IsConstructed { get { return runtime != null; } }
        public PlayerRuntimeComposition Runtime { get { return runtime; } }

        public PlayerRuntimeConstructionResult TryConstruct(
            PlayerRuntimeConfiguration configuration,
            PlayerRuntimeAttachments attachments)
        {
            if (runtime != null || disposed)
            {
                return Result(
                    PlayerRuntimeConstructionStatus.RejectedDuplicate,
                    PlayerRuntimeConstructionRejectionCode.AlreadyConstructed,
                    PlayerActorCreationRejectionCode.None,
                    runtime);
            }

            PlayerRuntimeConstructionRejectionCode invalid = Validate(configuration, attachments);
            if (invalid != PlayerRuntimeConstructionRejectionCode.None)
            {
                return Result(PlayerRuntimeConstructionStatus.RejectedInvalid, invalid,
                    PlayerActorCreationRejectionCode.None, null);
            }

            PlayerActorCreationResult actorCreation = PlayerActorAuthority.TryCreate(
                configuration.ActorDefinition);
            if (!actorCreation.IsCreated)
            {
                return Result(
                    PlayerRuntimeConstructionStatus.RejectedInvalid,
                    PlayerRuntimeConstructionRejectionCode.ActorDefinitionRejected,
                    actorCreation.RejectionCode,
                    null);
            }

            PlayerMovementSnapshot movement = attachments.Movement.ExportSnapshot();
            if (movement.Generation != configuration.ActorDefinition.InitialLifecycleGeneration)
            {
                return Result(
                    PlayerRuntimeConstructionStatus.RejectedInvalid,
                    PlayerRuntimeConstructionRejectionCode.InitialGenerationMismatch,
                    PlayerActorCreationRejectionCode.None,
                    null);
            }

            PlayerInputOwnership ownership = new PlayerInputOwnership(
                configuration.ActorDefinition.ActorInstanceId,
                configuration.ActorDefinition.RunParticipantId);
            if (!attachments.Input.TryAcquire(ownership))
            {
                return Result(
                    PlayerRuntimeConstructionStatus.RejectedOwnership,
                    PlayerRuntimeConstructionRejectionCode.InputOwnershipUnavailable,
                    PlayerActorCreationRejectionCode.None,
                    null);
            }

            runtime = new PlayerRuntimeComposition(actorCreation.Authority, attachments, ownership);
            return Result(PlayerRuntimeConstructionStatus.Constructed,
                PlayerRuntimeConstructionRejectionCode.None,
                PlayerActorCreationRejectionCode.None,
                runtime);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (runtime != null) runtime.Dispose();
        }

        private static PlayerRuntimeConstructionRejectionCode Validate(
            PlayerRuntimeConfiguration configuration,
            PlayerRuntimeAttachments attachments)
        {
            if (configuration == null) return PlayerRuntimeConstructionRejectionCode.MissingConfiguration;
            if (configuration.ActorDefinition == null) return PlayerRuntimeConstructionRejectionCode.MissingActorDefinition;
            if (attachments == null || attachments.Movement == null || attachments.Movement.IsDisposed)
                return PlayerRuntimeConstructionRejectionCode.MissingMovementAdapter;
            if (attachments.Presentation == null) return PlayerRuntimeConstructionRejectionCode.MissingPresentationAdapter;
            if (attachments.Input == null) return PlayerRuntimeConstructionRejectionCode.MissingInputAdapter;
            if (attachments.AttributionResolver == null) return PlayerRuntimeConstructionRejectionCode.MissingAttributionResolver;
            if (attachments.RunCoordinator == null) return PlayerRuntimeConstructionRejectionCode.MissingRunCoordinator;
            return PlayerRuntimeConstructionRejectionCode.None;
        }

        private static PlayerRuntimeConstructionResult Result(
            PlayerRuntimeConstructionStatus status,
            PlayerRuntimeConstructionRejectionCode rejection,
            PlayerActorCreationRejectionCode actorRejection,
            PlayerRuntimeComposition runtime)
        {
            return new PlayerRuntimeConstructionResult(status, rejection, actorRejection, runtime);
        }
    }

    /// <summary>
    /// Composes health/death authority with separate movement, presentation, input and run ports.
    /// It has no reward, inventory, routing, scene lookup or global-player responsibility.
    /// </summary>
    public sealed class PlayerRuntimeComposition : IDisposable
    {
        private readonly PlayerActorAuthority authority;
        private readonly IPlayerMovementRuntime movement;
        private readonly IPlayerPresentationRuntime presentation;
        private readonly IPlayerInputRuntime input;
        private readonly ITrustedPlayerAttributionResolver attribution;
        private readonly IPlayerRunCoordinator runCoordinator;
        private readonly PlayerInputOwnership inputOwnership;
        private PlayerRuntimeRestartCommand lastRestart;
        private bool disposed;

        internal PlayerRuntimeComposition(
            PlayerActorAuthority authority,
            PlayerRuntimeAttachments attachments,
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

        public PlayerRuntimeSnapshot ExportSnapshot()
        {
            ThrowIfDisposed();
            return new PlayerRuntimeSnapshot(authority.ExportSnapshot(), movement.ExportSnapshot());
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

        public PlayerRuntimeRestartResult Restart(PlayerRuntimeRestartCommand command)
        {
            if (disposed)
            {
                return RestartResult(PlayerRuntimeRestartStatus.RejectedByLifecycle,
                    PlayerRuntimeRestartRejectionCode.Disposed, command, null);
            }

            if (lastRestart != null && command != null && lastRestart.OperationId == command.OperationId)
            {
                bool exact = lastRestart.Equals(command);
                return RestartResult(
                    exact ? PlayerRuntimeRestartStatus.Duplicate : PlayerRuntimeRestartStatus.RejectedInvalid,
                    exact ? PlayerRuntimeRestartRejectionCode.None : PlayerRuntimeRestartRejectionCode.ConflictingDuplicate,
                    command,
                    ExportSnapshot());
            }

            PlayerRuntimeSnapshot before = ExportSnapshot();
            PlayerRuntimeRestartRejectionCode invalid = ValidateRestart(command, before);
            if (invalid != PlayerRuntimeRestartRejectionCode.None)
            {
                bool lifecycle = invalid == PlayerRuntimeRestartRejectionCode.StaleGeneration
                    || invalid == PlayerRuntimeRestartRejectionCode.FutureGeneration
                    || invalid == PlayerRuntimeRestartRejectionCode.MovementGenerationMismatch;
                return RestartResult(
                    lifecycle ? PlayerRuntimeRestartStatus.RejectedByLifecycle : PlayerRuntimeRestartStatus.RejectedInvalid,
                    invalid,
                    command,
                    before);
            }

            if (!movement.TryRestart(command.RetiringGeneration, command.ReplacementGeneration))
            {
                return RestartResult(PlayerRuntimeRestartStatus.RejectedByMovement,
                    PlayerRuntimeRestartRejectionCode.MovementRejected, command, ExportSnapshot());
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
            PlayerRuntimeSnapshot after = ExportSnapshot();
            presentation.Restart(after);
            return RestartResult(PlayerRuntimeRestartStatus.Applied,
                PlayerRuntimeRestartRejectionCode.None, command, after);
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

        private static PlayerRuntimeRestartRejectionCode ValidateRestart(
            PlayerRuntimeRestartCommand command,
            PlayerRuntimeSnapshot current)
        {
            if (command == null) return PlayerRuntimeRestartRejectionCode.NullCommand;
            if (command.OperationId == null) return PlayerRuntimeRestartRejectionCode.MissingOperationId;
            if (command.TargetActorId == null) return PlayerRuntimeRestartRejectionCode.MissingTargetActorId;
            if (command.TargetActorId != current.Player.ActorInstanceId) return PlayerRuntimeRestartRejectionCode.TargetMismatch;
            if (command.RetiringGeneration < 0L || command.ReplacementGeneration < 0L)
                return PlayerRuntimeRestartRejectionCode.InvalidGeneration;
            if (current.Player.LifecycleGeneration != current.Movement.Generation)
                return PlayerRuntimeRestartRejectionCode.MovementGenerationMismatch;
            if (command.RetiringGeneration < current.Player.LifecycleGeneration)
                return PlayerRuntimeRestartRejectionCode.StaleGeneration;
            if (command.RetiringGeneration > current.Player.LifecycleGeneration)
                return PlayerRuntimeRestartRejectionCode.FutureGeneration;
            if (command.RetiringGeneration == long.MaxValue
                || command.ReplacementGeneration != command.RetiringGeneration + 1L)
                return PlayerRuntimeRestartRejectionCode.ReplacementGenerationMustIncrement;
            return PlayerRuntimeRestartRejectionCode.None;
        }

        private static PlayerRuntimeRestartResult RestartResult(
            PlayerRuntimeRestartStatus status,
            PlayerRuntimeRestartRejectionCode rejection,
            PlayerRuntimeRestartCommand command,
            PlayerRuntimeSnapshot snapshot)
        {
            return new PlayerRuntimeRestartResult(status, rejection, command, snapshot);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayerRuntimeComposition));
        }
    }
}

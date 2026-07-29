using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    /// <summary>
    /// Legacy execution authority retained only for existing tooling and EditMode regression
    /// fixtures. Production gameplay must use GunFiringScheduler through the live inventory
    /// composition and must not construct this type.
    /// </summary>
    [Obsolete(
        "Legacy tooling/test authority only. Production firing uses GunFiringScheduler.",
        false)]
    public sealed partial class GunExecutionCore
    {
        private readonly IGunActorOwnershipResolver ownershipResolver;
        private readonly IEquippedGunInstanceResolver equippedResolver;
        private readonly GunCatalogLiveProfileResolver profileResolver;
        private readonly GunBehaviorRegistry behaviorRegistry;
        private readonly IGunEffectBatchSink effectSink;
        private readonly Dictionary<StateKey, FireState> states =
            new Dictionary<StateKey, FireState>();
        private readonly Dictionary<OperationKey, AcceptedFireOperation> acceptedOperations =
            new Dictionary<OperationKey, AcceptedFireOperation>();

        public GunExecutionCore(
            IGunActorOwnershipResolver ownershipResolver,
            IEquippedGunInstanceResolver equippedResolver,
            GunCatalogLiveProfileResolver profileResolver,
            GunBehaviorRegistry behaviorRegistry,
            IGunEffectBatchSink effectSink)
        {
            this.ownershipResolver = ownershipResolver
                ?? throw new ArgumentNullException(nameof(ownershipResolver));
            this.equippedResolver = equippedResolver
                ?? throw new ArgumentNullException(nameof(equippedResolver));
            this.profileResolver = profileResolver
                ?? throw new ArgumentNullException(nameof(profileResolver));
            this.behaviorRegistry = behaviorRegistry
                ?? throw new ArgumentNullException(nameof(behaviorRegistry));
            this.effectSink = effectSink
                ?? throw new ArgumentNullException(nameof(effectSink));
        }

        public GunExecutionResult TryExecute(GunFireCommand command)
        {
            if (!IsValidCommand(command))
            {
                return GunExecutionResult.Reject(
                    GunExecutionStatus.InvalidCommand,
                    "gun-command-invalid",
                    0L);
            }

            RunParticipantId participant;
            if (!ownershipResolver.TryResolveParticipant(
                    command.ActorId,
                    command.LifecycleGeneration,
                    out participant)
                || participant == null)
            {
                return GunExecutionResult.Reject(
                    GunExecutionStatus.UnknownActorOwnership,
                    "gun-actor-ownership-unresolved",
                    0L);
            }

            OperationKey operationKey = new OperationKey(
                command.ActorId,
                command.LifecycleGeneration,
                command.FireOperationId);
            AcceptedFireOperation acceptedOperation;
            bool hasAcceptedOperation = acceptedOperations.TryGetValue(
                operationKey,
                out acceptedOperation);
            if (hasAcceptedOperation
                && !acceptedOperation.MatchesCommand(command.Fingerprint))
            {
                return GunExecutionResult.Reject(
                    GunExecutionStatus.ConflictingDuplicate,
                    "gun-operation-conflicting-duplicate",
                    acceptedOperation.ShotSequence);
            }

            StateKey stateKey = new StateKey(
                command.ActorId,
                command.EquipmentInstanceId,
                command.LifecycleGeneration);
            FireState state;
            if (!states.TryGetValue(stateKey, out state))
            {
                state = FireState.Initial;
            }

            EquipmentInstance instance;
            if (!equippedResolver.TryResolveEquippedGun(
                    command.ActorId,
                    command.EquipmentInstanceId,
                    out instance)
                || instance == null)
            {
                return GunExecutionResult.Reject(
                    GunExecutionStatus.MissingEquippedEquipment,
                    "gun-equipment-not-equipped",
                    state.ShotSequence);
            }

            GunProfileResolution profile = profileResolver.Resolve(
                command.EquipmentInstanceId,
                instance);
            if (!profile.Succeeded)
            {
                return GunExecutionResult.Reject(
                    Map(profile.Status),
                    profile.RejectionCode,
                    state.ShotSequence);
            }

            IGunBehavior behavior;
            if (!behaviorRegistry.TryResolve(profile.Profile.BehaviorId, out behavior)
                || behavior == null)
            {
                return GunExecutionResult.Reject(
                    GunExecutionStatus.UnknownBehavior,
                    "gun-behavior-unregistered:" + profile.Profile.BehaviorId,
                    state.ShotSequence);
            }

            if (hasAcceptedOperation)
            {
                BatchBuildResult replayBuild = BuildBatch(
                    command,
                    participant,
                    profile.Profile,
                    behavior,
                    acceptedOperation.ShotSequence);
                if (replayBuild.Succeeded
                    && acceptedOperation.MatchesBatch(replayBuild.Batch.Fingerprint))
                {
                    return GunExecutionResult.Replay(
                        acceptedOperation.EffectCount,
                        acceptedOperation.ShotSequence);
                }

                return GunExecutionResult.Reject(
                    GunExecutionStatus.ConflictingDuplicate,
                    "gun-operation-conflicting-duplicate",
                    acceptedOperation.ShotSequence);
            }

            if (command.SimulationTick < state.NextAllowedTick)
            {
                return GunExecutionResult.Reject(
                    GunExecutionStatus.CooldownActive,
                    "gun-cooldown-active",
                    state.ShotSequence);
            }

            BatchBuildResult build = BuildBatch(
                command,
                participant,
                profile.Profile,
                behavior,
                state.ShotSequence);
            if (!build.Succeeded)
            {
                return GunExecutionResult.Reject(
                    build.Status,
                    build.RejectionCode,
                    state.ShotSequence);
            }

            GunEffectBatchSinkResult acceptance;
            try
            {
                acceptance = effectSink.TryAccept(build.Batch);
            }
            catch
            {
                return GunExecutionResult.Reject(
                    GunExecutionStatus.SinkRejected,
                    "gun-effect-sink-exception",
                    state.ShotSequence);
            }

            if (acceptance == null || !acceptance.IsAcceptance)
            {
                return GunExecutionResult.Reject(
                    GunExecutionStatus.SinkRejected,
                    acceptance == null
                        ? "gun-effect-sink-null-result"
                        : acceptance.RejectionCode,
                    state.ShotSequence);
            }

            AcceptedFireOperation committedOperation = new AcceptedFireOperation(
                command.EquipmentInstanceId,
                command.Fingerprint,
                build.Batch.Fingerprint,
                state.ShotSequence,
                build.Batch.EffectCount);
            acceptedOperations.Add(operationKey, committedOperation);
            states[stateKey] = state.AfterAccepted(
                command.SimulationTick + profile.Profile.CooldownTicks);
            return GunExecutionResult.Accept(
                build.Batch.EffectCount,
                state.ShotSequence);
        }
    }
}

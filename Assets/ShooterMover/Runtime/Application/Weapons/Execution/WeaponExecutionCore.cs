using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Legacy execution authority retained only for existing tooling and EditMode regression
    /// fixtures. Production gameplay must use WeaponFiringScheduler through the live inventory
    /// composition and must not construct this type.
    /// </summary>
    [Obsolete(
        "Legacy tooling/test authority only. Production firing uses WeaponFiringScheduler.",
        false)]
    public sealed partial class WeaponExecutionCore
    {
        private readonly IWeaponActorOwnershipResolver ownershipResolver;
        private readonly IEquippedWeaponInstanceResolver equippedResolver;
        private readonly WeaponCatalogRuntimeProfileResolver profileResolver;
        private readonly WeaponBehaviorRegistry behaviorRegistry;
        private readonly IWeaponEffectBatchSink effectSink;
        private readonly Dictionary<StateKey, FireState> states =
            new Dictionary<StateKey, FireState>();
        private readonly Dictionary<OperationKey, AcceptedFireOperation> acceptedOperations =
            new Dictionary<OperationKey, AcceptedFireOperation>();

        public WeaponExecutionCore(
            IWeaponActorOwnershipResolver ownershipResolver,
            IEquippedWeaponInstanceResolver equippedResolver,
            WeaponCatalogRuntimeProfileResolver profileResolver,
            WeaponBehaviorRegistry behaviorRegistry,
            IWeaponEffectBatchSink effectSink)
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

        public WeaponExecutionResult TryExecute(WeaponFireCommand command)
        {
            if (!IsValidCommand(command))
            {
                return WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.InvalidCommand,
                    "weapon-command-invalid",
                    0L);
            }

            RunParticipantId participant;
            if (!ownershipResolver.TryResolveParticipant(
                    command.ActorId,
                    command.LifecycleGeneration,
                    out participant)
                || participant == null)
            {
                return WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.UnknownActorOwnership,
                    "weapon-actor-ownership-unresolved",
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
                return WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.ConflictingDuplicate,
                    "weapon-operation-conflicting-duplicate",
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
            if (!equippedResolver.TryResolveEquippedWeapon(
                    command.ActorId,
                    command.EquipmentInstanceId,
                    out instance)
                || instance == null)
            {
                return WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.MissingEquippedEquipment,
                    "weapon-equipment-not-equipped",
                    state.ShotSequence);
            }

            WeaponProfileResolution profile = profileResolver.Resolve(
                command.EquipmentInstanceId,
                instance);
            if (!profile.Succeeded)
            {
                return WeaponExecutionResult.Reject(
                    Map(profile.Status),
                    profile.RejectionCode,
                    state.ShotSequence);
            }

            IWeaponBehavior behavior;
            if (!behaviorRegistry.TryResolve(profile.Profile.BehaviorId, out behavior)
                || behavior == null)
            {
                return WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.UnknownBehavior,
                    "weapon-behavior-unregistered:" + profile.Profile.BehaviorId,
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
                    return WeaponExecutionResult.Replay(
                        acceptedOperation.EffectCount,
                        acceptedOperation.ShotSequence);
                }

                return WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.ConflictingDuplicate,
                    "weapon-operation-conflicting-duplicate",
                    acceptedOperation.ShotSequence);
            }

            if (command.SimulationTick < state.NextAllowedTick)
            {
                return WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.CooldownActive,
                    "weapon-cooldown-active",
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
                return WeaponExecutionResult.Reject(
                    build.Status,
                    build.RejectionCode,
                    state.ShotSequence);
            }

            WeaponEffectBatchSinkResult acceptance;
            try
            {
                acceptance = effectSink.TryAccept(build.Batch);
            }
            catch
            {
                return WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.SinkRejected,
                    "weapon-effect-sink-exception",
                    state.ShotSequence);
            }

            if (acceptance == null || !acceptance.IsAcceptance)
            {
                return WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.SinkRejected,
                    acceptance == null
                        ? "weapon-effect-sink-null-result"
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
            return WeaponExecutionResult.Accept(
                build.Batch.EffectCount,
                state.ShotSequence);
        }
    }
}

using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum WeaponExecutionStatus
    {
        Accepted = 1,
        InvalidCommand = 2,
        UnknownActorOwnership = 3,
        MissingEquippedEquipment = 4,
        InvalidEquipment = 5,
        UnknownWeaponDefinition = 6,
        PreviewOnlyWeaponDefinition = 7,
        InvalidTuning = 8,
        UnsupportedEffects = 9,
        UnknownBehavior = 10,
        CooldownActive = 11,
        ReplayAccepted = 12,
        BehaviorRejected = 13,
        InvalidEffectBatch = 14,
        SinkRejected = 15,
        ConflictingDuplicate = 16,
    }

    public sealed class WeaponExecutionResult
    {
        private WeaponExecutionResult(
            WeaponExecutionStatus status,
            string rejectionCode,
            int effectCount,
            long shotSequence)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            EffectCount = effectCount;
            ShotSequence = shotSequence;
        }

        public WeaponExecutionStatus Status { get; }
        public string RejectionCode { get; }
        public int EffectCount { get; }
        public long ShotSequence { get; }
        public bool Succeeded { get { return Status == WeaponExecutionStatus.Accepted; } }

        public static WeaponExecutionResult Accept(int count, long sequence)
        {
            return new WeaponExecutionResult(
                WeaponExecutionStatus.Accepted,
                string.Empty,
                count,
                sequence);
        }

        public static WeaponExecutionResult Reject(
            WeaponExecutionStatus status,
            string code,
            long sequence)
        {
            if (status == WeaponExecutionStatus.Accepted)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new WeaponExecutionResult(status, code, 0, sequence);
        }
    }

    public sealed class WeaponExecutionCore
    {
        private readonly IWeaponActorOwnershipResolver ownershipResolver;
        private readonly IEquippedWeaponInstanceResolver equippedResolver;
        private readonly WeaponCatalogRuntimeProfileResolver profileResolver;
        private readonly WeaponBehaviorRegistry behaviorRegistry;
        private readonly IWeaponEffectBatchSink effectSink;
        private readonly Dictionary<StateKey, FireState> states =
            new Dictionary<StateKey, FireState>();

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

            StateKey key = new StateKey(
                command.ActorId,
                command.EquipmentInstanceId,
                command.LifecycleGeneration);
            FireState state;
            if (!states.TryGetValue(key, out state))
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

            AcceptedFireOperation acceptedOperation;
            if (state.TryGetAccepted(command.FireOperationId, out acceptedOperation))
            {
                BatchBuildResult replayBuild = BuildBatch(
                    command,
                    participant,
                    profile.Profile,
                    behavior,
                    acceptedOperation.ShotSequence);
                if (replayBuild.Succeeded
                    && acceptedOperation.Matches(
                        command.Fingerprint,
                        replayBuild.Batch.Fingerprint))
                {
                    return WeaponExecutionResult.Reject(
                        WeaponExecutionStatus.ReplayAccepted,
                        "weapon-operation-already-accepted",
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
                command.FireOperationId,
                command.Fingerprint,
                build.Batch.Fingerprint,
                state.ShotSequence);
            states[key] = state.AfterAccepted(
                committedOperation,
                command.SimulationTick + profile.Profile.CooldownTicks);
            return WeaponExecutionResult.Accept(
                build.Batch.EffectCount,
                state.ShotSequence);
        }

        private static BatchBuildResult BuildBatch(
            WeaponFireCommand command,
            RunParticipantId participant,
            WeaponRuntimeFiringProfile profile,
            IWeaponBehavior behavior,
            long shotSequence)
        {
            WeaponBehaviorBuildResult built;
            try
            {
                built = behavior.Build(
                    new WeaponBehaviorContext(
                        command,
                        participant,
                        profile,
                        shotSequence));
            }
            catch
            {
                return BatchBuildResult.Reject(
                    WeaponExecutionStatus.BehaviorRejected,
                    "weapon-behavior-exception");
            }

            if (built == null || !built.Succeeded)
            {
                return BatchBuildResult.Reject(
                    WeaponExecutionStatus.BehaviorRejected,
                    built == null
                        ? "weapon-behavior-null-result"
                        : built.RejectionCode);
            }

            string batchCode;
            if (!ValidateBatch(
                    command,
                    participant,
                    profile,
                    shotSequence,
                    built.Batch,
                    out batchCode))
            {
                return BatchBuildResult.Reject(
                    WeaponExecutionStatus.InvalidEffectBatch,
                    batchCode);
            }

            return BatchBuildResult.Accept(built.Batch);
        }

        private static bool IsValidCommand(WeaponFireCommand command)
        {
            return command != null
                && command.SimulationTick >= 0L
                && command.Origin != null
                && command.Origin.IsFinite
                && command.AimDirection != null
                && command.AimDirection.IsFinite
                && command.AimDirection.LengthSquared > 0.000000000001d;
        }

        private static WeaponExecutionStatus Map(WeaponProfileResolutionStatus status)
        {
            switch (status)
            {
                case WeaponProfileResolutionStatus.InvalidEquipment:
                    return WeaponExecutionStatus.InvalidEquipment;
                case WeaponProfileResolutionStatus.UnknownWeaponDefinition:
                    return WeaponExecutionStatus.UnknownWeaponDefinition;
                case WeaponProfileResolutionStatus.PreviewOnlyWeaponDefinition:
                    return WeaponExecutionStatus.PreviewOnlyWeaponDefinition;
                case WeaponProfileResolutionStatus.InvalidTuning:
                    return WeaponExecutionStatus.InvalidTuning;
                case WeaponProfileResolutionStatus.UnsupportedEffects:
                    return WeaponExecutionStatus.UnsupportedEffects;
                case WeaponProfileResolutionStatus.UnknownBehavior:
                    return WeaponExecutionStatus.UnknownBehavior;
                default:
                    return WeaponExecutionStatus.InvalidTuning;
            }
        }

        private static bool ValidateBatch(
            WeaponFireCommand command,
            RunParticipantId participant,
            WeaponRuntimeFiringProfile profile,
            long sequence,
            WeaponEffectBatch batch,
            out string code)
        {
            if (batch == null || batch.EffectCount < 1)
            {
                code = "weapon-effect-batch-empty";
                return false;
            }

            for (int index = 0; index < batch.Effects.Count; index++)
            {
                IWeaponEffectDescription effect = batch.Effects[index];
                if (effect == null
                    || effect.Identity == null
                    || !effect.Identity.ActorId.Equals(command.ActorId)
                    || !effect.Identity.ParticipantId.Equals(participant)
                    || !effect.Identity.EquipmentInstanceId.Equals(command.EquipmentInstanceId)
                    || !effect.Identity.WeaponDefinitionId.Equals(profile.DefinitionId)
                    || !effect.Identity.FireOperationId.Equals(command.FireOperationId)
                    || !effect.Identity.LifecycleGeneration.Equals(command.LifecycleGeneration)
                    || effect.Identity.ShotSequence != sequence
                    || effect.Identity.ProjectileOrdinal.Value != index)
                {
                    code = "weapon-effect-identity-invalid:" + index;
                    return false;
                }

                if (!ValidateEffect(effect))
                {
                    code = "weapon-effect-payload-invalid:" + index;
                    return false;
                }
            }

            code = string.Empty;
            return true;
        }

        private static bool ValidateEffect(IWeaponEffectDescription effect)
        {
            DirectProjectileEffect direct = effect as DirectProjectileEffect;
            if (direct != null)
            {
                return IsVector(direct.Origin)
                    && IsDirection(direct.Direction)
                    && IsPositive(direct.Speed)
                    && IsPositive(direct.Range)
                    && IsNonNegative(direct.DirectDamage)
                    && direct.Pierce >= 0
                    && IsNonNegative(direct.Knockback)
                    && !string.IsNullOrWhiteSpace(direct.DamageType);
            }

            ExplosiveProjectileEffect explosive = effect as ExplosiveProjectileEffect;
            if (explosive != null)
            {
                return IsVector(explosive.Origin)
                    && IsDirection(explosive.Direction)
                    && IsPositive(explosive.Speed)
                    && IsPositive(explosive.Range)
                    && IsNonNegative(explosive.DirectDamage)
                    && IsPositive(explosive.AreaDamage)
                    && IsPositive(explosive.ExplosionRadius)
                    && IsNonNegative(explosive.Knockback)
                    && !string.IsNullOrWhiteSpace(explosive.DamageType);
            }

            ChainArcEffect chain = effect as ChainArcEffect;
            if (chain != null)
            {
                return IsVector(chain.Origin)
                    && IsDirection(chain.Direction)
                    && IsPositive(chain.Damage)
                    && chain.MaximumTargets > 0
                    && IsPositive(chain.MaximumRange)
                    && IsNonNegative(chain.Knockback)
                    && !string.IsNullOrWhiteSpace(chain.DamageType);
            }

            return false;
        }

        private static bool IsVector(WeaponVector2 value)
        {
            return value != null && value.IsFinite;
        }

        private static bool IsDirection(WeaponVector2 value)
        {
            return IsVector(value) && value.LengthSquared > 0.000000000001d;
        }

        private static bool IsPositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }

        private static bool IsNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private sealed class BatchBuildResult
        {
            private BatchBuildResult(
                WeaponExecutionStatus status,
                WeaponEffectBatch batch,
                string rejectionCode)
            {
                Status = status;
                Batch = batch;
                RejectionCode = rejectionCode ?? string.Empty;
            }

            public WeaponExecutionStatus Status { get; }
            public WeaponEffectBatch Batch { get; }
            public string RejectionCode { get; }
            public bool Succeeded { get { return Batch != null; } }

            public static BatchBuildResult Accept(WeaponEffectBatch batch)
            {
                return new BatchBuildResult(
                    WeaponExecutionStatus.Accepted,
                    batch ?? throw new ArgumentNullException(nameof(batch)),
                    string.Empty);
            }

            public static BatchBuildResult Reject(
                WeaponExecutionStatus status,
                string rejectionCode)
            {
                return new BatchBuildResult(status, null, rejectionCode);
            }
        }

        private sealed class StateKey : IEquatable<StateKey>
        {
            public StateKey(
                WeaponActorInstanceId actor,
                EquipmentInstanceId equipment,
                LifecycleGeneration generation)
            {
                Actor = actor;
                Equipment = equipment;
                Generation = generation;
            }

            public WeaponActorInstanceId Actor { get; }
            public EquipmentInstanceId Equipment { get; }
            public LifecycleGeneration Generation { get; }

            public bool Equals(StateKey other)
            {
                return !ReferenceEquals(other, null)
                    && Actor.Equals(other.Actor)
                    && Equipment.Equals(other.Equipment)
                    && Generation.Equals(other.Generation);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as StateKey);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Actor.GetHashCode();
                    hash = (hash * 397) ^ Equipment.GetHashCode();
                    return (hash * 397) ^ Generation.GetHashCode();
                }
            }
        }

        private sealed class AcceptedFireOperation
        {
            public AcceptedFireOperation(
                FireOperationId operationId,
                string commandFingerprint,
                string batchFingerprint,
                long shotSequence)
            {
                if (string.IsNullOrWhiteSpace(commandFingerprint))
                {
                    throw new ArgumentException(
                        "Command fingerprint is required.",
                        nameof(commandFingerprint));
                }

                if (string.IsNullOrWhiteSpace(batchFingerprint))
                {
                    throw new ArgumentException(
                        "Batch fingerprint is required.",
                        nameof(batchFingerprint));
                }

                if (shotSequence < 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(shotSequence));
                }

                OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
                CommandFingerprint = commandFingerprint;
                BatchFingerprint = batchFingerprint;
                ShotSequence = shotSequence;
            }

            public FireOperationId OperationId { get; }
            public string CommandFingerprint { get; }
            public string BatchFingerprint { get; }
            public long ShotSequence { get; }

            public bool Matches(string commandFingerprint, string batchFingerprint)
            {
                return string.Equals(
                        CommandFingerprint,
                        commandFingerprint,
                        StringComparison.Ordinal)
                    && string.Equals(
                        BatchFingerprint,
                        batchFingerprint,
                        StringComparison.Ordinal);
            }
        }

        private sealed class FireState
        {
            private readonly Dictionary<FireOperationId, AcceptedFireOperation> acceptedOperations;

            private FireState(
                long nextAllowedTick,
                long shotSequence,
                Dictionary<FireOperationId, AcceptedFireOperation> operations)
            {
                NextAllowedTick = nextAllowedTick;
                ShotSequence = shotSequence;
                acceptedOperations = operations;
            }

            public static FireState Initial
            {
                get
                {
                    return new FireState(
                        0L,
                        0L,
                        new Dictionary<FireOperationId, AcceptedFireOperation>());
                }
            }

            public long NextAllowedTick { get; }
            public long ShotSequence { get; }

            public bool TryGetAccepted(
                FireOperationId operationId,
                out AcceptedFireOperation acceptedOperation)
            {
                return acceptedOperations.TryGetValue(operationId, out acceptedOperation);
            }

            public FireState AfterAccepted(
                AcceptedFireOperation operation,
                long nextAllowedTick)
            {
                Dictionary<FireOperationId, AcceptedFireOperation> copy =
                    new Dictionary<FireOperationId, AcceptedFireOperation>(acceptedOperations);
                copy.Add(operation.OperationId, operation);
                return new FireState(nextAllowedTick, ShotSequence + 1L, copy);
            }
        }
    }
}

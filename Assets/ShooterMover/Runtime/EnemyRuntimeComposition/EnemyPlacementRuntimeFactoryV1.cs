using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public enum EnemyPlacementRuntimeFactoryRejectionV1
    {
        None = 0,
        RoomObjectNotFound = 1,
        EnemyDefinitionNotFound = 2,
        PresentationMismatch = 3,
        LevelOutOfRange = 4,
        MovementPolicyNotRegistered = 5,
        DecisionPolicyNotRegistered = 6,
        AttackCapabilityNotRegistered = 7,
        TargetingAimPolicyNotRegistered = 8,
        DuplicateSpawnIdentity = 9,
        MixedRoomBatch = 10,
        MixedRuntimeBatch = 11,
    }

    public sealed class EnemyPlacementRuntimeRequestV1
    {
        public EnemyPlacementRuntimeRequestV1(
            RoomEnemyPlacementContentV1 placement,
            StableId runStableId,
            StableId roomRuntimeInstanceStableId,
            StableId itemInstanceStableId,
            long roomLifecycleGeneration,
            long lifecycleGeneration,
            EnemyDifficultyContextV1 difficulty)
        {
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoomRuntimeInstanceStableId = roomRuntimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(roomRuntimeInstanceStableId));
            if (roomLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(roomLifecycleGeneration));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            ItemInstanceStableId = itemInstanceStableId;
            RoomLifecycleGeneration = roomLifecycleGeneration;
            LifecycleGeneration = lifecycleGeneration;
            Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
        }

        public RoomEnemyPlacementContentV1 Placement { get; }
        public StableId RunStableId { get; }
        public StableId RoomRuntimeInstanceStableId { get; }
        public StableId ItemInstanceStableId { get; }
        public long RoomLifecycleGeneration { get; }
        public long LifecycleGeneration { get; }
        public EnemyDifficultyContextV1 Difficulty { get; }
    }

    public sealed class EnemyRuntimeAttackBindingV1
    {
        public EnemyRuntimeAttackBindingV1(
            EnemyAttackCapabilityDescriptorV1 descriptor,
            EnemyTargetingAimPolicyRegistrationV1 targetingAim,
            EnemyAttackCapabilityRuntimeRegistrationV1 capability)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            TargetingAim = targetingAim ?? throw new ArgumentNullException(nameof(targetingAim));
            Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        }

        public EnemyAttackCapabilityDescriptorV1 Descriptor { get; }
        public EnemyTargetingAimPolicyRegistrationV1 TargetingAim { get; }
        public EnemyAttackCapabilityRuntimeRegistrationV1 Capability { get; }
    }

    public sealed class EnemyPlacementRuntimeInstanceV1
    {
        private sealed class IssuedDecisionRecord
        {
            public IssuedDecisionRecord(string fingerprint, EnemyPlacementDecisionV1 decision)
            {
                Fingerprint = fingerprint;
                Decision = decision;
            }

            public string Fingerprint { get; }
            public EnemyPlacementDecisionV1 Decision { get; }
        }

        private sealed class AcceptedExecutionRecord
        {
            public AcceptedExecutionRecord(
                string fingerprint,
                string decisionFingerprint,
                EnemyAttackExecutionRequestV1 execution)
            {
                Fingerprint = fingerprint;
                DecisionFingerprint = decisionFingerprint;
                Execution = execution;
            }

            public string Fingerprint { get; }
            public string DecisionFingerprint { get; }
            public EnemyAttackExecutionRequestV1 Execution { get; }
        }

        private sealed class AttackReplayRecord
        {
            public AttackReplayRecord(string signature, EnemyAttackExecutionResultV1 result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyAttackExecutionResultV1 Result { get; }
        }

        private sealed class DamageReplayRecord
        {
            public DamageReplayRecord(string signature, EnemyRuntimeDamageResultV1 result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyRuntimeDamageResultV1 Result { get; }
        }

        private sealed class ImpactReplayRecord
        {
            public ImpactReplayRecord(string signature, EnemyPlayerDamagePortResultV1 result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyPlayerDamagePortResultV1 Result { get; }
        }

        private readonly ReadOnlyCollection<EnemyRuntimeAttackBindingV1> attacks;
        private readonly Dictionary<StableId, EnemyRuntimeAttackBindingV1> attacksById;
        private readonly Dictionary<StableId, double> nextReadyAtByAttack;
        private readonly Dictionary<string, IssuedDecisionRecord> issuedDecisions;
        private readonly Dictionary<StableId, AcceptedExecutionRecord> acceptedExecutions;
        private readonly Dictionary<StableId, AttackReplayRecord> attackReplay;
        private readonly Dictionary<StableId, DamageReplayRecord> damageReplay;
        private readonly Dictionary<StableId, ImpactReplayRecord> impactReplay;
        private readonly EnemyPerceptionRuntimeRegistrationV1 perception;
        private readonly EnemyRuntimeDownstreamPortsV1 downstream;
        private readonly EnemyDefinitionProjection definitionProjection;
        private EnemyActorState actorState;
        private StableId currentTargetId;
        private EnemyDeathFactV1 publishedDeath;

        internal EnemyPlacementRuntimeInstanceV1(
            EnemyPlacementRuntimeRequestV1 request,
            EnemyRuntimeIdentityV1 identity,
            RoomContentObjectDefinitionV1 roomObject,
            EnemyDefinitionV1 definition,
            EnemyActorState actorState,
            EnemyDefinitionProjection definitionProjection,
            EnemyMovementPolicyRegistrationV1 movement,
            EnemyDecisionPolicyRegistrationV1 decision,
            EnemyPerceptionRuntimeRegistrationV1 perception,
            EnemyDifficultyScalingV1 difficultyScaling,
            IEnumerable<EnemyRuntimeAttackBindingV1> attacks,
            RoomOccupantRegistrationV1 roomOccupant,
            EnemyRuntimeDownstreamPortsV1 downstream)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            RoomObject = roomObject ?? throw new ArgumentNullException(nameof(roomObject));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.actorState = actorState ?? throw new ArgumentNullException(nameof(actorState));
            this.definitionProjection = definitionProjection
                ?? throw new ArgumentNullException(nameof(definitionProjection));
            Movement = movement ?? throw new ArgumentNullException(nameof(movement));
            Decision = decision ?? throw new ArgumentNullException(nameof(decision));
            this.perception = perception ?? throw new ArgumentNullException(nameof(perception));
            DifficultyScaling = difficultyScaling
                ?? throw new ArgumentNullException(nameof(difficultyScaling));
            RoomOccupant = roomOccupant ?? throw new ArgumentNullException(nameof(roomOccupant));
            this.downstream = downstream ?? throw new ArgumentNullException(nameof(downstream));

            var copy = new List<EnemyRuntimeAttackBindingV1>(
                attacks ?? throw new ArgumentNullException(nameof(attacks)));
            copy.Sort((left, right) => left.Descriptor.AttackId.CompareTo(right.Descriptor.AttackId));
            this.attacks = new ReadOnlyCollection<EnemyRuntimeAttackBindingV1>(copy);
            attacksById = new Dictionary<StableId, EnemyRuntimeAttackBindingV1>();
            for (int index = 0; index < copy.Count; index++)
            {
                EnemyRuntimeAttackBindingV1 binding = copy[index];
                if (attacksById.ContainsKey(binding.Descriptor.AttackId))
                {
                    throw new ArgumentException(
                        "Enemy runtime attack IDs must be unique: " + binding.Descriptor.AttackId,
                        nameof(attacks));
                }
                attacksById.Add(binding.Descriptor.AttackId, binding);
            }

            nextReadyAtByAttack = new Dictionary<StableId, double>();
            issuedDecisions = new Dictionary<string, IssuedDecisionRecord>(StringComparer.Ordinal);
            acceptedExecutions = new Dictionary<StableId, AcceptedExecutionRecord>();
            attackReplay = new Dictionary<StableId, AttackReplayRecord>();
            damageReplay = new Dictionary<StableId, DamageReplayRecord>();
            impactReplay = new Dictionary<StableId, ImpactReplayRecord>();
        }

        public EnemyPlacementRuntimeRequestV1 Request { get; }
        public EnemyRuntimeIdentityV1 Identity { get; }
        public RoomContentObjectDefinitionV1 RoomObject { get; }
        public EnemyDefinitionV1 Definition { get; }
        public EnemyMovementPolicyRegistrationV1 Movement { get; }
        public EnemyDecisionPolicyRegistrationV1 Decision { get; }
        public EnemyDifficultyScalingV1 DifficultyScaling { get; }
        public RoomOccupantRegistrationV1 RoomOccupant { get; }
        public IReadOnlyList<EnemyRuntimeAttackBindingV1> Attacks { get { return attacks; } }
        public StableId RoomStableId { get { return Request.Placement.RoomStableId; } }
        public StableId PlacementStableId { get { return Request.Placement.InstanceStableId; } }
        public StableId SpawnStableId { get { return Identity.EntityInstanceId; } }
        public StableId RunParticipantStableId { get { return Identity.RunParticipantId; } }
        public StableId ItemInstanceStableId { get { return Request.ItemInstanceStableId; } }
        public StableId PresentationStableId { get { return Definition.PresentationId; } }
        public int Level { get { return Request.Placement.Level; } }
        public long LifecycleGeneration { get { return Request.LifecycleGeneration; } }
        public StableId LifecycleStableId
        {
            get
            {
                return StableId.Create(
                    "enemy-lifecycle",
                    "runtime-" + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                        Identity.EntityInstanceId
                        + "|generation|"
                        + LifecycleGeneration.ToString(CultureInfo.InvariantCulture)));
            }
        }
        public EnemyActorState ActorState { get { return actorState; } }
        public EnemyDeathFactV1 PublishedDeath { get { return publishedDeath; } }

        public EnemyRuntimeProjection Runtime
        {
            get
            {
                return new EnemyRuntimeProjection(
                    new GameplayEntityIdentity(
                        Identity.EntityInstanceId,
                        GameplayEntityOwnership.Create(Identity.RunParticipantId, null),
                        Definition.FactionId),
                    definitionProjection,
                    actorState,
                    LifecycleGeneration,
                    currentTargetId,
                    Decision.Configuration.ReadyPhaseId);
            }
        }

        public EnemyPlacementDecisionV1 Evaluate(EnemyPerceptionSnapshot sourcePerception)
        {
            EnemyPerceptionSnapshot adapted = perception.Adapter.Adapt(
                Runtime,
                Definition,
                sourcePerception,
                perception.Configuration);
            EnemyDecisionEvaluation evaluation = Decision.Policy.Evaluate(
                Runtime,
                Definition,
                Decision.Configuration,
                adapted);
            currentTargetId = evaluation.Decision.SelectedTargetId;
            var projection = new EnemyPlacementDecisionV1(
                Identity.EntityInstanceId,
                LifecycleGeneration,
                adapted,
                evaluation);
            string fingerprint = EnemyRuntimeAuthorityFingerprintV1.Decision(projection);
            issuedDecisions[fingerprint] = new IssuedDecisionRecord(fingerprint, projection);
            return projection;
        }

        public EnemyMovementRealizationV1 RealizeMovement(
            EnemyPlacementDecisionV1 decision,
            EnemyMovementRealizationContextV1 context)
        {
            IssuedDecisionRecord issued = RequireIssuedDecision(decision);
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.EntityInstanceId != Identity.EntityInstanceId)
                throw new ArgumentException("Movement context must target this enemy instance.", nameof(context));
            if (context.RoomStableId != RoomStableId)
                throw new ArgumentException("Movement context must target this enemy room.", nameof(context));

            EnemyMovementPolicyIntentV1 intent = Movement.Policy.BuildIntent(
                issued.Decision.Evaluation,
                Movement.Configuration);
            var scaledContext = new EnemyMovementRealizationContextV1(
                context.EntityInstanceId,
                context.RoomStableId,
                context.CurrentPosition,
                context.CurrentFacing,
                context.SimulationTick,
                DifficultyScaling.MovementMultiplier,
                context.EnvironmentQuery);
            return Movement.Realizer.Realize(intent, scaledContext, Movement.Configuration);
        }

        public EnemyAttackExecutionResultV1 TryExecuteAttack(
            EnemyPlacementDecisionV1 decision,
            StableId operationStableId,
            double occurredAtSeconds)
        {
            return TryExecuteAttackCore(
                decision,
                null,
                false,
                operationStableId,
                occurredAtSeconds);
        }

        // Compatibility overload. The supplied projection is validation-only; execution always rebuilds
        // the authoritative context from the issued decision and this runtime's difficulty context.
        public EnemyAttackExecutionResultV1 TryExecuteAttack(
            EnemyPlacementDecisionV1 decision,
            EnemyTargetingAimContextV1 context,
            StableId operationStableId,
            double occurredAtSeconds)
        {
            return TryExecuteAttackCore(
                decision,
                context,
                true,
                operationStableId,
                occurredAtSeconds);
        }

        private EnemyAttackExecutionResultV1 TryExecuteAttackCore(
            EnemyPlacementDecisionV1 decision,
            EnemyTargetingAimContextV1 suppliedContext,
            bool callerSuppliedContext,
            StableId operationStableId,
            double occurredAtSeconds)
        {
            if (operationStableId == null) throw new ArgumentNullException(nameof(operationStableId));
            if (double.IsNaN(occurredAtSeconds)
                || double.IsInfinity(occurredAtSeconds)
                || occurredAtSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(occurredAtSeconds));

            IssuedDecisionRecord issued;
            EnemyRuntimeRejectionCodeV1 validation = ValidateDecisionCode(decision, out issued);
            EnemyAttackIntent requested = validation == EnemyRuntimeRejectionCodeV1.None
                ? issued.Decision.Evaluation.Decision.RequestedAttack
                : decision == null ? null : decision.Evaluation.Decision.RequestedAttack;
            EnemyRuntimeAttackBindingV1 binding = null;
            if (requested != null) attacksById.TryGetValue(requested.AttackId, out binding);

            EnemyTargetingAimContextV1 authoritativeContext = decision == null
                ? null
                : new EnemyTargetingAimContextV1(
                    validation == EnemyRuntimeRejectionCodeV1.None
                        ? issued.Decision.Perception
                        : decision.Perception,
                    Request.Difficulty.Scalar);
            EnemyTargetingAimContextV1 signatureContext = callerSuppliedContext
                ? suppliedContext
                : authoritativeContext;
            string decisionFingerprint = issued == null
                ? EnemyRuntimeAuthorityFingerprintV1.Decision(decision)
                : issued.Fingerprint;
            string signature = EnemyRuntimeAuthorityFingerprintV1.AttackAttempt(
                decisionFingerprint,
                signatureContext,
                false,
                occurredAtSeconds,
                Request.Difficulty,
                DifficultyScaling,
                binding);

            AttackReplayRecord replay;
            if (attackReplay.TryGetValue(operationStableId, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                    return RejectedAttack(EnemyRuntimeRejectionCodeV1.ConflictingDuplicate);
                return new EnemyAttackExecutionResultV1(
                    EnemyRuntimeOperationStatusV1.ExactReplay,
                    replay.Result.Rejection,
                    replay.Result.Request);
            }

            EnemyAttackExecutionResultV1 result;
            if (validation != EnemyRuntimeRejectionCodeV1.None)
            {
                result = RejectedAttack(validation);
            }
            else if (callerSuppliedContext
                && (suppliedContext == null
                    || !string.Equals(
                        EnemyRuntimeAuthorityFingerprintV1.AimContext(suppliedContext),
                        EnemyRuntimeAuthorityFingerprintV1.AimContext(authoritativeContext),
                        StringComparison.Ordinal)))
            {
                result = RejectedAttack(EnemyRuntimeRejectionCodeV1.InvalidCommand);
            }
            else if (!actorState.IsActive)
            {
                result = RejectedAttack(EnemyRuntimeRejectionCodeV1.ActorTerminal);
            }
            else if (requested == null)
            {
                result = new EnemyAttackExecutionResultV1(
                    EnemyRuntimeOperationStatusV1.NoEffect,
                    EnemyRuntimeRejectionCodeV1.MissingAttackIntent,
                    null);
            }
            else if (binding == null)
            {
                result = RejectedAttack(EnemyRuntimeRejectionCodeV1.UnknownAttack);
            }
            else
            {
                double readyAt;
                nextReadyAtByAttack.TryGetValue(requested.AttackId, out readyAt);
                if (occurredAtSeconds < readyAt)
                {
                    result = RejectedAttack(EnemyRuntimeRejectionCodeV1.CooldownActive);
                }
                else
                {
                    EnemyAttackIntent committed = binding.TargetingAim.Policy.Commit(
                        requested,
                        authoritativeContext,
                        binding.TargetingAim.Configuration);
                    var executionContext = new EnemyAttackExecutionContextV1(
                        operationStableId,
                        Identity,
                        LifecycleGeneration,
                        occurredAtSeconds,
                        DifficultyScaling);
                    StableId itemInstance = ResolveAttackItemInstance(binding.Descriptor.AttackId);
                    EnemyAttackExecutionRequestV1 execution =
                        binding.Capability.Adapter.BuildExecution(
                            binding.Descriptor,
                            committed,
                            itemInstance,
                            binding.Capability.Configuration,
                            executionContext);
                    if (!ExecutionMatchesAuthoritativeInputs(
                        execution,
                        operationStableId,
                        occurredAtSeconds,
                        binding,
                        committed,
                        itemInstance))
                    {
                        result = RejectedAttack(EnemyRuntimeRejectionCodeV1.InvalidCommand);
                    }
                    else
                    {
                        string executionFingerprint = EnemyRuntimeAuthorityFingerprintV1.Execution(
                            execution,
                            issued.Fingerprint);
                        acceptedExecutions.Add(
                            operationStableId,
                            new AcceptedExecutionRecord(
                                executionFingerprint,
                                issued.Fingerprint,
                                execution));
                        downstream.AttackEffects.Emit(execution);
                        nextReadyAtByAttack[requested.AttackId] =
                            occurredAtSeconds + execution.ResolvedCooldownSeconds;
                        result = new EnemyAttackExecutionResultV1(
                            EnemyRuntimeOperationStatusV1.Applied,
                            EnemyRuntimeRejectionCodeV1.None,
                            execution);
                    }
                }
            }

            attackReplay.Add(operationStableId, new AttackReplayRecord(signature, result));
            return result;
        }

        public EnemyPlayerDamagePortResultV1 RoutePlayerImpact(
            EnemyAttackExecutionRequestV1 execution,
            StableId hitEventStableId,
            StableId targetEntityStableId)
        {
            if (hitEventStableId == null) throw new ArgumentNullException(nameof(hitEventStableId));
            if (targetEntityStableId == null) throw new ArgumentNullException(nameof(targetEntityStableId));
            if (execution == null
                || execution.Identity == null
                || execution.Identity.EntityInstanceId != Identity.EntityInstanceId)
            {
                return RejectedPlayerImpact(EnemyRuntimeRejectionCodeV1.EntityMismatch);
            }
            if (execution.LifecycleGeneration != LifecycleGeneration)
                return RejectedPlayerImpact(EnemyRuntimeRejectionCodeV1.StaleLifecycle);

            AcceptedExecutionRecord accepted;
            if (!acceptedExecutions.TryGetValue(execution.OperationStableId, out accepted))
                return RejectedPlayerImpact(EnemyRuntimeRejectionCodeV1.ExecutionNotIssued);
            string suppliedFingerprint = EnemyRuntimeAuthorityFingerprintV1.Execution(
                execution,
                accepted.DecisionFingerprint);
            if (!string.Equals(accepted.Fingerprint, suppliedFingerprint, StringComparison.Ordinal))
                return RejectedPlayerImpact(EnemyRuntimeRejectionCodeV1.InvalidCommand);

            string signature = EnemyRuntimeAuthorityFingerprintV1.Impact(
                accepted.Fingerprint,
                targetEntityStableId);
            ImpactReplayRecord replay;
            if (impactReplay.TryGetValue(hitEventStableId, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                    return RejectedPlayerImpact(EnemyRuntimeRejectionCodeV1.ConflictingDuplicate);
                return new EnemyPlayerDamagePortResultV1(
                    EnemyRuntimeOperationStatusV1.ExactReplay,
                    replay.Result.Rejection);
            }

            EnemyAttackExecutionRequestV1 canonical = accepted.Execution;
            var request = new EnemyPlayerDamageRequestV1(
                hitEventStableId,
                canonical.OperationStableId,
                Identity.EntityInstanceId,
                Identity.RunParticipantId,
                targetEntityStableId,
                LifecycleGeneration,
                canonical.ResolvedDamage,
                canonical.Descriptor.DamageChannelId,
                canonical.CommittedIntent);
            EnemyPlayerDamagePortResultV1 result = downstream.PlayerDamage.Route(request)
                ?? throw new InvalidOperationException("Player damage ports must return a result.");
            impactReplay.Add(hitEventStableId, new ImpactReplayRecord(signature, result));
            return result;
        }

        public EnemyRuntimeDamageResultV1 ApplyDamage(EnemyRuntimeDamageCommandV1 command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            string signature = DamageSignature(command);
            DamageReplayRecord replay;
            if (damageReplay.TryGetValue(command.OperationStableId, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                {
                    return new EnemyRuntimeDamageResultV1(
                        EnemyRuntimeOperationStatusV1.Rejected,
                        EnemyRuntimeRejectionCodeV1.ConflictingDuplicate,
                        Runtime,
                        publishedDeath);
                }
                return new EnemyRuntimeDamageResultV1(
                    EnemyRuntimeOperationStatusV1.ExactReplay,
                    replay.Result.Rejection,
                    Runtime,
                    replay.Result.DeathFact);
            }

            EnemyRuntimeDamageResultV1 result;
            if (command.TargetEntityStableId != Identity.EntityInstanceId)
            {
                result = RejectedDamage(EnemyRuntimeRejectionCodeV1.EntityMismatch);
            }
            else if (command.TargetLifecycleGeneration != LifecycleGeneration)
            {
                result = RejectedDamage(EnemyRuntimeRejectionCodeV1.StaleLifecycle);
            }
            else if (!actorState.IsActive)
            {
                result = RejectedDamage(EnemyRuntimeRejectionCodeV1.ActorTerminal);
            }
            else
            {
                EnemyActorStepResult stepped = EnemyActorStepper.Step(
                    actorState,
                    new[]
                    {
                        EnemyActorCommand.Damage(
                            command.Order,
                            command.OperationStableId,
                            command.SourceEntityStableId,
                            command.ChannelValue,
                            command.Amount),
                    });
                actorState = stepped.State;
                EnemyDestroyedNotification destroyed = FindDestroyed(stepped.Notifications);
                EnemyDeathFactV1 death = destroyed == null ? null : PublishDeathOnce(command, destroyed);
                result = new EnemyRuntimeDamageResultV1(
                    EnemyRuntimeOperationStatusV1.Applied,
                    EnemyRuntimeRejectionCodeV1.None,
                    Runtime,
                    death);
            }

            damageReplay.Add(command.OperationStableId, new DamageReplayRecord(signature, result));
            return result;
        }

        public ReportRoomOccupantTerminalCommandV1 BuildTerminalCommand(StableId operationStableId)
        {
            return new ReportRoomOccupantTerminalCommandV1(
                Identity.RoomRuntimeInstanceStableId,
                operationStableId,
                Request.RoomLifecycleGeneration,
                RoomStableId,
                SpawnStableId);
        }

        private bool ExecutionMatchesAuthoritativeInputs(
            EnemyAttackExecutionRequestV1 execution,
            StableId operationStableId,
            double occurredAtSeconds,
            EnemyRuntimeAttackBindingV1 binding,
            EnemyAttackIntent committed,
            StableId itemInstance)
        {
            return execution != null
                && execution.OperationStableId == operationStableId
                && EnemyRuntimeAuthorityFingerprintV1.IdentityEquals(execution.Identity, Identity)
                && execution.LifecycleGeneration == LifecycleGeneration
                && execution.OccurredAtSeconds == occurredAtSeconds
                && string.Equals(
                    EnemyRuntimeAuthorityFingerprintV1.Descriptor(execution.Descriptor),
                    EnemyRuntimeAuthorityFingerprintV1.Descriptor(binding.Descriptor),
                    StringComparison.Ordinal)
                && string.Equals(
                    EnemyRuntimeAuthorityFingerprintV1.AttackIntent(execution.CommittedIntent),
                    EnemyRuntimeAuthorityFingerprintV1.AttackIntent(committed),
                    StringComparison.Ordinal)
                && execution.ItemInstanceStableId == itemInstance
                && execution.ExecutionKind == binding.Capability.Configuration.ExecutionKind;
        }

        private StableId ResolveAttackItemInstance(StableId attackStableId)
        {
            if (ItemInstanceStableId != null) return ItemInstanceStableId;
            return StableId.Create(
                "equipment-instance",
                "enemy-" + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                    Identity.EntityInstanceId + "|" + attackStableId));
        }

        private EnemyDeathFactV1 PublishDeathOnce(
            EnemyRuntimeDamageCommandV1 command,
            EnemyDestroyedNotification destroyed)
        {
            if (publishedDeath != null) return publishedDeath;
            publishedDeath = new EnemyDeathFactV1(
                destroyed.EventId,
                command.OperationStableId,
                Identity,
                Definition.DefinitionId,
                Level,
                LifecycleGeneration,
                command.SourceEntityStableId,
                command.SourceRunParticipantStableId,
                Definition.ExperienceProfileId,
                Definition.DropProfileId,
                destroyed.DeathCause);

            downstream.TerminalCollision.SetTerminal(
                new EnemyTerminalCollisionFactV1(
                    Identity.EntityInstanceId,
                    destroyed.EventId,
                    LifecycleGeneration));
            StableId roomOperation = StableId.Create(
                "room-operation",
                "enemy-terminal-"
                + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                    Identity.EntityInstanceId + "|" + destroyed.EventId));
            downstream.RoomTerminal.Report(BuildTerminalCommand(roomOperation), publishedDeath);
            downstream.Experience.Consume(publishedDeath);
            downstream.Drops.Consume(publishedDeath);
            downstream.KillStats.Consume(publishedDeath);
            return publishedDeath;
        }

        private IssuedDecisionRecord RequireIssuedDecision(EnemyPlacementDecisionV1 decision)
        {
            IssuedDecisionRecord issued;
            EnemyRuntimeRejectionCodeV1 code = ValidateDecisionCode(decision, out issued);
            if (code != EnemyRuntimeRejectionCodeV1.None)
                throw new InvalidOperationException("Enemy decision is not valid for this runtime: " + code);
            return issued;
        }

        private EnemyRuntimeRejectionCodeV1 ValidateDecisionCode(
            EnemyPlacementDecisionV1 decision,
            out IssuedDecisionRecord issued)
        {
            issued = null;
            if (decision == null) return EnemyRuntimeRejectionCodeV1.InvalidCommand;
            if (decision.EntityInstanceId != Identity.EntityInstanceId)
                return EnemyRuntimeRejectionCodeV1.EntityMismatch;
            if (decision.LifecycleGeneration != LifecycleGeneration)
                return EnemyRuntimeRejectionCodeV1.StaleLifecycle;
            string fingerprint = EnemyRuntimeAuthorityFingerprintV1.Decision(decision);
            if (!issuedDecisions.TryGetValue(fingerprint, out issued))
                return EnemyRuntimeRejectionCodeV1.DecisionNotIssued;
            return EnemyRuntimeRejectionCodeV1.None;
        }

        private static string DamageSignature(EnemyRuntimeDamageCommandV1 command)
        {
            return command.SourceEntityStableId
                + "|" + command.SourceRunParticipantStableId
                + "|" + command.TargetEntityStableId
                + "|" + command.TargetLifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|" + command.Order.ToString(CultureInfo.InvariantCulture)
                + "|" + command.ChannelValue.ToString(CultureInfo.InvariantCulture)
                + "|" + command.Amount.ToString("R", CultureInfo.InvariantCulture);
        }

        private static EnemyDestroyedNotification FindDestroyed(
            IReadOnlyList<EnemyActorNotification> notifications)
        {
            for (int index = 0; index < notifications.Count; index++)
            {
                EnemyDestroyedNotification destroyed = notifications[index] as EnemyDestroyedNotification;
                if (destroyed != null) return destroyed;
            }
            return null;
        }

        private static EnemyAttackExecutionResultV1 RejectedAttack(
            EnemyRuntimeRejectionCodeV1 rejection)
        {
            return new EnemyAttackExecutionResultV1(
                EnemyRuntimeOperationStatusV1.Rejected,
                rejection,
                null);
        }

        private EnemyRuntimeDamageResultV1 RejectedDamage(
            EnemyRuntimeRejectionCodeV1 rejection)
        {
            return new EnemyRuntimeDamageResultV1(
                EnemyRuntimeOperationStatusV1.Rejected,
                rejection,
                Runtime,
                publishedDeath);
        }

        private static EnemyPlayerDamagePortResultV1 RejectedPlayerImpact(
            EnemyRuntimeRejectionCodeV1 rejection)
        {
            return new EnemyPlayerDamagePortResultV1(
                EnemyRuntimeOperationStatusV1.Rejected,
                rejection);
        }
    }

    public sealed class EnemyPlacementRuntimeFactoryResultV1
    {
        private EnemyPlacementRuntimeFactoryResultV1(
            EnemyPlacementRuntimeInstanceV1 runtime,
            EnemyPlacementRuntimeFactoryRejectionV1 rejection,
            string diagnostic)
        {
            Runtime = runtime;
            Rejection = rejection;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public EnemyPlacementRuntimeInstanceV1 Runtime { get; }
        public EnemyPlacementRuntimeFactoryRejectionV1 Rejection { get; }
        public string Diagnostic { get; }
        public bool IsCreated
        {
            get
            {
                return Runtime != null
                    && Rejection == EnemyPlacementRuntimeFactoryRejectionV1.None;
            }
        }

        internal static EnemyPlacementRuntimeFactoryResultV1 Created(
            EnemyPlacementRuntimeInstanceV1 runtime)
        {
            return new EnemyPlacementRuntimeFactoryResultV1(
                runtime ?? throw new ArgumentNullException(nameof(runtime)),
                EnemyPlacementRuntimeFactoryRejectionV1.None,
                string.Empty);
        }

        internal static EnemyPlacementRuntimeFactoryResultV1 Rejected(
            EnemyPlacementRuntimeFactoryRejectionV1 rejection,
            string diagnostic)
        {
            if (rejection == EnemyPlacementRuntimeFactoryRejectionV1.None)
                throw new ArgumentOutOfRangeException(nameof(rejection));
            return new EnemyPlacementRuntimeFactoryResultV1(null, rejection, diagnostic);
        }
    }

    public sealed class EnemyRoomPlacementCompositionResultV1
    {
        private readonly ReadOnlyCollection<EnemyPlacementRuntimeInstanceV1> runtimes;
        private readonly ReadOnlyCollection<RoomOccupantRegistrationV1> occupants;

        private EnemyRoomPlacementCompositionResultV1(
            StableId roomStableId,
            IEnumerable<EnemyPlacementRuntimeInstanceV1> runtimes,
            IEnumerable<RoomOccupantRegistrationV1> occupants,
            EnemyPlacementRuntimeFactoryRejectionV1 rejection,
            string diagnostic)
        {
            RoomStableId = roomStableId;
            this.runtimes = new ReadOnlyCollection<EnemyPlacementRuntimeInstanceV1>(
                new List<EnemyPlacementRuntimeInstanceV1>(
                    runtimes ?? Array.Empty<EnemyPlacementRuntimeInstanceV1>()));
            this.occupants = new ReadOnlyCollection<RoomOccupantRegistrationV1>(
                new List<RoomOccupantRegistrationV1>(
                    occupants ?? Array.Empty<RoomOccupantRegistrationV1>()));
            Rejection = rejection;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public StableId RoomStableId { get; }
        public IReadOnlyList<EnemyPlacementRuntimeInstanceV1> Runtimes { get { return runtimes; } }
        public IReadOnlyList<RoomOccupantRegistrationV1> Occupants { get { return occupants; } }
        public EnemyPlacementRuntimeFactoryRejectionV1 Rejection { get; }
        public string Diagnostic { get; }
        public bool IsCreated { get { return Rejection == EnemyPlacementRuntimeFactoryRejectionV1.None; } }

        public RegisterRoomOccupantsCommandV1 BuildRegistrationCommand(
            StableId roomRuntimeInstanceStableId,
            StableId operationStableId,
            long roomLifecycleGeneration)
        {
            if (!IsCreated)
                throw new InvalidOperationException("A rejected room composition cannot register occupants.");
            return new RegisterRoomOccupantsCommandV1(
                roomRuntimeInstanceStableId,
                operationStableId,
                roomLifecycleGeneration,
                RoomStableId,
                occupants);
        }

        internal static EnemyRoomPlacementCompositionResultV1 Created(
            StableId roomStableId,
            IEnumerable<EnemyPlacementRuntimeInstanceV1> runtimes,
            IEnumerable<RoomOccupantRegistrationV1> occupants)
        {
            return new EnemyRoomPlacementCompositionResultV1(
                roomStableId,
                runtimes,
                occupants,
                EnemyPlacementRuntimeFactoryRejectionV1.None,
                string.Empty);
        }

        internal static EnemyRoomPlacementCompositionResultV1 Rejected(
            EnemyPlacementRuntimeFactoryRejectionV1 rejection,
            string diagnostic)
        {
            return new EnemyRoomPlacementCompositionResultV1(
                null,
                Array.Empty<EnemyPlacementRuntimeInstanceV1>(),
                Array.Empty<RoomOccupantRegistrationV1>(),
                rejection,
                diagnostic);
        }
    }

    public sealed class EnemyPlacementRuntimeFactoryV1
    {
        private static readonly Dictionary<EnemyCatalogRoomClearRoleV1, EnemyRoomClearRole>
            RuntimeRoomRoles = new Dictionary<EnemyCatalogRoomClearRoleV1, EnemyRoomClearRole>
            {
                { EnemyCatalogRoomClearRoleV1.RequiredEnemy, EnemyRoomClearRole.RequiredEnemy },
                { EnemyCatalogRoomClearRoleV1.OptionalEnemy, EnemyRoomClearRole.OptionalEnemy },
                { EnemyCatalogRoomClearRoleV1.ObjectiveEntity, EnemyRoomClearRole.ObjectiveEntity },
                { EnemyCatalogRoomClearRoleV1.DoesNotAffectRoomClear, EnemyRoomClearRole.DoesNotAffectRoomClear },
            };

        private static readonly Dictionary<EnemyCatalogRoomClearRoleV1, RoomOccupantClearRoleV1>
            OccupantRoomRoles = new Dictionary<EnemyCatalogRoomClearRoleV1, RoomOccupantClearRoleV1>
            {
                { EnemyCatalogRoomClearRoleV1.RequiredEnemy, RoomOccupantClearRoleV1.RequiredEnemy },
                { EnemyCatalogRoomClearRoleV1.OptionalEnemy, RoomOccupantClearRoleV1.OptionalEnemy },
                { EnemyCatalogRoomClearRoleV1.ObjectiveEntity, RoomOccupantClearRoleV1.ObjectiveEntity },
                { EnemyCatalogRoomClearRoleV1.DoesNotAffectRoomClear, RoomOccupantClearRoleV1.NonParticipant },
            };

        private readonly IRoomContentObjectCatalogV1 roomObjects;
        private readonly EnemyCatalogV1 enemies;
        private readonly EnemyRuntimePolicyRegistryV1 policies;
        private readonly IEnemyRuntimeIdentityDeriverV1 identityDeriver;
        private readonly EnemyDifficultyRuntimeRegistrationV1 difficulty;
        private readonly EnemyPerceptionRuntimeRegistrationV1 perception;
        private readonly EnemyRuntimeDownstreamPortsV1 downstream;

        public EnemyPlacementRuntimeFactoryV1(
            IRoomContentObjectCatalogV1 roomObjects,
            EnemyCatalogV1 enemies,
            EnemyRuntimePolicyRegistryV1 policies,
            IEnemyRuntimeIdentityDeriverV1 identityDeriver,
            EnemyDifficultyRuntimeRegistrationV1 difficulty,
            EnemyPerceptionRuntimeRegistrationV1 perception,
            EnemyRuntimeDownstreamPortsV1 downstream)
        {
            this.roomObjects = roomObjects ?? throw new ArgumentNullException(nameof(roomObjects));
            this.enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
            this.identityDeriver = identityDeriver ?? throw new ArgumentNullException(nameof(identityDeriver));
            this.difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            this.perception = perception ?? throw new ArgumentNullException(nameof(perception));
            this.downstream = downstream ?? throw new ArgumentNullException(nameof(downstream));
        }

        public EnemyPlacementRuntimeFactoryResultV1 Create(EnemyPlacementRuntimeRequestV1 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            RoomContentObjectDefinitionV1 roomObject;
            if (!roomObjects.TryResolve(
                request.Placement.ObjectStableId,
                RoomContentObjectKindV1.Enemy,
                out roomObject))
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.RoomObjectNotFound,
                    request.Placement.ObjectStableId);
            }

            EnemyDefinitionV1 definition;
            if (!enemies.TryGetDefinition(roomObject.RuntimeDefinitionStableId, out definition))
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.EnemyDefinitionNotFound,
                    roomObject.RuntimeDefinitionStableId);
            }
            if (roomObject.PresentationStableId != definition.PresentationId)
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.PresentationMismatch,
                    definition.DefinitionId);
            }
            if (definition.LevelScaling != null
                && (request.Placement.Level < definition.LevelScaling.BaseLevel
                    || request.Placement.Level > definition.LevelScaling.MaximumLevel))
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.LevelOutOfRange,
                    definition.DefinitionId);
            }

            EnemyMovementPolicyRegistrationV1 movement;
            if (!policies.TryResolveMovement(definition.MovementPolicyId, out movement))
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.MovementPolicyNotRegistered,
                    definition.MovementPolicyId);
            }
            EnemyDecisionPolicyRegistrationV1 decision;
            if (!policies.TryResolveDecision(definition.DecisionPolicyId, out decision))
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.DecisionPolicyNotRegistered,
                    definition.DecisionPolicyId);
            }

            var attacks = new List<EnemyRuntimeAttackBindingV1>();
            for (int index = 0; index < definition.Attacks.Count; index++)
            {
                EnemyAttackCapabilityDescriptorV1 descriptor = definition.Attacks[index];
                EnemyAttackCapabilityRuntimeRegistrationV1 capability;
                if (!policies.TryResolveAttackCapability(descriptor.CapabilityId, out capability))
                {
                    return Reject(
                        EnemyPlacementRuntimeFactoryRejectionV1.AttackCapabilityNotRegistered,
                        descriptor.CapabilityId);
                }
                EnemyTargetingAimPolicyRegistrationV1 targetingAim;
                if (!policies.TryResolveTargetingAim(
                    capability.Configuration.TargetingAimPolicyId,
                    out targetingAim))
                {
                    return Reject(
                        EnemyPlacementRuntimeFactoryRejectionV1.TargetingAimPolicyNotRegistered,
                        capability.Configuration.TargetingAimPolicyId);
                }
                attacks.Add(new EnemyRuntimeAttackBindingV1(descriptor, targetingAim, capability));
            }

            EnemyRuntimeIdentityV1 identity = identityDeriver.Derive(
                request.RunStableId,
                request.RoomRuntimeInstanceStableId,
                request.Placement.RoomStableId,
                request.Placement.InstanceStableId);
            EnemyDifficultyScalingV1 scaling = difficulty.Policy.Resolve(
                request.Placement.Level,
                request.Difficulty,
                difficulty.Configuration);
            double definitionHealth = definition.LevelScaling == null
                ? definition.BaseHealth
                : definition.LevelScaling.ResolveHealth(
                    definition.BaseHealth,
                    request.Placement.Level);
            double maximumHealth = definitionHealth * scaling.HealthMultiplier;
            EnemyActorState actor = EnemyActorState.Create(
                identity.EntityInstanceId,
                definition.DefinitionId,
                maximumHealth,
                2,
                EnemyContactPolicy.Create(
                    EnemyContactMode.None,
                    0d,
                    0.5d,
                    0.02d,
                    8));

            var attackIds = new List<StableId>();
            for (int index = 0; index < definition.Attacks.Count; index++)
                attackIds.Add(definition.Attacks[index].AttackId);
            var rewardIds = new[]
            {
                definition.ExperienceProfileId,
                definition.DropProfileId,
            };
            var definitionProjection = new EnemyDefinitionProjection(
                definition.DefinitionId,
                definition.MovementPolicyId,
                attackIds,
                rewardIds,
                MapRuntimeRoomRole(definition.RoomClearRole));
            var occupant = new RoomOccupantRegistrationV1(
                identity.EntityInstanceId,
                definition.DefinitionId,
                MapRoomOccupantRole(definition.RoomClearRole));

            return EnemyPlacementRuntimeFactoryResultV1.Created(
                new EnemyPlacementRuntimeInstanceV1(
                    request,
                    identity,
                    roomObject,
                    definition,
                    actor,
                    definitionProjection,
                    movement,
                    decision,
                    perception,
                    scaling,
                    attacks,
                    occupant,
                    downstream));
        }

        public EnemyRoomPlacementCompositionResultV1 CreateRoom(
            IEnumerable<EnemyPlacementRuntimeRequestV1> requests)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            var runtimes = new List<EnemyPlacementRuntimeInstanceV1>();
            var occupants = new List<RoomOccupantRegistrationV1>();
            var spawnIds = new HashSet<StableId>();
            StableId roomStableId = null;
            StableId runStableId = null;
            StableId roomRuntimeInstanceStableId = null;

            foreach (EnemyPlacementRuntimeRequestV1 request in requests)
            {
                if (request == null)
                    throw new ArgumentException("Room requests cannot contain null.", nameof(requests));
                if (roomStableId == null)
                {
                    roomStableId = request.Placement.RoomStableId;
                    runStableId = request.RunStableId;
                    roomRuntimeInstanceStableId = request.RoomRuntimeInstanceStableId;
                }
                else if (roomStableId != request.Placement.RoomStableId)
                {
                    return EnemyRoomPlacementCompositionResultV1.Rejected(
                        EnemyPlacementRuntimeFactoryRejectionV1.MixedRoomBatch,
                        "enemy-factory:mixed-room-batch");
                }
                else if (runStableId != request.RunStableId
                    || roomRuntimeInstanceStableId != request.RoomRuntimeInstanceStableId)
                {
                    return EnemyRoomPlacementCompositionResultV1.Rejected(
                        EnemyPlacementRuntimeFactoryRejectionV1.MixedRuntimeBatch,
                        "enemy-factory:mixed-runtime-batch");
                }

                EnemyPlacementRuntimeFactoryResultV1 result = Create(request);
                if (!result.IsCreated)
                {
                    return EnemyRoomPlacementCompositionResultV1.Rejected(
                        result.Rejection,
                        result.Diagnostic);
                }
                if (!spawnIds.Add(result.Runtime.SpawnStableId))
                {
                    return EnemyRoomPlacementCompositionResultV1.Rejected(
                        EnemyPlacementRuntimeFactoryRejectionV1.DuplicateSpawnIdentity,
                        "enemy-factory:duplicate-spawn:" + result.Runtime.SpawnStableId);
                }
                runtimes.Add(result.Runtime);
                occupants.Add(result.Runtime.RoomOccupant);
            }

            if (roomStableId == null)
                throw new ArgumentException(
                    "A room composition requires at least one enemy placement.",
                    nameof(requests));
            runtimes.Sort((left, right) => left.SpawnStableId.CompareTo(right.SpawnStableId));
            occupants.Sort((left, right) => left.EntityStableId.CompareTo(right.EntityStableId));
            return EnemyRoomPlacementCompositionResultV1.Created(roomStableId, runtimes, occupants);
        }

        private static EnemyPlacementRuntimeFactoryResultV1 Reject(
            EnemyPlacementRuntimeFactoryRejectionV1 rejection,
            StableId id)
        {
            return EnemyPlacementRuntimeFactoryResultV1.Rejected(
                rejection,
                "enemy-factory:" + rejection + ":" + id);
        }

        private static EnemyRoomClearRole MapRuntimeRoomRole(EnemyCatalogRoomClearRoleV1 role)
        {
            EnemyRoomClearRole mapped;
            if (!RuntimeRoomRoles.TryGetValue(role, out mapped))
                throw new ArgumentOutOfRangeException(nameof(role));
            return mapped;
        }

        private static RoomOccupantClearRoleV1 MapRoomOccupantRole(
            EnemyCatalogRoomClearRoleV1 role)
        {
            RoomOccupantClearRoleV1 mapped;
            if (!OccupantRoomRoles.TryGetValue(role, out mapped))
                throw new ArgumentOutOfRangeException(nameof(role));
            return mapped;
        }
    }
}

using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyCombatRuntime
{
    /// <summary>
    /// Engine-neutral live enemy orchestration. Decisions, weapons, player health, enemy
    /// health, rewards, and room state remain owned by their existing authorities.
    /// </summary>
    public sealed class EnemyCombatActorRuntime
    {
        private readonly EnemyCombatDefinition definition;
        private readonly GameplayEntityIdentity identity;
        private readonly int weightClassValue;
        private readonly IEnemyRangedAttackExecutor rangedExecutor;
        private readonly IEnemyPlayerDamageRouter playerDamageRouter;
        private readonly Dictionary<StableId, DamageReceiverCommand> damageLedger =
            new Dictionary<StableId, DamageReceiverCommand>();
        private readonly Dictionary<StableId, EnemyAttackIntent> acceptedIntents =
            new Dictionary<StableId, EnemyAttackIntent>();
        private readonly Dictionary<StableId, StableId> consumedPounces =
            new Dictionary<StableId, StableId>();

        private EnemyActorState state;
        private long generation;
        private long nextAttackTick;
        private StableId targetId;
        private StableId phaseId;
        private EnemyDecisionEvaluation lastEvaluation;
        private EnemyAttackIntent lastIntent;
        private EnemyPounceCommitment pounce;
        private EnemyCombatDeathFact lastDeath;

        public EnemyCombatActorRuntime(
            EnemyCombatDefinition definition,
            StableId actorInstanceId,
            GameplayEntityOwnership ownership,
            int weightClassValue,
            long initialLifecycleGeneration,
            IEnemyRangedAttackExecutor rangedExecutor,
            IEnemyPlayerDamageRouter playerDamageRouter)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (actorInstanceId == null) throw new ArgumentNullException(nameof(actorInstanceId));
            if (ownership == null) throw new ArgumentNullException(nameof(ownership));
            if (weightClassValue < 1 || weightClassValue > 4)
                throw new ArgumentOutOfRangeException(nameof(weightClassValue));
            if (initialLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(initialLifecycleGeneration));
            if (definition.AttackKind == EnemyAttackCapabilityKind.RangedWeapon
                && rangedExecutor == null)
            {
                throw new ArgumentNullException(
                    nameof(rangedExecutor),
                    "Ranged definitions require a WeaponExecutionCore-backed executor.");
            }

            this.rangedExecutor = rangedExecutor;
            this.playerDamageRouter = playerDamageRouter
                ?? throw new ArgumentNullException(nameof(playerDamageRouter));
            this.weightClassValue = weightClassValue;
            identity = new GameplayEntityIdentity(
                actorInstanceId,
                ownership,
                definition.FactionId);
            generation = initialLifecycleGeneration;
            state = definition.CreateInitialState(actorInstanceId, weightClassValue);
            phaseId = definition.ReadyPhaseId;
        }

        public EnemyCombatDefinition Definition { get { return definition; } }
        public GameplayEntityIdentity Identity { get { return identity; } }
        public EnemyActorState State { get { return state; } }
        public long LifecycleGeneration { get { return generation; } }
        public long NextAllowedAttackTick { get { return nextAttackTick; } }
        public EnemyDecisionEvaluation LastDecisionEvaluation { get { return lastEvaluation; } }
        public EnemyAttackIntent LastLockedIntent { get { return lastIntent; } }
        public EnemyPounceCommitment ActivePounceCommitment { get { return pounce; } }
        public EnemyCombatDeathFact LastDeathFact { get { return lastDeath; } }

        public EnemyRuntimeProjection CurrentProjection
        {
            get
            {
                return new EnemyRuntimeProjection(
                    identity,
                    definition.CreateDefinitionProjection(),
                    state,
                    generation,
                    targetId,
                    phaseId);
            }
        }

        public bool BlocksRoomClear { get { return CurrentProjection.BlocksRoomClear; } }

        public EnemyAttackCommitResult EvaluateAndCommitAttack(
            EnemyPerceptionSnapshot perception,
            EnemyVector2 committedOrigin,
            ulong deterministicSeed)
        {
            if (perception == null) throw new ArgumentNullException(nameof(perception));
            phaseId = perception.SimulationTick >= nextAttackTick
                ? definition.ReadyPhaseId
                : definition.CooldownPhaseId;
            lastEvaluation = EnemyDecisionPolicy.Evaluate(
                CurrentProjection,
                definition.CreateDecisionProfile(),
                perception,
                committedOrigin);
            targetId = lastEvaluation.Decision.SelectedTargetId;

            if (!state.IsActive)
                return Commit(EnemyAttackCommitStatus.ActorInactive, null, null, null);

            EnemyAttackIntent intent = lastEvaluation.Decision.RequestedAttack;
            if (intent == null)
                return Commit(EnemyAttackCommitStatus.NoAttack, null, null, null);

            EnemyAttackIntent existing;
            if (acceptedIntents.TryGetValue(intent.DecisionId, out existing))
            {
                return Commit(
                    definition.AttackKind == EnemyAttackCapabilityKind.MeleePounce
                        ? EnemyAttackCommitStatus.PounceDuplicate
                        : EnemyAttackCommitStatus.RangedDuplicate,
                    existing,
                    pounce,
                    null);
            }

            if (definition.AttackKind == EnemyAttackCapabilityKind.MeleePounce)
            {
                lastIntent = intent;
                pounce = new EnemyPounceCommitment(intent);
                acceptedIntents.Add(intent.DecisionId, intent);
                EnterCooldown(perception.SimulationTick);
                return Commit(
                    EnemyAttackCommitStatus.PounceCommitted,
                    intent,
                    pounce,
                    null);
            }

            EnemyRangedExecutionResult ranged = rangedExecutor.TryExecute(
                intent,
                generation,
                perception.SimulationTick,
                deterministicSeed);
            if (ranged == null || ranged.Status == EnemyRangedExecutionStatus.Rejected)
                return Commit(EnemyAttackCommitStatus.RangedRejected, intent, null, ranged);

            lastIntent = intent;
            acceptedIntents.Add(intent.DecisionId, intent);
            EnterCooldown(perception.SimulationTick);
            return Commit(
                ranged.Status == EnemyRangedExecutionStatus.Accepted
                    ? EnemyAttackCommitStatus.RangedAccepted
                    : EnemyAttackCommitStatus.RangedDuplicate,
                intent,
                null,
                ranged);
        }

        public EnemyAttackImpactResult ApplyLockedAttackImpact(
            StableId impactEventId,
            EnemyAttackIntent lockedIntent,
            StableId contactedTargetId,
            bool contactOccurred,
            long targetLifecycleGeneration)
        {
            if (impactEventId == null
                || lockedIntent == null
                || contactedTargetId == null
                || targetLifecycleGeneration < 0L)
                return Impact("enemy-impact-invalid");

            EnemyAttackIntent accepted;
            if (!state.IsActive
                || lockedIntent.AttackerEntityId != identity.EntityInstanceId
                || !acceptedIntents.TryGetValue(lockedIntent.DecisionId, out accepted)
                || !SameIntent(accepted, lockedIntent)
                || !contactOccurred
                || contactedTargetId != lockedIntent.TargetEntityId)
                return Impact("enemy-impact-not-from-accepted-locked-intent");

            if (definition.AttackKind == EnemyAttackCapabilityKind.MeleePounce)
            {
                if (pounce == null || pounce.AttackIntent.DecisionId != lockedIntent.DecisionId)
                    return Impact("enemy-pounce-commitment-unavailable");
                EnemyPounceImpactOpportunity opportunity =
                    pounce.ObserveImpact(contactedTargetId, contactOccurred);
                if (!opportunity.CanImpact || opportunity.ContactedEntityId != pounce.CommittedTargetId)
                    return Impact("enemy-pounce-contact-invalid");

                StableId priorEvent;
                if (consumedPounces.TryGetValue(lockedIntent.DecisionId, out priorEvent)
                    && priorEvent != impactEventId)
                    return Impact("enemy-pounce-already-consumed");
            }

            DamageReceiverResult applied = playerDamageRouter.ApplyEnemyDamage(
                new DamageReceiverCommand(
                    impactEventId,
                    identity.EntityInstanceId,
                    lockedIntent.SourceRunParticipantId,
                    contactedTargetId,
                    definition.Damage,
                    definition.DamageChannel,
                    targetLifecycleGeneration));
            if (applied == null) return Impact("enemy-player-damage-router-null-result");

            if (definition.AttackKind == EnemyAttackCapabilityKind.MeleePounce
                && (applied.Status == DamageReceiverStatus.Applied
                    || applied.Status == DamageReceiverStatus.Duplicate))
                consumedPounces[lockedIntent.DecisionId] = impactEventId;

            if (applied.Status == DamageReceiverStatus.Applied)
                return new EnemyAttackImpactResult(
                    EnemyAttackImpactStatus.Applied,
                    string.Empty,
                    applied);
            if (applied.Status == DamageReceiverStatus.Duplicate)
                return new EnemyAttackImpactResult(
                    EnemyAttackImpactStatus.Duplicate,
                    string.Empty,
                    applied);
            return new EnemyAttackImpactResult(
                EnemyAttackImpactStatus.Rejected,
                "enemy-player-damage-rejected:" + applied.RejectionCode,
                applied);
        }

        public EnemyIncomingDamageResult ApplyIncomingDamage(DamageReceiverCommand command)
        {
            string invalid = ValidateIncomingDamage(command);
            if (invalid.Length > 0)
                return Incoming(EnemyIncomingDamageStatus.Rejected, invalid, null, null);

            DamageReceiverCommand existing;
            if (damageLedger.TryGetValue(command.EventId, out existing))
            {
                return existing.Equals(command)
                    ? Incoming(EnemyIncomingDamageStatus.Duplicate, string.Empty, null, null)
                    : Incoming(
                        EnemyIncomingDamageStatus.ConflictingDuplicate,
                        "enemy-damage-conflicting-duplicate",
                        null,
                        null);
            }

            EnemyActorStepResult stepped = EnemyActorStepper.Step(
                state,
                new[]
                {
                    EnemyActorCommand.Damage(
                        0L,
                        command.EventId,
                        command.SourceActorId,
                        (int)command.Channel,
                        command.Amount),
                });
            damageLedger.Add(command.EventId, command);
            state = stepped.State;

            EnemyDamageNotification damage = null;
            EnemyDestroyedNotification destroyed = null;
            for (int index = 0; index < stepped.Notifications.Count; index++)
            {
                if (damage == null)
                    damage = stepped.Notifications[index] as EnemyDamageNotification;
                if (destroyed == null)
                    destroyed = stepped.Notifications[index] as EnemyDestroyedNotification;
            }

            EnemyCombatDeathFact emitted = null;
            if (destroyed != null)
            {
                emitted = new EnemyCombatDeathFact(
                    new EnemyAttributedDeathFact(
                        destroyed,
                        command.SourceRunParticipantId,
                        generation),
                    definition);
                lastDeath = emitted;
                pounce = null;
                targetId = null;
            }

            EnemyIncomingDamageStatus status = damage != null
                && damage.ResultValue == EnemyActorStepper.DamageTargetAlreadyDestroyedResultValue
                ? EnemyIncomingDamageStatus.TargetAlreadyDestroyed
                : EnemyIncomingDamageStatus.Applied;
            return Incoming(status, string.Empty, damage, emitted);
        }

        public void Restart(long replacementGeneration)
        {
            if (replacementGeneration != generation + 1L)
                throw new ArgumentOutOfRangeException(nameof(replacementGeneration));
            generation = replacementGeneration;
            state = definition.CreateInitialState(identity.EntityInstanceId, weightClassValue);
            nextAttackTick = 0L;
            targetId = null;
            phaseId = definition.ReadyPhaseId;
            lastEvaluation = null;
            lastIntent = null;
            pounce = null;
            lastDeath = null;
            damageLedger.Clear();
            acceptedIntents.Clear();
            consumedPounces.Clear();
        }

        private void EnterCooldown(long tick)
        {
            nextAttackTick = tick > long.MaxValue - definition.CooldownTicks
                ? long.MaxValue
                : tick + definition.CooldownTicks;
            phaseId = definition.CooldownPhaseId;
        }

        private string ValidateIncomingDamage(DamageReceiverCommand command)
        {
            if (command == null) return "enemy-damage-null-command";
            if (command.EventId == null) return "enemy-damage-missing-event";
            if (command.SourceActorId == null) return "enemy-damage-missing-source";
            if (command.TargetActorId != identity.EntityInstanceId)
                return "enemy-damage-target-mismatch";
            if (command.LifecycleGeneration != generation)
            {
                return command.LifecycleGeneration < generation
                    ? "enemy-damage-stale-generation"
                    : "enemy-damage-future-generation";
            }
            if (double.IsNaN(command.Amount)
                || double.IsInfinity(command.Amount)
                || command.Amount <= 0d)
                return "enemy-damage-invalid-amount";
            if (!Enum.IsDefined(typeof(CombatChannel), command.Channel)
                || command.Channel == CombatChannel.System)
                return "enemy-damage-invalid-channel";
            return string.Empty;
        }

        private EnemyAttackCommitResult Commit(
            EnemyAttackCommitStatus status,
            EnemyAttackIntent intent,
            EnemyPounceCommitment commitment,
            EnemyRangedExecutionResult ranged)
        {
            return new EnemyAttackCommitResult(
                status,
                lastEvaluation,
                intent,
                commitment,
                ranged);
        }

        private EnemyIncomingDamageResult Incoming(
            EnemyIncomingDamageStatus status,
            string code,
            EnemyDamageNotification damage,
            EnemyCombatDeathFact death)
        {
            return new EnemyIncomingDamageResult(status, code, state, damage, death);
        }

        private static EnemyAttackImpactResult Impact(string code)
        {
            return new EnemyAttackImpactResult(
                EnemyAttackImpactStatus.Rejected,
                code,
                null);
        }

        private static bool SameIntent(EnemyAttackIntent left, EnemyAttackIntent right)
        {
            return left != null
                && right != null
                && left.DecisionId == right.DecisionId
                && left.AttackerEntityId == right.AttackerEntityId
                && left.SourceRunParticipantId == right.SourceRunParticipantId
                && left.TargetEntityId == right.TargetEntityId
                && left.AttackId == right.AttackId
                && left.CommittedOrigin.Equals(right.CommittedOrigin)
                && left.CommittedDirection.Equals(right.CommittedDirection)
                && left.CommittedTargetPoint.Equals(right.CommittedTargetPoint);
        }
    }
}

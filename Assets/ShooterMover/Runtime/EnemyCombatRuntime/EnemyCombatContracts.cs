using System;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyCombatRuntime
{
    public enum EnemyAttackCommitStatus
    {
        NoAttack = 1,
        RangedAccepted = 2,
        RangedDuplicate = 3,
        RangedRejected = 4,
        PounceCommitted = 5,
        PounceDuplicate = 6,
        ActorInactive = 7,
    }

    public enum EnemyRangedExecutionStatus { Accepted = 1, Duplicate = 2, Rejected = 3 }

    public sealed class EnemyRangedExecutionResult
    {
        private EnemyRangedExecutionResult(
            EnemyRangedExecutionStatus status,
            string rejectionCode,
            WeaponExecutionResult executionResult)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            ExecutionResult = executionResult;
        }

        public EnemyRangedExecutionStatus Status { get; }
        public string RejectionCode { get; }
        public WeaponExecutionResult ExecutionResult { get; }

        public static EnemyRangedExecutionResult Accept(WeaponExecutionResult result)
        {
            return new EnemyRangedExecutionResult(
                EnemyRangedExecutionStatus.Accepted,
                string.Empty,
                result);
        }

        public static EnemyRangedExecutionResult Duplicate(WeaponExecutionResult result)
        {
            return new EnemyRangedExecutionResult(
                EnemyRangedExecutionStatus.Duplicate,
                string.Empty,
                result);
        }

        public static EnemyRangedExecutionResult Reject(
            string code,
            WeaponExecutionResult result = null)
        {
            return new EnemyRangedExecutionResult(
                EnemyRangedExecutionStatus.Rejected,
                code,
                result);
        }
    }

    public interface IEnemyRangedAttackExecutor
    {
        EnemyRangedExecutionResult TryExecute(
            EnemyAttackIntent lockedIntent,
            long lifecycleGeneration,
            long simulationTick,
            ulong deterministicSeed);
    }

    public interface IEnemyPlayerDamageRouter
    {
        DamageReceiverResult ApplyEnemyDamage(DamageReceiverCommand command);
    }

    public sealed class EnemyAttackCommitResult
    {
        internal EnemyAttackCommitResult(
            EnemyAttackCommitStatus status,
            EnemyDecisionEvaluation evaluation,
            EnemyAttackIntent lockedIntent,
            EnemyPounceCommitment pounceCommitment,
            EnemyRangedExecutionResult rangedExecution)
        {
            Status = status;
            Evaluation = evaluation;
            LockedIntent = lockedIntent;
            PounceCommitment = pounceCommitment;
            RangedExecution = rangedExecution;
        }

        public EnemyAttackCommitStatus Status { get; }
        public EnemyDecisionEvaluation Evaluation { get; }
        public EnemyAttackIntent LockedIntent { get; }
        public EnemyPounceCommitment PounceCommitment { get; }
        public EnemyRangedExecutionResult RangedExecution { get; }
    }

    public enum EnemyAttackImpactStatus { Applied = 1, Duplicate = 2, Rejected = 3 }

    public sealed class EnemyAttackImpactResult
    {
        internal EnemyAttackImpactResult(
            EnemyAttackImpactStatus status,
            string rejectionCode,
            DamageReceiverResult damageResult)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            DamageResult = damageResult;
        }

        public EnemyAttackImpactStatus Status { get; }
        public string RejectionCode { get; }
        public DamageReceiverResult DamageResult { get; }
    }

    public enum EnemyIncomingDamageStatus
    {
        Applied = 1,
        Duplicate = 2,
        ConflictingDuplicate = 3,
        Rejected = 4,
        TargetAlreadyDestroyed = 5,
    }

    /// <summary>Immutable downstream input; it does not award XP or generate drops.</summary>
    public sealed class EnemyCombatDeathFact
    {
        internal EnemyCombatDeathFact(
            EnemyAttributedDeathFact attribution,
            EnemyCombatDefinition definition)
        {
            Attribution = attribution ?? throw new ArgumentNullException(nameof(attribution));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            DefinitionId = definition.DefinitionId;
            Level = definition.Level;
            XpValue = definition.XpValue;
            DropProfileId = definition.DropProfileId;
            FactionId = definition.FactionId;
            RoomClearRole = definition.RoomClearRole;
            PresentationReferenceId = definition.PresentationReferenceId;
        }

        public EnemyAttributedDeathFact Attribution { get; }
        public StableId EventId { get { return Attribution.EventId; } }
        public StableId SourceEntityId { get { return Attribution.SourceEntityId; } }
        public StableId SourceRunParticipantId { get { return Attribution.SourceRunParticipantId; } }
        public StableId TargetEntityId { get { return Attribution.TargetEntityId; } }
        public long LifecycleGeneration { get { return Attribution.LifecycleGeneration; } }
        public StableId DefinitionId { get; }
        public int Level { get; }
        public long XpValue { get; }
        public StableId DropProfileId { get; }
        public StableId FactionId { get; }
        public EnemyRoomClearRole RoomClearRole { get; }
        public StableId PresentationReferenceId { get; }
    }

    public sealed class EnemyIncomingDamageResult
    {
        internal EnemyIncomingDamageResult(
            EnemyIncomingDamageStatus status,
            string rejectionCode,
            EnemyActorState state,
            EnemyDamageNotification damageNotification,
            EnemyCombatDeathFact emittedDeathFact)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            State = state ?? throw new ArgumentNullException(nameof(state));
            DamageNotification = damageNotification;
            EmittedDeathFact = emittedDeathFact;
        }

        public EnemyIncomingDamageStatus Status { get; }
        public string RejectionCode { get; }
        public EnemyActorState State { get; }
        public EnemyDamageNotification DamageNotification { get; }
        public EnemyCombatDeathFact EmittedDeathFact { get; }
    }
}

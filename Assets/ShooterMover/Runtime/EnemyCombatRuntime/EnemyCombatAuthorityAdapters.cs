using System;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyCombatRuntime
{
    /// <summary>
    /// Converts an accepted, locked enemy attack intent into the shared atomic weapon core.
    /// The concrete equipment instance is supplied by runtime composition, not inferred from
    /// an enemy name or weapon-name switch.
    /// </summary>
    public sealed class WeaponCoreEnemyRangedAttackExecutor : IEnemyRangedAttackExecutor
    {
        private readonly WeaponExecutionCore core;
        private readonly EquipmentInstanceId equipmentInstanceId;

        public WeaponCoreEnemyRangedAttackExecutor(
            WeaponExecutionCore core,
            EquipmentInstanceId equipmentInstanceId)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));
            this.equipmentInstanceId = equipmentInstanceId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
        }

        public EquipmentInstanceId EquipmentInstanceId { get { return equipmentInstanceId; } }
        public WeaponExecutionResult LastExecutionResult { get; private set; }

        public EnemyRangedExecutionResult TryExecute(
            EnemyAttackIntent lockedIntent,
            long lifecycleGeneration,
            long simulationTick,
            ulong deterministicSeed)
        {
            if (lockedIntent == null
                || lifecycleGeneration < 0L
                || simulationTick < 0L)
            {
                return EnemyRangedExecutionResult.Reject("enemy-ranged-command-invalid");
            }

            WeaponFireCommand command = new WeaponFireCommand(
                new WeaponActorInstanceId(lockedIntent.AttackerEntityId),
                equipmentInstanceId,
                new FireOperationId(lockedIntent.DecisionId),
                new LifecycleGeneration(lifecycleGeneration),
                simulationTick,
                deterministicSeed,
                new WeaponVector2(
                    lockedIntent.CommittedOrigin.X,
                    lockedIntent.CommittedOrigin.Y),
                new WeaponVector2(
                    lockedIntent.CommittedDirection.X,
                    lockedIntent.CommittedDirection.Y));
            LastExecutionResult = core.TryExecute(command);
            if (LastExecutionResult == null)
            {
                return EnemyRangedExecutionResult.Reject("enemy-weapon-core-null-result");
            }

            if (LastExecutionResult.Status == WeaponExecutionStatus.Accepted)
            {
                return EnemyRangedExecutionResult.Accept(LastExecutionResult);
            }

            if (LastExecutionResult.Status == WeaponExecutionStatus.ReplayAccepted)
            {
                return EnemyRangedExecutionResult.Duplicate(LastExecutionResult);
            }

            return EnemyRangedExecutionResult.Reject(
                "enemy-weapon-core-rejected:"
                    + LastExecutionResult.Status
                    + ":"
                    + LastExecutionResult.RejectionCode,
                LastExecutionResult);
        }
    }

    /// <summary>
    /// Narrow routing adapter proving that live enemy impacts never mutate player health
    /// directly. All validation, replay handling, health, and player death remain inside
    /// PlayerActorAuthority.
    /// </summary>
    public sealed class PlayerActorEnemyDamageRouter : IEnemyPlayerDamageRouter
    {
        private readonly PlayerActorAuthority authority;

        public PlayerActorEnemyDamageRouter(PlayerActorAuthority authority)
        {
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
        }

        public DamageReceiverResult ApplyEnemyDamage(DamageReceiverCommand command)
        {
            return authority.ApplyDamage(command);
        }
    }
}

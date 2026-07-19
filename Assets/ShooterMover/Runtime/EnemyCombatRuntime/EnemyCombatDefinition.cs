using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyCombatRuntime
{
    public enum EnemyAttackCapabilityKind
    {
        RangedWeapon = 1,
        MeleePounce = 2,
    }

    /// <summary>
    /// Immutable live-combat facts for one enemy definition. Reward values are facts only:
    /// this type never grants XP, rolls a drop, mutates inventory, or resolves room state.
    /// </summary>
    public sealed class EnemyCombatDefinition
    {
        public EnemyCombatDefinition(
            StableId definitionId,
            double maximumHealth,
            int level,
            double detectionRadius,
            double visionArcDegrees,
            double attackArcDegrees,
            double minimumAttackRange,
            double preferredAttackRange,
            double maximumAttackRange,
            int cooldownTicks,
            double damage,
            CombatChannel damageChannel,
            long xpValue,
            StableId factionId,
            EnemyRoomClearRole roomClearRole,
            StableId presentationReferenceId,
            StableId dropProfileId,
            StableId movementProfileId,
            StableId attackId,
            StableId readyPhaseId,
            StableId cooldownPhaseId,
            EnemyAttackCapabilityKind attackKind)
        {
            DefinitionId = RequireId(definitionId, nameof(definitionId));
            MaximumHealth = RequirePositive(maximumHealth, nameof(maximumHealth));
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            Level = level;
            DetectionRadius = RequirePositive(detectionRadius, nameof(detectionRadius));
            VisionArcDegrees = RequireArc(visionArcDegrees, nameof(visionArcDegrees));
            AttackArcDegrees = RequireArc(attackArcDegrees, nameof(attackArcDegrees));
            MinimumAttackRange = RequireNonNegative(minimumAttackRange, nameof(minimumAttackRange));
            PreferredAttackRange = RequireNonNegative(preferredAttackRange, nameof(preferredAttackRange));
            MaximumAttackRange = RequireNonNegative(maximumAttackRange, nameof(maximumAttackRange));
            if (MinimumAttackRange > PreferredAttackRange
                || PreferredAttackRange > MaximumAttackRange
                || MaximumAttackRange > DetectionRadius)
            {
                throw new ArgumentException(
                    "Enemy attack ranges must be ordered and remain inside detection radius.");
            }

            if (cooldownTicks < 1) throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            CooldownTicks = cooldownTicks;
            Damage = RequirePositive(damage, nameof(damage));
            if (!Enum.IsDefined(typeof(CombatChannel), damageChannel)
                || damageChannel == CombatChannel.System)
            {
                throw new ArgumentOutOfRangeException(nameof(damageChannel));
            }

            DamageChannel = damageChannel;
            if (xpValue < 0L) throw new ArgumentOutOfRangeException(nameof(xpValue));
            XpValue = xpValue;
            FactionId = RequireId(factionId, nameof(factionId));
            if (!Enum.IsDefined(typeof(EnemyRoomClearRole), roomClearRole))
            {
                throw new ArgumentOutOfRangeException(nameof(roomClearRole));
            }

            RoomClearRole = roomClearRole;
            PresentationReferenceId = RequireId(
                presentationReferenceId,
                nameof(presentationReferenceId));
            DropProfileId = RequireId(dropProfileId, nameof(dropProfileId));
            MovementProfileId = RequireId(movementProfileId, nameof(movementProfileId));
            AttackId = RequireId(attackId, nameof(attackId));
            ReadyPhaseId = RequireId(readyPhaseId, nameof(readyPhaseId));
            CooldownPhaseId = RequireId(cooldownPhaseId, nameof(cooldownPhaseId));
            if (!Enum.IsDefined(typeof(EnemyAttackCapabilityKind), attackKind))
            {
                throw new ArgumentOutOfRangeException(nameof(attackKind));
            }

            AttackKind = attackKind;
        }

        public StableId DefinitionId { get; }
        public double MaximumHealth { get; }
        public int Level { get; }
        public double DetectionRadius { get; }
        public double VisionArcDegrees { get; }
        public double AttackArcDegrees { get; }
        public double MinimumAttackRange { get; }
        public double PreferredAttackRange { get; }
        public double MaximumAttackRange { get; }
        public int CooldownTicks { get; }
        public double Damage { get; }
        public CombatChannel DamageChannel { get; }
        public long XpValue { get; }
        public StableId FactionId { get; }
        public EnemyRoomClearRole RoomClearRole { get; }
        public StableId PresentationReferenceId { get; }
        public StableId DropProfileId { get; }
        public StableId MovementProfileId { get; }
        public StableId AttackId { get; }
        public StableId ReadyPhaseId { get; }
        public StableId CooldownPhaseId { get; }
        public EnemyAttackCapabilityKind AttackKind { get; }

        public EnemyDecisionProfile CreateDecisionProfile()
        {
            return new EnemyDecisionProfile(
                DetectionRadius,
                MinimumAttackRange,
                PreferredAttackRange,
                MaximumAttackRange,
                AttackArcDegrees,
                AttackId,
                ReadyPhaseId);
        }

        public EnemyDefinitionProjection CreateDefinitionProjection()
        {
            return new EnemyDefinitionProjection(
                DefinitionId,
                MovementProfileId,
                new[] { AttackId },
                new[] { DropProfileId },
                RoomClearRole);
        }

        internal EnemyActorState CreateInitialState(StableId actorId, int weightClassValue)
        {
            EnemyContactPolicy contact = EnemyContactPolicy.Create(
                EnemyContactMode.None,
                0d,
                0.5d,
                0.02d,
                8);
            return EnemyActorState.Create(
                RequireId(actorId, nameof(actorId)),
                DefinitionId,
                MaximumHealth,
                weightClassValue,
                contact);
        }

        private static StableId RequireId(StableId value, string parameterName)
        {
            return value ?? throw new ArgumentNullException(parameterName);
        }

        private static double RequirePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static double RequireNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static double RequireArc(double value, string parameterName)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value <= 0d
                || value > 360d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Compact production-shaped examples. They are definition data, not enemy controllers.
    /// </summary>
    public static class EnemyCombatExampleDefinitions
    {
        public static EnemyCombatDefinition MobileBlasterDroid()
        {
            return new EnemyCombatDefinition(
                StableId.Parse("enemy.mobile-blaster-droid"),
                16d,
                1,
                20d,
                360d,
                90d,
                0d,
                5d,
                12d,
                6,
                10d,
                CombatChannel.Kinetic,
                20L,
                StableId.Parse("faction.enemy"),
                EnemyRoomClearRole.RequiredEnemy,
                StableId.Parse("presentation.moving-droid-sprite"),
                StableId.Parse("drop-profile.mobile-blaster-droid"),
                StableId.Parse("module.enemy-mobile-positioning"),
                StableId.Parse("weapon.blaster-machine-gun"),
                StableId.Parse("enemy-phase.mobile-blaster-droid-ready"),
                StableId.Parse("enemy-phase.mobile-blaster-droid-cooldown"),
                EnemyAttackCapabilityKind.RangedWeapon);
        }

        public static EnemyCombatDefinition Pouncer()
        {
            return new EnemyCombatDefinition(
                StableId.Parse("enemy.pouncer"),
                12d,
                1,
                12d,
                180d,
                70d,
                0d,
                1.5d,
                2.5d,
                30,
                8d,
                CombatChannel.Contact,
                15L,
                StableId.Parse("faction.enemy"),
                EnemyRoomClearRole.RequiredEnemy,
                StableId.Parse("presentation.pouncer-sprite"),
                StableId.Parse("drop-profile.pouncer"),
                StableId.Parse("module.enemy-pounce"),
                StableId.Parse("attack.enemy-pounce"),
                StableId.Parse("enemy-phase.pouncer-ready"),
                StableId.Parse("enemy-phase.pouncer-cooldown"),
                EnemyAttackCapabilityKind.MeleePounce);
        }
    }
}

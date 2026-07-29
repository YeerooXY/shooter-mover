using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed partial class EnemyAttackPatternStateTests
    {
        private static EnemyAttackSequence Sequence(
            EnemyAttackCapabilityDescriptor descriptor,
            string operationSuffix,
            double occurredAtSeconds)
        {
            EnemyLiveIdentity identity = Identity();
            return EnemyAttackPatternScheduler.Schedule(
                Execution(identity, descriptor, operationSuffix, occurredAtSeconds));
        }

        private static EnemyAttackExecutionRequest Execution(
            EnemyLiveIdentity identity,
            EnemyAttackCapabilityDescriptor descriptor,
            string operationSuffix,
            double occurredAtSeconds,
            long lifecycleGeneration = 4L)
        {
            StableId intentAttackId = descriptor.AttackId
                ?? Id("enemy-attack-profile.invalid-command-intent");
            var intent = new EnemyAttackIntent(
                identity.EntityInstanceId,
                identity.RunParticipantId,
                Id("player-entity.target"),
                intentAttackId,
                new EnemyVector2(1d, 2d),
                new EnemyVector2(1d, 0d),
                new EnemyVector2(8d, 2d),
                Id("enemy-decision.fixture"),
                Id("enemy-phase.ready"),
                Id("enemy-decision-reason.attack-ready"));
            EnemyAttackExecutionKind kind;
            if (descriptor.MeleePattern != null)
            {
                kind = descriptor.MeleePattern.LungeDistance > 0d
                    ? EnemyAttackExecutionKind.Pounce
                    : EnemyAttackExecutionKind.Contact;
            }
            else
            {
                kind = descriptor.ProjectilePayload != null
                    && descriptor.ProjectilePayload.AreaPayload != null
                    ? EnemyAttackExecutionKind.Area
                    : EnemyAttackExecutionKind.Projectile;
            }
            return new EnemyAttackExecutionRequest(
                Id("enemy-operation." + operationSuffix),
                identity,
                lifecycleGeneration,
                occurredAtSeconds,
                descriptor,
                intent,
                Id("equipment-instance.enemy-fixture"),
                kind,
                descriptor.Damage,
                descriptor.CooldownSeconds > 0d ? descriptor.CooldownSeconds : 1d);
        }

        private static EnemyAttackCapabilityDescriptor Shooting(
            string suffix,
            int shots,
            double interval,
            int projectilesPerShot,
            double spread,
            double windUp,
            double recovery,
            double speed,
            EnemyAreaPayload area)
        {
            return new EnemyAttackCapabilityDescriptor(
                Id("enemy-attack-profile." + suffix),
                Id(area == null
                    ? "enemy-attack.ranged-projectile"
                    : "enemy-attack.projectile-area"),
                10,
                120d,
                0d,
                5d,
                12d,
                3d,
                Id("damage.kinetic"),
                new EnemyShootingPattern(
                    shots,
                    interval,
                    projectilesPerShot,
                    spread,
                    EnemySequenceAimPolicy.LockAtSequenceStart,
                    windUp,
                    recovery,
                    EnemyAttackInterruptionPolicy.CancelPendingOnLifecycleEnd),
                new EnemyProjectilePayload(
                    Id(area == null
                        ? "projectile.enemy-blaster"
                        : "projectile.enemy-turret-shell"),
                    speed,
                    20d,
                    0.15d,
                    0,
                    area),
                null);
        }

        private static EnemyAttackCapabilityDescriptor Melee(
            string suffix,
            double windUp,
            double activeWindow,
            int strikeCount,
            double interval,
            double contactRadius,
            double lungeDistance,
            double recovery,
            EnemyMeleeAimCommitPolicy aim,
            EnemyMeleeTerminalOnImpactPolicy terminal)
        {
            return new EnemyAttackCapabilityDescriptor(
                Id("enemy-attack-profile." + suffix),
                Id(lungeDistance > 0d ? "enemy-attack.pounce" : "enemy-attack.contact"),
                10,
                140d,
                0d,
                0.4d,
                contactRadius + lungeDistance,
                4d,
                Id("damage.impact"),
                null,
                null,
                new EnemyMeleePattern(
                    windUp,
                    activeWindow,
                    strikeCount,
                    interval,
                    contactRadius,
                    lungeDistance,
                    aim,
                    recovery,
                    1,
                    terminal,
                    EnemyAttackInterruptionPolicy.CancelPendingOnLifecycleEnd));
        }

        private static EnemyLiveIdentity Identity()
        {
            return new EnemyLiveIdentity(
                Id("enemy-entity.pattern-fixture"),
                Id("run-participant.enemy-pattern-fixture"),
                Id("run.pattern-fixture"),
                Id("room-runtime.pattern-fixture"),
                Id("room.pattern-fixture"),
                Id("room-placement.pattern-fixture"));
        }

        private static EnemyCatalogRegistry Registry()
        {
            return new EnemyCatalogRegistry(
                Ids(
                    "enemy-movement.mobile-positioning",
                    "enemy-movement.pursuit",
                    "enemy-movement.stationary"),
                Ids(
                    "enemy-decision.ranged-standard",
                    "enemy-decision.pounce-standard",
                    "enemy-decision.turret-standard",
                    "enemy-decision.contact-standard",
                    "enemy-decision.multi-attack-standard"),
                new[]
                {
                    Attack("enemy-attack.ranged-projectile", EnemyAttackParameterKinds.Projectile),
                    Attack("enemy-attack.pounce", EnemyAttackParameterKinds.Melee),
                    Attack(
                        "enemy-attack.projectile-area",
                        EnemyAttackParameterKinds.Projectile | EnemyAttackParameterKinds.Area),
                    Attack("enemy-attack.contact", EnemyAttackParameterKinds.Melee),
                },
                Ids("enemy-special.locked-commitment", "enemy-special.rotating-aim"),
                Ids(
                    "presentation.enemy-mobile-blaster-droid",
                    "presentation.enemy-ram-pouncer",
                    "presentation.enemy-blaster-turret",
                    "presentation.enemy-pursuer-drone",
                    "presentation.enemy-hybrid-sentinel"),
                Ids("projectile.enemy-blaster", "projectile.enemy-turret-shell"),
                Ids("damage.kinetic", "damage.impact", "damage.thermal"),
                Ids("xp.enemy-standard", "xp.enemy-light", "xp.enemy-turret"),
                Ids("drop.enemy-common", "drop.enemy-none", "drop.enemy-turret"));
        }

        private static EnemyAttackCapabilityRegistration Attack(
            string id,
            EnemyAttackParameterKinds parameters)
        {
            return new EnemyAttackCapabilityRegistration(Id(id), parameters, parameters);
        }

        private static StableId[] Ids(params string[] values)
        {
            var result = new List<StableId>();
            for (int index = 0; index < values.Length; index++) result.Add(Id(values[index]));
            return result.ToArray();
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private static string FirstIssue(EnemyCatalogImportResult result)
        {
            return result.Issues.Count == 0 ? string.Empty : result.Issues[0].ToString();
        }
    }
}

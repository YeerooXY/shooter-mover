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
    public sealed partial class EnemyAttackPatternAuthorityV1Tests
    {
        private static EnemyAttackSequenceV1 Sequence(
            EnemyAttackCapabilityDescriptorV1 descriptor,
            string operationSuffix,
            double occurredAtSeconds)
        {
            EnemyRuntimeIdentityV1 identity = Identity();
            return EnemyAttackPatternSchedulerV1.Schedule(
                Execution(identity, descriptor, operationSuffix, occurredAtSeconds));
        }

        private static EnemyAttackExecutionRequestV1 Execution(
            EnemyRuntimeIdentityV1 identity,
            EnemyAttackCapabilityDescriptorV1 descriptor,
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
            EnemyAttackExecutionKindV1 kind;
            if (descriptor.MeleePattern != null)
            {
                kind = descriptor.MeleePattern.LungeDistance > 0d
                    ? EnemyAttackExecutionKindV1.Pounce
                    : EnemyAttackExecutionKindV1.Contact;
            }
            else
            {
                kind = descriptor.ProjectilePayload != null
                    && descriptor.ProjectilePayload.AreaPayload != null
                    ? EnemyAttackExecutionKindV1.Area
                    : EnemyAttackExecutionKindV1.Projectile;
            }
            return new EnemyAttackExecutionRequestV1(
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

        private static EnemyAttackCapabilityDescriptorV1 Shooting(
            string suffix,
            int shots,
            double interval,
            int projectilesPerShot,
            double spread,
            double windUp,
            double recovery,
            double speed,
            EnemyAreaPayloadV1 area)
        {
            return new EnemyAttackCapabilityDescriptorV1(
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
                new EnemyShootingPatternV1(
                    shots,
                    interval,
                    projectilesPerShot,
                    spread,
                    EnemySequenceAimPolicyV1.LockAtSequenceStart,
                    windUp,
                    recovery,
                    EnemyAttackInterruptionPolicyV1.CancelPendingOnLifecycleEnd),
                new EnemyProjectilePayloadV1(
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

        private static EnemyAttackCapabilityDescriptorV1 Melee(
            string suffix,
            double windUp,
            double activeWindow,
            int strikeCount,
            double interval,
            double contactRadius,
            double lungeDistance,
            double recovery,
            EnemyMeleeAimCommitPolicyV1 aim,
            EnemyMeleeTerminalOnImpactPolicyV1 terminal)
        {
            return new EnemyAttackCapabilityDescriptorV1(
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
                new EnemyMeleePatternV1(
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
                    EnemyAttackInterruptionPolicyV1.CancelPendingOnLifecycleEnd));
        }

        private static EnemyRuntimeIdentityV1 Identity()
        {
            return new EnemyRuntimeIdentityV1(
                Id("enemy-entity.pattern-fixture"),
                Id("run-participant.enemy-pattern-fixture"),
                Id("run.pattern-fixture"),
                Id("room-runtime.pattern-fixture"),
                Id("room.pattern-fixture"),
                Id("room-placement.pattern-fixture"));
        }

        private static EnemyCatalogRegistryV1 Registry()
        {
            return new EnemyCatalogRegistryV1(
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
                    Attack("enemy-attack.ranged-projectile", EnemyAttackParameterKindsV1.Projectile),
                    Attack("enemy-attack.pounce", EnemyAttackParameterKindsV1.Melee),
                    Attack(
                        "enemy-attack.projectile-area",
                        EnemyAttackParameterKindsV1.Projectile | EnemyAttackParameterKindsV1.Area),
                    Attack("enemy-attack.contact", EnemyAttackParameterKindsV1.Melee),
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

        private static EnemyAttackCapabilityRegistrationV1 Attack(
            string id,
            EnemyAttackParameterKindsV1 parameters)
        {
            return new EnemyAttackCapabilityRegistrationV1(Id(id), parameters, parameters);
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

        private static string FirstIssue(EnemyCatalogImportResultV1 result)
        {
            return result.Issues.Count == 0 ? string.Empty : result.Issues[0].ToString();
        }
    }
}

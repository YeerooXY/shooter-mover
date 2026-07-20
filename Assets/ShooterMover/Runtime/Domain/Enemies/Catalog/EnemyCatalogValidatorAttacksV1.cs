using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies.Catalog
{
    public static partial class EnemyCatalogValidatorV1
    {
        private static void ValidateAttacks(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyDefinitionV1 definition,
            IEnemyCatalogRegistryV1 registry)
        {
            if (definition.Attacks.Count == 0
                || definition.Attacks.Count > MaximumAttacksPerDefinition)
            {
                Add(
                    issues,
                    "enemy-catalog-attack-count-invalid",
                    path + ".attacks",
                    "Each enemy must define between 1 and "
                    + MaximumAttacksPerDefinition
                    + " attacks.");
            }

            var attackIds = new HashSet<StableId>();
            for (int index = 0; index < definition.Attacks.Count; index++)
            {
                EnemyAttackCapabilityDescriptorV1 attack = definition.Attacks[index];
                string attackPath = path + ".attacks[" + index + "]";
                if (attack == null)
                {
                    Add(
                        issues,
                        "enemy-catalog-attack-invalid",
                        attackPath,
                        "Attack descriptors cannot be null.");
                    continue;
                }
                if (attack.AttackId == null || !attackIds.Add(attack.AttackId))
                {
                    Add(
                        issues,
                        "enemy-catalog-attack-id-invalid",
                        attackPath + ".id",
                        "Attack IDs must be non-null and unique inside one definition.");
                }

                EnemyAttackCapabilityRegistrationV1 registration;
                if (attack.CapabilityId == null
                    || registry == null
                    || !registry.TryResolveAttackCapability(
                        attack.CapabilityId,
                        out registration))
                {
                    Add(
                        issues,
                        "enemy-catalog-attack-capability-unknown",
                        attackPath + ".capability",
                        "Attack capability is not registered: " + Value(attack.CapabilityId));
                }
                else
                {
                    EnemyAttackParameterKindsV1 supplied = attack.ParameterKinds;
                    bool requiredPresent =
                        (supplied & registration.RequiredParameters)
                        == registration.RequiredParameters;
                    bool onlyAllowed =
                        (supplied & ~registration.AllowedParameters)
                        == EnemyAttackParameterKindsV1.None;
                    if (!requiredPresent || !onlyAllowed)
                    {
                        Add(
                            issues,
                            "enemy-catalog-attack-parameters-incompatible",
                            attackPath,
                            "Attack parameters do not match registered capability "
                            + attack.CapabilityId
                            + ".");
                    }
                }

                if (!IsFiniteInRange(
                    attack.CooldownSeconds,
                    0d,
                    MaximumCooldownSeconds,
                    false))
                {
                    Add(
                        issues,
                        "enemy-catalog-attack-invalid",
                        attackPath + ".cooldown_seconds",
                        "Cooldown must be finite, positive, and bounded.");
                }
                if (!IsFiniteInRange(attack.Damage, 0d, MaximumDamage, false))
                {
                    Add(
                        issues,
                        "enemy-catalog-attack-invalid",
                        attackPath + ".damage",
                        "Damage must be finite, positive, and bounded.");
                }
                if (attack.DamageChannelId == null
                    || registry == null
                    || !registry.IsDamageChannelRegistered(attack.DamageChannelId))
                {
                    Add(
                        issues,
                        "enemy-catalog-damage-channel-unknown",
                        attackPath + ".damage_channel",
                        "Damage channel is not registered: " + Value(attack.DamageChannelId));
                }

                ValidateProjectile(issues, attackPath, attack.Projectile, definition);
                ValidateArea(issues, attackPath, attack.Area);
                ValidateMelee(issues, attackPath, attack.Melee);
            }
        }

        private static void ValidateProjectile(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyProjectileAttackParametersV1 projectile,
            EnemyDefinitionV1 definition)
        {
            if (projectile == null) return;
            bool valid = projectile.ProjectileProfileId != null
                && projectile.ProjectileCount >= 1
                && projectile.ProjectileCount <= 256
                && IsFiniteInRange(projectile.ProjectileSpeed, 0d, MaximumDistance, false)
                && IsFiniteInRange(
                    projectile.MaximumTravelDistance,
                    0d,
                    MaximumDistance,
                    false)
                && projectile.MaximumTravelDistance >= definition.MaximumAttackRange
                && IsFiniteInRange(projectile.CollisionRadius, 0d, 1000d, false)
                && IsFiniteInRange(projectile.SpreadDegrees, 0d, 360d, true)
                && projectile.PierceCount >= 0
                && projectile.PierceCount <= 1024;
            if (!valid)
            {
                Add(
                    issues,
                    "enemy-catalog-projectile-parameters-invalid",
                    path + ".projectile",
                    "Projectile profile, count, speed, travel, radius, spread, or pierce is invalid.");
            }
        }

        private static void ValidateArea(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyAreaAttackParametersV1 area)
        {
            if (area == null) return;
            bool valid = IsFiniteInRange(area.Radius, 0d, MaximumDistance, false)
                && IsFiniteInRange(area.DurationSeconds, 0d, MaximumCooldownSeconds, true)
                && area.MaximumTargets >= 1
                && area.MaximumTargets <= 4096;
            if (!valid)
            {
                Add(
                    issues,
                    "enemy-catalog-area-parameters-invalid",
                    path + ".area",
                    "Area radius, duration, or target limit is invalid.");
            }
        }

        private static void ValidateMelee(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyMeleeAttackParametersV1 melee)
        {
            if (melee == null) return;
            bool valid = IsFiniteInRange(melee.ContactRadius, 0d, MaximumDistance, false)
                && IsFiniteInRange(melee.PounceDistance, 0d, MaximumDistance, true)
                && IsFiniteInRange(
                    melee.WindUpSeconds,
                    0d,
                    MaximumCooldownSeconds,
                    true)
                && IsFiniteInRange(
                    melee.CommitmentSeconds,
                    0d,
                    MaximumCooldownSeconds,
                    true);
            if (!valid)
            {
                Add(
                    issues,
                    "enemy-catalog-melee-parameters-invalid",
                    path + ".melee",
                    "Melee radius, pounce distance, wind-up, or commitment is invalid.");
            }
        }

        private static void ValidateArc(
            List<EnemyCatalogIssueV1> issues,
            string path,
            double value)
        {
            if (!IsFiniteInRange(value, 0d, 360d, false))
            {
                Add(
                    issues,
                    "enemy-catalog-arc-invalid",
                    path,
                    "Arc must be finite, greater than zero, and at most 360 degrees.");
            }
        }

        private static bool IsFiniteInRange(
            double value,
            double minimum,
            double maximum,
            bool inclusiveMinimum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            if (inclusiveMinimum ? value < minimum : value <= minimum) return false;
            return value <= maximum;
        }

        private static string Value(StableId value)
        {
            return value == null ? "<null>" : value.ToString();
        }

        private static void Add(
            ICollection<EnemyCatalogIssueV1> issues,
            string code,
            string path,
            string message)
        {
            issues.Add(new EnemyCatalogIssueV1(code, path, message));
        }
    }
}

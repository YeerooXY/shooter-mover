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
            var priorities = new HashSet<int>();
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
                if (attack.SelectionPriority < 0
                    || attack.SelectionPriority > MaximumAttackSelectionPriority
                    || !priorities.Add(attack.SelectionPriority))
                {
                    Add(
                        issues,
                        "enemy-catalog-attack-priority-invalid",
                        attackPath + ".selection_priority",
                        "Selection priorities must be unique, non-negative, and bounded inside one definition.");
                }

                ValidateAttackGeometry(issues, attackPath, attack, definition.DetectionRadius);

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

                ValidateProjectile(issues, attackPath, attack, registry);
                ValidateArea(issues, attackPath, attack.Area);
                ValidateMelee(issues, attackPath, attack);
            }
        }

        private static void ValidateAttackGeometry(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyAttackCapabilityDescriptorV1 attack,
            double detectionRadius)
        {
            ValidateArc(issues, path + ".attack_arc_degrees", attack.AttackArcDegrees);

            bool minimumValid = IsFiniteInRange(
                attack.MinimumAttackRange,
                0d,
                MaximumDistance,
                true);
            bool preferredValid = IsFiniteInRange(
                attack.PreferredAttackRange,
                0d,
                MaximumDistance,
                true);
            bool maximumValid = IsFiniteInRange(
                attack.MaximumAttackRange,
                0d,
                MaximumDistance,
                true);

            if (!minimumValid)
            {
                Add(
                    issues,
                    "enemy-catalog-attack-range-invalid",
                    path + ".minimum_range",
                    "Minimum attack range must be finite, non-negative, and bounded.");
            }
            if (!preferredValid
                || (minimumValid && attack.PreferredAttackRange < attack.MinimumAttackRange))
            {
                Add(
                    issues,
                    "enemy-catalog-attack-range-invalid",
                    path + ".preferred_range",
                    "Preferred attack range must be finite, bounded, and at least the minimum range.");
            }
            if (!maximumValid
                || (preferredValid && attack.MaximumAttackRange < attack.PreferredAttackRange)
                || attack.MaximumAttackRange > detectionRadius)
            {
                Add(
                    issues,
                    "enemy-catalog-attack-range-invalid",
                    path + ".maximum_range",
                    "Maximum attack range must be finite, ordered, bounded, and within detection radius.");
            }
        }

        private static void ValidateProjectile(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyAttackCapabilityDescriptorV1 attack,
            IEnemyCatalogRegistryV1 registry)
        {
            EnemyProjectileAttackParametersV1 projectile = attack.Projectile;
            if (projectile == null) return;

            if (projectile.ProjectileProfileId == null
                || registry == null
                || !registry.IsProjectileProfileRegistered(projectile.ProjectileProfileId))
            {
                Add(
                    issues,
                    "enemy-catalog-projectile-profile-unknown",
                    path + ".projectile.profile",
                    "Projectile profile is not registered: "
                    + Value(projectile.ProjectileProfileId));
            }

            bool physicalValuesValid = projectile.ProjectileCount >= 1
                && projectile.ProjectileCount <= 256
                && IsFiniteInRange(projectile.ProjectileSpeed, 0d, MaximumDistance, false)
                && IsFiniteInRange(
                    projectile.MaximumTravelDistance,
                    0d,
                    MaximumDistance,
                    false)
                && IsFiniteInRange(projectile.CollisionRadius, 0d, 1000d, false)
                && IsFiniteInRange(projectile.SpreadDegrees, 0d, 360d, true)
                && projectile.PierceCount >= 0
                && projectile.PierceCount <= 1024;
            if (!physicalValuesValid)
            {
                Add(
                    issues,
                    "enemy-catalog-projectile-parameters-invalid",
                    path + ".projectile",
                    "Projectile count, speed, travel, radius, spread, or pierce is invalid.");
            }
            if (IsFiniteInRange(
                    projectile.MaximumTravelDistance,
                    0d,
                    MaximumDistance,
                    false)
                && projectile.MaximumTravelDistance < attack.MaximumAttackRange)
            {
                Add(
                    issues,
                    "enemy-catalog-projectile-range-invalid",
                    path + ".projectile.maximum_travel_distance",
                    "Projectile travel distance must support this attack's maximum range.");
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
            EnemyAttackCapabilityDescriptorV1 attack)
        {
            EnemyMeleeAttackParametersV1 melee = attack.Melee;
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
                return;
            }

            double supportedRange = melee.ContactRadius + melee.PounceDistance;
            if (attack.MaximumAttackRange > supportedRange)
            {
                Add(
                    issues,
                    "enemy-catalog-melee-range-incompatible",
                    path + ".maximum_range",
                    "Melee/contact reach must support this attack's maximum range.");
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

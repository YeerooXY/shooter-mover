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
            IEnemyCatalogRegistryV1 registry,
            int schemaVersion)
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

                if (schemaVersion <= 1)
                {
                    ValidateLegacyAttack(issues, attackPath, attack, registry);
                }
                else
                {
                    ValidatePatternAttack(issues, attackPath, attack, registry);
                }
            }
        }

        private static void ValidateLegacyAttack(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyAttackCapabilityDescriptorV1 attack,
            IEnemyCatalogRegistryV1 registry)
        {
            if (!IsFiniteInRange(
                attack.CooldownSeconds,
                0d,
                MaximumCooldownSeconds,
                false))
            {
                Add(
                    issues,
                    "enemy-catalog-attack-invalid",
                    path + ".cooldown_seconds",
                    "Cooldown must be finite, positive, and bounded.");
            }
            ValidateLegacyProjectile(issues, path, attack, registry);
            ValidateLegacyArea(issues, path, attack.Area);
            ValidateLegacyMelee(issues, path, attack);
        }

        private static void ValidatePatternAttack(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyAttackCapabilityDescriptorV1 attack,
            IEnemyCatalogRegistryV1 registry)
        {
            bool shooting = attack.ShootingPattern != null;
            bool melee = attack.MeleePattern != null;
            if (shooting == melee)
            {
                Add(
                    issues,
                    "enemy-catalog-attack-pattern-invalid",
                    path,
                    "Exactly one shooting_pattern or melee_pattern is required.");
                return;
            }

            if (shooting)
            {
                ValidateShootingPattern(issues, path, attack.ShootingPattern);
                ValidateProjectilePayload(issues, path, attack, registry);
            }
            else
            {
                if (attack.ProjectilePayload != null)
                {
                    Add(
                        issues,
                        "enemy-catalog-projectile-payload-unexpected",
                        path + ".projectile_payload",
                        "Melee attacks cannot author a projectile_payload.");
                }
                ValidateMeleePattern(issues, path, attack);
            }

            if (!IsFiniteInRange(
                attack.CooldownSeconds,
                0d,
                MaximumCooldownSeconds,
                false))
            {
                Add(
                    issues,
                    "enemy-catalog-attack-pattern-timing-invalid",
                    shooting ? path + ".shooting_pattern" : path + ".melee_pattern",
                    "Wind-up, intervals, active windows, and recovery must form a positive bounded sequence.");
            }
        }

        private static void ValidateShootingPattern(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyShootingPatternV1 pattern)
        {
            bool valid = pattern.ShotsPerSequence >= 1
                && pattern.ShotsPerSequence <= 1024
                && IsFiniteInRange(
                    pattern.IntervalBetweenShotsSeconds,
                    0d,
                    MaximumCooldownSeconds,
                    true)
                && pattern.ProjectilesPerShot >= 1
                && pattern.ProjectilesPerShot <= 256
                && IsFiniteInRange(pattern.PerShotSpreadDegrees, 0d, 360d, true)
                && IsFiniteInRange(
                    pattern.WindUpSeconds,
                    0d,
                    MaximumCooldownSeconds,
                    true)
                && IsFiniteInRange(
                    pattern.PostSequenceRecoverySeconds,
                    0d,
                    MaximumCooldownSeconds,
                    true)
                && Enum.IsDefined(typeof(EnemySequenceAimPolicyV1), pattern.SequenceAimPolicy)
                && Enum.IsDefined(
                    typeof(EnemyAttackInterruptionPolicyV1),
                    pattern.InterruptionPolicy);
            if (!valid)
            {
                Add(
                    issues,
                    "enemy-catalog-shooting-pattern-invalid",
                    path + ".shooting_pattern",
                    "Shot count, interval, pellet count, spread, aim, wind-up, recovery, or interruption is invalid.");
            }
        }

        private static void ValidateProjectilePayload(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyAttackCapabilityDescriptorV1 attack,
            IEnemyCatalogRegistryV1 registry)
        {
            EnemyProjectilePayloadV1 payload = attack.ProjectilePayload;
            if (payload == null)
            {
                Add(
                    issues,
                    "enemy-catalog-projectile-payload-missing",
                    path + ".projectile_payload",
                    "Shooting attacks require a projectile_payload.");
                return;
            }

            if (payload.ProjectileProfileId == null
                || registry == null
                || !registry.IsProjectileProfileRegistered(payload.ProjectileProfileId))
            {
                Add(
                    issues,
                    "enemy-catalog-projectile-profile-unknown",
                    path + ".projectile_payload.profile",
                    "Projectile profile is not registered: "
                    + Value(payload.ProjectileProfileId));
            }

            bool physicalValuesValid =
                IsFiniteInRange(payload.Speed, 0d, MaximumDistance, false)
                && IsFiniteInRange(
                    payload.MaximumTravelDistance,
                    0d,
                    MaximumDistance,
                    false)
                && IsFiniteInRange(payload.CollisionRadius, 0d, 1000d, false)
                && payload.PierceCount >= 0
                && payload.PierceCount <= 1024;
            if (!physicalValuesValid)
            {
                Add(
                    issues,
                    "enemy-catalog-projectile-payload-invalid",
                    path + ".projectile_payload",
                    "Projectile speed, travel, radius, or pierce is invalid.");
            }
            if (IsFiniteInRange(
                    payload.MaximumTravelDistance,
                    0d,
                    MaximumDistance,
                    false)
                && payload.MaximumTravelDistance < attack.MaximumAttackRange)
            {
                Add(
                    issues,
                    "enemy-catalog-projectile-range-invalid",
                    path + ".projectile_payload.maximum_travel_distance",
                    "Projectile travel distance must support this attack's maximum range.");
            }
            ValidateAreaPayload(issues, path, payload.AreaPayload);
        }

        private static void ValidateAreaPayload(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyAreaPayloadV1 area)
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
                    "enemy-catalog-area-payload-invalid",
                    path + ".projectile_payload.area_payload",
                    "Area radius, duration, or target limit is invalid.");
            }
        }

        private static void ValidateMeleePattern(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyAttackCapabilityDescriptorV1 attack)
        {
            EnemyMeleePatternV1 pattern = attack.MeleePattern;
            bool valid = pattern != null
                && IsFiniteInRange(
                    pattern.WindUpSeconds,
                    0d,
                    MaximumCooldownSeconds,
                    true)
                && IsFiniteInRange(
                    pattern.ActiveWindowSeconds,
                    0d,
                    MaximumCooldownSeconds,
                    false)
                && pattern.StrikeCount >= 1
                && pattern.StrikeCount <= 1024
                && IsFiniteInRange(
                    pattern.IntervalBetweenStrikesSeconds,
                    0d,
                    MaximumCooldownSeconds,
                    true)
                && IsFiniteInRange(pattern.ContactRadius, 0d, MaximumDistance, false)
                && IsFiniteInRange(pattern.LungeDistance, 0d, MaximumDistance, true)
                && IsFiniteInRange(
                    pattern.RecoverySeconds,
                    0d,
                    MaximumCooldownSeconds,
                    true)
                && pattern.HitsPerTarget >= 1
                && pattern.HitsPerTarget <= 1024
                && Enum.IsDefined(
                    typeof(EnemyMeleeAimCommitPolicyV1),
                    pattern.AimCommitPolicy)
                && Enum.IsDefined(
                    typeof(EnemyMeleeTerminalOnImpactPolicyV1),
                    pattern.TerminalOnImpactPolicy)
                && Enum.IsDefined(
                    typeof(EnemyAttackInterruptionPolicyV1),
                    pattern.InterruptionPolicy);
            if (!valid)
            {
                Add(
                    issues,
                    "enemy-catalog-melee-pattern-invalid",
                    path + ".melee_pattern",
                    "Melee wind-up, active window, strikes, interval, reach, aim, recovery, hit limit, terminal, or interruption is invalid.");
                return;
            }

            double supportedRange = pattern.ContactRadius + pattern.LungeDistance;
            if (attack.MaximumAttackRange > supportedRange)
            {
                Add(
                    issues,
                    "enemy-catalog-melee-range-incompatible",
                    path + ".maximum_range",
                    "Melee contact radius and lunge distance must support this attack's maximum range.");
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

        private static void ValidateLegacyProjectile(
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

        private static void ValidateLegacyArea(
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

        private static void ValidateLegacyMelee(
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

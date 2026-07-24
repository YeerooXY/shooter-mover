using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Canonical fingerprint over the complete immutable snapshot relevant to firing.
    /// It binds accepted schedules to one exact effective weapon, including installed augment
    /// identities, even when a changed snapshot happens to retain the same numeric output.
    /// </summary>
    public static class EffectiveWeaponFiringFingerprint
    {
        public static string Compute(EffectiveWeapon weapon)
        {
            return WeaponExecutionFingerprint.Compute(ToCanonicalString(weapon));
        }

        public static string ToCanonicalString(EffectiveWeapon weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            StringBuilder builder = new StringBuilder();
            Append(builder, "definition_id", weapon.DefinitionId);
            Append(builder, "equipment_instance_id", weapon.EquipmentInstanceId);
            Append(builder, "equipment_definition_id", weapon.EquipmentDefinitionId);
            Append(builder, "item_level", weapon.ItemLevel);
            Append(builder, "quality_id", weapon.QualityId);

            builder.Append("installed_augment_count=")
                .Append(weapon.InstalledAugments.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            for (int index = 0; index < weapon.InstalledAugments.Count; index++)
            {
                AugmentInstance augment = weapon.InstalledAugments[index];
                builder.Append("installed_augment[")
                    .Append(index.ToString(CultureInfo.InvariantCulture))
                    .Append("]=")
                    .Append(augment == null
                        ? "null"
                        : augment.ToCanonicalString().Replace("\n", "\\n"))
                    .Append('\n');
            }

            Append(builder, "fire_mode", weapon.FireSettings.Mode);
            Append(builder, "shots_per_second", weapon.FireSettings.ShotsPerSecond);
            Append(builder, "shots_per_trigger", weapon.FireSettings.ShotsPerTrigger);
            Append(builder, "shots_per_burst", weapon.FireSettings.ShotsPerBurst);
            Append(
                builder,
                "interval_between_burst_shots",
                weapon.FireSettings.IntervalBetweenBurstShotsSeconds);
            Append(builder, "interval_after_burst", weapon.FireSettings.IntervalAfterBurstSeconds);
            Append(builder, "damage_ticks_per_second", weapon.FireSettings.DamageTicksPerSecond);

            Append(builder, "pattern_kind", weapon.ShotPattern.Kind);
            Append(builder, "projectiles_per_shot", weapon.ShotPattern.ProjectilesPerShot);
            Append(builder, "spread_degrees", weapon.ShotPattern.SpreadDegrees);
            Append(builder, "randomness_degrees", weapon.ShotPattern.RandomnessDegrees);
            Append(builder, "pulses_per_shot", weapon.ShotPattern.PulsesPerShot);
            Append(
                builder,
                "interval_between_pulses",
                weapon.ShotPattern.IntervalBetweenPulsesSeconds);

            if (weapon.Projectile == null)
            {
                Append(builder, "projectile", "none");
            }
            else
            {
                Append(builder, "projectile_kind", weapon.Projectile.Kind);
                Append(builder, "projectile_speed", weapon.Projectile.Speed);
                Append(builder, "projectile_range", weapon.Projectile.Range);
                Append(builder, "projectile_pierce_tenths", weapon.Projectile.Pierce.Tenths);
                Append(builder, "projectile_termination", weapon.Projectile.TerminationBehavior);
            }

            Append(builder, "guidance_mode", weapon.Guidance.Mode);
            Append(builder, "guidance_acquisition_range", weapon.Guidance.AcquisitionRange);
            Append(builder, "guidance_turn_rate", weapon.Guidance.TurnRateDegreesPerSecond);
            Append(builder, "guidance_activation_delay", weapon.Guidance.ActivationDelaySeconds);
            Append(builder, "guidance_target_policy", weapon.Guidance.TargetPolicy);
            Append(builder, "guidance_reacquisition", weapon.Guidance.Reacquisition);

            Append(builder, "impact_enemy", weapon.Impact.HandlesEnemyImpact);
            Append(builder, "impact_wall", weapon.Impact.HandlesWallImpact);
            Append(builder, "impact_range_expiry", weapon.Impact.HandlesRangeExpiry);
            Append(builder, "impact_termination", weapon.Impact.HandlesTermination);
            if (weapon.Impact.Ricochet == null)
            {
                Append(builder, "ricochet", "none");
            }
            else
            {
                Append(
                    builder,
                    "ricochet_maximum_successful_bounces",
                    weapon.Impact.Ricochet.MaximumSuccessfulBounces);
                Append(
                    builder,
                    "ricochet_retained_speed",
                    weapon.Impact.Ricochet.RetainedSpeedPerRicochet);
                Append(
                    builder,
                    "ricochet_random_angle",
                    weapon.Impact.Ricochet.RandomAngleDegrees);
                Append(builder, "ricochet_bounce_chance", weapon.Impact.Ricochet.BounceChance);
                Append(
                    builder,
                    "ricochet_homing_pause",
                    weapon.Impact.Ricochet.PostBounceHomingPauseSeconds);
            }

            if (weapon.Impact.ExplosionTrigger == null)
            {
                Append(builder, "explosion_trigger", "none");
            }
            else
            {
                Append(
                    builder,
                    "explosion_on_enemy",
                    weapon.Impact.ExplosionTrigger.OnEnemyImpact);
                Append(
                    builder,
                    "explosion_on_wall",
                    weapon.Impact.ExplosionTrigger.OnWallImpact);
                Append(
                    builder,
                    "explosion_on_range",
                    weapon.Impact.ExplosionTrigger.OnRangeExpiry);
                Append(
                    builder,
                    "explosion_on_termination",
                    weapon.Impact.ExplosionTrigger.OnTermination);
            }

            Append(builder, "damage_category", weapon.Damage.Category);
            Append(builder, "direct_damage", weapon.Damage.DirectDamage);
            Append(builder, "area_damage", weapon.Damage.AreaDamage);
            Append(builder, "dot_dps", weapon.Damage.DamageOverTimePerSecond);
            Append(builder, "dot_duration", weapon.Damage.DamageOverTimeDurationSeconds);
            Append(builder, "knockback", weapon.Damage.Knockback);

            if (weapon.Effects.Explosion == null)
            {
                Append(builder, "explosion_effect", "none");
            }
            else
            {
                Append(builder, "explosion_radius", weapon.Effects.Explosion.Radius);
                Append(
                    builder,
                    "explosion_minimum_damage_multiplier",
                    weapon.Effects.Explosion.MinimumDamageMultiplier);
            }

            if (weapon.Effects.DamageOverTime == null)
            {
                Append(builder, "dot_effect", "none");
            }
            else
            {
                Append(
                    builder,
                    "dot_ticks_per_second",
                    weapon.Effects.DamageOverTime.TicksPerSecond);
                Append(
                    builder,
                    "dot_maximum_stacks",
                    weapon.Effects.DamageOverTime.MaximumStacks);
                Append(
                    builder,
                    "dot_refreshes_duration",
                    weapon.Effects.DamageOverTime.RefreshesDuration);
            }

            if (weapon.Effects.ChainArc == null)
            {
                Append(builder, "chain_effect", "none");
            }
            else
            {
                Append(
                    builder,
                    "chain_maximum_targets",
                    weapon.Effects.ChainArc.MaximumTargets);
                Append(
                    builder,
                    "chain_acquisition_range",
                    weapon.Effects.ChainArc.AcquisitionRange);
                Append(
                    builder,
                    "chain_retained_damage",
                    weapon.Effects.ChainArc.RetainedDamagePerJump);
            }

            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string name, object value)
        {
            builder.Append(name)
                .Append('=')
                .Append(Format(value))
                .Append('\n');
        }

        private static string Format(object value)
        {
            if (value == null)
            {
                return "null";
            }
            if (value is double)
            {
                return ((double)value).ToString("R", CultureInfo.InvariantCulture);
            }
            if (value is int)
            {
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            }
            if (value is long)
            {
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            }
            if (value is bool)
            {
                return (bool)value ? "1" : "0";
            }

            return value.ToString();
        }
    }
}

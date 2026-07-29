using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    /// <summary>
    /// Canonical fingerprint over the complete immutable snapshot relevant to firing.
    /// It binds accepted schedules to one exact effective gun, including installed augment
    /// identities, even when a changed snapshot happens to retain the same numeric output.
    /// </summary>
    public static class EffectiveGunFiringFingerprint
    {
        public static string Compute(EffectiveGun gun)
        {
            return GunExecutionFingerprint.Compute(ToCanonicalString(gun));
        }

        public static string ToCanonicalString(EffectiveGun gun)
        {
            if (gun == null)
            {
                throw new ArgumentNullException(nameof(gun));
            }

            StringBuilder builder = new StringBuilder();
            Append(builder, "definition_id", gun.DefinitionId);
            Append(builder, "equipment_instance_id", gun.EquipmentInstanceId);
            Append(builder, "equipment_definition_id", gun.EquipmentDefinitionId);
            Append(builder, "item_level", gun.ItemLevel);
            Append(builder, "quality_id", gun.QualityId);

            builder.Append("installed_augment_count=")
                .Append(gun.InstalledAugments.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            for (int index = 0; index < gun.InstalledAugments.Count; index++)
            {
                AugmentInstance augment = gun.InstalledAugments[index];
                builder.Append("installed_augment[")
                    .Append(index.ToString(CultureInfo.InvariantCulture))
                    .Append("]=")
                    .Append(augment == null
                        ? "null"
                        : augment.ToCanonicalString().Replace("\n", "\\n"))
                    .Append('\n');
            }

            Append(builder, "fire_mode", gun.FireSettings.Mode);
            Append(builder, "shots_per_second", gun.FireSettings.ShotsPerSecond);
            Append(builder, "shots_per_trigger", gun.FireSettings.ShotsPerTrigger);
            Append(builder, "shots_per_burst", gun.FireSettings.ShotsPerBurst);
            Append(
                builder,
                "interval_between_burst_shots",
                gun.FireSettings.IntervalBetweenBurstShotsSeconds);
            Append(builder, "interval_after_burst", gun.FireSettings.IntervalAfterBurstSeconds);
            Append(builder, "damage_ticks_per_second", gun.FireSettings.DamageTicksPerSecond);

            Append(builder, "pattern_kind", gun.ShotPattern.Kind);
            Append(builder, "projectiles_per_shot", gun.ShotPattern.ProjectilesPerShot);
            Append(builder, "spread_degrees", gun.ShotPattern.SpreadDegrees);
            Append(builder, "randomness_degrees", gun.ShotPattern.RandomnessDegrees);
            Append(builder, "pulses_per_shot", gun.ShotPattern.PulsesPerShot);
            Append(
                builder,
                "interval_between_pulses",
                gun.ShotPattern.IntervalBetweenPulsesSeconds);

            if (gun.Projectile == null)
            {
                Append(builder, "projectile", "none");
            }
            else
            {
                Append(builder, "projectile_kind", gun.Projectile.Kind);
                Append(builder, "projectile_speed", gun.Projectile.Speed);
                Append(builder, "projectile_range", gun.Projectile.Range);
                Append(builder, "projectile_pierce_tenths", gun.Projectile.Pierce.Tenths);
                Append(builder, "projectile_termination", gun.Projectile.TerminationBehavior);
            }

            Append(builder, "guidance_mode", gun.Guidance.Mode);
            Append(builder, "guidance_acquisition_range", gun.Guidance.AcquisitionRange);
            Append(builder, "guidance_turn_rate", gun.Guidance.TurnRateDegreesPerSecond);
            Append(builder, "guidance_activation_delay", gun.Guidance.ActivationDelaySeconds);
            Append(builder, "guidance_target_policy", gun.Guidance.TargetPolicy);
            Append(builder, "guidance_reacquisition", gun.Guidance.Reacquisition);

            Append(builder, "impact_enemy", gun.Impact.HandlesEnemyImpact);
            Append(builder, "impact_wall", gun.Impact.HandlesWallImpact);
            Append(builder, "impact_range_expiry", gun.Impact.HandlesRangeExpiry);
            Append(builder, "impact_termination", gun.Impact.HandlesTermination);
            if (gun.Impact.Ricochet == null)
            {
                Append(builder, "ricochet", "none");
            }
            else
            {
                Append(
                    builder,
                    "ricochet_maximum_successful_bounces",
                    gun.Impact.Ricochet.MaximumSuccessfulBounces);
                Append(
                    builder,
                    "ricochet_retained_speed",
                    gun.Impact.Ricochet.RetainedSpeedPerRicochet);
                Append(
                    builder,
                    "ricochet_random_angle",
                    gun.Impact.Ricochet.RandomAngleDegrees);
                Append(builder, "ricochet_bounce_chance", gun.Impact.Ricochet.BounceChance);
                Append(
                    builder,
                    "ricochet_homing_pause",
                    gun.Impact.Ricochet.PostBounceHomingPauseSeconds);
            }

            if (gun.Impact.ExplosionTrigger == null)
            {
                Append(builder, "explosion_trigger", "none");
            }
            else
            {
                Append(
                    builder,
                    "explosion_on_enemy",
                    gun.Impact.ExplosionTrigger.OnEnemyImpact);
                Append(
                    builder,
                    "explosion_on_wall",
                    gun.Impact.ExplosionTrigger.OnWallImpact);
                Append(
                    builder,
                    "explosion_on_range",
                    gun.Impact.ExplosionTrigger.OnRangeExpiry);
                Append(
                    builder,
                    "explosion_on_termination",
                    gun.Impact.ExplosionTrigger.OnTermination);
            }

            Append(builder, "damage_category", gun.Damage.Category);
            Append(builder, "direct_damage", gun.Damage.DirectDamage);
            Append(builder, "area_damage", gun.Damage.AreaDamage);
            Append(builder, "dot_dps", gun.Damage.DamageOverTimePerSecond);
            Append(builder, "dot_duration", gun.Damage.DamageOverTimeDurationSeconds);
            Append(builder, "knockback", gun.Damage.Knockback);

            if (gun.Effects.Explosion == null)
            {
                Append(builder, "explosion_effect", "none");
            }
            else
            {
                Append(builder, "explosion_radius", gun.Effects.Explosion.Radius);
                Append(
                    builder,
                    "explosion_minimum_damage_multiplier",
                    gun.Effects.Explosion.MinimumDamageMultiplier);
            }

            if (gun.Effects.DamageOverTime == null)
            {
                Append(builder, "dot_effect", "none");
            }
            else
            {
                Append(
                    builder,
                    "dot_ticks_per_second",
                    gun.Effects.DamageOverTime.TicksPerSecond);
                Append(
                    builder,
                    "dot_maximum_stacks",
                    gun.Effects.DamageOverTime.MaximumStacks);
                Append(
                    builder,
                    "dot_refreshes_duration",
                    gun.Effects.DamageOverTime.RefreshesDuration);
            }

            if (gun.Effects.ChainArc == null)
            {
                Append(builder, "chain_effect", "none");
            }
            else
            {
                Append(
                    builder,
                    "chain_maximum_targets",
                    gun.Effects.ChainArc.MaximumTargets);
                Append(
                    builder,
                    "chain_acquisition_range",
                    gun.Effects.ChainArc.AcquisitionRange);
                Append(
                    builder,
                    "chain_retained_damage",
                    gun.Effects.ChainArc.RetainedDamagePerJump);
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

using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Immutable provisional scheduler handoff for one concrete emission. Construction is
    /// internal so external callers cannot mint an accepted schedule entry. WEAPON-FIRING-001
    /// must become the sole producer or replace this type with its canonical output contract.
    /// </summary>
    public sealed class WeaponFiringScheduleEntry
    {
        internal WeaponFiringScheduleEntry(
            EffectiveWeapon weapon,
            WeaponFireCommand command,
            RunParticipantId participantId,
            long shotSequence,
            int cooldownTicks)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            if (participantId == null)
            {
                throw new ArgumentNullException(nameof(participantId));
            }
            if (shotSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(shotSequence));
            }
            if (cooldownTicks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            }
            if (!weapon.EquipmentInstanceId.Equals(command.EquipmentInstanceId))
            {
                throw new ArgumentException(
                    "A firing schedule entry must use the effective weapon equipment instance.",
                    nameof(command));
            }

            Command = command;
            ParticipantId = participantId;
            WeaponDefinitionId = weapon.DefinitionId;
            EquipmentInstanceId = weapon.EquipmentInstanceId;
            EffectiveWeaponFingerprint = EffectiveWeaponRuntimeBindingFingerprint.Compute(weapon);
            ShotSequence = shotSequence;
            CooldownTicks = cooldownTicks;
            CanonicalText = BuildCanonicalText();
            ScheduleFingerprint = WeaponExecutionFingerprint.Compute(CanonicalText);
        }

        public WeaponFireCommand Command { get; }
        public RunParticipantId ParticipantId { get; }
        public WeaponDefinitionId WeaponDefinitionId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public string EffectiveWeaponFingerprint { get; }
        public long ShotSequence { get; }
        public int CooldownTicks { get; }
        public string CanonicalText { get; }
        public string ScheduleFingerprint { get; }

        private string BuildCanonicalText()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "command_fingerprint=" + Command.Fingerprint,
                    "participant_id=" + ParticipantId,
                    "weapon_definition_id=" + WeaponDefinitionId,
                    "equipment_instance_id=" + EquipmentInstanceId,
                    "effective_weapon_fingerprint=" + EffectiveWeaponFingerprint,
                    "shot_sequence=" + ShotSequence.ToString(CultureInfo.InvariantCulture),
                    "cooldown_ticks=" + CooldownTicks.ToString(CultureInfo.InvariantCulture),
                });
        }
    }

    /// <summary>
    /// Stable binding over every effective combat field consumed or rejected by the adapter.
    /// A schedule produced for an older augment/effective-profile snapshot cannot be replayed
    /// against another snapshot of the same equipment instance.
    /// </summary>
    internal static class EffectiveWeaponRuntimeBindingFingerprint
    {
        public static string Compute(EffectiveWeapon weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            List<string> lines = new List<string>();
            Add(lines, "definition_id", weapon.DefinitionId);
            Add(lines, "equipment_instance_id", weapon.EquipmentInstanceId);
            Add(lines, "equipment_definition_id", weapon.EquipmentDefinitionId);
            Add(lines, "item_level", weapon.ItemLevel);
            Add(lines, "quality_id", weapon.QualityId);

            Add(lines, "fire_mode", weapon.FireSettings.Mode);
            Add(lines, "shots_per_second", weapon.FireSettings.ShotsPerSecond);
            Add(lines, "shots_per_trigger", weapon.FireSettings.ShotsPerTrigger);
            Add(lines, "shots_per_burst", weapon.FireSettings.ShotsPerBurst);
            Add(lines, "burst_shot_interval", weapon.FireSettings.IntervalBetweenBurstShotsSeconds);
            Add(lines, "post_burst_interval", weapon.FireSettings.IntervalAfterBurstSeconds);
            Add(lines, "damage_ticks_per_second", weapon.FireSettings.DamageTicksPerSecond);

            Add(lines, "pattern_kind", weapon.ShotPattern.Kind);
            Add(lines, "projectiles_per_shot", weapon.ShotPattern.ProjectilesPerShot);
            Add(lines, "spread_degrees", weapon.ShotPattern.SpreadDegrees);
            Add(lines, "randomness_degrees", weapon.ShotPattern.RandomnessDegrees);
            Add(lines, "pulses_per_shot", weapon.ShotPattern.PulsesPerShot);
            Add(lines, "pulse_interval", weapon.ShotPattern.IntervalBetweenPulsesSeconds);

            if (weapon.Projectile == null)
            {
                Add(lines, "projectile", "none");
            }
            else
            {
                Add(lines, "projectile_kind", weapon.Projectile.Kind);
                Add(lines, "projectile_speed", weapon.Projectile.Speed);
                Add(lines, "projectile_range", weapon.Projectile.Range);
                Add(lines, "projectile_pierce_tenths", weapon.Projectile.Pierce.Tenths);
                Add(lines, "projectile_termination", weapon.Projectile.TerminationBehavior);
            }

            Add(lines, "guidance_mode", weapon.Guidance.Mode);
            Add(lines, "guidance_acquisition_range", weapon.Guidance.AcquisitionRange);
            Add(lines, "guidance_turn_rate", weapon.Guidance.TurnRateDegreesPerSecond);
            Add(lines, "guidance_activation_delay", weapon.Guidance.ActivationDelaySeconds);
            Add(lines, "guidance_target_policy", weapon.Guidance.TargetPolicy);
            Add(lines, "guidance_reacquisition", weapon.Guidance.Reacquisition);

            Add(lines, "impact_enemy", weapon.Impact.HandlesEnemyImpact);
            Add(lines, "impact_wall", weapon.Impact.HandlesWallImpact);
            Add(lines, "impact_range_expiry", weapon.Impact.HandlesRangeExpiry);
            Add(lines, "impact_termination", weapon.Impact.HandlesTermination);
            if (weapon.Impact.Ricochet == null)
            {
                Add(lines, "ricochet", "none");
            }
            else
            {
                Add(lines, "ricochet_maximum", weapon.Impact.Ricochet.MaximumSuccessfulBounces);
                Add(lines, "ricochet_speed_retention", weapon.Impact.Ricochet.RetainedSpeedPerRicochet);
                Add(lines, "ricochet_random_angle", weapon.Impact.Ricochet.RandomAngleDegrees);
                Add(lines, "ricochet_bounce_chance", weapon.Impact.Ricochet.BounceChance);
                Add(lines, "ricochet_homing_pause", weapon.Impact.Ricochet.PostBounceHomingPauseSeconds);
            }
            if (weapon.Impact.ExplosionTrigger == null)
            {
                Add(lines, "explosion_trigger", "none");
            }
            else
            {
                Add(lines, "explosion_on_enemy", weapon.Impact.ExplosionTrigger.OnEnemyImpact);
                Add(lines, "explosion_on_wall", weapon.Impact.ExplosionTrigger.OnWallImpact);
                Add(lines, "explosion_on_range", weapon.Impact.ExplosionTrigger.OnRangeExpiry);
                Add(lines, "explosion_on_termination", weapon.Impact.ExplosionTrigger.OnTermination);
            }

            Add(lines, "damage_category", weapon.Damage.Category);
            Add(lines, "direct_damage", weapon.Damage.DirectDamage);
            Add(lines, "area_damage", weapon.Damage.AreaDamage);
            Add(lines, "dot_dps", weapon.Damage.DamageOverTimePerSecond);
            Add(lines, "dot_duration", weapon.Damage.DamageOverTimeDurationSeconds);
            Add(lines, "knockback", weapon.Damage.Knockback);

            if (weapon.Effects.Explosion == null)
            {
                Add(lines, "explosion_effect", "none");
            }
            else
            {
                Add(lines, "explosion_radius", weapon.Effects.Explosion.Radius);
                Add(lines, "explosion_minimum_multiplier", weapon.Effects.Explosion.MinimumDamageMultiplier);
            }
            if (weapon.Effects.DamageOverTime == null)
            {
                Add(lines, "dot_effect", "none");
            }
            else
            {
                Add(lines, "dot_ticks_per_second", weapon.Effects.DamageOverTime.TicksPerSecond);
                Add(lines, "dot_maximum_stacks", weapon.Effects.DamageOverTime.MaximumStacks);
                Add(lines, "dot_refreshes_duration", weapon.Effects.DamageOverTime.RefreshesDuration);
            }
            if (weapon.Effects.ChainArc == null)
            {
                Add(lines, "chain_effect", "none");
            }
            else
            {
                Add(lines, "chain_maximum_targets", weapon.Effects.ChainArc.MaximumTargets);
                Add(lines, "chain_acquisition_range", weapon.Effects.ChainArc.AcquisitionRange);
                Add(lines, "chain_retained_damage", weapon.Effects.ChainArc.RetainedDamagePerJump);
            }

            return WeaponExecutionFingerprint.Compute(string.Join("\n", lines.ToArray()));
        }

        private static void Add(List<string> lines, string key, object value)
        {
            lines.Add(key + "=" + Format(value));
        }

        private static string Format(object value)
        {
            if (value == null)
            {
                return "null";
            }

            double doubleValue;
            if (value is double)
            {
                doubleValue = (double)value;
                return doubleValue.ToString("R", CultureInfo.InvariantCulture);
            }
            if (value is bool)
            {
                return (bool)value ? "1" : "0";
            }
            if (value is int)
            {
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            }
            if (value is long)
            {
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            }
            return value.ToString();
        }
    }

    public enum EffectiveWeaponRuntimeAdapterStatus
    {
        Adapted = 1,
        InvalidInput = 2,
        IdentityMismatch = 3,
        UnsupportedFireMode = 4,
        UnsupportedShotPattern = 5,
        UnsupportedProjectile = 6,
        FractionalPierceUnsupported = 7,
        UnsupportedGuidance = 8,
        UnsupportedImpact = 9,
        UnsupportedEffects = 10,
        UnknownBehavior = 11,
        BehaviorRejected = 12,
        InvalidEffectBatch = 13,
    }

    public sealed class EffectiveWeaponRuntimeAdapterResult
    {
        private EffectiveWeaponRuntimeAdapterResult(
            EffectiveWeaponRuntimeAdapterStatus status,
            WeaponRuntimeFiringProfile profile,
            WeaponEffectBatch batch,
            string rejectionCode)
        {
            Status = status;
            Profile = profile;
            Batch = batch;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public EffectiveWeaponRuntimeAdapterStatus Status { get; }
        public WeaponRuntimeFiringProfile Profile { get; }
        public WeaponEffectBatch Batch { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Status == EffectiveWeaponRuntimeAdapterStatus.Adapted; } }

        public static EffectiveWeaponRuntimeAdapterResult Adapted(
            WeaponRuntimeFiringProfile profile,
            WeaponEffectBatch batch)
        {
            return new EffectiveWeaponRuntimeAdapterResult(
                EffectiveWeaponRuntimeAdapterStatus.Adapted,
                profile ?? throw new ArgumentNullException(nameof(profile)),
                batch ?? throw new ArgumentNullException(nameof(batch)),
                string.Empty);
        }

        public static EffectiveWeaponRuntimeAdapterResult Reject(
            EffectiveWeaponRuntimeAdapterStatus status,
            string rejectionCode)
        {
            if (status == EffectiveWeaponRuntimeAdapterStatus.Adapted)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new EffectiveWeaponRuntimeAdapterResult(
                status,
                null,
                null,
                rejectionCode);
        }
    }

    /// <summary>
    /// The single typed migration seam from an immutable EffectiveWeapon and one bound
    /// schedule handoff into the retained runtime profile and effect batch.
    /// </summary>
    public interface IEffectiveWeaponRuntimeAdapter
    {
        EffectiveWeaponRuntimeAdapterResult Adapt(
            EffectiveWeapon weapon,
            WeaponFiringScheduleEntry scheduleEntry);
    }
}

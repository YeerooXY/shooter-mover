using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Domain.Weapons
{
    internal sealed class EffectiveWeaponEvaluatedValues
    {
        public EffectiveWeaponEvaluatedValues(
            WeaponFireSettings fireSettings,
            WeaponShotPattern shotPattern,
            WeaponProjectileSpec projectile,
            WeaponGuidanceSpec guidance,
            WeaponImpactSpec impact,
            WeaponDamageSpec damage,
            WeaponEffects effects)
        {
            FireSettings = fireSettings;
            ShotPattern = shotPattern;
            Projectile = projectile;
            Guidance = guidance;
            Impact = impact;
            Damage = damage;
            Effects = effects;
        }

        public WeaponFireSettings FireSettings { get; }
        public WeaponShotPattern ShotPattern { get; }
        public WeaponProjectileSpec Projectile { get; }
        public WeaponGuidanceSpec Guidance { get; }
        public WeaponImpactSpec Impact { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponEffects Effects { get; }
    }

    /// <summary>
    /// Applies numeric modifier stages and reconstructs the validated immutable weapon contracts.
    /// </summary>
    internal static class EffectiveWeaponStatEvaluator
    {
        public static EffectiveWeaponEvaluatedValues Evaluate(
            WeaponBlueprint blueprint,
            IEnumerable<AugmentInstance> installedAugments,
            IDictionary<StableId, WeaponAugmentModifierSet> modifiersByAugmentId)
        {
            Dictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators =
                BuildAccumulators(blueprint, installedAugments, modifiersByAugmentId);

            WeaponFireSettings fireSettings = BuildFireSettings(blueprint, accumulators);
            WeaponShotPattern shotPattern = BuildShotPattern(blueprint, accumulators);
            WeaponProjectileSpec projectile = BuildProjectile(blueprint, accumulators);
            WeaponGuidanceSpec guidance = BuildGuidance(blueprint, accumulators);
            WeaponImpactSpec impact = BuildImpact(blueprint, accumulators);
            WeaponDamageSpec damage = BuildDamage(blueprint, accumulators);
            WeaponEffects effects = BuildEffects(blueprint, accumulators);

            ValidateEffectiveStructure(blueprint, projectile, guidance, impact, damage, effects);
            return new EffectiveWeaponEvaluatedValues(
                fireSettings,
                shotPattern,
                projectile,
                guidance,
                impact,
                damage,
                effects);
        }

        private static Dictionary<WeaponEffectiveStat, ModifierAccumulator> BuildAccumulators(
            WeaponBlueprint blueprint,
            IEnumerable<AugmentInstance> installedAugments,
            IDictionary<StableId, WeaponAugmentModifierSet> modifiersByAugmentId)
        {
            Dictionary<WeaponEffectiveStat, ModifierAccumulator> result =
                new Dictionary<WeaponEffectiveStat, ModifierAccumulator>();

            foreach (AugmentInstance installed in installedAugments)
            {
                WeaponAugmentModifierSet modifierSet = modifiersByAugmentId[installed.InstanceId];
                for (int index = 0; index < modifierSet.Modifiers.Count; index++)
                {
                    WeaponStatModifier modifier = modifierSet.Modifiers[index];
                    ValidateStructuralCompatibility(blueprint, modifierSet, modifier.Stat);

                    ModifierAccumulator accumulator;
                    if (!result.TryGetValue(modifier.Stat, out accumulator))
                    {
                        accumulator = new ModifierAccumulator(modifier.Stat);
                        result.Add(modifier.Stat, accumulator);
                    }
                    accumulator.Add(modifier, modifierSet.Instance.InstanceId);
                }
            }

            return result;
        }

        private static void ValidateStructuralCompatibility(
            WeaponBlueprint blueprint,
            WeaponAugmentModifierSet modifierSet,
            WeaponEffectiveStat stat)
        {
            string reason = null;
            switch (stat)
            {
                case WeaponEffectiveStat.RateOfFire:
                    if (blueprint.FireSettings.IsContinuous)
                    {
                        reason = "RateOfFire modifies projectile ShotsPerSecond only; continuous DamageTicksPerSecond is a separate authored cadence";
                    }
                    break;

                case WeaponEffectiveStat.AreaDamage:
                case WeaponEffectiveStat.ExplosionRadius:
                    if (blueprint.Effects.Explosion == null)
                    {
                        reason = "the authored weapon has no explosion structure";
                    }
                    break;

                case WeaponEffectiveStat.ProjectileSpeed:
                case WeaponEffectiveStat.ProjectileRange:
                case WeaponEffectiveStat.PierceTenths:
                    if (blueprint.Projectile == null)
                    {
                        reason = "the authored weapon has no projectile structure";
                    }
                    break;

                case WeaponEffectiveStat.SpreadDegrees:
                case WeaponEffectiveStat.RandomnessDegrees:
                    if (blueprint.ShotPattern.Kind == WeaponShotPatternKind.Single
                        || blueprint.ShotPattern.Kind == WeaponShotPatternKind.Beam)
                    {
                        reason = "the authored shot-pattern kind does not support angular spread changes";
                    }
                    break;

                case WeaponEffectiveStat.DamageOverTimePerSecond:
                case WeaponEffectiveStat.DamageOverTimeDurationSeconds:
                case WeaponEffectiveStat.DamageOverTimeTicksPerSecond:
                case WeaponEffectiveStat.DamageOverTimeMaximumStacks:
                    if (!blueprint.Damage.HasDamageOverTime
                        || blueprint.Effects.DamageOverTime == null)
                    {
                        reason = "the authored weapon has no damage-over-time structure";
                    }
                    break;

                case WeaponEffectiveStat.HomingAcquisitionRange:
                case WeaponEffectiveStat.HomingTurnRateDegreesPerSecond:
                case WeaponEffectiveStat.HomingActivationDelaySeconds:
                    if (blueprint.Guidance.Mode != WeaponGuidanceMode.Homing)
                    {
                        reason = "the authored weapon is not homing";
                    }
                    break;

                case WeaponEffectiveStat.RicochetMaximumRicochets:
                case WeaponEffectiveStat.RicochetRetainedSpeed:
                case WeaponEffectiveStat.RicochetRandomAngleDegrees:
                    if (blueprint.Impact.Ricochet == null)
                    {
                        reason = "the authored weapon has no ricochet structure";
                    }
                    break;

                case WeaponEffectiveStat.ChainMaximumTargets:
                case WeaponEffectiveStat.ChainAcquisitionRange:
                case WeaponEffectiveStat.ChainRetainedDamagePerJump:
                    if (blueprint.Effects.ChainArc == null)
                    {
                        reason = "the authored weapon has no chain-arc structure";
                    }
                    break;
            }

            if (reason != null)
            {
                throw new IncompatibleWeaponAugmentException(
                    modifierSet.Instance.InstanceId,
                    modifierSet.Definition.DefinitionId,
                    stat,
                    reason);
            }
        }

        private static WeaponFireSettings BuildFireSettings(
            WeaponBlueprint blueprint,
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators)
        {
            WeaponFireSettings authored = blueprint.FireSettings;
            if (authored.IsContinuous)
            {
                return WeaponFireSettings.Create(
                    authored.Mode,
                    0d,
                    0,
                    0,
                    0d,
                    0d,
                    authored.DamageTicksPerSecond);
            }

            double shotsPerSecond = RequirePositive(
                Apply(accumulators, WeaponEffectiveStat.RateOfFire, authored.ShotsPerSecond),
                WeaponEffectiveStat.RateOfFire);
            return WeaponFireSettings.Create(
                authored.Mode,
                shotsPerSecond,
                authored.ShotsPerTrigger,
                authored.ShotsPerBurst,
                authored.IntervalBetweenBurstShotsSeconds,
                authored.IntervalAfterBurstSeconds,
                0d);
        }

        private static WeaponShotPattern BuildShotPattern(
            WeaponBlueprint blueprint,
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators)
        {
            WeaponShotPattern authored = blueprint.ShotPattern;
            double spread = Clamp(
                Apply(accumulators, WeaponEffectiveStat.SpreadDegrees, authored.SpreadDegrees),
                0d,
                360d,
                WeaponEffectiveStat.SpreadDegrees);
            double randomness = Clamp(
                Apply(accumulators, WeaponEffectiveStat.RandomnessDegrees, authored.RandomnessDegrees),
                0d,
                360d,
                WeaponEffectiveStat.RandomnessDegrees);

            return WeaponShotPattern.Create(
                authored.Kind,
                authored.ProjectilesPerShot,
                spread,
                randomness,
                authored.PulsesPerShot,
                authored.IntervalBetweenPulsesSeconds);
        }

        private static WeaponProjectileSpec BuildProjectile(
            WeaponBlueprint blueprint,
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators)
        {
            WeaponProjectileSpec authored = blueprint.Projectile;
            if (authored == null)
            {
                return null;
            }

            double speed = RequirePositive(
                Apply(accumulators, WeaponEffectiveStat.ProjectileSpeed, authored.Speed),
                WeaponEffectiveStat.ProjectileSpeed);
            double range = RequirePositive(
                Apply(accumulators, WeaponEffectiveStat.ProjectileRange, authored.Range),
                WeaponEffectiveStat.ProjectileRange);
            int pierceTenths = ToNonNegativeInt(
                Apply(accumulators, WeaponEffectiveStat.PierceTenths, authored.Pierce.Tenths),
                WeaponEffectiveStat.PierceTenths);

            return WeaponProjectileSpec.Create(
                authored.Kind,
                speed,
                range,
                new PierceValue(pierceTenths),
                authored.TerminationBehavior);
        }

        private static WeaponGuidanceSpec BuildGuidance(
            WeaponBlueprint blueprint,
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators)
        {
            WeaponGuidanceSpec authored = blueprint.Guidance;
            if (authored.Mode == WeaponGuidanceMode.Unguided)
            {
                return WeaponGuidanceSpec.Unguided();
            }

            return WeaponGuidanceSpec.Homing(
                RequirePositive(
                    Apply(
                        accumulators,
                        WeaponEffectiveStat.HomingAcquisitionRange,
                        authored.AcquisitionRange),
                    WeaponEffectiveStat.HomingAcquisitionRange),
                RequirePositive(
                    Apply(
                        accumulators,
                        WeaponEffectiveStat.HomingTurnRateDegreesPerSecond,
                        authored.TurnRateDegreesPerSecond),
                    WeaponEffectiveStat.HomingTurnRateDegreesPerSecond),
                ClampNonNegative(
                    Apply(
                        accumulators,
                        WeaponEffectiveStat.HomingActivationDelaySeconds,
                        authored.ActivationDelaySeconds),
                    WeaponEffectiveStat.HomingActivationDelaySeconds),
                authored.TargetPolicy,
                authored.Reacquisition);
        }

        private static WeaponImpactSpec BuildImpact(
            WeaponBlueprint blueprint,
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators)
        {
            WeaponImpactSpec authored = blueprint.Impact;
            WeaponRicochetSpec ricochet = null;
            if (authored.Ricochet != null)
            {
                int maximumRicochets = ToPositiveInt(
                    Apply(
                        accumulators,
                        WeaponEffectiveStat.RicochetMaximumRicochets,
                        authored.Ricochet.MaximumRicochets),
                    WeaponEffectiveStat.RicochetMaximumRicochets);
                double retainedSpeed = Clamp(
                    Apply(
                        accumulators,
                        WeaponEffectiveStat.RicochetRetainedSpeed,
                        authored.Ricochet.RetainedSpeedPerRicochet),
                    0d,
                    1d,
                    WeaponEffectiveStat.RicochetRetainedSpeed);
                if (retainedSpeed <= 0d)
                {
                    throw new ArgumentOutOfRangeException(
                        WeaponEffectiveStat.RicochetRetainedSpeed.ToString(),
                        "Effective ricochet retained speed must remain positive after clamping.");
                }
                double randomAngle = Clamp(
                    Apply(
                        accumulators,
                        WeaponEffectiveStat.RicochetRandomAngleDegrees,
                        authored.Ricochet.RandomAngleDegrees),
                    0d,
                    360d,
                    WeaponEffectiveStat.RicochetRandomAngleDegrees);

                ricochet = new WeaponRicochetSpec(
                    maximumRicochets,
                    retainedSpeed,
                    randomAngle,
                    authored.Ricochet.BounceChance,
                    authored.Ricochet.PostBounceHomingPauseSeconds);
            }

            return WeaponImpactSpec.Create(
                authored.HandlesEnemyImpact,
                authored.HandlesWallImpact,
                authored.HandlesRangeExpiry,
                authored.HandlesTermination,
                ricochet,
                authored.ExplosionTrigger);
        }

        private static WeaponDamageSpec BuildDamage(
            WeaponBlueprint blueprint,
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators)
        {
            WeaponDamageSpec authored = blueprint.Damage;
            return WeaponDamageSpec.Create(
                authored.Category,
                ClampNonNegative(
                    Apply(accumulators, WeaponEffectiveStat.DirectDamage, authored.DirectDamage),
                    WeaponEffectiveStat.DirectDamage),
                ClampNonNegative(
                    Apply(accumulators, WeaponEffectiveStat.AreaDamage, authored.AreaDamage),
                    WeaponEffectiveStat.AreaDamage),
                ClampNonNegative(
                    Apply(
                        accumulators,
                        WeaponEffectiveStat.DamageOverTimePerSecond,
                        authored.DamageOverTimePerSecond),
                    WeaponEffectiveStat.DamageOverTimePerSecond),
                ClampNonNegative(
                    Apply(
                        accumulators,
                        WeaponEffectiveStat.DamageOverTimeDurationSeconds,
                        authored.DamageOverTimeDurationSeconds),
                    WeaponEffectiveStat.DamageOverTimeDurationSeconds),
                authored.Knockback);
        }

        private static WeaponEffects BuildEffects(
            WeaponBlueprint blueprint,
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators)
        {
            WeaponExplosionEffect explosion = null;
            if (blueprint.Effects.Explosion != null)
            {
                explosion = new WeaponExplosionEffect(
                    RequirePositive(
                        Apply(
                            accumulators,
                            WeaponEffectiveStat.ExplosionRadius,
                            blueprint.Effects.Explosion.Radius),
                        WeaponEffectiveStat.ExplosionRadius),
                    blueprint.Effects.Explosion.MinimumDamageMultiplier);
            }

            WeaponDamageOverTimeEffect damageOverTime = null;
            if (blueprint.Effects.DamageOverTime != null)
            {
                damageOverTime = new WeaponDamageOverTimeEffect(
                    RequirePositive(
                        Apply(
                            accumulators,
                            WeaponEffectiveStat.DamageOverTimeTicksPerSecond,
                            blueprint.Effects.DamageOverTime.TicksPerSecond),
                        WeaponEffectiveStat.DamageOverTimeTicksPerSecond),
                    ToPositiveInt(
                        Apply(
                            accumulators,
                            WeaponEffectiveStat.DamageOverTimeMaximumStacks,
                            blueprint.Effects.DamageOverTime.MaximumStacks),
                        WeaponEffectiveStat.DamageOverTimeMaximumStacks),
                    blueprint.Effects.DamageOverTime.RefreshesDuration);
            }

            WeaponChainArcEffect chainArc = null;
            if (blueprint.Effects.ChainArc != null)
            {
                chainArc = new WeaponChainArcEffect(
                    ToPositiveInt(
                        Apply(
                            accumulators,
                            WeaponEffectiveStat.ChainMaximumTargets,
                            blueprint.Effects.ChainArc.MaximumTargets),
                        WeaponEffectiveStat.ChainMaximumTargets),
                    RequirePositive(
                        Apply(
                            accumulators,
                            WeaponEffectiveStat.ChainAcquisitionRange,
                            blueprint.Effects.ChainArc.AcquisitionRange),
                        WeaponEffectiveStat.ChainAcquisitionRange),
                    Clamp(
                        Apply(
                            accumulators,
                            WeaponEffectiveStat.ChainRetainedDamagePerJump,
                            blueprint.Effects.ChainArc.RetainedDamagePerJump),
                        0d,
                        1d,
                        WeaponEffectiveStat.ChainRetainedDamagePerJump));
            }

            return new WeaponEffects(explosion, damageOverTime, chainArc);
        }

        private static void ValidateEffectiveStructure(
            WeaponBlueprint blueprint,
            WeaponProjectileSpec projectile,
            WeaponGuidanceSpec guidance,
            WeaponImpactSpec impact,
            WeaponDamageSpec damage,
            WeaponEffects effects)
        {
            if (blueprint.ShotPattern.UsesProjectiles && projectile == null)
            {
                throw new InvalidOperationException(
                    "Effective projectile-emitting weapons must retain projectile structure.");
            }
            if (guidance.Mode == WeaponGuidanceMode.Homing && projectile == null)
            {
                throw new InvalidOperationException(
                    "Effective homing weapons must retain projectile structure.");
            }
            if (impact.Ricochet != null && projectile == null)
            {
                throw new InvalidOperationException(
                    "Effective ricochet weapons must retain projectile structure.");
            }
            if ((impact.ExplosionTrigger != null || damage.HasAreaDamage)
                && effects.Explosion == null)
            {
                throw new InvalidOperationException(
                    "Effective explosion data requires authored explosion structure.");
            }
            if (damage.HasDamageOverTime && effects.DamageOverTime == null)
            {
                throw new InvalidOperationException(
                    "Effective damage-over-time data requires authored damage-over-time structure.");
            }
        }

        private static double Apply(
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators,
            WeaponEffectiveStat stat,
            double authoredValue)
        {
            ModifierAccumulator accumulator;
            return accumulators.TryGetValue(stat, out accumulator)
                ? accumulator.Apply(authoredValue)
                : authoredValue;
        }

        private static double ClampNonNegative(double value, WeaponEffectiveStat stat)
        {
            return Clamp(value, 0d, double.MaxValue, stat);
        }

        private static double RequirePositive(double value, WeaponEffectiveStat stat)
        {
            RequireFinite(value, stat);
            if (value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    stat.ToString(),
                    "Effective value must remain positive after modifiers.");
            }
            return value;
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum,
            WeaponEffectiveStat stat)
        {
            RequireFinite(value, stat);
            if (value < minimum)
            {
                return minimum;
            }
            return value > maximum ? maximum : value;
        }

        private static int ToNonNegativeInt(double value, WeaponEffectiveStat stat)
        {
            double clamped = Clamp(value, 0d, int.MaxValue, stat);
            return checked((int)Math.Round(clamped, MidpointRounding.AwayFromZero));
        }

        private static int ToPositiveInt(double value, WeaponEffectiveStat stat)
        {
            int result = ToNonNegativeInt(value, stat);
            if (result < 1)
            {
                throw new ArgumentOutOfRangeException(
                    stat.ToString(),
                    "Effective integer value must remain at least one after modifiers.");
            }
            return result;
        }

        private static void RequireFinite(double value, WeaponEffectiveStat stat)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    stat.ToString(),
                    "Effective value must be finite after modifiers.");
            }
        }

        private sealed class ModifierAccumulator
        {
            private readonly WeaponEffectiveStat stat;
            private double flatAddition;
            private double additivePercentage;
            private double multiplier = 1d;
            private bool hasOverride;
            private double overrideValue;
            private StableId overrideSource;

            public ModifierAccumulator(WeaponEffectiveStat stat)
            {
                this.stat = stat;
            }

            public void Add(WeaponStatModifier modifier, StableId augmentInstanceId)
            {
                switch (modifier.Operation)
                {
                    case WeaponModifierOperation.FlatAddition:
                        flatAddition += modifier.Value;
                        break;
                    case WeaponModifierOperation.AdditivePercentage:
                        additivePercentage += modifier.Value;
                        break;
                    case WeaponModifierOperation.Multiplier:
                        multiplier *= modifier.Value;
                        break;
                    case WeaponModifierOperation.Override:
                        if (hasOverride)
                        {
                            throw new InvalidOperationException(
                                "Multiple explicit overrides target "
                                + stat
                                + " from installed augments "
                                + overrideSource
                                + " and "
                                + augmentInstanceId
                                + ". Resolve the conflict explicitly.");
                        }
                        hasOverride = true;
                        overrideValue = modifier.Value;
                        overrideSource = augmentInstanceId;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(modifier));
                }
            }

            public double Apply(double authoredValue)
            {
                RequireFinite(authoredValue, stat);

                // Required order: authored, flat, additive percentage, multiplier, override.
                double result = authoredValue;
                result += flatAddition;
                result *= 1d + additivePercentage;
                result *= multiplier;
                if (hasOverride)
                {
                    result = overrideValue;
                }

                RequireFinite(result, stat);
                return result;
            }
        }
    }
}

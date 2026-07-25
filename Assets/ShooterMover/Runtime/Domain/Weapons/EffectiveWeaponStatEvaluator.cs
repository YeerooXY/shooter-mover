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
            WeaponEffects effects,
            WeaponAttackDistance maximumAttackDistance,
            PierceValue pierce,
            RicochetValue ricochet,
            double movementPenaltyPercent)
        {
            FireSettings = fireSettings;
            ShotPattern = shotPattern;
            Projectile = projectile;
            Guidance = guidance;
            Impact = impact;
            Damage = damage;
            Effects = effects;
            MaximumAttackDistance = maximumAttackDistance;
            Pierce = pierce;
            Ricochet = ricochet;
            MovementPenaltyPercent = movementPenaltyPercent;
        }

        public WeaponFireSettings FireSettings { get; }
        public WeaponShotPattern ShotPattern { get; }
        public WeaponProjectileSpec Projectile { get; }
        public WeaponGuidanceSpec Guidance { get; }
        public WeaponImpactSpec Impact { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponEffects Effects { get; }
        public WeaponAttackDistance MaximumAttackDistance { get; }
        public PierceValue Pierce { get; }
        public RicochetValue Ricochet { get; }
        public double MovementPenaltyPercent { get; }
    }

    /// <summary>
    /// Applies numeric modifier stages and reconstructs the validated immutable weapon contracts.
    /// Semantic values remain available even when a delivery has no travelling projectile.
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

            WeaponAttackDistance maximumAttackDistance =
                BuildMaximumAttackDistance(blueprint, accumulators);
            PierceValue pierce = BuildPierce(blueprint, accumulators);
            RicochetValue ricochet = BuildRicochet(blueprint);
            double movementPenaltyPercent = BuildMovementPenalty(blueprint);

            WeaponFireSettings fireSettings = BuildFireSettings(blueprint, accumulators);
            WeaponShotPattern shotPattern = BuildShotPattern(blueprint, accumulators);
            WeaponProjectileSpec projectile = BuildProjectile(
                blueprint,
                accumulators,
                maximumAttackDistance,
                pierce);
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
                effects,
                maximumAttackDistance,
                pierce,
                ricochet,
                movementPenaltyPercent);
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
                        reason = "RateOfFire modifies firing-cycle cadence only; continuous DamageTicksPerSecond is a separate transitional cadence";
                    }
                    break;

                case WeaponEffectiveStat.AreaDamage:
                    if (!blueprint.IsTransitionalCatalogProjection)
                    {
                        reason = "canonical weapons have one universal damage value; area delivery is projected explicitly rather than independently modified";
                    }
                    else if (blueprint.Effects.Explosion == null)
                    {
                        reason = "the authored weapon has no explosion structure";
                    }
                    break;

                case WeaponEffectiveStat.ExplosionRadius:
                    if (blueprint.Effects.Explosion == null)
                    {
                        reason = "the authored weapon has no explosion structure";
                    }
                    break;

                case WeaponEffectiveStat.ProjectileSpeed:
                    if (!SupportsProjectileSpeed(blueprint))
                    {
                        reason = "projectile speed is valid only for travelling Normal, Orb, or Rocket delivery";
                    }
                    break;

                case WeaponEffectiveStat.ProjectileRange:
                    if (!SupportsMaximumRange(blueprint))
                    {
                        reason = "the delivery does not expose a finite canonical maximum attack distance";
                    }
                    break;

                case WeaponEffectiveStat.PierceTenths:
                    if (!SupportsPierce(blueprint))
                    {
                        reason = "the delivery does not declare reusable canonical Pierce semantics";
                    }
                    break;

                case WeaponEffectiveStat.SpreadDegrees:
                    if (blueprint.IsTransitionalCatalogProjection)
                    {
                        if (blueprint.ShotPattern.Kind == WeaponShotPatternKind.Single
                            || blueprint.ShotPattern.Kind == WeaponShotPatternKind.Beam)
                        {
                            reason = "the transitional shot-pattern kind does not support angular spread changes";
                        }
                    }
                    else if (blueprint.ShotPattern.Kind != WeaponShotPatternKind.Spread)
                    {
                        reason = "canonical deterministic spread modifiers require an existing multi-emission Spread structure";
                    }
                    break;

                case WeaponEffectiveStat.RandomnessDegrees:
                    if (blueprint.IsTransitionalCatalogProjection)
                    {
                        if (blueprint.ShotPattern.Kind == WeaponShotPatternKind.Single
                            || blueprint.ShotPattern.Kind == WeaponShotPatternKind.Beam)
                        {
                            reason = "the transitional shot-pattern kind does not support angular randomness changes";
                        }
                    }
                    else if (blueprint.ShotPattern.Kind != WeaponShotPatternKind.Spray)
                    {
                        reason = "canonical random-deviation modifiers require an existing single-emission Spray structure";
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
                    if (blueprint.Impact.Ricochet == null)
                    {
                        reason = "the authored weapon has no ricochet structure";
                    }
                    else if (blueprint.Impact.Ricochet.HasCanonicalFixedPointBudget)
                    {
                        reason = "legacy maximum-ricochet modifiers cannot rewrite the canonical fixed-point guaranteed-plus-one-fraction budget";
                    }
                    break;

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

        private static bool SupportsProjectileSpeed(WeaponBlueprint blueprint)
        {
            return blueprint.IsTransitionalCatalogProjection
                ? blueprint.Projectile != null
                : blueprint.Delivery != null
                    && blueprint.Delivery.SupportsProjectileSpeedModifiers;
        }

        private static bool SupportsMaximumRange(WeaponBlueprint blueprint)
        {
            if (blueprint.IsTransitionalCatalogProjection)
            {
                return blueprint.Projectile != null;
            }
            return blueprint.Delivery != null
                && blueprint.Delivery.SupportsCanonicalRangeModifiers
                && blueprint.BaseStats != null
                && blueprint.BaseStats.MaximumAttackDistance.IsLimited;
        }

        private static bool SupportsPierce(WeaponBlueprint blueprint)
        {
            return blueprint.IsTransitionalCatalogProjection
                ? blueprint.Projectile != null
                : blueprint.Delivery != null
                    && blueprint.Delivery.SupportsCanonicalPierceModifiers;
        }

        private static WeaponAttackDistance BuildMaximumAttackDistance(
            WeaponBlueprint blueprint,
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators)
        {
            WeaponAttackDistance authored = blueprint.BaseStats == null
                ? (blueprint.Projectile == null
                    ? null
                    : WeaponAttackDistance.Limited(blueprint.Projectile.Range))
                : blueprint.BaseStats.MaximumAttackDistance;
            if (authored == null || !authored.IsLimited)
            {
                return authored;
            }

            return WeaponAttackDistance.Limited(
                RequirePositive(
                    Apply(
                        accumulators,
                        WeaponEffectiveStat.ProjectileRange,
                        authored.Distance),
                    WeaponEffectiveStat.ProjectileRange));
        }

        private static PierceValue BuildPierce(
            WeaponBlueprint blueprint,
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators)
        {
            PierceValue authored = blueprint.BaseStats == null
                ? (blueprint.Projectile == null
                    ? new PierceValue(0)
                    : blueprint.Projectile.Pierce)
                : blueprint.BaseStats.Pierce;
            return new PierceValue(
                ToNonNegativeInt(
                    Apply(
                        accumulators,
                        WeaponEffectiveStat.PierceTenths,
                        authored.Tenths),
                    WeaponEffectiveStat.PierceTenths));
        }

        private static RicochetValue BuildRicochet(WeaponBlueprint blueprint)
        {
            if (blueprint.BaseStats != null)
            {
                return blueprint.BaseStats.Ricochet;
            }
            if (blueprint.Impact.Ricochet == null)
            {
                return new RicochetValue(0);
            }
            if (blueprint.Impact.Ricochet.FixedPointBudget.HasValue)
            {
                return blueprint.Impact.Ricochet.FixedPointBudget.Value;
            }
            return new RicochetValue(
                checked(blueprint.Impact.Ricochet.MaximumSuccessfulBounces * 10));
        }

        private static double BuildMovementPenalty(WeaponBlueprint blueprint)
        {
            return blueprint.BaseStats == null
                ? 0d
                : blueprint.BaseStats.MovementPenaltyPercent;
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

            double rateOfFire = RequirePositive(
                Apply(accumulators, WeaponEffectiveStat.RateOfFire, authored.RateOfFire),
                WeaponEffectiveStat.RateOfFire);
            if (blueprint.IsTransitionalCatalogProjection)
            {
                return WeaponFireSettings.Create(
                    authored.Mode,
                    rateOfFire,
                    authored.ShotsPerTrigger,
                    authored.ShotsPerBurst,
                    authored.IntervalBetweenBurstShotsSeconds,
                    authored.IntervalAfterBurstSeconds,
                    0d);
            }

            switch (authored.Mode)
            {
                case WeaponFireMode.SemiAutomatic:
                    return WeaponFireSettings.SemiAutomatic(rateOfFire);
                case WeaponFireMode.Automatic:
                    return WeaponFireSettings.Automatic(rateOfFire);
                case WeaponFireMode.Burst:
                    return WeaponFireSettings.Burst(
                        rateOfFire,
                        new WeaponBurstSettings(
                            authored.ShotsPerBurst,
                            authored.IntervalBetweenBurstShotsSeconds));
                default:
                    throw new InvalidOperationException(
                        "Canonical effective weapons support semi-automatic, automatic, or burst fire.");
            }
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
            IDictionary<WeaponEffectiveStat, ModifierAccumulator> accumulators,
            WeaponAttackDistance maximumAttackDistance,
            PierceValue pierce)
        {
            WeaponProjectileSpec authored = blueprint.Projectile;
            if (authored == null)
            {
                return null;
            }
            if (maximumAttackDistance == null || !maximumAttackDistance.IsLimited)
            {
                throw new InvalidOperationException(
                    "Travelling projectile execution requires a finite effective maximum range.");
            }

            double speed = RequirePositive(
                Apply(accumulators, WeaponEffectiveStat.ProjectileSpeed, authored.Speed),
                WeaponEffectiveStat.ProjectileSpeed);

            return WeaponProjectileSpec.Create(
                authored.Kind,
                speed,
                maximumAttackDistance.Distance,
                pierce,
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

                if (authored.Ricochet.FixedPointBudget.HasValue)
                {
                    ricochet = new WeaponRicochetSpec(
                        authored.Ricochet.FixedPointBudget.Value,
                        retainedSpeed,
                        randomAngle,
                        authored.Ricochet.PostBounceHomingPauseSeconds);
                }
                else
                {
                    int maximumRicochets = ToPositiveInt(
                        Apply(
                            accumulators,
                            WeaponEffectiveStat.RicochetMaximumRicochets,
                            authored.Ricochet.MaximumRicochets),
                        WeaponEffectiveStat.RicochetMaximumRicochets);
                    ricochet = new WeaponRicochetSpec(
                        maximumRicochets,
                        retainedSpeed,
                        randomAngle,
                        authored.Ricochet.BounceChance,
                        authored.Ricochet.PostBounceHomingPauseSeconds);
                }
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
            double directDamage = ClampNonNegative(
                Apply(accumulators, WeaponEffectiveStat.DirectDamage, authored.DirectDamage),
                WeaponEffectiveStat.DirectDamage);
            double dotDamage = ClampNonNegative(
                Apply(
                    accumulators,
                    WeaponEffectiveStat.DamageOverTimePerSecond,
                    authored.DamageOverTimePerSecond),
                WeaponEffectiveStat.DamageOverTimePerSecond);
            double dotDuration = ClampNonNegative(
                Apply(
                    accumulators,
                    WeaponEffectiveStat.DamageOverTimeDurationSeconds,
                    authored.DamageOverTimeDurationSeconds),
                WeaponEffectiveStat.DamageOverTimeDurationSeconds);

            if (!blueprint.IsTransitionalCatalogProjection)
            {
                WeaponDamageOverTimeStats damageOverTime = dotDamage > 0d || dotDuration > 0d
                    ? new WeaponDamageOverTimeStats(dotDamage, dotDuration)
                    : null;
                return WeaponDamageSpec.Create(
                    authored.Category,
                    directDamage,
                    damageOverTime,
                    authored.Knockback);
            }

            return WeaponDamageSpec.Create(
                authored.Category,
                directDamage,
                ClampNonNegative(
                    Apply(accumulators, WeaponEffectiveStat.AreaDamage, authored.AreaDamage),
                    WeaponEffectiveStat.AreaDamage),
                dotDamage,
                dotDuration,
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
            bool requiresTravellingProjectile = blueprint.Delivery == null
                ? blueprint.ShotPattern.UsesProjectiles
                : blueprint.Delivery.IsTravelling;
            if (requiresTravellingProjectile && projectile == null)
            {
                throw new InvalidOperationException(
                    "Effective travelling deliveries must retain projectile structure.");
            }
            if (guidance.Mode == WeaponGuidanceMode.Homing && projectile == null)
            {
                throw new InvalidOperationException(
                    "Effective homing weapons must retain projectile structure.");
            }

            bool supportsNonProjectileRicochet = blueprint.Delivery != null
                && blueprint.Delivery.Type == WeaponDeliveryType.Laser;
            if (impact.Ricochet != null
                && projectile == null
                && !supportsNonProjectileRicochet)
            {
                throw new InvalidOperationException(
                    "Effective ricochet requires a travelling projectile or canonical Laser delivery.");
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

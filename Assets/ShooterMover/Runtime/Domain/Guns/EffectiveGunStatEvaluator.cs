using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Domain.Guns
{
    internal sealed class EffectiveGunEvaluatedValues
    {
        public EffectiveGunEvaluatedValues(
            FireSettings fireSettings,
            GunShotPattern shotPattern,
            ProjectileSettings projectile,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunDamageSpec damage,
            GunEffects effects,
            GunAttackDistance maximumAttackDistance,
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

        public FireSettings FireSettings { get; }
        public GunShotPattern ShotPattern { get; }
        public ProjectileSettings Projectile { get; }
        public GunGuidanceSpec Guidance { get; }
        public GunImpactSpec Impact { get; }
        public GunDamageSpec Damage { get; }
        public GunEffects Effects { get; }
        public GunAttackDistance MaximumAttackDistance { get; }
        public PierceValue Pierce { get; }
        public RicochetValue Ricochet { get; }
        public double MovementPenaltyPercent { get; }
    }

    /// <summary>
    /// Applies numeric modifier stages and reconstructs the validated immutable gun contracts.
    /// Semantic values remain available even when a delivery has no travelling projectile.
    /// </summary>
    internal static class EffectiveGunStatEvaluator
    {
        public static EffectiveGunEvaluatedValues Evaluate(
            Gun blueprint,
            IEnumerable<AugmentInstance> installedAugments,
            IDictionary<StableId, GunAugmentModifierSet> modifiersByAugmentId)
        {
            Dictionary<GunEffectiveStat, ModifierAccumulator> accumulators =
                BuildAccumulators(blueprint, installedAugments, modifiersByAugmentId);

            GunAttackDistance maximumAttackDistance =
                BuildMaximumAttackDistance(blueprint, accumulators);
            PierceValue pierce = BuildPierce(blueprint, accumulators);
            RicochetValue ricochet = BuildRicochet(blueprint, accumulators);
            double movementPenaltyPercent = BuildMovementPenalty(blueprint);

            FireSettings fireSettings = BuildFireSettings(blueprint, accumulators);
            GunShotPattern shotPattern = BuildShotPattern(blueprint, accumulators);
            ProjectileSettings projectile = BuildProjectile(
                blueprint,
                accumulators,
                maximumAttackDistance,
                pierce);
            GunGuidanceSpec guidance = BuildGuidance(blueprint, accumulators);
            GunImpactSpec impact = BuildImpact(blueprint, accumulators, ricochet);
            GunDamageSpec damage = BuildDamage(blueprint, accumulators);
            GunEffects effects = BuildEffects(blueprint, accumulators);

            ValidateEffectiveStructure(blueprint, projectile, guidance, impact, damage, effects);
            return new EffectiveGunEvaluatedValues(
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

        private static Dictionary<GunEffectiveStat, ModifierAccumulator> BuildAccumulators(
            Gun blueprint,
            IEnumerable<AugmentInstance> installedAugments,
            IDictionary<StableId, GunAugmentModifierSet> modifiersByAugmentId)
        {
            Dictionary<GunEffectiveStat, ModifierAccumulator> result =
                new Dictionary<GunEffectiveStat, ModifierAccumulator>();

            foreach (AugmentInstance installed in installedAugments)
            {
                GunAugmentModifierSet modifierSet = modifiersByAugmentId[installed.InstanceId];
                for (int index = 0; index < modifierSet.Modifiers.Count; index++)
                {
                    GunStatModifier modifier = modifierSet.Modifiers[index];
                    ValidateStructuralCompatibility(blueprint, modifierSet, modifier.Stat);
                    if (modifier.Stat == GunEffectiveStat.RicochetTenths)
                    {
                        ValidateRicochetModifier(modifierSet, modifier);
                    }

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
            Gun blueprint,
            GunAugmentModifierSet modifierSet,
            GunEffectiveStat stat)
        {
            string reason = null;
            switch (stat)
            {
                case GunEffectiveStat.RateOfFire:
                    if (blueprint.FireSettings.IsContinuous)
                    {
                        reason = "RateOfFire modifies firing-cycle cadence only; continuous DamageTicksPerSecond is a separate transitional cadence";
                    }
                    break;

                case GunEffectiveStat.AreaDamage:
                    if (!blueprint.IsTransitionalCatalogProjection)
                    {
                        reason = "canonical guns have one universal damage value; area delivery is projected explicitly rather than independently modified";
                    }
                    else if (blueprint.Effects.Explosion == null)
                    {
                        reason = "the authored gun has no explosion structure";
                    }
                    break;

                case GunEffectiveStat.ExplosionRadius:
                    if (blueprint.Effects.Explosion == null)
                    {
                        reason = "the authored gun has no explosion structure";
                    }
                    break;

                case GunEffectiveStat.ProjectileSpeed:
                    if (!SupportsProjectileSpeed(blueprint))
                    {
                        reason = "projectile speed is valid only for travelling Normal, Orb, or Rocket delivery";
                    }
                    break;

                case GunEffectiveStat.ProjectileRange:
                    if (!SupportsMaximumRange(blueprint))
                    {
                        reason = "the delivery does not expose a finite canonical maximum attack distance";
                    }
                    break;

                case GunEffectiveStat.PierceTenths:
                    if (!SupportsPierce(blueprint))
                    {
                        reason = "the delivery does not declare reusable canonical Pierce semantics";
                    }
                    break;

                case GunEffectiveStat.RicochetTenths:
                    if (!SupportsRicochet(blueprint))
                    {
                        reason = "the authored gun has no executable fixed-point ricochet structure";
                    }
                    break;

                case GunEffectiveStat.SpreadDegrees:
                    if (blueprint.IsTransitionalCatalogProjection)
                    {
                        if (blueprint.ShotPattern.Kind == GunShotPatternKind.Single
                            || blueprint.ShotPattern.Kind == GunShotPatternKind.Beam)
                        {
                            reason = "the transitional shot-pattern kind does not support angular spread changes";
                        }
                    }
                    else if (blueprint.ShotPattern.Kind != GunShotPatternKind.Spread)
                    {
                        reason = "canonical deterministic spread modifiers require an existing multi-emission Spread structure";
                    }
                    break;

                case GunEffectiveStat.RandomnessDegrees:
                    if (blueprint.IsTransitionalCatalogProjection)
                    {
                        if (blueprint.ShotPattern.Kind == GunShotPatternKind.Single
                            || blueprint.ShotPattern.Kind == GunShotPatternKind.Beam)
                        {
                            reason = "the transitional shot-pattern kind does not support angular randomness changes";
                        }
                    }
                    else if (blueprint.ShotPattern.Kind != GunShotPatternKind.Spray)
                    {
                        reason = "canonical random-deviation modifiers require an existing single-emission Spray structure";
                    }
                    break;

                case GunEffectiveStat.DamageOverTimePerSecond:
                case GunEffectiveStat.DamageOverTimeDurationSeconds:
                case GunEffectiveStat.DamageOverTimeTicksPerSecond:
                case GunEffectiveStat.DamageOverTimeMaximumStacks:
                    if (!blueprint.Damage.HasDamageOverTime
                        || blueprint.Effects.DamageOverTime == null)
                    {
                        reason = "the authored gun has no damage-over-time structure";
                    }
                    break;

                case GunEffectiveStat.HomingAcquisitionRange:
                case GunEffectiveStat.HomingTurnRateDegreesPerSecond:
                case GunEffectiveStat.HomingActivationDelaySeconds:
                    if (blueprint.Guidance.Mode != GunGuidanceMode.Homing)
                    {
                        reason = "the authored gun is not homing";
                    }
                    break;

                case GunEffectiveStat.RicochetMaximumRicochets:
                    if (blueprint.Impact.Ricochet == null)
                    {
                        reason = "the authored gun has no ricochet structure";
                    }
                    else if (blueprint.Impact.Ricochet.HasCanonicalFixedPointBudget)
                    {
                        reason = "legacy maximum-ricochet modifiers cannot rewrite the canonical fixed-point guaranteed-plus-one-fraction budget";
                    }
                    break;

                case GunEffectiveStat.RicochetRetainedSpeed:
                case GunEffectiveStat.RicochetRandomAngleDegrees:
                    if (blueprint.Impact.Ricochet == null)
                    {
                        reason = "the authored gun has no ricochet structure";
                    }
                    break;

                case GunEffectiveStat.ChainMaximumTargets:
                case GunEffectiveStat.ChainAcquisitionRange:
                case GunEffectiveStat.ChainRetainedDamagePerJump:
                    if (blueprint.Effects.ChainArc == null)
                    {
                        reason = "the authored gun has no chain-arc structure";
                    }
                    break;
            }

            if (reason != null)
            {
                throw new IncompatibleGunAugmentException(
                    modifierSet.Instance.InstanceId,
                    modifierSet.Definition.DefinitionId,
                    stat,
                    reason);
            }
        }

        private static void ValidateRicochetModifier(
            GunAugmentModifierSet modifierSet,
            GunStatModifier modifier)
        {
            if (modifier.Operation == GunModifierOperation.FlatAddition
                && modifier.Value >= 0d
                && modifier.Value <= int.MaxValue
                && modifier.Value == Math.Truncate(modifier.Value))
            {
                return;
            }

            throw new IncompatibleGunAugmentException(
                modifierSet.Instance.InstanceId,
                modifierSet.Definition.DefinitionId,
                modifier.Stat,
                "RicochetTenths accepts only non-negative whole-tenth flat additions");
        }

        private static bool SupportsProjectileSpeed(Gun blueprint)
        {
            return blueprint.IsTransitionalCatalogProjection
                ? blueprint.Projectile != null
                : blueprint.Delivery != null
                    && blueprint.Delivery.SupportsProjectileSpeedModifiers;
        }

        private static bool SupportsMaximumRange(Gun blueprint)
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

        private static bool SupportsPierce(Gun blueprint)
        {
            return blueprint.IsTransitionalCatalogProjection
                ? blueprint.Projectile != null
                : blueprint.Delivery != null
                    && blueprint.Delivery.SupportsCanonicalPierceModifiers;
        }

        private static bool SupportsRicochet(Gun blueprint)
        {
            GunRicochetSpec ricochet = blueprint.Impact.Ricochet;
            if (ricochet == null || !ricochet.HasCanonicalFixedPointBudget)
            {
                return false;
            }
            if (blueprint.Projectile != null)
            {
                return true;
            }
            return blueprint.Delivery != null
                && blueprint.Delivery.Type == GunDeliveryType.Laser;
        }

        private static GunAttackDistance BuildMaximumAttackDistance(
            Gun blueprint,
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators)
        {
            GunAttackDistance authored = blueprint.BaseStats == null
                ? (blueprint.Projectile == null
                    ? null
                    : GunAttackDistance.Limited(blueprint.Projectile.Range))
                : blueprint.BaseStats.MaximumAttackDistance;
            if (authored == null || !authored.IsLimited)
            {
                return authored;
            }

            return GunAttackDistance.Limited(
                RequirePositive(
                    Apply(
                        accumulators,
                        GunEffectiveStat.ProjectileRange,
                        authored.Distance),
                    GunEffectiveStat.ProjectileRange));
        }

        private static PierceValue BuildPierce(
            Gun blueprint,
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators)
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
                        GunEffectiveStat.PierceTenths,
                        authored.Tenths),
                    GunEffectiveStat.PierceTenths));
        }

        private static RicochetValue BuildRicochet(
            Gun blueprint,
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators)
        {
            RicochetValue authored;
            if (blueprint.BaseStats != null)
            {
                authored = blueprint.BaseStats.Ricochet;
            }
            else if (blueprint.Impact.Ricochet == null)
            {
                authored = new RicochetValue(0);
            }
            else if (blueprint.Impact.Ricochet.FixedPointBudget.HasValue)
            {
                authored = blueprint.Impact.Ricochet.FixedPointBudget.Value;
            }
            else
            {
                authored = new RicochetValue(
                    checked(blueprint.Impact.Ricochet.MaximumSuccessfulBounces * 10));
            }

            ModifierAccumulator accumulator;
            return accumulators.TryGetValue(
                    GunEffectiveStat.RicochetTenths,
                    out accumulator)
                ? new RicochetValue(accumulator.ApplyWholeAddition(authored.Tenths))
                : authored;
        }

        private static double BuildMovementPenalty(Gun blueprint)
        {
            return blueprint.BaseStats == null
                ? 0d
                : blueprint.BaseStats.MovementPenaltyPercent;
        }

        private static FireSettings BuildFireSettings(
            Gun blueprint,
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators)
        {
            FireSettings authored = blueprint.FireSettings;
            if (authored.IsContinuous)
            {
                return FireSettings.Create(
                    authored.Mode,
                    0d,
                    0,
                    0,
                    0d,
                    0d,
                    authored.DamageTicksPerSecond);
            }

            double rateOfFire = RequirePositive(
                Apply(accumulators, GunEffectiveStat.RateOfFire, authored.RateOfFire),
                GunEffectiveStat.RateOfFire);
            if (blueprint.IsTransitionalCatalogProjection)
            {
                return FireSettings.Create(
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
                case GunFireMode.SemiAutomatic:
                    return FireSettings.SemiAutomatic(rateOfFire);
                case GunFireMode.Automatic:
                    return FireSettings.Automatic(rateOfFire);
                case GunFireMode.Burst:
                    return FireSettings.Burst(
                        rateOfFire,
                        new GunBurstSettings(
                            authored.ShotsPerBurst,
                            authored.IntervalBetweenBurstShotsSeconds));
                default:
                    throw new InvalidOperationException(
                        "Canonical effective guns support semi-automatic, automatic, or burst fire.");
            }
        }

        private static GunShotPattern BuildShotPattern(
            Gun blueprint,
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators)
        {
            GunShotPattern authored = blueprint.ShotPattern;
            double spread = Clamp(
                Apply(accumulators, GunEffectiveStat.SpreadDegrees, authored.SpreadDegrees),
                0d,
                360d,
                GunEffectiveStat.SpreadDegrees);
            double randomness = Clamp(
                Apply(accumulators, GunEffectiveStat.RandomnessDegrees, authored.RandomnessDegrees),
                0d,
                360d,
                GunEffectiveStat.RandomnessDegrees);

            return GunShotPattern.Create(
                authored.Kind,
                authored.ProjectilesPerShot,
                spread,
                randomness,
                authored.PulsesPerShot,
                authored.IntervalBetweenPulsesSeconds);
        }

        private static ProjectileSettings BuildProjectile(
            Gun blueprint,
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators,
            GunAttackDistance maximumAttackDistance,
            PierceValue pierce)
        {
            ProjectileSettings authored = blueprint.Projectile;
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
                Apply(accumulators, GunEffectiveStat.ProjectileSpeed, authored.Speed),
                GunEffectiveStat.ProjectileSpeed);

            return ProjectileSettings.Create(
                authored.Kind,
                speed,
                maximumAttackDistance.Distance,
                pierce,
                authored.TerminationBehavior);
        }

        private static GunGuidanceSpec BuildGuidance(
            Gun blueprint,
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators)
        {
            GunGuidanceSpec authored = blueprint.Guidance;
            if (authored.Mode == GunGuidanceMode.Unguided)
            {
                return GunGuidanceSpec.Unguided();
            }

            return GunGuidanceSpec.Homing(
                RequirePositive(
                    Apply(
                        accumulators,
                        GunEffectiveStat.HomingAcquisitionRange,
                        authored.AcquisitionRange),
                    GunEffectiveStat.HomingAcquisitionRange),
                RequirePositive(
                    Apply(
                        accumulators,
                        GunEffectiveStat.HomingTurnRateDegreesPerSecond,
                        authored.TurnRateDegreesPerSecond),
                    GunEffectiveStat.HomingTurnRateDegreesPerSecond),
                ClampNonNegative(
                    Apply(
                        accumulators,
                        GunEffectiveStat.HomingActivationDelaySeconds,
                        authored.ActivationDelaySeconds),
                    GunEffectiveStat.HomingActivationDelaySeconds),
                authored.TargetPolicy,
                authored.Reacquisition);
        }

        private static GunImpactSpec BuildImpact(
            Gun blueprint,
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators,
            RicochetValue effectiveRicochet)
        {
            GunImpactSpec authored = blueprint.Impact;
            GunRicochetSpec ricochet = null;
            if (authored.Ricochet != null)
            {
                double retainedSpeed = Clamp(
                    Apply(
                        accumulators,
                        GunEffectiveStat.RicochetRetainedSpeed,
                        authored.Ricochet.RetainedSpeedPerRicochet),
                    0d,
                    1d,
                    GunEffectiveStat.RicochetRetainedSpeed);
                if (retainedSpeed <= 0d)
                {
                    throw new ArgumentOutOfRangeException(
                        GunEffectiveStat.RicochetRetainedSpeed.ToString(),
                        "Effective ricochet retained speed must remain positive after clamping.");
                }
                double randomAngle = Clamp(
                    Apply(
                        accumulators,
                        GunEffectiveStat.RicochetRandomAngleDegrees,
                        authored.Ricochet.RandomAngleDegrees),
                    0d,
                    360d,
                    GunEffectiveStat.RicochetRandomAngleDegrees);

                if (authored.Ricochet.FixedPointBudget.HasValue)
                {
                    ricochet = new GunRicochetSpec(
                        effectiveRicochet,
                        retainedSpeed,
                        randomAngle,
                        authored.Ricochet.PostBounceHomingPauseSeconds);
                }
                else
                {
                    int maximumRicochets = ToPositiveInt(
                        Apply(
                            accumulators,
                            GunEffectiveStat.RicochetMaximumRicochets,
                            authored.Ricochet.MaximumRicochets),
                        GunEffectiveStat.RicochetMaximumRicochets);
                    ricochet = new GunRicochetSpec(
                        maximumRicochets,
                        retainedSpeed,
                        randomAngle,
                        authored.Ricochet.BounceChance,
                        authored.Ricochet.PostBounceHomingPauseSeconds);
                }
            }

            return GunImpactSpec.Create(
                authored.HandlesEnemyImpact,
                authored.HandlesWallImpact,
                authored.HandlesRangeExpiry,
                authored.HandlesTermination,
                ricochet,
                authored.ExplosionTrigger);
        }

        private static GunDamageSpec BuildDamage(
            Gun blueprint,
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators)
        {
            GunDamageSpec authored = blueprint.Damage;
            double directDamage = ClampNonNegative(
                Apply(accumulators, GunEffectiveStat.DirectDamage, authored.DirectDamage),
                GunEffectiveStat.DirectDamage);
            double dotDamage = ClampNonNegative(
                Apply(
                    accumulators,
                    GunEffectiveStat.DamageOverTimePerSecond,
                    authored.DamageOverTimePerSecond),
                GunEffectiveStat.DamageOverTimePerSecond);
            double dotDuration = ClampNonNegative(
                Apply(
                    accumulators,
                    GunEffectiveStat.DamageOverTimeDurationSeconds,
                    authored.DamageOverTimeDurationSeconds),
                GunEffectiveStat.DamageOverTimeDurationSeconds);

            if (!blueprint.IsTransitionalCatalogProjection)
            {
                GunDamageOverTimeStats damageOverTime = dotDamage > 0d || dotDuration > 0d
                    ? new GunDamageOverTimeStats(dotDamage, dotDuration)
                    : null;
                return GunDamageSpec.Create(
                    authored.Category,
                    directDamage,
                    damageOverTime,
                    authored.Knockback);
            }

            return GunDamageSpec.Create(
                authored.Category,
                directDamage,
                ClampNonNegative(
                    Apply(accumulators, GunEffectiveStat.AreaDamage, authored.AreaDamage),
                    GunEffectiveStat.AreaDamage),
                dotDamage,
                dotDuration,
                authored.Knockback);
        }

        private static GunEffects BuildEffects(
            Gun blueprint,
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators)
        {
            GunExplosionEffect explosion = null;
            if (blueprint.Effects.Explosion != null)
            {
                explosion = new GunExplosionEffect(
                    RequirePositive(
                        Apply(
                            accumulators,
                            GunEffectiveStat.ExplosionRadius,
                            blueprint.Effects.Explosion.Radius),
                        GunEffectiveStat.ExplosionRadius),
                    blueprint.Effects.Explosion.MinimumDamageMultiplier);
            }

            GunDamageOverTimeEffect damageOverTime = null;
            if (blueprint.Effects.DamageOverTime != null)
            {
                damageOverTime = new GunDamageOverTimeEffect(
                    RequirePositive(
                        Apply(
                            accumulators,
                            GunEffectiveStat.DamageOverTimeTicksPerSecond,
                            blueprint.Effects.DamageOverTime.TicksPerSecond),
                        GunEffectiveStat.DamageOverTimeTicksPerSecond),
                    ToPositiveInt(
                        Apply(
                            accumulators,
                            GunEffectiveStat.DamageOverTimeMaximumStacks,
                            blueprint.Effects.DamageOverTime.MaximumStacks),
                        GunEffectiveStat.DamageOverTimeMaximumStacks),
                    blueprint.Effects.DamageOverTime.RefreshesDuration);
            }

            GunChainArcEffect chainArc = null;
            if (blueprint.Effects.ChainArc != null)
            {
                chainArc = new GunChainArcEffect(
                    ToPositiveInt(
                        Apply(
                            accumulators,
                            GunEffectiveStat.ChainMaximumTargets,
                            blueprint.Effects.ChainArc.MaximumTargets),
                        GunEffectiveStat.ChainMaximumTargets),
                    RequirePositive(
                        Apply(
                            accumulators,
                            GunEffectiveStat.ChainAcquisitionRange,
                            blueprint.Effects.ChainArc.AcquisitionRange),
                        GunEffectiveStat.ChainAcquisitionRange),
                    Clamp(
                        Apply(
                            accumulators,
                            GunEffectiveStat.ChainRetainedDamagePerJump,
                            blueprint.Effects.ChainArc.RetainedDamagePerJump),
                        0d,
                        1d,
                        GunEffectiveStat.ChainRetainedDamagePerJump));
            }

            return new GunEffects(explosion, damageOverTime, chainArc);
        }

        private static void ValidateEffectiveStructure(
            Gun blueprint,
            ProjectileSettings projectile,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunDamageSpec damage,
            GunEffects effects)
        {
            bool requiresTravellingProjectile = blueprint.Delivery == null
                ? blueprint.ShotPattern.UsesProjectiles
                : blueprint.Delivery.IsTravelling;
            if (requiresTravellingProjectile && projectile == null)
            {
                throw new InvalidOperationException(
                    "Effective travelling deliveries must retain projectile structure.");
            }
            if (guidance.Mode == GunGuidanceMode.Homing && projectile == null)
            {
                throw new InvalidOperationException(
                    "Effective homing guns must retain projectile structure.");
            }

            bool supportsNonProjectileRicochet = blueprint.Delivery != null
                && blueprint.Delivery.Type == GunDeliveryType.Laser;
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
            IDictionary<GunEffectiveStat, ModifierAccumulator> accumulators,
            GunEffectiveStat stat,
            double authoredValue)
        {
            ModifierAccumulator accumulator;
            return accumulators.TryGetValue(stat, out accumulator)
                ? accumulator.Apply(authoredValue)
                : authoredValue;
        }

        private static double ClampNonNegative(double value, GunEffectiveStat stat)
        {
            return Clamp(value, 0d, double.MaxValue, stat);
        }

        private static double RequirePositive(double value, GunEffectiveStat stat)
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
            GunEffectiveStat stat)
        {
            RequireFinite(value, stat);
            if (value < minimum)
            {
                return minimum;
            }
            return value > maximum ? maximum : value;
        }

        private static int ToNonNegativeInt(double value, GunEffectiveStat stat)
        {
            double clamped = Clamp(value, 0d, int.MaxValue, stat);
            return checked((int)Math.Round(clamped, MidpointRounding.AwayFromZero));
        }

        private static int ToPositiveInt(double value, GunEffectiveStat stat)
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

        private static void RequireFinite(double value, GunEffectiveStat stat)
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
            private readonly GunEffectiveStat stat;
            private double flatAddition;
            private int wholeAddition;
            private double additivePercentage;
            private double multiplier = 1d;
            private bool hasOverride;
            private double overrideValue;
            private StableId overrideSource;

            public ModifierAccumulator(GunEffectiveStat stat)
            {
                this.stat = stat;
            }

            public void Add(GunStatModifier modifier, StableId augmentInstanceId)
            {
                switch (modifier.Operation)
                {
                    case GunModifierOperation.FlatAddition:
                        if (stat == GunEffectiveStat.RicochetTenths)
                        {
                            wholeAddition = checked(
                                wholeAddition + checked((int)modifier.Value));
                        }
                        else
                        {
                            flatAddition += modifier.Value;
                        }
                        break;
                    case GunModifierOperation.AdditivePercentage:
                        additivePercentage += modifier.Value;
                        break;
                    case GunModifierOperation.Multiplier:
                        multiplier *= modifier.Value;
                        break;
                    case GunModifierOperation.Override:
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

            public int ApplyWholeAddition(int authoredValue)
            {
                if (stat != GunEffectiveStat.RicochetTenths
                    || flatAddition != 0d
                    || additivePercentage != 0d
                    || multiplier != 1d
                    || hasOverride)
                {
                    throw new InvalidOperationException(
                        "RicochetTenths must remain a whole flat-addition stat.");
                }

                return checked(authoredValue + wholeAddition);
            }
        }
    }
}

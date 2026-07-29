using System;
using System.Collections.Generic;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Guns.Catalog
{
    public static partial class GunCatalogBlueprintMapper
    {
        private static FireSettings BuildFireSettings(
            GunDefinitionData definition,
            GunCatalogBlueprintMappingIntent intent,
            IList<GunMappingIssue> issues)
        {
            if (intent.FireMode == GunFireMode.Continuous)
            {
                return null;
            }

            try
            {
                return FireSettings.Create(
                    intent.FireMode,
                    definition.FireRate,
                    intent.ShotsPerTrigger,
                    definition.BurstCount,
                    intent.IntervalBetweenBurstShotsSeconds,
                    intent.IntervalAfterBurstSeconds,
                    0d);
            }
            catch (Exception exception)
            {
                if (!(exception is ArgumentException) && !(exception is OverflowException))
                {
                    throw;
                }
                Add(
                    issues,
                    GunMappingIssueCode.InvalidFireConfiguration,
                    Path(definition, ".FireRate"),
                    exception.Message);
                return null;
            }
        }

        private static GunShotPattern BuildShotPattern(
            GunDefinitionData definition,
            GunCatalogBlueprintMappingIntent intent,
            IList<GunMappingIssue> issues)
        {
            if (intent.FireMode == GunFireMode.Continuous)
            {
                return null;
            }

            double spread = 0d;
            double randomness = 0d;
            switch (intent.SpreadInterpretation)
            {
                case GunCatalogSpreadInterpretation.None:
                    if (definition.SpreadDegrees != 0d)
                    {
                        Add(
                            issues,
                            GunMappingIssueCode.InvalidShotPattern,
                            Path(definition, ".SpreadDegrees"),
                            "A non-zero catalog spread requires an explicit spread interpretation.");
                        return null;
                    }
                    break;
                case GunCatalogSpreadInterpretation.AuthoredSpread:
                    spread = definition.SpreadDegrees;
                    break;
                case GunCatalogSpreadInterpretation.AuthoredRandomness:
                    randomness = definition.SpreadDegrees;
                    break;
                default:
                    Add(
                        issues,
                        GunMappingIssueCode.InvalidShotPattern,
                        Path(definition, ".SpreadDegrees"),
                        "Unknown spread interpretation.");
                    return null;
            }

            try
            {
                return GunShotPattern.Create(
                    intent.ShotPatternKind,
                    definition.ProjectilesPerTrigger,
                    spread,
                    randomness,
                    intent.PulsesPerShot,
                    intent.IntervalBetweenPulsesSeconds);
            }
            catch (Exception exception)
            {
                if (!(exception is ArgumentException) && !(exception is OverflowException))
                {
                    throw;
                }
                Add(
                    issues,
                    GunMappingIssueCode.InvalidShotPattern,
                    Path(definition, ".ProjectilesPerTrigger"),
                    exception.Message);
                return null;
            }
        }

        private static ProjectileSettings BuildProjectile(
            GunDefinitionData definition,
            GunCatalogBlueprintMappingIntent intent,
            IList<GunMappingIssue> issues)
        {
            if (intent.FireMode == GunFireMode.Continuous)
            {
                return null;
            }

            try
            {
                return ProjectileSettings.Create(
                    intent.ProjectileKind,
                    definition.ProjectileSpeed,
                    definition.Range,
                    PierceValue.FromLegacyInteger(definition.Pierce),
                    intent.ProjectileTermination);
            }
            catch (Exception exception)
            {
                if (!(exception is ArgumentException) && !(exception is OverflowException))
                {
                    throw;
                }
                Add(
                    issues,
                    GunMappingIssueCode.InvalidProjectileConfiguration,
                    Path(definition, ".ProjectileSpeed"),
                    exception.Message);
                return null;
            }
        }

        private static GunDamageSpec BuildDamage(
            GunDefinitionData definition,
            GunDamageCategory category,
            IList<GunMappingIssue> issues)
        {
            try
            {
                return GunDamageSpec.Create(
                    category,
                    definition.DamagePerProjectile,
                    definition.AreaDamagePerTrigger,
                    definition.DotDps,
                    definition.DotDuration,
                    definition.Knockback);
            }
            catch (Exception exception)
            {
                if (!(exception is ArgumentException) && !(exception is OverflowException))
                {
                    throw;
                }
                Add(
                    issues,
                    GunMappingIssueCode.DomainContractRejected,
                    Path(definition, ".DamagePerProjectile"),
                    exception.Message);
                return null;
            }
        }

        private static GunEffects BuildEffects(
            GunDefinitionData definition,
            GunCatalogBlueprintMappingIntent intent,
            IList<GunMappingIssue> issues)
        {
            GunExplosionEffect explosion = null;
            bool hasExplosionData = definition.ExplosionRadius > 0d
                || definition.AreaDamagePerTrigger > 0d;
            bool hasExplosionTrigger = intent.Impact != null
                && intent.Impact.ExplosionTrigger != null;
            if (hasExplosionData)
            {
                if (!hasExplosionTrigger)
                {
                    Add(
                        issues,
                        GunMappingIssueCode.MissingExplosionTrigger,
                        Path(definition, ".Impact.ExplosionTrigger"),
                        "Authored explosion radius or area damage requires an explicit impact explosion trigger.");
                }

                if (intent.Explosion == null)
                {
                    Add(
                        issues,
                        GunMappingIssueCode.MissingExplosionMapping,
                        Path(definition, ".ExplosionRadius"),
                        "Explosion radius or area damage requires explicit falloff semantics.");
                }
                else
                {
                    try
                    {
                        explosion = new GunExplosionEffect(
                            definition.ExplosionRadius,
                            intent.Explosion.MinimumDamageMultiplier);
                    }
                    catch (ArgumentException exception)
                    {
                        Add(
                            issues,
                            GunMappingIssueCode.MissingExplosionMapping,
                            Path(definition, ".ExplosionRadius"),
                            exception.Message);
                    }
                }
            }
            else
            {
                if (intent.Explosion != null)
                {
                    Add(
                        issues,
                        GunMappingIssueCode.UnexpectedExplosionMapping,
                        Path(definition, ".ExplosionRadius"),
                        "Mapping intent contains an explosion effect but the catalog has no explosion data.");
                }
                if (hasExplosionTrigger)
                {
                    Add(
                        issues,
                        GunMappingIssueCode.UnexpectedExplosionTrigger,
                        Path(definition, ".Impact.ExplosionTrigger"),
                        "Impact intent contains an explosion trigger but the catalog has no explosion radius or area damage.");
                }
            }

            GunDamageOverTimeEffect dot = null;
            bool hasDot = definition.DotDps > 0d || definition.DotDuration > 0d;
            if (hasDot)
            {
                if (definition.DotDps <= 0d || definition.DotDuration <= 0d)
                {
                    Add(
                        issues,
                        GunMappingIssueCode.MissingDamageOverTimeMapping,
                        Path(definition, ".DoTDPS"),
                        "DoT magnitude and duration must both be positive.");
                }
                else if (intent.DamageOverTime == null)
                {
                    Add(
                        issues,
                        GunMappingIssueCode.MissingDamageOverTimeMapping,
                        Path(definition, ".DoTDPS"),
                        "Catalog DoT damage requires explicit tick, stacking, and refresh semantics.");
                }
                else
                {
                    try
                    {
                        dot = new GunDamageOverTimeEffect(
                            intent.DamageOverTime.TicksPerSecond,
                            intent.DamageOverTime.MaximumStacks,
                            intent.DamageOverTime.RefreshesDuration);
                    }
                    catch (ArgumentException exception)
                    {
                        Add(
                            issues,
                            GunMappingIssueCode.MissingDamageOverTimeMapping,
                            Path(definition, ".DoTDPS"),
                            exception.Message);
                    }
                }
            }
            else if (intent.DamageOverTime != null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnexpectedDamageOverTimeMapping,
                    Path(definition, ".DoTDPS"),
                    "Mapping intent contains a DoT effect but the catalog has no DoT data.");
            }

            GunChainArcEffect chain = null;
            bool hasChain = definition.ChainTargets > 0 || definition.ChainRange > 0d;
            if (hasChain)
            {
                if (definition.ChainTargets < 1 || definition.ChainRange <= 0d)
                {
                    Add(
                        issues,
                        GunMappingIssueCode.MissingChainMapping,
                        Path(definition, ".ChainTargets"),
                        "Chain target count and range must both be positive.");
                }
                else if (intent.Chain == null)
                {
                    Add(
                        issues,
                        GunMappingIssueCode.MissingChainMapping,
                        Path(definition, ".ChainTargets"),
                        "Catalog chain data requires explicit retained-damage semantics.");
                }
                else
                {
                    try
                    {
                        chain = new GunChainArcEffect(
                            definition.ChainTargets,
                            definition.ChainRange,
                            intent.Chain.RetainedDamagePerJump);
                    }
                    catch (ArgumentException exception)
                    {
                        Add(
                            issues,
                            GunMappingIssueCode.MissingChainMapping,
                            Path(definition, ".ChainTargets"),
                            exception.Message);
                    }
                }
            }
            else if (intent.Chain != null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnexpectedChainMapping,
                    Path(definition, ".ChainTargets"),
                    "Mapping intent contains a chain effect but the catalog has no chain data.");
            }

            return new GunEffects(explosion, dot, chain);
        }

    }
}

using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    public static partial class WeaponCatalogBlueprintMapper
    {
        private static WeaponFireSettings BuildFireSettings(
            WeaponDefinitionData definition,
            WeaponCatalogBlueprintMappingIntent intent,
            IList<WeaponBlueprintMappingIssue> issues)
        {
            if (intent.FireMode == WeaponFireMode.Continuous)
            {
                return null;
            }

            try
            {
                return WeaponFireSettings.Create(
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
                    WeaponBlueprintMappingIssueCode.InvalidFireConfiguration,
                    Path(definition, ".FireRate"),
                    exception.Message);
                return null;
            }
        }

        private static WeaponShotPattern BuildShotPattern(
            WeaponDefinitionData definition,
            WeaponCatalogBlueprintMappingIntent intent,
            IList<WeaponBlueprintMappingIssue> issues)
        {
            if (intent.FireMode == WeaponFireMode.Continuous)
            {
                return null;
            }

            double spread = 0d;
            double randomness = 0d;
            switch (intent.SpreadInterpretation)
            {
                case WeaponCatalogSpreadInterpretation.None:
                    if (definition.SpreadDegrees != 0d)
                    {
                        Add(
                            issues,
                            WeaponBlueprintMappingIssueCode.InvalidShotPattern,
                            Path(definition, ".SpreadDegrees"),
                            "A non-zero catalog spread requires an explicit spread interpretation.");
                        return null;
                    }
                    break;
                case WeaponCatalogSpreadInterpretation.AuthoredSpread:
                    spread = definition.SpreadDegrees;
                    break;
                case WeaponCatalogSpreadInterpretation.AuthoredRandomness:
                    randomness = definition.SpreadDegrees;
                    break;
                default:
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.InvalidShotPattern,
                        Path(definition, ".SpreadDegrees"),
                        "Unknown spread interpretation.");
                    return null;
            }

            try
            {
                return WeaponShotPattern.Create(
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
                    WeaponBlueprintMappingIssueCode.InvalidShotPattern,
                    Path(definition, ".ProjectilesPerTrigger"),
                    exception.Message);
                return null;
            }
        }

        private static WeaponProjectileSpec BuildProjectile(
            WeaponDefinitionData definition,
            WeaponCatalogBlueprintMappingIntent intent,
            IList<WeaponBlueprintMappingIssue> issues)
        {
            if (intent.FireMode == WeaponFireMode.Continuous)
            {
                return null;
            }

            try
            {
                return WeaponProjectileSpec.Create(
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
                    WeaponBlueprintMappingIssueCode.InvalidProjectileConfiguration,
                    Path(definition, ".ProjectileSpeed"),
                    exception.Message);
                return null;
            }
        }

        private static WeaponDamageSpec BuildDamage(
            WeaponDefinitionData definition,
            WeaponDamageCategory category,
            IList<WeaponBlueprintMappingIssue> issues)
        {
            try
            {
                return WeaponDamageSpec.Create(
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
                    WeaponBlueprintMappingIssueCode.DomainContractRejected,
                    Path(definition, ".DamagePerProjectile"),
                    exception.Message);
                return null;
            }
        }

        private static WeaponEffects BuildEffects(
            WeaponDefinitionData definition,
            WeaponCatalogBlueprintMappingIntent intent,
            IList<WeaponBlueprintMappingIssue> issues)
        {
            WeaponExplosionEffect explosion = null;
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
                        WeaponBlueprintMappingIssueCode.MissingExplosionTrigger,
                        Path(definition, ".Impact.ExplosionTrigger"),
                        "Authored explosion radius or area damage requires an explicit impact explosion trigger.");
                }

                if (intent.Explosion == null)
                {
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.MissingExplosionMapping,
                        Path(definition, ".ExplosionRadius"),
                        "Explosion radius or area damage requires explicit falloff semantics.");
                }
                else
                {
                    try
                    {
                        explosion = new WeaponExplosionEffect(
                            definition.ExplosionRadius,
                            intent.Explosion.MinimumDamageMultiplier);
                    }
                    catch (ArgumentException exception)
                    {
                        Add(
                            issues,
                            WeaponBlueprintMappingIssueCode.MissingExplosionMapping,
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
                        WeaponBlueprintMappingIssueCode.UnexpectedExplosionMapping,
                        Path(definition, ".ExplosionRadius"),
                        "Mapping intent contains an explosion effect but the catalog has no explosion data.");
                }
                if (hasExplosionTrigger)
                {
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.UnexpectedExplosionTrigger,
                        Path(definition, ".Impact.ExplosionTrigger"),
                        "Impact intent contains an explosion trigger but the catalog has no explosion radius or area damage.");
                }
            }

            WeaponDamageOverTimeEffect dot = null;
            bool hasDot = definition.DotDps > 0d || definition.DotDuration > 0d;
            if (hasDot)
            {
                if (definition.DotDps <= 0d || definition.DotDuration <= 0d)
                {
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.MissingDamageOverTimeMapping,
                        Path(definition, ".DoTDPS"),
                        "DoT magnitude and duration must both be positive.");
                }
                else if (intent.DamageOverTime == null)
                {
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.MissingDamageOverTimeMapping,
                        Path(definition, ".DoTDPS"),
                        "Catalog DoT damage requires explicit tick, stacking, and refresh semantics.");
                }
                else
                {
                    try
                    {
                        dot = new WeaponDamageOverTimeEffect(
                            intent.DamageOverTime.TicksPerSecond,
                            intent.DamageOverTime.MaximumStacks,
                            intent.DamageOverTime.RefreshesDuration);
                    }
                    catch (ArgumentException exception)
                    {
                        Add(
                            issues,
                            WeaponBlueprintMappingIssueCode.MissingDamageOverTimeMapping,
                            Path(definition, ".DoTDPS"),
                            exception.Message);
                    }
                }
            }
            else if (intent.DamageOverTime != null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnexpectedDamageOverTimeMapping,
                    Path(definition, ".DoTDPS"),
                    "Mapping intent contains a DoT effect but the catalog has no DoT data.");
            }

            WeaponChainArcEffect chain = null;
            bool hasChain = definition.ChainTargets > 0 || definition.ChainRange > 0d;
            if (hasChain)
            {
                if (definition.ChainTargets < 1 || definition.ChainRange <= 0d)
                {
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.MissingChainMapping,
                        Path(definition, ".ChainTargets"),
                        "Chain target count and range must both be positive.");
                }
                else if (intent.Chain == null)
                {
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.MissingChainMapping,
                        Path(definition, ".ChainTargets"),
                        "Catalog chain data requires explicit retained-damage semantics.");
                }
                else
                {
                    try
                    {
                        chain = new WeaponChainArcEffect(
                            definition.ChainTargets,
                            definition.ChainRange,
                            intent.Chain.RetainedDamagePerJump);
                    }
                    catch (ArgumentException exception)
                    {
                        Add(
                            issues,
                            WeaponBlueprintMappingIssueCode.MissingChainMapping,
                            Path(definition, ".ChainTargets"),
                            exception.Message);
                    }
                }
            }
            else if (intent.Chain != null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnexpectedChainMapping,
                    Path(definition, ".ChainTargets"),
                    "Mapping intent contains a chain effect but the catalog has no chain data.");
            }

            return new WeaponEffects(explosion, dot, chain);
        }

    }
}

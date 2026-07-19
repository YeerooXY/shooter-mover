using System;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponCatalogRuntimeProfileResolver
    {
        private static WeaponProfileResolution Reject(
            WeaponProfileResolutionStatus status,
            string code)
        {
            return WeaponProfileResolution.Reject(status, code);
        }

        private static bool Validate(WeaponDefinitionData definition, out string code)
        {
            if (definition.BurstCount != 1 || definition.HealingPerSecond > Epsilon)
            {
                code = "weapon-effect-unsupported:" + definition.DefinitionId;
                return false;
            }

            if (!IsPositive(definition.FireRate)
                || definition.ProjectilesPerTrigger < 1
                || definition.ProjectilesPerTrigger > WeaponRuntimeFiringProfile.MaximumEffectsPerFire
                || !IsInRange(definition.SpreadDegrees, 0d, 360d)
                || !IsPositive(definition.ProjectileSpeed)
                || !IsPositive(definition.Range)
                || !IsNonNegative(definition.DamagePerProjectile)
                || definition.Pierce < 0
                || !IsNonNegative(definition.AreaDamagePerTrigger)
                || !IsNonNegative(definition.ExplosionRadius)
                || !IsNonNegative(definition.DotDps)
                || !IsNonNegative(definition.DotDuration)
                || !IsNonNegative(definition.PoolRadius)
                || !IsNonNegative(definition.PoolDuration)
                || definition.ChainTargets < 0
                || !IsNonNegative(definition.ChainRange)
                || !IsNonNegative(definition.Knockback)
                || string.IsNullOrWhiteSpace(definition.DamageType))
            {
                code = "weapon-tuning-invalid:" + definition.DefinitionId;
                return false;
            }

            bool explosive = definition.AreaDamagePerTrigger > Epsilon
                || definition.ExplosionRadius > Epsilon;
            bool chain = definition.ChainTargets > 0 || definition.ChainRange > Epsilon;
            bool damageOverTime = definition.DotShare > Epsilon
                || definition.DotDps > Epsilon
                || definition.DotDuration > Epsilon
                || definition.PoolRadius > Epsilon
                || definition.PoolDuration > Epsilon;

            int effectFamilies = (explosive ? 1 : 0)
                + (chain ? 1 : 0)
                + (damageOverTime ? 1 : 0);
            if (effectFamilies > 1)
            {
                code = "weapon-effect-unsupported-combination:" + definition.DefinitionId;
                return false;
            }

            if (explosive
                && (!IsPositive(definition.AreaDamagePerTrigger)
                    || !IsPositive(definition.ExplosionRadius)))
            {
                code = "weapon-tuning-invalid-explosion:" + definition.DefinitionId;
                return false;
            }

            if (chain
                && (definition.ChainTargets < 1
                    || !IsPositive(definition.ChainRange)
                    || definition.ProjectilesPerTrigger != 1
                    || !IsPositive(definition.DamagePerProjectile)))
            {
                code = "weapon-tuning-invalid-chain:" + definition.DefinitionId;
                return false;
            }

            if (damageOverTime
                && (!IsPositive(definition.DotDps)
                    || !IsPositive(definition.DotDuration)
                    || !IsPositive(definition.PoolRadius)
                    || !IsPositive(definition.PoolDuration)))
            {
                code = "weapon-effect-unsupported-damage-over-time-without-pool:"
                    + definition.DefinitionId;
                return false;
            }

            if (!explosive
                && !chain
                && !damageOverTime
                && !IsPositive(definition.DamagePerProjectile))
            {
                code = "weapon-tuning-invalid-direct:" + definition.DefinitionId;
                return false;
            }

            code = string.Empty;
            return true;
        }

        private static bool IsPositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }

        private static bool IsNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private static bool IsInRange(double value, double minimum, double maximum)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= minimum
                && value <= maximum;
        }
    }
}

using ShooterMover.Domain.Guns;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Exact gameplay compatibility gate for the lightweight player firing loop.
    /// It accepts only behaviours that the current live Bullet path realizes.
    /// </summary>
    public static class GunPlayRules
    {
        public static bool Supports(EffectiveGun gun)
        {
            if (gun == null
                || !gun.UsesCanonicalAuthoredDefinition
                || gun.Projectile == null
                || gun.Guidance == null
                || gun.Impact == null
                || gun.Damage == null
                || gun.Effects == null
                || gun.Damage.DirectDamage <= 0d
                || gun.Damage.HasAreaDamage
                || gun.Impact.Ricochet != null
                || gun.Effects.ChainArc != null
                || !gun.Impact.HandlesEnemyImpact
                || !gun.Impact.HandlesWallImpact
                || !gun.Impact.HandlesRangeExpiry
                || !gun.Impact.HandlesTermination)
            {
                return false;
            }

            if (gun.Guidance.Mode != GunGuidanceMode.Unguided
                && gun.Guidance.Mode != GunGuidanceMode.Homing)
            {
                return false;
            }

            if (gun.Guidance.Mode == GunGuidanceMode.Homing
                && (gun.Guidance.AcquisitionRange <= 0d
                    || gun.Guidance.TurnRateDegreesPerSecond <= 0d
                    || gun.Guidance.ActivationDelaySeconds < 0d))
            {
                return false;
            }

            bool hasDotData = gun.Damage.HasDamageOverTime;
            bool hasDotEffect = gun.Effects.DamageOverTime != null;
            if (hasDotData != hasDotEffect)
            {
                return false;
            }

            switch (gun.Projectile.Kind)
            {
                case GunProjectileKind.RegularProjectile:
                case GunProjectileKind.Orb:
                    return gun.Projectile.TerminationBehavior
                            == GunProjectileTerminationBehavior.StopWhenPierceIsSpent
                        && gun.Effects.Explosion == null;

                case GunProjectileKind.Rocket:
                    GunExplosionTriggerSpec trigger =
                        gun.Impact.ExplosionTrigger;
                    return gun.Projectile.TerminationBehavior
                            == GunProjectileTerminationBehavior.StopOnFirstBlockingImpact
                        && gun.Guidance.Mode == GunGuidanceMode.Unguided
                        && gun.Effects.Explosion != null
                        && !hasDotData
                        && trigger != null
                        && trigger.OnEnemyImpact
                        && trigger.OnWallImpact;

                default:
                    return false;
            }
        }
    }
}

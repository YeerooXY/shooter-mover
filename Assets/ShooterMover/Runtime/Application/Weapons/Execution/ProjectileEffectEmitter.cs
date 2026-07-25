using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Projects an already-resolved projectile decision into immutable descriptions. It does not
    /// move projectiles, decide impacts, or alter the retained WeaponEffectBatch authority.
    /// </summary>
    public sealed class ProjectileEffectEmitter
    {
        public ProjectileEmissionResult Emit(ProjectileImpactDecision decision)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            List<ProjectileEffectEmission> emissions =
                new List<ProjectileEffectEmission>();
            if (!decision.Handled || decision.RequiresWallImpactResolution)
            {
                return new ProjectileEmissionResult(emissions);
            }

            ProjectileLifecycleState state = decision.StateAfter;
            ProjectileExecutionProfile profile = state.Profile;
            int eventOrdinal = state.EventOrdinal;
            bool canonicalRocket = profile.IsCanonicalRocket;
            if (canonicalRocket
                && decision.Contact.Kind == ProjectileContactKind.Enemy
                && decision.EnemyImpactApplied
                && (decision.ExplosionTriggerReasons
                    & WeaponExplosionTriggerReason.EnemyImpact) == 0)
            {
                throw new InvalidOperationException(
                    "projectile-emission-canonical-rocket-enemy-explosion-required");
            }

            if (decision.EnemyImpactApplied && !canonicalRocket)
            {
                emissions.Add(Create(
                    ProjectileEffectEmissionKind.EnemyImpact,
                    decision,
                    eventOrdinal,
                    WeaponExplosionTriggerReason.None,
                    ProjectileTerminationReason.None));
            }
            else if (decision.Contact.Kind == ProjectileContactKind.Wall
                && profile.Impact.HandlesWallImpact)
            {
                emissions.Add(Create(
                    ProjectileEffectEmissionKind.WallImpact,
                    decision,
                    eventOrdinal,
                    WeaponExplosionTriggerReason.None,
                    ProjectileTerminationReason.None));
            }
            else if (decision.Contact.Kind == ProjectileContactKind.RangeExpiry
                && profile.Impact.HandlesRangeExpiry)
            {
                emissions.Add(Create(
                    ProjectileEffectEmissionKind.RangeExpiry,
                    decision,
                    eventOrdinal,
                    WeaponExplosionTriggerReason.None,
                    ProjectileTerminationReason.None));
            }

            if (decision.ExplosionTriggerReasons != WeaponExplosionTriggerReason.None)
            {
                if (profile.Effects.Explosion == null)
                {
                    throw new InvalidOperationException(
                        "projectile-emission-explosion-effect-required");
                }

                emissions.Add(Create(
                    ProjectileEffectEmissionKind.Explosion,
                    decision,
                    eventOrdinal,
                    decision.ExplosionTriggerReasons,
                    ProjectileTerminationReason.None));
            }

            if (decision.Terminates && profile.Impact.HandlesTermination)
            {
                emissions.Add(Create(
                    ProjectileEffectEmissionKind.Termination,
                    decision,
                    eventOrdinal,
                    WeaponExplosionTriggerReason.None,
                    decision.TerminationReason));
            }

            if (canonicalRocket
                && decision.Contact.Kind == ProjectileContactKind.Enemy
                && decision.EnemyImpactApplied
                && !ContainsCanonicalRocketExplosion(emissions))
            {
                throw new InvalidOperationException(
                    "projectile-emission-canonical-rocket-explosion-missing");
            }

            return new ProjectileEmissionResult(emissions);
        }

        private static bool ContainsCanonicalRocketExplosion(
            IList<ProjectileEffectEmission> emissions)
        {
            for (int index = 0; index < emissions.Count; index++)
            {
                ProjectileEffectEmission emission = emissions[index];
                if (emission != null
                    && emission.Kind == ProjectileEffectEmissionKind.Explosion
                    && emission.IsCanonicalRocket)
                {
                    return true;
                }
            }
            return false;
        }

        private static ProjectileEffectEmission Create(
            ProjectileEffectEmissionKind kind,
            ProjectileImpactDecision decision,
            int eventOrdinal,
            WeaponExplosionTriggerReason explosionReasons,
            ProjectileTerminationReason terminationReason)
        {
            ProjectileLifecycleState state = decision.StateAfter;
            return new ProjectileEffectEmission(
                kind,
                state.Lifecycle,
                decision.Contact.Kind,
                decision.Contact.Target,
                decision.Contact.SurfaceId,
                decision.Contact.Position,
                eventOrdinal,
                explosionReasons,
                terminationReason,
                state.Profile,
                state.Profile.Damage,
                state.Profile.Effects);
        }
    }
}

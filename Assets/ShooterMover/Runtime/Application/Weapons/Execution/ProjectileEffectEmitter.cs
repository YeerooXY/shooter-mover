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

            if (decision.EnemyImpactApplied)
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
                        "Projectile explosion decisions require authored explosion effect data.");
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

            return new ProjectileEmissionResult(emissions);
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
                state.Profile.Damage,
                state.Profile.Effects);
        }
    }
}

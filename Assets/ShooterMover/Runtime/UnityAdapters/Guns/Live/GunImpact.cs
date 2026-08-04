using System;
using System.Globalization;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Applies already-resolved projectile emissions to current Unity targets.
    /// The canonical core remains the authority for direct, explosion, and DoT decisions.
    /// </summary>
    public sealed class GunImpact
    {
        private readonly ProjectileExplosionResolutionBridge explosions =
            new ProjectileExplosionResolutionBridge(new Explosion());

        public void ApplyEnemy(
            ProjectileEmissionResult result,
            Damageable directTarget,
            GunTargets targets)
        {
            if (result == null
                || directTarget == null
                || targets == null)
            {
                throw new ArgumentNullException();
            }

            bool applied = false;
            for (int index = 0; index < result.Emissions.Count; index++)
            {
                ProjectileEffectEmission emission = result.Emissions[index];
                switch (emission.Kind)
                {
                    case ProjectileEffectEmissionKind.EnemyImpact:
                        ApplyDirect(emission, directTarget);
                        applied = true;
                        break;

                    case ProjectileEffectEmissionKind.Explosion:
                        ApplyExplosion(emission, targets);
                        applied = true;
                        break;

                    case ProjectileEffectEmissionKind.Termination:
                        break;

                    default:
                        throw new InvalidOperationException(
                            "player-gun-enemy-emission-invalid:"
                            + emission.Kind);
                }
            }

            if (!applied)
            {
                throw new InvalidOperationException(
                    "player-gun-enemy-damage-emission-missing");
            }
        }

        public void ApplyEnd(
            ProjectileEmissionResult result,
            ProjectileEffectEmissionKind expectedContact,
            GunTargets targets)
        {
            if (result == null || targets == null)
            {
                throw new ArgumentNullException();
            }
            if (expectedContact != ProjectileEffectEmissionKind.WallImpact
                && expectedContact
                    != ProjectileEffectEmissionKind.RangeExpiry)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedContact));
            }

            bool contactSeen = false;
            for (int index = 0; index < result.Emissions.Count; index++)
            {
                ProjectileEffectEmission emission = result.Emissions[index];
                if (emission.Kind == expectedContact)
                {
                    contactSeen = true;
                    continue;
                }
                if (emission.Kind == ProjectileEffectEmissionKind.Explosion)
                {
                    ApplyExplosion(emission, targets);
                    continue;
                }
                if (emission.Kind
                    == ProjectileEffectEmissionKind.Termination)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    "player-gun-terminal-emission-invalid:"
                    + emission.Kind);
            }

            if (!contactSeen)
            {
                throw new InvalidOperationException(
                    "player-gun-terminal-contact-emission-missing");
            }
        }

        private static void ApplyDirect(
            ProjectileEffectEmission emission,
            Damageable target)
        {
            if (emission == null
                || emission.Kind != ProjectileEffectEmissionKind.EnemyImpact
                || emission.Target == null
                || emission.Lifecycle == null
                || emission.Damage == null
                || emission.Damage.DirectDamage <= 0d
                || emission.Damage.HasAreaDamage
                || target.DamageableStableId == null
                || target.DamageableLifecycleGeneration <= 0L)
            {
                throw new InvalidOperationException(
                    "player-gun-direct-emission-invalid");
            }

            var expected = new GunTargetReference(
                new GunActorInstanceId(target.DamageableStableId),
                new LifecycleGeneration(
                    target.DamageableLifecycleGeneration));
            if (!emission.Target.Equals(expected))
            {
                throw new InvalidOperationException(
                    "player-gun-direct-target-mismatch");
            }

            HitDelivery.Deliver(target, BuildDirectHit(emission, target));
            bool hasDotData = emission.Damage.HasDamageOverTime;
            bool hasDotEffect = emission.Effects != null
                && emission.Effects.DamageOverTime != null;
            if (hasDotData != hasDotEffect)
            {
                throw new InvalidOperationException(
                    "player-gun-dot-contract-mismatch");
            }
            if (hasDotData && target.CanTakeDamage)
            {
                GunDot dot = target.GetComponent<GunDot>();
                if (dot == null) dot = target.gameObject.AddComponent<GunDot>();
                dot.Apply(emission, target);
            }
        }

        private void ApplyExplosion(
            ProjectileEffectEmission emission,
            GunTargets targets)
        {
            GunExplosionResolution resolution = explosions.Resolve(
                emission,
                targets,
                GunEffectLineOfSightPolicy.Ignore,
                null);
            GunExplosionView.Show(emission, targets);
            for (int index = 0; index < resolution.Decisions.Count; index++)
            {
                GunExplosionDamageDecision decision =
                    resolution.Decisions[index];
                Damageable target;
                if (!targets.TryResolve(decision.Target, out target)
                    || target == null
                    || !target.CanTakeDamage)
                {
                    continue;
                }
                HitDelivery.Deliver(
                    target,
                    BuildExplosionHit(
                        emission,
                        decision,
                        target,
                        index));
            }
        }

        private static Hit BuildDirectHit(
            ProjectileEffectEmission emission,
            Damageable target)
        {
            GunEffectIdentity identity =
                emission.Lifecycle.Identity.SourceIdentity;
            StableId eventId = StableId.Create(
                "direct-damage-operation",
                "canonical-player-projectile-"
                + Hash64(
                    emission.ToCanonicalString()
                    + "|" + target.DamageableStableId
                    + "|" + target.DamageableLifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture)));
            long order = checked(
                emission.Lifecycle.LaunchSimulationTick * 4096L
                + emission.EventOrdinal);
            return new Hit(
                eventId,
                identity.ActorId.Value,
                identity.ParticipantId.Value,
                target.DamageableStableId,
                target.DamageableLifecycleGeneration,
                order,
                (int)emission.Damage.Category,
                emission.Damage.DirectDamage,
                Time.fixedTimeAsDouble);
        }

        private static Hit BuildExplosionHit(
            ProjectileEffectEmission emission,
            GunExplosionDamageDecision decision,
            Damageable target,
            int decisionIndex)
        {
            GunEffectIdentity identity =
                emission.Lifecycle.Identity.SourceIdentity;
            StableId eventId = StableId.Create(
                "explosion-damage-operation",
                "canonical-player-projectile-"
                + Hash64(
                    emission.ToCanonicalString()
                    + "|explosion|" + target.DamageableStableId
                    + "|" + target.DamageableLifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture)));
            long order = checked(
                emission.Lifecycle.LaunchSimulationTick * 4096L
                + (emission.EventOrdinal * 64L)
                + decisionIndex);
            return new Hit(
                eventId,
                identity.ActorId.Value,
                identity.ParticipantId.Value,
                target.DamageableStableId,
                target.DamageableLifecycleGeneration,
                order,
                (int)decision.DamageCategory,
                decision.Damage,
                Time.fixedTimeAsDouble);
        }

        private static string Hash64(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= prime;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}

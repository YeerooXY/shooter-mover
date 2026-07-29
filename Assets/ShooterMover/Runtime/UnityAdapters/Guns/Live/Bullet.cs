using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    [DisallowMultipleComponent]
    public sealed class Bullet : MonoBehaviour
    {
        private static readonly StableId BlockingWallId = StableId.Create(
            "canonical-projectile-wall",
            "blocking-contact");

        private readonly ProjectileImpactResolver impactResolver =
            new ProjectileImpactResolver(
                new SharedDeterministicRandomFractionalPierceRoller());
        private readonly ProjectileEffectEmitter effectEmitter =
            new ProjectileEffectEmitter();
        private readonly HashSet<string> impactedTargets =
            new HashSet<string>(StringComparer.Ordinal);

        private ProjectileLaunchEffect effect;
        private ProjectileLifecycleState state;
        private Transform sourceOwner;
        private Rigidbody2D body;
        private CircleCollider2D trigger;
        private Action<Bullet> finishedCallback;
        private bool configured;
        private bool launched;
        private bool completed;
        private bool impactCommitted;
        private bool rangeExpiryPending;
        private string lastDiagnostic = string.Empty;

        public bool TryConfigure(
            ProjectileLaunchEffect configuredEffect,
            Sprite bulletSprite,
            Transform configuredSourceOwner,
            Action<Bullet> onFinished)
        {
            if (configured
                || configuredEffect == null
                || configuredEffect.InitialState == null
                || bulletSprite == null
                || configuredSourceOwner == null
                || onFinished == null)
            {
                return false;
            }

            effect = configuredEffect;
            state = configuredEffect.InitialState;
            sourceOwner = configuredSourceOwner;
            finishedCallback = onFinished;
            Vector2 direction = ToUnity(state.Direction);
            if (!state.IsActive
                || state.Speed <= 0d
                || state.RemainingRange <= 0d
                || direction.sqrMagnitude < 0.000001f)
            {
                return false;
            }

            Vector2 position = ToUnity(state.Position);
            transform.position = new Vector3(position.x, position.y, 0f);
            transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localScale = new Vector3(0.28f, 0.1f, 1f);
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = bulletSprite;
            renderer.color = new Color(1f, 0.82f, 0.2f, 1f);
            renderer.sortingOrder = 100;

            body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.simulated = false;
            trigger = gameObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.12f;
            configured = true;
            return true;
        }

        public bool Launch()
        {
            if (!configured || completed) return false;
            if (launched) return true;
            if (!gameObject.activeInHierarchy || body == null || trigger == null)
            {
                return false;
            }
            launched = true;
            body.position = ToUnity(state.Position);
            body.simulated = true;
            return true;
        }

        public void RemoveFromGame()
        {
            if (completed) return;
            Complete();
        }

        private void FixedUpdate()
        {
            if (!configured || !launched || completed || state == null) return;
            if (rangeExpiryPending)
            {
                rangeExpiryPending = false;
                ResolveRangeExpiry();
                return;
            }
            if (!state.IsActive)
            {
                Complete();
                return;
            }

            double distance = Math.Min(
                state.Speed * Time.fixedDeltaTime,
                state.RemainingRange);
            GunVector2 next = new GunVector2(
                state.Position.X + (state.Direction.X * distance),
                state.Position.Y + (state.Direction.Y * distance));
            state = state.WithKinematics(
                next,
                state.DistanceTravelled + distance);
            body.MovePosition(ToUnity(state.Position));

            // MovePosition is simulated after FixedUpdate. Defer canonical range expiry until the
            // following fixed tick so the final swept segment can still report a target or wall hit.
            if (state.RemainingRange <= 0.0000001d)
            {
                rangeExpiryPending = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (completed
                || impactCommitted
                || other == null
                || other.isTrigger
                || state == null
                || !state.IsActive
                || IsSourceCollider(other))
            {
                return;
            }

            Damageable target =
                other.GetComponentInParent<Damageable>();
            if (target != null)
            {
                if (target.CanTakeDamage) ResolveDamageableImpact(target);
                return;
            }

            ResolveBlockingWallImpact();
        }

        private void ResolveDamageableImpact(Damageable target)
        {
            if (target == null
                || !target.CanTakeDamage
                || target.DamageableStableId == null
                || target.DamageableLifecycleGeneration <= 0L)
            {
                return;
            }

            string targetKey = target.DamageableStableId
                + "|" + target.DamageableLifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture);
            if (impactedTargets.Contains(targetKey)) return;

            try
            {
                var targetReference = new GunTargetReference(
                    new GunActorInstanceId(target.DamageableStableId),
                    new LifecycleGeneration(target.DamageableLifecycleGeneration));
                ProjectileImpactDecision decision = impactResolver.Resolve(
                    state,
                    ProjectileContact.Enemy(targetReference, state.Position));
                ProjectileEffectEmission emission = FindSingleDirectDamageEmission(
                    effectEmitter.Emit(decision),
                    targetReference);
                if (!decision.Handled
                    || !decision.EnemyImpactApplied
                    || decision.StateAfter == null
                    || emission == null)
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-damageable-impact-invalid");
                }

                impactedTargets.Add(targetKey);
                impactCommitted = true;
                state = decision.StateAfter;
                StopTravel();

                Hit hit = BuildDamageHit(emission, target);
                try
                {
                    HitDelivery.Deliver(target, hit);
                }
                catch (Exception exception)
                {
                    if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                    Report(
                        "canonical-projectile-target-hit-failed:"
                        + exception.Message);
                }

                CompleteResolvedImpact();
            }
            catch (Exception exception)
            {
                if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                Report(
                    "canonical-projectile-damageable-impact-failed:"
                    + exception.Message);
                TerminateRejectedImpact();
            }
        }

        private static Hit BuildDamageHit(
            ProjectileEffectEmission emission,
            Damageable target)
        {
            if (emission == null
                || emission.Kind != ProjectileEffectEmissionKind.EnemyImpact
                || emission.Lifecycle == null
                || emission.Damage == null
                || target == null
                || target.DamageableStableId == null
                || target.DamageableLifecycleGeneration <= 0L)
            {
                throw new InvalidOperationException(
                    "canonical-projectile-direct-damage-emission-invalid");
            }

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

        private static ProjectileEffectEmission FindSingleDirectDamageEmission(
            ProjectileEmissionResult result,
            GunTargetReference expectedTarget)
        {
            if (result == null || expectedTarget == null) return null;
            ProjectileEffectEmission selected = null;
            for (int index = 0; index < result.Emissions.Count; index++)
            {
                ProjectileEffectEmission emission = result.Emissions[index];
                if (emission.Kind == ProjectileEffectEmissionKind.EnemyImpact)
                {
                    if (selected != null
                        || emission.Target == null
                        || !emission.Target.Equals(expectedTarget)
                        || emission.Damage == null
                        || emission.Damage.DirectDamage <= 0d
                        || emission.Damage.HasAreaDamage
                        || emission.Damage.HasDamageOverTime)
                    {
                        return null;
                    }
                    selected = emission;
                }
                else if (emission.Kind != ProjectileEffectEmissionKind.Termination
                    || emission.ExplosionTriggerReasons
                        != GunExplosionTriggerReason.None)
                {
                    return null;
                }
            }
            return selected;
        }

        private void CompleteResolvedImpact()
        {
            impactCommitted = false;
            lastDiagnostic = string.Empty;
            if (state != null && state.IsActive)
            {
                if (body != null)
                {
                    body.position = ToUnity(state.Position);
                    body.simulated = true;
                }
                if (trigger != null) trigger.enabled = true;
                return;
            }

            Complete();
        }

        private void ResolveRangeExpiry()
        {
            try
            {
                ProjectileImpactDecision decision = impactResolver.Resolve(
                    state,
                    ProjectileContact.RangeExpiry(state.Position));
                ProjectileEmissionResult emissions = effectEmitter.Emit(decision);
                if (!decision.Handled
                    || decision.StateAfter == null
                    || !decision.StateAfter.IsTerminated
                    || ContainsUnsupportedEmission(
                        emissions,
                        ProjectileEffectEmissionKind.RangeExpiry))
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-range-resolution-invalid");
                }
                state = decision.StateAfter;
            }
            catch (Exception exception)
            {
                if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                Report(
                    "canonical-projectile-range-resolution-failed:"
                    + exception.Message);
                if (state != null && state.IsActive)
                {
                    state = state.Terminate(
                        ProjectileTerminationReason.RangeExpired);
                }
            }
            Complete();
        }

        private void ResolveBlockingWallImpact()
        {
            try
            {
                ProjectileImpactDecision pending = impactResolver.Resolve(
                    state,
                    ProjectileContact.Wall(BlockingWallId, state.Position));
                if (pending == null || !pending.RequiresWallImpactResolution)
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-wall-resolution-required");
                }
                ProjectileImpactDecision resolved =
                    impactResolver.ApplyWallResolution(
                        pending,
                        ProjectileWallImpactResolution.BlockingImpact(
                            GunExplosionTriggerReason.None));
                ProjectileEmissionResult emissions = effectEmitter.Emit(resolved);
                if (resolved == null
                    || resolved.StateAfter == null
                    || !resolved.StateAfter.IsTerminated
                    || ContainsUnsupportedEmission(
                        emissions,
                        ProjectileEffectEmissionKind.WallImpact))
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-wall-resolution-invalid");
                }
                state = resolved.StateAfter;
            }
            catch (Exception exception)
            {
                if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                Report(
                    "canonical-projectile-wall-resolution-failed:"
                    + exception.Message);
                if (state != null && state.IsActive)
                {
                    state = state.Terminate(
                        ProjectileTerminationReason.WallImpact);
                }
            }
            Complete();
        }

        private static bool ContainsUnsupportedEmission(
            ProjectileEmissionResult result,
            ProjectileEffectEmissionKind allowed)
        {
            if (result == null) return true;
            for (int index = 0; index < result.Emissions.Count; index++)
            {
                ProjectileEffectEmission emission = result.Emissions[index];
                if (emission.Kind != allowed
                    && emission.Kind != ProjectileEffectEmissionKind.Termination)
                {
                    return true;
                }
                if (emission.ExplosionTriggerReasons
                    != GunExplosionTriggerReason.None)
                {
                    return true;
                }
            }
            return false;
        }

        private void TerminateRejectedImpact()
        {
            impactCommitted = false;
            if (state != null && state.IsActive)
            {
                state = state.Terminate(
                    ProjectileTerminationReason.EnemyImpact);
            }
            Complete();
        }

        private void StopTravel()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            if (trigger != null) trigger.enabled = false;
        }

        private void Complete()
        {
            if (completed) return;
            completed = true;
            impactCommitted = false;
            rangeExpiryPending = false;
            impactedTargets.Clear();
            StopTravel();
            Action<Bullet> callback =
                finishedCallback;
            finishedCallback = null;
            if (callback != null) callback(this);
            Destroy(gameObject);
        }

        private bool IsSourceCollider(Collider2D other)
        {
            if (sourceOwner == null || other == null) return false;
            Transform colliderTransform = other.transform;
            return colliderTransform == sourceOwner
                || colliderTransform.IsChildOf(sourceOwner);
        }

        private void Report(string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic)
                || string.Equals(
                    diagnostic,
                    lastDiagnostic,
                    StringComparison.Ordinal))
            {
                return;
            }
            lastDiagnostic = diagnostic;
            Debug.LogError(diagnostic, this);
        }

        private static Vector2 ToUnity(GunVector2 value)
        {
            return new Vector2((float)value.X, (float)value.Y);
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

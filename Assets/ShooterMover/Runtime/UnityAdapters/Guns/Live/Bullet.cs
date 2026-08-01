using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.Domain.Guns.Guidance;
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
        private readonly Homing homing = new Homing();
        private readonly GunImpact impacts = new GunImpact();
        private readonly HashSet<string> impactedTargets =
            new HashSet<string>(StringComparer.Ordinal);

        private ProjectileLifecycleState state;
        private Transform sourceOwner;
        private GunTargets targets;
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
            GunTargets configuredTargets,
            Action<Bullet> onFinished)
        {
            if (configured
                || configuredEffect == null
                || configuredEffect.InitialState == null
                || bulletSprite == null
                || configuredSourceOwner == null
                || configuredTargets == null
                || onFinished == null)
            {
                return false;
            }

            state = configuredEffect.InitialState;
            sourceOwner = configuredSourceOwner;
            targets = configuredTargets;
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
            SetFacing(direction);
            CreateVisual(bulletSprite, state.Profile.Projectile.Kind);

            body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.simulated = false;
            trigger = gameObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = ColliderRadius(state.Profile.Projectile.Kind);
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

            try
            {
                GunGuidanceDecision guidance = homing.Decide(
                    state.Profile.Guidance,
                    state.Guidance,
                    state.Position,
                    Time.fixedDeltaTime,
                    targets);
                state = state.WithGuidance(guidance.NextState);
                SetFacing(ToUnity(state.Direction));
            }
            catch (Exception exception)
            {
                if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                Report("player-bullet-guidance-failed:" + exception.Message);
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

            // MovePosition is simulated after FixedUpdate. Defer range expiry so the final
            // swept segment may still report an exact enemy or wall contact.
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

            Damageable target = other.GetComponentInParent<Damageable>();
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
                    new LifecycleGeneration(
                        target.DamageableLifecycleGeneration));
                ProjectileImpactDecision decision = impactResolver.Resolve(
                    state,
                    ProjectileContact.Enemy(
                        targetReference,
                        state.Position));
                ProjectileEmissionResult emissions =
                    effectEmitter.Emit(decision);
                if (!decision.Handled
                    || !decision.EnemyImpactApplied
                    || decision.StateAfter == null)
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-damageable-impact-invalid");
                }

                impactedTargets.Add(targetKey);
                impactCommitted = true;
                state = decision.StateAfter;
                StopTravel();
                impacts.ApplyEnemy(emissions, target, targets);
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
                ProjectileEmissionResult emissions =
                    effectEmitter.Emit(decision);
                if (!decision.Handled
                    || decision.StateAfter == null
                    || !decision.StateAfter.IsTerminated)
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-range-resolution-invalid");
                }
                impacts.ApplyEnd(
                    emissions,
                    ProjectileEffectEmissionKind.RangeExpiry,
                    targets);
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
                    ProjectileContact.Wall(
                        BlockingWallId,
                        state.Position));
                if (pending == null || !pending.RequiresWallImpactResolution)
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-wall-resolution-required");
                }
                ProjectileImpactDecision resolved =
                    impactResolver.ApplyWallResolution(
                        pending,
                        ProjectileWallImpactResolution.BlockingImpact(
                            WallExplosionReasons()));
                ProjectileEmissionResult emissions =
                    effectEmitter.Emit(resolved);
                if (resolved == null
                    || resolved.StateAfter == null
                    || !resolved.StateAfter.IsTerminated)
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-wall-resolution-invalid");
                }
                impacts.ApplyEnd(
                    emissions,
                    ProjectileEffectEmissionKind.WallImpact,
                    targets);
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

        private GunExplosionTriggerReason WallExplosionReasons()
        {
            GunExplosionTriggerSpec triggerSpec = state == null
                || state.Profile == null
                || state.Profile.Impact == null
                    ? null
                    : state.Profile.Impact.ExplosionTrigger;
            if (triggerSpec == null)
            {
                return GunExplosionTriggerReason.None;
            }

            GunExplosionTriggerReason reasons =
                GunExplosionTriggerReason.None;
            if (triggerSpec.OnWallImpact)
            {
                reasons |= GunExplosionTriggerReason.WallImpact;
            }
            if (triggerSpec.OnTermination)
            {
                reasons |= GunExplosionTriggerReason.Termination;
            }
            return reasons;
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
            Action<Bullet> callback = finishedCallback;
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

        private void CreateVisual(
            Sprite bulletSprite,
            GunProjectileKind kind)
        {
            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(transform, false);
            SpriteRenderer renderer =
                visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = bulletSprite;
            renderer.sortingOrder = 100;

            switch (kind)
            {
                case GunProjectileKind.Orb:
                    visualObject.transform.localScale =
                        new Vector3(0.38f, 0.38f, 1f);
                    renderer.color = new Color(0.3f, 0.85f, 1f, 1f);
                    break;

                case GunProjectileKind.Rocket:
                    visualObject.transform.localScale =
                        new Vector3(0.36f, 0.12f, 1f);
                    renderer.color = new Color(1f, 0.4f, 0.12f, 1f);
                    CreateExhaust(bulletSprite);
                    break;

                default:
                    visualObject.transform.localScale =
                        new Vector3(0.28f, 0.1f, 1f);
                    renderer.color = new Color(1f, 0.82f, 0.2f, 1f);
                    break;
            }
        }

        private void CreateExhaust(Sprite bulletSprite)
        {
            GameObject exhaustObject = new GameObject("Exhaust");
            exhaustObject.transform.SetParent(transform, false);
            exhaustObject.transform.localPosition =
                new Vector3(-0.23f, 0f, 0f);
            exhaustObject.transform.localScale =
                new Vector3(0.16f, 0.07f, 1f);
            SpriteRenderer exhaust =
                exhaustObject.AddComponent<SpriteRenderer>();
            exhaust.sprite = bulletSprite;
            exhaust.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            exhaust.sortingOrder = 99;
        }

        private static float ColliderRadius(GunProjectileKind kind)
        {
            switch (kind)
            {
                case GunProjectileKind.Orb:
                    return 0.2f;
                case GunProjectileKind.Rocket:
                    return 0.14f;
                default:
                    return 0.12f;
            }
        }

        private void SetFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.000001f) return;
            float angle = Mathf.Atan2(direction.y, direction.x)
                * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            if (body != null) body.rotation = angle;
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
    }
}

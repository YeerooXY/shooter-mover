using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Generic scene effect sink for canonical launch batches. The first production slice supports
    /// one unguided Normal projectile per scheduler emission and rejects every unsupported mechanic
    /// without reconstructing weapon data or fabricating a substitute launch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionCanonicalProjectileEffectSink2D :
        MonoBehaviour,
        IInventoryWeaponEffectBatchSink
    {
        private const int ReceiptCapacity =
            WeaponFiringScheduler.DefaultReplayRetentionCapacity;

        private readonly Dictionary<string, string> accepted =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Queue<string> acceptedOrder = new Queue<string>();
        private readonly HashSet<ProductionCanonicalNormalProjectile2D> active =
            new HashSet<ProductionCanonicalNormalProjectile2D>();
        private Texture2D texture;
        private Sprite sprite;
        private bool retired;

        public int AcceptedBatchCount { get { return accepted.Count; } }
        public int ActiveProjectileCount { get { return active.Count; } }
        public bool IsRetired { get { return retired; } }

        public WeaponEffectBatchSinkResult TryAccept(
            InventoryWeaponEffectBatch batch)
        {
            if (retired)
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "canonical-projectile-sink-retired");
            }
            if (batch == null
                || batch.CoreBatch == null
                || batch.Identity == null
                || batch.CoreBatch.EffectCount != 1
                || batch.CoreBatch.Effects.Count != 1)
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "canonical-projectile-batch-single-effect-required");
            }

            string key = OperationKey(batch.Identity);
            string retainedFingerprint;
            if (accepted.TryGetValue(key, out retainedFingerprint))
            {
                return string.Equals(
                        retainedFingerprint,
                        batch.Fingerprint,
                        StringComparison.Ordinal)
                    ? WeaponEffectBatchSinkResult.AlreadyAccepted()
                    : WeaponEffectBatchSinkResult.Reject(
                        "canonical-projectile-batch-conflicting-duplicate");
            }

            CanonicalProjectileLaunchEffect effect =
                batch.CoreBatch.Effects[0] as CanonicalProjectileLaunchEffect;
            string rejectionCode;
            if (!IsSupported(effect, out rejectionCode))
            {
                return WeaponEffectBatchSinkResult.Reject(rejectionCode);
            }

            EnsureSprite();
            GameObject projectileObject = null;
            try
            {
                projectileObject = new GameObject(
                    "CanonicalPlayerProjectile_"
                    + effect.Identity.ShotSequence.ToString(
                        CultureInfo.InvariantCulture)
                    + "_" + effect.Identity.ProjectileOrdinal.Value.ToString(
                        CultureInfo.InvariantCulture));
                SceneManager.MoveGameObjectToScene(projectileObject, gameObject.scene);
                projectileObject.SetActive(false);
                ProductionCanonicalNormalProjectile2D projectile =
                    projectileObject.AddComponent<
                        ProductionCanonicalNormalProjectile2D>();
                if (!projectile.TryConfigure(
                        effect,
                        sprite,
                        transform,
                        HandleProjectileCompleted))
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-configuration-rejected");
                }
                active.Add(projectile);
                projectileObject.SetActive(true);
                if (!projectile.BeginEmission())
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-emission-rejected");
                }

                RetainAccepted(key, batch.Fingerprint);
                return WeaponEffectBatchSinkResult.Accept();
            }
            catch (Exception exception)
            {
                if (projectileObject != null)
                {
                    ProductionCanonicalNormalProjectile2D projectile =
                        projectileObject.GetComponent<
                            ProductionCanonicalNormalProjectile2D>();
                    if (projectile != null) active.Remove(projectile);
                    projectileObject.SetActive(false);
                    Destroy(projectileObject);
                }
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                Debug.LogError(
                    "canonical-projectile-batch-staging-failed:"
                    + exception.Message,
                    this);
                return WeaponEffectBatchSinkResult.Reject(
                    "canonical-projectile-batch-staging-failed");
            }
        }

        public void RetireOwnerPresentation()
        {
            if (retired) return;
            retired = true;
            var snapshot = new List<ProductionCanonicalNormalProjectile2D>(active);
            for (int index = 0; index < snapshot.Count; index++)
            {
                if (snapshot[index] != null) snapshot[index].RetireOwner();
            }
        }

        private void RetainAccepted(string key, string fingerprint)
        {
            accepted.Add(key, fingerprint);
            acceptedOrder.Enqueue(key);
            while (acceptedOrder.Count > ReceiptCapacity)
            {
                accepted.Remove(acceptedOrder.Dequeue());
            }
        }

        private void HandleProjectileCompleted(
            ProductionCanonicalNormalProjectile2D projectile)
        {
            if (projectile != null) active.Remove(projectile);
        }

        private static bool IsSupported(
            CanonicalProjectileLaunchEffect effect,
            out string rejectionCode)
        {
            if (effect == null
                || effect.Profile == null
                || effect.InitialState == null
                || !effect.Profile.IsCanonical)
            {
                rejectionCode = "canonical-projectile-launch-required";
                return false;
            }
            if (effect.Profile.CanonicalDeliveryType != WeaponDeliveryType.Normal
                || effect.Profile.Projectile == null
                || effect.Profile.Projectile.Kind
                    != WeaponProjectileKind.RegularProjectile
                || effect.Profile.Projectile.TerminationBehavior
                    != WeaponProjectileTerminationBehavior.StopWhenPierceIsSpent)
            {
                rejectionCode = "canonical-projectile-normal-delivery-required";
                return false;
            }
            if (effect.Profile.Guidance == null
                || effect.Profile.Guidance.Mode != WeaponGuidanceMode.Unguided
                || effect.Profile.Impact == null
                || !effect.Profile.Impact.HandlesEnemyImpact
                || !effect.Profile.Impact.HandlesWallImpact
                || !effect.Profile.Impact.HandlesRangeExpiry
                || !effect.Profile.Impact.HandlesTermination
                || effect.Profile.Impact.Ricochet != null
                || effect.Profile.Ricochet.Tenths != 0
                || effect.Profile.Impact.ExplosionTrigger != null)
            {
                rejectionCode = "canonical-projectile-impact-policy-unsupported";
                return false;
            }
            if (effect.Profile.Damage == null
                || effect.Profile.Damage.DirectDamage <= 0d
                || effect.Profile.Damage.HasAreaDamage
                || effect.Profile.Damage.HasDamageOverTime
                || effect.Profile.Effects == null
                || effect.Profile.Effects.Explosion != null
                || effect.Profile.Effects.DamageOverTime != null
                || effect.Profile.Effects.ChainArc != null)
            {
                rejectionCode = "canonical-projectile-effect-profile-unsupported";
                return false;
            }
            if (!effect.InitialState.IsActive
                || effect.InitialState.Speed <= 0d
                || effect.InitialState.RemainingRange <= 0d
                || effect.InitialState.Direction == null
                || effect.InitialState.Direction.LengthSquared <= 0d
                || !ReferenceEquals(
                    effect.InitialState.Profile,
                    effect.Profile)
                || effect.InitialState.Pierce == null
                || effect.InitialState.Pierce.AuthoredValue != effect.Profile.Pierce)
            {
                rejectionCode = "canonical-projectile-baked-state-invalid";
                return false;
            }

            rejectionCode = string.Empty;
            return true;
        }

        private void EnsureSprite()
        {
            if (sprite != null) return;
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "Canonical Projectile Pixel";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            sprite.name = "Canonical Projectile Sprite";
        }

        private static string OperationKey(WeaponEffectIdentity identity)
        {
            return identity.ActorId
                + "|" + identity.LifecycleGeneration
                + "|" + identity.FireOperationId;
        }

        private void OnDisable()
        {
            RetireOwnerPresentation();
        }

        private void OnDestroy()
        {
            RetireOwnerPresentation();
            accepted.Clear();
            acceptedOrder.Clear();
            active.Clear();
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ProductionCanonicalNormalProjectile2D : MonoBehaviour
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

        private CanonicalProjectileLaunchEffect effect;
        private ProjectileLifecycleState state;
        private Transform sourceOwner;
        private Rigidbody2D body;
        private CircleCollider2D trigger;
        private Action<ProductionCanonicalNormalProjectile2D> completedCallback;
        private RoomEnemyActor2D pendingEnemy;
        private EnemyRuntimeDamageCommandV1 pendingCommand;
        private double pendingOccurredAtSeconds;
        private bool configured;
        private bool launched;
        private bool completed;
        private bool impactCommitted;
        private bool ownerRetired;
        private string lastDiagnostic = string.Empty;

        public bool HasPendingEnemyImpactRetry
        {
            get
            {
                return impactCommitted
                    && pendingEnemy != null
                    && pendingCommand != null;
            }
        }

        public bool TryConfigure(
            CanonicalProjectileLaunchEffect configuredEffect,
            Sprite projectileSprite,
            Transform configuredSourceOwner,
            Action<ProductionCanonicalNormalProjectile2D> onCompleted)
        {
            if (configured
                || configuredEffect == null
                || configuredEffect.InitialState == null
                || projectileSprite == null
                || configuredSourceOwner == null
                || onCompleted == null)
            {
                return false;
            }

            effect = configuredEffect;
            state = configuredEffect.InitialState;
            sourceOwner = configuredSourceOwner;
            completedCallback = onCompleted;
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
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = projectileSprite;
            renderer.color = new Color(1f, 0.82f, 0.2f, 1f);
            renderer.sortingOrder = 100;
            renderer.transform.localScale = new Vector3(0.28f, 0.1f, 1f);

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

        public bool BeginEmission()
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

        public void RetireOwner()
        {
            if (completed || ownerRetired) return;
            ownerRetired = true;
            if (HasPendingEnemyImpactRetry)
            {
                StopTravel();
                if (state != null && state.IsActive)
                {
                    state = state.Terminate(
                        ProjectileTerminationReason.EnemyImpact);
                }
                return;
            }
            Complete();
        }

        private void FixedUpdate()
        {
            if (!configured || !launched || completed || state == null) return;
            if (pendingCommand != null)
            {
                TryResolvePendingImpact();
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
            WeaponVector2 next = new WeaponVector2(
                state.Position.X + (state.Direction.X * distance),
                state.Position.Y + (state.Direction.Y * distance));
            state = state.WithKinematics(
                next,
                state.DistanceTravelled + distance);
            body.MovePosition(ToUnity(state.Position));
            if (state.RemainingRange <= 0.0000001d) ResolveRangeExpiry();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (completed
                || impactCommitted
                || other == null
                || state == null
                || !state.IsActive
                || IsSourceCollider(other))
            {
                return;
            }

            RoomEnemyActor2D enemy = other.GetComponentInParent<RoomEnemyActor2D>();
            if (enemy != null)
            {
                TryBeginEnemyImpact(enemy);
            }
            else if (!other.isTrigger)
            {
                ResolveBlockingWallImpact();
            }
        }

        private void TryBeginEnemyImpact(RoomEnemyActor2D enemy)
        {
            if (enemy == null || !enemy.IsBound || !enemy.IsAlive) return;
            string targetKey = enemy.ActorStableId
                + "|" + enemy.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture);
            if (impactedTargets.Contains(targetKey)) return;

            try
            {
                var target = new WeaponTargetReference(
                    new WeaponActorInstanceId(enemy.ActorStableId),
                    new LifecycleGeneration(enemy.LifecycleGeneration));
                ProjectileImpactDecision decision = impactResolver.Resolve(
                    state,
                    ProjectileContact.Enemy(target, state.Position));
                ProjectileEffectEmission emission = FindSingleEnemyDamageEmission(
                    effectEmitter.Emit(decision),
                    target);
                if (!decision.Handled
                    || !decision.EnemyImpactApplied
                    || decision.StateAfter == null
                    || emission == null)
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-enemy-impact-invalid");
                }

                impactedTargets.Add(targetKey);
                impactCommitted = true;
                state = decision.StateAfter;
                pendingEnemy = enemy;
                pendingCommand = BuildDamageCommand(emission, enemy);
                pendingOccurredAtSeconds = Time.fixedTimeAsDouble;
                StopTravel();
                TryResolvePendingImpact();
            }
            catch (Exception exception)
            {
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                Report("canonical-projectile-enemy-impact-failed:"
                    + exception.Message);
                TerminateRejectedImpact();
            }
        }

        private EnemyRuntimeDamageCommandV1 BuildDamageCommand(
            ProjectileEffectEmission emission,
            RoomEnemyActor2D enemy)
        {
            WeaponEffectIdentity identity =
                emission.Lifecycle.Identity.SourceIdentity;
            StableId operationId = StableId.Create(
                "enemy-damage-operation",
                "canonical-player-projectile-"
                + Hash64(
                    emission.ToCanonicalString()
                    + "|" + enemy.ActorStableId
                    + "|" + enemy.LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture)));
            long order = checked(
                emission.Lifecycle.LaunchSimulationTick * 4096L
                + emission.EventOrdinal);
            return new EnemyRuntimeDamageCommandV1(
                operationId,
                identity.ActorId.Value,
                identity.ParticipantId.Value,
                enemy.ActorStableId,
                enemy.LifecycleGeneration,
                order,
                (int)emission.Damage.Category,
                emission.Damage.DirectDamage);
        }

        private static ProjectileEffectEmission FindSingleEnemyDamageEmission(
            ProjectileEmissionResult result,
            WeaponTargetReference expectedTarget)
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
                        != WeaponExplosionTriggerReason.None)
                {
                    return null;
                }
            }
            return selected;
        }

        private void TryResolvePendingImpact()
        {
            if (completed || pendingCommand == null) return;
            if (pendingEnemy == null
                || !pendingEnemy.IsBound
                || pendingEnemy.ActorStableId
                    != pendingCommand.TargetEntityStableId
                || pendingEnemy.LifecycleGeneration
                    != pendingCommand.TargetLifecycleGeneration)
            {
                Report("canonical-projectile-enemy-target-stale");
                TerminateRejectedImpact();
                return;
            }

            EnemyRuntimeDamageResultV1 result;
            try
            {
                result = pendingEnemy.ApplyDamage(
                    pendingCommand,
                    pendingOccurredAtSeconds);
            }
            catch (Exception exception)
            {
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                Report("canonical-projectile-enemy-damage-retryable:"
                    + exception.Message);
                return;
            }
            if (result == null)
            {
                Report("canonical-projectile-enemy-damage-null-retryable");
                return;
            }
            if (result.Status == EnemyRuntimeOperationStatusV1.Applied
                || result.Status == EnemyRuntimeOperationStatusV1.ExactReplay)
            {
                CompleteAcceptedImpact();
                return;
            }
            if (result.Status == EnemyRuntimeOperationStatusV1.Rejected
                && result.Rejection
                    == EnemyRuntimeRejectionCodeV1.InvalidCommand
                && result.DeathFact != null)
            {
                Report("canonical-projectile-terminal-transition-retryable");
                return;
            }
            Report("canonical-projectile-enemy-damage-rejected:"
                + result.Status + ":" + result.Rejection);
            TerminateRejectedImpact();
        }

        private void CompleteAcceptedImpact()
        {
            ClearPending();
            if (state != null && state.IsActive && !ownerRetired)
            {
                impactCommitted = false;
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
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                Report("canonical-projectile-range-resolution-failed:"
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
                if (!pending.RequiresWallImpactResolution)
                {
                    throw new InvalidOperationException(
                        "canonical-projectile-wall-resolution-required");
                }
                ProjectileImpactDecision resolved =
                    impactResolver.ApplyWallResolution(
                        pending,
                        ProjectileWallImpactResolution.BlockingImpact(
                            WeaponExplosionTriggerReason.None));
                ProjectileEmissionResult emissions = effectEmitter.Emit(resolved);
                if (!resolved.StateAfter.IsTerminated
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
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                Report("canonical-projectile-wall-resolution-failed:"
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
                    != WeaponExplosionTriggerReason.None)
                {
                    return true;
                }
            }
            return false;
        }

        private void TerminateRejectedImpact()
        {
            ClearPending();
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

        private void ClearPending()
        {
            pendingEnemy = null;
            pendingCommand = null;
            pendingOccurredAtSeconds = 0d;
        }

        private void Complete()
        {
            if (completed) return;
            completed = true;
            StopTravel();
            Action<ProductionCanonicalNormalProjectile2D> callback =
                completedCallback;
            completedCallback = null;
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

        private static Vector2 ToUnity(WeaponVector2 value)
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

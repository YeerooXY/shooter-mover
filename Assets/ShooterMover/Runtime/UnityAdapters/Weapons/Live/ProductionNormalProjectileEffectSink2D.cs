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
    /// Scene-owned, fail-closed presentation sink for the first combat-room vertical slice.
    /// It accepts exactly one baked canonical Normal projectile launch and never reconstructs
    /// weapon cadence, damage, range, speed, Pierce, or identities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionNormalProjectileEffectSink2D :
        MonoBehaviour,
        IInventoryWeaponEffectBatchSink
    {
        private sealed class AcceptedBatch
        {
            public AcceptedBatch(string fingerprint)
            {
                Fingerprint = fingerprint;
            }

            public string Fingerprint { get; }
        }

        // This task supports one projectile effect per accepted scheduler emission. Retaining the
        // scheduler's full default replay window keeps exact replay idempotent while remaining
        // strictly bounded during sustained automatic fire.
        private const int MaxAcceptedBatchReceipts =
            WeaponFiringScheduler.DefaultReplayRetentionCapacity;

        private readonly Dictionary<string, AcceptedBatch> accepted =
            new Dictionary<string, AcceptedBatch>(StringComparer.Ordinal);
        private readonly Queue<string> acceptedOrder = new Queue<string>();
        private Texture2D runtimeTexture;
        private Sprite runtimeSprite;

        public int AcceptedBatchCount { get { return accepted.Count; } }
        public int AcceptedBatchCapacity { get { return MaxAcceptedBatchReceipts; } }

        private void Awake()
        {
            EnsureRuntimeSprite();
        }

        public WeaponEffectBatchSinkResult TryAccept(
            InventoryWeaponEffectBatch batch)
        {
            if (batch == null
                || batch.CoreBatch == null
                || batch.Identity == null
                || batch.CoreBatch.EffectCount < 1)
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "player-weapon-projectile-batch-invalid");
            }
            if (batch.CoreBatch.EffectCount != 1
                || batch.CoreBatch.Effects.Count != 1)
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "player-weapon-projectile-multi-projectile-unsupported");
            }

            string operationKey = OperationKey(batch.Identity);
            AcceptedBatch existing;
            if (accepted.TryGetValue(operationKey, out existing))
            {
                return string.Equals(
                        existing.Fingerprint,
                        batch.Fingerprint,
                        StringComparison.Ordinal)
                    ? WeaponEffectBatchSinkResult.AlreadyAccepted()
                    : WeaponEffectBatchSinkResult.Reject(
                        "player-weapon-projectile-conflicting-duplicate");
            }

            CanonicalProjectileLaunchEffect effect =
                batch.CoreBatch.Effects[0]
                    as CanonicalProjectileLaunchEffect;
            string rejectionCode;
            if (!IsSupported(effect, out rejectionCode))
            {
                return WeaponEffectBatchSinkResult.Reject(rejectionCode);
            }

            EnsureRuntimeSprite();
            GameObject projectileObject = null;
            try
            {
                projectileObject = new GameObject(
                    "PlayerWeaponProjectile_"
                    + effect.Identity.ShotSequence.ToString(
                        CultureInfo.InvariantCulture)
                    + "_"
                    + effect.Identity.ProjectileOrdinal.Value.ToString(
                        CultureInfo.InvariantCulture));
                SceneManager.MoveGameObjectToScene(
                    projectileObject,
                    gameObject.scene);
                projectileObject.SetActive(false);

                ProductionNormalProjectile2D projectile =
                    projectileObject.AddComponent<
                        ProductionNormalProjectile2D>();
                if (!projectile.TryConfigure(effect, runtimeSprite, transform))
                {
                    throw new InvalidOperationException(
                        "player-weapon-projectile-configuration-rejected");
                }

                projectileObject.SetActive(true);
                if (!projectile.BeginEmission())
                {
                    throw new InvalidOperationException(
                        "player-weapon-projectile-launch-rejected");
                }

                RetainAccepted(operationKey, batch.Fingerprint);
                return WeaponEffectBatchSinkResult.Accept();
            }
            catch (Exception exception)
            {
                if (projectileObject != null)
                {
                    projectileObject.SetActive(false);
                    Destroy(projectileObject);
                }
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                Debug.LogError(
                    "player-weapon-projectile-batch-rejected:"
                    + exception.Message,
                    this);
                return WeaponEffectBatchSinkResult.Reject(
                    "player-weapon-projectile-batch-staging-failed");
            }
        }

        private void RetainAccepted(string operationKey, string fingerprint)
        {
            accepted.Add(operationKey, new AcceptedBatch(fingerprint));
            acceptedOrder.Enqueue(operationKey);
            while (acceptedOrder.Count > MaxAcceptedBatchReceipts)
            {
                string expired = acceptedOrder.Dequeue();
                accepted.Remove(expired);
            }
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
                rejectionCode =
                    "player-weapon-projectile-canonical-launch-required";
                return false;
            }
            if (effect.Profile.CanonicalDeliveryType
                    != WeaponDeliveryType.Normal
                || effect.Profile.Projectile == null
                || effect.Profile.Projectile.Kind
                    != WeaponProjectileKind.RegularProjectile)
            {
                rejectionCode =
                    "player-weapon-projectile-delivery-unsupported:"
                    + (effect.Profile.CanonicalDeliveryType.HasValue
                        ? effect.Profile.CanonicalDeliveryType.Value.ToString()
                        : "none");
                return false;
            }
            if (effect.Profile.Projectile.TerminationBehavior
                    != WeaponProjectileTerminationBehavior.StopWhenPierceIsSpent)
            {
                rejectionCode =
                    "player-weapon-projectile-termination-policy-unsupported";
                return false;
            }
            if (effect.Profile.Guidance == null
                || effect.Profile.Guidance.Mode
                    != WeaponGuidanceMode.Unguided)
            {
                rejectionCode =
                    "player-weapon-projectile-guidance-unsupported";
                return false;
            }
            if (effect.Profile.Impact == null
                || !effect.Profile.Impact.HandlesEnemyImpact
                || !effect.Profile.Impact.HandlesWallImpact
                || !effect.Profile.Impact.HandlesRangeExpiry
                || !effect.Profile.Impact.HandlesTermination
                || effect.Profile.Impact.Ricochet != null
                || effect.Profile.Ricochet.Tenths != 0
                || effect.Profile.Impact.ExplosionTrigger != null)
            {
                rejectionCode =
                    "player-weapon-projectile-impact-policy-unsupported";
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
                rejectionCode =
                    "player-weapon-projectile-effect-profile-unsupported";
                return false;
            }
            if (!effect.InitialState.IsActive
                || effect.InitialState.Speed <= 0d
                || effect.InitialState.RemainingRange <= 0d
                || effect.InitialState.Direction == null
                || effect.InitialState.Direction.LengthSquared <= 0d
                || effect.InitialState.Profile == null
                || !ReferenceEquals(
                    effect.InitialState.Profile,
                    effect.Profile)
                || effect.InitialState.Pierce == null
                || effect.InitialState.Pierce.AuthoredValue
                    != effect.Profile.Pierce)
            {
                rejectionCode =
                    "player-weapon-projectile-baked-state-invalid";
                return false;
            }

            rejectionCode = string.Empty;
            return true;
        }

        private void EnsureRuntimeSprite()
        {
            if (runtimeSprite != null) return;
            runtimeTexture = new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);
            runtimeTexture.name = "Player Weapon Projectile Pixel";
            runtimeTexture.SetPixel(0, 0, Color.white);
            runtimeTexture.Apply(false, true);
            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            runtimeSprite.name = "Player Weapon Projectile Sprite";
        }

        private static string OperationKey(WeaponEffectIdentity identity)
        {
            return identity.ActorId
                + "|" + identity.LifecycleGeneration
                + "|" + identity.FireOperationId;
        }

        private void OnDestroy()
        {
            accepted.Clear();
            acceptedOrder.Clear();
            if (runtimeSprite != null) Destroy(runtimeSprite);
            if (runtimeTexture != null) Destroy(runtimeTexture);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ProductionNormalProjectile2D : MonoBehaviour
    {
        private readonly ProjectileImpactResolver impactResolver =
            new ProjectileImpactResolver(
                new SharedDeterministicRandomFractionalPierceRoller());
        private readonly ProjectileEffectEmitter effectEmitter =
            new ProjectileEffectEmitter();
        private readonly HashSet<string> impactedTargets =
            new HashSet<string>(StringComparer.Ordinal);

        private CanonicalProjectileLaunchEffect effect;
        private Transform sourceOwner;
        private ProjectileLifecycleState state;
        private Rigidbody2D body;
        private CircleCollider2D trigger;
        private RoomEnemyActor2D pendingImpactEnemy;
        private EnemyRuntimeDamageCommandV1 pendingDamageCommand;
        private double pendingOccurredAtSeconds;
        private bool configured;
        private bool launched;
        private bool completed;
        private bool impactCommitted;
        private string lastImpactDiagnostic = string.Empty;

        public bool TryConfigure(
            CanonicalProjectileLaunchEffect configuredEffect,
            Sprite projectileSprite,
            Transform configuredSourceOwner)
        {
            if (configured
                || configuredEffect == null
                || configuredEffect.InitialState == null
                || projectileSprite == null
                || configuredSourceOwner == null)
            {
                return false;
            }

            effect = configuredEffect;
            sourceOwner = configuredSourceOwner;
            state = configuredEffect.InitialState;
            Vector2 position = ToUnity(state.Position);
            Vector2 direction = ToUnity(state.Direction);
            if (!state.IsActive
                || direction.sqrMagnitude < 0.000001f
                || state.Speed <= 0d
                || state.RemainingRange <= 0d)
            {
                return false;
            }

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
            if (!gameObject.activeInHierarchy
                || body == null
                || trigger == null)
            {
                return false;
            }

            launched = true;
            body.position = ToUnity(state.Position);
            body.simulated = true;
            return true;
        }

        private void FixedUpdate()
        {
            if (!configured
                || !launched
                || completed
                || state == null)
            {
                return;
            }

            if (pendingDamageCommand != null)
            {
                TryResolvePendingImpact();
                return;
            }
            if (!state.IsActive)
            {
                Complete();
                return;
            }

            double requestedDistance = state.Speed * Time.fixedDeltaTime;
            double travelDistance = Math.Min(
                requestedDistance,
                state.RemainingRange);
            WeaponVector2 direction = state.Direction;
            WeaponVector2 nextPosition = new WeaponVector2(
                state.Position.X + (direction.X * travelDistance),
                state.Position.Y + (direction.Y * travelDistance));
            double nextDistance = state.DistanceTravelled + travelDistance;
            state = state.WithKinematics(nextPosition, nextDistance);
            body.MovePosition(ToUnity(state.Position));

            if (state.RemainingRange <= 0.0000001d)
            {
                ResolveRangeExpiry();
            }
        }

        private void ResolveRangeExpiry()
        {
            try
            {
                ProjectileContact contact =
                    ProjectileContact.RangeExpiry(state.Position);
                ProjectileImpactDecision decision =
                    impactResolver.Resolve(state, contact);
                ProjectileEmissionResult emissions =
                    effectEmitter.Emit(decision);
                if (!decision.Handled
                    || !decision.StateAfter.IsTerminated
                    || ContainsUnsupportedEmission(
                        emissions,
                        ProjectileEffectEmissionKind.RangeExpiry))
                {
                    throw new InvalidOperationException(
                        "player-weapon-projectile-range-resolution-invalid");
                }
                state = decision.StateAfter;
            }
            catch (Exception exception)
            {
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                ReportImpactDiagnostic(
                    "player-weapon-projectile-range-resolution-failed:"
                    + exception.Message);
                if (state != null && state.IsActive)
                {
                    state = state.Terminate(
                        ProjectileTerminationReason.RangeExpired);
                }
            }
            Complete();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (completed
                || impactCommitted
                || other == null
                || effect == null
                || state == null
                || !state.IsActive)
            {
                return;
            }

            if (IsSourceCollider(other))
            {
                return;
            }

            RoomEnemyActor2D enemy =
                other.GetComponentInParent<RoomEnemyActor2D>();
            if (enemy != null)
            {
                TryBeginEnemyImpact(enemy);
                return;
            }

            if (!other.isTrigger)
            {
                ResolveBlockingWallImpact(other);
            }
        }

        private void TryBeginEnemyImpact(RoomEnemyActor2D enemy)
        {
            if (enemy == null || !enemy.IsBound || !enemy.IsAlive)
            {
                return;
            }

            string targetKey = TargetKey(
                enemy.ActorStableId,
                enemy.LifecycleGeneration);
            if (impactedTargets.Contains(targetKey))
            {
                return;
            }

            try
            {
                WeaponTargetReference target =
                    new WeaponTargetReference(
                        new WeaponActorInstanceId(
                            enemy.ActorStableId),
                        new LifecycleGeneration(
                            enemy.LifecycleGeneration));
                ProjectileContact contact =
                    ProjectileContact.Enemy(
                        target,
                        CurrentPosition());
                ProjectileImpactDecision decision =
                    impactResolver.Resolve(state, contact);
                ProjectileEmissionResult emissions =
                    effectEmitter.Emit(decision);
                ProjectileEffectEmission damageEmission =
                    FindSingleEnemyDamageEmission(
                        emissions,
                        target);
                if (!decision.Handled
                    || !decision.EnemyImpactApplied
                    || damageEmission == null
                    || decision.StateAfter == null)
                {
                    throw new InvalidOperationException(
                        "player-weapon-enemy-impact-emission-invalid");
                }

                EnemyRuntimeDamageCommandV1 command =
                    BuildDamageCommand(
                        damageEmission,
                        enemy);
                double occurredAtSeconds =
                    Time.fixedTimeAsDouble;

                impactedTargets.Add(targetKey);
                impactCommitted = true;
                state = decision.StateAfter;
                pendingImpactEnemy = enemy;
                pendingDamageCommand = command;
                pendingOccurredAtSeconds = occurredAtSeconds;
                StopPhysicalTravelForImpact();
                TryResolvePendingImpact();
            }
            catch (Exception exception)
            {
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                ReportImpactDiagnostic(
                    "player-weapon-enemy-impact-resolution-invalid:"
                    + exception.Message);
                TerminateAfterRejectedImpact();
            }
        }

        private EnemyRuntimeDamageCommandV1 BuildDamageCommand(
            ProjectileEffectEmission emission,
            RoomEnemyActor2D enemy)
        {
            if (emission == null
                || emission.Kind
                    != ProjectileEffectEmissionKind.EnemyImpact
                || emission.Profile == null
                || !ReferenceEquals(
                    emission.Profile,
                    effect.Profile)
                || emission.Target == null
                || enemy == null)
            {
                throw new InvalidOperationException(
                    "player-weapon-enemy-damage-emission-invalid");
            }

            WeaponEffectIdentity identity =
                emission.Lifecycle.Identity.SourceIdentity;
            StableId operationId = StableId.Create(
                "enemy-damage-operation",
                "player-projectile-"
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
            if (result == null || expectedTarget == null)
            {
                return null;
            }

            ProjectileEffectEmission selected = null;
            for (int index = 0; index < result.Emissions.Count; index++)
            {
                ProjectileEffectEmission emission =
                    result.Emissions[index];
                if (emission.Kind
                    == ProjectileEffectEmissionKind.EnemyImpact)
                {
                    if (selected != null
                        || emission.Target == null
                        || !emission.Target.Equals(expectedTarget)
                        || emission.Profile == null
                        || emission.Damage == null
                        || emission.Damage.DirectDamage <= 0d
                        || emission.Damage.HasAreaDamage
                        || emission.Damage.HasDamageOverTime)
                    {
                        return null;
                    }
                    selected = emission;
                    continue;
                }

                if (emission.Kind
                        != ProjectileEffectEmissionKind.Termination
                    || emission.ExplosionTriggerReasons
                        != WeaponExplosionTriggerReason.None)
                {
                    return null;
                }
            }
            return selected;
        }

        private void ResolveBlockingWallImpact(Collider2D other)
        {
            try
            {
                ProjectileContact contact =
                    ProjectileContact.Wall(
                        StableId.Create(
                            "player-projectile-wall",
                            Hash64(ColliderIdentity(other))),
                        CurrentPosition());
                ProjectileImpactDecision pending =
                    impactResolver.Resolve(state, contact);
                if (!pending.RequiresWallImpactResolution)
                {
                    if (!pending.Handled) return;
                    throw new InvalidOperationException(
                        "player-weapon-projectile-wall-pending-required");
                }

                ProjectileImpactDecision resolved =
                    impactResolver.ApplyWallResolution(
                        pending,
                        ProjectileWallImpactResolution.BlockingImpact(
                            WeaponExplosionTriggerReason.None));
                ProjectileEmissionResult emissions =
                    effectEmitter.Emit(resolved);
                if (!resolved.StateAfter.IsTerminated
                    || ContainsUnsupportedEmission(
                        emissions,
                        ProjectileEffectEmissionKind.WallImpact))
                {
                    throw new InvalidOperationException(
                        "player-weapon-projectile-wall-resolution-invalid");
                }
                state = resolved.StateAfter;
            }
            catch (Exception exception)
            {
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                ReportImpactDiagnostic(
                    "player-weapon-projectile-wall-resolution-failed:"
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
            ProjectileEffectEmissionKind allowedContactKind)
        {
            if (result == null) return true;
            for (int index = 0; index < result.Emissions.Count; index++)
            {
                ProjectileEffectEmission emission =
                    result.Emissions[index];
                if (emission.Kind != allowedContactKind
                    && emission.Kind
                        != ProjectileEffectEmissionKind.Termination)
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

        private void TryResolvePendingImpact()
        {
            if (completed || pendingDamageCommand == null) return;
            if (pendingImpactEnemy == null
                || !pendingImpactEnemy.IsBound
                || pendingImpactEnemy.ActorStableId
                    != pendingDamageCommand.TargetEntityStableId
                || pendingImpactEnemy.LifecycleGeneration
                    != pendingDamageCommand.TargetLifecycleGeneration)
            {
                ReportImpactDiagnostic(
                    "player-weapon-enemy-damage-target-no-longer-valid");
                TerminateAfterRejectedImpact();
                return;
            }

            EnemyRuntimeDamageResultV1 result;
            try
            {
                result = pendingImpactEnemy.ApplyDamage(
                    pendingDamageCommand,
                    pendingOccurredAtSeconds);
            }
            catch (Exception exception)
            {
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                ReportImpactDiagnostic(
                    "player-weapon-enemy-damage-retryable-exception:"
                    + exception.Message);
                return;
            }

            if (result == null)
            {
                ReportImpactDiagnostic(
                    "player-weapon-enemy-damage-retryable-null-result");
                return;
            }
            if (result.Status == EnemyRuntimeOperationStatusV1.Applied
                || result.Status == EnemyRuntimeOperationStatusV1.ExactReplay)
            {
                CompleteAcceptedImpact();
                return;
            }
            if (IsRetryableTerminalTransition(result))
            {
                ReportImpactDiagnostic(
                    "player-weapon-enemy-terminal-transition-retryable:"
                    + result.Rejection);
                return;
            }

            ReportImpactDiagnostic(
                "player-weapon-enemy-damage-permanently-rejected:"
                + result.Status + ":" + result.Rejection);
            TerminateAfterRejectedImpact();
        }

        private static bool IsRetryableTerminalTransition(
            EnemyRuntimeDamageResultV1 result)
        {
            return result != null
                && result.Status == EnemyRuntimeOperationStatusV1.Rejected
                && result.Rejection == EnemyRuntimeRejectionCodeV1.InvalidCommand
                && result.DeathFact != null;
        }

        private void StopPhysicalTravelForImpact()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            if (trigger != null) trigger.enabled = false;
        }

        private void CompleteAcceptedImpact()
        {
            ClearPendingImpact();
            if (state != null && state.IsActive)
            {
                impactCommitted = false;
                lastImpactDiagnostic = string.Empty;
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

        private void TerminateAfterRejectedImpact()
        {
            ClearPendingImpact();
            if (state != null && state.IsActive)
            {
                state = state.Terminate(
                    ProjectileTerminationReason.EnemyImpact);
            }
            Complete();
        }

        private void ClearPendingImpact()
        {
            pendingImpactEnemy = null;
            pendingDamageCommand = null;
            pendingOccurredAtSeconds = 0d;
        }

        private void ReportImpactDiagnostic(string diagnostic)
        {
            string value = string.IsNullOrWhiteSpace(diagnostic)
                ? "player-weapon-enemy-damage-failed"
                : diagnostic;
            if (string.Equals(
                value,
                lastImpactDiagnostic,
                StringComparison.Ordinal))
            {
                return;
            }

            lastImpactDiagnostic = value;
            Debug.LogError(value, this);
        }

        private void Complete()
        {
            if (completed) return;
            completed = true;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            if (trigger != null) trigger.enabled = false;
            Destroy(gameObject);
        }

        private WeaponVector2 CurrentPosition()
        {
            Vector3 position = transform.position;
            return new WeaponVector2(position.x, position.y);
        }

        private static Vector2 ToUnity(WeaponVector2 value)
        {
            return new Vector2((float)value.X, (float)value.Y);
        }

        private bool IsSourceCollider(Collider2D other)
        {
            if (sourceOwner == null || other == null) return false;
            Transform colliderTransform = other.transform;
            return colliderTransform == sourceOwner
                || colliderTransform.IsChildOf(sourceOwner);
        }

        private static string TargetKey(
            StableId actorId,
            long lifecycleGeneration)
        {
            return actorId
                + "|"
                + lifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture);
        }

        private static string ColliderIdentity(Collider2D collider)
        {
            if (collider == null) return "null";
            Transform current = collider.transform;
            var parts = new List<string>();
            while (current != null)
            {
                parts.Add(
                    current.name
                    + "#"
                    + current.GetSiblingIndex().ToString(
                        CultureInfo.InvariantCulture));
                current = current.parent;
            }
            parts.Reverse();
            return collider.gameObject.scene.path
                + "|"
                + string.Join("/", parts)
                + "|"
                + collider.GetType().FullName;
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

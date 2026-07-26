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
    /// weapon cadence, damage, range, speed, or identities.
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

        private readonly Dictionary<string, AcceptedBatch> accepted =
            new Dictionary<string, AcceptedBatch>(StringComparer.Ordinal);
        private Texture2D runtimeTexture;
        private Sprite runtimeSprite;

        public int AcceptedBatchCount { get { return accepted.Count; } }

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
                if (!projectile.TryConfigure(effect, runtimeSprite))
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

                accepted.Add(
                    operationKey,
                    new AcceptedBatch(batch.Fingerprint));
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
            if (effect.Profile.Guidance == null
                || effect.Profile.Guidance.Mode
                    != WeaponGuidanceMode.Unguided)
            {
                rejectionCode =
                    "player-weapon-projectile-guidance-unsupported";
                return false;
            }
            if (effect.Profile.Damage == null
                || effect.Profile.Damage.DirectDamage <= 0d
                || effect.Profile.Damage.HasAreaDamage
                || effect.Profile.Damage.HasDamageOverTime
                || effect.Profile.Effects == null
                || effect.Profile.Effects.Explosion != null
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
                || effect.InitialState.Direction.LengthSquared <= 0d)
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
            if (runtimeSprite != null) Destroy(runtimeSprite);
            if (runtimeTexture != null) Destroy(runtimeTexture);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ProductionNormalProjectile2D : MonoBehaviour
    {
        private CanonicalProjectileLaunchEffect effect;
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
            Sprite projectileSprite)
        {
            if (configured
                || configuredEffect == null
                || configuredEffect.InitialState == null
                || projectileSprite == null)
            {
                return false;
            }

            effect = configuredEffect;
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
                state = state.Terminate(
                    ProjectileTerminationReason.RangeExpired);
                Complete();
            }
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

            RoomEnemyActor2D enemy =
                other.GetComponentInParent<RoomEnemyActor2D>();
            if (enemy == null || !enemy.IsBound || !enemy.IsAlive)
            {
                return;
            }

            EnemyRuntimeDamageCommandV1 command;
            double occurredAtSeconds = Time.fixedTimeAsDouble;
            try
            {
                WeaponEffectIdentity identity = effect.Identity;
                StableId operationId = StableId.Create(
                    "enemy-damage-operation",
                    "player-projectile-"
                    + Hash64(
                        identity.ToCanonicalString()
                        + "|" + enemy.ActorStableId
                        + "|" + enemy.LifecycleGeneration.ToString(
                            CultureInfo.InvariantCulture)));
                long order = checked(
                    state.Lifecycle.LaunchSimulationTick * 64L
                    + identity.ProjectileOrdinal.Value);
                command = new EnemyRuntimeDamageCommandV1(
                    operationId,
                    identity.ActorId.Value,
                    identity.ParticipantId.Value,
                    enemy.ActorStableId,
                    enemy.LifecycleGeneration,
                    order,
                    (int)effect.Profile.Damage.Category,
                    effect.Profile.Damage.DirectDamage);
            }
            catch (Exception exception)
            {
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                ReportImpactDiagnostic(
                    "player-weapon-enemy-damage-command-invalid:"
                    + exception.Message);
                TerminateAfterImpact();
                return;
            }

            impactCommitted = true;
            pendingImpactEnemy = enemy;
            pendingDamageCommand = command;
            pendingOccurredAtSeconds = occurredAtSeconds;
            StopPhysicalTravelForImpact();
            TryResolvePendingImpact();
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
                TerminateAfterImpact();
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
                TerminateAfterImpact();
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
            TerminateAfterImpact();
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

        private void TerminateAfterImpact()
        {
            pendingImpactEnemy = null;
            pendingDamageCommand = null;
            pendingOccurredAtSeconds = 0d;
            if (state != null && state.IsActive)
            {
                state = state.Terminate(
                    ProjectileTerminationReason.EnemyImpact);
            }
            Complete();
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

using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;
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
        private WeaponActorInstanceId sourceActorId;
        private LifecycleGeneration sourceLifecycle;
        private StableId sourceMountStableId;
        private EquipmentInstanceId sourceEquipmentInstanceId;
        private WeaponDefinitionId sourceWeaponDefinitionId;
        private Texture2D texture;
        private Sprite sprite;
        private bool sourceBound;
        private bool retired;

        public int AcceptedBatchCount { get { return accepted.Count; } }
        public int ActiveProjectileCount { get { return active.Count; } }
        public bool IsSourceBound { get { return sourceBound; } }
        public StableId SourceMountStableId { get { return sourceMountStableId; } }
        public bool IsRetired { get { return retired; } }

        public bool TryBindSource(
            WeaponActorInstanceId actorId,
            LifecycleGeneration lifecycle,
            StableId mountStableId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId)
        {
            if (retired
                || actorId == null
                || lifecycle == null
                || mountStableId == null
                || equipmentInstanceId == null
                || weaponDefinitionId == null)
            {
                return false;
            }
            if (sourceBound)
            {
                return sourceActorId.Equals(actorId)
                    && sourceLifecycle.Equals(lifecycle)
                    && sourceMountStableId == mountStableId
                    && sourceEquipmentInstanceId.Equals(equipmentInstanceId)
                    && sourceWeaponDefinitionId.Equals(weaponDefinitionId);
            }

            sourceActorId = actorId;
            sourceLifecycle = lifecycle;
            sourceMountStableId = mountStableId;
            sourceEquipmentInstanceId = equipmentInstanceId;
            sourceWeaponDefinitionId = weaponDefinitionId;
            sourceBound = true;
            return true;
        }

        public WeaponEffectBatchSinkResult TryAccept(
            InventoryWeaponEffectBatch batch)
        {
            if (retired)
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "canonical-projectile-sink-retired");
            }
            if (!sourceBound)
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "canonical-projectile-source-unbound");
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
            if (!MatchesBoundSource(batch.Identity))
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "canonical-projectile-source-identity-mismatch");
            }

            string key = batch.Identity.ToCanonicalString();
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
                SceneManager.MoveGameObjectToScene(
                    projectileObject,
                    gameObject.scene);
                projectileObject.SetActive(false);
                ProductionCanonicalNormalProjectile2D projectile =
                    projectileObject.AddComponent<
                        ProductionCanonicalNormalProjectile2D>();
                if (!projectile.TryConfigure(
                        effect,
                        sourceMountStableId,
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

        private bool MatchesBoundSource(WeaponEffectIdentity identity)
        {
            return identity != null
                && sourceActorId.Equals(identity.ActorId)
                && sourceLifecycle.Equals(identity.LifecycleGeneration)
                && sourceEquipmentInstanceId.Equals(identity.EquipmentInstanceId)
                && sourceWeaponDefinitionId.Equals(identity.WeaponDefinitionId);
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
                || effect.InitialState.Pierce.AuthoredValue
                    != effect.Profile.Projectile.Pierce)
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
}

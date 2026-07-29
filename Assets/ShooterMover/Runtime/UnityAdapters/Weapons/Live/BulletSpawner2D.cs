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
    [DisallowMultipleComponent]
    public sealed class BulletSource2D : MonoBehaviour
    {
        public WeaponActorInstanceId ActorId { get; private set; }
        public RunParticipantId ParticipantId { get; private set; }
        public LifecycleGeneration LifecycleGeneration { get; private set; }
        public StableId MountStableId { get; private set; }
        public EquipmentInstanceId EquipmentInstanceId { get; private set; }
        public WeaponDefinitionId WeaponDefinitionId { get; private set; }
        public bool IsBound { get; private set; }

        public bool TryBind(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
            LifecycleGeneration lifecycleGeneration,
            StableId mountStableId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId)
        {
            if (actorId == null
                || participantId == null
                || lifecycleGeneration == null
                || mountStableId == null
                || equipmentInstanceId == null
                || weaponDefinitionId == null)
            {
                return false;
            }
            if (IsBound)
            {
                return ActorId.Equals(actorId)
                    && ParticipantId.Equals(participantId)
                    && LifecycleGeneration.Equals(lifecycleGeneration)
                    && MountStableId == mountStableId
                    && EquipmentInstanceId.Equals(equipmentInstanceId)
                    && WeaponDefinitionId.Equals(weaponDefinitionId);
            }

            ActorId = actorId;
            ParticipantId = participantId;
            LifecycleGeneration = lifecycleGeneration;
            MountStableId = mountStableId;
            EquipmentInstanceId = equipmentInstanceId;
            WeaponDefinitionId = weaponDefinitionId;
            IsBound = true;
            return true;
        }
    }

    [DisallowMultipleComponent]
    public sealed class BulletSpawner2D :
        MonoBehaviour,
        IInventoryWeaponEffectBatchSink
    {
        private sealed class BoundGunSource
        {
            internal BoundGunSource(
                StableId mountStableId,
                EquipmentInstanceId equipmentInstanceId,
                WeaponDefinitionId weaponDefinitionId)
            {
                MountStableId = mountStableId;
                EquipmentInstanceId = equipmentInstanceId;
                WeaponDefinitionId = weaponDefinitionId;
            }

            internal StableId MountStableId { get; }
            internal EquipmentInstanceId EquipmentInstanceId { get; }
            internal WeaponDefinitionId WeaponDefinitionId { get; }
        }

        private const int ReceiptCapacity =
            WeaponFiringScheduler.DefaultReplayRetentionCapacity;

        private readonly Dictionary<string, string> accepted =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Queue<string> acceptedOrder = new Queue<string>();
        private readonly HashSet<Bullet2D> activeBullets =
            new HashSet<Bullet2D>();
        private readonly Dictionary<string, BoundGunSource> gunSources =
            new Dictionary<string, BoundGunSource>(StringComparer.Ordinal);
        private WeaponActorInstanceId sourceActorId;
        private RunParticipantId sourceParticipantId;
        private LifecycleGeneration sourceLifecycle;
        private StableId firstSourceMountStableId;
        private Texture2D texture;
        private Sprite sprite;
        private bool sourceBound;
        private bool retired;

        public int AcceptedBatchCount { get { return accepted.Count; } }
        public int ActiveBulletCount { get { return activeBullets.Count; } }
        public int BoundSourceCount { get { return gunSources.Count; } }
        public bool IsSourceBound { get { return sourceBound; } }
        public RunParticipantId SourceParticipantId { get { return sourceParticipantId; } }
        public StableId SourceMountStableId { get { return firstSourceMountStableId; } }
        public bool IsRetired { get { return retired; } }

        public bool TryBindSource(
            WeaponActorInstanceId actorId,
            LifecycleGeneration lifecycle,
            StableId mountStableId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId)
        {
            return TryBindSourceCore(
                actorId,
                null,
                lifecycle,
                mountStableId,
                equipmentInstanceId,
                weaponDefinitionId);
        }

        public bool TryBindSource(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
            LifecycleGeneration lifecycle,
            StableId mountStableId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId)
        {
            if (participantId == null) return false;
            return TryBindSourceCore(
                actorId,
                participantId,
                lifecycle,
                mountStableId,
                equipmentInstanceId,
                weaponDefinitionId);
        }

        private bool TryBindSourceCore(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
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
                if (!sourceActorId.Equals(actorId)
                    || !sourceLifecycle.Equals(lifecycle)
                    || (sourceParticipantId != null
                        && participantId != null
                        && !sourceParticipantId.Equals(participantId)))
                {
                    return false;
                }
            }
            else
            {
                sourceActorId = actorId;
                sourceLifecycle = lifecycle;
                firstSourceMountStableId = mountStableId;
                sourceBound = true;
            }

            if (sourceParticipantId == null && participantId != null)
            {
                sourceParticipantId = participantId;
            }

            string key = SourceKey(equipmentInstanceId);
            BoundGunSource existing;
            if (gunSources.TryGetValue(key, out existing))
            {
                return existing.MountStableId == mountStableId
                    && existing.EquipmentInstanceId.Equals(equipmentInstanceId)
                    && existing.WeaponDefinitionId.Equals(weaponDefinitionId);
            }

            foreach (BoundGunSource source in gunSources.Values)
            {
                if (source.MountStableId == mountStableId)
                {
                    return false;
                }
            }

            gunSources.Add(
                key,
                new BoundGunSource(
                    mountStableId,
                    equipmentInstanceId,
                    weaponDefinitionId));
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
            if (!sourceBound || gunSources.Count == 0)
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "canonical-projectile-source-unbound");
            }
            if (batch == null
                || batch.CoreBatch == null
                || batch.Identity == null
                || batch.Identity.ParticipantId == null)
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "canonical-projectile-batch-invalid");
            }

            BoundGunSource gunSource;
            if (!TryResolveBoundSource(batch.Identity, out gunSource))
            {
                return WeaponEffectBatchSinkResult.Reject(
                    "canonical-projectile-source-identity-mismatch");
            }

            string key = FireKey(batch.Identity);
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

            var effects = new List<ProjectileLaunchEffect>(batch.EffectCount);
            for (int index = 0; index < batch.CoreBatch.Effects.Count; index++)
            {
                ProjectileLaunchEffect effect =
                    batch.CoreBatch.Effects[index] as ProjectileLaunchEffect;
                if (effect == null)
                {
                    return WeaponEffectBatchSinkResult.Reject(
                        "canonical-projectile-launch-required");
                }

                string rejectionCode;
                if (!IsSupported(effect, out rejectionCode))
                {
                    return WeaponEffectBatchSinkResult.Reject(rejectionCode);
                }
                effects.Add(effect);
            }

            var bulletObjects = new List<GameObject>(effects.Count);
            var bullets = new List<Bullet2D>(effects.Count);
            try
            {
                EnsureBulletSprite();
                for (int index = 0; index < effects.Count; index++)
                {
                    ProjectileLaunchEffect effect = effects[index];
                    GameObject bulletObject = new GameObject(
                        "PlayerBullet_"
                        + effect.Identity.ShotSequence.ToString(
                            CultureInfo.InvariantCulture)
                        + "_" + effect.Identity.ProjectileOrdinal.Value.ToString(
                            CultureInfo.InvariantCulture));
                    bulletObjects.Add(bulletObject);
                    SceneManager.MoveGameObjectToScene(
                        bulletObject,
                        gameObject.scene);
                    bulletObject.SetActive(false);

                    BulletSource2D bulletSource =
                        bulletObject.AddComponent<
                            BulletSource2D>();
                    if (!bulletSource.TryBind(
                            sourceActorId,
                            sourceParticipantId ?? batch.Identity.ParticipantId,
                            sourceLifecycle,
                            gunSource.MountStableId,
                            gunSource.EquipmentInstanceId,
                            gunSource.WeaponDefinitionId))
                    {
                        throw new InvalidOperationException(
                            "canonical-projectile-source-projection-rejected");
                    }

                    Bullet2D bullet =
                        bulletObject.AddComponent<Bullet2D>();
                    bullets.Add(bullet);
                    if (!bullet.TryConfigure(
                            effect,
                            sprite,
                            transform,
                            HandleBulletFinished))
                    {
                        throw new InvalidOperationException(
                            "canonical-projectile-configuration-rejected");
                    }
                }

                for (int index = 0; index < bullets.Count; index++)
                {
                    activeBullets.Add(bullets[index]);
                    bulletObjects[index].SetActive(true);
                }
                for (int index = 0; index < bullets.Count; index++)
                {
                    if (!bullets[index].Launch())
                    {
                        throw new InvalidOperationException(
                            "canonical-projectile-emission-rejected");
                    }
                }

                RetainAccepted(key, batch.Fingerprint);
                if (sourceParticipantId == null)
                {
                    sourceParticipantId = batch.Identity.ParticipantId;
                }
                return WeaponEffectBatchSinkResult.Accept();
            }
            catch (Exception exception)
            {
                CleanupStagedBullets(bulletObjects, bullets);
                if (WeaponLiveExceptionPolicy.IsFatal(exception)) throw;
                Debug.LogError(
                    "canonical-projectile-batch-staging-failed:"
                    + exception.Message,
                    this);
                return WeaponEffectBatchSinkResult.Reject(
                    "canonical-projectile-batch-staging-failed");
            }
        }

        public void ClearOwnerBullets()
        {
            if (retired) return;
            retired = true;
            var snapshot = new List<Bullet2D>(activeBullets);
            for (int index = 0; index < snapshot.Count; index++)
            {
                if (snapshot[index] != null) snapshot[index].RemoveFromGame();
            }
        }

        private bool TryResolveBoundSource(
            WeaponEffectIdentity identity,
            out BoundGunSource resolved)
        {
            resolved = null;
            if (identity == null
                || identity.EquipmentInstanceId == null
                || identity.WeaponDefinitionId == null
                || !sourceActorId.Equals(identity.ActorId)
                || (sourceParticipantId != null
                    && !sourceParticipantId.Equals(identity.ParticipantId))
                || !sourceLifecycle.Equals(identity.LifecycleGeneration)
                || !gunSources.TryGetValue(
                    SourceKey(identity.EquipmentInstanceId),
                    out resolved)
                || !resolved.EquipmentInstanceId.Equals(
                    identity.EquipmentInstanceId)
                || !resolved.WeaponDefinitionId.Equals(
                    identity.WeaponDefinitionId))
            {
                resolved = null;
                return false;
            }
            return true;
        }

        private static string FireKey(WeaponEffectIdentity identity)
        {
            return identity.ActorId + "|"
                + identity.ParticipantId + "|"
                + identity.EquipmentInstanceId + "|"
                + identity.WeaponDefinitionId + "|"
                + identity.FireOperationId + "|"
                + identity.LifecycleGeneration + "|"
                + identity.ShotSequence.ToString(CultureInfo.InvariantCulture);
        }

        private static string SourceKey(EquipmentInstanceId equipmentInstanceId)
        {
            return equipmentInstanceId.Value.ToString();
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

        private void CleanupStagedBullets(
            IList<GameObject> bulletObjects,
            IList<Bullet2D> bullets)
        {
            for (int index = 0; index < bullets.Count; index++)
            {
                Bullet2D bullet = bullets[index];
                if (bullet != null) activeBullets.Remove(bullet);
            }
            for (int index = 0; index < bulletObjects.Count; index++)
            {
                GameObject bulletObject = bulletObjects[index];
                if (bulletObject == null) continue;
                bulletObject.SetActive(false);
                Destroy(bulletObject);
            }
        }

        private void HandleBulletFinished(
            Bullet2D bullet)
        {
            if (bullet != null) activeBullets.Remove(bullet);
        }

        private static bool IsSupported(
            ProjectileLaunchEffect effect,
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

        private void EnsureBulletSprite()
        {
            if (sprite != null) return;
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "Bullet Pixel";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            sprite.name = "Bullet Sprite";
        }

        private void OnDisable()
        {
            ClearOwnerBullets();
        }

        private void OnDestroy()
        {
            ClearOwnerBullets();
            accepted.Clear();
            acceptedOrder.Clear();
            activeBullets.Clear();
            gunSources.Clear();
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }
}

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
    public sealed class ProjectileSourceIdentity2D : MonoBehaviour
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
    public sealed class ProjectileEffectSink2D :
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
        private readonly HashSet<NormalProjectile2D> active =
            new HashSet<NormalProjectile2D>();
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
        public int ActiveProjectileCount { get { return active.Count; } }
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
                || batch.Identity.ParticipantId == null
                || batch.CoreBatch.EffectCount < 1
                || batch.CoreBatch.EffectCount != batch.CoreBatch.Effects.Count)
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

            var effects = new List<ProjectileLaunchEffect>(
                batch.CoreBatch.EffectCount);
            for (int index = 0; index < batch.CoreBatch.Effects.Count; index++)
            {
                ProjectileLaunchEffect effect =
                    batch.CoreBatch.Effects[index] as ProjectileLaunchEffect;
                if (effect == null
                    || effect.Identity == null
                    || !SameFire(batch.Identity, effect.Identity))
                {
                    return WeaponEffectBatchSinkResult.Reject(
                        "canonical-projectile-effect-identity-mismatch");
                }

                BoundGunSource effectSource;
                if (!TryResolveBoundSource(effect.Identity, out effectSource)
                    || effectSource.MountStableId != gunSource.MountStableId)
                {
                    return WeaponEffectBatchSinkResult.Reject(
                        "canonical-projectile-source-identity-mismatch");
                }

                string rejectionCode;
                if (!IsSupported(effect, out rejectionCode))
                {
                    return WeaponEffectBatchSinkResult.Reject(rejectionCode);
                }
                effects.Add(effect);
            }

            var projectileObjects = new List<GameObject>(effects.Count);
            var projectiles = new List<NormalProjectile2D>(effects.Count);
            try
            {
                EnsureSprite();
                for (int index = 0; index < effects.Count; index++)
                {
                    ProjectileLaunchEffect effect = effects[index];
                    GameObject projectileObject = new GameObject(
                        "CanonicalPlayerProjectile_"
                        + effect.Identity.ShotSequence.ToString(
                            CultureInfo.InvariantCulture)
                        + "_" + effect.Identity.ProjectileOrdinal.Value.ToString(
                            CultureInfo.InvariantCulture));
                    projectileObjects.Add(projectileObject);
                    SceneManager.MoveGameObjectToScene(
                        projectileObject,
                        gameObject.scene);
                    projectileObject.SetActive(false);

                    ProjectileSourceIdentity2D identityProjection =
                        projectileObject.AddComponent<
                            ProjectileSourceIdentity2D>();
                    if (!identityProjection.TryBind(
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

                    NormalProjectile2D projectile =
                        projectileObject.AddComponent<NormalProjectile2D>();
                    projectiles.Add(projectile);
                    if (!projectile.TryConfigure(
                            effect,
                            sprite,
                            transform,
                            HandleProjectileCompleted))
                    {
                        throw new InvalidOperationException(
                            "canonical-projectile-configuration-rejected");
                    }
                }

                for (int index = 0; index < projectiles.Count; index++)
                {
                    active.Add(projectiles[index]);
                    projectileObjects[index].SetActive(true);
                }
                for (int index = 0; index < projectiles.Count; index++)
                {
                    if (!projectiles[index].BeginEmission())
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
                CleanupStagedProjectiles(projectileObjects, projectiles);
                if (WeaponLiveExceptionPolicy.IsFatal(exception)) throw;
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
            var snapshot = new List<NormalProjectile2D>(active);
            for (int index = 0; index < snapshot.Count; index++)
            {
                if (snapshot[index] != null) snapshot[index].RetireOwner();
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

        private static bool SameFire(
            WeaponEffectIdentity left,
            WeaponEffectIdentity right)
        {
            return left != null
                && right != null
                && left.ActorId.Equals(right.ActorId)
                && left.ParticipantId.Equals(right.ParticipantId)
                && left.EquipmentInstanceId.Equals(right.EquipmentInstanceId)
                && left.WeaponDefinitionId.Equals(right.WeaponDefinitionId)
                && left.FireOperationId.Equals(right.FireOperationId)
                && left.LifecycleGeneration.Equals(right.LifecycleGeneration)
                && left.ShotSequence == right.ShotSequence;
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

        private void CleanupStagedProjectiles(
            IList<GameObject> projectileObjects,
            IList<NormalProjectile2D> projectiles)
        {
            for (int index = 0; index < projectiles.Count; index++)
            {
                NormalProjectile2D projectile = projectiles[index];
                if (projectile != null) active.Remove(projectile);
            }
            for (int index = 0; index < projectileObjects.Count; index++)
            {
                GameObject projectileObject = projectileObjects[index];
                if (projectileObject == null) continue;
                projectileObject.SetActive(false);
                Destroy(projectileObject);
            }
        }

        private void HandleProjectileCompleted(
            NormalProjectile2D projectile)
        {
            if (projectile != null) active.Remove(projectile);
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
            gunSources.Clear();
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }
}

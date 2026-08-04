using System;
using System.Collections.Generic;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Guns.Execution;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Direct live bullet spawn. It keeps only the bullets that currently exist in the room.
    /// There are no replay receipts, accepted-operation records, or retry queues here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletSpawn : MonoBehaviour
    {
        private readonly HashSet<Bullet> active = new HashSet<Bullet>();
        private Texture2D texture;
        private Sprite sprite;
        private GunTargets targets;

        public int ActiveCount { get { return active.Count; } }

        public bool TrySpawn(
            IList<ProjectileLaunchEffect> effects,
            Transform owner)
        {
            if (effects == null || effects.Count == 0 || owner == null)
            {
                return false;
            }

            var objects = new List<GameObject>(effects.Count);
            var bullets = new List<Bullet>(effects.Count);
            try
            {
                EnsureSprite();
                EnsureTargets(owner);
                for (int index = 0; index < effects.Count; index++)
                {
                    ProjectileLaunchEffect effect = effects[index];
                    if (effect == null)
                    {
                        throw new InvalidOperationException(
                            "bullet-spawn-effect-missing");
                    }

                    GameObject bulletObject = new GameObject(
                        "PlayerBullet_"
                        + effect.Identity.ShotSequence
                        + "_" + effect.Identity.ProjectileOrdinal.Value);
                    objects.Add(bulletObject);
                    SceneManager.MoveGameObjectToScene(
                        bulletObject,
                        gameObject.scene);
                    bulletObject.SetActive(false);

                    Bullet bullet = bulletObject.AddComponent<Bullet>();
                    bullets.Add(bullet);
                    if (!bullet.TryConfigure(
                            effect,
                            sprite,
                            owner,
                            targets,
                            HandleFinished))
                    {
                        throw new InvalidOperationException(
                            "bullet-spawn-config-failed");
                    }
                    BulletPresentation.Apply(
                        bulletObject,
                        effect.Profile,
                        sprite);
                }

                for (int index = 0; index < bullets.Count; index++)
                {
                    active.Add(bullets[index]);
                    objects[index].SetActive(true);
                    if (!bullets[index].Launch())
                    {
                        throw new InvalidOperationException(
                            "bullet-spawn-launch-failed");
                    }
                }
                return true;
            }
            catch (Exception exception)
            {
                if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                Cleanup(objects, bullets);
                Debug.LogError(
                    "bullet-spawn-failed:" + exception.Message,
                    this);
                return false;
            }
        }

        public void Clear()
        {
            var snapshot = new List<Bullet>(active);
            for (int index = 0; index < snapshot.Count; index++)
            {
                Bullet bullet = snapshot[index];
                if (bullet != null) bullet.RemoveFromGame();
            }
            active.Clear();
        }

        private void EnsureTargets(Transform owner)
        {
            if (targets == null)
            {
                targets = GetComponent<GunTargets>();
                if (targets == null)
                {
                    targets = gameObject.AddComponent<GunTargets>();
                }
            }
            targets.Configure(owner);
        }

        private void HandleFinished(Bullet bullet)
        {
            if (bullet != null) active.Remove(bullet);
        }

        private void Cleanup(
            IList<GameObject> objects,
            IList<Bullet> bullets)
        {
            for (int index = 0; index < bullets.Count; index++)
            {
                Bullet bullet = bullets[index];
                if (bullet != null) active.Remove(bullet);
            }
            for (int index = 0; index < objects.Count; index++)
            {
                GameObject value = objects[index];
                if (value == null) continue;
                value.SetActive(false);
                Destroy(value);
            }
        }

        private void EnsureSprite()
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
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
            targets = null;
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }
}

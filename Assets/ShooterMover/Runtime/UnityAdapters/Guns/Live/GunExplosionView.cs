using System;
using ShooterMover.Domain.Guns.Execution;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Short-lived visual projection of an already-authorized canonical explosion emission.
    /// It never decides damage, targets, radius, or trigger policy.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class GunExplosionView : MonoBehaviour
    {
        private const int TextureSize = 64;
        private const float DurationSeconds = 0.22f;

        private static Texture2D texture;
        private static Sprite sprite;

        private SpriteRenderer renderer;
        private float radius;
        private float elapsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedArt()
        {
            texture = null;
            sprite = null;
        }

        public static void Show(
            ProjectileEffectEmission emission,
            GunTargets targets)
        {
            if (emission == null
                || emission.Kind != ProjectileEffectEmissionKind.Explosion
                || emission.Position == null
                || emission.Effects == null
                || emission.Effects.Explosion == null
                || targets == null)
            {
                throw new ArgumentException(
                    "gun-explosion-view-emission-invalid");
            }

            double authoredRadius = emission.Effects.Explosion.Radius;
            if (double.IsNaN(authoredRadius)
                || double.IsInfinity(authoredRadius)
                || authoredRadius <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(authoredRadius));
            }

            EnsureSprite();
            GameObject viewObject = new GameObject("Gun Explosion");
            SceneManager.MoveGameObjectToScene(
                viewObject,
                targets.gameObject.scene);
            viewObject.transform.position = new Vector3(
                (float)emission.Position.X,
                (float)emission.Position.Y,
                0f);
            SpriteRenderer viewRenderer =
                viewObject.AddComponent<SpriteRenderer>();
            viewRenderer.sprite = sprite;
            viewRenderer.color = new Color(1f, 0.58f, 0.12f, 0.95f);
            viewRenderer.sortingOrder = 120;
            GunExplosionView view = viewObject.AddComponent<GunExplosionView>();
            view.Configure((float)authoredRadius, viewRenderer);
        }

        private void Configure(
            float configuredRadius,
            SpriteRenderer configuredRenderer)
        {
            radius = configuredRadius;
            renderer = configuredRenderer
                ?? throw new ArgumentNullException(nameof(configuredRenderer));
            ApplyFrame(0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / DurationSeconds);
            ApplyFrame(progress);
            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyFrame(float progress)
        {
            float diameter = radius * 2f;
            float scale = diameter * Mathf.Lerp(0.18f, 1f, progress);
            transform.localScale = new Vector3(scale, scale, 1f);
            if (renderer != null)
            {
                Color color = renderer.color;
                color.a = 1f - progress;
                renderer.color = color;
            }
        }

        private static void EnsureSprite()
        {
            if (sprite != null) return;

            texture = new Texture2D(
                TextureSize,
                TextureSize,
                TextureFormat.RGBA32,
                false);
            texture.name = "Gun Explosion Radial Texture";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            float center = (TextureSize - 1) * 0.5f;
            float maximum = center;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = (x - center) / maximum;
                    float dy = (y - center) / maximum;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Clamp01(1f - distance);
                    float ring = Mathf.Clamp01(
                        1f - Mathf.Abs(distance - 0.72f) * 9f);
                    float alpha = Mathf.Clamp01(
                        core * 0.72f + ring * 0.8f);
                    texture.SetPixel(
                        x,
                        y,
                        new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply(false, true);

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                TextureSize);
            sprite.name = "Gun Explosion Radial Sprite";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Typed Level 1 weapon integration. It projects effects emitted by the Hub-owned
    /// holdings/loadout composition. It never creates holdings, changes equipped slots,
    /// adopts authority state, or exposes an in-game loadout editor.
    /// </summary>
    [DefaultExecutionOrder(19900)]
    [DisallowMultipleComponent]
    public sealed class Stage1WeaponPresentationRepairV1 : MonoBehaviour
    {
        private const string RicochetDefinitionId =
            ProductionStarterWeaponCatalogV1
                .RicochetWeaponDefinitionId;

        private readonly HashSet<InventoryWeaponEffectInstance2D>
            preparedEffects =
                new HashSet<InventoryWeaponEffectInstance2D>();
        private readonly Dictionary<string, Sprite> projectileSprites =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> trailMaterials =
            new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly List<UnityEngine.Object> runtimeAssets =
            new List<UnityEngine.Object>();

        private Stage1PlayableLoopCompositionV1 composition;
        private InventoryWeaponEffectEmitter2D effectEmitter;
        private Material arcMaterial;
        private Material explosionMaterial;
        private bool installed;

        private void Update()
        {
            if (installed)
            {
                return;
            }

            composition = GetComponent<
                Stage1PlayableLoopCompositionV1>();
            if (composition == null
                || !composition.IsHubLoadoutIntegrationReady)
            {
                return;
            }

            effectEmitter = composition.HubWeaponEffectEmitter;
            installed = effectEmitter != null;
        }

        private void LateUpdate()
        {
            if (!installed || effectEmitter == null)
            {
                return;
            }

            IReadOnlyList<InventoryWeaponEffectInstance2D> emitted =
                effectEmitter.EmittedEffects;
            for (int index = 0; index < emitted.Count; index++)
            {
                InventoryWeaponEffectInstance2D effect = emitted[index];
                if (effect == null || !preparedEffects.Add(effect))
                {
                    continue;
                }

                ChainArcEffect chain =
                    effect.Description as ChainArcEffect;
                if (chain != null)
                {
                    ExecuteArc(chain, effect.gameObject);
                    continue;
                }

                PrepareProjectile(effect);
            }
        }

        private void PrepareProjectile(
            InventoryWeaponEffectInstance2D effect)
        {
            string definitionId =
                effect.Description.Identity.WeaponDefinitionId.Value;
            WeaponVisualProfile profile =
                WeaponVisualProfile.For(definitionId);

            SpriteRenderer[] existing =
                effect.GetComponents<SpriteRenderer>();
            for (int index = 0; index < existing.Length; index++)
            {
                existing[index].enabled = false;
            }

            effect.transform.localScale = Vector3.one;
            Rigidbody2D body = effect.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.collisionDetectionMode =
                    CollisionDetectionMode2D.Continuous;
                body.interpolation =
                    RigidbodyInterpolation2D.Interpolate;
                if (body.linearVelocity.sqrMagnitude > 0.0001f)
                {
                    effect.transform.right =
                        body.linearVelocity.normalized;
                }
            }

            CircleCollider2D collider =
                effect.GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.radius = profile.ColliderRadius;
            }

            GameObject visual = new GameObject("WeaponVisual");
            visual.transform.SetParent(effect.transform, false);
            visual.transform.localScale = profile.VisualScale;
            SpriteRenderer renderer =
                visual.AddComponent<SpriteRenderer>();
            renderer.sprite = ProjectileSprite(
                definitionId,
                profile);
            renderer.sortingOrder = 60;

            AddTrail(effect.gameObject, definitionId, profile);

            if (string.Equals(
                definitionId,
                RicochetDefinitionId,
                StringComparison.Ordinal))
            {
                Stage1RicochetBounceV1 bounce =
                    effect.gameObject
                        .AddComponent<Stage1RicochetBounceV1>();
                bounce.Configure(this);
            }
            if (effect.Description is ExplosiveProjectileEffect)
            {
                Stage1RocketExplosionVisualV1 explosion =
                    effect.gameObject.AddComponent<
                        Stage1RocketExplosionVisualV1>();
                explosion.Configure(this);
            }
        }

        private Sprite ProjectileSprite(
            string definitionId,
            WeaponVisualProfile profile)
        {
            Sprite existing;
            if (projectileSprites.TryGetValue(
                definitionId,
                out existing))
            {
                return existing;
            }

            const int width = 24;
            const int height = 12;
            Texture2D texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false);
            texture.name = "Weapon Visual " + definitionId;
            texture.filterMode = FilterMode.Point;
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = ((x + 0.5f) / width) * 2f - 1f;
                    float ny = ((y + 0.5f) / height) * 2f - 1f;
                    bool filled = profile.Diamond
                        ? Mathf.Abs(nx) + Mathf.Abs(ny) <= 1f
                        : (nx * nx) + (ny * ny * 2.2f) <= 1f;
                    texture.SetPixel(
                        x,
                        y,
                        filled
                            ? Color.Lerp(
                                profile.EdgeColor,
                                profile.CoreColor,
                                1f - Mathf.Abs(ny))
                            : clear);
                }
            }
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                width);
            projectileSprites.Add(definitionId, sprite);
            runtimeAssets.Add(texture);
            runtimeAssets.Add(sprite);
            return sprite;
        }

        private void AddTrail(
            GameObject projectile,
            string definitionId,
            WeaponVisualProfile profile)
        {
            Material material = TrailMaterial(
                definitionId,
                profile);
            if (material == null)
            {
                return;
            }

            TrailRenderer trail =
                projectile.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = profile.TrailTime;
            trail.startWidth = profile.TrailWidth;
            trail.endWidth = 0f;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.sortingOrder = 59;
        }

        private Material TrailMaterial(
            string definitionId,
            WeaponVisualProfile profile)
        {
            Material existing;
            if (trailMaterials.TryGetValue(
                definitionId,
                out existing))
            {
                return existing;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader);
            material.name = "Weapon Trail " + profile.Name;
            material.color = profile.EdgeColor;
            trailMaterials.Add(definitionId, material);
            runtimeAssets.Add(material);
            return material;
        }

        private void ExecuteArc(
            ChainArcEffect chain,
            GameObject effectObject)
        {
            IReadOnlyList<Stage1ArcTargetProjectionV1> candidates =
                composition.GatherArcTargets(chain);
            int maximumHits = Mathf.Clamp(
                chain.MaximumTargets + 1,
                1,
                4);
            var points = new List<Vector3>
            {
                new Vector3(
                    (float)chain.Origin.X,
                    (float)chain.Origin.Y,
                    0f),
            };

            int applied = 0;
            for (int index = 0;
                index < candidates.Count && applied < maximumHits;
                index++)
            {
                Stage1ArcTargetProjectionV1 candidate =
                    candidates[index];
                if (composition.TryApplyArcDamage(
                    candidate,
                    chain.Identity,
                    chain.Damage,
                    "arc-" + applied.ToString(
                        CultureInfo.InvariantCulture)))
                {
                    points.Add(candidate.Position);
                    applied++;
                }
            }

            if (points.Count == 1)
            {
                points.Add(
                    points[0]
                    + new Vector3(
                        (float)chain.Direction.X,
                        (float)chain.Direction.Y,
                        0f)
                    * Mathf.Min((float)chain.MaximumRange, 4f));
            }
            SpawnArcLine(points);
            Destroy(effectObject);
        }

        private void SpawnArcLine(
            IReadOnlyList<Vector3> points)
        {
            Material material = ArcMaterial();
            if (material == null)
            {
                return;
            }

            GameObject visual = new GameObject("ArcGunLiveLine");
            LineRenderer line =
                visual.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = points.Count;
            line.startWidth = 0.16f;
            line.endWidth = 0.06f;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.sortingOrder = 70;
            for (int index = 0; index < points.Count; index++)
            {
                line.SetPosition(index, points[index]);
            }
            visual.AddComponent<Stage1TimedDestroyV1>()
                .Configure(0.14f);
        }

        private Material ArcMaterial()
        {
            if (arcMaterial != null)
            {
                return arcMaterial;
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            arcMaterial = new Material(shader);
            arcMaterial.color = new Color(0.35f, 0.95f, 1f, 1f);
            runtimeAssets.Add(arcMaterial);
            return arcMaterial;
        }

        internal bool IsEnemyCollider(Collider2D collider)
        {
            return composition != null
                && composition
                    .IsEnemyColliderForWeaponPresentation(collider);
        }

        internal bool IsPlayerCollider(Collider2D collider)
        {
            return composition != null
                && composition
                    .IsPlayerColliderForWeaponPresentation(collider);
        }

        internal void SpawnRocketExplosion(Vector3 position)
        {
            Material material = ExplosionMaterial();
            if (material == null
                || !UnityEngine.Application.isPlaying)
            {
                return;
            }

            GameObject visual =
                new GameObject("RocketExplosionVisual");
            visual.transform.position = position;
            LineRenderer ring =
                visual.AddComponent<LineRenderer>();
            ring.sharedMaterial = material;
            ring.loop = true;
            ring.positionCount = 24;
            ring.startWidth = 0.14f;
            ring.endWidth = 0.14f;
            ring.sortingOrder = 69;
            for (int index = 0;
                index < ring.positionCount;
                index++)
            {
                float angle = index * Mathf.PI * 2f
                    / ring.positionCount;
                ring.SetPosition(
                    index,
                    new Vector3(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle),
                        0f) * 0.7f);
            }
            visual.AddComponent<Stage1TimedDestroyV1>()
                .Configure(0.18f);
        }

        private Material ExplosionMaterial()
        {
            if (explosionMaterial != null)
            {
                return explosionMaterial;
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            explosionMaterial = new Material(shader);
            explosionMaterial.color =
                new Color(1f, 0.35f, 0.05f, 0.9f);
            runtimeAssets.Add(explosionMaterial);
            return explosionMaterial;
        }

        private void OnDestroy()
        {
            for (int index = 0;
                index < runtimeAssets.Count;
                index++)
            {
                if (runtimeAssets[index] != null)
                {
                    Destroy(runtimeAssets[index]);
                }
            }
            runtimeAssets.Clear();
            projectileSprites.Clear();
            trailMaterials.Clear();
        }

        private sealed class WeaponVisualProfile
        {
            private WeaponVisualProfile(
                string name,
                Color core,
                Color edge,
                Vector3 visualScale,
                float colliderRadius,
                float trailTime,
                float trailWidth,
                bool diamond)
            {
                Name = name;
                CoreColor = core;
                EdgeColor = edge;
                VisualScale = visualScale;
                ColliderRadius = colliderRadius;
                TrailTime = trailTime;
                TrailWidth = trailWidth;
                Diamond = diamond;
            }

            public string Name { get; }
            public Color CoreColor { get; }
            public Color EdgeColor { get; }
            public Vector3 VisualScale { get; }
            public float ColliderRadius { get; }
            public float TrailTime { get; }
            public float TrailWidth { get; }
            public bool Diamond { get; }

            public static WeaponVisualProfile For(
                string definitionId)
            {
                if (string.Equals(
                    definitionId,
                    "weapon.shotgun",
                    StringComparison.Ordinal))
                {
                    return new WeaponVisualProfile(
                        "Shotgun",
                        new Color(1f, 0.95f, 0.45f, 1f),
                        new Color(1f, 0.55f, 0.08f, 1f),
                        new Vector3(0.42f, 0.32f, 1f),
                        0.1f,
                        0.08f,
                        0.06f,
                        false);
                }
                if (string.Equals(
                    definitionId,
                    "weapon.rocket-launcher",
                    StringComparison.Ordinal))
                {
                    return new WeaponVisualProfile(
                        "Rocket",
                        new Color(1f, 0.85f, 0.25f, 1f),
                        new Color(1f, 0.12f, 0.02f, 1f),
                        new Vector3(1.15f, 0.7f, 1f),
                        0.16f,
                        0.22f,
                        0.15f,
                        true);
                }
                if (string.Equals(
                    definitionId,
                    RicochetDefinitionId,
                    StringComparison.Ordinal))
                {
                    return new WeaponVisualProfile(
                        "Ricochet",
                        new Color(0.95f, 1f, 1f, 1f),
                        new Color(0.2f, 0.75f, 1f, 1f),
                        new Vector3(0.8f, 0.5f, 1f),
                        0.13f,
                        0.16f,
                        0.1f,
                        true);
                }
                return new WeaponVisualProfile(
                    "Blaster",
                    new Color(0.75f, 1f, 1f, 1f),
                    new Color(0.05f, 0.55f, 1f, 1f),
                    new Vector3(0.72f, 0.42f, 1f),
                    0.12f,
                    0.12f,
                    0.09f,
                    false);
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1RicochetBounceV1 : MonoBehaviour
    {
        private const int MaximumWallBounces = 2;

        private Stage1WeaponPresentationRepairV1 presentation;
        private Rigidbody2D body;
        private Collider2D lastCollider;
        private float lastBounceTime;
        private int bounceCount;

        public void Configure(
            Stage1WeaponPresentationRepairV1 configuredPresentation)
        {
            presentation = configuredPresentation;
            body = GetComponent<Rigidbody2D>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (presentation == null
                || body == null
                || other == null
                || other.isTrigger
                || presentation.IsEnemyCollider(other)
                || presentation.IsPlayerCollider(other))
            {
                return;
            }

            if (other == lastCollider
                && Time.time - lastBounceTime < 0.08f)
            {
                return;
            }
            lastCollider = other;
            lastBounceTime = Time.time;

            if (bounceCount >= MaximumWallBounces)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 velocity = body.linearVelocity;
            if (velocity.sqrMagnitude < 0.0001f)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 closest = other.ClosestPoint(
                transform.position);
            Vector2 normal =
                (Vector2)transform.position - closest;
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = -velocity.normalized;
            }
            normal.Normalize();
            body.linearVelocity = Vector2.Reflect(
                velocity,
                normal);
            transform.right = body.linearVelocity.normalized;
            bounceCount++;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1RocketExplosionVisualV1 : MonoBehaviour
    {
        private Stage1WeaponPresentationRepairV1 presentation;
        private bool shuttingDown;

        public void Configure(
            Stage1WeaponPresentationRepairV1 configuredPresentation)
        {
            presentation = configuredPresentation;
        }

        private void OnApplicationQuit()
        {
            shuttingDown = true;
        }

        private void OnDestroy()
        {
            if (!shuttingDown
                && presentation != null
                && presentation.isActiveAndEnabled)
            {
                presentation.SpawnRocketExplosion(
                    transform.position);
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1TimedDestroyV1 : MonoBehaviour
    {
        private float destroyAt;

        public void Configure(float lifetime)
        {
            destroyAt = Time.time + Mathf.Max(0.01f, lifetime);
        }

        private void Update()
        {
            if (Time.time >= destroyAt)
            {
                Destroy(gameObject);
            }
        }
    }
}

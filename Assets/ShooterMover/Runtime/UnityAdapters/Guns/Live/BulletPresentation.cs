using System;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Applies authored projectile footprint and lightweight runtime geometry after the
    /// canonical Bullet has configured movement and impact behavior.
    /// </summary>
    internal static class BulletPresentation
    {
        public static void Apply(
            GameObject projectileObject,
            ProjectileExecutionProfile profile,
            Sprite pixelSprite)
        {
            if (projectileObject == null)
            {
                throw new ArgumentNullException(nameof(projectileObject));
            }
            if (profile == null || profile.Projectile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            if (pixelSprite == null)
            {
                throw new ArgumentNullException(nameof(pixelSprite));
            }

            float radius = ResolveAuthoredRadius(profile);
            CircleCollider2D collider =
                projectileObject.GetComponent<CircleCollider2D>()
                ?? throw new InvalidOperationException(
                    "bullet-presentation-circle-collider-missing");
            collider.radius = radius;

            switch (profile.Projectile.Kind)
            {
                case GunProjectileKind.Orb:
                    ConfigureOrb(projectileObject.transform, radius);
                    break;
                case GunProjectileKind.Rocket:
                    ConfigureRocket(
                        projectileObject.transform,
                        pixelSprite,
                        radius);
                    break;
            }
        }

        private static float ResolveAuthoredRadius(
            ProjectileExecutionProfile profile)
        {
            ShotPattern delivery = profile.SourceBlueprint == null
                ? null
                : profile.SourceBlueprint.Delivery;
            double radius = 0d;
            if (delivery != null)
            {
                switch (profile.Projectile.Kind)
                {
                    case GunProjectileKind.RegularProjectile:
                        radius = delivery.Normal == null
                            ? 0d
                            : delivery.Normal.ProjectileRadius;
                        break;
                    case GunProjectileKind.Orb:
                        radius = delivery.Orb == null
                            ? 0d
                            : delivery.Orb.ProjectileRadius;
                        break;
                    case GunProjectileKind.Rocket:
                        radius = delivery.Rocket == null
                            ? 0d
                            : delivery.Rocket.ProjectileRadius;
                        break;
                }
            }

            if (double.IsNaN(radius)
                || double.IsInfinity(radius)
                || radius <= 0d)
            {
                return LegacyRadius(profile.Projectile.Kind);
            }
            return (float)radius;
        }

        private static float LegacyRadius(GunProjectileKind kind)
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

        private static void ConfigureOrb(Transform root, float radius)
        {
            Transform visual = root.Find("Visual");
            if (visual == null)
            {
                throw new InvalidOperationException(
                    "bullet-presentation-orb-visual-missing");
            }

            float diameter = radius * 2f;
            visual.localScale = new Vector3(diameter, diameter, 1f);
        }

        private static void ConfigureRocket(
            Transform root,
            Sprite pixelSprite,
            float radius)
        {
            DisableRenderer(root.Find("Visual"));
            DisableRenderer(root.Find("Exhaust"));

            float bodyLength = radius * 3.5f;
            float bodyWidth = radius * 1.35f;
            Color bodyColor = new Color(0.92f, 0.34f, 0.12f, 1f);
            Color noseColor = new Color(1f, 0.62f, 0.18f, 1f);
            Color finColor = new Color(0.72f, 0.18f, 0.1f, 1f);

            AddPixel(
                root,
                "Missile Body",
                pixelSprite,
                Vector2.zero,
                new Vector2(bodyLength, bodyWidth),
                0f,
                bodyColor,
                100);
            AddPixel(
                root,
                "Missile Nose",
                pixelSprite,
                new Vector2(bodyLength * 0.5f, 0f),
                new Vector2(bodyWidth * 0.9f, bodyWidth * 0.9f),
                45f,
                noseColor,
                101);
            AddPixel(
                root,
                "Missile Fin Upper",
                pixelSprite,
                new Vector2(-bodyLength * 0.38f, bodyWidth * 0.58f),
                new Vector2(radius * 0.95f, radius * 0.5f),
                22f,
                finColor,
                99);
            AddPixel(
                root,
                "Missile Fin Lower",
                pixelSprite,
                new Vector2(-bodyLength * 0.38f, -bodyWidth * 0.58f),
                new Vector2(radius * 0.95f, radius * 0.5f),
                -22f,
                finColor,
                99);
            AddPixel(
                root,
                "Missile Flame",
                pixelSprite,
                new Vector2(-bodyLength * 0.67f, 0f),
                new Vector2(radius * 1.25f, radius * 0.72f),
                0f,
                new Color(1f, 0.86f, 0.2f, 0.92f),
                98);
        }

        private static void DisableRenderer(Transform value)
        {
            if (value == null) return;
            SpriteRenderer renderer = value.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        private static void AddPixel(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            float rotationDegrees,
            Color color,
            int sortingOrder)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition =
                new Vector3(position.x, position.y, 0f);
            part.transform.localRotation =
                Quaternion.Euler(0f, 0f, rotationDegrees);
            part.transform.localScale =
                new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }
    }
}

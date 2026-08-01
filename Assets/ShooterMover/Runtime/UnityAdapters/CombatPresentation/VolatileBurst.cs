using System;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    [DisallowMultipleComponent]
    public sealed class VolatileBurst : MonoBehaviour
    {
        private const int RingPoints = 56;
        private const int StarPoints = 20;
        private const float DurationSeconds = 0.42f;

        private LineRenderer ring;
        private LineRenderer star;
        private Vector2 center;
        private float radius;
        private float startedAt;
        private bool configured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Enemy.VolatileExploded -= OnExplosion;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Enemy.VolatileExploded -= OnExplosion;
            Enemy.VolatileExploded += OnExplosion;
        }

        private static void OnExplosion(Enemy source, VolatileBlast blast)
        {
            if (source == null || blast == null) return;
            try
            {
                Spawn(blast, source);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogException(exception, source);
            }
        }

        public static VolatileBurst Spawn(VolatileBlast blast)
        {
            return Spawn(blast, null);
        }

        private static VolatileBurst Spawn(
            VolatileBlast blast,
            Enemy source)
        {
            if (blast == null) throw new ArgumentNullException(nameof(blast));
            ReadSorting(source, out int sortingLayerId, out int sortingOrder);
            GameObject burstObject = new GameObject("Volatile Burst");
            VolatileBurst burst = burstObject.AddComponent<VolatileBurst>();
            burst.Configure(
                blast.Position,
                (float)blast.Radius,
                sortingLayerId,
                sortingOrder);
            return burst;
        }

        private void Configure(
            Vector2 position,
            float maxRadius,
            int sortingLayerId,
            int sortingOrder)
        {
            if (maxRadius <= 0f
                || float.IsNaN(maxRadius)
                || float.IsInfinity(maxRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(maxRadius));
            }

            center = position;
            radius = maxRadius;
            transform.position = new Vector3(center.x, center.y, 0f);
            ring = CreateLine(
                "Burst Ring",
                RingPoints,
                0.09f,
                sortingLayerId,
                sortingOrder);
            star = CreateLine(
                "Burst Star",
                StarPoints,
                0.055f,
                sortingLayerId,
                sortingOrder + 1);
            startedAt = Time.unscaledTime;
            configured = true;
            Tick(0f);
        }

        private void Update()
        {
            if (!configured) return;
            float progress = Mathf.Clamp01(
                (Time.unscaledTime - startedAt) / DurationSeconds);
            Tick(progress);
            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void Tick(float progress)
        {
            float eased = 1f - (1f - progress) * (1f - progress);
            float alpha = 1f - progress;
            Color ringColor = new Color(1f, 0.12f, 0.03f, alpha * 0.9f);
            Color starColor = new Color(1f, 0.68f, 0.12f, alpha);

            SetCircle(
                ring,
                center,
                Mathf.Lerp(radius * 0.12f, radius, eased));
            SetStar(
                star,
                center,
                Mathf.Lerp(radius * 0.08f, radius * 0.82f, eased),
                Time.unscaledTime * 220f);

            ring.startColor = ringColor;
            ring.endColor = ringColor;
            star.startColor = starColor;
            star.endColor = starColor;
            ring.startWidth = ring.endWidth = Mathf.Lerp(0.13f, 0.02f, progress);
            star.startWidth = star.endWidth = Mathf.Lerp(0.08f, 0.01f, progress);
        }

        private LineRenderer CreateLine(
            string name,
            int points,
            float width,
            int sortingLayerId,
            int sortingOrder)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = TraitMaterial.Get();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = points;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sortingLayerID = sortingLayerId;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private static void ReadSorting(
            Enemy source,
            out int sortingLayerId,
            out int sortingOrder)
        {
            sortingLayerId = 0;
            sortingOrder = 500;
            if (source == null) return;

            SpriteRenderer[] renderers =
                source.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0) return;

            sortingLayerId = renderers[0].sortingLayerID;
            int highest = renderers[0].sortingOrder;
            for (int index = 1; index < renderers.Length; index++)
            {
                if (renderers[index].sortingLayerID == sortingLayerId)
                {
                    highest = Mathf.Max(highest, renderers[index].sortingOrder);
                }
            }
            sortingOrder = highest + 50;
        }

        private static void SetCircle(
            LineRenderer line,
            Vector2 origin,
            float value)
        {
            int count = line.positionCount;
            for (int index = 0; index < count; index++)
            {
                float angle = Mathf.PI * 2f * index / count;
                line.SetPosition(
                    index,
                    new Vector3(
                        origin.x + Mathf.Cos(angle) * value,
                        origin.y + Mathf.Sin(angle) * value,
                        0f));
            }
        }

        private static void SetStar(
            LineRenderer line,
            Vector2 origin,
            float value,
            float rotationDegrees)
        {
            int count = line.positionCount;
            float rotation = rotationDegrees * Mathf.Deg2Rad;
            for (int index = 0; index < count; index++)
            {
                float angle = rotation + Mathf.PI * 2f * index / count;
                float pointRadius = index % 2 == 0 ? value : value * 0.38f;
                line.SetPosition(
                    index,
                    new Vector3(
                        origin.x + Mathf.Cos(angle) * pointRadius,
                        origin.y + Mathf.Sin(angle) * pointRadius,
                        0f));
            }
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }
}

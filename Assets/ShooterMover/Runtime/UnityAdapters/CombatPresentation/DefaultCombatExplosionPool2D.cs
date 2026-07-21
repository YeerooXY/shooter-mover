using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    /// <summary>
    /// Bounded presentation-only pool for the existing default orange ring explosion.
    /// It owns no damage, hit, room, reward, or persistence behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefaultCombatExplosionPool2D : MonoBehaviour
    {
        public const int DefaultCapacity = 12;
        public const float DefaultLifetimeSeconds = 0.18f;
        public const float DefaultRadius = 0.7f;
        public const float DefaultLineWidth = 0.14f;
        public const int DefaultPointCount = 24;

        [SerializeField] private int capacity = DefaultCapacity;
        [SerializeField] private float lifetimeSeconds = DefaultLifetimeSeconds;

        private readonly List<DefaultCombatExplosionInstance2D> instances =
            new List<DefaultCombatExplosionInstance2D>();
        private Material material;
        private int recycleCursor;
        private long spawnSequence;

        public int Capacity { get { return capacity; } }
        public int TotalSpawnCount { get; private set; }
        public float LastSpawnScale { get; private set; }
        public Vector3 LastSpawnPosition { get; private set; }
        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < instances.Count; index++)
                {
                    if (instances[index] != null && instances[index].IsActive) count++;
                }
                return count;
            }
        }

        public void ConfigureForTests(int configuredCapacity, float configuredLifetimeSeconds)
        {
            if (configuredCapacity < 1 || configuredCapacity > 128)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredCapacity));
            }
            if (!IsFinitePositive(configuredLifetimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(configuredLifetimeSeconds));
            }
            if (instances.Count > 0)
            {
                throw new InvalidOperationException("Explosion pool configuration is frozen after first use.");
            }
            capacity = configuredCapacity;
            lifetimeSeconds = configuredLifetimeSeconds;
        }

        public DefaultCombatExplosionInstance2D Spawn(Vector3 position, float scale)
        {
            if (!IsFinite(position) || !IsFinitePositive(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale));
            }

            DefaultCombatExplosionInstance2D instance = Acquire();
            spawnSequence++;
            instance.Activate(position, scale, lifetimeSeconds, spawnSequence);
            TotalSpawnCount++;
            LastSpawnScale = scale;
            LastSpawnPosition = position;
            return instance;
        }

        private DefaultCombatExplosionInstance2D Acquire()
        {
            for (int index = 0; index < instances.Count; index++)
            {
                if (instances[index] != null && !instances[index].IsActive)
                {
                    return instances[index];
                }
            }

            if (instances.Count < capacity)
            {
                DefaultCombatExplosionInstance2D created = CreateInstance(instances.Count);
                instances.Add(created);
                return created;
            }

            if (recycleCursor >= instances.Count) recycleCursor = 0;
            DefaultCombatExplosionInstance2D recycled = instances[recycleCursor];
            recycleCursor = (recycleCursor + 1) % instances.Count;
            return recycled;
        }

        private DefaultCombatExplosionInstance2D CreateInstance(int ordinal)
        {
            EnsureMaterial();
            GameObject visual = new GameObject("DefaultCombatExplosion_" + ordinal);
            visual.transform.SetParent(transform, false);
            visual.AddComponent<CombatPresentationGeneratedVisual2D>();
            LineRenderer ring = visual.AddComponent<LineRenderer>();
            ring.sharedMaterial = material;
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = DefaultPointCount;
            ring.startWidth = DefaultLineWidth;
            ring.endWidth = DefaultLineWidth;
            ring.startColor = new Color(1f, 0.35f, 0.05f, 0.9f);
            ring.endColor = ring.startColor;
            ring.sortingOrder = 69;
            for (int index = 0; index < DefaultPointCount; index++)
            {
                float angle = index * Mathf.PI * 2f / DefaultPointCount;
                ring.SetPosition(
                    index,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * DefaultRadius);
            }
            DefaultCombatExplosionInstance2D instance =
                visual.AddComponent<DefaultCombatExplosionInstance2D>();
            instance.Configure(ring);
            return instance;
        }

        private void EnsureMaterial()
        {
            if (material != null) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                throw new InvalidOperationException("No compatible default explosion shader is available.");
            }
            material = new Material(shader)
            {
                name = "Default Combat Explosion Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
                material = null;
            }
            instances.Clear();
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }
    }

    [DisallowMultipleComponent]
    public sealed class DefaultCombatExplosionInstance2D : MonoBehaviour
    {
        private LineRenderer ring;
        private float deactivateAt;

        public bool IsActive { get; private set; }
        public long SpawnSequence { get; private set; }

        public void Configure(LineRenderer configuredRing)
        {
            ring = configuredRing ?? throw new ArgumentNullException(nameof(configuredRing));
            ring.enabled = false;
            gameObject.SetActive(false);
        }

        public void Activate(
            Vector3 position,
            float scale,
            float lifetimeSeconds,
            long spawnSequence)
        {
            transform.position = position;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * scale;
            SpawnSequence = spawnSequence;
            deactivateAt = Time.time + Mathf.Max(0.01f, lifetimeSeconds);
            gameObject.SetActive(true);
            ring.enabled = true;
            IsActive = true;
        }

        private void Update()
        {
            if (IsActive && Time.time >= deactivateAt)
            {
                IsActive = false;
                if (ring != null) ring.enabled = false;
                gameObject.SetActive(false);
            }
        }
    }
}

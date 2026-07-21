using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    public interface ICombatDeathVfxInstance2D
    {
        bool IsActive { get; }
        GameObject Root { get; }
        void Activate(Vector3 worldPosition, float scale, long spawnSequence);
        void Recycle();
    }

    public interface ICombatDeathVfxFactory2D : IDisposable
    {
        string SourcePresentationId { get; }
        ICombatDeathVfxInstance2D Create(Transform parent, int ordinal);
    }

    /// <summary>
    /// Bounded presentation-only pool. Appearance, frames, timing and sorting are supplied
    /// by an injected factory; the pool owns no combat or effect-authority behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatDeathVfxPool2D : MonoBehaviour
    {
        public const int DefaultCapacity = 12;

        private readonly List<ICombatDeathVfxInstance2D> instances =
            new List<ICombatDeathVfxInstance2D>();
        private ICombatDeathVfxFactory2D factory;
        private int capacity;
        private int recycleCursor;
        private long spawnSequence;
        private bool configured;

        public int Capacity { get { return capacity; } }
        public int TotalSpawnCount { get; private set; }
        public float LastSpawnScale { get; private set; }
        public Vector3 LastSpawnPosition { get; private set; }
        public string SourcePresentationId
        {
            get { return factory == null ? string.Empty : factory.SourcePresentationId; }
        }
        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < instances.Count; index++)
                {
                    ICombatDeathVfxInstance2D instance = instances[index];
                    if (instance != null && instance.Root != null && instance.IsActive)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public void Configure(
            ICombatDeathVfxFactory2D configuredFactory,
            int configuredCapacity = DefaultCapacity)
        {
            if (configuredFactory == null)
            {
                throw new ArgumentNullException(nameof(configuredFactory));
            }
            if (configuredCapacity < 1 || configuredCapacity > 128)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredCapacity));
            }
            if (configured)
            {
                if (!object.ReferenceEquals(factory, configuredFactory)
                    || capacity != configuredCapacity)
                {
                    throw new InvalidOperationException(
                        "A combat death VFX pool cannot be rebound to another factory.");
                }
                return;
            }

            factory = configuredFactory;
            capacity = configuredCapacity;
            configured = true;
        }

        public ICombatDeathVfxInstance2D Spawn(Vector3 position, float scale)
        {
            if (!configured || factory == null)
            {
                throw new InvalidOperationException(
                    "The combat death VFX pool requires an injected presentation factory.");
            }
            if (!IsFinite(position) || !IsFinitePositive(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale));
            }

            ICombatDeathVfxInstance2D instance = Acquire();
            spawnSequence++;
            instance.Activate(position, scale, spawnSequence);
            TotalSpawnCount++;
            LastSpawnScale = scale;
            LastSpawnPosition = position;
            return instance;
        }

        private ICombatDeathVfxInstance2D Acquire()
        {
            for (int index = 0; index < instances.Count; index++)
            {
                ICombatDeathVfxInstance2D existing = instances[index];
                if (existing != null
                    && existing.Root != null
                    && !existing.IsActive)
                {
                    return existing;
                }
            }

            if (instances.Count < capacity)
            {
                ICombatDeathVfxInstance2D created = factory.Create(
                    transform,
                    instances.Count);
                if (created == null || created.Root == null)
                {
                    throw new InvalidOperationException(
                        "The combat death VFX factory returned no reusable instance.");
                }
                instances.Add(created);
                return created;
            }

            if (recycleCursor >= instances.Count) recycleCursor = 0;
            ICombatDeathVfxInstance2D recycled = instances[recycleCursor];
            recycleCursor = (recycleCursor + 1) % instances.Count;
            recycled.Recycle();
            return recycled;
        }

        private void OnDestroy()
        {
            for (int index = 0; index < instances.Count; index++)
            {
                ICombatDeathVfxInstance2D instance = instances[index];
                if (instance != null)
                {
                    instance.Recycle();
                }
            }
            instances.Clear();
            if (factory != null)
            {
                factory.Dispose();
                factory = null;
            }
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

    public sealed class SpriteAnimationCombatDeathVfxDefinitionV1
    {
        private readonly Sprite[] frames;

        public SpriteAnimationCombatDeathVfxDefinitionV1(
            string sourcePresentationId,
            Sprite[] configuredFrames,
            float secondsPerFrame,
            Vector2 localOffset,
            Vector2 visualScale,
            int sortingOrder,
            bool useUnscaledTime)
        {
            if (string.IsNullOrWhiteSpace(sourcePresentationId))
            {
                throw new ArgumentException(
                    "A death VFX presentation source ID is required.",
                    nameof(sourcePresentationId));
            }
            if (configuredFrames == null)
            {
                throw new ArgumentNullException(nameof(configuredFrames));
            }
            if (!IsFinitePositive(secondsPerFrame)
                || !IsFinitePositive(visualScale.x)
                || !IsFinitePositive(visualScale.y)
                || !IsFinite(localOffset.x)
                || !IsFinite(localOffset.y))
            {
                throw new ArgumentOutOfRangeException(nameof(secondsPerFrame));
            }

            SourcePresentationId = sourcePresentationId.Trim();
            frames = (Sprite[])configuredFrames.Clone();
            SecondsPerFrame = secondsPerFrame;
            LocalOffset = localOffset;
            VisualScale = visualScale;
            SortingOrder = sortingOrder;
            UseUnscaledTime = useUnscaledTime;
        }

        public string SourcePresentationId { get; }
        public int FrameCount { get { return frames.Length; } }
        public float SecondsPerFrame { get; }
        public Vector2 LocalOffset { get; }
        public Vector2 VisualScale { get; }
        public int SortingOrder { get; }
        public bool UseUnscaledTime { get; }
        public bool HasFrames
        {
            get
            {
                for (int index = 0; index < frames.Length; index++)
                {
                    if (frames[index] != null) return true;
                }
                return false;
            }
        }

        public Sprite GetFrame(int index)
        {
            return index < 0 || index >= frames.Length ? null : frames[index];
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

    /// <summary>
    /// Factory for an injected retained sprite animation. The fallback is used only when the
    /// retained animation currently has no authored frames.
    /// </summary>
    public sealed class SpriteAnimationCombatDeathVfxFactory2D : ICombatDeathVfxFactory2D
    {
        private readonly SpriteAnimationCombatDeathVfxDefinitionV1 definition;
        private readonly ICombatDeathVfxFactory2D fallback;

        public SpriteAnimationCombatDeathVfxFactory2D(
            SpriteAnimationCombatDeathVfxDefinitionV1 definition,
            ICombatDeathVfxFactory2D fallback)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        public string SourcePresentationId { get { return definition.SourcePresentationId; } }
        public bool UsesRetainedFrames { get { return definition.HasFrames; } }

        public ICombatDeathVfxInstance2D Create(Transform parent, int ordinal)
        {
            if (!definition.HasFrames)
            {
                return fallback.Create(parent, ordinal);
            }

            GameObject root = new GameObject("CombatDeathVfx_Retained_" + ordinal);
            root.transform.SetParent(parent, false);
            root.AddComponent<CombatPresentationGeneratedVisual2D>();
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            SpriteAnimationCombatDeathVfxInstance2D instance =
                root.AddComponent<SpriteAnimationCombatDeathVfxInstance2D>();
            instance.Configure(definition, renderer);
            return instance;
        }

        public void Dispose()
        {
            fallback.Dispose();
        }
    }

    [DisallowMultipleComponent]
    public sealed class SpriteAnimationCombatDeathVfxInstance2D :
        MonoBehaviour,
        ICombatDeathVfxInstance2D
    {
        private SpriteAnimationCombatDeathVfxDefinitionV1 definition;
        private SpriteRenderer rendererComponent;
        private int frameIndex;
        private float frameElapsed;

        public bool IsActive { get; private set; }
        public GameObject Root { get { return gameObject; } }
        public long SpawnSequence { get; private set; }

        public void Configure(
            SpriteAnimationCombatDeathVfxDefinitionV1 configuredDefinition,
            SpriteRenderer configuredRenderer)
        {
            definition = configuredDefinition
                ?? throw new ArgumentNullException(nameof(configuredDefinition));
            rendererComponent = configuredRenderer
                ?? throw new ArgumentNullException(nameof(configuredRenderer));
            rendererComponent.sortingOrder = definition.SortingOrder;
            rendererComponent.enabled = false;
            gameObject.SetActive(false);
        }

        public void Activate(Vector3 worldPosition, float scale, long spawnSequence)
        {
            if (definition == null || rendererComponent == null || !definition.HasFrames)
            {
                throw new InvalidOperationException(
                    "A retained sprite death VFX instance requires authored frames.");
            }

            transform.position = worldPosition + (Vector3)definition.LocalOffset;
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(
                definition.VisualScale.x * scale,
                definition.VisualScale.y * scale,
                scale);
            SpawnSequence = spawnSequence;
            frameIndex = 0;
            frameElapsed = 0f;
            gameObject.SetActive(true);
            rendererComponent.enabled = true;
            rendererComponent.sprite = definition.GetFrame(frameIndex);
            IsActive = true;
        }

        public void Recycle()
        {
            IsActive = false;
            frameIndex = 0;
            frameElapsed = 0f;
            if (rendererComponent != null)
            {
                rendererComponent.enabled = false;
                rendererComponent.sprite = null;
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!IsActive || definition == null)
            {
                return;
            }

            frameElapsed += definition.UseUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            while (IsActive && frameElapsed >= definition.SecondsPerFrame)
            {
                frameElapsed -= definition.SecondsPerFrame;
                frameIndex++;
                if (frameIndex >= definition.FrameCount)
                {
                    Recycle();
                    return;
                }
                rendererComponent.sprite = definition.GetFrame(frameIndex);
            }
        }
    }

    /// <summary>Explicit procedural fallback used only when no retained frames are authored.</summary>
    public sealed class FallbackRingCombatDeathVfxFactory2D : ICombatDeathVfxFactory2D
    {
        public const float DefaultLifetimeSeconds = 0.18f;
        public const float DefaultRadius = 0.7f;
        public const float DefaultLineWidth = 0.14f;
        public const int DefaultPointCount = 24;

        private readonly float lifetimeSeconds;
        private Material material;

        public FallbackRingCombatDeathVfxFactory2D(
            float configuredLifetimeSeconds = DefaultLifetimeSeconds)
        {
            if (float.IsNaN(configuredLifetimeSeconds)
                || float.IsInfinity(configuredLifetimeSeconds)
                || configuredLifetimeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredLifetimeSeconds));
            }
            lifetimeSeconds = configuredLifetimeSeconds;
        }

        public string SourcePresentationId { get { return "fallback.procedural-ring"; } }

        public ICombatDeathVfxInstance2D Create(Transform parent, int ordinal)
        {
            EnsureMaterial();
            GameObject root = new GameObject("CombatDeathVfx_FallbackRing_" + ordinal);
            root.transform.SetParent(parent, false);
            root.AddComponent<CombatPresentationGeneratedVisual2D>();
            LineRenderer ring = root.AddComponent<LineRenderer>();
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

            FallbackRingCombatDeathVfxInstance2D instance =
                root.AddComponent<FallbackRingCombatDeathVfxInstance2D>();
            instance.Configure(ring, lifetimeSeconds);
            return instance;
        }

        public void Dispose()
        {
            if (material == null) return;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(material);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
            material = null;
        }

        private void EnsureMaterial()
        {
            if (material != null) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No compatible fallback explosion shader is available.");
            }
            material = new Material(shader)
            {
                name = "Fallback Combat Death VFX Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class FallbackRingCombatDeathVfxInstance2D :
        MonoBehaviour,
        ICombatDeathVfxInstance2D
    {
        private LineRenderer ring;
        private float lifetimeSeconds;
        private float deactivateAt;

        public bool IsActive { get; private set; }
        public GameObject Root { get { return gameObject; } }
        public long SpawnSequence { get; private set; }

        public void Configure(LineRenderer configuredRing, float configuredLifetimeSeconds)
        {
            ring = configuredRing ?? throw new ArgumentNullException(nameof(configuredRing));
            lifetimeSeconds = configuredLifetimeSeconds;
            ring.enabled = false;
            gameObject.SetActive(false);
        }

        public void Activate(Vector3 worldPosition, float scale, long spawnSequence)
        {
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * scale;
            SpawnSequence = spawnSequence;
            deactivateAt = Time.time + Mathf.Max(0.01f, lifetimeSeconds);
            gameObject.SetActive(true);
            ring.enabled = true;
            IsActive = true;
        }

        public void Recycle()
        {
            IsActive = false;
            if (ring != null) ring.enabled = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsActive && Time.time >= deactivateAt)
            {
                Recycle();
            }
        }
    }
}

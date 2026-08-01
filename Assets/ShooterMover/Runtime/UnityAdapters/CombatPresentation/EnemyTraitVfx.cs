using System;
using System.Collections.Generic;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    public sealed class EnemyTraitVfxStyle
    {
        public EnemyTraitVfxStyle(
            Color color,
            float radiusScale,
            float width,
            float spinDegreesPerSecond,
            float pulseAmount,
            float pulseSpeed,
            int orbiters)
        {
            if (!IsFinite(radiusScale)
                || !IsFinite(width)
                || !IsFinite(spinDegreesPerSecond)
                || !IsFinite(pulseAmount)
                || !IsFinite(pulseSpeed)
                || radiusScale <= 0f
                || width <= 0f
                || pulseAmount < 0f
                || pulseSpeed < 0f
                || orbiters < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            Color = color;
            RadiusScale = radiusScale;
            Width = width;
            SpinDegreesPerSecond = spinDegreesPerSecond;
            PulseAmount = pulseAmount;
            PulseSpeed = pulseSpeed;
            Orbiters = orbiters;
        }

        public Color Color { get; }
        public float RadiusScale { get; }
        public float Width { get; }
        public float SpinDegreesPerSecond { get; }
        public float PulseAmount { get; }
        public float PulseSpeed { get; }
        public int Orbiters { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Presentation-only trait style registry. Adding a visual for a future trait requires one
    /// registration and no change to the enemy actor or the stackable presenter.
    /// </summary>
    public static class EnemyTraitVfxCatalog
    {
        private static readonly Dictionary<EnemyTrait, EnemyTraitVfxStyle> styles =
            new Dictionary<EnemyTrait, EnemyTraitVfxStyle>();

        static EnemyTraitVfxCatalog()
        {
            Register(
                EnemyTrait.EnergyShielded,
                new EnemyTraitVfxStyle(
                    new Color(0.15f, 0.82f, 1f, 0.9f),
                    1f,
                    0.045f,
                    55f,
                    0.07f,
                    3.4f,
                    3));
            Register(
                EnemyTrait.Fortified,
                new EnemyTraitVfxStyle(
                    new Color(0.78f, 0.82f, 0.88f, 0.92f),
                    1.1f,
                    0.075f,
                    -18f,
                    0.025f,
                    2f,
                    4));
            Register(
                EnemyTrait.Golden,
                new EnemyTraitVfxStyle(
                    new Color(1f, 0.68f, 0.08f, 0.96f),
                    1.22f,
                    0.05f,
                    34f,
                    0.1f,
                    4f,
                    5));
            Register(
                EnemyTrait.Swift,
                new EnemyTraitVfxStyle(
                    new Color(0.45f, 0.9f, 1f, 0.82f),
                    0.92f,
                    0.032f,
                    190f,
                    0.045f,
                    5.5f,
                    2));
            Register(
                EnemyTrait.Overclocked,
                new EnemyTraitVfxStyle(
                    new Color(1f, 0.3f, 0.06f, 0.9f),
                    1.06f,
                    0.045f,
                    -135f,
                    0.12f,
                    6f,
                    3));
            Register(
                EnemyTrait.Volatile,
                new EnemyTraitVfxStyle(
                    new Color(1f, 0.08f, 0.04f, 0.94f),
                    1.16f,
                    0.065f,
                    12f,
                    0.18f,
                    7f,
                    1));
        }

        public static void Register(EnemyTrait trait, EnemyTraitVfxStyle style)
        {
            if (!Enum.IsDefined(typeof(EnemyTrait), trait))
                throw new ArgumentOutOfRangeException(nameof(trait));
            if (style == null) throw new ArgumentNullException(nameof(style));
            if (styles.ContainsKey(trait))
            {
                throw new InvalidOperationException(
                    "Enemy trait VFX style is already registered: " + trait + ".");
            }
            styles.Add(trait, style);
        }

        public static bool TryGet(
            EnemyTrait trait,
            out EnemyTraitVfxStyle style)
        {
            return styles.TryGetValue(trait, out style);
        }
    }

    [DisallowMultipleComponent]
    public sealed class EnemyTraitVfx : MonoBehaviour
    {
        private const int RingPoints = 48;
        private const int OrbiterPoints = 10;
        private const float LaneSpacing = 0.11f;

        private sealed class Layer
        {
            private readonly EnemyTraitVfxStyle style;
            private readonly float radius;
            private readonly float phase;
            private readonly LineRenderer ring;
            private readonly List<LineRenderer> orbiters;
            private readonly GameObject root;

            public Layer(
                Transform owner,
                EnemyTrait trait,
                EnemyTraitVfxStyle style,
                float bodyRadius,
                int lane,
                int sortingLayerId,
                int sortingOrder)
            {
                this.style = style;
                radius = bodyRadius * style.RadiusScale + lane * LaneSpacing;
                phase = lane * 1.371f + (int)trait * 0.617f;

                root = new GameObject("Trait VFX - " + trait);
                root.transform.SetParent(owner, false);
                ring = CreateLine(
                    root.transform,
                    "Ring",
                    RingPoints,
                    style.Width,
                    style.Color,
                    sortingLayerId,
                    sortingOrder);

                orbiters = new List<LineRenderer>(style.Orbiters);
                for (int index = 0; index < style.Orbiters; index++)
                {
                    orbiters.Add(CreateLine(
                        root.transform,
                        "Orbiter " + (index + 1),
                        OrbiterPoints,
                        style.Width * 0.72f,
                        style.Color,
                        sortingLayerId,
                        sortingOrder + 1));
                }
            }

            public void Tick(Vector2 center, float time)
            {
                float pulse = 1f + Mathf.Sin(
                    time * style.PulseSpeed + phase) * style.PulseAmount;
                float currentRadius = radius * pulse;
                SetCircle(ring, center, currentRadius, 0f);

                float brightness = 0.78f + 0.22f * Mathf.Sin(
                    time * style.PulseSpeed + phase);
                Color color = style.Color;
                color.a *= Mathf.Clamp01(brightness);
                ring.startColor = color;
                ring.endColor = color;

                if (orbiters.Count == 0) return;
                float rotation = time * style.SpinDegreesPerSecond + phase * 57.29578f;
                float orbiterRadius = Mathf.Max(
                    style.Width * 1.65f,
                    currentRadius * 0.055f);
                for (int index = 0; index < orbiters.Count; index++)
                {
                    float angle = rotation + 360f * index / orbiters.Count;
                    float radians = angle * Mathf.Deg2Rad;
                    Vector2 orbitCenter = center + new Vector2(
                        Mathf.Cos(radians),
                        Mathf.Sin(radians)) * currentRadius;
                    SetCircle(
                        orbiters[index],
                        orbitCenter,
                        orbiterRadius,
                        -angle);
                    orbiters[index].startColor = color;
                    orbiters[index].endColor = color;
                }
            }

            public void Dispose()
            {
                if (root == null) return;
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
            }

            private static LineRenderer CreateLine(
                Transform parent,
                string name,
                int points,
                float width,
                Color color,
                int sortingLayerId,
                int sortingOrder)
            {
                GameObject lineObject = new GameObject(name);
                lineObject.transform.SetParent(parent, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.sharedMaterial = EnemyTraitVfxMaterial.Get();
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = points;
                line.startWidth = width;
                line.endWidth = width;
                line.startColor = color;
                line.endColor = color;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.sortingLayerID = sortingLayerId;
                line.sortingOrder = sortingOrder;
                return line;
            }

            private static void SetCircle(
                LineRenderer line,
                Vector2 center,
                float radius,
                float rotationDegrees)
            {
                int count = line.positionCount;
                float rotation = rotationDegrees * Mathf.Deg2Rad;
                for (int index = 0; index < count; index++)
                {
                    float angle = rotation + Mathf.PI * 2f * index / count;
                    line.SetPosition(
                        index,
                        new Vector3(
                            center.x + Mathf.Cos(angle) * radius,
                            center.y + Mathf.Sin(angle) * radius,
                            0f));
                }
            }
        }

        private readonly List<Layer> layers = new List<Layer>();
        private Enemy enemy;
        private float bodyRadius;
        private int traitHash;

        public int LayerCount { get { return layers.Count; } }

        public static EnemyTraitVfx Attach(Enemy enemy)
        {
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            EnemyTraitVfx view = enemy.GetComponent<EnemyTraitVfx>();
            if (view == null)
            {
                view = enemy.gameObject.AddComponent<EnemyTraitVfx>();
            }
            view.Bind(enemy);
            return view;
        }

        public void Bind(Enemy value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.gameObject != gameObject)
            {
                throw new ArgumentException(
                    "Enemy trait VFX must live on the enemy root.",
                    nameof(value));
            }

            enemy = value;
            enabled = true;
            if (bodyRadius <= 0f)
            {
                float size = EnemyBounds.MeasureLargestDimension(transform);
                bodyRadius = Mathf.Max(0.35f, size * 0.55f);
            }
            Refresh();
        }

        public void Refresh()
        {
            if (enemy == null || !enemy.IsBound || enemy.Runtime == null)
            {
                Clear();
                return;
            }

            IReadOnlyList<EnemyTrait> traits = enemy.Runtime.Traits;
            int nextHash = Hash(traits);
            if (nextHash == traitHash && layers.Count == traits.Count)
            {
                return;
            }

            ClearLayers();
            int sortingLayerId;
            int sortingOrder;
            ResolveSorting(out sortingLayerId, out sortingOrder);
            for (int index = 0; index < traits.Count; index++)
            {
                EnemyTrait trait = traits[index];
                EnemyTraitVfxStyle style;
                if (!EnemyTraitVfxCatalog.TryGet(trait, out style))
                {
                    Debug.LogWarning(
                        "enemy-trait-vfx-style-missing:" + trait,
                        enemy);
                    continue;
                }
                layers.Add(new Layer(
                    transform,
                    trait,
                    style,
                    bodyRadius,
                    index,
                    sortingLayerId,
                    sortingOrder + index * 2));
            }
            traitHash = nextHash;
        }

        public void Clear()
        {
            ClearLayers();
            traitHash = 0;
            enemy = null;
            enabled = false;
        }

        private void LateUpdate()
        {
            if (enemy == null || !enemy.IsBound || enemy.Runtime == null)
            {
                Clear();
                return;
            }

            IReadOnlyList<EnemyTrait> traits = enemy.Runtime.Traits;
            if (Hash(traits) != traitHash)
            {
                Refresh();
            }

            Vector2 center = transform.position;
            float time = Time.time;
            for (int index = 0; index < layers.Count; index++)
            {
                layers[index].Tick(center, time);
            }
        }

        private void OnDestroy()
        {
            ClearLayers();
        }

        private void ClearLayers()
        {
            for (int index = 0; index < layers.Count; index++)
            {
                layers[index].Dispose();
            }
            layers.Clear();
        }

        private void ResolveSorting(
            out int sortingLayerId,
            out int sortingOrder)
        {
            sortingLayerId = 0;
            sortingOrder = 20;
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
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
            sortingOrder = highest + 10;
        }

        private static int Hash(IReadOnlyList<EnemyTrait> traits)
        {
            unchecked
            {
                int hash = 17;
                for (int index = 0; index < traits.Count; index++)
                {
                    hash = hash * 31 + (int)traits[index];
                }
                return hash;
            }
        }
    }

    internal static class EnemyTraitVfxMaterial
    {
        private static Material material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
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

        public static Material Get()
        {
            if (material != null) return material;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No compatible enemy trait VFX shader is available.");
            }
            material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Enemy Trait VFX Material",
            };
            return material;
        }
    }
}

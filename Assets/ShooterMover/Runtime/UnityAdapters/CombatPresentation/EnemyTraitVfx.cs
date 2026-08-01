using System;
using System.Collections.Generic;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    public sealed class TraitLook
    {
        public TraitLook(
            Color color,
            int sides,
            float radiusScale,
            float width,
            float spinSpeed,
            float pulseAmount,
            float pulseSpeed)
        {
            if (sides < 3
                || sides > 64
                || !IsFinite(radiusScale)
                || !IsFinite(width)
                || !IsFinite(spinSpeed)
                || !IsFinite(pulseAmount)
                || !IsFinite(pulseSpeed)
                || radiusScale <= 0f
                || width <= 0f
                || pulseAmount < 0f
                || pulseSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException();
            }

            Color = color;
            Sides = sides;
            RadiusScale = radiusScale;
            Width = width;
            SpinSpeed = spinSpeed;
            PulseAmount = pulseAmount;
            PulseSpeed = pulseSpeed;
        }

        public Color Color { get; }
        public int Sides { get; }
        public float RadiusScale { get; }
        public float Width { get; }
        public float SpinSpeed { get; }
        public float PulseAmount { get; }
        public float PulseSpeed { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>Visual settings for each enemy trait.</summary>
    public static class TraitLooks
    {
        private static readonly Dictionary<EnemyTrait, TraitLook> looks =
            new Dictionary<EnemyTrait, TraitLook>();

        static TraitLooks()
        {
            Add(
                EnemyTrait.EnergyShielded,
                new TraitLook(
                    new Color(0.15f, 0.82f, 1f, 0.9f),
                    32, 1f, 0.045f, 38f, 0.07f, 3.4f));
            Add(
                EnemyTrait.Fortified,
                new TraitLook(
                    new Color(0.78f, 0.82f, 0.88f, 0.92f),
                    4, 1.1f, 0.075f, -14f, 0.025f, 2f));
            Add(
                EnemyTrait.Golden,
                new TraitLook(
                    new Color(1f, 0.68f, 0.08f, 0.96f),
                    12, 1.22f, 0.05f, 28f, 0.1f, 4f));
            Add(
                EnemyTrait.Swift,
                new TraitLook(
                    new Color(0.45f, 0.9f, 1f, 0.82f),
                    3, 0.92f, 0.032f, 190f, 0.045f, 5.5f));
            Add(
                EnemyTrait.Overclocked,
                new TraitLook(
                    new Color(1f, 0.3f, 0.06f, 0.9f),
                    6, 1.06f, 0.045f, -135f, 0.12f, 6f));
            Add(
                EnemyTrait.Volatile,
                new TraitLook(
                    new Color(1f, 0.08f, 0.04f, 0.94f),
                    8, 1.16f, 0.065f, 10f, 0.18f, 7f));
        }

        public static void Add(EnemyTrait trait, TraitLook look)
        {
            if (!Enum.IsDefined(typeof(EnemyTrait), trait))
                throw new ArgumentOutOfRangeException(nameof(trait));
            if (look == null) throw new ArgumentNullException(nameof(look));
            if (looks.ContainsKey(trait))
            {
                throw new InvalidOperationException(
                    "Trait look already exists: " + trait + ".");
            }
            looks.Add(trait, look);
        }

        public static bool TryGet(EnemyTrait trait, out TraitLook look)
        {
            return looks.TryGetValue(trait, out look);
        }
    }

    [DisallowMultipleComponent]
    public sealed class EnemyTraitVfx : MonoBehaviour
    {
        private const float LaneSpacing = 0.11f;

        private sealed class Layer
        {
            private readonly TraitLook look;
            private readonly float radius;
            private readonly float phase;
            private readonly LineRenderer line;
            private readonly GameObject root;

            public Layer(
                Transform owner,
                EnemyTrait trait,
                TraitLook look,
                float bodyRadius,
                int lane,
                int sortingLayerId,
                int sortingOrder)
            {
                this.look = look;
                radius = bodyRadius * look.RadiusScale + lane * LaneSpacing;
                phase = lane * 1.371f + (int)trait * 0.617f;

                root = new GameObject("Trait VFX - " + trait);
                root.transform.SetParent(owner, false);
                line = root.AddComponent<LineRenderer>();
                line.sharedMaterial = TraitMaterial.Get();
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = look.Sides;
                line.startWidth = look.Width;
                line.endWidth = look.Width;
                line.startColor = look.Color;
                line.endColor = look.Color;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.sortingLayerID = sortingLayerId;
                line.sortingOrder = sortingOrder;
            }

            public void Tick(Vector2 center, float time)
            {
                float pulse = 1f + Mathf.Sin(
                    time * look.PulseSpeed + phase) * look.PulseAmount;
                float currentRadius = radius * pulse;
                float rotation = (
                    time * look.SpinSpeed
                    + phase * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                for (int index = 0; index < look.Sides; index++)
                {
                    float angle = rotation + Mathf.PI * 2f * index / look.Sides;
                    line.SetPosition(
                        index,
                        new Vector3(
                            center.x + Mathf.Cos(angle) * currentRadius,
                            center.y + Mathf.Sin(angle) * currentRadius,
                            0f));
                }

                Color color = look.Color;
                color.a *= 0.78f + 0.22f * Mathf.Sin(
                    time * look.PulseSpeed + phase);
                line.startColor = color;
                line.endColor = color;
            }

            public void Dispose()
            {
                if (root == null) return;
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
            }
        }

        private readonly List<Layer> layers = new List<Layer>();
        private Enemy enemy;
        private float bodyRadius;
        private int traitKey;

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
                bodyRadius = Mathf.Max(
                    0.35f,
                    EnemyBounds.MeasureLargestDimension(transform) * 0.55f);
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
            int nextKey = Key(traits);
            if (nextKey == traitKey) return;

            ClearLayers();
            int sortingLayerId;
            int sortingOrder;
            ReadSorting(out sortingLayerId, out sortingOrder);
            for (int index = 0; index < traits.Count; index++)
            {
                TraitLook look;
                if (!TraitLooks.TryGet(traits[index], out look))
                {
                    Debug.LogWarning(
                        "enemy-trait-vfx-look-missing:" + traits[index],
                        enemy);
                    continue;
                }
                layers.Add(new Layer(
                    transform,
                    traits[index],
                    look,
                    bodyRadius,
                    index,
                    sortingLayerId,
                    sortingOrder + index));
            }
            traitKey = nextKey;
        }

        public void Clear()
        {
            ClearLayers();
            traitKey = 0;
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
            if (Key(enemy.Runtime.Traits) != traitKey)
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

        private void ReadSorting(
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

        private static int Key(IReadOnlyList<EnemyTrait> traits)
        {
            unchecked
            {
                int key = 17;
                for (int index = 0; index < traits.Count; index++)
                {
                    key = key * 31 + (int)traits[index];
                }
                return key;
            }
        }
    }

    internal static class EnemyTraitVfxBinding
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Enemy.Bound -= OnBound;
            Enemy.TraitsChanged -= OnTraitsChanged;
            Enemy.Unbound -= OnUnbound;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Enemy.Bound -= OnBound;
            Enemy.TraitsChanged -= OnTraitsChanged;
            Enemy.Unbound -= OnUnbound;
            Enemy.Bound += OnBound;
            Enemy.TraitsChanged += OnTraitsChanged;
            Enemy.Unbound += OnUnbound;
        }

        private static void OnBound(Enemy enemy)
        {
            if (enemy != null) EnemyTraitVfx.Attach(enemy);
        }

        private static void OnTraitsChanged(Enemy enemy)
        {
            if (enemy == null) return;
            EnemyTraitVfx view = enemy.GetComponent<EnemyTraitVfx>();
            if (view != null) view.Refresh();
        }

        private static void OnUnbound(Enemy enemy)
        {
            if (enemy == null) return;
            EnemyTraitVfx view = enemy.GetComponent<EnemyTraitVfx>();
            if (view != null) view.Clear();
        }
    }

    internal static class TraitMaterial
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

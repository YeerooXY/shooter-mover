using System;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    public enum CombatHealthBarRefreshStatusV1
    {
        Applied = 1,
        Unchanged = 2,
        HiddenTerminal = 3,
        SourceUnavailable = 4,
        RejectedEntityMismatch = 5,
        RejectedStaleLifecycle = 6,
        NotConfigured = 7,
    }

    /// <summary>
    /// Reusable read-only world-space health bar. It retains only a typed snapshot source,
    /// never an authority mutation surface. Line renderers use world-space coordinates so
    /// the bar follows its transform while remaining upright.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHealthBarPresenter2D : MonoBehaviour
    {
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private float barWidth = 1.4f;
        [SerializeField] private float barHeight = 0.12f;
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        [SerializeField] private Color fillColor = new Color(0.2f, 0.95f, 0.3f, 1f);
        [SerializeField] private int sortingOrder = 80;

        private ICombatHealthBarSnapshotSourceV1 source;
        private StableId boundEntityStableId;
        private long observedLifecycleGeneration = -1L;
        private CombatHealthBarSnapshotV1 currentSnapshot;
        private LineRenderer background;
        private LineRenderer fill;
        private Material sharedMaterial;
        private bool configured;
        private int presentationUpdateCount;

        public StableId BoundEntityStableId { get { return boundEntityStableId; } }
        public CombatHealthBarSnapshotV1 CurrentSnapshot { get { return currentSnapshot; } }
        public bool IsVisible { get { return fill != null && fill.enabled; } }
        public int PresentationUpdateCount { get { return presentationUpdateCount; } }
        public bool HasPhysicsOwnership
        {
            get
            {
                return HasPhysics(background) || HasPhysics(fill);
            }
        }

        public void Configure(
            StableId entityInstanceStableId,
            ICombatHealthBarSnapshotSourceV1 snapshotSource,
            Vector3 configuredWorldOffset,
            float configuredWidth = 1.4f,
            float configuredHeight = 0.12f)
        {
            if (entityInstanceStableId == null)
            {
                throw new ArgumentNullException(nameof(entityInstanceStableId));
            }
            if (snapshotSource == null)
            {
                throw new ArgumentNullException(nameof(snapshotSource));
            }
            if (!IsFinite(configuredWorldOffset)
                || !IsFinitePositive(configuredWidth)
                || !IsFinitePositive(configuredHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(configuredWorldOffset));
            }
            if (configured
                && (boundEntityStableId != entityInstanceStableId
                    || !object.ReferenceEquals(source, snapshotSource)))
            {
                throw new InvalidOperationException(
                    "A combat health bar cannot be rebound to another entity or source.");
            }

            boundEntityStableId = entityInstanceStableId;
            source = snapshotSource;
            worldOffset = configuredWorldOffset;
            barWidth = configuredWidth;
            barHeight = configuredHeight;
            configured = true;
            EnsureView();
            Refresh();
        }

        public CombatHealthBarRefreshStatusV1 Refresh()
        {
            if (!configured || source == null || boundEntityStableId == null)
            {
                return CombatHealthBarRefreshStatusV1.NotConfigured;
            }

            CombatHealthBarSnapshotV1 snapshot;
            if (!source.TryRead(out snapshot) || snapshot == null)
            {
                Clear();
                return CombatHealthBarRefreshStatusV1.SourceUnavailable;
            }
            if (snapshot.EntityInstanceStableId != boundEntityStableId)
            {
                return CombatHealthBarRefreshStatusV1.RejectedEntityMismatch;
            }
            if (snapshot.LifecycleGeneration < observedLifecycleGeneration)
            {
                return CombatHealthBarRefreshStatusV1.RejectedStaleLifecycle;
            }
            if (snapshot.LifecycleGeneration > observedLifecycleGeneration)
            {
                observedLifecycleGeneration = snapshot.LifecycleGeneration;
                currentSnapshot = null;
            }
            if (snapshot.Equals(currentSnapshot))
            {
                return CombatHealthBarRefreshStatusV1.Unchanged;
            }

            currentSnapshot = snapshot;
            presentationUpdateCount++;
            if (snapshot.IsTerminal)
            {
                ClearViewOnly();
                return CombatHealthBarRefreshStatusV1.HiddenTerminal;
            }

            EnsureView();
            ApplyView(snapshot.NormalizedFill);
            return CombatHealthBarRefreshStatusV1.Applied;
        }

        public void Clear()
        {
            currentSnapshot = null;
            ClearViewOnly();
        }

        private void LateUpdate()
        {
            if (!configured)
            {
                return;
            }
            Refresh();
            if (IsVisible && currentSnapshot != null && currentSnapshot.IsAlive)
            {
                ApplyView(currentSnapshot.NormalizedFill);
            }
        }

        private void ApplyView(double normalizedFill)
        {
            Vector3 center = transform.position + worldOffset;
            float half = barWidth * 0.5f;
            Vector3 left = center + Vector3.left * half;
            Vector3 right = center + Vector3.right * half;
            float fraction = Mathf.Clamp01((float)normalizedFill);

            background.enabled = true;
            fill.enabled = true;
            background.SetPosition(0, left);
            background.SetPosition(1, right);
            fill.SetPosition(0, left);
            fill.SetPosition(1, Vector3.Lerp(left, right, fraction));
        }

        private void ClearViewOnly()
        {
            if (background != null) background.enabled = false;
            if (fill != null) fill.enabled = false;
        }

        private void EnsureView()
        {
            if (background != null && fill != null)
            {
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Hidden/Internal-Colored");
            }
            if (shader == null)
            {
                throw new InvalidOperationException("No compatible health-bar shader is available.");
            }

            sharedMaterial = new Material(shader)
            {
                name = "Combat Health Bar Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
            background = CreateLine("CombatHealthBarBackground", backgroundColor, sortingOrder);
            fill = CreateLine("CombatHealthBarFill", fillColor, sortingOrder + 1);
        }

        private LineRenderer CreateLine(string objectName, Color color, int order)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);
            lineObject.AddComponent<CombatPresentationGeneratedVisual2D>();
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = sharedMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = barHeight;
            line.endWidth = barHeight;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            line.numCapVertices = 2;
            line.enabled = false;
            return line;
        }

        private void OnDestroy()
        {
            if (sharedMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(sharedMaterial);
            }
            else
            {
                DestroyImmediate(sharedMaterial);
            }
            sharedMaterial = null;
        }

        private static bool HasPhysics(LineRenderer line)
        {
            return line != null
                && (line.GetComponent<Collider2D>() != null
                    || line.GetComponent<Rigidbody2D>() != null
                    || line.GetComponentInChildren<Collider2D>(true) != null
                    || line.GetComponentInChildren<Rigidbody2D>(true) != null);
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
    public sealed class CombatPresentationGeneratedVisual2D : MonoBehaviour
    {
    }
}

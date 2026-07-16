using System;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.BlasterTurret
{
    /// <summary>
    /// Temporary package-owned presentation. The line warning uses a center rail and
    /// repeated perpendicular ticks, so its meaning remains legible without hue.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlasterTurretPresentation2D : MonoBehaviour
    {
        public const int WarningTickCount = 4;

        private LineRenderer baseOutline;
        private LineRenderer barrel;
        private LineRenderer warningRail;
        private LineRenderer[] warningTicks;
        private Material sharedMaterial;
        private float warningWidth;
        private bool configured;
        private bool warningVisible;
        private bool destroyed;

        public bool IsConfigured
        {
            get { return configured; }
        }

        public bool IsWarningVisible
        {
            get { return warningVisible; }
        }

        public bool IsDestroyed
        {
            get { return destroyed; }
        }

        public bool UsesColorIndependentPattern
        {
            get { return true; }
        }

        public int PatternTickCount
        {
            get { return warningTicks == null ? 0 : warningTicks.Length; }
        }

        public LineRenderer WarningRail
        {
            get { return warningRail; }
        }

        public void Configure(double lineWidth)
        {
            if (double.IsNaN(lineWidth)
                || double.IsInfinity(lineWidth)
                || lineWidth <= 0d
                || lineWidth > BlasterTurretDefinition.HardMaximumWarningLineWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(lineWidth));
            }

            EnsureGeometry();
            warningWidth = (float)lineWidth;
            warningRail.widthMultiplier = warningWidth;
            for (int index = 0; index < warningTicks.Length; index++)
            {
                warningTicks[index].widthMultiplier = warningWidth * 1.35f;
            }

            configured = true;
            SetDestroyed(false);
            HideWarning();
        }

        public void ShowWarning(Vector2 origin, Vector2 target)
        {
            if (!configured)
            {
                throw new InvalidOperationException(
                    "Blaster Turret presentation must be configured before use.");
            }

            Vector2 delta = target - origin;
            if (!IsFinite(origin)
                || !IsFinite(target)
                || delta.sqrMagnitude <= 0.0000001f
                || destroyed)
            {
                HideWarning();
                return;
            }

            Vector2 direction = delta.normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float distance = delta.magnitude;
            warningRail.positionCount = 2;
            warningRail.SetPosition(0, origin);
            warningRail.SetPosition(1, target);
            warningRail.enabled = true;

            for (int index = 0; index < warningTicks.Length; index++)
            {
                float fraction = (index + 1f) / (warningTicks.Length + 1f);
                Vector2 center = origin + (direction * distance * fraction);
                float halfLength = warningWidth * (2.6f + (index % 2));
                LineRenderer tick = warningTicks[index];
                tick.positionCount = 2;
                tick.SetPosition(0, center - (perpendicular * halfLength));
                tick.SetPosition(1, center + (perpendicular * halfLength));
                tick.enabled = true;
            }

            barrel.positionCount = 2;
            barrel.SetPosition(0, (Vector2)transform.position);
            barrel.SetPosition(1, origin);
            warningVisible = true;
        }

        public void HideWarning()
        {
            warningVisible = false;
            if (warningRail != null)
            {
                warningRail.enabled = false;
            }

            if (warningTicks == null)
            {
                return;
            }

            for (int index = 0; index < warningTicks.Length; index++)
            {
                if (warningTicks[index] != null)
                {
                    warningTicks[index].enabled = false;
                }
            }
        }

        public void SetFacing(Vector2 facing, float muzzleOffset)
        {
            EnsureGeometry();
            if (!IsFinite(facing)
                || facing.sqrMagnitude <= 0.0000001f
                || float.IsNaN(muzzleOffset)
                || float.IsInfinity(muzzleOffset)
                || muzzleOffset < 0f)
            {
                return;
            }

            Vector2 origin = transform.position;
            barrel.positionCount = 2;
            barrel.SetPosition(0, origin);
            barrel.SetPosition(1, origin + (facing.normalized * muzzleOffset));
        }

        public void SetDestroyed(bool value)
        {
            destroyed = value;
            EnsureGeometry();
            baseOutline.enabled = !destroyed;
            barrel.enabled = !destroyed;
            if (destroyed)
            {
                HideWarning();
            }
        }

        private void Awake()
        {
            EnsureGeometry();
            HideWarning();
        }

        private void OnDisable()
        {
            HideWarning();
        }

        private void OnDestroy()
        {
            if (sharedMaterial != null)
            {
                Destroy(sharedMaterial);
                sharedMaterial = null;
            }
        }

        private void EnsureGeometry()
        {
            if (baseOutline != null
                && barrel != null
                && warningRail != null
                && warningTicks != null
                && warningTicks.Length == WarningTickCount)
            {
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null && sharedMaterial == null)
            {
                sharedMaterial = new Material(shader);
                sharedMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            baseOutline = CreateLine("StationaryBase", 5, false, 0.09f);
            baseOutline.loop = true;
            baseOutline.SetPosition(0, new Vector3(-0.65f, -0.5f, 0f));
            baseOutline.SetPosition(1, new Vector3(0.65f, -0.5f, 0f));
            baseOutline.SetPosition(2, new Vector3(0.65f, 0.5f, 0f));
            baseOutline.SetPosition(3, new Vector3(-0.65f, 0.5f, 0f));
            baseOutline.SetPosition(4, new Vector3(-0.65f, -0.5f, 0f));

            barrel = CreateLine("TurretBarrel", 2, true, 0.18f);
            barrel.SetPosition(0, transform.position);
            barrel.SetPosition(1, (Vector2)transform.position + Vector2.right * 0.7f);

            warningRail = CreateLine("LineOfFireWarningRail", 2, true, 0.07f);
            warningTicks = new LineRenderer[WarningTickCount];
            for (int index = 0; index < warningTicks.Length; index++)
            {
                warningTicks[index] = CreateLine(
                    "LineOfFireWarningTick" + (index + 1),
                    2,
                    true,
                    0.09f);
            }
        }

        private LineRenderer CreateLine(
            string objectName,
            int positionCount,
            bool useWorldSpace,
            float width)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = useWorldSpace;
            line.positionCount = positionCount;
            line.widthMultiplier = width;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.sortingOrder = 20;
            if (sharedMaterial != null)
            {
                line.sharedMaterial = sharedMaterial;
            }

            return line;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }
    }
}

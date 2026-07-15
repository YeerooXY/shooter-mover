using System;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.MobileBlasterDroid
{
    /// <summary>
    /// Temporary color-independent readability layer. A square outline identifies the
    /// mobile droid, a growing directional line identifies wind-up, and a compressed
    /// outline identifies recovery. Final art replaces this component's generated lines.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileBlasterDroidTemporaryPresentation : MonoBehaviour
    {
        private const float MinimumDirectionSquared = 0.000001f;

        private LineRenderer bodyOutline;
        private LineRenderer windUpDirection;
        private Transform visualRoot;
        private double telegraphLength;
        private double pulseSeconds;
        private bool configured;
        private MobileBlasterDroidFirePhase phase;
        private Vector2 direction = Vector2.right;

        public bool IsConfigured
        {
            get { return configured; }
        }

        public bool IsWindUpVisible
        {
            get { return windUpDirection != null && windUpDirection.enabled; }
        }

        public MobileBlasterDroidFirePhase Phase
        {
            get { return phase; }
        }

        public Vector2 Direction
        {
            get { return direction; }
        }

        public LineRenderer BodyOutline
        {
            get { return bodyOutline; }
        }

        public LineRenderer WindUpDirection
        {
            get { return windUpDirection; }
        }

        public void Configure(MobileBlasterDroidDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.ValidateOrThrow();
            EnsureLines();
            telegraphLength = definition.TelegraphLength;
            pulseSeconds = definition.WarningPulseSeconds;
            configured = true;
            SetBodyOutline();
            UpdateState(
                MobileBlasterDroidFirePhase.Ready,
                Vector2.right,
                false,
                false,
                0d);
        }

        public void UpdateState(
            MobileBlasterDroidFirePhase nextPhase,
            Vector2 readableDirection,
            bool active,
            bool destroyed,
            double elapsedSeconds)
        {
            if (!configured)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(MobileBlasterDroidFirePhase), nextPhase))
            {
                throw new ArgumentOutOfRangeException(nameof(nextPhase));
            }

            if (float.IsNaN(readableDirection.x)
                || float.IsInfinity(readableDirection.x)
                || float.IsNaN(readableDirection.y)
                || float.IsInfinity(readableDirection.y))
            {
                throw new ArgumentOutOfRangeException(nameof(readableDirection));
            }

            phase = nextPhase;
            if (readableDirection.sqrMagnitude >= MinimumDirectionSquared)
            {
                direction = readableDirection.normalized;
            }

            bool running = active && !destroyed;
            windUpDirection.enabled = running
                && phase == MobileBlasterDroidFirePhase.WindUp;
            if (windUpDirection.enabled)
            {
                float pulse = 0.8f + (0.2f * Pulse01(elapsedSeconds));
                windUpDirection.SetPosition(0, Vector3.zero);
                windUpDirection.SetPosition(
                    1,
                    (Vector3)(direction * (float)telegraphLength * pulse));
            }

            bodyOutline.enabled = !destroyed;
            if (visualRoot != null)
            {
                float scale = 1f;
                if (destroyed)
                {
                    scale = 0.65f;
                }
                else if (running && phase == MobileBlasterDroidFirePhase.WindUp)
                {
                    scale = 1f + (0.08f * Pulse01(elapsedSeconds));
                }
                else if (running && phase == MobileBlasterDroidFirePhase.Recovery)
                {
                    scale = 0.88f;
                }

                visualRoot.localScale = new Vector3(scale, scale, 1f);
            }

            Color bodyColor = destroyed
                ? new Color(0.2f, 0.2f, 0.2f, 1f)
                : phase == MobileBlasterDroidFirePhase.Recovery
                    ? new Color(0.45f, 0.45f, 0.45f, 1f)
                    : new Color(0.75f, 0.75f, 0.75f, 1f);
            bodyOutline.startColor = bodyColor;
            bodyOutline.endColor = bodyColor;
            windUpDirection.startColor = Color.white;
            windUpDirection.endColor = Color.white;
        }

        private float Pulse01(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds)
                || double.IsInfinity(elapsedSeconds)
                || elapsedSeconds < 0d)
            {
                return 0f;
            }

            float cycle = (float)(elapsedSeconds / pulseSeconds);
            return Mathf.PingPong(cycle, 1f);
        }

        private void EnsureLines()
        {
            if (visualRoot == null)
            {
                Transform existing = transform.Find("Temporary Droid Visual");
                if (existing != null)
                {
                    visualRoot = existing;
                }
                else
                {
                    GameObject root = new GameObject("Temporary Droid Visual");
                    visualRoot = root.transform;
                    visualRoot.SetParent(transform, false);
                }
            }

            bodyOutline = FindOrCreateLine("Mobile Body Outline", 5, 0.07f);
            windUpDirection = FindOrCreateLine("Wind-Up Direction", 2, 0.09f);
            windUpDirection.transform.localPosition = new Vector3(0.55f, 0f, 0f);
        }

        private LineRenderer FindOrCreateLine(string objectName, int positionCount, float width)
        {
            Transform existing = visualRoot.Find(objectName);
            GameObject lineObject;
            if (existing == null)
            {
                lineObject = new GameObject(objectName);
                lineObject.transform.SetParent(visualRoot, false);
            }
            else
            {
                lineObject = existing.gameObject;
            }

            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = lineObject.AddComponent<LineRenderer>();
            }

            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = positionCount;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sortingOrder = 10;
            return line;
        }

        private void SetBodyOutline()
        {
            bodyOutline.SetPosition(0, new Vector3(-0.55f, -0.4f, 0f));
            bodyOutline.SetPosition(1, new Vector3(-0.55f, 0.4f, 0f));
            bodyOutline.SetPosition(2, new Vector3(0.55f, 0.4f, 0f));
            bodyOutline.SetPosition(3, new Vector3(0.55f, -0.4f, 0f));
            bodyOutline.SetPosition(4, new Vector3(-0.55f, -0.4f, 0f));
        }
    }
}

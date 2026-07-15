using System;
using UnityEngine;

namespace ShooterMover.Presentation.VisibleSliceBlasterTurret
{
    /// <summary>
    /// Detachable world-space presentation for an accepted Blaster Turret snapshot.
    /// It owns only temporary renderers and text. It never receives or invokes gameplay
    /// mutation methods.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class VisibleSliceBlasterTurretPresenter : MonoBehaviour
    {
        public const string AcceptedPrototypeSpritePath =
            "Assets/ShooterMover/Art/Prototype/Stage1VisibleSlice/enemy_standing_turret_weak.png";

        [SerializeField] private bool visible = true;
        [SerializeField] private bool autoRefreshSource = true;
        [SerializeField, Min(0.01f)] private float hitReactionSeconds = 0.16f;
        [SerializeField] private bool reducedEffectsOverride;
        [SerializeField] private bool grayscaleOverride;

        private SpriteRenderer bodyRenderer;
        private IVisibleSliceBlasterTurretPresentationSource source;
        private VisibleSliceBlasterTurretFrame currentFrame;
        private long observedRestartGeneration = -1L;
        private long observedDamageSequence = -1L;
        private double damageVisibleUntilSeconds = double.NegativeInfinity;
        private Material lineMaterial;
        private TextMesh stateText;
        private TextMesh healthText;
        private TextMesh warningText;
        private TextMesh damageText;
        private LineRenderer healthBackground;
        private LineRenderer healthFill;
        private LineRenderer warningRail;
        private LineRenderer warningTriangle;
        private LineRenderer[] warningTicks;
        private LineRenderer firingFlash;
        private bool visualsCreated;
        private VisibleSliceBlasterTurretSnapshot lastSnapshot;

        public VisibleSliceBlasterTurretFrame CurrentFrame
        {
            get { return currentFrame; }
        }

        public bool IsVisible
        {
            get { return visible; }
        }

        public bool HasBoundSource
        {
            get { return source != null; }
        }

        public Sprite BodySprite
        {
            get { return bodyRenderer == null ? null : bodyRenderer.sprite; }
        }

        public void Configure(
            IVisibleSliceBlasterTurretPresentationSource injectedSource,
            Sprite replacementSprite = null)
        {
            if (injectedSource == null)
            {
                throw new ArgumentNullException(nameof(injectedSource));
            }

            EnsureVisuals();
            source = injectedSource;
            if (replacementSprite != null)
            {
                bodyRenderer.sprite = replacementSprite;
            }

            currentFrame = null;
            lastSnapshot = null;
            ClearTransientState();
            HideAllVisuals();
        }

        public void BindSource(IVisibleSliceBlasterTurretPresentationSource injectedSource)
        {
            Configure(injectedSource, null);
        }

        public void SetBodySprite(Sprite replacementSprite)
        {
            EnsureVisuals();
            bodyRenderer.sprite = replacementSprite;
        }

        public void SetVisible(bool shouldBeVisible)
        {
            visible = shouldBeVisible;
            if (!visible || !isActiveAndEnabled)
            {
                HideAllVisuals();
                return;
            }

            if (currentFrame != null)
            {
                ApplyFrame(currentFrame, Time.unscaledTimeAsDouble);
            }
        }

        public void SetAutoRefreshSource(bool shouldAutoRefresh)
        {
            autoRefreshSource = shouldAutoRefresh;
        }

        public void SetHitReactionSeconds(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            hitReactionSeconds = Mathf.Clamp(seconds, 0.01f, 1f);
        }

        public void SetReducedEffectsOverride(bool enabled)
        {
            reducedEffectsOverride = enabled;
            ReprojectCurrentSnapshot();
        }

        public void SetGrayscaleOverride(bool enabled)
        {
            grayscaleOverride = enabled;
            ReprojectCurrentSnapshot();
        }

        public bool RefreshFromSource(double nowSeconds)
        {
            if (source == null)
            {
                return false;
            }

            VisibleSliceBlasterTurretSnapshot snapshot;
            if (!source.TryReadSnapshot(out snapshot) || snapshot == null)
            {
                ClearPresentation();
                return false;
            }

            Present(snapshot, nowSeconds);
            return true;
        }

        public void Present(
            VisibleSliceBlasterTurretSnapshot snapshot,
            double nowSeconds)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds));
            }

            EnsureVisuals();
            if (snapshot.RestartGeneration != observedRestartGeneration)
            {
                observedRestartGeneration = snapshot.RestartGeneration;
                observedDamageSequence = -1L;
                damageVisibleUntilSeconds = double.NegativeInfinity;
            }

            bool terminal = snapshot.CurrentHealth <= 0
                || snapshot.Phase == VisibleSliceBlasterTurretPhase.Destroyed
                || snapshot.Phase == VisibleSliceBlasterTurretPhase.Deactivated;
            if (terminal)
            {
                damageVisibleUntilSeconds = double.NegativeInfinity;
            }
            else if (snapshot.DamageObserved
                && snapshot.DamageSequence >= 0L
                && snapshot.DamageSequence != observedDamageSequence)
            {
                observedDamageSequence = snapshot.DamageSequence;
                damageVisibleUntilSeconds = nowSeconds + hitReactionSeconds;
            }

            bool damageTransientVisible = nowSeconds < damageVisibleUntilSeconds;
            currentFrame = VisibleSliceBlasterTurretProjector.Project(
                snapshot,
                damageTransientVisible,
                reducedEffectsOverride,
                grayscaleOverride);
            lastSnapshot = snapshot;
            ApplyFrame(currentFrame, nowSeconds);
        }

        public void Present(VisibleSliceBlasterTurretSnapshot snapshot)
        {
            Present(snapshot, Time.unscaledTimeAsDouble);
        }

        public void ClearPresentation()
        {
            currentFrame = null;
            lastSnapshot = null;
            ClearTransientState();
            EnsureVisuals();
            HideAllVisuals();
        }

        private void Awake()
        {
            EnsureVisuals();
            HideAllVisuals();
        }

        private void Update()
        {
            if (autoRefreshSource && source != null)
            {
                RefreshFromSource(Time.unscaledTimeAsDouble);
                return;
            }

            if (lastSnapshot != null
                && currentFrame != null
                && currentFrame.DamageVisible
                && Time.unscaledTimeAsDouble >= damageVisibleUntilSeconds)
            {
                Present(lastSnapshot, Time.unscaledTimeAsDouble);
            }
        }

        private void OnDisable()
        {
            HideAllVisuals();
        }

        private void OnEnable()
        {
            if (currentFrame != null)
            {
                ApplyFrame(currentFrame, Time.unscaledTimeAsDouble);
            }
        }

        private void OnDestroy()
        {
            source = null;
            lastSnapshot = null;
            currentFrame = null;
            if (lineMaterial != null)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(lineMaterial);
                }
                else
                {
                    DestroyImmediate(lineMaterial);
                }

                lineMaterial = null;
            }
        }

        private void ReprojectCurrentSnapshot()
        {
            if (lastSnapshot != null)
            {
                Present(lastSnapshot, Time.unscaledTimeAsDouble);
            }
        }

        private void ClearTransientState()
        {
            observedRestartGeneration = -1L;
            observedDamageSequence = -1L;
            damageVisibleUntilSeconds = double.NegativeInfinity;
        }

        private void EnsureVisuals()
        {
            if (visualsCreated)
            {
                return;
            }

            bodyRenderer = GetComponent<SpriteRenderer>();
            lineMaterial = CreateLineMaterial();
            stateText = CreateText("State", new Vector3(0f, 1.18f, 0f), 0.12f, 40);
            healthText = CreateText("Health", new Vector3(0f, 0.94f, 0f), 0.10f, 40);
            warningText = CreateText("Warning", new Vector3(0f, 1.52f, 0f), 0.13f, 44);
            damageText = CreateText("Damage", new Vector3(0f, -0.92f, 0f), 0.15f, 44);
            healthBackground = CreateLine("Health Background", 0.10f, 35, 2);
            healthFill = CreateLine("Health Fill", 0.07f, 36, 2);
            warningRail = CreateLine("Warning Rail", 0.055f, 42, 2);
            warningTriangle = CreateLine("Warning Triangle", 0.07f, 43, 4);
            warningTicks = new LineRenderer[4];
            for (int index = 0; index < warningTicks.Length; index++)
            {
                warningTicks[index] = CreateLine(
                    "Warning Tick " + index,
                    0.055f,
                    43,
                    2);
            }

            firingFlash = CreateLine("Firing Flash", 0.09f, 45, 4);
            visualsCreated = true;
        }

        private static Material CreateLineMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Sprites/Default shader is required for temporary turret presentation.");
            }

            Material material = new Material(shader);
            material.name = "VS-003 Temporary Line Material";
            material.hideFlags = HideFlags.DontSave;
            return material;
        }

        private TextMesh CreateText(
            string childName,
            Vector3 localPosition,
            float characterSize,
            int sortingOrder)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            TextMesh text = child.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = characterSize;
            text.fontSize = 48;
            text.text = "";
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            renderer.sortingLayerID = bodyRenderer.sortingLayerID;
            renderer.sortingOrder = sortingOrder;
            return text;
        }

        private LineRenderer CreateLine(
            string childName,
            float width,
            int sortingOrder,
            int positionCount)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = positionCount;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sharedMaterial = lineMaterial;
            line.sortingLayerID = bodyRenderer.sortingLayerID;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private void ApplyFrame(
            VisibleSliceBlasterTurretFrame frame,
            double nowSeconds)
        {
            EnsureVisuals();
            if (!visible || !isActiveAndEnabled)
            {
                HideAllVisuals();
                return;
            }

            Color bodyColor;
            Color primaryColor;
            Color warningColor;
            Color damageColor;
            ResolvePalette(
                frame,
                out bodyColor,
                out primaryColor,
                out warningColor,
                out damageColor);

            bodyRenderer.enabled = true;
            float luminancePulse = frame.OptionalPulse
                ? 1f + (0.06f * Mathf.Sin((float)nowSeconds * 12f))
                : 1f;
            bodyRenderer.color = new Color(
                Mathf.Clamp01(bodyColor.r * luminancePulse),
                Mathf.Clamp01(bodyColor.g * luminancePulse),
                Mathf.Clamp01(bodyColor.b * luminancePulse),
                bodyColor.a);
            stateText.gameObject.SetActive(true);
            healthText.gameObject.SetActive(true);
            stateText.text = frame.StateText;
            healthText.text = frame.HealthText;
            stateText.color = primaryColor;
            healthText.color = primaryColor;

            healthBackground.gameObject.SetActive(true);
            healthFill.gameObject.SetActive(true);
            SetLine(
                healthBackground,
                new Vector3(-0.64f, 0.78f, 0f),
                new Vector3(0.64f, 0.78f, 0f),
                frame.Grayscale
                    ? new Color(0.22f, 0.22f, 0.22f, 1f)
                    : new Color(0.08f, 0.10f, 0.12f, 1f));
            float healthEnd = Mathf.Lerp(-0.64f, 0.64f, (float)frame.NormalizedHealth);
            SetLine(
                healthFill,
                new Vector3(-0.64f, 0.78f, 0f),
                new Vector3(healthEnd, 0.78f, 0f),
                primaryColor);

            ApplyWarning(frame, warningColor);
            ApplyFiring(frame, warningColor);
            ApplyDamage(frame, damageColor);
        }

        private void ApplyWarning(
            VisibleSliceBlasterTurretFrame frame,
            Color warningColor)
        {
            bool active = frame.WarningVisible;
            warningText.gameObject.SetActive(active);
            warningRail.gameObject.SetActive(active);
            warningTriangle.gameObject.SetActive(active);
            for (int index = 0; index < warningTicks.Length; index++)
            {
                warningTicks[index].gameObject.SetActive(active);
            }

            if (!active)
            {
                return;
            }

            warningText.text = frame.WarningGlyph
                + " "
                + frame.WarningShapeText
                + " "
                + frame.WarningCountText
                + " / "
                + frame.WarningTimingText;
            warningText.color = warningColor;

            Vector2 direction = frame.WarningDirection;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector3 railStart = direction * 0.78f;
            Vector3 railEnd = direction * 3.35f;
            SetLine(warningRail, railStart, railEnd, warningColor);

            Vector3 point = direction * 1.36f;
            Vector3 baseCenter = direction * 0.88f;
            Vector3 left = baseCenter + (Vector3)(perpendicular * 0.42f);
            Vector3 right = baseCenter - (Vector3)(perpendicular * 0.42f);
            warningTriangle.SetPosition(0, left);
            warningTriangle.SetPosition(1, point);
            warningTriangle.SetPosition(2, right);
            warningTriangle.SetPosition(3, left);
            SetLineColor(warningTriangle, warningColor);

            for (int index = 0; index < warningTicks.Length; index++)
            {
                float distance = 1.62f + (index * 0.42f);
                Vector3 center = direction * distance;
                Vector3 tickLeft = center + (Vector3)(perpendicular * 0.17f);
                Vector3 tickRight = center - (Vector3)(perpendicular * 0.17f);
                SetLine(warningTicks[index], tickLeft, tickRight, warningColor);
            }
        }

        private void ApplyFiring(
            VisibleSliceBlasterTurretFrame frame,
            Color firingColor)
        {
            bool active = frame.FiringVisible;
            firingFlash.gameObject.SetActive(active);
            if (!active)
            {
                return;
            }

            Vector2 direction = frame.WarningDirection;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector3 center = direction * 0.82f;
            firingFlash.SetPosition(0, center - (Vector3)(direction * 0.34f));
            firingFlash.SetPosition(1, center + (Vector3)(direction * 0.34f));
            firingFlash.SetPosition(2, center - (Vector3)(perpendicular * 0.30f));
            firingFlash.SetPosition(3, center + (Vector3)(perpendicular * 0.30f));
            SetLineColor(firingFlash, firingColor);
        }

        private void ApplyDamage(
            VisibleSliceBlasterTurretFrame frame,
            Color damageColor)
        {
            damageText.gameObject.SetActive(frame.DamageVisible);
            if (!frame.DamageVisible)
            {
                return;
            }

            damageText.text = frame.DamageText;
            damageText.color = damageColor;
        }

        private void HideAllVisuals()
        {
            if (!visualsCreated)
            {
                return;
            }

            bodyRenderer.enabled = false;
            stateText.gameObject.SetActive(false);
            healthText.gameObject.SetActive(false);
            warningText.gameObject.SetActive(false);
            damageText.gameObject.SetActive(false);
            healthBackground.gameObject.SetActive(false);
            healthFill.gameObject.SetActive(false);
            warningRail.gameObject.SetActive(false);
            warningTriangle.gameObject.SetActive(false);
            for (int index = 0; index < warningTicks.Length; index++)
            {
                warningTicks[index].gameObject.SetActive(false);
            }

            firingFlash.gameObject.SetActive(false);
        }

        private static void SetLine(
            LineRenderer line,
            Vector3 start,
            Vector3 end,
            Color color)
        {
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            SetLineColor(line, color);
        }

        private static void SetLineColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        private static void ResolvePalette(
            VisibleSliceBlasterTurretFrame frame,
            out Color bodyColor,
            out Color primaryColor,
            out Color warningColor,
            out Color damageColor)
        {
            if (frame.Grayscale)
            {
                bodyColor = frame.DestroyedVisible || frame.DeactivatedVisible
                    ? new Color(0.32f, 0.32f, 0.32f, 0.82f)
                    : Color.white;
                primaryColor = new Color(0.92f, 0.92f, 0.92f, 1f);
                warningColor = Color.white;
                damageColor = new Color(0.72f, 0.72f, 0.72f, 1f);
                return;
            }

            bodyColor = frame.DestroyedVisible || frame.DeactivatedVisible
                ? new Color(0.35f, 0.38f, 0.42f, 0.82f)
                : Color.white;
            primaryColor = frame.RecoveryVisible
                ? new Color(0.55f, 0.88f, 1f, 1f)
                : new Color(0.86f, 0.96f, 1f, 1f);
            warningColor = new Color(1f, 0.82f, 0.24f, 1f);
            damageColor = new Color(1f, 0.36f, 0.30f, 1f);
        }
    }
}

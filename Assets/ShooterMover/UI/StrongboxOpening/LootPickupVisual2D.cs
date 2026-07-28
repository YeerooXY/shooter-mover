using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    /// <summary>
    /// Reusable physical projection for an immutable loot-pickup view model. It owns
    /// sprites, glow and accepted-collection feedback only. It never submits collection,
    /// destroys the authoritative object, or changes reward state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootPickupVisual2D : MonoBehaviour
    {
        private static readonly Dictionary<LootPickupPresentationKindV1, Sprite> SpriteCache =
            new Dictionary<LootPickupPresentationKindV1, Sprite>();
        private static Sprite haloSprite;

        private SpriteRenderer haloRenderer;
        private SpriteRenderer bodyRenderer;
        private TextMesh label;
        private LootPickupPresentationV1 projection;
        private string boundFingerprint;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private float time;
        private bool acceptedFeedback;
        private float feedbackElapsed;
        private Vector3 feedbackStart;
        private Vector3 feedbackTarget;

        public event Action AcceptedCollectionFeedbackCompleted;

        public LootPickupPresentationV1 Projection { get { return projection; } }
        public bool IsBound { get { return projection != null; } }
        public bool IsVisible { get { return bodyRenderer != null && bodyRenderer.enabled; } }
        public bool IsPlayingAcceptedCollectionFeedback { get { return acceptedFeedback; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeCache()
        {
            SpriteCache.Clear();
            haloSprite = null;
        }

        public void Bind(LootPickupPresentationV1 immutableProjection)
        {
            if (immutableProjection == null)
            {
                throw new ArgumentNullException(nameof(immutableProjection));
            }

            string fingerprint = BuildFingerprint(immutableProjection);
            if (projection != null)
            {
                if (projection.PickupStableId != immutableProjection.PickupStableId)
                {
                    throw new InvalidOperationException(
                        "A loot pickup visual cannot be rebound to another exact pickup identity.");
                }
                if (!string.Equals(boundFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The same pickup identity cannot be rebound to conflicting presentation facts.");
                }
            }

            projection = immutableProjection;
            boundFingerprint = fingerprint;
            EnsureVisuals();
            baseLocalPosition = transform.localPosition;
            baseLocalScale = transform.localScale;
            time = 0f;
            acceptedFeedback = false;
            feedbackElapsed = 0f;
            ApplyProjection();
            SetVisible(true);
        }

        /// <summary>
        /// Call only after the canonical collection authority accepted the exact pickup
        /// (or accepted its exact replay). This method is visual and never reports truth.
        /// </summary>
        public bool PlayAcceptedCollectionFeedback(Vector3 attractionTarget)
        {
            if (projection == null || acceptedFeedback)
            {
                return false;
            }

            acceptedFeedback = true;
            feedbackElapsed = 0f;
            feedbackStart = transform.position;
            feedbackTarget = attractionTarget;
            return true;
        }

        public void RestoreVisibleProjection()
        {
            if (projection == null)
            {
                throw new InvalidOperationException(
                    "A projection must be bound before reconstruction.");
            }
            acceptedFeedback = false;
            feedbackElapsed = 0f;
            transform.localPosition = baseLocalPosition;
            transform.localScale = baseLocalScale;
            ApplyProjection();
            SetVisible(true);
        }

        private void Update()
        {
            if (projection == null)
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            time += delta;
            if (acceptedFeedback)
            {
                AdvanceAcceptedFeedback(delta);
                return;
            }

            float bob = Mathf.Sin(time * 3.2f) * 0.08f;
            transform.localPosition = baseLocalPosition + new Vector3(0f, bob, 0f);
            float pulse = 1f
                + Mathf.Sin(
                    time * (2.4f + projection.GlowStrength * 2.2f))
                    * (0.04f + projection.GlowStrength * 0.08f);
            if (haloRenderer != null)
            {
                haloRenderer.transform.localScale =
                    new Vector3(pulse, pulse, 1f);
            }
        }

        private void AdvanceAcceptedFeedback(float delta)
        {
            feedbackElapsed += delta;
            const float duration = 0.24f;
            float progress = Mathf.Clamp01(feedbackElapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            transform.position = Vector3.Lerp(
                feedbackStart,
                feedbackTarget,
                eased);
            transform.localScale =
                baseLocalScale * Mathf.Lerp(1f, 0.2f, eased);
            SetAlpha(1f - progress);
            if (progress < 1f)
            {
                return;
            }

            acceptedFeedback = false;
            SetVisible(false);
            Action handler = AcceptedCollectionFeedbackCompleted;
            if (handler != null)
            {
                handler();
            }
        }

        private void EnsureVisuals()
        {
            if (haloRenderer == null)
            {
                GameObject haloObject = new GameObject("LootGlow");
                haloObject.transform.SetParent(transform, false);
                haloRenderer = haloObject.AddComponent<SpriteRenderer>();
                haloRenderer.sortingOrder = -1;
                haloRenderer.sprite = GetHaloSprite();
            }
            if (bodyRenderer == null)
            {
                GameObject bodyObject = new GameObject("LootBody");
                bodyObject.transform.SetParent(transform, false);
                bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
                bodyRenderer.sortingOrder = 0;
            }
            if (label == null)
            {
                GameObject labelObject = new GameObject("LootLabel");
                labelObject.transform.SetParent(transform, false);
                labelObject.transform.localPosition =
                    new Vector3(0f, -0.72f, 0f);
                label = labelObject.AddComponent<TextMesh>();
                label.anchor = TextAnchor.UpperCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = 0.08f;
                label.fontSize = 40;
            }
        }

        private void ApplyProjection()
        {
            Color accent = ResolveAccent(projection);
            bodyRenderer.sprite = GetBodySprite(projection.PresentationKind);
            bodyRenderer.color = accent;
            float bodyScale = projection.IsStrongbox ? 1.15f : 0.92f;
            bodyRenderer.transform.localScale =
                new Vector3(bodyScale, bodyScale, 1f);

            Color halo = accent;
            halo.a = 0.16f + projection.GlowStrength * 0.5f;
            haloRenderer.color = halo;
            float haloScale = 1.45f + projection.GlowStrength * 0.75f;
            haloRenderer.transform.localScale =
                new Vector3(haloScale, haloScale, 1f);

            label.color = Color.Lerp(Color.white, accent, 0.25f);
            label.text = projection.Label
                + (projection.Quantity == 1L
                    ? string.Empty
                    : " x"
                        + projection.Quantity.ToString(
                            CultureInfo.InvariantCulture));
        }

        private void SetVisible(bool visible)
        {
            if (haloRenderer != null) haloRenderer.enabled = visible;
            if (bodyRenderer != null) bodyRenderer.enabled = visible;
            if (label != null) label.gameObject.SetActive(visible);
        }

        private void SetAlpha(float alpha)
        {
            if (haloRenderer != null)
            {
                Color color = haloRenderer.color;
                color.a = Mathf.Min(color.a, alpha);
                haloRenderer.color = color;
            }
            if (bodyRenderer != null)
            {
                Color color = bodyRenderer.color;
                color.a = alpha;
                bodyRenderer.color = color;
            }
            if (label != null)
            {
                Color color = label.color;
                color.a = alpha;
                label.color = color;
            }
        }

        private static string BuildFingerprint(
            LootPickupPresentationV1 value)
        {
            return value.PickupStableId
                + "|" + value.RewardInstanceStableId
                + "|" + value.RewardKind
                + "|" + value.ContentStableId
                + "|" + value.Quantity.ToString(
                    CultureInfo.InvariantCulture);
        }

        private static Color ResolveAccent(LootPickupPresentationV1 value)
        {
            switch (value.PresentationKind)
            {
                case LootPickupPresentationKindV1.Credits:
                    return new Color(1f, 0.78f, 0.12f, 1f);
                case LootPickupPresentationKindV1.Scrap:
                    return new Color(0.62f, 0.69f, 0.75f, 1f);
                case LootPickupPresentationKindV1.Strongbox:
                    return ResolveStrongboxAccent(value.TierNumber);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(value.PresentationKind),
                        value.PresentationKind,
                        "Unsupported loot presentation kind.");
            }
        }

        private static Color ResolveStrongboxAccent(int tierNumber)
        {
            if (tierNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tierNumber));
            }
            if (tierNumber == 1)
            {
                return new Color(0.48f, 0.53f, 0.57f, 1f);
            }

            // Presentation-only procedural palette. The production catalogue remains
            // the sole tier list, so adding a tier cannot require a second UI table.
            float ordinal = tierNumber - 2f;
            float hue = Mathf.Repeat(0.055f + ordinal * 0.117f, 1f);
            float saturation = Mathf.Clamp01(0.64f + ordinal * 0.025f);
            float value = Mathf.Clamp01(0.78f + ordinal * 0.022f);
            return Color.HSVToRGB(hue, saturation, value);
        }

        private static Sprite GetBodySprite(
            LootPickupPresentationKindV1 kind)
        {
            Sprite sprite;
            if (SpriteCache.TryGetValue(kind, out sprite))
            {
                if (sprite != null)
                {
                    return sprite;
                }
                SpriteCache.Remove(kind);
            }

            const int size = 48;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "LootPresentation_" + kind,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool filled = IsFilled(kind, x, y, size);
                    pixels[y * size + x] = filled
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                32f);
            sprite.name = texture.name;
            SpriteCache.Add(kind, sprite);
            return sprite;
        }

        private static bool IsFilled(
            LootPickupPresentationKindV1 kind,
            int x,
            int y,
            int size)
        {
            int center = size / 2;
            switch (kind)
            {
                case LootPickupPresentationKindV1.Credits:
                    int dx = x - center;
                    int dy = y - center;
                    int radiusSquared = dx * dx + dy * dy;
                    return radiusSquared <= 18 * 18
                        && radiusSquared >= 9 * 9;
                case LootPickupPresentationKindV1.Scrap:
                    return (x >= 11 && x <= 36 && y >= 14 && y <= 33)
                        || (x >= 18 && x <= 29 && y >= 7 && y <= 40)
                        || (x + y >= 31
                            && x + y <= 61
                            && x - y >= -16
                            && x - y <= 16);
                case LootPickupPresentationKindV1.Strongbox:
                    bool shell = x >= 6 && x <= 41
                        && y >= 10 && y <= 37;
                    bool seam = y >= 25 && y <= 28;
                    bool lockPlate = x >= 20 && x <= 27
                        && y >= 20 && y <= 31;
                    return shell && (!seam || lockPlate);
                default:
                    return false;
            }
        }

        private static Sprite GetHaloSprite()
        {
            if (haloSprite != null)
            {
                return haloSprite;
            }

            const int size = 64;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "LootPresentation_Halo",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            Vector2 center =
                new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(
                        new Vector2(x, y),
                        center) / (size * 0.5f);
                    byte alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(1f - distance) * 255f);
                    pixels[y * size + x] =
                        new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            haloSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                32f);
            haloSprite.name = texture.name;
            return haloSprite;
        }
    }
}

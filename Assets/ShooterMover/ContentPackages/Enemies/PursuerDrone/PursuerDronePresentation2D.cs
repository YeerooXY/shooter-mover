using System;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.PursuerDrone
{
    /// <summary>
    /// Temporary package-owned grayscale presentation. The broad arrow silhouette,
    /// paired warning fins, and fin motion remain legible without hue information.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PursuerDronePresentation2D : MonoBehaviour
    {
        private const int TextureSize = 16;
        private const float PixelsPerUnit = 16f;

        private SpriteRenderer bodyRenderer;
        private SpriteRenderer leftWarningRenderer;
        private SpriteRenderer rightWarningRenderer;
        private Transform leftWarning;
        private Transform rightWarning;
        private Sprite bodySprite;
        private Sprite warningSprite;
        private Texture2D bodyTexture;
        private Texture2D warningTexture;
        private double warningPulseSeconds;
        private bool configured;
        private bool running;
        private bool destroyed;

        public bool IsConfigured
        {
            get { return configured; }
        }

        public bool IsWarningVisible
        {
            get
            {
                return configured
                    && running
                    && !destroyed
                    && leftWarningRenderer != null
                    && leftWarningRenderer.enabled
                    && rightWarningRenderer != null
                    && rightWarningRenderer.enabled;
            }
        }

        public void Configure(double pulseSeconds)
        {
            if (double.IsNaN(pulseSeconds)
                || double.IsInfinity(pulseSeconds)
                || pulseSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(pulseSeconds));
            }

            if (configured)
            {
                if (warningPulseSeconds == pulseSeconds)
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Pursuer Drone presentation is already configured.");
            }

            warningPulseSeconds = pulseSeconds;
            bodySprite = CreateBodySprite(out bodyTexture);
            warningSprite = CreateWarningSprite(out warningTexture);
            bodyRenderer = CreateRenderer(
                "Pursuer Drone Silhouette",
                bodySprite,
                0);
            leftWarningRenderer = CreateRenderer(
                "Pursuer Drone Warning Left",
                warningSprite,
                1);
            rightWarningRenderer = CreateRenderer(
                "Pursuer Drone Warning Right",
                warningSprite,
                1);
            leftWarning = leftWarningRenderer.transform;
            rightWarning = rightWarningRenderer.transform;
            leftWarning.localScale = new Vector2(-1f, 1f);
            configured = true;
            SetRunning(false);
        }

        public void SetRunning(bool value)
        {
            running = value;
            RefreshWarningVisibility();
        }

        public void SetDestroyed(bool value)
        {
            destroyed = value;
            RefreshWarningVisibility();
        }

        private void Update()
        {
            if (!configured || leftWarning == null || rightWarning == null)
            {
                return;
            }

            float phase = Mathf.PingPong(
                (float)(Time.unscaledTimeAsDouble / warningPulseSeconds),
                1f);
            float offset = 0.62f + (phase * 0.18f);
            leftWarning.localPosition = new Vector2(-offset, 0f);
            rightWarning.localPosition = new Vector2(offset, 0f);
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(bodySprite);
            DestroyGeneratedObject(warningSprite);
            DestroyGeneratedObject(bodyTexture);
            DestroyGeneratedObject(warningTexture);
            bodySprite = null;
            warningSprite = null;
            bodyTexture = null;
            warningTexture = null;
        }

        private SpriteRenderer CreateRenderer(
            string objectName,
            Sprite sprite,
            int sortingOrder)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = sortingOrder == 0
                ? new Color(0.72f, 0.72f, 0.72f, 1f)
                : Color.white;
            return renderer;
        }

        private void RefreshWarningVisibility()
        {
            bool visible = configured && running && !destroyed;
            if (leftWarningRenderer != null)
            {
                leftWarningRenderer.enabled = visible;
            }

            if (rightWarningRenderer != null)
            {
                rightWarningRenderer.enabled = visible;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = true;
            }
        }

        private static Sprite CreateBodySprite(out Texture2D texture)
        {
            texture = CreateTexture("Pursuer Drone Temporary Silhouette");
            Color32[] pixels = ClearPixels();
            Color32 solid = new Color32(255, 255, 255, 255);

            for (int y = 2; y <= 13; y++)
            {
                int distance = Math.Abs(y - 7);
                int halfWidth = Math.Max(1, 5 - distance);
                FillHorizontal(pixels, y, 3, 3 + (halfWidth * 2), solid);
            }

            FillHorizontal(pixels, 6, 11, 14, solid);
            FillHorizontal(pixels, 7, 11, 15, solid);
            FillHorizontal(pixels, 8, 11, 14, solid);
            FillHorizontal(pixels, 4, 1, 4, solid);
            FillHorizontal(pixels, 10, 1, 4, solid);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture, "Pursuer Drone Temporary Silhouette");
        }

        private static Sprite CreateWarningSprite(out Texture2D texture)
        {
            texture = CreateTexture("Pursuer Drone Temporary Warning Fin");
            Color32[] pixels = ClearPixels();
            Color32 solid = new Color32(255, 255, 255, 255);

            for (int y = 3; y <= 12; y++)
            {
                int width = y <= 7 ? y - 2 : 13 - y;
                FillHorizontal(pixels, y, 2, 2 + width, solid);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture, "Pursuer Drone Temporary Warning Fin");
        }

        private static Texture2D CreateTexture(string name)
        {
            Texture2D texture = new Texture2D(
                TextureSize,
                TextureSize,
                TextureFormat.RGBA32,
                false);
            texture.name = name;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.DontSave;
            return texture;
        }

        private static Sprite CreateSprite(Texture2D texture, string name)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Color32[] ClearPixels()
        {
            Color32[] pixels = new Color32[TextureSize * TextureSize];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = clear;
            }

            return pixels;
        }

        private static void FillHorizontal(
            Color32[] pixels,
            int y,
            int minimumX,
            int maximumX,
            Color32 color)
        {
            int clampedMinimum = Mathf.Clamp(minimumX, 0, TextureSize - 1);
            int clampedMaximum = Mathf.Clamp(maximumX, 0, TextureSize - 1);
            for (int x = clampedMinimum; x <= clampedMaximum; x++)
            {
                pixels[(y * TextureSize) + x] = color;
            }
        }

        private static void DestroyGeneratedObject(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}

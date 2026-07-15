using UnityEngine;

namespace ShooterMover.ContentPackages.MapPresentation.Stage1Floor
{
    /// <summary>
    /// Temporary, presentation-only Stage 1 floor.
    ///
    /// The component creates one hidden SpriteRenderer while the prefab is open or instantiated.
    /// It uses an explicitly assigned replacement sprite when available; otherwise it generates a
    /// deterministic seamless dark-metal tile. It never creates collision or gameplay hooks.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Stage1FloorVisual : MonoBehaviour
    {
        private const string GeneratedSurfaceName = "Generated Stage 1 Floor Surface";

        [Header("Prototype footprint")]
        [SerializeField]
        private Vector2 floorSize = new Vector2(48f, 27f);

        [SerializeField]
        [Min(0.25f)]
        private float tileWorldSize = 4f;

        [SerializeField]
        private int sortingOrder = -100;

        [Header("Replaceable presentation")]
        [SerializeField]
        private Sprite replacementSprite;

        [SerializeField]
        private Material materialOverride;

        [Header("Procedural fallback")]
        [SerializeField]
        [Range(32, 256)]
        private int textureResolution = 64;

        [SerializeField]
        private Color baseColor = new Color(0.075f, 0.09f, 0.11f, 1f);

        [SerializeField]
        private Color seamColor = new Color(0.025f, 0.032f, 0.043f, 1f);

        [SerializeField]
        private Color bevelColor = new Color(0.13f, 0.15f, 0.175f, 1f);

        [SerializeField]
        private Color wearColor = new Color(0.17f, 0.18f, 0.19f, 1f);

        private GameObject generatedSurface;
        private Texture2D generatedTexture;
        private Sprite generatedSprite;

        private void OnEnable()
        {
            TryRebuild();
        }

        private void OnValidate()
        {
            TryRebuild();
        }

        private void OnDisable()
        {
            ReleaseGeneratedPresentation();
        }

        private void OnDestroy()
        {
            ReleaseGeneratedPresentation();
        }

        private void TryRebuild()
        {
            if (!isActiveAndEnabled || !gameObject.scene.IsValid())
            {
                return;
            }

            RebuildPresentation();
        }

        private void RebuildPresentation()
        {
            ReleaseGeneratedPresentation();

            generatedSurface = new GameObject(GeneratedSurfaceName);
            generatedSurface.hideFlags = HideFlags.HideAndDontSave;
            generatedSurface.transform.SetParent(transform, false);
            generatedSurface.transform.localPosition = Vector3.zero;
            generatedSurface.transform.localRotation = Quaternion.identity;
            generatedSurface.transform.localScale = Vector3.one;

            SpriteRenderer renderer = generatedSurface.AddComponent<SpriteRenderer>();
            renderer.sprite = replacementSprite != null
                ? replacementSprite
                : CreateFallbackSprite();

            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(
                Mathf.Max(0.25f, floorSize.x),
                Mathf.Max(0.25f, floorSize.y));
            renderer.sortingOrder = sortingOrder;
            renderer.color = Color.white;

            if (materialOverride != null)
            {
                renderer.sharedMaterial = materialOverride;
            }
        }

        private Sprite CreateFallbackSprite()
        {
            int resolution = Mathf.Clamp(textureResolution, 32, 256);
            generatedTexture = new Texture2D(
                resolution,
                resolution,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = "VS-ART-001 Temporary Floor Tile",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 1,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Color32[] pixels = new Color32[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    pixels[(y * resolution) + x] = EvaluatePixel(x, y, resolution);
                }
            }

            generatedTexture.SetPixels32(pixels);
            generatedTexture.Apply(false, true);

            float pixelsPerUnit = resolution / Mathf.Max(0.25f, tileWorldSize);
            generatedSprite = Sprite.Create(
                generatedTexture,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            generatedSprite.name = "VS-ART-001 Temporary Floor Sprite";
            generatedSprite.hideFlags = HideFlags.HideAndDontSave;
            return generatedSprite;
        }

        private Color32 EvaluatePixel(int x, int y, int resolution)
        {
            int seamThickness = Mathf.Max(1, resolution / 32);
            int bevelThickness = Mathf.Max(1, resolution / 64);
            int distanceToEdge = Mathf.Min(
                Mathf.Min(x, resolution - 1 - x),
                Mathf.Min(y, resolution - 1 - y));

            if (distanceToEdge < seamThickness)
            {
                return seamColor;
            }

            if (distanceToEdge < seamThickness + bevelThickness)
            {
                return bevelColor;
            }

            float angleX = ((x + 0.5f) / resolution) * Mathf.PI * 2f;
            float angleY = ((y + 0.5f) / resolution) * Mathf.PI * 2f;
            float periodicShade = Mathf.Cos(angleX) * Mathf.Cos(angleY) * 0.018f;
            Color pixel = OffsetRgb(baseColor, periodicShade);

            int diagonalPeriod = Mathf.Max(8, resolution / 4);
            int diagonal = PositiveModulo(x + y, diagonalPeriod);
            if (diagonal < seamThickness
                && x > resolution / 5
                && x < (resolution * 4) / 5
                && y > resolution / 5
                && y < (resolution * 4) / 5)
            {
                pixel = Color.Lerp(pixel, wearColor, 0.32f);
            }

            int rivetRadius = Mathf.Max(1, resolution / 40);
            int inset = Mathf.Max(rivetRadius + 2, resolution / 8);
            if (IsInsideRivet(x, y, inset, inset, rivetRadius)
                || IsInsideRivet(x, y, resolution - 1 - inset, inset, rivetRadius)
                || IsInsideRivet(x, y, inset, resolution - 1 - inset, rivetRadius)
                || IsInsideRivet(
                    x,
                    y,
                    resolution - 1 - inset,
                    resolution - 1 - inset,
                    rivetRadius))
            {
                pixel = bevelColor;
            }

            return pixel;
        }

        private static bool IsInsideRivet(
            int x,
            int y,
            int centerX,
            int centerY,
            int radius)
        {
            int dx = x - centerX;
            int dy = y - centerY;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static Color OffsetRgb(Color color, float offset)
        {
            return new Color(
                Mathf.Clamp01(color.r + offset),
                Mathf.Clamp01(color.g + offset),
                Mathf.Clamp01(color.b + offset),
                color.a);
        }

        private void ReleaseGeneratedPresentation()
        {
            DestroyGeneratedObject(generatedSurface);
            DestroyGeneratedObject(generatedSprite);
            DestroyGeneratedObject(generatedTexture);

            generatedSurface = null;
            generatedSprite = null;
            generatedTexture = null;
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}

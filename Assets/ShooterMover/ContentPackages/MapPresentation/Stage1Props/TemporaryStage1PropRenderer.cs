using System;
using UnityEngine;

namespace ShooterMover.ContentPackages.MapPresentation.Stage1Props
{
    /// <summary>
    /// Temporary, presentation-only pixel renderer for VS-ART-002 props.
    /// It creates an unsaved sprite in edit mode and at runtime; it owns no collision,
    /// interaction, mission, enemy, player, save, reward, or scene authority.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TemporaryStage1PropRenderer : MonoBehaviour
    {
        public enum TemporaryPropKind
        {
            Crate = 0,
            FloorVent = 1,
            ServiceConsole = 2,
            SupportPillar = 3,
            PipeCluster = 4,
            WarningMarker = 5,
        }

        private const int TextureSize = 96;
        private const float PixelsPerUnit = 48f;

        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);
        private static readonly Color32 Outline = new Color32(18, 24, 32, 255);
        private static readonly Color32 DeepMetal = new Color32(39, 50, 63, 255);
        private static readonly Color32 Steel = new Color32(82, 101, 120, 255);
        private static readonly Color32 LightSteel = new Color32(143, 166, 187, 255);
        private static readonly Color32 Cyan = new Color32(43, 193, 214, 255);
        private static readonly Color32 Amber = new Color32(242, 181, 62, 255);
        private static readonly Color32 Red = new Color32(218, 76, 70, 255);

        [SerializeField]
        private TemporaryPropKind propKind;

        [NonSerialized]
        private SpriteRenderer spriteRenderer;

        [NonSerialized]
        private Texture2D generatedTexture;

        [NonSerialized]
        private Sprite generatedSprite;

        public TemporaryPropKind PropKind => propKind;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            ReleaseGeneratedAssets();
        }

        private void OnDestroy()
        {
            ReleaseGeneratedAssets();
        }

        [ContextMenu("VS-ART-002/Rebuild Temporary Prop")]
        private void Rebuild()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            ReleaseGeneratedAssets();

            Color32[] pixels = new Color32[TextureSize * TextureSize];
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = Transparent;

            DrawProp(pixels, propKind);

            generatedTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, true)
            {
                name = "VS-ART-002 " + propKind + " Temporary Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            generatedTexture.SetPixels32(pixels);
            generatedTexture.Apply(false, true);

            generatedSprite = Sprite.Create(
                generatedTexture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            generatedSprite.name = "VS-ART-002 " + propKind + " Temporary Sprite";
            generatedSprite.hideFlags = HideFlags.HideAndDontSave;

            spriteRenderer.sprite = generatedSprite;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 5;
        }

        private void ReleaseGeneratedAssets()
        {
            if (spriteRenderer != null && spriteRenderer.sprite == generatedSprite)
                spriteRenderer.sprite = null;

            DestroyGenerated(generatedSprite);
            DestroyGenerated(generatedTexture);
            generatedSprite = null;
            generatedTexture = null;
        }

        private static void DestroyGenerated(UnityEngine.Object generatedObject)
        {
            if (generatedObject == null)
                return;

            if (Application.isPlaying)
                Destroy(generatedObject);
            else
                DestroyImmediate(generatedObject);
        }

        private static void DrawProp(Color32[] pixels, TemporaryPropKind kind)
        {
            switch (kind)
            {
                case TemporaryPropKind.Crate:
                    DrawCrate(pixels);
                    break;
                case TemporaryPropKind.FloorVent:
                    DrawFloorVent(pixels);
                    break;
                case TemporaryPropKind.ServiceConsole:
                    DrawServiceConsole(pixels);
                    break;
                case TemporaryPropKind.SupportPillar:
                    DrawSupportPillar(pixels);
                    break;
                case TemporaryPropKind.PipeCluster:
                    DrawPipeCluster(pixels);
                    break;
                case TemporaryPropKind.WarningMarker:
                    DrawWarningMarker(pixels);
                    break;
                default:
                    DrawCrate(pixels);
                    break;
            }
        }

        private static void DrawCrate(Color32[] pixels)
        {
            FillRect(pixels, 13, 13, 70, 70, Outline);
            FillRect(pixels, 18, 18, 60, 60, DeepMetal);
            StrokeRect(pixels, 23, 23, 50, 50, 4, Steel);
            DrawLine(pixels, 25, 25, 70, 70, 5, LightSteel);
            DrawLine(pixels, 70, 25, 25, 70, 5, Steel);
            FillRect(pixels, 39, 40, 18, 16, Outline);
            FillRect(pixels, 43, 43, 10, 10, Amber);
            FillCircle(pixels, 22, 22, 3, LightSteel);
            FillCircle(pixels, 73, 22, 3, LightSteel);
            FillCircle(pixels, 22, 73, 3, LightSteel);
            FillCircle(pixels, 73, 73, 3, LightSteel);
        }

        private static void DrawFloorVent(Color32[] pixels)
        {
            FillRect(pixels, 7, 23, 82, 50, Outline);
            FillRect(pixels, 12, 28, 72, 40, DeepMetal);
            StrokeRect(pixels, 16, 32, 64, 32, 3, LightSteel);
            for (int x = 22; x <= 68; x += 9)
                FillRect(pixels, x, 35, 4, 26, Outline);
            FillRect(pixels, 16, 29, 64, 3, Cyan);
            FillCircle(pixels, 14, 26, 3, Steel);
            FillCircle(pixels, 81, 26, 3, Steel);
            FillCircle(pixels, 14, 69, 3, Steel);
            FillCircle(pixels, 81, 69, 3, Steel);
        }

        private static void DrawServiceConsole(Color32[] pixels)
        {
            FillRect(pixels, 12, 16, 72, 64, Outline);
            FillRect(pixels, 17, 20, 62, 55, DeepMetal);
            FillRect(pixels, 21, 49, 54, 22, Steel);
            FillRect(pixels, 26, 54, 44, 12, Outline);
            FillRect(pixels, 30, 57, 36, 6, Cyan);
            FillRect(pixels, 22, 27, 52, 15, Steel);
            FillRect(pixels, 27, 31, 10, 7, Amber);
            FillRect(pixels, 42, 31, 10, 7, LightSteel);
            FillRect(pixels, 57, 31, 10, 7, Red);
            FillRect(pixels, 16, 11, 16, 8, Outline);
            FillRect(pixels, 64, 11, 16, 8, Outline);
            DrawLine(pixels, 18, 47, 24, 73, 4, LightSteel);
            DrawLine(pixels, 77, 47, 71, 73, 4, Steel);
        }

        private static void DrawSupportPillar(Color32[] pixels)
        {
            FillRect(pixels, 22, 8, 52, 80, Outline);
            FillRect(pixels, 17, 12, 62, 18, Outline);
            FillRect(pixels, 17, 66, 62, 18, Outline);
            FillRect(pixels, 26, 15, 44, 66, DeepMetal);
            FillRect(pixels, 31, 20, 34, 56, Steel);
            FillRect(pixels, 36, 20, 6, 56, LightSteel);
            FillRect(pixels, 54, 20, 6, 56, Outline);
            FillRect(pixels, 21, 16, 54, 6, Amber);
            FillRect(pixels, 21, 74, 54, 6, Amber);
            FillCircle(pixels, 25, 25, 3, LightSteel);
            FillCircle(pixels, 70, 25, 3, LightSteel);
            FillCircle(pixels, 25, 70, 3, LightSteel);
            FillCircle(pixels, 70, 70, 3, LightSteel);
        }

        private static void DrawPipeCluster(Color32[] pixels)
        {
            FillRect(pixels, 14, 36, 68, 24, Outline);
            FillRect(pixels, 20, 40, 56, 16, Steel);
            FillRect(pixels, 17, 28, 8, 40, Amber);
            FillRect(pixels, 71, 28, 8, 40, Amber);
            DrawPipeEnd(pixels, 27, 48, 15);
            DrawPipeEnd(pixels, 48, 48, 18);
            DrawPipeEnd(pixels, 70, 48, 14);
            FillRect(pixels, 38, 16, 20, 10, Outline);
            FillRect(pixels, 41, 19, 14, 4, Cyan);
            FillRect(pixels, 38, 70, 20, 10, Outline);
            FillRect(pixels, 41, 73, 14, 4, Cyan);
        }

        private static void DrawPipeEnd(Color32[] pixels, int centerX, int centerY, int radius)
        {
            FillCircle(pixels, centerX, centerY, radius, Outline);
            FillCircle(pixels, centerX, centerY, radius - 4, LightSteel);
            FillCircle(pixels, centerX, centerY, radius - 8, DeepMetal);
            FillRect(pixels, centerX - radius + 2, centerY - 2, radius * 2 - 4, 4, Steel);
        }

        private static void DrawWarningMarker(Color32[] pixels)
        {
            FillDiamond(pixels, 48, 48, 40, Outline);
            FillDiamond(pixels, 48, 48, 34, Amber);
            FillDiamond(pixels, 48, 48, 28, DeepMetal);
            for (int offset = -18; offset <= 18; offset += 12)
                DrawLine(pixels, 27 + offset, 28, 67 + offset, 68, 5, Amber);
            FillDiamond(pixels, 48, 48, 18, Outline);
            FillRect(pixels, 44, 39, 8, 21, LightSteel);
            FillRect(pixels, 44, 31, 8, 6, Red);
        }

        private static void FillRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (int py = Math.Max(0, y); py < Math.Min(TextureSize, y + height); py++)
            {
                for (int px = Math.Max(0, x); px < Math.Min(TextureSize, x + width); px++)
                    pixels[(py * TextureSize) + px] = color;
            }
        }

        private static void StrokeRect(Color32[] pixels, int x, int y, int width, int height, int thickness, Color32 color)
        {
            FillRect(pixels, x, y, width, thickness, color);
            FillRect(pixels, x, y + height - thickness, width, thickness, color);
            FillRect(pixels, x, y, thickness, height, color);
            FillRect(pixels, x + width - thickness, y, thickness, height, color);
        }

        private static void FillCircle(Color32[] pixels, int centerX, int centerY, int radius, Color32 color)
        {
            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if ((dx * dx) + (dy * dy) <= radiusSquared)
                        SetPixel(pixels, x, y, color);
                }
            }
        }

        private static void FillDiamond(Color32[] pixels, int centerX, int centerY, int radius, Color32 color)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                int halfWidth = radius - Math.Abs(y - centerY);
                FillRect(pixels, centerX - halfWidth, y, (halfWidth * 2) + 1, 1, color);
            }
        }

        private static void DrawLine(Color32[] pixels, int x0, int y0, int x1, int y1, int thickness, Color32 color)
        {
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            int radius = Math.Max(0, thickness / 2);

            while (true)
            {
                FillCircle(pixels, x0, y0, radius, color);
                if (x0 == x1 && y0 == y1)
                    break;

                int doubledError = error * 2;
                if (doubledError >= dy)
                {
                    error += dy;
                    x0 += sx;
                }
                if (doubledError <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void SetPixel(Color32[] pixels, int x, int y, Color32 color)
        {
            if (x < 0 || x >= TextureSize || y < 0 || y >= TextureSize)
                return;
            pixels[(y * TextureSize) + x] = color;
        }
    }
}

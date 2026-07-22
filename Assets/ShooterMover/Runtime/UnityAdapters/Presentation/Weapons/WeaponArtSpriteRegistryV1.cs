using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Presentation.Weapons
{
    public sealed class WeaponArtSpriteResolutionV1
    {
        public WeaponArtSpriteResolutionV1(
            string artReferenceId,
            Sprite sprite,
            bool usedFallback,
            string diagnostic)
        {
            ArtReferenceId = artReferenceId ?? string.Empty;
            Sprite = sprite;
            UsedFallback = usedFallback;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public string ArtReferenceId { get; }

        public Sprite Sprite { get; }

        public bool UsedFallback { get; }

        public string Diagnostic { get; }
    }

    /// <summary>
    /// Unity-only mapping from canonical art reference IDs to Resources sprites. The
    /// registry owns resource paths and caching; gameplay catalogs never do.
    /// </summary>
    public static class WeaponArtSpriteRegistryV1
    {
        private static readonly Dictionary<string, string> ResourceByArtId =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "weapon-art.blaster.side-v1", "WeaponArt/blaster_sp" },
                { "weapon-art.shotgun-basic.side-v1", "WeaponArt/shotgun_basic_sp" },
                { "weapon-art.rocket-launcher.side-v1", "WeaponArt/rocket_launcher_sp" },
                { "weapon-art.arc-rifle.side-v1", "WeaponArt/arc_rifle_sp" },
                { "weapon-art.ricochet-weapon.side-v1", "WeaponArt/ricochet_weapon_sp" },
            };

        private static readonly Dictionary<string, WeaponArtSpriteResolutionV1>
            Cache = new Dictionary<string, WeaponArtSpriteResolutionV1>(
                StringComparer.Ordinal);
        private static readonly HashSet<string> LoggedMissing =
            new HashSet<string>(StringComparer.Ordinal);
        private static Sprite fallbackSprite;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Cache.Clear();
            LoggedMissing.Clear();
            fallbackSprite = null;
        }

        public static WeaponArtSpriteResolutionV1 Preload(
            string artReferenceId)
        {
            string canonical = string.IsNullOrWhiteSpace(artReferenceId)
                ? string.Empty
                : artReferenceId.Trim();
            WeaponArtSpriteResolutionV1 cached;
            if (Cache.TryGetValue(canonical, out cached))
            {
                return cached;
            }

            string resourcePath;
            Sprite sprite = null;
            if (ResourceByArtId.TryGetValue(canonical, out resourcePath))
            {
                sprite = Resources.Load<Sprite>(resourcePath);
            }

            bool fallback = sprite == null;
            string diagnostic = fallback
                ? "weapon-art-sprite-missing:" + canonical
                : string.Empty;
            if (fallback)
            {
                sprite = GetFallbackSprite();
                if (LoggedMissing.Add(canonical))
                {
                    Debug.LogWarning(
                        "Weapon side-profile art could not be resolved for exact art ID '"
                        + canonical + "'. The generic silhouette will be used.");
                }
            }

            var resolution = new WeaponArtSpriteResolutionV1(
                canonical,
                sprite,
                fallback,
                diagnostic);
            Cache.Add(canonical, resolution);
            return resolution;
        }

        private static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            const int width = 128;
            const int height = 64;
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false)
            {
                name = "WeaponArtFallbackTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color metal = new Color(0.25f, 0.33f, 0.4f, 0.95f);
            Color glow = new Color(0.1f, 0.75f, 0.95f, 0.95f);
            var pixels = new Color[width * height];
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = clear;
            }
            for (int y = 24; y < 42; y++)
            {
                for (int x = 18; x < 106; x++)
                {
                    pixels[(y * width) + x] = metal;
                }
            }
            for (int y = 18; y < 48; y++)
            {
                for (int x = 72; x < 92; x++)
                {
                    pixels[(y * width) + x] = metal;
                }
            }
            for (int y = 28; y < 38; y++)
            {
                for (int x = 24; x < 58; x++)
                {
                    pixels[(y * width) + x] = glow;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            fallbackSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f);
            fallbackSprite.name = "WeaponArtFallbackSprite";
            fallbackSprite.hideFlags = HideFlags.HideAndDontSave;
            return fallbackSprite;
        }
    }
}

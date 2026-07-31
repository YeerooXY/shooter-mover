using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    /// <summary>
    /// Reusable runtime presentation catalog keyed by the canonical strongbox tier
    /// ContentStableId. It derives its roster exclusively from StrongboxCatalog and
    /// returns a safe silhouette whenever an authored/runtime sprite is unavailable.
    /// </summary>
    public static class BoxSprites
    {
        private const int TextureSize = 64;
        private static readonly Dictionary<StableId, Sprite> Sprites =
            new Dictionary<StableId, Sprite>();
        private static Sprite fallbackSprite;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeCache()
        {
            Sprites.Clear();
            fallbackSprite = null;
        }

        public static bool TryResolve(
            StableId contentStableId,
            out Sprite sprite,
            out string diagnostic)
        {
            StrongboxTier tier;
            if (contentStableId == null
                || !StrongboxCatalog.TryGet(contentStableId, out tier)
                || tier == null)
            {
                sprite = GetFallbackSprite();
                diagnostic = "strongbox-tier-sprite-tier-unresolved";
                return false;
            }

            if (Sprites.TryGetValue(tier.TierStableId, out sprite)
                && sprite != null)
            {
                diagnostic = string.Empty;
                return true;
            }

            try
            {
                sprite = CreateTierSprite(tier);
                if (sprite == null)
                {
                    sprite = GetFallbackSprite();
                    diagnostic = "strongbox-tier-sprite-generation-null";
                    return false;
                }

                Sprites[tier.TierStableId] = sprite;
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                sprite = GetFallbackSprite();
                diagnostic = "strongbox-tier-sprite-generation-failed:"
                    + exception.GetType().Name;
                return false;
            }
        }

        public static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            fallbackSprite = CreateSprite(
                "StrongboxTier_Fallback",
                delegate(int x, int y)
                {
                    return IsBoxShell(x, y)
                        || IsLid(x, y)
                        || IsLock(x, y);
                });
            return fallbackSprite;
        }

        private static Sprite CreateTierSprite(StrongboxTier tier)
        {
            if (tier == null)
            {
                return GetFallbackSprite();
            }

            return CreateSprite(
                "StrongboxTier_" + tier.Slug,
                delegate(int x, int y)
                {
                    if (IsBoxShell(x, y)
                        || IsLid(x, y)
                        || IsLock(x, y))
                    {
                        return true;
                    }

                    return IsTierEmblem(
                        x,
                        y,
                        tier.TierNumber);
                });
        }

        private static Sprite CreateSprite(
            string name,
            Func<int, int, bool> isFilled)
        {
            var texture = new Texture2D(
                TextureSize,
                TextureSize,
                TextureFormat.RGBA32,
                false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    pixels[y * TextureSize + x] = isFilled(x, y)
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                40f);
            sprite.name = name;
            return sprite;
        }

        private static bool IsBoxShell(int x, int y)
        {
            bool outer = x >= 7 && x <= 56
                && y >= 12 && y <= 47;
            bool inner = x >= 11 && x <= 52
                && y >= 16 && y <= 43;
            bool bottomBand = x >= 10 && x <= 53
                && y >= 16 && y <= 19;
            bool upperSeam = x >= 10 && x <= 53
                && y >= 40 && y <= 43;
            return (outer && !inner)
                || bottomBand
                || upperSeam;
        }

        private static bool IsLid(int x, int y)
        {
            return (x >= 11 && x <= 52
                    && y >= 45 && y <= 52)
                || (x >= 16 && x <= 47
                    && y >= 53 && y <= 55);
        }

        private static bool IsLock(int x, int y)
        {
            return x >= 28 && x <= 35
                && y >= 29 && y <= 39;
        }

        private static bool IsTierEmblem(
            int x,
            int y,
            int tierNumber)
        {
            if (tierNumber < 1)
            {
                return false;
            }

            int row = y - 21;
            int column = x - 17;
            if (row < 0 || row >= 6 || column < 0 || column >= 30)
            {
                return false;
            }

            int pip = column / 6;
            if (pip < 0 || pip >= 5)
            {
                return false;
            }

            int bitIndex = row < 3 ? pip : pip + 5;
            bool enabled = (tierNumber & (1 << bitIndex)) != 0;
            if (!enabled)
            {
                return false;
            }

            int localX = column % 6;
            int localY = row % 3;
            return localX >= 1 && localX <= 4
                && localY >= 0 && localY <= 2;
        }
    }
}

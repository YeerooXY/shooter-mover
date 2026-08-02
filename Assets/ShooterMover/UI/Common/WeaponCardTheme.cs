using System;
using UnityEngine;

namespace ShooterMover.UI.Common
{
    [CreateAssetMenu(
        menuName = "Shooter Mover/UI/Weapon Card Theme",
        fileName = "WeaponCardTheme")]
    public sealed class WeaponCardTheme : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public WeaponRarity Rarity;
            public Sprite Background;
            public Color BackgroundColor = Color.white;
            public Color GlowColor = Color.white;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public bool TryGet(
            WeaponRarity rarity,
            out Sprite background,
            out Color backgroundColor,
            out Color glowColor)
        {
            for (int index = 0; index < entries.Length; index++)
            {
                Entry entry = entries[index];
                if (entry != null && entry.Rarity == rarity)
                {
                    background = entry.Background;
                    backgroundColor = entry.BackgroundColor;
                    glowColor = entry.GlowColor;
                    return true;
                }
            }

            background = null;
            WeaponCardPalette.GetFallback(
                rarity,
                out backgroundColor,
                out glowColor);
            return false;
        }
    }

    public static class WeaponCardPalette
    {
        public static void GetFallback(
            WeaponRarity rarity,
            out Color background,
            out Color glow)
        {
            switch (rarity)
            {
                case WeaponRarity.Rare:
                    background = Hex(0x2F7DF6);
                    glow = Hex(0x72B2FF);
                    return;
                case WeaponRarity.Epic:
                    background = Hex(0x8B5CF6);
                    glow = Hex(0xC4B5FD);
                    return;
                case WeaponRarity.Legendary:
                    background = Hex(0xF0C419);
                    glow = Hex(0xFFE67A);
                    return;
                case WeaponRarity.Mythic:
                    background = Hex(0xE53D43);
                    glow = Hex(0xFF7A7F);
                    return;
                default:
                    background = Hex(0x858B94);
                    glow = Hex(0xC5CBD3);
                    return;
            }
        }

        private static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }
    }
}

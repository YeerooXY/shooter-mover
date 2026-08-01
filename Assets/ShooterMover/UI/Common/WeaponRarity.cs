using System;

namespace ShooterMover.UI.Common
{
    public enum WeaponRarity
    {
        Common = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Mythic = 5,
    }

    public static class WeaponRarityText
    {
        public static bool TryParseQualityId(
            string qualityId,
            out WeaponRarity rarity)
        {
            string value = (qualityId ?? string.Empty).Trim();
            if (value.EndsWith(".mythic", StringComparison.OrdinalIgnoreCase))
            {
                rarity = WeaponRarity.Mythic;
                return true;
            }
            if (value.EndsWith(".legendary", StringComparison.OrdinalIgnoreCase))
            {
                rarity = WeaponRarity.Legendary;
                return true;
            }
            if (value.EndsWith(".epic", StringComparison.OrdinalIgnoreCase))
            {
                rarity = WeaponRarity.Epic;
                return true;
            }
            if (value.EndsWith(".rare", StringComparison.OrdinalIgnoreCase))
            {
                rarity = WeaponRarity.Rare;
                return true;
            }
            if (value.EndsWith(".common", StringComparison.OrdinalIgnoreCase))
            {
                rarity = WeaponRarity.Common;
                return true;
            }

            rarity = WeaponRarity.Common;
            return false;
        }

        public static string Label(WeaponRarity rarity)
        {
            return rarity.ToString().ToUpperInvariant();
        }
    }
}

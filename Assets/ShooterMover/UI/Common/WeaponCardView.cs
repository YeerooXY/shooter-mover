using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.UI.Common
{
    public sealed class WeaponCardView
    {
        private static readonly IReadOnlyList<AugmentLine> EmptyAugments =
            Array.Empty<AugmentLine>();

        public WeaponCardView(
            string name,
            Sprite art,
            WeaponRarity rarity,
            int itemLevel,
            IReadOnlyList<AugmentLine> augments)
        {
            Name = name ?? string.Empty;
            Art = art;
            Rarity = rarity;
            ItemLevel = itemLevel;
            Augments = augments ?? EmptyAugments;
        }

        public string Name { get; }
        public Sprite Art { get; }
        public WeaponRarity Rarity { get; }
        public int ItemLevel { get; }
        public IReadOnlyList<AugmentLine> Augments { get; }

        public static WeaponCardView RarityOnly(WeaponRarity rarity)
        {
            return new WeaponCardView(
                string.Empty,
                null,
                rarity,
                -1,
                EmptyAugments);
        }
    }

    public enum WeaponCardDisplay
    {
        Roll = 1,
        Reveal = 2,
    }
}

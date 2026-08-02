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
            : this(name, art, rarity, itemLevel, augments, -1, -1, -1)
        {
        }

        public WeaponCardView(
            string name,
            Sprite art,
            WeaponRarity rarity,
            int itemLevel,
            IReadOnlyList<AugmentLine> augments,
            int augmentSlotCapacity,
            int augmentSharedLevel,
            int installedAugmentCount)
        {
            Name = name ?? string.Empty;
            Art = art;
            Rarity = rarity;
            ItemLevel = itemLevel;
            Augments = augments ?? EmptyAugments;
            AugmentSlotCapacity = augmentSlotCapacity;
            AugmentSharedLevel = augmentSharedLevel;
            InstalledAugmentCount = installedAugmentCount;
        }

        public string Name { get; }
        public Sprite Art { get; }
        public WeaponRarity Rarity { get; }
        public int ItemLevel { get; }
        public IReadOnlyList<AugmentLine> Augments { get; }
        public int AugmentSlotCapacity { get; }
        public int AugmentSharedLevel { get; }
        public int InstalledAugmentCount { get; }
        public bool HasAugmentSlots { get { return AugmentSlotCapacity >= 0; } }

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

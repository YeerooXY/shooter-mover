using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.UI.Common;

namespace ShooterMover.UI.StrongboxOpening
{
    public sealed class StrongboxRollPlan
    {
        private readonly ReadOnlyCollection<WeaponCardView> entries;

        internal StrongboxRollPlan(
            IEnumerable<WeaponCardView> entries,
            int winnerIndex,
            float tensionRandom01,
            float tensionStopIndex,
            uint presentationSeed)
        {
            this.entries = new ReadOnlyCollection<WeaponCardView>(
                new List<WeaponCardView>(entries ?? throw new ArgumentNullException(nameof(entries))));
            WinnerIndex = winnerIndex;
            TensionRandom01 = tensionRandom01;
            TensionStopIndex = tensionStopIndex;
            PresentationSeed = presentationSeed;
        }

        public IReadOnlyList<WeaponCardView> Entries { get { return entries; } }
        public int WinnerIndex { get; }
        public WeaponCardView Winner { get { return entries[WinnerIndex]; } }
        public float TensionRandom01 { get; }
        public float TensionStopIndex { get; }
        public uint PresentationSeed { get; }
    }

    public static class StrongboxRollPlanner
    {
        public static StrongboxRollPlan Create(
            WeaponCardView winner,
            string presentationIdentity,
            StrongboxRollSettings settings)
        {
            if (winner == null)
            {
                throw new ArgumentNullException(nameof(winner));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Validate();
            uint seed = StableSeed(presentationIdentity);
            var random = new StableRandom(seed);
            var entries = new List<WeaponCardView>(settings.EntryCount);
            for (int index = 0; index < settings.EntryCount; index++)
            {
                entries.Add(WeaponCardView.RarityOnly(RollRarity(ref random)));
            }
            entries[settings.WinnerIndex] = winner;

            float tensionRandom01 = random.Next01();
            float usableHalfCard = Math.Max(
                0f,
                settings.CardHeight * 0.5f - settings.EdgePaddingPixels);
            float signedOffset = tensionRandom01 * 2f - 1f;
            float offsetPixels = signedOffset * usableHalfCard;
            float tensionStopIndex = settings.WinnerIndex
                + offsetPixels / settings.CardStep;

            return new StrongboxRollPlan(
                entries,
                settings.WinnerIndex,
                tensionRandom01,
                tensionStopIndex,
                seed);
        }

        private static WeaponRarity RollRarity(ref StableRandom random)
        {
            // Temporary presentation weights. The authoritative weapon and rarity are
            // already resolved before this filler sequence is created.
            int value = random.NextInt(100);
            if (value < 55) return WeaponRarity.Common;
            if (value < 80) return WeaponRarity.Rare;
            if (value < 92) return WeaponRarity.Epic;
            if (value < 98) return WeaponRarity.Legendary;
            return WeaponRarity.Mythic;
        }

        private static uint StableSeed(string value)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= prime;
            }
            return hash == 0u ? 0x9E3779B9u : hash;
        }

        private struct StableRandom
        {
            private uint state;

            public StableRandom(uint seed)
            {
                state = seed == 0u ? 0x9E3779B9u : seed;
            }

            public uint NextUInt()
            {
                uint value = state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                state = value;
                return value;
            }

            public float Next01()
            {
                return (NextUInt() & 0x00FFFFFFu) / 16777216f;
            }

            public int NextInt(int exclusiveMaximum)
            {
                if (exclusiveMaximum <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
                }
                return (int)(NextUInt() % (uint)exclusiveMaximum);
            }
        }
    }
}

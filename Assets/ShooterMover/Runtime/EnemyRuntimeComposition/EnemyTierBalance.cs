using System;

namespace ShooterMover.EnemyRuntimeComposition
{
    /// <summary>
    /// The starter four-tier enemy balance. These are explicit authored tier values, not a
    /// player-level growth formula. A later enemy catalogue revision can move these values into
    /// each enemy family without changing room placement or runtime contracts.
    /// </summary>
    public static class EnemyTierBalance
    {
        private static readonly double[] Health = { 1d, 2d, 4d, 8d };
        private static readonly double[] Damage = { 1d, 1.5d, 2d, 3d };

        public static double HealthMultiplier(int tier)
        {
            return Resolve(Health, tier);
        }

        public static double DamageMultiplier(int tier)
        {
            return Resolve(Damage, tier);
        }

        private static double Resolve(double[] values, int tier)
        {
            if (tier < 1 || tier > values.Length)
                throw new ArgumentOutOfRangeException(nameof(tier));
            return values[tier - 1];
        }
    }
}

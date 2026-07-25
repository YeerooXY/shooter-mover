using System;

namespace ShooterMover.Domain.Weapons
{
    /// <summary>
    /// Result of one eligible Ricochet collision. The caller obtains a deterministic random result
    /// from the existing random authority only when RequiresFractionalRoll is true, then invokes
    /// ResolveEligibleRicochetCollision exactly once for that collision.
    /// </summary>
    public struct WeaponRicochetCollisionResolution
    {
        internal WeaponRicochetCollisionResolution(
            bool bounces,
            bool usedFractionalRoll,
            RicochetValue remaining)
        {
            Bounces = bounces;
            UsedFractionalRoll = usedFractionalRoll;
            Remaining = remaining;
        }

        public bool Bounces { get; }
        public bool UsedFractionalRoll { get; }
        public RicochetValue Remaining { get; }
    }

    /// <summary>
    /// Pure fixed-point rules shared by travelling projectiles, orbs, rockets, and lasers.
    /// This class does not own or call a random service. Runtime adapters must obtain at most one
    /// roll from the existing deterministic random authority for the one fractional remainder.
    /// </summary>
    public static class WeaponFixedPointBudgetRules
    {
        /// <summary>
        /// Resolves the maximum number of deterministically ordered enemies that may be affected.
        /// The integer Pierce capacity is always included. The fractional remainder can add one
        /// and only one extra target.
        /// </summary>
        public static int ResolvePierceTargetCapacity(
            PierceValue pierce,
            bool fractionalRollSucceeded)
        {
            return checked(
                pierce.GuaranteedHits
                + (pierce.HasFractionalAdditionalHitChance
                    && fractionalRollSucceeded
                        ? 1
                        : 0));
        }

        /// <summary>
        /// True only when the next eligible collision has no guaranteed bounce remaining and must
        /// consume the single fractional final-bounce chance.
        /// </summary>
        public static bool RequiresFractionalRicochetRoll(RicochetValue remaining)
        {
            return remaining.GuaranteedBounces == 0
                && remaining.HasFractionalFinalBounce;
        }

        /// <summary>
        /// Resolves one eligible collision without floating-point subtraction.
        ///
        /// A guaranteed bounce consumes exactly ten tenths. A fractional remainder is exhausted
        /// after one supplied deterministic roll regardless of success. An exhausted budget never
        /// bounces. The supplied roll result is ignored while guaranteed bounces remain.
        /// </summary>
        public static WeaponRicochetCollisionResolution ResolveEligibleRicochetCollision(
            RicochetValue remaining,
            bool fractionalRollSucceeded)
        {
            if (remaining.GuaranteedBounces > 0)
            {
                return new WeaponRicochetCollisionResolution(
                    true,
                    false,
                    new RicochetValue(remaining.Tenths - 10));
            }

            if (remaining.HasFractionalFinalBounce)
            {
                return new WeaponRicochetCollisionResolution(
                    fractionalRollSucceeded,
                    true,
                    new RicochetValue(0));
            }

            return new WeaponRicochetCollisionResolution(
                false,
                false,
                new RicochetValue(0));
        }

        /// <summary>
        /// Converts a deterministic unit-interval roll to the one fractional Pierce decision.
        /// This helper performs no random generation and exists only to centralise boundary rules.
        /// </summary>
        public static bool IsPierceFractionalRollSuccessful(
            PierceValue pierce,
            double deterministicUnitRoll)
        {
            ValidateUnitRoll(deterministicUnitRoll);
            return pierce.HasFractionalAdditionalHitChance
                && deterministicUnitRoll < pierce.FractionalAdditionalHitChance;
        }

        /// <summary>
        /// Converts a deterministic unit-interval roll to the one fractional Ricochet decision.
        /// This helper performs no random generation and exists only to centralise boundary rules.
        /// </summary>
        public static bool IsRicochetFractionalRollSuccessful(
            RicochetValue ricochet,
            double deterministicUnitRoll)
        {
            ValidateUnitRoll(deterministicUnitRoll);
            return RequiresFractionalRicochetRoll(ricochet)
                && deterministicUnitRoll < ricochet.FractionalFinalBounceChance;
        }

        private static void ValidateUnitRoll(double value)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value < 0d
                || value >= 1d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "A deterministic unit roll must be finite and in the range [0, 1).");
            }
        }
    }
}

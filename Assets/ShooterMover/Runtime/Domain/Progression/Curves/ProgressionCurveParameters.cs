using System;

namespace ShooterMover.Domain.Progression.Curves
{
    /// <summary>
    /// Defines a non-zero early tail and a smooth transition to full availability.
    /// </summary>
    public sealed class SoftActivationCurveParameters
    {
        public SoftActivationCurveParameters(
            double earlyTailWeight,
            long earlyTailLevels,
            long postNominalActivationLevels)
        {
            if (!ProgressionCurveValidation.IsFinite(earlyTailWeight)
                || earlyTailWeight <= 0.0
                || earlyTailWeight >= 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(earlyTailWeight),
                    "Early-tail weight must be finite and strictly between zero and one.");
            }

            if (earlyTailLevels <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(earlyTailLevels),
                    "Early-tail levels must be positive.");
            }

            if (postNominalActivationLevels <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(postNominalActivationLevels),
                    "Post-nominal activation levels must be positive.");
            }

            EarlyTailWeight = earlyTailWeight;
            EarlyTailLevels = earlyTailLevels;
            PostNominalActivationLevels = postNominalActivationLevels;
        }

        public double EarlyTailWeight { get; }

        public long EarlyTailLevels { get; }

        public long PostNominalActivationLevels { get; }
    }

    /// <summary>
    /// Defines exponential decay for older items with a positive retention floor.
    /// </summary>
    public sealed class ObsolescenceCurveParameters
    {
        public ObsolescenceCurveParameters(
            long decayStartsAfterLevels,
            double halfLifeLevels,
            double minimumRetention)
        {
            if (decayStartsAfterLevels < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(decayStartsAfterLevels),
                    "Decay start offset must not be negative.");
            }

            if (!ProgressionCurveValidation.IsFinite(halfLifeLevels) || halfLifeLevels <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(halfLifeLevels),
                    "Decay half-life must be finite and positive.");
            }

            if (!ProgressionCurveValidation.IsFinite(minimumRetention)
                || minimumRetention <= 0.0
                || minimumRetention > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumRetention),
                    "Minimum retention must be finite, positive, and at most one.");
            }

            DecayStartsAfterLevels = decayStartsAfterLevels;
            HalfLifeLevels = halfLifeLevels;
            MinimumRetention = minimumRetention;
        }

        public long DecayStartsAfterLevels { get; }

        public double HalfLifeLevels { get; }

        public double MinimumRetention { get; }
    }

    /// <summary>
    /// Combines activation, old-item retention, base weight, and positive source bias.
    /// Values are fixture/tuning inputs; this type contains no production balance defaults.
    /// </summary>
    public sealed class ItemEligibilityCurveParameters
    {
        public ItemEligibilityCurveParameters(
            SoftActivationCurveParameters activation,
            ObsolescenceCurveParameters obsolescence,
            double baseWeight,
            double sourceBias)
        {
            Activation = activation ?? throw new ArgumentNullException(nameof(activation));
            Obsolescence = obsolescence ?? throw new ArgumentNullException(nameof(obsolescence));

            if (!ProgressionCurveValidation.IsFinite(baseWeight) || baseWeight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseWeight),
                    "Base weight must be finite and positive.");
            }

            if (!ProgressionCurveValidation.IsFinite(sourceBias) || sourceBias <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceBias),
                    "Source bias must be finite and positive so it cannot create a hard gate.");
            }

            BaseWeight = baseWeight;
            SourceBias = sourceBias;
        }

        public SoftActivationCurveParameters Activation { get; }

        public ObsolescenceCurveParameters Obsolescence { get; }

        public double BaseWeight { get; }

        public double SourceBias { get; }
    }

    /// <summary>
    /// Shifts the same soft activation family later for crafting admission.
    /// </summary>
    public sealed class CraftingAvailabilityCurveParameters
    {
        public CraftingAvailabilityCurveParameters(
            SoftActivationCurveParameters activation,
            long delayLevels)
        {
            Activation = activation ?? throw new ArgumentNullException(nameof(activation));
            if (delayLevels <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(delayLevels),
                    "Crafting delay must be positive.");
            }

            DelayLevels = delayLevels;
        }

        public SoftActivationCurveParameters Activation { get; }

        public long DelayLevels { get; }
    }

    internal static class ProgressionCurveValidation
    {
        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static void EnsureLevel(long value, string parameterName)
        {
            if (value < 0L)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Progression levels must not be negative.");
            }
        }
    }
}

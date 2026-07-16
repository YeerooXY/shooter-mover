using System;

namespace ShooterMover.Domain.Progression.Curves
{
    /// <summary>
    /// Pure, engine-independent soft-progression mathematics. All methods are deterministic
    /// for equal inputs and contain no authored production tuning.
    /// </summary>
    public static class ProgressionCurveMath
    {
        public static double EvaluateItemEligibilityWeight(
            long currentLevel,
            long itemLevel,
            long nominalActivationLevel,
            ItemEligibilityCurveParameters parameters)
        {
            ProgressionCurveValidation.EnsureLevel(currentLevel, nameof(currentLevel));
            ProgressionCurveValidation.EnsureLevel(itemLevel, nameof(itemLevel));
            ProgressionCurveValidation.EnsureLevel(
                nominalActivationLevel,
                nameof(nominalActivationLevel));

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            double activation = EvaluateSoftActivation(
                currentLevel,
                nominalActivationLevel,
                parameters.Activation);
            double retention = EvaluateOldItemRetention(
                currentLevel,
                itemLevel,
                parameters.Obsolescence);
            double unbiasedWeight = activation
                * retention
                * parameters.BaseWeight;
            if (!ProgressionCurveValidation.IsFinite(unbiasedWeight))
            {
                throw new OverflowException("The item eligibility weight is not finite.");
            }

            return ApplySourceBias(unbiasedWeight, parameters.SourceBias);
        }

        public static double EvaluateSoftActivation(
            long currentLevel,
            long nominalActivationLevel,
            SoftActivationCurveParameters parameters)
        {
            ProgressionCurveValidation.EnsureLevel(currentLevel, nameof(currentLevel));
            ProgressionCurveValidation.EnsureLevel(
                nominalActivationLevel,
                nameof(nominalActivationLevel));

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            return EvaluateSoftActivationCore(
                SignedLevelDifference(currentLevel, nominalActivationLevel),
                parameters);
        }

        public static double EvaluateQualityAvailability(
            long currentLevel,
            long nominalQualityLevel,
            SoftActivationCurveParameters parameters)
        {
            return EvaluateSoftActivation(currentLevel, nominalQualityLevel, parameters);
        }

        public static double EvaluateOldItemRetention(
            long currentLevel,
            long itemLevel,
            ObsolescenceCurveParameters parameters)
        {
            ProgressionCurveValidation.EnsureLevel(currentLevel, nameof(currentLevel));
            ProgressionCurveValidation.EnsureLevel(itemLevel, nameof(itemLevel));

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            double decayAge = SignedLevelDifference(currentLevel, itemLevel)
                - parameters.DecayStartsAfterLevels;
            if (decayAge <= 0.0)
            {
                return 1.0;
            }

            double undecayedShare = Math.Pow(0.5, decayAge / parameters.HalfLifeLevels);
            double retention = parameters.MinimumRetention
                + ((1.0 - parameters.MinimumRetention) * undecayedShare);

            if (retention < parameters.MinimumRetention)
            {
                return parameters.MinimumRetention;
            }

            return retention > 1.0 ? 1.0 : retention;
        }

        public static double ApplySourceBias(double weight, double sourceBias)
        {
            if (!ProgressionCurveValidation.IsFinite(weight) || weight < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(weight),
                    "Weight must be finite and non-negative.");
            }

            if (!ProgressionCurveValidation.IsFinite(sourceBias) || sourceBias <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceBias),
                    "Source bias must be finite and positive.");
            }

            double result = weight * sourceBias;
            if (!ProgressionCurveValidation.IsFinite(result))
            {
                throw new OverflowException("The source-biased weight is not finite.");
            }

            return result;
        }

        public static double EvaluateCraftingAvailability(
            long currentLevel,
            long naturalNominalActivationLevel,
            CraftingAvailabilityCurveParameters parameters)
        {
            ProgressionCurveValidation.EnsureLevel(currentLevel, nameof(currentLevel));
            ProgressionCurveValidation.EnsureLevel(
                naturalNominalActivationLevel,
                nameof(naturalNominalActivationLevel));

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            double delayedRelativeLevel = SignedLevelDifference(
                currentLevel,
                naturalNominalActivationLevel) - parameters.DelayLevels;
            return EvaluateSoftActivationCore(
                delayedRelativeLevel,
                parameters.Activation);
        }

        private static double EvaluateSoftActivationCore(
            double relativeLevel,
            SoftActivationCurveParameters parameters)
        {
            double transitionStart = -parameters.EarlyTailLevels;
            double transitionEnd = parameters.PostNominalActivationLevels;

            if (relativeLevel <= transitionStart)
            {
                return parameters.EarlyTailWeight;
            }

            if (relativeLevel >= transitionEnd)
            {
                return 1.0;
            }

            double normalized = (relativeLevel - transitionStart)
                / (transitionEnd - transitionStart);
            double smoothStep = normalized * normalized * (3.0 - (2.0 * normalized));
            return parameters.EarlyTailWeight
                + ((1.0 - parameters.EarlyTailWeight) * smoothStep);
        }

        private static double SignedLevelDifference(long left, long right)
        {
            return left >= right
                ? (double)(left - right)
                : -(double)(right - left);
        }
    }
}

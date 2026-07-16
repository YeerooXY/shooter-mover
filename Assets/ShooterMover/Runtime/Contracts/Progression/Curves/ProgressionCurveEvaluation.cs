using System;

namespace ShooterMover.Contracts.Progression.Curves
{
    /// <summary>
    /// Immutable cross-boundary observation of the shared progression curve family.
    /// The owning domain mathematics produces these values; this contract does not contain
    /// alternate curve logic or production tuning.
    /// </summary>
    public sealed class ProgressionCurveEvaluation
    {
        public ProgressionCurveEvaluation(
            long currentLevel,
            long nominalActivationLevel,
            double naturalAvailability,
            double oldItemRetention,
            double sourceBiasedWeight,
            double craftingAvailability)
        {
            if (currentLevel < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(currentLevel));
            }

            if (nominalActivationLevel < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(nominalActivationLevel));
            }

            EnsureUnitValue(naturalAvailability, nameof(naturalAvailability));
            EnsureUnitValue(oldItemRetention, nameof(oldItemRetention));
            EnsureNonNegativeFinite(sourceBiasedWeight, nameof(sourceBiasedWeight));
            EnsureUnitValue(craftingAvailability, nameof(craftingAvailability));

            CurrentLevel = currentLevel;
            NominalActivationLevel = nominalActivationLevel;
            NaturalAvailability = naturalAvailability;
            OldItemRetention = oldItemRetention;
            SourceBiasedWeight = sourceBiasedWeight;
            CraftingAvailability = craftingAvailability;
        }

        public long CurrentLevel { get; }

        public long NominalActivationLevel { get; }

        public double NaturalAvailability { get; }

        public double OldItemRetention { get; }

        public double SourceBiasedWeight { get; }

        public double CraftingAvailability { get; }

        private static void EnsureUnitValue(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Availability and retention values must be finite and inside [0, 1].");
            }
        }

        private static void EnsureNonNegativeFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Weights must be finite and non-negative.");
            }
        }
    }
}

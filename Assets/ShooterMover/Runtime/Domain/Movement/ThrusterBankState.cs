using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Immutable, engine-independent state for a bank of independently regenerating thruster charges.
    /// </summary>
    public sealed class ThrusterBankState : IEquatable<ThrusterBankState>
    {
        private readonly decimal[] rechargeElapsedSeconds;
        private readonly decimal rechargeSeconds;

        private ThrusterBankState(
            StableId tuningIdentity,
            int baselineChargeCount,
            int additionalChargeCount,
            decimal rechargeSeconds,
            decimal[] rechargeElapsedSeconds)
        {
            if (tuningIdentity == null)
            {
                throw new ArgumentNullException(nameof(tuningIdentity));
            }

            if (baselineChargeCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baselineChargeCount),
                    baselineChargeCount,
                    "Baseline charge count must be at least one.");
            }

            if (additionalChargeCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(additionalChargeCount),
                    additionalChargeCount,
                    "Additional charge count cannot be negative.");
            }

            if (rechargeSeconds <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rechargeSeconds),
                    rechargeSeconds,
                    "Recharge duration must be greater than zero.");
            }

            if (rechargeElapsedSeconds == null)
            {
                throw new ArgumentNullException(nameof(rechargeElapsedSeconds));
            }

            TuningIdentity = tuningIdentity;
            BaselineChargeCount = baselineChargeCount;
            AdditionalChargeCount = additionalChargeCount;
            MaximumCharges = checked(baselineChargeCount + additionalChargeCount);
            this.rechargeSeconds = rechargeSeconds;

            if (rechargeElapsedSeconds.Length > MaximumCharges)
            {
                throw new ArgumentException(
                    "Regenerating charge count cannot exceed bank capacity.",
                    nameof(rechargeElapsedSeconds));
            }

            this.rechargeElapsedSeconds = new decimal[rechargeElapsedSeconds.Length];
            for (int index = 0; index < rechargeElapsedSeconds.Length; index++)
            {
                decimal elapsed = rechargeElapsedSeconds[index];
                if (elapsed < 0m || elapsed >= rechargeSeconds)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(rechargeElapsedSeconds),
                        elapsed,
                        "Stored recharge progress must be within the incomplete recharge interval.");
                }

                this.rechargeElapsedSeconds[index] = elapsed;
            }
        }

        public StableId TuningIdentity { get; }

        public int BaselineChargeCount { get; }

        public int AdditionalChargeCount { get; }

        public int MaximumCharges { get; }

        public int AvailableCharges => MaximumCharges - rechargeElapsedSeconds.Length;

        public int RegeneratingChargeCount => rechargeElapsedSeconds.Length;

        public double RechargeSeconds => (double)rechargeSeconds;

        public bool IsActivationEligible => AvailableCharges > 0;

        public bool IsFull => AvailableCharges == MaximumCharges;

        /// <summary>
        /// Creates a full bank with only the tuning profile's baseline charges enabled.
        /// </summary>
        public static ThrusterBankState CreateFull(MovementThrusterTuningProfile tuning)
        {
            return CreateFull(tuning, 0);
        }

        /// <summary>
        /// Creates a full bank with an authored number of additional charges within the tuning cap.
        /// </summary>
        public static ThrusterBankState CreateFull(
            MovementThrusterTuningProfile tuning,
            int additionalChargeCount)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            MovementThrusterTuningProfileValidator.Validate(tuning);

            if (additionalChargeCount < 0
                || additionalChargeCount > tuning.ThrusterMaximumAdditionalCharges)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(additionalChargeCount),
                    additionalChargeCount,
                    "Additional charge count must be within the tuning profile's authored cap.");
            }

            return new ThrusterBankState(
                tuning.DeterministicIdentity,
                tuning.ThrusterBaselineChargeCount,
                additionalChargeCount,
                (decimal)tuning.ThrusterRechargeSeconds,
                Array.Empty<decimal>());
        }

        /// <summary>
        /// Returns elapsed recharge seconds for an incomplete charge in deterministic consumption order.
        /// Index zero is the oldest still-regenerating charge.
        /// </summary>
        public double GetRechargeElapsedSeconds(int index)
        {
            return (double)GetRechargeElapsedDecimal(index);
        }

        /// <summary>
        /// Returns normalized recharge progress in the inclusive-zero, exclusive-one interval.
        /// </summary>
        public double GetRechargeFraction(int index)
        {
            return (double)(GetRechargeElapsedDecimal(index) / rechargeSeconds);
        }

        public bool Equals(ThrusterBankState other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null
                || !object.Equals(TuningIdentity, other.TuningIdentity)
                || BaselineChargeCount != other.BaselineChargeCount
                || AdditionalChargeCount != other.AdditionalChargeCount
                || rechargeSeconds != other.rechargeSeconds
                || rechargeElapsedSeconds.Length != other.rechargeElapsedSeconds.Length)
            {
                return false;
            }

            for (int index = 0; index < rechargeElapsedSeconds.Length; index++)
            {
                if (rechargeElapsedSeconds[index] != other.rechargeElapsedSeconds[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ThrusterBankState);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + TuningIdentity.GetHashCode();
                hash = (hash * 31) + BaselineChargeCount;
                hash = (hash * 31) + AdditionalChargeCount;
                hash = (hash * 31) + rechargeSeconds.GetHashCode();
                for (int index = 0; index < rechargeElapsedSeconds.Length; index++)
                {
                    hash = (hash * 31) + rechargeElapsedSeconds[index].GetHashCode();
                }

                return hash;
            }
        }

        public static bool operator ==(ThrusterBankState left, ThrusterBankState right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(ThrusterBankState left, ThrusterBankState right)
        {
            return !(left == right);
        }

        internal decimal RechargeSecondsDecimal => rechargeSeconds;

        internal decimal GetRechargeElapsedDecimal(int index)
        {
            if (index < 0 || index >= rechargeElapsedSeconds.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Recharge index is outside the bank.");
            }

            return rechargeElapsedSeconds[index];
        }

        internal ThrusterBankState WithRechargeElapsed(decimal[] nextRechargeElapsedSeconds)
        {
            return new ThrusterBankState(
                TuningIdentity,
                BaselineChargeCount,
                AdditionalChargeCount,
                rechargeSeconds,
                nextRechargeElapsedSeconds);
        }
    }
}

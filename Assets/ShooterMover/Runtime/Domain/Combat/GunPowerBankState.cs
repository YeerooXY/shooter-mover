using System;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Immutable empowered-fire resource owned by one mounted gun runtime.
    /// Normal fire is unlimited and is deliberately not represented by this state.
    /// </summary>
    public sealed class GunPowerBankState
    {
        private GunPowerBankState(
            bool isConfigured,
            double availableUnits,
            double capacityUnits,
            double empoweredCostUnits)
        {
            IsConfigured = isConfigured;
            AvailableUnits = availableUnits;
            CapacityUnits = capacityUnits;
            EmpoweredCostUnits = empoweredCostUnits;
        }

        public bool IsConfigured { get; }

        public double AvailableUnits { get; }

        public double CapacityUnits { get; }

        public double EmpoweredCostUnits { get; }

        public bool IsEmpty => IsConfigured && AvailableUnits == 0d;

        public bool CanAffordEmpoweredFire =>
            IsConfigured && AvailableUnits >= EmpoweredCostUnits;

        /// <summary>
        /// Creates one mount-local bank from the validated CB-001 runtime profile.
        /// An unconfigured profile accepts only zero available units.
        /// </summary>
        public static GunPowerBankState FromProfile(
            GunLiveProfile profile,
            double availableUnits)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            RequireFiniteNonNegative(availableUnits, nameof(availableUnits));

            if (!profile.HasIndependentPowerBank)
            {
                if (availableUnits != 0d)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(availableUnits),
                        availableUnits,
                        "A profile without an independent power bank cannot hold power.");
                }

                return None;
            }

            if (availableUnits > profile.PowerBankCapacityUnits)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(availableUnits),
                    availableUnits,
                    "Available power cannot exceed the validated profile capacity.");
            }

            return new GunPowerBankState(
                true,
                availableUnits,
                profile.PowerBankCapacityUnits,
                profile.EmpoweredCostUnits);
        }

        public static GunPowerBankState FullFromProfile(GunLiveProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return FromProfile(
                profile,
                profile.HasIndependentPowerBank ? profile.PowerBankCapacityUnits : 0d);
        }

        public static GunPowerBankState None
        {
            get { return new GunPowerBankState(false, 0d, 0d, 0d); }
        }

        internal GunPowerBankState WithAvailableUnits(double availableUnits)
        {
            if (!IsConfigured)
            {
                if (availableUnits != 0d)
                {
                    throw new InvalidOperationException(
                        "An unconfigured power bank cannot receive available units.");
                }

                return this;
            }

            RequireFiniteNonNegative(availableUnits, nameof(availableUnits));
            if (availableUnits > CapacityUnits)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(availableUnits),
                    availableUnits,
                    "Available power cannot exceed capacity.");
            }

            if (availableUnits == AvailableUnits)
            {
                return this;
            }

            return new GunPowerBankState(
                true,
                availableUnits,
                CapacityUnits,
                EmpoweredCostUnits);
        }

        internal static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Power units must be finite and non-negative.");
            }
        }
    }
}

using System;

namespace ShooterMover.Domain.Combat
{
    public enum WeaponPowerFireDecisionKind
    {
        NormalFired = 1,
        EmpoweredFired = 2,
        NormalFallbackPowerUnavailable = 3,
        NotReady = 4,
    }

    public enum WeaponPowerRefillEligibility
    {
        Ineligible = 1,
        AuthoredEligible = 2,
    }

    public enum WeaponPowerRefillResultKind
    {
        Applied = 1,
        NoChange = 2,
        IneligibleSource = 3,
        BankNotConfigured = 4,
    }

    /// <summary>
    /// Explicit refill request issued only after a later authored source has decided
    /// whether it is eligible. CB-003 does not discover pickups or regenerate power.
    /// </summary>
    public sealed class WeaponPowerRefillCommand
    {
        public WeaponPowerRefillCommand(
            double requestedUnits,
            WeaponPowerRefillEligibility eligibility)
        {
            WeaponPowerBankState.RequireFiniteNonNegative(
                requestedUnits,
                nameof(requestedUnits));

            if (!Enum.IsDefined(typeof(WeaponPowerRefillEligibility), eligibility))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(eligibility),
                    eligibility,
                    "Unknown refill eligibility.");
            }

            RequestedUnits = requestedUnits;
            Eligibility = eligibility;
        }

        public double RequestedUnits { get; }

        public WeaponPowerRefillEligibility Eligibility { get; }
    }

    public sealed class WeaponPowerFireDecision
    {
        internal WeaponPowerFireDecision(
            WeaponPowerFireDecisionKind kind,
            WeaponPowerBankState updatedState,
            double spentUnits)
        {
            if (!Enum.IsDefined(typeof(WeaponPowerFireDecisionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (updatedState == null)
            {
                throw new ArgumentNullException(nameof(updatedState));
            }

            WeaponPowerBankState.RequireFiniteNonNegative(spentUnits, nameof(spentUnits));

            Kind = kind;
            UpdatedState = updatedState;
            SpentUnits = spentUnits;
        }

        public WeaponPowerFireDecisionKind Kind { get; }

        public WeaponPowerBankState UpdatedState { get; }

        public double SpentUnits { get; }

        public bool Fires => Kind != WeaponPowerFireDecisionKind.NotReady;

        public bool FiresNormally =>
            Kind == WeaponPowerFireDecisionKind.NormalFired
            || Kind == WeaponPowerFireDecisionKind.NormalFallbackPowerUnavailable;

        public bool FiresEmpowered => Kind == WeaponPowerFireDecisionKind.EmpoweredFired;
    }

    public sealed class WeaponPowerRefillResult
    {
        internal WeaponPowerRefillResult(
            WeaponPowerRefillResultKind kind,
            WeaponPowerBankState updatedState,
            double appliedUnits,
            double unappliedUnits)
        {
            if (!Enum.IsDefined(typeof(WeaponPowerRefillResultKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (updatedState == null)
            {
                throw new ArgumentNullException(nameof(updatedState));
            }

            WeaponPowerBankState.RequireFiniteNonNegative(appliedUnits, nameof(appliedUnits));
            WeaponPowerBankState.RequireFiniteNonNegative(unappliedUnits, nameof(unappliedUnits));

            Kind = kind;
            UpdatedState = updatedState;
            AppliedUnits = appliedUnits;
            UnappliedUnits = unappliedUnits;
        }

        public WeaponPowerRefillResultKind Kind { get; }

        public WeaponPowerBankState UpdatedState { get; }

        public double AppliedUnits { get; }

        public double UnappliedUnits { get; }
    }

    /// <summary>
    /// Pure deterministic policy for one bank. Every call receives and returns only
    /// one mount-local state, which prevents cross-mount expenditure by construction.
    /// </summary>
    public static class WeaponPowerBankPolicy
    {
        public static WeaponPowerFireDecision ResolveFire(
            WeaponPowerBankState state,
            bool isMountReady,
            bool empoweredRequested)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!isMountReady)
            {
                return new WeaponPowerFireDecision(
                    WeaponPowerFireDecisionKind.NotReady,
                    state,
                    0d);
            }

            if (!empoweredRequested)
            {
                return new WeaponPowerFireDecision(
                    WeaponPowerFireDecisionKind.NormalFired,
                    state,
                    0d);
            }

            if (!state.CanAffordEmpoweredFire)
            {
                return new WeaponPowerFireDecision(
                    WeaponPowerFireDecisionKind.NormalFallbackPowerUnavailable,
                    state,
                    0d);
            }

            double remainingUnits = state.AvailableUnits - state.EmpoweredCostUnits;
            WeaponPowerBankState updatedState = state.WithAvailableUnits(remainingUnits);

            return new WeaponPowerFireDecision(
                WeaponPowerFireDecisionKind.EmpoweredFired,
                updatedState,
                state.EmpoweredCostUnits);
        }

        public static WeaponPowerRefillResult ApplyRefill(
            WeaponPowerBankState state,
            WeaponPowerRefillCommand command)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (command.Eligibility != WeaponPowerRefillEligibility.AuthoredEligible)
            {
                return new WeaponPowerRefillResult(
                    WeaponPowerRefillResultKind.IneligibleSource,
                    state,
                    0d,
                    command.RequestedUnits);
            }

            if (!state.IsConfigured)
            {
                return new WeaponPowerRefillResult(
                    WeaponPowerRefillResultKind.BankNotConfigured,
                    state,
                    0d,
                    command.RequestedUnits);
            }

            double availableCapacity = state.CapacityUnits - state.AvailableUnits;
            double appliedUnits = Math.Min(command.RequestedUnits, availableCapacity);
            double unappliedUnits = command.RequestedUnits - appliedUnits;

            if (appliedUnits == 0d)
            {
                return new WeaponPowerRefillResult(
                    WeaponPowerRefillResultKind.NoChange,
                    state,
                    0d,
                    unappliedUnits);
            }

            WeaponPowerBankState updatedState = state.WithAvailableUnits(
                state.AvailableUnits + appliedUnits);

            return new WeaponPowerRefillResult(
                WeaponPowerRefillResultKind.Applied,
                updatedState,
                appliedUnits,
                unappliedUnits);
        }
    }
}

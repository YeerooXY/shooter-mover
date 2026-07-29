using System;

namespace ShooterMover.Domain.Combat
{
    public enum GunPowerFireDecisionKind
    {
        NormalFired = 1,
        EmpoweredFired = 2,
        NormalFallbackPowerUnavailable = 3,
        NotReady = 4,
    }

    public enum GunPowerRefillEligibility
    {
        Ineligible = 1,
        AuthoredEligible = 2,
    }

    public enum GunPowerRefillResultKind
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
    public sealed class GunPowerRefillCommand
    {
        public GunPowerRefillCommand(
            double requestedUnits,
            GunPowerRefillEligibility eligibility)
        {
            GunPowerBankState.RequireFiniteNonNegative(
                requestedUnits,
                nameof(requestedUnits));

            if (!Enum.IsDefined(typeof(GunPowerRefillEligibility), eligibility))
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

        public GunPowerRefillEligibility Eligibility { get; }
    }

    public sealed class GunPowerFireDecision
    {
        internal GunPowerFireDecision(
            GunPowerFireDecisionKind kind,
            GunPowerBankState updatedState,
            double spentUnits)
        {
            if (!Enum.IsDefined(typeof(GunPowerFireDecisionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (updatedState == null)
            {
                throw new ArgumentNullException(nameof(updatedState));
            }

            GunPowerBankState.RequireFiniteNonNegative(spentUnits, nameof(spentUnits));

            Kind = kind;
            UpdatedState = updatedState;
            SpentUnits = spentUnits;
        }

        public GunPowerFireDecisionKind Kind { get; }

        public GunPowerBankState UpdatedState { get; }

        public double SpentUnits { get; }

        public bool Fires => Kind != GunPowerFireDecisionKind.NotReady;

        public bool FiresNormally =>
            Kind == GunPowerFireDecisionKind.NormalFired
            || Kind == GunPowerFireDecisionKind.NormalFallbackPowerUnavailable;

        public bool FiresEmpowered => Kind == GunPowerFireDecisionKind.EmpoweredFired;
    }

    public sealed class GunPowerRefillResult
    {
        internal GunPowerRefillResult(
            GunPowerRefillResultKind kind,
            GunPowerBankState updatedState,
            double appliedUnits,
            double unappliedUnits)
        {
            if (!Enum.IsDefined(typeof(GunPowerRefillResultKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (updatedState == null)
            {
                throw new ArgumentNullException(nameof(updatedState));
            }

            GunPowerBankState.RequireFiniteNonNegative(appliedUnits, nameof(appliedUnits));
            GunPowerBankState.RequireFiniteNonNegative(unappliedUnits, nameof(unappliedUnits));

            Kind = kind;
            UpdatedState = updatedState;
            AppliedUnits = appliedUnits;
            UnappliedUnits = unappliedUnits;
        }

        public GunPowerRefillResultKind Kind { get; }

        public GunPowerBankState UpdatedState { get; }

        public double AppliedUnits { get; }

        public double UnappliedUnits { get; }
    }

    /// <summary>
    /// Pure deterministic policy for one bank. Every call receives and returns only
    /// one mount-local state, which prevents cross-mount expenditure by construction.
    /// </summary>
    public static class GunPowerBankPolicy
    {
        public static GunPowerFireDecision ResolveFire(
            GunPowerBankState state,
            bool isMountReady,
            bool empoweredRequested)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!isMountReady)
            {
                return new GunPowerFireDecision(
                    GunPowerFireDecisionKind.NotReady,
                    state,
                    0d);
            }

            if (!empoweredRequested)
            {
                return new GunPowerFireDecision(
                    GunPowerFireDecisionKind.NormalFired,
                    state,
                    0d);
            }

            if (!state.CanAffordEmpoweredFire)
            {
                return new GunPowerFireDecision(
                    GunPowerFireDecisionKind.NormalFallbackPowerUnavailable,
                    state,
                    0d);
            }

            double remainingUnits = state.AvailableUnits - state.EmpoweredCostUnits;
            GunPowerBankState updatedState = state.WithAvailableUnits(remainingUnits);

            return new GunPowerFireDecision(
                GunPowerFireDecisionKind.EmpoweredFired,
                updatedState,
                state.EmpoweredCostUnits);
        }

        public static GunPowerRefillResult ApplyRefill(
            GunPowerBankState state,
            GunPowerRefillCommand command)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (command.Eligibility != GunPowerRefillEligibility.AuthoredEligible)
            {
                return new GunPowerRefillResult(
                    GunPowerRefillResultKind.IneligibleSource,
                    state,
                    0d,
                    command.RequestedUnits);
            }

            if (!state.IsConfigured)
            {
                return new GunPowerRefillResult(
                    GunPowerRefillResultKind.BankNotConfigured,
                    state,
                    0d,
                    command.RequestedUnits);
            }

            double availableCapacity = state.CapacityUnits - state.AvailableUnits;
            double appliedUnits = Math.Min(command.RequestedUnits, availableCapacity);
            double unappliedUnits = command.RequestedUnits - appliedUnits;

            if (appliedUnits == 0d)
            {
                return new GunPowerRefillResult(
                    GunPowerRefillResultKind.NoChange,
                    state,
                    0d,
                    unappliedUnits);
            }

            GunPowerBankState updatedState = state.WithAvailableUnits(
                state.AvailableUnits + appliedUnits);

            return new GunPowerRefillResult(
                GunPowerRefillResultKind.Applied,
                updatedState,
                appliedUnits,
                unappliedUnits);
        }
    }
}

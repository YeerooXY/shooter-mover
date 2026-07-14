using System;
using System.Globalization;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// The complete operational phase of one independently simulated weapon mount.
    /// Cross-mount coordination is deliberately absent from this type.
    /// </summary>
    public enum WeaponMountPhase
    {
        Ready = 1,
        Firing = 2,
        Recovering = 3,
        Depleted = 4,
        Faulted = 5,
    }

    public enum WeaponMountFaultKind
    {
        ExternalFault = 1,
        InvalidElapsedTime = 2,
        MalformedState = 3,
        TransitionBudgetExceeded = 4,
        NumericalFailure = 5,
    }

    /// <summary>
    /// Stable, actionable reason why one mount failed closed.
    /// </summary>
    public sealed class WeaponMountFault
    {
        public WeaponMountFault(WeaponMountFaultKind kind, string detail)
        {
            if (!Enum.IsDefined(typeof(WeaponMountFaultKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown weapon-mount fault kind.");
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                throw new ArgumentException("A weapon-mount fault requires actionable detail.", nameof(detail));
            }

            Kind = kind;
            Detail = detail;
        }

        public WeaponMountFaultKind Kind { get; }

        public string Detail { get; }

        public override string ToString()
        {
            return Kind + ": " + Detail;
        }
    }

    /// <summary>
    /// Input sampled for one fixed step. FireRequested is not queued: if the mount is
    /// blocked for the whole step, the request expires with that step.
    /// </summary>
    public readonly struct WeaponMountStepInput
    {
        public WeaponMountStepInput(bool fireRequested, string externalFaultDetail = null)
        {
            FireRequested = fireRequested;
            ExternalFault = externalFaultDetail == null
                ? null
                : new WeaponMountFault(WeaponMountFaultKind.ExternalFault, externalFaultDetail);
        }

        public bool FireRequested { get; }

        public WeaponMountFault ExternalFault { get; }

        public bool HasExternalFault => ExternalFault != null;

        public static WeaponMountStepInput Idle
        {
            get { return new WeaponMountStepInput(false); }
        }

        public static WeaponMountStepInput Fire
        {
            get { return new WeaponMountStepInput(true); }
        }

        public static WeaponMountStepInput Fault(string detail)
        {
            return new WeaponMountStepInput(false, detail);
        }
    }

    /// <summary>
    /// Immutable runtime state for exactly one mounted weapon. All timers and cycle
    /// resources belong to this mount instance only.
    /// </summary>
    public sealed class WeaponMountState
    {
        private WeaponMountState(
            WeaponMountPhase phase,
            double cadenceRemainingSeconds,
            int burstShotsRemaining,
            double burstIntervalRemainingSeconds,
            double recoveryRemainingSeconds,
            double heatUnits,
            bool heatRecoveryLocked,
            double chargeProgressSeconds,
            long totalShotsFired,
            long totalCyclesStarted,
            WeaponMountFault fault)
        {
            Phase = phase;
            CadenceRemainingSeconds = cadenceRemainingSeconds;
            BurstShotsRemaining = burstShotsRemaining;
            BurstIntervalRemainingSeconds = burstIntervalRemainingSeconds;
            RecoveryRemainingSeconds = recoveryRemainingSeconds;
            HeatUnits = heatUnits;
            HeatRecoveryLocked = heatRecoveryLocked;
            ChargeProgressSeconds = chargeProgressSeconds;
            TotalShotsFired = totalShotsFired;
            TotalCyclesStarted = totalCyclesStarted;
            Fault = fault;
        }

        public WeaponMountPhase Phase { get; }

        /// <summary>
        /// Minimum cycle-start cadence still outstanding from the most recent cycle.
        /// </summary>
        public double CadenceRemainingSeconds { get; }

        /// <summary>
        /// Shots still scheduled in the active burst, excluding shots already emitted.
        /// </summary>
        public int BurstShotsRemaining { get; }

        public double BurstIntervalRemainingSeconds { get; }

        /// <summary>
        /// Post-cycle or interrupted-burst recovery still outstanding.
        /// </summary>
        public double RecoveryRemainingSeconds { get; }

        public double HeatUnits { get; }

        /// <summary>
        /// Once maximum heat is reached, the mount remains depleted until heat fully
        /// returns to zero. This gives overheat recovery a deterministic boundary.
        /// </summary>
        public bool HeatRecoveryLocked { get; }

        /// <summary>
        /// Charge accumulated since the most recent cycle start. A charge-mode mount is
        /// eligible only when this equals the profile's ChargeSeconds value.
        /// </summary>
        public double ChargeProgressSeconds { get; }

        public long TotalShotsFired { get; }

        public long TotalCyclesStarted { get; }

        public WeaponMountFault Fault { get; }

        public bool IsReady => Phase == WeaponMountPhase.Ready;

        public bool IsFaulted => Phase == WeaponMountPhase.Faulted;

        public static WeaponMountState Initial(WeaponRuntimeProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            double initialCharge = profile.CycleMode == WeaponCycleMode.Charge
                ? profile.ChargeSeconds
                : 0d;

            return new WeaponMountState(
                WeaponMountPhase.Ready,
                0d,
                0,
                0d,
                0d,
                0d,
                false,
                initialCharge,
                0L,
                0L,
                null);
        }

        /// <summary>
        /// Deterministic human-readable trace row used by tests and evidence capture.
        /// </summary>
        public string ToTraceString()
        {
            string faultText = Fault == null ? "none" : Fault.Kind.ToString();
            return string.Format(
                CultureInfo.InvariantCulture,
                "phase={0};cadence={1:R};burst={2};burst_wait={3:R};recovery={4:R};heat={5:R};heat_lock={6};charge={7:R};shots={8};cycles={9};fault={10}",
                Phase,
                CadenceRemainingSeconds,
                BurstShotsRemaining,
                BurstIntervalRemainingSeconds,
                RecoveryRemainingSeconds,
                HeatUnits,
                HeatRecoveryLocked ? "true" : "false",
                ChargeProgressSeconds,
                TotalShotsFired,
                TotalCyclesStarted,
                faultText);
        }

        public override string ToString()
        {
            return ToTraceString();
        }

        internal static WeaponMountState CreateRuntime(
            WeaponMountPhase phase,
            double cadenceRemainingSeconds,
            int burstShotsRemaining,
            double burstIntervalRemainingSeconds,
            double recoveryRemainingSeconds,
            double heatUnits,
            bool heatRecoveryLocked,
            double chargeProgressSeconds,
            long totalShotsFired,
            long totalCyclesStarted)
        {
            return new WeaponMountState(
                phase,
                cadenceRemainingSeconds,
                burstShotsRemaining,
                burstIntervalRemainingSeconds,
                recoveryRemainingSeconds,
                heatUnits,
                heatRecoveryLocked,
                chargeProgressSeconds,
                totalShotsFired,
                totalCyclesStarted,
                null);
        }

        internal static WeaponMountState CreateFaulted(
            WeaponMountFault fault,
            long totalShotsFired,
            long totalCyclesStarted)
        {
            if (fault == null)
            {
                throw new ArgumentNullException(nameof(fault));
            }

            return new WeaponMountState(
                WeaponMountPhase.Faulted,
                0d,
                0,
                0d,
                0d,
                0d,
                false,
                0d,
                totalShotsFired < 0L ? 0L : totalShotsFired,
                totalCyclesStarted < 0L ? 0L : totalCyclesStarted,
                fault);
        }
    }

    /// <summary>
    /// Immutable result of advancing one mount. Shot counts are emissions for this step
    /// only; cumulative counts remain on State for deterministic tracing.
    /// </summary>
    public sealed class WeaponMountStepResult
    {
        internal WeaponMountStepResult(
            WeaponMountState state,
            int shotsFired,
            int cyclesStarted,
            bool burstInterrupted,
            int transitionCount)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            ShotsFired = shotsFired;
            CyclesStarted = cyclesStarted;
            BurstInterrupted = burstInterrupted;
            TransitionCount = transitionCount;
        }

        public WeaponMountState State { get; }

        public int ShotsFired { get; }

        public int CyclesStarted { get; }

        public bool BurstInterrupted { get; }

        public int TransitionCount { get; }

        public WeaponMountFault Fault => State.Fault;

        public bool Succeeded => !State.IsFaulted;
    }
}

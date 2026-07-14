using System;
using System.Globalization;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Deterministically advances exactly one weapon mount. The API contains no array,
    /// slot lookup, shared timer, or reference to another mount.
    /// </summary>
    public static class WeaponMountStepper
    {
        public const int MaximumTransitionsPerStep = 100000;

        private const double BoundaryEpsilon = 0.000000000001d;

        public static WeaponMountStepResult Step(
            WeaponRuntimeProfile profile,
            WeaponMountState state,
            double elapsedSeconds,
            bool fireRequested)
        {
            return Step(
                profile,
                state,
                elapsedSeconds,
                new WeaponMountStepInput(fireRequested));
        }

        public static WeaponMountStepResult Step(
            WeaponRuntimeProfile profile,
            WeaponMountState state,
            double elapsedSeconds,
            WeaponMountStepInput input)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            long startingTotalShots = state.TotalShotsFired;
            long startingTotalCycles = state.TotalCyclesStarted;

            string stateDiagnostic;
            if (!TryValidateState(profile, state, out stateDiagnostic))
            {
                return FaultClosed(
                    WeaponMountFaultKind.MalformedState,
                    stateDiagnostic,
                    startingTotalShots,
                    startingTotalCycles);
            }

            if (state.IsFaulted)
            {
                return new WeaponMountStepResult(state, 0, 0, false, 0);
            }

            if (!IsFinite(elapsedSeconds) || elapsedSeconds < 0d)
            {
                return FaultClosed(
                    WeaponMountFaultKind.InvalidElapsedTime,
                    "elapsedSeconds must be finite and non-negative; received "
                        + FormatDouble(elapsedSeconds)
                        + ".",
                    startingTotalShots,
                    startingTotalCycles);
            }

            if (input.HasExternalFault)
            {
                return new WeaponMountStepResult(
                    WeaponMountState.CreateFaulted(
                        input.ExternalFault,
                        startingTotalShots,
                        startingTotalCycles),
                    0,
                    0,
                    false,
                    1);
            }

            double cadenceRemaining = state.CadenceRemainingSeconds;
            int burstShotsRemaining = state.BurstShotsRemaining;
            double burstIntervalRemaining = state.BurstIntervalRemainingSeconds;
            double recoveryRemaining = state.RecoveryRemainingSeconds;
            double heatUnits = state.HeatUnits;
            bool heatRecoveryLocked = state.HeatRecoveryLocked;
            double chargeProgress = state.ChargeProgressSeconds;
            long totalShots = state.TotalShotsFired;
            long totalCycles = state.TotalCyclesStarted;

            double elapsedRemaining = elapsedSeconds;
            int shotsFired = 0;
            int cyclesStarted = 0;
            bool burstInterrupted = false;
            int iterations = 0;

            while (true)
            {
                iterations++;
                if (iterations > MaximumTransitionsPerStep)
                {
                    return FaultClosed(
                        WeaponMountFaultKind.TransitionBudgetExceeded,
                        "One fixed step exceeded the transition budget of "
                            + MaximumTransitionsPerStep.ToString(CultureInfo.InvariantCulture)
                            + ". Reduce elapsedSeconds or increase authored cadence and burst intervals.",
                        startingTotalShots,
                        startingTotalCycles);
                }

                NormalizeRuntimeValues(
                    profile,
                    ref cadenceRemaining,
                    ref burstIntervalRemaining,
                    ref recoveryRemaining,
                    ref heatUnits,
                    ref heatRecoveryLocked,
                    ref chargeProgress);

                if (burstShotsRemaining > 0 && !input.FireRequested)
                {
                    burstShotsRemaining = 0;
                    burstIntervalRemaining = 0d;
                    recoveryRemaining = Math.Max(recoveryRemaining, profile.RecoverySeconds);
                    burstInterrupted = true;
                    continue;
                }

                WeaponMountPhase phase = DerivePhase(
                    profile,
                    cadenceRemaining,
                    burstShotsRemaining,
                    recoveryRemaining,
                    heatRecoveryLocked,
                    chargeProgress);

                if (phase == WeaponMountPhase.Ready && input.FireRequested)
                {
                    if (totalShots == long.MaxValue || totalCycles == long.MaxValue)
                    {
                        return FaultClosed(
                            WeaponMountFaultKind.NumericalFailure,
                            "Cumulative weapon-mount counters reached Int64 capacity.",
                            startingTotalShots,
                            startingTotalCycles);
                    }

                    totalCycles++;
                    cyclesStarted++;
                    cadenceRemaining = profile.CadenceSeconds;

                    EmitShot(
                        profile,
                        ref heatUnits,
                        ref heatRecoveryLocked,
                        ref chargeProgress);
                    totalShots++;
                    shotsFired++;

                    if (profile.BurstShotCount > 1 && !heatRecoveryLocked)
                    {
                        burstShotsRemaining = profile.BurstShotCount - 1;
                        burstIntervalRemaining = profile.BurstShotIntervalSeconds;
                    }
                    else
                    {
                        if (profile.BurstShotCount > 1 && heatRecoveryLocked)
                        {
                            burstInterrupted = true;
                        }

                        burstShotsRemaining = 0;
                        burstIntervalRemaining = 0d;
                        recoveryRemaining = Math.Max(recoveryRemaining, profile.RecoverySeconds);
                    }

                    continue;
                }

                if (elapsedRemaining <= BoundaryEpsilon)
                {
                    elapsedRemaining = 0d;
                    break;
                }

                double advanceSeconds = FindNextAdvance(
                    profile,
                    elapsedRemaining,
                    cadenceRemaining,
                    burstShotsRemaining,
                    burstIntervalRemaining,
                    recoveryRemaining,
                    heatUnits,
                    heatRecoveryLocked,
                    chargeProgress);

                if (!IsFinite(advanceSeconds) || advanceSeconds <= 0d)
                {
                    return FaultClosed(
                        WeaponMountFaultKind.NumericalFailure,
                        "The state machine could not find a positive finite event boundary.",
                        startingTotalShots,
                        startingTotalCycles);
                }

                AdvanceContinuousState(
                    profile,
                    advanceSeconds,
                    ref cadenceRemaining,
                    ref burstIntervalRemaining,
                    ref recoveryRemaining,
                    ref heatUnits,
                    ref heatRecoveryLocked,
                    ref chargeProgress);
                elapsedRemaining = DecreaseToZero(elapsedRemaining, advanceSeconds);

                if (burstShotsRemaining > 0
                    && burstIntervalRemaining <= BoundaryEpsilon)
                {
                    if (totalShots == long.MaxValue)
                    {
                        return FaultClosed(
                            WeaponMountFaultKind.NumericalFailure,
                            "The cumulative shot counter reached Int64 capacity.",
                            startingTotalShots,
                            startingTotalCycles);
                    }

                    burstShotsRemaining--;
                    EmitShot(
                        profile,
                        ref heatUnits,
                        ref heatRecoveryLocked,
                        ref chargeProgress);
                    totalShots++;
                    shotsFired++;

                    if (burstShotsRemaining > 0 && heatRecoveryLocked)
                    {
                        burstShotsRemaining = 0;
                        burstIntervalRemaining = 0d;
                        recoveryRemaining = Math.Max(recoveryRemaining, profile.RecoverySeconds);
                        burstInterrupted = true;
                    }
                    else if (burstShotsRemaining > 0)
                    {
                        burstIntervalRemaining = profile.BurstShotIntervalSeconds;
                    }
                    else
                    {
                        burstIntervalRemaining = 0d;
                        recoveryRemaining = Math.Max(recoveryRemaining, profile.RecoverySeconds);
                    }
                }
            }

            NormalizeRuntimeValues(
                profile,
                ref cadenceRemaining,
                ref burstIntervalRemaining,
                ref recoveryRemaining,
                ref heatUnits,
                ref heatRecoveryLocked,
                ref chargeProgress);

            WeaponMountPhase finalPhase = DerivePhase(
                profile,
                cadenceRemaining,
                burstShotsRemaining,
                recoveryRemaining,
                heatRecoveryLocked,
                chargeProgress);

            WeaponMountState finalState = WeaponMountState.CreateRuntime(
                finalPhase,
                cadenceRemaining,
                burstShotsRemaining,
                burstIntervalRemaining,
                recoveryRemaining,
                heatUnits,
                heatRecoveryLocked,
                chargeProgress,
                totalShots,
                totalCycles);

            string finalDiagnostic;
            if (!TryValidateState(profile, finalState, out finalDiagnostic))
            {
                return FaultClosed(
                    WeaponMountFaultKind.NumericalFailure,
                    "Stepper produced an invalid state: " + finalDiagnostic,
                    startingTotalShots,
                    startingTotalCycles);
            }

            return new WeaponMountStepResult(
                finalState,
                shotsFired,
                cyclesStarted,
                burstInterrupted,
                iterations);
        }

        private static WeaponMountStepResult FaultClosed(
            WeaponMountFaultKind kind,
            string detail,
            long totalShots,
            long totalCycles)
        {
            WeaponMountFault fault = new WeaponMountFault(kind, detail);
            return new WeaponMountStepResult(
                WeaponMountState.CreateFaulted(fault, totalShots, totalCycles),
                0,
                0,
                false,
                1);
        }

        private static void EmitShot(
            WeaponRuntimeProfile profile,
            ref double heatUnits,
            ref bool heatRecoveryLocked,
            ref double chargeProgress)
        {
            if (profile.CycleMode == WeaponCycleMode.Heat)
            {
                double remainingCapacity = profile.HeatCapacityUnits - heatUnits;
                if (profile.HeatPerShotUnits >= remainingCapacity - BoundaryEpsilon)
                {
                    heatUnits = profile.HeatCapacityUnits;
                    heatRecoveryLocked = true;
                }
                else
                {
                    heatUnits += profile.HeatPerShotUnits;
                }
            }
            else if (profile.CycleMode == WeaponCycleMode.Charge)
            {
                chargeProgress = 0d;
            }
        }

        private static double FindNextAdvance(
            WeaponRuntimeProfile profile,
            double elapsedRemaining,
            double cadenceRemaining,
            int burstShotsRemaining,
            double burstIntervalRemaining,
            double recoveryRemaining,
            double heatUnits,
            bool heatRecoveryLocked,
            double chargeProgress)
        {
            double result = elapsedRemaining;
            result = EarlierPositiveBoundary(result, cadenceRemaining);
            result = EarlierPositiveBoundary(result, recoveryRemaining);

            if (burstShotsRemaining > 0)
            {
                result = EarlierPositiveBoundary(result, burstIntervalRemaining);
            }

            if (profile.CycleMode == WeaponCycleMode.Heat
                && heatRecoveryLocked
                && heatUnits > BoundaryEpsilon)
            {
                result = EarlierPositiveBoundary(
                    result,
                    heatUnits / profile.HeatRecoveryUnitsPerSecond);
            }

            if (profile.CycleMode == WeaponCycleMode.Charge
                && chargeProgress < profile.ChargeSeconds - BoundaryEpsilon)
            {
                result = EarlierPositiveBoundary(
                    result,
                    profile.ChargeSeconds - chargeProgress);
            }

            return result;
        }

        private static double EarlierPositiveBoundary(double current, double candidate)
        {
            if (candidate > BoundaryEpsilon && candidate < current)
            {
                return candidate;
            }

            return current;
        }

        private static void AdvanceContinuousState(
            WeaponRuntimeProfile profile,
            double elapsedSeconds,
            ref double cadenceRemaining,
            ref double burstIntervalRemaining,
            ref double recoveryRemaining,
            ref double heatUnits,
            ref bool heatRecoveryLocked,
            ref double chargeProgress)
        {
            cadenceRemaining = DecreaseToZero(cadenceRemaining, elapsedSeconds);
            burstIntervalRemaining = DecreaseToZero(burstIntervalRemaining, elapsedSeconds);
            recoveryRemaining = DecreaseToZero(recoveryRemaining, elapsedSeconds);

            if (profile.CycleMode == WeaponCycleMode.Heat && heatUnits > 0d)
            {
                double timeToZero = heatUnits / profile.HeatRecoveryUnitsPerSecond;
                if (elapsedSeconds >= timeToZero - BoundaryEpsilon)
                {
                    heatUnits = 0d;
                    heatRecoveryLocked = false;
                }
                else
                {
                    heatUnits -= profile.HeatRecoveryUnitsPerSecond * elapsedSeconds;
                }
            }

            if (profile.CycleMode == WeaponCycleMode.Charge
                && chargeProgress < profile.ChargeSeconds)
            {
                double chargeRemaining = profile.ChargeSeconds - chargeProgress;
                if (elapsedSeconds >= chargeRemaining - BoundaryEpsilon)
                {
                    chargeProgress = profile.ChargeSeconds;
                }
                else
                {
                    chargeProgress += elapsedSeconds;
                }
            }
        }

        private static double DecreaseToZero(double value, double amount)
        {
            return amount >= value - BoundaryEpsilon ? 0d : value - amount;
        }

        private static void NormalizeRuntimeValues(
            WeaponRuntimeProfile profile,
            ref double cadenceRemaining,
            ref double burstIntervalRemaining,
            ref double recoveryRemaining,
            ref double heatUnits,
            ref bool heatRecoveryLocked,
            ref double chargeProgress)
        {
            cadenceRemaining = NormalizeNonNegative(cadenceRemaining);
            burstIntervalRemaining = NormalizeNonNegative(burstIntervalRemaining);
            recoveryRemaining = NormalizeNonNegative(recoveryRemaining);
            heatUnits = NormalizeNonNegative(heatUnits);
            chargeProgress = NormalizeNonNegative(chargeProgress);

            if (profile.CycleMode != WeaponCycleMode.Heat)
            {
                heatUnits = 0d;
                heatRecoveryLocked = false;
            }
            else
            {
                if (heatUnits > profile.HeatCapacityUnits)
                {
                    heatUnits = profile.HeatCapacityUnits;
                }

                if (heatUnits <= BoundaryEpsilon)
                {
                    heatUnits = 0d;
                    heatRecoveryLocked = false;
                }
            }

            if (profile.CycleMode != WeaponCycleMode.Charge)
            {
                chargeProgress = 0d;
            }
            else if (chargeProgress >= profile.ChargeSeconds - BoundaryEpsilon)
            {
                chargeProgress = profile.ChargeSeconds;
            }
        }

        private static double NormalizeNonNegative(double value)
        {
            return value <= BoundaryEpsilon ? 0d : value;
        }

        private static WeaponMountPhase DerivePhase(
            WeaponRuntimeProfile profile,
            double cadenceRemaining,
            int burstShotsRemaining,
            double recoveryRemaining,
            bool heatRecoveryLocked,
            double chargeProgress)
        {
            if (burstShotsRemaining > 0)
            {
                return WeaponMountPhase.Firing;
            }

            if (profile.CycleMode == WeaponCycleMode.Heat && heatRecoveryLocked)
            {
                return WeaponMountPhase.Depleted;
            }

            if (profile.CycleMode == WeaponCycleMode.Charge
                && chargeProgress < profile.ChargeSeconds - BoundaryEpsilon)
            {
                return WeaponMountPhase.Depleted;
            }

            if (cadenceRemaining > BoundaryEpsilon || recoveryRemaining > BoundaryEpsilon)
            {
                return WeaponMountPhase.Recovering;
            }

            return WeaponMountPhase.Ready;
        }

        private static bool TryValidateState(
            WeaponRuntimeProfile profile,
            WeaponMountState state,
            out string diagnostic)
        {
            if (!Enum.IsDefined(typeof(WeaponMountPhase), state.Phase))
            {
                diagnostic = "Phase contains an unknown enum value.";
                return false;
            }

            if (state.TotalShotsFired < 0L || state.TotalCyclesStarted < 0L)
            {
                diagnostic = "Cumulative shot and cycle counters cannot be negative.";
                return false;
            }

            if (state.Phase == WeaponMountPhase.Faulted)
            {
                if (state.Fault == null)
                {
                    diagnostic = "A faulted state requires an actionable fault diagnostic.";
                    return false;
                }

                diagnostic = null;
                return true;
            }

            if (state.Fault != null)
            {
                diagnostic = "A non-faulted state cannot carry a fault diagnostic.";
                return false;
            }

            if (!IsFiniteNonNegative(state.CadenceRemainingSeconds)
                || !IsFiniteNonNegative(state.BurstIntervalRemainingSeconds)
                || !IsFiniteNonNegative(state.RecoveryRemainingSeconds)
                || !IsFiniteNonNegative(state.HeatUnits)
                || !IsFiniteNonNegative(state.ChargeProgressSeconds))
            {
                diagnostic = "All timers and cycle-resource values must be finite and non-negative.";
                return false;
            }

            if (state.BurstShotsRemaining < 0
                || state.BurstShotsRemaining >= profile.BurstShotCount)
            {
                diagnostic = "BurstShotsRemaining must be between zero and one less than the authored burst count.";
                return false;
            }

            if (state.BurstShotsRemaining == 0
                && state.BurstIntervalRemainingSeconds != 0d)
            {
                diagnostic = "A state without pending burst shots must have zero burst interval remaining.";
                return false;
            }

            if (state.BurstShotsRemaining > 0
                && (state.BurstIntervalRemainingSeconds <= 0d
                    || state.BurstIntervalRemainingSeconds > profile.BurstShotIntervalSeconds))
            {
                diagnostic = "An active burst requires a positive interval no greater than the authored burst interval.";
                return false;
            }

            if (state.CadenceRemainingSeconds > profile.CadenceSeconds + BoundaryEpsilon)
            {
                diagnostic = "Cadence remaining exceeds the authored cadence.";
                return false;
            }

            if (state.RecoveryRemainingSeconds > profile.RecoverySeconds + BoundaryEpsilon)
            {
                diagnostic = "Recovery remaining exceeds the authored recovery duration.";
                return false;
            }

            if (profile.CycleMode == WeaponCycleMode.None)
            {
                if (state.HeatUnits != 0d
                    || state.HeatRecoveryLocked
                    || state.ChargeProgressSeconds != 0d)
                {
                    diagnostic = "A no-resource profile requires neutral heat and charge state.";
                    return false;
                }
            }
            else if (profile.CycleMode == WeaponCycleMode.Heat)
            {
                if (state.ChargeProgressSeconds != 0d)
                {
                    diagnostic = "A heat profile cannot carry charge progress.";
                    return false;
                }

                if (state.HeatUnits > profile.HeatCapacityUnits + BoundaryEpsilon)
                {
                    diagnostic = "Heat exceeds the authored capacity.";
                    return false;
                }

                if (state.HeatRecoveryLocked && state.HeatUnits <= 0d)
                {
                    diagnostic = "Heat recovery lock cannot remain set at zero heat.";
                    return false;
                }

                if (!state.HeatRecoveryLocked
                    && state.HeatUnits >= profile.HeatCapacityUnits - BoundaryEpsilon)
                {
                    diagnostic = "Maximum heat requires the deterministic recovery lock.";
                    return false;
                }
            }
            else if (profile.CycleMode == WeaponCycleMode.Charge)
            {
                if (state.HeatUnits != 0d || state.HeatRecoveryLocked)
                {
                    diagnostic = "A charge profile cannot carry heat state.";
                    return false;
                }

                if (state.ChargeProgressSeconds > profile.ChargeSeconds + BoundaryEpsilon)
                {
                    diagnostic = "Charge progress exceeds the authored completion duration.";
                    return false;
                }
            }
            else
            {
                diagnostic = "The profile contains an unknown cycle mode.";
                return false;
            }

            WeaponMountPhase expected = DerivePhase(
                profile,
                state.CadenceRemainingSeconds,
                state.BurstShotsRemaining,
                state.RecoveryRemainingSeconds,
                state.HeatRecoveryLocked,
                state.ChargeProgressSeconds);

            if (state.Phase != expected)
            {
                diagnostic = "Impossible phase transition: state declares "
                    + state.Phase
                    + " but its timers and resources require "
                    + expected
                    + ".";
                return false;
            }

            diagnostic = null;
            return true;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return IsFinite(value) && value >= 0d;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}

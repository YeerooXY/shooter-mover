using System;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Deterministic charge consumption and independent fractional regeneration for ThrusterBankState.
    /// </summary>
    public static class ThrusterBankStepper
    {
        /// <summary>
        /// Attempts to consume exactly one charge. An ineligible bank is returned unchanged.
        /// </summary>
        public static ThrusterBankState TryConsume(
            ThrusterBankState state,
            MovementThrusterTuningProfile tuning,
            out bool consumed)
        {
            ValidateStateAndTuning(state, tuning);

            if (!state.IsActivationEligible)
            {
                consumed = false;
                return state;
            }

            decimal[] nextRechargeElapsed = new decimal[state.RegeneratingChargeCount + 1];
            for (int index = 0; index < state.RegeneratingChargeCount; index++)
            {
                nextRechargeElapsed[index] = state.GetRechargeElapsedDecimal(index);
            }

            nextRechargeElapsed[nextRechargeElapsed.Length - 1] = 0m;
            consumed = true;
            return state.WithRechargeElapsed(nextRechargeElapsed);
        }

        /// <summary>
        /// Advances every incomplete charge by the same timestep. Completed charges become available
        /// at the exact recharge boundary; incomplete timers retain deterministic consumption order.
        /// </summary>
        public static ThrusterBankState Regenerate(
            ThrusterBankState state,
            MovementThrusterTuningProfile tuning,
            double elapsedSeconds)
        {
            ValidateStateAndTuning(state, tuning);
            ValidateElapsedSeconds(elapsedSeconds);

            if (elapsedSeconds == 0d || state.RegeneratingChargeCount == 0)
            {
                return state;
            }

            // Every active timer is shorter than one complete recharge interval. A timestep at least
            // that long therefore completes all of them and also avoids decimal conversion overflow.
            if (elapsedSeconds >= tuning.ThrusterRechargeSeconds)
            {
                return state.WithRechargeElapsed(Array.Empty<decimal>());
            }

            decimal elapsed = (decimal)elapsedSeconds;
            decimal rechargeSeconds = state.RechargeSecondsDecimal;
            decimal[] retained = new decimal[state.RegeneratingChargeCount];
            int retainedCount = 0;

            for (int index = 0; index < state.RegeneratingChargeCount; index++)
            {
                decimal nextProgress = state.GetRechargeElapsedDecimal(index) + elapsed;
                if (nextProgress < rechargeSeconds)
                {
                    retained[retainedCount] = nextProgress;
                    retainedCount++;
                }
            }

            if (retainedCount == 0)
            {
                return state.WithRechargeElapsed(Array.Empty<decimal>());
            }

            if (retainedCount == retained.Length)
            {
                return state.WithRechargeElapsed(retained);
            }

            decimal[] trimmed = new decimal[retainedCount];
            Array.Copy(retained, trimmed, retainedCount);
            return state.WithRechargeElapsed(trimmed);
        }

        /// <summary>
        /// Advances regeneration before evaluating an optional consume request. This ordering makes
        /// a request on the exact recharge boundary eligible and deterministic.
        /// </summary>
        public static ThrusterBankState Step(
            ThrusterBankState state,
            MovementThrusterTuningProfile tuning,
            double elapsedSeconds,
            bool consumeRequested,
            out bool consumed)
        {
            ThrusterBankState regenerated = Regenerate(state, tuning, elapsedSeconds);
            if (!consumeRequested)
            {
                consumed = false;
                return regenerated;
            }

            return TryConsume(regenerated, tuning, out consumed);
        }

        private static void ValidateStateAndTuning(
            ThrusterBankState state,
            MovementThrusterTuningProfile tuning)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            MovementThrusterTuningProfileValidator.Validate(tuning);

            if (!Equals(state.TuningIdentity, tuning.DeterministicIdentity))
            {
                throw new InvalidOperationException(
                    "Thruster bank state was created for a different movement-thruster tuning profile.");
            }
        }

        private static void ValidateElapsedSeconds(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds)
                || double.IsInfinity(elapsedSeconds)
                || elapsedSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedSeconds),
                    elapsedSeconds,
                    "Elapsed seconds must be finite and non-negative.");
            }
        }
    }
}

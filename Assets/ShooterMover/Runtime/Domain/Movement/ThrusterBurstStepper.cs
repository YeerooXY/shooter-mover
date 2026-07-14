using System;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Deterministic activation, steering, chaining, phase transition, and exit-momentum rules.
    /// Input axes are the normalized CS-003 move intent components; activation requests are press edges.
    /// </summary>
    public static class ThrusterBurstStepper
    {
        private const double NormalizedMagnitudeTolerance = 0.000001d;
        private const double ZeroToleranceSquared = 0.000000000001d;
        private const double SpeedTolerance = 0.000000001d;
        private const double DegreesToRadians = Math.PI / 180d;
        private const double TwoPi = Math.PI * 2d;

        /// <summary>
        /// Advances regeneration and the current movement phase for the elapsed interval, then evaluates
        /// an activation press at that exact boundary. Accepted activation consumes one charge and
        /// immediately replaces velocity with the authored burst velocity.
        /// </summary>
        public static ThrusterBurstState Step(
            ThrusterBurstState state,
            ThrusterBankState bank,
            double normalizedMoveX,
            double normalizedMoveY,
            double deltaTimeSeconds,
            bool activationRequested,
            MovementThrusterTuningProfile tuning,
            out ThrusterBankState nextBank,
            out bool activated)
        {
            ValidateStateBankAndTuning(state, bank, tuning);
            ValidateFinite(normalizedMoveX, nameof(normalizedMoveX));
            ValidateFinite(normalizedMoveY, nameof(normalizedMoveY));
            ValidateFinite(deltaTimeSeconds, nameof(deltaTimeSeconds));

            if (deltaTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTimeSeconds),
                    deltaTimeSeconds,
                    "Thruster burst timestep must be non-negative.");
            }

            double intentX;
            double intentY;
            bool hasDirectionalIntent = ResolveDirectionalIntent(
                normalizedMoveX,
                normalizedMoveY,
                tuning.ThrusterDirectionInputThreshold,
                out intentX,
                out intentY);

            nextBank = ThrusterBankStepper.Regenerate(bank, tuning, deltaTimeSeconds);
            ThrusterBurstState advanced = Advance(
                state,
                intentX,
                intentY,
                hasDirectionalIntent,
                deltaTimeSeconds,
                tuning);

            if (!activationRequested
                || advanced.ChainElapsedDecimal
                    < (decimal)tuning.ThrusterMinimumChainIntervalSeconds)
            {
                activated = false;
                return advanced;
            }

            double activationDirectionX;
            double activationDirectionY;
            if (hasDirectionalIntent)
            {
                activationDirectionX = intentX;
                activationDirectionY = intentY;
            }
            else if (!TryNormalize(
                advanced.VelocityX,
                advanced.VelocityY,
                out activationDirectionX,
                out activationDirectionY))
            {
                // Directionless activation from rest has no deterministic velocity-replacement direction.
                activated = false;
                return advanced;
            }

            bool consumed;
            ThrusterBankState consumedBank =
                ThrusterBankStepper.TryConsume(nextBank, tuning, out consumed);
            if (!consumed)
            {
                activated = false;
                return advanced;
            }

            nextBank = consumedBank;
            activated = true;
            return BeginBurst(
                advanced.TuningIdentity,
                activationDirectionX,
                activationDirectionY,
                tuning);
        }

        /// <summary>
        /// Convenience activation at the current boundary without advancing time or regeneration.
        /// </summary>
        public static ThrusterBurstState TryActivate(
            ThrusterBurstState state,
            ThrusterBankState bank,
            double normalizedMoveX,
            double normalizedMoveY,
            MovementThrusterTuningProfile tuning,
            out ThrusterBankState nextBank,
            out bool activated)
        {
            return Step(
                state,
                bank,
                normalizedMoveX,
                normalizedMoveY,
                0d,
                true,
                tuning,
                out nextBank,
                out activated);
        }

        private static ThrusterBurstState Advance(
            ThrusterBurstState state,
            double intentX,
            double intentY,
            bool hasDirectionalIntent,
            double deltaTimeSeconds,
            MovementThrusterTuningProfile tuning)
        {
            decimal chainLimit = (decimal)tuning.ThrusterMinimumChainIntervalSeconds;
            decimal nextChainElapsed = AdvanceCapped(
                state.ChainElapsedDecimal,
                deltaTimeSeconds,
                chainLimit);

            if (deltaTimeSeconds == 0d || state.Phase == ThrusterBurstPhase.Ready)
            {
                if (nextChainElapsed == state.ChainElapsedDecimal)
                {
                    return state;
                }

                return ThrusterBurstState.CreateInternal(
                    state.TuningIdentity,
                    state.Phase,
                    state.VelocityX,
                    state.VelocityY,
                    state.DirectionX,
                    state.DirectionY,
                    state.BurstElapsedDecimal,
                    state.ExitElapsedDecimal,
                    nextChainElapsed);
            }

            decimal burstDuration = (decimal)tuning.ThrusterBurstDurationSeconds;
            decimal exitDuration = (decimal)tuning.ThrusterExitMomentumSeconds;
            decimal maximumRelevantDuration = burstDuration + exitDuration;
            decimal remainingStep = ToBoundedDecimal(deltaTimeSeconds, maximumRelevantDuration);

            if (state.Phase == ThrusterBurstPhase.ExitMomentum)
            {
                return AdvanceExit(
                    state.TuningIdentity,
                    state.DirectionX,
                    state.DirectionY,
                    state.ExitElapsedDecimal,
                    remainingStep,
                    nextChainElapsed,
                    tuning);
            }

            decimal burstRemaining = burstDuration - state.BurstElapsedDecimal;
            decimal burstStep = remainingStep < burstRemaining
                ? remainingStep
                : burstRemaining;

            double directionX = state.DirectionX;
            double directionY = state.DirectionY;
            if (burstStep > 0m && hasDirectionalIntent)
            {
                decimal forgiveness = (decimal)tuning.ThrusterStartupForgivenessSeconds;
                if (state.BurstElapsedDecimal < forgiveness)
                {
                    // During the short startup window, a late direction sample corrects the initial
                    // direction immediately. Once the window closes, every turn is angularly bounded.
                    directionX = intentX;
                    directionY = intentY;
                }
                else
                {
                    RotateTowards(
                        directionX,
                        directionY,
                        intentX,
                        intentY,
                        tuning.ThrusterSteeringDegreesPerSecond * (double)burstStep,
                        out directionX,
                        out directionY);
                }
            }

            decimal nextBurstElapsed = state.BurstElapsedDecimal + burstStep;
            double burstSpeed = MaximumBurstSpeed(tuning);
            if (nextBurstElapsed < burstDuration)
            {
                return ThrusterBurstState.CreateInternal(
                    state.TuningIdentity,
                    ThrusterBurstPhase.Burst,
                    directionX * burstSpeed,
                    directionY * burstSpeed,
                    directionX,
                    directionY,
                    nextBurstElapsed,
                    0m,
                    nextChainElapsed);
            }

            decimal exitStep = remainingStep - burstStep;
            return AdvanceExit(
                state.TuningIdentity,
                directionX,
                directionY,
                0m,
                exitStep,
                nextChainElapsed,
                tuning);
        }

        private static ThrusterBurstState AdvanceExit(
            ShooterMover.Domain.Common.StableId tuningIdentity,
            double directionX,
            double directionY,
            decimal currentExitElapsed,
            decimal exitStep,
            decimal chainElapsed,
            MovementThrusterTuningProfile tuning)
        {
            decimal exitDuration = (decimal)tuning.ThrusterExitMomentumSeconds;
            double retainedSpeed = MaximumBurstSpeed(tuning) * tuning.ThrusterExitSpeedRetention;
            double handoffSpeed = Math.Min(retainedSpeed, tuning.BaseMaximumSpeed);

            if (exitDuration == 0m)
            {
                return CreateReadyInternal(
                    tuningIdentity,
                    directionX,
                    directionY,
                    handoffSpeed,
                    chainElapsed);
            }

            decimal nextExitElapsed = currentExitElapsed + exitStep;
            if (nextExitElapsed >= exitDuration)
            {
                return CreateReadyInternal(
                    tuningIdentity,
                    directionX,
                    directionY,
                    handoffSpeed,
                    chainElapsed);
            }

            double progress = (double)(nextExitElapsed / exitDuration);
            double remaining = 1d - progress;
            double shapedRemaining = Math.Pow(remaining, tuning.ThrusterExitDecayExponent);
            double speed = handoffSpeed + ((retainedSpeed - handoffSpeed) * shapedRemaining);

            return ThrusterBurstState.CreateInternal(
                tuningIdentity,
                ThrusterBurstPhase.ExitMomentum,
                directionX * speed,
                directionY * speed,
                directionX,
                directionY,
                0m,
                nextExitElapsed,
                chainElapsed);
        }

        private static ThrusterBurstState BeginBurst(
            ShooterMover.Domain.Common.StableId tuningIdentity,
            double directionX,
            double directionY,
            MovementThrusterTuningProfile tuning)
        {
            double speed = MaximumBurstSpeed(tuning);
            return ThrusterBurstState.CreateInternal(
                tuningIdentity,
                ThrusterBurstPhase.Burst,
                directionX * speed,
                directionY * speed,
                directionX,
                directionY,
                0m,
                0m,
                0m);
        }

        private static ThrusterBurstState CreateReadyInternal(
            ShooterMover.Domain.Common.StableId tuningIdentity,
            double directionX,
            double directionY,
            double speed,
            decimal chainElapsed)
        {
            return ThrusterBurstState.CreateInternal(
                tuningIdentity,
                ThrusterBurstPhase.Ready,
                directionX * speed,
                directionY * speed,
                directionX,
                directionY,
                0m,
                0m,
                chainElapsed);
        }

        private static bool ResolveDirectionalIntent(
            double normalizedMoveX,
            double normalizedMoveY,
            double directionThreshold,
            out double directionX,
            out double directionY)
        {
            double magnitudeSquared =
                (normalizedMoveX * normalizedMoveX)
                + (normalizedMoveY * normalizedMoveY);
            if (magnitudeSquared > 1d + NormalizedMagnitudeTolerance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedMoveX),
                    "Move intent must be normalized to the unit circle before thruster stepping.");
            }

            if (magnitudeSquared > 1d)
            {
                double inverseRoundedMagnitude = 1d / Math.Sqrt(magnitudeSquared);
                normalizedMoveX *= inverseRoundedMagnitude;
                normalizedMoveY *= inverseRoundedMagnitude;
                magnitudeSquared = 1d;
            }

            double thresholdSquared = directionThreshold * directionThreshold;
            if (magnitudeSquared <= ZeroToleranceSquared
                || magnitudeSquared < thresholdSquared)
            {
                directionX = 0d;
                directionY = 0d;
                return false;
            }

            double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
            directionX = normalizedMoveX * inverseMagnitude;
            directionY = normalizedMoveY * inverseMagnitude;
            return true;
        }

        private static void RotateTowards(
            double currentX,
            double currentY,
            double targetX,
            double targetY,
            double maximumDegrees,
            out double resultX,
            out double resultY)
        {
            if (maximumDegrees <= 0d)
            {
                resultX = currentX;
                resultY = currentY;
                return;
            }

            double currentAngle = Math.Atan2(currentY, currentX);
            double targetAngle = Math.Atan2(targetY, targetX);
            double delta = targetAngle - currentAngle;
            while (delta > Math.PI)
            {
                delta -= TwoPi;
            }

            while (delta < -Math.PI)
            {
                delta += TwoPi;
            }

            double maximumRadians = maximumDegrees * DegreesToRadians;
            if (Math.Abs(delta) <= maximumRadians)
            {
                resultX = targetX;
                resultY = targetY;
                return;
            }

            double resultAngle = currentAngle + (Math.Sign(delta) * maximumRadians);
            resultX = Math.Cos(resultAngle);
            resultY = Math.Sin(resultAngle);
        }

        private static decimal AdvanceCapped(
            decimal current,
            double elapsedSeconds,
            decimal maximum)
        {
            if (current >= maximum || elapsedSeconds == 0d)
            {
                return maximum == 0m ? 0m : current;
            }

            decimal remaining = maximum - current;
            return current + ToBoundedDecimal(elapsedSeconds, remaining);
        }

        private static decimal ToBoundedDecimal(double value, decimal maximum)
        {
            if (value <= 0d || maximum <= 0m)
            {
                return 0m;
            }

            if (value >= (double)maximum)
            {
                return maximum;
            }

            return (decimal)value;
        }

        private static bool TryNormalize(
            double x,
            double y,
            out double normalizedX,
            out double normalizedY)
        {
            double magnitudeSquared = (x * x) + (y * y);
            if (magnitudeSquared <= ZeroToleranceSquared)
            {
                normalizedX = 0d;
                normalizedY = 0d;
                return false;
            }

            double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
            normalizedX = x * inverseMagnitude;
            normalizedY = y * inverseMagnitude;
            return true;
        }

        private static double MaximumBurstSpeed(MovementThrusterTuningProfile tuning)
        {
            return tuning.BaseMaximumSpeed * tuning.ThrusterSpeedMultiplier;
        }

        private static void ValidateStateBankAndTuning(
            ThrusterBurstState state,
            ThrusterBankState bank,
            MovementThrusterTuningProfile tuning)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (bank == null)
            {
                throw new ArgumentNullException(nameof(bank));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            MovementThrusterTuningProfileValidator.Validate(tuning);

            if (!object.Equals(state.TuningIdentity, tuning.DeterministicIdentity))
            {
                throw new InvalidOperationException(
                    "Thruster burst state was created for a different movement-thruster tuning profile.");
            }

            if (!object.Equals(bank.TuningIdentity, tuning.DeterministicIdentity))
            {
                throw new InvalidOperationException(
                    "Thruster bank state was created for a different movement-thruster tuning profile.");
            }

            double maximumAllowedSpeed = state.Phase == ThrusterBurstPhase.Ready
                ? tuning.BaseMaximumSpeed
                : MaximumBurstSpeed(tuning);
            if (state.Speed > maximumAllowedSpeed + SpeedTolerance)
            {
                throw new InvalidOperationException(
                    "Thruster burst velocity exceeds the active tuning profile bound.");
            }

            if (state.Phase == ThrusterBurstPhase.Burst
                && state.BurstElapsedDecimal
                    >= (decimal)tuning.ThrusterBurstDurationSeconds)
            {
                throw new InvalidOperationException(
                    "Burst phase elapsed time must remain below the authored duration.");
            }

            if (state.Phase == ThrusterBurstPhase.ExitMomentum
                && ((decimal)tuning.ThrusterExitMomentumSeconds == 0m
                    || state.ExitElapsedDecimal
                        >= (decimal)tuning.ThrusterExitMomentumSeconds))
            {
                throw new InvalidOperationException(
                    "Exit phase elapsed time must remain inside the authored duration.");
            }
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Thruster burst inputs must be finite.");
            }
        }
    }
}

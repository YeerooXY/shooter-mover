using System;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Deterministic wall-contact reflection for MT-004 movement state.
    /// Contact normals point away from the contacted surface.
    /// </summary>
    public static class WallReflectionPolicy
    {
        private const double NormalizedMagnitudeTolerance = 0.000001d;
        private const double MinimumNormalMagnitudeSquared = 0.000000000001d;
        private const double ZeroToleranceSquared = 0.000000000001d;
        private const double IncomingImpactTolerance = 0.000000001d;
        private const double SpeedTolerance = 0.000000001d;

        public static WallReflectionResult Reflect(
            ThrusterBurstState state,
            double contactNormalX,
            double contactNormalY,
            double normalizedMoveX,
            double normalizedMoveY,
            int contactsAlreadyProcessed,
            MovementThrusterTuningProfile tuning)
        {
            ValidateStateAndTuning(state, tuning);
            ValidateFinite(contactNormalX, nameof(contactNormalX));
            ValidateFinite(contactNormalY, nameof(contactNormalY));
            ValidateFinite(normalizedMoveX, nameof(normalizedMoveX));
            ValidateFinite(normalizedMoveY, nameof(normalizedMoveY));

            if (contactsAlreadyProcessed < 0
                || contactsAlreadyProcessed > tuning.WallReflectionMaximumContacts)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contactsAlreadyProcessed),
                    contactsAlreadyProcessed,
                    "Processed contact count must remain inside the tuning contact bound.");
            }

            double normalX;
            double normalY;
            NormalizeContactNormal(
                contactNormalX,
                contactNormalY,
                out normalX,
                out normalY);

            double intentX;
            double intentY;
            bool hasDirectionalIntent = ResolveDirectionalIntent(
                normalizedMoveX,
                normalizedMoveY,
                tuning.ThrusterDirectionInputThreshold,
                out intentX,
                out intentY);

            double incomingVelocityX = state.VelocityX;
            double incomingVelocityY = state.VelocityY;

            if (contactsAlreadyProcessed == tuning.WallReflectionMaximumContacts)
            {
                return WallReflectionResult.Create(
                    state,
                    WallReflectionOutcome.ContactLimitReached,
                    contactsAlreadyProcessed,
                    incomingVelocityX,
                    incomingVelocityY,
                    normalX,
                    normalY);
            }

            int contactsProcessed = contactsAlreadyProcessed + 1;
            double incomingSpeedSquared =
                (incomingVelocityX * incomingVelocityX)
                + (incomingVelocityY * incomingVelocityY);
            double incomingNormalSpeed =
                (incomingVelocityX * normalX)
                + (incomingVelocityY * normalY);

            if (incomingSpeedSquared <= ZeroToleranceSquared
                || incomingNormalSpeed >= -IncomingImpactTolerance)
            {
                return WallReflectionResult.Create(
                    state,
                    WallReflectionOutcome.NoIncomingImpact,
                    contactsProcessed,
                    incomingVelocityX,
                    incomingVelocityY,
                    normalX,
                    normalY);
            }

            double reflectedVelocityX =
                incomingVelocityX - (2d * incomingNormalSpeed * normalX);
            double reflectedVelocityY =
                incomingVelocityY - (2d * incomingNormalSpeed * normalY);

            double reflectedDirectionX;
            double reflectedDirectionY;
            if (!TryNormalize(
                reflectedVelocityX,
                reflectedVelocityY,
                out reflectedDirectionX,
                out reflectedDirectionY))
            {
                throw new InvalidOperationException(
                    "A finite incoming wall impact must produce a finite reflected direction.");
            }

            double outgoingDirectionX = reflectedDirectionX;
            double outgoingDirectionY = reflectedDirectionY;
            if (hasDirectionalIntent && tuning.WallReflectionInputInfluence > 0d)
            {
                double influence = tuning.WallReflectionInputInfluence;
                double blendedX =
                    reflectedDirectionX + ((intentX - reflectedDirectionX) * influence);
                double blendedY =
                    reflectedDirectionY + ((intentY - reflectedDirectionY) * influence);

                // Player influence may steer tangentially or away from the wall, never back into it.
                double blendedNormal = (blendedX * normalX) + (blendedY * normalY);
                if (blendedNormal < 0d)
                {
                    blendedX -= blendedNormal * normalX;
                    blendedY -= blendedNormal * normalY;
                }

                double normalizedBlendedX;
                double normalizedBlendedY;
                if (TryNormalize(
                    blendedX,
                    blendedY,
                    out normalizedBlendedX,
                    out normalizedBlendedY))
                {
                    outgoingDirectionX = normalizedBlendedX;
                    outgoingDirectionY = normalizedBlendedY;
                }
            }

            double maximumSpeed = MaximumSpeedForPhase(state.Phase, tuning);
            double incomingSpeed = Math.Sqrt(incomingSpeedSquared);
            double minimumSpeed = Math.Min(tuning.WallReflectionMinimumSpeed, maximumSpeed);
            double outgoingSpeed = incomingSpeed * tuning.WallReflectionSpeedRetention;
            if (outgoingSpeed < minimumSpeed)
            {
                outgoingSpeed = minimumSpeed;
            }

            if (outgoingSpeed > maximumSpeed)
            {
                outgoingSpeed = maximumSpeed;
            }

            ThrusterBurstState reflectedState = ThrusterBurstState.CreateInternal(
                state.TuningIdentity,
                state.Phase,
                outgoingDirectionX * outgoingSpeed,
                outgoingDirectionY * outgoingSpeed,
                outgoingDirectionX,
                outgoingDirectionY,
                state.BurstElapsedDecimal,
                state.ExitElapsedDecimal,
                state.ChainElapsedDecimal);

            return WallReflectionResult.Create(
                reflectedState,
                WallReflectionOutcome.Reflected,
                contactsProcessed,
                incomingVelocityX,
                incomingVelocityY,
                normalX,
                normalY);
        }

        private static void ValidateStateAndTuning(
            ThrusterBurstState state,
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

            if (!object.Equals(state.TuningIdentity, tuning.DeterministicIdentity))
            {
                throw new InvalidOperationException(
                    "Thruster burst state was created for a different movement-thruster tuning profile.");
            }

            double speed = state.Speed;
            if (double.IsNaN(speed) || double.IsInfinity(speed))
            {
                throw new InvalidOperationException("Thruster burst state speed must be finite.");
            }

            double maximumSpeed = MaximumSpeedForPhase(state.Phase, tuning);
            if (speed > maximumSpeed + SpeedTolerance)
            {
                throw new InvalidOperationException(
                    "Thruster burst state exceeds the active wall-reflection speed bound.");
            }
        }

        private static void NormalizeContactNormal(
            double x,
            double y,
            out double normalizedX,
            out double normalizedY)
        {
            double magnitudeSquared = (x * x) + (y * y);
            if (double.IsInfinity(magnitudeSquared)
                || magnitudeSquared <= MinimumNormalMagnitudeSquared)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    "Wall-contact normal must have a finite, non-negligible magnitude.");
            }

            double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
            normalizedX = x * inverseMagnitude;
            normalizedY = y * inverseMagnitude;
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
            if (double.IsInfinity(magnitudeSquared)
                || magnitudeSquared > 1d + NormalizedMagnitudeTolerance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedMoveX),
                    "Move intent must be normalized to the unit circle before wall reflection.");
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

        private static bool TryNormalize(
            double x,
            double y,
            out double normalizedX,
            out double normalizedY)
        {
            double magnitudeSquared = (x * x) + (y * y);
            if (double.IsNaN(magnitudeSquared)
                || double.IsInfinity(magnitudeSquared)
                || magnitudeSquared <= ZeroToleranceSquared)
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

        private static double MaximumSpeedForPhase(
            ThrusterBurstPhase phase,
            MovementThrusterTuningProfile tuning)
        {
            return phase == ThrusterBurstPhase.Ready
                ? tuning.BaseMaximumSpeed
                : tuning.BaseMaximumSpeed * tuning.ThrusterSpeedMultiplier;
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Wall-reflection inputs must be finite.");
            }
        }
    }
}

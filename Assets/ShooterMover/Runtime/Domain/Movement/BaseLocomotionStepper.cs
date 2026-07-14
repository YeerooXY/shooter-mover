using System;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Deterministically advances bounded base locomotion from normalized move intent.
    /// Aim and every non-movement action are intentionally outside this domain API.
    /// </summary>
    public static class BaseLocomotionStepper
    {
        private const double NormalizedMagnitudeTolerance = 0.000001d;
        private const double ZeroToleranceSquared = 0.000000000001d;

        public static BaseLocomotionState Step(
            BaseLocomotionState current,
            double normalizedMoveX,
            double normalizedMoveY,
            double deltaTimeSeconds,
            MovementThrusterTuningProfile tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            ValidateFinite(normalizedMoveX, nameof(normalizedMoveX));
            ValidateFinite(normalizedMoveY, nameof(normalizedMoveY));
            ValidateFinite(deltaTimeSeconds, nameof(deltaTimeSeconds));

            if (deltaTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTimeSeconds),
                    "The locomotion timestep cannot be negative.");
            }

            double moveMagnitudeSquared =
                (normalizedMoveX * normalizedMoveX)
                + (normalizedMoveY * normalizedMoveY);

            if (moveMagnitudeSquared > 1d + NormalizedMagnitudeTolerance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedMoveX),
                    "Move intent must be normalized to the unit circle before locomotion stepping.");
            }

            if (deltaTimeSeconds == 0d)
            {
                return current;
            }

            double currentX = current.VelocityX;
            double currentY = current.VelocityY;
            ClampMagnitude(
                ref currentX,
                ref currentY,
                tuning.BaseMaximumSpeed);

            if (moveMagnitudeSquared <= ZeroToleranceSquared)
            {
                return MoveTowards(
                    currentX,
                    currentY,
                    0d,
                    0d,
                    tuning.BaseBraking * deltaTimeSeconds,
                    tuning.BaseMaximumSpeed);
            }

            if (moveMagnitudeSquared > 1d)
            {
                double inverseRoundedMagnitude = 1d / Math.Sqrt(moveMagnitudeSquared);
                normalizedMoveX *= inverseRoundedMagnitude;
                normalizedMoveY *= inverseRoundedMagnitude;
                moveMagnitudeSquared = 1d;
            }

            double moveMagnitude = Math.Sqrt(moveMagnitudeSquared);
            double responseMagnitude = Math.Pow(
                moveMagnitude,
                tuning.BaseVelocityResponseExponent);
            double targetSpeed = tuning.BaseMaximumSpeed * responseMagnitude;
            double inverseMoveMagnitude = 1d / moveMagnitude;
            double targetX = normalizedMoveX * inverseMoveMagnitude * targetSpeed;
            double targetY = normalizedMoveY * inverseMoveMagnitude * targetSpeed;

            double currentSpeedSquared = (currentX * currentX) + (currentY * currentY);
            double currentSpeed = Math.Sqrt(currentSpeedSquared);
            double alignment = (currentX * targetX) + (currentY * targetY);
            double responseRate;

            if (alignment < 0d)
            {
                responseRate = tuning.BaseCounterSteerBraking;
            }
            else if (currentSpeed > targetSpeed)
            {
                responseRate = tuning.BaseBraking;
            }
            else
            {
                responseRate = tuning.BaseAcceleration;
            }

            return MoveTowards(
                currentX,
                currentY,
                targetX,
                targetY,
                responseRate * deltaTimeSeconds,
                tuning.BaseMaximumSpeed);
        }

        private static BaseLocomotionState MoveTowards(
            double currentX,
            double currentY,
            double targetX,
            double targetY,
            double maximumDelta,
            double maximumSpeed)
        {
            double differenceX = targetX - currentX;
            double differenceY = targetY - currentY;
            double differenceSquared =
                (differenceX * differenceX)
                + (differenceY * differenceY);

            double resultX;
            double resultY;
            if (differenceSquared == 0d || maximumDelta == 0d)
            {
                resultX = currentX;
                resultY = currentY;
            }
            else
            {
                double differenceMagnitude = Math.Sqrt(differenceSquared);
                if (differenceMagnitude <= maximumDelta)
                {
                    resultX = targetX;
                    resultY = targetY;
                }
                else
                {
                    double scale = maximumDelta / differenceMagnitude;
                    resultX = currentX + (differenceX * scale);
                    resultY = currentY + (differenceY * scale);
                }
            }

            ClampMagnitude(ref resultX, ref resultY, maximumSpeed);
            return BaseLocomotionState.Create(resultX, resultY);
        }

        private static void ClampMagnitude(
            ref double x,
            ref double y,
            double maximumMagnitude)
        {
            double magnitudeSquared = (x * x) + (y * y);
            double maximumSquared = maximumMagnitude * maximumMagnitude;
            if (magnitudeSquared <= maximumSquared)
            {
                return;
            }

            double scale = maximumMagnitude / Math.Sqrt(magnitudeSquared);
            x *= scale;
            y *= scale;
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Locomotion inputs must be finite.");
            }
        }
    }
}

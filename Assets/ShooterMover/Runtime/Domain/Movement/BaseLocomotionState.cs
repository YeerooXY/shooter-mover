using System;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Immutable engine-independent base locomotion velocity.
    /// </summary>
    public readonly struct BaseLocomotionState : IEquatable<BaseLocomotionState>
    {
        private BaseLocomotionState(double velocityX, double velocityY)
        {
            VelocityX = CanonicalizeZero(velocityX);
            VelocityY = CanonicalizeZero(velocityY);
        }

        public double VelocityX { get; }

        public double VelocityY { get; }

        public double SpeedSquared
        {
            get { return (VelocityX * VelocityX) + (VelocityY * VelocityY); }
        }

        public double Speed
        {
            get { return Math.Sqrt(SpeedSquared); }
        }

        public static BaseLocomotionState Stationary
        {
            get { return new BaseLocomotionState(0d, 0d); }
        }

        public static BaseLocomotionState Create(double velocityX, double velocityY)
        {
            if (!IsFinite(velocityX))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(velocityX),
                    "Velocity components must be finite.");
            }

            if (!IsFinite(velocityY))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(velocityY),
                    "Velocity components must be finite.");
            }

            return new BaseLocomotionState(velocityX, velocityY);
        }

        public bool Equals(BaseLocomotionState other)
        {
            return VelocityX.Equals(other.VelocityX)
                && VelocityY.Equals(other.VelocityY);
        }

        public override bool Equals(object obj)
        {
            return obj is BaseLocomotionState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (VelocityX.GetHashCode() * 397) ^ VelocityY.GetHashCode();
            }
        }

        public static bool operator ==(
            BaseLocomotionState left,
            BaseLocomotionState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            BaseLocomotionState left,
            BaseLocomotionState right)
        {
            return !left.Equals(right);
        }

        private static double CanonicalizeZero(double value)
        {
            return value == 0d ? 0d : value;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

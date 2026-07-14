using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Authoritative phase of deterministic thruster movement.
    /// </summary>
    public enum ThrusterBurstPhase
    {
        Ready = 0,
        Burst = 1,
        ExitMomentum = 2,
    }

    /// <summary>
    /// Immutable, engine-independent velocity and phase state for one thruster burst sequence.
    /// </summary>
    public sealed class ThrusterBurstState : IEquatable<ThrusterBurstState>
    {
        private const double DirectionMagnitudeTolerance = 0.000001d;

        private readonly decimal burstElapsedSeconds;
        private readonly decimal exitElapsedSeconds;
        private readonly decimal chainElapsedSeconds;

        private ThrusterBurstState(
            StableId tuningIdentity,
            ThrusterBurstPhase phase,
            double velocityX,
            double velocityY,
            double directionX,
            double directionY,
            decimal burstElapsedSeconds,
            decimal exitElapsedSeconds,
            decimal chainElapsedSeconds)
        {
            if (tuningIdentity == null)
            {
                throw new ArgumentNullException(nameof(tuningIdentity));
            }

            if (!Enum.IsDefined(typeof(ThrusterBurstPhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown thruster burst phase.");
            }

            ValidateFinite(velocityX, nameof(velocityX));
            ValidateFinite(velocityY, nameof(velocityY));
            ValidateFinite(directionX, nameof(directionX));
            ValidateFinite(directionY, nameof(directionY));

            if (burstElapsedSeconds < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(burstElapsedSeconds),
                    burstElapsedSeconds,
                    "Burst elapsed time cannot be negative.");
            }

            if (exitElapsedSeconds < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exitElapsedSeconds),
                    exitElapsedSeconds,
                    "Exit elapsed time cannot be negative.");
            }

            if (chainElapsedSeconds < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chainElapsedSeconds),
                    chainElapsedSeconds,
                    "Chain elapsed time cannot be negative.");
            }

            double directionMagnitudeSquared =
                (directionX * directionX)
                + (directionY * directionY);
            bool hasDirection = directionMagnitudeSquared > 0d;
            if (hasDirection
                && Math.Abs(directionMagnitudeSquared - 1d) > DirectionMagnitudeTolerance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(directionX),
                    "Stored thruster direction must be normalized.");
            }

            if (phase != ThrusterBurstPhase.Ready && !hasDirection)
            {
                throw new ArgumentException(
                    "Active burst and exit phases require a normalized direction.",
                    nameof(directionX));
            }

            if (phase == ThrusterBurstPhase.Ready
                && (burstElapsedSeconds != 0m || exitElapsedSeconds != 0m))
            {
                throw new ArgumentException("Ready state cannot retain active phase elapsed time.");
            }

            if (phase == ThrusterBurstPhase.Burst && exitElapsedSeconds != 0m)
            {
                throw new ArgumentException("Burst state cannot retain exit elapsed time.");
            }

            if (phase == ThrusterBurstPhase.ExitMomentum && burstElapsedSeconds != 0m)
            {
                throw new ArgumentException("Exit state cannot retain burst elapsed time.");
            }

            TuningIdentity = tuningIdentity;
            Phase = phase;
            VelocityX = CanonicalizeZero(velocityX);
            VelocityY = CanonicalizeZero(velocityY);
            DirectionX = CanonicalizeZero(directionX);
            DirectionY = CanonicalizeZero(directionY);
            this.burstElapsedSeconds = burstElapsedSeconds;
            this.exitElapsedSeconds = exitElapsedSeconds;
            this.chainElapsedSeconds = chainElapsedSeconds;
        }

        public StableId TuningIdentity { get; }

        public ThrusterBurstPhase Phase { get; }

        public double VelocityX { get; }

        public double VelocityY { get; }

        public double DirectionX { get; }

        public double DirectionY { get; }

        public double SpeedSquared
        {
            get { return (VelocityX * VelocityX) + (VelocityY * VelocityY); }
        }

        public double Speed
        {
            get { return Math.Sqrt(SpeedSquared); }
        }

        public double BurstElapsedSeconds
        {
            get { return (double)burstElapsedSeconds; }
        }

        public double ExitElapsedSeconds
        {
            get { return (double)exitElapsedSeconds; }
        }

        public double ChainElapsedSeconds
        {
            get { return (double)chainElapsedSeconds; }
        }

        public bool IsBursting
        {
            get { return Phase == ThrusterBurstPhase.Burst; }
        }

        public bool IsInExitMomentum
        {
            get { return Phase == ThrusterBurstPhase.ExitMomentum; }
        }

        /// <summary>
        /// Creates a chain-ready state from bounded base locomotion velocity.
        /// Oversized source velocity is clamped to the profile's base maximum.
        /// </summary>
        public static ThrusterBurstState CreateReady(
            BaseLocomotionState locomotion,
            MovementThrusterTuningProfile tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            MovementThrusterTuningProfileValidator.Validate(tuning);

            double velocityX = locomotion.VelocityX;
            double velocityY = locomotion.VelocityY;
            ClampMagnitude(ref velocityX, ref velocityY, tuning.BaseMaximumSpeed);

            double directionX;
            double directionY;
            NormalizeOrZero(velocityX, velocityY, out directionX, out directionY);

            return new ThrusterBurstState(
                tuning.DeterministicIdentity,
                ThrusterBurstPhase.Ready,
                velocityX,
                velocityY,
                directionX,
                directionY,
                0m,
                0m,
                (decimal)tuning.ThrusterMinimumChainIntervalSeconds);
        }

        public bool Equals(ThrusterBurstState other)
        {
            return other != null
                && object.Equals(TuningIdentity, other.TuningIdentity)
                && Phase == other.Phase
                && VelocityX.Equals(other.VelocityX)
                && VelocityY.Equals(other.VelocityY)
                && DirectionX.Equals(other.DirectionX)
                && DirectionY.Equals(other.DirectionY)
                && burstElapsedSeconds == other.burstElapsedSeconds
                && exitElapsedSeconds == other.exitElapsedSeconds
                && chainElapsedSeconds == other.chainElapsedSeconds;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ThrusterBurstState);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + TuningIdentity.GetHashCode();
                hash = (hash * 31) + Phase.GetHashCode();
                hash = (hash * 31) + VelocityX.GetHashCode();
                hash = (hash * 31) + VelocityY.GetHashCode();
                hash = (hash * 31) + DirectionX.GetHashCode();
                hash = (hash * 31) + DirectionY.GetHashCode();
                hash = (hash * 31) + burstElapsedSeconds.GetHashCode();
                hash = (hash * 31) + exitElapsedSeconds.GetHashCode();
                hash = (hash * 31) + chainElapsedSeconds.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(ThrusterBurstState left, ThrusterBurstState right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(ThrusterBurstState left, ThrusterBurstState right)
        {
            return !(left == right);
        }

        internal decimal BurstElapsedDecimal
        {
            get { return burstElapsedSeconds; }
        }

        internal decimal ExitElapsedDecimal
        {
            get { return exitElapsedSeconds; }
        }

        internal decimal ChainElapsedDecimal
        {
            get { return chainElapsedSeconds; }
        }

        internal static ThrusterBurstState CreateInternal(
            StableId tuningIdentity,
            ThrusterBurstPhase phase,
            double velocityX,
            double velocityY,
            double directionX,
            double directionY,
            decimal burstElapsedSeconds,
            decimal exitElapsedSeconds,
            decimal chainElapsedSeconds)
        {
            return new ThrusterBurstState(
                tuningIdentity,
                phase,
                velocityX,
                velocityY,
                directionX,
                directionY,
                burstElapsedSeconds,
                exitElapsedSeconds,
                chainElapsedSeconds);
        }

        private static void ClampMagnitude(ref double x, ref double y, double maximumMagnitude)
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

        private static void NormalizeOrZero(
            double x,
            double y,
            out double normalizedX,
            out double normalizedY)
        {
            double magnitudeSquared = (x * x) + (y * y);
            if (magnitudeSquared == 0d)
            {
                normalizedX = 0d;
                normalizedY = 0d;
                return;
            }

            double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
            normalizedX = x * inverseMagnitude;
            normalizedY = y * inverseMagnitude;
        }

        private static double CanonicalizeZero(double value)
        {
            return value == 0d ? 0d : value;
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Thruster burst values must be finite.");
            }
        }
    }
}

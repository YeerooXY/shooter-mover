using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Coarse presentation-facing state for one read-only thruster projection.
    /// Detailed burst phase and recharge fields remain available on the snapshot.
    /// </summary>
    public enum ThrusterStatusState
    {
        Unavailable = 1,
        Ready = 2,
        Empty = 3,
        Regenerating = 4,
        Burst = 5,
        Disposed = 6,
    }

    /// <summary>
    /// Immutable, engine-independent read model for thruster status.
    /// It owns no runtime authority and exposes no mutation path back to movement state.
    /// </summary>
    public sealed class ThrusterStatusSnapshot : IEquatable<ThrusterStatusSnapshot>
    {
        private const double UnitVectorTolerance = 0.000001d;

        public ThrusterStatusSnapshot(
            ThrusterStatusState state,
            StableId tuningIdentity,
            long runtimeGeneration,
            int availableCharges,
            int maximumCharges,
            int regeneratingCharges,
            double rechargeSeconds,
            ThrusterBurstPhase burstPhase,
            double velocityX,
            double velocityY,
            double burstDirectionX,
            double burstDirectionY,
            double steeringIntentX,
            double steeringIntentY,
            double burstElapsedSeconds,
            double exitElapsedSeconds,
            double chainElapsedSeconds,
            double minimumChainIntervalSeconds)
        {
            if (!Enum.IsDefined(typeof(ThrusterStatusState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown thruster status state.");
            }

            if (tuningIdentity == null)
            {
                throw new ArgumentNullException(nameof(tuningIdentity));
            }

            if (runtimeGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runtimeGeneration),
                    runtimeGeneration,
                    "Runtime generation cannot be negative.");
            }

            if (maximumCharges < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumCharges),
                    maximumCharges,
                    "Maximum charge count cannot be negative.");
            }

            if (availableCharges < 0 || availableCharges > maximumCharges)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(availableCharges),
                    availableCharges,
                    "Available charge count must be within capacity.");
            }

            if (regeneratingCharges < 0 || regeneratingCharges > maximumCharges)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(regeneratingCharges),
                    regeneratingCharges,
                    "Regenerating charge count must be within capacity.");
            }

            ValidateFinitePositive(rechargeSeconds, nameof(rechargeSeconds));
            ValidateFinite(velocityX, nameof(velocityX));
            ValidateFinite(velocityY, nameof(velocityY));
            ValidateFinite(burstDirectionX, nameof(burstDirectionX));
            ValidateFinite(burstDirectionY, nameof(burstDirectionY));
            ValidateFinite(steeringIntentX, nameof(steeringIntentX));
            ValidateFinite(steeringIntentY, nameof(steeringIntentY));
            ValidateFiniteNonNegative(burstElapsedSeconds, nameof(burstElapsedSeconds));
            ValidateFiniteNonNegative(exitElapsedSeconds, nameof(exitElapsedSeconds));
            ValidateFiniteNonNegative(chainElapsedSeconds, nameof(chainElapsedSeconds));
            ValidateFiniteNonNegative(minimumChainIntervalSeconds, nameof(minimumChainIntervalSeconds));

            if (!Enum.IsDefined(typeof(ThrusterBurstPhase), burstPhase))
            {
                throw new ArgumentOutOfRangeException(nameof(burstPhase), burstPhase, "Unknown burst phase.");
            }

            ValidateUnitOrZero(burstDirectionX, burstDirectionY, nameof(burstDirectionX));
            ValidateUnitOrZero(steeringIntentX, steeringIntentY, nameof(steeringIntentX));

            bool isRuntimeAvailable =
                state != ThrusterStatusState.Unavailable
                && state != ThrusterStatusState.Disposed;
            if (!isRuntimeAvailable)
            {
                if (availableCharges != 0 || regeneratingCharges != 0)
                {
                    throw new ArgumentException(
                        "Unavailable or disposed snapshots cannot expose live charge state.");
                }

                if (velocityX != 0d
                    || velocityY != 0d
                    || burstDirectionX != 0d
                    || burstDirectionY != 0d
                    || steeringIntentX != 0d
                    || steeringIntentY != 0d
                    || burstElapsedSeconds != 0d
                    || exitElapsedSeconds != 0d
                    || chainElapsedSeconds != 0d)
                {
                    throw new ArgumentException(
                        "Unavailable or disposed snapshots cannot expose live movement state.");
                }
            }
            else
            {
                if (maximumCharges < 1)
                {
                    throw new ArgumentException(
                        "Available thruster runtime state requires at least one configured charge.");
                }

                if (availableCharges + regeneratingCharges != maximumCharges)
                {
                    throw new ArgumentException(
                        "Available and regenerating charge counts must exactly cover capacity.");
                }

                ValidateStatusConsistency(
                    state,
                    availableCharges,
                    regeneratingCharges,
                    burstPhase);
            }

            State = state;
            TuningIdentity = tuningIdentity;
            RuntimeGeneration = runtimeGeneration;
            AvailableCharges = availableCharges;
            MaximumCharges = maximumCharges;
            RegeneratingCharges = regeneratingCharges;
            RechargeSeconds = rechargeSeconds;
            BurstPhase = burstPhase;
            VelocityX = CanonicalizeZero(velocityX);
            VelocityY = CanonicalizeZero(velocityY);
            BurstDirectionX = CanonicalizeZero(burstDirectionX);
            BurstDirectionY = CanonicalizeZero(burstDirectionY);
            SteeringIntentX = CanonicalizeZero(steeringIntentX);
            SteeringIntentY = CanonicalizeZero(steeringIntentY);
            BurstElapsedSeconds = burstElapsedSeconds;
            ExitElapsedSeconds = exitElapsedSeconds;
            ChainElapsedSeconds = chainElapsedSeconds;
            MinimumChainIntervalSeconds = minimumChainIntervalSeconds;
        }

        public ThrusterStatusState State { get; }

        public StableId TuningIdentity { get; }

        public long RuntimeGeneration { get; }

        public int AvailableCharges { get; }

        public int MaximumCharges { get; }

        public int RegeneratingCharges { get; }

        public double RechargeSeconds { get; }

        public ThrusterBurstPhase BurstPhase { get; }

        public double VelocityX { get; }

        public double VelocityY { get; }

        public double BurstDirectionX { get; }

        public double BurstDirectionY { get; }

        public double SteeringIntentX { get; }

        public double SteeringIntentY { get; }

        public double BurstElapsedSeconds { get; }

        public double ExitElapsedSeconds { get; }

        public double ChainElapsedSeconds { get; }

        public double MinimumChainIntervalSeconds { get; }

        public bool IsRuntimeAvailable
        {
            get
            {
                return State != ThrusterStatusState.Unavailable
                    && State != ThrusterStatusState.Disposed;
            }
        }

        public bool IsDisposed
        {
            get { return State == ThrusterStatusState.Disposed; }
        }

        public bool IsReady
        {
            get { return State == ThrusterStatusState.Ready; }
        }

        public bool HasReadyCharge
        {
            get { return IsRuntimeAvailable && AvailableCharges > 0; }
        }

        public bool IsEmpty
        {
            get { return IsRuntimeAvailable && AvailableCharges == 0; }
        }

        public bool IsRegenerating
        {
            get { return IsRuntimeAvailable && RegeneratingCharges > 0; }
        }

        public bool IsBursting
        {
            get { return IsRuntimeAvailable && BurstPhase == ThrusterBurstPhase.Burst; }
        }

        public bool HasSteeringIntent
        {
            get
            {
                return (SteeringIntentX * SteeringIntentX)
                    + (SteeringIntentY * SteeringIntentY) > 0d;
            }
        }

        public bool IsChainWindowReady
        {
            get
            {
                return IsRuntimeAvailable
                    && ChainElapsedSeconds >= MinimumChainIntervalSeconds;
            }
        }

        public bool Equals(ThrusterStatusSnapshot other)
        {
            return other != null
                && State == other.State
                && object.Equals(TuningIdentity, other.TuningIdentity)
                && RuntimeGeneration == other.RuntimeGeneration
                && AvailableCharges == other.AvailableCharges
                && MaximumCharges == other.MaximumCharges
                && RegeneratingCharges == other.RegeneratingCharges
                && RechargeSeconds.Equals(other.RechargeSeconds)
                && BurstPhase == other.BurstPhase
                && VelocityX.Equals(other.VelocityX)
                && VelocityY.Equals(other.VelocityY)
                && BurstDirectionX.Equals(other.BurstDirectionX)
                && BurstDirectionY.Equals(other.BurstDirectionY)
                && SteeringIntentX.Equals(other.SteeringIntentX)
                && SteeringIntentY.Equals(other.SteeringIntentY)
                && BurstElapsedSeconds.Equals(other.BurstElapsedSeconds)
                && ExitElapsedSeconds.Equals(other.ExitElapsedSeconds)
                && ChainElapsedSeconds.Equals(other.ChainElapsedSeconds)
                && MinimumChainIntervalSeconds.Equals(other.MinimumChainIntervalSeconds);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ThrusterStatusSnapshot);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + State.GetHashCode();
                hash = (hash * 31) + TuningIdentity.GetHashCode();
                hash = (hash * 31) + RuntimeGeneration.GetHashCode();
                hash = (hash * 31) + AvailableCharges;
                hash = (hash * 31) + MaximumCharges;
                hash = (hash * 31) + RegeneratingCharges;
                hash = (hash * 31) + RechargeSeconds.GetHashCode();
                hash = (hash * 31) + BurstPhase.GetHashCode();
                hash = (hash * 31) + VelocityX.GetHashCode();
                hash = (hash * 31) + VelocityY.GetHashCode();
                hash = (hash * 31) + BurstDirectionX.GetHashCode();
                hash = (hash * 31) + BurstDirectionY.GetHashCode();
                hash = (hash * 31) + SteeringIntentX.GetHashCode();
                hash = (hash * 31) + SteeringIntentY.GetHashCode();
                hash = (hash * 31) + BurstElapsedSeconds.GetHashCode();
                hash = (hash * 31) + ExitElapsedSeconds.GetHashCode();
                hash = (hash * 31) + ChainElapsedSeconds.GetHashCode();
                hash = (hash * 31) + MinimumChainIntervalSeconds.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(ThrusterStatusSnapshot left, ThrusterStatusSnapshot right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(ThrusterStatusSnapshot left, ThrusterStatusSnapshot right)
        {
            return !(left == right);
        }

        private static void ValidateStatusConsistency(
            ThrusterStatusState state,
            int availableCharges,
            int regeneratingCharges,
            ThrusterBurstPhase burstPhase)
        {
            switch (state)
            {
                case ThrusterStatusState.Ready:
                    if (availableCharges < 1
                        || regeneratingCharges != 0
                        || burstPhase == ThrusterBurstPhase.Burst)
                    {
                        throw new ArgumentException("Ready status does not match the supplied runtime state.");
                    }

                    break;

                case ThrusterStatusState.Empty:
                    if (availableCharges != 0 || burstPhase == ThrusterBurstPhase.Burst)
                    {
                        throw new ArgumentException("Empty status does not match the supplied runtime state.");
                    }

                    break;

                case ThrusterStatusState.Regenerating:
                    if (availableCharges < 1
                        || regeneratingCharges < 1
                        || burstPhase == ThrusterBurstPhase.Burst)
                    {
                        throw new ArgumentException(
                            "Regenerating status does not match the supplied runtime state.");
                    }

                    break;

                case ThrusterStatusState.Burst:
                    if (burstPhase != ThrusterBurstPhase.Burst)
                    {
                        throw new ArgumentException("Burst status requires the burst phase.");
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported available status.");
            }
        }

        private static void ValidateUnitOrZero(double x, double y, string parameterName)
        {
            double magnitudeSquared = (x * x) + (y * y);
            if (magnitudeSquared == 0d)
            {
                return;
            }

            if (Math.Abs(magnitudeSquared - 1d) > UnitVectorTolerance)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Direction values must be normalized or zero.");
            }
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Thruster status values must be finite.");
            }
        }

        private static void ValidateFinitePositive(double value, string parameterName)
        {
            ValidateFinite(value, parameterName);
            if (value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Thruster status duration must be positive.");
            }
        }

        private static void ValidateFiniteNonNegative(double value, string parameterName)
        {
            ValidateFinite(value, parameterName);
            if (value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Thruster status duration cannot be negative.");
            }
        }

        private static double CanonicalizeZero(double value)
        {
            return value == 0d ? 0d : value;
        }
    }
}

using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Deterministic outcome of processing one wall-contact normal.
    /// </summary>
    public enum WallReflectionOutcome
    {
        Reflected = 0,
        NoIncomingImpact = 1,
        ContactLimitReached = 2,
    }

    /// <summary>
    /// Immutable, engine-independent wall-reflection result.
    /// </summary>
    public sealed class WallReflectionResult : IEquatable<WallReflectionResult>
    {
        private const double NormalMagnitudeTolerance = 0.000001d;

        private WallReflectionResult(
            ThrusterBurstState state,
            WallReflectionOutcome outcome,
            int contactsProcessed,
            double incomingVelocityX,
            double incomingVelocityY,
            double contactNormalX,
            double contactNormalY)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!Enum.IsDefined(typeof(WallReflectionOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome,
                    "Unknown wall-reflection outcome.");
            }

            if (contactsProcessed < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contactsProcessed),
                    contactsProcessed,
                    "Processed contact count cannot be negative.");
            }

            ValidateFinite(incomingVelocityX, nameof(incomingVelocityX));
            ValidateFinite(incomingVelocityY, nameof(incomingVelocityY));
            ValidateFinite(contactNormalX, nameof(contactNormalX));
            ValidateFinite(contactNormalY, nameof(contactNormalY));

            double normalMagnitudeSquared =
                (contactNormalX * contactNormalX)
                + (contactNormalY * contactNormalY);
            if (double.IsInfinity(normalMagnitudeSquared)
                || Math.Abs(normalMagnitudeSquared - 1d) > NormalMagnitudeTolerance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contactNormalX),
                    "Stored wall-contact normal must be normalized.");
            }

            State = state;
            Outcome = outcome;
            ContactsProcessed = contactsProcessed;
            IncomingVelocityX = CanonicalizeZero(incomingVelocityX);
            IncomingVelocityY = CanonicalizeZero(incomingVelocityY);
            ContactNormalX = CanonicalizeZero(contactNormalX);
            ContactNormalY = CanonicalizeZero(contactNormalY);
        }

        public ThrusterBurstState State { get; }

        public StableId TuningIdentity
        {
            get { return State.TuningIdentity; }
        }

        public WallReflectionOutcome Outcome { get; }

        public int ContactsProcessed { get; }

        public double IncomingVelocityX { get; }

        public double IncomingVelocityY { get; }

        public double ContactNormalX { get; }

        public double ContactNormalY { get; }

        public double OutgoingVelocityX
        {
            get { return State.VelocityX; }
        }

        public double OutgoingVelocityY
        {
            get { return State.VelocityY; }
        }

        public double OutgoingSpeed
        {
            get { return State.Speed; }
        }

        public bool WasReflected
        {
            get { return Outcome == WallReflectionOutcome.Reflected; }
        }

        public bool ReachedContactLimit
        {
            get { return Outcome == WallReflectionOutcome.ContactLimitReached; }
        }

        public bool Equals(WallReflectionResult other)
        {
            return other != null
                && object.Equals(State, other.State)
                && Outcome == other.Outcome
                && ContactsProcessed == other.ContactsProcessed
                && IncomingVelocityX.Equals(other.IncomingVelocityX)
                && IncomingVelocityY.Equals(other.IncomingVelocityY)
                && ContactNormalX.Equals(other.ContactNormalX)
                && ContactNormalY.Equals(other.ContactNormalY);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WallReflectionResult);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + State.GetHashCode();
                hash = (hash * 31) + Outcome.GetHashCode();
                hash = (hash * 31) + ContactsProcessed.GetHashCode();
                hash = (hash * 31) + IncomingVelocityX.GetHashCode();
                hash = (hash * 31) + IncomingVelocityY.GetHashCode();
                hash = (hash * 31) + ContactNormalX.GetHashCode();
                hash = (hash * 31) + ContactNormalY.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(WallReflectionResult left, WallReflectionResult right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(WallReflectionResult left, WallReflectionResult right)
        {
            return !(left == right);
        }

        internal static WallReflectionResult Create(
            ThrusterBurstState state,
            WallReflectionOutcome outcome,
            int contactsProcessed,
            double incomingVelocityX,
            double incomingVelocityY,
            double contactNormalX,
            double contactNormalY)
        {
            return new WallReflectionResult(
                state,
                outcome,
                contactsProcessed,
                incomingVelocityX,
                incomingVelocityY,
                contactNormalX,
                contactNormalY);
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
                    "Wall-reflection result values must be finite.");
            }
        }
    }
}

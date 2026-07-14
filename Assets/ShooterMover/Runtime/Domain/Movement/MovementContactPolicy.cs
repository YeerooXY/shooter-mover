using System;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Domain-owned movement outcome produced from the CS-004 WeightResult v1 value.
    /// </summary>
    public enum MovementContactOutcome
    {
        ShoveThrough = 1,
        BlockedByWeight = 2,
        UnknownWeightBlocked = 3,
    }

    /// <summary>
    /// Immutable velocity decision for one enemy-body contact.
    /// </summary>
    public sealed class MovementContactResolution : IEquatable<MovementContactResolution>
    {
        internal MovementContactResolution(
            MovementContactOutcome outcome,
            double velocityX,
            double velocityY)
        {
            if (!Enum.IsDefined(typeof(MovementContactOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown movement-contact outcome.");
            }

            ValidateFinite(velocityX, nameof(velocityX));
            ValidateFinite(velocityY, nameof(velocityY));

            Outcome = outcome;
            VelocityX = CanonicalizeZero(velocityX);
            VelocityY = CanonicalizeZero(velocityY);
        }

        public MovementContactOutcome Outcome { get; }

        public double VelocityX { get; }

        public double VelocityY { get; }

        public double SpeedSquared
        {
            get { return (VelocityX * VelocityX) + (VelocityY * VelocityY); }
        }

        public bool AllowsShoveThrough
        {
            get { return Outcome == MovementContactOutcome.ShoveThrough; }
        }

        public bool BlocksApproach
        {
            get { return Outcome != MovementContactOutcome.ShoveThrough; }
        }

        public bool Equals(MovementContactResolution other)
        {
            return other != null
                && Outcome == other.Outcome
                && VelocityX.Equals(other.VelocityX)
                && VelocityY.Equals(other.VelocityY);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MovementContactResolution);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Outcome.GetHashCode();
                hash = (hash * 31) + VelocityX.GetHashCode();
                hash = (hash * 31) + VelocityY.GetHashCode();
                return hash;
            }
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
                    "Movement-contact velocity must be finite.");
            }
        }
    }

    /// <summary>
    /// Deterministic, engine-independent enemy-weight response for MT-004 movement velocity.
    ///
    /// The Domain assembly cannot reference the outward Contracts assembly. Callers therefore pass the
    /// numeric value of CS-004 WeightResult v1. The constants below deliberately mirror that frozen
    /// contract and provide one explicit adaptation boundary instead of a duplicate combat DTO.
    /// </summary>
    public static class MovementContactPolicy
    {
        public const int SourceLighterWeightResultValue = 1;
        public const int EqualWeightResultValue = 2;
        public const int SourceHeavierWeightResultValue = 3;
        public const int TargetImmovableWeightResultValue = 4;

        private const double NormalMagnitudeTolerance = 0.000001d;
        private const double ZeroToleranceSquared = 0.000000000001d;

        /// <summary>
        /// Resolves one body contact. The normal points from the contacted enemy toward the mover.
        /// A source-heavier result shoves through with authored normal/tangential retention. Equal,
        /// source-lighter, immovable, and unknown results remove inward normal velocity and retain only
        /// the authored bounded tangential momentum.
        /// </summary>
        public static MovementContactResolution Resolve(
            ThrusterBurstState movement,
            int combatWeightResultValue,
            double contactNormalX,
            double contactNormalY,
            MovementThrusterTuningProfile tuning)
        {
            if (movement == null)
            {
                throw new ArgumentNullException(nameof(movement));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            MovementThrusterTuningProfileValidator.Validate(tuning);
            if (movement.TuningIdentity != tuning.DeterministicIdentity)
            {
                throw new ArgumentException(
                    "Movement state and contact tuning must use the same deterministic tuning identity.",
                    nameof(tuning));
            }

            ValidateFinite(contactNormalX, nameof(contactNormalX));
            ValidateFinite(contactNormalY, nameof(contactNormalY));

            double normalMagnitudeSquared =
                (contactNormalX * contactNormalX)
                + (contactNormalY * contactNormalY);
            if (normalMagnitudeSquared <= ZeroToleranceSquared)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contactNormalX),
                    "Enemy-contact normal must be non-zero.");
            }

            if (Math.Abs(normalMagnitudeSquared - 1d) > NormalMagnitudeTolerance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contactNormalX),
                    "Enemy-contact normal must be normalized.");
            }

            double inverseMagnitude = 1d / Math.Sqrt(normalMagnitudeSquared);
            double normalX = contactNormalX * inverseMagnitude;
            double normalY = contactNormalY * inverseMagnitude;

            if (combatWeightResultValue == SourceHeavierWeightResultValue)
            {
                return ResolveShoveThrough(movement, normalX, normalY, tuning);
            }

            MovementContactOutcome outcome =
                combatWeightResultValue == SourceLighterWeightResultValue
                || combatWeightResultValue == EqualWeightResultValue
                || combatWeightResultValue == TargetImmovableWeightResultValue
                    ? MovementContactOutcome.BlockedByWeight
                    : MovementContactOutcome.UnknownWeightBlocked;

            return ResolveBlocked(movement, normalX, normalY, tuning, outcome);
        }

        private static MovementContactResolution ResolveShoveThrough(
            ThrusterBurstState movement,
            double normalX,
            double normalY,
            MovementThrusterTuningProfile tuning)
        {
            double inwardDot =
                (movement.VelocityX * normalX)
                + (movement.VelocityY * normalY);
            if (inwardDot >= 0d)
            {
                return new MovementContactResolution(
                    MovementContactOutcome.ShoveThrough,
                    movement.VelocityX,
                    movement.VelocityY);
            }

            double normalVelocityX = normalX * inwardDot;
            double normalVelocityY = normalY * inwardDot;
            double tangentVelocityX = movement.VelocityX - normalVelocityX;
            double tangentVelocityY = movement.VelocityY - normalVelocityY;

            return new MovementContactResolution(
                MovementContactOutcome.ShoveThrough,
                (normalVelocityX * tuning.LightContactMomentumRetention)
                    + (tangentVelocityX * tuning.LightContactSteeringRetention),
                (normalVelocityY * tuning.LightContactMomentumRetention)
                    + (tangentVelocityY * tuning.LightContactSteeringRetention));
        }

        private static MovementContactResolution ResolveBlocked(
            ThrusterBurstState movement,
            double normalX,
            double normalY,
            MovementThrusterTuningProfile tuning,
            MovementContactOutcome outcome)
        {
            double inwardDot =
                (movement.VelocityX * normalX)
                + (movement.VelocityY * normalY);
            if (inwardDot >= 0d)
            {
                return new MovementContactResolution(
                    outcome,
                    movement.VelocityX,
                    movement.VelocityY);
            }

            double normalVelocityX = normalX * inwardDot;
            double normalVelocityY = normalY * inwardDot;
            double tangentVelocityX = movement.VelocityX - normalVelocityX;
            double tangentVelocityY = movement.VelocityY - normalVelocityY;

            return new MovementContactResolution(
                outcome,
                tangentVelocityX * tuning.HeavyContactMomentumRetention,
                tangentVelocityY * tuning.HeavyContactMomentumRetention);
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Enemy-contact values must be finite.");
            }
        }
    }
}

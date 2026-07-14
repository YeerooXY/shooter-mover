using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;

namespace ShooterMover.UnityAdapters.Physics
{
    /// <summary>
    /// Read-only source contract used by the projector. It intentionally contains getters only.
    /// Steering input may preserve analogue magnitude; the projector exposes its normalized direction.
    /// </summary>
    public interface IThrusterStatusSource
    {
        bool IsActive { get; }

        bool IsDisposed { get; }

        long Generation { get; }

        StableId TuningIdentity { get; }

        int AvailableCharges { get; }

        int MaximumCharges { get; }

        double RechargeSeconds { get; }

        ThrusterBurstPhase BurstPhase { get; }

        double VelocityX { get; }

        double VelocityY { get; }

        double BurstDirectionX { get; }

        double BurstDirectionY { get; }

        double SteeringIntentX { get; }

        double SteeringIntentY { get; }

        double BurstElapsedSeconds { get; }

        double ExitElapsedSeconds { get; }

        double ChainElapsedSeconds { get; }

        double MinimumChainIntervalSeconds { get; }
    }

    /// <summary>
    /// Projects movement-owned thruster state into an immutable domain snapshot.
    /// The projector performs reads only and never retains the actor or a mutable state reference.
    /// </summary>
    public static class ThrusterStatusProjector
    {
        public static ThrusterStatusSnapshot Project(
            MovementActor2D actor,
            MovementThrusterTuningProfile tuning)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            MovementThrusterTuningProfileValidator.Validate(tuning);
            return Project(new MovementActorThrusterStatusSource(actor, tuning));
        }

        public static ThrusterStatusSnapshot Project(IThrusterStatusSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            StableId tuningIdentity = source.TuningIdentity;
            if (tuningIdentity == null)
            {
                throw new ArgumentException(
                    "Thruster status source must expose a tuning identity.",
                    nameof(source));
            }

            long generation = source.Generation;
            if (generation < 0L)
            {
                throw new ArgumentException(
                    "Thruster status source generation cannot be negative.",
                    nameof(source));
            }

            int maximumCharges = source.MaximumCharges;
            if (maximumCharges < 0)
            {
                throw new ArgumentException(
                    "Thruster status source maximum charges cannot be negative.",
                    nameof(source));
            }

            double rechargeSeconds = source.RechargeSeconds;
            ValidateFinitePositive(rechargeSeconds, nameof(source));

            if (source.IsDisposed)
            {
                if (source.IsActive)
                {
                    throw new ArgumentException(
                        "A disposed thruster source cannot also be active.",
                        nameof(source));
                }

                return CreateUnavailable(
                    ThrusterStatusState.Disposed,
                    tuningIdentity,
                    generation,
                    maximumCharges,
                    rechargeSeconds,
                    source.MinimumChainIntervalSeconds);
            }

            if (!source.IsActive)
            {
                return CreateUnavailable(
                    ThrusterStatusState.Unavailable,
                    tuningIdentity,
                    generation,
                    maximumCharges,
                    rechargeSeconds,
                    source.MinimumChainIntervalSeconds);
            }

            int availableCharges = source.AvailableCharges;
            if (maximumCharges < 1
                || availableCharges < 0
                || availableCharges > maximumCharges)
            {
                throw new ArgumentException(
                    "Active thruster charge counts must be within a non-zero capacity.",
                    nameof(source));
            }

            int regeneratingCharges = maximumCharges - availableCharges;
            ThrusterBurstPhase burstPhase = source.BurstPhase;
            if (!Enum.IsDefined(typeof(ThrusterBurstPhase), burstPhase))
            {
                throw new ArgumentException("Thruster source exposes an unknown burst phase.", nameof(source));
            }

            double steeringDirectionX;
            double steeringDirectionY;
            NormalizeOrZero(
                source.SteeringIntentX,
                source.SteeringIntentY,
                out steeringDirectionX,
                out steeringDirectionY);

            ThrusterStatusState state = Classify(
                availableCharges,
                regeneratingCharges,
                burstPhase);

            return new ThrusterStatusSnapshot(
                state,
                tuningIdentity,
                generation,
                availableCharges,
                maximumCharges,
                regeneratingCharges,
                rechargeSeconds,
                burstPhase,
                source.VelocityX,
                source.VelocityY,
                source.BurstDirectionX,
                source.BurstDirectionY,
                steeringDirectionX,
                steeringDirectionY,
                source.BurstElapsedSeconds,
                source.ExitElapsedSeconds,
                source.ChainElapsedSeconds,
                source.MinimumChainIntervalSeconds);
        }

        private static ThrusterStatusSnapshot CreateUnavailable(
            ThrusterStatusState state,
            StableId tuningIdentity,
            long generation,
            int maximumCharges,
            double rechargeSeconds,
            double minimumChainIntervalSeconds)
        {
            ValidateFiniteNonNegative(minimumChainIntervalSeconds, nameof(minimumChainIntervalSeconds));
            return new ThrusterStatusSnapshot(
                state,
                tuningIdentity,
                generation,
                0,
                maximumCharges,
                0,
                rechargeSeconds,
                ThrusterBurstPhase.Ready,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                minimumChainIntervalSeconds);
        }

        private static ThrusterStatusState Classify(
            int availableCharges,
            int regeneratingCharges,
            ThrusterBurstPhase phase)
        {
            if (phase == ThrusterBurstPhase.Burst)
            {
                return ThrusterStatusState.Burst;
            }

            if (availableCharges == 0)
            {
                return ThrusterStatusState.Empty;
            }

            if (regeneratingCharges > 0)
            {
                return ThrusterStatusState.Regenerating;
            }

            return ThrusterStatusState.Ready;
        }

        private static void NormalizeOrZero(
            double x,
            double y,
            out double normalizedX,
            out double normalizedY)
        {
            ValidateFinite(x, nameof(x));
            ValidateFinite(y, nameof(y));

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

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentException(
                    "Thruster status source values must be finite.",
                    parameterName);
            }
        }

        private static void ValidateFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentException(
                    "Thruster status source duration must be finite and positive.",
                    parameterName);
            }
        }

        private static void ValidateFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentException(
                    "Thruster status source duration must be finite and non-negative.",
                    parameterName);
            }
        }

        private sealed class MovementActorThrusterStatusSource : IThrusterStatusSource
        {
            public MovementActorThrusterStatusSource(
                MovementActor2D actor,
                MovementThrusterTuningProfile suppliedTuning)
            {
                IsActive = actor.IsActive;
                IsDisposed = actor.IsDisposed;
                Generation = actor.Generation;
                AvailableCharges = actor.AvailableThrusterCharges;
                MaximumCharges = actor.MaximumThrusterCharges;

                MovementContactStateSnapshot movementSnapshot;
                bool hasMovementSnapshot = actor.TryReadContactSnapshot(out movementSnapshot);
                MovementThrusterTuningProfile effectiveTuning =
                    hasMovementSnapshot ? movementSnapshot.Tuning : suppliedTuning;

                if (hasMovementSnapshot
                    && effectiveTuning.DeterministicIdentity != suppliedTuning.DeterministicIdentity)
                {
                    throw new InvalidOperationException(
                        "Movement actor status was requested with a different tuning identity.");
                }

                if (IsActive
                    && MaximumCharges != effectiveTuning.ThrusterBaselineChargeCount)
                {
                    throw new InvalidOperationException(
                        "Movement actor charge capacity does not match the supplied tuning profile.");
                }

                TuningIdentity = effectiveTuning.DeterministicIdentity;
                RechargeSeconds = effectiveTuning.ThrusterRechargeSeconds;
                MinimumChainIntervalSeconds =
                    effectiveTuning.ThrusterMinimumChainIntervalSeconds;

                if (hasMovementSnapshot)
                {
                    ThrusterBurstState movement = movementSnapshot.Movement;
                    BurstPhase = movement.Phase;
                    VelocityX = movement.VelocityX;
                    VelocityY = movement.VelocityY;
                    BurstDirectionX = movement.DirectionX;
                    BurstDirectionY = movement.DirectionY;
                    SteeringIntentX = movementSnapshot.NormalizedMoveX;
                    SteeringIntentY = movementSnapshot.NormalizedMoveY;
                    BurstElapsedSeconds = movement.BurstElapsedSeconds;
                    ExitElapsedSeconds = movement.ExitElapsedSeconds;
                    ChainElapsedSeconds = movement.ChainElapsedSeconds;
                }
                else
                {
                    BurstPhase = actor.CurrentPhase;
                    VelocityX = actor.CurrentVelocityX;
                    VelocityY = actor.CurrentVelocityY;
                    BurstDirectionX = 0d;
                    BurstDirectionY = 0d;
                    SteeringIntentX = 0d;
                    SteeringIntentY = 0d;
                    BurstElapsedSeconds = 0d;
                    ExitElapsedSeconds = 0d;
                    ChainElapsedSeconds = IsActive
                        ? effectiveTuning.ThrusterMinimumChainIntervalSeconds
                        : 0d;
                }
            }

            public bool IsActive { get; }

            public bool IsDisposed { get; }

            public long Generation { get; }

            public StableId TuningIdentity { get; }

            public int AvailableCharges { get; }

            public int MaximumCharges { get; }

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
        }
    }
}

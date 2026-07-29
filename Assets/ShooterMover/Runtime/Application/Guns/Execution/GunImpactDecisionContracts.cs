using System;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public enum GunImpactEventKind
    {
        EnemyImpact = 1,
        WallImpact = 2,
        RangeExpiry = 3,
        Termination = 4,
    }

    public enum GunImpactDecisionKind
    {
        Ignored = 1,
        Continue = 2,
        Terminate = 3,
        Ricochet = 4,
        DuplicateWallContact = 5,
    }

    public enum GunImpactContinuation
    {
        Continue = 1,
        Terminate = 2,
    }

    /// <summary>
    /// Engine-independent input for one impact event. Projectile identity and impact ordinal
    /// are carried through unchanged; Unity collision objects never cross this boundary.
    /// </summary>
    public sealed class GunImpactRequest
    {
        public GunImpactRequest(
            GunEffectIdentity projectileIdentity,
            int impactOrdinal,
            long simulationStep,
            GunImpactEventKind eventKind,
            GunImpactSpec impactSpec,
            GunVector2 incomingDirection,
            double speed,
            GunVector2 wallNormal,
            GunWallContactId wallContactId,
            RicochetState ricochetState)
        {
            if (impactOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(impactOrdinal));
            }
            if (simulationStep < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationStep));
            }
            if (!Enum.IsDefined(typeof(GunImpactEventKind), eventKind))
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind));
            }
            if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }
            if (incomingDirection != null && !incomingDirection.IsFinite)
            {
                throw new ArgumentException(
                    "Incoming direction must be finite when supplied.",
                    nameof(incomingDirection));
            }
            if (wallNormal != null && !wallNormal.IsFinite)
            {
                throw new ArgumentException(
                    "Wall normal must be finite when supplied.",
                    nameof(wallNormal));
            }

            ProjectileIdentity = projectileIdentity
                ?? throw new ArgumentNullException(nameof(projectileIdentity));
            ImpactSpec = impactSpec ?? throw new ArgumentNullException(nameof(impactSpec));
            RicochetState = ricochetState
                ?? throw new ArgumentNullException(nameof(ricochetState));

            if (eventKind == GunImpactEventKind.WallImpact)
            {
                WallContactId = wallContactId
                    ?? throw new ArgumentNullException(nameof(wallContactId));

                if (impactSpec.Ricochet != null)
                {
                    if (incomingDirection == null || incomingDirection.LengthSquared <= 0d)
                    {
                        throw new ArgumentException(
                            "Ricochet evaluation requires a non-zero incoming direction.",
                            nameof(incomingDirection));
                    }
                    if (wallNormal == null || wallNormal.LengthSquared <= 0d)
                    {
                        throw new ArgumentException(
                            "Ricochet evaluation requires a non-zero wall normal.",
                            nameof(wallNormal));
                    }
                }
            }
            else if (wallContactId != null)
            {
                throw new ArgumentException(
                    "Wall contact identity may only be supplied for wall-impact events.",
                    nameof(wallContactId));
            }

            ImpactOrdinal = impactOrdinal;
            SimulationStep = simulationStep;
            EventKind = eventKind;
            IncomingDirection = incomingDirection;
            Speed = speed;
            WallNormal = wallNormal;
        }

        public GunEffectIdentity ProjectileIdentity { get; }
        public int ImpactOrdinal { get; }
        public long SimulationStep { get; }
        public GunImpactEventKind EventKind { get; }
        public GunImpactSpec ImpactSpec { get; }
        public GunVector2 IncomingDirection { get; }
        public double Speed { get; }
        public GunVector2 WallNormal { get; }
        public GunWallContactId WallContactId { get; }
        public RicochetState RicochetState { get; }
    }

    public sealed class GunImpactDecision
    {
        internal GunImpactDecision(
            GunEffectIdentity projectileIdentity,
            int impactOrdinal,
            GunImpactEventKind eventKind,
            GunImpactDecisionKind kind,
            GunImpactContinuation continuation,
            GunExplosionTriggerReason explosionReasons,
            bool consumesPierce,
            bool consumesBounceOpportunity,
            GunVector2 directionAfterImpact,
            double speedAfterImpact,
            double homingPauseSeconds,
            RicochetState ricochetState,
            DeterministicRandom random)
        {
            ProjectileIdentity = projectileIdentity
                ?? throw new ArgumentNullException(nameof(projectileIdentity));
            if (!Enum.IsDefined(typeof(GunImpactEventKind), eventKind))
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind));
            }
            if (!Enum.IsDefined(typeof(GunImpactDecisionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (!Enum.IsDefined(typeof(GunImpactContinuation), continuation))
            {
                throw new ArgumentOutOfRangeException(nameof(continuation));
            }
            if (!GunExplosionTriggerReasonRules.IsValid(explosionReasons))
            {
                throw new ArgumentOutOfRangeException(nameof(explosionReasons));
            }

            ImpactOrdinal = impactOrdinal;
            EventKind = eventKind;
            Kind = kind;
            Continuation = continuation;
            ExplosionReasons = explosionReasons;
            ConsumesPierce = consumesPierce;
            ConsumesBounceOpportunity = consumesBounceOpportunity;
            DirectionAfterImpact = directionAfterImpact;
            SpeedAfterImpact = speedAfterImpact;
            HomingPauseSeconds = homingPauseSeconds;
            RicochetState = ricochetState
                ?? throw new ArgumentNullException(nameof(ricochetState));
            Random = random;
        }

        public GunEffectIdentity ProjectileIdentity { get; }
        public int ImpactOrdinal { get; }
        public GunImpactEventKind EventKind { get; }
        public GunImpactDecisionKind Kind { get; }
        public GunImpactContinuation Continuation { get; }
        public GunExplosionTriggerReason ExplosionReasons { get; }
        public bool ConsumesPierce { get; }
        public bool ConsumesBounceOpportunity { get; }
        public GunVector2 DirectionAfterImpact { get; }
        public double SpeedAfterImpact { get; }
        public double HomingPauseSeconds { get; }
        public RicochetState RicochetState { get; }
        public DeterministicRandom Random { get; }

        public bool ShouldTerminate
        {
            get { return Continuation == GunImpactContinuation.Terminate; }
        }

        public bool ShouldExplode
        {
            get { return ExplosionReasons != GunExplosionTriggerReason.None; }
        }
    }
}

using System;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum WeaponImpactEventKind
    {
        EnemyImpact = 1,
        WallImpact = 2,
        RangeExpiry = 3,
        Termination = 4,
    }

    public enum WeaponImpactDecisionKind
    {
        Ignored = 1,
        Continue = 2,
        Terminate = 3,
        Ricochet = 4,
        DuplicateWallContact = 5,
    }

    public enum WeaponImpactContinuation
    {
        Continue = 1,
        Terminate = 2,
    }

    /// <summary>
    /// Engine-independent input for one impact event. Projectile identity and impact ordinal
    /// are carried through unchanged; Unity collision objects never cross this boundary.
    /// </summary>
    public sealed class WeaponImpactRequest
    {
        public WeaponImpactRequest(
            WeaponEffectIdentity projectileIdentity,
            int impactOrdinal,
            long simulationStep,
            WeaponImpactEventKind eventKind,
            WeaponImpactSpec impactSpec,
            WeaponVector2 incomingDirection,
            double speed,
            WeaponVector2 wallNormal,
            WeaponWallContactId wallContactId,
            WeaponRicochetRuntimeState ricochetState)
        {
            if (impactOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(impactOrdinal));
            }
            if (simulationStep < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationStep));
            }
            if (!Enum.IsDefined(typeof(WeaponImpactEventKind), eventKind))
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

            if (eventKind == WeaponImpactEventKind.WallImpact)
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

        public WeaponEffectIdentity ProjectileIdentity { get; }
        public int ImpactOrdinal { get; }
        public long SimulationStep { get; }
        public WeaponImpactEventKind EventKind { get; }
        public WeaponImpactSpec ImpactSpec { get; }
        public WeaponVector2 IncomingDirection { get; }
        public double Speed { get; }
        public WeaponVector2 WallNormal { get; }
        public WeaponWallContactId WallContactId { get; }
        public WeaponRicochetRuntimeState RicochetState { get; }
    }

    public sealed class WeaponImpactDecision
    {
        internal WeaponImpactDecision(
            WeaponEffectIdentity projectileIdentity,
            int impactOrdinal,
            WeaponImpactEventKind eventKind,
            WeaponImpactDecisionKind kind,
            WeaponImpactContinuation continuation,
            WeaponExplosionTriggerReason explosionReasons,
            bool consumesPierce,
            bool consumesBounceOpportunity,
            WeaponVector2 directionAfterImpact,
            double speedAfterImpact,
            double homingPauseSeconds,
            WeaponRicochetRuntimeState ricochetState,
            DeterministicRandom random)
        {
            ProjectileIdentity = projectileIdentity
                ?? throw new ArgumentNullException(nameof(projectileIdentity));
            if (!Enum.IsDefined(typeof(WeaponImpactEventKind), eventKind))
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind));
            }
            if (!Enum.IsDefined(typeof(WeaponImpactDecisionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (!Enum.IsDefined(typeof(WeaponImpactContinuation), continuation))
            {
                throw new ArgumentOutOfRangeException(nameof(continuation));
            }
            if (!WeaponExplosionTriggerReasonRules.IsValid(explosionReasons))
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

        public WeaponEffectIdentity ProjectileIdentity { get; }
        public int ImpactOrdinal { get; }
        public WeaponImpactEventKind EventKind { get; }
        public WeaponImpactDecisionKind Kind { get; }
        public WeaponImpactContinuation Continuation { get; }
        public WeaponExplosionTriggerReason ExplosionReasons { get; }
        public bool ConsumesPierce { get; }
        public bool ConsumesBounceOpportunity { get; }
        public WeaponVector2 DirectionAfterImpact { get; }
        public double SpeedAfterImpact { get; }
        public double HomingPauseSeconds { get; }
        public WeaponRicochetRuntimeState RicochetState { get; }
        public DeterministicRandom Random { get; }

        public bool ShouldTerminate
        {
            get { return Continuation == WeaponImpactContinuation.Terminate; }
        }

        public bool ShouldExplode
        {
            get { return ExplosionReasons != WeaponExplosionTriggerReason.None; }
        }
    }
}

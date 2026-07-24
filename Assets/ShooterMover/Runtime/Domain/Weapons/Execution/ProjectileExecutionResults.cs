using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Domain.Weapons.Execution
{
    public sealed class ProjectileContact
    {
        private ProjectileContact(
            ProjectileContactKind kind,
            WeaponTargetReference target,
            StableId surfaceId,
            WeaponVector2 position)
        {
            Kind = kind;
            Target = target;
            SurfaceId = surfaceId;
            Position = position;
        }

        public ProjectileContactKind Kind { get; }
        public WeaponTargetReference Target { get; }
        public StableId SurfaceId { get; }
        public WeaponVector2 Position { get; }

        public static ProjectileContact Enemy(
            WeaponTargetReference target,
            WeaponVector2 position)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            return Create(ProjectileContactKind.Enemy, target, null, position);
        }

        public static ProjectileContact Wall(StableId surfaceId, WeaponVector2 position)
        {
            if (surfaceId == null)
            {
                throw new ArgumentNullException(nameof(surfaceId));
            }
            return Create(ProjectileContactKind.Wall, null, surfaceId, position);
        }

        public static ProjectileContact RangeExpiry(WeaponVector2 position)
        {
            return Create(ProjectileContactKind.RangeExpiry, null, null, position);
        }

        public static ProjectileContact ExplicitTermination(WeaponVector2 position)
        {
            return Create(ProjectileContactKind.ExplicitTermination, null, null, position);
        }

        private static ProjectileContact Create(
            ProjectileContactKind kind,
            WeaponTargetReference target,
            StableId surfaceId,
            WeaponVector2 position)
        {
            if (position == null || !position.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            return new ProjectileContact(kind, target, surfaceId, position);
        }
    }

    public sealed class ProjectileMovementResult
    {
        public ProjectileMovementResult(
            ProjectileLifecycleState state,
            double requestedDistance,
            double travelledDistance,
            bool reachedRangeLimit)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            RequestedDistance = requestedDistance;
            TravelledDistance = travelledDistance;
            ReachedRangeLimit = reachedRangeLimit;
        }

        public ProjectileLifecycleState State { get; }
        public double RequestedDistance { get; }
        public double TravelledDistance { get; }
        public bool ReachedRangeLimit { get; }
    }

    public enum ProjectileWallImpactResolutionKind
    {
        SuccessfulBounce = 1,
        BlockingImpact = 2,
    }

    /// <summary>
    /// Result supplied by WEAPON-IMPACT-001 or another dedicated wall-impact authority.
    /// Explosion reasons are preserved independently from continuation.
    /// </summary>
    public sealed class ProjectileWallImpactResolution
    {
        private const WeaponExplosionTriggerReason AllowedWallReasons =
            WeaponExplosionTriggerReason.WallImpact
            | WeaponExplosionTriggerReason.Termination;

        private ProjectileWallImpactResolution(
            ProjectileWallImpactResolutionKind kind,
            WeaponVector2 directionAfterImpact,
            double speedAfterImpact,
            double homingPauseSeconds,
            WeaponExplosionTriggerReason explosionReasons)
        {
            Kind = kind;
            DirectionAfterImpact = directionAfterImpact;
            SpeedAfterImpact = speedAfterImpact;
            HomingPauseSeconds = homingPauseSeconds;
            ExplosionReasons = explosionReasons;
        }

        public ProjectileWallImpactResolutionKind Kind { get; }
        public WeaponVector2 DirectionAfterImpact { get; }
        public double SpeedAfterImpact { get; }
        public double HomingPauseSeconds { get; }
        public WeaponExplosionTriggerReason ExplosionReasons { get; }

        public static ProjectileWallImpactResolution SuccessfulBounce(
            WeaponVector2 directionAfterImpact,
            double speedAfterImpact,
            double homingPauseSeconds,
            WeaponExplosionTriggerReason explosionReasons)
        {
            if (directionAfterImpact == null
                || !directionAfterImpact.IsFinite
                || directionAfterImpact.LengthSquared <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(directionAfterImpact));
            }
            if (double.IsNaN(speedAfterImpact)
                || double.IsInfinity(speedAfterImpact)
                || speedAfterImpact <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(speedAfterImpact));
            }
            if (double.IsNaN(homingPauseSeconds)
                || double.IsInfinity(homingPauseSeconds)
                || homingPauseSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(homingPauseSeconds));
            }
            RequireWallReasons(explosionReasons);
            if ((explosionReasons & WeaponExplosionTriggerReason.Termination) != 0)
            {
                throw new ArgumentException(
                    "A successful bounce cannot carry a termination explosion reason.",
                    nameof(explosionReasons));
            }

            return new ProjectileWallImpactResolution(
                ProjectileWallImpactResolutionKind.SuccessfulBounce,
                directionAfterImpact.Normalized,
                speedAfterImpact,
                homingPauseSeconds,
                explosionReasons);
        }

        public static ProjectileWallImpactResolution BlockingImpact(
            WeaponExplosionTriggerReason explosionReasons)
        {
            RequireWallReasons(explosionReasons);
            return new ProjectileWallImpactResolution(
                ProjectileWallImpactResolutionKind.BlockingImpact,
                null,
                0d,
                0d,
                explosionReasons);
        }

        private static void RequireWallReasons(WeaponExplosionTriggerReason explosionReasons)
        {
            if (!WeaponExplosionTriggerReasonRules.IsValid(explosionReasons)
                || (explosionReasons & ~AllowedWallReasons) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(explosionReasons));
            }
        }
    }

    public enum ProjectileImpactDecisionStatus
    {
        Ignored = 1,
        Resolved = 2,
        RequiresWallImpactResolution = 3,
    }

    public sealed class ProjectileImpactDecision
    {
        public ProjectileImpactDecision(
            ProjectileLifecycleState stateBefore,
            ProjectileLifecycleState stateAfter,
            ProjectileContact contact,
            ProjectileImpactDecisionStatus status,
            bool enemyImpactApplied,
            WeaponExplosionTriggerReason explosionTriggerReasons)
        {
            StateBefore = stateBefore ?? throw new ArgumentNullException(nameof(stateBefore));
            StateAfter = stateAfter ?? throw new ArgumentNullException(nameof(stateAfter));
            Contact = contact ?? throw new ArgumentNullException(nameof(contact));
            if (!Enum.IsDefined(typeof(ProjectileImpactDecisionStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (!WeaponExplosionTriggerReasonRules.IsValid(explosionTriggerReasons))
            {
                throw new ArgumentOutOfRangeException(nameof(explosionTriggerReasons));
            }
            if (status == ProjectileImpactDecisionStatus.Ignored
                && !ReferenceEquals(stateBefore, stateAfter))
            {
                throw new ArgumentException(
                    "Ignored projectile contacts cannot alter lifecycle state.",
                    nameof(stateAfter));
            }
            if (status == ProjectileImpactDecisionStatus.RequiresWallImpactResolution)
            {
                if (!stateAfter.IsAwaitingWallImpactResolution)
                {
                    throw new ArgumentException(
                        "Pending wall decisions require an awaiting lifecycle state.",
                        nameof(stateAfter));
                }
                if (explosionTriggerReasons != WeaponExplosionTriggerReason.None)
                {
                    throw new ArgumentException(
                        "Pending wall decisions cannot emit explosion reasons.",
                        nameof(explosionTriggerReasons));
                }
            }

            Status = status;
            EnemyImpactApplied = enemyImpactApplied;
            ExplosionTriggerReasons = explosionTriggerReasons;
        }

        public ProjectileLifecycleState StateBefore { get; }
        public ProjectileLifecycleState StateAfter { get; }
        public ProjectileContact Contact { get; }
        public ProjectileImpactDecisionStatus Status { get; }
        public bool EnemyImpactApplied { get; }
        public WeaponExplosionTriggerReason ExplosionTriggerReasons { get; }
        public bool Handled { get { return Status != ProjectileImpactDecisionStatus.Ignored; } }
        public bool RequiresWallImpactResolution
        {
            get { return Status == ProjectileImpactDecisionStatus.RequiresWallImpactResolution; }
        }
        public bool ContinuesFlight
        {
            get { return Status == ProjectileImpactDecisionStatus.Resolved && StateAfter.IsActive; }
        }
        public bool Terminates { get { return StateAfter.IsTerminated; } }
        public ProjectileTerminationReason TerminationReason
        {
            get { return StateAfter.TerminationReason; }
        }
    }

    public sealed class ProjectileEffectEmission
    {
        public ProjectileEffectEmission(
            ProjectileEffectEmissionKind kind,
            ProjectileLifecycleContext lifecycle,
            ProjectileContactKind sourceContactKind,
            WeaponTargetReference target,
            StableId surfaceId,
            WeaponVector2 position,
            int eventOrdinal,
            WeaponExplosionTriggerReason explosionTriggerReasons,
            ProjectileTerminationReason terminationReason,
            WeaponDamageSpec damage,
            WeaponEffects effects)
        {
            if (!Enum.IsDefined(typeof(ProjectileEffectEmissionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (position == null || !position.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            if (eventOrdinal < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(eventOrdinal));
            }
            if (!WeaponExplosionTriggerReasonRules.IsValid(explosionTriggerReasons))
            {
                throw new ArgumentOutOfRangeException(nameof(explosionTriggerReasons));
            }

            Kind = kind;
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            SourceContactKind = sourceContactKind;
            Target = target;
            SurfaceId = surfaceId;
            Position = position;
            EventOrdinal = eventOrdinal;
            ExplosionTriggerReasons = explosionTriggerReasons;
            TerminationReason = terminationReason;
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        public ProjectileEffectEmissionKind Kind { get; }
        public ProjectileLifecycleContext Lifecycle { get; }
        public ProjectileExecutionIdentity ProjectileIdentity { get { return Lifecycle.Identity; } }
        public ProjectileContactKind SourceContactKind { get; }
        public WeaponTargetReference Target { get; }
        public StableId SurfaceId { get; }
        public WeaponVector2 Position { get; }
        public int EventOrdinal { get; }
        public WeaponExplosionTriggerReason ExplosionTriggerReasons { get; }
        public ProjectileTerminationReason TerminationReason { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponEffects Effects { get; }

        public string ToCanonicalString()
        {
            return string.Join(
                "|",
                new[]
                {
                    Kind.ToString(),
                    ProjectileIdentity.ProjectileId,
                    SourceContactKind.ToString(),
                    Target == null ? string.Empty : Target.ToCanonicalString(),
                    SurfaceId == null ? string.Empty : SurfaceId.ToString(),
                    Position.ToString(),
                    EventOrdinal.ToString(CultureInfo.InvariantCulture),
                    ((int)ExplosionTriggerReasons).ToString(CultureInfo.InvariantCulture),
                    ((int)TerminationReason).ToString(CultureInfo.InvariantCulture),
                });
        }
    }

    /// <summary>
    /// Pure projectile emission result. WeaponEffectBatch remains the retained downstream boundary.
    /// </summary>
    public sealed class ProjectileEmissionResult
    {
        private readonly ReadOnlyCollection<ProjectileEffectEmission> emissions;

        public ProjectileEmissionResult(IList<ProjectileEffectEmission> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            List<ProjectileEffectEmission> copy = new List<ProjectileEffectEmission>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                ProjectileEffectEmission emission = values[index];
                if (emission == null)
                {
                    throw new ArgumentException(
                        "Projectile emission results cannot contain null values.",
                        nameof(values));
                }
                copy.Add(emission);
            }

            emissions = new ReadOnlyCollection<ProjectileEffectEmission>(copy);
        }

        public IReadOnlyList<ProjectileEffectEmission> Emissions { get { return emissions; } }
        public int Count { get { return emissions.Count; } }
    }
}

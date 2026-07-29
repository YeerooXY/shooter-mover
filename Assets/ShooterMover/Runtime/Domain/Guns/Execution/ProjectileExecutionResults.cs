using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Domain.Guns.Execution
{
    public sealed class ProjectileContact
    {
        private ProjectileContact(
            ProjectileContactKind kind,
            GunTargetReference target,
            StableId surfaceId,
            GunVector2 position)
        {
            Kind = kind;
            Target = target;
            SurfaceId = surfaceId;
            Position = position;
        }

        public ProjectileContactKind Kind { get; }
        public GunTargetReference Target { get; }
        public StableId SurfaceId { get; }
        public GunVector2 Position { get; }

        public static ProjectileContact Enemy(
            GunTargetReference target,
            GunVector2 position)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            return Create(ProjectileContactKind.Enemy, target, null, position);
        }

        public static ProjectileContact Wall(StableId surfaceId, GunVector2 position)
        {
            if (surfaceId == null)
            {
                throw new ArgumentNullException(nameof(surfaceId));
            }
            return Create(ProjectileContactKind.Wall, null, surfaceId, position);
        }

        public static ProjectileContact RangeExpiry(GunVector2 position)
        {
            return Create(ProjectileContactKind.RangeExpiry, null, null, position);
        }

        public static ProjectileContact ExplicitTermination(GunVector2 position)
        {
            return Create(ProjectileContactKind.ExplicitTermination, null, null, position);
        }

        private static ProjectileContact Create(
            ProjectileContactKind kind,
            GunTargetReference target,
            StableId surfaceId,
            GunVector2 position)
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
        private const GunExplosionTriggerReason AllowedWallReasons =
            GunExplosionTriggerReason.WallImpact
            | GunExplosionTriggerReason.Termination;

        private ProjectileWallImpactResolution(
            ProjectileWallImpactResolutionKind kind,
            GunVector2 directionAfterImpact,
            double speedAfterImpact,
            double homingPauseSeconds,
            GunExplosionTriggerReason explosionReasons)
        {
            Kind = kind;
            DirectionAfterImpact = directionAfterImpact;
            SpeedAfterImpact = speedAfterImpact;
            HomingPauseSeconds = homingPauseSeconds;
            ExplosionReasons = explosionReasons;
        }

        public ProjectileWallImpactResolutionKind Kind { get; }
        public GunVector2 DirectionAfterImpact { get; }
        public double SpeedAfterImpact { get; }
        public double HomingPauseSeconds { get; }
        public GunExplosionTriggerReason ExplosionReasons { get; }

        public static ProjectileWallImpactResolution SuccessfulBounce(
            GunVector2 directionAfterImpact,
            double speedAfterImpact,
            double homingPauseSeconds,
            GunExplosionTriggerReason explosionReasons)
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
            if ((explosionReasons & GunExplosionTriggerReason.Termination) != 0)
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
            GunExplosionTriggerReason explosionReasons)
        {
            RequireWallReasons(explosionReasons);
            return new ProjectileWallImpactResolution(
                ProjectileWallImpactResolutionKind.BlockingImpact,
                null,
                0d,
                0d,
                explosionReasons);
        }

        private static void RequireWallReasons(GunExplosionTriggerReason explosionReasons)
        {
            if (!GunExplosionTriggerReasonRules.IsValid(explosionReasons)
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
            GunExplosionTriggerReason explosionTriggerReasons)
        {
            StateBefore = stateBefore ?? throw new ArgumentNullException(nameof(stateBefore));
            StateAfter = stateAfter ?? throw new ArgumentNullException(nameof(stateAfter));
            Contact = contact ?? throw new ArgumentNullException(nameof(contact));
            if (!Enum.IsDefined(typeof(ProjectileImpactDecisionStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (!GunExplosionTriggerReasonRules.IsValid(explosionTriggerReasons))
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
                if (explosionTriggerReasons != GunExplosionTriggerReason.None)
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
        public GunExplosionTriggerReason ExplosionTriggerReasons { get; }
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
        /// <summary>
        /// Transitional compatibility constructor. Canonical projectile execution must use the
        /// profile-retaining overload below.
        /// </summary>
        public ProjectileEffectEmission(
            ProjectileEffectEmissionKind kind,
            ProjectileLifecycleContext lifecycle,
            ProjectileContactKind sourceContactKind,
            GunTargetReference target,
            StableId surfaceId,
            GunVector2 position,
            int eventOrdinal,
            GunExplosionTriggerReason explosionTriggerReasons,
            ProjectileTerminationReason terminationReason,
            GunDamageSpec damage,
            GunEffects effects)
            : this(
                kind,
                lifecycle,
                sourceContactKind,
                target,
                surfaceId,
                position,
                eventOrdinal,
                explosionTriggerReasons,
                terminationReason,
                null,
                damage,
                effects)
        {
        }

        public ProjectileEffectEmission(
            ProjectileEffectEmissionKind kind,
            ProjectileLifecycleContext lifecycle,
            ProjectileContactKind sourceContactKind,
            GunTargetReference target,
            StableId surfaceId,
            GunVector2 position,
            int eventOrdinal,
            GunExplosionTriggerReason explosionTriggerReasons,
            ProjectileTerminationReason terminationReason,
            ProjectileExecutionProfile profile,
            GunDamageSpec damage,
            GunEffects effects)
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
            if (!GunExplosionTriggerReasonRules.IsValid(explosionTriggerReasons))
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
            Profile = profile;
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));

            if (profile != null)
            {
                GunEffectIdentity identity = lifecycle.Identity.SourceIdentity;
                if (!profile.DefinitionId.Equals(identity.GunDefinitionId)
                    || (profile.IsCanonical
                        && (profile.EquipmentInstanceId == null
                            || !profile.EquipmentInstanceId.Equals(
                                identity.EquipmentInstanceId)))
                    || !ReferenceEquals(profile.Damage, damage)
                    || !ReferenceEquals(profile.Effects, effects))
                {
                    throw new ArgumentException(
                        "projectile-emission-profile-payload-mismatch",
                        nameof(profile));
                }
            }
        }

        public ProjectileEffectEmissionKind Kind { get; }
        public ProjectileLifecycleContext Lifecycle { get; }
        public ProjectileExecutionIdentity ProjectileIdentity { get { return Lifecycle.Identity; } }
        public ProjectileContactKind SourceContactKind { get; }
        public GunTargetReference Target { get; }
        public StableId SurfaceId { get; }
        public GunVector2 Position { get; }
        public int EventOrdinal { get; }
        public GunExplosionTriggerReason ExplosionTriggerReasons { get; }
        public ProjectileTerminationReason TerminationReason { get; }
        public ProjectileExecutionProfile Profile { get; }
        public GunDamageSpec Damage { get; }
        public GunEffects Effects { get; }
        public bool IsCanonicalRocket
        {
            get { return Profile != null && Profile.IsCanonicalRocket; }
        }

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
                    Profile == null
                        ? "transitional"
                        : ((int)Profile.ExecutionMode).ToString(CultureInfo.InvariantCulture),
                    Profile == null || !Profile.CanonicalDeliveryType.HasValue
                        ? "none"
                        : ((int)Profile.CanonicalDeliveryType.Value).ToString(
                            CultureInfo.InvariantCulture),
                    Profile == null
                        ? "none"
                        : Profile.Pierce.Tenths.ToString(CultureInfo.InvariantCulture),
                    Damage.DirectDamage.ToString("R", CultureInfo.InvariantCulture),
                    Damage.AreaDamage.ToString("R", CultureInfo.InvariantCulture),
                    Effects.Explosion == null
                        ? "none"
                        : Effects.Explosion.Radius.ToString("R", CultureInfo.InvariantCulture),
                });
        }
    }

    /// <summary>
    /// Pure projectile emission result. GunEffectBatch remains the retained downstream boundary.
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

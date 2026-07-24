using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons.Guidance;

namespace ShooterMover.Domain.Weapons.Execution
{
    /// <summary>
    /// Immutable projectile lifecycle. WeaponGuidanceState is the sole direction and target-lock
    /// authority; wall-impact policy remains outside this model.
    /// </summary>
    public sealed class ProjectileLifecycleState
    {
        private ProjectileLifecycleState(
            ProjectileLifecycleContext lifecycle,
            ProjectileExecutionProfile profile,
            WeaponVector2 position,
            double distanceTravelled,
            double speed,
            WeaponGuidanceState guidance,
            ProjectilePierceState pierce,
            int eventOrdinal,
            WeaponTargetReference lastTarget,
            StableId lastSurfaceId,
            ProjectileContactKind? lastContactKind,
            ProjectileLifecycleStatus status,
            ProjectileTerminationReason terminationReason)
        {
            Lifecycle = lifecycle;
            Profile = profile;
            Position = position;
            DistanceTravelled = distanceTravelled;
            Speed = speed;
            Guidance = guidance;
            Pierce = pierce;
            EventOrdinal = eventOrdinal;
            LastTarget = lastTarget;
            LastSurfaceId = lastSurfaceId;
            LastContactKind = lastContactKind;
            Status = status;
            TerminationReason = terminationReason;
        }

        public ProjectileLifecycleContext Lifecycle { get; }
        public ProjectileExecutionProfile Profile { get; }
        public WeaponVector2 Position { get; }
        public double DistanceTravelled { get; }
        public double Speed { get; }
        public double RemainingRange
        {
            get { return Math.Max(0d, Profile.Projectile.Range - DistanceTravelled); }
        }
        public WeaponGuidanceState Guidance { get; }
        public WeaponVector2 Direction { get { return Guidance.Direction; } }
        public ProjectilePierceState Pierce { get; }
        public int EventOrdinal { get; }
        public WeaponTargetReference LastTarget { get; }
        public StableId LastSurfaceId { get; }
        public ProjectileContactKind? LastContactKind { get; }
        public ProjectileLifecycleStatus Status { get; }
        public ProjectileTerminationReason TerminationReason { get; }
        public bool IsActive { get { return Status == ProjectileLifecycleStatus.Active; } }
        public bool IsAwaitingWallImpactResolution
        {
            get { return Status == ProjectileLifecycleStatus.AwaitingWallImpactResolution; }
        }
        public bool IsTerminated { get { return Status == ProjectileLifecycleStatus.Terminated; } }

        public static ProjectileLifecycleState Launch(ProjectileLaunchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return new ProjectileLifecycleState(
                request.Lifecycle,
                request.Profile,
                request.Origin,
                0d,
                request.Profile.Projectile.Speed,
                WeaponGuidanceState.Create(request.Direction, request.InitialTarget),
                ProjectilePierceState.Create(request.Profile.Projectile.Pierce),
                0,
                null,
                null,
                null,
                ProjectileLifecycleStatus.Active,
                ProjectileTerminationReason.None);
        }

        public ProjectileLifecycleState WithKinematics(
            WeaponVector2 position,
            double distanceTravelled)
        {
            RequireActive();
            if (position == null || !position.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            if (double.IsNaN(distanceTravelled)
                || double.IsInfinity(distanceTravelled)
                || distanceTravelled < DistanceTravelled
                || distanceTravelled > Profile.Projectile.Range)
            {
                throw new ArgumentOutOfRangeException(nameof(distanceTravelled));
            }

            return Copy(position: position, distanceTravelled: distanceTravelled);
        }

        public ProjectileLifecycleState WithGuidance(WeaponGuidanceState guidance)
        {
            RequireActive();
            if (guidance == null)
            {
                throw new ArgumentNullException(nameof(guidance));
            }
            return Copy(guidance: guidance);
        }

        public ProjectileLifecycleState WithPierce(ProjectilePierceState pierce)
        {
            RequireActive();
            if (pierce == null)
            {
                throw new ArgumentNullException(nameof(pierce));
            }
            if (pierce.AuthoredValue != Profile.Projectile.Pierce)
            {
                throw new ArgumentException(
                    "Projectile pierce state must retain the authored launch value.",
                    nameof(pierce));
            }
            return Copy(pierce: pierce);
        }

        public ProjectileLifecycleState RecordContact(ProjectileContact contact)
        {
            RequireActive();
            if (contact == null)
            {
                throw new ArgumentNullException(nameof(contact));
            }

            return Copy(
                position: contact.Position,
                eventOrdinal: checked(EventOrdinal + 1),
                lastTarget: contact.Target,
                lastSurfaceId: contact.SurfaceId,
                lastContactKind: contact.Kind);
        }

        public ProjectileLifecycleState AwaitWallImpactResolution(ProjectileContact contact)
        {
            if (contact == null || contact.Kind != ProjectileContactKind.Wall)
            {
                throw new ArgumentException(
                    "Only a wall contact may enter wall-impact resolution.",
                    nameof(contact));
            }

            ProjectileLifecycleState contacted = RecordContact(contact);
            return contacted.Copy(status: ProjectileLifecycleStatus.AwaitingWallImpactResolution);
        }

        public ProjectileLifecycleState ResolveSuccessfulWallBounce(
            ProjectileWallImpactResolution resolution)
        {
            RequireAwaitingWallResolution();
            if (resolution == null
                || resolution.Kind != ProjectileWallImpactResolutionKind.SuccessfulBounce)
            {
                throw new ArgumentException(
                    "A successful external wall resolution is required.",
                    nameof(resolution));
            }

            WeaponGuidanceState guidance = Guidance.PauseAfterRicochet(
                resolution.DirectionAfterImpact,
                resolution.HomingPauseSeconds);
            return Copy(
                speed: resolution.SpeedAfterImpact,
                guidance: guidance,
                status: ProjectileLifecycleStatus.Active);
        }

        public ProjectileLifecycleState ResolveBlockingWallImpact()
        {
            RequireAwaitingWallResolution();
            return TerminateCore(ProjectileTerminationReason.WallImpact);
        }

        public ProjectileLifecycleState Terminate(ProjectileTerminationReason reason)
        {
            RequireActive();
            return TerminateCore(reason);
        }

        private ProjectileLifecycleState TerminateCore(ProjectileTerminationReason reason)
        {
            if (reason == ProjectileTerminationReason.None
                || !Enum.IsDefined(typeof(ProjectileTerminationReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }

            return Copy(
                status: ProjectileLifecycleStatus.Terminated,
                terminationReason: reason);
        }

        private ProjectileLifecycleState Copy(
            WeaponVector2 position = null,
            double? distanceTravelled = null,
            double? speed = null,
            WeaponGuidanceState guidance = null,
            ProjectilePierceState pierce = null,
            int? eventOrdinal = null,
            WeaponTargetReference lastTarget = null,
            StableId lastSurfaceId = null,
            ProjectileContactKind? lastContactKind = null,
            ProjectileLifecycleStatus? status = null,
            ProjectileTerminationReason? terminationReason = null)
        {
            bool replacesContact = lastContactKind.HasValue;
            return new ProjectileLifecycleState(
                Lifecycle,
                Profile,
                position ?? Position,
                distanceTravelled ?? DistanceTravelled,
                speed ?? Speed,
                guidance ?? Guidance,
                pierce ?? Pierce,
                eventOrdinal ?? EventOrdinal,
                replacesContact ? lastTarget : LastTarget,
                replacesContact ? lastSurfaceId : LastSurfaceId,
                lastContactKind ?? LastContactKind,
                status ?? Status,
                terminationReason ?? TerminationReason);
        }

        private void RequireActive()
        {
            if (!IsActive)
            {
                throw new InvalidOperationException(
                    "Only active projectile lifecycle state may be changed by this operation.");
            }
        }

        private void RequireAwaitingWallResolution()
        {
            if (!IsAwaitingWallImpactResolution)
            {
                throw new InvalidOperationException(
                    "Projectile is not awaiting external wall-impact resolution.");
            }
        }
    }
}

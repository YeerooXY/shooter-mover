using System;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Domain.Weapons.Execution
{
    public enum ProjectileLifecycleStatus
    {
        Active = 1,
        AwaitingWallImpactResolution = 2,
        Terminated = 3,
    }

    public enum ProjectileFractionalPierceRollState
    {
        NotApplicable = 1,
        Pending = 2,
        Granted = 3,
        Denied = 4,
    }

    public enum ProjectileContactKind
    {
        Enemy = 1,
        Wall = 2,
        RangeExpiry = 3,
        ExplicitTermination = 4,
    }

    public enum ProjectileTerminationReason
    {
        None = 0,
        EnemyImpact = 1,
        WallImpact = 2,
        PierceSpent = 3,
        RangeExpired = 4,
        ExplicitTermination = 5,
    }

    public enum ProjectileEffectEmissionKind
    {
        EnemyImpact = 1,
        WallImpact = 2,
        RangeExpiry = 3,
        Explosion = 4,
        Termination = 5,
    }

    /// <summary>
    /// Deterministic projectile identity derived from the retained WeaponEffectIdentity authority.
    /// </summary>
    public sealed class ProjectileExecutionIdentity : IEquatable<ProjectileExecutionIdentity>
    {
        public ProjectileExecutionIdentity(WeaponEffectIdentity sourceIdentity)
        {
            SourceIdentity = sourceIdentity ?? throw new ArgumentNullException(nameof(sourceIdentity));
            CanonicalText = "projectile|" + SourceIdentity.ToCanonicalString();
            ProjectileId = WeaponExecutionFingerprint.Compute(CanonicalText);
        }

        public WeaponEffectIdentity SourceIdentity { get; }
        public string CanonicalText { get; }
        public string ProjectileId { get; }
        public long ShotSequence { get { return SourceIdentity.ShotSequence; } }
        public ProjectileOrdinal ShotOrdinal { get { return SourceIdentity.ProjectileOrdinal; } }
        public LifecycleGeneration LifecycleGeneration
        {
            get { return SourceIdentity.LifecycleGeneration; }
        }

        public bool Equals(ProjectileExecutionIdentity other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(ProjectileId, other.ProjectileId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProjectileExecutionIdentity);
        }

        public override int GetHashCode()
        {
            return WeaponExecutionHash.Of(ProjectileId);
        }

        public override string ToString()
        {
            return ProjectileId;
        }
    }

    /// <summary>
    /// Immutable launch context. Randomness is supplied by the shared deterministic-random
    /// authority as a projectile-specific stream; this layer never defines another algorithm.
    /// </summary>
    public sealed class ProjectileLifecycleContext
    {
        public ProjectileLifecycleContext(
            ProjectileExecutionIdentity identity,
            long launchSimulationTick,
            DeterministicRandom random)
        {
            if (launchSimulationTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(launchSimulationTick));
            }
            if (random.AlgorithmVersion != DeterministicRandom.CurrentAlgorithmVersion)
            {
                throw new ArgumentException(
                    "A usable shared deterministic-random stream is required.",
                    nameof(random));
            }

            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            LaunchSimulationTick = launchSimulationTick;
            Random = random;
        }

        public ProjectileExecutionIdentity Identity { get; }
        public long LaunchSimulationTick { get; }
        public DeterministicRandom Random { get; }
        public LifecycleGeneration LifecycleGeneration
        {
            get { return Identity.LifecycleGeneration; }
        }
    }

    /// <summary>
    /// Immutable execution projection derived only from an already validated WeaponBlueprint.
    /// It is not a second public weapon-authoring surface.
    /// </summary>
    public sealed class ProjectileExecutionProfile
    {
        private ProjectileExecutionProfile(WeaponBlueprint blueprint)
        {
            SourceBlueprint = blueprint;
            Projectile = blueprint.Projectile;
            Guidance = blueprint.Guidance;
            Impact = blueprint.Impact;
            Damage = blueprint.Damage;
            Effects = blueprint.Effects;
        }

        public WeaponBlueprint SourceBlueprint { get; }
        public WeaponProjectileSpec Projectile { get; }
        public WeaponGuidanceSpec Guidance { get; }
        public WeaponImpactSpec Impact { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponEffects Effects { get; }

        public static ProjectileExecutionProfile From(WeaponBlueprint blueprint)
        {
            if (blueprint == null)
            {
                throw new ArgumentNullException(nameof(blueprint));
            }
            if (blueprint.Projectile == null)
            {
                throw new ArgumentException(
                    "Projectile execution requires a validated projectile-emitting blueprint.",
                    nameof(blueprint));
            }

            return new ProjectileExecutionProfile(blueprint);
        }
    }

    public sealed class ProjectileLaunchRequest
    {
        public ProjectileLaunchRequest(
            ProjectileLifecycleContext lifecycle,
            ProjectileExecutionProfile profile,
            WeaponVector2 origin,
            WeaponVector2 direction,
            WeaponTargetReference initialTarget)
        {
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Origin = RequireFiniteVector(origin, nameof(origin));
            Direction = RequireDirection(direction, nameof(direction));
            if (initialTarget != null && profile.Guidance.Mode != WeaponGuidanceMode.Homing)
            {
                throw new ArgumentException(
                    "Only homing projectile launches may carry an initial target.",
                    nameof(initialTarget));
            }

            InitialTarget = initialTarget;
        }

        public ProjectileLifecycleContext Lifecycle { get; }
        public ProjectileExecutionProfile Profile { get; }
        public WeaponVector2 Origin { get; }
        public WeaponVector2 Direction { get; }
        public WeaponTargetReference InitialTarget { get; }

        private static WeaponVector2 RequireFiniteVector(WeaponVector2 value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (!value.IsFinite)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
            return value;
        }

        private static WeaponVector2 RequireDirection(WeaponVector2 value, string parameterName)
        {
            WeaponVector2 finite = RequireFiniteVector(value, parameterName);
            WeaponVector2 normalized = finite.Normalized;
            if (normalized.LengthSquared <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
            return normalized;
        }
    }
}

using System;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Domain.Guns.Execution
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

    public enum ProjectileExecutionMode
    {
        Transitional = 1,
        Canonical = 2,
    }

    /// <summary>
    /// Deterministic projectile identity derived from the retained GunEffectIdentity authority.
    /// </summary>
    public sealed class ProjectileExecutionIdentity : IEquatable<ProjectileExecutionIdentity>
    {
        public ProjectileExecutionIdentity(GunEffectIdentity sourceIdentity)
        {
            SourceIdentity = sourceIdentity ?? throw new ArgumentNullException(nameof(sourceIdentity));
            CanonicalText = "projectile|" + SourceIdentity.ToCanonicalString();
            ProjectileId = GunExecutionFingerprint.Compute(CanonicalText);
        }

        public GunEffectIdentity SourceIdentity { get; }
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
            return GunExecutionHash.Of(ProjectileId);
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
    /// Immutable projectile execution payload. Canonical profiles copy final values from an
    /// EffectiveGun and never recover combat values from the authored blueprint after launch.
    /// The blueprint-only factory is retained exclusively for transitional catalogue projections.
    /// </summary>
    public sealed class ProjectileExecutionProfile
    {
        private ProjectileExecutionProfile(
            Gun sourceBlueprint,
            GunDefinitionId definitionId,
            EquipmentInstanceId equipmentInstanceId,
            ProjectileExecutionMode executionMode,
            GunDeliveryType? canonicalDeliveryType,
            ProjectileSettings projectile,
            GunAttackDistance maximumAttackDistance,
            PierceValue pierce,
            RicochetValue ricochet,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunDamageSpec damage,
            GunEffects effects,
            double movementPenaltyPercent)
        {
            SourceBlueprint = sourceBlueprint;
            DefinitionId = definitionId;
            EquipmentInstanceId = equipmentInstanceId;
            ExecutionMode = executionMode;
            CanonicalDeliveryType = canonicalDeliveryType;
            Projectile = projectile;
            MaximumAttackDistance = maximumAttackDistance;
            Pierce = pierce;
            Ricochet = ricochet;
            Guidance = guidance;
            Impact = impact;
            Damage = damage;
            Effects = effects;
            MovementPenaltyPercent = movementPenaltyPercent;
        }

        /// <summary>
        /// Retained only for transitional compatibility and stable authored identity inspection.
        /// Canonical execution values are the copied properties on this profile.
        /// </summary>
        public Gun SourceBlueprint { get; }
        public GunDefinitionId DefinitionId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public ProjectileExecutionMode ExecutionMode { get; }
        public GunDeliveryType? CanonicalDeliveryType { get; }
        public ProjectileSettings Projectile { get; }
        public GunAttackDistance MaximumAttackDistance { get; }
        public PierceValue Pierce { get; }
        public RicochetValue Ricochet { get; }
        public GunGuidanceSpec Guidance { get; }
        public GunImpactSpec Impact { get; }
        public GunDamageSpec Damage { get; }
        public GunEffects Effects { get; }
        public double MovementPenaltyPercent { get; }
        public bool IsCanonical { get { return ExecutionMode == ProjectileExecutionMode.Canonical; } }
        public bool IsTransitional
        {
            get { return ExecutionMode == ProjectileExecutionMode.Transitional; }
        }
        public bool IsCanonicalRocket
        {
            get
            {
                return IsCanonical
                    && CanonicalDeliveryType == GunDeliveryType.Rocket;
            }
        }

        public static ProjectileExecutionProfile From(Gun blueprint)
        {
            if (blueprint == null)
            {
                throw new ArgumentNullException(nameof(blueprint));
            }
            if (!blueprint.IsTransitionalCatalogProjection)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-blueprint-rejected");
            }
            if (blueprint.Projectile == null)
            {
                throw new ArgumentException(
                    "projectile-profile-transitional-projectile-required",
                    nameof(blueprint));
            }

            RicochetValue ricochet = blueprint.Impact.Ricochet != null
                    && blueprint.Impact.Ricochet.FixedPointBudget.HasValue
                ? blueprint.Impact.Ricochet.FixedPointBudget.Value
                : new RicochetValue(0);
            return new ProjectileExecutionProfile(
                blueprint,
                blueprint.DefinitionId,
                null,
                ProjectileExecutionMode.Transitional,
                null,
                blueprint.Projectile,
                GunAttackDistance.Limited(blueprint.Projectile.Range),
                blueprint.Projectile.Pierce,
                ricochet,
                blueprint.Guidance,
                blueprint.Impact,
                blueprint.Damage,
                blueprint.Effects,
                0d);
        }

        public static ProjectileExecutionProfile From(EffectiveGun effectiveGun)
        {
            if (effectiveGun == null)
            {
                throw new ArgumentNullException(nameof(effectiveGun));
            }
            if (!effectiveGun.UsesCanonicalAuthoredDefinition
                || effectiveGun.Blueprint == null
                || effectiveGun.Blueprint.Delivery == null)
            {
                throw new InvalidOperationException(
                    "projectile-profile-effective-canonical-definition-required");
            }
            ShotPattern delivery = effectiveGun.Blueprint.Delivery;
            if (!delivery.IsTravelling || effectiveGun.Projectile == null)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-travelling-delivery-required");
            }
            if (effectiveGun.EffectiveMaximumAttackDistance == null
                || !effectiveGun.EffectiveMaximumAttackDistance.IsLimited)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-finite-range-required");
            }
            if (!effectiveGun.Projectile.Range.Equals(
                    effectiveGun.EffectiveMaximumAttackDistance.Distance))
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-range-conflict");
            }
            if (effectiveGun.Projectile.Pierce != effectiveGun.EffectivePierce)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-pierce-conflict");
            }

            ValidateDeliveryProjection(delivery.Type, effectiveGun.Projectile);
            ValidateRicochetProjection(
                effectiveGun.EffectiveRicochet,
                effectiveGun.Impact);
            ValidateCanonicalRocket(effectiveGun, delivery.Type);

            return new ProjectileExecutionProfile(
                effectiveGun.Blueprint,
                effectiveGun.DefinitionId,
                effectiveGun.EquipmentInstanceId,
                ProjectileExecutionMode.Canonical,
                delivery.Type,
                effectiveGun.Projectile,
                effectiveGun.EffectiveMaximumAttackDistance,
                effectiveGun.EffectivePierce,
                effectiveGun.EffectiveRicochet,
                effectiveGun.Guidance,
                effectiveGun.Impact,
                effectiveGun.Damage,
                effectiveGun.Effects,
                effectiveGun.EffectiveMovementPenaltyPercent);
        }

        private static void ValidateDeliveryProjection(
            GunDeliveryType deliveryType,
            ProjectileSettings projectile)
        {
            GunProjectileKind expectedKind;
            switch (deliveryType)
            {
                case GunDeliveryType.Normal:
                    expectedKind = GunProjectileKind.RegularProjectile;
                    break;
                case GunDeliveryType.Orb:
                    expectedKind = GunProjectileKind.Orb;
                    break;
                case GunDeliveryType.Rocket:
                    expectedKind = GunProjectileKind.Rocket;
                    break;
                default:
                    throw new InvalidOperationException(
                        "projectile-profile-canonical-delivery-not-travelling");
            }
            if (projectile.Kind != expectedKind)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-delivery-kind-conflict");
            }
        }

        private static void ValidateRicochetProjection(
            RicochetValue ricochet,
            GunImpactSpec impact)
        {
            GunRicochetSpec spec = impact == null ? null : impact.Ricochet;
            if (ricochet.Tenths == 0)
            {
                if (spec != null)
                {
                    throw new InvalidOperationException(
                        "projectile-profile-canonical-ricochet-conflict");
                }
                return;
            }
            if (spec == null
                || !spec.FixedPointBudget.HasValue
                || spec.FixedPointBudget.Value != ricochet)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-ricochet-fixed-point-required");
            }
        }

        private static void ValidateCanonicalRocket(
            EffectiveGun gun,
            GunDeliveryType deliveryType)
        {
            if (deliveryType != GunDeliveryType.Rocket)
            {
                return;
            }
            if (gun.Damage == null || gun.Damage.DirectDamage <= 0d)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-positive-damage-required");
            }
            if (gun.EffectivePierce.Tenths <= 0)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-pierce-required");
            }
            if (gun.Effects == null || gun.Effects.Explosion == null)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-explosion-required");
            }
            GunExplosionTriggerSpec trigger = gun.Impact == null
                ? null
                : gun.Impact.ExplosionTrigger;
            if (trigger == null
                || !trigger.OnEnemyImpact
                || !trigger.OnWallImpact)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-contact-triggers-required");
            }
            if (gun.Damage.HasAreaDamage)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-independent-area-damage-rejected");
            }
            if (gun.Projectile.TerminationBehavior
                != GunProjectileTerminationBehavior.StopOnFirstBlockingImpact)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-first-contact-termination-required");
            }
        }
    }

    public sealed class ProjectileLaunchRequest
    {
        public ProjectileLaunchRequest(
            ProjectileLifecycleContext lifecycle,
            ProjectileExecutionProfile profile,
            GunVector2 origin,
            GunVector2 direction,
            GunTargetReference initialTarget)
        {
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Origin = RequireFiniteVector(origin, nameof(origin));
            Direction = RequireDirection(direction, nameof(direction));
            GunEffectIdentity sourceIdentity = lifecycle.Identity.SourceIdentity;
            if (!profile.DefinitionId.Equals(sourceIdentity.GunDefinitionId))
            {
                throw new ArgumentException(
                    "projectile-launch-profile-definition-mismatch",
                    nameof(profile));
            }
            if (profile.IsCanonical
                && (profile.EquipmentInstanceId == null
                    || !profile.EquipmentInstanceId.Equals(
                        sourceIdentity.EquipmentInstanceId)))
            {
                throw new ArgumentException(
                    "projectile-launch-profile-equipment-mismatch",
                    nameof(profile));
            }
            if (initialTarget != null && profile.Guidance.Mode != GunGuidanceMode.Homing)
            {
                throw new ArgumentException(
                    "Only homing projectile launches may carry an initial target.",
                    nameof(initialTarget));
            }

            InitialTarget = initialTarget;
        }

        public ProjectileLifecycleContext Lifecycle { get; }
        public ProjectileExecutionProfile Profile { get; }
        public GunVector2 Origin { get; }
        public GunVector2 Direction { get; }
        public GunTargetReference InitialTarget { get; }

        private static GunVector2 RequireFiniteVector(GunVector2 value, string parameterName)
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

        private static GunVector2 RequireDirection(GunVector2 value, string parameterName)
        {
            GunVector2 finite = RequireFiniteVector(value, parameterName);
            GunVector2 normalized = finite.Normalized;
            if (normalized.LengthSquared <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
            return normalized;
        }
    }
}

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

    public enum ProjectileExecutionMode
    {
        Transitional = 1,
        Canonical = 2,
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
    /// Immutable projectile execution payload. Canonical profiles copy final values from an
    /// EffectiveWeapon and never recover combat values from the authored blueprint after launch.
    /// The blueprint-only factory is retained exclusively for transitional catalogue projections.
    /// </summary>
    public sealed class ProjectileExecutionProfile
    {
        private ProjectileExecutionProfile(
            WeaponBlueprint sourceBlueprint,
            WeaponDefinitionId definitionId,
            EquipmentInstanceId equipmentInstanceId,
            ProjectileExecutionMode executionMode,
            WeaponDeliveryType? canonicalDeliveryType,
            WeaponProjectileSpec projectile,
            WeaponAttackDistance maximumAttackDistance,
            PierceValue pierce,
            RicochetValue ricochet,
            WeaponGuidanceSpec guidance,
            WeaponImpactSpec impact,
            WeaponDamageSpec damage,
            WeaponEffects effects,
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
        public WeaponBlueprint SourceBlueprint { get; }
        public WeaponDefinitionId DefinitionId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public ProjectileExecutionMode ExecutionMode { get; }
        public WeaponDeliveryType? CanonicalDeliveryType { get; }
        public WeaponProjectileSpec Projectile { get; }
        public WeaponAttackDistance MaximumAttackDistance { get; }
        public PierceValue Pierce { get; }
        public RicochetValue Ricochet { get; }
        public WeaponGuidanceSpec Guidance { get; }
        public WeaponImpactSpec Impact { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponEffects Effects { get; }
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
                    && CanonicalDeliveryType == WeaponDeliveryType.Rocket;
            }
        }

        public static ProjectileExecutionProfile From(WeaponBlueprint blueprint)
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
                WeaponAttackDistance.Limited(blueprint.Projectile.Range),
                blueprint.Projectile.Pierce,
                ricochet,
                blueprint.Guidance,
                blueprint.Impact,
                blueprint.Damage,
                blueprint.Effects,
                0d);
        }

        public static ProjectileExecutionProfile From(EffectiveWeapon effectiveWeapon)
        {
            if (effectiveWeapon == null)
            {
                throw new ArgumentNullException(nameof(effectiveWeapon));
            }
            if (!effectiveWeapon.UsesCanonicalAuthoredDefinition
                || effectiveWeapon.Blueprint == null
                || effectiveWeapon.Blueprint.Delivery == null)
            {
                throw new InvalidOperationException(
                    "projectile-profile-effective-canonical-definition-required");
            }
            WeaponDeliverySpec delivery = effectiveWeapon.Blueprint.Delivery;
            if (!delivery.IsTravelling || effectiveWeapon.Projectile == null)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-travelling-delivery-required");
            }
            if (effectiveWeapon.EffectiveMaximumAttackDistance == null
                || !effectiveWeapon.EffectiveMaximumAttackDistance.IsLimited)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-finite-range-required");
            }
            if (!effectiveWeapon.Projectile.Range.Equals(
                    effectiveWeapon.EffectiveMaximumAttackDistance.Distance))
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-range-conflict");
            }
            if (effectiveWeapon.Projectile.Pierce != effectiveWeapon.EffectivePierce)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-pierce-conflict");
            }

            ValidateDeliveryProjection(delivery.Type, effectiveWeapon.Projectile);
            ValidateRicochetProjection(
                effectiveWeapon.EffectiveRicochet,
                effectiveWeapon.Impact);
            ValidateCanonicalRocket(effectiveWeapon, delivery.Type);

            return new ProjectileExecutionProfile(
                effectiveWeapon.Blueprint,
                effectiveWeapon.DefinitionId,
                effectiveWeapon.EquipmentInstanceId,
                ProjectileExecutionMode.Canonical,
                delivery.Type,
                effectiveWeapon.Projectile,
                effectiveWeapon.EffectiveMaximumAttackDistance,
                effectiveWeapon.EffectivePierce,
                effectiveWeapon.EffectiveRicochet,
                effectiveWeapon.Guidance,
                effectiveWeapon.Impact,
                effectiveWeapon.Damage,
                effectiveWeapon.Effects,
                effectiveWeapon.EffectiveMovementPenaltyPercent);
        }

        private static void ValidateDeliveryProjection(
            WeaponDeliveryType deliveryType,
            WeaponProjectileSpec projectile)
        {
            WeaponProjectileKind expectedKind;
            switch (deliveryType)
            {
                case WeaponDeliveryType.Normal:
                    expectedKind = WeaponProjectileKind.RegularProjectile;
                    break;
                case WeaponDeliveryType.Orb:
                    expectedKind = WeaponProjectileKind.Orb;
                    break;
                case WeaponDeliveryType.Rocket:
                    expectedKind = WeaponProjectileKind.Rocket;
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
            WeaponImpactSpec impact)
        {
            WeaponRicochetSpec spec = impact == null ? null : impact.Ricochet;
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
            EffectiveWeapon weapon,
            WeaponDeliveryType deliveryType)
        {
            if (deliveryType != WeaponDeliveryType.Rocket)
            {
                return;
            }
            if (weapon.Damage == null || weapon.Damage.DirectDamage <= 0d)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-positive-damage-required");
            }
            if (weapon.EffectivePierce.Tenths <= 0)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-pierce-required");
            }
            if (weapon.Effects == null || weapon.Effects.Explosion == null)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-explosion-required");
            }
            WeaponExplosionTriggerSpec trigger = weapon.Impact == null
                ? null
                : weapon.Impact.ExplosionTrigger;
            if (trigger == null
                || !trigger.OnEnemyImpact
                || !trigger.OnWallImpact)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-contact-triggers-required");
            }
            if (weapon.Damage.HasAreaDamage)
            {
                throw new InvalidOperationException(
                    "projectile-profile-canonical-rocket-independent-area-damage-rejected");
            }
            if (weapon.Projectile.TerminationBehavior
                != WeaponProjectileTerminationBehavior.StopOnFirstBlockingImpact)
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
            WeaponVector2 origin,
            WeaponVector2 direction,
            WeaponTargetReference initialTarget)
        {
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Origin = RequireFiniteVector(origin, nameof(origin));
            Direction = RequireDirection(direction, nameof(direction));
            WeaponEffectIdentity sourceIdentity = lifecycle.Identity.SourceIdentity;
            if (!profile.DefinitionId.Equals(sourceIdentity.WeaponDefinitionId))
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

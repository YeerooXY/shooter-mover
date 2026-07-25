using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum AcceptedEmissionRuntimeAdapterStatus
    {
        Adapted = 1,
        InvalidInput = 2,
        IdentityMismatch = 3,
        UnsupportedFireMode = 4,
        UnsupportedShotPattern = 5,
        UnsupportedProjectile = 6,
        UnsupportedGuidance = 7,
        UnsupportedImpact = 8,
        UnsupportedEffects = 9,
        FractionalPierceUnsupported = 10,
        UnknownBehavior = 11,
        BehaviorRejected = 12,
        InvalidEffectBatch = 13,
        NumericalFailure = 14,
        InvalidProjectileProfile = 15,
        InvalidProjectileLaunch = 16,
    }

    /// <summary>
    /// One scheduler-authorized canonical projectile launch and its already-created initial
    /// lifecycle state. The state is created at composition time so final speed, range, Pierce,
    /// guidance, impact, damage, effects and delivery identity cannot be reconstructed later.
    /// </summary>
    public sealed class AcceptedProjectileLaunch
    {
        public AcceptedProjectileLaunch(ProjectileLaunchRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            InitialState = ProjectileLifecycleState.Launch(request);
            if (InitialState.Profile == null
                || !ReferenceEquals(InitialState.Profile, request.Profile)
                || !InitialState.Lifecycle.Identity.Equals(request.Lifecycle.Identity))
            {
                throw new InvalidOperationException(
                    "weapon-runtime-canonical-projectile-state-invalid");
            }
        }

        public ProjectileLaunchRequest Request { get; }
        public ProjectileLifecycleState InitialState { get; }
        public ProjectileExecutionProfile Profile { get { return Request.Profile; } }
        public ProjectileExecutionIdentity Identity { get { return Request.Lifecycle.Identity; } }
    }

    public sealed class AcceptedEmissionRuntimeAdapterResult
    {
        private readonly ReadOnlyCollection<AcceptedProjectileLaunch> projectileLaunches;

        private AcceptedEmissionRuntimeAdapterResult(
            AcceptedEmissionRuntimeAdapterStatus status,
            string rejectionCode,
            WeaponRuntimeFiringProfile profile,
            WeaponEffectBatch batch,
            ProjectileExecutionProfile projectileProfile,
            IList<AcceptedProjectileLaunch> canonicalProjectileLaunches)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Profile = profile;
            Batch = batch;
            ProjectileProfile = projectileProfile;
            projectileLaunches = new ReadOnlyCollection<AcceptedProjectileLaunch>(
                canonicalProjectileLaunches == null
                    ? new List<AcceptedProjectileLaunch>()
                    : new List<AcceptedProjectileLaunch>(canonicalProjectileLaunches));
        }

        public AcceptedEmissionRuntimeAdapterStatus Status { get; }
        public string RejectionCode { get; }

        /// <summary>
        /// Retained scalar compatibility projection used by the existing inventory delivery
        /// envelope. Canonical projectile execution must use ProjectileProfile/ProjectileLaunches.
        /// </summary>
        public WeaponRuntimeFiringProfile Profile { get; }

        /// <summary>
        /// Existing immutable delivery envelope. Canonical batches contain only
        /// CanonicalProjectileLaunchEffect descriptions with baked launch requests and states.
        /// </summary>
        public WeaponEffectBatch Batch { get; }

        public ProjectileExecutionProfile ProjectileProfile { get; }
        public IReadOnlyList<AcceptedProjectileLaunch> ProjectileLaunches
        {
            get { return projectileLaunches; }
        }
        public bool IsCanonicalProjectile
        {
            get
            {
                return Status == AcceptedEmissionRuntimeAdapterStatus.Adapted
                    && Profile != null
                    && Batch != null
                    && ProjectileProfile != null
                    && ProjectileProfile.IsCanonical
                    && projectileLaunches.Count > 0;
            }
        }
        public bool IsTransitionalBatch
        {
            get
            {
                return Status == AcceptedEmissionRuntimeAdapterStatus.Adapted
                    && Profile != null
                    && Batch != null
                    && ProjectileProfile == null
                    && projectileLaunches.Count == 0;
            }
        }
        public bool Succeeded
        {
            get
            {
                return Status == AcceptedEmissionRuntimeAdapterStatus.Adapted
                    && (IsCanonicalProjectile || IsTransitionalBatch);
            }
        }

        public static AcceptedEmissionRuntimeAdapterResult Adapted(
            WeaponRuntimeFiringProfile profile,
            WeaponEffectBatch batch)
        {
            return new AcceptedEmissionRuntimeAdapterResult(
                AcceptedEmissionRuntimeAdapterStatus.Adapted,
                string.Empty,
                profile ?? throw new ArgumentNullException(nameof(profile)),
                batch ?? throw new ArgumentNullException(nameof(batch)),
                null,
                null);
        }

        public static AcceptedEmissionRuntimeAdapterResult CanonicalProjectile(
            ProjectileExecutionProfile projectileProfile,
            IList<AcceptedProjectileLaunch> launches)
        {
            if (projectileProfile == null || !projectileProfile.IsCanonical)
            {
                throw new ArgumentException(
                    "A canonical projectile profile is required.",
                    nameof(projectileProfile));
            }
            if (launches == null || launches.Count < 1)
            {
                throw new ArgumentException(
                    "At least one canonical projectile launch is required.",
                    nameof(launches));
            }

            var effects = new List<IWeaponEffectDescription>(launches.Count);
            for (int index = 0; index < launches.Count; index++)
            {
                AcceptedProjectileLaunch launch = launches[index];
                if (launch == null
                    || !ReferenceEquals(launch.Profile, projectileProfile)
                    || launch.Identity == null
                    || launch.Identity.SourceIdentity.ProjectileOrdinal.Value != index)
                {
                    throw new ArgumentException(
                        "Canonical projectile launches must use one shared profile and ordered unique ordinals.",
                        nameof(launches));
                }
                effects.Add(new CanonicalProjectileLaunchEffect(
                    launch.Request,
                    launch.InitialState));
            }

            WeaponEffectBatch batch = new WeaponEffectBatch(effects);
            WeaponExplosionEffect explosion = projectileProfile.Effects.Explosion;
            WeaponDamageOverTimeStats dot = projectileProfile.Damage.DamageOverTime;
            WeaponChainArcEffect chain = projectileProfile.Effects.ChainArc;
            WeaponRuntimeFiringProfile compatibilityProfile =
                new WeaponRuntimeFiringProfile(
                    projectileProfile.DefinitionId,
                    projectileProfile.IsCanonicalRocket
                        ? BuiltInWeaponBehaviorIds.Explosive
                        : BuiltInWeaponBehaviorIds.Projectile,
                    0,
                    launches.Count,
                    0d,
                    projectileProfile.Projectile.Speed,
                    projectileProfile.Projectile.Range,
                    projectileProfile.Damage.DirectDamage,
                    projectileProfile.Pierce.GuaranteedHits,
                    0d,
                    explosion == null ? 0d : explosion.Radius,
                    dot == null ? 0d : dot.DamagePerSecond,
                    dot == null ? 0d : dot.DurationSeconds,
                    0d,
                    0d,
                    chain == null ? 0 : chain.MaximumTargets,
                    chain == null ? 0d : chain.AcquisitionRange,
                    projectileProfile.Damage.Knockback,
                    WeaponDamageCategoryConversion.ToCatalogValue(
                        projectileProfile.Damage.Category));

            return CanonicalProjectile(
                projectileProfile,
                launches,
                compatibilityProfile,
                batch);
        }

        public static AcceptedEmissionRuntimeAdapterResult CanonicalProjectile(
            ProjectileExecutionProfile projectileProfile,
            IList<AcceptedProjectileLaunch> launches,
            WeaponRuntimeFiringProfile compatibilityProfile,
            WeaponEffectBatch batch)
        {
            if (projectileProfile == null || !projectileProfile.IsCanonical)
            {
                throw new ArgumentException(
                    "A canonical projectile profile is required.",
                    nameof(projectileProfile));
            }
            if (compatibilityProfile == null)
            {
                throw new ArgumentNullException(nameof(compatibilityProfile));
            }
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }
            if (launches == null || launches.Count < 1 || batch.EffectCount != launches.Count)
            {
                throw new ArgumentException(
                    "Canonical launch and delivery counts must match and be positive.",
                    nameof(launches));
            }
            for (int index = 0; index < launches.Count; index++)
            {
                AcceptedProjectileLaunch launch = launches[index];
                CanonicalProjectileLaunchEffect effect =
                    batch.Effects[index] as CanonicalProjectileLaunchEffect;
                if (launch == null
                    || !ReferenceEquals(launch.Profile, projectileProfile)
                    || launch.Identity == null
                    || launch.Identity.SourceIdentity.ProjectileOrdinal.Value != index
                    || effect == null
                    || !ReferenceEquals(effect.LaunchRequest, launch.Request)
                    || !ReferenceEquals(effect.InitialState, launch.InitialState))
                {
                    throw new ArgumentException(
                        "Canonical projectile launches must use one shared profile and ordered retained launch effects.",
                        nameof(launches));
                }
            }

            return new AcceptedEmissionRuntimeAdapterResult(
                AcceptedEmissionRuntimeAdapterStatus.Adapted,
                string.Empty,
                compatibilityProfile,
                batch,
                projectileProfile,
                launches);
        }

        public static AcceptedEmissionRuntimeAdapterResult Reject(
            AcceptedEmissionRuntimeAdapterStatus status,
            string rejectionCode)
        {
            if (status == AcceptedEmissionRuntimeAdapterStatus.Adapted)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (string.IsNullOrWhiteSpace(rejectionCode))
            {
                throw new ArgumentException(
                    "A stable adapter rejection code is required.",
                    nameof(rejectionCode));
            }

            return new AcceptedEmissionRuntimeAdapterResult(
                status,
                rejectionCode,
                null,
                null,
                null,
                null);
        }
    }
}

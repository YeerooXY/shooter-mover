using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.ContentPackages.Weapons.Shared.Presentation;
using ShooterMover.ContentPackages.Weapons.Stage1;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime
{
    public enum RicochetProjectileTerminationReason
    {
        None = 0,
        ThirdWallCollision = 1,
        LifetimeExpired = 2,
        ConfirmedTargetHit = 3,
        CollisionWithoutConfirmedTarget = 4,
        Cancelled = 5,
    }

    public enum RicochetWallContactResultKind
    {
        Reflected = 1,
        GrazingIgnored = 2,
        TerminatedBounceLimit = 3,
        AlreadyTerminated = 4,
    }

    /// <summary>
    /// Small immutable engine-independent vector used by the deterministic policy.
    /// </summary>
    public struct RicochetVector2 : IEquatable<RicochetVector2>
    {
        public RicochetVector2(double x, double y)
        {
            RequireFinite(x, nameof(x));
            RequireFinite(y, nameof(y));
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public double LengthSquared
        {
            get { return (X * X) + (Y * Y); }
        }

        public RicochetVector2 Normalized()
        {
            double squared = LengthSquared;
            if (squared <= 0d || double.IsNaN(squared) || double.IsInfinity(squared))
            {
                throw new InvalidOperationException("A zero or non-finite vector cannot be normalized.");
            }

            double inverseLength = 1d / Math.Sqrt(squared);
            return new RicochetVector2(X * inverseLength, Y * inverseLength);
        }

        public double Dot(RicochetVector2 other)
        {
            return (X * other.X) + (Y * other.Y);
        }

        public bool Equals(RicochetVector2 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is RicochetVector2 && Equals((RicochetVector2)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return "("
                + X.ToString("R", CultureInfo.InvariantCulture)
                + ","
                + Y.ToString("R", CultureInfo.InvariantCulture)
                + ")";
        }

        internal static RicochetVector2 Add(RicochetVector2 left, RicochetVector2 right)
        {
            return new RicochetVector2(left.X + right.X, left.Y + right.Y);
        }

        internal static RicochetVector2 Scale(RicochetVector2 value, double scalar)
        {
            return new RicochetVector2(value.X * scalar, value.Y * scalar);
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Vector components must be finite.");
            }
        }
    }

    public sealed class RicochetWallContactResult
    {
        internal RicochetWallContactResult(
            RicochetWallContactResultKind kind,
            RicochetVector2 direction,
            int wallBounceCount,
            RicochetProjectileTerminationReason terminationReason)
        {
            Kind = kind;
            Direction = direction;
            WallBounceCount = wallBounceCount;
            TerminationReason = terminationReason;
        }

        public RicochetWallContactResultKind Kind { get; }

        public RicochetVector2 Direction { get; }

        public int WallBounceCount { get; }

        public RicochetProjectileTerminationReason TerminationReason { get; }
    }

    /// <summary>
    /// Engine-independent lifetime and valid-wall bounce state. Contact normal input is
    /// canonicalized, so a simultaneous corner produces the same result regardless of
    /// callback normal order.
    /// </summary>
    public sealed class RicochetProjectilePolicy
    {
        public const int MaximumWallBounces = 2;
        public const double MinimumLifetimeSeconds = 0.01d;
        public const double MaximumLifetimeSeconds = 30d;

        private const double OpposingContactEpsilon = 0.000000001d;
        private const double DuplicateNormalDotThreshold = 0.999999999d;
        private const double DegenerateVectorThreshold = 0.000000000001d;

        private double remainingLifetimeSeconds;
        private int wallBounceCount;
        private bool isTerminated;
        private RicochetProjectileTerminationReason terminationReason;

        public RicochetProjectilePolicy(double lifetimeSeconds)
        {
            if (!IsValidLifetime(lifetimeSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifetimeSeconds),
                    "Ricochet lifetime must be finite and within the projectile bounds.");
            }

            remainingLifetimeSeconds = lifetimeSeconds;
            terminationReason = RicochetProjectileTerminationReason.None;
        }

        public double RemainingLifetimeSeconds
        {
            get { return remainingLifetimeSeconds; }
        }

        public int WallBounceCount
        {
            get { return wallBounceCount; }
        }

        public bool IsTerminated
        {
            get { return isTerminated; }
        }

        public RicochetProjectileTerminationReason TerminationReason
        {
            get { return terminationReason; }
        }

        public bool AdvanceLifetime(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds)
                || double.IsInfinity(deltaSeconds)
                || deltaSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds),
                    "Lifetime advancement must be finite and non-negative.");
            }

            if (isTerminated)
            {
                return false;
            }

            remainingLifetimeSeconds -= deltaSeconds;
            if (remainingLifetimeSeconds > 0d)
            {
                return true;
            }

            remainingLifetimeSeconds = 0d;
            Terminate(RicochetProjectileTerminationReason.LifetimeExpired);
            return false;
        }

        public RicochetWallContactResult ResolveWallContact(
            RicochetVector2 incomingDirection,
            IEnumerable<RicochetVector2> contactNormals)
        {
            RicochetVector2 normalizedIncoming = incomingDirection.Normalized();
            if (isTerminated)
            {
                return new RicochetWallContactResult(
                    RicochetWallContactResultKind.AlreadyTerminated,
                    normalizedIncoming,
                    wallBounceCount,
                    terminationReason);
            }

            if (wallBounceCount >= MaximumWallBounces)
            {
                Terminate(RicochetProjectileTerminationReason.ThirdWallCollision);
                return new RicochetWallContactResult(
                    RicochetWallContactResultKind.TerminatedBounceLimit,
                    normalizedIncoming,
                    wallBounceCount,
                    terminationReason);
            }

            List<RicochetVector2> usableNormals = CopyCanonicalNormals(
                normalizedIncoming,
                contactNormals);
            if (usableNormals.Count == 0)
            {
                return new RicochetWallContactResult(
                    RicochetWallContactResultKind.GrazingIgnored,
                    normalizedIncoming,
                    wallBounceCount,
                    RicochetProjectileTerminationReason.None);
            }

            RicochetVector2 combinedNormal = CombineNormals(
                normalizedIncoming,
                usableNormals);
            double projection = normalizedIncoming.Dot(combinedNormal);
            RicochetVector2 reflected = RicochetVector2.Add(
                    normalizedIncoming,
                    RicochetVector2.Scale(combinedNormal, -2d * projection))
                .Normalized();

            wallBounceCount++;
            return new RicochetWallContactResult(
                RicochetWallContactResultKind.Reflected,
                reflected,
                wallBounceCount,
                RicochetProjectileTerminationReason.None);
        }

        public void Terminate(RicochetProjectileTerminationReason reason)
        {
            if (isTerminated)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(RicochetProjectileTerminationReason), reason)
                || reason == RicochetProjectileTerminationReason.None)
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }

            isTerminated = true;
            terminationReason = reason;
            remainingLifetimeSeconds = 0d;
        }

        public static bool IsValidLifetime(double lifetimeSeconds)
        {
            return !double.IsNaN(lifetimeSeconds)
                && !double.IsInfinity(lifetimeSeconds)
                && lifetimeSeconds >= MinimumLifetimeSeconds
                && lifetimeSeconds <= MaximumLifetimeSeconds;
        }

        private static List<RicochetVector2> CopyCanonicalNormals(
            RicochetVector2 incomingDirection,
            IEnumerable<RicochetVector2> contactNormals)
        {
            if (contactNormals == null)
            {
                throw new ArgumentNullException(nameof(contactNormals));
            }

            List<RicochetVector2> result = new List<RicochetVector2>();
            foreach (RicochetVector2 candidate in contactNormals)
            {
                if (candidate.LengthSquared <= DegenerateVectorThreshold)
                {
                    continue;
                }

                RicochetVector2 normalized = candidate.Normalized();
                if (incomingDirection.Dot(normalized) >= -OpposingContactEpsilon)
                {
                    continue;
                }

                bool duplicate = false;
                for (int index = 0; index < result.Count; index++)
                {
                    if (result[index].Dot(normalized) >= DuplicateNormalDotThreshold)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    result.Add(normalized);
                }
            }

            result.Sort(CompareVectors);
            return result;
        }

        private static RicochetVector2 CombineNormals(
            RicochetVector2 incomingDirection,
            IReadOnlyList<RicochetVector2> normals)
        {
            RicochetVector2 sum = new RicochetVector2(0d, 0d);
            for (int index = 0; index < normals.Count; index++)
            {
                sum = RicochetVector2.Add(sum, normals[index]);
            }

            if (sum.LengthSquared > DegenerateVectorThreshold)
            {
                return sum.Normalized();
            }

            RicochetVector2 selected = normals[0];
            double selectedProjection = incomingDirection.Dot(selected);
            for (int index = 1; index < normals.Count; index++)
            {
                double projection = incomingDirection.Dot(normals[index]);
                if (projection < selectedProjection)
                {
                    selected = normals[index];
                    selectedProjection = projection;
                }
            }

            return selected;
        }

        private static int CompareVectors(RicochetVector2 left, RicochetVector2 right)
        {
            int xComparison = left.X.CompareTo(right.X);
            return xComparison != 0 ? xComparison : left.Y.CompareTo(right.Y);
        }
    }

    /// <summary>
    /// Immutable numeric projectile tuning. The two-bounce topology is deliberately
    /// absent so empowerment cannot alter it.
    /// </summary>
    public sealed class RicochetGunTuning
    {
        public RicochetGunTuning(
            double projectileSpeed,
            double projectileLifetimeSeconds,
            double projectileRadius,
            CombatChannel channel)
        {
            if (!IsFinite(projectileSpeed)
                || projectileSpeed < RicochetGunPackage.MinimumProjectileSpeed
                || projectileSpeed > RicochetGunPackage.MaximumProjectileSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileSpeed));
            }

            if (!RicochetProjectilePolicy.IsValidLifetime(projectileLifetimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(projectileLifetimeSeconds));
            }

            if (!IsFinite(projectileRadius)
                || projectileRadius < RicochetGunPackage.MinimumProjectileRadius
                || projectileRadius > RicochetGunPackage.MaximumProjectileRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileRadius));
            }

            if (!Enum.IsDefined(typeof(CombatChannel), channel)
                || channel == CombatChannel.System)
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            ProjectileSpeed = projectileSpeed;
            ProjectileLifetimeSeconds = projectileLifetimeSeconds;
            ProjectileRadius = projectileRadius;
            Channel = channel;
        }

        public double ProjectileSpeed { get; }

        public double ProjectileLifetimeSeconds { get; }

        public double ProjectileRadius { get; }

        public CombatChannel Channel { get; }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public sealed class RicochetProjectileExecutionOperation :
        IWeaponFireExecutionOperation
    {
        public RicochetProjectileExecutionOperation(
            StableId operationKindId,
            StableId operationId,
            double projectileSpeed,
            double projectileLifetimeSeconds,
            double projectileRadius,
            CombatChannel channel)
        {
            if (operationKindId == null)
            {
                throw new ArgumentNullException(nameof(operationKindId));
            }

            if (operationId == null)
            {
                throw new ArgumentNullException(nameof(operationId));
            }

            OperationKindId = operationKindId;
            OperationId = operationId;
            ProjectileSpeed = projectileSpeed;
            ProjectileLifetimeSeconds = projectileLifetimeSeconds;
            ProjectileRadius = projectileRadius;
            Channel = channel;
        }

        public StableId OperationKindId { get; }

        public StableId OperationId { get; }

        public double ProjectileSpeed { get; }

        public double ProjectileLifetimeSeconds { get; }

        public double ProjectileRadius { get; }

        public CombatChannel Channel { get; }
    }

    /// <summary>
    /// Pure CB-004 behavior module. CB-003 power fallback is represented only by
    /// WeaponBehaviorInput.IsEmpowered; this module never owns a resource bank.
    /// </summary>
    public sealed class RicochetGunBehaviorModule : IWeaponBehaviorModule
    {
        private readonly RicochetGunTuning normalTuning;
        private readonly RicochetGunTuning empoweredTuning;

        public RicochetGunBehaviorModule(
            RicochetGunTuning normalTuning,
            RicochetGunTuning empoweredTuning)
        {
            this.normalTuning = normalTuning
                ?? throw new ArgumentNullException(nameof(normalTuning));
            this.empoweredTuning = empoweredTuning
                ?? throw new ArgumentNullException(nameof(empoweredTuning));
        }

        public StableId ModuleId
        {
            get { return RicochetGunPackage.ModuleId; }
        }

        public WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.WeaponId != RicochetGunPackage.WeaponId)
            {
                throw new InvalidOperationException(
                    "RicochetGunBehaviorModule only accepts weapon.ricochet-gun.");
            }

            RicochetGunTuning tuning = input.IsEmpowered
                ? empoweredTuning
                : normalTuning;
            return new WeaponBehaviorModulePlan(
                ModuleId,
                new RicochetProjectileExecutionOperation(
                    RicochetGunPackage.OperationKindId,
                    CreateOperationId(input, tuning),
                    tuning.ProjectileSpeed,
                    tuning.ProjectileLifetimeSeconds,
                    tuning.ProjectileRadius,
                    tuning.Channel));
        }

        private static StableId CreateOperationId(
            WeaponBehaviorInput input,
            RicochetGunTuning tuning)
        {
            string canonical = string.Join(
                "\n",
                new[]
                {
                    "combat_event_id=" + input.CombatEventId,
                    "weapon_id=" + input.WeaponId,
                    "mount_id=" + input.MountId,
                    "simulation_step=" + input.SimulationStep.ToString(CultureInfo.InvariantCulture),
                    "is_empowered=" + (input.IsEmpowered ? "true" : "false"),
                    "projectile_speed=" + tuning.ProjectileSpeed.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_lifetime_seconds=" + tuning.ProjectileLifetimeSeconds.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_radius=" + tuning.ProjectileRadius.ToString("R", CultureInfo.InvariantCulture),
                    "channel=" + ((int)tuning.Channel).ToString(CultureInfo.InvariantCulture),
                });

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                StringBuilder builder = new StringBuilder(64);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return StableId.Create(
                    "operation",
                    "ricochet-" + builder.ToString());
            }
        }
    }

    /// <summary>
    /// Package identity, numeric defaults, and WP-001 descriptor projection. It writes
    /// no generated registry output and exposes no tunable bounce-count value.
    /// </summary>
    public static class RicochetGunPackage
    {
        public const double NormalProjectileSpeed = 15d;
        public const double EmpoweredProjectileSpeed = 18d;
        public const double NormalProjectileLifetimeSeconds = 8d;
        public const double EmpoweredProjectileLifetimeSeconds = 10d;
        public const double NormalProjectileRadius = 0.12d;
        public const double EmpoweredProjectileRadius = 0.14d;
        public const double NormalDamageCoefficient = 9d;
        public const double EmpoweredDamageCoefficient = 13d;
        public const double MinimumProjectileSpeed = 0.01d;
        public const double MaximumProjectileSpeed = 250d;
        public const double MinimumProjectileRadius = 0.01d;
        public const double MaximumProjectileRadius = 10d;

        private static readonly StableId WeaponIdValue =
            StableId.Parse("weapon.ricochet-gun");
        private static readonly StableId ModuleIdValue =
            StableId.Parse("module.weapon-ricochet-projectile");
        private static readonly StableId OperationKindIdValue =
            StableId.Parse("operation-kind.ricochet-projectile-2d");

        public static StableId WeaponId
        {
            get { return WeaponIdValue; }
        }

        public static StableId ModuleId
        {
            get { return ModuleIdValue; }
        }

        public static StableId OperationKindId
        {
            get { return OperationKindIdValue; }
        }

        public static int MaximumWallBounces
        {
            get { return RicochetProjectilePolicy.MaximumWallBounces; }
        }

        public static RicochetGunTuning CreateNormalTuning()
        {
            return new RicochetGunTuning(
                NormalProjectileSpeed,
                NormalProjectileLifetimeSeconds,
                NormalProjectileRadius,
                CombatChannel.Kinetic);
        }

        public static RicochetGunTuning CreateEmpoweredTuning()
        {
            return new RicochetGunTuning(
                EmpoweredProjectileSpeed,
                EmpoweredProjectileLifetimeSeconds,
                EmpoweredProjectileRadius,
                CombatChannel.Kinetic);
        }

        public static RicochetGunBehaviorModule CreateBehaviorModule()
        {
            return new RicochetGunBehaviorModule(
                CreateNormalTuning(),
                CreateEmpoweredTuning());
        }

        public static WeaponRuntimeProfile CreateRuntimeProfile(bool empowered)
        {
            StableId[] moduleIds = { ModuleId };
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                StableId.Parse(
                    empowered
                        ? "weapon-profile.ricochet-gun-empowered"
                        : "weapon-profile.ricochet-gun-normal"),
                empowered ? 0.45d : 0.6d,
                1,
                0d,
                empowered ? 0.12d : 0.18d,
                WeaponCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                true,
                10d,
                2d,
                0d,
                moduleIds,
                moduleIds,
                2);
        }

        public static Stage1WeaponPackageDescriptor CreateDescriptor()
        {
            ContentReference moduleReference = ContentReference.Create(
                ModuleId,
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                WeaponId,
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion,
                StableId.Parse("provenance.ricochet-gun-stage1"),
                false,
                moduleReference);
            Stage1WeaponBehaviorTopology topology =
                Stage1WeaponBehaviorTopology.Create(
                    Stage1WeaponBehaviorKind.RicochetProjectile,
                    0,
                    RicochetProjectilePolicy.MaximumWallBounces,
                    0,
                    false);

            return Stage1WeaponPackageDescriptor.Create(
                Stage1WeaponPackageDescriptor.CurrentDescriptorVersion,
                content,
                false,
                CreateFireProfile(false, topology),
                CreateFireProfile(true, topology));
        }

        private static Stage1WeaponFireProfile CreateFireProfile(
            bool empowered,
            Stage1WeaponBehaviorTopology topology)
        {
            Stage1WeaponNumericCoefficient[] coefficients =
            {
                Stage1WeaponNumericCoefficient.Create(
                    Stage1WeaponNumericCoefficientKind.Damage,
                    empowered ? EmpoweredDamageCoefficient : NormalDamageCoefficient),
                Stage1WeaponNumericCoefficient.Create(
                    Stage1WeaponNumericCoefficientKind.ProjectileSpeed,
                    empowered ? EmpoweredProjectileSpeed : NormalProjectileSpeed),
                Stage1WeaponNumericCoefficient.Create(
                    Stage1WeaponNumericCoefficientKind.ProjectileLifetimeSeconds,
                    empowered
                        ? EmpoweredProjectileLifetimeSeconds
                        : NormalProjectileLifetimeSeconds),
                Stage1WeaponNumericCoefficient.Create(
                    Stage1WeaponNumericCoefficientKind.ProjectileRadius,
                    empowered ? EmpoweredProjectileRadius : NormalProjectileRadius),
            };

            return Stage1WeaponFireProfile.Create(
                CreateRuntimeProfile(empowered),
                topology,
                false,
                coefficients);
        }
    }

    /// <summary>
    /// Explicit CB-009 handler for the package-owned ricochet operation kind.
    /// </summary>
    public sealed class RicochetProjectileExecutionAdapter :
        IWeaponFireExecutionOperation2DHandler,
        IDisposable
    {
        private readonly RicochetProjectile2D projectilePrefab;
        private readonly CombatHit2DAdapter hitAdapter;
        private readonly Collider2D[] ownerColliders;
        private readonly Transform projectileParent;
        private readonly bool enableHitPresentation;
        private readonly float hitPresentationLifetimeSeconds;
        private readonly List<RicochetProjectile2D> activeProjectiles =
            new List<RicochetProjectile2D>();

        private bool isDisposed;
        private RicochetProjectile2D lastSpawnedProjectile;

        public RicochetProjectileExecutionAdapter(
            RicochetProjectile2D projectilePrefab,
            CombatHit2DAdapter hitAdapter)
            : this(
                projectilePrefab,
                hitAdapter,
                null,
                null,
                true,
                TemporaryHitPresentation.DefaultLifetimeSeconds)
        {
        }

        public RicochetProjectileExecutionAdapter(
            RicochetProjectile2D projectilePrefab,
            CombatHit2DAdapter hitAdapter,
            IEnumerable<Collider2D> ownerColliders,
            Transform projectileParent,
            bool enableHitPresentation,
            float hitPresentationLifetimeSeconds)
        {
            this.projectilePrefab = projectilePrefab
                ?? throw new ArgumentNullException(nameof(projectilePrefab));
            this.hitAdapter = hitAdapter
                ?? throw new ArgumentNullException(nameof(hitAdapter));
            if (enableHitPresentation
                && !TemporaryHitPresentation.IsValidLifetime(
                    hitPresentationLifetimeSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hitPresentationLifetimeSeconds));
            }

            this.ownerColliders = CopyOwnerColliders(ownerColliders);
            this.projectileParent = projectileParent;
            this.enableHitPresentation = enableHitPresentation;
            this.hitPresentationLifetimeSeconds = hitPresentationLifetimeSeconds;
        }

        public StableId OperationKindId
        {
            get { return RicochetGunPackage.OperationKindId; }
        }

        public bool IsDisposed
        {
            get { return isDisposed; }
        }

        public int ActiveProjectileCount
        {
            get
            {
                PruneDestroyedProjectiles();
                return activeProjectiles.Count;
            }
        }

        public RicochetProjectile2D LastSpawnedProjectile
        {
            get
            {
                if (lastSpawnedProjectile == null)
                {
                    lastSpawnedProjectile = null;
                }

                return lastSpawnedProjectile;
            }
        }

        public bool TryExecute(
            WeaponFireExecutionOperationEntry operation,
            WeaponMount2DExecutionContext context)
        {
            if (isDisposed || !TryValidateEnvelope(operation, context))
            {
                return false;
            }

            RicochetProjectileExecutionOperation payload =
                operation.Operation as RicochetProjectileExecutionOperation;
            if (payload == null
                || payload.OperationKindId != operation.OperationKindId
                || payload.OperationId != operation.OperationId)
            {
                return false;
            }

            float speed;
            float lifetimeSeconds;
            float radius;
            if (!TryConvertFinite(payload.ProjectileSpeed, out speed)
                || !TryConvertFinite(
                    payload.ProjectileLifetimeSeconds,
                    out lifetimeSeconds)
                || !TryConvertFinite(payload.ProjectileRadius, out radius)
                || !RicochetProjectile2D.IsValidSpeed(speed)
                || !RicochetProjectilePolicy.IsValidLifetime(lifetimeSeconds)
                || !RicochetProjectile2D.IsValidRadius(radius)
                || !Enum.IsDefined(typeof(CombatChannel), payload.Channel)
                || payload.Channel == CombatChannel.System)
            {
                return false;
            }

            StableId hitEventId;
            if (!TryCreateHitEventId(context, out hitEventId))
            {
                return false;
            }

            RicochetProjectile2D instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(
                    projectilePrefab,
                    projectileParent);
                instance.gameObject.name = "RicochetProjectile2D";
                if (!instance.gameObject.activeSelf)
                {
                    instance.gameObject.SetActive(true);
                }

                instance.Completed += HandleProjectileCompleted;
                if (!instance.TryInitialize(
                    hitEventId,
                    context.Origin,
                    context.Direction,
                    speed,
                    lifetimeSeconds,
                    radius,
                    payload.Channel,
                    hitAdapter,
                    ownerColliders,
                    enableHitPresentation,
                    hitPresentationLifetimeSeconds))
                {
                    instance.Completed -= HandleProjectileCompleted;
                    UnityEngine.Object.Destroy(instance.gameObject);
                    return false;
                }

                activeProjectiles.Add(instance);
                lastSpawnedProjectile = instance;
                return true;
            }
            catch (Exception)
            {
                if (instance != null)
                {
                    instance.Completed -= HandleProjectileCompleted;
                    UnityEngine.Object.Destroy(instance.gameObject);
                }

                return false;
            }
        }

        public void ResetSession()
        {
            RicochetProjectile2D[] snapshot = activeProjectiles.ToArray();
            activeProjectiles.Clear();
            lastSpawnedProjectile = null;

            for (int index = 0; index < snapshot.Length; index++)
            {
                RicochetProjectile2D projectile = snapshot[index];
                if (projectile == null)
                {
                    continue;
                }

                projectile.Completed -= HandleProjectileCompleted;
                projectile.Cancel();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            ResetSession();
            isDisposed = true;
        }

        private bool TryValidateEnvelope(
            WeaponFireExecutionOperationEntry operation,
            WeaponMount2DExecutionContext context)
        {
            return operation != null
                && operation.Operation != null
                && operation.OperationKindId == OperationKindId
                && operation.OperationId != null
                && context != null
                && context.PhysicsScene.IsValid()
                && context.SourceId != null
                && context.SourceId == hitAdapter.SourceId
                && context.CombatEventId != null
                && context.WeaponId == RicochetGunPackage.WeaponId
                && context.MountId != null
                && context.PlanId != null
                && context.PlanOperationIndex >= 0
                && IsFinite(context.Origin.x)
                && IsFinite(context.Origin.y)
                && IsFinite(context.Direction.x)
                && IsFinite(context.Direction.y)
                && context.Direction.sqrMagnitude > 0f;
        }

        private static bool TryCreateHitEventId(
            WeaponMount2DExecutionContext context,
            out StableId hitEventId)
        {
            hitEventId = null;
            string value = context.PlanId.Value
                + "-"
                + context.PlanOperationIndex.ToString(CultureInfo.InvariantCulture);
            if (value.Length > StableId.MaxValueLength)
            {
                return false;
            }

            try
            {
                hitEventId = StableId.Create("ricochet-hit", value);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void HandleProjectileCompleted(RicochetProjectile2D projectile)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.Completed -= HandleProjectileCompleted;
            activeProjectiles.Remove(projectile);
            if (lastSpawnedProjectile == projectile)
            {
                lastSpawnedProjectile = null;
            }
        }

        private void PruneDestroyedProjectiles()
        {
            for (int index = activeProjectiles.Count - 1; index >= 0; index--)
            {
                if (activeProjectiles[index] == null)
                {
                    activeProjectiles.RemoveAt(index);
                }
            }

            if (lastSpawnedProjectile == null)
            {
                lastSpawnedProjectile = null;
            }
        }

        private static Collider2D[] CopyOwnerColliders(
            IEnumerable<Collider2D> ownerColliders)
        {
            if (ownerColliders == null)
            {
                return new Collider2D[0];
            }

            List<Collider2D> copy = new List<Collider2D>();
            HashSet<int> ids = new HashSet<int>();
            foreach (Collider2D ownerCollider in ownerColliders)
            {
                if (ownerCollider == null
                    || !ids.Add(ownerCollider.GetInstanceID()))
                {
                    continue;
                }

                copy.Add(ownerCollider);
            }

            return copy.ToArray();
        }

        private static bool TryConvertFinite(double value, out float converted)
        {
            converted = (float)value;
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && IsFinite(converted);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

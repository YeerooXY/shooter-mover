using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.ContentPackages.Weapons.Stage1;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Weapons.RocketLauncher.Runtime
{
    /// <summary>
    /// Immutable numeric tuning for one Rocket Launcher fire mode. The package has one
    /// fixed behavior topology; empowerment may select only another validated numeric set.
    /// </summary>
    public sealed class RocketLauncherTuning
    {
        public RocketLauncherTuning(
            double damage,
            double projectileSpeed,
            double projectileLifetimeSeconds,
            double projectileRadius,
            double areaRadius)
        {
            RequireFinitePositive(damage, nameof(damage));
            RequireFinitePositive(projectileSpeed, nameof(projectileSpeed));
            RequireFinitePositive(projectileLifetimeSeconds, nameof(projectileLifetimeSeconds));
            RequireFinitePositive(projectileRadius, nameof(projectileRadius));
            RequireFinitePositive(areaRadius, nameof(areaRadius));

            if (projectileSpeed < BoundedProjectile2D.MinimumSpeed
                || projectileSpeed > BoundedProjectile2D.MaximumSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileSpeed));
            }

            if (projectileLifetimeSeconds < BoundedProjectile2D.MinimumLifetimeSeconds
                || projectileLifetimeSeconds >= BoundedProjectile2D.MaximumLifetimeSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileLifetimeSeconds));
            }

            if (projectileRadius < BoundedProjectile2D.MinimumRadius
                || projectileRadius > BoundedProjectile2D.MaximumRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileRadius));
            }

            if (areaRadius > RocketLauncherExecutionPlanAdapter.MaximumAreaRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(areaRadius));
            }

            Damage = damage;
            ProjectileSpeed = projectileSpeed;
            ProjectileLifetimeSeconds = projectileLifetimeSeconds;
            ProjectileRadius = projectileRadius;
            AreaRadius = areaRadius;
        }

        public double Damage { get; }

        public double ProjectileSpeed { get; }

        public double ProjectileLifetimeSeconds { get; }

        public double ProjectileRadius { get; }

        public double AreaRadius { get; }

        public string ToCanonicalString()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "damage=" + Damage.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_speed=" + ProjectileSpeed.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_lifetime_seconds="
                        + ProjectileLifetimeSeconds.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_radius=" + ProjectileRadius.ToString("R", CultureInfo.InvariantCulture),
                    "area_radius=" + AreaRadius.ToString("R", CultureInfo.InvariantCulture),
                });
        }

        private static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Rocket tuning values must be finite and positive.");
            }
        }
    }

    /// <summary>
    /// One immutable rocket operation emitted through the accepted CB-004 plan boundary.
    /// Damage remains numeric intent only; the Unity adapter emits confirmed CS-004 hits
    /// and does not mutate enemy state.
    /// </summary>
    public sealed class RocketLauncherExecutionOperation : IWeaponFireExecutionOperation
    {
        public RocketLauncherExecutionOperation(
            StableId operationKindId,
            StableId operationId,
            double damage,
            double projectileSpeed,
            double projectileLifetimeSeconds,
            double projectileRadius,
            double areaRadius,
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

            RocketLauncherTuning tuning = new RocketLauncherTuning(
                damage,
                projectileSpeed,
                projectileLifetimeSeconds,
                projectileRadius,
                areaRadius);
            if (!Enum.IsDefined(typeof(CombatChannel), channel)
                || channel == CombatChannel.System)
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            OperationKindId = operationKindId;
            OperationId = operationId;
            Damage = tuning.Damage;
            ProjectileSpeed = tuning.ProjectileSpeed;
            ProjectileLifetimeSeconds = tuning.ProjectileLifetimeSeconds;
            ProjectileRadius = tuning.ProjectileRadius;
            AreaRadius = tuning.AreaRadius;
            Channel = channel;
        }

        public StableId OperationKindId { get; }

        public StableId OperationId { get; }

        public double Damage { get; }

        public double ProjectileSpeed { get; }

        public double ProjectileLifetimeSeconds { get; }

        public double ProjectileRadius { get; }

        public double AreaRadius { get; }

        public CombatChannel Channel { get; }
    }

    /// <summary>
    /// Pure package-owned behavior module. It emits exactly one rocket operation and
    /// chooses only between the package's normal and empowered numeric tuning.
    /// </summary>
    public sealed class RocketLauncherBehaviorModule : IWeaponBehaviorModule
    {
        private const string OperationIdentityNamespace = "rocket-operation";

        private readonly RocketLauncherTuning normalTuning;
        private readonly RocketLauncherTuning empoweredTuning;

        public RocketLauncherBehaviorModule(
            StableId moduleId,
            StableId operationKindId,
            RocketLauncherTuning normalTuning,
            RocketLauncherTuning empoweredTuning)
        {
            ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
            OperationKindId =
                operationKindId ?? throw new ArgumentNullException(nameof(operationKindId));
            this.normalTuning =
                normalTuning ?? throw new ArgumentNullException(nameof(normalTuning));
            this.empoweredTuning =
                empoweredTuning ?? throw new ArgumentNullException(nameof(empoweredTuning));
        }

        public StableId ModuleId { get; }

        public StableId OperationKindId { get; }

        public WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.WeaponId != RocketLauncherPackage.WeaponId)
            {
                throw new ArgumentException(
                    "The Rocket Launcher module accepts only weapon.rocket-launcher.",
                    nameof(input));
            }

            RocketLauncherTuning tuning = input.IsEmpowered
                ? empoweredTuning
                : normalTuning;
            StableId operationId = CreateOperationId(input, tuning);
            RocketLauncherExecutionOperation operation =
                new RocketLauncherExecutionOperation(
                    OperationKindId,
                    operationId,
                    tuning.Damage,
                    tuning.ProjectileSpeed,
                    tuning.ProjectileLifetimeSeconds,
                    tuning.ProjectileRadius,
                    tuning.AreaRadius,
                    CombatChannel.Explosive);
            return new WeaponBehaviorModulePlan(ModuleId, operation);
        }

        private StableId CreateOperationId(
            WeaponBehaviorInput input,
            RocketLauncherTuning tuning)
        {
            string canonical = string.Join(
                "\n",
                new[]
                {
                    "module_id=" + ModuleId,
                    "operation_kind_id=" + OperationKindId,
                    "combat_event_id=" + input.CombatEventId,
                    "weapon_id=" + input.WeaponId,
                    "mount_id=" + input.MountId,
                    "simulation_step="
                        + input.SimulationStep.ToString(CultureInfo.InvariantCulture),
                    "is_empowered=" + (input.IsEmpowered ? "true" : "false"),
                    "origin_x=" + input.OriginX.ToString("R", CultureInfo.InvariantCulture),
                    "origin_y=" + input.OriginY.ToString("R", CultureInfo.InvariantCulture),
                    "direction_x=" + input.DirectionX.ToString("R", CultureInfo.InvariantCulture),
                    "direction_y=" + input.DirectionY.ToString("R", CultureInfo.InvariantCulture),
                    tuning.ToCanonicalString(),
                });
            return StableId.Create(
                OperationIdentityNamespace,
                ComputeSha256Hex(canonical));
        }

        private static string ComputeSha256Hex(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                byte[] digest = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }

    /// <summary>
    /// Canonical package definition and numeric tuning for WP-005.
    /// </summary>
    public static class RocketLauncherPackage
    {
        public static readonly StableId WeaponId =
            StableId.Parse("weapon.rocket-launcher");
        public static readonly StableId ModuleId =
            StableId.Parse("module.weapon-rocket-area-detonation");
        public static readonly StableId OperationKindId =
            StableId.Parse("operation-kind.rocket-area-detonation-2d");

        private static readonly RocketLauncherTuning NormalTuningValue =
            new RocketLauncherTuning(30d, 8d, 3d, 0.12d, 2d);
        private static readonly RocketLauncherTuning EmpoweredTuningValue =
            new RocketLauncherTuning(42d, 10d, 3d, 0.12d, 2.5d);
        private static readonly WeaponRuntimeProfile NormalRuntimeProfileValue =
            CreateRuntimeProfile(false);
        private static readonly WeaponRuntimeProfile EmpoweredRuntimeProfileValue =
            CreateRuntimeProfile(true);
        private static readonly Stage1WeaponPackageDescriptor DescriptorValue =
            CreateDescriptorInternal();

        public static RocketLauncherTuning NormalTuning
        {
            get { return NormalTuningValue; }
        }

        public static RocketLauncherTuning EmpoweredTuning
        {
            get { return EmpoweredTuningValue; }
        }

        public static WeaponRuntimeProfile NormalRuntimeProfile
        {
            get { return NormalRuntimeProfileValue; }
        }

        public static WeaponRuntimeProfile EmpoweredRuntimeProfile
        {
            get { return EmpoweredRuntimeProfileValue; }
        }

        public static Stage1WeaponPackageDescriptor Descriptor
        {
            get { return DescriptorValue; }
        }

        public static RocketLauncherBehaviorModule CreateBehaviorModule()
        {
            return new RocketLauncherBehaviorModule(
                ModuleId,
                OperationKindId,
                NormalTuningValue,
                EmpoweredTuningValue);
        }

        private static WeaponRuntimeProfile CreateRuntimeProfile(bool empowered)
        {
            StableId profileId = StableId.Create(
                "weapon-profile",
                WeaponId.Value + (empowered ? "-empowered" : "-normal"));
            StableId[] modules = { ModuleId };
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                profileId,
                0.75d,
                1,
                0d,
                0.2d,
                WeaponCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                true,
                10d,
                2d,
                0d,
                modules,
                modules,
                2);
        }

        private static Stage1WeaponPackageDescriptor CreateDescriptorInternal()
        {
            ContentReference moduleReference = ContentReference.Create(
                ModuleId,
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                WeaponId,
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion,
                StableId.Parse("provenance.weapon-rocket-launcher"),
                false,
                moduleReference);
            Stage1WeaponBehaviorTopology topology =
                Stage1WeaponBehaviorTopology.Create(
                    Stage1WeaponBehaviorKind.RocketAreaDetonation,
                    0,
                    0,
                    1,
                    false);
            Stage1WeaponFireProfile normal = Stage1WeaponFireProfile.Create(
                NormalRuntimeProfileValue,
                topology,
                false,
                CreateCoefficients(NormalTuningValue));
            Stage1WeaponFireProfile empowered = Stage1WeaponFireProfile.Create(
                EmpoweredRuntimeProfileValue,
                topology,
                false,
                CreateCoefficients(EmpoweredTuningValue));
            return Stage1WeaponPackageDescriptor.Create(
                Stage1WeaponPackageDescriptor.CurrentDescriptorVersion,
                content,
                false,
                normal,
                empowered);
        }

        private static IEnumerable<Stage1WeaponNumericCoefficient> CreateCoefficients(
            RocketLauncherTuning tuning)
        {
            return new[]
            {
                Stage1WeaponNumericCoefficient.Create(
                    Stage1WeaponNumericCoefficientKind.Damage,
                    tuning.Damage),
                Stage1WeaponNumericCoefficient.Create(
                    Stage1WeaponNumericCoefficientKind.ProjectileSpeed,
                    tuning.ProjectileSpeed),
                Stage1WeaponNumericCoefficient.Create(
                    Stage1WeaponNumericCoefficientKind.ProjectileLifetimeSeconds,
                    tuning.ProjectileLifetimeSeconds),
                Stage1WeaponNumericCoefficient.Create(
                    Stage1WeaponNumericCoefficientKind.ProjectileRadius,
                    tuning.ProjectileRadius),
                Stage1WeaponNumericCoefficient.Create(
                    Stage1WeaponNumericCoefficientKind.AreaRadius,
                    tuning.AreaRadius),
            };
        }
    }
}

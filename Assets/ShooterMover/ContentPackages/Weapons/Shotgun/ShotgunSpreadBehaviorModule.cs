using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Weapons.Shotgun
{
    /// <summary>
    /// Immutable intent for one ordered pellet. Damage remains numeric package intent;
    /// the 2D handler only creates a bounded projectile and never applies damage.
    /// </summary>
    public sealed class ShotgunPelletExecutionOperation :
        IWeaponFireExecutionOperation
    {
        internal ShotgunPelletExecutionOperation(
            StableId operationId,
            int pelletIndex,
            int pelletCount,
            double offsetDegrees,
            double directionX,
            double directionY,
            double damage,
            double projectileSpeed,
            double projectileLifetimeSeconds,
            double projectileRadius)
        {
            OperationKindId = ShotgunSpreadBehaviorModule.PelletOperationKindId;
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            PelletIndex = pelletIndex;
            PelletCount = pelletCount;
            OffsetDegrees = offsetDegrees;
            DirectionX = directionX;
            DirectionY = directionY;
            Damage = damage;
            ProjectileSpeed = projectileSpeed;
            ProjectileLifetimeSeconds = projectileLifetimeSeconds;
            ProjectileRadius = projectileRadius;
            Channel = CombatChannel.Kinetic;
        }

        public StableId OperationKindId { get; }

        public StableId OperationId { get; }

        public int PelletIndex { get; }

        public int PelletCount { get; }

        public double OffsetDegrees { get; }

        public double DirectionX { get; }

        public double DirectionY { get; }

        public double Damage { get; }

        public double ProjectileSpeed { get; }

        public double ProjectileLifetimeSeconds { get; }

        public double ProjectileRadius { get; }

        public CombatChannel Channel { get; }

        public string ToCanonicalString()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "operation_kind_id=" + OperationKindId,
                    "operation_id=" + OperationId,
                    "pellet_index=" + PelletIndex.ToString(CultureInfo.InvariantCulture),
                    "pellet_count=" + PelletCount.ToString(CultureInfo.InvariantCulture),
                    "offset_degrees=" + OffsetDegrees.ToString("R", CultureInfo.InvariantCulture),
                    "direction_x=" + DirectionX.ToString("R", CultureInfo.InvariantCulture),
                    "direction_y=" + DirectionY.ToString("R", CultureInfo.InvariantCulture),
                    "damage=" + Damage.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_speed="
                        + ProjectileSpeed.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_lifetime_seconds="
                        + ProjectileLifetimeSeconds.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_radius="
                        + ProjectileRadius.ToString("R", CultureInfo.InvariantCulture),
                    "channel=kinetic",
                });
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// Pure deterministic CB-004 behavior module. One fire input produces a fixed,
    /// left-to-right ordered list of bounded pellet operations with no random source.
    /// </summary>
    public sealed class ShotgunSpreadBehaviorModule : IWeaponBehaviorModule
    {
        private const string OperationIdentityNamespace = "shotgun-pellet";

        private static readonly StableId ModuleIdValue =
            StableId.Parse("module.weapon-spread-projectile");
        private static readonly StableId PelletOperationKindIdValue =
            StableId.Parse("operation-kind.shotgun-pellet-2d");

        private readonly ShotgunTuning normalTuning;
        private readonly ShotgunTuning empoweredTuning;

        public ShotgunSpreadBehaviorModule(
            ShotgunTuning normalTuning,
            ShotgunTuning empoweredTuning)
        {
            ShotgunTuning.ValidateEmpowermentBoundary(normalTuning, empoweredTuning);
            this.normalTuning = normalTuning;
            this.empoweredTuning = empoweredTuning;
        }

        public StableId ModuleId
        {
            get { return ModuleIdValue; }
        }

        public static StableId SharedModuleId
        {
            get { return ModuleIdValue; }
        }

        public static StableId PelletOperationKindId
        {
            get { return PelletOperationKindIdValue; }
        }

        public ShotgunTuning NormalTuning
        {
            get { return normalTuning; }
        }

        public ShotgunTuning EmpoweredTuning
        {
            get { return empoweredTuning; }
        }

        public WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (!input.WeaponId.Equals(ShotgunPackageDefinition.WeaponId))
            {
                throw new ArgumentException(
                    "The shotgun spread module accepts only weapon.shotgun input.",
                    nameof(input));
            }

            ShotgunTuning tuning = input.IsEmpowered
                ? empoweredTuning
                : normalTuning;

            double length = Math.Sqrt(
                (input.DirectionX * input.DirectionX)
                + (input.DirectionY * input.DirectionY));
            if (double.IsNaN(length)
                || double.IsInfinity(length)
                || length <= 0d)
            {
                throw new ArgumentException(
                    "Shotgun aim direction must be finite and non-zero.",
                    nameof(input));
            }

            double baseX = input.DirectionX / length;
            double baseY = input.DirectionY / length;
            IWeaponFireExecutionOperation[] operations =
                new IWeaponFireExecutionOperation[tuning.PelletCount];

            for (int pelletIndex = 0;
                 pelletIndex < tuning.PelletCount;
                 pelletIndex++)
            {
                double offsetDegrees = GetOffsetDegrees(
                    pelletIndex,
                    tuning.PelletCount,
                    tuning.SpreadDegrees);
                double radians = offsetDegrees * (Math.PI / 180d);
                double cosine = Math.Cos(radians);
                double sine = Math.Sin(radians);
                double directionX = (baseX * cosine) - (baseY * sine);
                double directionY = (baseX * sine) + (baseY * cosine);
                StableId operationId = CreateOperationId(
                    input,
                    tuning,
                    pelletIndex,
                    offsetDegrees,
                    directionX,
                    directionY);

                operations[pelletIndex] = new ShotgunPelletExecutionOperation(
                    operationId,
                    pelletIndex,
                    tuning.PelletCount,
                    offsetDegrees,
                    directionX,
                    directionY,
                    tuning.Damage,
                    tuning.ProjectileSpeed,
                    tuning.ProjectileLifetimeSeconds,
                    tuning.ProjectileRadius);
            }

            return new WeaponBehaviorModulePlan(ModuleId, operations);
        }

        public static double GetOffsetDegrees(
            int pelletIndex,
            int pelletCount,
            double spreadDegrees)
        {
            if (pelletCount < ShotgunTuning.MinimumPelletCount
                || pelletCount > ShotgunTuning.MaximumPelletCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pelletCount));
            }

            if (pelletIndex < 0 || pelletIndex >= pelletCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pelletIndex));
            }

            if (double.IsNaN(spreadDegrees)
                || double.IsInfinity(spreadDegrees)
                || spreadDegrees < ShotgunTuning.MinimumSpreadDegrees
                || spreadDegrees > ShotgunTuning.MaximumSpreadDegrees)
            {
                throw new ArgumentOutOfRangeException(nameof(spreadDegrees));
            }

            double step = spreadDegrees / (pelletCount - 1);
            return (-spreadDegrees * 0.5d) + (pelletIndex * step);
        }

        private static StableId CreateOperationId(
            WeaponBehaviorInput input,
            ShotgunTuning tuning,
            int pelletIndex,
            double offsetDegrees,
            double directionX,
            double directionY)
        {
            string canonical = string.Join(
                "\n",
                new[]
                {
                    "combat_event_id=" + input.CombatEventId,
                    "weapon_id=" + input.WeaponId,
                    "mount_id=" + input.MountId,
                    "simulation_step="
                        + input.SimulationStep.ToString(CultureInfo.InvariantCulture),
                    "runtime_profile_identity="
                        + input.RuntimeProfile.DeterministicIdentity,
                    "is_empowered=" + (input.IsEmpowered ? "true" : "false"),
                    "pellet_index=" + pelletIndex.ToString(CultureInfo.InvariantCulture),
                    "pellet_count="
                        + tuning.PelletCount.ToString(CultureInfo.InvariantCulture),
                    "offset_degrees="
                        + offsetDegrees.ToString("R", CultureInfo.InvariantCulture),
                    "direction_x=" + directionX.ToString("R", CultureInfo.InvariantCulture),
                    "direction_y=" + directionY.ToString("R", CultureInfo.InvariantCulture),
                    tuning.ToCanonicalString(),
                });

            return StableId.Create(
                OperationIdentityNamespace,
                ComputeSha256(canonical));
        }

        internal static string ComputeSha256(string text)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                for (int index = 0; index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }
}

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Weapons.BlasterMachineGun
{
    /// <summary>
    /// One deterministic projectile operation for every accepted automatic-fire cycle.
    /// It performs no target search and owns no cadence, ammunition, power-bank, or hit authority.
    /// </summary>
    public sealed class BlasterMachineGunAutomaticProjectileModule : IWeaponBehaviorModule
    {
        private static readonly StableId NormalOperationId = CreateOperationId(false);
        private static readonly StableId EmpoweredOperationId = CreateOperationId(true);

        public StableId ModuleId
        {
            get { return BlasterMachineGunPackage.ModuleId; }
        }

        public WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.WeaponId != BlasterMachineGunPackage.WeaponId)
            {
                throw new ArgumentException(
                    "The Blaster Machine Gun module only accepts its package weapon identity.",
                    nameof(input));
            }

            WeaponRuntimeProfile normal =
                BlasterMachineGunPackage.GetNormalRuntimeProfile();
            WeaponRuntimeProfile empowered =
                BlasterMachineGunPackage.GetEmpoweredRuntimeProfile();
            if (!input.RuntimeProfile.Equals(normal)
                && !input.RuntimeProfile.Equals(empowered))
            {
                throw new ArgumentException(
                    "The Blaster Machine Gun module requires one of its immutable runtime profiles.",
                    nameof(input));
            }

            bool useEmpoweredNumbers = input.IsEmpowered;
            BoundedProjectileExecutionOperation operation =
                new BoundedProjectileExecutionOperation(
                    BlasterMachineGunPackage.OperationKindId,
                    useEmpoweredNumbers ? EmpoweredOperationId : NormalOperationId,
                    useEmpoweredNumbers
                        ? BlasterMachineGunPackage.EmpoweredProjectileSpeed
                        : BlasterMachineGunPackage.NormalProjectileSpeed,
                    useEmpoweredNumbers
                        ? BlasterMachineGunPackage.EmpoweredProjectileLifetimeSeconds
                        : BlasterMachineGunPackage.NormalProjectileLifetimeSeconds,
                    useEmpoweredNumbers
                        ? BlasterMachineGunPackage.EmpoweredProjectileRadius
                        : BlasterMachineGunPackage.NormalProjectileRadius,
                    CombatChannel.Kinetic);

            return new WeaponBehaviorModulePlan(ModuleId, operation);
        }

        private static StableId CreateOperationId(bool empowered)
        {
            string canonicalPayload = string.Join(
                "\n",
                new[]
                {
                    "operation_kind=" + BlasterMachineGunPackage.OperationKindStableIdText,
                    "mode=" + (empowered ? "empowered" : "normal"),
                    "projectile_speed=" + (empowered
                        ? BlasterMachineGunPackage.EmpoweredProjectileSpeed
                        : BlasterMachineGunPackage.NormalProjectileSpeed)
                        .ToString("R", CultureInfo.InvariantCulture),
                    "projectile_lifetime_seconds=" + (empowered
                        ? BlasterMachineGunPackage.EmpoweredProjectileLifetimeSeconds
                        : BlasterMachineGunPackage.NormalProjectileLifetimeSeconds)
                        .ToString("R", CultureInfo.InvariantCulture),
                    "projectile_radius=" + (empowered
                        ? BlasterMachineGunPackage.EmpoweredProjectileRadius
                        : BlasterMachineGunPackage.NormalProjectileRadius)
                        .ToString("R", CultureInfo.InvariantCulture),
                    "combat_channel=" + ((int)CombatChannel.Kinetic)
                        .ToString(CultureInfo.InvariantCulture),
                });

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalPayload));
                StringBuilder hexadecimal = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    hexadecimal.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return StableId.Create(
                    "operation",
                    "blaster-machine-gun-" + hexadecimal);
            }
        }
    }
}

using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Weapons.BlasterMachineGun
{
    /// <summary>
    /// Immutable authored package for the uncomplicated Stage 1 automatic-fire baseline.
    /// Runtime cadence, shared aim, independent power, and fallback remain owned by the
    /// accepted combat foundation; this package contributes only identity, numeric tuning,
    /// one explicit behavior module, and registry descriptor inputs.
    /// </summary>
    public static class BlasterMachineGunPackage
    {
        public const string WeaponStableIdText = "weapon.blaster-machine-gun";
        public const string ModuleStableIdText = "module.weapon-automatic-projectile";
        public const string OperationKindStableIdText = "operation-kind.bounded-projectile-2d";

        public const double CadenceSeconds = 0.1d;
        public const double PowerBankCapacityUnits = 4d;
        public const double EmpoweredCostUnits = 1d;

        public const double NormalDamage = 10d;
        public const double NormalProjectileSpeed = 20d;
        public const double NormalProjectileLifetimeSeconds = 2d;
        public const double NormalProjectileRadius = 0.1d;

        public const double EmpoweredDamage = 15d;
        public const double EmpoweredProjectileSpeed = 24d;
        public const double EmpoweredProjectileLifetimeSeconds = 2d;
        public const double EmpoweredProjectileRadius = 0.12d;

        private static readonly StableId WeaponIdValue =
            StableId.Parse(WeaponStableIdText);
        private static readonly StableId ModuleIdValue =
            StableId.Parse(ModuleStableIdText);
        private static readonly StableId OperationKindIdValue =
            StableId.Parse(OperationKindStableIdText);
        private static readonly StableId[] ModuleIds = { ModuleIdValue };
        private static readonly ContentReference ModuleReferenceValue =
            ContentReference.Create(
                ModuleIdValue,
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
        private static readonly ContentDefinitionDescriptor ModuleContentDefinitionValue =
            ContentDefinitionDescriptor.Create(
                ModuleIdValue,
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion,
                StableId.Parse("provenance.blaster-machine-gun-module"),
                false,
                new ContentReference[0]);
        private static readonly ContentDefinitionDescriptor WeaponContentDefinitionValue =
            ContentDefinitionDescriptor.Create(
                WeaponIdValue,
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion,
                StableId.Parse("provenance.blaster-machine-gun"),
                false,
                ModuleReferenceValue);
        private static readonly Stage1WeaponBehaviorTopology TopologyValue =
            Stage1WeaponBehaviorTopology.Create(
                Stage1WeaponBehaviorKind.AutomaticProjectile,
                0,
                0,
                0,
                false);
        private static readonly WeaponRuntimeProfile NormalRuntimeProfileValue =
            CreateRuntimeProfile("blaster-machine-gun-normal");
        private static readonly WeaponRuntimeProfile EmpoweredRuntimeProfileValue =
            CreateRuntimeProfile("blaster-machine-gun-empowered");
        private static readonly Stage1WeaponPackageDescriptor DescriptorValue =
            CreateDescriptorValue();

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

        public static Stage1WeaponPackageDescriptor CreateDescriptor()
        {
            return DescriptorValue;
        }

        public static ContentDefinitionDescriptor CreateModuleContentDefinition()
        {
            return ModuleContentDefinitionValue;
        }

        public static WeaponRuntimeProfile GetNormalRuntimeProfile()
        {
            return NormalRuntimeProfileValue;
        }

        public static WeaponRuntimeProfile GetEmpoweredRuntimeProfile()
        {
            return EmpoweredRuntimeProfileValue;
        }

        public static IWeaponBehaviorModule CreateBehaviorModule()
        {
            return new BlasterMachineGunAutomaticProjectileModule();
        }

        public static string CreateManifestSnapshot()
        {
            return "default_starting_weapon=true"
                + "\nweapon_definition:\n"
                + WeaponContentDefinitionValue.ToCanonicalString()
                + "\nmodule_definition:\n"
                + ModuleContentDefinitionValue.ToCanonicalString()
                + "\nnormal_profile:\n"
                + NormalRuntimeProfileValue.ToCanonicalString()
                + "\nempowered_profile:\n"
                + EmpoweredRuntimeProfileValue.ToCanonicalString();
        }

        private static Stage1WeaponPackageDescriptor CreateDescriptorValue()
        {
            Stage1WeaponFireProfile normalFire = Stage1WeaponFireProfile.Create(
                NormalRuntimeProfileValue,
                TopologyValue,
                false,
                new[]
                {
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.Damage,
                        NormalDamage),
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.ProjectileSpeed,
                        NormalProjectileSpeed),
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.ProjectileLifetimeSeconds,
                        NormalProjectileLifetimeSeconds),
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.ProjectileRadius,
                        NormalProjectileRadius),
                });
            Stage1WeaponFireProfile empoweredFire = Stage1WeaponFireProfile.Create(
                EmpoweredRuntimeProfileValue,
                TopologyValue,
                false,
                new[]
                {
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.Damage,
                        EmpoweredDamage),
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.ProjectileSpeed,
                        EmpoweredProjectileSpeed),
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.ProjectileLifetimeSeconds,
                        EmpoweredProjectileLifetimeSeconds),
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.ProjectileRadius,
                        EmpoweredProjectileRadius),
                });

            return Stage1WeaponPackageDescriptor.Create(
                Stage1WeaponPackageDescriptor.CurrentDescriptorVersion,
                WeaponContentDefinitionValue,
                true,
                normalFire,
                empoweredFire);
        }

        private static WeaponRuntimeProfile CreateRuntimeProfile(string profileValue)
        {
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                StableId.Create("weapon-profile", profileValue),
                CadenceSeconds,
                1,
                0d,
                0d,
                WeaponCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                true,
                PowerBankCapacityUnits,
                EmpoweredCostUnits,
                0d,
                ModuleIds,
                ModuleIds,
                10);
        }
    }
}

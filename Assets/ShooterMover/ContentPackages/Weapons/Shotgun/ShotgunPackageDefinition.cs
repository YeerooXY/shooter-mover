using ShooterMover.ContentPackages.Weapons.Stage1;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Weapons.Shotgun
{
    /// <summary>
    /// Canonical WP-004 descriptor input and deterministic default numeric fixture.
    /// It contributes no generated registry output and owns no scene or damage state.
    /// </summary>
    public static class ShotgunPackageDefinition
    {
        private static readonly StableId WeaponIdValue =
            StableId.Parse("weapon.shotgun");
        private static readonly StableId ProvenanceIdValue =
            StableId.Parse("provenance.wp-004-shotgun-package");

        private static readonly ShotgunTuning NormalTuningValue =
            new ShotgunTuning(
                7,
                24d,
                5d,
                0.55d,
                18d,
                0.7d,
                0.08d,
                0.15d);

        private static readonly ShotgunTuning EmpoweredTuningValue =
            new ShotgunTuning(
                7,
                18d,
                7d,
                0.45d,
                20d,
                0.7d,
                0.08d,
                0.1d);

        public static StableId WeaponId
        {
            get { return WeaponIdValue; }
        }

        public static StableId ModuleId
        {
            get { return ShotgunSpreadBehaviorModule.SharedModuleId; }
        }

        public static ShotgunTuning NormalTuning
        {
            get { return NormalTuningValue; }
        }

        public static ShotgunTuning EmpoweredTuning
        {
            get { return EmpoweredTuningValue; }
        }

        public static ShotgunSpreadBehaviorModule CreateBehaviorModule()
        {
            return new ShotgunSpreadBehaviorModule(
                NormalTuningValue,
                EmpoweredTuningValue);
        }

        public static Stage1WeaponPackageDescriptor CreateDefaultDescriptor()
        {
            ContentReference moduleReference = ContentReference.Create(
                ModuleId,
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
            ContentDefinitionDescriptor contentDefinition =
                ContentDefinitionDescriptor.Create(
                    WeaponIdValue,
                    ContentDefinitionKind.Weapon,
                    ContentReference.SupportedDefinitionVersion,
                    ProvenanceIdValue,
                    false,
                    moduleReference);
            Stage1WeaponBehaviorTopology topology =
                Stage1WeaponBehaviorTopology.Create(
                    Stage1WeaponBehaviorKind.SpreadProjectile,
                    0,
                    0,
                    0,
                    false);

            return Stage1WeaponPackageDescriptor.Create(
                Stage1WeaponPackageDescriptor.CurrentDescriptorVersion,
                contentDefinition,
                false,
                CreateFireProfile(NormalTuningValue, false, topology),
                CreateFireProfile(EmpoweredTuningValue, true, topology));
        }

        private static Stage1WeaponFireProfile CreateFireProfile(
            ShotgunTuning tuning,
            bool empowered,
            Stage1WeaponBehaviorTopology topology)
        {
            return Stage1WeaponFireProfile.Create(
                CreateRuntimeProfile(tuning, empowered),
                topology,
                false,
                new[]
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
                        Stage1WeaponNumericCoefficientKind.SpreadDegrees,
                        tuning.SpreadDegrees),
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.ProjectileRadius,
                        tuning.ProjectileRadius),
                });
        }

        private static WeaponRuntimeProfile CreateRuntimeProfile(
            ShotgunTuning tuning,
            bool empowered)
        {
            StableId profileId = StableId.Parse(
                empowered
                    ? "weapon-profile.shotgun-empowered"
                    : "weapon-profile.shotgun-normal");
            StableId[] modules = { ModuleId };

            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                profileId,
                tuning.CadenceSeconds,
                1,
                0d,
                tuning.RecoverySeconds,
                WeaponCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                true,
                12d,
                3d,
                0d,
                modules,
                modules,
                2);
        }
    }
}

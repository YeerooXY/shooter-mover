using System;
using ShooterMover.ContentPackages.Enemies.Stage1;
using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.BlasterTurret
{
    /// <summary>
    /// Package-owned authoring and bounded tuning for the stationary Stage 1 Blaster Turret.
    /// Health and lifecycle truth remain EN-002 EnemyActorState values.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BlasterTurretDefinition",
        menuName = "Shooter Mover/Enemies/Blaster Turret Definition")]
    public sealed class BlasterTurretDefinition : ScriptableObject
    {
        public const double HardMaximumHealth = 10000d;
        public const double HardMaximumWarningSeconds = 10d;
        public const double HardMaximumRecoverySeconds = 60d;
        public const double HardMaximumRange = 250d;
        public const double HardMaximumMuzzleOffset = 10d;
        public const double HardMaximumWarningLineWidth = 2d;
        public const int HardMaximumMoverColliderCapacity = 64;

        [SerializeField] private float maximumHealth = 30f;
        [SerializeField] private float warningSeconds = 0.4f;
        [SerializeField] private float recoverySeconds = 0.8f;
        [SerializeField] private float maximumRange = 28f;
        [SerializeField] private float muzzleOffset = 0.7f;
        [SerializeField] private float warningLineWidth = 0.07f;
        [SerializeField] private float contactGraceSeconds = 0.5f;
        [SerializeField] private float simultaneousContactWindowSeconds = 0.02f;
        [SerializeField] private int moverColliderCapacity = 4;

        public double MaximumHealth
        {
            get { return maximumHealth; }
        }

        public double WarningSeconds
        {
            get { return warningSeconds; }
        }

        public double RecoverySeconds
        {
            get { return recoverySeconds; }
        }

        public double MaximumRange
        {
            get { return maximumRange; }
        }

        public double MuzzleOffset
        {
            get { return muzzleOffset; }
        }

        public double WarningLineWidth
        {
            get { return warningLineWidth; }
        }

        public double ContactGraceSeconds
        {
            get { return contactGraceSeconds; }
        }

        public double SimultaneousContactWindowSeconds
        {
            get { return simultaneousContactWindowSeconds; }
        }

        public int MoverColliderCapacity
        {
            get { return moverColliderCapacity; }
        }

        public static BlasterTurretDefinition CreateRuntime(
            double maximumHealth,
            double warningSeconds,
            double recoverySeconds,
            double maximumRange,
            double muzzleOffset,
            double warningLineWidth,
            double contactGraceSeconds,
            double simultaneousContactWindowSeconds,
            int moverColliderCapacity)
        {
            ValidateValues(
                maximumHealth,
                warningSeconds,
                recoverySeconds,
                maximumRange,
                muzzleOffset,
                warningLineWidth,
                contactGraceSeconds,
                simultaneousContactWindowSeconds,
                moverColliderCapacity);

            BlasterTurretDefinition definition = CreateInstance<BlasterTurretDefinition>();
            definition.maximumHealth = (float)maximumHealth;
            definition.warningSeconds = (float)warningSeconds;
            definition.recoverySeconds = (float)recoverySeconds;
            definition.maximumRange = (float)maximumRange;
            definition.muzzleOffset = (float)muzzleOffset;
            definition.warningLineWidth = (float)warningLineWidth;
            definition.contactGraceSeconds = (float)contactGraceSeconds;
            definition.simultaneousContactWindowSeconds =
                (float)simultaneousContactWindowSeconds;
            definition.moverColliderCapacity = moverColliderCapacity;
            definition.hideFlags = HideFlags.HideAndDontSave;
            return definition;
        }

        public void ValidateOrThrow()
        {
            ValidateValues(
                MaximumHealth,
                WarningSeconds,
                RecoverySeconds,
                MaximumRange,
                MuzzleOffset,
                WarningLineWidth,
                ContactGraceSeconds,
                SimultaneousContactWindowSeconds,
                MoverColliderCapacity);
        }

        public Stage1EnemyPackageDescriptor CreatePackageDescriptor()
        {
            ContentReference movement = SharedModule("module.enemy-stationary-positioning");
            ContentReference attack = ContentReference.Create(
                BlasterMachineGunPackage.WeaponId,
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion);
            ContentReference telegraph = SharedModule("module.enemy-line-of-fire-telegraph");
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                Stage1EnemyPackageDescriptor.BlasterTurretId,
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                StableId.Create("provenance", "enemy-blaster-turret-en007"),
                false,
                movement,
                attack,
                telegraph);

            return Stage1EnemyPackageDescriptor.Create(
                Stage1EnemyPackageDescriptor.CurrentDescriptorVersion,
                content,
                Stage1EnemyPackageClassification.Ordinary,
                CombatChannel.Kinetic,
                CombatWeightClass.Immovable,
                movement,
                attack,
                telegraph,
                Stage1EnemyCapability.StationaryPositioning
                    | Stage1EnemyCapability.BlasterProjectile
                    | Stage1EnemyCapability.SafeRecoveryWindow
                    | Stage1EnemyCapability.LineOfFireTelegraph);
        }

        internal EnemyActorState CreateInitialState(StableId actorId)
        {
            if (actorId == null)
            {
                throw new ArgumentNullException(nameof(actorId));
            }

            ValidateOrThrow();
            EnemyContactPolicy contactPolicy = EnemyContactPolicy.Create(
                EnemyContactMode.None,
                0d,
                ContactGraceSeconds,
                SimultaneousContactWindowSeconds,
                MoverColliderCapacity);
            return EnemyActorState.Create(
                actorId,
                Stage1EnemyPackageDescriptor.BlasterTurretId,
                MaximumHealth,
                (int)CombatWeightClass.Immovable,
                contactPolicy);
        }

        private static ContentReference SharedModule(string stableId)
        {
            return ContentReference.Create(
                StableId.Parse(stableId),
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Clamp(maximumHealth, 0.01f, (float)HardMaximumHealth);
            warningSeconds = Mathf.Clamp(warningSeconds, 0.01f, (float)HardMaximumWarningSeconds);
            recoverySeconds = Mathf.Clamp(
                recoverySeconds,
                0.01f,
                (float)HardMaximumRecoverySeconds);
            maximumRange = Mathf.Clamp(maximumRange, 0.01f, (float)HardMaximumRange);
            float maximumSafeMuzzleOffset = Mathf.Max(
                0f,
                Mathf.Min((float)HardMaximumMuzzleOffset, maximumRange - 0.001f));
            muzzleOffset = Mathf.Clamp(muzzleOffset, 0f, maximumSafeMuzzleOffset);
            warningLineWidth = Mathf.Clamp(
                warningLineWidth,
                0.005f,
                (float)HardMaximumWarningLineWidth);
            contactGraceSeconds = Mathf.Clamp(contactGraceSeconds, 0.01f, 60f);
            simultaneousContactWindowSeconds = Mathf.Clamp(
                simultaneousContactWindowSeconds,
                0f,
                contactGraceSeconds);
            moverColliderCapacity = Mathf.Clamp(
                moverColliderCapacity,
                1,
                HardMaximumMoverColliderCapacity);
        }

        private static void ValidateValues(
            double maximumHealth,
            double warningSeconds,
            double recoverySeconds,
            double maximumRange,
            double muzzleOffset,
            double warningLineWidth,
            double contactGraceSeconds,
            double simultaneousContactWindowSeconds,
            int moverColliderCapacity)
        {
            RequireFiniteRange(maximumHealth, 0d, HardMaximumHealth, nameof(maximumHealth), false);
            RequireFiniteRange(
                warningSeconds,
                0d,
                HardMaximumWarningSeconds,
                nameof(warningSeconds),
                false);
            RequireFiniteRange(
                recoverySeconds,
                0d,
                HardMaximumRecoverySeconds,
                nameof(recoverySeconds),
                false);
            RequireFiniteRange(maximumRange, 0d, HardMaximumRange, nameof(maximumRange), false);
            RequireFiniteRange(
                muzzleOffset,
                0d,
                HardMaximumMuzzleOffset,
                nameof(muzzleOffset),
                true);
            if (muzzleOffset >= maximumRange)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(muzzleOffset),
                    muzzleOffset,
                    "Muzzle offset must remain strictly inside the authored firing range.");
            }

            RequireFiniteRange(
                warningLineWidth,
                0d,
                HardMaximumWarningLineWidth,
                nameof(warningLineWidth),
                false);
            RequireFiniteRange(
                contactGraceSeconds,
                0d,
                60d,
                nameof(contactGraceSeconds),
                false);
            RequireFiniteRange(
                simultaneousContactWindowSeconds,
                0d,
                contactGraceSeconds,
                nameof(simultaneousContactWindowSeconds),
                true);

            if (moverColliderCapacity <= 0
                || moverColliderCapacity > HardMaximumMoverColliderCapacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moverColliderCapacity),
                    moverColliderCapacity,
                    "Mover collider capacity is outside the package boundary.");
            }
        }

        private static void RequireFiniteRange(
            double value,
            double minimum,
            double maximum,
            string parameterName,
            bool minimumInclusive)
        {
            bool belowMinimum = minimumInclusive ? value < minimum : value <= minimum;
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || belowMinimum
                || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value is outside the finite package boundary.");
            }
        }
    }
}

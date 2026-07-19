using System;
using ShooterMover.ContentPackages.Enemies.Stage1;
using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.MobileBlasterDroid
{
    /// <summary>
    /// Package-local authoring for the uncomplicated moving ranged enemy. Projectile
    /// shape, speed, lifetime, radius, and channel remain owned by the accepted Blaster
    /// Machine Gun package rather than being copied into this definition.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MobileBlasterDroidDefinition",
        menuName = "Shooter Mover/Enemies/Mobile Blaster Droid Definition")]
    public sealed class MobileBlasterDroidDefinition : ScriptableObject
    {
        public const double HardMaximumHealth = 10000d;
        public const double HardMaximumMovementSpeed = 50d;
        public const double HardMaximumPositioningDistance = 1000d;
        public const double HardMaximumPhaseSeconds = 30d;
        public const double HardMaximumMuzzleOffset = 10d;
        public const double HardMaximumTelegraphLength = 50d;
        public const int HardMaximumContactCapacity = 64;

        [SerializeField] private float maximumHealth = 16f;
        [SerializeField] private float movementSpeed = 2.5f;
        [SerializeField] private float preferredDistance = 5f;
        [SerializeField] private float positioningTolerance = 0.5f;
        [SerializeField] private float windUpSeconds = 0.3f;
        [SerializeField] private float recoverySeconds = 0.8f;
        [SerializeField] private float muzzleOffset = 0.65f;
        [SerializeField] private int contactCapacity = 4;
        [SerializeField] private float colliderRadius = 0.55f;
        [SerializeField] private float telegraphLength = 4f;
        [SerializeField] private float warningPulseSeconds = 0.2f;

        public double MaximumHealth
        {
            get { return maximumHealth; }
        }

        public double MovementSpeed
        {
            get { return movementSpeed; }
        }

        public double PreferredDistance
        {
            get { return preferredDistance; }
        }

        public double PositioningTolerance
        {
            get { return positioningTolerance; }
        }

        public double WindUpSeconds
        {
            get { return windUpSeconds; }
        }

        public double RecoverySeconds
        {
            get { return recoverySeconds; }
        }

        public double MuzzleOffset
        {
            get { return muzzleOffset; }
        }

        public int ContactCapacity
        {
            get { return contactCapacity; }
        }

        public double ColliderRadius
        {
            get { return colliderRadius; }
        }

        public double TelegraphLength
        {
            get { return telegraphLength; }
        }

        public double WarningPulseSeconds
        {
            get { return warningPulseSeconds; }
        }

        public static MobileBlasterDroidDefinition CreateRuntime(
            double maximumHealth,
            double movementSpeed,
            double preferredDistance,
            double positioningTolerance,
            double windUpSeconds,
            double recoverySeconds,
            double muzzleOffset,
            int contactCapacity,
            double colliderRadius,
            double telegraphLength,
            double warningPulseSeconds)
        {
            ValidateValues(
                maximumHealth,
                movementSpeed,
                preferredDistance,
                positioningTolerance,
                windUpSeconds,
                recoverySeconds,
                muzzleOffset,
                contactCapacity,
                colliderRadius,
                telegraphLength,
                warningPulseSeconds);

            MobileBlasterDroidDefinition definition =
                CreateInstance<MobileBlasterDroidDefinition>();
            definition.maximumHealth = (float)maximumHealth;
            definition.movementSpeed = (float)movementSpeed;
            definition.preferredDistance = (float)preferredDistance;
            definition.positioningTolerance = (float)positioningTolerance;
            definition.windUpSeconds = (float)windUpSeconds;
            definition.recoverySeconds = (float)recoverySeconds;
            definition.muzzleOffset = (float)muzzleOffset;
            definition.contactCapacity = contactCapacity;
            definition.colliderRadius = (float)colliderRadius;
            definition.telegraphLength = (float)telegraphLength;
            definition.warningPulseSeconds = (float)warningPulseSeconds;
            definition.hideFlags = HideFlags.HideAndDontSave;
            return definition;
        }

        public void ValidateOrThrow()
        {
            ValidateValues(
                MaximumHealth,
                MovementSpeed,
                PreferredDistance,
                PositioningTolerance,
                WindUpSeconds,
                RecoverySeconds,
                MuzzleOffset,
                ContactCapacity,
                ColliderRadius,
                TelegraphLength,
                WarningPulseSeconds);
        }

        public Stage1EnemyPackageDescriptor CreatePackageDescriptor()
        {
            ContentReference movement = SharedModule("module.enemy-mobile-positioning");
            ContentReference attack = ContentReference.Create(
                BlasterMachineGunPackage.WeaponId,
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion);
            ContentReference telegraph = SharedModule("module.enemy-ranged-windup");
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                Stage1EnemyPackageDescriptor.MobileBlasterDroidId,
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                StableId.Create("provenance", "enemy-mobile-blaster-droid-en006"),
                false,
                movement,
                attack,
                telegraph);

            return Stage1EnemyPackageDescriptor.Create(
                Stage1EnemyPackageDescriptor.CurrentDescriptorVersion,
                content,
                Stage1EnemyPackageClassification.Ordinary,
                CombatChannel.Kinetic,
                CombatWeightClass.Standard,
                movement,
                attack,
                telegraph,
                Stage1EnemyCapability.MobilePositioning
                    | Stage1EnemyCapability.BlasterProjectile
                    | Stage1EnemyCapability.SafeRecoveryWindow);
        }

        /// <summary>
        /// Projects this package and its canonical actor state into the shared enemy boundary.
        /// The projection owns no health, lifecycle transition, targeting query, or reward action.
        /// </summary>
        public EnemyRuntimeProjection CreateRuntimeProjection(
            GameplayEntityIdentity identity,
            EnemyActorState actorState,
            long lifecycleGeneration,
            StableId currentTargetId,
            StableId behaviorPhaseId)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (actorState == null) throw new ArgumentNullException(nameof(actorState));
            ValidateOrThrow();

            EnemyDefinitionProjection definition = new EnemyDefinitionProjection(
                Stage1EnemyPackageDescriptor.MobileBlasterDroidId,
                StableId.Parse("module.enemy-mobile-positioning"),
                new[] { BlasterMachineGunPackage.WeaponId },
                new StableId[0],
                EnemyRoomClearRole.RequiredEnemy);
            return new EnemyRuntimeProjection(
                identity,
                definition,
                actorState,
                lifecycleGeneration,
                currentTargetId,
                behaviorPhaseId);
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
                0.5d,
                0.02d,
                ContactCapacity);
            return EnemyActorState.Create(
                actorId,
                Stage1EnemyPackageDescriptor.MobileBlasterDroidId,
                MaximumHealth,
                (int)CombatWeightClass.Standard,
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
            movementSpeed = Mathf.Clamp(
                movementSpeed,
                0.01f,
                (float)HardMaximumMovementSpeed);
            preferredDistance = Mathf.Clamp(
                preferredDistance,
                0f,
                (float)HardMaximumPositioningDistance);
            positioningTolerance = Mathf.Clamp(
                positioningTolerance,
                0f,
                preferredDistance);
            windUpSeconds = Mathf.Clamp(
                windUpSeconds,
                0.01f,
                (float)HardMaximumPhaseSeconds);
            recoverySeconds = Mathf.Clamp(
                recoverySeconds,
                0.01f,
                (float)HardMaximumPhaseSeconds);
            muzzleOffset = Mathf.Clamp(
                muzzleOffset,
                0f,
                (float)HardMaximumMuzzleOffset);
            contactCapacity = Mathf.Clamp(contactCapacity, 1, HardMaximumContactCapacity);
            colliderRadius = Mathf.Clamp(colliderRadius, 0.05f, 10f);
            telegraphLength = Mathf.Clamp(
                telegraphLength,
                0.1f,
                (float)HardMaximumTelegraphLength);
            warningPulseSeconds = Mathf.Clamp(warningPulseSeconds, 0.05f, 10f);
        }

        private static void ValidateValues(
            double maximumHealth,
            double movementSpeed,
            double preferredDistance,
            double positioningTolerance,
            double windUpSeconds,
            double recoverySeconds,
            double muzzleOffset,
            int contactCapacity,
            double colliderRadius,
            double telegraphLength,
            double warningPulseSeconds)
        {
            RequireFiniteRange(
                maximumHealth,
                0d,
                HardMaximumHealth,
                nameof(maximumHealth),
                false);
            RequireFiniteRange(
                movementSpeed,
                0d,
                HardMaximumMovementSpeed,
                nameof(movementSpeed),
                false);
            RequireFiniteRange(
                preferredDistance,
                0d,
                HardMaximumPositioningDistance,
                nameof(preferredDistance),
                true);
            RequireFiniteRange(
                positioningTolerance,
                0d,
                preferredDistance,
                nameof(positioningTolerance),
                true);
            RequireFiniteRange(
                windUpSeconds,
                0d,
                HardMaximumPhaseSeconds,
                nameof(windUpSeconds),
                false);
            RequireFiniteRange(
                recoverySeconds,
                0d,
                HardMaximumPhaseSeconds,
                nameof(recoverySeconds),
                false);
            RequireFiniteRange(
                muzzleOffset,
                0d,
                HardMaximumMuzzleOffset,
                nameof(muzzleOffset),
                true);
            RequireFiniteRange(
                colliderRadius,
                0d,
                10d,
                nameof(colliderRadius),
                false);
            RequireFiniteRange(
                telegraphLength,
                0d,
                HardMaximumTelegraphLength,
                nameof(telegraphLength),
                false);
            RequireFiniteRange(
                warningPulseSeconds,
                0d,
                10d,
                nameof(warningPulseSeconds),
                false);

            if (contactCapacity <= 0 || contactCapacity > HardMaximumContactCapacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contactCapacity),
                    contactCapacity,
                    "Contact capacity is outside the package boundary.");
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
                    "Value is outside the finite Mobile Blaster Droid boundary.");
            }
        }
    }
}

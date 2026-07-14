using System;
using ShooterMover.ContentPackages.Enemies.Stage1;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.RamDroid
{
    /// <summary>
    /// Package-owned identity and tuning for the disposable Ram Droid. The Pursuer
    /// comparison values are acceptance guardrails because EN-004 is a parallel task,
    /// not a dependency; they do not own or serialize Pursuer Drone tuning.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RamDroidDefinition",
        menuName = "Shooter Mover/Enemies/Ram Droid Definition")]
    public sealed class RamDroidDefinition : ScriptableObject
    {
        public const float PursuerComparisonSpeed = 4.5f;
        public const float PursuerComparisonMaximumHealth = 80f;
        public const float PursuerComparisonColliderRadius = 0.48f;

        private static readonly StableId RamDroidRoleIdValue =
            StableId.Parse("enemy.ram-droid");
        private static readonly StableId MovementModuleIdValue =
            StableId.Parse("module.enemy-direct-pursuit");
        private static readonly StableId AttackModuleIdValue =
            StableId.Parse("module.enemy-disposable-impact");
        private static readonly StableId TelegraphModuleIdValue =
            StableId.Parse("module.enemy-impact-telegraph");
        private static readonly StableId ProvenanceIdValue =
            StableId.Parse("provenance.enemy-ram-droid-original");

        [Header("Disposable impact tuning")]
        [SerializeField] private float movementSpeed = 7.5f;
        [SerializeField] private float maximumHealth = 24f;
        [SerializeField] private float colliderRadius = 0.28f;
        [SerializeField] private float impactDamage = 16f;
        [SerializeField] private float contactGraceSeconds = 0.35f;
        [SerializeField] private float simultaneousContactWindowSeconds = 0.02f;
        [SerializeField] private int contactCapacity = 8;

        [Header("Temporary readable warning")]
        [SerializeField] private float warningDistance = 3.25f;
        [SerializeField] private string warningLabel = "RAM!";
        [SerializeField] private float warningPulseAmplitude = 0.16f;
        [SerializeField] private float warningPulseFrequency = 5f;

        public StableId RoleId
        {
            get { return RamDroidRoleIdValue; }
        }

        public float MovementSpeed
        {
            get { return movementSpeed; }
        }

        public float MaximumHealth
        {
            get { return maximumHealth; }
        }

        public float ColliderRadius
        {
            get { return colliderRadius; }
        }

        public float ImpactDamage
        {
            get { return impactDamage; }
        }

        public float ContactGraceSeconds
        {
            get { return contactGraceSeconds; }
        }

        public float SimultaneousContactWindowSeconds
        {
            get { return simultaneousContactWindowSeconds; }
        }

        public int ContactCapacity
        {
            get { return contactCapacity; }
        }

        public float WarningDistance
        {
            get { return warningDistance; }
        }

        public string WarningLabel
        {
            get { return warningLabel; }
        }

        public float WarningPulseAmplitude
        {
            get { return warningPulseAmplitude; }
        }

        public float WarningPulseFrequency
        {
            get { return warningPulseFrequency; }
        }

        public bool IsFasterSmallerAndLowerHealthThanPursuerReference
        {
            get
            {
                return movementSpeed > PursuerComparisonSpeed
                    && maximumHealth < PursuerComparisonMaximumHealth
                    && colliderRadius < PursuerComparisonColliderRadius;
            }
        }

        public void ValidateOrThrow()
        {
            RequireFinitePositive(movementSpeed, nameof(movementSpeed));
            RequireFinitePositive(maximumHealth, nameof(maximumHealth));
            RequireFinitePositive(colliderRadius, nameof(colliderRadius));
            RequireFinitePositive(impactDamage, nameof(impactDamage));
            RequireFinitePositive(contactGraceSeconds, nameof(contactGraceSeconds));
            RequireFiniteNonNegative(
                simultaneousContactWindowSeconds,
                nameof(simultaneousContactWindowSeconds));
            RequireFinitePositive(warningDistance, nameof(warningDistance));
            RequireFiniteNonNegative(warningPulseAmplitude, nameof(warningPulseAmplitude));
            RequireFinitePositive(warningPulseFrequency, nameof(warningPulseFrequency));

            if (simultaneousContactWindowSeconds > contactGraceSeconds)
            {
                throw new InvalidOperationException(
                    "The simultaneous-contact window cannot exceed contact grace.");
            }

            if (contactCapacity <= 0
                || contactCapacity > EnemyContact2DAdapter.HardMaximumMoverColliders)
            {
                throw new InvalidOperationException(
                    "Ram Droid contact capacity must fit the accepted EN-003 boundary.");
            }

            if (movementSpeed > EnemyActor2DAdapter.HardMaximumSpeed)
            {
                throw new InvalidOperationException(
                    "Ram Droid speed exceeds the accepted EN-003 hard maximum.");
            }

            if (string.IsNullOrWhiteSpace(warningLabel))
            {
                throw new InvalidOperationException(
                    "A readable non-color warning label is required.");
            }

            if (!IsFasterSmallerAndLowerHealthThanPursuerReference)
            {
                throw new InvalidOperationException(
                    "Ram Droid tuning must remain faster, smaller, and lower-health than the Pursuer comparison guardrails.");
            }
        }

        public EnemyActorState CreateInitialState(StableId actorId)
        {
            ValidateOrThrow();
            if (actorId == null)
            {
                throw new ArgumentNullException(nameof(actorId));
            }

            EnemyContactPolicy contactPolicy = EnemyContactPolicy.Create(
                EnemyContactMode.DisposableImpact,
                impactDamage,
                contactGraceSeconds,
                simultaneousContactWindowSeconds,
                contactCapacity);
            return EnemyActorState.Create(
                actorId,
                RamDroidRoleIdValue,
                maximumHealth,
                (int)CombatWeightClass.Light,
                contactPolicy);
        }

        public Stage1EnemyPackageDescriptor CreatePackageDescriptor()
        {
            ValidateOrThrow();
            ContentReference movement = SharedModule(MovementModuleIdValue);
            ContentReference attack = SharedModule(AttackModuleIdValue);
            ContentReference telegraph = SharedModule(TelegraphModuleIdValue);
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                RamDroidRoleIdValue,
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                ProvenanceIdValue,
                false,
                movement,
                attack,
                telegraph);

            return Stage1EnemyPackageDescriptor.Create(
                Stage1EnemyPackageDescriptor.CurrentDescriptorVersion,
                content,
                Stage1EnemyPackageClassification.Ordinary,
                CombatChannel.Contact,
                CombatWeightClass.Light,
                movement,
                attack,
                telegraph,
                Stage1EnemyCapability.DirectPursuit
                    | Stage1EnemyCapability.DisposableImpactAttack);
        }

        private static ContentReference SharedModule(StableId id)
        {
            return ContentReference.Create(
                id,
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
        }

        private static void RequireFinitePositive(float value, string fieldName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new InvalidOperationException(fieldName + " must be finite and positive.");
            }
        }

        private static void RequireFiniteNonNegative(float value, string fieldName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new InvalidOperationException(fieldName + " must be finite and non-negative.");
            }
        }
    }
}

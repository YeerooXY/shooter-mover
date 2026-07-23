using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.PursuerDrone
{
    /// <summary>
    /// Package-owned authoring and tuning for the uncomplicated contact pursuer.
    /// Runtime health and contact truth are still EN-002 EnemyActorState values.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PursuerDroneDefinition",
        menuName = "Shooter Mover/Enemies/Pursuer Drone Definition")]
    public sealed class PursuerDroneDefinition : ScriptableObject
    {
        private static readonly StableId EnemyDefinitionIdValue =
            StableId.Parse("enemy.pursuer-drone");

        public const double HardMaximumHealth = 10000d;
        public const double HardMaximumMovementSpeed = 50d;
        public const double HardMaximumContactDamage = 1000d;
        public const double HardMaximumContactCadenceSeconds = 60d;
        public const int HardMaximumMoverColliderCapacity = 64;

        [SerializeField] private float maximumHealth = 12f;
        [SerializeField] private float movementSpeed = 4f;
        [SerializeField] private float stoppingDistance = 0.2f;
        [SerializeField] private float contactDamage = 2f;
        [SerializeField] private float contactCadenceSeconds = 0.5f;
        [SerializeField] private float simultaneousContactWindowSeconds = 0.02f;
        [SerializeField] private int moverColliderCapacity = 4;
        [SerializeField] private float warningPulseSeconds = 0.6f;

        public double MaximumHealth
        {
            get { return maximumHealth; }
        }

        public double MovementSpeed
        {
            get { return movementSpeed; }
        }

        public double StoppingDistance
        {
            get { return stoppingDistance; }
        }

        public double ContactDamage
        {
            get { return contactDamage; }
        }

        public double ContactCadenceSeconds
        {
            get { return contactCadenceSeconds; }
        }

        public double SimultaneousContactWindowSeconds
        {
            get { return simultaneousContactWindowSeconds; }
        }

        public int MoverColliderCapacity
        {
            get { return moverColliderCapacity; }
        }

        public double WarningPulseSeconds
        {
            get { return warningPulseSeconds; }
        }

        public static PursuerDroneDefinition CreateRuntime(
            double maximumHealth,
            double movementSpeed,
            double stoppingDistance,
            double contactDamage,
            double contactCadenceSeconds,
            double simultaneousContactWindowSeconds,
            int moverColliderCapacity,
            double warningPulseSeconds)
        {
            ValidateValues(
                maximumHealth,
                movementSpeed,
                stoppingDistance,
                contactDamage,
                contactCadenceSeconds,
                simultaneousContactWindowSeconds,
                moverColliderCapacity,
                warningPulseSeconds);

            PursuerDroneDefinition definition = CreateInstance<PursuerDroneDefinition>();
            definition.maximumHealth = (float)maximumHealth;
            definition.movementSpeed = (float)movementSpeed;
            definition.stoppingDistance = (float)stoppingDistance;
            definition.contactDamage = (float)contactDamage;
            definition.contactCadenceSeconds = (float)contactCadenceSeconds;
            definition.simultaneousContactWindowSeconds =
                (float)simultaneousContactWindowSeconds;
            definition.moverColliderCapacity = moverColliderCapacity;
            definition.warningPulseSeconds = (float)warningPulseSeconds;
            definition.hideFlags = HideFlags.HideAndDontSave;
            return definition;
        }

        public void ValidateOrThrow()
        {
            ValidateValues(
                MaximumHealth,
                MovementSpeed,
                StoppingDistance,
                ContactDamage,
                ContactCadenceSeconds,
                SimultaneousContactWindowSeconds,
                MoverColliderCapacity,
                WarningPulseSeconds);
        }

        internal EnemyActorState CreateInitialState(StableId actorId)
        {
            if (actorId == null)
            {
                throw new ArgumentNullException(nameof(actorId));
            }

            ValidateOrThrow();
            EnemyContactPolicy contactPolicy = EnemyContactPolicy.Create(
                EnemyContactMode.OrdinaryDamage,
                ContactDamage,
                ContactCadenceSeconds,
                SimultaneousContactWindowSeconds,
                MoverColliderCapacity);
            return EnemyActorState.Create(
                actorId,
                EnemyDefinitionIdValue,
                MaximumHealth,
                (int)CombatWeightClass.Standard,
                contactPolicy);
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Clamp(maximumHealth, 0.01f, (float)HardMaximumHealth);
            movementSpeed = Mathf.Clamp(
                movementSpeed,
                0.01f,
                (float)HardMaximumMovementSpeed);
            stoppingDistance = Mathf.Max(0f, stoppingDistance);
            contactDamage = Mathf.Clamp(
                contactDamage,
                0.01f,
                (float)HardMaximumContactDamage);
            contactCadenceSeconds = Mathf.Clamp(
                contactCadenceSeconds,
                0.01f,
                (float)HardMaximumContactCadenceSeconds);
            simultaneousContactWindowSeconds = Mathf.Clamp(
                simultaneousContactWindowSeconds,
                0f,
                contactCadenceSeconds);
            moverColliderCapacity = Mathf.Clamp(
                moverColliderCapacity,
                1,
                HardMaximumMoverColliderCapacity);
            warningPulseSeconds = Mathf.Clamp(warningPulseSeconds, 0.05f, 10f);
        }

        private static void ValidateValues(
            double maximumHealth,
            double movementSpeed,
            double stoppingDistance,
            double contactDamage,
            double contactCadenceSeconds,
            double simultaneousContactWindowSeconds,
            int moverColliderCapacity,
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
                stoppingDistance,
                0d,
                double.MaxValue,
                nameof(stoppingDistance),
                true);
            RequireFiniteRange(
                contactDamage,
                0d,
                HardMaximumContactDamage,
                nameof(contactDamage),
                false);
            RequireFiniteRange(
                contactCadenceSeconds,
                0d,
                HardMaximumContactCadenceSeconds,
                nameof(contactCadenceSeconds),
                false);
            RequireFiniteRange(
                simultaneousContactWindowSeconds,
                0d,
                contactCadenceSeconds,
                nameof(simultaneousContactWindowSeconds),
                true);
            RequireFiniteRange(
                warningPulseSeconds,
                0d,
                10d,
                nameof(warningPulseSeconds),
                false);

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

using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.BlasterTurret
{
    /// <summary>
    /// One scene-level bridge between placed turrets and the player's combat runtime.
    /// The player/bootstrap configures this once; every authored turret registers itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlasterTurretSceneContext2D : MonoBehaviour
    {
        private readonly Dictionary<StableId, BlasterTurretPackage> turrets =
            new Dictionary<StableId, BlasterTurretPackage>();

        private EnemyTarget2DAdapter playerTarget;
        private CombatHit2DAdapter playerShotHitAdapter;
        private double playerShotDamage;
        private double turretShotDamage;
        private Action<double> fallbackPlayerDamage;
        private long damageOrder;
        private bool configured;

        public bool IsConfigured => configured;

        public EnemyTarget2DAdapter PlayerTarget => playerTarget;

        public int RegisteredTurretCount => turrets.Count;

        public void Configure(
            EnemyTarget2DAdapter configuredPlayerTarget,
            CombatHit2DAdapter configuredPlayerShotHitAdapter,
            double configuredPlayerShotDamage,
            double configuredTurretShotDamage,
            Action<double> playerDamageFallback = null)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Blaster Turret scene context is already configured.");
            }

            if (configuredPlayerTarget == null
                || !configuredPlayerTarget.IsConfigured
                || configuredPlayerTarget.TargetCollider == null)
            {
                throw new ArgumentException(
                    "A configured player target is required.",
                    nameof(configuredPlayerTarget));
            }

            if (configuredPlayerShotHitAdapter == null)
            {
                throw new ArgumentNullException(nameof(configuredPlayerShotHitAdapter));
            }

            RequirePositiveFinite(configuredPlayerShotDamage, nameof(configuredPlayerShotDamage));
            RequirePositiveFinite(configuredTurretShotDamage, nameof(configuredTurretShotDamage));

            playerTarget = configuredPlayerTarget;
            playerShotHitAdapter = configuredPlayerShotHitAdapter;
            playerShotDamage = configuredPlayerShotDamage;
            turretShotDamage = configuredTurretShotDamage;
            fallbackPlayerDamage = playerDamageFallback;
            playerShotHitAdapter.HitTranslated += HandlePlayerShotHit;
            configured = true;

            BlasterTurretAuthoring2D[] authoredTurrets =
                FindObjectsByType<BlasterTurretAuthoring2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < authoredTurrets.Length; index++)
            {
                authoredTurrets[index].TryConfigureNow();
            }
        }

        public bool RegisterTurret(StableId actorId, BlasterTurretPackage package)
        {
            if (!configured
                || actorId == null
                || package == null
                || !package.IsConfigured
                || package.EnemyCollider == null
                || package.HitAdapter == null)
            {
                return false;
            }

            BlasterTurretPackage existing;
            if (turrets.TryGetValue(actorId, out existing))
            {
                return existing == package;
            }

            CombatHit2DTargetRegistrationStatus registration =
                playerShotHitAdapter.RegisterTarget(package.EnemyCollider, actorId);
            if (registration != CombatHit2DTargetRegistrationStatus.Registered
                && registration != CombatHit2DTargetRegistrationStatus.AlreadyRegistered)
            {
                return false;
            }

            turrets.Add(actorId, package);
            package.HitAdapter.HitTranslated += HandleTurretShotHit;
            return true;
        }

        public void UnregisterTurret(StableId actorId, BlasterTurretPackage package)
        {
            if (actorId == null || package == null)
            {
                return;
            }

            BlasterTurretPackage existing;
            if (!turrets.TryGetValue(actorId, out existing) || existing != package)
            {
                return;
            }

            turrets.Remove(actorId);
            if (playerShotHitAdapter != null && package.EnemyCollider != null)
            {
                playerShotHitAdapter.UnregisterTarget(package.EnemyCollider, actorId);
            }

            if (package.HitAdapter != null)
            {
                package.HitAdapter.HitTranslated -= HandleTurretShotHit;
            }
        }

        private void HandlePlayerShotHit(CombatHit2DTranslationResult translation)
        {
            if (translation == null
                || translation.Status != CombatHit2DTranslationStatus.Confirmed
                || translation.Message == null)
            {
                return;
            }

            BlasterTurretPackage package;
            if (!turrets.TryGetValue(translation.Message.TargetId, out package)
                || package == null
                || package.TargetAdapter == null)
            {
                return;
            }

            damageOrder++;
            package.TargetAdapter.ApplyHit(
                translation.Message,
                playerShotDamage,
                damageOrder);
        }

        private void HandleTurretShotHit(CombatHit2DTranslationResult translation)
        {
            if (translation == null
                || translation.Status != CombatHit2DTranslationStatus.Confirmed
                || translation.Message == null)
            {
                return;
            }

            if (playerTarget != null && playerTarget.CanReceiveEnemyDamage)
            {
                damageOrder++;
                playerTarget.ApplyHit(
                    translation.Message,
                    turretShotDamage,
                    damageOrder);
                return;
            }

            fallbackPlayerDamage?.Invoke(turretShotDamage);
        }

        private void OnDestroy()
        {
            if (playerShotHitAdapter != null)
            {
                playerShotHitAdapter.HitTranslated -= HandlePlayerShotHit;
            }

            foreach (KeyValuePair<StableId, BlasterTurretPackage> binding in turrets)
            {
                if (binding.Value != null && binding.Value.HitAdapter != null)
                {
                    binding.Value.HitAdapter.HitTranslated -= HandleTurretShotHit;
                }
            }

            turrets.Clear();
            configured = false;
        }

        private static void RequirePositiveFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}

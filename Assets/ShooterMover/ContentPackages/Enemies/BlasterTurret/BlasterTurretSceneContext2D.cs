using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.BlasterTurret
{
    /// <summary>
    /// Package-specific combat ports co-located with one generic OBJ-001 gameplay
    /// scope. Turrets resolve this component through that explicit/parent scope and
    /// register themselves; the context never scans a scene for concrete packages.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplaySceneScope2D))]
    public sealed class BlasterTurretSceneContext2D : MonoBehaviour
    {
        private readonly Dictionary<StableId, BlasterTurretPackage> turrets =
            new Dictionary<StableId, BlasterTurretPackage>();

        private GameplaySceneScope2D gameplayScope;
        private EnemyTarget2DAdapter playerTarget;
        private CombatHit2DAdapter playerShotHitAdapter;
        private double playerShotDamage;
        private double turretShotDamage;
        private Action<double> fallbackPlayerDamage;
        private long damageOrder;
        private bool configured;
        private string lastRegistrationDiagnostic = string.Empty;

        public bool IsConfigured => configured;

        public GameplaySceneScope2D GameplayScope => gameplayScope;

        public EnemyTarget2DAdapter PlayerTarget => playerTarget;

        public int RegisteredTurretCount => turrets.Count;

        public string LastRegistrationDiagnostic =>
            lastRegistrationDiagnostic ?? string.Empty;

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

            gameplayScope = GetComponent<GameplaySceneScope2D>();
            if (gameplayScope == null || !gameplayScope.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Blaster Turret scene context requires a configured co-located "
                    + nameof(GameplaySceneScope2D)
                    + ".");
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

            RequirePositiveFinite(
                configuredPlayerShotDamage,
                nameof(configuredPlayerShotDamage));
            RequirePositiveFinite(
                configuredTurretShotDamage,
                nameof(configuredTurretShotDamage));

            playerTarget = configuredPlayerTarget;
            playerShotHitAdapter = configuredPlayerShotHitAdapter;
            playerShotDamage = configuredPlayerShotDamage;
            turretShotDamage = configuredTurretShotDamage;
            fallbackPlayerDamage = playerDamageFallback;
            playerShotHitAdapter.HitTranslated += HandlePlayerShotHit;
            damageOrder = 0L;
            lastRegistrationDiagnostic = string.Empty;
            configured = true;
        }

        public bool RegisterTurret(StableId actorId, BlasterTurretPackage package)
        {
            if (!configured)
            {
                return FailRegistration(
                    "Blaster Turret scene context is not configured.");
            }

            if (actorId == null)
            {
                return FailRegistration(
                    "Blaster Turret registration requires a canonical actor ID.");
            }

            if (package == null
                || !package.IsConfigured
                || package.EnemyCollider == null
                || package.HitAdapter == null)
            {
                return FailRegistration(
                    "Blaster Turret registration requires a fully configured package.");
            }

            if (package.TargetAdapter == null
                || package.TargetAdapter.TargetId == null
                || !actorId.Equals(package.TargetAdapter.TargetId))
            {
                return FailRegistration(
                    "Blaster Turret package identity does not match its authored actor ID.");
            }

            BlasterTurretPackage existing;
            if (turrets.TryGetValue(actorId, out existing))
            {
                if (existing == package)
                {
                    lastRegistrationDiagnostic =
                        "Exact Blaster Turret combat registration retry produced no change.";
                    return true;
                }

                return FailRegistration(
                    "Duplicate Blaster Turret actor ID '"
                    + actorId
                    + "' was rejected by gameplay scope '"
                    + gameplayScope.ScopeId
                    + "'. Existing object: '"
                    + existing.name
                    + "'; attempted object: '"
                    + package.name
                    + "'.");
            }

            CombatHit2DTargetRegistrationStatus registration =
                playerShotHitAdapter.RegisterTarget(package.EnemyCollider, actorId);
            if (registration != CombatHit2DTargetRegistrationStatus.Registered
                && registration != CombatHit2DTargetRegistrationStatus.AlreadyRegistered)
            {
                return FailRegistration(
                    "The player-shot hit adapter rejected Blaster Turret '"
                    + actorId
                    + "': "
                    + registration
                    + ".");
            }

            turrets.Add(actorId, package);
            package.HitAdapter.HitTranslated += HandleTurretShotHit;
            lastRegistrationDiagnostic =
                "Blaster Turret registered in gameplay scope '"
                + gameplayScope.ScopeId
                + "'.";
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

        private bool FailRegistration(string diagnostic)
        {
            lastRegistrationDiagnostic = string.IsNullOrEmpty(diagnostic)
                ? "Blaster Turret combat registration failed without a diagnostic."
                : diagnostic;
            return false;
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

                if (playerShotHitAdapter != null
                    && binding.Value != null
                    && binding.Value.EnemyCollider != null)
                {
                    playerShotHitAdapter.UnregisterTarget(
                        binding.Value.EnemyCollider,
                        binding.Key);
                }
            }

            turrets.Clear();
            configured = false;
            gameplayScope = null;
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

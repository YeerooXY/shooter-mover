from pathlib import Path
import textwrap

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def write(path, content):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8", newline="\n")


def replace_once(text, old, new, label):
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


controller_path = "Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs"
controller = read(controller_path)
controller = replace_once(
    controller,
    "using System.Collections.Generic;\n",
    "using System.Collections.Generic;\nusing System.Globalization;\n",
    "controller culture using")
controller = replace_once(
    controller,
    "using ShooterMover.Domain.Movement;\n",
    "using ShooterMover.Domain.Movement;\nusing ShooterMover.GameplayEntities;\n",
    "controller gameplay entity using")
controller = replace_once(
    controller,
    "using ShooterMover.UnityAdapters.Physics;\n",
    "using ShooterMover.UnityAdapters.Physics;\nusing ShooterMover.UnityAdapters.Players;\n",
    "controller player runtime using")
controller = replace_once(
    controller,
    "    /// VS-007's scene-local composition root. It wires accepted packages together and\n"
    "    /// owns only disposable prototype presentation plus one session-only player vital.\n",
    "    /// VS-007's scene-local composition root. It wires accepted packages together and\n"
    "    /// projects accepted player-authority lifecycle changes into disposable scene state.\n",
    "controller summary")
controller = replace_once(
    controller,
    "        [SerializeField] private bool grayscale;\n",
    "        [SerializeField] private bool grayscale;\n"
    "        [Header(\"Live player identity\")]\n"
    "        [SerializeField] private string playerRunParticipantIdText =\n"
    "            \"participant.stage1-player\";\n"
    "        [SerializeField] private string playerCharacterIdText = \"character.striker\";\n"
    "        [SerializeField] private string playerFactionIdText = \"faction.player\";\n",
    "controller identity fields")
controller = replace_once(
    controller,
    "        private Vector3 playerSpawn;\n        private int playerHealth;\n        private long restartGeneration;\n",
    "        private Vector3 playerSpawn;\n"
    "        private Stage1PlayerLiveAuthorityAdapterV1 playerLiveAuthority;\n"
    "        private long restartGeneration;\n",
    "controller live authority field")
controller = replace_once(
    controller,
    "        public bool ReducedEffectsEnabled => reducedEffects;\n"
    "        public bool IsInitialized => initialized;\n"
    "        public bool IsSessionActive => sessionActive;\n"
    "        public int PlayerHealth => playerHealth;\n"
    "        public long RestartGeneration => restartGeneration;\n"
    "        public Transform PlayerTransform => playerTransform;\n",
    "        public bool ReducedEffectsEnabled => reducedEffects;\n"
    "        public bool IsInitialized => initialized;\n"
    "        public bool IsSessionActive => sessionActive;\n"
    "        public bool IsPlayerGameplayActive => sessionActive\n"
    "            && movementLifecycle != null\n"
    "            && movementLifecycle.IsRunning\n"
    "            && playerCollider != null\n"
    "            && playerCollider.enabled;\n"
    "        public int PlayerHealth\n"
    "        {\n"
    "            get\n"
    "            {\n"
    "                PlayerHudHealthSnapshot health = playerLiveAuthority != null\n"
    "                    && playerLiveAuthority.IsInitialized\n"
    "                        ? playerLiveAuthority.ExportHudHealth()\n"
    "                        : null;\n"
    "                return health == null\n"
    "                    ? StartingPlayerHealth\n"
    "                    : Mathf.RoundToInt((float)health.CurrentHealth);\n"
    "            }\n"
    "        }\n"
    "        public long RestartGeneration => playerLiveAuthority != null\n"
    "            && playerLiveAuthority.IsInitialized\n"
    "                ? playerLiveAuthority.ExportSnapshot().Player.LifecycleGeneration\n"
    "                : restartGeneration;\n"
    "        public StableId PlayerRunParticipantId =>\n"
    "            StableId.Parse(playerRunParticipantIdText);\n"
    "        public StableId PlayerCharacterId => StableId.Parse(playerCharacterIdText);\n"
    "        public StableId PlayerFactionId => StableId.Parse(playerFactionIdText);\n"
    "        public Stage1PlayerLiveAuthorityAdapterV1 PlayerLiveAuthority =>\n"
    "            playerLiveAuthority;\n"
    "        public Transform PlayerTransform => playerTransform;\n"
    "        public Rigidbody2D PlayerBody => playerBody;\n"
    "        public Collider2D PlayerCollider => playerCollider;\n"
    "        public EnemyTarget2DAdapter PlayerTargetAdapter => playerTargetAdapter;\n"
    "        public VoidHazardTarget2D PlayerVoidTarget => playerVoidTarget;\n"
    "        public MovementActorLifecycle PlayerMovementLifecycle => movementLifecycle;\n"
    "        public MovementThrusterTuningProfile PlayerMovementTuning => movementTuning;\n"
    "        public InputActionAsset PlayerInputActions => inputActions;\n"
    "        public TrailRenderer PlayerBoostTrail => playerBoostTrail;\n",
    "controller public player boundary")
controller = replace_once(
    controller,
    "        public VoidHazardPortResult RequestDamage(VoidHazardDamageRequest request)\n"
    "        {\n"
    "            if (request == null || playerTransform == null)\n"
    "            {\n"
    "                return VoidHazardPortResult.Rejected;\n"
    "            }\n\n"
    "            voidDamageCount++;\n"
    "            ApplyTurretProjectileDamageToPlayer(request.Amount);\n"
    "            return VoidHazardPortResult.Accepted;\n"
    "        }\n\n"
    "        public VoidHazardPortResult RequestInstantDeath(VoidHazardInstantDeathRequest request)\n"
    "        {\n"
    "            if (request == null || playerTransform == null)\n"
    "            {\n"
    "                return VoidHazardPortResult.Rejected;\n"
    "            }\n\n"
    "            voidDamageCount++;\n"
    "            playerHealth = 0;\n"
    "            return VoidHazardPortResult.Accepted;\n"
    "        }\n",
    "        public VoidHazardPortResult RequestDamage(VoidHazardDamageRequest request)\n"
    "        {\n"
    "            return playerLiveAuthority == null || !playerLiveAuthority.IsInitialized\n"
    "                ? VoidHazardPortResult.Rejected\n"
    "                : playerLiveAuthority.RequestDamage(request);\n"
    "        }\n\n"
    "        public VoidHazardPortResult RequestInstantDeath(VoidHazardInstantDeathRequest request)\n"
    "        {\n"
    "            return playerLiveAuthority == null || !playerLiveAuthority.IsInitialized\n"
    "                ? VoidHazardPortResult.Rejected\n"
    "                : playerLiveAuthority.RequestInstantDeath(request);\n"
    "        }\n",
    "controller void delegation")
controller = replace_once(
    controller,
    "        private void Awake()\n"
    "        {\n"
    "            ValidateSerializedDependencies();\n"
    "            BuildSession();\n"
    "            initialized = true;\n"
    "        }\n",
    "        private void Awake()\n"
    "        {\n"
    "            ValidateSerializedDependencies();\n"
    "            BuildSession();\n"
    "            initialized = true;\n"
    "            playerLiveAuthority = GetComponent<Stage1PlayerLiveAuthorityAdapterV1>();\n"
    "            if (playerLiveAuthority == null)\n"
    "            {\n"
    "                playerLiveAuthority =\n"
    "                    gameObject.AddComponent<Stage1PlayerLiveAuthorityAdapterV1>();\n"
    "            }\n"
    "            if (playerLiveAuthority == null || !playerLiveAuthority.IsInitialized)\n"
    "            {\n"
    "                throw new InvalidOperationException(\n"
    "                    \"Stage 1 failed to compose the live player authority.\");\n"
    "            }\n"
    "        }\n",
    "controller authority composition")
old_restart = """        public void QuickRestart()
        {
            restartGeneration = restartGeneration == long.MaxValue
                ? long.MaxValue
                : restartGeneration + 1L;
            playerHealth = StartingPlayerHealth;
            playerShotSequence = 0L;
            damageSequence = 0L;
            damageObserved = false;
            firingObserved = false;
            observedTurretShotSequence = 0L;
            observedTurretHitCount = 0;
            observedPlayerHitCount = 0;
            droidDamageOrder = 0L;
            arenaComplete = false;
            voidDamageCount = 0;
            selectedLoadout = Stage1WeaponLoadoutCatalog.Approved.DefaultFixture;
            sessionActive = shootingSandbox;
            nextBlasterShotTime = 0f;
            if (roomMissionLayout != null)
            {
                roomMissionLayout.Restart();
            }

            ClearShotTraces();
            playerBody.position = playerSpawn;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            movementLifecycle.RestartActor();
            if (playerBoostTrail != null)
            {
                playerBoostTrail.emitting = false;
                playerBoostTrail.Clear();
            }
            if (turretPackage != null)
            {
                turretPackage.RestartSession();
            }
            if (mobileBlasterDroid != null)
            {
                mobileBlasterDroid.RestartSession();
            }
            if (gameplayScope != null && gameplayScope.IsConfigured)
            {
                gameplayScope.RunRestart(restartGeneration);
            }
            if (roomMissionLayout != null)
            {
                RoomContentDefinition2D entry = FindRoomContent(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId);
                SwitchRoom(
                    entry.RoomStableId,
                    entry.ForwardEntryPosition,
                    true);
            }
            if (playerHitAdapter != null)
            {
                playerHitAdapter.ResetProcessedEvents();
            }
            if (loadoutSelector != null)
            {
                loadoutSelector.ResetForRestart();
                loadoutSelector.Hide();
            }
            combatHud.ResetPresentationForRestart(restartGeneration);
            cameraRig.Restart();
            RefreshHud();
            RefreshTurretPresentation();
            RefreshArenaFlow();
        }
"""
new_restart = """        public PlayerRuntimeRestartResult QuickRestart()
        {
            if (playerLiveAuthority == null || !playerLiveAuthority.IsInitialized)
            {
                return null;
            }

            return playerLiveAuthority.RequestRestart();
        }

        public bool ApplyAcceptedPlayerRestart(PlayerRuntimeRestartResult result)
        {
            if (result == null
                || result.Status != PlayerRuntimeRestartStatus.Applied
                || result.Snapshot == null
                || result.Snapshot.Player == null
                || result.Snapshot.Movement == null
                || result.Snapshot.Player.LifecycleGeneration
                    != result.Snapshot.Movement.Generation)
            {
                return false;
            }

            restartGeneration = result.Snapshot.Player.LifecycleGeneration;
            playerShotSequence = 0L;
            damageSequence = 0L;
            damageObserved = false;
            firingObserved = false;
            observedTurretShotSequence = 0L;
            observedTurretHitCount = 0;
            observedPlayerHitCount = 0;
            droidDamageOrder = 0L;
            arenaComplete = false;
            voidDamageCount = 0;
            selectedLoadout = Stage1WeaponLoadoutCatalog.Approved.DefaultFixture;
            sessionActive = shootingSandbox;
            nextBlasterShotTime = 0f;
            if (roomMissionLayout != null)
            {
                roomMissionLayout.Restart();
            }

            ClearShotTraces();
            CancelPlayerProjectiles();
            playerBody.position = playerSpawn;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            SetPlayerInputEnabled(true);
            if (playerCollider != null)
            {
                playerCollider.enabled = true;
            }
            if (movementLifecycle != null && !movementLifecycle.IsRunning)
            {
                movementLifecycle.StartActor();
            }
            if (playerBoostTrail != null)
            {
                playerBoostTrail.emitting = false;
                playerBoostTrail.Clear();
            }
            if (turretPackage != null)
            {
                turretPackage.RestartSession();
            }
            if (mobileBlasterDroid != null)
            {
                mobileBlasterDroid.RestartSession();
            }
            if (gameplayScope != null && gameplayScope.IsConfigured)
            {
                gameplayScope.RunRestart(restartGeneration);
            }
            if (roomMissionLayout != null)
            {
                RoomContentDefinition2D entry = FindRoomContent(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId);
                SwitchRoom(
                    entry.RoomStableId,
                    entry.ForwardEntryPosition,
                    true);
            }
            if (playerHitAdapter != null)
            {
                playerHitAdapter.ResetProcessedEvents();
            }
            if (loadoutSelector != null)
            {
                loadoutSelector.ResetForRestart();
                loadoutSelector.Hide();
            }
            combatHud.ResetPresentationForRestart(restartGeneration);
            cameraRig.Restart();
            RefreshHud();
            RefreshTurretPresentation();
            RefreshArenaFlow();
            return true;
        }

        public void ApplyPlayerDeathProjection(GameplayEntityDeathFact deathFact)
        {
            if (deathFact == null
                || playerTargetAdapter == null
                || deathFact.TargetActorId != playerTargetAdapter.TargetId)
            {
                return;
            }

            sessionActive = false;
            nextBlasterShotTime = float.PositiveInfinity;
            SetPlayerInputEnabled(false);
            if (movementLifecycle != null)
            {
                movementLifecycle.StopActor();
            }
            if (playerCollider != null)
            {
                playerCollider.enabled = false;
            }
            if (playerBody != null)
            {
                playerBody.linearVelocity = Vector2.zero;
                playerBody.angularVelocity = 0f;
            }
            if (playerBoostTrail != null)
            {
                playerBoostTrail.emitting = false;
                playerBoostTrail.Clear();
            }
            CancelPlayerProjectiles();
        }

        public void SetPlayerInputEnabled(bool enabled)
        {
            if (inputActions == null)
            {
                return;
            }

            if (enabled)
            {
                inputActions.Enable();
            }
            else
            {
                inputActions.Disable();
            }
        }

        public void ObserveAcceptedVoidDamage()
        {
            if (voidDamageCount < int.MaxValue)
            {
                voidDamageCount++;
            }
        }
"""
controller = replace_once(controller, old_restart, new_restart, "controller restart ownership")
controller = replace_once(
    controller,
    "            snapshot = new GeneralCombatHudSnapshot(\n"
    "                new VitalState(playerHealth, StartingPlayerHealth, 0d, 0d),\n"
    "                thrusterReader == null ? null : thrusterReader.ReadSnapshot(),\n",
    "            PlayerRuntimeSnapshot live = playerLiveAuthority != null\n"
    "                && playerLiveAuthority.IsInitialized\n"
    "                    ? playerLiveAuthority.ExportSnapshot()\n"
    "                    : null;\n"
    "            snapshot = new GeneralCombatHudSnapshot(\n"
    "                live == null\n"
    "                    ? new VitalState(StartingPlayerHealth, StartingPlayerHealth, 0d, 0d)\n"
    "                    : live.Player.VitalState,\n"
    "                live == null\n"
    "                    ? (thrusterReader == null ? null : thrusterReader.ReadSnapshot())\n"
    "                    : live.Movement.ThrusterStatus,\n",
    "controller immutable HUD vital")
controller = replace_once(
    controller,
    "                reducedEffects,\n                restartGeneration);\n",
    "                reducedEffects,\n"
    "                live == null\n"
    "                    ? restartGeneration\n"
    "                    : live.Player.LifecycleGeneration);\n",
    "controller immutable HUD generation")
controller = replace_once(
    controller,
    "        private void BuildSession()\n        {\n            playerHealth = StartingPlayerHealth;\n",
    "        private void BuildSession()\n        {\n",
    "controller remove legacy health initialization")
controller = replace_once(
    controller,
    "                PlayerShotDamage,\n                TurretShotDamage,\n                ApplyTurretProjectileDamageToPlayer);\n",
    "                PlayerShotDamage,\n                TurretShotDamage,\n                null);\n",
    "controller remove turret fallback")
controller = replace_once(
    controller,
    "            GUI.Label(new Rect(30f, 51f, 210f, 20f),\n"
    "                \"HP \" + playerHealth + \"/\" + StartingPlayerHealth,\n"
    "                compactBodyStyle);\n",
    "            PlayerHudHealthSnapshot playerHud = playerLiveAuthority != null\n"
    "                && playerLiveAuthority.IsInitialized\n"
    "                    ? playerLiveAuthority.ExportHudHealth()\n"
    "                    : null;\n"
    "            double displayedHealth = playerHud == null\n"
    "                ? StartingPlayerHealth\n"
    "                : playerHud.CurrentHealth;\n"
    "            double displayedMaximum = playerHud == null\n"
    "                ? StartingPlayerHealth\n"
    "                : playerHud.MaximumHealth;\n"
    "            GUI.Label(new Rect(30f, 51f, 210f, 20f),\n"
    "                \"HP \" + FormatHealth(displayedHealth)\n"
    "                    + \"/\" + FormatHealth(displayedMaximum),\n"
    "                compactBodyStyle);\n",
    "controller visible HUD authority snapshot")
controller = replace_once(
    controller,
    "        private void ApplyTurretProjectileDamageToPlayer(double damage)\n"
    "        {\n"
    "            playerHealth = Mathf.Max(0, playerHealth - Mathf.RoundToInt((float)damage));\n"
    "        }\n\n",
    "",
    "controller remove direct health mutation")
controller = replace_once(
    controller,
    "        private Vector2 ReadReticleViewport()\n",
    "        private static string FormatHealth(double value)\n"
    "        {\n"
    "            return value.ToString(\"0.##\", CultureInfo.InvariantCulture);\n"
    "        }\n\n"
    "        private void CancelPlayerProjectiles()\n"
    "        {\n"
    "            BoundedProjectile2D[] projectiles =\n"
    "                FindObjectsByType<BoundedProjectile2D>(\n"
    "                    FindObjectsInactive.Include,\n"
    "                    FindObjectsSortMode.None);\n"
    "            for (int index = 0; index < projectiles.Length; index++)\n"
    "            {\n"
    "                BoundedProjectile2D projectile = projectiles[index];\n"
    "                if (projectile == null\n"
    "                    || !projectile.IsInitialized\n"
    "                    || projectile.IsComplete\n"
    "                    || (!string.Equals(projectile.name, \"PlayerWeaponShot\",\n"
    "                            StringComparison.Ordinal)\n"
    "                        && !string.Equals(projectile.name, \"PlayerBlasterShot\",\n"
    "                            StringComparison.Ordinal)))\n"
    "                {\n"
    "                    continue;\n"
    "                }\n"
    "                projectile.Cancel();\n"
    "            }\n"
    "        }\n\n"
    "        private Vector2 ReadReticleViewport()\n",
    "controller projectile cancellation helpers")
controller = replace_once(
    controller,
    "            if (roomPresentationPrefab == null\n"
    "                || blasterShotSprite == null\n"
    "                || turretPresentationPrefab == null\n"
    "                || roomContentDefinitions == null\n"
    "                || roomContentDefinitions.Length == 0)\n",
    "            StableId parsedIdentity;\n"
    "            bool validPlayerIdentity =\n"
    "                StableId.TryParse(playerRunParticipantIdText, out parsedIdentity)\n"
    "                && StableId.TryParse(playerCharacterIdText, out parsedIdentity)\n"
    "                && StableId.TryParse(playerFactionIdText, out parsedIdentity);\n"
    "            if (roomPresentationPrefab == null\n"
    "                || blasterShotSprite == null\n"
    "                || turretPresentationPrefab == null\n"
    "                || roomContentDefinitions == null\n"
    "                || roomContentDefinitions.Length == 0\n"
    "                || !validPlayerIdentity)\n",
    "controller identity validation")
controller = replace_once(
    controller,
    "                    + (roomContentDefinitions == null\n"
    "                        ? 0\n"
    "                        : roomContentDefinitions.Length));\n",
    "                    + (roomContentDefinitions == null\n"
    "                        ? 0\n"
    "                        : roomContentDefinitions.Length)\n"
    "                    + \" player-identity=\" + validPlayerIdentity);\n",
    "controller identity diagnostic")
if "playerHealth" in controller:
    raise RuntimeError("controller still contains legacy mutable playerHealth")
write(controller_path, controller)


droid_path = "Assets/ShooterMover/ContentPackages/Enemies/MobileBlasterDroid/MobileBlasterDroidRuntime2D.cs"
droid = read(droid_path)
droid = replace_once(
    droid,
    "        public WeaponMount2DAdapter WeaponMount { get { return weaponMount; } }\n"
    "        public Rigidbody2D EnemyBody { get { return enemyBody; } }\n",
    "        public WeaponMount2DAdapter WeaponMount { get { return weaponMount; } }\n"
    "        public CombatHit2DAdapter HitAdapter { get { return hitAdapter; } }\n"
    "        public Rigidbody2D EnemyBody { get { return enemyBody; } }\n",
    "droid typed hit port")
write(droid_path, droid)


void_target_path = "Assets/ShooterMover/ContentPackages/Environment/VoidHazards/VoidHazardTarget2D.cs"
void_target = read(void_target_path)
void_target = replace_once(
    void_target,
    "        public bool TryGetCombatPort(out IVoidHazardCombatPort port)\n"
    "        {\n"
    "            port = combatPort as IVoidHazardCombatPort;\n"
    "            return port != null;\n"
    "        }\n",
    "        public bool TryGetCombatPort(out IVoidHazardCombatPort port)\n"
    "        {\n"
    "            port = combatPort as IVoidHazardCombatPort;\n"
    "            return port != null;\n"
    "        }\n\n"
    "        public bool BindCombatPort(MonoBehaviour configuredCombatPort)\n"
    "        {\n"
    "            if (!(configuredCombatPort is IVoidHazardCombatPort))\n"
    "            {\n"
    "                return false;\n"
    "            }\n"
    "            combatPort = configuredCombatPort;\n"
    "            return true;\n"
    "        }\n",
    "void typed rebind port")
write(void_target_path, void_target)


adapter_path = "Assets/ShooterMover/TestSupport/VisibleSlice/Stage1PlayerLiveAuthorityAdapterV1.cs"
adapter = r'''using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.ContentPackages.Enemies.BlasterTurret;
using ShooterMover.ContentPackages.Enemies.MobileBlasterDroid;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.UI.VisibleSliceGeneralCombatHud;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Players;
using UnityEngine;

namespace ShooterMover.TestSupport.VisibleSlice
{
    /// <summary>
    /// Typed Stage 1 composition boundary. PlayerRuntimeComposition owns player health,
    /// healing, death and generation; this component only translates accepted Unity facts
    /// and projects accepted lifecycle changes back into the retained scene.
    /// </summary>
    [DefaultExecutionOrder(10100)]
    [DisallowMultipleComponent]
    public sealed class Stage1PlayerLiveAuthorityAdapterV1 :
        MonoBehaviour,
        IGeneralCombatHudStateSource,
        IVoidHazardCombatPort
    {
        private static readonly StableId DroidParticipantId =
            StableId.Parse("participant.stage1-mobile-droid");
        private static readonly StableId TurretParticipantId =
            StableId.Parse("participant.stage1-blaster-turret");
        private static readonly StableId EnvironmentParticipantId =
            StableId.Parse("participant.stage1-environment");

        private Stage1VisibleSliceController controller;
        private PlayerRuntimeCompositionRoot compositionRoot;
        private PlayerRuntimeComposition runtime;
        private MovementActorPlayerRuntimeAdapter movement;
        private Stage1PlayerPresentationRuntimeV1 presentation;
        private Stage1PlayerInputRuntimeV1 input;
        private Stage1PlayerAttributionResolverV1 attribution;
        private Stage1PlayerRunCoordinatorV1 runCoordinator;
        private CombatHit2DAdapter turretHitAdapter;
        private CombatHit2DAdapter droidHitAdapter;
        private VoidHazardTarget2D voidTarget;
        private bool initialized;
        private bool disposed;

        public bool IsInitialized
        {
            get { return initialized && !disposed && runtime != null; }
        }

        public bool IsPlayerGameplayActive
        {
            get { return controller != null && controller.IsPlayerGameplayActive; }
        }

        public int DeathFactCount
        {
            get { return runCoordinator == null ? 0 : runCoordinator.DeathFacts.Count; }
        }

        public GameplayEntityDeathFact LastDeathFact
        {
            get { return runCoordinator == null ? null : runCoordinator.LastDeathFact; }
        }

        public PlayerRuntimeSnapshot ExportSnapshot()
        {
            EnsureInitialized();
            return runtime.ExportSnapshot();
        }

        public PlayerHudHealthSnapshot ExportHudHealth()
        {
            EnsureInitialized();
            return runtime.ExportHudHealth();
        }

        public GeneralCombatHudSnapshot ExportVisibleHudSnapshot()
        {
            GeneralCombatHudSnapshot snapshot;
            if (!TryRead(out snapshot) || snapshot == null)
            {
                throw new InvalidOperationException(
                    "The Stage 1 immutable HUD snapshot is unavailable.");
            }
            return snapshot;
        }

        public DamageReceiverResult ApplyDamage(PlayerDamageRequest request)
        {
            EnsureInitialized();
            return runtime.ApplyDamage(request);
        }

        public PlayerActorHealingResult ApplyHealing(PlayerHealingRequest request)
        {
            EnsureInitialized();
            return runtime.ApplyHealing(request);
        }

        public DamageReceiverResult ApplyProjectileDamage(
            StableId eventId,
            StableId sourceActorId,
            StableId targetActorId,
            double amount,
            CombatChannel channel,
            long emissionGeneration)
        {
            EnsureInitialized();
            return runtime.ApplyDamage(
                new PlayerDamageRequest(
                    eventId,
                    sourceActorId,
                    null,
                    targetActorId,
                    amount,
                    channel,
                    emissionGeneration));
        }

        public PlayerRuntimeRestartResult RequestRestart()
        {
            EnsureInitialized();
            PlayerRuntimeSnapshot before = runtime.ExportSnapshot();
            long retiring = before.Player.LifecycleGeneration;
            long replacement = retiring == long.MaxValue
                ? long.MaxValue
                : retiring + 1L;
            PlayerRuntimeRestartCommand command = new PlayerRuntimeRestartCommand(
                StableId.Create(
                    "operation",
                    "stage1-player-restart-g"
                        + replacement.ToString(CultureInfo.InvariantCulture)),
                before.Player.ActorInstanceId,
                retiring,
                replacement);
            PlayerRuntimeRestartResult result = runtime.Restart(command);
            if (result.Status == PlayerRuntimeRestartStatus.Applied
                && !controller.ApplyAcceptedPlayerRestart(result))
            {
                throw new InvalidOperationException(
                    "The scene rejected an accepted player-authority restart projection.");
            }
            return result;
        }

        public bool TryRead(out GeneralCombatHudSnapshot snapshot)
        {
            snapshot = null;
            return IsInitialized
                && controller != null
                && controller.TryRead(out snapshot)
                && snapshot != null;
        }

        public VoidHazardPortResult RequestDamage(VoidHazardDamageRequest request)
        {
            if (!IsInitialized || request == null)
            {
                return VoidHazardPortResult.Rejected;
            }

            long generation;
            if (!TryResolveLifecycleGeneration(request.EventId, out generation))
            {
                return VoidHazardPortResult.Rejected;
            }

            DamageReceiverResult result = runtime.ApplyDamage(
                new PlayerDamageRequest(
                    request.EventId,
                    request.HazardId,
                    null,
                    request.TargetId,
                    request.Amount,
                    request.Channel,
                    generation));
            if (result.Status == DamageReceiverStatus.Applied)
            {
                controller.ObserveAcceptedVoidDamage();
            }
            return MapVoidResult(result);
        }

        public VoidHazardPortResult RequestInstantDeath(
            VoidHazardInstantDeathRequest request)
        {
            if (!IsInitialized || request == null)
            {
                return VoidHazardPortResult.Rejected;
            }

            long generation;
            if (!TryResolveLifecycleGeneration(request.EventId, out generation))
            {
                return VoidHazardPortResult.Rejected;
            }

            PlayerActorSnapshot player = runtime.ExportSnapshot().Player;
            DamageReceiverResult result = runtime.ApplyDamage(
                new PlayerDamageRequest(
                    request.EventId,
                    request.HazardId,
                    null,
                    request.TargetId,
                    player.MaximumHealth,
                    request.Channel,
                    generation));
            if (result.Status == DamageReceiverStatus.Applied)
            {
                controller.ObserveAcceptedVoidDamage();
            }
            return MapVoidResult(result);
        }

        public static bool TryResolveLifecycleGeneration(
            StableId eventId,
            out long generation)
        {
            generation = -1L;
            if (eventId == null || string.IsNullOrEmpty(eventId.Value))
            {
                return false;
            }

            string value = eventId.Value;
            string[] tokens =
            {
                "generation-",
                "attempt-",
                "-g",
                ".g",
                "_g",
            };
            for (int index = 0; index < tokens.Length; index++)
            {
                int start = value.IndexOf(tokens[index], StringComparison.Ordinal);
                while (start >= 0)
                {
                    int digitStart = start + tokens[index].Length;
                    int digitEnd = digitStart;
                    while (digitEnd < value.Length && char.IsDigit(value[digitEnd]))
                    {
                        digitEnd++;
                    }
                    if (digitEnd > digitStart
                        && long.TryParse(
                            value.Substring(digitStart, digitEnd - digitStart),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out generation)
                        && generation >= 0L)
                    {
                        return true;
                    }
                    start = value.IndexOf(
                        tokens[index],
                        start + 1,
                        StringComparison.Ordinal);
                }
            }
            generation = -1L;
            return false;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (IsInitialized)
            {
                runtime.RefreshContinuousPresentation();
            }
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            controller = GetComponent<Stage1VisibleSliceController>();
            if (controller == null || !controller.IsInitialized)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 requires the initialized Stage 1 composition root.");
            }
            if (controller.PlayerMovementLifecycle == null
                || controller.PlayerMovementTuning == null
                || controller.PlayerTargetAdapter == null
                || !controller.PlayerTargetAdapter.IsConfigured)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 requires configured movement and target adapters.");
            }

            movement = new MovementActorPlayerRuntimeAdapter(
                controller.PlayerMovementLifecycle,
                controller.PlayerMovementTuning);
            presentation = new Stage1PlayerPresentationRuntimeV1(
                controller.PlayerBoostTrail);
            input = new Stage1PlayerInputRuntimeV1(controller);
            attribution = new Stage1PlayerAttributionResolverV1();
            runCoordinator = new Stage1PlayerRunCoordinatorV1(controller);

            PlayerMovementSnapshot initialMovement = movement.ExportSnapshot();
            PlayerActorDefinition definition = new PlayerActorDefinition(
                controller.PlayerTargetAdapter.TargetId,
                controller.PlayerRunParticipantId,
                controller.PlayerCharacterId,
                controller.PlayerFactionId,
                Stage1VisibleSliceController.StartingPlayerHealth,
                initialMovement.Generation);
            compositionRoot = new PlayerRuntimeCompositionRoot();
            PlayerRuntimeConstructionResult construction =
                compositionRoot.TryConstruct(
                    new PlayerRuntimeConfiguration(definition),
                    new PlayerRuntimeAttachments(
                        movement,
                        presentation,
                        input,
                        attribution,
                        runCoordinator));
            if (!construction.IsConstructed)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 composition failed: "
                    + construction.Status
                    + "/"
                    + construction.RejectionCode
                    + "/"
                    + construction.ActorRejectionCode
                    + ".");
            }
            runtime = construction.Runtime;
            BindTrustedAttribution();
            BindDamageRoutes();
            initialized = true;
        }

        private void BindTrustedAttribution()
        {
            if (controller.MobileBlasterDroid != null
                && controller.MobileBlasterDroid.EnemyTarget != null)
            {
                attribution.Register(
                    controller.MobileBlasterDroid.EnemyTarget.TargetId,
                    DroidParticipantId);
            }
            if (controller.TurretPackage != null
                && controller.TurretPackage.TargetAdapter != null)
            {
                attribution.Register(
                    controller.TurretPackage.TargetAdapter.TargetId,
                    TurretParticipantId);
            }
            if (controller.VoidHazard != null
                && controller.VoidHazard.RestartParticipantId != null)
            {
                attribution.Register(
                    controller.VoidHazard.RestartParticipantId,
                    EnvironmentParticipantId);
            }
        }

        private void BindDamageRoutes()
        {
            if (controller.TurretPackage == null
                || controller.TurretPackage.HitAdapter == null
                || controller.MobileBlasterDroid == null
                || controller.MobileBlasterDroid.HitAdapter == null)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 could not bind enemy projectile facts.");
            }

            turretHitAdapter = controller.TurretPackage.HitAdapter;
            droidHitAdapter = controller.MobileBlasterDroid.HitAdapter;
            turretHitAdapter.HitTranslated += HandleTurretHit;
            droidHitAdapter.HitTranslated += HandleDroidHit;

            voidTarget = controller.PlayerVoidTarget;
            if (voidTarget == null || !voidTarget.BindCombatPort(this))
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 could not bind the typed void combat port.");
            }

            if (controller.CombatHud == null)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 could not resolve the Stage 1 HUD.");
            }
            controller.CombatHud.UnbindSources();
            controller.CombatHud.BindSources(this, null);
        }

        private void HandleTurretHit(CombatHit2DTranslationResult translation)
        {
            ApplyTranslatedDamage(
                translation,
                Stage1VisibleSliceController.TurretShotDamage);
        }

        private void HandleDroidHit(CombatHit2DTranslationResult translation)
        {
            ApplyTranslatedDamage(
                translation,
                BlasterMachineGunPackage.NormalDamage);
        }

        private void ApplyTranslatedDamage(
            CombatHit2DTranslationResult translation,
            double amount)
        {
            if (!IsInitialized
                || translation == null
                || translation.Status != CombatHit2DTranslationStatus.Confirmed
                || translation.Message == null)
            {
                return;
            }

            long generation;
            if (!TryResolveLifecycleGeneration(
                    translation.Message.EventId,
                    out generation))
            {
                return;
            }
            HitMessage hit = translation.Message;
            ApplyProjectileDamage(
                hit.EventId,
                hit.SourceId,
                hit.TargetId,
                amount,
                hit.Channel,
                generation);
        }

        private static VoidHazardPortResult MapVoidResult(
            DamageReceiverResult result)
        {
            if (result == null)
            {
                return VoidHazardPortResult.Rejected;
            }
            switch (result.Status)
            {
                case DamageReceiverStatus.Applied:
                    return VoidHazardPortResult.Accepted;
                case DamageReceiverStatus.Duplicate:
                    return VoidHazardPortResult.DuplicateNoChange;
                default:
                    return VoidHazardPortResult.Rejected;
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 is not initialized.");
            }
        }

        private void OnDestroy()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (turretHitAdapter != null)
            {
                turretHitAdapter.HitTranslated -= HandleTurretHit;
            }
            if (droidHitAdapter != null)
            {
                droidHitAdapter.HitTranslated -= HandleDroidHit;
            }
            if (voidTarget != null && controller != null)
            {
                voidTarget.BindCombatPort(controller);
            }
            if (controller != null && controller.CombatHud != null)
            {
                controller.CombatHud.UnbindSources();
                controller.CombatHud.BindSources(controller, null);
            }
            if (compositionRoot != null)
            {
                compositionRoot.Dispose();
            }
            runtime = null;
            initialized = false;
        }

        private sealed class Stage1PlayerPresentationRuntimeV1 :
            IPlayerPresentationRuntime
        {
            private readonly TrailRenderer boostTrail;
            private bool disposed;

            public Stage1PlayerPresentationRuntimeV1(TrailRenderer boostTrail)
            {
                this.boostTrail = boostTrail;
            }

            public void RefreshContinuousBoost(
                PlayerMovementSnapshot movementSnapshot)
            {
                if (disposed || movementSnapshot == null || boostTrail == null)
                {
                    return;
                }
                boostTrail.emitting = movementSnapshot.IsBoosting;
            }

            public void Restart(PlayerRuntimeSnapshot runtimeSnapshot)
            {
                if (disposed || boostTrail == null)
                {
                    return;
                }
                boostTrail.emitting = false;
                boostTrail.Clear();
            }

            public void Dispose()
            {
                disposed = true;
            }
        }

        private sealed class Stage1PlayerInputRuntimeV1 : IPlayerInputRuntime
        {
            private readonly Stage1VisibleSliceController controller;
            private PlayerInputOwnership ownership;
            private bool disposed;

            public Stage1PlayerInputRuntimeV1(
                Stage1VisibleSliceController controller)
            {
                this.controller = controller
                    ?? throw new ArgumentNullException(nameof(controller));
            }

            public bool TryAcquire(PlayerInputOwnership requested)
            {
                if (disposed || requested == null)
                {
                    return false;
                }
                if (ownership == null)
                {
                    ownership = requested;
                    controller.SetPlayerInputEnabled(true);
                    return true;
                }
                return ownership.Equals(requested);
            }

            public bool Release(PlayerInputOwnership requested)
            {
                if (disposed
                    || ownership == null
                    || requested == null
                    || !ownership.Equals(requested))
                {
                    return false;
                }
                ownership = null;
                controller.SetPlayerInputEnabled(false);
                return true;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;
                ownership = null;
                controller.SetPlayerInputEnabled(false);
            }
        }

        private sealed class Stage1PlayerAttributionResolverV1 :
            ITrustedPlayerAttributionResolver
        {
            private readonly Dictionary<StableId, StableId> participants =
                new Dictionary<StableId, StableId>();

            public void Register(StableId actorId, StableId runParticipantId)
            {
                if (actorId == null || runParticipantId == null)
                {
                    throw new ArgumentNullException(
                        actorId == null ? nameof(actorId) : nameof(runParticipantId));
                }
                StableId existing;
                if (participants.TryGetValue(actorId, out existing)
                    && existing != runParticipantId)
                {
                    throw new InvalidOperationException(
                        "A live source actor cannot change trusted participant identity.");
                }
                participants[actorId] = runParticipantId;
            }

            public StableId ResolveSourceRunParticipant(StableId sourceActorId)
            {
                if (sourceActorId == null)
                {
                    return null;
                }
                StableId participant;
                return participants.TryGetValue(sourceActorId, out participant)
                    ? participant
                    : null;
            }
        }

        private sealed class Stage1PlayerRunCoordinatorV1 : IPlayerRunCoordinator
        {
            private readonly Stage1VisibleSliceController controller;
            private readonly List<GameplayEntityDeathFact> deathFacts =
                new List<GameplayEntityDeathFact>();

            public Stage1PlayerRunCoordinatorV1(
                Stage1VisibleSliceController controller)
            {
                this.controller = controller
                    ?? throw new ArgumentNullException(nameof(controller));
            }

            public IReadOnlyList<GameplayEntityDeathFact> DeathFacts
            {
                get { return deathFacts.AsReadOnly(); }
            }

            public GameplayEntityDeathFact LastDeathFact
            {
                get
                {
                    return deathFacts.Count == 0
                        ? null
                        : deathFacts[deathFacts.Count - 1];
                }
            }

            public void ObservePlayerDeath(GameplayEntityDeathFact deathFact)
            {
                if (deathFact == null)
                {
                    return;
                }
                deathFacts.Add(deathFact);
                controller.ApplyPlayerDeathProjection(deathFact);
            }
        }
    }
}
'''
write(adapter_path, adapter)


play_test_path = "Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/Stage1PlayerLiveAuthorityPlayModeTests.cs"
play_tests = r'''#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.UI.VisibleSliceGeneralCombatHud;
using ShooterMover.UnityAdapters.Players;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.VisibleSliceIntegration
{
    public sealed class Stage1PlayerLiveAuthorityPlayModeTests
    {
        private const string SceneName = "Stage1VisibleSlice";
        private const string ScenePath =
            "Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity";
        private const string ControllerTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController";
        private const string LiveAdapterTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Stage1PlayerLiveAuthorityAdapterV1";

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.sceneCount == 1)
                {
                    SceneManager.SetActiveScene(
                        SceneManager.CreateScene("PLAYER-LIVE-001 Cleanup"));
                }
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RapidRestart_TwoCallsBeforeYieldAdvanceAuthorityTwice()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot before =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            StableId actorId = before.Player.ActorInstanceId;

            PlayerRuntimeRestartResult first =
                Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");
            PlayerRuntimeRestartResult second =
                Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");

            Assert.That(first.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
            Assert.That(second.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
            PlayerRuntimeSnapshot after =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Assert.That(after.Player.ActorInstanceId, Is.EqualTo(actorId));
            Assert.That(
                after.Player.LifecycleGeneration,
                Is.EqualTo(before.Player.LifecycleGeneration + 2L));
            Assert.That(after.Movement.Generation, Is.EqualTo(after.Player.LifecycleGeneration));
            Assert.That(after.Player.CurrentHealth, Is.EqualTo(100d));
            Assert.That(Read<long>(controller, "RestartGeneration"),
                Is.EqualTo(after.Player.LifecycleGeneration));
        }

        [UnityTest]
        public IEnumerator LiveDamageHealingDuplicatesAndHudUseAuthoritySnapshot()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot initial =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            StableId eventId = StableId.Parse("combat-event.player-live-duplicate");
            StableId source = StableId.Parse("actor.player-live-source");
            PlayerDamageRequest damage = new PlayerDamageRequest(
                eventId,
                source,
                StableId.Parse("participant.untrusted"),
                initial.Player.ActorInstanceId,
                12.5d,
                CombatChannel.Kinetic,
                initial.Player.LifecycleGeneration);
            DamageReceiverResult applied =
                Invoke<DamageReceiverResult>(adapter, "ApplyDamage", damage);
            DamageReceiverResult duplicate =
                Invoke<DamageReceiverResult>(adapter, "ApplyDamage", damage);
            DamageReceiverResult conflict = Invoke<DamageReceiverResult>(
                adapter,
                "ApplyDamage",
                new PlayerDamageRequest(
                    eventId,
                    source,
                    null,
                    initial.Player.ActorInstanceId,
                    8d,
                    CombatChannel.Kinetic,
                    initial.Player.LifecycleGeneration));

            Assert.That(applied.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
            Assert.That(conflict.Status, Is.EqualTo(DamageReceiverStatus.RejectedInvalid));
            Assert.That(
                conflict.RejectionCode,
                Is.EqualTo(DamageReceiverRejectionCode.ConflictingDuplicate));

            PlayerActorHealingResult healed = Invoke<PlayerActorHealingResult>(
                adapter,
                "ApplyHealing",
                new PlayerHealingRequest(
                    StableId.Parse("operation.player-live-heal"),
                    source,
                    StableId.Parse("participant.untrusted"),
                    initial.Player.ActorInstanceId,
                    2.25d,
                    initial.Player.LifecycleGeneration));
            Assert.That(healed.Status, Is.EqualTo(PlayerActorOperationStatus.Applied));

            PlayerRuntimeSnapshot live =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            GeneralCombatHudSnapshot hud = Invoke<GeneralCombatHudSnapshot>(
                adapter,
                "ExportVisibleHudSnapshot");
            Assert.That(live.Player.CurrentHealth, Is.EqualTo(89.75d));
            Assert.That(hud.PlayerVital.Health, Is.EqualTo(89.75d));
            Assert.That(hud.PlayerVital.MaximumHealth, Is.EqualTo(100d));
            Assert.That(hud.RestartGeneration, Is.EqualTo(live.Player.LifecycleGeneration));
            Assert.That(Read<int>(controller, "PlayerHealth"), Is.EqualTo(90),
                "The integer property is compatibility-only; the HUD retains fractional health.");
        }

        [UnityTest]
        public IEnumerator LethalDamageProjectsDeathOnceAndRestartRestoresParticipation()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot before =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            object droid = Read<object>(controller, "MobileBlasterDroid");
            StableId sourceActor = Read<StableId>(
                Read<object>(droid, "EnemyTarget"),
                "TargetId");
            PlayerDamageRequest lethal = new PlayerDamageRequest(
                StableId.Parse("combat-event.player-live-lethal"),
                sourceActor,
                null,
                before.Player.ActorInstanceId,
                1000d,
                CombatChannel.Kinetic,
                before.Player.LifecycleGeneration);

            DamageReceiverResult applied =
                Invoke<DamageReceiverResult>(adapter, "ApplyDamage", lethal);
            DamageReceiverResult replay =
                Invoke<DamageReceiverResult>(adapter, "ApplyDamage", lethal);
            Assert.That(applied.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(applied.DeathFact, Is.Not.Null);
            Assert.That(replay.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
            Assert.That(Read<int>(adapter, "DeathFactCount"), Is.EqualTo(1));
            GameplayEntityDeathFact fact =
                Read<GameplayEntityDeathFact>(adapter, "LastDeathFact");
            Assert.That(fact.SourceRunParticipantId,
                Is.EqualTo(StableId.Parse("participant.stage1-mobile-droid")));
            Assert.That(Read<bool>(controller, "IsSessionActive"), Is.False);
            Assert.That(Read<bool>(controller, "IsPlayerGameplayActive"), Is.False);
            Assert.That(Read<Collider2D>(controller, "PlayerCollider").enabled, Is.False);
            Assert.That(
                Read<bool>(Read<object>(controller, "PlayerMovementLifecycle"), "IsRunning"),
                Is.False);
            Assert.That(Invoke<bool>(controller, "FireAtMobileDroidForTests"), Is.False);

            PlayerRuntimeRestartResult restart =
                Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");
            Assert.That(restart.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
            PlayerRuntimeSnapshot restored =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Assert.That(restored.Player.CurrentHealth, Is.EqualTo(100d));
            Assert.That(restored.Player.ActorInstanceId,
                Is.EqualTo(before.Player.ActorInstanceId));
            Assert.That(Read<bool>(controller, "IsPlayerGameplayActive"), Is.True);
            Assert.That(Read<Collider2D>(controller, "PlayerCollider").enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator MobileDroidProjectile_DamagesLiveAuthorityAfterContact()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            Component droid = (Component)Read<object>(controller, "MobileBlasterDroid");
            Transform player = Read<Transform>(controller, "PlayerTransform");
            player.position = droid.transform.position + Vector3.right * 3f;
            float deadline = Time.time + 3f;
            while (Time.time < deadline
                && Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth
                    == 100d)
            {
                yield return null;
            }
            Assert.That(
                Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth,
                Is.LessThan(100d));
        }

        [UnityTest]
        public IEnumerator StaleProjectileAndVoidRequestsRejectAfterRestart()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot before =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");
            PlayerRuntimeSnapshot after =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            StableId source = StableId.Parse("actor.stale-projectile-source");
            DamageReceiverResult staleProjectile = Invoke<DamageReceiverResult>(
                adapter,
                "ApplyProjectileDamage",
                StableId.Parse("projectile-hit.stale-g0-f1"),
                source,
                after.Player.ActorInstanceId,
                25d,
                CombatChannel.Kinetic,
                before.Player.LifecycleGeneration);
            Assert.That(staleProjectile.Status,
                Is.EqualTo(DamageReceiverStatus.RejectedByLifecycle));
            Assert.That(
                Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth,
                Is.EqualTo(100d));

            VoidHazardPortResult staleVoid = Invoke<VoidHazardPortResult>(
                adapter,
                "RequestDamage",
                new VoidHazardDamageRequest(
                    StableId.Parse("void-event.stale-g0-c1"),
                    StableId.Parse("placed.demo002-void-hazard"),
                    after.Player.ActorInstanceId,
                    35d));
            Assert.That(staleVoid, Is.EqualTo(VoidHazardPortResult.Rejected));
            Assert.That(Read<int>(controller, "VoidDamageCount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator VoidCountChangesOnlyForAcceptedAuthorityDamage()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot player =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            StableId eventId = StableId.Parse("void-event.test-g0-c1");
            StableId hazardId = StableId.Parse("placed.demo002-void-hazard");
            VoidHazardDamageRequest acceptedRequest = new VoidHazardDamageRequest(
                eventId,
                hazardId,
                player.Player.ActorInstanceId,
                35d);
            VoidHazardPortResult accepted = Invoke<VoidHazardPortResult>(
                adapter,
                "RequestDamage",
                acceptedRequest);
            VoidHazardPortResult duplicate = Invoke<VoidHazardPortResult>(
                adapter,
                "RequestDamage",
                acceptedRequest);
            VoidHazardPortResult conflict = Invoke<VoidHazardPortResult>(
                adapter,
                "RequestDamage",
                new VoidHazardDamageRequest(
                    eventId,
                    hazardId,
                    player.Player.ActorInstanceId,
                    10d));
            VoidHazardPortResult mismatch = Invoke<VoidHazardPortResult>(
                adapter,
                "RequestDamage",
                new VoidHazardDamageRequest(
                    StableId.Parse("void-event.mismatch-g0-c2"),
                    hazardId,
                    StableId.Parse("actor.someone-else"),
                    35d));

            Assert.That(accepted, Is.EqualTo(VoidHazardPortResult.Accepted));
            Assert.That(duplicate, Is.EqualTo(VoidHazardPortResult.DuplicateNoChange));
            Assert.That(conflict, Is.EqualTo(VoidHazardPortResult.Rejected));
            Assert.That(mismatch, Is.EqualTo(VoidHazardPortResult.Rejected));
            Assert.That(Read<int>(controller, "VoidDamageCount"), Is.EqualTo(1));
            Assert.That(
                Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth,
                Is.EqualTo(65d));
        }

        [UnityTest]
        public IEnumerator PhysicalVoidDamage_UsesAuthorityAndPreservesIdentityOnRestart()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot before =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Transform player = Read<Transform>(controller, "PlayerTransform");
            player.position = new Vector3(-1.5f, 4.2f, 0f);
            float deadline = Time.time + 1f;
            while (Time.time < deadline
                && Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth
                    == 100d)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(
                Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth,
                Is.EqualTo(65d));
            Assert.That(Read<int>(controller, "VoidDamageCount"), Is.EqualTo(1));

            Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");
            PlayerRuntimeSnapshot after =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Assert.That(after.Player.ActorInstanceId,
                Is.EqualTo(before.Player.ActorInstanceId));
            Assert.That(after.Player.CurrentHealth, Is.EqualTo(100d));
            Assert.That(after.Player.LifecycleGeneration,
                Is.EqualTo(before.Player.LifecycleGeneration + 1L));
        }

        private static IEnumerator LoadComposition(
            Action<MonoBehaviour> assignController,
            Action<MonoBehaviour> assignAdapter)
        {
            AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone)
            {
                yield return null;
            }
            yield return null;
            MonoBehaviour controller =
                UnityEngine.Object.FindFirstObjectByType(FindType(ControllerTypeName))
                as MonoBehaviour;
            MonoBehaviour adapter =
                UnityEngine.Object.FindFirstObjectByType(FindType(LiveAdapterTypeName))
                as MonoBehaviour;
            Assert.That(controller, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(Read<bool>(adapter, "IsInitialized"), Is.True);
            assignController(controller);
            assignAdapter(adapter);
        }

        private static T Read<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }

        private static T Invoke<T>(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            object result = method.Invoke(target, arguments);
            return result == null ? default(T) : (T)result;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            throw new InvalidOperationException("Required type not found: " + fullName);
        }
    }
}
#endif
'''
write(play_test_path, play_tests)


doc_path = "docs/architecture/gameplay/PLAYER_LIVE_AUTHORITY_V1.md"
doc = read(doc_path)
doc += """

## Review repair

The initial polling bridge was replaced by a synchronous typed composition boundary:

- `QuickRestart` delegates to `PlayerRuntimeComposition.Restart` first. Only an
  accepted restart projects room, projectile, enemy, HUD and camera reset state.
- rapid same-frame restart calls therefore advance `0 -> 1 -> 2` without an
  Update-based catch-up race;
- the active compact HUD reads an immutable authority snapshot and preserves
  fractional health; the integer `PlayerHealth` property is compatibility-only;
- accepted death facts synchronously disable movement, real input, combat,
  targetability and outstanding player projectiles;
- turret, droid and void bindings use public typed ports rather than private-field
  reflection;
- projectile and void damage use the lifecycle generation encoded at emission,
  so stale callbacks cannot be relabelled with the current generation;
- void presentation counts increment only after accepted authority damage;
- trusted live source actor identities resolve to source run-participant identities.

`Stage1VisibleSliceController.cs` now contains only the minimal typed delegation
and downstream projection seams required to make the authority genuinely lead the
scene. The Stage 1 scene asset remains unchanged.
"""
write(doc_path, doc)

print("PLAYER-LIVE-001 repair applied")

using System;
using System.Collections.Generic;
using System.Reflection;
using ShooterMover.ContentPackages.Enemies.BlasterTurret;
using ShooterMover.ContentPackages.Enemies.MobileBlasterDroid;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.GameplayEntities;
using ShooterMover.UI.VisibleSliceGeneralCombatHud;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Physics;
using ShooterMover.UnityAdapters.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.TestSupport.VisibleSlice
{
    /// <summary>
    /// Scene-local migration adapter for PLAYER-LIVE-001. It keeps Unity movement and
    /// presentation attached to the retained Stage 1 player while PlayerRuntimeComposition
    /// and PlayerActorAuthority own every health and lifecycle decision.
    /// </summary>
    [DefaultExecutionOrder(10100)]
    [DisallowMultipleComponent]
    public sealed class Stage1PlayerLiveAuthorityAdapterV1 :
        MonoBehaviour,
        IGeneralCombatHudStateSource,
        IVoidHazardCombatPort
    {
        private const string PlayerRunParticipantIdText = "participant.stage1-player";
        private const string PlayerCharacterIdText = "character.striker";
        private const string PlayerFactionIdText = "faction.player";

        private static readonly BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private Stage1VisibleSliceController controller;
        private PlayerRuntimeCompositionRoot compositionRoot;
        private PlayerRuntimeComposition runtime;
        private Stage1PlayerMovementRuntimeV1 movement;
        private Stage1PlayerPresentationRuntimeV1 presentation;
        private Stage1PlayerInputRuntimeV1 input;
        private Stage1PlayerRunCoordinatorV1 runCoordinator;
        private CombatHit2DAdapter turretHitAdapter;
        private CombatHit2DAdapter droidHitAdapter;
        private BlasterTurretSceneContext2D turretContext;
        private VoidHazardTarget2D voidTarget;
        private Action<double> retainedTurretFallback;
        private MonoBehaviour retainedVoidCombatPort;
        private FieldInfo controllerHealthField;
        private FieldInfo controllerVoidDamageCountField;
        private FieldInfo turretFallbackField;
        private FieldInfo voidCombatPortField;
        private long observedControllerGeneration;
        private bool initialized;
        private bool disposed;

        public bool IsInitialized
        {
            get { return initialized && !disposed && runtime != null; }
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

        public DamageReceiverResult ApplyDamage(PlayerDamageRequest request)
        {
            EnsureInitialized();
            DamageReceiverResult result = runtime.ApplyDamage(request);
            SynchronizeLegacyReadMirror();
            return result;
        }

        public PlayerActorHealingResult ApplyHealing(PlayerHealingRequest request)
        {
            EnsureInitialized();
            PlayerActorHealingResult result = runtime.ApplyHealing(request);
            SynchronizeLegacyReadMirror();
            return result;
        }

        public bool TryRead(out GeneralCombatHudSnapshot snapshot)
        {
            snapshot = null;
            if (!IsInitialized
                || controller == null
                || !controller.TryRead(out GeneralCombatHudSnapshot retained))
            {
                return false;
            }

            PlayerRuntimeSnapshot live = runtime.ExportSnapshot();
            snapshot = new GeneralCombatHudSnapshot(
                live.Player.VitalState,
                live.Movement.ThrusterStatus,
                retained.FocusedEnemy,
                retained.FocusedEnemyLabel,
                retained.RoomName,
                retained.ObjectiveText,
                retained.RestartKeyboardHint,
                retained.RestartControllerHint,
                retained.ReticleVisible,
                retained.ReticleNormalizedX,
                retained.ReticleNormalizedY,
                retained.ReducedEffects,
                live.Player.LifecycleGeneration);
            return true;
        }

        public VoidHazardPortResult RequestDamage(VoidHazardDamageRequest request)
        {
            if (!IsInitialized || request == null)
            {
                return VoidHazardPortResult.Rejected;
            }

            IncrementVoidDamagePresentationCount();
            PlayerActorSnapshot player = runtime.ExportSnapshot().Player;
            DamageReceiverResult result = runtime.ApplyDamage(
                new PlayerDamageRequest(
                    request.EventId,
                    request.HazardId,
                    null,
                    request.TargetId,
                    request.Amount,
                    request.Channel,
                    player.LifecycleGeneration));
            SynchronizeLegacyReadMirror();
            return MapVoidResult(result);
        }

        public VoidHazardPortResult RequestInstantDeath(
            VoidHazardInstantDeathRequest request)
        {
            if (!IsInitialized || request == null)
            {
                return VoidHazardPortResult.Rejected;
            }

            IncrementVoidDamagePresentationCount();
            PlayerActorSnapshot player = runtime.ExportSnapshot().Player;
            DamageReceiverResult result = runtime.ApplyDamage(
                new PlayerDamageRequest(
                    request.EventId,
                    request.HazardId,
                    null,
                    request.TargetId,
                    player.MaximumHealth,
                    request.Channel,
                    player.LifecycleGeneration));
            SynchronizeLegacyReadMirror();
            return MapVoidResult(result);
        }

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                return;
            }

            SynchronizeControllerRestart();
            runtime.RefreshContinuousPresentation();
            SynchronizeLegacyReadMirror();
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

            Transform playerTransform = controller.PlayerTransform;
            if (playerTransform == null)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 could not resolve the retained Unity player.");
            }

            MovementActorLifecycle lifecycle =
                playerTransform.GetComponent<MovementActorLifecycle>();
            EnemyTarget2DAdapter playerTarget =
                playerTransform.GetComponent<EnemyTarget2DAdapter>();
            if (lifecycle == null
                || !lifecycle.IsConstructed
                || lifecycle.Actor == null
                || playerTarget == null
                || !playerTarget.IsConfigured)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 requires configured movement and player-target adapters.");
            }

            MovementThrusterTuningProfile tuning =
                ReadRequiredPrivateField<MovementThrusterTuningProfile>(
                    lifecycle,
                    "tuning");
            movement = new Stage1PlayerMovementRuntimeV1(lifecycle, tuning);
            PlayerMovementSnapshot initialMovement = movement.ExportSnapshot();
            if (initialMovement.Generation != controller.RestartGeneration)
            {
                throw new InvalidOperationException(
                    "Stage 1 player movement and retained restart generation disagree at composition.");
            }

            TrailRenderer boostTrail =
                playerTransform.GetComponentInChildren<TrailRenderer>(true);
            presentation = new Stage1PlayerPresentationRuntimeV1(boostTrail);
            input = new Stage1PlayerInputRuntimeV1();
            runCoordinator = new Stage1PlayerRunCoordinatorV1();

            PlayerActorDefinition definition = new PlayerActorDefinition(
                playerTarget.TargetId,
                StableId.Parse(PlayerRunParticipantIdText),
                StableId.Parse(PlayerCharacterIdText),
                StableId.Parse(PlayerFactionIdText),
                Stage1VisibleSliceController.StartingPlayerHealth,
                initialMovement.Generation);
            PlayerRuntimeAttachments attachments = new PlayerRuntimeAttachments(
                movement,
                presentation,
                input,
                Stage1PlayerAttributionResolverV1.Instance,
                runCoordinator);

            compositionRoot = new PlayerRuntimeCompositionRoot();
            PlayerRuntimeConstructionResult construction =
                compositionRoot.TryConstruct(
                    new PlayerRuntimeConfiguration(definition),
                    attachments);
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
            observedControllerGeneration = initialMovement.Generation;
            BindProjectileDamage();
            BindVoidHazard();
            BindHud();

            controllerHealthField = RequiredField(
                typeof(Stage1VisibleSliceController),
                "playerHealth");
            controllerVoidDamageCountField = RequiredField(
                typeof(Stage1VisibleSliceController),
                "voidDamageCount");

            initialized = true;
            SynchronizeLegacyReadMirror();
        }

        private void BindProjectileDamage()
        {
            BlasterTurretPackage turret = controller.TurretPackage;
            turretContext = controller.GameplayScope == null
                ? null
                : controller.GameplayScope.GetComponent<BlasterTurretSceneContext2D>();
            if (turret == null
                || turret.HitAdapter == null
                || turretContext == null)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 could not bind the Stage 1 turret damage route.");
            }

            turretFallbackField = RequiredField(
                typeof(BlasterTurretSceneContext2D),
                "fallbackPlayerDamage");
            retainedTurretFallback =
                (Action<double>)turretFallbackField.GetValue(turretContext);
            turretFallbackField.SetValue(turretContext, null);

            turretHitAdapter = turret.HitAdapter;
            turretHitAdapter.HitTranslated += HandleTurretHit;

            MobileBlasterDroidRuntime2D droid = controller.MobileBlasterDroid;
            if (droid == null || droid.EnemyTarget == null)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 could not resolve the Stage 1 mobile enemy.");
            }

            droidHitAdapter = ReadRequiredPrivateField<CombatHit2DAdapter>(
                droid,
                "hitAdapter");
            droidHitAdapter.HitTranslated += HandleDroidHit;
        }

        private void BindVoidHazard()
        {
            voidTarget = controller.PlayerTransform.GetComponent<VoidHazardTarget2D>();
            if (voidTarget == null)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 could not resolve the player void-hazard target.");
            }

            voidCombatPortField = RequiredField(
                typeof(VoidHazardTarget2D),
                "combatPort");
            retainedVoidCombatPort =
                (MonoBehaviour)voidCombatPortField.GetValue(voidTarget);
            voidCombatPortField.SetValue(voidTarget, this);

            if (!voidTarget.TryGetCombatPort(out IVoidHazardCombatPort rebound)
                || !ReferenceEquals(rebound, this))
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 failed to replace the legacy void damage port.");
            }
        }

        private void BindHud()
        {
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

            PlayerActorSnapshot player = runtime.ExportSnapshot().Player;
            HitMessage hit = translation.Message;
            runtime.ApplyDamage(
                new PlayerDamageRequest(
                    hit.EventId,
                    hit.SourceId,
                    null,
                    hit.TargetId,
                    amount,
                    hit.Channel,
                    player.LifecycleGeneration));
            SynchronizeLegacyReadMirror();
        }

        private void SynchronizeControllerRestart()
        {
            long requestedGeneration = controller.RestartGeneration;
            while (observedControllerGeneration < requestedGeneration)
            {
                if (observedControllerGeneration == long.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Stage 1 player lifecycle generation cannot advance past Int64.MaxValue.");
                }

                long replacement = observedControllerGeneration + 1L;
                PlayerRuntimeRestartResult result = runtime.Restart(
                    new PlayerRuntimeRestartCommand(
                        StableId.Create(
                            "operation",
                            "stage1-player-restart-g" + replacement),
                        runtime.ExportSnapshot().Player.ActorInstanceId,
                        observedControllerGeneration,
                        replacement));
                if (result.Status != PlayerRuntimeRestartStatus.Applied
                    && result.Status != PlayerRuntimeRestartStatus.Duplicate)
                {
                    throw new InvalidOperationException(
                        "PLAYER-LIVE-001 rejected the retained Stage 1 restart: "
                        + result.Status
                        + "/"
                        + result.RejectionCode
                        + ".");
                }

                observedControllerGeneration = replacement;
            }

            if (observedControllerGeneration > requestedGeneration)
            {
                throw new InvalidOperationException(
                    "The retained Stage 1 controller reported a stale restart generation.");
            }
        }

        private void SynchronizeLegacyReadMirror()
        {
            if (controller == null
                || controllerHealthField == null
                || runtime == null)
            {
                return;
            }

            double health = runtime.ExportSnapshot().Player.CurrentHealth;
            controllerHealthField.SetValue(
                controller,
                Mathf.RoundToInt((float)health));
        }

        private void IncrementVoidDamagePresentationCount()
        {
            if (controller == null || controllerVoidDamageCountField == null)
            {
                return;
            }

            int current = (int)controllerVoidDamageCountField.GetValue(controller);
            if (current < int.MaxValue)
            {
                controllerVoidDamageCountField.SetValue(controller, current + 1);
            }
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

        private static T ReadRequiredPrivateField<T>(
            object target,
            string fieldName)
            where T : class
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            FieldInfo field = RequiredField(target.GetType(), fieldName);
            T value = field.GetValue(target) as T;
            if (value == null)
            {
                throw new InvalidOperationException(
                    target.GetType().Name
                    + "."
                    + fieldName
                    + " did not contain the required "
                    + typeof(T).Name
                    + ".");
            }

            return value;
        }

        private static FieldInfo RequiredField(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, PrivateInstance);
            if (field == null)
            {
                throw new MissingFieldException(type.FullName, fieldName);
            }

            return field;
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

            if (turretContext != null && turretFallbackField != null)
            {
                turretFallbackField.SetValue(
                    turretContext,
                    retainedTurretFallback);
            }

            if (voidTarget != null && voidCombatPortField != null)
            {
                voidCombatPortField.SetValue(
                    voidTarget,
                    retainedVoidCombatPort);
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

        private sealed class Stage1PlayerMovementRuntimeV1 :
            IPlayerMovementRuntime
        {
            private readonly MovementActorLifecycle lifecycle;
            private readonly MovementThrusterTuningProfile tuning;
            private long generation;
            private bool disposed;

            public Stage1PlayerMovementRuntimeV1(
                MovementActorLifecycle lifecycle,
                MovementThrusterTuningProfile tuning)
            {
                this.lifecycle = lifecycle
                    ?? throw new ArgumentNullException(nameof(lifecycle));
                this.tuning = tuning
                    ?? throw new ArgumentNullException(nameof(tuning));
                if (!lifecycle.IsConstructed || lifecycle.Actor == null)
                {
                    throw new ArgumentException(
                        "A constructed movement lifecycle is required.",
                        nameof(lifecycle));
                }

                MovementThrusterTuningProfileValidator.Validate(tuning);
                generation = lifecycle.Actor.Generation;
            }

            public bool IsDisposed
            {
                get { return disposed || lifecycle == null || lifecycle.IsDisposed; }
            }

            public PlayerMovementSnapshot ExportSnapshot()
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                MovementActor2D actor = lifecycle.Actor;
                ThrusterStatusSnapshot thruster =
                    ThrusterStatusProjector.Project(actor, tuning);
                Vector3 position = lifecycle.transform.position;
                return new PlayerMovementSnapshot(
                    generation,
                    position.x,
                    position.y,
                    thruster.VelocityX,
                    thruster.VelocityY,
                    thruster);
            }

            public bool TryRestart(
                long retiringGeneration,
                long replacementGeneration)
            {
                if (IsDisposed
                    || retiringGeneration != generation
                    || retiringGeneration == long.MaxValue
                    || replacementGeneration != retiringGeneration + 1L)
                {
                    return false;
                }

                long liveGeneration = lifecycle.Actor.Generation;
                if (liveGeneration == retiringGeneration)
                {
                    lifecycle.RestartActor();
                    liveGeneration = lifecycle.Actor.Generation;
                }

                if (liveGeneration != replacementGeneration)
                {
                    return false;
                }

                generation = replacementGeneration;
                return true;
            }

            public void Dispose()
            {
                disposed = true;
            }
        }

        private sealed class Stage1PlayerPresentationRuntimeV1 :
            IPlayerPresentationRuntime
        {
            private readonly TrailRenderer boostTrail;
            private bool disposed;

            public Stage1PlayerPresentationRuntimeV1(
                TrailRenderer boostTrail)
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

        private sealed class Stage1PlayerInputRuntimeV1 :
            IPlayerInputRuntime
        {
            private PlayerInputOwnership ownership;
            private bool disposed;

            public bool TryAcquire(PlayerInputOwnership requested)
            {
                if (disposed || requested == null)
                {
                    return false;
                }

                if (ownership == null)
                {
                    ownership = requested;
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
                return true;
            }

            public void Dispose()
            {
                disposed = true;
                ownership = null;
            }
        }

        private sealed class Stage1PlayerAttributionResolverV1 :
            ITrustedPlayerAttributionResolver
        {
            public static readonly Stage1PlayerAttributionResolverV1 Instance =
                new Stage1PlayerAttributionResolverV1();

            private Stage1PlayerAttributionResolverV1()
            {
            }

            public StableId ResolveSourceRunParticipant(StableId sourceActorId)
            {
                return null;
            }
        }

        private sealed class Stage1PlayerRunCoordinatorV1 :
            IPlayerRunCoordinator
        {
            private readonly List<GameplayEntityDeathFact> deathFacts =
                new List<GameplayEntityDeathFact>();

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
                if (deathFact != null)
                {
                    deathFacts.Add(deathFact);
                }
            }
        }
    }

    internal static class Stage1PlayerLiveAuthorityInstallerV1
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallCurrentScene()
        {
            TryInstall(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            TryInstall(scene);
        }

        private static void TryInstall(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || !string.Equals(
                    scene.path,
                    Stage1VisibleSliceController.ScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Stage1VisibleSliceController controller =
                    roots[index].GetComponentInChildren<Stage1VisibleSliceController>(
                        true);
                if (controller == null)
                {
                    continue;
                }

                if (controller.GetComponent<Stage1PlayerLiveAuthorityAdapterV1>()
                    == null)
                {
                    controller.gameObject.AddComponent<
                        Stage1PlayerLiveAuthorityAdapterV1>();
                }

                return;
            }
        }
    }
}

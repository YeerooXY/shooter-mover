using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.Production.Combat;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UI.VisibleSliceGeneralCombatHud;
using ShooterMover.UnityAdapters.Players;
using UnityEngine;

namespace ShooterMover.Production.Level1
{
    /// <summary>
    /// Canonical Level 1 player runtime composition. PlayerRuntimeComposition owns health,
    /// healing, death, attribution and lifecycle generation. This component projects those
    /// accepted facts into the retained Level 1 scene and binds every enemy projectile
    /// source through one reusable EnemyToPlayerDamageRouterV1.
    /// </summary>
    [DefaultExecutionOrder(10100)]
    [DisallowMultipleComponent]
    public class Level1PlayerRuntimeAdapterV1 :
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
        private Level1PlayerPresentationRuntimeV1 presentation;
        private Level1PlayerInputRuntimeV1 input;
        private Level1PlayerAttributionResolverV1 attribution;
        private Level1PlayerRunCoordinatorV1 runCoordinator;
        private EnemyToPlayerDamageRouterV1 enemyDamageRouter;
        private EnemyProjectileDamageSourceBinderV1 enemyDamageSources;
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
                    "The Level 1 immutable HUD snapshot is unavailable.");
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
            return ApplyRestartCommand(
                new PlayerRuntimeRestartCommand(
                    StableId.Create(
                        "operation",
                        "stage1-player-restart-g"
                            + replacement.ToString(CultureInfo.InvariantCulture)),
                    before.Player.ActorInstanceId,
                    retiring,
                    replacement));
        }

        public PlayerRuntimeRestartResult ApplyRestartCommand(
            PlayerRuntimeRestartCommand command)
        {
            EnsureInitialized();
            PlayerRuntimeRestartResult result = runtime.Restart(command);
            if (result.Status == PlayerRuntimeRestartStatus.Applied)
            {
                if (!controller.ApplyAcceptedPlayerRestart(result))
                {
                    throw new InvalidOperationException(
                        "The Level 1 scene rejected an accepted player restart projection.");
                }
                if (enemyDamageSources != null)
                {
                    enemyDamageSources.ClearLifecycle();
                }
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

            DamageReceiverResult result = runtime.ApplyDamage(
                new PlayerDamageRequest(
                    request.EventId,
                    request.HazardId,
                    null,
                    request.TargetId,
                    request.Amount,
                    request.Channel,
                    request.AttemptGeneration));
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

            PlayerActorSnapshot player = runtime.ExportSnapshot().Player;
            DamageReceiverResult result = runtime.ApplyDamage(
                new PlayerDamageRequest(
                    request.EventId,
                    request.HazardId,
                    null,
                    request.TargetId,
                    player.MaximumHealth,
                    request.Channel,
                    request.AttemptGeneration));
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
            return EnemyProjectileDamageSourceBinderV1
                .TryResolveLifecycleGeneration(eventId, out generation);
        }

        protected virtual void Awake()
        {
            InitializeRuntime();
        }

        protected virtual void Update()
        {
            if (IsInitialized)
            {
                runtime.RefreshContinuousPresentation();
            }
        }

        protected virtual void OnDestroy()
        {
            DisposeRuntime();
        }

        protected void InitializeRuntime()
        {
            if (initialized)
            {
                return;
            }

            controller = GetComponent<Stage1VisibleSliceController>();
            if (controller == null || !controller.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Level 1 requires an initialized scene presentation root.");
            }
            if (controller.PlayerMovementLifecycle == null
                || controller.PlayerMovementTuning == null
                || controller.PlayerTargetAdapter == null
                || !controller.PlayerTargetAdapter.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Level 1 requires configured movement and target adapters.");
            }

            movement = new MovementActorPlayerRuntimeAdapter(
                controller.PlayerMovementLifecycle,
                controller.PlayerMovementTuning);
            presentation = new Level1PlayerPresentationRuntimeV1(
                controller.PlayerBoostTrail);
            input = new Level1PlayerInputRuntimeV1(controller);
            attribution = new Level1PlayerAttributionResolverV1();
            runCoordinator = new Level1PlayerRunCoordinatorV1(controller);

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
                    "Level 1 player composition failed: "
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

        protected void DisposeRuntime()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (enemyDamageSources != null)
            {
                enemyDamageSources.Dispose();
            }
            if (enemyDamageRouter != null)
            {
                enemyDamageRouter.Dispose();
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
                || controller.TurretPackage.ProjectileAdapter == null
                || controller.MobileBlasterDroid == null
                || controller.MobileBlasterDroid.HitAdapter == null
                || controller.MobileBlasterDroid.ProjectileAdapter == null)
            {
                throw new InvalidOperationException(
                    "Level 1 could not bind enemy projectile sources.");
            }

            enemyDamageRouter = new EnemyToPlayerDamageRouterV1(
                request => runtime.ApplyDamage(request),
                () => IsInitialized);
            enemyDamageSources = new EnemyProjectileDamageSourceBinderV1(
                enemyDamageRouter);

            if (!enemyDamageSources.RegisterSource(
                    controller.TurretPackage.HitAdapter,
                    controller.TurretPackage.ProjectileAdapter,
                    Stage1VisibleSliceController.TurretShotDamage)
                || !enemyDamageSources.RegisterSource(
                    controller.MobileBlasterDroid.HitAdapter,
                    controller.MobileBlasterDroid.ProjectileAdapter,
                    BlasterMachineGunPackage.NormalDamage))
            {
                throw new InvalidOperationException(
                    "Level 1 rejected an enemy projectile source registration.");
            }

            voidTarget = controller.PlayerVoidTarget;
            if (voidTarget == null || !voidTarget.BindCombatPort(this))
            {
                throw new InvalidOperationException(
                    "Level 1 could not bind the typed void combat port.");
            }

            if (controller.CombatHud == null)
            {
                throw new InvalidOperationException(
                    "Level 1 could not resolve the combat HUD.");
            }
            controller.CombatHud.UnbindSources();
            controller.CombatHud.BindSources(this, null);
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
                    "The Level 1 player runtime is not initialized.");
            }
        }

        private sealed class Level1PlayerPresentationRuntimeV1 :
            IPlayerPresentationRuntime
        {
            private readonly TrailRenderer boostTrail;
            private bool disposed;

            public Level1PlayerPresentationRuntimeV1(TrailRenderer boostTrail)
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

        private sealed class Level1PlayerInputRuntimeV1 : IPlayerInputRuntime
        {
            private readonly Stage1VisibleSliceController controller;
            private PlayerInputOwnership ownership;
            private bool disposed;

            public Level1PlayerInputRuntimeV1(
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

        private sealed class Level1PlayerAttributionResolverV1 :
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
                        "A live source actor cannot change participant identity.");
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

        private sealed class Level1PlayerRunCoordinatorV1 : IPlayerRunCoordinator
        {
            private readonly Stage1VisibleSliceController controller;
            private readonly List<GameplayEntityDeathFact> deathFacts =
                new List<GameplayEntityDeathFact>();

            public Level1PlayerRunCoordinatorV1(
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

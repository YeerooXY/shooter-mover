using System;
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

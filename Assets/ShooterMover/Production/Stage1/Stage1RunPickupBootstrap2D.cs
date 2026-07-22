using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Pickup consumer for the one shared Stage 1 Run Session aggregate. This component owns
    /// only pickup realization/presentation adapters and retained admission delivery. It never
    /// starts, replaces, or reconstructs a Run Session or any player/weapon/condition/room port.
    /// </summary>
    [DefaultExecutionOrder(21000)]
    [DisallowMultipleComponent]
    public sealed class Stage1RunPickupBootstrap2D : MonoBehaviour
    {
        private sealed class EnemySourceRegistration
        {
            public EnemySourceRegistration(
                IEnemyActor2DAuthority authority,
                Transform sourceTransform,
                EnemyDefinitionV1 definition,
                EnemyRuntimeIdentityV1 identity,
                StableId roomStableId,
                StableId placementStableId)
            {
                Authority = authority;
                SourceTransform = sourceTransform;
                Definition = definition;
                Identity = identity;
                RoomStableId = roomStableId;
                PlacementStableId = placementStableId;
            }

            public IEnemyActor2DAuthority Authority { get; }
            public Transform SourceTransform { get; }
            public EnemyDefinitionV1 Definition { get; }
            public EnemyRuntimeIdentityV1 Identity { get; }
            public StableId RoomStableId { get; }
            public StableId PlacementStableId { get; }
        }

        private static readonly StableId RoomRuntimeStableId =
            StableId.Parse("room-runtime-instance.demo-cutover-level1");
        private static readonly StableId MobileDefinitionStableId =
            StableId.Parse("enemy.mobile-blaster-droid");
        private static readonly StableId TurretDefinitionStableId =
            StableId.Parse("enemy.blaster-turret");

        private Stage1PlayableLoopCompositionV1 stage1;
        private GameObject runtimeRoot;
        private RunSessionAggregateV1 run;
        private long observedLifecycleGeneration = -1L;
        private RunPickupSourcePositionRegistry2D sourcePositions;
        private RunPickupLiveCompositionV1 pickups;
        private TerminalDropBindingCompositionV1 terminalDrops;
        private RunPickupPresenter2D presenter;
        private readonly Stage1PendingAdmissionPickupBridgeV1 admissionBridge =
            new Stage1PendingAdmissionPickupBridgeV1();
        private readonly List<EnemySourceRegistration> enemySources =
            new List<EnemySourceRegistration>();
        private readonly List<UnityEngine.Object> runtimeAssets =
            new List<UnityEngine.Object>();
        private string diagnostic = string.Empty;

        public bool IsComposed
        {
            get
            {
                return run != null
                    && pickups != null
                    && ReferenceEquals(run, ResolveSharedRunOrNull());
            }
        }

        public string Diagnostic { get { return diagnostic; } }
        public RunSessionAggregateV1 RunSession { get { return run; } }
        public RunLocalPickupAuthorityV1 PickupAuthority
        {
            get { return pickups == null ? null : pickups.Authority; }
        }
        public int PendingAdmissionCount { get { return admissionBridge.PendingCount; } }
        public PendingTerminalDropAdmissionResultV1 LastEnemyAdmission
        {
            get
            {
                return terminalDrops == null
                    ? null
                    : terminalDrops.EnemyConsumer.LastAdmission;
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Install(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (!scene.IsValid()) return;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Stage1VisibleSliceController[] controllers =
                    roots[rootIndex].GetComponentsInChildren<
                        Stage1VisibleSliceController>(true);
                for (int index = 0; index < controllers.Length; index++)
                {
                    Stage1VisibleSliceController controller = controllers[index];
                    if (controller != null
                        && controller.GetComponent<
                            Stage1RunPickupBootstrap2D>() == null)
                    {
                        controller.gameObject.AddComponent<
                            Stage1RunPickupBootstrap2D>();
                    }
                }
            }
        }

        private IEnumerator Start()
        {
            stage1 = GetComponent<Stage1PlayableLoopCompositionV1>();
            while (stage1 != null)
            {
                RunSessionAggregateV1 shared;
                if (stage1.IsRunPickupProductionReady
                    && stage1.TryResolveSharedRunSession(out shared)
                    && shared != null)
                {
                    TryCompose(shared);
                    yield break;
                }
                yield return null;
            }
        }

        private void LateUpdate()
        {
            if (stage1 == null)
                stage1 = GetComponent<Stage1PlayableLoopCompositionV1>();
            if (stage1 == null || !stage1.IsRunPickupProductionReady) return;

            RunSessionAggregateV1 shared;
            if (!stage1.TryResolveSharedRunSession(out shared) || shared == null)
            {
                diagnostic = "stage1-pickup-shared-run-unavailable";
                admissionBridge.ReleaseRuntime();
                return;
            }

            if (run == null || !ReferenceEquals(run, shared))
            {
                TryCompose(shared);
            }
            else if (observedLifecycleGeneration != shared.LifecycleGeneration)
            {
                TryRefreshLifecycle(shared);
            }

            admissionBridge.ProcessPending();
            if (!string.IsNullOrWhiteSpace(admissionBridge.LastDiagnostic))
                diagnostic = admissionBridge.LastDiagnostic;
            else if (presenter != null && stage1.RunPickupRooms != null)
                presenter.Synchronize(stage1.RunPickupRooms.CurrentRoomStableId);
        }

        public Stage1PickupDeliveryResultV1 EnqueueAdmission(
            PendingTerminalDropAdmissionResultV1 admission)
        {
            Stage1PickupDeliveryResultV1 result =
                admissionBridge.TryEnqueue(admission);
            admissionBridge.ProcessPending();
            diagnostic = admissionBridge.LastDiagnostic;
            return result;
        }

        public void RegisterFixedSource(
            StableId runStableId,
            long lifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            StableId roomStableId,
            Vector2 position,
            string fingerprint)
        {
            admissionBridge.RegisterFixedSource(
                runStableId,
                lifecycleGeneration,
                sourceEntityStableId,
                sourcePlacementStableId,
                roomStableId,
                position,
                fingerprint);
        }

        private RunSessionAggregateV1 ResolveSharedRunOrNull()
        {
            RunSessionAggregateV1 shared;
            return stage1 != null
                && stage1.TryResolveSharedRunSession(out shared)
                    ? shared
                    : null;
        }

        private void TryCompose(RunSessionAggregateV1 shared)
        {
            try
            {
                Compose(shared);
                diagnostic = string.Empty;
            }
            catch (Exception exception)
            {
                diagnostic = "Stage 1 pickup integration failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;
                Debug.LogException(exception, this);
                TeardownProjection(false);
            }
        }

        private void Compose(RunSessionAggregateV1 shared)
        {
            if (stage1 == null || !stage1.IsRunPickupProductionReady)
                throw new InvalidOperationException(
                    "Stage 1 pickup prerequisites are unavailable.");
            if (shared == null
                || shared.LifecycleState != RunSessionLifecycleStateV1.Active
                || shared.RunStableId != stage1.RunPickupRunStableId)
            {
                throw new InvalidOperationException(
                    "The exact shared production Run Session is unavailable for pickups.");
            }

            bool changedRun = run != null && run.RunStableId != shared.RunStableId;
            TeardownProjection(changedRun);
            run = shared;

            runtimeRoot = new GameObject(
                "PICKUP-LIVE-001 Shared Run Consumer");
            runtimeRoot.transform.SetParent(transform, false);
            sourcePositions = runtimeRoot.AddComponent<
                RunPickupSourcePositionRegistry2D>();
            pickups = RunPickupLiveCompositionV1.Create(run, sourcePositions);

            RunPickupAuthorityHost2D authorityHost =
                runtimeRoot.AddComponent<RunPickupAuthorityHost2D>();
            authorityHost.Configure(pickups.Authority);
            RunPickupPresentationRegistry2D presentations =
                runtimeRoot.AddComponent<RunPickupPresentationRegistry2D>();
            presentations.Configure(BuildPresentationEntries());
            presenter = runtimeRoot.AddComponent<RunPickupPresenter2D>();
            presenter.Configure(
                authorityHost,
                presentations,
                runtimeRoot.transform);

            admissionBridge.ConfigureRuntime(
                new Stage1UnityPickupAdmissionRuntimeV1(
                    sourcePositions,
                    pickups.PendingConsumer,
                    presenter));

            ConfigureCollector();
            EnemyCatalogV1 enemyCatalog = BuildPickupEnemyCatalog();
            terminalDrops = TerminalDropBindingCompositionV1.Create(
                enemyCatalog,
                new Stage1EnemyTerminalSourceContextResolverV1(() => run),
                new PropCatalogV1(
                    PropCapabilityRegistryV1.CreateBuiltIns(),
                    Array.Empty<PropDefinitionV1>()),
                new Stage1MissingPropTerminalSourceContextResolverV1(),
                new Stage1PickupTerminalDropRunContextResolverV1(
                    () => run,
                    () => stage1.RunPickupExperience.CurrentState.Level),
                BuildRewardProfiles(),
                new RewardGenerationServiceV1(),
                new PendingTerminalDropAdmissionAuthorityV1(),
                null,
                admissionBridge);

            RegisterEnemy(
                stage1.RunPickupController.MobileBlasterDroid,
                stage1.RunPickupController.MobileBlasterDroid.transform,
                MobileDefinitionStableId,
                Level1AuthorableRoomDefinitionV1
                    .MovingDroidInstanceStableId,
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                enemyCatalog);
            RegisterEnemy(
                stage1.RunPickupController.TurretPackage.Authority,
                stage1.RunPickupController.TurretPackage.transform,
                TurretDefinitionStableId,
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId,
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                enemyCatalog);

            observedLifecycleGeneration = run.LifecycleGeneration;
            admissionBridge.RetireOtherLifecycles(
                run.RunStableId,
                run.LifecycleGeneration);
            RefreshEnemySourceBindings();
            admissionBridge.ProcessPending();
            presenter.Synchronize(stage1.RunPickupRooms.CurrentRoomStableId);
        }

        private void TryRefreshLifecycle(RunSessionAggregateV1 shared)
        {
            try
            {
                if (!ReferenceEquals(run, shared))
                    throw new InvalidOperationException(
                        "Pickup lifecycle refresh requires the same shared aggregate.");
                observedLifecycleGeneration = shared.LifecycleGeneration;
                admissionBridge.RetireOtherLifecycles(
                    shared.RunStableId,
                    shared.LifecycleGeneration);
                ConfigureCollector();
                RefreshEnemySourceBindings();
                admissionBridge.ProcessPending();
                diagnostic = admissionBridge.LastDiagnostic;
            }
            catch (Exception exception)
            {
                diagnostic = "Stage 1 pickup lifecycle refresh failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void ConfigureCollector()
        {
            RunPlayerRuntimeSnapshotV1 playerSnapshot =
                run.RuntimePorts.Player.ExportSnapshot();
            RunPickupCollector2D collector = stage1.RunPickupController
                .PlayerTransform.GetComponent<RunPickupCollector2D>();
            if (collector == null)
            {
                collector = stage1.RunPickupController.PlayerTransform
                    .gameObject.AddComponent<RunPickupCollector2D>();
            }
            collector.Configure(
                playerSnapshot.ActorInstanceStableId,
                playerSnapshot.ParticipantStableId);
        }

        private void RegisterEnemy(
            IEnemyActor2DAuthority authority,
            Transform sourceTransform,
            StableId definitionStableId,
            StableId placementStableId,
            StableId roomStableId,
            EnemyCatalogV1 catalog)
        {
            EnemyDefinitionV1 definition =
                catalog.GetDefinition(definitionStableId);
            EnemyRuntimeIdentityV1 identity =
                new DeterministicEnemyRuntimeIdentityDeriverV1().Derive(
                    run.RunStableId,
                    RoomRuntimeStableId,
                    roomStableId,
                    placementStableId);
            var registration = new EnemySourceRegistration(
                authority,
                sourceTransform,
                definition,
                identity,
                roomStableId,
                placementStableId);
            enemySources.Add(registration);

            Stage1EnemyTerminalDropObserver2D observer =
                runtimeRoot.AddComponent<Stage1EnemyTerminalDropObserver2D>();
            observer.Configure(
                authority,
                delegate { return TryEmitEnemyDeath(registration); });
        }

        private void RefreshEnemySourceBindings()
        {
            for (int index = 0; index < enemySources.Count; index++)
            {
                EnemySourceRegistration registration = enemySources[index];
                if (registration == null
                    || registration.SourceTransform == null
                    || registration.Identity == null)
                {
                    continue;
                }
                admissionBridge.RegisterTransformSource(
                    run.RunStableId,
                    run.LifecycleGeneration,
                    registration.Identity.EntityInstanceId,
                    registration.PlacementStableId,
                    registration.RoomStableId,
                    registration.SourceTransform);
            }
        }

        private Stage1PickupDeliveryResultV1 TryEmitEnemyDeath(
            EnemySourceRegistration registration)
        {
            if (run == null
                || terminalDrops == null
                || registration == null
                || registration.Definition == null
                || registration.Identity == null)
            {
                return new Stage1PickupDeliveryResultV1(
                    Stage1PickupDeliveryDispositionV1.Retryable,
                    null,
                    "stage1-enemy-terminal-runtime-unavailable");
            }

            try
            {
                RunPlayerRuntimeSnapshotV1 player =
                    run.RuntimePorts.Player.ExportSnapshot();
                string suffix = run.RunStableId
                    + "|"
                    + run.LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture)
                    + "|"
                    + registration.Identity.PlacementStableId;
                StableId deathEvent = StableId.Create(
                    "enemy-death-event",
                    DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                        suffix + "|death"));
                var fact = new EnemyDeathFactV1(
                    deathEvent,
                    StableId.Create(
                        "combat-event",
                        DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                            suffix + "|trigger")),
                    registration.Identity,
                    registration.Definition.DefinitionId,
                    1,
                    run.LifecycleGeneration,
                    player.ActorInstanceStableId,
                    player.ParticipantStableId,
                    registration.Definition.ExperienceProfileId,
                    registration.Definition.DropProfileId,
                    (EnemyActorDeathCause)1);

                admissionBridge.RegisterTransformSource(
                    run.RunStableId,
                    run.LifecycleGeneration,
                    registration.Identity.EntityInstanceId,
                    registration.PlacementStableId,
                    registration.RoomStableId,
                    registration.SourceTransform,
                    deathEvent);
                terminalDrops.EnemyConsumer.Consume(fact);
                PendingTerminalDropAdmissionResultV1 admission =
                    terminalDrops.EnemyConsumer.LastAdmission;
                if (admission == null || !admission.IsAccepted)
                {
                    return new Stage1PickupDeliveryResultV1(
                        Stage1PickupDeliveryDispositionV1.Retryable,
                        admission,
                        admission == null
                            ? "stage1-enemy-terminal-admission-null"
                            : admission.Diagnostic);
                }

                Stage1PickupDeliveryResultV1 queued =
                    admissionBridge.TryEnqueue(admission);
                admissionBridge.ProcessPending();
                diagnostic = admissionBridge.LastDiagnostic;
                return queued;
            }
            catch (Exception exception)
            {
                return new Stage1PickupDeliveryResultV1(
                    Stage1PickupDeliveryDispositionV1.Retryable,
                    null,
                    "stage1-enemy-terminal-attempt-exception:"
                        + exception.GetType().Name
                        + ":"
                        + exception.Message);
            }
        }

        private IEnumerable<RunPickupPresentationEntryV1>
            BuildPresentationEntries()
        {
            return new[]
            {
                Presentation(
                    RewardGrantKindV1.Money,
                    "Money",
                    new Color(1f, 0.85f, 0.15f, 1f)),
                Presentation(
                    RewardGrantKindV1.Scrap,
                    "Scrap",
                    new Color(0.6f, 0.85f, 1f, 1f)),
                Presentation(
                    RewardGrantKindV1.Strongbox,
                    "Strongbox",
                    new Color(0.2f, 1f, 0.45f, 1f)),
                Presentation(
                    RewardGrantKindV1.EquipmentReference,
                    "Equipment",
                    new Color(0.85f, 0.35f, 1f, 1f)),
            };
        }

        private RunPickupPresentationEntryV1 Presentation(
            RewardGrantKindV1 kind,
            string label,
            Color color)
        {
            var texture = new Texture2D(
                8,
                8,
                TextureFormat.RGBA32,
                false);
            Color[] pixels = new Color[64];
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = color;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0.5f),
                16f);
            texture.name = "Stage1" + label + "PickupTexture";
            sprite.name = "Stage1" + label + "PickupSprite";
            runtimeAssets.Add(texture);
            runtimeAssets.Add(sprite);
            var entry = new RunPickupPresentationEntryV1();
            entry.Configure(
                kind,
                null,
                null,
                sprite,
                Vector3.one,
                0.75f,
                label);
            return entry;
        }

        private static RewardProfileCatalogResolverV1 BuildRewardProfiles()
        {
            RewardProfileV1 common = RewardProfileV1.Create(
                StableId.Parse("drop.enemy-common"),
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse(
                            "grant.stage1-enemy-common-money"),
                        RewardGrantKindV1.Money,
                        StableId.Parse("currency.credits"),
                        5L),
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse(
                            "grant.stage1-enemy-common-scrap"),
                        RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"),
                        1L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 turret = RewardProfileV1.Create(
                StableId.Parse("drop.enemy-turret"),
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse(
                            "grant.stage1-enemy-turret-money"),
                        RewardGrantKindV1.Money,
                        StableId.Parse("currency.credits"),
                        15L),
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse(
                            "grant.stage1-enemy-turret-box"),
                        RewardGrantKindV1.Strongbox,
                        StableId.Parse("strongbox.tier-common"),
                        1L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            return new RewardProfileCatalogResolverV1(new[]
            {
                common,
                turret,
                RewardProfileV1.CreateExplicitNoDrop(
                    StableId.Parse("drop.enemy-none")),
            });
        }

        private static EnemyCatalogV1 BuildPickupEnemyCatalog()
        {
            return new EnemyCatalogV1(
                EnemyCatalogV1.SupportedSchemaVersion,
                StableId.Parse(
                    "enemy-content.stage1-pickup-live-v1"),
                new[]
                {
                    PickupEnemyDefinition(
                        MobileDefinitionStableId,
                        "presentation.enemy-mobile-blaster-droid",
                        16d,
                        "xp.enemy-standard",
                        "drop.enemy-common"),
                    PickupEnemyDefinition(
                        TurretDefinitionStableId,
                        "presentation.enemy-blaster-turret",
                        30d,
                        "xp.enemy-turret",
                        "drop.enemy-turret"),
                });
        }

        private static EnemyDefinitionV1 PickupEnemyDefinition(
            StableId definitionStableId,
            string presentation,
            double health,
            string experienceProfile,
            string dropProfile)
        {
            return new EnemyDefinitionV1(
                definitionStableId,
                StableId.Parse(presentation),
                health,
                new EnemyLevelScalingProfileV1(1, 100, 0d, 1d),
                StableId.Parse("faction.hostile-machines"),
                24d,
                360d,
                StableId.Parse("enemy-movement.stationary"),
                StableId.Parse("enemy-decision.ranged-standard"),
                Array.Empty<EnemyAttackCapabilityDescriptorV1>(),
                StableId.Parse(experienceProfile),
                StableId.Parse(dropProfile),
                EnemyCatalogRoomClearRoleV1.RequiredEnemy,
                Array.Empty<StableId>());
        }

        private void TeardownProjection(bool clearDeliveryState)
        {
            admissionBridge.ReleaseRuntime();
            if (runtimeRoot != null)
            {
                runtimeRoot.SetActive(false);
                Destroy(runtimeRoot);
            }
            for (int index = 0; index < runtimeAssets.Count; index++)
            {
                if (runtimeAssets[index] != null)
                    Destroy(runtimeAssets[index]);
            }
            runtimeAssets.Clear();
            enemySources.Clear();
            runtimeRoot = null;
            sourcePositions = null;
            pickups = null;
            terminalDrops = null;
            presenter = null;
            observedLifecycleGeneration = -1L;
            if (clearDeliveryState)
                admissionBridge.ClearAll();
            run = null;
        }

        private void OnDestroy()
        {
            TeardownProjection(true);
        }
    }
}

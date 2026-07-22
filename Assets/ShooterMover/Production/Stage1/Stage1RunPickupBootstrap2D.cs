using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Flow.Production;
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
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Production Stage 1 connection for PICKUP-LIVE-001. It observes the accepted enemy
    /// authorities, routes their terminal transition through #277 exactly once, captures the
    /// exact terminal transform position, realizes the admitted children, and binds collection
    /// to the production Run Session player identity. A Stage 1 restart rebuilds only this
    /// transient pickup composition for the new lifecycle generation; permanent state remains
    /// untouched and the new Run Session journal starts collection ordering at one.
    /// </summary>
    [DefaultExecutionOrder(21000)]
    [DisallowMultipleComponent]
    public sealed class Stage1RunPickupBootstrap2D : MonoBehaviour
    {
        private static readonly StableId RoomRuntimeStableId =
            StableId.Parse("room-runtime-instance.demo-cutover-level1");
        private static readonly StableId MobileDefinitionStableId =
            StableId.Parse("enemy.mobile-blaster-droid");
        private static readonly StableId TurretDefinitionStableId =
            StableId.Parse("enemy.blaster-turret");

        private Stage1PlayableLoopCompositionV1 stage1;
        private GameObject runtimeRoot;
        private StableId observedRunStableId;
        private long observedLifecycleGeneration = -1L;
        private RunSessionAuthorityV1 runAuthority;
        private RunSessionAggregateV1 run;
        private RunPickupSourcePositionRegistry2D sourcePositions;
        private RunPickupLiveCompositionV1 pickups;
        private TerminalDropBindingCompositionV1 terminalDrops;
        private RunPickupPresenter2D presenter;
        private Stage1PendingAdmissionPickupBridgeV1 admissionBridge;
        private readonly List<UnityEngine.Object> runtimeAssets =
            new List<UnityEngine.Object>();
        private string diagnostic = string.Empty;

        public bool IsComposed { get { return run != null && pickups != null; } }
        public string Diagnostic { get { return diagnostic; } }
        public RunSessionAggregateV1 RunSession { get { return run; } }
        public RunLocalPickupAuthorityV1 PickupAuthority
        {
            get { return pickups == null ? null : pickups.Authority; }
        }
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
            while (stage1 != null && !stage1.IsRunPickupProductionReady)
                yield return null;
            TryComposeCurrentRun();
        }

        private void LateUpdate()
        {
            if (stage1 == null)
                stage1 = GetComponent<Stage1PlayableLoopCompositionV1>();
            if (stage1 == null || !stage1.IsRunPickupProductionReady) return;

            long liveLifecycleGeneration;
            try
            {
                liveLifecycleGeneration = stage1.RunPickupController
                    .PlayerLiveAuthority.ExportSnapshot()
                    .Player.LifecycleGeneration;
            }
            catch (Exception exception)
            {
                diagnostic = "Stage 1 pickup lifecycle context unavailable: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;
                return;
            }

            if (observedRunStableId != stage1.RunPickupRunStableId
                || observedLifecycleGeneration != liveLifecycleGeneration)
            {
                TryComposeCurrentRun();
            }
            if (presenter != null && stage1.RunPickupRooms != null)
                presenter.Synchronize(stage1.RunPickupRooms.CurrentRoomStableId);
        }

        private void TryComposeCurrentRun()
        {
            try
            {
                ComposeCurrentRun();
                diagnostic = string.Empty;
            }
            catch (Exception exception)
            {
                diagnostic = "Stage 1 pickup integration failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;
                Debug.LogException(exception, this);
                TeardownCurrentRun();
            }
        }

        private void ComposeCurrentRun()
        {
            if (stage1 == null || !stage1.IsRunPickupProductionReady)
                throw new InvalidOperationException(
                    "Stage 1 pickup prerequisites are unavailable.");

            TeardownCurrentRun();
            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 selectedProfile;
            ShooterMover.Application.Persistence.Composition
                .CharacterCompositionCoordinatorV1 characterComposition;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out selectedProfile,
                    out characterComposition)
                || graph == null
                || selectedProfile == null
                || characterComposition == null)
            {
                throw new InvalidOperationException(
                    "The selected production character graph is unavailable for pickups.");
            }

            var missionResultPort = new ExistingMissionResultRunPortV1(
                stage1.RunPickupMissionResults,
                graph.LoadoutRuntime.Holdings,
                graph.StrongboxAuthority.ExportSnapshot);
            var portFactory = new Stage1PickupRunSessionRuntimePortFactoryV1(
                stage1.RunPickupController.PlayerLiveAuthority,
                stage1.RunPickupRooms,
                missionResultPort,
                stage1.RunPickupEffectEmitter.ClearEmittedEffects);
            var startSource = new ProductionCharacterRunSessionStartSourceV1(
                characterComposition,
                new Stage1PickupRunStatInputResolverV1(
                    stage1.RunPickupController.PlayerLiveAuthority),
                portFactory);
            runAuthority = new RunSessionAuthorityV1(startSource);

            long lifecycleGeneration = stage1.RunPickupController
                .PlayerLiveAuthority.ExportSnapshot().Player.LifecycleGeneration;
            var startCommand = new StartRunSessionCommandV1(
                StableId.Create(
                    "operation",
                    "stage1-pickup-run-start-g"
                        + lifecycleGeneration.ToString(
                            CultureInfo.InvariantCulture)),
                stage1.RunPickupRunStableId,
                string.Empty,
                graph.Character.CharacterInstanceStableId,
                graph.Character.Revision,
                graph.Character.Fingerprint,
                Level1AuthorableRoomDefinitionV1.LayoutStableId,
                StableId.Parse("difficulty.normal"),
                lifecycleGeneration,
                0L,
                RunSessionFingerprintV1.Hash(
                    "stage1-pickup-no-active-events-v1"));
            RunSessionStartResultV1 started = runAuthority.Start(startCommand);
            if (started == null
                || (started.Status != RunSessionStartStatusV1.Started
                    && started.Status != RunSessionStartStatusV1.ExactReplay)
                || !runAuthority.TryGetRun(
                    stage1.RunPickupRunStableId,
                    out run)
                || run == null)
            {
                throw new InvalidOperationException(
                    "The production pickup Run Session could not start: "
                    + (started == null
                        ? "result-null"
                        : started.RejectionCode));
            }

            runtimeRoot = new GameObject(
                "PICKUP-LIVE-001 Stage 1 Runtime");
            runtimeRoot.transform.SetParent(transform, false);
            sourcePositions = runtimeRoot.AddComponent<
                RunPickupSourcePositionRegistry2D>();
            pickups = RunPickupLiveCompositionV1.Create(
                run,
                sourcePositions);

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

            admissionBridge = new Stage1PendingAdmissionPickupBridgeV1(
                sourcePositions,
                pickups.PendingConsumer,
                presenter);
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

            RegisterEnemyObserver(
                stage1.RunPickupController.MobileBlasterDroid,
                stage1.RunPickupController.MobileBlasterDroid.transform,
                MobileDefinitionStableId,
                Level1AuthorableRoomDefinitionV1
                    .MovingDroidInstanceStableId,
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                enemyCatalog);
            RegisterEnemyObserver(
                stage1.RunPickupController.TurretPackage.Authority,
                stage1.RunPickupController.TurretPackage.transform,
                TurretDefinitionStableId,
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId,
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                enemyCatalog);

            observedRunStableId = stage1.RunPickupRunStableId;
            observedLifecycleGeneration = lifecycleGeneration;
            presenter.Synchronize(
                stage1.RunPickupRooms.CurrentRoomStableId);
        }

        private void RegisterEnemyObserver(
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
            admissionBridge.RegisterSource(
                run.RunStableId,
                run.LifecycleGeneration,
                identity.EntityInstanceId,
                placementStableId,
                roomStableId,
                sourceTransform);

            Stage1EnemyTerminalDropObserver2D observer =
                runtimeRoot.AddComponent<
                    Stage1EnemyTerminalDropObserver2D>();
            observer.Configure(
                authority,
                delegate { EmitEnemyDeath(definition, identity); });
        }

        private void EmitEnemyDeath(
            EnemyDefinitionV1 definition,
            EnemyRuntimeIdentityV1 identity)
        {
            if (run == null || terminalDrops == null || definition == null)
                return;
            RunPlayerRuntimeSnapshotV1 player =
                run.RuntimePorts.Player.ExportSnapshot();
            string suffix = run.RunStableId
                + "|"
                + run.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture)
                + "|"
                + identity.PlacementStableId;
            var fact = new EnemyDeathFactV1(
                StableId.Create(
                    "enemy-death-event",
                    DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                        suffix + "|death")),
                StableId.Create(
                    "combat-event",
                    DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                        suffix + "|trigger")),
                identity,
                definition.DefinitionId,
                1,
                run.LifecycleGeneration,
                player.ActorInstanceStableId,
                player.ParticipantStableId,
                definition.ExperienceProfileId,
                definition.DropProfileId,
                (EnemyActorDeathCause)1);
            terminalDrops.EnemyConsumer.Consume(fact);
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

        private void TeardownCurrentRun()
        {
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
            runtimeRoot = null;
            runAuthority = null;
            run = null;
            sourcePositions = null;
            pickups = null;
            terminalDrops = null;
            presenter = null;
            admissionBridge = null;
            observedRunStableId = null;
            observedLifecycleGeneration = -1L;
        }

        private void OnDestroy()
        {
            TeardownCurrentRun();
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.ContentPackages.Enemies.BlasterTurret;
using ShooterMover.ContentPackages.Enemies.MobileBlasterDroid;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// DEMO-CUTOVER-001 composition adapter. The retained Stage 1 controller supplies
    /// scene-authored Unity presentation only; this component connects the accepted
    /// player, weapon, enemy, room, mission-result and flow authorities into one loop.
    /// </summary>
    [DefaultExecutionOrder(20000)]
    [DisallowMultipleComponent]
    public sealed partial class Stage1PlayableLoopCompositionV1 : MonoBehaviour
    {
        private const int SimulationTicksPerSecond = 60;
        private const float DoorLaneHalfWidth = 2.2f;
        private const float DoorTraversalX = 10.75f;
        private const string PlayerActorStableIdText = "actor.vs007-player";

        private static readonly Dictionary<StableId, IEnemyActor2DAuthority>
            projectedRoomEnemies = new Dictionary<StableId, IEnemyActor2DAuthority>();
        private static Stage1PlayableLoopCompositionV1 activeComposition;
        private static Stage1ReadOnlyResultsProjectionV1 pendingResults;

        internal static void ClearPendingResults()
        {
            pendingResults = null;
        }

        private readonly Dictionary<Collider2D, EnemyBinding> enemyByCollider =
            new Dictionary<Collider2D, EnemyBinding>();
        private readonly HashSet<StableId> rewardedEnemies = new HashSet<StableId>();
        private readonly Dictionary<StableId, ParticipantRunStats> participantStats =
            new Dictionary<StableId, ParticipantRunStats>();
        private readonly List<PendingEnemyReward> pendingEnemyRewards =
            new List<PendingEnemyReward>();
        private static readonly string[] WeaponDisplayNames =
        {
            "Blaster",
            "Shotgun",
            "Rocket Launcher",
            "Flamethrower",
        };

        private readonly HashSet<InventoryWeaponEffectInstance2D> preparedEffects =
            new HashSet<InventoryWeaponEffectInstance2D>();
        private readonly HashSet<InventoryWeaponPersistentDamageArea2D> preparedPools =
            new HashSet<InventoryWeaponPersistentDamageArea2D>();
        private readonly List<UnityEngine.Object> runtimeAssets =
            new List<UnityEngine.Object>();

        private Stage1VisibleSliceController controller;
        private ProductionFlowCoordinatorV1 flow;
        private ProductionFlowProfileRecordV1 profile;
        private GameObject entryRoomRoot;
        private GameObject terminalRoomRoot;
        private RoomRuntimeComposition2D rooms;
        private RoomPresentationCatalog2D roomCatalog;
        private InventoryWeaponEffectEmitter2D effectEmitter;
        private InventoryWeaponRuntimeComposition weapons;
        private PlayerHoldingsService holdings;
        private EquipmentCatalog equipmentCatalog;
        private WeaponCatalog weaponCatalog;
        private PlayerExperienceAuthorityV1 experience;
        private EnemyExperienceRewardServiceV1 enemyRewards;
        private EmptyStrongboxMissionPortV1 missionPort;
        private MissionRunResultAuthorityV1 missionResults;
        private StableId runStableId;
        private long fireSequence;
        private long enemyDamageOrder;
        private long restartObserved;
        private bool playerDeathProjected;
        private bool initialized;
        private bool ending;
        private string diagnostic = string.Empty;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;

        public long Generation
        {
            get { return controller == null ? 0L : controller.RestartGeneration; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            projectedRoomEnemies.Clear();
            activeComposition = null;
            pendingResults = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InstallForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForScene(scene);
        }

        private static void InstallForScene(Scene scene)
        {
            if (string.Equals(
                    scene.path,
                    Stage1VisibleSliceController.ScenePath,
                    StringComparison.Ordinal))
            {
                Stage1VisibleSliceController visibleSlice =
                    FindInScene<Stage1VisibleSliceController>(scene);
                if (visibleSlice != null
                    && visibleSlice.GetComponent<Stage1PlayableLoopCompositionV1>() == null)
                {
                    visibleSlice.gameObject.AddComponent<Stage1PlayableLoopCompositionV1>();
                }

                return;
            }

            if (string.Equals(
                    scene.path,
                    ProductionFlowScenePathsV1.Results,
                    StringComparison.Ordinal)
                && pendingResults != null)
            {
                ProductionResultsControllerV1 existing =
                    FindInScene<ProductionResultsControllerV1>(scene);
                TextAsset suppliedBackground = ReadResultsBackground(existing);
                if (existing != null)
                {
                    existing.enabled = false;
                }

                GameObject owner = new GameObject("DEMO-CUTOVER-001 Read-Only Results");
                Stage1ReadOnlyResultsControllerV1 results =
                    owner.AddComponent<Stage1ReadOnlyResultsControllerV1>();
                results.Configure(pendingResults, suppliedBackground);
            }
        }

        internal static bool TryResolveProjectedEnemy(
            StableId placedInstanceStableId,
            out IEnemyActor2DAuthority authority)
        {
            authority = null;
            return placedInstanceStableId != null
                && projectedRoomEnemies.TryGetValue(
                    placedInstanceStableId,
                    out authority)
                && authority != null;
        }

        private IEnumerator Start()
        {
            controller = GetComponent<Stage1VisibleSliceController>();
            while (controller != null && !controller.IsInitialized)
            {
                yield return null;
            }

            if (controller == null)
            {
                diagnostic = "Stage1VisibleSliceController is unavailable.";
                yield break;
            }

            try
            {
                Compose();
                initialized = true;
            }
            catch (Exception exception)
            {
                diagnostic = exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void Compose()
        {
            activeComposition = this;
            flow = FindFirstObjectByType<ProductionFlowCoordinatorV1>(
                FindObjectsInactive.Include);
            if (flow == null || flow.Profile == null)
            {
                throw new InvalidOperationException(
                    "A confirmed production character profile is required before Level 1.");
            }

            profile = flow.Profile;
            ValidateAcceptedSceneAuthorities();
            RetireLegacyGameplayLoop();
            BuildInventoryAndWeaponAuthority();
            BuildExperienceAndMissionAuthorities();
            BuildAcceptedRoomRuntime();
            RegisterRealEnemies();
            restartObserved = controller.RestartGeneration;
            BeginRun();
            ProjectCurrentRoom(true);
        }

        private void ValidateAcceptedSceneAuthorities()
        {
            if (controller.PlayerLiveAuthority == null
                || !controller.PlayerLiveAuthority.IsInitialized)
            {
                throw new InvalidOperationException(
                    "The accepted PLAYER-LIVE adapter is not initialized.");
            }

            if (controller.MobileBlasterDroid == null
                || controller.TurretPackage == null
                || controller.TurretPackage.Authority == null)
            {
                throw new InvalidOperationException(
                    "The accepted moving-droid and turret authorities are required.");
            }

            if (controller.EntryExitDoor == null
                || controller.TerminalExitDoor == null
                || controller.PlayerTransform == null)
            {
                throw new InvalidOperationException(
                    "The Stage 1 presentation boundary is incomplete.");
            }
        }

        private void RetireLegacyGameplayLoop()
        {
            FieldInfo legacyRooms = typeof(Stage1VisibleSliceController).GetField(
                "roomMissionLayout",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (legacyRooms == null)
            {
                throw new MissingFieldException(
                    typeof(Stage1VisibleSliceController).FullName,
                    "roomMissionLayout");
            }

            legacyRooms.SetValue(controller, null);
            controller.enabled = false;
        }

        private void BuildInventoryAndWeaponAuthority()
        {
            equipmentCatalog = BuildEquipmentCatalog();
            holdings = new PlayerHoldingsService(
                StableId.Parse("authority.demo-cutover-player-holdings"),
                999L,
                new CatalogEquipmentValidatorV1(equipmentCatalog));

            string[] definitionIds =
            {
                "equipment.demo-cutover-blaster",
                "equipment.demo-cutover-shotgun",
                "equipment.demo-cutover-rocket-launcher",
                "equipment.demo-cutover-flamethrower",
            };
            StableId common = StableId.Parse("equipment-quality.common");
            int firstBoundSlot = -1;
            for (int index = 0; index < PlayerRouteProfilePayloadV1.WeaponSlotCount; index++)
            {
                StableId instanceId =
                    profile.Payload.WeaponSlots[index].EquipmentInstanceStableId;
                if (instanceId == null)
                {
                    continue;
                }
                if (firstBoundSlot < 0)
                {
                    firstBoundSlot = index;
                }

                EquipmentInstance instance = EquipmentInstance.Create(
                    instanceId,
                    StableId.Parse(definitionIds[index]),
                    1,
                    common,
                    Array.Empty<AugmentInstance>());
                AddEquipment(instance, index);
            }
            if (firstBoundSlot < 0)
            {
                throw new InvalidOperationException(
                    "The retained Stage 1 bootstrap requires one bound weapon position.");
            }

            weaponCatalog = BuildWeaponCatalog();
            GameObject emitterObject = new GameObject(
                "DEMO-CUTOVER-001 Inventory Weapon Effects");
            emitterObject.transform.SetParent(transform, false);
            effectEmitter = emitterObject.AddComponent<InventoryWeaponEffectEmitter2D>();

            var actorState = new PlayerWeaponActorStateSourceV1(controller);
            var activeWeapon = new RouteProfileActiveWeaponSource(
                profile.Payload,
                firstBoundSlot);
            var adapter = new InventoryBackedWeaponExecutionAdapter(
                holdings,
                equipmentCatalog,
                weaponCatalog,
                new PlayerWeaponOwnershipResolverV1(controller),
                effectEmitter,
                SimulationTicksPerSecond);
            weapons = new InventoryWeaponRuntimeComposition(
                actorState,
                activeWeapon,
                adapter);
        }

        private void BuildExperienceAndMissionAuthorities()
        {
            experience = new PlayerExperienceAuthorityV1(
                new PlayerExperienceCurveV1(
                    100L,
                    100L,
                    50,
                    new SoftActivationCurveParameters(0.1, 10L, 10L)),
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.normal"),
                    0,
                    new[] { StableId.Parse("progression-tag.campaign") }));
            enemyRewards = new EnemyExperienceRewardServiceV1(
                experience,
                new EnemyExperienceRewardCatalogV1(
                    new[]
                    {
                        Reward(
                            EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                            40L),
                        Reward(
                            EnemyExperienceRewardIdsV1.BlasterTurret,
                            60L),
                    }));
            missionPort = new EmptyStrongboxMissionPortV1(holdings);
            missionResults = new MissionRunResultAuthorityV1(missionPort);
        }

        private void BuildAcceptedRoomRuntime()
        {
            projectedRoomEnemies.Clear();
            projectedRoomEnemies.Add(
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                controller.MobileBlasterDroid);
            projectedRoomEnemies.Add(
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId,
                controller.TurretPackage.Authority);

            GameObject runtimeObject = new GameObject(
                "DEMO-CUTOVER-001 Room Runtime");
            runtimeObject.transform.SetParent(transform, false);
            rooms = runtimeObject.AddComponent<RoomRuntimeComposition2D>();
            roomCatalog = BuildRoomPresentationCatalog();
            rooms.ConfigureForTests(
                Level1AuthorableRoomDefinitionV1.Create(),
                roomCatalog,
                runtimeObject.transform);
            rooms.BuildSession(
                StableId.Parse("room-runtime-instance.demo-cutover-level1"));
            rooms.FinalExitReached += HandleFinalExitReached;
        }

        private void RegisterRealEnemies()
        {
            entryRoomRoot = controller.MobileBlasterDroid.transform.parent.gameObject;
            terminalRoomRoot = controller.TurretPackage.transform.parent.gameObject;

            RegisterEnemy(
                controller.MobileBlasterDroid.gameObject,
                controller.MobileBlasterDroid,
                EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId);
            RegisterEnemy(
                controller.TurretPackage.gameObject,
                controller.TurretPackage.Authority,
                EnemyExperienceRewardIdsV1.BlasterTurret,
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId);
        }

        private void RegisterEnemy(
            GameObject root,
            IEnemyActor2DAuthority authority,
            StableId definitionStableId,
            StableId roomInstanceStableId)
        {
            var binding = new EnemyBinding(
                authority,
                definitionStableId,
                roomInstanceStableId);
            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    enemyByCollider[colliders[index]] = binding;
                }
            }
        }

        private void BeginRun()
        {
            rewardedEnemies.Clear();
            participantStats.Clear();
            pendingEnemyRewards.Clear();
            ending = false;
            runStableId = StableId.Create(
                "run",
                "demo-cutover-level1-g"
                    + controller.RestartGeneration.ToString(CultureInfo.InvariantCulture));
            missionResults = new MissionRunResultAuthorityV1(missionPort);
        }
    }
}

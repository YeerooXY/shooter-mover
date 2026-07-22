using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.ContentPackages.Environment.Doors;
using ShooterMover.ContentPackages.Enemies.BlasterTurret;
using ShooterMover.ContentPackages.Enemies.MobileBlasterDroid;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using ShooterMover.ContentPackages.Rooms.Stage1VisibleSlicePresentation;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.ContentPackages.Weapons.Stage1Loadouts;
using ShooterMover.ContentPackages.Weapons.Stage1Presentation;
using ShooterMover.ContentPackages.Weapons.Stage1;
using ShooterMover.ContentPackages.Weapons.Shotgun;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Input;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Movement;
using ShooterMover.GameplayEntities;
using ShooterMover.Presentation.VisibleSliceBlasterTurret;
using ShooterMover.Presentation.VisibleSliceCameraReadability;
using ShooterMover.UI.VisibleSliceGeneralCombatHud;
using ShooterMover.UI.VisibleSliceLoadoutSelector;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Input;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Physics;
using ShooterMover.UnityAdapters.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.TestSupport.VisibleSlice
{
    /// <summary>
    /// VS-007's scene-local composition root. It wires accepted packages together and
    /// projects accepted player-authority lifecycle changes into disposable scene state.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class Stage1VisibleSliceController :
        MonoBehaviour,
        IGeneralCombatHudStateSource,
        IVisibleSliceBlasterTurretPresentationSource,
        IVisibleSliceReducedEffectsSource,
        IDoorTargetConditionReader,
        IVoidHazardCombatPort
    {
        public const string ScenePath =
            "Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity";
        public const int ExpectedWallCount = 4;
        public const int StartingPlayerHealth = 100;
        public const int TurretShotDamage = 10;
        public const int PlayerShotDamage = 1;
        private const float BlasterFireIntervalSeconds = 0.09f;
        private const float BlasterProjectileSpeed = 18f;
        private const float BlasterProjectileLifetimeSeconds = 2f;
        private const float BlasterProjectileRadius = 0.08f;
        private const float PlayerVisualScale = 0.9f;
        private const float PropGridSize = 0.5f;
        private static readonly Vector2 CrateCollisionSize = new Vector2(2.2f, 1.35f);
        private static readonly Vector2 ExplosiveCollisionSize = new Vector2(1.2f, 1.2f);

        [SerializeField] private Stage1VisibleSliceRoomPresentation roomPresentationPrefab;
        [SerializeField] private RoomContentDefinition2D[] roomContentDefinitions;
        [SerializeField] private VisibleSliceBlasterTurretPresenter turretPresentationPrefab;
        [SerializeField] private Sprite blasterShotSprite;
        [SerializeField] private Sprite turretShotSprite;
        [SerializeField] private Sprite playerSprite;
        [Header("Destructible prop VFX")]
        [SerializeField]
        private DestructiblePropDestructionAnimation crateDestructionAnimation;
        [SerializeField]
        private DestructiblePropDestructionAnimation explosiveDestructionAnimation;
        [SerializeField] private bool shootingSandbox = true;
        [SerializeField] private bool reducedEffects;
        [SerializeField] private bool grayscale;
        [Header("Live player identity")]
        [SerializeField] private string playerRunParticipantIdText =
            "participant.stage1-player";
        [SerializeField] private string playerCharacterIdText = "character.striker";
        [SerializeField] private string playerFactionIdText = "faction.player";

        private readonly List<GameObject> sessionObjects = new List<GameObject>();
        private readonly List<ShotTrace> shotTraces = new List<ShotTrace>();
        private readonly Dictionary<StableId, DemoRoomProjection> roomProjections =
            new Dictionary<StableId, DemoRoomProjection>();

        private Stage1VisibleSliceRoomPresentation roomPresentation;
        private GameplaySceneScope2D gameplayScope;
        private DoorController2D exitDoor;
        private DoorController2D entryExitDoor;
        private DoorController2D terminalExitDoor;
        private RoomMissionLayoutV1 roomMissionLayout;
        private RoomContentDefinition2D currentRoomContent;
        private VoidHazardAuthoring2D voidHazard;
        private VoidHazardTarget2D playerVoidTarget;
        private ObjectFamilyDefinitionAsset runtimeEnvironmentFamily;
        private VisibleSliceLoadoutSelector loadoutSelector;
        private VisibleSliceGeneralCombatHud combatHud;
        private Stage1WeaponStatusStrip weaponStrip;
        private VisibleSliceCameraRig cameraRig;
        private VisibleSliceBlasterTurretPresenter turretPresenter;
        private BlasterTurretPackage turretPackage;
        private BlasterTurretSceneContext2D turretSceneContext;
        private BlasterTurretDefinition turretDefinition;
        private MobileBlasterDroidRuntime2D mobileBlasterDroid;
        private MobileBlasterDroidDefinition mobileBlasterDroidDefinition;
        private DestructiblePropSet2D destructiblePropSet;
        private MovementActorLifecycle movementLifecycle;
        private MovementThrusterTuningProfile movementTuning;
        private MovementActorThrusterStatusReader thrusterReader;
        private PlayerCombatIntentAdapter combatInput;
        private CombatHit2DAdapter playerHitAdapter;
        private BoundedProjectile2D playerProjectileTemplate;
        private InputActionAsset inputActions;
        private Transform playerTransform;
        private Rigidbody2D playerBody;
        private Collider2D playerCollider;
        private EnemyTarget2DAdapter playerTargetAdapter;
        private SpriteRenderer playerBodyRenderer;
        private TrailRenderer playerBoostTrail;
        private Camera sceneCamera;
        private Material lineMaterial;
        private Stage1WeaponLoadoutFixture selectedLoadout;
        private Vector3 playerSpawn;
        private Stage1PlayerLiveAuthorityAdapterV1 playerLiveAuthority;
        private long restartGeneration;
        private long playerShotSequence;
        private long damageSequence;
        private long observedTurretShotSequence;
        private long droidDamageOrder;
        private int observedTurretHitCount;
        private int observedPlayerHitCount;
        private bool damageObserved;
        private bool firingObserved;
        private bool sessionActive;
        private bool initialized;
        private bool arenaComplete;
        private int voidDamageCount;
        private GUIStyle compactTitleStyle;
        private GUIStyle compactBodyStyle;
        private GUIStyle compactObjectiveStyle;
        private float nextBlasterShotTime;

        public bool ReducedEffectsEnabled => reducedEffects;
        public bool IsInitialized => initialized;
        public bool IsSessionActive => sessionActive;
        public bool IsPlayerGameplayActive => sessionActive
            && movementLifecycle != null
            && movementLifecycle.IsRunning
            && playerCollider != null
            && playerCollider.enabled;
        public bool IsPlayerDead => playerLiveAuthority != null
            && playerLiveAuthority.IsInitialized
            && playerLiveAuthority.ExportHudHealth().IsDead;
        public int PlayerHealth
        {
            get
            {
                PlayerHudHealthSnapshot health = playerLiveAuthority != null
                    && playerLiveAuthority.IsInitialized
                        ? playerLiveAuthority.ExportHudHealth()
                        : null;
                return health == null
                    ? StartingPlayerHealth
                    : Mathf.RoundToInt((float)health.CurrentHealth);
            }
        }
        public long RestartGeneration => playerLiveAuthority != null
            && playerLiveAuthority.IsInitialized
                ? playerLiveAuthority.ExportSnapshot().Player.LifecycleGeneration
                : restartGeneration;
        public StableId PlayerRunParticipantId =>
            StableId.Parse(playerRunParticipantIdText);
        public StableId PlayerCharacterId => StableId.Parse(playerCharacterIdText);
        public StableId PlayerFactionId => StableId.Parse(playerFactionIdText);
        public Stage1PlayerLiveAuthorityAdapterV1 PlayerLiveAuthority =>
            playerLiveAuthority;
        public Transform PlayerTransform => playerTransform;
        public Rigidbody2D PlayerBody => playerBody;
        public Collider2D PlayerCollider => playerCollider;
        public EnemyTarget2DAdapter PlayerTargetAdapter => playerTargetAdapter;
        public VoidHazardTarget2D PlayerVoidTarget => playerVoidTarget;
        public MovementActorLifecycle PlayerMovementLifecycle => movementLifecycle;
        public MovementThrusterTuningProfile PlayerMovementTuning => movementTuning;
        public InputActionAsset PlayerInputActions => inputActions;
        public TrailRenderer PlayerBoostTrail => playerBoostTrail;
        public SpriteRenderer PlayerBodyRenderer => playerBodyRenderer;
        public BlasterTurretPackage TurretPackage => turretPackage;
        public MobileBlasterDroidRuntime2D MobileBlasterDroid => mobileBlasterDroid;
        public DestructiblePropSet2D DestructiblePropSet => destructiblePropSet;
        public Stage1VisibleSliceRoomPresentation RoomPresentation => roomPresentation;
        public VisibleSliceLoadoutSelector LoadoutSelector => loadoutSelector;
        public Stage1WeaponLoadoutFixture SelectedLoadout => selectedLoadout;
        public VisibleSliceGeneralCombatHud CombatHud => combatHud;
        public Stage1WeaponStatusStrip WeaponStrip => weaponStrip;
        public VisibleSliceCameraRig CameraRig => cameraRig;
        public GameplaySceneScope2D GameplayScope => gameplayScope;
        public DoorController2D ExitDoor => exitDoor;
        public DoorController2D EntryExitDoor => entryExitDoor;
        public DoorController2D TerminalExitDoor => terminalExitDoor;
        public RoomMissionLayoutV1 RoomMissionLayout => roomMissionLayout;
        public StableId CurrentRoomStableId => roomMissionLayout == null
            ? null
            : roomMissionLayout.CurrentRoomState.RoomStableId;
        public string CurrentRoomDisplayName => currentRoomContent == null
            ? string.Empty
            : currentRoomContent.DisplayName;
        public VoidHazardAuthoring2D VoidHazard => voidHazard;
        public bool IsArenaComplete => arenaComplete;
        public int VoidDamageCount => voidDamageCount;

        public bool IsTargetDestroyed(StableId targetId)
        {
            return turretPackage != null
                && turretPackage.Authority != null
                && turretPackage.Authority.CurrentState != null
                && turretPackage.Authority.CurrentState.IsDestroyed;
        }

        public VoidHazardPortResult RequestDamage(VoidHazardDamageRequest request)
        {
            return playerLiveAuthority == null || !playerLiveAuthority.IsInitialized
                ? VoidHazardPortResult.Rejected
                : playerLiveAuthority.RequestDamage(request);
        }

        public VoidHazardPortResult RequestInstantDeath(VoidHazardInstantDeathRequest request)
        {
            return playerLiveAuthority == null || !playerLiveAuthority.IsInitialized
                ? VoidHazardPortResult.Rejected
                : playerLiveAuthority.RequestInstantDeath(request);
        }
        public int SessionObjectCount => sessionObjects.Count;
        public int ActiveProjectileCount
        {
            get
            {
                int count = 0;
                foreach (BoundedProjectile2D projectile in FindObjectsByType<BoundedProjectile2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
                {
                    if (projectile.IsInitialized && !projectile.IsComplete)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
        public int HudOwnerCount =>
            FindObjectsByType<VisibleSliceGeneralCombatHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        public int CameraOwnerCount =>
            FindObjectsByType<VisibleSliceCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        private void Awake()
        {
            ValidateSerializedDependencies();
            BuildSession();
            initialized = true;
            playerLiveAuthority = GetComponent<Stage1PlayerLiveAuthorityAdapterV1>();
            if (playerLiveAuthority == null)
            {
                playerLiveAuthority =
                    gameObject.AddComponent<Stage1PlayerLiveAuthorityAdapterV1>();
            }
            if (playerLiveAuthority == null || !playerLiveAuthority.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Stage 1 failed to compose the live player authority.");
            }
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (Keyboard.current != null
                && Keyboard.current.lKey.wasPressedThisFrame
                && !IsPlayerDead)
            {
                OpenLoadoutSelection();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                QuickRestart();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                SetReducedEffects(!reducedEffects);
            }

            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                SetGrayscale(!grayscale);
            }

            if (sessionActive)
            {
                ReadCombatInput();
            }

            RefreshHud();
            RefreshTurretPresentation();
            RefreshArenaFlow();
            RefreshBoostPresentation();
            TickShotTraces();
            damageObserved = false;
            firingObserved = false;
        }

        private void FixedUpdate()
        {
            if (!sessionActive || turretPackage == null || !turretPackage.IsConfigured)
            {
                return;
            }

            long emitted = turretPackage.Cadence.NextShotSequence;
            if (emitted > observedTurretShotSequence)
            {
                observedTurretShotSequence = emitted;
                firingObserved = true;
            }

            int turretHitCount = turretPackage.ConfirmedHitCount;
            while (observedTurretHitCount < turretHitCount)
            {
                observedTurretHitCount++;
            }

            int playerHitCount = playerHitAdapter == null
                ? 0
                : playerHitAdapter.ProcessedEventCount;
            while (observedPlayerHitCount < playerHitCount)
            {
                observedPlayerHitCount++;
                damageSequence++;
                damageObserved = true;
            }
        }

        public bool ConfirmDefaultLoadout()
        {
            if (loadoutSelector == null || !loadoutSelector.Visible)
            {
                return false;
            }

            return loadoutSelector.ApplyCommand(
                ShooterMover.UI.VisibleSliceLoadoutSelector.Core.LoadoutSelectorCommand.Confirm);
        }

        public bool FireAtTurretForTests()
        {
            if (shootingSandbox)
            {
                return turretPackage != null
                    && FireBlaster(turretPackage.transform.position);
            }

            if (!sessionActive || turretPackage == null || turretPackage.Authority == null)
            {
                return false;
            }

            EnemyActorState before = turretPackage.Authority.CurrentState;
            if (before == null || before.IsDestroyed)
            {
                return false;
            }

            StableId eventId = StableId.Create(
                "combat-event",
                "vs007-player-g" + restartGeneration + "-s" + playerShotSequence);
            playerShotSequence++;
            turretPackage.Authority.Apply(EnemyActorCommand.Damage(
                playerShotSequence,
                eventId,
                StableId.Parse("actor.vs007-player"),
                (int)CombatChannel.Kinetic,
                PlayerShotDamage));
            damageSequence++;
            damageObserved = true;

            Vector3 origin = playerTransform.position;
            Vector3 target = turretPackage.transform.position;
            CreateShotTrace(origin, target, new Color(0.2f, 0.9f, 1f, 1f));
            return turretPackage.Authority.CurrentState.Health < before.Health;
        }

        public bool FireAtMobileDroidForTests()
        {
            return mobileBlasterDroid != null
                && mobileBlasterDroid.CurrentState != null
                && !mobileBlasterDroid.CurrentState.IsDestroyed
                && FireBlaster(mobileBlasterDroid.transform.position);
        }

        public PlayerRuntimeRestartResult QuickRestart()
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

        public void SetReducedEffects(bool value)
        {
            reducedEffects = value;
            roomPresentation.SetReducedEffects(value);
            if (weaponStrip != null)
            {
                weaponStrip.SetReducedEffects(value);
            }
            if (turretPresenter != null)
            {
                turretPresenter.SetReducedEffectsOverride(value);
            }
        }

        public void SetGrayscale(bool value)
        {
            grayscale = value;
            if (turretPresenter != null)
            {
                turretPresenter.SetGrayscaleOverride(value);
            }
            sceneCamera.backgroundColor = value
                ? new Color(0.075f, 0.075f, 0.075f, 1f)
                : new Color(0.018f, 0.026f, 0.038f, 1f);
        }

        public bool TryRead(out GeneralCombatHudSnapshot snapshot)
        {
            bool entryRoom = CurrentRoomStableId
                == Level1RoomGraphDefinitionV1.EntryRoomStableId;
            EnemyActorState focused = entryRoom
                ? mobileBlasterDroid == null
                    ? null
                    : mobileBlasterDroid.CurrentState
                : turretPackage == null || turretPackage.Authority == null
                    ? null
                    : turretPackage.Authority.CurrentState;
            string enemyName = entryRoom
                ? "MOVING DROID"
                : "BLASTER TURRET";
            Vector2 reticle = ReadReticleViewport();
            PlayerRuntimeSnapshot live = playerLiveAuthority != null
                && playerLiveAuthority.IsInitialized
                    ? playerLiveAuthority.ExportSnapshot()
                    : null;
            snapshot = new GeneralCombatHudSnapshot(
                live == null
                    ? new VitalState(StartingPlayerHealth, StartingPlayerHealth, 0d, 0d)
                    : live.Player.VitalState,
                live == null
                    ? (thrusterReader == null ? null : thrusterReader.ReadSnapshot())
                    : live.Movement.ThrusterStatus,
                focused,
                focused == null ? "NO ENEMIES" : enemyName,
                string.IsNullOrEmpty(CurrentRoomDisplayName)
                    ? "LEVEL 1"
                    : CurrentRoomDisplayName,
                focused != null && focused.IsDestroyed
                    ? (exitDoor != null && exitDoor.IsOpen
                        ? (arenaComplete
                            ? "LEVEL COMPLETE  /  PRESS R TO RESTART"
                            : "ROOM CLEAR  /  REACH THE EAST DOOR")
                        : enemyName + " DOWN  /  OPENING EXIT")
                    : "DESTROY THE " + enemyName
                        + "  /  USE COVER OR KEEP MOVING",
                "R",
                "MENU",
                sessionActive,
                reticle.x,
                reticle.y,
                reducedEffects,
                live == null
                    ? restartGeneration
                    : live.Player.LifecycleGeneration);
            return true;
        }

        public bool TryReadSnapshot(out VisibleSliceBlasterTurretSnapshot snapshot)
        {
            snapshot = null;
            if (turretPackage == null || turretPackage.Authority == null)
            {
                return false;
            }

            EnemyActorState state = turretPackage.Authority.CurrentState;
            BlasterTurretCadence cadence = turretPackage.Cadence;
            VisibleSliceBlasterTurretPhase phase;
            if (!turretPackage.IsActive)
            {
                phase = VisibleSliceBlasterTurretPhase.Deactivated;
            }
            else if (state.IsDestroyed)
            {
                phase = VisibleSliceBlasterTurretPhase.Destroyed;
            }
            else if (firingObserved)
            {
                phase = VisibleSliceBlasterTurretPhase.Firing;
            }
            else if (cadence.Phase == BlasterTurretCadencePhase.Warning)
            {
                phase = VisibleSliceBlasterTurretPhase.Warning;
            }
            else if (cadence.Phase == BlasterTurretCadencePhase.Recovery)
            {
                phase = VisibleSliceBlasterTurretPhase.Recovery;
            }
            else
            {
                phase = VisibleSliceBlasterTurretPhase.Idle;
            }

            Vector2 warningDirection = turretPackage.CurrentFacing;
            double phaseDuration = cadence.Phase == BlasterTurretCadencePhase.Warning
                ? turretDefinition.WarningSeconds
                : turretDefinition.RecoverySeconds;
            snapshot = new VisibleSliceBlasterTurretSnapshot(
                restartGeneration,
                turretPackage.FixedStepCount,
                phase,
                Mathf.RoundToInt((float)state.Health),
                Mathf.RoundToInt((float)state.MaximumHealth),
                cadence.PhaseElapsedSeconds,
                phaseDuration,
                phase == VisibleSliceBlasterTurretPhase.Warning ? 1 : 0,
                warningDirection.x,
                warningDirection.y,
                damageObserved,
                damageSequence,
                reducedEffects,
                grayscale);
            return true;
        }

        private void BuildSession()
        {
            roomMissionLayout = new RoomMissionLayoutV1(
                Level1RoomGraphDefinitionV1.Create());
            ValidateRoomContentDefinitions();
            currentRoomContent = FindRoomContent(
                Level1RoomGraphDefinitionV1.EntryRoomStableId);
            playerSpawn = currentRoomContent.ForwardEntryPosition;
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lineMaterial.name = "VS007 Session Line Material";

            roomPresentation = CreateRoomPresentation();
            roomPresentation.name = "RoomPresentation";
            roomPresentation.SetReducedEffects(reducedEffects);
            sessionObjects.Add(roomPresentation.gameObject);

            BuildGameplayScope();
            BuildPropObstacles();
            BuildWalls();
            BuildPlayer();
            BuildVoidHazard();
            playerHitAdapter = new CombatHit2DAdapter(StableId.Parse("actor.vs007-player"));
            destructiblePropSet = Stage1DestructiblePropIntegration.Attach(
                gameObject,
                roomPresentation.PropRoot,
                transform,
                playerHitAdapter,
                PlayerShotDamage,
                () => RestartGeneration);
            GameObject turretContextObject = gameplayScope.gameObject;
            turretSceneContext =
                turretContextObject.AddComponent<BlasterTurretSceneContext2D>();
            turretSceneContext.Configure(
                playerTargetAdapter,
                playerHitAdapter,
                PlayerShotDamage,
                TurretShotDamage,
                null);
            playerProjectileTemplate = CreateProjectileTemplate(
                "PlayerBlasterProjectileTemplate",
                blasterShotSprite,
                new Vector3(0.09f, 0.09f, 1f));
            BuildCamera();
            BuildAuthoredRooms();
            BuildUi();
            selectedLoadout = Stage1WeaponLoadoutCatalog.Approved.DefaultFixture;
            sessionActive = shootingSandbox;
            SetGrayscale(grayscale);
            RefreshHud();
            RefreshTurretPresentation();
            RefreshArenaFlow();
        }

        private void BuildGameplayScope()
        {
            GameObject scopeObject = new GameObject("Demo002GameplayScope");
            scopeObject.transform.SetParent(transform, false);
            sessionObjects.Add(scopeObject);
            gameplayScope = scopeObject.AddComponent<GameplaySceneScope2D>();
            gameplayScope.ConfigureForTests(
                "scope.demo002-arena",
                "scope.gameplay",
                "projection.demo002-arena",
                "run.demo002",
                0L);
            runtimeEnvironmentFamily = ObjectFamilyDefinitionAsset.CreateRuntime(
                "family.demo002-environment",
                "DEMO-002 Environment",
                "variant.default",
                null,
                new ObjectVariantAuthoring("variant.default", 1));
        }

        private void BuildPlayer()
        {
            GameObject player = new GameObject("PlayerMover");
            player.transform.SetParent(transform, false);
            player.transform.position = playerSpawn;
            sessionObjects.Add(player);
            playerTransform = player.transform;

            playerBody = player.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D circle = player.AddComponent<CircleCollider2D>();
            circle.radius = 0.55f;
            playerCollider = circle;

            inputActions = BuildInputActions();
            PlayerMovementIntentAdapter movementInput = player.AddComponent<PlayerMovementIntentAdapter>();
            MovementContact2DAdapter contact = player.AddComponent<MovementContact2DAdapter>();
            movementLifecycle = player.AddComponent<MovementActorLifecycle>();
            movementTuning = BuildMovementTuning();
            movementLifecycle.Construct(playerBody, movementInput, inputActions, contact, movementTuning);
            movementLifecycle.StartActor();
            thrusterReader = new MovementActorThrusterStatusReader(
                movementLifecycle.Actor,
                movementTuning);

            combatInput = player.AddComponent<PlayerCombatIntentAdapter>();
            combatInput.Configure(inputActions);

            playerTargetAdapter = player.AddComponent<EnemyTarget2DAdapter>();
            playerTargetAdapter.Configure(
                StableId.Parse("actor.vs007-player"),
                player.transform,
                playerCollider);

            playerVoidTarget = player.AddComponent<VoidHazardTarget2D>();
            playerVoidTarget.ConfigureForTests(
                "actor.vs007-player",
                VoidHazardTargetCategory.Player,
                false,
                this,
                null,
                null,
                null,
                null);

            playerBodyRenderer = player.AddComponent<SpriteRenderer>();
            playerBodyRenderer.sprite = playerSprite != null
                ? playerSprite
                : CreateRuntimeSprite(
                    "VS007 Player",
                    new Color(0.14f, 0.82f, 1f, 1f));
            playerBodyRenderer.sortingOrder = 10;
            player.transform.localScale = new Vector3(
                PlayerVisualScale,
                PlayerVisualScale,
                1f);
            if (playerSprite == null)
            {
                CreateGunMount(player.transform, new Vector2(-0.48f, 0.34f));
                CreateGunMount(player.transform, new Vector2(0.48f, 0.34f));
                CreateGunMount(player.transform, new Vector2(-0.48f, -0.34f));
                CreateGunMount(player.transform, new Vector2(0.48f, -0.34f));
            }

            CreateBoostTrail(player.transform);
        }

        private void BuildCamera()
        {
            GameObject cameraObject = new GameObject("VisibleSliceCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            sessionObjects.Add(cameraObject);
            sceneCamera = cameraObject.AddComponent<Camera>();
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.nearClipPlane = 0.1f;
            sceneCamera.farClipPlane = 100f;
            sceneCamera.tag = "MainCamera";
            cameraRig = cameraObject.AddComponent<VisibleSliceCameraRig>();
            cameraRig.Configure(
                sceneCamera,
                new TransformCameraFollowSource(playerTransform),
                thrusterReader,
                this,
                VisibleSliceCameraConfiguration.CreateDefault(roomPresentation.RoomBounds));
        }

        private void BuildAuthoredRooms()
        {
            for (int index = 0; index < roomContentDefinitions.Length; index++)
            {
                RoomContentDefinition2D definition = roomContentDefinitions[index];
                GameObject root = new GameObject(
                    "RoomContent_" + definition.RoomStableIdText);
                root.transform.SetParent(transform, false);
                sessionObjects.Add(root);

                var projection = new DemoRoomProjection(definition, root);
                roomProjections.Add(definition.RoomStableId, projection);

                RoomContentPlacement2D[] placements = definition.Placements;
                for (int placementIndex = 0;
                    placementIndex < placements.Length;
                    placementIndex++)
                {
                    RoomContentPlacement2D placement = placements[placementIndex];
                    if (placement.PlacementKind != LevelPlacementKind.EnemySpawn)
                    {
                        continue;
                    }

                    if (placement.ContentStableId
                        == StableId.Parse("enemy.mobile-blaster-droid"))
                    {
                        BuildMobileBlasterDroid(root.transform, placement);
                        MobileBlasterDroidRuntime2D roomDroid = mobileBlasterDroid;
                        projection.RegisterEnemyDestroyedReader(
                            () => roomDroid != null
                                && roomDroid.CurrentState != null
                                && roomDroid.CurrentState.IsDestroyed);
                    }
                    else if (placement.ContentStableId
                        == StableId.Parse("enemy.blaster-turret"))
                    {
                        BuildTurret(root.transform, placement);
                        BlasterTurretPackage roomTurret = turretPackage;
                        projection.RegisterEnemyDestroyedReader(
                            () => roomTurret != null
                                && roomTurret.Authority != null
                                && roomTurret.Authority.CurrentState != null
                                && roomTurret.Authority.CurrentState.IsDestroyed);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "The visible slice does not have an enemy projector for "
                            + placement.ContentStableIdText + ".");
                    }
                }

                projection.ExitDoor = BuildExitDoor(
                    root.transform,
                    definition.RoomStableId
                        == Level1RoomGraphDefinitionV1.EntryRoomStableId
                            ? "placed.level1-entry-exit"
                            : "placed.level1-terminal-exit");
            }

            entryExitDoor = roomProjections[
                Level1RoomGraphDefinitionV1.EntryRoomStableId].ExitDoor;
            terminalExitDoor = roomProjections[
                Level1RoomGraphDefinitionV1.TerminalRoomStableId].ExitDoor;
            SwitchRoom(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                currentRoomContent.ForwardEntryPosition,
                false);
        }

        private void BuildTurret(
            Transform roomRoot,
            RoomContentPlacement2D placement)
        {
            GameObject turretObject = Instantiate(placement.Prefab, roomRoot);
            turretObject.name = "AcceptedBlasterTurret";
            turretObject.transform.position = SnapToGrid(
                placement.LocalPosition,
                PropGridSize);
            turretObject.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                placement.LocalRotationDegrees);
            sessionObjects.Add(turretObject);
            turretDefinition = BlasterTurretDefinition.CreateRuntime(
                60d,
                0d,
                1d,
                13d,
                0.7d,
                0.07d,
                70d,
                7.5d,
                0.5d,
                0.02d,
                4);
            BlasterTurretAuthoring2D authoring =
                turretObject.GetComponent<BlasterTurretAuthoring2D>();
            authoring.ConfigurePlacementForTests(
                placement.InstanceStableIdText.Replace("spawn.", "placed."),
                gameplayScope,
                "scope.gameplay");
            authoring.SetRuntimeOverrides(
                turretDefinition,
                turretShotSprite == null ? blasterShotSprite : turretShotSprite);
            if (!authoring.TryConfigureNow())
            {
                throw new InvalidOperationException(
                    "The authored Stage 1 Blaster Turret could not bind to its scene context.");
            }

            turretPackage = authoring.Package;

            turretPresenter = Instantiate(turretPresentationPrefab, roomRoot);
            turretPresenter.name = "VisibleSliceTurretPresentation";
            turretPresenter.transform.position = turretObject.transform.position;
            turretPresenter.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            turretPresenter.BindSource(this);
            turretPresenter.SetReducedEffectsOverride(reducedEffects);
            turretPresenter.SetGrayscaleOverride(grayscale);
            sessionObjects.Add(turretPresenter.gameObject);
        }

        private void BuildMobileBlasterDroid(
            Transform roomRoot,
            RoomContentPlacement2D placement)
        {
            GameObject droidObject = Instantiate(placement.Prefab, roomRoot);
            droidObject.name = "moving_droid";
            droidObject.transform.localPosition = placement.LocalPosition;
            droidObject.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                placement.LocalRotationDegrees);
            sessionObjects.Add(droidObject);

            mobileBlasterDroid =
                droidObject.GetComponent<MobileBlasterDroidRuntime2D>();
            if (mobileBlasterDroid == null)
            {
                throw new InvalidOperationException(
                    "The moving droid room prefab is missing MobileBlasterDroidRuntime2D.");
            }
            mobileBlasterDroidDefinition = MobileBlasterDroidDefinition.CreateRuntime(
                16d,
                2.5d,
                5d,
                0.5d,
                0.3d,
                0.8d,
                0.65d,
                4,
                0.55d,
                4d,
                0.2d);
            mobileBlasterDroid.ConfigureSession(
                mobileBlasterDroidDefinition,
                StableId.Create(
                    "actor",
                    placement.InstanceStableId.Value),
                playerTargetAdapter,
                new Collider2D[] { playerCollider },
                StableId.Parse("actor.vs007-player"),
                CombatWeightClass.Standard,
                playerProjectileTemplate);

            CombatHit2DTargetRegistrationStatus registration =
                mobileBlasterDroid.EnemyTarget.RegisterForCombatHits(playerHitAdapter);
            if (registration != CombatHit2DTargetRegistrationStatus.Registered
                && registration
                    != CombatHit2DTargetRegistrationStatus.AlreadyRegistered)
            {
                throw new InvalidOperationException(
                    "The moving droid could not register for player projectile hits: "
                    + registration);
            }

            playerHitAdapter.HitTranslated += HandlePlayerShotHit;
        }

        private void HandlePlayerShotHit(CombatHit2DTranslationResult translation)
        {
            if (translation == null
                || translation.Status != CombatHit2DTranslationStatus.Confirmed
                || translation.Message == null
                || mobileBlasterDroid == null
                || mobileBlasterDroid.EnemyTarget == null
                || translation.Message.TargetId
                    != mobileBlasterDroid.EnemyTarget.TargetId)
            {
                return;
            }

            mobileBlasterDroid.EnemyTarget.ApplyHit(
                translation.Message,
                PlayerShotDamage,
                droidDamageOrder);
            if (droidDamageOrder < long.MaxValue)
            {
                droidDamageOrder++;
            }
        }

        private void LateUpdate()
        {
            if (initialized)
            {
                EnforceCurrentRoomProjection();
            }
        }

        private DoorController2D BuildExitDoor(
            Transform roomRoot,
            string placedStableId)
        {
            GameObject doorObject = new GameObject(
                "RoomExitDoor_" + placedStableId);
            doorObject.transform.SetParent(roomRoot, false);
            doorObject.transform.position = new Vector3(14.5f, 0f, 0f);
            doorObject.SetActive(false);
            sessionObjects.Add(doorObject);

            BoxCollider2D closedCollider = doorObject.AddComponent<BoxCollider2D>();
            closedCollider.size = new Vector2(0.8f, 3.4f);

            GameObject closedRoot = new GameObject("ClosedPresentation");
            closedRoot.transform.SetParent(doorObject.transform, false);
            CreateRectSprite(
                closedRoot.transform,
                "DoorClosed",
                new Vector2(0.55f, 3.1f),
                new Color(0.9f, 0.33f, 0.12f, 0.95f),
                18);

            GameObject openRoot = new GameObject("OpenPresentation");
            openRoot.transform.SetParent(doorObject.transform, false);
            CreateRectSprite(
                openRoot.transform,
                "DoorOpen",
                new Vector2(0.14f, 3.1f),
                new Color(0.15f, 0.95f, 0.8f, 0.9f),
                18);

            PlacedObjectAuthoring2D placed =
                doorObject.AddComponent<PlacedObjectAuthoring2D>();
            placed.ConfigureForTests(
                placedStableId,
                runtimeEnvironmentFamily,
                "variant.default",
                gameplayScope,
                "scope.gameplay",
                null);

            DoorController2D door = doorObject.AddComponent<DoorController2D>();
            door.ConfigureForTests(
                placed,
                DoorInitialState.Closed,
                DoorConditionComposition.All,
                new[] { DoorConditionRequirement.InteractionRequested() },
                new Collider2D[] { closedCollider },
                closedRoot,
                openRoot,
                DoorOneWayPolicy.Bidirectional,
                null,
                null,
                false);
            door.SetConditionPortsForTests(null, null, null, null);
            doorObject.SetActive(true);
            if (!door.TryInitialize().IsValid)
            {
                throw new InvalidOperationException(
                    placedStableId + " failed to initialize.");
            }

            return door;
        }

        private void BuildVoidHazard()
        {
            GameObject hazardObject = new GameObject("Demo002VoidHazard");
            hazardObject.transform.SetParent(transform, false);
            hazardObject.transform.position = new Vector3(-1.5f, 4.2f, 0f);
            hazardObject.SetActive(false);
            sessionObjects.Add(hazardObject);

            BoxCollider2D hazardCollider = hazardObject.AddComponent<BoxCollider2D>();
            hazardCollider.isTrigger = true;
            hazardCollider.size = new Vector2(5.5f, 1.7f);
            CreateRectSprite(
                hazardObject.transform,
                "VoidSurface",
                hazardCollider.size,
                new Color(0.12f, 0.035f, 0.18f, 0.9f),
                3);
            CreateRectSprite(
                hazardObject.transform,
                "VoidEdge",
                new Vector2(5.5f, 0.08f),
                new Color(0.95f, 0.22f, 0.55f, 0.9f),
                4);

            PlacedObjectAuthoring2D placed =
                hazardObject.AddComponent<PlacedObjectAuthoring2D>();
            placed.ConfigureForTests(
                "placed.demo002-void-hazard",
                runtimeEnvironmentFamily,
                "variant.default",
                gameplayScope,
                "scope.gameplay",
                null);
            voidHazard = hazardObject.AddComponent<VoidHazardAuthoring2D>();
            voidHazard.ConfigureForTests(
                placed,
                hazardCollider,
                true,
                VoidPlayerResponseKind.Damage,
                35d,
                "checkpoint.demo002",
                VoidEnemyResponseKind.Ignore,
                VoidProjectileResponseKind.RemoveProjectile,
                VoidPropResponseKind.KeepSupported,
                null,
                null);
            hazardObject.SetActive(true);
            if (!voidHazard.TryActivate())
            {
                throw new InvalidOperationException(
                    "DEMO-002 void hazard failed to initialize.");
            }
        }

        private void BuildUi()
        {
            if (!shootingSandbox)
            {
                GameObject loadoutObject = new GameObject("FixedLoadoutSelector");
                loadoutObject.transform.SetParent(transform, false);
                sessionObjects.Add(loadoutObject);
                loadoutSelector = loadoutObject.AddComponent<VisibleSliceLoadoutSelector>();
                loadoutSelector.Confirmed += OnLoadoutConfirmed;
                loadoutSelector.Cancelled += OnLoadoutCancelled;
            }

            GameObject hudObject = new GameObject("GeneralCombatHud");
            hudObject.transform.SetParent(transform, false);
            sessionObjects.Add(hudObject);
            combatHud = hudObject.AddComponent<VisibleSliceGeneralCombatHud>();
            combatHud.BindSources(this, null);
            combatHud.enabled = false;

            if (!shootingSandbox)
            {
                GameObject stripObject = new GameObject("Stage1WeaponStatusStrip");
                stripObject.transform.SetParent(transform, false);
                sessionObjects.Add(stripObject);
                weaponStrip = stripObject.AddComponent<Stage1WeaponStatusStrip>();
                weaponStrip.SetTemporaryAudioEnabled(false);
                weaponStrip.SetReducedEffects(reducedEffects);
            }
        }

        private void RefreshArenaFlow()
        {
            if (IsPlayerDead || roomMissionLayout == null || playerTransform == null)
            {
                return;
            }

            StableId currentRoom = roomMissionLayout.CurrentRoomState.RoomStableId;
            bool inDoorLane = Mathf.Abs(playerTransform.position.y) <= 2.2f;
            DemoRoomProjection currentProjection = roomProjections[currentRoom];
            if (currentProjection.AreAllEnemiesDestroyed)
            {
                roomMissionLayout.CompleteCurrentRoom();
                currentProjection.ExitDoor.NotifyInteractionRequested();
            }

            if (currentRoom == Level1RoomGraphDefinitionV1.EntryRoomStableId)
            {
                if (entryExitDoor.IsOpen
                    && inDoorLane
                    && playerTransform.position.x >= 13.2f)
                {
                    if (roomMissionLayout.Traverse(
                        Level1RoomGraphDefinitionV1.ForwardExitStableId).Changed)
                    {
                        RoomContentDefinition2D destination = FindRoomContent(
                            Level1RoomGraphDefinitionV1.TerminalRoomStableId);
                        SwitchRoom(
                            destination.RoomStableId,
                            destination.ForwardEntryPosition,
                            true);
                    }
                }

                return;
            }

            if (currentRoom == Level1RoomGraphDefinitionV1.TerminalRoomStableId)
            {
                if (terminalExitDoor.IsOpen
                    && inDoorLane
                    && playerTransform.position.x <= -13.2f)
                {
                    if (roomMissionLayout.Traverse(
                        Level1RoomGraphDefinitionV1.ReturnExitStableId).Changed)
                    {
                        RoomContentDefinition2D destination = FindRoomContent(
                            Level1RoomGraphDefinitionV1.EntryRoomStableId);
                        SwitchRoom(
                            destination.RoomStableId,
                            destination.ReturnEntryPosition,
                            true);
                    }

                    return;
                }

                if (terminalExitDoor.IsOpen
                    && inDoorLane
                    && playerTransform.position.x >= 13.2f)
                {
                    arenaComplete = true;
                }
            }
        }

        private void SwitchRoom(
            StableId roomStableId,
            Vector2 entryPosition,
            bool movePlayer)
        {
            if (!roomProjections.TryGetValue(
                roomStableId,
                out DemoRoomProjection destination))
            {
                throw new InvalidOperationException(
                    "No room projection is authored for " + roomStableId + ".");
            }

            bool entryRoom = roomStableId
                == Level1RoomGraphDefinitionV1.EntryRoomStableId;
            SetMobileDroidProjectionActive(entryRoom);
            SetTurretProjectionActive(!entryRoom);
            entryExitDoor.transform.localPosition = entryRoom
                ? new Vector3(14.5f, 0f, 0f)
                : new Vector3(14.5f, 1000f, 0f);
            terminalExitDoor.transform.localPosition = entryRoom
                ? new Vector3(14.5f, 1000f, 0f)
                : new Vector3(14.5f, 0f, 0f);

            currentRoomContent = destination.Definition;
            exitDoor = destination.ExitDoor;
            if (movePlayer && playerBody != null)
            {
                playerBody.position = entryPosition;
                playerTransform.position = entryPosition;
                playerBody.linearVelocity = Vector2.zero;
                playerBody.angularVelocity = 0f;
            }

            EnforceCurrentRoomProjection();
        }

        private void EnforceCurrentRoomProjection()
        {
            bool entryRoom = CurrentRoomStableId
                == Level1RoomGraphDefinitionV1.EntryRoomStableId;
            if (mobileBlasterDroid != null)
            {
                bool droidDestroyed = mobileBlasterDroid.CurrentState != null
                    && mobileBlasterDroid.CurrentState.IsDestroyed;
                mobileBlasterDroid.EnemyCollider.enabled =
                    entryRoom && !droidDestroyed;
                SetRendererProjectionActive(
                    mobileBlasterDroid.gameObject,
                    entryRoom);
            }

            if (turretPackage != null)
            {
                EnemyActorState turretState = turretPackage.Authority == null
                    ? null
                    : turretPackage.Authority.CurrentState;
                turretPackage.EnemyCollider.enabled = !entryRoom
                    && turretState != null
                    && !turretState.IsDestroyed;
                SetRendererProjectionActive(
                    turretPackage.gameObject,
                    !entryRoom);
            }
        }

        private void SetMobileDroidProjectionActive(bool active)
        {
            if (mobileBlasterDroid == null)
            {
                return;
            }

            if (active)
            {
                mobileBlasterDroid.ActivateSession();
            }
            else
            {
                mobileBlasterDroid.DeactivateSession();
            }

            bool destroyed = mobileBlasterDroid.CurrentState != null
                && mobileBlasterDroid.CurrentState.IsDestroyed;
            mobileBlasterDroid.EnemyCollider.enabled = active && !destroyed;
            SetRendererProjectionActive(mobileBlasterDroid.gameObject, active);
        }

        private void SetTurretProjectionActive(bool active)
        {
            if (turretPackage == null)
            {
                return;
            }

            if (active)
            {
                turretPackage.Activate();
            }
            else
            {
                turretPackage.Deactivate();
            }

            EnemyActorState state = turretPackage.Authority == null
                ? null
                : turretPackage.Authority.CurrentState;
            turretPackage.EnemyCollider.enabled = active
                && state != null
                && !state.IsDestroyed;
            SetRendererProjectionActive(turretPackage.gameObject, active);
            if (turretPresenter != null)
            {
                turretPresenter.gameObject.SetActive(active);
            }
        }

        private static void SetRendererProjectionActive(
            GameObject root,
            bool active)
        {
            SpriteRenderer[] renderers =
                root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].enabled = active;
            }
        }

        private RoomContentDefinition2D FindRoomContent(StableId roomStableId)
        {
            for (int index = 0; index < roomContentDefinitions.Length; index++)
            {
                RoomContentDefinition2D definition = roomContentDefinitions[index];
                if (definition != null && definition.RoomStableId == roomStableId)
                {
                    return definition;
                }
            }

            throw new InvalidOperationException(
                "Missing room content definition for " + roomStableId + ".");
        }

        private void ValidateRoomContentDefinitions()
        {
            if (roomContentDefinitions == null || roomContentDefinitions.Length == 0)
            {
                throw new InvalidOperationException(
                    "The visible slice requires authored room content definitions.");
            }

            var seenRooms = new HashSet<StableId>();
            for (int index = 0; index < roomContentDefinitions.Length; index++)
            {
                RoomContentDefinition2D definition = roomContentDefinitions[index];
                if (definition == null)
                {
                    throw new InvalidOperationException(
                        "Room content definition " + index + " is missing.");
                }

                definition.ValidateOrThrow();
                if (!seenRooms.Add(definition.RoomStableId))
                {
                    throw new InvalidOperationException(
                        "Duplicate room content definition: "
                        + definition.RoomStableIdText);
                }
            }

            for (int index = 0;
                index < roomMissionLayout.Definition.Rooms.Count;
                index++)
            {
                StableId requiredRoom =
                    roomMissionLayout.Definition.Rooms[index].RoomStableId;
                if (!seenRooms.Contains(requiredRoom))
                {
                    throw new InvalidOperationException(
                        "The room graph has no authored content for "
                        + requiredRoom + ".");
                }
            }
        }

        private void RefreshCompactHud()
        {
            if (compactTitleStyle == null)
            {
                compactTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.82f, 0.95f, 1f) }
                };
                compactBodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(0.85f, 0.9f, 0.95f) }
                };
                compactObjectiveStyle = new GUIStyle(compactTitleStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14
                };
            }
        }

        private void OnGUI()
        {
            if (!initialized)
            {
                return;
            }

            RefreshCompactHud();

            EnemyActorState turretState = turretPackage == null || turretPackage.Authority == null
                ? null
                : turretPackage.Authority.CurrentState;
            EnemyActorState droidState = mobileBlasterDroid == null
                ? null
                : mobileBlasterDroid.CurrentState;
            bool entryRoom = CurrentRoomStableId
                == Level1RoomGraphDefinitionV1.EntryRoomStableId;
            bool turretDestroyed = turretState != null && turretState.IsDestroyed;
            bool droidDestroyed = droidState != null && droidState.IsDestroyed;
            bool currentEnemyDestroyed = entryRoom ? droidDestroyed : turretDestroyed;
            string objective = currentEnemyDestroyed
                ? (arenaComplete
                    ? "LEVEL CLEAR"
                    : entryRoom
                        ? "ROOM CLEAR  /  ENTER EAST DOOR"
                        : "EXIT UNLOCKED  /  ENTER EAST DOOR")
                : entryRoom
                    ? "DESTROY MOVING DROID"
                    : "DESTROY TURRET  /  EVADE ITS FACING";

            Color previous = GUI.color;
            GUI.color = new Color(0.015f, 0.025f, 0.04f, 0.86f);
            GUI.Box(new Rect(18f, 18f, 235f, 76f), GUIContent.none);
            GUI.Box(new Rect(Screen.width - 253f, 18f, 235f, 76f), GUIContent.none);
            GUI.Box(new Rect(Screen.width * 0.5f - 230f, 18f, 460f, 42f), GUIContent.none);
            GUI.color = Color.white;

            GUI.Label(new Rect(30f, 26f, 210f, 22f), "PLAYER", compactTitleStyle);
            PlayerHudHealthSnapshot playerHud = playerLiveAuthority != null
                && playerLiveAuthority.IsInitialized
                    ? playerLiveAuthority.ExportHudHealth()
                    : null;
            double displayedHealth = playerHud == null
                ? StartingPlayerHealth
                : playerHud.CurrentHealth;
            double displayedMaximum = playerHud == null
                ? StartingPlayerHealth
                : playerHud.MaximumHealth;
            GUI.Label(new Rect(30f, 51f, 210f, 20f),
                "HP " + FormatHealth(displayedHealth)
                    + "/" + FormatHealth(displayedMaximum),
                compactBodyStyle);
            GUI.Label(new Rect(30f, 72f, 210f, 18f),
                "WASD MOVE   SHIFT BOOST   L LOADOUT",
                compactBodyStyle);

            GUI.Label(new Rect(Screen.width - 241f, 26f, 210f, 22f),
                string.IsNullOrEmpty(CurrentRoomDisplayName)
                    ? "CURRENT ROOM"
                    : CurrentRoomDisplayName,
                compactTitleStyle);
            GUI.Label(new Rect(Screen.width - 241f, 51f, 210f, 20f),
                entryRoom
                    ? droidState == null
                        ? "DROID OFFLINE"
                        : droidDestroyed
                            ? "DROID DESTROYED"
                            : "DROID HP "
                                + Mathf.RoundToInt((float)droidState.Health)
                                + "/"
                                + Mathf.RoundToInt((float)droidState.MaximumHealth)
                    : turretState == null
                        ? "TURRET OFFLINE"
                        : turretDestroyed
                            ? "TURRET DESTROYED"
                            : "TURRET HP "
                                + Mathf.RoundToInt((float)turretState.Health)
                                + "/"
                                + Mathf.RoundToInt((float)turretState.MaximumHealth),
                compactBodyStyle);
            GUI.Label(new Rect(Screen.width - 241f, 72f, 210f, 18f),
                entryRoom
                    ? "CLEAR TO UNLOCK ROOM 2"
                    : "WEST: RETURN   EAST: FINISH",
                compactBodyStyle);
            GUI.Label(
                new Rect(Screen.width * 0.5f - 220f, 24f, 440f, 28f),
                objective,
                compactObjectiveStyle);
            GUI.color = previous;
        }

        private SpriteRenderer CreateRectSprite(
            Transform parent,
            string name,
            Vector2 size,
            Color color,
            int sortingOrder)
        {
            GameObject visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateRuntimeSprite(name, color);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private BoundedProjectile2D CreateProjectileTemplate(
            string objectName,
            Sprite sprite,
            Vector3 visualScale)
        {
            GameObject projectileObject = new GameObject(objectName);
            projectileObject.transform.SetParent(transform, false);
            projectileObject.transform.localPosition = Vector3.zero;
            projectileObject.transform.localScale = visualScale;
            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 40;
            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.simulated = false;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
            projectileCollider.isTrigger = true;
            BoundedProjectile2D projectile = projectileObject.AddComponent<BoundedProjectile2D>();
            projectileObject.SetActive(false);
            sessionObjects.Add(projectileObject);
            return projectile;
        }

        private void BuildWalls()
        {
            Rect bounds = roomPresentation.RoomBounds;
            const float thickness = 0.8f;
            CreateWall("Wall_North", new Vector2(bounds.center.x, bounds.yMax), new Vector2(bounds.width, thickness));
            CreateWall("Wall_South", new Vector2(bounds.center.x, bounds.yMin), new Vector2(bounds.width, thickness));
            CreateWall("Wall_East", new Vector2(bounds.xMax, bounds.center.y), new Vector2(thickness, bounds.height));
            CreateWall("Wall_West", new Vector2(bounds.xMin, bounds.center.y), new Vector2(thickness, bounds.height));
        }

        private void BuildPropObstacles()
        {
            if (roomPresentation == null || roomPresentation.PropRoot == null)
            {
                return;
            }

            Transform propRoot = roomPresentation.PropRoot;
            for (int index = 0; index < propRoot.childCount; index++)
            {
                Transform visual = propRoot.GetChild(index);
                visual.localPosition = SnapToGrid(visual.localPosition, PropGridSize);

                Vector2 collisionSize;
                Vector2 collisionOffset = Vector2.zero;
                DestructiblePropAuthoring2D authoring =
                    visual.GetComponent<DestructiblePropAuthoring2D>();
                if (authoring == null
                    && visual.name.StartsWith("Crate_", StringComparison.Ordinal))
                {
                    authoring = visual.gameObject.AddComponent<DestructiblePropAuthoring2D>();
                    authoring.ConfigureGenerated(
                        Stage1DestructiblePropIntegration.CrateMaximumHealth,
                        CrateCollisionSize,
                        Vector2.zero,
                        crateDestructionAnimation,
                        Stage1TerminalDropContentV1
                            .ResolveLegacyAuthoringKey(visual.name)
                            .WithPlacement(
                                Level1AuthorableRoomDefinitionV1
                                    .EntryRoomStableId,
                                Stage1DestructiblePropIntegration
                                    .CreateLegacyPlacementId(authoring)));
                }
                else if (authoring == null
                    && visual.name.StartsWith("Explosive_", StringComparison.Ordinal))
                {
                    authoring = visual.gameObject.AddComponent<DestructiblePropAuthoring2D>();
                    authoring.ConfigureGenerated(
                        Stage1DestructiblePropIntegration.ExplosiveMaximumHealth,
                        ExplosiveCollisionSize,
                        Vector2.zero,
                        explosiveDestructionAnimation,
                        Stage1TerminalDropContentV1
                            .ResolveLegacyAuthoringKey(visual.name)
                            .WithPlacement(
                                Level1AuthorableRoomDefinitionV1
                                    .EntryRoomStableId,
                                Stage1DestructiblePropIntegration
                                    .CreateLegacyPlacementId(authoring)));
                }

                if (authoring != null)
                {
                    collisionSize = authoring.ColliderSize;
                    collisionOffset = authoring.ColliderOffset;
                }
                else if (visual.name.StartsWith("Crate_", StringComparison.Ordinal))
                {
                    collisionSize = CrateCollisionSize;
                }
                else if (visual.name.StartsWith("Explosive_", StringComparison.Ordinal))
                {
                    collisionSize = ExplosiveCollisionSize;
                }
                else
                {
                    continue;
                }

                GameObject obstacle = new GameObject(visual.name + "_Collision");
                obstacle.transform.SetParent(transform, false);
                obstacle.transform.position = new Vector3(
                    visual.position.x + collisionOffset.x,
                    visual.position.y + collisionOffset.y,
                    0f);
                obstacle.transform.rotation = visual.rotation;
                BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
                collider.size = collisionSize;
                obstacle.AddComponent<VisibleSliceWallContract>();
                sessionObjects.Add(obstacle);
            }
        }

        private void CreateWall(string name, Vector2 position, Vector2 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(transform, false);
            wall.transform.position = position;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
            wall.AddComponent<VisibleSliceWallContract>();
            sessionObjects.Add(wall);
        }

        private static Vector3 SnapToGrid(Vector3 position, float gridSize)
        {
            if (gridSize <= 0f)
            {
                return position;
            }

            return new Vector3(
                Mathf.Round(position.x / gridSize) * gridSize,
                Mathf.Round(position.y / gridSize) * gridSize,
                position.z);
        }

        private void ReadCombatInput()
        {
            PlayerIntentFrame intent = combatInput.ReadIntentFrame();
            if ((intent.Fire.IsHeld || intent.Fire.WasPressed)
                && Time.unscaledTime >= nextBlasterShotTime)
            {
                FireSelectedLoadout();
                nextBlasterShotTime = Time.unscaledTime + BlasterFireIntervalSeconds;
            }

            Vector2 direction = ReadAimWorld() - (Vector2)playerTransform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                playerTransform.up = direction.normalized;
            }
        }

        private void OnLoadoutConfirmed(Stage1WeaponLoadoutFixture fixture)
        {
            selectedLoadout = fixture;
            sessionActive = true;
        }

        private void OnLoadoutCancelled()
        {
            selectedLoadout = Stage1WeaponLoadoutCatalog.Approved.DefaultFixture;
            sessionActive = shootingSandbox;
        }

        private void OpenLoadoutSelection()
        {
            if (loadoutSelector == null)
            {
                GameObject loadoutObject = new GameObject("RuntimeLoadoutSelector");
                loadoutObject.transform.SetParent(transform, false);
                sessionObjects.Add(loadoutObject);
                loadoutSelector = loadoutObject.AddComponent<VisibleSliceLoadoutSelector>();
                loadoutSelector.Confirmed += OnLoadoutConfirmed;
                loadoutSelector.Cancelled += OnLoadoutCancelled;
            }

            loadoutSelector.ResetForRestart();
            selectedLoadout = null;
            sessionActive = false;
        }

        private void RefreshHud()
        {
            combatHud.RefreshFromSources(Time.unscaledTimeAsDouble);
        }

        private void RefreshTurretPresentation()
        {
            if (turretPresenter != null)
            {
                if (turretPackage != null)
                {
                    float angle = Vector2.SignedAngle(
                        Vector2.left,
                        turretPackage.CurrentFacing);
                    turretPresenter.transform.rotation =
                        Quaternion.Euler(0f, 0f, angle);
                }

                turretPresenter.RefreshFromSource(Time.unscaledTimeAsDouble);
            }
        }

        private void RefreshBoostPresentation()
        {
            if (playerTransform == null || playerBodyRenderer == null || thrusterReader == null)
            {
                return;
            }

            ThrusterStatusSnapshot status = thrusterReader.ReadSnapshot();
            bool isBoosting = status != null && status.IsBursting;
            playerTransform.localScale = new Vector3(
                PlayerVisualScale,
                PlayerVisualScale,
                1f);
            playerBodyRenderer.color = isBoosting
                ? new Color(0.72f, 0.96f, 1f, 1f)
                : Color.white;
            if (playerBoostTrail != null)
            {
                playerBoostTrail.emitting = isBoosting;
            }
        }

        private Vector2 ReadAimWorld()
        {
            if (Mouse.current == null || sceneCamera == null)
            {
                return turretPackage == null
                    ? (Vector2)playerTransform.position + Vector2.up
                    : (Vector2)turretPackage.transform.position;
            }

            Vector2 pointer = Mouse.current.position.ReadValue();
            Vector3 world = sceneCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, 10f));
            return new Vector2(world.x, world.y);
        }

        private bool FireBlaster()
        {
            return FireBlaster(ReadAimWorld());
        }

        private bool FireSelectedLoadout()
        {
            Stage1WeaponLoadoutFixture loadout = selectedLoadout
                ?? Stage1WeaponLoadoutCatalog.Approved.DefaultFixture;
            bool fired = false;
            for (int index = 0; index < loadout.Slots.Count; index++)
            {
                fired |= FireWeapon(loadout.Slots[index].WeaponId, ReadAimWorld());
            }

            return fired;
        }

        private bool FireWeapon(StableId weaponId, Vector2 aimPoint)
        {
            if (weaponId == Stage1WeaponPackageDescriptor.ShotgunId)
            {
                ShotgunTuning tuning = ShotgunPackageDefinition.NormalTuning;
                bool fired = false;
                for (int index = 0; index < tuning.PelletCount; index++)
                {
                    double spread = ShotgunSpreadBehaviorModule.GetOffsetDegrees(
                        index,
                        tuning.PelletCount,
                        tuning.SpreadDegrees);
                    fired |= FireProjectile(
                        aimPoint,
                        (float)tuning.ProjectileSpeed,
                        (float)tuning.ProjectileLifetimeSeconds,
                        (float)tuning.ProjectileRadius,
                        (float)spread);
                }

                return fired;
            }

            if (weaponId == Stage1WeaponPackageDescriptor.RocketLauncherId)
            {
                return FireProjectile(aimPoint, 8f, 3f, 0.16f, 0f);
            }

            if (weaponId == Stage1WeaponPackageDescriptor.ArcGunId)
            {
                return FireProjectile(aimPoint, 22f, 1.5f, 0.1f, 0f);
            }

            if (weaponId == Stage1WeaponPackageDescriptor.RicochetGunId)
            {
                return FireProjectile(aimPoint, 16f, 2.5f, 0.09f, 0f);
            }

            return FireProjectile(
                aimPoint,
                BlasterProjectileSpeed,
                BlasterProjectileLifetimeSeconds,
                BlasterProjectileRadius,
                0f);
        }

        private bool FireProjectile(
            Vector2 aimPoint,
            float speed,
            float lifetime,
            float radius,
            float spreadDegrees)
        {
            if (!sessionActive
                || playerProjectileTemplate == null
                || playerHitAdapter == null
                || playerTransform == null)
            {
                return false;
            }

            Vector2 direction = aimPoint - (Vector2)playerTransform.position;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = playerTransform.up;
            }

            direction = Quaternion.Euler(0f, 0f, spreadDegrees)
                * direction.normalized;
            BoundedProjectile2D projectile = Instantiate(playerProjectileTemplate, transform);
            projectile.gameObject.name = "PlayerWeaponShot";
            projectile.transform.position = new Vector3(
                playerTransform.position.x,
                playerTransform.position.y,
                0f);
            projectile.gameObject.SetActive(true);

            StableId eventId = StableId.Create(
                "combat-event",
                "vs007-player-g" + restartGeneration + "-s" + playerShotSequence);
            playerShotSequence++;
            Vector2 origin = (Vector2)playerTransform.position + direction * 0.9f;
            bool initializedProjectile = projectile.TryInitialize(
                eventId,
                origin,
                direction,
                speed,
                lifetime,
                radius,
                CombatChannel.Kinetic,
                playerHitAdapter,
                new[] { playerCollider },
                false,
                0.12f);
            if (!initializedProjectile)
            {
                Destroy(projectile.gameObject);
                return false;
            }

            projectile.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            return true;
        }

        private bool FireBlaster(Vector2 aimPoint)
        {
            if (!sessionActive
                || playerProjectileTemplate == null
                || playerHitAdapter == null
                || playerTransform == null)
            {
                return false;
            }

            Vector2 direction = aimPoint - (Vector2)playerTransform.position;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = playerTransform.up;
            }
            direction.Normalize();

            BoundedProjectile2D projectile = Instantiate(playerProjectileTemplate, transform);
            projectile.gameObject.name = "PlayerBlasterShot";
            projectile.transform.position = new Vector3(
                playerTransform.position.x,
                playerTransform.position.y,
                0f);
            projectile.gameObject.SetActive(true);

            StableId eventId = StableId.Create(
                "combat-event",
                "vs007-player-g" + restartGeneration + "-s" + playerShotSequence);
            playerShotSequence++;
            Vector2 origin = (Vector2)playerTransform.position + direction * 0.9f;
            bool initializedProjectile = projectile.TryInitialize(
                eventId,
                origin,
                direction,
                BlasterProjectileSpeed,
                BlasterProjectileLifetimeSeconds,
                BlasterProjectileRadius,
                CombatChannel.Kinetic,
                playerHitAdapter,
                new[] { playerCollider },
                false,
                0.12f);
            if (!initializedProjectile)
            {
                Destroy(projectile.gameObject);
                return false;
            }

            projectile.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            return true;
        }

        private void ApplyPlayerProjectileDamageToTurret()
        {
            if (turretPackage == null
                || turretPackage.Authority == null
                || turretPackage.Authority.CurrentState == null
                || turretPackage.Authority.CurrentState.IsDestroyed)
            {
                return;
            }

            StableId eventId = StableId.Create(
                "combat-event",
                "vs007-player-hit-g" + restartGeneration + "-s" + playerShotSequence);
            playerShotSequence++;
            turretPackage.Authority.Apply(
                EnemyActorCommand.Damage(
                    playerShotSequence,
                    eventId,
                    StableId.Parse("actor.vs007-player"),
                    (int)CombatChannel.Kinetic,
                    PlayerShotDamage));
            damageSequence++;
            damageObserved = true;
        }

        private static string FormatHealth(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void CancelPlayerProjectiles()
        {
            BoundedProjectile2D[] projectiles =
                FindObjectsByType<BoundedProjectile2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < projectiles.Length; index++)
            {
                BoundedProjectile2D projectile = projectiles[index];
                if (projectile == null
                    || !projectile.IsInitialized
                    || projectile.IsComplete
                    || (!string.Equals(projectile.name, "PlayerWeaponShot",
                            StringComparison.Ordinal)
                        && !string.Equals(projectile.name, "PlayerBlasterShot",
                            StringComparison.Ordinal)))
                {
                    continue;
                }
                projectile.Cancel();
            }
        }

        private Vector2 ReadReticleViewport()
        {
            if (Mouse.current == null || Screen.width < 1 || Screen.height < 1)
            {
                return new Vector2(0.5f, 0.5f);
            }

            Vector2 pointer = Mouse.current.position.ReadValue();
            return new Vector2(
                Mathf.Clamp01(pointer.x / Screen.width),
                Mathf.Clamp01(pointer.y / Screen.height));
        }

        private void CreateGunMount(Transform parent, Vector2 localPosition)
        {
            GameObject mount = new GameObject("ParallelBlasterMount");
            mount.transform.SetParent(parent, false);
            mount.transform.localPosition = localPosition;
            SpriteRenderer renderer = mount.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateRuntimeSprite("VS007 Mount", new Color(0.82f, 0.9f, 0.96f, 1f));
            renderer.sortingOrder = 11;
            mount.transform.localScale = new Vector3(0.22f, 0.48f, 1f);
        }

        private void CreateBoostTrail(Transform parent)
        {
            GameObject trailObject = new GameObject("BoostTrail");
            trailObject.transform.SetParent(parent, false);
            trailObject.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            playerBoostTrail = trailObject.AddComponent<TrailRenderer>();
            playerBoostTrail.material = lineMaterial;
            playerBoostTrail.time = 0.24f;
            playerBoostTrail.minVertexDistance = 0.04f;
            playerBoostTrail.widthMultiplier = 0.5f;
            playerBoostTrail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 0.08f));
            playerBoostTrail.colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(0.92f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(0.12f, 0.82f, 1f), 0.4f),
                    new GradientColorKey(new Color(0.05f, 0.32f, 1f), 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.65f, 0.45f),
                    new GradientAlphaKey(0f, 1f),
                },
            };
            playerBoostTrail.sortingOrder = 9;
            playerBoostTrail.emitting = false;
        }

        private Sprite CreateRuntimeSprite(string spriteName, Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = spriteName + " Texture";
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = spriteName;
            return sprite;
        }

        private void CreateShotTrace(Vector3 origin, Vector3 target, Color color)
        {
            GameObject traceObject = new GameObject("PlayerShotTrace");
            traceObject.transform.SetParent(transform, false);
            LineRenderer line = traceObject.AddComponent<LineRenderer>();
            line.material = lineMaterial;
            line.positionCount = 2;
            line.SetPosition(0, origin);
            line.SetPosition(1, target);
            line.startWidth = 0.08f;
            line.endWidth = 0.025f;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.1f);
            line.sortingOrder = 30;
            shotTraces.Add(new ShotTrace(traceObject, 0.09f));
        }

        private void TickShotTraces()
        {
            for (int index = shotTraces.Count - 1; index >= 0; index--)
            {
                ShotTrace trace = shotTraces[index];
                trace.RemainingSeconds -= Time.unscaledDeltaTime;
                if (trace.RemainingSeconds <= 0f)
                {
                    Destroy(trace.Root);
                    shotTraces.RemoveAt(index);
                }
            }
        }

        private void ClearShotTraces()
        {
            foreach (ShotTrace trace in shotTraces)
            {
                if (trace.Root != null)
                {
                    DestroyImmediate(trace.Root);
                }
            }

            shotTraces.Clear();
        }

        private static InputActionAsset BuildInputActions()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "VS007 Session Input";
            InputActionMap movement = asset.AddActionMap("Movement");
            InputAction move = movement.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            movement.AddAction("Aim", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");
            InputAction thruster = movement.AddAction("Thruster", InputActionType.Button);
            thruster.AddBinding("<Keyboard>/leftShift");
            thruster.AddBinding("<Keyboard>/rightShift");
            thruster.AddBinding("<Keyboard>/space");
            thruster.AddBinding("<Gamepad>/rightShoulder");
            thruster.AddBinding("<Gamepad>/buttonSouth");

            InputActionMap combat = asset.AddActionMap("Combat");
            InputAction aim = combat.AddAction("Aim", InputActionType.Value, expectedControlLayout: "Vector2");
            aim.AddBinding("<Pointer>/delta");
            aim.AddBinding("<Gamepad>/rightStick");
            InputAction fire = combat.AddAction("Fire", InputActionType.Button);
            fire.AddBinding("<Mouse>/leftButton");
            fire.AddBinding("<Gamepad>/rightTrigger");
            InputAction power = combat.AddAction("Power", InputActionType.Button);
            power.AddBinding("<Mouse>/rightButton");
            power.AddBinding("<Gamepad>/leftTrigger");
            return asset;
        }

        private static MovementThrusterTuningProfile BuildMovementTuning()
        {
            return MovementThrusterTuningProfile.Create(
                MovementThrusterTuningProfile.CurrentProfileVersion,
                StableId.Parse("tuning.vs007-visible-slice"),
                8d, 34d, 44d, 70d, 1.2d,
                2, 1, 1.75d, 2.3d, 0.28d, 0.1d, 0.05d, 120d,
                0.04d, 0.2d, 0.75d, 2d,
                0.8d, 0.15d, 4d, 4,
                0.8d, 0.9d, 0.1d, 0.5d, 0.02d, 128);
        }

        private void ValidateSerializedDependencies()
        {
            StableId parsedIdentity;
            bool validPlayerIdentity =
                StableId.TryParse(playerRunParticipantIdText, out parsedIdentity)
                && StableId.TryParse(playerCharacterIdText, out parsedIdentity)
                && StableId.TryParse(playerFactionIdText, out parsedIdentity);
            if (blasterShotSprite == null
                || turretPresentationPrefab == null
                || roomContentDefinitions == null
                || roomContentDefinitions.Length == 0
                || !validPlayerIdentity)
            {
                throw new InvalidOperationException(
                    "VS-007 scene prefab bindings: room=" + (roomPresentationPrefab != null)
                    + " shot=" + (blasterShotSprite != null)
                    + " presentation=" + (turretPresentationPrefab != null)
                    + " room-content="
                    + (roomContentDefinitions == null
                        ? 0
                        : roomContentDefinitions.Length)
                    + " player-identity=" + validPlayerIdentity);
            }

            if (roomPresentationPrefab == null)
            {
                Debug.LogWarning(
                    "VS-007 room presentation prefab is missing; using the built-in room presentation fallback.",
                    this);
            }
        }

        private Stage1VisibleSliceRoomPresentation CreateRoomPresentation()
        {
            if (roomPresentationPrefab != null)
            {
                return Instantiate(roomPresentationPrefab, transform);
            }

            GameObject fallbackObject = new GameObject("RoomPresentationFallback");
            fallbackObject.transform.SetParent(transform, false);
            return fallbackObject.AddComponent<Stage1VisibleSliceRoomPresentation>();
        }

        private void OnDestroy()
        {
            if (playerHitAdapter != null)
            {
                playerHitAdapter.HitTranslated -= HandlePlayerShotHit;
            }

            if (loadoutSelector != null)
            {
                loadoutSelector.Confirmed -= OnLoadoutConfirmed;
                loadoutSelector.Cancelled -= OnLoadoutCancelled;
            }

            if (combatHud != null)
            {
                combatHud.UnbindSources();
            }

            ClearShotTraces();
            if (lineMaterial != null)
            {
                DestroyImmediate(lineMaterial);
            }

            if (inputActions != null)
            {
                DestroyImmediate(inputActions);
            }

            if (turretDefinition != null)
            {
                DestroyImmediate(turretDefinition);
            }

            if (mobileBlasterDroidDefinition != null)
            {
                DestroyImmediate(mobileBlasterDroidDefinition);
            }

            if (runtimeEnvironmentFamily != null)
            {
                DestroyImmediate(runtimeEnvironmentFamily);
            }
        }

        private sealed class ShotTrace
        {
            public ShotTrace(GameObject root, float remainingSeconds)
            {
                Root = root;
                RemainingSeconds = remainingSeconds;
            }

            public GameObject Root { get; }
            public float RemainingSeconds { get; set; }
        }

        private sealed class DemoRoomProjection
        {
            private readonly List<Func<bool>> enemyDestroyedReaders =
                new List<Func<bool>>();

            public DemoRoomProjection(
                RoomContentDefinition2D definition,
                GameObject root)
            {
                Definition = definition
                    ?? throw new ArgumentNullException(nameof(definition));
                Root = root ?? throw new ArgumentNullException(nameof(root));
            }

            public RoomContentDefinition2D Definition { get; }

            public GameObject Root { get; }

            public DoorController2D ExitDoor { get; set; }

            public bool AreAllEnemiesDestroyed
            {
                get
                {
                    for (int index = 0; index < enemyDestroyedReaders.Count; index++)
                    {
                        if (!enemyDestroyedReaders[index]())
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public void RegisterEnemyDestroyedReader(Func<bool> reader)
            {
                enemyDestroyedReaders.Add(
                    reader ?? throw new ArgumentNullException(nameof(reader)));
            }
        }
    }

    /// <summary>Explicit contact classification for the four session-only room colliders.</summary>
    public sealed class VisibleSliceWallContract : MonoBehaviour, IMovementContact2DContract
    {
        public bool TryDescribeMovementContact(out MovementContact2DDescriptor descriptor)
        {
            descriptor = MovementContact2DDescriptor.Wall();
            return true;
        }
    }
}

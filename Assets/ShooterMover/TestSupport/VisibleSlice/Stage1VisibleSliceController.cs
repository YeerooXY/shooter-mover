using System;
using System.Collections.Generic;
using ShooterMover.ContentPackages.Enemies.BlasterTurret;
using ShooterMover.ContentPackages.Rooms.Stage1VisibleSlicePresentation;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.ContentPackages.Weapons.Stage1Loadouts;
using ShooterMover.ContentPackages.Weapons.Stage1Presentation;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Input;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Movement;
using ShooterMover.Presentation.VisibleSliceBlasterTurret;
using ShooterMover.Presentation.VisibleSliceCameraReadability;
using ShooterMover.UI.VisibleSliceGeneralCombatHud;
using ShooterMover.UI.VisibleSliceLoadoutSelector;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Input;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.TestSupport.VisibleSlice
{
    /// <summary>
    /// VS-007's scene-local composition root. It wires accepted packages together and
    /// owns only disposable prototype presentation plus one session-only player vital.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage1VisibleSliceController :
        MonoBehaviour,
        IGeneralCombatHudStateSource,
        IVisibleSliceBlasterTurretPresentationSource,
        IVisibleSliceReducedEffectsSource
    {
        public const string ScenePath =
            "Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity";
        public const int ExpectedWallCount = 4;
        public const int StartingPlayerHealth = 100;
        public const int TurretShotDamage = 10;
        public const int PlayerShotDamage = 6;

        [SerializeField] private Stage1VisibleSliceRoomPresentation roomPresentationPrefab;
        [SerializeField] private GameObject blasterTurretPrefab;
        [SerializeField] private VisibleSliceBlasterTurretPresenter turretPresentationPrefab;
        [SerializeField] private bool reducedEffects;
        [SerializeField] private bool grayscale;

        private readonly List<GameObject> sessionObjects = new List<GameObject>();
        private readonly List<ShotTrace> shotTraces = new List<ShotTrace>();

        private Stage1VisibleSliceRoomPresentation roomPresentation;
        private VisibleSliceLoadoutSelector loadoutSelector;
        private VisibleSliceGeneralCombatHud combatHud;
        private Stage1WeaponStatusStrip weaponStrip;
        private VisibleSliceCameraRig cameraRig;
        private VisibleSliceBlasterTurretPresenter turretPresenter;
        private BlasterTurretPackage turretPackage;
        private BlasterTurretDefinition turretDefinition;
        private MovementActorLifecycle movementLifecycle;
        private MovementThrusterTuningProfile movementTuning;
        private MovementActorThrusterStatusReader thrusterReader;
        private PlayerCombatIntentAdapter combatInput;
        private InputActionAsset inputActions;
        private Transform playerTransform;
        private Rigidbody2D playerBody;
        private Collider2D playerCollider;
        private Camera sceneCamera;
        private Material lineMaterial;
        private Stage1WeaponLoadoutFixture selectedLoadout;
        private Vector3 playerSpawn;
        private int playerHealth;
        private long restartGeneration;
        private long playerShotSequence;
        private long damageSequence;
        private long observedTurretShotSequence;
        private bool damageObserved;
        private bool firingObserved;
        private bool sessionActive;
        private bool initialized;

        public bool ReducedEffectsEnabled => reducedEffects;
        public bool IsInitialized => initialized;
        public bool IsSessionActive => sessionActive;
        public int PlayerHealth => playerHealth;
        public long RestartGeneration => restartGeneration;
        public Transform PlayerTransform => playerTransform;
        public BlasterTurretPackage TurretPackage => turretPackage;
        public Stage1VisibleSliceRoomPresentation RoomPresentation => roomPresentation;
        public VisibleSliceLoadoutSelector LoadoutSelector => loadoutSelector;
        public VisibleSliceGeneralCombatHud CombatHud => combatHud;
        public Stage1WeaponStatusStrip WeaponStrip => weaponStrip;
        public VisibleSliceCameraRig CameraRig => cameraRig;
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
        }

        private void Update()
        {
            if (!initialized)
            {
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
                playerHealth = Mathf.Max(0, playerHealth - TurretShotDamage);
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

        public void QuickRestart()
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
            selectedLoadout = null;
            sessionActive = false;

            ClearShotTraces();
            playerBody.position = playerSpawn;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            movementLifecycle.RestartActor();
            turretPackage.RestartSession();
            loadoutSelector.ResetForRestart();
            combatHud.ResetPresentationForRestart(restartGeneration);
            cameraRig.Restart();
            RefreshHud();
            RefreshTurretPresentation();
        }

        public void SetReducedEffects(bool value)
        {
            reducedEffects = value;
            roomPresentation.SetReducedEffects(value);
            weaponStrip.SetReducedEffects(value);
            turretPresenter.SetReducedEffectsOverride(value);
        }

        public void SetGrayscale(bool value)
        {
            grayscale = value;
            turretPresenter.SetGrayscaleOverride(value);
            sceneCamera.backgroundColor = value
                ? new Color(0.075f, 0.075f, 0.075f, 1f)
                : new Color(0.018f, 0.026f, 0.038f, 1f);
        }

        public bool TryRead(out GeneralCombatHudSnapshot snapshot)
        {
            EnemyActorState focused = turretPackage == null || turretPackage.Authority == null
                ? null
                : turretPackage.Authority.CurrentState;
            Vector2 reticle = ReadReticleViewport();
            snapshot = new GeneralCombatHudSnapshot(
                new VitalState(playerHealth, StartingPlayerHealth, 0d, 0d),
                thrusterReader == null ? null : thrusterReader.ReadSnapshot(),
                focused,
                "BLASTER TURRET",
                "FOUNDRY TEST BAY",
                focused != null && focused.IsDestroyed ? "ROOM CLEAR" : "DESTROY THE TURRET",
                "R",
                "MENU",
                sessionActive,
                reticle.x,
                reticle.y,
                reducedEffects,
                restartGeneration);
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

            Vector2 warningDirection = ((Vector2)playerTransform.position
                - (Vector2)turretPackage.transform.position).normalized;
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
            playerHealth = StartingPlayerHealth;
            playerSpawn = new Vector3(-8f, -2f, 0f);
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lineMaterial.name = "VS007 Session Line Material";

            roomPresentation = Instantiate(roomPresentationPrefab, transform);
            roomPresentation.name = "RoomPresentation";
            roomPresentation.SetReducedEffects(reducedEffects);
            sessionObjects.Add(roomPresentation.gameObject);

            BuildWalls();
            BuildPlayer();
            BuildCamera();
            BuildTurret();
            BuildUi();
            SetGrayscale(grayscale);
            RefreshHud();
            RefreshTurretPresentation();
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

            EnemyTarget2DAdapter playerTarget = player.AddComponent<EnemyTarget2DAdapter>();
            playerTarget.Configure(
                StableId.Parse("actor.vs007-player"),
                player.transform,
                playerCollider);

            SpriteRenderer body = player.AddComponent<SpriteRenderer>();
            body.sprite = CreateRuntimeSprite("VS007 Player", new Color(0.14f, 0.82f, 1f, 1f));
            body.sortingOrder = 10;
            player.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
            CreateGunMount(player.transform, new Vector2(-0.48f, 0.34f));
            CreateGunMount(player.transform, new Vector2(0.48f, 0.34f));
            CreateGunMount(player.transform, new Vector2(-0.48f, -0.34f));
            CreateGunMount(player.transform, new Vector2(0.48f, -0.34f));
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

        private void BuildTurret()
        {
            GameObject turretObject = Instantiate(blasterTurretPrefab, transform);
            turretObject.name = "AcceptedBlasterTurret";
            turretObject.transform.position = new Vector3(7f, 2.5f, 0f);
            sessionObjects.Add(turretObject);
            turretPackage = turretObject.GetComponent<BlasterTurretPackage>();
            BoundedProjectile2D projectileTemplate = CreateProjectileTemplate();
            turretDefinition = BlasterTurretDefinition.CreateRuntime(
                30d, 0.65d, 1.1d, 28d, 0.7d, 0.07d, 0.5d, 0.02d, 4);
            EnemyTarget2DAdapter playerTarget = playerTransform.GetComponent<EnemyTarget2DAdapter>();
            turretPackage.Configure(
                turretDefinition,
                playerTarget,
                playerCollider,
                projectileTemplate,
                StableId.Parse("enemy.vs007-blaster-turret"),
                StableId.Parse("actor.vs007-player"),
                CombatWeightClass.Standard);

            turretPresenter = Instantiate(turretPresentationPrefab, transform);
            turretPresenter.name = "VisibleSliceTurretPresentation";
            turretPresenter.transform.position = turretObject.transform.position;
            turretPresenter.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
            turretPresenter.BindSource(this);
            turretPresenter.SetReducedEffectsOverride(reducedEffects);
            turretPresenter.SetGrayscaleOverride(grayscale);
            sessionObjects.Add(turretPresenter.gameObject);
        }

        private void BuildUi()
        {
            GameObject loadoutObject = new GameObject("FixedLoadoutSelector");
            loadoutObject.transform.SetParent(transform, false);
            sessionObjects.Add(loadoutObject);
            loadoutSelector = loadoutObject.AddComponent<VisibleSliceLoadoutSelector>();
            loadoutSelector.Confirmed += OnLoadoutConfirmed;
            loadoutSelector.Cancelled += OnLoadoutCancelled;

            GameObject hudObject = new GameObject("GeneralCombatHud");
            hudObject.transform.SetParent(transform, false);
            sessionObjects.Add(hudObject);
            combatHud = hudObject.AddComponent<VisibleSliceGeneralCombatHud>();
            combatHud.BindSources(this, null);

            GameObject stripObject = new GameObject("Stage1WeaponStatusStrip");
            stripObject.transform.SetParent(transform, false);
            sessionObjects.Add(stripObject);
            weaponStrip = stripObject.AddComponent<Stage1WeaponStatusStrip>();
            weaponStrip.SetTemporaryAudioEnabled(false);
            weaponStrip.SetReducedEffects(reducedEffects);
        }

        private BoundedProjectile2D CreateProjectileTemplate()
        {
            GameObject projectileObject = new GameObject("AcceptedBoundedProjectileTemplate");
            projectileObject.transform.SetParent(transform, false);
            projectileObject.transform.position = new Vector3(0f, 0f, -50f);
            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.simulated = false;
            projectileObject.AddComponent<CircleCollider2D>();
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

        private void ReadCombatInput()
        {
            PlayerIntentFrame intent = combatInput.ReadIntentFrame();
            if (intent.Fire.WasPressed)
            {
                FireAtTurretForTests();
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
            selectedLoadout = null;
            sessionActive = false;
        }

        private void RefreshHud()
        {
            combatHud.RefreshFromSources(Time.unscaledTimeAsDouble);
        }

        private void RefreshTurretPresentation()
        {
            turretPresenter.RefreshFromSource(Time.unscaledTimeAsDouble);
        }

        private Vector2 ReadAimWorld()
        {
            if (Mouse.current == null || sceneCamera == null)
            {
                return turretPackage == null
                    ? Vector2.zero
                    : (Vector2)turretPackage.transform.position;
            }

            Vector2 pointer = Mouse.current.position.ReadValue();
            Vector3 world = sceneCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, 10f));
            return new Vector2(world.x, world.y);
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
            thruster.AddBinding("<Gamepad>/rightShoulder");

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
            if (roomPresentationPrefab == null
                || blasterTurretPrefab == null
                || turretPresentationPrefab == null)
            {
                throw new InvalidOperationException(
                    "VS-007 scene prefab bindings: room=" + (roomPresentationPrefab != null)
                    + " turret=" + (blasterTurretPrefab != null)
                    + " presentation=" + (turretPresentationPrefab != null));
            }
        }

        private void OnDestroy()
        {
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

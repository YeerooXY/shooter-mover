using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UI.LevelSelection;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Generic production traversal composition for catalogue-selected authored JSON levels.
    /// The selected character graph remains the sole owner of profile, holdings and loadout.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class ProductionPlayableLevelControllerV1 : MonoBehaviour
    {
        [SerializeField] private JsonRoomRuntimeBootstrap2D roomBootstrap;
        [SerializeField] private RoomRuntimeComposition2D roomRuntime;
        [SerializeField] private JsonRoomVisualPresentation2D visualPresentation;
        [SerializeField] private RoomEnemySpawner2D enemySpawner;
        [SerializeField] private RoomPresentationCatalog2D presentationCatalog;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private float playerSpeed = 6f;
        [SerializeField] private float cameraSize = 8f;

        private readonly List<GameObject> ownedBindings = new List<GameObject>();
        private ProductionPlayableLevelDefinitionV1 levelDefinition;
        private ProductionCharacterRuntimeGraphV1 characterGraph;
        private object exactHoldingsAuthority;
        private object exactLoadoutAuthority;
        private string routeFingerprint;
        private PlayablePlayerMarker2D playerMarker;
        private Rigidbody2D playerBody;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private long operationSequence;
        private bool isConfigured;
        private bool completionAccepted;
        private bool failureReturnRequested;
        private string diagnostic = string.Empty;

        public bool IsConfigured { get { return isConfigured; } }
        public string Diagnostic { get { return diagnostic; } }
        public StableId LevelStableId
        {
            get { return levelDefinition == null ? null : levelDefinition.LevelStableId; }
        }
        public StableId CharacterInstanceStableId
        {
            get
            {
                return characterGraph == null
                    ? null
                    : characterGraph.Character.CharacterInstanceStableId;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeHook()
        {
            SceneManager.sceneLoaded -= HandleProductionSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimeHook()
        {
            SceneManager.sceneLoaded -= HandleProductionSceneLoaded;
            SceneManager.sceneLoaded += HandleProductionSceneLoaded;
            TryStartScene(SceneManager.GetActiveScene());
        }

        private static void HandleProductionSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryStartScene(scene);
        }

        private static void TryStartScene(Scene scene)
        {
            if (!scene.IsValid()
                || !string.Equals(
                    scene.path,
                    ProductionPlayableLevelCatalogV1.PlayableLevelScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            ProductionPlayableLevelControllerV1 controller = FindInScene(scene);
            if (controller == null)
            {
                Debug.LogError("playable-level-controller-missing");
                ReturnCurrentCharacterToHub();
                return;
            }
            controller.BeginFromProductionContext();
        }

        private static ProductionPlayableLevelControllerV1 FindInScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                ProductionPlayableLevelControllerV1 value = roots[index]
                    .GetComponentInChildren<ProductionPlayableLevelControllerV1>(true);
                if (value != null) return value;
            }
            return null;
        }

        private void BeginFromProductionContext()
        {
            if (isConfigured)
            {
                FailAndReturn("playable-level-controller-duplicate-configuration");
                return;
            }

            PlayerRouteProfilePayloadV1 routePayload;
            StableId selectedModeStableId;
            StableId selectedLevelStableId;
            if (!LevelSelectionRouteContextV1.TryRead(
                    out routePayload,
                    out selectedModeStableId,
                    out selectedLevelStableId)
                || selectedLevelStableId == null)
            {
                FailAndReturn("playable-level-selection-context-missing");
                return;
            }

            ProductionPlayableLevelDefinitionV1 selectedLevel;
            if (!ProductionPlayableLevelCatalogV1.TryResolve(
                    selectedLevelStableId,
                    out selectedLevel)
                || selectedLevel == null)
            {
                FailAndReturn(
                    "playable-level-definition-missing:" + selectedLevelStableId);
                return;
            }
            if (!string.Equals(
                selectedLevel.GameplayScenePath,
                gameObject.scene.path,
                StringComparison.Ordinal))
            {
                FailAndReturn("playable-level-scene-route-mismatch");
                return;
            }

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out profile)
                || graph == null
                || profile == null
                || graph.IsDisposed)
            {
                FailAndReturn("playable-level-character-context-missing");
                return;
            }
            if (routePayload == null
                || !routePayload.HasValidFingerprint()
                || !graph.RoutePayload.Equals(routePayload)
                || !profile.Payload.Equals(routePayload))
            {
                FailAndReturn("playable-level-character-route-mismatch");
                return;
            }

            Begin(selectedLevel, graph);
        }

        private void Begin(
            ProductionPlayableLevelDefinitionV1 selectedLevel,
            ProductionCharacterRuntimeGraphV1 graph)
        {
            try
            {
                ValidateSceneReferences();
                levelDefinition = selectedLevel
                    ?? throw new ArgumentNullException(nameof(selectedLevel));
                characterGraph = graph
                    ?? throw new ArgumentNullException(nameof(graph));
                exactHoldingsAuthority = graph.LoadoutRuntime.Holdings;
                exactLoadoutAuthority = graph.LoadoutRuntime.LoadoutAuthority;
                routeFingerprint = graph.RoutePayload.Fingerprint;

                JsonRoomContentDefinition2D roomContent =
                    Resources.Load<JsonRoomContentDefinition2D>(
                        selectedLevel.RoomContentResourcePath);
                if (roomContent == null)
                {
                    throw Failure(
                        "playable-level-json-asset-missing:"
                        + selectedLevel.RoomContentResourcePath);
                }
                EnemyCatalogAsset2D enemyCatalog =
                    Resources.Load<EnemyCatalogAsset2D>(
                        selectedLevel.EnemyCatalogResourcePath);
                if (enemyCatalog == null)
                {
                    throw Failure(
                        "playable-level-enemy-catalog-missing:"
                        + selectedLevel.EnemyCatalogResourcePath);
                }

                roomBootstrap.Configure(
                    roomContent,
                    roomRuntime,
                    presentationCatalog,
                    presentationRoot,
                    "room-runtime-instance.playable-level");
                roomRuntime.CurrentRoomPresentationRebuilt +=
                    HandleRoomPresentationRebuilt;
                roomRuntime.FinalExitReached += HandleFinalExitReached;

                if (!roomBootstrap.BuildFromJson())
                {
                    throw Failure(BuildImportDiagnostic());
                }
                ValidateImportedLevel();
                CreateRuntimeSprite();
                SpawnExactlyOnePlayer();
                CreateExactlyOneGameplayCamera();
                SynchronizeCurrentRoom();

                if (!visualPresentation.Synchronize())
                {
                    throw Failure(
                        "playable-level-room-visual-composition-rejected:"
                        + visualPresentation.LastBuildError);
                }
                if (!enemySpawner.Synchronize())
                {
                    throw Failure(
                        "playable-level-room-enemy-composition-rejected:"
                        + enemySpawner.LastBuildError);
                }
                DecoratePresentation();
                isConfigured = true;
                diagnostic = string.Empty;
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                FailAndReturn(
                    string.IsNullOrWhiteSpace(exception.Message)
                        ? "playable-level-composition-rejected"
                        : exception.Message);
            }
        }

        private void ValidateSceneReferences()
        {
            if (roomBootstrap == null) throw Failure("playable-level-json-bootstrap-missing");
            if (roomRuntime == null) throw Failure("playable-level-room-composition-missing");
            if (visualPresentation == null) throw Failure("playable-level-visual-presentation-missing");
            if (enemySpawner == null) throw Failure("playable-level-enemy-binding-missing");
            if (presentationCatalog == null) throw Failure("playable-level-presentation-catalog-missing");
            if (presentationRoot == null) throw Failure("playable-level-presentation-root-missing");
            if (playerPrefab == null) throw Failure("playable-level-player-prefab-missing");
            if (playerSpeed <= 0f) throw Failure("playable-level-player-speed-invalid");
            if (cameraSize <= 0f) throw Failure("playable-level-camera-size-invalid");
        }

        private string BuildImportDiagnostic()
        {
            if (roomBootstrap.LastImportIssues != null
                && roomBootstrap.LastImportIssues.Count > 0
                && roomBootstrap.LastImportIssues[0] != null)
            {
                var issue = roomBootstrap.LastImportIssues[0];
                return "playable-level-json-import-rejected:["
                    + issue.Code + "] " + issue.Path + " " + issue.Message;
            }
            return "playable-level-json-import-rejected";
        }

        private void ValidateImportedLevel()
        {
            if (roomBootstrap.ImportedBundle == null
                || roomRuntime.Definition == null
                || roomRuntime.CurrentProjection == null)
            {
                throw Failure("playable-level-room-composition-rejected");
            }

            AuthorableRoomGraphDefinitionV1 definition = roomRuntime.Definition;
            AuthorableRoomDefinitionV1 startRoom = definition.GetRoom(
                definition.StartRoomStableId);
            int playerSpawns = 0;
            for (int index = 0; index < startRoom.SpawnPoints.Count; index++)
            {
                if (startRoom.SpawnPoints[index].Kind == RoomSpawnPointKindV1.Player)
                {
                    playerSpawns++;
                }
            }
            if (playerSpawns == 0)
            {
                throw Failure("playable-level-player-spawn-missing");
            }
            if (playerSpawns != 1)
            {
                throw Failure("playable-level-player-spawn-duplicated");
            }

            int finalExits = 0;
            for (int roomIndex = 0; roomIndex < definition.Rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinitionV1 room = definition.Rooms[roomIndex];
                for (int exitIndex = 0; exitIndex < room.Exits.Count; exitIndex++)
                {
                    if (room.Exits[exitIndex].LinkKind == RoomLiveLinkKindV1.FinalExit)
                    {
                        finalExits++;
                    }
                }
            }
            if (finalExits == 0) throw Failure("playable-level-authored-exit-missing");
            if (finalExits != 1) throw Failure("playable-level-authored-exit-duplicated");
        }

        private void SpawnExactlyOnePlayer()
        {
            if (CountPlayersInScene() != 0 || playerMarker != null)
            {
                throw Failure("playable-level-duplicate-player-creation");
            }

            GameObject player = Instantiate(playerPrefab, transform);
            if (player == null)
            {
                throw Failure("playable-level-player-presentation-missing");
            }
            player.name = "Production Player "
                + characterGraph.Character.CharacterInstanceStableId;
            player.SetActive(true);
            playerMarker = player.GetComponent<PlayablePlayerMarker2D>()
                ?? player.AddComponent<PlayablePlayerMarker2D>();
            playerMarker.Bind(
                characterGraph.Character.CharacterInstanceStableId,
                characterGraph.Character.ClassDefinitionStableId,
                characterGraph.RoutePayload,
                exactHoldingsAuthority,
                exactLoadoutAuthority);

            playerBody = player.GetComponent<Rigidbody2D>();
            if (playerBody == null)
            {
                throw Failure("playable-level-player-rigidbody-missing");
            }
            if (player.GetComponent<Collider2D>() == null)
            {
                throw Failure("playable-level-player-collider-missing");
            }
            playerBody.gravityScale = 0f;
            playerBody.freezeRotation = true;
            playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            PlayableTopDownMovement2D movement =
                player.GetComponent<PlayableTopDownMovement2D>()
                ?? player.AddComponent<PlayableTopDownMovement2D>();
            movement.Bind(playerBody, playerSpeed);
        }

        private int CountPlayersInScene()
        {
            int count = 0;
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                count += roots[index]
                    .GetComponentsInChildren<PlayablePlayerMarker2D>(true).Length;
            }
            return count;
        }

        private void CreateExactlyOneGameplayCamera()
        {
            Camera[] cameras = FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                if (cameras[index] != null
                    && cameras[index].gameObject.scene == gameObject.scene
                    && cameras[index].enabled)
                {
                    throw Failure("playable-level-gameplay-camera-duplicated");
                }
            }

            GameObject cameraObject = new GameObject("Playable Level Camera");
            cameraObject.transform.SetParent(transform, false);
            Camera gameplayCamera = cameraObject.AddComponent<Camera>();
            gameplayCamera.orthographic = true;
            gameplayCamera.orthographicSize = cameraSize;
            gameplayCamera.clearFlags = CameraClearFlags.SolidColor;
            gameplayCamera.backgroundColor = new Color(0.025f, 0.035f, 0.05f, 1f);
            gameplayCamera.transform.position = new Vector3(0f, 0f, -10f);
            PlayableCameraFollow2D follow =
                cameraObject.AddComponent<PlayableCameraFollow2D>();
            follow.Bind(playerMarker.transform, -10f);
        }

        private void HandleRoomPresentationRebuilt()
        {
            if (playerMarker == null) return;
            try
            {
                SynchronizeCurrentRoom();
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                FailAndReturn(
                    "playable-level-room-rebuild-rejected:" + exception.Message);
            }
        }

        private void SynchronizeCurrentRoom()
        {
            if (roomRuntime == null
                || !roomRuntime.IsBuilt
                || roomRuntime.CurrentRoomStableId == null)
            {
                throw Failure("playable-level-current-room-missing");
            }

            RoomSpawnPointDefinitionV1 spawn = ResolveCurrentSpawn();
            playerBody.position = new Vector2(
                (float)spawn.LocalPosition.X,
                (float)spawn.LocalPosition.Y);
            playerBody.rotation = (float)spawn.LocalRotationDegrees;
            playerBody.linearVelocity = Vector2.zero;
            RebuildOwnedBindings();
            DecoratePresentation();
        }

        private RoomSpawnPointDefinitionV1 ResolveCurrentSpawn()
        {
            AuthorableRoomDefinitionV1 room = roomRuntime.Definition.GetRoom(
                roomRuntime.CurrentRoomStableId);
            RoomSpawnPointDefinitionV1 spawn;
            if (!room.TryGetSpawnPoint(
                roomRuntime.CurrentSpawnPointStableId,
                out spawn)
                || spawn == null)
            {
                throw Failure("playable-level-current-player-spawn-missing");
            }
            return spawn;
        }

        private void RebuildOwnedBindings()
        {
            for (int index = ownedBindings.Count - 1; index >= 0; index--)
            {
                if (ownedBindings[index] != null) Destroy(ownedBindings[index]);
            }
            ownedBindings.Clear();

            AuthorableRoomDefinitionV1 room = roomRuntime.Definition.GetRoom(
                roomRuntime.CurrentRoomStableId);
            BuildAuthoredBoundary(room.Bounds);
            for (int index = 0; index < room.Doors.Count; index++)
            {
                RoomDoorInstance2D door;
                if (!roomRuntime.TryGetSpawnedDoor(
                    room.Doors[index].DoorInstanceStableId,
                    out door)
                    || door == null)
                {
                    throw Failure(
                        "playable-level-authored-door-presentation-missing:"
                        + room.Doors[index].DoorInstanceStableId);
                }

                BoxCollider2D trigger = door.gameObject.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(2.2f, 3.4f);
                PlayableDoorTrigger2D traversal =
                    door.gameObject.AddComponent<PlayableDoorTrigger2D>();
                traversal.Bind(door, playerMarker, NextOperationId);
            }
        }

        private void BuildAuthoredBoundary(RoomBoundsV1 bounds)
        {
            float centerX = (float)bounds.Center.X;
            float centerY = (float)bounds.Center.Y;
            float width = (float)bounds.Size.X;
            float height = (float)bounds.Size.Y;
            const float thickness = 0.5f;
            AddBoundary("North", new Vector2(centerX, centerY + height * 0.5f),
                new Vector2(width + thickness, thickness));
            AddBoundary("South", new Vector2(centerX, centerY - height * 0.5f),
                new Vector2(width + thickness, thickness));
            AddBoundary("East", new Vector2(centerX + width * 0.5f, centerY),
                new Vector2(thickness, height + thickness));
            AddBoundary("West", new Vector2(centerX - width * 0.5f, centerY),
                new Vector2(thickness, height + thickness));
        }

        private void AddBoundary(string name, Vector2 position, Vector2 size)
        {
            GameObject boundary = new GameObject("Authored Boundary " + name);
            boundary.transform.SetParent(transform, false);
            boundary.transform.position = position;
            BoxCollider2D collider = boundary.AddComponent<BoxCollider2D>();
            collider.size = size;
            SpriteRenderer renderer = boundary.AddComponent<SpriteRenderer>();
            renderer.sprite = runtimeSprite;
            renderer.color = new Color(0.22f, 0.28f, 0.36f, 1f);
            renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            ownedBindings.Add(boundary);
        }

        private StableId NextOperationId()
        {
            operationSequence = checked(operationSequence + 1L);
            return StableId.Create(
                "operation",
                "playable-level-door-"
                + operationSequence.ToString(CultureInfo.InvariantCulture));
        }

        private void HandleFinalExitReached()
        {
            if (completionAccepted)
            {
                Debug.LogError("playable-level-duplicate-completion-request", this);
                return;
            }
            completionAccepted = true;

            if (!ReferenceEquals(
                    exactHoldingsAuthority,
                    characterGraph.LoadoutRuntime.Holdings)
                || !ReferenceEquals(
                    exactLoadoutAuthority,
                    characterGraph.LoadoutRuntime.LoadoutAuthority)
                || !string.Equals(
                    routeFingerprint,
                    characterGraph.RoutePayload.Fingerprint,
                    StringComparison.Ordinal))
            {
                FailAndReturn("playable-level-character-authority-changed");
                return;
            }

            ProductionFlowCoordinatorV1 flow = FindFirstObjectByType<
                ProductionFlowCoordinatorV1>(FindObjectsInactive.Include);
            if (flow == null
                || flow.Transitions == null
                || !flow.Transitions.TryReturnToHub(characterGraph.RoutePayload))
            {
                Debug.LogError("playable-level-hub-return-rejected", this);
            }
        }

        private void CreateRuntimeSprite()
        {
            runtimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            runtimeTexture.name = "Playable Level Runtime Pixel";
            runtimeTexture.SetPixel(0, 0, Color.white);
            runtimeTexture.Apply(false, true);
            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            runtimeSprite.name = "Playable Level Runtime Sprite";
        }

        private void DecoratePresentation()
        {
            if (runtimeSprite == null || presentationRoot == null) return;
            SpriteRenderer[] renderers = presentationRoot.GetComponentsInChildren<
                SpriteRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                SpriteRenderer renderer = renderers[index];
                if (renderer == null) continue;
                if (renderer.sprite == null) renderer.sprite = runtimeSprite;
                RoomDoorInstance2D door = renderer.GetComponentInParent<RoomDoorInstance2D>();
                RoomPlacedInstance2D placed =
                    renderer.GetComponentInParent<RoomPlacedInstance2D>();
                if (door != null)
                {
                    renderer.color = door.IsOpen
                        ? new Color(0.2f, 0.85f, 0.45f, 1f)
                        : new Color(0.8f, 0.25f, 0.2f, 1f);
                }
                else if (placed != null
                    && placed.PlacementKind == RoomLivePlacementKindV1.Enemy)
                {
                    renderer.color = new Color(0.85f, 0.3f, 0.25f, 1f);
                }
                else if (placed != null)
                {
                    renderer.color = new Color(0.45f, 0.52f, 0.62f, 1f);
                }
                else
                {
                    renderer.color = new Color(0.12f, 0.15f, 0.2f, 1f);
                }
            }

            if (playerMarker != null)
            {
                SpriteRenderer playerRenderer =
                    playerMarker.GetComponentInChildren<SpriteRenderer>(true);
                if (playerRenderer != null)
                {
                    if (playerRenderer.sprite == null) playerRenderer.sprite = runtimeSprite;
                    playerRenderer.color = new Color(0.25f, 0.65f, 1f, 1f);
                }
            }
        }

        private void FailAndReturn(string code)
        {
            if (failureReturnRequested) return;
            failureReturnRequested = true;
            diagnostic = string.IsNullOrWhiteSpace(code)
                ? "playable-level-failure"
                : code;
            Debug.LogError(diagnostic, this);
            ReturnCurrentCharacterToHub();
        }

        private static void ReturnCurrentCharacterToHub()
        {
            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            ProductionFlowCoordinatorV1 flow = FindFirstObjectByType<
                ProductionFlowCoordinatorV1>(FindObjectsInactive.Include);
            if (flow != null
                && flow.Transitions != null
                && ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out profile)
                && graph != null
                && !graph.IsDisposed)
            {
                flow.Transitions.TryReturnToHub(graph.RoutePayload);
            }
        }

        private static InvalidOperationException Failure(string code)
        {
            return new InvalidOperationException(code);
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private void OnDestroy()
        {
            if (roomRuntime != null)
            {
                roomRuntime.CurrentRoomPresentationRebuilt -=
                    HandleRoomPresentationRebuilt;
                roomRuntime.FinalExitReached -= HandleFinalExitReached;
            }
            if (runtimeSprite != null) Destroy(runtimeSprite);
            if (runtimeTexture != null) Destroy(runtimeTexture);
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayablePlayerMarker2D : MonoBehaviour
    {
        public StableId CharacterInstanceStableId { get; private set; }
        public StableId ClassDefinitionStableId { get; private set; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; private set; }
        public object HoldingsAuthority { get; private set; }
        public object LoadoutAuthority { get; private set; }

        public void Bind(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            object holdingsAuthority,
            object loadoutAuthority)
        {
            if (CharacterInstanceStableId != null)
            {
                throw new InvalidOperationException(
                    "playable-level-player-context-duplicate-binding");
            }
            CharacterInstanceStableId = characterInstanceStableId
                ?? throw new ArgumentNullException(nameof(characterInstanceStableId));
            ClassDefinitionStableId = classDefinitionStableId
                ?? throw new ArgumentNullException(nameof(classDefinitionStableId));
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            HoldingsAuthority = holdingsAuthority
                ?? throw new ArgumentNullException(nameof(holdingsAuthority));
            LoadoutAuthority = loadoutAuthority
                ?? throw new ArgumentNullException(nameof(loadoutAuthority));
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayableTopDownMovement2D : MonoBehaviour
    {
        private Rigidbody2D body;
        private float speed;
        private Vector2 input;

        public void Bind(Rigidbody2D configuredBody, float configuredSpeed)
        {
            body = configuredBody ?? throw new ArgumentNullException(nameof(configuredBody));
            if (configuredSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredSpeed));
            }
            speed = configuredSpeed;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                input = Vector2.zero;
                return;
            }
            float horizontal = 0f;
            float vertical = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
            input = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        private void FixedUpdate()
        {
            if (body != null) body.linearVelocity = input * speed;
        }

        private void OnDisable()
        {
            input = Vector2.zero;
            if (body != null) body.linearVelocity = Vector2.zero;
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayableCameraFollow2D : MonoBehaviour
    {
        private Transform target;
        private float depth;

        public void Bind(Transform configuredTarget, float configuredDepth)
        {
            target = configuredTarget
                ?? throw new ArgumentNullException(nameof(configuredTarget));
            depth = configuredDepth;
            Snap();
        }

        private void LateUpdate()
        {
            Snap();
        }

        private void Snap()
        {
            if (target == null) return;
            transform.position = new Vector3(
                target.position.x,
                target.position.y,
                depth);
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayableDoorTrigger2D : MonoBehaviour
    {
        private RoomDoorInstance2D door;
        private PlayablePlayerMarker2D player;
        private Func<StableId> operationFactory;
        private bool accepted;

        public void Bind(
            RoomDoorInstance2D configuredDoor,
            PlayablePlayerMarker2D configuredPlayer,
            Func<StableId> configuredOperationFactory)
        {
            door = configuredDoor ?? throw new ArgumentNullException(nameof(configuredDoor));
            player = configuredPlayer ?? throw new ArgumentNullException(nameof(configuredPlayer));
            operationFactory = configuredOperationFactory
                ?? throw new ArgumentNullException(nameof(configuredOperationFactory));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (accepted || door == null || !door.IsOpen || other == null) return;
            PlayablePlayerMarker2D entered =
                other.GetComponentInParent<PlayablePlayerMarker2D>();
            if (entered == null || !ReferenceEquals(entered, player)) return;

            accepted = true;
            RoomLiveOperationResultV1 result = door.TryTraverse(operationFactory());
            if (result == null || result.Status == RoomLiveOperationStatusV1.Rejected)
            {
                accepted = false;
                Debug.LogError(
                    "playable-level-door-traversal-rejected:"
                    + (result == null ? "result-missing" : result.RejectionCode),
                    this);
            }
        }
    }
}

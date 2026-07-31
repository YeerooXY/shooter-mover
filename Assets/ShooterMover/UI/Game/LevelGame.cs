using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Flow.Game;
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

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Generic production traversal composition for catalogue-selected authored JSON levels.
    /// The selected character graph remains the sole owner of profile, holdings and loadout.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class LevelGame : MonoBehaviour
    {
        [SerializeField] private RoomLoader roomBootstrap;
        [SerializeField] private LevelRooms roomRuntime;
        [SerializeField] private RoomView visualPresentation;
        [SerializeField] private RoomEnemies enemySpawner;
        [SerializeField] private RoomArt presentationCatalog;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private float playerSpeed = 6f;
        [SerializeField] private float cameraSize = 8f;

        private readonly List<GameObject> ownedBindings = new List<GameObject>();
        private PlayableLevelDefinition levelDefinition;
        private CharacterLiveGraph characterGraph;
        private object exactHoldingsAuthority;
        private object exactLoadoutAuthority;
        private string routeFingerprint;
        private PlayerMarker playerMarker;
        private Rigidbody2D playerBody;
        private Func<bool> runCompletion;
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

        public void ConfigureRunCompletion(Func<bool> completion)
        {
            if (completion == null)
                throw new ArgumentNullException(nameof(completion));
            if (runCompletion != null && !ReferenceEquals(runCompletion, completion))
            {
                throw new InvalidOperationException(
                    "playable-level-run-completion-already-configured");
            }
            runCompletion = completion;
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
                    PlayableLevelCatalog.PlayableLevelScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            LevelGame controller = FindInScene(scene);
            if (controller == null)
            {
                Debug.LogError("playable-level-controller-missing");
                ReturnCurrentCharacterToHub();
                return;
            }
            controller.BeginFromProductionContext();
        }

        private static LevelGame FindInScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                LevelGame value = roots[index]
                    .GetComponentInChildren<LevelGame>(true);
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

            PlayerRouteProfilePayload routePayload;
            StableId selectedModeStableId;
            StableId selectedLevelStableId;
            if (!LevelSelectionRouteContext.TryRead(
                    out routePayload,
                    out selectedModeStableId,
                    out selectedLevelStableId)
                || selectedLevelStableId == null)
            {
                FailAndReturn("playable-level-selection-context-missing");
                return;
            }

            PlayableLevelDefinition selectedLevel;
            if (!PlayableLevelCatalog.TryResolve(
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

            CharacterLiveGraph graph;
            FlowProfileRecord profile;
            if (!CharacterSave.TryResolveCurrent(
                    out graph,
                    out profile)
                || graph == null
                || profile == null
                || graph.IsDisposed)
            {
                FailAndReturn(
                    "playable-level-character-context-missing:"
                        + CharacterSave
                            .CurrentDiagnostic);
                return;
            }
            if (!HasSameCharacterRouteIdentity(routePayload, graph, profile)
                || graph.RoutePayload == null
                || !graph.RoutePayload.HasValidFingerprint()
                || profile.Payload == null
                || !profile.Payload.HasValidFingerprint()
                || !graph.RoutePayload.Equals(profile.Payload))
            {
                FailAndReturn("playable-level-character-route-mismatch");
                return;
            }

            // Navigation payloads are immutable snapshots. Inventory/equipment changes
            // can legitimately occur after the snapshot used to enter Play was created.
            // The selected character and class still have to match exactly, but a stale
            // gun-slot fingerprint must not reject the run. Gameplay binds to the
            // current character graph below, which is the authoritative payload.
            Begin(selectedLevel, graph);
        }

        private static bool HasSameCharacterRouteIdentity(
            PlayerRouteProfilePayload routePayload,
            CharacterLiveGraph graph,
            FlowProfileRecord profile)
        {
            return routePayload != null
                && routePayload.HasValidFingerprint()
                && graph != null
                && graph.Character != null
                && profile != null
                && profile.Payload != null
                && routePayload.SelectedCharacterStableId
                    == graph.Character.CharacterInstanceStableId
                && routePayload.SelectedCharacterStableId
                    == profile.Payload.SelectedCharacterStableId
                && routePayload.LoadoutProfileStableId
                    == graph.Character.ClassDefinitionStableId
                && routePayload.LoadoutProfileStableId
                    == profile.Payload.LoadoutProfileStableId;
        }

        private void Begin(
            PlayableLevelDefinition selectedLevel,
            CharacterLiveGraph graph)
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

                RoomFile roomContent =
                    Resources.Load<RoomFile>(
                        selectedLevel.RoomContentResourcePath);
                if (roomContent == null)
                {
                    throw Failure(
                        "playable-level-json-asset-missing:"
                        + selectedLevel.RoomContentResourcePath);
                }
                EnemyCatalogAsset enemyCatalog =
                    Resources.Load<EnemyCatalogAsset>(
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
            ValidateAuthoredPlayerPresentation();
            if (playerSpeed <= 0f) throw Failure("playable-level-player-speed-invalid");
            if (cameraSize <= 0f) throw Failure("playable-level-camera-size-invalid");
        }

        private void ValidateAuthoredPlayerPresentation()
        {
            SpriteRenderer[] renderers =
                playerPrefab.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                throw Failure("playable-level-player-renderer-missing");
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                SpriteRenderer renderer = renderers[index];
                if (renderer != null && renderer.sprite != null) return;
            }

            throw Failure("playable-level-player-sprite-missing");
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

            AuthorableRoomGraphDefinition definition = roomRuntime.Definition;
            AuthorableRoomDefinition startRoom = definition.GetRoom(
                definition.StartRoomStableId);
            int playerSpawns = 0;
            for (int index = 0; index < startRoom.SpawnPoints.Count; index++)
            {
                if (startRoom.SpawnPoints[index].Kind == RoomSpawnPointKind.Player)
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
                AuthorableRoomDefinition room = definition.Rooms[roomIndex];
                for (int exitIndex = 0; exitIndex < room.Exits.Count; exitIndex++)
                {
                    if (room.Exits[exitIndex].LinkKind == RoomLiveLinkKind.FinalExit)
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
            playerMarker = player.GetComponent<PlayerMarker>()
                ?? player.AddComponent<PlayerMarker>();
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
            TopDownMovement movement =
                player.GetComponent<TopDownMovement>()
                ?? player.AddComponent<TopDownMovement>();
            movement.Bind(playerBody, playerSpeed);
        }

        private int CountPlayersInScene()
        {
            int count = 0;
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                count += roots[index]
                    .GetComponentsInChildren<PlayerMarker>(true).Length;
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
            CameraFollow follow =
                cameraObject.AddComponent<CameraFollow>();
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

            RoomSpawnPointDefinition spawn = ResolveCurrentSpawn();
            playerBody.position = new Vector2(
                (float)spawn.LocalPosition.X,
                (float)spawn.LocalPosition.Y);
            playerBody.rotation = (float)spawn.LocalRotationDegrees;
            playerBody.linearVelocity = Vector2.zero;
            RebuildOwnedBindings();
        }

        private RoomSpawnPointDefinition ResolveCurrentSpawn()
        {
            AuthorableRoomDefinition room = roomRuntime.Definition.GetRoom(
                roomRuntime.CurrentRoomStableId);
            RoomSpawnPointDefinition spawn;
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

            AuthorableRoomDefinition room = roomRuntime.Definition.GetRoom(
                roomRuntime.CurrentRoomStableId);
            BuildAuthoredBoundary(room.Bounds);
            for (int index = 0; index < room.Doors.Count; index++)
            {
                RoomDoor door;
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
                DoorTrigger traversal =
                    door.gameObject.AddComponent<DoorTrigger>();
                traversal.Bind(door, playerMarker, NextOperationId);
            }
        }

        private void BuildAuthoredBoundary(RoomBounds bounds)
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

            if (runCompletion != null)
            {
                if (!runCompletion())
                {
                    completionAccepted = false;
                    Debug.LogError(
                        "playable-level-run-completion-rejected",
                        this);
                }
                return;
            }

            GameFlow flow = FindFirstObjectByType<
                GameFlow>(FindObjectsInactive.Include);
            if (flow == null
                || flow.Transitions == null
                || !flow.Transitions.TryReturnToHub(characterGraph.RoutePayload))
            {
                completionAccepted = false;
                Debug.LogError("playable-level-hub-return-rejected", this);
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
            CharacterLiveGraph graph;
            FlowProfileRecord profile;
            GameFlow flow = FindFirstObjectByType<
                GameFlow>(FindObjectsInactive.Include);
            if (flow != null
                && flow.Transitions != null
                && CharacterSave.TryResolveCurrent(
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
            runCompletion = null;
            if (roomRuntime != null)
            {
                roomRuntime.CurrentRoomPresentationRebuilt -=
                    HandleRoomPresentationRebuilt;
                roomRuntime.FinalExitReached -= HandleFinalExitReached;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerMarker : MonoBehaviour
    {
        public StableId CharacterInstanceStableId { get; private set; }
        public StableId ClassDefinitionStableId { get; private set; }
        public PlayerRouteProfilePayload RoutePayload { get; private set; }
        public object HoldingsAuthority { get; private set; }
        public object LoadoutAuthority { get; private set; }

        public void Bind(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            PlayerRouteProfilePayload routePayload,
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
    public sealed class TopDownMovement : MonoBehaviour
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
    public sealed class CameraFollow : MonoBehaviour
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
    public sealed class DoorTrigger : MonoBehaviour
    {
        private RoomDoor door;
        private PlayerMarker player;
        private Func<StableId> operationFactory;
        private bool accepted;

        public void Bind(
            RoomDoor configuredDoor,
            PlayerMarker configuredPlayer,
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
            PlayerMarker entered =
                other.GetComponentInParent<PlayerMarker>();
            if (entered == null || !ReferenceEquals(entered, player)) return;

            accepted = true;
            RoomLiveOperationResult result = door.TryTraverse(operationFactory());
            if (result == null || result.Status == RoomLiveOperationStatus.Rejected)
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

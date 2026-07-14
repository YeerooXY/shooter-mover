using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Input;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.TestSupport.Movement
{
    public enum MovementPlaygroundHarnessRole
    {
        CompositionRoot = 0,
        WallContract = 1,
    }

    /// <summary>
    /// Test-only composition root for MT-013. The harness consumes the accepted
    /// MT-007 input asset and MT-010 lifecycle without adding another movement driver.
    /// The same component is attached to room walls in WallContract mode so contact
    /// classification remains explicit and does not depend on names, tags, or layers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementPlaygroundHarness :
        MonoBehaviour,
        IMovementContact2DContract
    {
        public const string SceneName = "MovementPlayground";
        public const string ScenePath =
            "Assets/ShooterMover/Tests/PlayMode/Movement/Scenes/MovementPlayground.unity";

        [SerializeField]
        private MovementPlaygroundHarnessRole role;

        [SerializeField]
        private Transform playerTransform;

        [SerializeField]
        private Rigidbody2D playerBody;

        [SerializeField]
        private PlayerMovementIntentAdapter inputAdapter;

        [SerializeField]
        private MovementContact2DAdapter contactAdapter;

        [SerializeField]
        private MovementActorLifecycle movementLifecycle;

        [SerializeField]
        private InputActionAsset movementInputActions;

        [SerializeField]
        private Camera followCamera;

        [SerializeField]
        private GameObject playerVisual;

        [SerializeField]
        private GameObject thrusterVisual;

        [SerializeField]
        private Collider2D[] roomWalls;

        [SerializeField]
        private GameObject[] wallVisuals;

        [SerializeField]
        private Vector2 roomInteriorSize = new Vector2(48f, 27f);

        private MovementThrusterTuningProfile tuning;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float cameraDepth;
        private Texture2D placeholderTexture;
        private Sprite placeholderSprite;
        private SpriteRenderer playerRenderer;
        private SpriteRenderer thrusterRenderer;
        private SpriteRenderer[] wallRenderers;
        private bool initialized;
        private bool activationPending;

        public bool IsCompositionRoot
        {
            get { return role == MovementPlaygroundHarnessRole.CompositionRoot; }
        }

        public bool IsReady
        {
            get
            {
                return IsCompositionRoot
                    && initialized
                    && movementLifecycle != null
                    && movementLifecycle.IsConstructed
                    && movementLifecycle.IsRunning;
            }
        }

        public Rigidbody2D PlayerBody
        {
            get { return playerBody; }
        }

        public MovementActorLifecycle MovementLifecycle
        {
            get { return movementLifecycle; }
        }

        public Camera FollowCamera
        {
            get { return followCamera; }
        }

        public Vector2 RoomInteriorSize
        {
            get { return roomInteriorSize; }
        }

        public int RoomWallCount
        {
            get { return roomWalls == null ? 0 : roomWalls.Length; }
        }

        public int PlaceholderSpriteInstanceId
        {
            get { return placeholderSprite == null ? 0 : placeholderSprite.GetInstanceID(); }
        }

        public int FinalVelocityWriterCount
        {
            get
            {
                return playerBody == null
                    ? 0
                    : playerBody.GetComponents<MovementActorLifecycle>().Length;
            }
        }

        public int SecondaryDriverCount
        {
            get
            {
                return playerBody == null
                    ? 0
                    : playerBody.GetComponents<MovementFixedStepDriver>().Length;
            }
        }

        public bool ThrusterVisualActive
        {
            get { return thrusterRenderer != null && thrusterRenderer.enabled; }
        }

        private void Awake()
        {
            if (!IsCompositionRoot)
            {
                return;
            }

            ValidateCompositionReferences();

            spawnPosition = playerTransform.position;
            spawnRotation = playerTransform.rotation;
            cameraDepth = followCamera.transform.position.z;
            tuning = BuildPlaygroundTuning();

            bool newlyConstructed = movementLifecycle.Construct(
                playerBody,
                inputAdapter,
                movementInputActions,
                contactAdapter,
                tuning);
            if (!newlyConstructed && !movementLifecycle.IsConstructed)
            {
                throw new InvalidOperationException(
                    "Movement playground failed to construct the MT-010 lifecycle.");
            }

            EnsurePlaceholderPresentation();
            initialized = true;
        }

        private void Start()
        {
            if (!IsCompositionRoot || !initialized || !activationPending)
            {
                return;
            }

            activationPending = false;
            if (!isActiveAndEnabled
                || movementLifecycle == null
                || !movementLifecycle.isActiveAndEnabled
                || movementLifecycle.IsRunning)
            {
                return;
            }

            movementLifecycle.StartActor();
            RefreshCameraForTests();
            RefreshPresentationForTests();
        }

        private void OnEnable()
        {
            if (!IsCompositionRoot || !initialized)
            {
                return;
            }

            ResetPlayerPose();
            if (!movementLifecycle.IsRunning)
            {
                if (movementLifecycle.isActiveAndEnabled)
                {
                    movementLifecycle.StartActor();
                }
                else
                {
                    activationPending = true;
                }
            }

            RefreshCameraForTests();
            RefreshPresentationForTests();
        }

        private void OnDisable()
        {
            activationPending = false;
            if (!IsCompositionRoot
                || movementLifecycle == null
                || !movementLifecycle.IsConstructed
                || movementLifecycle.IsDisposed)
            {
                return;
            }

            movementLifecycle.StopActor();
            RefreshPresentationForTests();
        }

        private void LateUpdate()
        {
            if (!IsReady)
            {
                return;
            }

            RefreshCameraForTests();
            RefreshPresentationForTests();
        }

        private void OnDestroy()
        {
            if (!IsCompositionRoot)
            {
                return;
            }

            DestroyPlaceholderPresentation();
        }

        public bool TryDescribeMovementContact(out MovementContact2DDescriptor descriptor)
        {
            if (role != MovementPlaygroundHarnessRole.WallContract)
            {
                descriptor = null;
                return false;
            }

            descriptor = MovementContact2DDescriptor.Wall();
            return true;
        }

        /// <summary>
        /// Uses the accepted MT-010 deterministic seam. This method exists only for
        /// focused PlayMode proof; normal scene execution remains owned by the single
        /// MovementActorLifecycle FixedUpdate callback.
        /// </summary>
        public bool StepForTest(double deltaTimeSeconds)
        {
            RequireReady();
            bool stepped = movementLifecycle.ExecuteFixedStep(deltaTimeSeconds);
            RefreshPresentationForTests();
            return stepped;
        }

        public void RestartPlayground()
        {
            RequireReady();
            movementLifecycle.RestartActor();
            ResetPlayerPose();
            RefreshCameraForTests();
            RefreshPresentationForTests();
        }

        public void RefreshCameraForTests()
        {
            RequireCompositionRoot();
            if (!initialized)
            {
                return;
            }

            Vector2 playerPosition = playerBody.position;
            followCamera.transform.position = new Vector3(
                playerPosition.x,
                playerPosition.y,
                cameraDepth);
        }

        public void RefreshPresentationForTests()
        {
            if (!IsCompositionRoot || !initialized || thrusterRenderer == null)
            {
                return;
            }

            MovementActor2D actor = movementLifecycle.Actor;
            thrusterRenderer.enabled = actor != null
                && actor.IsActive
                && actor.CurrentPhase != ThrusterBurstPhase.Ready;
        }

        private void ResetPlayerPose()
        {
            playerTransform.SetPositionAndRotation(spawnPosition, spawnRotation);
            playerBody.position = new Vector2(spawnPosition.x, spawnPosition.y);
            playerBody.rotation = spawnRotation.eulerAngles.z;
            playerBody.angularVelocity = 0f;
            Physics2D.SyncTransforms();
        }

        private void ValidateCompositionReferences()
        {
            if (playerTransform == null)
            {
                throw new InvalidOperationException(
                    "Movement playground requires one explicit player transform.");
            }

            if (playerBody == null
                || inputAdapter == null
                || contactAdapter == null
                || movementLifecycle == null)
            {
                throw new InvalidOperationException(
                    "Movement playground player references are incomplete.");
            }

            if (movementInputActions == null)
            {
                throw new InvalidOperationException(
                    "Movement playground must reference the accepted MT-007 action asset.");
            }

            if (followCamera == null || !followCamera.orthographic)
            {
                throw new InvalidOperationException(
                    "Movement playground requires one explicit orthographic camera.");
            }

            if (playerVisual == null || thrusterVisual == null)
            {
                throw new InvalidOperationException(
                    "Movement playground placeholder visual references are incomplete.");
            }

            if (!ReferenceEquals(playerTransform.gameObject, playerBody.gameObject)
                || !ReferenceEquals(playerBody.gameObject, inputAdapter.gameObject)
                || !ReferenceEquals(playerBody.gameObject, contactAdapter.gameObject)
                || !ReferenceEquals(playerBody.gameObject, movementLifecycle.gameObject))
            {
                throw new InvalidOperationException(
                    "The player body and all accepted movement components must share one GameObject.");
            }

            if (roomWalls == null
                || wallVisuals == null
                || roomWalls.Length != 4
                || wallVisuals.Length != roomWalls.Length)
            {
                throw new InvalidOperationException(
                    "Movement playground requires exactly four explicit room walls.");
            }

            for (int index = 0; index < roomWalls.Length; index++)
            {
                Collider2D wall = roomWalls[index];
                GameObject wallVisual = wallVisuals[index];
                if (wall == null || wallVisual == null || wall.isTrigger)
                {
                    throw new InvalidOperationException(
                        "Every playground wall must be a non-trigger collider with an explicit visual target.");
                }

                MovementPlaygroundHarness wallContract =
                    wall.GetComponent<MovementPlaygroundHarness>();
                if (wallContract == null
                    || wallContract.role != MovementPlaygroundHarnessRole.WallContract)
                {
                    throw new InvalidOperationException(
                        "Every playground wall must expose the explicit movement wall contract.");
                }
            }

            if (playerBody.GetComponents<Rigidbody2D>().Length != 1
                || playerBody.GetComponents<MovementActorLifecycle>().Length != 1
                || playerBody.GetComponents<MovementFixedStepDriver>().Length != 0)
            {
                throw new InvalidOperationException(
                    "Movement playground must contain one body, one lifecycle writer, and no secondary driver.");
            }
        }

        private void EnsurePlaceholderPresentation()
        {
            if (placeholderSprite != null)
            {
                return;
            }

            placeholderTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            placeholderTexture.name = "MT-013 Placeholder Texture";
            placeholderTexture.hideFlags = HideFlags.DontSave;
            placeholderTexture.SetPixel(0, 0, Color.white);
            placeholderTexture.Apply(false, true);

            placeholderSprite = Sprite.Create(
                placeholderTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            placeholderSprite.name = "MT-013 Placeholder Sprite";
            placeholderSprite.hideFlags = HideFlags.DontSave;

            playerRenderer = ConfigureRenderer(
                playerVisual,
                new Color(0.2f, 0.85f, 1f, 1f),
                20);
            thrusterRenderer = ConfigureRenderer(
                thrusterVisual,
                new Color(1f, 0.45f, 0.08f, 1f),
                19);
            thrusterRenderer.enabled = false;

            wallRenderers = new SpriteRenderer[wallVisuals.Length];
            for (int index = 0; index < wallVisuals.Length; index++)
            {
                wallRenderers[index] = ConfigureRenderer(
                    wallVisuals[index],
                    new Color(0.28f, 0.32f, 0.38f, 1f),
                    10);
            }

            followCamera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
        }

        private SpriteRenderer ConfigureRenderer(
            GameObject target,
            Color color,
            int sortingOrder)
        {
            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = target.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = placeholderSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void DestroyPlaceholderPresentation()
        {
            if (placeholderSprite != null)
            {
                DestroyOwnedObject(placeholderSprite);
                placeholderSprite = null;
            }

            if (placeholderTexture != null)
            {
                DestroyOwnedObject(placeholderTexture);
                placeholderTexture = null;
            }
        }

        private static void DestroyOwnedObject(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }

        private void RequireReady()
        {
            RequireCompositionRoot();
            if (!IsReady)
            {
                throw new InvalidOperationException(
                    "Movement playground must be initialized and running before this operation.");
            }
        }

        private void RequireCompositionRoot()
        {
            if (!IsCompositionRoot)
            {
                throw new InvalidOperationException(
                    "Wall-contract instances cannot operate the movement playground.");
            }
        }

        private static MovementThrusterTuningProfile BuildPlaygroundTuning()
        {
            return MovementThrusterTuningProfile.Create(
                MovementThrusterTuningProfile.CurrentProfileVersion,
                StableId.Parse("tuning.mt-013-playground"),
                12d,
                50d,
                60d,
                90d,
                1.25d,
                2,
                1,
                1.75d,
                2.5d,
                0.3d,
                0.1d,
                0.05d,
                120d,
                0.04d,
                0.2d,
                0.75d,
                2d,
                0.8d,
                0.15d,
                5d,
                4,
                0.8d,
                0.9d,
                0.1d,
                0.5d,
                0.02d,
                128);
        }
    }
}

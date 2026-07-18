using System;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Input;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.Production.Stage1
{
    /// <summary>
    /// Production-owned Stage 1 player projection and movement lifecycle.
    ///
    /// The component can either construct the projection exactly once or capture the retained
    /// projection during migration. Both paths converge on the same validated component set,
    /// restart behavior and presentation refresh. It never creates player health, inventory,
    /// combat-hit or mission-result authority.
    /// </summary>
    [DefaultExecutionOrder(10001)]
    [DisallowMultipleComponent]
    public sealed class Stage1PlayerPresentationV1 : MonoBehaviour
    {
        public const string RetainedPlayerObjectName = "PlayerMover";
        public const float PlayerVisualScale = 0.9f;

        private Transform playerTransform;
        private Rigidbody2D playerBody;
        private Collider2D playerCollider;
        private MovementActorLifecycle movementLifecycle;
        private MovementThrusterTuningProfile movementTuning;
        private MovementActorThrusterStatusReader thrusterReader;
        private PlayerCombatIntentAdapter combatInput;
        private EnemyTarget2DAdapter targetAdapter;
        private VoidHazardTarget2D voidTarget;
        private SpriteRenderer bodyRenderer;
        private TrailRenderer boostTrail;
        private InputActionAsset inputActions;
        private Sprite ownedRuntimeSprite;
        private Texture2D ownedRuntimeTexture;
        private Vector3 spawnPosition;
        private bool captureAttempted;
        private bool ownsProjection;

        public bool IsCaptured { get; private set; }
        public bool OwnsProjection => ownsProjection;
        public string RejectionCode { get; private set; }
        public Transform PlayerTransform => playerTransform;
        public Rigidbody2D PlayerBody => playerBody;
        public Collider2D PlayerCollider => playerCollider;
        public MovementActorLifecycle MovementLifecycle => movementLifecycle;
        public MovementThrusterTuningProfile MovementTuning => movementTuning;
        public MovementActorThrusterStatusReader ThrusterReader => thrusterReader;
        public PlayerCombatIntentAdapter CombatInput => combatInput;
        public EnemyTarget2DAdapter TargetAdapter => targetAdapter;
        public VoidHazardTarget2D VoidTarget => voidTarget;
        public SpriteRenderer BodyRenderer => bodyRenderer;
        public TrailRenderer BoostTrail => boostTrail;
        public InputActionAsset InputActions => inputActions;
        public Vector3 SpawnPosition => spawnPosition;

        private void Start()
        {
            if (!IsCaptured)
            {
                TryCaptureRetainedPlayer();
            }
        }

        private void LateUpdate()
        {
            if (IsCaptured && thrusterReader != null)
            {
                RefreshBoostPresentation();
            }
        }

        public void Construct(
            Vector3 spawn,
            Sprite playerSprite,
            Material trailMaterial,
            IVoidHazardCombatPort voidHazardPort)
        {
            if (IsCaptured || captureAttempted)
            {
                throw new InvalidOperationException(
                    "Stage 1 player presentation has already been constructed or captured.");
            }

            if (trailMaterial == null)
            {
                throw new ArgumentNullException(nameof(trailMaterial));
            }

            if (voidHazardPort == null)
            {
                throw new ArgumentNullException(nameof(voidHazardPort));
            }

            captureAttempted = true;
            GameObject player = new GameObject(RetainedPlayerObjectName);
            player.transform.SetParent(transform, false);
            player.transform.position = spawn;

            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D circle = player.AddComponent<CircleCollider2D>();
            circle.radius = 0.55f;

            inputActions = BuildInputActions();
            PlayerMovementIntentAdapter movementInput =
                player.AddComponent<PlayerMovementIntentAdapter>();
            MovementContact2DAdapter contact = player.AddComponent<MovementContact2DAdapter>();
            MovementActorLifecycle lifecycle = player.AddComponent<MovementActorLifecycle>();
            movementTuning = BuildMovementTuning();
            lifecycle.Construct(body, movementInput, inputActions, contact, movementTuning);
            lifecycle.StartActor();

            PlayerCombatIntentAdapter input = player.AddComponent<PlayerCombatIntentAdapter>();
            input.Configure(inputActions);

            EnemyTarget2DAdapter target = player.AddComponent<EnemyTarget2DAdapter>();
            target.Configure(
                StableId.Parse("actor.vs007-player"),
                player.transform,
                circle);

            VoidHazardTarget2D hazardTarget = player.AddComponent<VoidHazardTarget2D>();
            hazardTarget.ConfigureForTests(
                "actor.vs007-player",
                VoidHazardTargetCategory.Player,
                false,
                voidHazardPort,
                null,
                null,
                null,
                null);

            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = playerSprite != null
                ? playerSprite
                : CreateRuntimePlayerSprite();
            renderer.sortingOrder = 10;
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

            TrailRenderer trail = CreateBoostTrail(player.transform, trailMaterial);
            BindProjection(
                player.transform,
                body,
                circle,
                lifecycle,
                input,
                target,
                hazardTarget,
                renderer,
                trail,
                spawn,
                true);
        }

        public bool TryCaptureRetainedPlayer()
        {
            if (IsCaptured)
            {
                return true;
            }

            if (captureAttempted)
            {
                return false;
            }

            captureAttempted = true;
            Transform candidate = transform.Find(RetainedPlayerObjectName);
            if (candidate == null)
            {
                return Reject("stage1-player-projection-missing");
            }

            return TryCaptureInternal(candidate);
        }

        public bool TryCapture(Transform candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (IsCaptured)
            {
                if (ReferenceEquals(playerTransform, candidate))
                {
                    return true;
                }

                throw new InvalidOperationException(
                    "Stage 1 player presentation already owns a different player projection.");
            }

            captureAttempted = true;
            return TryCaptureInternal(candidate);
        }

        public void Restart(Vector3 spawn)
        {
            EnsureCaptured();
            spawnPosition = spawn;
            playerBody.position = spawn;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            movementLifecycle.RestartActor();
            if (boostTrail != null)
            {
                boostTrail.emitting = false;
                boostTrail.Clear();
            }
        }

        public void RefreshBoostPresentation()
        {
            EnsureCaptured();
            if (thrusterReader == null)
            {
                return;
            }

            ThrusterStatusSnapshot status = thrusterReader.ReadSnapshot();
            bool isBoosting = status != null && status.IsBursting;
            playerTransform.localScale = new Vector3(
                PlayerVisualScale,
                PlayerVisualScale,
                1f);
            bodyRenderer.color = isBoosting
                ? new Color(0.72f, 0.96f, 1f, 1f)
                : Color.white;
            if (boostTrail != null)
            {
                boostTrail.emitting = isBoosting;
            }
        }

        private bool TryCaptureInternal(Transform candidate)
        {
            Rigidbody2D body = candidate.GetComponent<Rigidbody2D>();
            Collider2D collider = candidate.GetComponent<Collider2D>();
            MovementActorLifecycle lifecycle = candidate.GetComponent<MovementActorLifecycle>();
            PlayerCombatIntentAdapter input = candidate.GetComponent<PlayerCombatIntentAdapter>();
            EnemyTarget2DAdapter target = candidate.GetComponent<EnemyTarget2DAdapter>();
            VoidHazardTarget2D hazardTarget = candidate.GetComponent<VoidHazardTarget2D>();
            SpriteRenderer renderer = candidate.GetComponent<SpriteRenderer>();
            TrailRenderer trail = candidate.GetComponentInChildren<TrailRenderer>(true);

            if (body == null
                || collider == null
                || lifecycle == null
                || lifecycle.Actor == null
                || input == null
                || target == null
                || hazardTarget == null
                || renderer == null)
            {
                return Reject("stage1-player-projection-incomplete");
            }

            BindProjection(
                candidate,
                body,
                collider,
                lifecycle,
                input,
                target,
                hazardTarget,
                renderer,
                trail,
                candidate.position,
                false);
            return true;
        }

        private void BindProjection(
            Transform candidate,
            Rigidbody2D body,
            Collider2D collider,
            MovementActorLifecycle lifecycle,
            PlayerCombatIntentAdapter input,
            EnemyTarget2DAdapter target,
            VoidHazardTarget2D hazardTarget,
            SpriteRenderer renderer,
            TrailRenderer trail,
            Vector3 spawn,
            bool owns)
        {
            playerTransform = candidate;
            playerBody = body;
            playerCollider = collider;
            movementLifecycle = lifecycle;
            combatInput = input;
            targetAdapter = target;
            voidTarget = hazardTarget;
            bodyRenderer = renderer;
            boostTrail = trail;
            spawnPosition = spawn;
            ownsProjection = owns;
            if (movementTuning != null)
            {
                thrusterReader = new MovementActorThrusterStatusReader(
                    movementLifecycle.Actor,
                    movementTuning);
            }
            IsCaptured = true;
            RejectionCode = string.Empty;
        }

        private Sprite CreateRuntimePlayerSprite()
        {
            ownedRuntimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            ownedRuntimeTexture.name = "Stage 1 Player Texture";
            ownedRuntimeTexture.SetPixel(0, 0, new Color(0.14f, 0.82f, 1f, 1f));
            ownedRuntimeTexture.Apply(false, true);
            ownedRuntimeSprite = Sprite.Create(
                ownedRuntimeTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            ownedRuntimeSprite.name = "Stage 1 Player";
            return ownedRuntimeSprite;
        }

        private static void CreateGunMount(Transform parent, Vector2 localPosition)
        {
            GameObject mount = new GameObject("ParallelBlasterMount");
            mount.transform.SetParent(parent, false);
            mount.transform.localPosition = localPosition;
            SpriteRenderer renderer = mount.AddComponent<SpriteRenderer>();
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, new Color(0.82f, 0.9f, 0.96f, 1f));
            texture.Apply(false, true);
            renderer.sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            renderer.sortingOrder = 11;
            mount.transform.localScale = new Vector3(0.22f, 0.48f, 1f);
        }

        private static TrailRenderer CreateBoostTrail(Transform parent, Material material)
        {
            GameObject trailObject = new GameObject("BoostTrail");
            trailObject.transform.SetParent(parent, false);
            trailObject.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.material = material;
            trail.time = 0.24f;
            trail.minVertexDistance = 0.04f;
            trail.widthMultiplier = 0.5f;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 0.08f));
            trail.colorGradient = new Gradient
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
            trail.sortingOrder = 9;
            trail.emitting = false;
            return trail;
        }

        private static InputActionAsset BuildInputActions()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "Stage 1 Player Input";
            InputActionMap movement = asset.AddActionMap("Movement");
            InputAction move = movement.AddAction(
                "Move",
                InputActionType.Value,
                expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            movement.AddAction(
                "Aim",
                InputActionType.Value,
                "<Gamepad>/rightStick",
                expectedControlLayout: "Vector2");
            InputAction thruster = movement.AddAction("Thruster", InputActionType.Button);
            thruster.AddBinding("<Keyboard>/leftShift");
            thruster.AddBinding("<Keyboard>/rightShift");
            thruster.AddBinding("<Keyboard>/space");
            thruster.AddBinding("<Gamepad>/rightShoulder");
            thruster.AddBinding("<Gamepad>/buttonSouth");

            InputActionMap combat = asset.AddActionMap("Combat");
            InputAction aim = combat.AddAction(
                "Aim",
                InputActionType.Value,
                expectedControlLayout: "Vector2");
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

        private bool Reject(string code)
        {
            RejectionCode = code ?? string.Empty;
            IsCaptured = false;
            return false;
        }

        private void EnsureCaptured()
        {
            if (!IsCaptured)
            {
                throw new InvalidOperationException(
                    "Stage 1 player presentation has not captured a complete player projection.");
            }
        }

        private void OnDestroy()
        {
            if (inputActions != null)
            {
                DestroyImmediate(inputActions);
            }

            if (ownedRuntimeSprite != null)
            {
                DestroyImmediate(ownedRuntimeSprite);
            }

            if (ownedRuntimeTexture != null)
            {
                DestroyImmediate(ownedRuntimeTexture);
            }
        }
    }
}

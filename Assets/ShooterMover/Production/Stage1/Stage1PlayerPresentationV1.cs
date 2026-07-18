using System;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Input;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;

namespace ShooterMover.Production.Stage1
{
    /// <summary>
    /// Production-owned boundary around the retained Stage 1 player projection.
    ///
    /// During the staged extraction the legacy presenter still creates the player object.
    /// This component captures that exact object and exposes one validated player subsystem
    /// for movement, combat targeting, boost presentation, restart and later delegation.
    /// It never creates a second movement actor or player authority.
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
        private MovementActorThrusterStatusReader thrusterReader;
        private PlayerCombatIntentAdapter combatInput;
        private EnemyTarget2DAdapter targetAdapter;
        private SpriteRenderer bodyRenderer;
        private TrailRenderer boostTrail;
        private Vector3 spawnPosition;
        private bool captureAttempted;

        public bool IsCaptured { get; private set; }
        public string RejectionCode { get; private set; }
        public Transform PlayerTransform => playerTransform;
        public Rigidbody2D PlayerBody => playerBody;
        public Collider2D PlayerCollider => playerCollider;
        public MovementActorLifecycle MovementLifecycle => movementLifecycle;
        public MovementActorThrusterStatusReader ThrusterReader => thrusterReader;
        public PlayerCombatIntentAdapter CombatInput => combatInput;
        public EnemyTarget2DAdapter TargetAdapter => targetAdapter;
        public SpriteRenderer BodyRenderer => bodyRenderer;
        public TrailRenderer BoostTrail => boostTrail;
        public Vector3 SpawnPosition => spawnPosition;

        private void Start()
        {
            TryCaptureRetainedPlayer();
        }

        private void LateUpdate()
        {
            if (!IsCaptured)
            {
                return;
            }

            RefreshBoostPresentation();
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

            return TryCapture(candidate);
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

            Rigidbody2D body = candidate.GetComponent<Rigidbody2D>();
            Collider2D collider = candidate.GetComponent<Collider2D>();
            MovementActorLifecycle lifecycle = candidate.GetComponent<MovementActorLifecycle>();
            PlayerCombatIntentAdapter input = candidate.GetComponent<PlayerCombatIntentAdapter>();
            EnemyTarget2DAdapter target = candidate.GetComponent<EnemyTarget2DAdapter>();
            SpriteRenderer renderer = candidate.GetComponent<SpriteRenderer>();
            TrailRenderer trail = candidate.GetComponentInChildren<TrailRenderer>(true);

            if (body == null
                || collider == null
                || lifecycle == null
                || lifecycle.Actor == null
                || input == null
                || target == null
                || renderer == null)
            {
                return Reject("stage1-player-projection-incomplete");
            }

            playerTransform = candidate;
            playerBody = body;
            playerCollider = collider;
            movementLifecycle = lifecycle;
            combatInput = input;
            targetAdapter = target;
            bodyRenderer = renderer;
            boostTrail = trail;
            spawnPosition = candidate.position;
            thrusterReader = new MovementActorThrusterStatusReader(
                movementLifecycle.Actor,
                movementLifecycle.Tuning);
            IsCaptured = true;
            RejectionCode = string.Empty;
            return true;
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
    }
}

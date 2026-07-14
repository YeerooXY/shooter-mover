using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.PursuerDrone
{
    /// <summary>
    /// Package-owned authority that delegates every transition to EN-002.
    /// </summary>
    public sealed class PursuerDroneAuthority : IEnemyActor2DAuthority
    {
        private readonly EnemyActorState initialState;
        private EnemyActorState currentState;

        internal PursuerDroneAuthority(EnemyActorState state)
        {
            initialState = state ?? throw new ArgumentNullException(nameof(state));
            currentState = state;
        }

        public EnemyActorState CurrentState
        {
            get { return currentState; }
        }

        public int ApplyCount { get; private set; }

        public int ResetCount { get; private set; }

        public bool TryReadState(out EnemyActorState state)
        {
            state = currentState;
            return state != null;
        }

        public EnemyActorStepResult Apply(EnemyActorCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            ApplyCount++;
            EnemyActorStepResult result = EnemyActorStepper.Step(
                currentState,
                new[] { command });
            currentState = result.State;
            return result;
        }

        public bool Reset()
        {
            ResetCount++;
            currentState = initialState;
            return true;
        }
    }

    internal interface IPursuerDronePositionSource
    {
        bool TryReadPosition(out double positionX, out double positionY);
    }

    internal sealed class PursuerDroneTransformPositionSource :
        IPursuerDronePositionSource
    {
        private readonly Transform actorTransform;

        public PursuerDroneTransformPositionSource(Transform actorTransform)
        {
            this.actorTransform = actorTransform
                ?? throw new ArgumentNullException(nameof(actorTransform));
        }

        public bool TryReadPosition(out double positionX, out double positionY)
        {
            positionX = 0d;
            positionY = 0d;
            if (actorTransform == null)
            {
                return false;
            }

            positionX = actorTransform.position.x;
            positionY = actorTransform.position.y;
            return true;
        }
    }

    /// <summary>
    /// Direct, engine-independent pursuit decision supplied through EN-003's decision port.
    /// </summary>
    public sealed class PursuerDroneDecisionSource : IEnemyActor2DDecisionSource
    {
        private readonly StableId actorId;
        private readonly IPursuerDronePositionSource positionSource;
        private readonly double movementSpeed;
        private readonly double stoppingDistanceSquared;
        private long sequence;

        internal PursuerDroneDecisionSource(
            StableId actorId,
            IPursuerDronePositionSource positionSource,
            double movementSpeed,
            double stoppingDistance)
        {
            this.actorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            this.positionSource = positionSource
                ?? throw new ArgumentNullException(nameof(positionSource));

            if (double.IsNaN(movementSpeed)
                || double.IsInfinity(movementSpeed)
                || movementSpeed <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(movementSpeed));
            }

            if (double.IsNaN(stoppingDistance)
                || double.IsInfinity(stoppingDistance)
                || stoppingDistance < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(stoppingDistance));
            }

            this.movementSpeed = movementSpeed;
            stoppingDistanceSquared = stoppingDistance * stoppingDistance;
        }

        public long Sequence
        {
            get { return sequence; }
        }

        public bool TryDecide(
            EnemyActorState state,
            EnemyTarget2DObservation target,
            double deltaTimeSeconds,
            out EnemyActor2DDecision decision)
        {
            decision = null;
            if (state == null
                || target == null
                || state.ActorId != actorId
                || !state.IsActive
                || double.IsNaN(deltaTimeSeconds)
                || double.IsInfinity(deltaTimeSeconds)
                || deltaTimeSeconds < 0d)
            {
                return false;
            }

            double positionX;
            double positionY;
            if (!positionSource.TryReadPosition(out positionX, out positionY))
            {
                return false;
            }

            double deltaX = target.PositionX - positionX;
            double deltaY = target.PositionY - positionY;
            double distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            double velocityX = 0d;
            double velocityY = 0d;
            if (distanceSquared > stoppingDistanceSquared && distanceSquared > 0d)
            {
                double scale = movementSpeed / Math.Sqrt(distanceSquared);
                velocityX = deltaX * scale;
                velocityY = deltaY * scale;
            }

            decision = new EnemyActor2DDecision(
                sequence,
                actorId,
                target.TargetId,
                velocityX,
                velocityY);
            if (sequence < long.MaxValue)
            {
                sequence++;
            }

            return true;
        }

        public void Reset()
        {
            sequence = 0L;
        }
    }

    /// <summary>
    /// Independently owned Pursuer Drone composition root. It configures, but never
    /// replaces, EN-003's shared target, contact, and movement adapters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PursuerDronePackage : MonoBehaviour
    {
        [SerializeField] private PursuerDroneDefinition definition;

        private Rigidbody2D enemyBody;
        private BoxCollider2D enemyCollider;
        private EnemyTarget2DAdapter targetAdapter;
        private EnemyContact2DAdapter contactAdapter;
        private EnemyActor2DAdapter actorAdapter;
        private PursuerDronePresentation2D presentation;
        private PursuerDroneAuthority authority;
        private PursuerDroneDecisionSource decisionSource;
        private bool configured;
        private bool activeRequested;

        public bool IsConfigured
        {
            get { return configured; }
        }

        public bool IsActive
        {
            get
            {
                return configured
                    && activeRequested
                    && isActiveAndEnabled
                    && actorAdapter != null
                    && actorAdapter.IsActive;
            }
        }

        public PursuerDroneDefinition Definition
        {
            get { return definition; }
        }

        public PursuerDroneAuthority Authority
        {
            get { return authority; }
        }

        public PursuerDroneDecisionSource DecisionSource
        {
            get { return decisionSource; }
        }

        public EnemyActor2DAdapter ActorAdapter
        {
            get { return actorAdapter; }
        }

        public EnemyTarget2DAdapter TargetAdapter
        {
            get { return targetAdapter; }
        }

        public EnemyContact2DAdapter ContactAdapter
        {
            get { return contactAdapter; }
        }

        public Rigidbody2D EnemyBody
        {
            get { return enemyBody; }
        }

        public Collider2D EnemyCollider
        {
            get { return enemyCollider; }
        }

        public void Configure(
            PursuerDroneDefinition packageDefinition,
            IEnemyTarget2DSource pursuitTarget,
            Collider2D moverCollider,
            StableId actorId,
            StableId moverId,
            CombatWeightClass moverWeight)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Pursuer Drone package is already configured.");
            }

            if (packageDefinition == null)
            {
                throw new ArgumentNullException(nameof(packageDefinition));
            }

            if (pursuitTarget == null)
            {
                throw new ArgumentNullException(nameof(pursuitTarget));
            }

            if (moverCollider == null)
            {
                throw new ArgumentNullException(nameof(moverCollider));
            }

            if (actorId == null)
            {
                throw new ArgumentNullException(nameof(actorId));
            }

            if (moverId == null)
            {
                throw new ArgumentNullException(nameof(moverId));
            }

            if (!Enum.IsDefined(typeof(CombatWeightClass), moverWeight))
            {
                throw new ArgumentOutOfRangeException(nameof(moverWeight));
            }

            packageDefinition.ValidateOrThrow();
            EnsurePackageComponents();

            definition = packageDefinition;
            authority = new PursuerDroneAuthority(
                definition.CreateInitialState(actorId));
            decisionSource = new PursuerDroneDecisionSource(
                actorId,
                new PursuerDroneTransformPositionSource(transform),
                definition.MovementSpeed,
                definition.StoppingDistance);

            targetAdapter.Configure(
                actorId,
                transform,
                enemyCollider,
                authority);
            contactAdapter.Configure(
                targetAdapter,
                authority,
                moverId,
                moverWeight,
                definition.MoverColliderCapacity);

            EnemyContact2DRegistrationStatus registration =
                contactAdapter.RegisterMoverCollider(
                    moverCollider,
                    moverId,
                    moverWeight);
            if (registration != EnemyContact2DRegistrationStatus.Registered
                && registration != EnemyContact2DRegistrationStatus.AlreadyRegistered)
            {
                throw new InvalidOperationException(
                    "The explicit mover collider could not be registered: " + registration);
            }

            actorAdapter.Configure(
                enemyBody,
                authority,
                decisionSource,
                pursuitTarget,
                contactAdapter,
                definition.MovementSpeed);
            presentation.Configure(definition.WarningPulseSeconds);

            configured = true;
            activeRequested = true;
            if (isActiveAndEnabled)
            {
                actorAdapter.Activate();
                presentation.SetRunning(true);
            }
        }

        public bool Activate()
        {
            if (!configured)
            {
                throw new InvalidOperationException(
                    "Pursuer Drone package must be configured before activation.");
            }

            bool changed = !activeRequested;
            activeRequested = true;
            if (isActiveAndEnabled && actorAdapter != null && !actorAdapter.IsActive)
            {
                changed = actorAdapter.Activate() || changed;
            }

            UpdatePresentation();
            return changed;
        }

        public bool Deactivate()
        {
            bool changed = activeRequested;
            activeRequested = false;
            if (actorAdapter != null)
            {
                changed = actorAdapter.Deactivate() || changed;
            }

            if (presentation != null)
            {
                presentation.SetRunning(false);
            }

            return changed;
        }

        public bool RestartSession()
        {
            if (!configured || actorAdapter == null)
            {
                return false;
            }

            bool reset = actorAdapter.Restart();
            if (!reset)
            {
                presentation.SetRunning(false);
                return false;
            }

            if (activeRequested
                && isActiveAndEnabled
                && !actorAdapter.IsActive)
            {
                actorAdapter.Activate();
            }

            UpdatePresentation();
            return true;
        }

        private void Awake()
        {
            EnsurePackageComponents();
        }

        private void OnEnable()
        {
            if (configured
                && activeRequested
                && actorAdapter != null
                && !actorAdapter.IsActive)
            {
                actorAdapter.Activate();
                UpdatePresentation();
            }
        }

        private void OnDisable()
        {
            if (actorAdapter != null)
            {
                actorAdapter.Deactivate();
            }

            if (presentation != null)
            {
                presentation.SetRunning(false);
            }
        }

        private void OnDestroy()
        {
            if (actorAdapter != null)
            {
                actorAdapter.Deactivate();
            }

            configured = false;
            activeRequested = false;
            authority = null;
            decisionSource = null;
        }

        private void LateUpdate()
        {
            UpdatePresentation();
        }

        private void UpdatePresentation()
        {
            if (presentation == null)
            {
                return;
            }

            EnemyActorState state;
            bool actorRunning = configured
                && activeRequested
                && isActiveAndEnabled
                && actorAdapter != null
                && actorAdapter.IsActive
                && authority != null
                && authority.TryReadState(out state)
                && state != null
                && state.IsActive;
            presentation.SetDestroyed(
                configured
                    && authority != null
                    && authority.TryReadState(out state)
                    && state != null
                    && state.IsDestroyed);
            presentation.SetRunning(actorRunning);
        }

        private void EnsurePackageComponents()
        {
            enemyBody = GetComponent<Rigidbody2D>();
            if (enemyBody == null)
            {
                enemyBody = gameObject.AddComponent<Rigidbody2D>();
            }

            enemyBody.gravityScale = 0f;
            enemyBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            enemyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            enemyCollider = GetComponent<BoxCollider2D>();
            if (enemyCollider == null)
            {
                enemyCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            enemyCollider.isTrigger = false;
            enemyCollider.size = new Vector2(1.2f, 0.9f);

            targetAdapter = GetComponent<EnemyTarget2DAdapter>();
            if (targetAdapter == null)
            {
                targetAdapter = gameObject.AddComponent<EnemyTarget2DAdapter>();
            }

            contactAdapter = GetComponent<EnemyContact2DAdapter>();
            if (contactAdapter == null)
            {
                contactAdapter = gameObject.AddComponent<EnemyContact2DAdapter>();
            }

            actorAdapter = GetComponent<EnemyActor2DAdapter>();
            if (actorAdapter == null)
            {
                actorAdapter = gameObject.AddComponent<EnemyActor2DAdapter>();
            }

            presentation = GetComponent<PursuerDronePresentation2D>();
            if (presentation == null)
            {
                presentation = gameObject.AddComponent<PursuerDronePresentation2D>();
            }
        }
    }
}

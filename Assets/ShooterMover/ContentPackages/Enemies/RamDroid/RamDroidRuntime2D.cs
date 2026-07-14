using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.RamDroid
{
    /// <summary>
    /// Independently owned Ram Droid composition. EN-002 remains health/contact truth
    /// and EN-003 remains the only Unity movement/contact boundary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RamDroidRuntime2D :
        MonoBehaviour,
        IEnemyActor2DAuthority,
        IEnemyActor2DDecisionSource
    {
        [SerializeField] private RamDroidDefinition definition;
        [SerializeField] private Rigidbody2D enemyBody;
        [SerializeField] private CircleCollider2D enemyCollider;
        [SerializeField] private EnemyTarget2DAdapter enemyTarget;
        [SerializeField] private EnemyContact2DAdapter contactAdapter;
        [SerializeField] private EnemyActor2DAdapter actorAdapter;
        [SerializeField] private RamDroidTemporaryPresentation temporaryPresentation;

        private StableId actorId;
        private EnemyActorState currentState;
        private EnemyTarget2DAdapter playerTarget;
        private long decisionSequence;
        private double simulationTimeSeconds;
        private bool configured;

        public bool IsConfigured
        {
            get { return configured; }
        }

        public RamDroidDefinition Definition
        {
            get { return definition; }
        }

        public EnemyActorState CurrentState
        {
            get { return currentState; }
        }

        public EnemyActor2DAdapter ActorAdapter
        {
            get { return actorAdapter; }
        }

        public EnemyContact2DAdapter ContactAdapter
        {
            get { return contactAdapter; }
        }

        public EnemyTarget2DAdapter EnemyTarget
        {
            get { return enemyTarget; }
        }

        public void ConfigureSession(
            StableId stableActorId,
            EnemyTarget2DAdapter observedPlayerTarget,
            Collider2D[] playerColliders,
            StableId playerId,
            CombatWeightClass playerWeight)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Ram Droid session composition is explicit and may only be configured once.");
            }

            if (stableActorId == null)
            {
                throw new ArgumentNullException(nameof(stableActorId));
            }

            if (observedPlayerTarget == null || !observedPlayerTarget.IsConfigured)
            {
                throw new ArgumentException(
                    "A configured EN-003 player target is required.",
                    nameof(observedPlayerTarget));
            }

            if (playerColliders == null || playerColliders.Length == 0)
            {
                throw new ArgumentException(
                    "At least one explicit player Collider2D is required.",
                    nameof(playerColliders));
            }

            if (playerId == null)
            {
                throw new ArgumentNullException(nameof(playerId));
            }

            if (!Enum.IsDefined(typeof(CombatWeightClass), playerWeight))
            {
                throw new ArgumentOutOfRangeException(nameof(playerWeight));
            }

            ResolvePackageComponents();
            definition.ValidateOrThrow();
            if (playerColliders.Length > definition.ContactCapacity)
            {
                throw new ArgumentException(
                    "Player collider count exceeds the package contact capacity.",
                    nameof(playerColliders));
            }

            actorId = stableActorId;
            playerTarget = observedPlayerTarget;
            currentState = definition.CreateInitialState(actorId);
            decisionSequence = 0L;
            simulationTimeSeconds = 0d;

            enemyBody.bodyType = RigidbodyType2D.Dynamic;
            enemyBody.gravityScale = 0f;
            enemyBody.freezeRotation = true;
            enemyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            enemyCollider.isTrigger = false;
            enemyCollider.radius = definition.ColliderRadius;

            enemyTarget.Configure(
                actorId,
                transform,
                enemyCollider,
                this);
            contactAdapter.Configure(
                enemyTarget,
                this,
                playerId,
                playerWeight,
                definition.ContactCapacity);

            for (int index = 0; index < playerColliders.Length; index++)
            {
                Collider2D playerCollider = playerColliders[index];
                if (playerCollider == null)
                {
                    throw new ArgumentException(
                        "Player collider collection cannot contain null.",
                        nameof(playerColliders));
                }

                EnemyContact2DRegistrationStatus registration =
                    contactAdapter.RegisterMoverCollider(
                        playerCollider,
                        playerId,
                        playerWeight);
                if (registration != EnemyContact2DRegistrationStatus.Registered
                    && registration != EnemyContact2DRegistrationStatus.AlreadyRegistered)
                {
                    throw new InvalidOperationException(
                        "The accepted EN-003 contact adapter rejected a player collider: "
                        + registration);
                }
            }

            temporaryPresentation.Configure(definition);
            actorAdapter.Configure(
                enemyBody,
                this,
                this,
                playerTarget,
                contactAdapter,
                definition.MovementSpeed);
            configured = true;
            actorAdapter.Activate();
            UpdateWarning();
        }

        public EnemyActor2DFixedStepResult ExecuteFixedStep(double deltaTimeSeconds)
        {
            if (!configured)
            {
                throw new InvalidOperationException(
                    "Ram Droid must be configured before fixed-step execution.");
            }

            if (double.IsNaN(deltaTimeSeconds)
                || double.IsInfinity(deltaTimeSeconds)
                || deltaTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));
            }

            EnemyActor2DFixedStepResult result =
                actorAdapter.ExecuteFixedStep(deltaTimeSeconds);
            simulationTimeSeconds += deltaTimeSeconds;
            UpdateWarning();
            return result;
        }

        public EnemyContact2DApplication TryProcessImpact(
            Collider2D contactedCollider,
            ContactClassification classification,
            double observedAtSeconds)
        {
            if (!configured)
            {
                throw new InvalidOperationException(
                    "Ram Droid must be configured before impact processing.");
            }

            EnemyContact2DApplication application = contactAdapter.TryProcessContact(
                contactedCollider,
                classification,
                observedAtSeconds);
            UpdateWarning();
            return application;
        }

        public bool RestartSession()
        {
            if (!configured)
            {
                return false;
            }

            bool restarted = actorAdapter.Restart();
            if (restarted)
            {
                simulationTimeSeconds = 0d;
                UpdateWarning();
            }

            return restarted;
        }

        public bool ActivateSession()
        {
            return configured && actorAdapter.Activate();
        }

        public bool DeactivateSession()
        {
            if (!configured)
            {
                return false;
            }

            temporaryPresentation.UpdateWarning(false, simulationTimeSeconds);
            return actorAdapter.Deactivate();
        }

        public bool TryReadState(out EnemyActorState state)
        {
            state = currentState;
            return configured && state != null;
        }

        public EnemyActorStepResult Apply(EnemyActorCommand command)
        {
            if (!configured || currentState == null)
            {
                throw new InvalidOperationException(
                    "Ram Droid authority is unavailable before session configuration.");
            }

            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            EnemyActorStepResult result = EnemyActorStepper.Step(
                currentState,
                new[] { command });
            currentState = result.State;
            return result;
        }

        public bool TryDecide(
            EnemyActorState state,
            EnemyTarget2DObservation target,
            double deltaTimeSeconds,
            out EnemyActor2DDecision decision)
        {
            decision = null;
            if (!configured
                || state == null
                || target == null
                || !state.IsActive
                || state.ActorId != actorId
                || double.IsNaN(deltaTimeSeconds)
                || double.IsInfinity(deltaTimeSeconds)
                || deltaTimeSeconds < 0d)
            {
                return false;
            }

            double offsetX = target.PositionX - transform.position.x;
            double offsetY = target.PositionY - transform.position.y;
            double magnitudeSquared = (offsetX * offsetX) + (offsetY * offsetY);
            double velocityX = 0d;
            double velocityY = 0d;
            if (magnitudeSquared > 0.000000000001d)
            {
                double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
                velocityX = offsetX * inverseMagnitude * definition.MovementSpeed;
                velocityY = offsetY * inverseMagnitude * definition.MovementSpeed;
            }

            decision = new EnemyActor2DDecision(
                decisionSequence,
                state.ActorId,
                target.TargetId,
                velocityX,
                velocityY);
            if (decisionSequence < long.MaxValue)
            {
                decisionSequence++;
            }

            return true;
        }

        bool IEnemyActor2DAuthority.Reset()
        {
            if (!configured || actorId == null || definition == null)
            {
                return false;
            }

            currentState = definition.CreateInitialState(actorId);
            decisionSequence = 0L;
            simulationTimeSeconds = 0d;
            temporaryPresentation.Configure(definition);
            return true;
        }

        void IEnemyActor2DDecisionSource.Reset()
        {
            decisionSequence = 0L;
        }

        private void ResolvePackageComponents()
        {
            if (definition == null)
            {
                throw new InvalidOperationException(
                    "Ram Droid prefab must reference its package-owned definition asset.");
            }

            enemyBody = enemyBody == null ? GetComponent<Rigidbody2D>() : enemyBody;
            enemyCollider = enemyCollider == null
                ? GetComponent<CircleCollider2D>()
                : enemyCollider;
            enemyTarget = enemyTarget == null
                ? GetComponent<EnemyTarget2DAdapter>()
                : enemyTarget;
            contactAdapter = contactAdapter == null
                ? GetComponent<EnemyContact2DAdapter>()
                : contactAdapter;
            actorAdapter = actorAdapter == null
                ? GetComponent<EnemyActor2DAdapter>()
                : actorAdapter;
            temporaryPresentation = temporaryPresentation == null
                ? GetComponent<RamDroidTemporaryPresentation>()
                : temporaryPresentation;

            if (enemyBody == null
                || enemyCollider == null
                || enemyTarget == null
                || contactAdapter == null
                || actorAdapter == null
                || temporaryPresentation == null)
            {
                throw new InvalidOperationException(
                    "Ram Droid prefab is missing a required package or EN-003 2D component.");
            }
        }

        private void UpdateWarning()
        {
            if (temporaryPresentation == null || definition == null)
            {
                return;
            }

            EnemyTarget2DObservation observation;
            bool targetAvailable = playerTarget != null
                && playerTarget.TryReadTarget(out observation);
            bool visible = false;
            if (targetAvailable && currentState != null && currentState.IsActive)
            {
                double offsetX = observation.PositionX - transform.position.x;
                double offsetY = observation.PositionY - transform.position.y;
                double warningDistance = definition.WarningDistance;
                visible = ((offsetX * offsetX) + (offsetY * offsetY))
                    <= warningDistance * warningDistance;
            }

            temporaryPresentation.UpdateWarning(visible, simulationTimeSeconds);
        }

        private void OnDisable()
        {
            if (actorAdapter != null)
            {
                actorAdapter.Deactivate();
            }

            if (temporaryPresentation != null)
            {
                temporaryPresentation.UpdateWarning(false, simulationTimeSeconds);
            }
        }

        private void OnDestroy()
        {
            configured = false;
            actorId = null;
            currentState = null;
            playerTarget = null;
        }
    }
}

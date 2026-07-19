using System;
using System.Collections.Generic;
using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.MobileBlasterDroid
{
    public enum MobileBlasterDroidFirePhase
    {
        Ready = 1,
        WindUp = 2,
        Recovery = 3,
    }

    /// <summary>
    /// Package-owned composition for one moving ranged enemy. EN-002 remains health and
    /// lifecycle truth, EN-003 remains movement/contact projection, the shared enemy
    /// perception/decision policy owns live intent selection, CB-009 remains plan execution
    /// authority, and WP-002/WP-003 remain projectile and Blaster authority.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class MobileBlasterDroidRuntime2D :
        MonoBehaviour,
        IEnemyActor2DAuthority,
        IEnemyActor2DDecisionSource
    {
        private static readonly StableId MountIdValue =
            StableId.Parse("weapon-mount.mobile-blaster-droid");
        private static readonly StableId EnemyFactionIdValue =
            StableId.Parse("faction.enemy");
        private static readonly StableId PlayerFactionIdValue =
            StableId.Parse("faction.player");
        private static readonly StableId WindUpPhaseIdValue =
            StableId.Parse("enemy-phase.mobile-blaster-droid.wind-up");
        private static readonly StableId RecoveryPhaseIdValue =
            StableId.Parse("enemy-phase.mobile-blaster-droid.recovery");

        [SerializeField] private MobileBlasterDroidDefinition definition;
        [SerializeField] private BoundedProjectile2D acceptedProjectilePrefab;
        [SerializeField] private Rigidbody2D enemyBody;
        [SerializeField] private CircleCollider2D enemyCollider;
        [SerializeField] private MobileBlasterDroidTemporaryPresentation temporaryPresentation;

        private readonly Queue<EnemyAttackIntent> acceptedAttackIntents =
            new Queue<EnemyAttackIntent>();
        private EnemyTarget2DAdapter enemyTarget;
        private EnemyContact2DAdapter contactAdapter;
        private EnemyActor2DAdapter actorAdapter;
        private WeaponMount2DAdapter weaponMount;
        private Transform muzzle;
        private EnemyTarget2DAdapter playerTarget;
        private Collider2D[] playerColliders = new Collider2D[0];
        private CombatHit2DAdapter hitAdapter;
        private ProjectileExecutionPlanAdapter projectileExecutor;
        private WeaponBehaviorPipeline blasterPipeline;
        private StableId actorId;
        private StableId playerId;
        private GameplayEntityIdentity identity;
        private EnemyDecisionProfile decisionProfile;
        private EnemyActorState currentState;
        private MobileBlasterDroidFirePhase firePhase = MobileBlasterDroidFirePhase.Ready;
        private double firePhaseElapsedSeconds;
        private double presentationElapsedSeconds;
        private double lockedDirectionX = 1d;
        private double lockedDirectionY;
        private long decisionSequence;
        private long simulationStep;
        private long fireAttemptCount;
        private long successfulShotCount;
        private long generation;
        private bool hasLockedDirection;
        private bool configured;
        private bool activeRequested;
        private WeaponFireExecutionPlan lastExecutionPlan;
        private WeaponMount2DExecutionResult lastExecutionResult;
        private EnemyDecisionEvaluation lastDecisionEvaluation;
        private EnemyPerceptionSnapshot lastPerceptionSnapshot;
        private EnemyAttackIntent pendingAttackIntent;
        private EnemyAttackIntent lastAcceptedAttackIntent;
        private EnemyDestroyedNotification lastDestroyedNotification;

        public bool IsConfigured { get { return configured; } }

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

        public MobileBlasterDroidDefinition Definition { get { return definition; } }
        public EnemyActorState CurrentState { get { return currentState; } }
        public MobileBlasterDroidFirePhase FirePhase { get { return firePhase; } }
        public double FirePhaseElapsedSeconds { get { return firePhaseElapsedSeconds; } }
        public long FireAttemptCount { get { return fireAttemptCount; } }
        public long SuccessfulShotCount { get { return successfulShotCount; } }
        public long DecisionSequence { get { return decisionSequence; } }
        public long Generation { get { return generation; } }
        public EnemyDecisionEvaluation LastDecisionEvaluation { get { return lastDecisionEvaluation; } }
        public EnemyDebugSnapshot LiveDebugSnapshot
        {
            get { return lastDecisionEvaluation == null ? null : lastDecisionEvaluation.Debug; }
        }
        public EnemyPerceptionSnapshot LastPerceptionSnapshot { get { return lastPerceptionSnapshot; } }
        public EnemyAttackIntent LastAcceptedAttackIntent { get { return lastAcceptedAttackIntent; } }
        public EnemyDestroyedNotification LastDestroyedNotification { get { return lastDestroyedNotification; } }
        public int PendingAttackIntentCount { get { return acceptedAttackIntents.Count; } }

        public StableId CurrentBehaviorPhaseId
        {
            get
            {
                switch (firePhase)
                {
                    case MobileBlasterDroidFirePhase.WindUp:
                        return WindUpPhaseIdValue;
                    case MobileBlasterDroidFirePhase.Recovery:
                        return RecoveryPhaseIdValue;
                    default:
                        return definition == null ? null : definition.ReadyPhaseId;
                }
            }
        }

        public EnemyRuntimeProjection CurrentRuntimeProjection
        {
            get
            {
                if (!configured || definition == null || identity == null || currentState == null)
                {
                    return null;
                }

                return definition.CreateRuntimeProjection(
                    identity,
                    currentState,
                    generation,
                    playerId,
                    CurrentBehaviorPhaseId);
            }
        }

        public bool BlocksRoomClear
        {
            get
            {
                EnemyRuntimeProjection projection = CurrentRuntimeProjection;
                return projection != null && projection.BlocksRoomClear;
            }
        }

        public Vector2 LockedDirection
        {
            get { return new Vector2((float)lockedDirectionX, (float)lockedDirectionY); }
        }

        public bool HasLockedDirection { get { return hasLockedDirection; } }

        public int ActiveProjectileCount
        {
            get { return projectileExecutor == null ? 0 : projectileExecutor.ActiveProjectileCount; }
        }

        public BoundedProjectile2D LastSpawnedProjectile
        {
            get { return projectileExecutor == null ? null : projectileExecutor.LastSpawnedProjectile; }
        }

        public WeaponFireExecutionPlan LastExecutionPlan { get { return lastExecutionPlan; } }
        public WeaponMount2DExecutionResult LastExecutionResult { get { return lastExecutionResult; } }
        public EnemyActor2DAdapter ActorAdapter { get { return actorAdapter; } }
        public EnemyTarget2DAdapter EnemyTarget { get { return enemyTarget; } }
        public EnemyContact2DAdapter ContactAdapter { get { return contactAdapter; } }
        public WeaponMount2DAdapter WeaponMount { get { return weaponMount; } }
        public Rigidbody2D EnemyBody { get { return enemyBody; } }
        public Collider2D EnemyCollider { get { return enemyCollider; } }

        public MobileBlasterDroidTemporaryPresentation Presentation
        {
            get { return temporaryPresentation; }
        }

        public void ConfigureSession(
            MobileBlasterDroidDefinition packageDefinition,
            StableId stableActorId,
            EnemyTarget2DAdapter observedPlayerTarget,
            Collider2D[] observedPlayerColliders,
            StableId stablePlayerId,
            CombatWeightClass playerWeight,
            BoundedProjectile2D projectilePrefab)
        {
            ConfigureSession(
                packageDefinition,
                stableActorId,
                observedPlayerTarget,
                observedPlayerColliders,
                stablePlayerId,
                playerWeight,
                projectilePrefab,
                GameplayEntityOwnership.None());
        }

        public void ConfigureSession(
            MobileBlasterDroidDefinition packageDefinition,
            StableId stableActorId,
            EnemyTarget2DAdapter observedPlayerTarget,
            Collider2D[] observedPlayerColliders,
            StableId stablePlayerId,
            CombatWeightClass playerWeight,
            BoundedProjectile2D projectilePrefab,
            GameplayEntityOwnership ownership)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Mobile Blaster Droid session composition may only be configured once.");
            }

            if (packageDefinition == null) throw new ArgumentNullException(nameof(packageDefinition));
            if (stableActorId == null) throw new ArgumentNullException(nameof(stableActorId));
            if (ownership == null) throw new ArgumentNullException(nameof(ownership));
            if (observedPlayerTarget == null || !observedPlayerTarget.IsConfigured)
            {
                throw new ArgumentException(
                    "A configured EN-003 player target is required.",
                    nameof(observedPlayerTarget));
            }

            if (observedPlayerColliders == null || observedPlayerColliders.Length == 0)
            {
                throw new ArgumentException(
                    "At least one explicit player Collider2D is required.",
                    nameof(observedPlayerColliders));
            }

            if (stablePlayerId == null) throw new ArgumentNullException(nameof(stablePlayerId));
            if (!Enum.IsDefined(typeof(CombatWeightClass), playerWeight))
                throw new ArgumentOutOfRangeException(nameof(playerWeight));
            if (projectilePrefab == null) throw new ArgumentNullException(nameof(projectilePrefab));

            EnsurePackageComponents();
            packageDefinition.ValidateOrThrow();
            if (observedPlayerColliders.Length > packageDefinition.ContactCapacity)
            {
                throw new ArgumentException(
                    "Player collider count exceeds the package contact capacity.",
                    nameof(observedPlayerColliders));
            }

            definition = packageDefinition;
            acceptedProjectilePrefab = projectilePrefab;
            actorId = stableActorId;
            playerId = stablePlayerId;
            playerTarget = observedPlayerTarget;
            playerColliders = CopyPlayerColliders(observedPlayerColliders);
            identity = new GameplayEntityIdentity(actorId, ownership, EnemyFactionIdValue);
            decisionProfile = definition.CreateDecisionProfile();
            currentState = definition.CreateInitialState(actorId);
            ResetSessionState(false);

            enemyBody.bodyType = RigidbodyType2D.Dynamic;
            enemyBody.gravityScale = 0f;
            enemyBody.freezeRotation = true;
            enemyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            enemyCollider.isTrigger = false;
            enemyCollider.radius = (float)definition.ColliderRadius;
            muzzle.localPosition = new Vector3((float)definition.MuzzleOffset, 0f, 0f);

            enemyTarget.Configure(actorId, transform, enemyCollider, this);
            contactAdapter.Configure(
                enemyTarget,
                this,
                playerId,
                playerWeight,
                definition.ContactCapacity);
            RegisterPlayerColliders(playerColliders, playerId, playerWeight);

            hitAdapter = new CombatHit2DAdapter(actorId);
            CombatHit2DTargetRegistrationStatus targetRegistration =
                playerTarget.RegisterForCombatHits(hitAdapter);
            if (targetRegistration != CombatHit2DTargetRegistrationStatus.Registered
                && targetRegistration != CombatHit2DTargetRegistrationStatus.AlreadyRegistered)
            {
                throw new InvalidOperationException(
                    "The explicit player target could not be registered for Blaster hits: "
                    + targetRegistration);
            }

            projectileExecutor = new ProjectileExecutionPlanAdapter(
                BlasterMachineGunPackage.OperationKindId,
                acceptedProjectilePrefab,
                hitAdapter,
                new Collider2D[] { enemyCollider },
                null,
                false,
                0.12f);
            weaponMount.Configure(
                actorId,
                BlasterMachineGunPackage.WeaponId,
                MountIdValue,
                new IWeaponFireExecutionOperation2DHandler[] { projectileExecutor });
            blasterPipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[]
                {
                    BlasterMachineGunPackage.CreateBehaviorModule(),
                });

            actorAdapter.Configure(
                enemyBody,
                this,
                this,
                playerTarget,
                contactAdapter,
                definition.MovementSpeed);
            temporaryPresentation.Configure(definition);

            configured = true;
            activeRequested = true;
            if (isActiveAndEnabled)
            {
                actorAdapter.Activate();
            }

            UpdatePresentation();
        }

        /// <summary>
        /// Deterministic test/lifecycle boundary. In normal play EN-003 drives movement
        /// from its own FixedUpdate while this package's FixedUpdate advances fire cadence.
        /// </summary>
        public EnemyActor2DFixedStepResult ExecuteFixedStep(double deltaTimeSeconds)
        {
            if (!configured)
            {
                throw new InvalidOperationException(
                    "Mobile Blaster Droid must be configured before fixed-step execution.");
            }

            ValidateDeltaTime(deltaTimeSeconds);
            EnemyActor2DFixedStepResult movementResult =
                actorAdapter.ExecuteFixedStep(deltaTimeSeconds);
            ExecuteFireFixedStep(deltaTimeSeconds);
            return movementResult;
        }

        public bool TryDequeueAttackIntent(out EnemyAttackIntent intent)
        {
            if (acceptedAttackIntents.Count == 0)
            {
                intent = null;
                return false;
            }

            intent = acceptedAttackIntents.Dequeue();
            return true;
        }

        public bool TryReadRuntimeProjection(out EnemyRuntimeProjection projection)
        {
            projection = CurrentRuntimeProjection;
            return projection != null;
        }

        public bool ActivateSession()
        {
            if (!configured) return false;

            bool changed = !activeRequested;
            activeRequested = true;
            if (isActiveAndEnabled && !actorAdapter.IsActive)
            {
                changed = actorAdapter.Activate() || changed;
            }

            UpdatePresentation();
            return changed;
        }

        public bool DeactivateSession()
        {
            bool changed = activeRequested;
            activeRequested = false;
            CancelPendingFire();
            acceptedAttackIntents.Clear();
            if (projectileExecutor != null)
            {
                projectileExecutor.ResetSession();
            }

            if (actorAdapter != null)
            {
                changed = actorAdapter.Deactivate() || changed;
            }

            UpdatePresentation();
            return changed;
        }

        public bool RestartSession()
        {
            if (!configured || actorAdapter == null) return false;

            bool restarted = actorAdapter.Restart();
            if (!restarted)
            {
                CancelPendingFire();
                UpdatePresentation();
                return false;
            }

            if (generation < long.MaxValue)
            {
                generation++;
            }

            if (hitAdapter != null)
            {
                hitAdapter.ResetProcessedEvents();
            }

            if (activeRequested && isActiveAndEnabled && !actorAdapter.IsActive)
            {
                actorAdapter.Activate();
            }

            UpdatePresentation();
            return true;
        }

        public bool TryReadState(out EnemyActorState state)
        {
            state = currentState;
            return state != null;
        }

        public EnemyActorStepResult Apply(EnemyActorCommand command)
        {
            if (!configured || currentState == null)
            {
                throw new InvalidOperationException(
                    "Mobile Blaster Droid authority is unavailable before session configuration.");
            }

            if (command == null) throw new ArgumentNullException(nameof(command));

            bool wasActive = currentState.IsActive;
            EnemyActorStepResult result = EnemyActorStepper.Step(
                currentState,
                new[] { command });
            currentState = result.State;
            for (int index = 0; index < result.Notifications.Count; index++)
            {
                EnemyDestroyedNotification destroyed =
                    result.Notifications[index] as EnemyDestroyedNotification;
                if (destroyed != null)
                {
                    lastDestroyedNotification = destroyed;
                }
            }

            if (wasActive && !currentState.IsActive)
            {
                CancelPendingFire();
                acceptedAttackIntents.Clear();
                if (projectileExecutor != null)
                {
                    projectileExecutor.ResetSession();
                }
            }

            UpdatePresentation();
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

            EnemyDecisionEvaluation evaluation = EvaluateLiveDecision(state, target);
            EnemyVector2 desiredMovement = evaluation.Decision.DesiredMovement;
            decision = new EnemyActor2DDecision(
                decisionSequence,
                state.ActorId,
                target.TargetId,
                desiredMovement.X * definition.MovementSpeed,
                desiredMovement.Y * definition.MovementSpeed);
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
            ResetSessionState(true);
            return true;
        }

        void IEnemyActor2DDecisionSource.Reset()
        {
            decisionSequence = 0L;
            CancelPendingFire();
            lastDecisionEvaluation = null;
            lastPerceptionSnapshot = null;
        }

        private void Awake()
        {
            EnsurePackageComponents();
        }

        private void OnEnable()
        {
            if (configured && activeRequested && actorAdapter != null && !actorAdapter.IsActive)
            {
                actorAdapter.Activate();
                UpdatePresentation();
            }
        }

        private void FixedUpdate()
        {
            if (configured)
            {
                ExecuteFireFixedStep(Time.fixedDeltaTime);
            }
        }

        private void Update()
        {
            if (!configured) return;

            presentationElapsedSeconds += Time.deltaTime;
            UpdatePresentation();
        }

        private void OnDisable()
        {
            if (actorAdapter != null)
            {
                actorAdapter.Deactivate();
            }

            CancelPendingFire();
            acceptedAttackIntents.Clear();
            if (projectileExecutor != null)
            {
                projectileExecutor.ResetSession();
            }

            UpdatePresentation();
        }

        private void OnDestroy()
        {
            if (playerTarget != null && hitAdapter != null)
            {
                playerTarget.UnregisterFromCombatHits(hitAdapter);
            }

            if (projectileExecutor != null)
            {
                projectileExecutor.Dispose();
            }

            if (weaponMount != null)
            {
                weaponMount.ClearConfiguration();
            }

            configured = false;
            activeRequested = false;
            currentState = null;
            playerTarget = null;
            playerColliders = new Collider2D[0];
            hitAdapter = null;
            projectileExecutor = null;
            blasterPipeline = null;
            identity = null;
            decisionProfile = null;
            lastDecisionEvaluation = null;
            lastPerceptionSnapshot = null;
            pendingAttackIntent = null;
            lastAcceptedAttackIntent = null;
            lastDestroyedNotification = null;
            acceptedAttackIntents.Clear();
        }

        private void ExecuteFireFixedStep(double deltaTimeSeconds)
        {
            ValidateDeltaTime(deltaTimeSeconds);
            presentationElapsedSeconds += deltaTimeSeconds;

            EnemyActorState state;
            EnemyTarget2DObservation target = null;
            bool actorAvailable = TryReadState(out state)
                && state != null
                && state.IsActive;
            bool targetAvailable = actorAvailable
                && playerTarget != null
                && playerTarget.TryReadTarget(out target)
                && target != null;

            if (!actorAvailable || !targetAvailable || !IsActive)
            {
                CancelPendingFire();
                if (!actorAvailable && projectileExecutor != null)
                {
                    projectileExecutor.ResetSession();
                }
            }
            else
            {
                EnemyDecisionEvaluation evaluation = EvaluateLiveDecision(state, target);
                if (firePhase == MobileBlasterDroidFirePhase.WindUp
                    && evaluation.Decision.SelectedTargetId == null)
                {
                    CancelPendingFire();
                }
                else
                {
                    AdvanceFireState(evaluation, deltaTimeSeconds);
                }
            }

            if (simulationStep < long.MaxValue)
            {
                simulationStep++;
            }

            UpdatePresentation();
        }

        private EnemyDecisionEvaluation EvaluateLiveDecision(
            EnemyActorState state,
            EnemyTarget2DObservation target)
        {
            Vector3 observerPosition = transform.position;
            Vector3 facing = transform.right;
            bool hasLineOfSight = HasLineOfSightToTarget(target);
            EnemyPerceptionCandidate candidate = new EnemyPerceptionCandidate(
                target.TargetId,
                PlayerFactionIdValue,
                EnemyTargetRelationship.Hostile,
                new EnemyVector2(target.PositionX, target.PositionY),
                new EnemyVector2(),
                hasLineOfSight);
            lastPerceptionSnapshot = EnemyPerceptionBuilder.Build(
                new EnemyVector2(observerPosition.x, observerPosition.y),
                new EnemyVector2(facing.x, facing.y),
                new[] { candidate },
                definition.DetectionRadius,
                definition.VisionArcDegrees,
                simulationStep);
            EnemyRuntimeProjection runtime = definition.CreateRuntimeProjection(
                identity,
                state,
                generation,
                target.TargetId,
                CurrentBehaviorPhaseId);
            Vector3 attackOrigin = muzzle.position;
            lastDecisionEvaluation = EnemyDecisionPolicy.Evaluate(
                runtime,
                decisionProfile,
                lastPerceptionSnapshot,
                new EnemyVector2(attackOrigin.x, attackOrigin.y));
            return lastDecisionEvaluation;
        }

        private bool HasLineOfSightToTarget(EnemyTarget2DObservation target)
        {
            Vector2 origin = muzzle == null ? (Vector2)transform.position : (Vector2)muzzle.position;
            Vector2 targetPoint = new Vector2((float)target.PositionX, (float)target.PositionY);
            var hits = Physics2D.LinecastAll(origin, targetPoint);
            for (int index = 0; index < hits.Length; index++)
            {
                Collider2D hitCollider = hits[index].collider;
                if (hitCollider == null || IsOwnCollider(hitCollider))
                {
                    continue;
                }

                return IsPlayerCollider(hitCollider);
            }

            return true;
        }

        private bool IsOwnCollider(Collider2D candidate)
        {
            return candidate == enemyCollider
                || candidate.transform == transform
                || candidate.transform.IsChildOf(transform);
        }

        private bool IsPlayerCollider(Collider2D candidate)
        {
            for (int index = 0; index < playerColliders.Length; index++)
            {
                if (playerColliders[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateDeltaTime(double deltaTimeSeconds)
        {
            if (double.IsNaN(deltaTimeSeconds)
                || double.IsInfinity(deltaTimeSeconds)
                || deltaTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));
            }
        }

        private static Collider2D[] CopyPlayerColliders(Collider2D[] colliders)
        {
            Collider2D[] copy = new Collider2D[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] == null)
                {
                    throw new ArgumentException(
                        "Player collider collection cannot contain null.",
                        nameof(colliders));
                }

                copy[index] = colliders[index];
            }

            return copy;
        }

        private void RegisterPlayerColliders(
            Collider2D[] colliders,
            StableId stablePlayerId,
            CombatWeightClass playerWeight)
        {
            for (int index = 0; index < colliders.Length; index++)
            {
                EnemyContact2DRegistrationStatus registration =
                    contactAdapter.RegisterMoverCollider(
                        colliders[index],
                        stablePlayerId,
                        playerWeight);
                if (registration != EnemyContact2DRegistrationStatus.Registered
                    && registration != EnemyContact2DRegistrationStatus.AlreadyRegistered)
                {
                    throw new InvalidOperationException(
                        "The accepted EN-003 contact adapter rejected a player collider: "
                        + registration);
                }
            }
        }

        private void AdvanceFireState(
            EnemyDecisionEvaluation evaluation,
            double deltaTimeSeconds)
        {
            switch (firePhase)
            {
                case MobileBlasterDroidFirePhase.Ready:
                    if (evaluation.Decision.RequestedAttack != null)
                    {
                        AcceptAttackIntent(evaluation.Decision.RequestedAttack);
                    }
                    return;

                case MobileBlasterDroidFirePhase.WindUp:
                    firePhaseElapsedSeconds += deltaTimeSeconds;
                    if (firePhaseElapsedSeconds >= definition.WindUpSeconds)
                    {
                        FireAcceptedBlaster(pendingAttackIntent);
                        pendingAttackIntent = null;
                        firePhase = MobileBlasterDroidFirePhase.Recovery;
                        firePhaseElapsedSeconds = 0d;
                        hasLockedDirection = false;
                    }
                    return;

                case MobileBlasterDroidFirePhase.Recovery:
                    firePhaseElapsedSeconds += deltaTimeSeconds;
                    if (firePhaseElapsedSeconds >= definition.RecoverySeconds)
                    {
                        firePhase = MobileBlasterDroidFirePhase.Ready;
                        firePhaseElapsedSeconds = 0d;
                    }
                    return;

                default:
                    CancelPendingFire();
                    return;
            }
        }

        private void AcceptAttackIntent(EnemyAttackIntent intent)
        {
            if (intent == null
                || intent.AttackerEntityId != actorId
                || intent.TargetEntityId != playerId
                || intent.AttackId != BlasterMachineGunPackage.WeaponId)
            {
                return;
            }

            pendingAttackIntent = intent;
            lastAcceptedAttackIntent = intent;
            acceptedAttackIntents.Enqueue(intent);
            lockedDirectionX = intent.CommittedDirection.X;
            lockedDirectionY = intent.CommittedDirection.Y;
            hasLockedDirection = true;
            firePhase = MobileBlasterDroidFirePhase.WindUp;
            firePhaseElapsedSeconds = 0d;
        }

        private bool FireAcceptedBlaster(EnemyAttackIntent intent)
        {
            if (intent == null
                || blasterPipeline == null
                || weaponMount == null
                || !hasLockedDirection)
            {
                return false;
            }

            if (fireAttemptCount < long.MaxValue)
            {
                fireAttemptCount++;
            }

            try
            {
                StableId eventId = StableId.Create(
                    "event",
                    "en006-g" + generation + "-f" + fireAttemptCount);
                WeaponBehaviorInput input = new WeaponBehaviorInput(
                    eventId,
                    BlasterMachineGunPackage.WeaponId,
                    MountIdValue,
                    simulationStep,
                    BlasterMachineGunPackage.GetNormalRuntimeProfile(),
                    false,
                    intent.CommittedOrigin.X,
                    intent.CommittedOrigin.Y,
                    intent.CommittedDirection.X,
                    intent.CommittedDirection.Y,
                    1d);
                lastExecutionPlan = blasterPipeline.BuildExecutionPlan(input);
                lastExecutionResult = weaponMount.ExecutePlan(lastExecutionPlan);
                if (lastExecutionResult != null && lastExecutionResult.Succeeded)
                {
                    if (successfulShotCount < long.MaxValue)
                    {
                        successfulShotCount++;
                    }

                    return true;
                }
            }
            catch (ArgumentException)
            {
                lastExecutionPlan = null;
                lastExecutionResult = null;
            }
            catch (InvalidOperationException)
            {
                lastExecutionPlan = null;
                lastExecutionResult = null;
            }

            return false;
        }

        private void CancelPendingFire()
        {
            firePhase = MobileBlasterDroidFirePhase.Ready;
            firePhaseElapsedSeconds = 0d;
            hasLockedDirection = false;
            pendingAttackIntent = null;
        }

        private void ResetSessionState(bool resetProjectiles)
        {
            decisionSequence = 0L;
            simulationStep = 0L;
            fireAttemptCount = 0L;
            successfulShotCount = 0L;
            presentationElapsedSeconds = 0d;
            lastExecutionPlan = null;
            lastExecutionResult = null;
            lastDecisionEvaluation = null;
            lastPerceptionSnapshot = null;
            pendingAttackIntent = null;
            lastAcceptedAttackIntent = null;
            lastDestroyedNotification = null;
            acceptedAttackIntents.Clear();
            lockedDirectionX = 1d;
            lockedDirectionY = 0d;
            CancelPendingFire();
            if (resetProjectiles && projectileExecutor != null)
            {
                projectileExecutor.ResetSession();
            }
        }

        private void UpdatePresentation()
        {
            if (temporaryPresentation == null) return;

            bool destroyed = currentState != null && currentState.IsDestroyed;
            temporaryPresentation.UpdateState(
                firePhase,
                new Vector2((float)lockedDirectionX, (float)lockedDirectionY),
                IsActive,
                destroyed,
                presentationElapsedSeconds);
        }

        private void EnsurePackageComponents()
        {
            enemyBody = enemyBody == null ? GetComponent<Rigidbody2D>() : enemyBody;
            if (enemyBody == null)
            {
                enemyBody = gameObject.AddComponent<Rigidbody2D>();
            }

            enemyCollider = enemyCollider == null
                ? GetComponent<CircleCollider2D>()
                : enemyCollider;
            if (enemyCollider == null)
            {
                enemyCollider = gameObject.AddComponent<CircleCollider2D>();
            }

            enemyTarget = GetComponent<EnemyTarget2DAdapter>();
            if (enemyTarget == null)
            {
                enemyTarget = gameObject.AddComponent<EnemyTarget2DAdapter>();
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

            weaponMount = GetComponent<WeaponMount2DAdapter>();
            if (weaponMount == null)
            {
                weaponMount = gameObject.AddComponent<WeaponMount2DAdapter>();
            }

            temporaryPresentation = temporaryPresentation == null
                ? GetComponent<MobileBlasterDroidTemporaryPresentation>()
                : temporaryPresentation;
            if (temporaryPresentation == null)
            {
                temporaryPresentation =
                    gameObject.AddComponent<MobileBlasterDroidTemporaryPresentation>();
            }

            if (muzzle == null)
            {
                Transform existing = transform.Find("Blaster Muzzle");
                if (existing != null)
                {
                    muzzle = existing;
                }
                else
                {
                    GameObject muzzleObject = new GameObject("Blaster Muzzle");
                    muzzle = muzzleObject.transform;
                    muzzle.SetParent(transform, false);
                }
            }
        }
    }
}

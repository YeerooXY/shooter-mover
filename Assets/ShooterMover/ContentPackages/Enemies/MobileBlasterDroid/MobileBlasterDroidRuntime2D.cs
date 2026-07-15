using System;
using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
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
    /// lifecycle truth, EN-003 remains movement/contact projection, CB-009 remains plan
    /// execution authority, and WP-002/WP-003 remain projectile and Blaster authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileBlasterDroidRuntime2D :
        MonoBehaviour,
        IEnemyActor2DAuthority,
        IEnemyActor2DDecisionSource
    {
        private const double DirectionEpsilonSquared = 0.000000000001d;
        private static readonly StableId MountIdValue =
            StableId.Parse("weapon-mount.mobile-blaster-droid");

        [SerializeField] private MobileBlasterDroidDefinition definition;
        [SerializeField] private BoundedProjectile2D acceptedProjectilePrefab;
        [SerializeField] private Rigidbody2D enemyBody;
        [SerializeField] private CircleCollider2D enemyCollider;
        [SerializeField] private MobileBlasterDroidTemporaryPresentation temporaryPresentation;

        private EnemyTarget2DAdapter enemyTarget;
        private EnemyContact2DAdapter contactAdapter;
        private EnemyActor2DAdapter actorAdapter;
        private WeaponMount2DAdapter weaponMount;
        private Transform muzzle;
        private EnemyTarget2DAdapter playerTarget;
        private CombatHit2DAdapter hitAdapter;
        private ProjectileExecutionPlanAdapter projectileExecutor;
        private WeaponBehaviorPipeline blasterPipeline;
        private StableId actorId;
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

        public MobileBlasterDroidDefinition Definition
        {
            get { return definition; }
        }

        public EnemyActorState CurrentState
        {
            get { return currentState; }
        }

        public MobileBlasterDroidFirePhase FirePhase
        {
            get { return firePhase; }
        }

        public double FirePhaseElapsedSeconds
        {
            get { return firePhaseElapsedSeconds; }
        }

        public long FireAttemptCount
        {
            get { return fireAttemptCount; }
        }

        public long SuccessfulShotCount
        {
            get { return successfulShotCount; }
        }

        public long DecisionSequence
        {
            get { return decisionSequence; }
        }

        public long Generation
        {
            get { return generation; }
        }

        public Vector2 LockedDirection
        {
            get { return new Vector2((float)lockedDirectionX, (float)lockedDirectionY); }
        }

        public bool HasLockedDirection
        {
            get { return hasLockedDirection; }
        }

        public int ActiveProjectileCount
        {
            get { return projectileExecutor == null ? 0 : projectileExecutor.ActiveProjectileCount; }
        }

        public BoundedProjectile2D LastSpawnedProjectile
        {
            get { return projectileExecutor == null ? null : projectileExecutor.LastSpawnedProjectile; }
        }

        public WeaponFireExecutionPlan LastExecutionPlan
        {
            get { return lastExecutionPlan; }
        }

        public WeaponMount2DExecutionResult LastExecutionResult
        {
            get { return lastExecutionResult; }
        }

        public EnemyActor2DAdapter ActorAdapter
        {
            get { return actorAdapter; }
        }

        public EnemyTarget2DAdapter EnemyTarget
        {
            get { return enemyTarget; }
        }

        public EnemyContact2DAdapter ContactAdapter
        {
            get { return contactAdapter; }
        }

        public WeaponMount2DAdapter WeaponMount
        {
            get { return weaponMount; }
        }

        public Rigidbody2D EnemyBody
        {
            get { return enemyBody; }
        }

        public Collider2D EnemyCollider
        {
            get { return enemyCollider; }
        }

        public MobileBlasterDroidTemporaryPresentation Presentation
        {
            get { return temporaryPresentation; }
        }

        public void ConfigureSession(
            MobileBlasterDroidDefinition packageDefinition,
            StableId stableActorId,
            EnemyTarget2DAdapter observedPlayerTarget,
            Collider2D[] playerColliders,
            StableId playerId,
            CombatWeightClass playerWeight,
            BoundedProjectile2D projectilePrefab)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Mobile Blaster Droid session composition is explicit and may only be configured once.");
            }

            if (packageDefinition == null)
            {
                throw new ArgumentNullException(nameof(packageDefinition));
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

            if (projectilePrefab == null)
            {
                throw new ArgumentNullException(nameof(projectilePrefab));
            }

            EnsurePackageComponents();
            packageDefinition.ValidateOrThrow();
            if (playerColliders.Length > packageDefinition.ContactCapacity)
            {
                throw new ArgumentException(
                    "Player collider count exceeds the package contact capacity.",
                    nameof(playerColliders));
            }

            definition = packageDefinition;
            acceptedProjectilePrefab = projectilePrefab;
            actorId = stableActorId;
            playerTarget = observedPlayerTarget;
            currentState = definition.CreateInitialState(actorId);
            ResetCountersAndFireState(false);

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

        public EnemyActor2DFixedStepResult ExecuteFixedStep(double deltaTimeSeconds)
        {
            if (!configured)
            {
                throw new InvalidOperationException(
                    "Mobile Blaster Droid must be configured before fixed-step execution.");
            }

            if (double.IsNaN(deltaTimeSeconds)
                || double.IsInfinity(deltaTimeSeconds)
                || deltaTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));
            }

            EnemyActor2DFixedStepResult movementResult =
                actorAdapter.ExecuteFixedStep(deltaTimeSeconds);
            presentationElapsedSeconds += deltaTimeSeconds;

            EnemyActorState state;
            EnemyTarget2DObservation target;
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
                AdvanceFireState(target, deltaTimeSeconds);
            }

            if (simulationStep < long.MaxValue)
            {
                simulationStep++;
            }

            UpdatePresentation();
            return movementResult;
        }

        public bool ActivateSession()
        {
            if (!configured)
            {
                return false;
            }

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
            if (!configured || actorAdapter == null)
            {
                return false;
            }

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

            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            bool wasActive = currentState.IsActive;
            EnemyActorStepResult result = EnemyActorStepper.Step(
                currentState,
                new[] { command });
            currentState = result.State;
            if (wasActive && !currentState.IsActive)
            {
                CancelPendingFire();
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

            double offsetX = target.PositionX - transform.position.x;
            double offsetY = target.PositionY - transform.position.y;
            double distanceSquared = (offsetX * offsetX) + (offsetY * offsetY);
            double velocityX = 0d;
            double velocityY = 0d;
            if (distanceSquared > DirectionEpsilonSquared)
            {
                double distance = Math.Sqrt(distanceSquared);
                double outerDistance = definition.PreferredDistance
                    + definition.PositioningTolerance;
                double innerDistance = Math.Max(
                    0d,
                    definition.PreferredDistance - definition.PositioningTolerance);
                double sign = 0d;
                if (distance > outerDistance)
                {
                    sign = 1d;
                }
                else if (distance < innerDistance)
                {
                    sign = -1d;
                }

                if (sign != 0d)
                {
                    double scale = sign * definition.MovementSpeed / distance;
                    velocityX = offsetX * scale;
                    velocityY = offsetY * scale;
                }
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
            ResetCountersAndFireState(true);
            return true;
        }

        void IEnemyActor2DDecisionSource.Reset()
        {
            decisionSequence = 0L;
            CancelPendingFire();
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

        private void OnDisable()
        {
            if (actorAdapter != null)
            {
                actorAdapter.Deactivate();
            }

            CancelPendingFire();
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
            hitAdapter = null;
            projectileExecutor = null;
            blasterPipeline = null;
        }

        private void Update()
        {
            if (!configured)
            {
                return;
            }

            presentationElapsedSeconds += Time.deltaTime;
            UpdatePresentation();
        }

        private void AdvanceFireState(
            EnemyTarget2DObservation target,
            double deltaTimeSeconds)
        {
            switch (firePhase)
            {
                case MobileBlasterDroidFirePhase.Ready:
                    BeginWindUp(target);
                    break;

                case MobileBlasterDroidFirePhase.WindUp:
                    firePhaseElapsedSeconds += deltaTimeSeconds;
                    if (firePhaseElapsedSeconds >= definition.WindUpSeconds)
                    {
                        FireAcceptedBlaster();
                        firePhase = MobileBlasterDroidFirePhase.Recovery;
                        firePhaseElapsedSeconds = 0d;
                        hasLockedDirection = false;
                    }
                    break;

                case MobileBlasterDroidFirePhase.Recovery:
                    firePhaseElapsedSeconds += deltaTimeSeconds;
                    if (firePhaseElapsedSeconds >= definition.RecoverySeconds)
                    {
                        firePhase = MobileBlasterDroidFirePhase.Ready;
                        firePhaseElapsedSeconds = 0d;
                    }
                    break;

                default:
                    CancelPendingFire();
                    break;
            }
        }

        private void BeginWindUp(EnemyTarget2DObservation target)
        {
            Vector3 origin = muzzle.position;
            double offsetX = target.PositionX - origin.x;
            double offsetY = target.PositionY - origin.y;
            double magnitudeSquared = (offsetX * offsetX) + (offsetY * offsetY);
            if (magnitudeSquared <= DirectionEpsilonSquared)
            {
                lockedDirectionX = 1d;
                lockedDirectionY = 0d;
            }
            else
            {
                double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
                lockedDirectionX = offsetX * inverseMagnitude;
                lockedDirectionY = offsetY * inverseMagnitude;
            }

            hasLockedDirection = true;
            firePhase = MobileBlasterDroidFirePhase.WindUp;
            firePhaseElapsedSeconds = 0d;
        }

        private bool FireAcceptedBlaster()
        {
            if (blasterPipeline == null
                || weaponMount == null
                || !hasLockedDirection
                || muzzle == null)
            {
                return false;
            }

            if (fireAttemptCount < long.MaxValue)
            {
                fireAttemptCount++;
            }

            try
            {
                StableId combatEventId = StableId.Create(
                    "event",
                    "en006-g" + generation + "-f" + fireAttemptCount);
                Vector3 origin = muzzle.position;
                WeaponBehaviorInput input = new WeaponBehaviorInput(
                    combatEventId,
                    BlasterMachineGunPackage.WeaponId,
                    MountIdValue,
                    simulationStep,
                    BlasterMachineGunPackage.GetNormalRuntimeProfile(),
                    false,
                    origin.x,
                    origin.y,
                    lockedDirectionX,
                    lockedDirectionY,
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
        }

        private void ResetCountersAndFireState(bool resetProjectiles)
        {
            decisionSequence = 0L;
            simulationStep = 0L;
            fireAttemptCount = 0L;
            successfulShotCount = 0L;
            presentationElapsedSeconds = 0d;
            lastExecutionPlan = null;
            lastExecutionResult = null;
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
            if (temporaryPresentation == null)
            {
                return;
            }

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

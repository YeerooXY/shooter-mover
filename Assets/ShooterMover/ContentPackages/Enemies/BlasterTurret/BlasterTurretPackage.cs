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

namespace ShooterMover.ContentPackages.Enemies.BlasterTurret
{
    public enum BlasterTurretCadencePhase
    {
        Idle = 1,
        Warning = 2,
        Recovery = 3,
    }

    public sealed class BlasterTurretCadenceResult
    {
        internal BlasterTurretCadenceResult(
            BlasterTurretCadencePhase phase,
            bool warningVisible,
            bool shouldFire,
            long shotSequence)
        {
            Phase = phase;
            WarningVisible = warningVisible;
            ShouldFire = shouldFire;
            ShotSequence = shotSequence;
        }

        public BlasterTurretCadencePhase Phase { get; }

        public bool WarningVisible { get; }

        public bool ShouldFire { get; }

        public long ShotSequence { get; }
    }

    /// <summary>
    /// Plain-C# deterministic warn, fire, recover cadence. Entering Warning always
    /// consumes one complete decision step before a shot can be emitted.
    /// </summary>
    public sealed class BlasterTurretCadence
    {
        private readonly decimal warningSeconds;
        private readonly decimal recoverySeconds;
        private BlasterTurretCadencePhase phase;
        private decimal phaseElapsedSeconds;
        private long nextShotSequence;

        public BlasterTurretCadence(double warningSeconds, double recoverySeconds)
        {
            RequireFinitePositive(warningSeconds, nameof(warningSeconds));
            RequireFinitePositive(recoverySeconds, nameof(recoverySeconds));
            this.warningSeconds = (decimal)warningSeconds;
            this.recoverySeconds = (decimal)recoverySeconds;
            Reset();
        }

        public BlasterTurretCadencePhase Phase
        {
            get { return phase; }
        }

        public double PhaseElapsedSeconds
        {
            get { return (double)phaseElapsedSeconds; }
        }

        public long NextShotSequence
        {
            get { return nextShotSequence; }
        }

        public BlasterTurretCadenceResult Step(double deltaTimeSeconds, bool canAttack)
        {
            RequireFiniteNonNegative(deltaTimeSeconds, nameof(deltaTimeSeconds));
            if (!canAttack)
            {
                CancelPendingShot();
                return Result(false, false, -1L);
            }

            decimal elapsed = (decimal)deltaTimeSeconds;
            if (phase == BlasterTurretCadencePhase.Idle)
            {
                phase = BlasterTurretCadencePhase.Warning;
                phaseElapsedSeconds = 0m;
                return Result(true, false, -1L);
            }

            if (phase == BlasterTurretCadencePhase.Warning)
            {
                phaseElapsedSeconds += elapsed;
                if (phaseElapsedSeconds < warningSeconds)
                {
                    return Result(true, false, -1L);
                }

                long emittedSequence = nextShotSequence;
                if (nextShotSequence < long.MaxValue)
                {
                    nextShotSequence++;
                }

                phase = BlasterTurretCadencePhase.Recovery;
                phaseElapsedSeconds = 0m;
                return Result(false, true, emittedSequence);
            }

            phaseElapsedSeconds += elapsed;
            if (phaseElapsedSeconds >= recoverySeconds)
            {
                phase = BlasterTurretCadencePhase.Warning;
                phaseElapsedSeconds = 0m;
                return Result(true, false, -1L);
            }

            return Result(false, false, -1L);
        }

        public void CancelPendingShot()
        {
            phase = BlasterTurretCadencePhase.Idle;
            phaseElapsedSeconds = 0m;
        }

        public void Reset()
        {
            phase = BlasterTurretCadencePhase.Idle;
            phaseElapsedSeconds = 0m;
            nextShotSequence = 0L;
        }

        private BlasterTurretCadenceResult Result(
            bool warningVisible,
            bool shouldFire,
            long shotSequence)
        {
            return new BlasterTurretCadenceResult(
                phase,
                warningVisible,
                shouldFire,
                shotSequence);
        }

        private static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    /// <summary>
    /// Package-owned authority that delegates every health/contact/lifecycle transition to EN-002.
    /// </summary>
    public sealed class BlasterTurretAuthority : IEnemyActor2DAuthority
    {
        private readonly EnemyActorState initialState;
        private EnemyActorState currentState;

        internal BlasterTurretAuthority(EnemyActorState state)
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

    /// <summary>
    /// EN-003 decision source for a fixed-position role. It always projects zero velocity.
    /// </summary>
    public sealed class BlasterTurretStationaryDecisionSource : IEnemyActor2DDecisionSource
    {
        private readonly StableId actorId;
        private long sequence;

        public BlasterTurretStationaryDecisionSource(StableId actorId)
        {
            this.actorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
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

            decision = new EnemyActor2DDecision(
                sequence,
                actorId,
                target.TargetId,
                0d,
                0d);
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

    public interface IBlasterTurretLineOfFireSource
    {
        bool HasClearLine(
            Vector2 origin,
            Vector2 targetPosition,
            Collider2D targetCollider,
            Collider2D ownerCollider);
    }

    /// <summary>
    /// Package-local 2D obstruction query. It ignores the turret's own collider and
    /// requires the explicitly supplied target collider to be the first relevant hit.
    /// </summary>
    public sealed class BlasterTurretPhysicsLineOfFireSource :
        IBlasterTurretLineOfFireSource
    {
        private readonly int layerMask;

        public BlasterTurretPhysicsLineOfFireSource()
            : this(Physics2D.DefaultRaycastLayers)
        {
        }

        public BlasterTurretPhysicsLineOfFireSource(int layerMask)
        {
            this.layerMask = layerMask;
        }

        public bool HasClearLine(
            Vector2 origin,
            Vector2 targetPosition,
            Collider2D targetCollider,
            Collider2D ownerCollider)
        {
            if (!IsFinite(origin)
                || !IsFinite(targetPosition)
                || targetCollider == null
                || !targetCollider.enabled
                || !targetCollider.gameObject.activeInHierarchy
                || (targetPosition - origin).sqrMagnitude <= 0.0000001f)
            {
                return false;
            }

            RaycastHit2D[] hits = Physics2D.LinecastAll(origin, targetPosition, layerMask);
            Array.Sort(hits, CompareHits);
            for (int index = 0; index < hits.Length; index++)
            {
                Collider2D collider = hits[index].collider;
                if (collider == null || collider == ownerCollider)
                {
                    continue;
                }

                if (collider == targetCollider)
                {
                    return true;
                }

                if (!collider.isTrigger)
                {
                    return false;
                }
            }

            return false;
        }

        private static int CompareHits(RaycastHit2D left, RaycastHit2D right)
        {
            int fractionComparison = left.fraction.CompareTo(right.fraction);
            if (fractionComparison != 0)
            {
                return fractionComparison;
            }

            int leftId = left.collider == null ? int.MinValue : left.collider.GetInstanceID();
            int rightId = right.collider == null ? int.MinValue : right.collider.GetInstanceID();
            return leftId.CompareTo(rightId);
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }
    }

    public enum BlasterTurretStepStatus
    {
        Warning = 1,
        Recovery = 2,
        ShotExecuted = 3,
        ShotExecutionFailed = 4,
        NotConfigured = 5,
        Inactive = 6,
        ActorDestroyed = 7,
        TargetUnavailable = 8,
        TargetOutOfRange = 9,
        Obstructed = 10,
        PointBlankTarget = 11,
    }

    public sealed class BlasterTurretStepResult
    {
        internal BlasterTurretStepResult(
            BlasterTurretStepStatus status,
            long fixedStep,
            bool warningVisible,
            long shotSequence,
            WeaponFireExecutionPlan plan,
            WeaponMount2DExecutionResult execution)
        {
            Status = status;
            FixedStep = fixedStep;
            WarningVisible = warningVisible;
            ShotSequence = shotSequence;
            Plan = plan;
            Execution = execution;
        }

        public BlasterTurretStepStatus Status { get; }

        public long FixedStep { get; }

        public bool WarningVisible { get; }

        public long ShotSequence { get; }

        public WeaponFireExecutionPlan Plan { get; }

        public WeaponMount2DExecutionResult Execution { get; }

        public bool ShotExecuted
        {
            get { return Status == BlasterTurretStepStatus.ShotExecuted; }
        }
    }

    /// <summary>
    /// Independently owned Blaster Turret composition root. It keeps position fixed,
    /// reads one explicit target, warns with a shape-patterned line, builds the accepted
    /// WP-003 Blaster plan, and executes it through CB-009/WP-002.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlasterTurretPackage : MonoBehaviour
    {
        public const double StationaryAdapterSpeedBoundary = 1d;

        private static readonly StableId MountIdValue =
            StableId.Parse("weapon-mount.blaster-turret-primary");

        [SerializeField] private BlasterTurretDefinition definition;

        private Rigidbody2D enemyBody;
        private BoxCollider2D enemyCollider;
        private EnemyTarget2DAdapter enemyTargetAdapter;
        private EnemyContact2DAdapter contactAdapter;
        private EnemyActor2DAdapter actorAdapter;
        private WeaponMount2DAdapter weaponMountAdapter;
        private BlasterTurretPresentation2D presentation;
        private BlasterTurretAuthority authority;
        private BlasterTurretStationaryDecisionSource stationaryDecisionSource;
        private IEnemyTarget2DSource fireTargetSource;
        private Collider2D fireTargetCollider;
        private StableId fireTargetId;
        private CombatHit2DAdapter hitAdapter;
        private ProjectileExecutionPlanAdapter projectileAdapter;
        private WeaponBehaviorPipeline behaviorPipeline;
        private IBlasterTurretLineOfFireSource lineOfFireSource;
        private BlasterTurretCadence cadence;
        private Vector2 anchorPosition;
        private long fixedStepCount;
        private long generation;
        private bool configured;
        private bool activeRequested;
        private bool lastAttackAvailable;

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

        public BlasterTurretDefinition Definition
        {
            get { return definition; }
        }

        public BlasterTurretAuthority Authority
        {
            get { return authority; }
        }

        public BlasterTurretStationaryDecisionSource StationaryDecisionSource
        {
            get { return stationaryDecisionSource; }
        }

        public BlasterTurretCadence Cadence
        {
            get { return cadence; }
        }

        public ProjectileExecutionPlanAdapter ProjectileAdapter
        {
            get { return projectileAdapter; }
        }

        public WeaponMount2DAdapter WeaponMountAdapter
        {
            get { return weaponMountAdapter; }
        }

        public EnemyActor2DAdapter ActorAdapter
        {
            get { return actorAdapter; }
        }

        public EnemyTarget2DAdapter TargetAdapter
        {
            get { return enemyTargetAdapter; }
        }

        public EnemyContact2DAdapter ContactAdapter
        {
            get { return contactAdapter; }
        }

        public BlasterTurretPresentation2D Presentation
        {
            get { return presentation; }
        }

        public Rigidbody2D EnemyBody
        {
            get { return enemyBody; }
        }

        public Collider2D EnemyCollider
        {
            get { return enemyCollider; }
        }

        public long Generation
        {
            get { return generation; }
        }

        public long FixedStepCount
        {
            get { return fixedStepCount; }
        }

        public Vector2 AnchorPosition
        {
            get { return anchorPosition; }
        }

        public void Configure(
            BlasterTurretDefinition packageDefinition,
            IEnemyTarget2DSource targetSource,
            Collider2D targetCollider,
            BoundedProjectile2D projectilePrefab,
            StableId actorId,
            StableId targetId,
            CombatWeightClass targetWeight,
            IBlasterTurretLineOfFireSource obstructionSource = null)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Blaster Turret package is already configured.");
            }

            if (packageDefinition == null)
            {
                throw new ArgumentNullException(nameof(packageDefinition));
            }

            if (targetSource == null)
            {
                throw new ArgumentNullException(nameof(targetSource));
            }

            if (targetCollider == null)
            {
                throw new ArgumentNullException(nameof(targetCollider));
            }

            if (projectilePrefab == null)
            {
                throw new ArgumentNullException(nameof(projectilePrefab));
            }

            if (actorId == null)
            {
                throw new ArgumentNullException(nameof(actorId));
            }

            if (targetId == null)
            {
                throw new ArgumentNullException(nameof(targetId));
            }

            if (!Enum.IsDefined(typeof(CombatWeightClass), targetWeight))
            {
                throw new ArgumentOutOfRangeException(nameof(targetWeight));
            }

            packageDefinition.ValidateOrThrow();
            EnsurePackageComponents();

            definition = packageDefinition;
            fireTargetSource = targetSource;
            fireTargetCollider = targetCollider;
            fireTargetId = targetId;
            lineOfFireSource = obstructionSource ?? new BlasterTurretPhysicsLineOfFireSource();
            authority = new BlasterTurretAuthority(definition.CreateInitialState(actorId));
            stationaryDecisionSource = new BlasterTurretStationaryDecisionSource(actorId);
            cadence = new BlasterTurretCadence(
                definition.WarningSeconds,
                definition.RecoverySeconds);
            behaviorPipeline = new WeaponBehaviorPipeline(
                new[] { BlasterMachineGunPackage.CreateBehaviorModule() });

            enemyTargetAdapter.Configure(actorId, transform, enemyCollider, authority);
            contactAdapter.Configure(
                enemyTargetAdapter,
                authority,
                targetId,
                targetWeight,
                definition.MoverColliderCapacity);
            EnemyContact2DRegistrationStatus contactRegistration =
                contactAdapter.RegisterMoverCollider(targetCollider, targetId, targetWeight);
            if (contactRegistration != EnemyContact2DRegistrationStatus.Registered
                && contactRegistration != EnemyContact2DRegistrationStatus.AlreadyRegistered)
            {
                throw new InvalidOperationException(
                    "The explicit target collider could not be registered for contact: "
                    + contactRegistration);
            }

            actorAdapter.Configure(
                enemyBody,
                authority,
                stationaryDecisionSource,
                fireTargetSource,
                contactAdapter,
                StationaryAdapterSpeedBoundary);

            hitAdapter = new CombatHit2DAdapter(actorId);
            CombatHit2DTargetRegistrationStatus hitRegistration =
                hitAdapter.RegisterTarget(targetCollider, targetId);
            if (hitRegistration != CombatHit2DTargetRegistrationStatus.Registered
                && hitRegistration != CombatHit2DTargetRegistrationStatus.AlreadyRegistered)
            {
                throw new InvalidOperationException(
                    "The explicit target collider could not be registered for Blaster hits: "
                    + hitRegistration);
            }

            projectileAdapter = new ProjectileExecutionPlanAdapter(
                BlasterMachineGunPackage.OperationKindId,
                projectilePrefab,
                hitAdapter,
                new Collider2D[] { enemyCollider },
                null,
                false,
                0.05f);
            weaponMountAdapter.Configure(
                actorId,
                BlasterMachineGunPackage.WeaponId,
                MountIdValue,
                new IWeaponFireExecutionOperation2DHandler[] { projectileAdapter });
            presentation.Configure(definition.WarningLineWidth);

            anchorPosition = enemyBody.position;
            fixedStepCount = 0L;
            generation = 0L;
            configured = true;
            activeRequested = true;
            lastAttackAvailable = false;
            RestoreStationaryAnchor();
            if (isActiveAndEnabled)
            {
                actorAdapter.Activate();
            }
        }

        public bool Activate()
        {
            if (!configured)
            {
                throw new InvalidOperationException(
                    "Blaster Turret package must be configured before activation.");
            }

            bool changed = !activeRequested;
            activeRequested = true;
            if (isActiveAndEnabled && actorAdapter != null && !actorAdapter.IsActive)
            {
                changed = actorAdapter.Activate() || changed;
            }

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

            CancelAttackState(true);
            return changed;
        }

        public BlasterTurretStepResult ExecuteFixedStep(double deltaTimeSeconds)
        {
            if (double.IsNaN(deltaTimeSeconds)
                || double.IsInfinity(deltaTimeSeconds)
                || deltaTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));
            }

            long step = fixedStepCount;
            if (!configured)
            {
                return Result(BlasterTurretStepStatus.NotConfigured, step, false, -1L, null, null);
            }

            RestoreStationaryAnchor();
            if (!IsActive)
            {
                CancelAttackState(true);
                AdvanceFixedStep();
                return Result(BlasterTurretStepStatus.Inactive, step, false, -1L, null, null);
            }

            actorAdapter.ExecuteFixedStep(deltaTimeSeconds);
            RestoreStationaryAnchor();

            EnemyActorState state;
            if (!authority.TryReadState(out state) || state == null || !state.IsActive)
            {
                presentation.SetDestroyed(state != null && state.IsDestroyed);
                CancelAttackState(true);
                AdvanceFixedStep();
                return Result(
                    BlasterTurretStepStatus.ActorDestroyed,
                    step,
                    false,
                    -1L,
                    null,
                    null);
            }

            presentation.SetDestroyed(false);
            EnemyTarget2DObservation target;
            if (!TryReadTarget(out target))
            {
                CancelAttackState(true);
                AdvanceFixedStep();
                return Result(
                    BlasterTurretStepStatus.TargetUnavailable,
                    step,
                    false,
                    -1L,
                    null,
                    null);
            }

            Vector2 targetPosition = new Vector2(
                (float)target.PositionX,
                (float)target.PositionY);
            Vector2 delta = targetPosition - anchorPosition;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
            {
                CancelAttackState(true);
                AdvanceFixedStep();
                return Result(
                    BlasterTurretStepStatus.PointBlankTarget,
                    step,
                    false,
                    -1L,
                    null,
                    null);
            }

            if (distance > definition.MaximumRange)
            {
                CancelAttackState(true);
                AdvanceFixedStep();
                return Result(
                    BlasterTurretStepStatus.TargetOutOfRange,
                    step,
                    false,
                    -1L,
                    null,
                    null);
            }

            Vector2 direction = delta / distance;
            Vector2 muzzle = anchorPosition + (direction * (float)definition.MuzzleOffset);
            bool clear = lineOfFireSource.HasClearLine(
                muzzle,
                targetPosition,
                fireTargetCollider,
                enemyCollider);
            if (!clear)
            {
                CancelAttackState(true);
                AdvanceFixedStep();
                return Result(
                    BlasterTurretStepStatus.Obstructed,
                    step,
                    false,
                    -1L,
                    null,
                    null);
            }

            lastAttackAvailable = true;
            BlasterTurretCadenceResult cadenceResult = cadence.Step(deltaTimeSeconds, true);
            if (cadenceResult.WarningVisible)
            {
                presentation.ShowWarning(muzzle, targetPosition);
            }
            else
            {
                presentation.HideWarning();
            }

            if (!cadenceResult.ShouldFire)
            {
                AdvanceFixedStep();
                return Result(
                    cadenceResult.WarningVisible
                        ? BlasterTurretStepStatus.Warning
                        : BlasterTurretStepStatus.Recovery,
                    step,
                    cadenceResult.WarningVisible,
                    -1L,
                    null,
                    null);
            }

            WeaponFireExecutionPlan plan = BuildBlasterPlan(
                step,
                cadenceResult.ShotSequence,
                muzzle,
                direction);
            WeaponMount2DExecutionResult execution = weaponMountAdapter.ExecutePlan(plan);
            AdvanceFixedStep();
            return Result(
                execution != null && execution.Succeeded
                    ? BlasterTurretStepStatus.ShotExecuted
                    : BlasterTurretStepStatus.ShotExecutionFailed,
                step,
                false,
                cadenceResult.ShotSequence,
                plan,
                execution);
        }

        public bool RestartSession()
        {
            if (!configured || actorAdapter == null)
            {
                return false;
            }

            CancelAttackState(true);
            hitAdapter.ResetProcessedEvents();
            cadence.Reset();
            bool reset = actorAdapter.Restart();
            fixedStepCount = 0L;
            generation = generation == long.MaxValue ? long.MaxValue : generation + 1L;
            RestoreStationaryAnchor();
            presentation.SetDestroyed(false);
            presentation.HideWarning();
            if (!reset)
            {
                return false;
            }

            if (activeRequested && isActiveAndEnabled && !actorAdapter.IsActive)
            {
                actorAdapter.Activate();
            }

            return true;
        }

        private void Awake()
        {
            EnsurePackageComponents();
        }

        private void FixedUpdate()
        {
            if (configured)
            {
                ExecuteFixedStep(Time.fixedDeltaTime);
            }
        }

        private void LateUpdate()
        {
            SynchronizeLifecyclePresentation();
            RestoreStationaryAnchor();
        }

        private void OnEnable()
        {
            if (configured && activeRequested && actorAdapter != null && !actorAdapter.IsActive)
            {
                actorAdapter.Activate();
            }
        }

        private void OnDisable()
        {
            if (actorAdapter != null)
            {
                actorAdapter.Deactivate();
            }

            CancelAttackState(true);
        }

        private void OnDestroy()
        {
            if (projectileAdapter != null)
            {
                projectileAdapter.Dispose();
                projectileAdapter = null;
            }

            if (weaponMountAdapter != null)
            {
                weaponMountAdapter.ClearConfiguration();
            }

            configured = false;
            activeRequested = false;
            authority = null;
            stationaryDecisionSource = null;
            fireTargetSource = null;
            fireTargetCollider = null;
            fireTargetId = null;
            hitAdapter = null;
            behaviorPipeline = null;
            lineOfFireSource = null;
            cadence = null;
        }

        private bool TryReadTarget(out EnemyTarget2DObservation target)
        {
            target = null;
            try
            {
                return fireTargetSource != null
                    && fireTargetSource.TryReadTarget(out target)
                    && target != null
                    && target.TargetId == fireTargetId;
            }
            catch (MissingReferenceException)
            {
                target = null;
                return false;
            }
            catch (InvalidOperationException)
            {
                target = null;
                return false;
            }
            catch (ArgumentException)
            {
                target = null;
                return false;
            }
        }

        private WeaponFireExecutionPlan BuildBlasterPlan(
            long simulationStep,
            long shotSequence,
            Vector2 origin,
            Vector2 direction)
        {
            WeaponBehaviorInput input = new WeaponBehaviorInput(
                CreateCombatEventId(shotSequence),
                BlasterMachineGunPackage.WeaponId,
                MountIdValue,
                simulationStep,
                BlasterMachineGunPackage.GetNormalRuntimeProfile(),
                false,
                origin.x,
                origin.y,
                direction.x,
                direction.y,
                1d);
            return behaviorPipeline.BuildExecutionPlan(input);
        }

        private StableId CreateCombatEventId(long shotSequence)
        {
            uint actorHash = unchecked((uint)authority.CurrentState.ActorId.GetHashCode());
            string value = "turret-"
                + actorHash.ToString("x8")
                + "-g"
                + generation
                + "-s"
                + shotSequence;
            return StableId.Create("combat-event", value);
        }

        private void CancelAttackState(bool cancelProjectiles)
        {
            lastAttackAvailable = false;
            if (cadence != null)
            {
                cadence.CancelPendingShot();
            }

            if (presentation != null)
            {
                presentation.HideWarning();
            }

            if (cancelProjectiles && projectileAdapter != null)
            {
                projectileAdapter.ResetSession();
            }
        }

        private void SynchronizeLifecyclePresentation()
        {
            if (!configured || presentation == null || authority == null)
            {
                return;
            }

            EnemyActorState state;
            bool destroyed = !authority.TryReadState(out state)
                || state == null
                || state.IsDestroyed;
            presentation.SetDestroyed(destroyed);
            if (destroyed)
            {
                CancelAttackState(true);
            }
        }

        private void RestoreStationaryAnchor()
        {
            if (!configured && enemyBody == null)
            {
                return;
            }

            if (enemyBody != null)
            {
                enemyBody.position = anchorPosition;
                enemyBody.angularVelocity = 0f;
                enemyBody.linearVelocity = Vector2.zero;
            }

            Vector3 current = transform.position;
            transform.position = new Vector3(anchorPosition.x, anchorPosition.y, current.z);
        }

        private void AdvanceFixedStep()
        {
            if (fixedStepCount < long.MaxValue)
            {
                fixedStepCount++;
            }
        }

        private static BlasterTurretStepResult Result(
            BlasterTurretStepStatus status,
            long step,
            bool warningVisible,
            long shotSequence,
            WeaponFireExecutionPlan plan,
            WeaponMount2DExecutionResult execution)
        {
            return new BlasterTurretStepResult(
                status,
                step,
                warningVisible,
                shotSequence,
                plan,
                execution);
        }

        private void EnsurePackageComponents()
        {
            enemyBody = GetComponent<Rigidbody2D>();
            if (enemyBody == null)
            {
                enemyBody = gameObject.AddComponent<Rigidbody2D>();
            }

            enemyBody.bodyType = RigidbodyType2D.Kinematic;
            enemyBody.gravityScale = 0f;
            enemyBody.constraints = RigidbodyConstraints2D.FreezeAll;
            enemyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            enemyCollider = GetComponent<BoxCollider2D>();
            if (enemyCollider == null)
            {
                enemyCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            enemyCollider.isTrigger = false;
            enemyCollider.size = new Vector2(1.3f, 1f);

            enemyTargetAdapter = GetComponent<EnemyTarget2DAdapter>();
            if (enemyTargetAdapter == null)
            {
                enemyTargetAdapter = gameObject.AddComponent<EnemyTarget2DAdapter>();
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

            weaponMountAdapter = GetComponent<WeaponMount2DAdapter>();
            if (weaponMountAdapter == null)
            {
                weaponMountAdapter = gameObject.AddComponent<WeaponMount2DAdapter>();
            }

            presentation = GetComponent<BlasterTurretPresentation2D>();
            if (presentation == null)
            {
                presentation = gameObject.AddComponent<BlasterTurretPresentation2D>();
            }
        }
    }
}

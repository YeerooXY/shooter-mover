using System;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Inward-facing owner of the current immutable EN-002 enemy state. Concrete
    /// packages may implement this port, but the Unity adapters never retain or
    /// manufacture authoritative health, contact, or lifecycle state.
    /// </summary>
    public interface IEnemyActor2DAuthority
    {
        bool TryReadState(out EnemyActorState state);

        EnemyActorStepResult Apply(EnemyActorCommand command);

        bool Reset();
    }

    /// <summary>
    /// Plain-C# decision source consumed once per explicit fixed step. Implementations
    /// may use package-authored rules, but must return an immutable bounded projection.
    /// </summary>
    public interface IEnemyActor2DDecisionSource
    {
        bool TryDecide(
            EnemyActorState state,
            EnemyTarget2DObservation target,
            double deltaTimeSeconds,
            out EnemyActor2DDecision decision);

        void Reset();
    }

    public sealed class EnemyActor2DDecision
    {
        public EnemyActor2DDecision(
            long sequence,
            ShooterMover.Domain.Common.StableId actorId,
            ShooterMover.Domain.Common.StableId targetId,
            double velocityX,
            double velocityY)
        {
            if (sequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (actorId == null)
            {
                throw new ArgumentNullException(nameof(actorId));
            }

            if (targetId == null)
            {
                throw new ArgumentNullException(nameof(targetId));
            }

            RequireFinite(velocityX, nameof(velocityX));
            RequireFinite(velocityY, nameof(velocityY));

            Sequence = sequence;
            ActorId = actorId;
            TargetId = targetId;
            VelocityX = velocityX;
            VelocityY = velocityY;
        }

        public long Sequence { get; }

        public ShooterMover.Domain.Common.StableId ActorId { get; }

        public ShooterMover.Domain.Common.StableId TargetId { get; }

        public double VelocityX { get; }

        public double VelocityY { get; }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Enemy movement decisions must be finite.");
            }
        }
    }

    public enum EnemyActor2DFixedStepStatus
    {
        Applied = 1,
        AppliedClamped = 2,
        NotConfigured = 3,
        AdapterInactive = 4,
        AuthorityUnavailable = 5,
        ActorInactive = 6,
        TargetUnavailable = 7,
        DecisionUnavailable = 8,
        InvalidDecision = 9,
        BodyUnavailable = 10,
    }

    public sealed class EnemyActor2DFixedStepResult
    {
        internal EnemyActor2DFixedStepResult(
            EnemyActor2DFixedStepStatus status,
            long fixedStep,
            EnemyActor2DDecision decision,
            double appliedVelocityX,
            double appliedVelocityY)
        {
            Status = status;
            FixedStep = fixedStep;
            Decision = decision;
            AppliedVelocityX = appliedVelocityX;
            AppliedVelocityY = appliedVelocityY;
        }

        public EnemyActor2DFixedStepStatus Status { get; }

        public long FixedStep { get; }

        public EnemyActor2DDecision Decision { get; }

        public double AppliedVelocityX { get; }

        public double AppliedVelocityY { get; }

        public bool Applied
        {
            get
            {
                return Status == EnemyActor2DFixedStepStatus.Applied
                    || Status == EnemyActor2DFixedStepStatus.AppliedClamped;
            }
        }
    }

    /// <summary>
    /// Fixed-step Unity 2D projection for one enemy. The configured decision source
    /// and EN-002 authority remain the only gameplay truth; this component merely
    /// bounds and projects their velocity to its own enemy Rigidbody2D.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyActor2DAdapter : MonoBehaviour
    {
        public const double HardMaximumSpeed = 10000d;

        private Rigidbody2D enemyBody;
        private MovementBody2DAdapter bodyAdapter;
        private IEnemyActor2DAuthority authority;
        private IEnemyActor2DDecisionSource decisionSource;
        private IEnemyTarget2DSource targetSource;
        private EnemyContact2DAdapter contactAdapter;
        private double maximumSpeed;
        private long fixedStepCount;
        private long generation;
        private bool activeRequested;

        public bool IsConfigured
        {
            get
            {
                return bodyAdapter != null
                    && authority != null
                    && decisionSource != null
                    && targetSource != null
                    && contactAdapter != null;
            }
        }

        public bool IsActive
        {
            get { return activeRequested && isActiveAndEnabled; }
        }

        public long FixedStepCount
        {
            get { return fixedStepCount; }
        }

        public long Generation
        {
            get { return generation; }
        }

        public double MaximumSpeed
        {
            get { return maximumSpeed; }
        }

        public void Configure(
            Rigidbody2D body,
            IEnemyActor2DAuthority actorAuthority,
            IEnemyActor2DDecisionSource actorDecisionSource,
            IEnemyTarget2DSource actorTargetSource,
            EnemyContact2DAdapter actorContactAdapter,
            double maximumMovementSpeed)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (actorAuthority == null)
            {
                throw new ArgumentNullException(nameof(actorAuthority));
            }

            if (actorDecisionSource == null)
            {
                throw new ArgumentNullException(nameof(actorDecisionSource));
            }

            if (actorTargetSource == null)
            {
                throw new ArgumentNullException(nameof(actorTargetSource));
            }

            if (actorContactAdapter == null)
            {
                throw new ArgumentNullException(nameof(actorContactAdapter));
            }

            if (double.IsNaN(maximumMovementSpeed)
                || double.IsInfinity(maximumMovementSpeed)
                || maximumMovementSpeed <= 0d
                || maximumMovementSpeed > HardMaximumSpeed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumMovementSpeed),
                    maximumMovementSpeed,
                    "Enemy movement speed must be finite, positive, and inside the hard 2D boundary.");
            }

            if (!object.ReferenceEquals(body.gameObject, gameObject)
                || !object.ReferenceEquals(actorContactAdapter.gameObject, gameObject))
            {
                throw new ArgumentException(
                    "Enemy body, actor adapter, and contact adapter must belong to one explicit GameObject.");
            }

            if (!actorContactAdapter.IsConfigured)
            {
                throw new InvalidOperationException(
                    "EnemyContact2DAdapter must be configured before the actor adapter.");
            }

            if (IsConfigured)
            {
                if (object.ReferenceEquals(enemyBody, body)
                    && object.ReferenceEquals(authority, actorAuthority)
                    && object.ReferenceEquals(decisionSource, actorDecisionSource)
                    && object.ReferenceEquals(targetSource, actorTargetSource)
                    && object.ReferenceEquals(contactAdapter, actorContactAdapter)
                    && maximumSpeed == maximumMovementSpeed)
                {
                    return;
                }

                throw new InvalidOperationException(
                    "EnemyActor2DAdapter is already configured with different dependencies.");
            }

            enemyBody = body;
            bodyAdapter = new MovementBody2DAdapter(body);
            authority = actorAuthority;
            decisionSource = actorDecisionSource;
            targetSource = actorTargetSource;
            contactAdapter = actorContactAdapter;
            maximumSpeed = maximumMovementSpeed;
            fixedStepCount = 0L;
            activeRequested = false;
            ClearEnemyVelocity();
        }

        public bool Activate()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "EnemyActor2DAdapter must be configured before activation.");
            }

            if (activeRequested)
            {
                return false;
            }

            activeRequested = true;
            contactAdapter.Activate();
            ClearEnemyVelocity();
            return true;
        }

        public bool Deactivate()
        {
            bool changed = activeRequested;
            activeRequested = false;
            if (contactAdapter != null)
            {
                contactAdapter.Deactivate();
            }

            ClearEnemyVelocity();
            return changed;
        }

        /// <summary>
        /// Resets transport/lifecycle state while delegating authoritative state reset
        /// to the configured package owner. A successful restart never retains velocity,
        /// contact callback identity, or a prior fixed-step index.
        /// </summary>
        public bool Restart()
        {
            if (!IsConfigured)
            {
                return false;
            }

            bool resume = activeRequested;
            activeRequested = false;
            contactAdapter.Deactivate();
            ClearEnemyVelocity();

            bool reset;
            try
            {
                decisionSource.Reset();
                reset = authority.Reset();
            }
            catch (ArgumentException)
            {
                reset = false;
            }
            catch (InvalidOperationException)
            {
                reset = false;
            }

            contactAdapter.ResetSession();
            fixedStepCount = 0L;
            generation = generation == long.MaxValue ? long.MaxValue : generation + 1L;

            if (!reset)
            {
                return false;
            }

            activeRequested = resume;
            if (resume)
            {
                contactAdapter.Activate();
            }

            return true;
        }

        /// <summary>
        /// Executes the same path used by FixedUpdate, allowing lifecycle owners and
        /// deterministic PlayMode tests to control the fixed-step boundary explicitly.
        /// </summary>
        public EnemyActor2DFixedStepResult ExecuteFixedStep(double deltaTimeSeconds)
        {
            if (double.IsNaN(deltaTimeSeconds)
                || double.IsInfinity(deltaTimeSeconds)
                || deltaTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));
            }

            long step = fixedStepCount;
            if (!IsConfigured)
            {
                return Result(EnemyActor2DFixedStepStatus.NotConfigured, step, null, 0d, 0d);
            }

            if (!IsActive)
            {
                ClearEnemyVelocity();
                return Result(EnemyActor2DFixedStepStatus.AdapterInactive, step, null, 0d, 0d);
            }

            contactAdapter.BeginFixedStep(step);

            EnemyActorState state;
            try
            {
                if (!authority.TryReadState(out state) || state == null)
                {
                    ClearEnemyVelocity();
                    AdvanceFixedStep();
                    return Result(
                        EnemyActor2DFixedStepStatus.AuthorityUnavailable,
                        step,
                        null,
                        0d,
                        0d);
                }
            }
            catch (ArgumentException)
            {
                ClearEnemyVelocity();
                AdvanceFixedStep();
                return Result(EnemyActor2DFixedStepStatus.AuthorityUnavailable, step, null, 0d, 0d);
            }
            catch (InvalidOperationException)
            {
                ClearEnemyVelocity();
                AdvanceFixedStep();
                return Result(EnemyActor2DFixedStepStatus.AuthorityUnavailable, step, null, 0d, 0d);
            }

            if (!state.IsActive)
            {
                ClearEnemyVelocity();
                AdvanceFixedStep();
                return Result(EnemyActor2DFixedStepStatus.ActorInactive, step, null, 0d, 0d);
            }

            EnemyTarget2DObservation target;
            try
            {
                if (!targetSource.TryReadTarget(out target) || target == null)
                {
                    ClearEnemyVelocity();
                    AdvanceFixedStep();
                    return Result(EnemyActor2DFixedStepStatus.TargetUnavailable, step, null, 0d, 0d);
                }
            }
            catch (MissingReferenceException)
            {
                ClearEnemyVelocity();
                AdvanceFixedStep();
                return Result(EnemyActor2DFixedStepStatus.TargetUnavailable, step, null, 0d, 0d);
            }
            catch (InvalidOperationException)
            {
                ClearEnemyVelocity();
                AdvanceFixedStep();
                return Result(EnemyActor2DFixedStepStatus.TargetUnavailable, step, null, 0d, 0d);
            }

            EnemyActor2DDecision decision;
            try
            {
                if (!decisionSource.TryDecide(
                        state,
                        target,
                        deltaTimeSeconds,
                        out decision)
                    || decision == null)
                {
                    ClearEnemyVelocity();
                    AdvanceFixedStep();
                    return Result(EnemyActor2DFixedStepStatus.DecisionUnavailable, step, null, 0d, 0d);
                }
            }
            catch (ArgumentException)
            {
                ClearEnemyVelocity();
                AdvanceFixedStep();
                return Result(EnemyActor2DFixedStepStatus.InvalidDecision, step, null, 0d, 0d);
            }
            catch (InvalidOperationException)
            {
                ClearEnemyVelocity();
                AdvanceFixedStep();
                return Result(EnemyActor2DFixedStepStatus.InvalidDecision, step, null, 0d, 0d);
            }

            if (decision.ActorId != state.ActorId || decision.TargetId != target.TargetId)
            {
                ClearEnemyVelocity();
                AdvanceFixedStep();
                return Result(EnemyActor2DFixedStepStatus.InvalidDecision, step, decision, 0d, 0d);
            }

            double velocityX = decision.VelocityX;
            double velocityY = decision.VelocityY;
            bool clamped = ClampVelocity(ref velocityX, ref velocityY, maximumSpeed);

            try
            {
                if (enemyBody == null || !bodyAdapter.IsBodyAvailable)
                {
                    AdvanceFixedStep();
                    return Result(EnemyActor2DFixedStepStatus.BodyUnavailable, step, decision, 0d, 0d);
                }

                bodyAdapter.ApplyAuthoritativeVelocity(velocityX, velocityY);
            }
            catch (ArgumentException)
            {
                ClearEnemyVelocity();
                AdvanceFixedStep();
                return Result(EnemyActor2DFixedStepStatus.InvalidDecision, step, decision, 0d, 0d);
            }
            catch (InvalidOperationException)
            {
                AdvanceFixedStep();
                return Result(EnemyActor2DFixedStepStatus.BodyUnavailable, step, decision, 0d, 0d);
            }

            AdvanceFixedStep();
            return Result(
                clamped
                    ? EnemyActor2DFixedStepStatus.AppliedClamped
                    : EnemyActor2DFixedStepStatus.Applied,
                step,
                decision,
                velocityX,
                velocityY);
        }

        private void FixedUpdate()
        {
            ExecuteFixedStep(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            Deactivate();
        }

        private void OnDestroy()
        {
            activeRequested = false;
            ClearEnemyVelocity();
            enemyBody = null;
            bodyAdapter = null;
            authority = null;
            decisionSource = null;
            targetSource = null;
            contactAdapter = null;
        }

        private static bool ClampVelocity(
            ref double velocityX,
            ref double velocityY,
            double speedLimit)
        {
            double magnitudeSquared = (velocityX * velocityX) + (velocityY * velocityY);
            double limitSquared = speedLimit * speedLimit;
            if (magnitudeSquared <= limitSquared)
            {
                return false;
            }

            double scale = speedLimit / Math.Sqrt(magnitudeSquared);
            velocityX *= scale;
            velocityY *= scale;
            return true;
        }

        private void AdvanceFixedStep()
        {
            fixedStepCount = fixedStepCount == long.MaxValue
                ? long.MaxValue
                : fixedStepCount + 1L;
        }

        private void ClearEnemyVelocity()
        {
            if (bodyAdapter != null && bodyAdapter.IsBodyAvailable)
            {
                bodyAdapter.ClearVelocity();
            }
        }

        private static EnemyActor2DFixedStepResult Result(
            EnemyActor2DFixedStepStatus status,
            long fixedStep,
            EnemyActor2DDecision decision,
            double velocityX,
            double velocityY)
        {
            return new EnemyActor2DFixedStepResult(
                status,
                fixedStep,
                decision,
                velocityX,
                velocityY);
        }
    }
}

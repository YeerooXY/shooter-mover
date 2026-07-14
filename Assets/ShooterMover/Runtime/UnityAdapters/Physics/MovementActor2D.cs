using System;
using ShooterMover.Contracts.Input;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UnityAdapters.Physics
{
    /// <summary>
    /// Lifecycle-owned movement authority that composes the MT-007 input boundary,
    /// engine-independent movement steppers, MT-008 Rigidbody2D projection, and
    /// MT-009 contact authority without scene lookup or a second physics driver.
    /// </summary>
    public sealed class MovementActor2D :
        IAuthoritativeMovementVelocitySource,
        IMovementContactAuthority,
        IDisposable
    {
        private readonly Rigidbody2D body;
        private readonly PlayerMovementIntentAdapter inputAdapter;
        private readonly MovementContact2DAdapter contactAdapter;
        private readonly MovementBody2DAdapter bodyAdapter;
        private readonly MovementThrusterTuningProfile tuning;

        private ThrusterBurstState movement;
        private ThrusterBankState thrusterBank;
        private PerContactGraceTracker graceTracker;
        private double normalizedMoveX;
        private double normalizedMoveY;
        private long generation;
        private long fixedStepCount;
        private bool isActive;
        private bool isDisposed;
        private bool contactAuthorityReady;

        public MovementActor2D(
            Rigidbody2D body,
            PlayerMovementIntentAdapter inputAdapter,
            InputActionAsset inputActions,
            MovementContact2DAdapter contactAdapter,
            MovementThrusterTuningProfile tuning)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (inputAdapter == null)
            {
                throw new ArgumentNullException(nameof(inputAdapter));
            }

            if (inputActions == null)
            {
                throw new ArgumentNullException(nameof(inputActions));
            }

            if (contactAdapter == null)
            {
                throw new ArgumentNullException(nameof(contactAdapter));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (!object.ReferenceEquals(body.gameObject, inputAdapter.gameObject)
                || !object.ReferenceEquals(body.gameObject, contactAdapter.gameObject))
            {
                throw new ArgumentException(
                    "Movement body, input adapter, and contact adapter must belong to one explicit actor GameObject.");
            }

            MovementThrusterTuningProfileValidator.Validate(tuning);

            this.body = body;
            this.inputAdapter = inputAdapter;
            this.contactAdapter = contactAdapter;
            this.tuning = tuning;
            bodyAdapter = new MovementBody2DAdapter(body);

            // Construction is deliberately quiescent. No Unity callback may sample or
            // mutate authority until Activate is called by the owning lifecycle.
            inputAdapter.enabled = false;
            contactAdapter.enabled = false;
            inputAdapter.Configure(inputActions);
            ResetDomainState();
            contactAdapter.Configure(body, this);
            ClearBodyIfAvailable();
        }

        public bool IsActive
        {
            get { return isActive && !isDisposed; }
        }

        public bool IsDisposed
        {
            get { return isDisposed; }
        }

        public long Generation
        {
            get { return generation; }
        }

        public long FixedStepCount
        {
            get { return fixedStepCount; }
        }

        public double CurrentVelocityX
        {
            get { return movement == null ? 0d : movement.VelocityX; }
        }

        public double CurrentVelocityY
        {
            get { return movement == null ? 0d : movement.VelocityY; }
        }

        public ThrusterBurstPhase CurrentPhase
        {
            get { return movement == null ? ThrusterBurstPhase.Ready : movement.Phase; }
        }

        public int AvailableThrusterCharges
        {
            get { return thrusterBank == null ? 0 : thrusterBank.AvailableCharges; }
        }

        public int MaximumThrusterCharges
        {
            get { return thrusterBank == null ? 0 : thrusterBank.MaximumCharges; }
        }

        public bool Activate()
        {
            ThrowIfDisposed();
            if (isActive)
            {
                return false;
            }

            ResetDomainState();
            ClearBodyIfAvailable();
            isActive = true;
            generation = generation == long.MaxValue ? long.MaxValue : generation + 1L;

            contactAdapter.enabled = true;
            inputAdapter.enabled = true;
            return true;
        }

        public bool Deactivate()
        {
            bool changed = isActive
                || (inputAdapter != null && inputAdapter.enabled)
                || (contactAdapter != null && contactAdapter.enabled);

            isActive = false;
            contactAuthorityReady = false;

            if (contactAdapter != null)
            {
                contactAdapter.enabled = false;
            }

            if (inputAdapter != null)
            {
                inputAdapter.enabled = false;
            }

            ResetDomainState();
            ClearBodyIfAvailable();
            return changed;
        }

        public void Restart()
        {
            ThrowIfDisposed();
            Deactivate();
            Activate();
        }

        /// <summary>
        /// Executes the only movement-driving path for one fixed step. Input is sampled,
        /// plain-C# state advances, and the resulting authority is projected to Rigidbody2D
        /// in that explicit order.
        /// </summary>
        public bool ExecuteFixedStep(double deltaTimeSeconds)
        {
            ValidateDeltaTime(deltaTimeSeconds);
            if (!IsActive)
            {
                return false;
            }

            PlayerIntentFrame intent = inputAdapter.ReadIntentFrame();
            normalizedMoveX = intent.Move.X;
            normalizedMoveY = intent.Move.Y;

            if (movement.Phase == ThrusterBurstPhase.Ready)
            {
                BaseLocomotionState locomotion = BaseLocomotionStepper.Step(
                    BaseLocomotionState.Create(movement.VelocityX, movement.VelocityY),
                    normalizedMoveX,
                    normalizedMoveY,
                    deltaTimeSeconds,
                    tuning);
                movement = ThrusterBurstState.CreateReady(locomotion, tuning);
            }

            bool activated;
            ThrusterBankState nextBank;
            movement = ThrusterBurstStepper.Step(
                movement,
                thrusterBank,
                normalizedMoveX,
                normalizedMoveY,
                deltaTimeSeconds,
                intent.Thruster.WasPressed,
                tuning,
                out nextBank,
                out activated);
            thrusterBank = nextBank;

            bodyAdapter.Apply(movement);
            fixedStepCount = fixedStepCount == long.MaxValue
                ? long.MaxValue
                : fixedStepCount + 1L;
            contactAuthorityReady = true;
            return true;
        }

        public bool TryReadVelocity(out double velocityX, out double velocityY)
        {
            if (!IsActive || movement == null)
            {
                velocityX = 0d;
                velocityY = 0d;
                return false;
            }

            velocityX = movement.VelocityX;
            velocityY = movement.VelocityY;
            return true;
        }

        public bool TryReadContactSnapshot(out MovementContactStateSnapshot snapshot)
        {
            if (!IsActive || !contactAuthorityReady)
            {
                snapshot = null;
                return false;
            }

            snapshot = new MovementContactStateSnapshot(
                movement,
                tuning,
                normalizedMoveX,
                normalizedMoveY,
                graceTracker);
            return true;
        }

        public void ApplyWallContact(WallReflectionResult result)
        {
            if (!IsActive || !contactAuthorityReady)
            {
                return;
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.State == null
                || result.State.TuningIdentity != tuning.DeterministicIdentity)
            {
                throw new ArgumentException(
                    "Wall-contact state must use the actor tuning identity.",
                    nameof(result));
            }

            movement = result.State;
        }

        public void ApplyEnemyContact(
            ContactGraceRegistration registration,
            MovementContactResolution resolution,
            PerContactGraceTracker nextGraceTracker)
        {
            if (!IsActive || !contactAuthorityReady)
            {
                return;
            }

            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            if (nextGraceTracker == null)
            {
                throw new ArgumentNullException(nameof(nextGraceTracker));
            }

            if (nextGraceTracker.TuningIdentity != tuning.DeterministicIdentity)
            {
                throw new ArgumentException(
                    "Enemy-contact grace must use the actor tuning identity.",
                    nameof(nextGraceTracker));
            }

            graceTracker = nextGraceTracker;
            if (resolution != null)
            {
                // MT-006 resolves velocity rather than an active burst phase. The explicit
                // handoff therefore terminates that burst and resumes ready locomotion from
                // the bounded resolved velocity instead of retaining stale phase authority.
                movement = ThrusterBurstState.CreateReady(
                    BaseLocomotionState.Create(resolution.VelocityX, resolution.VelocityY),
                    tuning);
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            Deactivate();
            isDisposed = true;
        }

        private void ResetDomainState()
        {
            movement = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);
            thrusterBank = ThrusterBankState.CreateFull(tuning);
            graceTracker = PerContactGraceTracker.Create(tuning);
            normalizedMoveX = 0d;
            normalizedMoveY = 0d;
            fixedStepCount = 0L;
            contactAuthorityReady = false;
        }

        private void ClearBodyIfAvailable()
        {
            if (body != null && bodyAdapter.IsBodyAvailable)
            {
                bodyAdapter.ClearVelocity();
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(MovementActor2D));
            }
        }

        private static void ValidateDeltaTime(double deltaTimeSeconds)
        {
            if (double.IsNaN(deltaTimeSeconds)
                || double.IsInfinity(deltaTimeSeconds)
                || deltaTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTimeSeconds),
                    deltaTimeSeconds,
                    "Movement actor fixed-step duration must be finite and non-negative.");
            }
        }
    }
}

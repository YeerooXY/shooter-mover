using System;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UnityAdapters.Physics
{
    /// <summary>
    /// Explicit Unity lifecycle owner for one <see cref="MovementActor2D"/>.
    /// Construction, start, restart, stop, and disposal are deliberate API calls;
    /// OnDisable and OnDestroy only enforce cleanup and never auto-start an actor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementActorLifecycle : MonoBehaviour
    {
        private Rigidbody2D body;
        private PlayerMovementIntentAdapter inputAdapter;
        private InputActionAsset inputActions;
        private MovementContact2DAdapter contactAdapter;
        private MovementThrusterTuningProfile tuning;
        private MovementActor2D actor;
        private bool isDisposed;

        public bool IsConstructed
        {
            get { return actor != null; }
        }

        public bool IsRunning
        {
            get { return actor != null && actor.IsActive; }
        }

        public bool IsDisposed
        {
            get { return isDisposed; }
        }

        public MovementActor2D Actor
        {
            get { return actor; }
        }

        /// <summary>
        /// Constructs exactly one actor from explicitly supplied dependencies. Repeating
        /// the same construction is idempotent; attempting to replace any dependency is rejected.
        /// </summary>
        public bool Construct(
            Rigidbody2D body,
            PlayerMovementIntentAdapter inputAdapter,
            InputActionAsset inputActions,
            MovementContact2DAdapter contactAdapter,
            MovementThrusterTuningProfile tuning)
        {
            ThrowIfDisposed();
            ValidateDependencies(body, inputAdapter, inputActions, contactAdapter, tuning);

            if (actor != null)
            {
                if (object.ReferenceEquals(this.body, body)
                    && object.ReferenceEquals(this.inputAdapter, inputAdapter)
                    && object.ReferenceEquals(this.inputActions, inputActions)
                    && object.ReferenceEquals(this.contactAdapter, contactAdapter)
                    && object.ReferenceEquals(this.tuning, tuning))
                {
                    return false;
                }

                throw new InvalidOperationException(
                    "MovementActorLifecycle is already constructed with different dependencies.");
            }

            this.body = body;
            this.inputAdapter = inputAdapter;
            this.inputActions = inputActions;
            this.contactAdapter = contactAdapter;
            this.tuning = tuning;
            actor = new MovementActor2D(
                body,
                inputAdapter,
                inputActions,
                contactAdapter,
                tuning);
            return true;
        }

        public bool StartActor()
        {
            EnsureCanRun();
            return actor.Activate();
        }

        public bool StopActor()
        {
            return actor != null && actor.Deactivate();
        }

        public bool RestartActor()
        {
            EnsureCanRun();
            actor.Restart();
            return true;
        }

        /// <summary>
        /// Public deterministic seam used by tests and explicit simulation hosts. Unity's
        /// FixedUpdate callback delegates to this same method and no secondary driver is created.
        /// </summary>
        public bool ExecuteFixedStep(double deltaTimeSeconds)
        {
            return actor != null && actor.ExecuteFixedStep(deltaTimeSeconds);
        }

        public bool DisposeActor()
        {
            if (isDisposed)
            {
                return false;
            }

            isDisposed = true;
            if (actor != null)
            {
                actor.Dispose();
            }

            return true;
        }

        private void FixedUpdate()
        {
            if (actor != null && actor.IsActive)
            {
                actor.ExecuteFixedStep(Time.fixedDeltaTime);
            }
        }

        private void OnDisable()
        {
            StopActor();
        }

        private void OnDestroy()
        {
            DisposeActor();
        }

        private void EnsureCanRun()
        {
            ThrowIfDisposed();
            if (actor == null)
            {
                throw new InvalidOperationException(
                    "MovementActorLifecycle must be constructed before it can run.");
            }

            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    "MovementActorLifecycle must be active and enabled before it can run.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(MovementActorLifecycle));
            }
        }

        private void ValidateDependencies(
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

            if (!object.ReferenceEquals(gameObject, body.gameObject)
                || !object.ReferenceEquals(gameObject, inputAdapter.gameObject)
                || !object.ReferenceEquals(gameObject, contactAdapter.gameObject))
            {
                throw new ArgumentException(
                    "MovementActorLifecycle and every supplied actor component must share one GameObject.");
            }
        }
    }
}

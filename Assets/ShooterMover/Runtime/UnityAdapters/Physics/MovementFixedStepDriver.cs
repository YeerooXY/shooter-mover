using System;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Physics
{
    /// <summary>
    /// Engine-facing read port for the current authoritative domain velocity.
    /// Implementations remain responsible for selecting base locomotion, burst,
    /// or exit-momentum state before Unity is sampled.
    /// </summary>
    public interface IAuthoritativeMovementVelocitySource
    {
        bool TryReadVelocity(out double velocityX, out double velocityY);
    }

    /// <summary>
    /// Samples authoritative movement once per Unity fixed step and projects it
    /// through a Rigidbody2D adapter. No gameplay velocity is cached here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementFixedStepDriver : MonoBehaviour
    {
        private MovementBody2DAdapter bodyAdapter;
        private IAuthoritativeMovementVelocitySource velocitySource;
        private bool driveRequested;

        public bool IsConfigured
        {
            get { return bodyAdapter != null && velocitySource != null; }
        }

        public bool IsDriving
        {
            get { return driveRequested && isActiveAndEnabled; }
        }

        public void Configure(
            Rigidbody2D body,
            IAuthoritativeMovementVelocitySource source)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (IsConfigured)
            {
                if (object.ReferenceEquals(bodyAdapter.Body, body)
                    && object.ReferenceEquals(velocitySource, source))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "MovementFixedStepDriver is already configured with different dependencies.");
            }

            bodyAdapter = new MovementBody2DAdapter(body);
            velocitySource = source;
            bodyAdapter.ClearVelocity();
        }

        public void StartDriving()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "MovementFixedStepDriver must be configured before it can start.");
            }

            driveRequested = true;
        }

        public void StopDriving()
        {
            driveRequested = false;
            ClearBodyIfAvailable();
        }

        /// <summary>
        /// Executes one explicit fixed-step projection. This is public so the
        /// composition root and deterministic tests can drive the same path used
        /// by Unity's FixedUpdate callback.
        /// </summary>
        public bool ExecuteFixedStep()
        {
            if (!driveRequested || !isActiveAndEnabled)
            {
                return false;
            }

            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "MovementFixedStepDriver lost its configuration while driving.");
            }

            double velocityX;
            double velocityY;
            if (!velocitySource.TryReadVelocity(out velocityX, out velocityY))
            {
                ClearBodyIfAvailable();
                return false;
            }

            bodyAdapter.ApplyAuthoritativeVelocity(velocityX, velocityY);
            return true;
        }

        private void FixedUpdate()
        {
            ExecuteFixedStep();
        }

        private void OnDisable()
        {
            ClearBodyIfAvailable();
        }

        private void OnDestroy()
        {
            driveRequested = false;
            ClearBodyIfAvailable();
            velocitySource = null;
            bodyAdapter = null;
        }

        private void ClearBodyIfAvailable()
        {
            if (bodyAdapter != null && bodyAdapter.IsBodyAvailable)
            {
                bodyAdapter.ClearVelocity();
            }
        }
    }
}

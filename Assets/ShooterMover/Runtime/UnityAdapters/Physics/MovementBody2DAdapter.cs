using System;
using ShooterMover.Domain.Movement;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Physics
{
    /// <summary>
    /// Thin Unity 2D projection of authoritative domain velocity.
    /// This adapter never retains a gameplay velocity of its own; observed velocity
    /// is always read directly from the bound <see cref="Rigidbody2D"/>.
    /// </summary>
    public sealed class MovementBody2DAdapter
    {
        private readonly Rigidbody2D body;

        public MovementBody2DAdapter(Rigidbody2D body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            this.body = body;
        }

        public Vector2 ObservedVelocity
        {
            get
            {
                EnsureBodyAvailable();
                return body.linearVelocity;
            }
        }

        internal Rigidbody2D Body
        {
            get { return body; }
        }

        internal bool IsBodyAvailable
        {
            get { return body != null; }
        }

        public void Apply(BaseLocomotionState state)
        {
            ApplyAuthoritativeVelocity(state.VelocityX, state.VelocityY);
        }

        public void Apply(ThrusterBurstState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            ApplyAuthoritativeVelocity(state.VelocityX, state.VelocityY);
        }

        public void ApplyAuthoritativeVelocity(double velocityX, double velocityY)
        {
            EnsureBodyAvailable();

            float projectedX = ConvertComponent(velocityX, nameof(velocityX));
            float projectedY = ConvertComponent(velocityY, nameof(velocityY));

            body.linearVelocity = new Vector2(projectedX, projectedY);
        }

        public void ClearVelocity()
        {
            EnsureBodyAvailable();
            body.linearVelocity = Vector2.zero;
        }

        private static float ConvertComponent(double value, string parameterName)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value > float.MaxValue
                || value < -float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Authoritative movement velocity must fit a finite Rigidbody2D component.");
            }

            return (float)value;
        }

        private void EnsureBodyAvailable()
        {
            if (body == null)
            {
                throw new InvalidOperationException("The bound Rigidbody2D is no longer available.");
            }
        }
    }
}

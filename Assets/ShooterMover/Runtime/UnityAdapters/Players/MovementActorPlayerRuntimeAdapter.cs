using System;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Physics;

namespace ShooterMover.UnityAdapters.Players
{
    /// <summary>Read/restart/disposal adapter over the existing MovementActorLifecycle.</summary>
    public sealed class MovementActorPlayerRuntimeAdapter : IPlayerMovementRuntime
    {
        private readonly MovementActorLifecycle lifecycle;
        private readonly MovementThrusterTuningProfile tuning;
        private bool disposed;

        public MovementActorPlayerRuntimeAdapter(
            MovementActorLifecycle lifecycle,
            MovementThrusterTuningProfile tuning)
        {
            if (lifecycle == null) throw new ArgumentNullException(nameof(lifecycle));
            if (!lifecycle.IsConstructed || lifecycle.Actor == null)
            {
                throw new ArgumentException(
                    "Movement lifecycle must already contain its explicit movement actor.",
                    nameof(lifecycle));
            }
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            MovementThrusterTuningProfileValidator.Validate(tuning);
            this.lifecycle = lifecycle;
            this.tuning = tuning;
        }

        public bool IsDisposed { get { return disposed || lifecycle == null || lifecycle.IsDisposed; } }

        public PlayerMovementSnapshot ExportSnapshot()
        {
            ThrowIfDisposed();
            MovementActor2D actor = lifecycle.Actor;
            ThrusterStatusSnapshot thruster = ThrusterStatusProjector.Project(actor, tuning);
            UnityEngine.Vector3 position = lifecycle.transform.position;
            return new PlayerMovementSnapshot(
                actor.Generation,
                position.x,
                position.y,
                thruster.VelocityX,
                thruster.VelocityY,
                thruster);
        }

        public bool TryRestart(long retiringGeneration, long replacementGeneration)
        {
            if (IsDisposed) return false;
            MovementActor2D actor = lifecycle.Actor;
            if (retiringGeneration < 0L
                || replacementGeneration < 0L
                || actor.Generation != retiringGeneration
                || retiringGeneration == long.MaxValue
                || replacementGeneration != retiringGeneration + 1L)
            {
                return false;
            }

            lifecycle.RestartActor();
            return lifecycle.Actor.Generation == replacementGeneration;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (lifecycle != null) lifecycle.DisposeActor();
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(MovementActorPlayerRuntimeAdapter));
        }
    }
}

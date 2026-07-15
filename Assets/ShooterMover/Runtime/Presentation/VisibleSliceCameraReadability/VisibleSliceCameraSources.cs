using System;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;

namespace ShooterMover.Presentation.VisibleSliceCameraReadability
{
    /// <summary>
    /// Getter-only world-position boundary used by the camera rig.
    /// </summary>
    public interface IVisibleSliceCameraFollowSource
    {
        bool TryReadWorldPosition(out Vector2 worldPosition);
    }

    /// <summary>
    /// Getter-only MT-011 snapshot boundary used for bounded burst look-ahead.
    /// </summary>
    public interface IVisibleSliceThrusterStatusReader
    {
        ThrusterStatusSnapshot ReadSnapshot();
    }

    /// <summary>
    /// Injected accessibility state. Implementations remain owned by their settings
    /// authority; this package only reads the current value.
    /// </summary>
    public interface IVisibleSliceReducedEffectsSource
    {
        bool ReducedEffectsEnabled { get; }
    }

    /// <summary>
    /// Explicit read-only adapter for the actor transform selected by the integration owner.
    /// It performs no scene search and exposes no transform mutation method.
    /// </summary>
    public sealed class TransformCameraFollowSource : IVisibleSliceCameraFollowSource
    {
        private readonly Transform target;

        public TransformCameraFollowSource(Transform target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            this.target = target;
        }

        public bool TryReadWorldPosition(out Vector2 worldPosition)
        {
            if (target == null)
            {
                worldPosition = Vector2.zero;
                return false;
            }

            Vector3 position = target.position;
            worldPosition = new Vector2(position.x, position.y);
            return true;
        }
    }

    /// <summary>
    /// Read-only bridge from the accepted MT-010 actor and tuning to the immutable
    /// MT-011 status projection. The projector performs reads only.
    /// </summary>
    public sealed class MovementActorThrusterStatusReader : IVisibleSliceThrusterStatusReader
    {
        private readonly MovementActor2D actor;
        private readonly MovementThrusterTuningProfile tuning;

        public MovementActorThrusterStatusReader(
            MovementActor2D actor,
            MovementThrusterTuningProfile tuning)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            this.actor = actor;
            this.tuning = tuning;
        }

        public ThrusterStatusSnapshot ReadSnapshot()
        {
            return ThrusterStatusProjector.Project(actor, tuning);
        }
    }
}

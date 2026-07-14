using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Physics
{
    /// <summary>
    /// Engine-facing classification of one contacted 2D collider.
    /// Unity tags, layers, names, and hierarchy conventions are deliberately not used.
    /// </summary>
    public enum MovementContact2DKind
    {
        Wall = 1,
        Enemy = 2,
    }

    public enum MovementContact2DClassificationResult
    {
        Classified = 1,
        MissingCollider = 2,
        MissingContract = 3,
        InvalidContract = 4,
        AmbiguousContract = 5,
    }

    /// <summary>
    /// Immutable explicit contact description supplied by a contacted Unity component.
    /// Enemy descriptions carry the accepted CS-004 weight message; wall descriptions do not.
    /// </summary>
    public sealed class MovementContact2DDescriptor
    {
        private MovementContact2DDescriptor(
            MovementContact2DKind kind,
            StableId enemyId,
            WeightMessage weightMessage)
        {
            if (!Enum.IsDefined(typeof(MovementContact2DKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown movement contact kind.");
            }

            if (kind == MovementContact2DKind.Wall)
            {
                if (enemyId != null || weightMessage != null)
                {
                    throw new ArgumentException("Wall contacts cannot carry enemy identity or weight data.");
                }
            }
            else
            {
                if (enemyId == null)
                {
                    throw new ArgumentNullException(nameof(enemyId));
                }

                if (weightMessage == null)
                {
                    throw new ArgumentNullException(nameof(weightMessage));
                }

                if (weightMessage.Channel != CombatChannel.Contact)
                {
                    throw new ArgumentException(
                        "Enemy movement contacts must use the CS-004 Contact channel.",
                        nameof(weightMessage));
                }

                if (weightMessage.TargetId != enemyId)
                {
                    throw new ArgumentException(
                        "Enemy contact identity must match the target of its CS-004 weight message.",
                        nameof(weightMessage));
                }
            }

            Kind = kind;
            EnemyId = enemyId;
            WeightMessage = weightMessage;
        }

        public MovementContact2DKind Kind { get; }

        public StableId EnemyId { get; }

        public WeightMessage WeightMessage { get; }

        public static MovementContact2DDescriptor Wall()
        {
            return new MovementContact2DDescriptor(MovementContact2DKind.Wall, null, null);
        }

        public static MovementContact2DDescriptor Enemy(
            StableId enemyId,
            WeightMessage weightMessage)
        {
            return new MovementContact2DDescriptor(
                MovementContact2DKind.Enemy,
                enemyId,
                weightMessage);
        }
    }

    /// <summary>
    /// Explicit component contract consumed by <see cref="MovementContactClassifier"/>.
    /// Implementations are projections only and must not perform movement, damage, or enemy behavior.
    /// </summary>
    public interface IMovementContact2DContract
    {
        bool TryDescribeMovementContact(out MovementContact2DDescriptor descriptor);
    }

    public static class MovementContactClassifier
    {
        public static MovementContact2DClassificationResult Classify(
            Collider2D collider,
            out MovementContact2DDescriptor descriptor)
        {
            descriptor = null;
            if (collider == null)
            {
                return MovementContact2DClassificationResult.MissingCollider;
            }

            MonoBehaviour[] behaviours = collider.GetComponents<MonoBehaviour>();
            IMovementContact2DContract contract = null;
            for (int index = 0; index < behaviours.Length; index++)
            {
                IMovementContact2DContract candidate = behaviours[index] as IMovementContact2DContract;
                if (candidate == null)
                {
                    continue;
                }

                if (contract != null)
                {
                    return MovementContact2DClassificationResult.AmbiguousContract;
                }

                contract = candidate;
            }

            if (contract == null)
            {
                return MovementContact2DClassificationResult.MissingContract;
            }

            try
            {
                MovementContact2DDescriptor described;
                if (!contract.TryDescribeMovementContact(out described) || described == null)
                {
                    return MovementContact2DClassificationResult.InvalidContract;
                }

                descriptor = described;
                return MovementContact2DClassificationResult.Classified;
            }
            catch (ArgumentException)
            {
                return MovementContact2DClassificationResult.InvalidContract;
            }
            catch (InvalidOperationException)
            {
                return MovementContact2DClassificationResult.InvalidContract;
            }
        }
    }
}

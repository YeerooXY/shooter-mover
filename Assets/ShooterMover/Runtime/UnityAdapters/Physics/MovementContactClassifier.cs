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
    public enum MovementContactKind
    {
        Wall = 1,
        Enemy = 2,
    }

    public enum MovementContactClassificationResult
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
    public sealed class MovementContactDescriptor
    {
        private MovementContactDescriptor(
            MovementContactKind kind,
            StableId enemyId,
            WeightMessage weightMessage)
        {
            if (!Enum.IsDefined(typeof(MovementContactKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown movement contact kind.");
            }

            if (kind == MovementContactKind.Wall)
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

        public MovementContactKind Kind { get; }

        public StableId EnemyId { get; }

        public WeightMessage WeightMessage { get; }

        public static MovementContactDescriptor Wall()
        {
            return new MovementContactDescriptor(MovementContactKind.Wall, null, null);
        }

        public static MovementContactDescriptor Enemy(
            StableId enemyId,
            WeightMessage weightMessage)
        {
            return new MovementContactDescriptor(
                MovementContactKind.Enemy,
                enemyId,
                weightMessage);
        }
    }

    /// <summary>
    /// Explicit component contract consumed by <see cref="MovementContactClassifier"/>.
    /// Implementations are projections only and must not perform movement, damage, or enemy behavior.
    /// </summary>
    public interface IMovementContact
    {
        bool TryDescribeMovementContact(out MovementContactDescriptor descriptor);
    }

    public static class MovementContactClassifier
    {
        public static MovementContactClassificationResult Classify(
            Collider2D collider,
            out MovementContactDescriptor descriptor)
        {
            descriptor = null;
            if (collider == null)
            {
                return MovementContactClassificationResult.MissingCollider;
            }

            MonoBehaviour[] behaviours = collider.GetComponents<MonoBehaviour>();
            IMovementContact contract = null;
            for (int index = 0; index < behaviours.Length; index++)
            {
                IMovementContact candidate = behaviours[index] as IMovementContact;
                if (candidate == null)
                {
                    continue;
                }

                if (contract != null)
                {
                    return MovementContactClassificationResult.AmbiguousContract;
                }

                contract = candidate;
            }

            if (contract == null)
            {
                return MovementContactClassificationResult.MissingContract;
            }

            try
            {
                MovementContactDescriptor described;
                if (!contract.TryDescribeMovementContact(out described) || described == null)
                {
                    return MovementContactClassificationResult.InvalidContract;
                }

                descriptor = described;
                return MovementContactClassificationResult.Classified;
            }
            catch (ArgumentException)
            {
                return MovementContactClassificationResult.InvalidContract;
            }
            catch (InvalidOperationException)
            {
                return MovementContactClassificationResult.InvalidContract;
            }
        }
    }
}

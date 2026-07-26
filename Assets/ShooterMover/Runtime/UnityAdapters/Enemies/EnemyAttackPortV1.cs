using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Missions.Rooms;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Room-scoped port for enemy attacks that can currently be shown as simple travelling shots.
    /// </summary>
    public class EnemyAttackPortV1 :
        IEnemyAttackEffectPortV1,
        IEnemyAttackPatternEffectPortV1
    {
        private readonly Dictionary<StableId, EnemyAttackBinding2D> attacks =
            new Dictionary<StableId, EnemyAttackBinding2D>();
        private readonly Dictionary<StableId, string> dispatches =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, string> cancellations =
            new Dictionary<StableId, string>();

        public void Bind(RoomEnemyActor2D actor, long revision)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (revision <= 0L) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!actor.IsBound || actor.Runtime == null)
            {
                throw new InvalidOperationException("enemy-attack-requires-bound-actor");
            }
            if (!EnemyAttack2D.Supports(actor.Runtime))
            {
                return;
            }

            EnemyAttackBinding2D current;
            if (attacks.TryGetValue(actor.ActorStableId, out current))
            {
                if (current == null)
                {
                    throw new InvalidOperationException("enemy-attack-binding-lost");
                }
                current.Bind(actor, revision);
                return;
            }

            EnemyAttackBinding2D binding = actor.GetComponent<EnemyAttackBinding2D>()
                ?? actor.gameObject.AddComponent<EnemyAttackBinding2D>();
            binding.Bind(actor, revision);
            attacks.Add(actor.ActorStableId, binding);
        }

        public void Emit(EnemyAttackExecutionRequestV1 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            throw new InvalidOperationException("enemy-attack-requires-pattern-dispatch");
        }

        public EnemyAttackPatternDispatchResultV1 Dispatch(
            EnemyAttackSequenceDispatchV1 sequence)
        {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));

            string fingerprint;
            if (dispatches.TryGetValue(sequence.DispatchStableId, out fingerprint))
            {
                return string.Equals(fingerprint, sequence.Fingerprint, StringComparison.Ordinal)
                    ? EnemyAttackPatternDispatchResultV1.ExactReplay(
                        sequence.DispatchStableId,
                        sequence.Fingerprint)
                    : EnemyAttackPatternDispatchResultV1.Rejected(
                        sequence.DispatchStableId,
                        sequence.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.ConflictingDuplicate);
            }

            EnemyAttackBinding2D binding;
            EnemyAttack2D attack;
            if (!attacks.TryGetValue(
                    sequence.Execution.Identity.EntityInstanceId,
                    out binding)
                || binding == null
                || (attack = binding.CurrentAttack) == null)
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    sequence.DispatchStableId,
                    sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.UnsupportedPort);
            }

            try
            {
                string diagnostic;
                if (!attack.TryAccept(sequence, out diagnostic))
                {
                    attack.Report(diagnostic);
                    return EnemyAttackPatternDispatchResultV1.Rejected(
                        sequence.DispatchStableId,
                        sequence.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
                }
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                attack.Report(
                    "enemy-attack-dispatch-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message);
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    sequence.DispatchStableId,
                    sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            dispatches.Add(sequence.DispatchStableId, sequence.Fingerprint);
            return EnemyAttackPatternDispatchResultV1.Applied(
                sequence.DispatchStableId,
                sequence.Fingerprint);
        }

        public EnemyAttackPatternDispatchResultV1 Cancel(
            EnemyAttackSequenceCancellationFactV1 cancellation)
        {
            if (cancellation == null) throw new ArgumentNullException(nameof(cancellation));

            string fingerprint;
            if (cancellations.TryGetValue(cancellation.CancellationStableId, out fingerprint))
            {
                return string.Equals(fingerprint, cancellation.Fingerprint, StringComparison.Ordinal)
                    ? EnemyAttackPatternDispatchResultV1.ExactReplay(
                        cancellation.CancellationStableId,
                        cancellation.Fingerprint)
                    : EnemyAttackPatternDispatchResultV1.Rejected(
                        cancellation.CancellationStableId,
                        cancellation.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.ConflictingDuplicate);
            }

            EnemyAttackBinding2D binding;
            EnemyAttack2D attack;
            if (!attacks.TryGetValue(cancellation.SourceEntityStableId, out binding)
                || binding == null
                || (attack = binding.CurrentAttack) == null)
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    cancellation.CancellationStableId,
                    cancellation.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.UnsupportedPort);
            }

            try
            {
                string diagnostic;
                if (!attack.TryCancel(cancellation, out diagnostic))
                {
                    attack.Report(diagnostic);
                    return EnemyAttackPatternDispatchResultV1.Rejected(
                        cancellation.CancellationStableId,
                        cancellation.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
                }
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                attack.Report(
                    "enemy-attack-cancellation-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message);
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    cancellation.CancellationStableId,
                    cancellation.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            cancellations.Add(cancellation.CancellationStableId, cancellation.Fingerprint);
            return EnemyAttackPatternDispatchResultV1.Applied(
                cancellation.CancellationStableId,
                cancellation.Fingerprint);
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }

    /// <summary>
    /// Compatibility name retained for the existing room-spawner call site.
    /// </summary>
    public sealed class RoomEnemyAttackPresentationPortV1 : EnemyAttackPortV1
    {
    }
}

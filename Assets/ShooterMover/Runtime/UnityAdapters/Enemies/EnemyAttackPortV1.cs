using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Enemies.Presentation;
using ShooterMover.UnityAdapters.Missions.Rooms;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Room-scoped port for enemy attacks that can currently be realized as supported simple
    /// travelling shots. Canonical dispatch remains authoritative; presentation mirrors only
    /// accepted sequence facts through a separate projection.
    /// </summary>
    public class EnemyAttackPortV1 :
        IEnemyAttackEffectPortV1,
        IEnemyAttackPatternEffectPortV1
    {
        private readonly Dictionary<StableId, EnemyAttack2D> attacks =
            new Dictionary<StableId, EnemyAttack2D>();
        private readonly Dictionary<StableId, EnemyAttackPresentationProjection2D> projections =
            new Dictionary<StableId, EnemyAttackPresentationProjection2D>();
        private readonly Dictionary<StableId, string> dispatches =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, string> cancellations =
            new Dictionary<StableId, string>();

        public int BoundPublisherCount { get { return attacks.Count; } }

        public EnemyAttack2D Bind(RoomEnemyActor2D actor, long revision)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (revision <= 0L) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!actor.IsBound || actor.Runtime == null)
            {
                throw new InvalidOperationException("enemy-attack-requires-bound-actor");
            }
            if (!EnemyAttack2D.Supports(actor.Runtime))
            {
                throw new InvalidOperationException("enemy-attack-mechanics-unsupported");
            }

            EnemyAttack2D currentAttack;
            EnemyAttackPresentationProjection2D currentProjection;
            bool hasAttack = attacks.TryGetValue(actor.ActorStableId, out currentAttack);
            bool hasProjection = projections.TryGetValue(
                actor.ActorStableId,
                out currentProjection);
            if (hasAttack || hasProjection)
            {
                if (!hasAttack
                    || !hasProjection
                    || currentAttack == null
                    || currentProjection == null)
                {
                    throw new InvalidOperationException("enemy-attack-binding-lost");
                }
                if (currentAttack.gameObject != actor.gameObject
                    || currentProjection.gameObject != actor.gameObject)
                {
                    throw new InvalidOperationException(
                        "enemy-attack-actor-identity-duplicated");
                }
                currentAttack.Bind(actor, revision);
                currentProjection.Bind(actor, revision);
                RequireCurrentBinding(currentAttack, currentProjection, actor, revision);
                return currentAttack;
            }

            EnemyAttack2D attack = actor.GetComponent<EnemyAttack2D>()
                ?? actor.gameObject.AddComponent<EnemyAttack2D>();
            EnemyAttackPresentationProjection2D projection = actor.GetComponent<
                    EnemyAttackPresentationProjection2D>()
                ?? actor.gameObject.AddComponent<EnemyAttackPresentationProjection2D>();
            attack.Bind(actor, revision);
            projection.Bind(actor, revision);
            RequireCurrentBinding(attack, projection, actor, revision);
            attacks.Add(actor.ActorStableId, attack);
            projections.Add(actor.ActorStableId, projection);
            return attack;
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

            StableId sourceId = sequence.Execution.Identity.EntityInstanceId;
            EnemyAttack2D attack;
            EnemyAttackPresentationProjection2D projection;
            if (!attacks.TryGetValue(sourceId, out attack)
                || attack == null
                || !projections.TryGetValue(sourceId, out projection)
                || projection == null)
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    sequence.DispatchStableId,
                    sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.UnsupportedPort);
            }

            EnemyAttackPresentationPlanV1 plan;
            string diagnostic;
            if (!projection.TryCreatePlan(sequence, out plan, out diagnostic))
            {
                attack.Report(diagnostic);
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    sequence.DispatchStableId,
                    sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            bool presentationApplied = false;
            try
            {
                // Warn-before-danger policy: stage the validated visual plan first. If canonical
                // attack acceptance rejects, rollback removes all deferred pulses before a frame
                // can render them. A harmless stale warning is preferable to an untelegraphed hit.
                projection.Apply(plan);
                presentationApplied = true;
                if (!attack.TryAccept(sequence, out diagnostic))
                {
                    projection.Rollback(plan);
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
                if (presentationApplied)
                {
                    try
                    {
                        projection.Rollback(plan);
                    }
                    catch (Exception rollbackException)
                    {
                        if (IsFatal(rollbackException)) throw;
                        attack.Report(
                            "enemy-attack-presentation-rollback-exception:"
                            + rollbackException.GetType().Name
                            + ":"
                            + rollbackException.Message);
                    }
                }
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

            EnemyAttack2D attack;
            EnemyAttackPresentationProjection2D projection;
            if (!attacks.TryGetValue(cancellation.SourceEntityStableId, out attack)
                || attack == null
                || !projections.TryGetValue(
                    cancellation.SourceEntityStableId,
                    out projection)
                || projection == null)
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
                projection.Cancel(cancellation);
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

        private static void RequireCurrentBinding(
            EnemyAttack2D attack,
            EnemyAttackPresentationProjection2D projection,
            RoomEnemyActor2D actor,
            long revision)
        {
            if (attack == null
                || projection == null
                || actor == null
                || attack.gameObject != actor.gameObject
                || projection.gameObject != actor.gameObject
                || !attack.IsBound
                || !projection.IsBound
                || attack.PresentationRevision != revision
                || actor.LifecycleGeneration != revision)
            {
                throw new InvalidOperationException("enemy-attack-publisher-binding-incomplete");
            }
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

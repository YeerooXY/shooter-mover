using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    public interface IEnemyAttackPatternLineOfSightV1
    {
        bool HasClearLine(
            Vector2 origin,
            Vector2 target,
            IReadOnlyList<Collider2D> ownerColliders,
            IReadOnlyList<Collider2D> targetColliders);
    }

    public sealed class PhysicsEnemyAttackPatternLineOfSightV1 :
        IEnemyAttackPatternLineOfSightV1
    {
        private readonly int layerMask;

        public PhysicsEnemyAttackPatternLineOfSightV1(
            int layerMask = Physics2D.DefaultRaycastLayers)
        {
            this.layerMask = layerMask;
        }

        public bool HasClearLine(
            Vector2 origin,
            Vector2 target,
            IReadOnlyList<Collider2D> ownerColliders,
            IReadOnlyList<Collider2D> targetColliders)
        {
            if (!Finite(origin)
                || !Finite(target)
                || (target - origin).sqrMagnitude <= 0.000001f
                || targetColliders == null
                || targetColliders.Count == 0)
            {
                return false;
            }
            RaycastHit2D[] hits = Physics2D.LinecastAll(
                origin,
                target,
                layerMask);
            Array.Sort(hits, (left, right) =>
                left.fraction.CompareTo(right.fraction));
            for (int index = 0; index < hits.Length; index++)
            {
                Collider2D collider = hits[index].collider;
                if (collider == null || Contains(ownerColliders, collider))
                {
                    continue;
                }
                return Contains(targetColliders, collider);
            }
            return false;
        }

        private static bool Contains(
            IReadOnlyList<Collider2D> source,
            Collider2D candidate)
        {
            if (source == null || candidate == null)
            {
                return false;
            }
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index] == candidate)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Finite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }
    }

    /// <summary>
    /// Generic definition-driven production attack decision adapter. Retained enemy packages may
    /// continue owning movement, health and presentation while this controller exclusively commits
    /// schema-v2 attack sequences. No enemy definition or prefab name is switched here.
    /// </summary>
    public sealed class EnemyAttackPatternProductionController2D
    {
        private static readonly StableId DecisionId =
            StableId.Parse("enemy-decision.production-schema-v2");
        private static readonly StableId ReadyPhaseId =
            StableId.Parse("enemy-phase.attack-ready");
        private static readonly StableId ReadyReasonId =
            StableId.Parse("enemy-decision-reason.attack-ready");

        private readonly EnemyDefinitionV1 definition;
        private readonly EnemyAttackCapabilityDescriptorV1 attack;
        private readonly EnemyAttackPatternUnitySourceBindingV1 source;
        private readonly EnemyAttackPatternTargetBindingV1 target;
        private readonly Func<Vector2> sourceOriginExporter;
        private readonly Func<Vector2> sourceFacingExporter;
        private readonly Func<Vector2> targetPositionExporter;
        private readonly IEnemyAttackPatternLineOfSightV1 lineOfSight;
        private readonly EnemyCommittedAttackPatternExecutorV1 executor;
        private long observedLifecycle;
        private long nextOperationOrdinal;
        private bool lifecycleCancelled;

        public EnemyAttackPatternProductionController2D(
            EnemyDefinitionV1 definition,
            EnemyAttackCapabilityDescriptorV1 attack,
            EnemyAttackPatternUnitySourceBindingV1 source,
            EnemyAttackPatternTargetBindingV1 target,
            Func<Vector2> sourceOriginExporter,
            Func<Vector2> sourceFacingExporter,
            Func<Vector2> targetPositionExporter,
            IEnemyAttackPatternLineOfSightV1 lineOfSight,
            EnemyCommittedAttackPatternExecutorV1 executor)
        {
            this.definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            this.attack = attack ?? throw new ArgumentNullException(nameof(attack));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.sourceOriginExporter = sourceOriginExporter
                ?? throw new ArgumentNullException(nameof(sourceOriginExporter));
            this.sourceFacingExporter = sourceFacingExporter
                ?? throw new ArgumentNullException(nameof(sourceFacingExporter));
            this.targetPositionExporter = targetPositionExporter
                ?? throw new ArgumentNullException(nameof(targetPositionExporter));
            this.lineOfSight = lineOfSight
                ?? throw new ArgumentNullException(nameof(lineOfSight));
            this.executor = executor
                ?? throw new ArgumentNullException(nameof(executor));
            if (source.SourceEntityStableId != executor.Identity.EntityInstanceId
                || source.SourceRunParticipantStableId
                    != executor.Identity.RunParticipantId
                || source.FindTarget(target.TargetEntityStableId) == null)
            {
                throw new ArgumentException(
                    "Production attack controller identities do not share one binding.");
            }
            observedLifecycle = source.LifecycleGeneration;
        }

        public EnemyCommittedAttackPatternResultV1 Tick()
        {
            long lifecycle = source.LifecycleGeneration;
            if (lifecycle != observedLifecycle)
            {
                observedLifecycle = lifecycle;
                nextOperationOrdinal = 0L;
                lifecycleCancelled = false;
            }

            if (!source.IsActive)
            {
                CancelLifecycleOnce();
                return null;
            }
            lifecycleCancelled = false;
            if (!target.IsActive
                || executor.NextAvailableAtSeconds
                    > CurrentTimeSeconds())
            {
                return null;
            }

            Vector2 origin = sourceOriginExporter();
            Vector2 targetPosition = targetPositionExporter();
            Vector2 delta = targetPosition - origin;
            float distance = delta.magnitude;
            if (!Finite(origin)
                || !Finite(targetPosition)
                || distance <= 0.000001f
                || distance > definition.DetectionRadius
                || distance < attack.MinimumAttackRange
                || distance > attack.MaximumAttackRange)
            {
                return null;
            }

            Vector2 direction = delta.normalized;
            Vector2 facing = sourceFacingExporter();
            if (!Finite(facing) || facing.sqrMagnitude <= 0.000001f)
            {
                facing = direction;
            }
            facing.Normalize();
            if (!WithinArc(facing, direction, definition.VisionArcDegrees)
                || !WithinArc(facing, direction, attack.AttackArcDegrees)
                || !lineOfSight.HasClearLine(
                    origin,
                    targetPosition,
                    source.OwnerColliders,
                    target.Colliders))
            {
                return null;
            }

            StableId operationStableId = StableId.Create(
                "enemy-attack-operation",
                "runtime-"
                    + Hash64(
                        source.SourceEntityStableId
                        + "|"
                        + lifecycle.ToString(CultureInfo.InvariantCulture)
                        + "|"
                        + nextOperationOrdinal.ToString(
                            CultureInfo.InvariantCulture)));
            var intent = new EnemyAttackIntent(
                source.SourceEntityStableId,
                source.SourceRunParticipantStableId,
                target.TargetEntityStableId,
                attack.AttackId,
                new EnemyVector2(origin.x, origin.y),
                new EnemyVector2(direction.x, direction.y),
                new EnemyVector2(targetPosition.x, targetPosition.y),
                DecisionId,
                ReadyPhaseId,
                ReadyReasonId);
            EnemyCommittedAttackPatternResultV1 result =
                executor.Commit(operationStableId, intent);
            if (result != null
                && result.Status
                    != EnemyCommittedAttackPatternStatusV1.CooldownActive)
            {
                if (nextOperationOrdinal < long.MaxValue)
                {
                    nextOperationOrdinal++;
                }
            }
            return result;
        }

        private void CancelLifecycleOnce()
        {
            if (lifecycleCancelled || observedLifecycle <= 0L)
            {
                return;
            }
            lifecycleCancelled = true;
            StableId operationStableId = StableId.Create(
                "enemy-attack-cancellation",
                "runtime-"
                    + Hash64(
                        source.SourceEntityStableId
                        + "|"
                        + observedLifecycle.ToString(
                            CultureInfo.InvariantCulture)));
            executor.CancelLifecycle(
                operationStableId,
                CurrentTimeSeconds());
        }

        private double CurrentTimeSeconds()
        {
            return executor.NextAvailableAtSeconds <= 0d
                ? 0d
                : Math.Min(
                    executor.NextAvailableAtSeconds,
                    executor.NextAvailableAtSeconds);
        }

        private static bool WithinArc(
            Vector2 facing,
            Vector2 direction,
            double totalArcDegrees)
        {
            if (totalArcDegrees >= 360d)
            {
                return true;
            }
            if (totalArcDegrees <= 0d
                || double.IsNaN(totalArcDegrees)
                || double.IsInfinity(totalArcDegrees))
            {
                return false;
            }
            return Vector2.Angle(facing, direction)
                <= ((float)totalArcDegrees * 0.5f) + 0.0001f;
        }

        private static bool Finite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }

        private static string Hash64(string value)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                hash ^= source[index];
                hash *= prime;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}

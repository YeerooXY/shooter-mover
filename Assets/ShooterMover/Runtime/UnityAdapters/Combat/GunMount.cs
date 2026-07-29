using System;
using System.Collections.Generic;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Combat
{
    public enum GunMountExecutionStatus
    {
        Executed = 1,
        Disabled = 2,
        NotConfigured = 3,
        InvalidPlan = 4,
        Invalid2DScene = 5,
        MissingHandler = 6,
        HandlerRejected = 7,
        HandlerFaulted = 8,
    }

    /// <summary>
    /// Immutable result from one attempt to apply a validated engine-independent plan.
    /// Failure classifications deliberately omit exception text and scene state.
    /// </summary>
    public sealed class GunMountExecutionResult
    {
        internal GunMountExecutionResult(
            GunMountExecutionStatus status,
            StableId planId,
            int executedOperationCount,
            int failedOperationIndex,
            StableId failedOperationId)
        {
            Status = status;
            PlanId = planId;
            ExecutedOperationCount = executedOperationCount;
            FailedOperationIndex = failedOperationIndex;
            FailedOperationId = failedOperationId;
        }

        public GunMountExecutionStatus Status { get; }

        public StableId PlanId { get; }

        public int ExecutedOperationCount { get; }

        public int FailedOperationIndex { get; }

        public StableId FailedOperationId { get; }

        public bool Succeeded
        {
            get { return Status == GunMountExecutionStatus.Executed; }
        }
    }

    /// <summary>
    /// Explicit 2D-only execution context. It exposes no scene search, Transform,
    /// GameObject, Rigidbody, Collider, or three-dimensional physics API.
    /// </summary>
    public sealed class GunMountExecutionContext
    {
        internal GunMountExecutionContext(
            PhysicsScene2D physicsScene,
            StableId sourceId,
            StableId combatEventId,
            StableId gunId,
            StableId mountId,
            StableId planId,
            Vector2 origin,
            Vector2 direction,
            int planOperationIndex)
        {
            PhysicsScene = physicsScene;
            SourceId = sourceId;
            CombatEventId = combatEventId;
            GunId = gunId;
            MountId = mountId;
            PlanId = planId;
            Origin = origin;
            Direction = direction;
            PlanOperationIndex = planOperationIndex;
        }

        public PhysicsScene2D PhysicsScene { get; }

        public StableId SourceId { get; }

        public StableId CombatEventId { get; }

        public StableId GunId { get; }

        public StableId MountId { get; }

        public StableId PlanId { get; }

        public Vector2 Origin { get; }

        public Vector2 Direction { get; }

        public int PlanOperationIndex { get; }
    }

    /// <summary>
    /// Explicitly registered bridge for one immutable operation kind. Concrete gun
    /// packages own their handlers; this common adapter never switches on gun IDs.
    /// </summary>
    public interface IGunFireExecutionHandler
    {
        StableId OperationKindId { get; }

        bool TryExecute(
            GunFireExecutionOperationEntry operation,
            GunMountExecutionContext context);
    }

    /// <summary>
    /// Applies canonical CB-004 execution-plan operations in plan order through an
    /// explicit registry of 2D handlers. The component owns no mount simulation,
    /// damage authority, pooling policy, content identity, or scene-wide state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GunMount : MonoBehaviour
    {
        private readonly Dictionary<StableId, IGunFireExecutionHandler>
            handlersByKind =
                new Dictionary<StableId, IGunFireExecutionHandler>();

        private StableId sourceId;
        private StableId gunId;
        private StableId mountId;
        private bool isConfigured;

        public bool IsConfigured
        {
            get { return isConfigured; }
        }

        public StableId SourceId
        {
            get { return sourceId; }
        }

        public StableId GunId
        {
            get { return gunId; }
        }

        public StableId MountId
        {
            get { return mountId; }
        }

        public int RegisteredHandlerCount
        {
            get { return handlersByKind.Count; }
        }

        /// <summary>
        /// Explicit runtime composition. Duplicate operation-kind registrations are
        /// rejected before the adapter can execute any work.
        /// </summary>
        public void Configure(
            StableId sourceActorId,
            StableId configuredGunId,
            StableId configuredMountId,
            IEnumerable<IGunFireExecutionHandler> handlers)
        {
            if (sourceActorId == null)
            {
                throw new ArgumentNullException(nameof(sourceActorId));
            }

            if (configuredGunId == null)
            {
                throw new ArgumentNullException(nameof(configuredGunId));
            }

            if (configuredMountId == null)
            {
                throw new ArgumentNullException(nameof(configuredMountId));
            }

            if (handlers == null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            Dictionary<StableId, IGunFireExecutionHandler> candidate =
                new Dictionary<StableId, IGunFireExecutionHandler>();
            foreach (IGunFireExecutionHandler handler in handlers)
            {
                if (handler == null)
                {
                    throw new ArgumentException(
                        "The 2D operation-handler registry cannot contain null.",
                        nameof(handlers));
                }

                if (handler.OperationKindId == null)
                {
                    throw new ArgumentException(
                        "Every 2D operation handler requires a stable operation-kind ID.",
                        nameof(handlers));
                }

                if (candidate.ContainsKey(handler.OperationKindId))
                {
                    throw new ArgumentException(
                        "Ambiguous duplicate 2D handler for operation kind "
                        + handler.OperationKindId
                        + ".",
                        nameof(handlers));
                }

                candidate.Add(handler.OperationKindId, handler);
            }

            handlersByKind.Clear();
            foreach (KeyValuePair<StableId, IGunFireExecutionHandler> pair
                in candidate)
            {
                handlersByKind.Add(pair.Key, pair.Value);
            }

            sourceId = sourceActorId;
            gunId = configuredGunId;
            mountId = configuredMountId;
            isConfigured = true;
        }

        public void ClearConfiguration()
        {
            handlersByKind.Clear();
            sourceId = null;
            gunId = null;
            mountId = null;
            isConfigured = false;
        }

        /// <summary>
        /// Applies one already validated CB-004 plan. All plan metadata and handler
        /// availability are preflighted before the first handler is invoked.
        /// </summary>
        public GunMountExecutionResult ExecutePlan(GunFireExecutionPlan plan)
        {
            if (!isActiveAndEnabled)
            {
                return Result(GunMountExecutionStatus.Disabled, plan, 0, -1, null);
            }

            if (!isConfigured)
            {
                return Result(GunMountExecutionStatus.NotConfigured, plan, 0, -1, null);
            }

            if (!TryValidatePlanEnvelope(plan))
            {
                return Result(GunMountExecutionStatus.InvalidPlan, plan, 0, -1, null);
            }

            Vector2 origin;
            Vector2 direction;
            if (!TryConvertGeometry(plan, out origin, out direction))
            {
                return Result(GunMountExecutionStatus.InvalidPlan, plan, 0, -1, null);
            }

            IGunFireExecutionHandler[] orderedHandlers =
                new IGunFireExecutionHandler[plan.OperationCount];
            GunFireExecutionOperationEntry[] orderedOperations =
                new GunFireExecutionOperationEntry[plan.OperationCount];

            for (int index = 0; index < plan.OperationCount; index++)
            {
                GunFireExecutionOperationEntry entry;
                try
                {
                    entry = plan.GetOperation(index);
                }
                catch (Exception)
                {
                    return Result(
                        GunMountExecutionStatus.InvalidPlan,
                        plan,
                        0,
                        index,
                        null);
                }

                if (!IsValidEntry(entry, index))
                {
                    return Result(
                        GunMountExecutionStatus.InvalidPlan,
                        plan,
                        0,
                        index,
                        entry == null ? null : entry.OperationId);
                }

                IGunFireExecutionHandler handler;
                if (!handlersByKind.TryGetValue(entry.OperationKindId, out handler))
                {
                    return Result(
                        GunMountExecutionStatus.MissingHandler,
                        plan,
                        0,
                        index,
                        entry.OperationId);
                }

                orderedOperations[index] = entry;
                orderedHandlers[index] = handler;
            }

            PhysicsScene2D physicsScene = gameObject.scene.GetPhysicsScene2D();
            if (!physicsScene.IsValid())
            {
                return Result(
                    GunMountExecutionStatus.Invalid2DScene,
                    plan,
                    0,
                    -1,
                    null);
            }

            int executedCount = 0;
            for (int index = 0; index < orderedOperations.Length; index++)
            {
                GunFireExecutionOperationEntry entry = orderedOperations[index];
                GunMountExecutionContext context = new GunMountExecutionContext(
                    physicsScene,
                    sourceId,
                    plan.CombatEventId,
                    plan.GunId,
                    plan.MountId,
                    plan.DeterministicIdentity,
                    origin,
                    direction,
                    index);

                bool accepted;
                try
                {
                    accepted = orderedHandlers[index].TryExecute(entry, context);
                }
                catch (Exception)
                {
                    return Result(
                        GunMountExecutionStatus.HandlerFaulted,
                        plan,
                        executedCount,
                        index,
                        entry.OperationId);
                }

                if (!accepted)
                {
                    return Result(
                        GunMountExecutionStatus.HandlerRejected,
                        plan,
                        executedCount,
                        index,
                        entry.OperationId);
                }

                executedCount++;
            }

            return Result(
                GunMountExecutionStatus.Executed,
                plan,
                executedCount,
                -1,
                null);
        }

        private bool TryValidatePlanEnvelope(GunFireExecutionPlan plan)
        {
            return plan != null
                && plan.PlanVersion == GunFireExecutionPlan.CurrentPlanVersion
                && plan.Input != null
                && plan.Input.RuntimeProfile != null
                && plan.CombatEventId != null
                && plan.GunId == gunId
                && plan.MountId == mountId
                && plan.DeterministicIdentity != null
                && !string.IsNullOrEmpty(plan.Fingerprint)
                && plan.OperationCount >= 0
                && plan.OperationCount <= GunFireExecutionPlan.MaximumOperationCount;
        }

        private static bool TryConvertGeometry(
            GunFireExecutionPlan plan,
            out Vector2 origin,
            out Vector2 direction)
        {
            float originX = (float)plan.Input.OriginX;
            float originY = (float)plan.Input.OriginY;
            float directionX = (float)plan.Input.DirectionX;
            float directionY = (float)plan.Input.DirectionY;

            origin = new Vector2(originX, originY);
            direction = new Vector2(directionX, directionY);

            return IsFinite(originX)
                && IsFinite(originY)
                && IsFinite(directionX)
                && IsFinite(directionY)
                && direction.sqrMagnitude > 0f;
        }

        private static bool IsValidEntry(
            GunFireExecutionOperationEntry entry,
            int expectedIndex)
        {
            return entry != null
                && entry.SourceModuleId != null
                && entry.Operation != null
                && entry.OperationKindId != null
                && entry.OperationId != null
                && entry.PlanOperationIndex == expectedIndex
                && entry.ModuleOperationIndex >= 0
                && entry.OperationKindId == entry.Operation.OperationKindId
                && entry.OperationId == entry.Operation.OperationId;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static GunMountExecutionResult Result(
            GunMountExecutionStatus status,
            GunFireExecutionPlan plan,
            int executedCount,
            int failedIndex,
            StableId failedOperationId)
        {
            return new GunMountExecutionResult(
                status,
                plan == null ? null : plan.DeterministicIdentity,
                executedCount,
                failedIndex,
                failedOperationId);
        }
    }
}

using System;
using System.Collections.Generic;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Combat
{
    public enum WeaponMount2DExecutionStatus
    {
        Executed = 1,
        Disabled = 2,
        NotConfigured = 3,
        InvalidPlan = 4,
        InvalidPhysicsScene = 5,
        MissingHandler = 6,
        HandlerRejected = 7,
        HandlerFaulted = 8,
    }

    /// <summary>
    /// Immutable result from one attempt to apply a validated engine-independent plan.
    /// Failure classifications deliberately omit exception text and scene state.
    /// </summary>
    public sealed class WeaponMount2DExecutionResult
    {
        internal WeaponMount2DExecutionResult(
            WeaponMount2DExecutionStatus status,
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

        public WeaponMount2DExecutionStatus Status { get; }

        public StableId PlanId { get; }

        public int ExecutedOperationCount { get; }

        public int FailedOperationIndex { get; }

        public StableId FailedOperationId { get; }

        public bool Succeeded
        {
            get { return Status == WeaponMount2DExecutionStatus.Executed; }
        }
    }

    /// <summary>
    /// Explicit 2D-only execution context. It exposes no scene search, Transform,
    /// GameObject, Rigidbody, Collider, or three-dimensional physics API.
    /// </summary>
    public sealed class WeaponMount2DExecutionContext
    {
        internal WeaponMount2DExecutionContext(
            PhysicsScene2D physicsScene,
            StableId sourceId,
            StableId combatEventId,
            StableId weaponId,
            StableId mountId,
            StableId planId,
            Vector2 origin,
            Vector2 direction,
            int planOperationIndex)
        {
            PhysicsScene = physicsScene;
            SourceId = sourceId;
            CombatEventId = combatEventId;
            WeaponId = weaponId;
            MountId = mountId;
            PlanId = planId;
            Origin = origin;
            Direction = direction;
            PlanOperationIndex = planOperationIndex;
        }

        public PhysicsScene2D PhysicsScene { get; }

        public StableId SourceId { get; }

        public StableId CombatEventId { get; }

        public StableId WeaponId { get; }

        public StableId MountId { get; }

        public StableId PlanId { get; }

        public Vector2 Origin { get; }

        public Vector2 Direction { get; }

        public int PlanOperationIndex { get; }
    }

    /// <summary>
    /// Explicitly registered bridge for one immutable operation kind. Concrete weapon
    /// packages own their handlers; this common adapter never switches on weapon IDs.
    /// </summary>
    public interface IWeaponFireExecutionOperation2DHandler
    {
        StableId OperationKindId { get; }

        bool TryExecute(
            WeaponFireExecutionOperationEntry operation,
            WeaponMount2DExecutionContext context);
    }

    /// <summary>
    /// Applies canonical CB-004 execution-plan operations in plan order through an
    /// explicit registry of 2D handlers. The component owns no mount simulation,
    /// damage authority, pooling policy, content identity, or scene-wide state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponMount2DAdapter : MonoBehaviour
    {
        private readonly Dictionary<StableId, IWeaponFireExecutionOperation2DHandler>
            handlersByKind =
                new Dictionary<StableId, IWeaponFireExecutionOperation2DHandler>();

        private StableId sourceId;
        private StableId weaponId;
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

        public StableId WeaponId
        {
            get { return weaponId; }
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
            StableId configuredWeaponId,
            StableId configuredMountId,
            IEnumerable<IWeaponFireExecutionOperation2DHandler> handlers)
        {
            if (sourceActorId == null)
            {
                throw new ArgumentNullException(nameof(sourceActorId));
            }

            if (configuredWeaponId == null)
            {
                throw new ArgumentNullException(nameof(configuredWeaponId));
            }

            if (configuredMountId == null)
            {
                throw new ArgumentNullException(nameof(configuredMountId));
            }

            if (handlers == null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            Dictionary<StableId, IWeaponFireExecutionOperation2DHandler> candidate =
                new Dictionary<StableId, IWeaponFireExecutionOperation2DHandler>();
            foreach (IWeaponFireExecutionOperation2DHandler handler in handlers)
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
            foreach (KeyValuePair<StableId, IWeaponFireExecutionOperation2DHandler> pair
                in candidate)
            {
                handlersByKind.Add(pair.Key, pair.Value);
            }

            sourceId = sourceActorId;
            weaponId = configuredWeaponId;
            mountId = configuredMountId;
            isConfigured = true;
        }

        public void ClearConfiguration()
        {
            handlersByKind.Clear();
            sourceId = null;
            weaponId = null;
            mountId = null;
            isConfigured = false;
        }

        /// <summary>
        /// Applies one already validated CB-004 plan. All plan metadata and handler
        /// availability are preflighted before the first handler is invoked.
        /// </summary>
        public WeaponMount2DExecutionResult ExecutePlan(WeaponFireExecutionPlan plan)
        {
            if (!isActiveAndEnabled)
            {
                return Result(WeaponMount2DExecutionStatus.Disabled, plan, 0, -1, null);
            }

            if (!isConfigured)
            {
                return Result(WeaponMount2DExecutionStatus.NotConfigured, plan, 0, -1, null);
            }

            if (!TryValidatePlanEnvelope(plan))
            {
                return Result(WeaponMount2DExecutionStatus.InvalidPlan, plan, 0, -1, null);
            }

            Vector2 origin;
            Vector2 direction;
            if (!TryConvertGeometry(plan, out origin, out direction))
            {
                return Result(WeaponMount2DExecutionStatus.InvalidPlan, plan, 0, -1, null);
            }

            IWeaponFireExecutionOperation2DHandler[] orderedHandlers =
                new IWeaponFireExecutionOperation2DHandler[plan.OperationCount];
            WeaponFireExecutionOperationEntry[] orderedOperations =
                new WeaponFireExecutionOperationEntry[plan.OperationCount];

            for (int index = 0; index < plan.OperationCount; index++)
            {
                WeaponFireExecutionOperationEntry entry;
                try
                {
                    entry = plan.GetOperation(index);
                }
                catch (Exception)
                {
                    return Result(
                        WeaponMount2DExecutionStatus.InvalidPlan,
                        plan,
                        0,
                        index,
                        null);
                }

                if (!IsValidEntry(entry, index))
                {
                    return Result(
                        WeaponMount2DExecutionStatus.InvalidPlan,
                        plan,
                        0,
                        index,
                        entry == null ? null : entry.OperationId);
                }

                IWeaponFireExecutionOperation2DHandler handler;
                if (!handlersByKind.TryGetValue(entry.OperationKindId, out handler))
                {
                    return Result(
                        WeaponMount2DExecutionStatus.MissingHandler,
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
                    WeaponMount2DExecutionStatus.InvalidPhysicsScene,
                    plan,
                    0,
                    -1,
                    null);
            }

            int executedCount = 0;
            for (int index = 0; index < orderedOperations.Length; index++)
            {
                WeaponFireExecutionOperationEntry entry = orderedOperations[index];
                WeaponMount2DExecutionContext context = new WeaponMount2DExecutionContext(
                    physicsScene,
                    sourceId,
                    plan.CombatEventId,
                    plan.WeaponId,
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
                        WeaponMount2DExecutionStatus.HandlerFaulted,
                        plan,
                        executedCount,
                        index,
                        entry.OperationId);
                }

                if (!accepted)
                {
                    return Result(
                        WeaponMount2DExecutionStatus.HandlerRejected,
                        plan,
                        executedCount,
                        index,
                        entry.OperationId);
                }

                executedCount++;
            }

            return Result(
                WeaponMount2DExecutionStatus.Executed,
                plan,
                executedCount,
                -1,
                null);
        }

        private bool TryValidatePlanEnvelope(WeaponFireExecutionPlan plan)
        {
            return plan != null
                && plan.PlanVersion == WeaponFireExecutionPlan.CurrentPlanVersion
                && plan.Input != null
                && plan.Input.RuntimeProfile != null
                && plan.CombatEventId != null
                && plan.WeaponId == weaponId
                && plan.MountId == mountId
                && plan.DeterministicIdentity != null
                && !string.IsNullOrEmpty(plan.Fingerprint)
                && plan.OperationCount >= 0
                && plan.OperationCount <= WeaponFireExecutionPlan.MaximumOperationCount;
        }

        private static bool TryConvertGeometry(
            WeaponFireExecutionPlan plan,
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
            WeaponFireExecutionOperationEntry entry,
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

        private static WeaponMount2DExecutionResult Result(
            WeaponMount2DExecutionStatus status,
            WeaponFireExecutionPlan plan,
            int executedCount,
            int failedIndex,
            StableId failedOperationId)
        {
            return new WeaponMount2DExecutionResult(
                status,
                plan == null ? null : plan.DeterministicIdentity,
                executedCount,
                failedIndex,
                failedOperationId);
        }
    }
}

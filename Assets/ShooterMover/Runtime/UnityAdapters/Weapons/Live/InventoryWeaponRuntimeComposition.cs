using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    public enum InventoryWeaponSlotSelectionStatus
    {
        Selected = 1,
        ExactDuplicateNoChange = 2,
        InvalidSlot = 3,
    }

    /// <summary>
    /// Legacy active-slot projection retained for callers outside the production mount path.
    /// Production gameplay uses the mounted constructor below and does not switch.
    /// </summary>
    public sealed class RouteProfileActiveWeaponSource :
        IActiveWeaponEquipmentInstanceSource
    {
        private readonly PlayerRouteProfilePayloadV1 routeProfile;
        private int selectedSlotIndex;

        public RouteProfileActiveWeaponSource(
            PlayerRouteProfilePayloadV1 profile,
            int initialSlotIndex = 0)
        {
            routeProfile = profile
                ?? throw new ArgumentNullException(nameof(profile));
            if (routeProfile.WeaponSlots == null
                || routeProfile.WeaponSlots.Count
                    != PlayerRouteProfilePayloadV1.WeaponSlotCount)
            {
                throw new ArgumentException(
                    "The route profile must contain four position records.",
                    nameof(profile));
            }
            if (initialSlotIndex < 0
                || initialSlotIndex
                    >= PlayerRouteProfilePayloadV1.WeaponSlotCount
                || routeProfile.WeaponSlots[initialSlotIndex]
                    .EquipmentInstanceStableId == null)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialSlotIndex));
            }
            selectedSlotIndex = initialSlotIndex;
        }

        public int SelectedSlotIndex
        {
            get { return selectedSlotIndex; }
        }

        public EquipmentInstanceId SelectedEquipmentInstanceId
        {
            get
            {
                StableId stableId = routeProfile
                    .WeaponSlots[selectedSlotIndex]
                    .EquipmentInstanceStableId;
                return stableId == null
                    ? null
                    : new EquipmentInstanceId(stableId);
            }
        }

        public InventoryWeaponSlotSelectionStatus SelectSlot(int slotIndex)
        {
            if (slotIndex < 0
                || slotIndex
                    >= PlayerRouteProfilePayloadV1.WeaponSlotCount
                || routeProfile.WeaponSlots[slotIndex]
                    .EquipmentInstanceStableId == null)
            {
                return InventoryWeaponSlotSelectionStatus.InvalidSlot;
            }
            if (slotIndex == selectedSlotIndex)
            {
                return InventoryWeaponSlotSelectionStatus
                    .ExactDuplicateNoChange;
            }
            selectedSlotIndex = slotIndex;
            return InventoryWeaponSlotSelectionStatus.Selected;
        }

        public bool TryResolveActiveEquipmentInstance(
            WeaponActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration,
            out EquipmentInstanceId equipmentInstanceId)
        {
            equipmentInstanceId = actorId == null
                    || lifecycleGeneration == null
                ? null
                : SelectedEquipmentInstanceId;
            return equipmentInstanceId != null;
        }
    }

    /// <summary>
    /// One currently enabled physical mount. It carries only position, exact equipment identity,
    /// and the muzzle's lateral offset. Activation policy remains upstream.
    /// </summary>
    public sealed class InventoryWeaponMountedRuntimeV1
    {
        public InventoryWeaponMountedRuntimeV1(
            StableId mountStableId,
            EquipmentInstanceId equipmentInstanceId,
            double lateralOffset)
        {
            MountStableId = mountStableId
                ?? throw new ArgumentNullException(nameof(mountStableId));
            EquipmentInstanceId = equipmentInstanceId
                ?? throw new ArgumentNullException(
                    nameof(equipmentInstanceId));
            if (double.IsNaN(lateralOffset)
                || double.IsInfinity(lateralOffset))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lateralOffset));
            }
            LateralOffset = lateralOffset;
        }

        public StableId MountStableId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public double LateralOffset { get; }
    }

    /// <summary>
    /// Sole live owner of scheduler state, downstream pending delivery, and input-edge state for one
    /// actor lifecycle. Scheduling and pending admission publish together under firingStateGate;
    /// sink delivery happens only after that publication and only for entries due at the supplied tick.
    /// </summary>
    public sealed class InventoryWeaponRuntimeComposition : IDisposable
    {
        private readonly object firingStateGate = new object();
        private readonly IInventoryWeaponActorStateSource actorStateSource;
        private readonly InventoryWeaponFireIntentFactory intentFactory;
        private readonly InventoryBackedWeaponExecutionAdapter executionAdapter;
        private readonly RouteProfileActiveWeaponSource activeWeaponSource;
        private readonly ReadOnlyCollection<InventoryWeaponMountedRuntimeV1>
            mountedWeapons;
        private WeaponFiringSessionState firingSessionState;
        private InventoryWeaponPendingDeliveryState pendingDeliveryState;
        private InventoryWeaponTriggerEdgeState triggerEdgeState;
        private WeaponActorInstanceId activeActorId;
        private LifecycleGeneration activeLifecycleGeneration;
        private bool disposed;

        public InventoryWeaponRuntimeComposition(
            IInventoryWeaponActorStateSource actorState,
            RouteProfileActiveWeaponSource activeWeapon,
            InventoryBackedWeaponExecutionAdapter adapter)
        {
            actorStateSource = actorState
                ?? throw new ArgumentNullException(nameof(actorState));
            activeWeaponSource = activeWeapon
                ?? throw new ArgumentNullException(nameof(activeWeapon));
            executionAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            intentFactory = new InventoryWeaponFireIntentFactory(
                activeWeaponSource);
            mountedWeapons = new ReadOnlyCollection<
                InventoryWeaponMountedRuntimeV1>(
                new List<InventoryWeaponMountedRuntimeV1>());
            firingSessionState = WeaponFiringSessionState.Empty;
            pendingDeliveryState = InventoryWeaponPendingDeliveryState.Empty;
            triggerEdgeState = InventoryWeaponTriggerEdgeState.Empty;
        }

        public InventoryWeaponRuntimeComposition(
            IInventoryWeaponActorStateSource actorState,
            IEnumerable<InventoryWeaponMountedRuntimeV1> enabledMounts,
            InventoryBackedWeaponExecutionAdapter adapter)
        {
            actorStateSource = actorState
                ?? throw new ArgumentNullException(nameof(actorState));
            executionAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            var mounts = new List<InventoryWeaponMountedRuntimeV1>(
                enabledMounts
                ?? throw new ArgumentNullException(nameof(enabledMounts)));
            if (mounts.Count < 1 || mounts.Count > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(enabledMounts));
            }

            var mountIds = new HashSet<StableId>();
            var equipmentIds = new HashSet<StableId>();
            for (int index = 0; index < mounts.Count; index++)
            {
                InventoryWeaponMountedRuntimeV1 mount = mounts[index];
                if (mount == null
                    || !mountIds.Add(mount.MountStableId)
                    || !equipmentIds.Add(
                        mount.EquipmentInstanceId.Value))
                {
                    throw new ArgumentException(
                        "Enabled mounts and exact equipment instances must be unique.",
                        nameof(enabledMounts));
                }
            }

            mountedWeapons = new ReadOnlyCollection<
                InventoryWeaponMountedRuntimeV1>(mounts);
            firingSessionState = WeaponFiringSessionState.Empty;
            pendingDeliveryState = InventoryWeaponPendingDeliveryState.Empty;
            triggerEdgeState = InventoryWeaponTriggerEdgeState.Empty;
        }

        public bool IsConcurrentMountMode
        {
            get { return mountedWeapons.Count > 0; }
        }

        public int EnabledMountCount
        {
            get
            {
                return IsConcurrentMountMode
                    ? mountedWeapons.Count
                    : 1;
            }
        }

        public IReadOnlyList<InventoryWeaponMountedRuntimeV1> EnabledMounts
        {
            get { return mountedWeapons; }
        }

        public WeaponFiringSessionState FiringSessionState
        {
            get
            {
                lock (firingStateGate)
                {
                    return firingSessionState;
                }
            }
        }

        public InventoryWeaponPendingDeliveryState PendingDeliveryState
        {
            get
            {
                lock (firingStateGate)
                {
                    return pendingDeliveryState;
                }
            }
        }

        public InventoryWeaponTriggerEdgeState TriggerEdgeState
        {
            get
            {
                lock (firingStateGate)
                {
                    return triggerEdgeState;
                }
            }
        }

        public int SelectedSlotIndex
        {
            get
            {
                return activeWeaponSource == null
                    ? 0
                    : activeWeaponSource.SelectedSlotIndex;
            }
        }

        public InventoryWeaponSlotSelectionStatus SelectSlot(int slotIndex)
        {
            if (activeWeaponSource == null)
            {
                return slotIndex >= 0
                        && slotIndex
                            < PlayerRouteProfilePayloadV1.WeaponSlotCount
                    ? InventoryWeaponSlotSelectionStatus
                        .ExactDuplicateNoChange
                    : InventoryWeaponSlotSelectionStatus.InvalidSlot;
            }
            return activeWeaponSource.SelectSlot(slotIndex);
        }

        [Obsolete(
            "One-shot Pressed compatibility only. Live input must supply explicit trigger state.",
            false)]
        public bool TryCreateFireIntent(
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection,
            out InventoryWeaponFireRequest request,
            out string rejectionCode)
        {
            return TryCreateFireIntent(
                fireOperationId,
                simulationTick,
                deterministicSeed,
                origin,
                aimDirection,
                WeaponTriggerSignal.Pressed,
                out request,
                out rejectionCode);
        }

        public bool TryCreateFireIntent(
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection,
            WeaponTriggerSignal triggerSignal,
            out InventoryWeaponFireRequest request,
            out string rejectionCode)
        {
            request = null;
            WeaponActorInstanceId actorId;
            LifecycleGeneration generation;
            lock (firingStateGate)
            {
                if (!TryResolveAndActivateCurrentLifecycleLocked(
                        out actorId,
                        out generation,
                        out rejectionCode))
                {
                    return false;
                }
            }

            if (!IsConcurrentMountMode)
            {
                return intentFactory.TryCreate(
                    actorId,
                    fireOperationId,
                    generation,
                    simulationTick,
                    deterministicSeed,
                    origin,
                    aimDirection,
                    triggerSignal,
                    out request,
                    out rejectionCode);
            }

            if (fireOperationId == null)
            {
                rejectionCode = "weapon-live-intent-invalid";
                return false;
            }

            InventoryWeaponMountedRuntimeV1 first = mountedWeapons[0];
            request = CreateMountedRequest(
                actorId,
                generation,
                first,
                0,
                fireOperationId,
                simulationTick,
                deterministicSeed,
                origin,
                aimDirection,
                triggerSignal);
            rejectionCode = string.Empty;
            return true;
        }

        public InventoryWeaponExecutionResult TryExecute(
            InventoryWeaponFireRequest request)
        {
            lock (firingStateGate)
            {
                if (!ValidateRequestLifecycleLocked(request, out string rejectionCode))
                {
                    return Reject(rejectionCode);
                }

                InventoryWeaponExecutionResult scheduled =
                    ScheduleAndPublishLocked(request);
                InventoryWeaponExecutionResult drained =
                    DrainDueLocked(request.SimulationTick);
                return CombineScheduledAndDrained(
                    new[] { scheduled },
                    drained);
            }
        }

        /// <summary>
        /// One-shot source compatibility only. Repeated automatic input must use UpdateTriggerInput
        /// or explicit TryTrigger edges; this method owns no cadence and always means one Pressed edge.
        /// </summary>
        [Obsolete(
            "One-shot Pressed compatibility only. Use UpdateTriggerInput for live held input.",
            false)]
        public InventoryWeaponExecutionResult TryFire(
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection)
        {
            return TryTrigger(
                fireOperationId,
                simulationTick,
                deterministicSeed,
                origin,
                aimDirection,
                WeaponTriggerSignal.Pressed);
        }

        /// <summary>
        /// Lower-level explicit edge API for a caller that already owns input-edge classification.
        /// Every enabled mount receives the same signal with deterministic per-mount operation IDs.
        /// </summary>
        public InventoryWeaponExecutionResult TryTrigger(
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection,
            WeaponTriggerSignal triggerSignal)
        {
            lock (firingStateGate)
            {
                WeaponActorInstanceId actorId;
                LifecycleGeneration generation;
                string rejectionCode;
                if (!TryResolveAndActivateCurrentLifecycleLocked(
                        out actorId,
                        out generation,
                        out rejectionCode)
                    || fireOperationId == null
                    || !Enum.IsDefined(
                        typeof(WeaponTriggerSignal),
                        triggerSignal))
                {
                    return Reject(
                        string.IsNullOrWhiteSpace(rejectionCode)
                            ? "weapon-live-trigger-invalid"
                            : rejectionCode);
                }

                return TriggerAllMountsAndDrainLocked(
                    actorId,
                    generation,
                    fireOperationId,
                    simulationTick,
                    deterministicSeed,
                    origin,
                    aimDirection,
                    triggerSignal);
            }
        }

        /// <summary>
        /// Input-facing live API. Physical held state becomes Pressed, Held, Released, or no
        /// scheduler request. The caller supplies one deterministic operation ID for each scheduler
        /// request; an exact retry reuses the same ID and exact input facts. Idle ticks still drain.
        /// The repository currently exposes this API honestly; no Unity scene/input hook is created
        /// by this architecture-only PR.
        /// </summary>
        public InventoryWeaponExecutionResult UpdateTriggerInput(
            bool isHeld,
            FireOperationId inputOperationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection)
        {
            lock (firingStateGate)
            {
                WeaponActorInstanceId actorId;
                LifecycleGeneration generation;
                string rejectionCode;
                if (!TryResolveAndActivateCurrentLifecycleLocked(
                        out actorId,
                        out generation,
                        out rejectionCode))
                {
                    return Reject(rejectionCode);
                }

                InventoryWeaponTriggerEdgeDecision edge = triggerEdgeState.Resolve(
                    isHeld,
                    inputOperationId,
                    simulationTick,
                    deterministicSeed,
                    origin,
                    aimDirection);
                if (edge == null || !edge.Succeeded)
                {
                    return Reject(
                        edge == null
                            ? "weapon-live-trigger-edge-null-result"
                            : edge.RejectionCode);
                }

                triggerEdgeState = edge.NextState;
                if (!edge.HasSchedulerRequest)
                {
                    return DrainDueLocked(simulationTick);
                }

                return TriggerAllMountsAndDrainLocked(
                    actorId,
                    generation,
                    inputOperationId,
                    simulationTick,
                    deterministicSeed,
                    origin,
                    aimDirection,
                    edge.TriggerSignal.Value);
            }
        }

        /// <summary>
        /// Advances downstream delivery independently of trigger transitions. This must be called
        /// every simulation tick by the eventual gameplay loop so accepted burst/pulse emissions and
        /// retryable sink failures continue even while input is idle or released.
        /// </summary>
        public InventoryWeaponExecutionResult Advance(long simulationTick)
        {
            lock (firingStateGate)
            {
                WeaponActorInstanceId actorId;
                LifecycleGeneration generation;
                string rejectionCode;
                if (simulationTick < 0L
                    || !TryResolveAndActivateCurrentLifecycleLocked(
                        out actorId,
                        out generation,
                        out rejectionCode))
                {
                    return Reject(
                        string.IsNullOrWhiteSpace(rejectionCode)
                            ? "weapon-live-advance-invalid"
                            : rejectionCode);
                }

                return DrainDueLocked(simulationTick);
            }
        }

        public InventoryWeaponExecutionResult DrainDueEmissions(
            long simulationTick)
        {
            return Advance(simulationTick);
        }

        public void Dispose()
        {
            lock (firingStateGate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                ClearLifecycleStateLocked();
                activeActorId = null;
                activeLifecycleGeneration = null;
            }
        }

        private InventoryWeaponExecutionResult TriggerAllMountsAndDrainLocked(
            WeaponActorInstanceId actorId,
            LifecycleGeneration generation,
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection,
            WeaponTriggerSignal triggerSignal)
        {
            var scheduledResults = new List<InventoryWeaponExecutionResult>();
            if (!IsConcurrentMountMode)
            {
                InventoryWeaponFireRequest request;
                string rejectionCode;
                if (!intentFactory.TryCreate(
                        actorId,
                        fireOperationId,
                        generation,
                        simulationTick,
                        deterministicSeed,
                        origin,
                        aimDirection,
                        triggerSignal,
                        out request,
                        out rejectionCode))
                {
                    scheduledResults.Add(Reject(rejectionCode));
                }
                else
                {
                    scheduledResults.Add(ScheduleAndPublishLocked(request));
                }
            }
            else
            {
                for (int index = 0; index < mountedWeapons.Count; index++)
                {
                    InventoryWeaponFireRequest request = CreateMountedRequest(
                        actorId,
                        generation,
                        mountedWeapons[index],
                        index,
                        fireOperationId,
                        simulationTick,
                        deterministicSeed,
                        origin,
                        aimDirection,
                        triggerSignal);
                    scheduledResults.Add(ScheduleAndPublishLocked(request));
                }
            }

            InventoryWeaponExecutionResult drained =
                DrainDueLocked(simulationTick);
            return CombineScheduledAndDrained(scheduledResults, drained);
        }

        private InventoryWeaponExecutionResult ScheduleAndPublishLocked(
            InventoryWeaponFireRequest request)
        {
            InventoryWeaponExecutionTransition transition =
                executionAdapter.TryExecute(
                    request,
                    firingSessionState,
                    pendingDeliveryState);
            if (transition == null)
            {
                return Reject("weapon-live-transition-null-result");
            }

            if (transition.PublishStatePair)
            {
                // Both immutable snapshots become authoritative together under the same lock.
                firingSessionState = transition.NextFiringState;
                pendingDeliveryState = transition.NextPendingState;
            }
            return transition.Result;
        }

        private InventoryWeaponExecutionResult DrainDueLocked(
            long simulationTick)
        {
            var delivered = new List<InventoryWeaponEffectBatch>();
            int acceptedCount = 0;
            int alreadyAcceptedCount = 0;
            EquipmentInstanceId firstEquipmentInstanceId = null;

            InventoryWeaponPendingDeliveryEntry entry;
            while (pendingDeliveryState.TryPeekDue(simulationTick, out entry))
            {
                InventoryWeaponPendingDeliveryAttempt attempt =
                    executionAdapter.TryDeliverPending(entry);
                if (attempt == null || !attempt.Succeeded)
                {
                    return InventoryWeaponExecutionResult.Reject(
                        entry.EquipmentInstanceId,
                        WeaponExecutionStatus.SinkRejected,
                        attempt == null
                            ? "weapon-live-retryable-delivery-null-result"
                            : attempt.RejectionCode,
                        false,
                        true,
                        pendingDeliveryState.PendingCount,
                        delivered,
                        acceptedCount,
                        alreadyAcceptedCount);
                }

                if (firstEquipmentInstanceId == null)
                {
                    firstEquipmentInstanceId = entry.EquipmentInstanceId;
                }
                delivered.Add(entry.ProjectedBatch);
                if (attempt.WasAlreadyAccepted)
                {
                    alreadyAcceptedCount++;
                }
                else
                {
                    acceptedCount++;
                }

                try
                {
                    // Removal happens only after Accepted or exact AlreadyAccepted.
                    pendingDeliveryState = pendingDeliveryState.MarkDelivered(entry);
                }
                catch
                {
                    // The sink may already have accepted the batch. Retaining the entry is safe:
                    // the next attempt must return exact AlreadyAccepted before removal.
                    return InventoryWeaponExecutionResult.Reject(
                        entry.EquipmentInstanceId,
                        WeaponExecutionStatus.SinkRejected,
                        "weapon-live-retryable-pending-commit-failed",
                        false,
                        true,
                        pendingDeliveryState.PendingCount,
                        delivered,
                        acceptedCount,
                        alreadyAcceptedCount);
                }
            }

            if (delivered.Count == 0)
            {
                return InventoryWeaponExecutionResult.NoDue(
                    pendingDeliveryState.PendingCount);
            }

            return InventoryWeaponExecutionResult.Delivery(
                firstEquipmentInstanceId,
                delivered,
                acceptedCount,
                alreadyAcceptedCount,
                pendingDeliveryState.PendingCount);
        }

        private InventoryWeaponExecutionResult CombineScheduledAndDrained(
            IList<InventoryWeaponExecutionResult> scheduledResults,
            InventoryWeaponExecutionResult drained)
        {
            InventoryWeaponExecutionResult firstFailure = null;
            int totalScheduledEmissions = 0;
            bool anyNewSchedule = false;
            bool anyReplaySchedule = false;
            InventoryWeaponExecutionResult firstTransition = null;

            for (int index = 0; index < scheduledResults.Count; index++)
            {
                InventoryWeaponExecutionResult result = scheduledResults[index];
                if (result == null)
                {
                    if (firstFailure == null)
                    {
                        firstFailure = Reject("weapon-live-mount-result-null");
                    }
                    continue;
                }
                if (!result.Succeeded && firstFailure == null)
                {
                    firstFailure = result;
                }
                if (result.OutcomeKind
                    == InventoryWeaponExecutionOutcomeKind.AcceptedScheduleQueued)
                {
                    anyNewSchedule = true;
                    totalScheduledEmissions += result.ScheduledEmissionCount;
                }
                else if (result.OutcomeKind
                    == InventoryWeaponExecutionOutcomeKind.ReplayedScheduleRetained)
                {
                    anyReplaySchedule = true;
                    totalScheduledEmissions += result.ScheduledEmissionCount;
                }
                else if (firstTransition == null && result.Succeeded)
                {
                    firstTransition = result;
                }
            }

            if (drained != null
                && drained.OutcomeKind
                    == InventoryWeaponExecutionOutcomeKind.RetryableDeliveryFailure)
            {
                return drained;
            }

            if (firstFailure != null)
            {
                return InventoryWeaponExecutionResult.Reject(
                    firstFailure.EquipmentInstanceId,
                    firstFailure.Status,
                    firstFailure.RejectionCode,
                    firstFailure.OutcomeKind
                        == InventoryWeaponExecutionOutcomeKind.SchedulerRejected,
                    false,
                    pendingDeliveryState.PendingCount,
                    drained == null ? null : drained.DeliveredBatches,
                    drained == null ? 0 : drained.AcceptedDeliveryCount,
                    drained == null ? 0 : drained.AlreadyAcceptedDeliveryCount);
            }

            if (drained != null && drained.DeliveredBatchCount > 0)
            {
                return drained;
            }

            if (anyNewSchedule || anyReplaySchedule)
            {
                return InventoryWeaponExecutionResult.Schedule(
                    null,
                    !anyNewSchedule && anyReplaySchedule,
                    totalScheduledEmissions,
                    pendingDeliveryState.PendingCount);
            }

            return firstTransition
                ?? drained
                ?? InventoryWeaponExecutionResult.NoDue(
                    pendingDeliveryState.PendingCount);
        }

        private bool ValidateRequestLifecycleLocked(
            InventoryWeaponFireRequest request,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (disposed)
            {
                rejectionCode = "weapon-live-runtime-disposed";
                return false;
            }
            if (request == null)
            {
                rejectionCode = "weapon-live-request-invalid";
                return false;
            }

            WeaponActorInstanceId currentActorId;
            LifecycleGeneration currentLifecycleGeneration;
            if (!actorStateSource.TryResolveActorState(
                    out currentActorId,
                    out currentLifecycleGeneration)
                || currentActorId == null
                || currentLifecycleGeneration == null
                || !currentActorId.Equals(request.ActorId)
                || !currentLifecycleGeneration.Equals(
                    request.LifecycleGeneration))
            {
                // A stale request must not clear the current lifecycle's state.
                rejectionCode = "weapon-live-request-lifecycle-mismatch";
                return false;
            }

            ActivateLifecycleLocked(currentActorId, currentLifecycleGeneration);
            return true;
        }

        private bool TryResolveAndActivateCurrentLifecycleLocked(
            out WeaponActorInstanceId actorId,
            out LifecycleGeneration generation,
            out string rejectionCode)
        {
            actorId = null;
            generation = null;
            if (disposed
                || !actorStateSource.TryResolveActorState(
                    out actorId,
                    out generation)
                || actorId == null
                || generation == null)
            {
                rejectionCode = disposed
                    ? "weapon-live-runtime-disposed"
                    : "weapon-live-actor-state-unresolved";
                return false;
            }

            ActivateLifecycleLocked(actorId, generation);
            rejectionCode = string.Empty;
            return true;
        }

        private void ActivateLifecycleLocked(
            WeaponActorInstanceId actorId,
            LifecycleGeneration generation)
        {
            if (activeActorId == null
                || activeLifecycleGeneration == null
                || !activeActorId.Equals(actorId)
                || !activeLifecycleGeneration.Equals(generation))
            {
                ClearLifecycleStateLocked();
                activeActorId = actorId;
                activeLifecycleGeneration = generation;
            }
        }

        private void ClearLifecycleStateLocked()
        {
            firingSessionState = WeaponFiringSessionState.Empty;
            pendingDeliveryState = InventoryWeaponPendingDeliveryState.Empty;
            triggerEdgeState = InventoryWeaponTriggerEdgeState.Empty;
        }

        private static InventoryWeaponFireRequest CreateMountedRequest(
            WeaponActorInstanceId actorId,
            LifecycleGeneration generation,
            InventoryWeaponMountedRuntimeV1 mount,
            int mountOrdinal,
            FireOperationId baseOperationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection,
            WeaponTriggerSignal triggerSignal)
        {
            double length = Math.Sqrt(
                (aimDirection.X * aimDirection.X)
                + (aimDirection.Y * aimDirection.Y));
            double normalizedX = length <= 0.0000001d
                ? 0d
                : aimDirection.X / length;
            double normalizedY = length <= 0.0000001d
                ? 1d
                : aimDirection.Y / length;
            double perpendicularX = -normalizedY;
            double perpendicularY = normalizedX;
            var mountOrigin = new WeaponVector2(
                origin.X + (perpendicularX * mount.LateralOffset),
                origin.Y + (perpendicularY * mount.LateralOffset));
            string operationFingerprint =
                WeaponExecutionFingerprint.Compute(
                    baseOperationId
                    + "|"
                    + mount.MountStableId);
            var operationId = new FireOperationId(
                StableId.Create(
                    "fire-operation",
                    operationFingerprint.Substring(
                        WeaponExecutionFingerprint.Prefix.Length)));
            ulong mountSeed = deterministicSeed
                ^ (unchecked((ulong)(mountOrdinal + 1))
                    * 11400714819323198485UL);

            return new InventoryWeaponFireRequest(
                actorId,
                mount.EquipmentInstanceId,
                operationId,
                generation,
                simulationTick,
                mountSeed,
                mountOrigin,
                aimDirection,
                triggerSignal);
        }

        private InventoryWeaponExecutionResult Reject(
            string rejectionCode)
        {
            return InventoryWeaponExecutionResult.Reject(
                null,
                WeaponExecutionStatus.InvalidCommand,
                rejectionCode,
                false,
                false,
                pendingDeliveryState == null
                    ? 0
                    : pendingDeliveryState.PendingCount,
                null,
                0,
                0);
        }
    }
}

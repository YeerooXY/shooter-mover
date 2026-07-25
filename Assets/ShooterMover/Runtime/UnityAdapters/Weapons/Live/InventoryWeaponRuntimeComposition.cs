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
                || slotIndex >= PlayerRouteProfilePayloadV1.WeaponSlotCount
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
    /// actor lifecycle. Scheduling, receipt pruning, and pending admission publish together under
    /// firingStateGate. Only this composition selects and submits the exact retained first due entry.
    /// </summary>
    public sealed class InventoryWeaponRuntimeComposition : IDisposable
    {
        private sealed class MountAttempt
        {
            internal MountAttempt(
                StableId mountStableId,
                int mountOrdinal,
                EquipmentInstanceId equipmentInstanceId,
                InventoryWeaponExecutionResult result,
                bool statePairPublished)
            {
                MountStableId = mountStableId;
                MountOrdinal = mountOrdinal;
                EquipmentInstanceId = equipmentInstanceId;
                Result = result ?? throw new ArgumentNullException(nameof(result));
                StatePairPublished = statePairPublished;
            }

            internal StableId MountStableId { get; }
            internal int MountOrdinal { get; }
            internal EquipmentInstanceId EquipmentInstanceId { get; }
            internal InventoryWeaponExecutionResult Result { get; }
            internal bool StatePairPublished { get; }
        }

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
            get { return IsConcurrentMountMode ? mountedWeapons.Count : 1; }
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
                        && slotIndex < PlayerRouteProfilePayloadV1.WeaponSlotCount
                    ? InventoryWeaponSlotSelectionStatus.ExactDuplicateNoChange
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
                string rejectionCode;
                if (!ValidateRequestLifecycleLocked(request, out rejectionCode))
                {
                    return Reject(rejectionCode);
                }

                MountAttempt scheduled = ScheduleAndPublishLocked(
                    request,
                    null,
                    0);
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
                    triggerSignal,
                    null,
                    false);
            }
        }

        /// <summary>
        /// Input-facing live API. A candidate physical edge is published only after every mount has
        /// either published its scheduler/pending pair or produced an exact canonical replay.
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

                if (!edge.HasSchedulerRequest)
                {
                    triggerEdgeState = edge.NextState;
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
                    edge.TriggerSignal.Value,
                    edge.NextState,
                    true);
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
                return simulationTick < 0L
                    ? Reject("weapon-live-advance-invalid")
                    : DrainDueLocked(simulationTick);
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
            WeaponTriggerSignal triggerSignal,
            InventoryWeaponTriggerEdgeState candidateEdgeState,
            bool publishCandidateEdgeWhenSafe)
        {
            var scheduled = new List<MountAttempt>();
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
                    scheduled.Add(new MountAttempt(
                        null,
                        0,
                        activeWeaponSource == null
                            ? null
                            : activeWeaponSource.SelectedEquipmentInstanceId,
                        Reject(
                            activeWeaponSource == null
                                ? null
                                : activeWeaponSource.SelectedEquipmentInstanceId,
                            WeaponExecutionStatus.InvalidCommand,
                            rejectionCode,
                            false,
                            null),
                        false));
                }
                else
                {
                    scheduled.Add(ScheduleAndPublishLocked(request, null, 0));
                }
            }
            else
            {
                for (int index = 0; index < mountedWeapons.Count; index++)
                {
                    InventoryWeaponMountedRuntimeV1 mount = mountedWeapons[index];
                    try
                    {
                        InventoryWeaponFireRequest request = CreateMountedRequest(
                            actorId,
                            generation,
                            mount,
                            index,
                            fireOperationId,
                            simulationTick,
                            deterministicSeed,
                            origin,
                            aimDirection,
                            triggerSignal);
                        scheduled.Add(ScheduleAndPublishLocked(
                            request,
                            mount.MountStableId,
                            index));
                    }
                    catch (ArgumentException)
                    {
                        scheduled.Add(new MountAttempt(
                            mount.MountStableId,
                            index,
                            mount.EquipmentInstanceId,
                            Reject(
                                mount.EquipmentInstanceId,
                                WeaponExecutionStatus.InvalidCommand,
                                "weapon-live-mounted-request-invalid",
                                false,
                                null),
                            false));
                    }
                    catch (OverflowException)
                    {
                        scheduled.Add(new MountAttempt(
                            mount.MountStableId,
                            index,
                            mount.EquipmentInstanceId,
                            Reject(
                                mount.EquipmentInstanceId,
                                WeaponExecutionStatus.InvalidCommand,
                                "weapon-live-mounted-request-overflow",
                                false,
                                null),
                            false));
                    }
                }
            }

            bool allSafelyRepresented = true;
            for (int index = 0; index < scheduled.Count; index++)
            {
                if (!HasSafeCanonicalRepresentation(scheduled[index]))
                {
                    allSafelyRepresented = false;
                    break;
                }
            }
            if (publishCandidateEdgeWhenSafe && allSafelyRepresented)
            {
                triggerEdgeState = candidateEdgeState
                    ?? throw new InvalidOperationException(
                        "A safe trigger publication requires a candidate edge state.");
            }

            InventoryWeaponExecutionResult drained =
                DrainDueLocked(simulationTick);
            return CombineScheduledAndDrained(scheduled, drained);
        }

        private MountAttempt ScheduleAndPublishLocked(
            InventoryWeaponFireRequest request,
            StableId mountStableId,
            int mountOrdinal)
        {
            InventoryWeaponExecutionTransition transition =
                executionAdapter.TryExecute(
                    request,
                    firingSessionState,
                    pendingDeliveryState);
            if (transition == null)
            {
                return new MountAttempt(
                    mountStableId,
                    mountOrdinal,
                    request == null ? null : request.EquipmentInstanceId,
                    Reject(
                        request == null ? null : request.EquipmentInstanceId,
                        WeaponExecutionStatus.InvalidCommand,
                        "weapon-live-transition-null-result",
                        false,
                        null),
                    false);
            }

            bool statePairPublished = false;
            if (transition.Result.Succeeded)
            {
                InventoryWeaponPendingDeliveryState prunedPending;
                try
                {
                    // Admission has already produced NextPendingState. Receipt pruning is derived
                    // solely from the exact scheduler replay records in NextFiringState.
                    prunedPending = transition.NextPendingState
                        .PruneDeliveredReceipts(transition.NextFiringState);
                }
                catch (ArgumentException)
                {
                    return new MountAttempt(
                        mountStableId,
                        mountOrdinal,
                        request.EquipmentInstanceId,
                        Reject(
                            request.EquipmentInstanceId,
                            WeaponExecutionStatus.InvalidEffectBatch,
                            "weapon-live-delivered-receipt-pruning-invalid",
                            false,
                            transition.Result.SchedulerStatus),
                        false);
                }
                catch (InvalidOperationException)
                {
                    return new MountAttempt(
                        mountStableId,
                        mountOrdinal,
                        request.EquipmentInstanceId,
                        Reject(
                            request.EquipmentInstanceId,
                            WeaponExecutionStatus.InvalidEffectBatch,
                            "weapon-live-delivered-receipt-pruning-failed",
                            false,
                            transition.Result.SchedulerStatus),
                        false);
                }

                statePairPublished = transition.PublishStatePair
                    || !ReferenceEquals(prunedPending, pendingDeliveryState)
                    || !ReferenceEquals(
                        transition.NextFiringState,
                        firingSessionState);
                if (statePairPublished)
                {
                    // The scheduler and outbox snapshots become authoritative together.
                    firingSessionState = transition.NextFiringState;
                    pendingDeliveryState = prunedPending;
                }
            }

            return new MountAttempt(
                mountStableId,
                mountOrdinal,
                request.EquipmentInstanceId,
                transition.Result,
                statePairPublished);
        }

        private InventoryWeaponExecutionResult DrainDueLocked(
            long simulationTick)
        {
            WeaponActorInstanceId actorId;
            LifecycleGeneration generation;
            string lifecycleRejection;
            if (simulationTick < 0L
                || !TryResolveAndActivateCurrentLifecycleLocked(
                    out actorId,
                    out generation,
                    out lifecycleRejection))
            {
                return Reject(
                    string.IsNullOrWhiteSpace(lifecycleRejection)
                        ? "weapon-live-drain-invalid"
                        : lifecycleRejection);
            }

            var delivered = new List<InventoryWeaponEffectBatch>();
            int acceptedCount = 0;
            int alreadyAcceptedCount = 0;
            EquipmentInstanceId firstEquipmentInstanceId = null;

            while (true)
            {
                // Re-resolve immediately before selecting each sink submission. A legitimate
                // lifecycle replacement clears scheduler, pending, receipts, and edge state here.
                if (!TryResolveAndActivateCurrentLifecycleLocked(
                        out actorId,
                        out generation,
                        out lifecycleRejection))
                {
                    return InventoryWeaponExecutionResult.Reject(
                        firstEquipmentInstanceId,
                        WeaponExecutionStatus.InvalidCommand,
                        lifecycleRejection,
                        false,
                        false,
                        pendingDeliveryState.PendingCount,
                        delivered,
                        acceptedCount,
                        alreadyAcceptedCount);
                }

                InventoryWeaponPendingDeliveryEntry entry;
                if (!pendingDeliveryState.TryPeekDue(simulationTick, out entry))
                {
                    break;
                }
                if (entry == null
                    || !entry.ActorId.Equals(actorId)
                    || !entry.LifecycleGeneration.Equals(generation)
                    || activeActorId == null
                    || activeLifecycleGeneration == null
                    || !activeActorId.Equals(actorId)
                    || !activeLifecycleGeneration.Equals(generation))
                {
                    return InventoryWeaponExecutionResult.Reject(
                        entry == null ? null : entry.EquipmentInstanceId,
                        WeaponExecutionStatus.InvalidEffectBatch,
                        "weapon-live-pending-lifecycle-mismatch",
                        false,
                        false,
                        pendingDeliveryState.PendingCount,
                        delivered,
                        acceptedCount,
                        alreadyAcceptedCount);
                }

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
                    // The sink may already have accepted the batch. Retaining it is safe because
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
            IList<MountAttempt> scheduledAttempts,
            InventoryWeaponExecutionResult drained)
        {
            MountAttempt firstIntegrationFailure = null;
            MountAttempt firstSchedulerFailure = null;
            InventoryWeaponExecutionResult firstTransition = null;
            int totalScheduledEmissions = 0;
            bool anyNewSchedule = false;
            bool anyReplaySchedule = false;
            var schedulingOutcomes = new List<InventoryWeaponSchedulingOutcome>();
            var mountOutcomes = new List<InventoryWeaponMountExecutionOutcome>();

            for (int index = 0; index < scheduledAttempts.Count; index++)
            {
                MountAttempt attempt = scheduledAttempts[index];
                InventoryWeaponExecutionResult result = attempt.Result;
                InventoryWeaponMountExecutionOutcome mountOutcome =
                    CreateMountOutcome(attempt);
                mountOutcomes.Add(mountOutcome);
                totalScheduledEmissions = checked(
                    totalScheduledEmissions
                    + mountOutcome.ScheduledEmissionCount);

                for (int outcomeIndex = 0;
                    outcomeIndex < result.SchedulingOutcomes.Count;
                    outcomeIndex++)
                {
                    schedulingOutcomes.Add(
                        result.SchedulingOutcomes[outcomeIndex]);
                }

                if (mountOutcome.OutcomeKind
                    == InventoryWeaponExecutionOutcomeKind.AcceptedScheduleQueued)
                {
                    anyNewSchedule = true;
                }
                else if (mountOutcome.OutcomeKind
                    == InventoryWeaponExecutionOutcomeKind.ReplayedScheduleRetained)
                {
                    anyReplaySchedule = true;
                }

                if (mountOutcome.Source
                    == InventoryWeaponMountOutcomeSource.IntegrationRejection
                    && firstIntegrationFailure == null)
                {
                    firstIntegrationFailure = attempt;
                }
                else if (mountOutcome.Source
                    == InventoryWeaponMountOutcomeSource.SchedulerRejection
                    && firstSchedulerFailure == null)
                {
                    firstSchedulerFailure = attempt;
                }
                if (firstTransition == null
                    && result.Succeeded
                    && result.IsNoEmissionTransition)
                {
                    firstTransition = result;
                }
            }

            InventoryWeaponExecutionResult combined;
            if (drained != null
                && drained.OutcomeKind
                    == InventoryWeaponExecutionOutcomeKind.RetryableDeliveryFailure)
            {
                combined = drained;
            }
            else if (firstIntegrationFailure != null)
            {
                combined = CombineFailureWithDelivery(
                    firstIntegrationFailure.Result,
                    false,
                    drained);
            }
            else if (firstSchedulerFailure != null)
            {
                combined = CombineFailureWithDelivery(
                    firstSchedulerFailure.Result,
                    true,
                    drained);
            }
            else if (drained != null && drained.DeliveredBatchCount > 0)
            {
                combined = drained;
            }
            else if (anyNewSchedule || anyReplaySchedule)
            {
                combined = InventoryWeaponExecutionResult.Schedule(
                    null,
                    !anyNewSchedule && anyReplaySchedule,
                    totalScheduledEmissions,
                    pendingDeliveryState.PendingCount);
            }
            else
            {
                combined = firstTransition
                    ?? drained
                    ?? InventoryWeaponExecutionResult.NoDue(
                        pendingDeliveryState.PendingCount);
            }

            if (schedulingOutcomes.Count > 0)
            {
                combined = combined.WithSchedulingOutcomes(
                    schedulingOutcomes,
                    totalScheduledEmissions);
            }
            return mountOutcomes.Count == 0
                ? combined
                : combined.WithMountOutcomes(
                    mountOutcomes,
                    totalScheduledEmissions);
        }

        private InventoryWeaponExecutionResult CombineFailureWithDelivery(
            InventoryWeaponExecutionResult failure,
            bool schedulerRejection,
            InventoryWeaponExecutionResult drained)
        {
            return InventoryWeaponExecutionResult.Reject(
                failure.EquipmentInstanceId,
                failure.Status,
                failure.RejectionCode,
                schedulerRejection,
                false,
                pendingDeliveryState.PendingCount,
                drained == null ? null : drained.DeliveredBatches,
                drained == null ? 0 : drained.AcceptedDeliveryCount,
                drained == null ? 0 : drained.AlreadyAcceptedDeliveryCount,
                failure.SchedulerStatus);
        }

        private static InventoryWeaponMountExecutionOutcome CreateMountOutcome(
            MountAttempt attempt)
        {
            InventoryWeaponExecutionResult result = attempt.Result;
            InventoryWeaponMountOutcomeSource source = result.Succeeded
                ? InventoryWeaponMountOutcomeSource.SuccessfulScheduling
                : result.OutcomeKind
                    == InventoryWeaponExecutionOutcomeKind.SchedulerRejected
                    ? InventoryWeaponMountOutcomeSource.SchedulerRejection
                    : InventoryWeaponMountOutcomeSource.IntegrationRejection;
            bool exactReplay = IsExactReplay(result);
            bool retryable = result.OutcomeKind
                    == InventoryWeaponExecutionOutcomeKind.RetryableDeliveryFailure
                || (!result.Succeeded
                    && result.RejectionCode.IndexOf(
                        "pending-capacity-exceeded",
                        StringComparison.Ordinal) >= 0);
            return new InventoryWeaponMountExecutionOutcome(
                attempt.MountStableId,
                attempt.MountOrdinal,
                attempt.EquipmentInstanceId ?? result.EquipmentInstanceId,
                result.OutcomeKind,
                result.Status,
                result.SchedulerStatus,
                result.RejectionCode,
                exactReplay,
                result.ScheduledEmissionCount,
                attempt.StatePairPublished,
                retryable,
                source);
        }

        private static bool HasSafeCanonicalRepresentation(
            MountAttempt attempt)
        {
            return attempt.Result.Succeeded
                && (attempt.StatePairPublished || IsExactReplay(attempt.Result));
        }

        private static bool IsExactReplay(
            InventoryWeaponExecutionResult result)
        {
            return result != null
                && (result.IsExactReplay
                    || result.SchedulerStatus
                        == WeaponFiringScheduleStatus.Replayed);
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
            return Reject(
                null,
                WeaponExecutionStatus.InvalidCommand,
                rejectionCode,
                false,
                null);
        }

        private InventoryWeaponExecutionResult Reject(
            EquipmentInstanceId equipmentInstanceId,
            WeaponExecutionStatus status,
            string rejectionCode,
            bool schedulerRejection,
            WeaponFiringScheduleStatus? schedulerStatus)
        {
            return InventoryWeaponExecutionResult.Reject(
                equipmentInstanceId,
                status,
                rejectionCode,
                schedulerRejection,
                false,
                pendingDeliveryState == null
                    ? 0
                    : pendingDeliveryState.PendingCount,
                null,
                0,
                0,
                schedulerStatus);
        }
    }
}

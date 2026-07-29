using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    public enum InventoryGunSlotSelectionStatus
    {
        Selected = 1,
        ExactDuplicateNoChange = 2,
        InvalidSlot = 3,
    }

    /// <summary>
    /// Legacy active-slot projection retained for callers outside the production mount path.
    /// Production gameplay uses the mounted constructor below and does not switch.
    /// </summary>
    public sealed class RouteProfileActiveGunSource :
        IActiveGunItemSource
    {
        private readonly PlayerRouteProfilePayload routeProfile;
        private int selectedSlotIndex;

        public RouteProfileActiveGunSource(
            PlayerRouteProfilePayload profile,
            int initialSlotIndex = 0)
        {
            routeProfile = profile
                ?? throw new ArgumentNullException(nameof(profile));
            if (routeProfile.GunSlots == null
                || routeProfile.GunSlots.Count
                    != PlayerRouteProfilePayload.GunSlotCount)
            {
                throw new ArgumentException(
                    "The route profile must contain four position records.",
                    nameof(profile));
            }
            if (initialSlotIndex < 0
                || initialSlotIndex
                    >= PlayerRouteProfilePayload.GunSlotCount
                || routeProfile.GunSlots[initialSlotIndex]
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
                    .GunSlots[selectedSlotIndex]
                    .EquipmentInstanceStableId;
                return stableId == null
                    ? null
                    : new EquipmentInstanceId(stableId);
            }
        }

        public InventoryGunSlotSelectionStatus SelectSlot(int slotIndex)
        {
            if (slotIndex < 0
                || slotIndex >= PlayerRouteProfilePayload.GunSlotCount
                || routeProfile.GunSlots[slotIndex]
                    .EquipmentInstanceStableId == null)
            {
                return InventoryGunSlotSelectionStatus.InvalidSlot;
            }
            if (slotIndex == selectedSlotIndex)
            {
                return InventoryGunSlotSelectionStatus
                    .ExactDuplicateNoChange;
            }
            selectedSlotIndex = slotIndex;
            return InventoryGunSlotSelectionStatus.Selected;
        }

        public bool TryResolveActiveEquipmentInstance(
            GunActorInstanceId actorId,
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
    public sealed class InventoryGunMountedLive
    {
        public InventoryGunMountedLive(
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
    /// actor lifecycle. Scheduling, receipt alignment, pending admission, and delivery are serialized
    /// under firingStateGate. Only this composition submits the exact retained first due entry.
    /// </summary>
    public sealed class InventoryGunLiveSetup : IDisposable
    {
        private sealed class MountAttempt
        {
            internal MountAttempt(
                StableId mountStableId,
                int mountOrdinal,
                EquipmentInstanceId equipmentInstanceId,
                InventoryGunExecutionResult result,
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
            internal InventoryGunExecutionResult Result { get; }
            internal bool StatePairPublished { get; }
        }

        private readonly object firingStateGate = new object();
        private readonly IInventoryGunActorStateSource actorStateSource;
        private readonly InventoryGunFireIntentFactory intentFactory;
        private readonly InventoryBackedGunExecutionBridge executionAdapter;
        private readonly RouteProfileActiveGunSource activeGunSource;
        private readonly ReadOnlyCollection<InventoryGunMountedLive>
            mountedGuns;
        private GunFiringSessionState firingSessionState;
        private InventoryGunPendingDeliveryState pendingDeliveryState;
        private InventoryGunTriggerEdgeState triggerEdgeState;
        private GunActorInstanceId activeActorId;
        private LifecycleGeneration activeLifecycleGeneration;
        private bool disposed;

        public InventoryGunLiveSetup(
            IInventoryGunActorStateSource actorState,
            RouteProfileActiveGunSource activeGun,
            InventoryBackedGunExecutionBridge adapter)
        {
            actorStateSource = actorState
                ?? throw new ArgumentNullException(nameof(actorState));
            activeGunSource = activeGun
                ?? throw new ArgumentNullException(nameof(activeGun));
            executionAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            intentFactory = new InventoryGunFireIntentFactory(
                activeGunSource);
            mountedGuns = new ReadOnlyCollection<
                InventoryGunMountedLive>(
                new List<InventoryGunMountedLive>());
            firingSessionState = GunFiringSessionState.Empty;
            pendingDeliveryState = InventoryGunPendingDeliveryState.Empty;
            triggerEdgeState = InventoryGunTriggerEdgeState.Empty;
        }

        public InventoryGunLiveSetup(
            IInventoryGunActorStateSource actorState,
            IEnumerable<InventoryGunMountedLive> enabledMounts,
            InventoryBackedGunExecutionBridge adapter)
        {
            actorStateSource = actorState
                ?? throw new ArgumentNullException(nameof(actorState));
            executionAdapter = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
            activeGunSource = null;
            intentFactory = null;

            var mounts = new List<InventoryGunMountedLive>(
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
                InventoryGunMountedLive mount = mounts[index];
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

            mountedGuns = new ReadOnlyCollection<
                InventoryGunMountedLive>(mounts);
            firingSessionState = GunFiringSessionState.Empty;
            pendingDeliveryState = InventoryGunPendingDeliveryState.Empty;
            triggerEdgeState = InventoryGunTriggerEdgeState.Empty;
        }

        public bool IsConcurrentMountMode
        {
            get { return mountedGuns.Count > 0; }
        }

        public int EnabledMountCount
        {
            get { return IsConcurrentMountMode ? mountedGuns.Count : 1; }
        }

        public IReadOnlyList<InventoryGunMountedLive> EnabledMounts
        {
            get { return mountedGuns; }
        }

        public GunFiringSessionState FiringSessionState
        {
            get
            {
                lock (firingStateGate)
                {
                    return firingSessionState;
                }
            }
        }

        public InventoryGunPendingDeliveryState PendingDeliveryState
        {
            get
            {
                lock (firingStateGate)
                {
                    return pendingDeliveryState;
                }
            }
        }

        public InventoryGunTriggerEdgeState TriggerEdgeState
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
                return activeGunSource == null
                    ? 0
                    : activeGunSource.SelectedSlotIndex;
            }
        }

        public InventoryGunSlotSelectionStatus SelectSlot(int slotIndex)
        {
            if (activeGunSource == null)
            {
                return slotIndex >= 0
                        && slotIndex < PlayerRouteProfilePayload.GunSlotCount
                    ? InventoryGunSlotSelectionStatus.ExactDuplicateNoChange
                    : InventoryGunSlotSelectionStatus.InvalidSlot;
            }
            return activeGunSource.SelectSlot(slotIndex);
        }

        [Obsolete(
            "One-shot Pressed compatibility only. Live input must supply explicit trigger state.",
            false)]
        public bool TryCreateFireIntent(
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection,
            out InventoryGunFireRequest request,
            out string rejectionCode)
        {
            return TryCreateFireIntent(
                fireOperationId,
                simulationTick,
                deterministicSeed,
                origin,
                aimDirection,
                GunTriggerSignal.Pressed,
                out request,
                out rejectionCode);
        }

        public bool TryCreateFireIntent(
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection,
            GunTriggerSignal triggerSignal,
            out InventoryGunFireRequest request,
            out string rejectionCode)
        {
            request = null;
            if (!HasValidTriggerInput(
                    fireOperationId,
                    simulationTick,
                    origin,
                    aimDirection,
                    triggerSignal))
            {
                rejectionCode = "gun-live-intent-invalid";
                return false;
            }

            GunActorInstanceId actorId;
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

            InventoryGunMountedLive first = mountedGuns[0];
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

        public InventoryGunExecutionResult TryExecute(
            InventoryGunFireRequest request)
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
                InventoryGunExecutionResult drained =
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
        public InventoryGunExecutionResult TryFire(
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection)
        {
            return TryTrigger(
                fireOperationId,
                simulationTick,
                deterministicSeed,
                origin,
                aimDirection,
                GunTriggerSignal.Pressed);
        }

        /// <summary>
        /// Lower-level explicit edge API for a caller that already owns input-edge classification.
        /// Every enabled mount receives the same signal with deterministic per-mount operation IDs.
        /// </summary>
        public InventoryGunExecutionResult TryTrigger(
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection,
            GunTriggerSignal triggerSignal)
        {
            lock (firingStateGate)
            {
                if (!HasValidTriggerInput(
                        fireOperationId,
                        simulationTick,
                        origin,
                        aimDirection,
                        triggerSignal))
                {
                    return Reject("gun-live-trigger-invalid");
                }

                GunActorInstanceId actorId;
                LifecycleGeneration generation;
                string rejectionCode;
                if (!TryResolveAndActivateCurrentLifecycleLocked(
                        out actorId,
                        out generation,
                        out rejectionCode))
                {
                    return Reject(rejectionCode);
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
        public InventoryGunExecutionResult UpdateTriggerInput(
            bool isHeld,
            FireOperationId inputOperationId,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection)
        {
            lock (firingStateGate)
            {
                GunActorInstanceId actorId;
                LifecycleGeneration generation;
                string rejectionCode;
                if (!TryResolveAndActivateCurrentLifecycleLocked(
                        out actorId,
                        out generation,
                        out rejectionCode))
                {
                    return Reject(rejectionCode);
                }

                InventoryGunTriggerEdgeDecision edge = triggerEdgeState.Resolve(
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
                            ? "gun-live-trigger-edge-null-result"
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
        public InventoryGunExecutionResult Advance(long simulationTick)
        {
            lock (firingStateGate)
            {
                return simulationTick < 0L
                    ? Reject("gun-live-advance-invalid")
                    : DrainDueLocked(simulationTick);
            }
        }

        public InventoryGunExecutionResult DrainDueEmissions(
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

        private InventoryGunExecutionResult TriggerAllMountsAndDrainLocked(
            GunActorInstanceId actorId,
            LifecycleGeneration generation,
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection,
            GunTriggerSignal triggerSignal,
            InventoryGunTriggerEdgeState candidateEdgeState,
            bool publishCandidateEdgeWhenSafe)
        {
            var scheduled = new List<MountAttempt>();
            if (!IsConcurrentMountMode)
            {
                InventoryGunFireRequest request;
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
                        activeGunSource == null
                            ? null
                            : activeGunSource.SelectedEquipmentInstanceId,
                        Reject(
                            activeGunSource == null
                                ? null
                                : activeGunSource.SelectedEquipmentInstanceId,
                            GunExecutionStatus.InvalidCommand,
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
                for (int index = 0; index < mountedGuns.Count; index++)
                {
                    InventoryGunMountedLive mount = mountedGuns[index];
                    try
                    {
                        InventoryGunFireRequest request = CreateMountedRequest(
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
                                GunExecutionStatus.InvalidCommand,
                                "gun-live-mounted-request-invalid",
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
                                GunExecutionStatus.InvalidCommand,
                                "gun-live-mounted-request-overflow",
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

            InventoryGunExecutionResult drained =
                DrainDueLocked(simulationTick);
            return CombineScheduledAndDrained(scheduled, drained);
        }

        private MountAttempt ScheduleAndPublishLocked(
            InventoryGunFireRequest request,
            StableId mountStableId,
            int mountOrdinal)
        {
            InventoryGunPendingDeliveryState alignedPending;
            try
            {
                // A receipt may participate in admission only while the current canonical scheduler
                // still retains the accepted-schedule replay that created it.
                alignedPending = pendingDeliveryState
                    .PruneDeliveredReceipts(firingSessionState);
            }
            catch (ArgumentException)
            {
                return new MountAttempt(
                    mountStableId,
                    mountOrdinal,
                    request == null ? null : request.EquipmentInstanceId,
                    Reject(
                        request == null ? null : request.EquipmentInstanceId,
                        GunExecutionStatus.InvalidEffectBatch,
                        "gun-live-pre-admission-receipt-pruning-invalid",
                        false,
                        null),
                    false);
            }
            catch (InvalidOperationException)
            {
                return new MountAttempt(
                    mountStableId,
                    mountOrdinal,
                    request == null ? null : request.EquipmentInstanceId,
                    Reject(
                        request == null ? null : request.EquipmentInstanceId,
                        GunExecutionStatus.InvalidEffectBatch,
                        "gun-live-pre-admission-receipt-pruning-failed",
                        false,
                        null),
                    false);
            }

            InventoryGunExecutionTransition transition =
                executionAdapter.TryExecute(
                    request,
                    firingSessionState,
                    alignedPending);
            if (transition == null)
            {
                return new MountAttempt(
                    mountStableId,
                    mountOrdinal,
                    request == null ? null : request.EquipmentInstanceId,
                    Reject(
                        request == null ? null : request.EquipmentInstanceId,
                        GunExecutionStatus.InvalidCommand,
                        "gun-live-transition-null-result",
                        false,
                        null),
                    false);
            }

            bool statePairPublished = false;
            if (transition.Result.Succeeded)
            {
                InventoryGunPendingDeliveryState prunedPending;
                try
                {
                    // A newly accepted schedule can prune older replay records, so align once more
                    // against the scheduler state that will become authoritative.
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
                            GunExecutionStatus.InvalidEffectBatch,
                            "gun-live-delivered-receipt-pruning-invalid",
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
                            GunExecutionStatus.InvalidEffectBatch,
                            "gun-live-delivered-receipt-pruning-failed",
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

        private InventoryGunExecutionResult DrainDueLocked(
            long simulationTick)
        {
            GunActorInstanceId actorId;
            LifecycleGeneration generation;
            string lifecycleRejection = string.Empty;
            if (simulationTick < 0L
                || !TryResolveAndActivateCurrentLifecycleLocked(
                    out actorId,
                    out generation,
                    out lifecycleRejection))
            {
                return Reject(
                    string.IsNullOrWhiteSpace(lifecycleRejection)
                        ? "gun-live-drain-invalid"
                        : lifecycleRejection);
            }

            var delivered = new List<InventoryGunEffectBatch>();
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
                    return InventoryGunExecutionResult.Reject(
                        firstEquipmentInstanceId,
                        GunExecutionStatus.InvalidCommand,
                        lifecycleRejection,
                        false,
                        false,
                        pendingDeliveryState.PendingCount,
                        delivered,
                        acceptedCount,
                        alreadyAcceptedCount);
                }

                InventoryGunPendingDeliveryEntry entry;
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
                    return InventoryGunExecutionResult.Reject(
                        entry == null ? null : entry.EquipmentInstanceId,
                        GunExecutionStatus.InvalidEffectBatch,
                        "gun-live-pending-lifecycle-mismatch",
                        false,
                        false,
                        pendingDeliveryState.PendingCount,
                        delivered,
                        acceptedCount,
                        alreadyAcceptedCount);
                }

                InventoryGunPendingDeliveryAttempt attempt =
                    executionAdapter.TryDeliverPending(entry);
                if (attempt == null || !attempt.Succeeded)
                {
                    return InventoryGunExecutionResult.Reject(
                        entry.EquipmentInstanceId,
                        GunExecutionStatus.SinkRejected,
                        attempt == null
                            ? "gun-live-retryable-delivery-null-result"
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
                    // Record the downstream acceptance, then immediately discard the receipt when
                    // the canonical scheduler has already pruned the matching replay record.
                    pendingDeliveryState = pendingDeliveryState
                        .MarkDelivered(entry)
                        .PruneDeliveredReceipts(firingSessionState);
                }
                catch
                {
                    // The sink may already have accepted the batch. Retaining the entry is safe because
                    // the next attempt must return exact AlreadyAccepted before removal.
                    return InventoryGunExecutionResult.Reject(
                        entry.EquipmentInstanceId,
                        GunExecutionStatus.SinkRejected,
                        "gun-live-retryable-pending-commit-failed",
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
                return InventoryGunExecutionResult.NoDue(
                    pendingDeliveryState.PendingCount);
            }

            return InventoryGunExecutionResult.Delivery(
                firstEquipmentInstanceId,
                delivered,
                acceptedCount,
                alreadyAcceptedCount,
                pendingDeliveryState.PendingCount);
        }

        private InventoryGunExecutionResult CombineScheduledAndDrained(
            IList<MountAttempt> scheduledAttempts,
            InventoryGunExecutionResult drained)
        {
            MountAttempt firstIntegrationFailure = null;
            MountAttempt firstSchedulerFailure = null;
            InventoryGunExecutionResult firstTransition = null;
            int totalScheduledEmissions = 0;
            bool anyNewSchedule = false;
            bool anyReplaySchedule = false;
            var schedulingOutcomes = new List<InventoryGunSchedulingOutcome>();
            var mountOutcomes = new List<InventoryGunMountExecutionOutcome>();

            for (int index = 0; index < scheduledAttempts.Count; index++)
            {
                MountAttempt attempt = scheduledAttempts[index];
                InventoryGunExecutionResult result = attempt.Result;
                InventoryGunMountExecutionOutcome mountOutcome =
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
                    == InventoryGunExecutionOutcomeKind.AcceptedScheduleQueued)
                {
                    anyNewSchedule = true;
                }
                else if (mountOutcome.OutcomeKind
                    == InventoryGunExecutionOutcomeKind.ReplayedScheduleRetained)
                {
                    anyReplaySchedule = true;
                }

                if (mountOutcome.Source
                    == InventoryGunMountOutcomeSource.IntegrationRejection
                    && firstIntegrationFailure == null)
                {
                    firstIntegrationFailure = attempt;
                }
                else if (mountOutcome.Source
                    == InventoryGunMountOutcomeSource.SchedulerRejection
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

            InventoryGunExecutionResult combined;
            if (drained != null
                && drained.OutcomeKind
                    == InventoryGunExecutionOutcomeKind.RetryableDeliveryFailure)
            {
                combined = drained;
            }
            else if (drained != null && !drained.Succeeded)
            {
                // Lifecycle, membership, disposal, and other non-retryable drain failures must not be
                // hidden by an otherwise successful scheduling result from the same call.
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
                combined = InventoryGunExecutionResult.Schedule(
                    null,
                    !anyNewSchedule && anyReplaySchedule,
                    totalScheduledEmissions,
                    pendingDeliveryState.PendingCount);
            }
            else
            {
                combined = firstTransition
                    ?? drained
                    ?? InventoryGunExecutionResult.NoDue(
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

        private InventoryGunExecutionResult CombineFailureWithDelivery(
            InventoryGunExecutionResult failure,
            bool schedulerRejection,
            InventoryGunExecutionResult drained)
        {
            return InventoryGunExecutionResult.Reject(
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

        private static InventoryGunMountExecutionOutcome CreateMountOutcome(
            MountAttempt attempt)
        {
            InventoryGunExecutionResult result = attempt.Result;
            InventoryGunMountOutcomeSource source = result.Succeeded
                ? InventoryGunMountOutcomeSource.SuccessfulScheduling
                : result.OutcomeKind
                    == InventoryGunExecutionOutcomeKind.SchedulerRejected
                    ? InventoryGunMountOutcomeSource.SchedulerRejection
                    : InventoryGunMountOutcomeSource.IntegrationRejection;
            bool exactReplay = IsExactReplay(result);
            bool retryable = result.OutcomeKind
                    == InventoryGunExecutionOutcomeKind.RetryableDeliveryFailure
                || (!result.Succeeded
                    && result.RejectionCode.IndexOf(
                        "pending-capacity-exceeded",
                        StringComparison.Ordinal) >= 0);
            return new InventoryGunMountExecutionOutcome(
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
            InventoryGunExecutionResult result)
        {
            return result != null
                && (result.IsExactReplay
                    || result.SchedulerStatus
                        == GunFiringScheduleStatus.Replayed);
        }

        private bool ValidateRequestLifecycleLocked(
            InventoryGunFireRequest request,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (disposed)
            {
                rejectionCode = "gun-live-runtime-disposed";
                return false;
            }
            if (request == null)
            {
                rejectionCode = "gun-live-request-invalid";
                return false;
            }

            GunActorInstanceId currentActorId;
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
                rejectionCode = "gun-live-request-lifecycle-mismatch";
                return false;
            }

            ActivateLifecycleLocked(currentActorId, currentLifecycleGeneration);
            return true;
        }

        private bool TryResolveAndActivateCurrentLifecycleLocked(
            out GunActorInstanceId actorId,
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
                    ? "gun-live-runtime-disposed"
                    : "gun-live-actor-state-unresolved";
                return false;
            }

            ActivateLifecycleLocked(actorId, generation);
            rejectionCode = string.Empty;
            return true;
        }

        private void ActivateLifecycleLocked(
            GunActorInstanceId actorId,
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
            firingSessionState = GunFiringSessionState.Empty;
            pendingDeliveryState = InventoryGunPendingDeliveryState.Empty;
            triggerEdgeState = InventoryGunTriggerEdgeState.Empty;
        }

        private static bool HasValidTriggerInput(
            FireOperationId fireOperationId,
            long simulationTick,
            GunVector2 origin,
            GunVector2 aimDirection,
            GunTriggerSignal triggerSignal)
        {
            return fireOperationId != null
                && simulationTick >= 0L
                && origin != null
                && origin.IsFinite
                && aimDirection != null
                && aimDirection.IsFinite
                && aimDirection.LengthSquared > 0.000000000001d
                && Enum.IsDefined(
                    typeof(GunTriggerSignal),
                    triggerSignal);
        }

        private static InventoryGunFireRequest CreateMountedRequest(
            GunActorInstanceId actorId,
            LifecycleGeneration generation,
            InventoryGunMountedLive mount,
            int mountOrdinal,
            FireOperationId baseOperationId,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection,
            GunTriggerSignal triggerSignal)
        {
            if (actorId == null
                || generation == null
                || mount == null
                || mountOrdinal < 0
                || !HasValidTriggerInput(
                    baseOperationId,
                    simulationTick,
                    origin,
                    aimDirection,
                    triggerSignal))
            {
                throw new ArgumentException(
                    "A valid actor, lifecycle, mount, and trigger input are required.");
            }

            double length = Math.Sqrt(
                (aimDirection.X * aimDirection.X)
                + (aimDirection.Y * aimDirection.Y));
            double normalizedX = aimDirection.X / length;
            double normalizedY = aimDirection.Y / length;
            double perpendicularX = -normalizedY;
            double perpendicularY = normalizedX;
            var mountOrigin = new GunVector2(
                origin.X + (perpendicularX * mount.LateralOffset),
                origin.Y + (perpendicularY * mount.LateralOffset));
            string operationFingerprint =
                GunExecutionFingerprint.Compute(
                    baseOperationId
                    + "|"
                    + mount.MountStableId);
            var operationId = new FireOperationId(
                StableId.Create(
                    "fire-operation",
                    operationFingerprint.Substring(
                        GunExecutionFingerprint.Prefix.Length)));
            ulong mountSeed = deterministicSeed
                ^ (unchecked((ulong)(mountOrdinal + 1))
                    * 11400714819323198485UL);

            return new InventoryGunFireRequest(
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

        private InventoryGunExecutionResult Reject(
            string rejectionCode)
        {
            return Reject(
                null,
                GunExecutionStatus.InvalidCommand,
                rejectionCode,
                false,
                null);
        }

        private InventoryGunExecutionResult Reject(
            EquipmentInstanceId equipmentInstanceId,
            GunExecutionStatus status,
            string rejectionCode,
            bool schedulerRejection,
            GunFiringScheduleStatus? schedulerStatus)
        {
            return InventoryGunExecutionResult.Reject(
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

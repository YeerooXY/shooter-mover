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
    /// Legacy active-slot projection retained for callers outside the production mount
    /// path. Production gameplay uses the mounted constructor below and does not switch.
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
    /// One currently enabled physical mount. It carries only position, exact equipment
    /// identity, and the muzzle's lateral offset. Activation policy remains upstream.
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
    /// Composes actor/lifecycle facts with either the retained active-slot source or a set of
    /// concurrently firing physical mounts. This class is the sole owner and publication boundary
    /// for the immutable WeaponFiringSessionState for one runtime/run composition.
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
            if (disposed
                || !actorStateSource.TryResolveActorState(
                    out actorId,
                    out generation)
                || actorId == null
                || generation == null)
            {
                rejectionCode =
                    "weapon-live-actor-state-unresolved";
                return false;
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
            return ExecuteAndPublish(request);
        }

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

        public InventoryWeaponExecutionResult TryTrigger(
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection,
            WeaponTriggerSignal triggerSignal)
        {
            if (!IsConcurrentMountMode)
            {
                InventoryWeaponFireRequest request;
                string rejectionCode;
                if (!TryCreateFireIntent(
                    fireOperationId,
                    simulationTick,
                    deterministicSeed,
                    origin,
                    aimDirection,
                    triggerSignal,
                    out request,
                    out rejectionCode))
                {
                    return Reject(rejectionCode);
                }
                return ExecuteAndPublish(request);
            }

            WeaponActorInstanceId actorId;
            LifecycleGeneration generation;
            if (disposed
                || fireOperationId == null
                || !Enum.IsDefined(
                    typeof(WeaponTriggerSignal),
                    triggerSignal)
                || !actorStateSource.TryResolveActorState(
                    out actorId,
                    out generation)
                || actorId == null
                || generation == null)
            {
                return Reject("weapon-live-actor-state-unresolved");
            }

            InventoryWeaponExecutionResult firstAccepted = null;
            InventoryWeaponExecutionResult firstCooldown = null;
            InventoryWeaponExecutionResult firstFailure = null;
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
                InventoryWeaponExecutionResult result =
                    ExecuteAndPublish(request);
                if (result.Status == WeaponExecutionStatus.Accepted
                    || result.Status
                        == WeaponExecutionStatus.ReplayAccepted)
                {
                    if (firstAccepted == null)
                    {
                        firstAccepted = result;
                    }
                }
                else if (result.Status
                    == WeaponExecutionStatus.CooldownActive)
                {
                    if (firstCooldown == null)
                    {
                        firstCooldown = result;
                    }
                }
                else if (firstFailure == null)
                {
                    firstFailure = result;
                }
            }

            return firstAccepted
                ?? firstCooldown
                ?? firstFailure
                ?? Reject("weapon-live-no-enabled-mounts");
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
                firingSessionState = WeaponFiringSessionState.Empty;
            }
        }

        private InventoryWeaponExecutionResult ExecuteAndPublish(
            InventoryWeaponFireRequest request)
        {
            lock (firingStateGate)
            {
                if (disposed)
                {
                    return Reject("weapon-live-runtime-disposed");
                }

                InventoryWeaponExecutionTransition transition =
                    executionAdapter.TryExecute(
                        request,
                        firingSessionState);
                if (transition.PublishNextState)
                {
                    firingSessionState = transition.NextState;
                }
                return transition.Result;
            }
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

        private static InventoryWeaponExecutionResult Reject(
            string rejectionCode)
        {
            return new InventoryWeaponExecutionResult(
                null,
                WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.InvalidCommand,
                    rejectionCode,
                    0L),
                null);
        }
    }
}

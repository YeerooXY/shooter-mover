using System;
using ShooterMover.Contracts.Flow.Session;
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
    /// Active-slot projection over the real immutable route/loadout payload. The payload
    /// remains loadout truth; this class owns only the current four-slot selection index.
    /// </summary>
    public sealed class RouteProfileActiveWeaponSource : IActiveWeaponEquipmentInstanceSource
    {
        private readonly PlayerRouteProfilePayloadV1 routeProfile;
        private int selectedSlotIndex;

        public RouteProfileActiveWeaponSource(
            PlayerRouteProfilePayloadV1 profile,
            int initialSlotIndex = 0)
        {
            routeProfile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (routeProfile.WeaponSlots == null
                || routeProfile.WeaponSlots.Count != PlayerRouteProfilePayloadV1.WeaponSlotCount)
            {
                throw new ArgumentException(
                    "The route profile must contain exactly four weapon slots.",
                    nameof(profile));
            }

            if (initialSlotIndex < 0
                || initialSlotIndex >= PlayerRouteProfilePayloadV1.WeaponSlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(initialSlotIndex));
            }

            selectedSlotIndex = initialSlotIndex;
        }

        public int SelectedSlotIndex { get { return selectedSlotIndex; } }

        public EquipmentInstanceId SelectedEquipmentInstanceId
        {
            get
            {
                return new EquipmentInstanceId(
                    routeProfile.WeaponSlots[selectedSlotIndex].EquipmentInstanceStableId);
            }
        }

        public InventoryWeaponSlotSelectionStatus SelectSlot(int slotIndex)
        {
            if (slotIndex < 0
                || slotIndex >= PlayerRouteProfilePayloadV1.WeaponSlotCount)
            {
                return InventoryWeaponSlotSelectionStatus.InvalidSlot;
            }

            if (slotIndex == selectedSlotIndex)
            {
                return InventoryWeaponSlotSelectionStatus.ExactDuplicateNoChange;
            }

            selectedSlotIndex = slotIndex;
            return InventoryWeaponSlotSelectionStatus.Selected;
        }

        public bool TryResolveActiveEquipmentInstance(
            WeaponActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration,
            out EquipmentInstanceId equipmentInstanceId)
        {
            equipmentInstanceId = actorId == null || lifecycleGeneration == null
                ? null
                : SelectedEquipmentInstanceId;
            return equipmentInstanceId != null;
        }
    }

    /// <summary>
    /// Production-facing composition of actor/lifecycle facts, active route slot,
    /// immutable fire-intent locking, and the inventory-backed execution adapter.
    /// </summary>
    public sealed class InventoryWeaponRuntimeComposition
    {
        private readonly IInventoryWeaponActorStateSource actorStateSource;
        private readonly InventoryWeaponFireIntentFactory intentFactory;
        private readonly InventoryBackedWeaponExecutionAdapter executionAdapter;
        private readonly RouteProfileActiveWeaponSource activeWeaponSource;

        public InventoryWeaponRuntimeComposition(
            IInventoryWeaponActorStateSource actorState,
            RouteProfileActiveWeaponSource activeWeapon,
            InventoryBackedWeaponExecutionAdapter adapter)
        {
            actorStateSource = actorState ?? throw new ArgumentNullException(nameof(actorState));
            activeWeaponSource = activeWeapon ?? throw new ArgumentNullException(nameof(activeWeapon));
            executionAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            intentFactory = new InventoryWeaponFireIntentFactory(activeWeaponSource);
        }

        public int SelectedSlotIndex { get { return activeWeaponSource.SelectedSlotIndex; } }

        public InventoryWeaponSlotSelectionStatus SelectSlot(int slotIndex)
        {
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
            request = null;
            WeaponActorInstanceId actorId;
            LifecycleGeneration generation;
            if (!actorStateSource.TryResolveActorState(out actorId, out generation)
                || actorId == null
                || generation == null)
            {
                rejectionCode = "weapon-live-actor-state-unresolved";
                return false;
            }

            return intentFactory.TryCreate(
                actorId,
                fireOperationId,
                generation,
                simulationTick,
                deterministicSeed,
                origin,
                aimDirection,
                out request,
                out rejectionCode);
        }

        public InventoryWeaponExecutionResult TryExecute(InventoryWeaponFireRequest request)
        {
            return executionAdapter.TryExecute(request);
        }

        public InventoryWeaponExecutionResult TryFire(
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection)
        {
            InventoryWeaponFireRequest request;
            string rejectionCode;
            if (!TryCreateFireIntent(
                    fireOperationId,
                    simulationTick,
                    deterministicSeed,
                    origin,
                    aimDirection,
                    out request,
                    out rejectionCode))
            {
                return new InventoryWeaponExecutionResult(
                    null,
                    ShooterMover.Application.Weapons.Execution.WeaponExecutionResult.Reject(
                        ShooterMover.Application.Weapons.Execution.WeaponExecutionStatus.InvalidCommand,
                        rejectionCode,
                        0L),
                    null);
            }

            return executionAdapter.TryExecute(request);
        }
    }
}

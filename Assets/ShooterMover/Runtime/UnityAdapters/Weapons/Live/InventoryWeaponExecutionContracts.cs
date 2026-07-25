using System;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    public interface IActiveWeaponEquipmentInstanceSource
    {
        bool TryResolveActiveEquipmentInstance(
            WeaponActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration,
            out EquipmentInstanceId equipmentInstanceId);
    }

    public interface IInventoryWeaponActorStateSource
    {
        bool TryResolveActorState(
            out WeaponActorInstanceId actorId,
            out LifecycleGeneration lifecycleGeneration);
    }

    public interface IPlayerEquipmentInstanceLookup
    {
        bool TryResolve(
            EquipmentInstanceId equipmentInstanceId,
            out EquipmentInstance equipmentInstance);
    }

    public interface IInventoryWeaponEffectBatchSink
    {
        WeaponEffectBatchSinkResult TryAccept(InventoryWeaponEffectBatch batch);
    }

    public sealed class InventoryWeaponFireRequest
    {
        public InventoryWeaponFireRequest(
            WeaponActorInstanceId actorId,
            EquipmentInstanceId equipmentInstanceId,
            FireOperationId fireOperationId,
            LifecycleGeneration lifecycleGeneration,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection,
            WeaponTriggerSignal triggerSignal = WeaponTriggerSignal.Pressed)
        {
            if (!Enum.IsDefined(typeof(WeaponTriggerSignal), triggerSignal))
            {
                throw new ArgumentOutOfRangeException(nameof(triggerSignal));
            }

            ActorId = actorId;
            EquipmentInstanceId = equipmentInstanceId;
            FireOperationId = fireOperationId;
            LifecycleGeneration = lifecycleGeneration;
            SimulationTick = simulationTick;
            DeterministicSeed = deterministicSeed;
            Origin = origin;
            AimDirection = aimDirection;
            TriggerSignal = triggerSignal;
        }

        public WeaponActorInstanceId ActorId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public FireOperationId FireOperationId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }
        public long SimulationTick { get; }
        public ulong DeterministicSeed { get; }
        public WeaponVector2 Origin { get; }
        public WeaponVector2 AimDirection { get; }
        public WeaponTriggerSignal TriggerSignal { get; }
    }

    public sealed class InventoryWeaponFireIntentFactory
    {
        private readonly IActiveWeaponEquipmentInstanceSource activeEquipmentSource;

        public InventoryWeaponFireIntentFactory(
            IActiveWeaponEquipmentInstanceSource activeEquipment)
        {
            activeEquipmentSource = activeEquipment
                ?? throw new ArgumentNullException(nameof(activeEquipment));
        }

        [Obsolete(
            "One-shot Pressed compatibility only. Live held input must supply an explicit trigger signal.",
            false)]
        public bool TryCreate(
            WeaponActorInstanceId actorId,
            FireOperationId fireOperationId,
            LifecycleGeneration lifecycleGeneration,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection,
            out InventoryWeaponFireRequest request,
            out string rejectionCode)
        {
            return TryCreate(
                actorId,
                fireOperationId,
                lifecycleGeneration,
                simulationTick,
                deterministicSeed,
                origin,
                aimDirection,
                WeaponTriggerSignal.Pressed,
                out request,
                out rejectionCode);
        }

        public bool TryCreate(
            WeaponActorInstanceId actorId,
            FireOperationId fireOperationId,
            LifecycleGeneration lifecycleGeneration,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection,
            WeaponTriggerSignal triggerSignal,
            out InventoryWeaponFireRequest request,
            out string rejectionCode)
        {
            request = null;
            if (actorId == null
                || fireOperationId == null
                || lifecycleGeneration == null
                || !Enum.IsDefined(typeof(WeaponTriggerSignal), triggerSignal))
            {
                rejectionCode = "weapon-live-intent-invalid";
                return false;
            }

            EquipmentInstanceId equipmentInstanceId;
            if (!activeEquipmentSource.TryResolveActiveEquipmentInstance(
                    actorId,
                    lifecycleGeneration,
                    out equipmentInstanceId)
                || equipmentInstanceId == null)
            {
                rejectionCode = "weapon-live-active-equipment-unresolved";
                return false;
            }

            request = new InventoryWeaponFireRequest(
                actorId,
                equipmentInstanceId,
                fireOperationId,
                lifecycleGeneration,
                simulationTick,
                deterministicSeed,
                origin,
                aimDirection,
                triggerSignal);
            rejectionCode = string.Empty;
            return true;
        }
    }

    public sealed class InventoryWeaponExecutionTransition
    {
        public InventoryWeaponExecutionTransition(
            InventoryWeaponExecutionResult result,
            WeaponFiringSessionState nextFiringState,
            InventoryWeaponPendingDeliveryState nextPendingState,
            bool publishStatePair)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            NextFiringState = nextFiringState
                ?? throw new ArgumentNullException(nameof(nextFiringState));
            NextPendingState = nextPendingState
                ?? throw new ArgumentNullException(nameof(nextPendingState));
            PublishStatePair = publishStatePair;
        }

        [Obsolete(
            "Live state publication requires both scheduler and pending-delivery state.",
            false)]
        public InventoryWeaponExecutionTransition(
            InventoryWeaponExecutionResult result,
            WeaponFiringSessionState nextState,
            bool publishNextState)
            : this(
                result,
                nextState,
                InventoryWeaponPendingDeliveryState.Empty,
                publishNextState)
        {
        }

        public InventoryWeaponExecutionResult Result { get; }
        public WeaponFiringSessionState NextFiringState { get; }
        public InventoryWeaponPendingDeliveryState NextPendingState { get; }
        public bool PublishStatePair { get; }

        public WeaponFiringSessionState NextState
        {
            get { return NextFiringState; }
        }

        public bool PublishNextState
        {
            get { return PublishStatePair; }
        }
    }
}

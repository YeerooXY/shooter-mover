using System;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    public interface IActiveGunItemSource
    {
        bool TryResolveActiveEquipmentInstance(
            GunActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration,
            out EquipmentInstanceId equipmentInstanceId);
    }

    public interface IInventoryGunActorStateSource
    {
        bool TryResolveActorState(
            out GunActorInstanceId actorId,
            out LifecycleGeneration lifecycleGeneration);
    }

    public interface IPlayerEquipmentInstanceLookup
    {
        bool TryResolve(
            EquipmentInstanceId equipmentInstanceId,
            out EquipmentInstance equipmentInstance);
    }

    public interface IInventoryGunEffectBatchSink
    {
        GunEffectBatchSinkResult TryAccept(InventoryGunEffectBatch batch);
    }

    public sealed class InventoryGunFireRequest
    {
        public InventoryGunFireRequest(
            GunActorInstanceId actorId,
            EquipmentInstanceId equipmentInstanceId,
            FireOperationId fireOperationId,
            LifecycleGeneration lifecycleGeneration,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection,
            GunTriggerSignal triggerSignal = GunTriggerSignal.Pressed)
        {
            if (!Enum.IsDefined(typeof(GunTriggerSignal), triggerSignal))
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

        public GunActorInstanceId ActorId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public FireOperationId FireOperationId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }
        public long SimulationTick { get; }
        public ulong DeterministicSeed { get; }
        public GunVector2 Origin { get; }
        public GunVector2 AimDirection { get; }
        public GunTriggerSignal TriggerSignal { get; }
    }

    public sealed class InventoryGunFireIntentFactory
    {
        private readonly IActiveGunItemSource activeEquipmentSource;

        public InventoryGunFireIntentFactory(
            IActiveGunItemSource activeEquipment)
        {
            activeEquipmentSource = activeEquipment
                ?? throw new ArgumentNullException(nameof(activeEquipment));
        }

        [Obsolete(
            "One-shot Pressed compatibility only. Live held input must supply an explicit trigger signal.",
            false)]
        public bool TryCreate(
            GunActorInstanceId actorId,
            FireOperationId fireOperationId,
            LifecycleGeneration lifecycleGeneration,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection,
            out InventoryGunFireRequest request,
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
                GunTriggerSignal.Pressed,
                out request,
                out rejectionCode);
        }

        public bool TryCreate(
            GunActorInstanceId actorId,
            FireOperationId fireOperationId,
            LifecycleGeneration lifecycleGeneration,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection,
            GunTriggerSignal triggerSignal,
            out InventoryGunFireRequest request,
            out string rejectionCode)
        {
            request = null;
            if (actorId == null
                || fireOperationId == null
                || lifecycleGeneration == null
                || !Enum.IsDefined(typeof(GunTriggerSignal), triggerSignal))
            {
                rejectionCode = "gun-live-intent-invalid";
                return false;
            }

            EquipmentInstanceId equipmentInstanceId;
            if (!activeEquipmentSource.TryResolveActiveEquipmentInstance(
                    actorId,
                    lifecycleGeneration,
                    out equipmentInstanceId)
                || equipmentInstanceId == null)
            {
                rejectionCode = "gun-live-active-equipment-unresolved";
                return false;
            }

            request = new InventoryGunFireRequest(
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

    public sealed class InventoryGunExecutionTransition
    {
        public InventoryGunExecutionTransition(
            InventoryGunExecutionResult result,
            GunFiringSessionState nextFiringState,
            InventoryGunPendingDeliveryState nextPendingState,
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
        public InventoryGunExecutionTransition(
            InventoryGunExecutionResult result,
            GunFiringSessionState nextState,
            bool publishNextState)
            : this(
                result,
                nextState,
                InventoryGunPendingDeliveryState.Empty,
                publishNextState)
        {
        }

        public InventoryGunExecutionResult Result { get; }
        public GunFiringSessionState NextFiringState { get; }
        public InventoryGunPendingDeliveryState NextPendingState { get; }
        public bool PublishStatePair { get; }

        public GunFiringSessionState NextState
        {
            get { return NextFiringState; }
        }

        public bool PublishNextState
        {
            get { return PublishStatePair; }
        }
    }
}

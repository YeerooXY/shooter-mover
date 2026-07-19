using System;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
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
            WeaponVector2 aimDirection)
        {
            ActorId = actorId;
            EquipmentInstanceId = equipmentInstanceId;
            FireOperationId = fireOperationId;
            LifecycleGeneration = lifecycleGeneration;
            SimulationTick = simulationTick;
            DeterministicSeed = deterministicSeed;
            Origin = origin;
            AimDirection = aimDirection;
        }

        public WeaponActorInstanceId ActorId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public FireOperationId FireOperationId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }
        public long SimulationTick { get; }
        public ulong DeterministicSeed { get; }
        public WeaponVector2 Origin { get; }
        public WeaponVector2 AimDirection { get; }
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
            request = null;
            if (actorId == null
                || fireOperationId == null
                || lifecycleGeneration == null)
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
                aimDirection);
            rejectionCode = string.Empty;
            return true;
        }
}
}

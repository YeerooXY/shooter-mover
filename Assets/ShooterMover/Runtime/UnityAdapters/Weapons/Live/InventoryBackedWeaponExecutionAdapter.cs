using System;
using System.Collections.Generic;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Converts one immutable inventory-backed fire intent into WPN-CORE-002 execution.
    /// Equipment is already locked into the request and is never re-resolved from the
    /// currently selected slot during a retry.
    /// </summary>
    public sealed class InventoryBackedWeaponExecutionAdapter : IEquippedWeaponInstanceResolver
    {
        private readonly IPlayerEquipmentInstanceLookup equipmentLookup;
        private readonly RecordingEffectSink effectSink;
        private readonly WeaponExecutionCore executionCore;

        public InventoryBackedWeaponExecutionAdapter(
            IPlayerEquipmentInstanceLookup lookup,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            IWeaponActorOwnershipResolver ownershipResolver,
            IInventoryWeaponEffectBatchSink downstreamEffectSink,
            int simulationTicksPerSecond,
            IWeaponBehaviorSelector behaviorSelector = null,
            WeaponBehaviorRegistry behaviorRegistry = null)
        {
            if (simulationTicksPerSecond < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationTicksPerSecond));
            }

            equipmentLookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            WeaponCatalog catalog = weaponCatalog ?? throw new ArgumentNullException(nameof(weaponCatalog));
            effectSink = new RecordingEffectSink(
                catalog,
                downstreamEffectSink ?? throw new ArgumentNullException(nameof(downstreamEffectSink)),
                simulationTicksPerSecond);

            var profileResolver = new WeaponCatalogRuntimeProfileResolver(
                equipmentCatalog ?? throw new ArgumentNullException(nameof(equipmentCatalog)),
                catalog,
                behaviorSelector ?? new DefaultWeaponBehaviorSelector(),
                simulationTicksPerSecond);
            executionCore = new WeaponExecutionCore(
                ownershipResolver ?? throw new ArgumentNullException(nameof(ownershipResolver)),
                this,
                profileResolver,
                behaviorRegistry ?? WeaponBehaviorRegistry.CreateWithBuiltIns(),
                effectSink);
        }

        public InventoryBackedWeaponExecutionAdapter(
            IPlayerHoldingsAuthorityV1 holdings,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            IWeaponActorOwnershipResolver ownershipResolver,
            IInventoryWeaponEffectBatchSink downstreamEffectSink,
            int simulationTicksPerSecond,
            IWeaponBehaviorSelector behaviorSelector = null,
            WeaponBehaviorRegistry behaviorRegistry = null)
            : this(
                new PlayerHoldingsEquipmentInstanceLookup(
                    holdings ?? throw new ArgumentNullException(nameof(holdings))),
                equipmentCatalog,
                weaponCatalog,
                ownershipResolver,
                downstreamEffectSink,
                simulationTicksPerSecond,
                behaviorSelector,
                behaviorRegistry)
        {
        }

        public InventoryWeaponExecutionResult TryExecute(InventoryWeaponFireRequest request)
        {
            if (request == null
                || request.ActorId == null
                || request.EquipmentInstanceId == null
                || request.FireOperationId == null
                || request.LifecycleGeneration == null)
            {
                return Reject(
                    request == null ? null : request.EquipmentInstanceId,
                    WeaponExecutionStatus.InvalidCommand,
                    "weapon-live-request-invalid");
            }

            var command = new WeaponFireCommand(
                request.ActorId,
                request.EquipmentInstanceId,
                request.FireOperationId,
                request.LifecycleGeneration,
                request.SimulationTick,
                request.DeterministicSeed,
                request.Origin,
                request.AimDirection);
            WeaponExecutionResult execution = executionCore.TryExecute(command);

            InventoryWeaponEffectBatch batch = null;
            if (execution.Status == WeaponExecutionStatus.Accepted
                || execution.Status == WeaponExecutionStatus.ReplayAccepted)
            {
                effectSink.TryGetAccepted(command, out batch);
            }

            return new InventoryWeaponExecutionResult(
                request.EquipmentInstanceId,
                execution,
                batch);
        }

        public bool TryResolveEquippedWeapon(
            WeaponActorInstanceId actorId,
            EquipmentInstanceId requestedEquipmentInstanceId,
            out EquipmentInstance equipmentInstance)
        {
            equipmentInstance = null;
            return actorId != null
                && requestedEquipmentInstanceId != null
                && equipmentLookup.TryResolve(
                    requestedEquipmentInstanceId,
                    out equipmentInstance)
                && equipmentInstance != null
                && equipmentInstance.InstanceId == requestedEquipmentInstanceId.Value;
        }

        private static InventoryWeaponExecutionResult Reject(
            EquipmentInstanceId equipmentInstanceId,
            WeaponExecutionStatus status,
            string rejectionCode)
        {
            return new InventoryWeaponExecutionResult(
                equipmentInstanceId,
                WeaponExecutionResult.Reject(status, rejectionCode, 0L),
                null);
        }

        private sealed class RecordingEffectSink : IWeaponEffectBatchSink
        {
            private readonly object gate = new object();
            private readonly WeaponCatalog weaponCatalog;
            private readonly IInventoryWeaponEffectBatchSink downstream;
            private readonly int ticksPerSecond;
            private readonly Dictionary<string, InventoryWeaponEffectBatch> acceptedBatches =
                new Dictionary<string, InventoryWeaponEffectBatch>(StringComparer.Ordinal);

            public RecordingEffectSink(
                WeaponCatalog catalog,
                IInventoryWeaponEffectBatchSink downstreamSink,
                int simulationTicksPerSecond)
            {
                weaponCatalog = catalog;
                downstream = downstreamSink;
                ticksPerSecond = simulationTicksPerSecond;
            }

            public WeaponEffectBatchSinkResult TryAccept(WeaponEffectBatch coreBatch)
            {
                if (coreBatch == null || coreBatch.Identity == null)
                {
                    return WeaponEffectBatchSinkResult.Reject("weapon-live-effect-batch-invalid");
                }

                WeaponDefinitionData definition;
                string definitionId = coreBatch.Identity.WeaponDefinitionId.Value;
                if (!weaponCatalog.TryGetDefinition(definitionId, out definition)
                    || definition == null)
                {
                    return WeaponEffectBatchSinkResult.Reject(
                        "weapon-live-definition-unresolved:" + definitionId);
                }

                var projected = new InventoryWeaponEffectBatch(
                    coreBatch,
                    InventoryWeaponEffectProfile.From(definition, ticksPerSecond));
                WeaponEffectBatchSinkResult result = downstream.TryAccept(projected);
                if (result != null && result.IsAcceptance)
                {
                    lock (gate)
                    {
                        acceptedBatches[Key(coreBatch.Identity)] = projected;
                    }
                }

                return result;
            }

            public bool TryGetAccepted(
                WeaponFireCommand command,
                out InventoryWeaponEffectBatch batch)
            {
                if (command == null)
                {
                    batch = null;
                    return false;
                }

                lock (gate)
                {
                    return acceptedBatches.TryGetValue(Key(command), out batch);
                }
            }

            private static string Key(WeaponEffectIdentity identity)
            {
                return identity.ActorId + "|"
                    + identity.LifecycleGeneration + "|"
                    + identity.FireOperationId;
            }

            private static string Key(WeaponFireCommand command)
            {
                return command.ActorId + "|"
                    + command.LifecycleGeneration + "|"
                    + command.FireOperationId;
            }
        }
    }
}

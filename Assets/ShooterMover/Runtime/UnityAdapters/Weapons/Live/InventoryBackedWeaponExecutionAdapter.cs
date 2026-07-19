using System;
using System.Collections.Generic;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    public sealed class InventoryBackedWeaponExecutionAdapter : IEquippedWeaponInstanceResolver
    {
        private readonly IPlayerHoldingsAuthorityV1 holdingsAuthority;
        private readonly IActiveWeaponEquipmentInstanceSource activeEquipmentSource;
        private readonly CatalogProjectingEffectSink effectSink;
        private readonly WeaponExecutionCore executionCore;

        public InventoryBackedWeaponExecutionAdapter(
            IPlayerHoldingsAuthorityV1 holdings,
            IActiveWeaponEquipmentInstanceSource activeEquipment,
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

            holdingsAuthority = holdings ?? throw new ArgumentNullException(nameof(holdings));
            activeEquipmentSource = activeEquipment
                ?? throw new ArgumentNullException(nameof(activeEquipment));
            WeaponCatalog originalCatalog = weaponCatalog
                ?? throw new ArgumentNullException(nameof(weaponCatalog));
            effectSink = new CatalogProjectingEffectSink(
                originalCatalog,
                downstreamEffectSink ?? throw new ArgumentNullException(nameof(downstreamEffectSink)),
                simulationTicksPerSecond);

            WeaponCatalogRuntimeProfileResolver profileResolver =
                new WeaponCatalogRuntimeProfileResolver(
                    equipmentCatalog ?? throw new ArgumentNullException(nameof(equipmentCatalog)),
                    WeaponCatalogExecutionProjection.Create(originalCatalog),
                    behaviorSelector ?? new DefaultWeaponBehaviorSelector(),
                    simulationTicksPerSecond);

            executionCore = new WeaponExecutionCore(
                ownershipResolver ?? throw new ArgumentNullException(nameof(ownershipResolver)),
                this,
                profileResolver,
                behaviorRegistry ?? WeaponBehaviorRegistry.CreateWithBuiltIns(),
                effectSink);
        }

        public InventoryWeaponExecutionResult TryExecute(InventoryWeaponFireRequest request)
        {
            if (request == null
                || request.ActorId == null
                || request.FireOperationId == null
                || request.LifecycleGeneration == null)
            {
                return Reject(
                    null,
                    WeaponExecutionStatus.InvalidCommand,
                    "weapon-live-request-invalid");
            }

            EquipmentInstanceId activeEquipmentId;
            if (!activeEquipmentSource.TryResolveActiveEquipmentInstance(
                    request.ActorId,
                    request.LifecycleGeneration,
                    out activeEquipmentId)
                || activeEquipmentId == null)
            {
                return Reject(
                    null,
                    WeaponExecutionStatus.MissingEquippedEquipment,
                    "weapon-live-active-equipment-unresolved");
            }

            WeaponFireCommand command = new WeaponFireCommand(
                request.ActorId,
                activeEquipmentId,
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

            return new InventoryWeaponExecutionResult(activeEquipmentId, execution, batch);
        }

        public bool TryResolveEquippedWeapon(
            WeaponActorInstanceId actorId,
            EquipmentInstanceId requestedEquipmentInstanceId,
            out EquipmentInstance equipmentInstance)
        {
            equipmentInstance = null;
            if (actorId == null || requestedEquipmentInstanceId == null)
            {
                return false;
            }

            PlayerHoldingsSnapshotV1 snapshot;
            try
            {
                snapshot = holdingsAuthority.ExportSnapshot();
            }
            catch
            {
                return false;
            }

            if (snapshot == null)
            {
                return false;
            }

            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                var holding = snapshot.UniqueHoldings[index];
                if (holding == null
                    || holding.RewardKind != RewardGrantKindV1.EquipmentReference
                    || holding.InstanceStableId != requestedEquipmentInstanceId.Value
                    || holding.EquipmentInstance == null
                    || holding.EquipmentInstance.InstanceId != requestedEquipmentInstanceId.Value)
                {
                    continue;
                }

                equipmentInstance = holding.EquipmentInstance;
                return true;
            }

            return false;
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

        private sealed class CatalogProjectingEffectSink : IWeaponEffectBatchSink
        {
            private readonly object gate = new object();
            private readonly WeaponCatalog weaponCatalog;
            private readonly IInventoryWeaponEffectBatchSink downstream;
            private readonly int ticksPerSecond;
            private readonly Dictionary<string, InventoryWeaponEffectBatch> acceptedBatches =
                new Dictionary<string, InventoryWeaponEffectBatch>(StringComparer.Ordinal);

            public CatalogProjectingEffectSink(
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
                if (!weaponCatalog.TryGetDefinition(definitionId, out definition) || definition == null)
                {
                    return WeaponEffectBatchSinkResult.Reject(
                        "weapon-live-definition-unresolved:" + definitionId);
                }

                InventoryWeaponEffectBatch projected = new InventoryWeaponEffectBatch(
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
                    + identity.EquipmentInstanceId + "|"
                    + identity.FireOperationId + "|"
                    + identity.LifecycleGeneration;
            }

            private static string Key(WeaponFireCommand command)
            {
                return command.ActorId + "|"
                    + command.EquipmentInstanceId + "|"
                    + command.FireOperationId + "|"
                    + command.LifecycleGeneration;
            }
        }
    }
}

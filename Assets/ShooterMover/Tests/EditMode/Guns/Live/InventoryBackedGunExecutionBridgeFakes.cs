using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Guns.Live;

namespace ShooterMover.Tests.EditMode.Guns.Live
{
    public sealed partial class InventoryBackedGunExecutionBridgeTests
    {
        private sealed class Harness
        {
            public Harness(
                InventoryBackedGunExecutionBridge adapter,
                RecordingSink sink)
            {
                Adapter = adapter;
                Sink = sink;
            }

            public InventoryBackedGunExecutionBridge Adapter { get; }
            public RecordingSink Sink { get; }
        }

        private sealed class InMemoryEquipmentLookup : IPlayerEquipmentInstanceLookup
        {
            private readonly Dictionary<StableId, EquipmentInstance> equipment =
                new Dictionary<StableId, EquipmentInstance>();

            public InMemoryEquipmentLookup(IEnumerable<EquipmentInstance> values)
            {
                foreach (EquipmentInstance value in values)
                {
                    equipment[value.InstanceId] = value;
                }
            }

            public bool TryResolve(
                EquipmentInstanceId equipmentInstanceId,
                out EquipmentInstance equipmentInstance)
            {
                if (equipmentInstanceId == null)
                {
                    equipmentInstance = null;
                    return false;
                }

                return equipment.TryGetValue(
                    equipmentInstanceId.Value,
                    out equipmentInstance);
            }
        }

        private sealed class RecordingSink : IInventoryGunEffectBatchSink
        {
            public List<InventoryGunEffectBatch> Batches { get; } =
                new List<InventoryGunEffectBatch>();

            public GunEffectBatchSinkResult TryAccept(
                InventoryGunEffectBatch batch)
            {
                Batches.Add(batch);
                return GunEffectBatchSinkResult.Accept();
            }
        }

        private sealed class FixedActorSource :
            IInventoryGunActorStateSource,
            IGunActorOwnershipResolver
        {
            public bool TryResolveActorState(
                out GunActorInstanceId actorId,
                out LifecycleGeneration lifecycleGeneration)
            {
                actorId = new GunActorInstanceId(ActorId);
                lifecycleGeneration = new LifecycleGeneration(0L);
                return true;
            }

            public bool TryResolveParticipant(
                GunActorInstanceId actorId,
                LifecycleGeneration lifecycleGeneration,
                out RunParticipantId participantId)
            {
                participantId = actorId != null
                    && actorId.Value == ActorId
                    && lifecycleGeneration != null
                    && lifecycleGeneration.Value == 0L
                    ? new RunParticipantId(ParticipantId)
                    : null;
                return participantId != null;
            }
        }

        private sealed class MutableActiveGunSource : IActiveGunItemSource
        {
            private EquipmentInstance current;

            public MutableActiveGunSource(EquipmentInstance initial)
            {
                current = initial;
            }

            public void Set(EquipmentInstance equipment)
            {
                current = equipment;
            }

            public bool TryResolveActiveEquipmentInstance(
                GunActorInstanceId actorId,
                LifecycleGeneration lifecycleGeneration,
                out EquipmentInstanceId equipmentInstanceId)
            {
                equipmentInstanceId = actorId == null
                    || lifecycleGeneration == null
                    || current == null
                    ? null
                    : new EquipmentInstanceId(current.InstanceId);
                return equipmentInstanceId != null;
            }
        }

        private sealed class AcceptingEquipmentValidator : IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    request != null && request.Instance != null,
                    "catalog-test",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    new EquipmentModelIssue[0]);
            }
        }

        private sealed class CountingHoldingsState : IPlayerHoldingsState
        {
            private readonly IPlayerHoldingsState inner;

            public CountingHoldingsState(IPlayerHoldingsState authority)
            {
                inner = authority;
            }

            public int ExportCount { get; private set; }
            public StableId AuthorityStableId { get { return inner.AuthorityStableId; } }
            public long Sequence { get { return inner.Sequence; } }

            public PlayerHoldingsMutationResult Apply(PlayerHoldingsCommand command)
            {
                return inner.Apply(command);
            }

            public PlayerHoldingsSnapshot ExportSnapshot()
            {
                ExportCount++;
                return inner.ExportSnapshot();
            }

            public PlayerHoldingsImportResult ImportSnapshot(
                PlayerHoldingsSnapshot snapshot)
            {
                return inner.ImportSnapshot(snapshot);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;

namespace ShooterMover.Tests.EditMode.Weapons.Live
{
    public sealed partial class InventoryBackedWeaponExecutionAdapterTests
    {
        private sealed class Harness
        {
            public Harness(
                InventoryBackedWeaponExecutionAdapter adapter,
                RecordingSink sink)
            {
                Adapter = adapter;
                Sink = sink;
            }

            public InventoryBackedWeaponExecutionAdapter Adapter { get; }
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

        private sealed class RecordingSink : IInventoryWeaponEffectBatchSink
        {
            public List<InventoryWeaponEffectBatch> Batches { get; } =
                new List<InventoryWeaponEffectBatch>();

            public WeaponEffectBatchSinkResult TryAccept(
                InventoryWeaponEffectBatch batch)
            {
                Batches.Add(batch);
                return WeaponEffectBatchSinkResult.Accept();
            }
        }

        private sealed class FixedActorSource :
            IInventoryWeaponActorStateSource,
            IWeaponActorOwnershipResolver
        {
            public bool TryResolveActorState(
                out WeaponActorInstanceId actorId,
                out LifecycleGeneration lifecycleGeneration)
            {
                actorId = new WeaponActorInstanceId(ActorId);
                lifecycleGeneration = new LifecycleGeneration(0L);
                return true;
            }

            public bool TryResolveParticipant(
                WeaponActorInstanceId actorId,
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

        private sealed class MutableActiveWeaponSource : IActiveWeaponEquipmentInstanceSource
        {
            private EquipmentInstance current;

            public MutableActiveWeaponSource(EquipmentInstance initial)
            {
                current = initial;
            }

            public void Set(EquipmentInstance equipment)
            {
                current = equipment;
            }

            public bool TryResolveActiveEquipmentInstance(
                WeaponActorInstanceId actorId,
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

        private sealed class CountingHoldingsAuthority : IPlayerHoldingsAuthorityV1
        {
            private readonly IPlayerHoldingsAuthorityV1 inner;

            public CountingHoldingsAuthority(IPlayerHoldingsAuthorityV1 authority)
            {
                inner = authority;
            }

            public int ExportCount { get; private set; }
            public StableId AuthorityStableId { get { return inner.AuthorityStableId; } }
            public long Sequence { get { return inner.Sequence; } }

            public PlayerHoldingsMutationResultV1 Apply(PlayerHoldingsCommandV1 command)
            {
                return inner.Apply(command);
            }

            public PlayerHoldingsSnapshotV1 ExportSnapshot()
            {
                ExportCount++;
                return inner.ExportSnapshot();
            }

            public PlayerHoldingsImportResultV1 ImportSnapshot(
                PlayerHoldingsSnapshotV1 snapshot)
            {
                return inner.ImportSnapshot(snapshot);
            }
        }
    }
}

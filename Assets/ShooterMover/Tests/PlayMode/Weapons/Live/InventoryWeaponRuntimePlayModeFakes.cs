using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Weapons.Live
{
    public sealed partial class InventoryWeaponRuntimePlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly GameObject emitterObject;

            public Fixture(
                GameObject gameObject,
                InventoryWeaponEffectEmitter2D emitter,
                InventoryWeaponRuntimeComposition runtime)
            {
                emitterObject = gameObject;
                Emitter = emitter;
                Runtime = runtime;
            }

            public InventoryWeaponEffectEmitter2D Emitter { get; }
            public InventoryWeaponRuntimeComposition Runtime { get; }

            public void Dispose()
            {
                InventoryWeaponPersistentDamageArea2D[] pools =
                    UnityEngine.Object.FindObjectsByType<InventoryWeaponPersistentDamageArea2D>(
                        FindObjectsSortMode.None);
                for (int index = 0; index < pools.Length; index++)
                {
                    if (pools[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(pools[index].gameObject);
                    }
                }

                if (emitterObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(emitterObject);
                }
            }
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
    }
}
